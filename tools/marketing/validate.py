#!/usr/bin/env python3
"""Checks files against the pipeline's JSON Schemas (tools/marketing/schemas).

    uv run validate.py <file.json>... [--kind edit|markers|shot|shot-path|brand|manifest] [--files] [--root <folder>]

The kind is worked out from the file's name or shape if not given. With --files, an edit's
files must all be in the working folder too. Exit codes: 0 all valid · 1 something isn't.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from km import paths, schemas


def main(argv: list[str] | None = None) -> int:
    p = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    p.add_argument("files", nargs="+", type=Path)
    p.add_argument("--kind", choices=sorted(schemas.KINDS))
    p.add_argument("--files", dest="check_files", action="store_true", help="an edit's files must exist in the working folder")
    p.add_argument("--root", default=None)
    a = p.parse_args(argv)
    root = paths.root(a.root)

    bad = 0
    for f in a.files:
        try:
            data = json.loads(f.read_text(encoding="utf-8"))
        except (OSError, ValueError) as e:
            print(f"{f}: can't read: {e}")
            bad += 1
            continue
        kind = a.kind or schemas.guess_kind(f, data)
        if kind is None:
            print(f"{f}: which schema? give --kind")
            bad += 1
            continue
        problems = schemas.errors(kind, data)
        if not problems and kind == "edit" and a.check_files:
            problems = [f"missing file: {x}" for x in schemas.edit_files(data) if not (root / x).exists()]
        if problems:
            bad += 1
            print(f"{f}: not a valid {kind}:")
            for line in problems[:20]:
                print(f"  {line}")
        else:
            print(f"{f}: valid {kind}")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
