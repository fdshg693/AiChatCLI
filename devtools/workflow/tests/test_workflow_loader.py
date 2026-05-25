from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

from workflow_cli.workflow_loader import load_workflow
from workflow_cli.validation import validate_workflow


class WorkflowLoaderTests(unittest.TestCase):
    def test_repo_workflows_are_valid_with_markdown_prompts(self) -> None:
        root = Path(__file__).resolve().parent.parent
        workflows_dir = root / "workflows"

        workflow_expectations = {
            "aichatcli-dev-support.json": ["repository-survey", "plan-approach"],
            "aichatcli-implement-feature.json": ["implement", "review", "apply-review-fixes"],
            "aichatcli-complex-delivery.json": [
                "repository-survey",
                "plan-approach",
                "implement",
                "review",
                "apply-review-fixes",
            ],
        }

        self.assertEqual(
            sorted(path.name for path in workflows_dir.glob("*.json")),
            sorted(workflow_expectations),
        )
        for workflow_name, step_names in workflow_expectations.items():
            workflow = load_workflow(workflows_dir / workflow_name)
            self.assertEqual([step.name for step in workflow.steps], step_names)
            self.assertEqual(validate_workflow(workflow, workflows_dir / workflow_name), [])

    def test_repo_prompt_configs_are_markdown_only(self) -> None:
        root = Path(__file__).resolve().parent.parent
        prompt_configs_dir = root / "prompt_configs"

        self.assertTrue(list(prompt_configs_dir.glob("*.md")))
        self.assertEqual(list(prompt_configs_dir.glob("*.yaml")), [])

    def test_repo_variables_are_json_only(self) -> None:
        root = Path(__file__).resolve().parent.parent
        variables_dir = root / "variables"

        self.assertTrue(list(variables_dir.glob("*.json")))
        self.assertEqual(
            sorted(path.name for path in variables_dir.glob("*.json")),
            ["complex-delivery.json", "dev-support.json", "implement-feature.json"],
        )

    def test_load_workflow_preserves_merge_order_and_definition_relative_paths(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            (root / "repo").mkdir()

            base_dir = root / "base"
            fragment_dir = root / "fragment"
            child_dir = root / "child"
            for directory in (base_dir, fragment_dir, child_dir):
                directory.mkdir(parents=True)

            (base_dir / "base.json").write_text(
                json.dumps(
                    {
                        "name": "base",
                        "variables": {"base_only": "yes"},
                        "defaults": {"cwd": "../repo"},
                        "steps": [{"name": "shared", "prompt": "base prompt"}],
                    }
                ),
                encoding="utf-8",
            )
            (fragment_dir / "fragment.json").write_text(
                json.dumps(
                    {
                        "steps": [{"name": "fragment-step"}],
                    }
                ),
                encoding="utf-8",
            )
            (child_dir / "workflow.json").write_text(
                json.dumps(
                    {
                        "extends": "../base/base.json",
                        "compose": ["../fragment/fragment.json"],
                        "name": "child",
                        "variables": {"child_only": "yes"},
                        "steps": [{"name": "shared", "prompt": "override prompt"}],
                    }
                ),
                encoding="utf-8",
            )

            workflow = load_workflow(child_dir / "workflow.json")

            self.assertEqual(workflow.name, "child")
            self.assertEqual(workflow.variables["base_only"], "yes")
            self.assertEqual(workflow.variables["child_only"], "yes")
            self.assertEqual(workflow.defaults.cwd, str((root / "repo").resolve()))
            self.assertEqual(len(workflow.steps), 2)
            self.assertEqual(workflow.steps[0].name, "shared")
            self.assertEqual(workflow.steps[0].prompt, "override prompt")
            self.assertEqual(workflow.steps[1].name, "fragment-step")
            self.assertIsNone(workflow.steps[1].prompt)

    def test_prompt_file_and_output_file_are_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            workflow_path = Path(temp_dir) / "workflow.json"
            workflow_path.write_text(
                json.dumps(
                    {
                        "name": "demo",
                        "steps": [
                            {
                                "name": "example",
                                "prompt_file": "./prompt.md",
                            }
                        ],
                    }
                ),
                encoding="utf-8",
            )

            with self.assertRaisesRegex(ValueError, "prompt_file is not supported"):
                load_workflow(workflow_path)

    def test_load_workflow_reads_variables_file_from_workflow_root(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            workflows_dir = root / "workflows"
            variables_dir = root / "variables"
            workflows_dir.mkdir()
            variables_dir.mkdir()
            (variables_dir / "task.json").write_text(
                json.dumps({"repo_name": "AiChatCLI", "task_summary": "demo"}),
                encoding="utf-8",
            )
            workflow_path = workflows_dir / "workflow.json"
            workflow_path.write_text(
                json.dumps(
                    {
                        "name": "demo",
                        "variables_file": "task.json",
                        "steps": [{"name": "example", "prompt": "Hello ${repo_name}"}],
                    }
                ),
                encoding="utf-8",
            )

            workflow = load_workflow(workflow_path)

            self.assertEqual(workflow.variables["repo_name"], "AiChatCLI")
            self.assertEqual(workflow.variables["task_summary"], "demo")

    def test_variables_file_cannot_be_combined_with_inline_variables(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            workflows_dir = root / "workflows"
            variables_dir = root / "variables"
            workflows_dir.mkdir()
            variables_dir.mkdir()
            (variables_dir / "task.json").write_text(json.dumps({"repo_name": "AiChatCLI"}), encoding="utf-8")
            workflow_path = workflows_dir / "workflow.json"
            workflow_path.write_text(
                json.dumps(
                    {
                        "name": "demo",
                        "variables_file": "task.json",
                        "variables": {"task_summary": "override"},
                        "steps": [{"name": "example", "prompt": "hello"}],
                    }
                ),
                encoding="utf-8",
            )

            with self.assertRaisesRegex(ValueError, "variables_file cannot be combined"):
                load_workflow(workflow_path)

    def test_variables_file_must_be_a_file_name(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            workflows_dir = root / "workflows"
            workflows_dir.mkdir()
            workflow_path = workflows_dir / "workflow.json"
            workflow_path.write_text(
                json.dumps(
                    {
                        "name": "demo",
                        "variables_file": "../task.json",
                        "steps": [{"name": "example", "prompt": "hello"}],
                    }
                ),
                encoding="utf-8",
            )

            with self.assertRaisesRegex(ValueError, "variables_file must be a JSON file name"):
                load_workflow(workflow_path)

    def test_representative_workflow_has_inline_defaults_and_variables(self) -> None:
        root = Path(__file__).resolve().parent.parent
        workflow = load_workflow(root / "workflows" / "aichatcli-implement-feature.json")

        self.assertEqual(workflow.run_name, "aichatcli-implement-feature")
        self.assertEqual(workflow.defaults.cwd, str(root.parent.parent.resolve()))
        self.assertTrue(workflow.defaults.force)
        self.assertEqual(workflow.variables["repo_name"], "AiChatCLI")
        self.assertEqual(workflow.variables["task_summary"], "implement the requested feature")
        self.assertEqual(workflow.steps[0].prompt, "${file:implement.md}")


if __name__ == "__main__":
    unittest.main()
