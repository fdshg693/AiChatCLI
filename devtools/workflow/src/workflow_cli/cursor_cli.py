from __future__ import annotations

import subprocess
from dataclasses import dataclass
from pathlib import Path
from shutil import which

from workflow_cli.models import CursorOptions
from workflow_cli.options import extend_invocation_args


@dataclass(frozen=True)
class CursorInvocation:
    args: list[str]
    cwd: Path
    workspace: Path | None
    output_format: str


def build_invocation(
    options: CursorOptions,
    prompt: str,
    *,
    cwd: Path,
    workspace: Path | None,
    resume_chat_id: str | None,
) -> CursorInvocation:
    resolved_command = which(options.command) or options.command
    args = [resolved_command]
    extend_invocation_args(
        args,
        options,
        cwd=cwd,
        workspace=workspace,
        resume_chat_id=resume_chat_id,
    )
    args.append(prompt)

    return CursorInvocation(
        args=args,
        cwd=cwd,
        workspace=workspace,
        output_format=options.output_format,
    )


def invoke_cursor(invocation: CursorInvocation) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        invocation.args,
        cwd=str(invocation.cwd),
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        check=False,
    )
