#!/usr/bin/env python3
"""The pipeline's schedule without n8n running: one macOS LaunchAgent per workflow.

The times come from the workflows' Schedule nodes (n8n/workflows/*.json), so n8n and launchd keep
one schedule. Each agent runs `job.sh <job>` and nothing stays in memory between runs. A calendar
time the Mac slept through runs once when it wakes (launchd coalesces the missed ones); the
every-few-minutes jobs simply carry on after a wake.

    python3 n8n/launchd.py show                    what would be scheduled, and when
    python3 n8n/launchd.py install --logs <dir>    write and load the agents (com.tokenlimit.kehai.job.*)
    python3 n8n/launchd.py remove                  unload and delete them

setup.sh runs it (`setup.sh --launchd`); the standard library only, so the Mac's own python3 will do.
"""
from __future__ import annotations

import argparse
import json
import os
import plistlib
import re
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
TOOLS = HERE.parent
PREFIX = "com.tokenlimit.kehai.job."
AGENTS = Path.home() / "Library" / "LaunchAgents"
# The quick jobs run at background priority; the ones that render keep the normal one.
LIGHT = {"telegram", "publish", "housekeeping", "report"}
FIELDS = (("Minute", 0, 59), ("Hour", 0, 23), ("Day", 1, 31), ("Month", 1, 12), ("Weekday", 0, 7))


class BadSchedule(ValueError):
    pass


def values(field: str, low: int, high: int) -> list[int] | None:
    """One cron field as the numbers it allows, or None for every value."""
    if field == "*":
        return None
    out: set[int] = set()
    for part in field.split(","):
        m = re.fullmatch(r"(\*|\d+)(?:-(\d+))?(?:/(\d+))?", part)
        if not m:
            raise BadSchedule(f"can't read the cron field {field!r}")
        start = low if m.group(1) == "*" else int(m.group(1))
        end = int(m.group(2)) if m.group(2) else (high if m.group(1) == "*" or m.group(3) else start)
        step = int(m.group(3) or 1)
        if not (low <= start <= end <= high) or step < 1:
            raise BadSchedule(f"the cron field {field!r} is out of range {low}-{high}")
        out.update(range(start, end + 1, step))
    return sorted(out)


def calendar(expression: str) -> list[dict]:
    """A 5-field cron expression (n8n's 6-field form with seconds is accepted too) as
    StartCalendarInterval entries."""
    parts = expression.split()
    if len(parts) == 6:
        if parts[0] not in ("0", "*"):
            raise BadSchedule(f"{expression!r}: launchd can't run at a given second")
        parts = parts[1:]
    if len(parts) != 5:
        raise BadSchedule(f"{expression!r} isn't a cron expression")
    entries: list[dict] = [{}]
    for (key, low, high), field in zip(FIELDS, parts):
        allowed = values(field, low, high)
        if allowed is None:
            continue
        entries = [{**e, key: v} for e in entries for v in allowed]
    return entries


def schedule(params: dict) -> dict:
    """An n8n Schedule node's parameters as launchd keys."""
    rules = params.get("rule", {}).get("interval", [])
    if len(rules) != 1:
        raise BadSchedule("one rule per Schedule node, please")
    rule = rules[0]
    if rule.get("field") == "cronExpression":
        return {"StartCalendarInterval": calendar(rule["expression"])}
    if rule.get("field") == "minutes":
        return {"StartInterval": 60 * int(rule.get("minutesInterval", 1))}
    if rule.get("field") == "hours":
        return {"StartInterval": 3600 * int(rule.get("hoursInterval", 1))}
    raise BadSchedule(f"a {rule.get('field')!r} schedule isn't supported here")


def jobs(workflows: Path = HERE / "workflows") -> list[tuple[str, dict, str]]:
    """(job, launchd schedule keys, workflow name) for every workflow."""
    out = []
    for f in sorted(workflows.glob("*.json")):
        w = json.loads(f.read_text(encoding="utf-8"))
        trigger = next((n for n in w["nodes"] if n["type"] == "n8n-nodes-base.scheduleTrigger"), None)
        command = next((n["parameters"]["command"] for n in w["nodes"] if n["type"] == "n8n-nodes-base.executeCommand"), "")
        m = re.search(r"job\.sh\"?\s+([a-z-]+)", command)
        if not trigger or not m:
            raise BadSchedule(f"{f.name}: no Schedule node or no job.sh command")
        out.append((m.group(1), schedule(trigger["parameters"]), w["name"]))
    return out


