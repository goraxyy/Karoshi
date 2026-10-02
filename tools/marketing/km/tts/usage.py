"""Characters spoken per backend, in <root>/logs/tts_usage.csv, and the monthly quotas.

Azure's free tier (F0) gives about 0.5M neural characters a month (KEHAI_AZURE_MONTHLY_CHARS);
ElevenLabs' free plan 10k (KEHAI_ELEVENLABS_MONTHLY_CHARS). `say` is free and unlimited.
"""
from __future__ import annotations

import csv
import datetime as dt
from pathlib import Path

from .. import alerts, env

COLUMNS = ["time", "backend", "voice", "lang", "chars", "seconds", "ref"]
QUOTAS = {"azure": ("KEHAI_AZURE_MONTHLY_CHARS", 500_000), "elevenlabs": ("KEHAI_ELEVENLABS_MONTHLY_CHARS", 10_000)}


class OverQuota(Exception):
    """This month's characters for the backend would pass its quota."""


def usage_file(root: Path) -> Path:
    return root / "logs" / "tts_usage.csv"


def record(root: Path, row: dict) -> None:
    path = usage_file(root)
    path.parent.mkdir(parents=True, exist_ok=True)
    new = not path.exists()
    with path.open("a", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=COLUMNS, extrasaction="ignore")
        if new:
            w.writeheader()
        w.writerow({k: row.get(k, "") for k in COLUMNS})


def month_chars(root: Path, backend: str, now: dt.datetime | None = None) -> int:
    path = usage_file(root)
    if not path.exists():
        return 0
    start = (now or dt.datetime.now()).replace(day=1, hour=0, minute=0, second=0, microsecond=0)
    end = (start + dt.timedelta(days=32)).replace(day=1)
    total = 0
    with path.open(newline="", encoding="utf-8") as f:
        for r in csv.DictReader(f):
            try:
                if r["backend"] == backend and start <= dt.datetime.fromisoformat(r["time"]) < end:
                    total += int(r["chars"] or 0)
            except (KeyError, ValueError):
                continue
    return total


def quota(backend: str) -> int | None:
    if backend not in QUOTAS:
        return None
    name, default = QUOTAS[backend]
    return int(env.get(name, str(default)) or default)


def check(root: Path, backend: str, chars: int) -> None:
    limit = quota(backend)
    if limit is None:
        return
    used = month_chars(root, backend)
    if used + chars > limit:
        alerts.alert(root, "tts_quota", f"{backend} voice stopped: {used:,} of {limit:,} characters used this month, "
                     f"{chars:,} more needed.", backend=backend, used=used, quota=limit)
        raise OverQuota(f"{backend}: {used:,} of {limit:,} characters used this month; {chars:,} more would pass it")
