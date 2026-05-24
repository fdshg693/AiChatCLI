from __future__ import annotations

import json
import os
import socket
import subprocess
import sys
import time
from pathlib import Path
from typing import Any

from cli_session.protocol import ManagerEndpoint, endpoint_file, json_line, resolve_state_dir


class ClientError(RuntimeError):
    pass


class ManagerClient:
    def __init__(self, state_dir: Path | None = None) -> None:
        self.state_dir = resolve_state_dir(state_dir)
        self.endpoint_path = endpoint_file(self.state_dir)

    def request(self, payload: dict[str, Any], *, start_manager: bool = True) -> dict[str, Any]:
        endpoint = self._get_endpoint(start_manager=start_manager)
        payload = dict(payload)
        payload["token"] = endpoint.token
        try:
            return self._send(endpoint, payload)
        except OSError:
            if not start_manager:
                raise

            self._remove_endpoint_file()
            endpoint = self._start_manager()
            payload["token"] = endpoint.token
            return self._send(endpoint, payload)

    def wait_until_stopped(self, timeout_seconds: float) -> bool:
        deadline = time.monotonic() + max(0.0, timeout_seconds)
        while time.monotonic() < deadline:
            endpoint = self._read_endpoint()
            if endpoint is None or not self._can_ping(endpoint):
                return True

            time.sleep(0.05)

        return False

    def _get_endpoint(self, *, start_manager: bool) -> ManagerEndpoint:
        endpoint = self._read_endpoint()
        if endpoint is not None and self._can_ping(endpoint):
            return endpoint

        self._remove_endpoint_file()
        if not start_manager:
            raise ClientError("cli-session manager is not running.")

        return self._start_manager()

    def _read_endpoint(self) -> ManagerEndpoint | None:
        if not self.endpoint_path.exists():
            return None

        try:
            return ManagerEndpoint.from_file(self.endpoint_path)
        except (OSError, KeyError, ValueError, json.JSONDecodeError):
            return None

    def _can_ping(self, endpoint: ManagerEndpoint) -> bool:
        try:
            response = self._send(endpoint, {"action": "ping", "token": endpoint.token}, timeout=1.0)
        except OSError:
            return False

        return bool(response.get("ok"))

    def _start_manager(self) -> ManagerEndpoint:
        self.state_dir.mkdir(parents=True, exist_ok=True)
        self._remove_endpoint_file()

        stdout_path = self.state_dir / "manager.stdout.log"
        stderr_path = self.state_dir / "manager.stderr.log"
        stdout = stdout_path.open("a", encoding="utf-8")
        stderr = stderr_path.open("a", encoding="utf-8")

        args = [
            sys.executable,
            "-m",
            "cli_session.manager_server",
            "--state-dir",
            str(self.state_dir),
        ]
        subprocess.Popen(
            args,
            cwd=str(Path.cwd()),
            stdout=stdout,
            stderr=stderr,
            stdin=subprocess.DEVNULL,
            env=self._manager_environment(),
            creationflags=_detached_creation_flags(),
            close_fds=True,
        )
        stdout.close()
        stderr.close()

        deadline = time.monotonic() + 8.0
        while time.monotonic() < deadline:
            endpoint = self._read_endpoint()
            if endpoint is not None and self._can_ping(endpoint):
                return endpoint

            time.sleep(0.05)

        raise ClientError(f"Timed out starting cli-session manager. See logs in {self.state_dir}.")

    def _manager_environment(self) -> dict[str, str]:
        env = dict(os.environ)
        package_root = Path(__file__).resolve().parents[2]
        source_root = package_root / "src"
        existing = env.get("PYTHONPATH")
        entries = [str(package_root), str(source_root)]
        if existing:
            entries.append(existing)
        env["PYTHONPATH"] = os.pathsep.join(entries)
        env.setdefault("PYTHONUTF8", "1")
        return env

    def _send(
        self,
        endpoint: ManagerEndpoint,
        payload: dict[str, Any],
        *,
        timeout: float | None = None,
    ) -> dict[str, Any]:
        with socket.create_connection((endpoint.host, endpoint.port), timeout=timeout) as sock:
            sock.sendall(json_line(payload))
            with sock.makefile("rb") as reader:
                raw = reader.readline()

        if not raw:
            raise ClientError("cli-session manager closed the connection without a response.")

        return json.loads(raw.decode("utf-8"))

    def _remove_endpoint_file(self) -> None:
        try:
            self.endpoint_path.unlink()
        except FileNotFoundError:
            pass


def _detached_creation_flags() -> int:
    if os.name != "nt":
        return 0

    flags = subprocess.CREATE_NEW_PROCESS_GROUP | subprocess.DETACHED_PROCESS
    if hasattr(subprocess, "CREATE_BREAKAWAY_FROM_JOB"):
        flags |= subprocess.CREATE_BREAKAWAY_FROM_JOB
    return flags
