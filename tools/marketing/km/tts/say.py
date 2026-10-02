"""macOS `say`: a stand-in voice that needs no key, for dry runs and the pipeline before Azure.

`say` gives no word timings, so they are estimated: the audio's silences mark where phrases
break, and each phrase's words share its stretch of speech by their length. Close enough for
captions on a draft; Azure's are exact.
"""
from __future__ import annotations

import re
import subprocess
from pathlib import Path

from . import Spoken, TtsError, speakable
from .wav import duration, voiced_spans

WPM = 180                   # say's usual rate


def _percent(value: str | None) -> float:
    m = re.fullmatch(r"([+-]\d{1,2})%", value or "+0%")
    return int(m.group(1)) / 100 if m else 0.0


def _weight(token: str) -> float:
    letters = len(re.sub(r"[^\w]", "", token))
    return 0.12 + 0.06 * max(1, letters)


def _spread(tokens: list[str], start: float, end: float) -> list[dict]:
    weights = [_weight(t) for t in tokens]
    total = sum(weights) or 1.0
    out, t = [], start
    for token, w in zip(tokens, weights):
        span = (end - start) * w / total
        out.append({"text": token, "start": round(t, 3), "end": round(t + span, 3)})
        t += span
    return out


def estimate_words(text: str, spans: list[tuple[float, float]], length: float) -> list[dict]:
    tokens = speakable(text).split()
    if not tokens:
        return []
    if not spans:
        spans = [(0.0, length)]
    phrases: list[list[str]] = [[]]
    for token in tokens:
        phrases[-1].append(token)
        if re.search(r"[,.;:!?…—]$", token):
            phrases.append([])
    phrases = [p for p in phrases if p]
    if len(phrases) > 1 and len(spans) >= len(phrases):
        # The widest silences between spans are taken to be the phrase breaks.
        gaps = sorted(range(1, len(spans)), key=lambda i: spans[i][0] - spans[i - 1][1], reverse=True)
        cuts = sorted(gaps[:len(phrases) - 1])
        groups, first = [], 0
        for cut in cuts + [len(spans)]:
            groups.append((spans[first][0], spans[cut - 1][1]))
            first = cut
        words: list[dict] = []
        for phrase, (a, b) in zip(phrases, groups):
            words += _spread(phrase, a, b)
        return words
    return _spread(tokens, spans[0][0], spans[-1][1])


class Say:
    name = "say"

    def speak(self, text: str, lang: str, voice: dict, out: Path) -> Spoken:
        name = voice.get("say")
        if not name:
            raise TtsError("brand.json has no macOS `say` voice for this role")
        out.parent.mkdir(parents=True, exist_ok=True)
        rate = int(WPM * (1 + _percent(voice.get("rate"))))
        words = speakable(text)
        try:
            subprocess.run(["say", "-v", name, "-r", str(rate), "-o", str(out), "--data-format=LEI16@48000", "--", words],
                           check=True, capture_output=True, text=True, timeout=120)
        except FileNotFoundError as e:
            raise TtsError("`say` is macOS only") from e
        except subprocess.CalledProcessError as e:
            raise TtsError(f"say failed: {e.stderr.strip() or e}") from e
        length = duration(out)
        return Spoken(out, length, estimate_words(text, voiced_spans(out, min_gap=0.12), length), self.name, name)
