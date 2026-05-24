from __future__ import annotations

import json
from typing import Any

CHAT_ID_KEYS = ("chatId", "chat_id", "conversationId", "conversation_id")


def parse_cursor_output(stdout: str, output_format: str) -> Any:
    if output_format == "json":
        try:
            return json.loads(stdout)
        except json.JSONDecodeError:
            return None

    if output_format == "stream-json":
        parsed_lines = []
        for line in stdout.splitlines():
            if not line.strip():
                continue
            try:
                parsed_lines.append(json.loads(line))
            except json.JSONDecodeError:
                return None
        return parsed_lines

    return None


def extract_chat_id(parsed_output: Any) -> str | None:
    if isinstance(parsed_output, dict):
        return _extract_chat_id_from_mapping(parsed_output)

    if isinstance(parsed_output, list):
        for item in parsed_output:
            if isinstance(item, dict):
                chat_id = _extract_chat_id_from_mapping(item)
                if chat_id:
                    return chat_id

    return None


def _extract_chat_id_from_mapping(parsed_output: dict[str, Any]) -> str | None:
    for key in CHAT_ID_KEYS:
        value = parsed_output.get(key)
        if isinstance(value, str) and value:
            return value
    return None
