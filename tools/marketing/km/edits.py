"""Where an edit and everything about it lives, and its versions.

<root>/edits/<id>/
  edit.json            what the editor renders (schemas/edit.schema.json)
  draft.json           Claude's version of it (schemas/llm/draft.schema.json); revise.py works on this
  context.json         the shots and assets it was written with, for revising
  translations.json    {lang: {English: translation}}, so a revision only translates what changed
  package.json         titles, captions and posts per platform (package.py)
  versions/v001/…      every earlier edit.json and draft.json; undo puts the last one back
  shown/v001/…         each version the owner was shown in Telegram (↩️ there goes back to one)
"""
from __future__ import annotations

import datetime as dt
import json
import re
import shutil
from pathlib import Path

from . import schemas
from .cli import BadInput, read_json, write_json

ID = re.compile(r"^[a-z0-9][a-z0-9_-]{2,80}$")
VERSIONED = ("edit.json", "draft.json", "translations.json")


def folder(root: Path, edit_id: str) -> Path:
    if not ID.match(edit_id):
        raise BadInput(f"{edit_id!r} isn't an edit id (lowercase letters, digits, - and _)")
    return root / "edits" / edit_id


def resolve(root: Path, target: str) -> Path:
    """An edit.json from a path, an edit folder, or an id."""
    p = Path(target).expanduser()
    if p.is_file():
        return p.resolve()
    if p.is_dir() and (p / "edit.json").is_file():
        return (p / "edit.json").resolve()
    f = folder(root, target) / "edit.json"
    if f.is_file():
        return f
    raise BadInput(f"no edit at {target} (a path to edit.json, its folder, or an id under {root / 'edits'})")


def load(path: Path) -> dict:
    edit = read_json(path, "edit")
    problems = schemas.errors("edit", edit)
    if problems:
        raise BadInput(f"{path} isn't a valid edit: {problems[0]}")
    return edit


def save(path: Path, edit: dict) -> None:
    problems = schemas.errors("edit", edit)
    if problems:
        raise BadInput(f"the edit wouldn't be valid: {problems[0]}")
    write_json(path, edit)


def versions(edit_folder: Path) -> list[Path]:
    v = edit_folder / "versions"
    return sorted(p for p in v.iterdir() if p.is_dir() and re.fullmatch(r"v\d{3,}", p.name)) if v.is_dir() else []


def snapshot(edit_folder: Path, why: str) -> Path | None:
    """Keeps the current files as the next version before they change."""
    present = [n for n in VERSIONED if (edit_folder / n).exists()]
    if not present:
        return None
    existing = versions(edit_folder)
    n = int(existing[-1].name[1:]) + 1 if existing else 1
    target = edit_folder / "versions" / f"v{n:03d}"
    target.mkdir(parents=True)
    for name in present:
        shutil.copy2(edit_folder / name, target / name)
    (target / "why.txt").write_text(f"{dt.datetime.now().isoformat(timespec='seconds')} {why}\n", encoding="utf-8")
    return target


def undo(edit_folder: Path) -> Path:
    """Puts the latest version back (and drops it from the history)."""
    existing = versions(edit_folder)
    if not existing:
        raise BadInput(f"{edit_folder} has no earlier version")
    last = existing[-1]
    for name in VERSIONED:
        src = last / name
        if src.exists():
            shutil.copy(src, edit_folder / name)          # a fresh time: what was rendered from the other version is stale
        elif (edit_folder / name).exists() and name != "edit.json":
            (edit_folder / name).unlink()
    shutil.rmtree(last)
    return last


def remember_shown(edit_folder: Path, version: int) -> None:
    """Keeps the files of a version the owner was shown, so ↩️ can bring exactly that back."""
    target = edit_folder / "shown" / f"v{version:03d}"
    target.mkdir(parents=True, exist_ok=True)
    for name in VERSIONED:
        if (edit_folder / name).exists():
            shutil.copy2(edit_folder / name, target / name)


def shown(edit_folder: Path, version: int) -> Path | None:
    target = edit_folder / "shown" / f"v{version:03d}"
    return target if (target / "edit.json").exists() else None


def restore_shown(edit_folder: Path, version: int) -> None:
    """Puts back a version the owner was shown (keeping the current one as a version first)."""
    source = shown(edit_folder, version)
    if source is None:
        raise BadInput(f"{edit_folder.name} has no v{version} to go back to")
    snapshot(edit_folder, f"back to v{version}")
    for name in VERSIONED:
        if (source / name).exists():
            shutil.copy(source / name, edit_folder / name)   # a fresh time, so it renders again


def translations(edit_folder: Path) -> dict:
    path = edit_folder / "translations.json"
    return json.loads(path.read_text(encoding="utf-8")) if path.exists() else {}


def save_translations(edit_folder: Path, data: dict) -> None:
    write_json(edit_folder / "translations.json", data)
