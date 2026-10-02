"""Text to speech for the voice-over: one interface, three backends.

- `azure`: Azure Speech neural voices (the F0 free tier), word timings from WordBoundary events.
- `elevenlabs`: ElevenLabs, word timings from its character alignment.
- `say`: the macOS voices, a stand-in that needs no key; word timings are estimated from where
  the audio is silent.

Voices per language and role come from brand.json (`voices`). Every backend writes a 16-bit
mono WAV and returns its length and word timings (seconds into the clip).
"""
from __future__ import annotations

import re
from dataclasses import dataclass, field
from pathlib import Path

BACKENDS = ("azure", "elevenlabs", "say")
LOCALES = {"en": "en-US", "ru": "ru-RU"}


class TtsError(Exception):
    """The backend couldn't speak the line."""


@dataclass
class Spoken:
    file: Path
    duration: float
    words: list[dict] = field(default_factory=list)
    backend: str = ""
    voice: str = ""


def speakable(text: str) -> str:
    """What is said: *stars* (crimson on screen) and runs of spaces dropped."""
    return re.sub(r"\s+", " ", text.replace("*", "")).strip()


def voice_for(brand: dict, lang: str, speaker: str) -> dict:
    try:
        return brand["voices"][lang][speaker]
    except KeyError as e:
        raise TtsError(f"brand.json has no {speaker} voice for {lang}") from e


def backend(name: str):
    if name == "azure":
        from .azure import Azure
        return Azure()
    if name == "elevenlabs":
        from .elevenlabs import ElevenLabs
        return ElevenLabs()
    if name == "say":
        from .say import Say
        return Say()
    raise TtsError(f"unknown backend {name!r} (one of {', '.join(BACKENDS)})")


def attach_punctuation(words: list[dict]) -> list[dict]:
    """Folds punctuation-only entries into the word before, so captions read 'there.' not 'there' '.'."""
    out: list[dict] = []
    for w in words:
        if out and not re.search(r"\w", w["text"]):
            out[-1] = {**out[-1], "text": out[-1]["text"] + w["text"], "end": max(out[-1]["end"], w["end"])}
        else:
            out.append(dict(w))
    return out
