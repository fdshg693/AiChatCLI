from __future__ import annotations

from workflow_cli.models import WorkflowConfig
from workflow_cli.options import merge_step_options, validate_cursor_options
from workflow_cli.step_support import prompt_config_root
from workflow_cli.templating import expand_file_references


def validate_workflow(workflow: WorkflowConfig, workflow_path, *, require_prompts: bool = True) -> list[str]:
    messages: list[str] = []
    file_root = prompt_config_root(workflow_path)

    for index, step in enumerate(workflow.steps):
        if require_prompts and not step.prompt:
            messages.append(
                f"Step '{step.name}' does not have a prompt. Add a prompt field to the workflow step."
            )
        if step.prompt:
            try:
                expand_file_references(step.prompt, file_root=file_root)
            except ValueError as error:
                messages.append(f"Step '{step.name}' has an invalid prompt: {error}")

        if index == 0 and step.resume_from_previous:
            messages.append(
                f"Step '{step.name}' cannot use resume_from_previous because there is no previous step."
            )

        options = merge_step_options(workflow.defaults, step)
        messages.extend(validate_cursor_options(options, step_name=step.name))

    return messages
