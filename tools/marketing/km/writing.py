"""What the steps that write videos share (write_short, revise, long_video): the user turn they
send, and saving a draft as an edit with its history."""
from __future__ import annotations

import json
from pathlib import Path

from . import drafts, edits
from .cli import write_json
from .llm import prompts


def recent_hooks(root: Path, n: int = 12) -> list[str]:
    """The last few shorts' hooks, so a new one doesn't repeat them."""
    found = []
    for f in (root / "edits").glob("*/draft.json"):
        try:
            d = json.loads(f.read_text(encoding="utf-8"))
            found.append((f.stat().st_mtime, d["hook"]))
        except (OSError, ValueError, KeyError):
            continue
    return [h for _, h in sorted(found, reverse=True)[:n]]


def unique_id(root: Path, wanted: str) -> str:
    edit_id, n = wanted, 1
    while (root / "edits" / edit_id).exists():
        n += 1
        edit_id = f"{wanted}-{n}"
    return edit_id


def material(ctx: dict) -> str:
    """The shots and assets as the user turn shows them (paths left out: ids are what drafts use)."""
    shots = [{k: v for k, v in s.items() if k not in ("src", "track")} for s in ctx["shots"]]
    assets = [{k: v for k, v in a.items() if k != "file"} for a in ctx["assets"]]
    return "\n\n".join([prompts.data_block("shots", shots), prompts.data_block("assets", assets)])


def save(root: Path, draft: dict, ctx: dict, why: str) -> tuple[Path, set[str]]:
    """Writes draft.json, context.json and edit.json (keeping the old ones as a version).
    Returns the edit's path and the languages whose translations are missing now."""
    folder = edits.folder(root, draft["id"])
    if (folder / "edit.json").exists():
        edits.snapshot(folder, why)
    translations = edits.translations(folder)
    edit, missing = drafts.to_edit(draft, ctx, translations)
    folder.mkdir(parents=True, exist_ok=True)
    write_json(folder / "draft.json", draft)
    write_json(folder / "context.json", ctx)
    path = folder / "edit.json"
    edits.save(path, edit)
    return path, missing
