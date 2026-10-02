"""tools/marketing/pipeline.json: when the pipeline produces and posts, where, what it keeps."""
from __future__ import annotations

import datetime as dt
import json
import os
from functools import lru_cache
from pathlib import Path
from zoneinfo import ZoneInfo

from . import paths, schemas

PIPELINE = paths.HERE / "pipeline.json"
DAYS = ("mon", "tue", "wed", "thu", "fri", "sat", "sun")


@lru_cache(maxsize=1)
def settings() -> dict:
    """pipeline.json, or the file KEHAI_PIPELINE names (for trial runs)."""
    path = Path(os.environ["KEHAI_PIPELINE"]) if os.environ.get("KEHAI_PIPELINE") else PIPELINE
    data = json.loads(path.read_text(encoding="utf-8"))
    problems = schemas.errors("pipeline", data)
    if problems:
        raise ValueError(f"{path} isn't valid: {problems[0]}")
    return data


def tz() -> ZoneInfo:
    return ZoneInfo(settings()["timezone"])


def now() -> dt.datetime:
    """The owner's local time (naive), which every date in the working folder uses."""
    return dt.datetime.now(tz()).replace(tzinfo=None)


def day_name(when: dt.date) -> str:
    return DAYS[when.weekday()]
