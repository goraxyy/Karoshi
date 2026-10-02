"""An edit's timeline in Python, matching the editor's (editor/src/lib/timeline.ts): where scenes
start once transitions overlap them, how long the video runs, how much of a shot a scene uses."""
from __future__ import annotations


def frames(seconds: float, fps: int) -> int:
    return round(seconds * fps)


def scene_frames(edit: dict, i: int) -> int:
    return max(1, frames(edit["scenes"][i]["duration"], edit["fps"]))


def transition_frames(edit: dict, i: int) -> int:
    if i <= 0:
        return 0
    t = edit["scenes"][i].get("transition")
    if not t or t.get("type") == "cut":
        return 0
    want = frames(t.get("duration", 0.4), edit["fps"])
    room = min(scene_frames(edit, i - 1), scene_frames(edit, i)) - 1
    return max(0, min(want, room))


def scene_starts(edit: dict) -> list[float]:
    """Seconds into the video each scene starts."""
    starts, at = [], 0
    for i in range(len(edit["scenes"])):
        at -= transition_frames(edit, i)
        starts.append(at / edit["fps"])
        at += scene_frames(edit, i)
    return starts


def scenes_end(edit: dict) -> float:
    last = len(edit["scenes"]) - 1
    return scene_starts(edit)[last] + scene_frames(edit, last) / edit["fps"]


def total(edit: dict) -> float:
    card = edit.get("endCard")
    return scenes_end(edit) + (frames(card["duration"], edit["fps"]) / edit["fps"] if card else 0)


def source_seconds(speed, duration: float) -> float:
    """Seconds of the file a shot plays through in `duration` seconds of scene, at a constant rate
    or along speed keys (linear between keys, steady before the first and after the last)."""
    if speed is None:
        return duration
    if isinstance(speed, (int, float)):
        return duration * speed
    keys = sorted(speed, key=lambda k: k["at"])
    if not keys:
        return duration

    def rate(t: float) -> float:
        if t <= keys[0]["at"]:
            return keys[0]["rate"]
        for a, b in zip(keys, keys[1:]):
            if t <= b["at"]:
                return a["rate"] + (b["rate"] - a["rate"]) * (t - a["at"]) / max(1e-6, b["at"] - a["at"])
        return keys[-1]["rate"]

    points = [0.0] + [k["at"] for k in keys if 0 < k["at"] < duration] + [duration]
    return sum((b - a) * (rate(a) + rate(b)) / 2 for a, b in zip(points, points[1:]))


def _smooth(u: float) -> float:
    return u * u * (3 - 2 * u)


def zoom_at(keys: list[dict] | None, t: float) -> tuple[float, float, float]:
    """The framing at t (scale, x, y): still before the first key and after the last, eased between."""
    if not keys:
        return 1.0, 0.5, 0.5
    ks = sorted(keys, key=lambda k: k["at"])

    def f(k: dict) -> tuple[float, float, float]:
        return k["scale"], k.get("x", 0.5), k.get("y", 0.5)

    if t <= ks[0]["at"]:
        return f(ks[0])
    for a, b in zip(ks, ks[1:]):
        if t <= b["at"]:
            u = _smooth((t - a["at"]) / max(1e-6, b["at"] - a["at"]))
            fa, fb = f(a), f(b)
            return tuple(fa[i] + (fb[i] - fa[i]) * u for i in range(3))
    return f(ks[-1])
