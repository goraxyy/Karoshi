"""Every Claude call's tokens and cost, in <root>/logs/llm_costs.csv, and the monthly cap.

The cap (KEHAI_LLM_MONTHLY_USD, default $15) is checked before each call against what this
calendar month has cost so far plus a high estimate of the call; past it, nothing is sent and the
owner is told. The Console workspace's own spend limit is the hard stop behind this one.
"""
from __future__ import annotations

import csv
import datetime as dt
import json
from pathlib import Path

from .. import alerts, env

COLUMNS = ["time", "step", "ref", "model", "request_id", "input_tokens", "output_tokens",
           "cache_write_tokens", "cache_read_tokens", "usd", "stop_reason", "note"]
WARN_AT = 0.8


class OverBudget(Exception):
    """This month's Claude spend would pass the cap."""


def ledger_file(root: Path) -> Path:
    return root / "logs" / "llm_costs.csv"


def cap() -> float:
    return float(env.get("KEHAI_LLM_MONTHLY_USD", "15") or 15)


def record(root: Path, row: dict) -> None:
    path = ledger_file(root)
    path.parent.mkdir(parents=True, exist_ok=True)
    new = not path.exists()
    with path.open("a", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=COLUMNS, extrasaction="ignore")
        if new:
            w.writeheader()
        w.writerow({k: row.get(k, "") for k in COLUMNS})


def rows(root: Path) -> list[dict]:
    path = ledger_file(root)
    if not path.exists():
        return []
    with path.open(newline="", encoding="utf-8") as f:
        return list(csv.DictReader(f))


def spent(root: Path, since: dt.datetime, until: dt.datetime | None = None) -> float:
    total = 0.0
    for r in rows(root):
        try:
            t = dt.datetime.fromisoformat(r["time"])
            usd = float(r["usd"] or 0)
        except (KeyError, ValueError):
            continue
        if t >= since and (until is None or t < until):
            total += usd
    return round(total, 6)


def month_start(now: dt.datetime | None = None) -> dt.datetime:
    now = now or dt.datetime.now()
    return now.replace(day=1, hour=0, minute=0, second=0, microsecond=0)


def month_spent(root: Path, now: dt.datetime | None = None) -> float:
    start = month_start(now)
    return spent(root, start, (start + dt.timedelta(days=32)).replace(day=1))


def _state(root: Path) -> tuple[Path, dict]:
    path = root / "state" / "llm_budget.json"
    try:
        return path, json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return path, {}


def check(root: Path, estimate: float, step: str) -> None:
    """Refuses a call that would take this month past the cap."""
    so_far = month_spent(root)
    limit = cap()
    if so_far + estimate > limit:
        month = month_start().strftime("%Y-%m")
        path, state = _state(root)
        if state.get("stopped") != month:
            alerts.alert(root, "llm_budget", f"Claude steps stopped for {month}: ${so_far:.2f} spent of the "
                         f"${limit:.2f} monthly cap; {step} would cost about ${estimate:.2f}.",
                         spent=so_far, cap=limit, step=step)
            state["stopped"] = month
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(json.dumps(state), encoding="utf-8")
        raise OverBudget(f"this month's Claude spend is ${so_far:.2f} of ${limit:.2f}; {step} (about "
                         f"${estimate:.2f}) would pass the cap. It resets on the 1st, or raise "
                         f"KEHAI_LLM_MONTHLY_USD in .env (and the Console limit)")


def after_call(root: Path) -> None:
    """Warns once a month when the spend passes 80% of the cap."""
    so_far = month_spent(root)
    limit = cap()
    if so_far < limit * WARN_AT:
        return
    month = month_start().strftime("%Y-%m")
    path, state = _state(root)
    if state.get("warned") == month:
        return
    alerts.alert(root, "llm_budget", f"Claude steps have used ${so_far:.2f} of the ${limit:.2f} cap for {month}.",
                 spent=so_far, cap=limit)
    state["warned"] = month
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(state), encoding="utf-8")
