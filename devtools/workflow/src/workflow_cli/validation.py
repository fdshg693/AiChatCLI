from __future__ import annotations

from workflow_cli.models import WorkflowConfig
from workflow_cli.options import merge_step_options, validate_cursor_options


def validate_workflow(workflow: WorkflowConfig, _workflow_path, *, require_prompts: bool = True) -> list[str]:
    messages: list[str] = []

    for index, step in enumerate(workflow.steps):
        if require_prompts and not step.prompt:
            messages.append(
                f"Step '{step.name}' does not have a prompt. Supply one inline or via --prompt-config."
            )

        if index == 0 and step.resume_from_previous:
            messages.append(
                f"Step '{step.name}' cannot use resume_from_previous because there is no previous step."
            )

        options = merge_step_options(workflow.defaults, step)
        messages.extend(validate_cursor_options(options, step_name=step.name))

    return messages
