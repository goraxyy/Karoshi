"""Subtitles (SRT) from an edit's voice track: the word timings become lines of a few words, the
way they're said, for YouTube Studio."""
from __future__ import annotations

from pathlib import Path

MAX_CHARS = 42
MAX_SECONDS = 3.2


def _time(t: float) -> str:
    ms = int(round(t * 1000))
    h, ms = divmod(ms, 3_600_000)
    m, ms = divmod(ms, 60_000)
    s, ms = divmod(ms, 1000)
    return f"{h:02d}:{m:02d}:{s:02d},{ms:03d}"


def cues(edit: dict, lang: str) -> list[tuple[float, float, str]]:
    out = []
    for clip in sorted(edit.get("voice", {}).get(lang, []), key=lambda c: c["at"]):
        words = clip.get("words") or []
        if not words:
            continue
        line, start = [], None
        for i, w in enumerate(words):
            if start is None:
                start = clip["at"] + w["start"]
            line.append(w["text"])
            end = clip["at"] + w["end"]
            text = " ".join(line)
            nxt = words[i + 1] if i + 1 < len(words) else None
            breaks = (nxt is None or len(text) + 1 + len(nxt["text"]) > MAX_CHARS or end - start >= MAX_SECONDS
                      or text.endswith((".", "!", "?", "…")))
            if breaks:
                out.append((start, end, text))
                line, start = [], None
    return out


def srt(edit: dict, lang: str) -> str:
    return "".join(f"{n}\n{_time(a)} --> {_time(b)}\n{text}\n\n" for n, (a, b, text) in enumerate(cues(edit, lang), 1))


def write(edit: dict, lang: str, path: Path) -> Path:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(srt(edit, lang), encoding="utf-8")
    return path
