"""Things the owner must hear about (the spend cap reached, a refusal, a quota used up).

Appended to <root>/state/alerts.jsonl; Phase 7 sends the unsent ones to Telegram. Until then
they are printed to stderr as well.
"""
from __future__ import annotations

import datetime as dt
import json
import sys
from pathlib import Path


def alerts_file(root: Path) -> Path:
    return root / "state" / "alerts.jsonl"


def alert(root: Path, kind: str, message: str, **data: object) -> None:
    entry = {"time": dt.datetime.now().isoformat(timespec="seconds"), "kind": kind, "message": message,
             "data": data, "sent": False}
    path = alerts_file(root)
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("a", encoding="utf-8") as f:
        f.write(json.dumps(entry, ensure_ascii=False) + "\n")
    print(f"ALERT ({kind}): {message}", file=sys.stderr)


def pending(root: Path) -> list[dict]:
    path = alerts_file(root)
    if not path.exists():
        return []
    out = []
    for line in path.read_text(encoding="utf-8").splitlines():
        if line.strip():
            entry = json.loads(line)
            if not entry.get("sent"):
                out.append(entry)
    return out
