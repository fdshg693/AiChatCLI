from __future__ import annotations

import argparse
import asyncio
import json
import os
import secrets
from pathlib import Path
from typing import Any

from cli_session.manager import SessionManager
from cli_session.protocol import ManagerEndpoint, endpoint_file, json_line, resolve_state_dir


async def run_server(state_dir: Path, host: str = "127.0.0.1") -> int:
    state_dir.mkdir(parents=True, exist_ok=True)
    token = secrets.token_urlsafe(32)
    manager = SessionManager()
    shutdown = asyncio.Event()

    async def handle_client(reader: asyncio.StreamReader, writer: asyncio.StreamWriter) -> None:
        response: dict[str, Any]
        try:
            raw = await reader.readline()
            request = json.loads(raw.decode("utf-8"))
            if request.get("token") != token:
                response = {"ok": False, "error": "Invalid manager token."}
            elif request.get("action") == "shutdown":
                await manager.stop_all_sessions(float(request.get("timeout", 5.0)))
                response = {"ok": True, "state": "stopping"}
                shutdown.set()
            else:
                response = await manager.handle(request)
        except Exception as error:  # pragma: no cover - keeps daemon diagnostics JSON-shaped.
            response = {"ok": False, "error": f"{type(error).__name__}: {error}"}

        writer.write(json_line(response))
        await writer.drain()
        writer.close()
        await writer.wait_closed()

    server = await asyncio.start_server(handle_client, host=host, port=0)
    sockets = server.sockets or []
    if not sockets:
        raise RuntimeError("Failed to create cli-session manager socket.")

    endpoint = ManagerEndpoint(
        host=host,
        port=int(sockets[0].getsockname()[1]),
        token=token,
        pid=os.getpid(),
    )
    endpoint.write(endpoint_file(state_dir))

    try:
        async with server:
            await shutdown.wait()
    finally:
        await manager.stop_all_sessions()
        try:
            endpoint_file(state_dir).unlink()
        except FileNotFoundError:
            pass

    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description="Internal cli-session manager daemon.")
    parser.add_argument("--state-dir", type=Path, required=True)
    parser.add_argument("--host", default="127.0.0.1")
    args = parser.parse_args()
    return asyncio.run(run_server(resolve_state_dir(args.state_dir), host=args.host))


if __name__ == "__main__":
    raise SystemExit(main())
