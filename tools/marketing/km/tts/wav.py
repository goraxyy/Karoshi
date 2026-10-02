"""Small WAV helpers (16-bit PCM): write raw PCM as a WAV, read a file's length, find where speech is."""
from __future__ import annotations

import array
import math
import sys
import wave
from pathlib import Path


def write_pcm(path: Path, pcm: bytes, rate: int, channels: int = 1) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "wb") as w:
        w.setnchannels(channels)
        w.setsampwidth(2)
        w.setframerate(rate)
        w.writeframes(pcm)


def duration(path: Path) -> float:
    with wave.open(str(path), "rb") as w:
        return w.getnframes() / float(w.getframerate())


def levels(path: Path, window: float = 0.01) -> tuple[list[float], float]:
    """RMS per window (0..1) and the window length, from a 16-bit WAV (channels mixed)."""
    with wave.open(str(path), "rb") as w:
        if w.getsampwidth() != 2:
            raise ValueError(f"{path}: only 16-bit PCM WAV is read here")
        rate, channels = w.getframerate(), w.getnchannels()
        samples = array.array("h", w.readframes(w.getnframes()))
    if sys.byteorder == "big":
        samples.byteswap()
    step = max(1, int(rate * window)) * channels
    out = []
    for i in range(0, len(samples), step):
        chunk = samples[i:i + step]
        if not chunk:
            break
        out.append(math.sqrt(sum(s * s for s in chunk) / len(chunk)) / 32768.0)
    return out, step / channels / rate


def voiced_spans(path: Path, min_gap: float = 0.08, floor: float = 0.02) -> list[tuple[float, float]]:
    """Stretches of speech, separated by silences of at least `min_gap` seconds."""
    rms, win = levels(path)
    if not rms:
        return []
    threshold = max(max(rms) * floor, 1e-4)
    spans: list[tuple[float, float]] = []
    start = None
    quiet = 0
    for i, v in enumerate(rms):
        if v >= threshold:
            if start is None:
                start = i
            quiet = 0
        elif start is not None:
            quiet += 1
            if quiet * win >= min_gap:
                spans.append((start * win, (i - quiet + 1) * win))
                start, quiet = None, 0
    if start is not None:
        spans.append((start * win, (len(rms) - quiet) * win))
    return spans
