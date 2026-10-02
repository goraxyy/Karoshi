"""Making the videos: each step is the script that does it, run as its own process.

A short goes through `CHAIN` in order (shots → write → translate → voice → render → package →
present); every step looks at what's already on disk and does nothing if its part is done, so a
run that stopped halfway carries on where it was. What a step's exit code means for the video:

- 0: on to the next step;
- 75 (the heavy-job lock is busy) or 3 from a render (the Unity editor is open): try again on the
  next run, without counting it as a failure;
- 3 (a key isn't set up) or 4 (over the monthly spend or voice quota): blocked until the owner
  acts; the owner hears once a day;
- anything else: a failure; after `production.retries` of them the video waits for the owner.
"""
from __future__ import annotations

import datetime as dt
import json
import os
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path

from . import alerts, paths
from .settings import settings

CHAIN = ("shots", "write", "translate", "voice", "render", "package", "present")
LANGS = ("en", "ru")


@dataclass
class Result:
    code: int
    output: str

    @property
    def ok(self) -> bool:
        return self.code == 0


def log_file(root: Path) -> Path:
    return root / "logs" / "jobs" / f"{dt.date.today():%Y-%m-%d}.log"


def run(root: Path, args: list[str], label: str, timeout: int = 4 * 3600, extra_env: dict | None = None,
        cwd: Path | None = None) -> Result:
    """Runs one step, logging its output; returns its exit code and output."""
    environment = {**os.environ, "KEHAI_MARKETING": str(root), **(extra_env or {})}
    started = dt.datetime.now()
    try:
        r = subprocess.run(args, cwd=cwd or paths.HERE, env=environment, capture_output=True, text=True, timeout=timeout)
        code, out = r.returncode, (r.stdout + r.stderr).strip()
    except subprocess.TimeoutExpired as e:
        code, out = 124, f"timed out after {timeout} s: {e}"
    log = log_file(root)
    log.parent.mkdir(parents=True, exist_ok=True)
    with log.open("a", encoding="utf-8") as f:
        f.write(f"--- {started:%H:%M:%S} {label} (exit {code}, {(dt.datetime.now() - started).seconds} s)\n{out}\n")
    return Result(code, out)


def script(root: Path, name: str, *args: str, dry_run: bool = False, **kw) -> Result:
    cmd = [sys.executable, str(paths.HERE / name), *args, "--root", str(root)]
    if dry_run:
        cmd.append("--dry-run")
    return run(root, cmd, f"{name} {' '.join(args)}", **kw)


def render(root: Path, edit_path: Path, lang: str, dry_run: bool = False) -> Result:
    cmd = ["node", str(paths.HERE / "editor" / "render.mjs"), str(edit_path), "--lang", lang, "--root", str(root)]
    if dry_run:
        cmd.append("--dry-run")
    # From the editor's folder, where Remotion keeps its browser (a second copy elsewhere is 190 MB).
    return run(root, cmd, f"render {edit_path.parent.name} {lang}", cwd=paths.HERE / "editor")


def edit_path(root: Path, vid: str) -> Path:
    return root / "edits" / vid / "edit.json"


def draft_file(root: Path, vid: str, lang: str) -> Path:
    return root / "drafts" / f"{vid}.{lang}.mp4"


def newer(a: Path, b: Path) -> bool:
    return a.exists() and b.exists() and a.stat().st_mtime >= b.stat().st_mtime


def todo(root: Path, video: dict, plan: dict | None) -> str | None:
    """The first step of the chain still to do for a video, from what's on disk."""
    vid = video["id"]
    if plan is not None:
        renders = [r for r in plan["renders"] if r.get("pick") == video["pick"]]
        if any(not ((root / r["out"]).exists() and (root / r["out"]).with_suffix(".json").exists()) for r in renders):
            return "shots"
    path = edit_path(root, vid)
    if not path.exists():
        return "write"
    edit = json.loads(path.read_text(encoding="utf-8"))
    if "ru" not in edit["languages"]:
        return "translate"
    if edit.get("script") and set(edit.get("voice", {})) != set(edit["languages"]):
        return "voice"
    if any(not newer(draft_file(root, vid, l), path) for l in edit["languages"]):
        return "render"
    if not newer(path.parent / "package.json", path):
        return "package"
    if video["status"] in ("writing", "revising"):
        return "present"
    return None


