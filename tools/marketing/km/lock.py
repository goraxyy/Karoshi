"""The heavy-job lock: one Unity render, Remotion render or voice job at a time (8 GB of RAM).

The same lock render_shot.sh and editor/render.mjs take: the directory <root>/state/heavy.lock
holding `pid` (its owner) and `job` (what it's doing). A lock whose owner has died is taken over.
A job that finds it held waits KEHAI_LOCK_WAIT seconds (default 0) and then gives up: exit 75.
"""
from __future__ import annotations

import os
import shutil
import time
from contextlib import contextmanager
from pathlib import Path

BUSY_EXIT = 75


class Busy(Exception):
    """Another heavy job holds the lock."""


def _alive(pid: int) -> bool:
    if pid <= 0:
        return False
    try:
        os.kill(pid, 0)
    except ProcessLookupError:
        return False
    except PermissionError:
        return True
    return True


def _take(lock: Path, job: str) -> bool:
    lock.parent.mkdir(parents=True, exist_ok=True)
    try:
        lock.mkdir()
    except FileExistsError:
        try:
            holder = int((lock / "pid").read_text().strip() or 0)
        except (OSError, ValueError):
            holder = 0
        if holder and _alive(holder):
            return False
        if not holder:
            # Just made by someone who hasn't written its pid yet: give it a moment.
            age = time.time() - lock.stat().st_mtime
            if age < 5:
                return False
        shutil.rmtree(lock, ignore_errors=True)    # its owner is gone
        try:
            lock.mkdir()
        except FileExistsError:
            return False
    (lock / "pid").write_text(str(os.getpid()))
    (lock / "job").write_text(job)
    return True


def holder(root: Path) -> str:
    try:
        return (root / "state" / "heavy.lock" / "job").read_text().strip()
    except OSError:
        return "unknown"


@contextmanager
def heavy(root: Path, job: str, wait: float | None = None):
    lock = root / "state" / "heavy.lock"
    if wait is None:
        wait = float(os.environ.get("KEHAI_LOCK_WAIT", "0") or 0)
    waited = 0.0
    while not _take(lock, job):
        if waited >= wait:
            raise Busy(f"another heavy job is running ({holder(root)}); try again later")
        time.sleep(5)
        waited += 5
    try:
        yield
    finally:
        try:
            if int((lock / "pid").read_text().strip()) == os.getpid():
                shutil.rmtree(lock, ignore_errors=True)
        except (OSError, ValueError):
            pass
