#!/usr/bin/env python3
"""A scripted baseline agent over the wire: nearest job first, serve the queue, fix the
lights, clock out when the HUD says done. It exists to prove the harness end to end and to
give language-model agents (llm_agent.py) a floor to beat.

    python run_baseline.py --episodes 3 --rung F --shift-seconds 180 --out baseline.jsonl
"""
from __future__ import annotations

import argparse
import json
import random

from kehai_env import KehaiEnv


def choose(obs: dict, rng: random.Random) -> dict:
    you = obs["you"]
    power = obs["power"]
    if not power["lights_on"] and power.get("breakers"):
        off = sorted((b for b in power["breakers"] if not b["on"]), key=lambda b: b["hum_pitch_rank"])
        if off:
            return {"verb": "flip_breaker", "target": off[0]["id"]}
    if you["energy"] < 0.25:
        return {"verb": "drink_coffee"}
    if obs["checkout_queue"]:
        return {"verb": "serve", "target": max(obs["checkout_queue"], key=lambda c: c["waited_s"])["id"]}
    asking = [c for c in obs["customers_asking"] if c["stage"] == "Asking"]
    if asking:
        return {"verb": "help", "target": asking[0]["id"], "accept": True}
    inventory = [i for i in you["inventory"] if i]
    if "trash bag" in inventory:
        return {"verb": "dispose"}

    jobs = []
    for s in obs["spills"]:
        jobs.append((s["walk_m"], "mop", s["id"], "mop"))
    for b in obs["shelves_to_restock"]:
        jobs.append((b["walk_m"], "restock", b["id"], "crate"))
    for b in obs["bins"]:
        if b["fill"] > 0:
            jobs.append((b["walk_m"], "bag_trash", b["id"], None))
    jobs = [j for j in jobs if j[0] >= 0]
    if jobs:
        _, verb, target, tool = min(jobs)
        if tool and tool not in inventory:
            return {"verb": "pick_up", "target": tool}
        return {"verb": verb, "target": target}

    if not obs["store_open"] and all(t["done"] for t in obs["hud_tasks"]):
        return {"verb": "clock_out"}
    return {"verb": "wait", "seconds": 2} if rng.random() < 0.5 else {"verb": "move_to", "target": "checkout_2"}


def main(argv=None) -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--port", type=int, default=5555)
    ap.add_argument("--episodes", type=int, default=3)
    ap.add_argument("--seed", type=int, default=1)
    ap.add_argument("--rung", default="F")
    ap.add_argument("--shift-seconds", type=float, default=180)
    ap.add_argument("--fps", type=int, default=20)
    ap.add_argument("--max-steps", type=int, default=400)
    ap.add_argument("--out", default="baseline.jsonl")
    args = ap.parse_args(argv)

    rng = random.Random(args.seed)
    with KehaiEnv(port=args.port) as env, open(args.out, "a", encoding="utf-8") as out:
        for episode in range(args.episodes):
            obs = env.reset(seed=args.seed + episode, rung=args.rung, shift_seconds=args.shift_seconds,
                            fps=args.fps, agent="baseline")
            steps, failed = 0, {}
            while not env.done and steps < args.max_steps:
                action = choose(obs, rng)
                key = (action["verb"], action.get("target"))
                if failed.get(key, 0) >= 2:          # stop retrying what keeps failing
                    action = {"verb": "wait", "seconds": 3}
                obs = env.step(**action)
                last = obs.get("last_action") or {}
                if not last.get("ok", True):
                    failed[key] = failed.get(key, 0) + 1
                steps += 1
            metrics = env.metrics()
            out.write(json.dumps(metrics) + "\n")
            out.flush()
            print(f"episode {episode + 1}: clocked_out={metrics['clocked_out']} steps={metrics['steps']} "
                  f"mopped={metrics['spills_mopped']} restocked={metrics['shelves_restocked']} "
                  f"served={metrics['customers_served']} failures={metrics['failures']}")


if __name__ == "__main__":
    main()
