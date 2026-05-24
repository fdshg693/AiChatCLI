from __future__ import annotations

import re


PLACEHOLDER_PATTERN = re.compile(r"\$\{([A-Za-z0-9_]+)\}")
ESCAPED_PLACEHOLDER_OPEN = "__CURSOR_WORKFLOW_ESCAPED_PLACEHOLDER_OPEN__"


def render_template(text: str, variables: dict[str, str]) -> str:
    missing: set[str] = set()
    escaped_text = text.replace("$${", ESCAPED_PLACEHOLDER_OPEN)

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
