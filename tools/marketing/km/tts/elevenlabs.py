"""ElevenLabs: the with-timestamps endpoint, word timings from its character alignment.

Needs ELEVENLABS_API_KEY (.env) and a voice id per role in brand.json (`elevenlabs`).
"""
from __future__ import annotations

import base64
import time
from pathlib import Path

from .. import env
from . import Spoken, TtsError, speakable
from .wav import duration, write_pcm

URL = "https://api.elevenlabs.io/v1/text-to-speech/{voice}/with-timestamps"
MODEL = "eleven_multilingual_v2"
RATE = 44100


def words_from_alignment(alignment: dict) -> list[dict]:
    """Groups characters into words (split on spaces), each from its first character's start to its last's end."""
    chars = alignment.get("characters") or []
    starts = alignment.get("character_start_times_seconds") or []
    ends = alignment.get("character_end_times_seconds") or []
    words: list[dict] = []
    current = ""
    begin = end = 0.0
    for ch, s, e in zip(chars, starts, ends):
        if ch.isspace():
            if current:
                words.append({"text": current, "start": round(begin, 3), "end": round(end, 3)})
                current = ""
            continue
        if not current:
            begin = s
        current += ch
        end = e
    if current:
        words.append({"text": current, "start": round(begin, 3), "end": round(end, 3)})
    return words


class ElevenLabs:
    name = "elevenlabs"

    def __init__(self) -> None:
        import httpx

        self.httpx = httpx
        self.key = env.need("ELEVENLABS_API_KEY", "the ElevenLabs voice")

    def speak(self, text: str, lang: str, voice: dict, out: Path) -> Spoken:
        voice_id = voice.get("elevenlabs")
        if not voice_id:
            raise TtsError("brand.json has no ElevenLabs voice id for this role")
        body = {"text": speakable(text), "model_id": MODEL, "language_code": lang}
        for attempt in range(4):
            r = self.httpx.post(URL.format(voice=voice_id), params={"output_format": f"pcm_{RATE}"},
                                headers={"xi-api-key": self.key}, json=body, timeout=120)
            if r.status_code == 429 and attempt < 3:
                time.sleep(10 * (attempt + 1))
                continue
            if r.status_code == 401:
                raise TtsError("ElevenLabs refused the key (ELEVENLABS_API_KEY)")
            if r.status_code >= 400:
                raise TtsError(f"ElevenLabs answered {r.status_code}: {r.text[:300]}")
            data = r.json()
            write_pcm(out, base64.b64decode(data["audio_base64"]), RATE)
            words = words_from_alignment(data.get("alignment") or {})
            return Spoken(out, duration(out), words, self.name, voice_id)
        raise TtsError("ElevenLabs kept answering 'too many requests'")
