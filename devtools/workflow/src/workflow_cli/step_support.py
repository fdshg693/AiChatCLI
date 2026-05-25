from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path

from workflow_cli.models import CursorOptions, StepConfig, WorkflowConfig
from workflow_cli.options import merge_step_options
from workflow_cli.templating import render_template


@dataclass(frozen=True)
class PreparedStep:
    step: StepConfig
    options: CursorOptions
    workspace: Path | None
    cwd: Path
    output_file: Path
    metadata_file: Path
    stderr_file: Path
    variables: dict[str, str]
    rendered_prompt: str
    resume_chat_id: str | None


def prepare_step(
    workflow: WorkflowConfig,
    workflow_path: Path,
    step: StepConfig,
    *,
    step_index: int,
    previous_chat_id: str | None,
) -> PreparedStep:
    workflow_dir = workflow_path.parent.resolve()
    options = merge_step_options(workflow.defaults, step)
    workspace = resolve_optional_path(options.workspace, base_dir=workflow_dir)
    cwd = resolve_optional_path(options.cwd, base_dir=workflow_dir) or workspace or workflow_dir
    output_file = resolve_output_path(workflow, workflow_dir, step, step_index, options.output_format)
    metadata_file = output_file.with_suffix(output_file.suffix + ".meta.json")
    stderr_file = output_file.with_suffix(output_file.suffix + ".stderr.txt")

    variables = build_variables(
        workflow=workflow,
        workflow_path=workflow_path,
        step_name=step.name,
        output_path=output_file,
        workspace=workspace,
        cwd=cwd,
        extra_variables=step.variables,
    )
    prompt = load_prompt(step)
    rendered_prompt = render_template(prompt, variables, file_root=prompt_config_root(workflow_path))

    resume_chat_id = previous_chat_id if step.resume_from_previous else None
    if step.resume_from_previous and not resume_chat_id:
        raise RuntimeError(
            f"Step '{step.name}' requested resume_from_previous, but no chat id is available."
        )

    return PreparedStep(
        step=step,
        options=options,
        workspace=workspace,
        cwd=cwd,
        output_file=output_file,
        metadata_file=metadata_file,
        stderr_file=stderr_file,
        variables=variables,
        rendered_prompt=rendered_prompt,
        resume_chat_id=resume_chat_id,
    )


def build_variables(
    *,
    workflow: WorkflowConfig,
    workflow_path: Path,
    step_name: str,
    output_path: Path,
    workspace: Path | None,
    cwd: Path,
    extra_variables: dict[str, str],
) -> dict[str, str]:
    workflow_dir = workflow_path.parent.resolve()
    run_name = resolved_run_name(workflow)
    variables = {
        "workflow_name": workflow.name,
        "run_name": run_name,
        "workflow_file": str(workflow_path.resolve()),
        "workflow_dir": str(workflow_dir),
        "step_name": step_name,
        "output_file": str(output_path),
        "cwd": str(cwd),
    }
    if workspace:
        variables["workspace"] = str(workspace)

    variables.update(workflow.variables)
    variables.update(extra_variables)
    return variables


def load_prompt(step: StepConfig) -> str:
    if step.prompt is not None:
        return step.prompt

    raise RuntimeError(
        f"Step '{step.name}' does not have a prompt. Add a prompt field to the workflow step."
    )


def prompt_config_root(workflow_path: Path) -> Path:
    return workflow_path.parent.parent / "prompt_configs"


def resolve_output_path(
    workflow: WorkflowConfig,
    workflow_dir: Path,
    step: StepConfig,
    index: int,
    output_format: str,
) -> Path:
    extension = {
        "json": ".json",
        "stream-json": ".ndjson",
    }.get(output_format, ".md")
    safe_name = sanitize_name(step.name)
    run_name = resolved_run_name(workflow)
    return (workflow_dir.parent / "runs" / run_name / f"{index:02d}-{safe_name}{extension}").resolve()


def resolve_optional_path(raw_path: str | None, *, base_dir: Path) -> Path | None:
    if raw_path is None:
        return None
    return resolve_path(raw_path, base_dir=base_dir)


def resolve_path(raw_path: str, *, base_dir: Path) -> Path:
    path = Path(raw_path)
    if not path.is_absolute():
        path = base_dir / path
    return path.resolve()


def sanitize_name(value: str) -> str:
    return "".join(character if character.isalnum() or character in {"-", "_"} else "-" for character in value)


def resolved_run_name(workflow: WorkflowConfig) -> str:
    return sanitize_name(workflow.run_name or workflow.name)
