from __future__ import annotations

import json
import subprocess
from pathlib import Path
from typing import Any

from workflow_cli.cursor_cli import CursorInvocation
from workflow_cli.models import WorkflowConfig
from workflow_cli.step_support import PreparedStep


def ensure_output_directory(output_file: Path) -> None:
    output_file.parent.mkdir(parents=True, exist_ok=True)


def write_step_outputs(
    completed: subprocess.CompletedProcess[str],
    prepared_step: PreparedStep,
) -> Path | None:
    prepared_step.output_file.write_text(completed.stdout, encoding="utf-8")

    if not completed.stderr.strip():
        return None

    prepared_step.stderr_file.write_text(completed.stderr, encoding="utf-8")
    return prepared_step.stderr_file


def write_step_metadata(
    workflow: WorkflowConfig,
    prepared_step: PreparedStep,
    *,
    invocation: CursorInvocation,
    exit_code: int,
    elapsed_seconds: float,
    stderr_file: Path | None,
    chat_id: str | None,
) -> dict[str, Any]:
    metadata = {
        "workflow_name": workflow.name,
        "step_name": prepared_step.step.name,
        "command": invocation.args,
        "cwd": str(prepared_step.cwd),
        "workspace": str(prepared_step.workspace) if prepared_step.workspace else None,
        "output_format": prepared_step.options.output_format,
        "exit_code": exit_code,
        "elapsed_seconds": round(elapsed_seconds, 3),
        "output_file": str(prepared_step.output_file),
        "stderr_file": str(stderr_file) if stderr_file else None,
        "chat_id": chat_id,
    }
    prepared_step.metadata_file.write_text(
        json.dumps(metadata, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    return metadata
