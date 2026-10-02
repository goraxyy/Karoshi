"""Speaking an edit's script: each line in each language becomes a WAV with word timings, placed
on the video's clock as the edit's `voice`.

- A line's audio is kept beside a small JSON with what made it (backend, voice, text); the same
  line isn't spoken twice, so a revision only pays for the lines that changed.
- Lines that would overlap are moved later (a line starts at least GAP after the one before).
- One voice job at a time, behind the heavy-job lock; the backend's monthly quota is checked first.
"""
from __future__ import annotations

import datetime as dt
import hashlib
import json
from dataclasses import dataclass, field
from pathlib import Path

from . import lock, timeline, tts
from .tts import usage

GAP = 0.12


@dataclass
class Plan:
    lang: str
    backend: str
    lines: list[dict]                    # {id, speaker, at, text, voice, key, file, cached}
    chars: int = 0
    missing: list[str] = field(default_factory=list)   # line ids with no text in this language

    @property
    def to_speak(self) -> list[dict]:
        return [l for l in self.lines if not l["cached"]]


def text_in(text, lang: str) -> str | None:
    if isinstance(text, str):
        return text
    if isinstance(text, dict):
        return text.get(lang)
    return None


def line_key(backend: str, voice: dict, lang: str, text: str) -> str:
    blob = json.dumps({"backend": backend, "voice": voice, "lang": lang, "text": tts.speakable(text)},
                      sort_keys=True, ensure_ascii=False)
    return hashlib.sha256(blob.encode("utf-8")).hexdigest()[:20]


def audio_file(root: Path, edit_id: str, lang: str, line_id: str) -> Path:
    return root / "audio" / edit_id / lang / f"{line_id}.wav"


def plan(root: Path, edit: dict, lang: str, brand: dict, backend: str, force: bool = False) -> Plan:
    p = Plan(lang, backend, [])
    for line in edit.get("script", []):
        text = text_in(line["text"], lang)
        if not text:
            p.missing.append(line["id"])
            continue
        voice = tts.voice_for(brand, lang, line["speaker"])
        key = line_key(backend, voice, lang, text)
        wav = audio_file(root, edit["id"], lang, line["id"])
        cached = False
        if not force and wav.exists():
            try:
                cached = json.loads(wav.with_suffix(".json").read_text(encoding="utf-8")).get("key") == key
            except (OSError, ValueError):
                cached = False
        p.lines.append({**line, "text": text, "voice": voice, "key": key, "file": wav, "cached": cached})
    p.chars = sum(len(tts.speakable(l["text"])) for l in p.to_speak)
    return p


def speak(root: Path, edit: dict, p: Plan) -> None:
    """Speaks what isn't cached yet (behind the lock, within the quota)."""
    if not p.to_speak:
        return
    usage.check(root, p.backend, p.chars)
    engine = tts.backend(p.backend)
    with lock.heavy(root, f"voice {edit['id']} {p.lang}"):
        for line in p.to_speak:
            spoken = engine.speak(line["text"], p.lang, line["voice"], line["file"])
            line["file"].with_suffix(".json").write_text(json.dumps({
                "key": line["key"], "backend": spoken.backend, "voice": spoken.voice, "lang": p.lang,
                "text": tts.speakable(line["text"]), "duration": round(spoken.duration, 3), "words": spoken.words,
            }, ensure_ascii=False, indent=1), encoding="utf-8")
            usage.record(root, {"time": dt.datetime.now().isoformat(timespec="seconds"), "backend": spoken.backend,
                                "voice": spoken.voice, "lang": p.lang, "chars": len(tts.speakable(line["text"])),
                                "seconds": round(spoken.duration, 2), "ref": f"{edit['id']}/{line['id']}"})
            line["cached"] = True


def clips(root: Path, p: Plan) -> list[dict]:
    """The spoken lines as voice clips, in script order, at their written times."""
    out = []
    for line in p.lines:
        side = json.loads(line["file"].with_suffix(".json").read_text(encoding="utf-8"))
        out.append({"src": line["file"].relative_to(root).as_posix(), "at": round(float(line["at"]), 3),
                    "duration": side["duration"], "speaker": line["speaker"], "words": side["words"]})
    return out


def fit(clips_: list[dict], end: float) -> list[str]:
    """Moves overlapping lines later, in place; says what moved and anything left running past `end`."""
    notes = []
    ordered = sorted(clips_, key=lambda c: c["at"])
    prev_end = None
    for c in ordered:
        if prev_end is not None and c["at"] < prev_end + GAP:
            moved = round(prev_end + GAP, 3)
            notes.append(f"{Path(c['src']).stem} moved {moved - c['at']:.2f}s later (the line before runs to {prev_end:.2f}s)")
            c["at"] = moved
        prev_end = c["at"] + c["duration"]
    if ordered and prev_end is not None and prev_end > end + 0.05:
        notes.append(f"the voice runs {prev_end - end:.2f}s past the last scene ({prev_end:.2f}s against {end:.2f}s)")
    return notes


def apply(root: Path, edit: dict, p: Plan) -> list[str]:
    """Puts the language's clips into edit['voice'] and returns notes on what had to move."""
    voice_clips = clips(root, p)
    notes = fit(voice_clips, timeline.scenes_end(edit))
    edit.setdefault("voice", {})[p.lang] = voice_clips
    return notes


def overruns(notes: list[str]) -> bool:
    return any("past the last scene" in n for n in notes)
