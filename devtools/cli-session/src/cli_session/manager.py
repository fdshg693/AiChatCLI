from __future__ import annotations

import asyncio
import codecs
import os
import signal
import time
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

from cli_session.protocol import PromptPatterns


class SessionError(RuntimeError):
    pass


@dataclass
class OutputBuffer:
    text: str = ""
    offset: int = 0

    def append(self, value: str) -> None:
        self.text += value

    def consume(self) -> str:
        value = self.text[self.offset :]
        self.offset = len(self.text)
        return value


@dataclass
class ManagedSession:
    session_id: str
    command: list[str]
    cwd: Path
    patterns: PromptPatterns
    process: asyncio.subprocess.Process
    stdout: OutputBuffer = field(default_factory=OutputBuffer)
    stderr: OutputBuffer = field(default_factory=OutputBuffer)
    condition: asyncio.Condition = field(default_factory=asyncio.Condition)
    started_at: float = field(default_factory=time.time)
    pending_after: tuple[int, int] | None = None

    @classmethod
    async def start(
        cls,
        *,
        session_id: str,
        command: list[str],
        cwd: Path,
        patterns: PromptPatterns,
    ) -> "ManagedSession":
        if not command:
            raise SessionError("start requires a command after '--'.")

        process = await asyncio.create_subprocess_exec(
            *command,
            cwd=str(cwd),
            stdin=asyncio.subprocess.PIPE,
            stdout=asyncio.subprocess.PIPE,
            stderr=asyncio.subprocess.PIPE,
        )
        session = cls(
            session_id=session_id,
            command=command,
            cwd=cwd,
            patterns=patterns,
            process=process,
        )
        asyncio.create_task(session._collect_stream("stdout"))
        asyncio.create_task(session._collect_stream("stderr"))
        asyncio.create_task(session._watch_exit())
        return session

    def state(self) -> str:
        if self.process.returncode is not None:
            return "exited"

        if self.pending_after is not None:
            stdout_len, stderr_len = self.output_position()
            if (stdout_len, stderr_len) <= self.pending_after:
                return "running"

            self.pending_after = None

        if self.stdout.text.endswith(self.patterns.denial_reason_suffix):
            return "denial_reason"

        if self.stdout.text.endswith(self.patterns.approval_suffix):
            return "approval"

        if self.stdout.text.endswith(self.patterns.ready_suffix):
            return "ready"

        return "running"

    def exit_code(self) -> int | None:
        return self.process.returncode

    async def send_line(self, line: str) -> None:
        if self.process.returncode is not None:
            raise SessionError(f"Session '{self.session_id}' has already exited.")

        if self.process.stdin is None:
            raise SessionError(f"Session '{self.session_id}' stdin is not available.")

        self.pending_after = self.output_position()
        self.process.stdin.write((line + "\n").encode("utf-8"))
        await self.process.stdin.drain()

    def output_position(self) -> tuple[int, int]:
        return len(self.stdout.text), len(self.stderr.text)

    async def wait_for_settled_state(self, timeout_seconds: float) -> bool:
        deadline = time.monotonic() + max(0.0, timeout_seconds)
        async with self.condition:
            while self.state() == "running":
                remaining = deadline - time.monotonic()
                if remaining <= 0:
                    return True

                try:
                    await asyncio.wait_for(self.condition.wait(), timeout=remaining)
                except TimeoutError:
                    return True

        return False

    async def wait_for_response_after(self, position: tuple[int, int], timeout_seconds: float) -> bool:
        deadline = time.monotonic() + max(0.0, timeout_seconds)
        async with self.condition:
            while self.output_position() <= position and self.process.returncode is None:
                remaining = deadline - time.monotonic()
                if remaining <= 0:
                    return True

                try:
                    await asyncio.wait_for(self.condition.wait(), timeout=remaining)
                except TimeoutError:
                    return True

        remaining = deadline - time.monotonic()
        if remaining <= 0:
            return self.state() == "running"

        return await self.wait_for_settled_state(remaining)

    async def stop(self, timeout_seconds: float) -> None:
        if self.process.returncode is not None:
            return

        try:
            await self.send_line("exit")
            await asyncio.wait_for(self.process.wait(), timeout=max(0.1, timeout_seconds))
            return
        except (BrokenPipeError, ConnectionResetError, SessionError, TimeoutError):
            pass

        self._terminate()
        try:
            await asyncio.wait_for(self.process.wait(), timeout=max(0.1, timeout_seconds))
        except TimeoutError:
            self.process.kill()
            await self.process.wait()

    def consume_snapshot(self, *, timed_out: bool = False) -> dict[str, Any]:
        return {
            "ok": True,
            "id": self.session_id,
            "state": self.state(),
            "stdout": self.stdout.consume(),
            "stderr": self.stderr.consume(),
            "exit_code": self.exit_code(),
            "timed_out": timed_out,
            "command": self.command,
            "cwd": str(self.cwd),
        }

    def summary(self) -> dict[str, Any]:
        return {
            "id": self.session_id,
            "state": self.state(),
            "exit_code": self.exit_code(),
            "command": self.command,
            "cwd": str(self.cwd),
            "started_at": self.started_at,
        }

    async def _collect_stream(self, stream_name: str) -> None:
        stream = getattr(self.process, stream_name)
        buffer = getattr(self, stream_name)
        decoder = codecs.getincrementaldecoder("utf-8")(errors="replace")

        while True:
            chunk = await stream.read(4096)
            if not chunk:
                tail = decoder.decode(b"", final=True)
                if tail:
                    async with self.condition:
                        buffer.append(tail)
                        self.condition.notify_all()
                return

            text = decoder.decode(chunk)
            if text:
                async with self.condition:
                    buffer.append(text)
                    self.condition.notify_all()

    async def _watch_exit(self) -> None:
        await self.process.wait()
        async with self.condition:
            self.condition.notify_all()

    def _terminate(self) -> None:
        if os.name == "nt":
            self.process.terminate()
            return

        try:
            os.killpg(self.process.pid, signal.SIGTERM)
        except ProcessLookupError:
            self.process.terminate()
        except PermissionError:
            self.process.terminate()


