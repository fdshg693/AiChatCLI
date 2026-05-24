from __future__ import annotations

from dataclasses import replace
from pathlib import Path
from typing import Any

import yaml

from workflow_cli.models import PromptConfig, PromptStepConfig, StepConfig, WorkflowConfig
from workflow_cli.parsing import expect_str, expect_string_dict


def load_prompt_config(path: Path) -> PromptConfig:
    raw = yaml.safe_load(path.read_text(encoding="utf-8"))
    if raw is None:
        raw = {}
    if not isinstance(raw, dict):
        raise ValueError(f"Prompt config file must contain an object: {path}")

    run_name = expect_optional_named_string(raw.get("run_name"), "run_name")
    variables = expect_string_dict(raw.get("variables", {}), "variables")
    steps = _load_prompt_steps(raw.get("steps", {}))
    return PromptConfig(
        run_name=run_name or path.stem,
        variables=variables,
        steps=steps,
    )


def apply_prompt_config(workflow: WorkflowConfig, prompt_config: PromptConfig) -> WorkflowConfig:
    workflow_step_names = {step.name for step in workflow.steps}
    unknown_steps = sorted(set(prompt_config.steps) - workflow_step_names)
    if unknown_steps:
        joined = ", ".join(unknown_steps)
        raise ValueError(f"Prompt config contains unknown steps: {joined}")

    merged_variables = dict(workflow.variables)
    merged_variables.update(prompt_config.variables)

    merged_steps: list[StepConfig] = []
    for step in workflow.steps:
        prompt_step = prompt_config.steps.get(step.name)
        merged_step_variables = dict(step.variables)
        prompt = step.prompt
        if prompt_step:
            prompt = prompt_step.prompt
            merged_step_variables.update(prompt_step.variables)
        merged_steps.append(replace(step, prompt=prompt, variables=merged_step_variables))

    return replace(
        workflow,
        run_name=prompt_config.run_name or workflow.run_name,
        variables=merged_variables,
        steps=merged_steps,
    )


def _load_prompt_steps(raw: Any) -> dict[str, PromptStepConfig]:
    if not isinstance(raw, dict):
        raise ValueError("steps must be an object keyed by workflow step name.")

    steps: dict[str, PromptStepConfig] = {}
    for step_name, item in raw.items():
        name = expect_str(step_name, "steps key")
        if isinstance(item, str):
            steps[name] = PromptStepConfig(prompt=expect_str(item, f"steps.{name}"))
            continue
        if not isinstance(item, dict):
            raise ValueError(f"steps.{name} must be a string or object.")
        steps[name] = PromptStepConfig(
            prompt=expect_str(item.get("prompt"), f"steps.{name}.prompt"),
            variables=expect_string_dict(item.get("variables", {}), f"steps.{name}.variables"),
        )
    return steps


def expect_optional_named_string(value: Any, field_name: str) -> str | None:
    if value is None:
        return None
    return expect_str(value, field_name)
