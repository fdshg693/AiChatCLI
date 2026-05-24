from __future__ import annotations

from typing import Any


def expect_str(value: Any, field_name: str) -> str:
    if not isinstance(value, str) or not value.strip():
        raise ValueError(f"{field_name} must be a non-empty string.")
    return value


def expect_optional_str(value: Any, field_name: str) -> str | None:
    if value is None:
        return None
    if not isinstance(value, str):
        raise ValueError(f"{field_name} must be a string.")
    return value


def expect_int(value: Any, field_name: str) -> int:
    if not isinstance(value, int):
        raise ValueError(f"{field_name} must be an integer.")
    return value


def expect_bool(value: Any, field_name: str) -> bool:
    if not isinstance(value, bool):
        raise ValueError(f"{field_name} must be a boolean.")
    return value


def expect_optional_bool(value: Any, field_name: str) -> bool | None:
    if value is None:
        return None
    return expect_bool(value, field_name)


def expect_string_dict(value: Any, field_name: str) -> dict[str, str]:
    if not isinstance(value, dict):
        raise ValueError(f"{field_name} must be an object.")

    result: dict[str, str] = {}
    for key, item in value.items():
        if not isinstance(key, str) or not isinstance(item, str):
            raise ValueError(f"{field_name} must contain string keys and string values.")
        result[key] = item
    return result


def expect_string_list(value: Any, field_name: str) -> list[str]:
    if not isinstance(value, list):
        raise ValueError(f"{field_name} must be an array.")
    if not all(isinstance(item, str) for item in value):
        raise ValueError(f"{field_name} must contain strings only.")
    return list(value)


def expect_optional_string_list(value: Any, field_name: str) -> list[str] | None:
    if value is None:
        return None
    return expect_string_list(value, field_name)
