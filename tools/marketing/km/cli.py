"""What every pipeline script shares: the exit codes, and turning known failures into them.

0 done (or a dry run) · 1 something unexpected · 2 bad input, or an answer that broke the rules
3 a key or tool isn't set up · 4 over the monthly spend or voice quota · 5 Claude declined
75 another heavy job holds the lock (try again later)
"""
from __future__ import annotations

import json
import sys
from pathlib import Path
from typing import Callable

OK, FAILED, BAD_INPUT, NOT_SET_UP, OVER_BUDGET, REFUSED, BUSY = 0, 1, 2, 3, 4, 5, 75


class BadInput(Exception):
    """The input files are missing or wrong."""


def run(main: Callable[[], int | None]) -> None:
    from . import env, lock
    from .llm.client import ApiError, Invalid, Refused
    from .llm.ledger import OverBudget
    from .tts import TtsError
    from .tts.usage import OverQuota

    name = Path(sys.argv[0]).stem
    try:
        code = main() or OK
    except BadInput as e:
        print(f"{name}: {e}", file=sys.stderr)
        code = BAD_INPUT
    except Invalid as e:
        print(f"{name}: {e}", file=sys.stderr)
        code = BAD_INPUT
    except env.Missing as e:
        print(f"{name}: {e}", file=sys.stderr)
        code = NOT_SET_UP
    except (OverBudget, OverQuota) as e:
        print(f"{name}: {e}", file=sys.stderr)
        code = OVER_BUDGET
    except Refused as e:
        print(f"{name}: {e}", file=sys.stderr)
        code = REFUSED
    except lock.Busy as e:
        print(f"{name}: {e}", file=sys.stderr)
        code = BUSY
    except (ApiError, TtsError) as e:
        print(f"{name}: {e}", file=sys.stderr)
        code = FAILED
    sys.exit(code)


def read_json(path: Path, what: str) -> dict:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except FileNotFoundError as e:
        raise BadInput(f"no {what} at {path}") from e
    except ValueError as e:
        raise BadInput(f"{path} isn't valid JSON: {e}") from e


def write_json(path: Path, data: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_suffix(path.suffix + ".tmp")
    tmp.write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    tmp.replace(path)
