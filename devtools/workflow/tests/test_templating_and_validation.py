from __future__ import annotations

import unittest
from pathlib import Path
import tempfile

from workflow_cli.cursor_output import extract_chat_id, parse_cursor_output
from workflow_cli.models import CursorOptions, StepConfig, WorkflowConfig
from workflow_cli.options import merge_step_options
from workflow_cli.step_support import resolve_output_path
from workflow_cli.templating import render_template
from workflow_cli.validation import validate_workflow


class TemplatingTests(unittest.TestCase):
    def test_render_template_supports_escaped_placeholders(self) -> None:
        rendered = render_template("literal=$${name} value=${name}", {"name": "demo"})
        self.assertEqual(rendered, "literal=${name} value=demo")

    def test_render_template_reports_available_variables(self) -> None:
        with self.assertRaisesRegex(ValueError, "Available variables: present"):
            render_template("${missing}", {"present": "demo"})

    def test_render_template_expands_markdown_file_before_variables(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            prompt_root = Path(temp_dir)
            (prompt_root / "prompt.md").write_text("Hello $${name} ${name}.", encoding="utf-8")

            rendered = render_template("Start: ${file:prompt.md}", {"name": "AiChatCLI"}, file_root=prompt_root)

            self.assertEqual(rendered, "Start: Hello ${name} AiChatCLI.")

    def test_render_template_leaves_escaped_file_reference_literal(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            rendered = render_template("$${file:prompt.md}", {}, file_root=Path(temp_dir))

            self.assertEqual(rendered, "${file:prompt.md}")

    def test_render_template_rejects_missing_markdown_file(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            with self.assertRaisesRegex(ValueError, "Prompt file does not exist"):
                render_template("${file:missing.md}", {}, file_root=Path(temp_dir))

    def test_render_template_rejects_malformed_file_reference(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            with self.assertRaisesRegex(ValueError, "Malformed"):
                render_template("${file:missing.md", {}, file_root=Path(temp_dir))

    def test_render_template_rejects_file_reference_outside_prompt_root(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            with self.assertRaisesRegex(ValueError, "must stay under prompt_configs"):
                render_template("${file:../escape.md}", {}, file_root=Path(temp_dir))


class CursorOutputTests(unittest.TestCase):
    def test_extract_chat_id_from_stream_json_lines(self) -> None:
        parsed = parse_cursor_output('{"event":"start"}\n{"chatId":"chat-123"}\n', "stream-json")
        self.assertEqual(extract_chat_id(parsed), "chat-123")


class WorkflowValidationTests(unittest.TestCase):
    def test_merge_step_options_preserves_default_command_and_copies_extra_args(self) -> None:
        defaults = CursorOptions(command="cursor-agent", extra_args=["--foo"])
        merged = merge_step_options(defaults, StepConfig(name="demo", prompt="hi", command=""))

        self.assertEqual(merged.command, "cursor-agent")
        self.assertEqual(merged.extra_args, ["--foo"])
        self.assertIsNot(merged.extra_args, defaults.extra_args)

    def test_first_step_cannot_resume_without_previous_chat(self) -> None:
        workflow = WorkflowConfig(
            name="demo",
            steps=[StepConfig(name="resume", prompt="hello", resume_from_previous=True)],
        )

        messages = validate_workflow(workflow, Path("workflow.json"))

        self.assertTrue(any("cannot use resume_from_previous" in message for message in messages))

    def test_validate_can_skip_prompt_requirement_for_templates(self) -> None:
        workflow = WorkflowConfig(
            name="template",
            steps=[StepConfig(name="implement")],
        )

        messages = validate_workflow(workflow, Path("workflow.json"), require_prompts=False)

        self.assertEqual(messages, [])

    def test_validate_reports_invalid_prompt_file_reference(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            workflow_path = root / "workflows" / "workflow.json"
            workflow_path.parent.mkdir()
            workflow = WorkflowConfig(
                name="template",
                steps=[StepConfig(name="implement", prompt="${file:missing.md}")],
            )

            messages = validate_workflow(workflow, workflow_path)

            self.assertTrue(any("Prompt file does not exist" in message for message in messages))

    def test_output_path_uses_run_name_when_available(self) -> None:
        workflow = WorkflowConfig(
            name="template",
            run_name="task-instance",
            steps=[StepConfig(name="implement", prompt="hello")],
        )
        workflow_dir = (Path(__file__).resolve().parent.parent / "workflows").resolve()

        output_path = resolve_output_path(
            workflow,
            workflow_dir,
            workflow.steps[0],
            1,
            "text",
        )

        self.assertIn("task-instance", str(output_path))
        self.assertTrue(str(output_path).endswith("01-implement.md"))


if __name__ == "__main__":
    unittest.main()