class SessionManager:
    def __init__(self) -> None:
        self._sessions: dict[str, ManagedSession] = {}

    async def handle(self, request: dict[str, Any]) -> dict[str, Any]:
        action = str(request.get("action") or "")
        try:
            if action == "ping":
                return {"ok": True, "state": "running"}
            if action == "start":
                return await self._start(request)
            if action == "send":
                return await self._send(request)
            if action == "read":
                return await self._read(request)
            if action == "list":
                return self._list()
            if action == "stop":
                return await self._stop(request)
            if action == "stop_all":
                return await self._stop_all(request)

            return {"ok": False, "error": f"Unknown action '{action}'."}
        except SessionError as error:
            return {"ok": False, "error": str(error)}
        except FileNotFoundError as error:
            return {"ok": False, "error": f"Command not found: {error.filename}"}
        except Exception as error:  # pragma: no cover - returned for CLI diagnostics.
            return {"ok": False, "error": f"{type(error).__name__}: {error}"}

    async def stop_all_sessions(self, timeout_seconds: float = 5.0) -> None:
        await asyncio.gather(
            *(session.stop(timeout_seconds) for session in list(self._sessions.values())),
            return_exceptions=True,
        )
        self._sessions.clear()

    async def _start(self, request: dict[str, Any]) -> dict[str, Any]:
        session_id = _required_string(request, "id")
        if session_id in self._sessions and self._sessions[session_id].state() != "exited":
            raise SessionError(f"Session '{session_id}' already exists.")

        command = request.get("command")
        if not isinstance(command, list) or not all(isinstance(value, str) for value in command):
            raise SessionError("start requires a string array command.")

        cwd = Path(str(request.get("cwd") or Path.cwd())).expanduser().resolve()
        patterns = PromptPatterns.from_request(request)
        session = await ManagedSession.start(
            session_id=session_id,
            command=command,
            cwd=cwd,
            patterns=patterns,
        )
        self._sessions[session_id] = session
        timed_out = await session.wait_for_settled_state(_timeout(request))
        return session.consume_snapshot(timed_out=timed_out)

    async def _send(self, request: dict[str, Any]) -> dict[str, Any]:
        session = self._get_session(request)
        position = session.output_position()
        await session.send_line(str(request.get("input") or ""))
        timed_out = await session.wait_for_response_after(position, _timeout(request))
        return session.consume_snapshot(timed_out=timed_out)

    async def _read(self, request: dict[str, Any]) -> dict[str, Any]:
        session = self._get_session(request)
        timeout_seconds = _timeout(request, default=0.0)
        timed_out = await session.wait_for_settled_state(timeout_seconds) if timeout_seconds > 0 else False
        return session.consume_snapshot(timed_out=timed_out)

    async def _stop(self, request: dict[str, Any]) -> dict[str, Any]:
        session = self._get_session(request)
        await session.stop(_timeout(request, default=5.0))
        response = session.consume_snapshot()
        self._sessions.pop(session.session_id, None)
        return response

    async def _stop_all(self, request: dict[str, Any]) -> dict[str, Any]:
        timeout_seconds = _timeout(request, default=5.0)
        sessions = list(self._sessions.values())
        await asyncio.gather(*(session.stop(timeout_seconds) for session in sessions), return_exceptions=True)
        responses = [session.consume_snapshot() for session in sessions]
        self._sessions.clear()
        return {"ok": True, "sessions": responses}

    def _list(self) -> dict[str, Any]:
        return {"ok": True, "sessions": [session.summary() for session in self._sessions.values()]}

    def _get_session(self, request: dict[str, Any]) -> ManagedSession:
        session_id = _required_string(request, "id")
        session = self._sessions.get(session_id)
        if session is None:
            raise SessionError(f"Session '{session_id}' does not exist.")

        return session


def _required_string(request: dict[str, Any], key: str) -> str:
    value = request.get(key)
    if not isinstance(value, str) or not value:
        raise SessionError(f"'{key}' is required.")

    return value


def _timeout(request: dict[str, Any], *, default: float = 30.0) -> float:
    try:
        return float(request.get("timeout", default))
    except (TypeError, ValueError):
        return default
