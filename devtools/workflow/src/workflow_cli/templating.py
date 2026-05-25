from __future__ import annotations

import re
from pathlib import Path


PLACEHOLDER_PATTERN = re.compile(r"\$\{([A-Za-z0-9_]+)\}")
FILE_PLACEHOLDER_PATTERN = re.compile(r"\$\{file:([^}]*)\}")
ESCAPED_PLACEHOLDER_OPEN = "__CURSOR_WORKFLOW_ESCAPED_PLACEHOLDER_OPEN__"


def render_template(text: str, variables: dict[str, str], *, file_root: Path | None = None) -> str:
    missing: set[str] = set()
    escaped_text = text.replace("$${", ESCAPED_PLACEHOLDER_OPEN)
    escaped_text = expand_file_references(escaped_text, file_root=file_root)
    escaped_text = escaped_text.replace("$${", ESCAPED_PLACEHOLDER_OPEN)

    def replace(match: re.Match[str]) -> str:
        key = match.group(1)
        if key not in variables:
            missing.add(key)
            return match.group(0)
        return variables[key]

    rendered = PLACEHOLDER_PATTERN.sub(replace, escaped_text)
    if missing:
        keys = ", ".join(sorted(missing))
        available = ", ".join(sorted(variables)) or "(none)"
        raise ValueError(f"Missing template variables: {keys}. Available variables: {available}")
    return rendered.replace(ESCAPED_PLACEHOLDER_OPEN, "${")


def expand_file_references(text: str, *, file_root: Path | None) -> str:
    def replace(match: re.Match[str]) -> str:
        raw_reference = match.group(1).strip()
        if not raw_reference:
            raise ValueError("${file:...} reference must include a markdown file path.")
        if file_root is None:
            raise ValueError("${file:...} references require a prompt config directory.")

        prompt_path = _resolve_prompt_file(file_root, raw_reference)
        if not prompt_path.exists():
            raise ValueError(f"Prompt file does not exist: {raw_reference}")
        if not prompt_path.is_file():
            raise ValueError(f"Prompt file is not a file: {raw_reference}")
        return prompt_path.read_text(encoding="utf-8")

    rendered = FILE_PLACEHOLDER_PATTERN.sub(replace, text)
    if "${file:" in rendered:
        raise ValueError("Malformed ${file:...} reference.")
    return rendered


def _resolve_prompt_file(file_root: Path, raw_reference: str) -> Path:
    reference_path = Path(raw_reference)
    if reference_path.is_absolute():
        raise ValueError(f"Prompt file reference must be relative: {raw_reference}")
    if reference_path.suffix.lower() != ".md":
        raise ValueError(f"Prompt file reference must point to a markdown file: {raw_reference}")

    root = file_root.resolve()
    resolved = (root / reference_path).resolve()
    if not resolved.is_relative_to(root):
        raise ValueError(f"Prompt file reference must stay under prompt_configs: {raw_reference}")
    return resolved
