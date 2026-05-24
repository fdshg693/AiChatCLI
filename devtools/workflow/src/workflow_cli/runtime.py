from __future__ import annotations

import time
from dataclasses import dataclass
from pathlib import Path

from workflow_cli.artifacts import ensure_output_directory, write_step_metadata, write_step_outputs
from workflow_cli.cursor_cli import build_invocation, invoke_cursor
from workflow_cli.cursor_output import extract_chat_id, parse_cursor_output
from workflow_cli.models import WorkflowConfig
from workflow_cli.step_support import prepare_step


@dataclass(frozen=True)
class StepResult:
    name: str
    succeeded: bool
    exit_code: int
    output_file: Path
    metadata_file: Path
    stderr_file: Path | None
    elapsed_seconds: float
    chat_id: str | None


def run_workflow(workflow: WorkflowConfig, workflow_path: Path, *, dry_run: bool = False) -> list[StepResult]:
    results: list[StepResult] = []
    previous_chat_id: str | None = None

    for index, step in enumerate(workflow.steps, start=1):
        prepared_step = prepare_step(
            workflow,
            workflow_path,
            step,
            step_index=index,
            previous_chat_id=previous_chat_id,
        )
        invocation = build_invocation(
            prepared_step.options,
            prepared_step.rendered_prompt,
            cwd=prepared_step.cwd,
            workspace=prepared_step.workspace,
            resume_chat_id=prepared_step.resume_chat_id,
        )

        print(f"[{index}/{len(workflow.steps)}] {step.name}")
        print(f"  command: {' '.join(invocation.args[:-1])} <prompt>")
        print(f"  cwd: {prepared_step.cwd}")
        print(f"  output: {prepared_step.output_file}")

        if dry_run:
            results.append(
                StepResult(
                    name=step.name,
                    succeeded=True,
                    exit_code=0,
                    output_file=prepared_step.output_file,
                    metadata_file=prepared_step.metadata_file,
                    stderr_file=None,
                    elapsed_seconds=0.0,
                    chat_id=prepared_step.resume_chat_id,
                )
            )
            continue

        ensure_output_directory(prepared_step.output_file)

        started_at = time.perf_counter()
        completed = invoke_cursor(invocation)
        elapsed = time.perf_counter() - started_at

        stderr_file = write_step_outputs(completed, prepared_step)
        parsed_output = parse_cursor_output(completed.stdout, prepared_step.options.output_format)
        chat_id = extract_chat_id(parsed_output)
        write_step_metadata(
            workflow,
            prepared_step,
            invocation=invocation,
            exit_code=completed.returncode,
            elapsed_seconds=elapsed,
            stderr_file=stderr_file,
            chat_id=chat_id,
        )

        succeeded = completed.returncode == 0
        results.append(
            StepResult(
                name=step.name,
                succeeded=succeeded,
                exit_code=completed.returncode,
                output_file=prepared_step.output_file,
                metadata_file=prepared_step.metadata_file,
                stderr_file=stderr_file,
                elapsed_seconds=elapsed,
                chat_id=chat_id,
            )
        )

        previous_chat_id = chat_id or previous_chat_id
        if not succeeded and not step.continue_on_error:
            break

    return results