def plist(job: str, keys: dict, logs: Path) -> dict:
    doc = {
        "Label": PREFIX + job,
        "ProgramArguments": ["/bin/bash", str(HERE / "job.sh"), job],
        "EnvironmentVariables": {"KEHAI_LOG_DIR": str(logs / "scheduled")},
        "RunAtLoad": False,
        # job.sh writes its own dated log; this file only catches what bash itself says.
        "StandardOutPath": str(logs / "scheduled" / "launchd.log"),
        "StandardErrorPath": str(logs / "scheduled" / "launchd.log"),
        **keys,
    }
    if job in LIGHT:
        doc["ProcessType"] = "Background"
    return doc


def when(keys: dict) -> str:
    if "StartInterval" in keys:
        n = keys["StartInterval"]
        return f"every {n // 60} min" if n % 3600 else f"every {n // 3600} h"
    days = "Sun Mon Tue Wed Thu Fri Sat Sun".split()
    out = []
    for e in keys["StartCalendarInterval"]:
        t = f"{e.get('Hour', '*')}:{e.get('Minute', 0):02d}" if "Hour" in e else f"hourly at :{e.get('Minute', 0):02d}"
        if "Weekday" in e:
            t = f"{days[e['Weekday']]} {t}"
        if "Day" in e:
            t = f"day {e['Day']} {t}"
        out.append(t)
    return ", ".join(out)


def mac_timezone() -> str:
    try:
        return os.readlink("/etc/localtime").split("zoneinfo/", 1)[1]
    except (OSError, IndexError):
        return "unknown"


def timezone_note() -> str | None:
    try:
        wanted = json.loads((TOOLS / "pipeline.json").read_text(encoding="utf-8"))["timezone"]
    except (OSError, KeyError, ValueError):
        return None
    mac = mac_timezone()
    if mac != wanted:
        return (f"note: pipeline.json's timezone is {wanted} but the Mac's is {mac}; launchd uses the Mac's, "
                "so the times above are in that zone")
    return None


def launchctl(*args: str) -> subprocess.CompletedProcess:
    return subprocess.run(["launchctl", *args], capture_output=True, text=True)


def installed() -> list[Path]:
    return sorted(AGENTS.glob(PREFIX + "*.plist"))


def remove() -> list[str]:
    done = []
    for path in installed():
        launchctl("bootout", f"gui/{os.getuid()}", str(path))
        path.unlink()
        done.append(path.stem.removeprefix(PREFIX))
    return done


def install(logs: Path) -> list[str]:
    planned = jobs()
    remove()
    (logs / "scheduled").mkdir(parents=True, exist_ok=True)
    AGENTS.mkdir(parents=True, exist_ok=True)
    lines = []
    for job, keys, _ in planned:
        path = AGENTS / f"{PREFIX}{job}.plist"
        with path.open("wb") as f:
            plistlib.dump(plist(job, keys, logs), f)
        r = launchctl("bootstrap", f"gui/{os.getuid()}", str(path))
        if r.returncode != 0:
            raise RuntimeError(f"launchctl couldn't load {path.name}: {r.stderr.strip() or r.returncode}")
        lines.append(f"{job}: {when(keys)}")
    return lines


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("what", choices=("show", "install", "remove"))
    ap.add_argument("--logs", type=Path, default=Path.home() / "TokenLimit" / "marketing" / "logs")
    a = ap.parse_args()
    try:
        if a.what == "show":
            lines = [f"{job}: {when(keys)}" for job, keys, _ in jobs()]
        elif a.what == "install":
            lines = install(a.logs.expanduser())
        else:
            gone = remove()
            lines = [f"removed {', '.join(gone)}" if gone else "no job agents were installed"]
    except (BadSchedule, RuntimeError) as e:
        print(f"launchd: {e}", file=sys.stderr)
        return 2
    for line in lines:
        print(line)
    if a.what != "remove":
        note = timezone_note()
        if note:
            print(note)
    return 0


if __name__ == "__main__":
    sys.exit(main())
