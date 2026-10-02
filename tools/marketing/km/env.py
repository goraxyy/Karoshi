"""Secrets and settings from tools/marketing/.env (gitignored), under what the shell already set.

The file is KEY=VALUE lines; `#` starts a comment; quotes around a value are dropped. Nothing here
ever prints a value.
"""
from __future__ import annotations

import os
from pathlib import Path

from . import paths

ENV_FILE = paths.HERE / ".env"
_loaded = False


class Missing(Exception):
    """A key the step needs isn't set."""


def load(path: Path | None = None) -> None:
    """Reads .env once; a variable already in the environment wins."""
    global _loaded
    if _loaded and path is None:
        return
    file = path or ENV_FILE
    if file.is_file():
        for raw in file.read_text(encoding="utf-8").splitlines():
            line = raw.strip()
            if not line or line.startswith("#") or "=" not in line:
                continue
            key, _, value = line.partition("=")
            key = key.strip().removeprefix("export ").strip()
            value = value.strip()
            if len(value) >= 2 and value[0] == value[-1] and value[0] in "'\"":
                value = value[1:-1]
            if key and value and key not in os.environ:
                os.environ[key] = value
    if path is None:
        _loaded = True


def get(name: str, default: str | None = None) -> str | None:
    load()
    value = os.environ.get(name, "").strip()
    return value or default


def need(name: str, why: str) -> str:
    value = get(name)
    if not value:
        raise Missing(f"{name} isn't set, so {why} can't run: add it to tools/marketing/.env "
                      f"(copy .env.example), or use --dry-run")
    return value
