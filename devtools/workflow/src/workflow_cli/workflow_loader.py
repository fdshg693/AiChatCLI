from __future__ import annotations

import json
from dataclasses import replace
from pathlib import Path
from typing import Any

from workflow_cli.models import StepConfig, WorkflowConfig
from workflow_cli.options import load_defaults, load_step_option_overrides
from workflow_cli.parsing import (
    expect_bool,
    expect_int,
    expect_optional_str,
    expect_str,
    expect_string_dict,
)

MAX_COMPOSITION_DEPTH = 20
PATH_FIELDS = {"workspace", "cwd"}
STEP_PATH_FIELDS = {"workspace", "cwd"}


def load_workflow(path: Path) -> WorkflowConfig:
    raw = _resolve_workflow_definition(
        path.resolve(),
        stack=(),
        depth=0,
    )

    schema_version = expect_int(raw.get("schema_version", 1), "schema_version")
    name = expect_str(raw.get("name"), "name")
    description = expect_optional_str(raw.get("description"), "description") or ""
    variables = expect_string_dict(raw.get("variables", {}), "variables")
    defaults = load_defaults(raw.get("defaults", {}))
    steps = _load_steps(raw.get("steps"))

    if not steps:
        raise ValueError("Workflow must contain at least one step.")

    return WorkflowConfig(
        name=name,
        run_name=expect_optional_str(raw.get("run_name"), "run_name"),
        description=description,
        schema_version=schema_version,
        variables=variables,
        defaults=defaults,
        steps=steps,
    )


def apply_variable_overrides(workflow: WorkflowConfig, overrides: dict[str, str]) -> WorkflowConfig:
    if not overrides:
        return workflow

    merged_variables = dict(workflow.variables)
    merged_variables.update(overrides)
    return replace(workflow, variables=merged_variables)


def _load_steps(raw: Any) -> list[StepConfig]:
    if not isinstance(raw, list):
        raise ValueError("steps must be an array.")

    steps: list[StepConfig] = []
    for index, item in enumerate(raw, start=1):
        if not isinstance(item, dict):
            raise ValueError(f"steps[{index - 1}] must be an object.")

        prefix = f"steps[{index - 1}]"
        prompt = expect_optional_str(item.get("prompt"), f"{prefix}.prompt")
        if "prompt_file" in item:
            raise ValueError(
                f"{prefix}.prompt_file is no longer supported. Move prompts into a runtime YAML file."
            )
        if "output_file" in item:
            raise ValueError(
                f"{prefix}.output_file is no longer supported. Output paths are generated automatically."
            )

        option_overrides = load_step_option_overrides(item, prefix=prefix)
        steps.append(
            StepConfig(
                name=expect_str(item.get("name"), f"{prefix}.name"),
                prompt=prompt,
                continue_on_error=expect_bool(item.get("continue_on_error", False), f"{prefix}.continue_on_error"),
                resume_from_previous=expect_bool(
                    item.get("resume_from_previous", False),
                    f"{prefix}.resume_from_previous",
                ),
                variables=expect_string_dict(item.get("variables", {}), f"{prefix}.variables"),
                **option_overrides,
            )
        )

    return steps


def _resolve_workflow_definition(path: Path, *, stack: tuple[Path, ...], depth: int) -> dict[str, Any]:
    if depth > MAX_COMPOSITION_DEPTH:
        chain = " -> ".join(str(item) for item in stack + (path,))
        raise ValueError(
            f"Workflow inheritance/composition exceeded max depth {MAX_COMPOSITION_DEPTH}: {chain}"
        )

    if path in stack:
        chain = " -> ".join(str(item) for item in stack + (path,))
        raise ValueError(f"Workflow inheritance/composition cycle detected: {chain}")

    raw = _read_workflow_object(path)
    raw = _normalize_relative_paths(raw, path.parent)

    merged: dict[str, Any] = {}
    next_stack = stack + (path,)

    parent_value = raw.get("extends")
    if parent_value is not None:
        parent_path = _resolve_reference_path(path.parent, parent_value, "extends")
        merged = _merge_workflow_objects(
            merged,
            _resolve_workflow_definition(parent_path, stack=next_stack, depth=depth + 1),
        )

    compose_items = raw.get("compose", [])
    if compose_items is None:
        compose_items = []
    if not isinstance(compose_items, list):
        raise ValueError("compose must be an array of file paths.")

    for index, item in enumerate(compose_items):
        compose_path = _resolve_reference_path(path.parent, item, f"compose[{index}]")
        merged = _merge_workflow_objects(
            merged,
            _resolve_workflow_definition(compose_path, stack=next_stack, depth=depth + 1),
        )

    current = {key: value for key, value in raw.items() if key not in {"extends", "compose"}}
    return _merge_workflow_objects(merged, current)


