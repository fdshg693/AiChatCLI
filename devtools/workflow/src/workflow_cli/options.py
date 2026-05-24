from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Any, Callable

from workflow_cli.models import CursorOptions, StepConfig, merge_step_options
from workflow_cli.parsing import (
    expect_bool,
    expect_optional_bool,
    expect_optional_str,
    expect_optional_string_list,
    expect_string_list,
)

SUPPORTED_OUTPUT_FORMATS = frozenset({"text", "json", "stream-json"})
SUPPORTED_MODES = frozenset({"ask", "plan"})
SUPPORTED_SANDBOX_MODES = frozenset({"enabled", "disabled"})


@dataclass(frozen=True)
class OptionSpec:
    name: str
    default_loader: Callable[[Any, str], Any]
    step_loader: Callable[[Any, str], Any]
    default_value: Any


OPTION_SPECS: tuple[OptionSpec, ...] = (
    OptionSpec("command", expect_optional_str, expect_optional_str, "cursor-agent"),
    OptionSpec("subcommand", expect_optional_str, expect_optional_str, None),
    OptionSpec("workspace", expect_optional_str, expect_optional_str, None),
    OptionSpec("cwd", expect_optional_str, expect_optional_str, None),
    OptionSpec("mode", expect_optional_str, expect_optional_str, "ask"),
    OptionSpec("model", expect_optional_str, expect_optional_str, None),
    OptionSpec("output_format", expect_optional_str, expect_optional_str, "text"),
    OptionSpec("print_mode", expect_bool, expect_optional_bool, True),
    OptionSpec("force", expect_bool, expect_optional_bool, False),
    OptionSpec("yolo", expect_bool, expect_optional_bool, False),
    OptionSpec("sandbox", expect_optional_str, expect_optional_str, None),
    OptionSpec("approve_mcps", expect_bool, expect_optional_bool, False),
    OptionSpec("trust", expect_bool, expect_optional_bool, False),
    OptionSpec("extra_args", expect_string_list, expect_optional_string_list, []),
)


def load_defaults(raw: Any) -> CursorOptions:
    if raw is None:
        return CursorOptions()
    if not isinstance(raw, dict):
        raise ValueError("defaults must be an object.")

    values: dict[str, Any] = {}
    for spec in OPTION_SPECS:
        field_name = f"defaults.{spec.name}"
        if spec.name in raw:
            values[spec.name] = spec.default_loader(raw.get(spec.name), field_name)
        else:
            default_value = spec.default_value
            values[spec.name] = list(default_value) if isinstance(default_value, list) else default_value

    values["command"] = values["command"] or "cursor-agent"
    values["output_format"] = values["output_format"] or "text"

    return CursorOptions(**values)


def load_step_option_overrides(raw: dict[str, Any], *, prefix: str) -> dict[str, Any]:
    values: dict[str, Any] = {}
    for spec in OPTION_SPECS:
        values[spec.name] = spec.step_loader(raw.get(spec.name), f"{prefix}.{spec.name}")
    return values


def validate_cursor_options(options: CursorOptions, *, step_name: str) -> list[str]:
    messages: list[str] = []

    if options.output_format not in SUPPORTED_OUTPUT_FORMATS:
        messages.append(f"Unsupported output_format for step '{step_name}': {options.output_format}")

    if options.mode not in {None, *SUPPORTED_MODES}:
        messages.append(f"Unsupported mode for step '{step_name}': {options.mode}")

    if options.sandbox not in {None, *SUPPORTED_SANDBOX_MODES}:
        messages.append(f"Unsupported sandbox for step '{step_name}': {options.sandbox}")

    return messages


def extend_invocation_args(
    args: list[str],
    options: CursorOptions,
    *,
    cwd: Path,
    workspace: Path | None,
    resume_chat_id: str | None,
) -> None:
    if options.subcommand:
        args.append(options.subcommand)

    if options.print_mode:
        args.append("--print")

    if options.output_format and options.print_mode:
        args.extend(["--output-format", options.output_format])

    if options.workspace:
        args.extend(["--workspace", str(workspace or cwd)])

    if options.mode:
        args.extend(["--mode", options.mode])

    if options.model:
        args.extend(["--model", options.model])

    if options.force:
        args.append("--force")

    if options.yolo:
        args.append("--yolo")

    if options.sandbox:
        args.extend(["--sandbox", options.sandbox])

    if options.approve_mcps:
        args.append("--approve-mcps")

    if options.trust:
        args.append("--trust")

    if resume_chat_id:
        args.extend(["--resume", resume_chat_id])

    args.extend(options.extra_args)


__all__ = [
    "SUPPORTED_MODES",
    "SUPPORTED_OUTPUT_FORMATS",
    "SUPPORTED_SANDBOX_MODES",
    "extend_invocation_args",
    "load_defaults",
    "load_step_option_overrides",
    "merge_step_options",
    "validate_cursor_options",
]
