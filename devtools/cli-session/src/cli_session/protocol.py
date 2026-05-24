from __future__ import annotations

import json
import os
import tempfile
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Any

DEFAULT_READY_SUFFIX = "エージェント> "
DEFAULT_APPROVAL_SUFFIX = "実行しますか? YES/NO: "
DEFAULT_DENIAL_REASON_SUFFIX = "NO の理由 (任意): "
DEFAULT_TIMEOUT_SECONDS = 30.0


@dataclass(frozen=True)
class PromptPatterns:
    ready_suffix: str = DEFAULT_READY_SUFFIX
    approval_suffix: str = DEFAULT_APPROVAL_SUFFIX
    denial_reason_suffix: str = DEFAULT_DENIAL_REASON_SUFFIX

    @classmethod
    def from_request(cls, request: dict[str, Any]) -> "PromptPatterns":
        return cls(
            ready_suffix=str(request.get("ready_suffix") or DEFAULT_READY_SUFFIX),
            approval_suffix=str(request.get("approval_suffix") or DEFAULT_APPROVAL_SUFFIX),
            denial_reason_suffix=str(request.get("denial_reason_suffix") or DEFAULT_DENIAL_REASON_SUFFIX),
        )

    def to_dict(self) -> dict[str, str]:
        return asdict(self)


@dataclass(frozen=True)
class ManagerEndpoint:
    host: str
    port: int
    token: str
    pid: int

    @classmethod
    def from_file(cls, path: Path) -> "ManagerEndpoint":
        data = json.loads(path.read_text(encoding="utf-8"))
        return cls(
            host=str(data["host"]),
            port=int(data["port"]),
            token=str(data["token"]),
            pid=int(data["pid"]),
        )

    def write(self, path: Path) -> None:
        path.write_text(
            json.dumps(asdict(self), ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )


def resolve_state_dir(state_dir: Path | None) -> Path:
    if state_dir is not None:
        return state_dir.expanduser().resolve()

    configured = os.environ.get("CLI_SESSION_STATE_DIR")
    if configured:
        return Path(configured).expanduser().resolve()

    return Path(tempfile.gettempdir()) / "aichatcli-cli-session"


def endpoint_file(state_dir: Path) -> Path:
    return state_dir / "manager.json"


def json_line(data: dict[str, Any]) -> bytes:
    return (json.dumps(data, ensure_ascii=False) + "\n").encode("utf-8")
