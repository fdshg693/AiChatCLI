from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

from workflow_cli.config import apply_prompt_config, load_prompt_config, load_workflow
from workflow_cli.validation import validate_workflow


class WorkflowLoaderTests(unittest.TestCase):
    def test_repo_workflows_and_prompt_configs_stay_aligned(self) -> None:
        root = Path(__file__).resolve().parent.parent
        workflows_dir = root / "workflows"
        prompt_configs_dir = root / "prompt_configs"

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

        for workflow_name, step_names in workflow_expectations.items():
            workflow = load_workflow(workflows_dir / workflow_name)
            self.assertEqual([step.name for step in workflow.steps], step_names)
            self.assertEqual(validate_workflow(workflow, workflows_dir / workflow_name, require_prompts=False), [])

        prompt_config_expectations = {
            "aichatcli-dev-support.yaml": "aichatcli-dev-support.json",
            "aichatcli-implement-chat-history-logging.yaml": "aichatcli-implement-feature.json",
            "aichatcli-implement-prompt-config.yaml": "aichatcli-implement-feature.json",
            "aichatcli-complex-chat-history-logging.yaml": "aichatcli-complex-delivery.json",
        }

        for prompt_config_name, workflow_name in prompt_config_expectations.items():
            workflow = apply_prompt_config(
                load_workflow(workflows_dir / workflow_name),
                load_prompt_config(prompt_configs_dir / prompt_config_name),
            )
            self.assertEqual(validate_workflow(workflow, workflows_dir / workflow_name), [])

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

            with self.assertRaisesRegex(ValueError, "prompt_file is no longer supported"):
                load_workflow(workflow_path)

    def test_prompt_config_applies_run_name_variables_and_step_prompts(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            workflow_path = root / "workflow.json"
            prompt_config_path = root / "task.yaml"
            workflow_path.write_text(
                json.dumps(
                    {
                        "name": "template-workflow",
                        "variables": {"repo_name": "AiChatCLI"},
                        "steps": [
                            {"name": "implement", "variables": {"phase": "write"}},
                            {"name": "review", "prompt": "fallback prompt"},
                        ],
                    }
                ),
                encoding="utf-8",
            )
            prompt_config_path.write_text(
                "\n".join(
                    [
                        "run_name: prompt-config-task",
                        "variables:",
                        "  task_summary: Implement prompt config",
                        "steps:",
                        "  implement:",
                        "    prompt: |",
                        "      Do the implementation for ${repo_name}.",
                        "    variables:",
                        "      phase: execute",
                        "  review:",
                        "    prompt: Review the implementation.",
                    ]
                ),
                encoding="utf-8",
            )

            workflow = apply_prompt_config(load_workflow(workflow_path), load_prompt_config(prompt_config_path))

            self.assertEqual(workflow.run_name, "prompt-config-task")
            self.assertEqual(workflow.variables["repo_name"], "AiChatCLI")
            self.assertEqual(workflow.variables["task_summary"], "Implement prompt config")
            self.assertEqual(workflow.steps[0].prompt, "Do the implementation for ${repo_name}.\n")
            self.assertEqual(workflow.steps[0].variables["phase"], "execute")
            self.assertEqual(workflow.steps[1].prompt, "Review the implementation.")


if __name__ == "__main__":
    unittest.main()
