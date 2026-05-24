from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any

from cli_session.client import ClientError, ManagerClient
from cli_session.protocol import DEFAULT_TIMEOUT_SECONDS


def main() -> int:
    parser = _build_parser()
    args = parser.parse_args()

    client = ManagerClient(args.state_dir)
    try:
        response = _dispatch(client, args, parser)
    except ClientError as error:
        response = {"ok": False, "error": str(error)}

    _print_response(response, text=args.text)
    return 0 if response.get("ok") else 1


def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="cli-session",
        description="Wrap interactive CLI processes behind reusable session IDs.",
    )
    parser.add_argument("--state-dir", type=Path, help="Directory for manager connection state.")
    parser.add_argument("--text", action="store_true", help="Print raw output instead of JSON.")
    subparsers = parser.add_subparsers(dest="command", required=True)

    start = subparsers.add_parser("start", help="Start a wrapped CLI process.")
    start.add_argument("--id", required=True, help="Session ID.")
    start.add_argument("--cwd", type=Path, default=Path.cwd(), help="Child process working directory.")
    start.add_argument("--timeout", type=float, default=DEFAULT_TIMEOUT_SECONDS)
    _add_pattern_arguments(start)
    start.add_argument("child_command", nargs=argparse.REMAINDER, help="Command to run after '--'.")

    send = subparsers.add_parser("send", help="Send one line to a session.")
    send.add_argument("--id", required=True, help="Session ID.")
    send.add_argument("--timeout", type=float, default=DEFAULT_TIMEOUT_SECONDS)
    send.add_argument("input", nargs=argparse.REMAINDER, help="Line to send.")

    read = subparsers.add_parser("read", help="Read unread output from a session.")
    read.add_argument("--id", required=True, help="Session ID.")
    read.add_argument("--timeout", type=float, default=0.0)

    subparsers.add_parser("list", help="List active sessions.")

    stop = subparsers.add_parser("stop", help="Stop one session.")
    stop.add_argument("--id", required=True, help="Session ID.")
    stop.add_argument("--timeout", type=float, default=5.0)

    stop_all = subparsers.add_parser("stop-all", help="Stop all sessions.")
    stop_all.add_argument("--timeout", type=float, default=5.0)

    shutdown = subparsers.add_parser("shutdown-manager", help=argparse.SUPPRESS)
    shutdown.add_argument("--timeout", type=float, default=5.0)
    return parser


def _add_pattern_arguments(parser: argparse.ArgumentParser) -> None:
    parser.add_argument("--ready-suffix", help="Output suffix that means the CLI is ready for input.")
    parser.add_argument("--approval-suffix", help="Output suffix that means the CLI is waiting for YES/NO.")
    parser.add_argument("--denial-reason-suffix", help="Output suffix that means the CLI is waiting for a denial reason.")


def _dispatch(client: ManagerClient, args: argparse.Namespace, parser: argparse.ArgumentParser) -> dict[str, Any]:
    if args.command == "start":
        command = _strip_separator(args.child_command)
        if not command:
            parser.error("start requires a child command after '--'.")

        return client.request(
            {
                "action": "start",
                "id": args.id,
                "cwd": str(args.cwd.resolve()),
                "command": command,
                "timeout": args.timeout,
                "ready_suffix": args.ready_suffix,
                "approval_suffix": args.approval_suffix,
                "denial_reason_suffix": args.denial_reason_suffix,
            }
        )

    if args.command == "send":
        return client.request(
            {
                "action": "send",
                "id": args.id,
                "input": " ".join(_strip_separator(args.input)),
                "timeout": args.timeout,
            }
        )

    if args.command == "read":
        return client.request({"action": "read", "id": args.id, "timeout": args.timeout})

    if args.command == "list":
        return client.request({"action": "list"})

    if args.command == "stop":
        return client.request({"action": "stop", "id": args.id, "timeout": args.timeout})

    if args.command == "stop-all":
        return client.request({"action": "stop_all", "timeout": args.timeout})

    if args.command == "shutdown-manager":
        response = client.request({"action": "shutdown", "timeout": args.timeout}, start_manager=False)
        if response.get("ok"):
            client.wait_until_stopped(args.timeout)
        return response

    parser.error(f"Unknown command {args.command!r}.")


def _strip_separator(values: list[str]) -> list[str]:
    if values and values[0] == "--":
        return values[1:]

    return values


def _print_response(response: dict[str, Any], *, text: bool) -> None:
    if not text:
        print(json.dumps(response, ensure_ascii=False, indent=2))
        return

    if not response.get("ok"):
        print(response.get("error", "Unknown error."), file=sys.stderr)
        return

    if "sessions" in response and "stdout" not in response:
        for session in response["sessions"]:
            if "id" in session:
                print(f"{session['id']}: {session.get('state', 'unknown')}")
        return

    stdout = response.get("stdout")
    stderr = response.get("stderr")
    if stdout:
        print(stdout, end="")
    if stderr:
        print(stderr, end="", file=sys.stderr)