def _read_workflow_object(path: Path) -> dict[str, Any]:
    raw = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(raw, dict):
        raise ValueError(f"Workflow file must contain a JSON object: {path}")
    return raw


def _resolve_reference_path(base_dir: Path, value: Any, field_name: str) -> Path:
    relative_path = expect_str(value, field_name)
    return (base_dir / relative_path).resolve()


def _normalize_relative_paths(raw: dict[str, Any], base_dir: Path) -> dict[str, Any]:
    normalized = dict(raw)

    defaults = normalized.get("defaults")
    if isinstance(defaults, dict):
        normalized["defaults"] = _normalize_object_paths(defaults, PATH_FIELDS, base_dir)

    steps = normalized.get("steps")
    if isinstance(steps, list):
        normalized_steps: list[Any] = []
        for index, item in enumerate(steps):
            if not isinstance(item, dict):
                normalized_steps.append(item)
                continue
            normalized_steps.append(
                _normalize_object_paths(item, STEP_PATH_FIELDS, base_dir, prefix=f"steps[{index}]")
            )
        normalized["steps"] = normalized_steps

    return normalized


def _normalize_object_paths(
    raw: dict[str, Any],
    path_fields: set[str],
    base_dir: Path,
    *,
    prefix: str | None = None,
) -> dict[str, Any]:
    normalized = dict(raw)
    for field_name in path_fields:
        value = normalized.get(field_name)
        if value is None:
            continue
        if not isinstance(value, str):
            label = f"{prefix}.{field_name}" if prefix else field_name
            raise ValueError(f"{label} must be a string.")
        normalized[field_name] = str((base_dir / value).resolve())
    return normalized


def _merge_workflow_objects(base: dict[str, Any], overlay: dict[str, Any]) -> dict[str, Any]:
    merged = dict(base)
    for key, value in overlay.items():
        if key == "variables":
            merged[key] = _merge_string_dicts(merged.get(key), value, "variables")
            continue
        if key == "defaults":
            merged[key] = _merge_object_dicts(merged.get(key), value, "defaults")
            continue
        if key == "steps":
            merged[key] = _merge_steps(merged.get(key), value)
            continue
        merged[key] = value
    return merged


def _merge_string_dicts(base: Any, overlay: Any, field_name: str) -> dict[str, Any]:
    merged: dict[str, Any] = {}
    if base is not None:
        if not isinstance(base, dict):
            raise ValueError(f"{field_name} must be an object.")
        merged.update(base)
    if overlay is not None:
        if not isinstance(overlay, dict):
            raise ValueError(f"{field_name} must be an object.")
        merged.update(overlay)
    return merged


def _merge_object_dicts(base: Any, overlay: Any, field_name: str) -> dict[str, Any]:
    if base is None:
        base = {}
    if overlay is None:
        overlay = {}
    if not isinstance(base, dict) or not isinstance(overlay, dict):
        raise ValueError(f"{field_name} must be an object.")
    merged = dict(base)
    merged.update(overlay)
    return merged


def _merge_steps(base: Any, overlay: Any) -> list[Any]:
    merged: list[Any] = []
    index_by_name: dict[str, int] = {}

    for source in (base, overlay):
        if source is None:
            continue
        if not isinstance(source, list):
            raise ValueError("steps must be an array.")
        for item in source:
            if not isinstance(item, dict):
                raise ValueError("steps must contain objects only.")
            name = item.get("name")
            if not isinstance(name, str) or not name.strip():
                merged.append(dict(item))
                continue

            copied = dict(item)
            if name in index_by_name:
                merged[index_by_name[name]] = copied
            else:
                index_by_name[name] = len(merged)
                merged.append(copied)

    return merged
