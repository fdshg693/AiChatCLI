from __future__ import annotations

from pathlib import Path

__all__ = ["__version__"]

__version__ = "0.1.0"

# Allow `python -m workflow_cli` to work from a source checkout
# without requiring installation or PYTHONPATH tweaks.
_source_package_dir = Path(__file__).resolve().parent.parent / "src" / "workflow_cli"
if _source_package_dir.is_dir():
    __path__.append(str(_source_package_dir))
