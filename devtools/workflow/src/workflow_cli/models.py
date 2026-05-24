from __future__ import annotations

from dataclasses import dataclass, field, fields


@dataclass(frozen=True)
class CursorOptions:
    command: str = "cursor-agent"
    subcommand: str | None = None
    workspace: str | None = None
    cwd: str | None = None
    mode: str | None = "ask"
    model: str | None = None
    output_format: str = "text"
    print_mode: bool = True
    force: bool = False
    yolo: bool = False
    sandbox: str | None = None
    approve_mcps: bool = False
    trust: bool = False
    extra_args: list[str] = field(default_factory=list)


@dataclass(frozen=True)
class StepConfig:
    name: str
    prompt: str | None = None
    continue_on_error: bool = False
    resume_from_previous: bool = False
    variables: dict[str, str] = field(default_factory=dict)
    command: str | None = None
    subcommand: str | None = None
    workspace: str | None = None
    cwd: str | None = None
    mode: str | None = None
    model: str | None = None
    output_format: str | None = None
    print_mode: bool | None = None
    force: bool | None = None
    yolo: bool | None = None
    sandbox: str | None = None
    approve_mcps: bool | None = None
    trust: bool | None = None
    extra_args: list[str] | None = None


@dataclass(frozen=True)
class WorkflowConfig:
    name: str
    run_name: str | None = None
    description: str = ""
    schema_version: int = 1
    variables: dict[str, str] = field(default_factory=dict)
    defaults: CursorOptions = field(default_factory=CursorOptions)
    steps: list[StepConfig] = field(default_factory=list)


@dataclass(frozen=True)
class PromptStepConfig:
    prompt: str
    variables: dict[str, str] = field(default_factory=dict)


@dataclass(frozen=True)
class PromptConfig:
    run_name: str | None = None
    variables: dict[str, str] = field(default_factory=dict)
    steps: dict[str, PromptStepConfig] = field(default_factory=dict)


CURSOR_OPTION_FIELD_NAMES = tuple(field.name for field in fields(CursorOptions))


def merge_step_options(defaults: CursorOptions, step: StepConfig) -> CursorOptions:
    merged: dict[str, object] = {}
    for field_name in CURSOR_OPTION_FIELD_NAMES:
        step_value = getattr(step, field_name)
        if field_name == "command":
            merged[field_name] = step_value or defaults.command
            continue
        if field_name == "extra_args":
            base_value = defaults.extra_args if step_value is None else step_value
            merged[field_name] = list(base_value)
            continue
        merged[field_name] = getattr(defaults, field_name) if step_value is None else step_value
    return CursorOptions(**merged)
