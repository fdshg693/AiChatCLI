from __future__ import annotations

import argparse
from pathlib import Path
from typing import Literal

from workflow_cli.config import (
    apply_prompt_config,
    apply_variable_overrides,
    load_prompt_config,
    load_workflow,
)
from workflow_cli.executor import run_workflow, validate_workflow


def main() -> int:
    parser = _build_parser()
    args = parser.parse_args()
    try:
        variable_overrides = _parse_variable_overrides(args.var)
    except ValueError as error:
        parser.error(str(error))

    workflow_path, workflow = _load_validated_workflow(
        args.workflow,
        prompt_config_path=args.prompt_config,
        variable_overrides=variable_overrides,
        command=args.command,
    )
    if workflow is None:
        return 1

    if args.command == "validate":
        print(f"Workflow '{workflow.name}' is valid.")
        print(f"Steps: {len(workflow.steps)}")
        return 0

    results = run_workflow(workflow, workflow_path, dry_run=args.dry_run)
    _print_results_summary(results)
    return 1 if any(not result.succeeded for result in results) else 0


def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="workflow-cli",
        description="Run configurable batch workflows on top of Cursor CLI.",
    )
    subparsers = parser.add_subparsers(dest="command", required=True)

    validate_parser = subparsers.add_parser("validate", help="Validate a workflow JSON file.")
    _add_common_workflow_arguments(validate_parser)

    run_parser = subparsers.add_parser("run", help="Execute a workflow JSON file.")
    _add_common_workflow_arguments(run_parser)
    run_parser.add_argument("--dry-run", action="store_true", help="Print commands without running Cursor CLI.")
    return parser


def _add_common_workflow_arguments(parser: argparse.ArgumentParser) -> None:
    parser.add_argument("workflow", type=Path, help="Path to the workflow JSON file.")
    parser.add_argument(
        "--prompt-config",
        type=Path,
        help="Path to the runtime prompt YAML file.",
    )
    parser.add_argument(
        "--var",
        action="append",
        default=[],
        metavar="KEY=VALUE",
        help="Override or add a workflow variable.",
    )


def _load_validated_workflow(
    workflow_path: Path,
    *,
    prompt_config_path: Path | None,
    variable_overrides: dict[str, str],
    command: Literal["validate", "run"],
):
    resolved_path = workflow_path.resolve()
    workflow = load_workflow(resolved_path)
    if prompt_config_path is not None:
        workflow = apply_prompt_config(workflow, load_prompt_config(prompt_config_path.resolve()))
    workflow = apply_variable_overrides(workflow, variable_overrides)
    validation_errors = validate_workflow(
        workflow,
        resolved_path,
        require_prompts=(command == "run" or prompt_config_path is not None),
    )
    if validation_errors:
        for message in validation_errors:
            print(f"Validation error: {message}")
        return resolved_path, None
    return resolved_path, workflow


def _print_results_summary(results) -> None:
    print()
    print("Summary")
    for result in results:
        status = "OK" if result.succeeded else f"FAILED ({result.exit_code})"
        print(f"- {result.name}: {status}")
        print(f"  output: {result.output_file}")
        print(f"  meta: {result.metadata_file}")
        if result.stderr_file:
            print(f"  stderr: {result.stderr_file}")


def _parse_variable_overrides(items: list[str]) -> dict[str, str]:
    overrides: dict[str, str] = {}
    for item in items:
        if "=" not in item:
            raise ValueError(f"Invalid --var value '{item}'. Expected KEY=VALUE.")

        key, value = item.split("=", 1)
        key = key.strip()
        if not key:
            raise ValueError(f"Invalid --var value '{item}'. Key must not be empty.")

        overrides[key] = value

    return overrides