def do(root: Path, step: str, video: dict, plan_path: Path | None, dry_run: bool = False) -> Result:
    vid = video["id"]
    if step == "shots":
        return script(root, "render_picks.py", str(plan_path), "--only", video["pick"], dry_run=dry_run,
                      extra_env={"KEHAI_LOCK_WAIT": "60"})
    if step == "write":
        return script(root, "write_short.py", "--picks", str(plan_path), "--pick", video["pick"], "--id", vid, dry_run=dry_run)
    if step == "translate":
        return script(root, "translate.py", vid, dry_run=dry_run)
    if step == "voice":
        return script(root, "voice.py", vid, dry_run=dry_run)
    if step == "render":
        path = edit_path(root, vid)
        langs = json.loads(path.read_text(encoding="utf-8"))["languages"]
        for lang in langs:
            if newer(draft_file(root, vid, lang), path):
                continue
            r = render(root, path, lang, dry_run)
            if not r.ok:
                return r
        return Result(0, "rendered")
    if step == "package":
        return script(root, "package.py", vid, dry_run=dry_run)
    raise ValueError(step)


def outcome(code: int, step: str) -> str:
    if code == 0:
        return "ok"
    if code == 75 or (code == 3 and step == "shots"):
        return "later"
    if code in (3, 4):
        return "blocked"
    return "failed"


def advance(root: Path, store, bot, video: dict, plan_path: Path | None, present, dry_run: bool = False) -> str:
    """Takes a video as far along the chain as it will go now. Returns what happened, in a few words."""
    plan = json.loads(plan_path.read_text(encoding="utf-8")) if plan_path else None
    done_steps = []
    while True:
        video = store.video(video["id"])
        step = todo(root, video, plan)
        if step is None:
            return f"{video['id']}: ready ({video['status']})" + (f" after {', '.join(done_steps)}" if done_steps else "")
        if step == "present":
            if dry_run:
                return f"{video['id']}: would send the preview"
            present(video)
            return f"{video['id']}: sent for approval after {', '.join(done_steps) or 'nothing new'}"
        store.update(video["id"], step=step)
        r = do(root, step, video, plan_path, dry_run)
        what = outcome(r.code, step)
        if dry_run:
            return f"{video['id']}: {step} (dry run, exit {r.code}): {r.output.splitlines()[-1] if r.output else ''}"
        if what == "ok":
            done_steps.append(step)
            store.update(video["id"], attempts=0, error=None)
            continue
        last = r.output.splitlines()[-1] if r.output else f"exit {r.code}"
        if what == "later":
            return f"{video['id']}: {step} waits ({last})"
        if what == "blocked":
            day = dt.date.today().isoformat()
            if store.get(f"blocked.{step}") != day:
                store.set(f"blocked.{step}", day)
                alerts.alert(root, "blocked", f"{video['id']} is waiting at {step}: {last}", video=video["id"], step=step)
            store.update(video["id"], error=last)
            return f"{video['id']}: {step} blocked ({last})"
        attempts = video["attempts"] + 1
        if attempts > settings()["production"]["retries"]:
            store.update(video["id"], status="failed", attempts=attempts, error=last)
            alerts.alert(root, "failed", f"{video['id']} failed at {step} {attempts} times: {last}. "
                         "It waits for you (/retry in Telegram).", video=video["id"], step=step)
        else:
            store.update(video["id"], attempts=attempts, error=last)
        return f"{video['id']}: {step} failed ({last})"

