# KEHAI marketing pipeline: build plan

> Studio **TokenLimit** · Game **Kehai** (気配, "the sense that someone is there") · Antagonist **Aiko** (愛子, "she")
> Plan started 2026-09-29. This file is the source of truth for the build: a later session
> reads it first, continues from **Status**, and ticks boxes as it goes.

## Status

| | |
|---|---|
| **Current phase** | Phase 3 (replay recorder) in progress — branch `feat/replay-recorder`, worktree `~/Developer/kehai-replay` |
| **Next action** | Build the recorder, verify (round-trip within 1 cm), open the PR ✋ |
| **Blocked on owner** | Answers to Q7–Q12 and Q14; the [owner setup checklist](#owner-setup-checklist) from Phase 5 on |
| **Last updated** | 2026-09-30 |

**How to resume (for a later session).**
1. Read this file, then the memory notes for this project.
2. `git fetch origin && git worktree list`. Phase work happens in a worktree next to the project
   (`~/Developer/kehai-<phase>`) from `origin/main`, never in the main folder `~/Developer/Kehai`
   (it holds untracked art that `stash`/`checkout` can destroy).
3. Continue at the first unticked box of the current phase. Stop at every ✋ and wait.

---

## Ground rules (from the owner's brief)

- **Commit code and docs only:** C#, TypeScript, Python, n8n workflow JSON without secrets, `.md`,
  JSON schemas. Never media, voices, music, keys. Secrets live in `tools/marketing/.env`
  (gitignored) and in n8n credentials.
- **One branch + PR per phase**, in a `git worktree` from `origin/main`. Scripts move with their
  `.meta`. The owner merges.
- **Names and branding** come from one place: `GameNames` (C#, player-facing text) and
  `tools/marketing/brand.json` (names, handles, links, colours, fonts, voices). The old names
  appear only in `KehaiMigration.cs`, its test and one `CHANGELOG.md` line.
- **Hardware:** M2, 8 GB RAM, ~17 GB free. Heavy jobs (Unity render, Remotion render, TTS) run
  **one at a time behind a lock**; Remotion concurrency 1–2. Local working budget 10 GB; archive
  to Google Drive and delete local copies by the retention rule below.
- **Nothing goes public without the owner's ✅ in Telegram.** Every step has a dry-run mode.
- Claude can't create accounts or enter passwords: the owner does the setup checklist.
- **Verification** (memory: headless Unity): compile offline with `csc` and the editor's `.rsp`;
  run tests and bot shifts in batch mode on an APFS clone (`cp -Rc`) of `~/Developer/Kehai` with the
  branch laid over it and productName set to a throwaway (`Kehai-verify`), then delete the clone,
  that data folder and its prefs. Unity renders (Phase 4) only run when the editor is closed.
- **Sleep:** a sleeping Mac freezes Unity mid-job, and EvalBatch's timeout (wall clock) then fires
  on wake — seen 2026-09-30. Run long jobs under `caffeinate -i`; closing the lid on battery still
  sleeps, so overnight jobs need the charger (or the lid open).

---

## Phase 0 — Prerequisites ✅

Done 2026-09-29. What later phases still need from it:

- **Buffer's API** (developers.buffer.com, GraphQL, API key from `publish.buffer.com/settings/api`)
  supports Instagram, Threads, LinkedIn, X, Facebook, Google Business, Mastodon, YouTube,
  Pinterest and Bluesky, **not TikTok** (fine: TikTok is by hand). **Video must be a public
  URL** (`createPost` → `assets.video.url`), so approved files need a public host → Q8.
  Free plan: 1 API key, 100 requests/15 min, 250/day, 3,000/30 days, 3 channels. Per-channel
  video support and YouTube title metadata get tested in Phase 7.
- **n8n needs Node 20.19–24.x**; this Mac has Node 25.9 (Homebrew). Plan: `node@22` (keg-only,
  doesn't replace Node 25) just for n8n. n8n 2.x disables the Execute Command and Local File
  Trigger nodes by default; we re-enable Execute Command via env and poll folders on a schedule.
- **macOS privacy (TCC):** background jobs can't read `~/Desktop`; the project now lives at
  `~/Developer/Kehai` for that reason.
- **Belief grid size:** the store map uses 1.5 m cells over ~150×150 m (≤10,000 cells). At 2 Hz
  that's ~12 MB per 10 min raw at one byte per cell, so Phase 3 downsamples to 3 m and
  compresses (≈1 MB per 10 min).
- **Unity Hub** keeps its project list in `~/Library/Application Support/UnityHub/hub.db`
  (SQLite, table `projects`); `projects-v1.json` is a leftover it no longer reads.

| Tool | Found (2026-09-29) | Needed for |
|---|---|---|
| macOS 26.5.1, Apple M2, 8 GB | — | — |
| Disk | ~17 GB free of 228 GB | everything; keep ≥ 3 GB free at all times |
| Unity | 6000.5.10f1 (+6000.3.10f1), project at `~/Developer/Kehai` | Phases 2–4 |
| git 2.54, gh 2.97 (logged in as goraxyy) | ✓ | PRs |
| Node 25.9 / npm 11.12 (Homebrew) | ✓ for Remotion; ✗ for n8n | Phase 5 (Remotion), Phase 7 (n8n needs `node@22`) |
| Python 3.9.6 (Apple CLT), uv 0.12.7 | ✓ uv will provide Python 3.12 in a project venv | Phase 6 |
| dotnet 7.0.317, jq 1.7.1, Homebrew 6.0.22 | ✓ | tools |
| **ffmpeg / ffprobe** | ✗ (`brew install ffmpeg`, 9.0.2) | Phase 4 on |
| **rclone** | ✗ (`brew install rclone`, 1.75.1) | Drive, Phase 5 on |
| **n8n** | ✗ (npm, on `node@22` 22.23.3) | Phase 7 |
| Telegram.app | ✓ installed | approvals |

## Phase 1 — Rename to Kehai / Aiko ✅

Merged 2026-09-30 as PR #13 (`d823e35`); see `CHANGELOG.md` and the PR for what changed. Player-facing
text reads `GameNames`; save data and settings were migrated to `TokenLimit/Kehai` (the old folder
stays as a backup); productName `Kehai`, bundle id `com.tokenlimit.kehai`; the repo is
`goraxyy/Kehai`; the project moved to `~/Developer/Kehai`, the art and reference folder to
`~/Desktop/KehaiRelated/`, Unity Hub and Claude's memory followed. EditMode: 37/37 at the new path.

## Phase 2 — Clip markers (Unity) ✋

Branch `feat/clip-markers`, worktree `~/Developer/kehai-markers`. Markers **only observe**: the
only changes to game code are two events for listeners (`AikoBrain.ShelfSabotaged`,
`ShiftRecorder.Recorded`/`Finishing`) and the recorder writing one more file.

- [x] `ClipMarkers.cs`: the one table — id, weight, pre/post-roll, subjects, tags, always-kept — plus
      every threshold (merge gap 8 s, chase ×1.5, min score 5, 3 m near miss, 2 m / 50 % found blind,
      0.5 m in 6 s stuck, 60 s undone work, 3 s loud mistake, 10 s tell → trick, cooldowns).
      Weights as briefed; `manual_good` 10 and `manual_bug` 0, both always kept.
- [x] `ClipWatch.cs` (plain C#, testable) + `ClipMarkerRecorder.cs` (listens, feeds it):
  - blink_move ← `StoryKind.Blink` · learned ← `StoryKind.Learned`
  - catch ← the thought log's `CAUGHT` · escape ← a chase in the live frame ending with no catch
  - near_miss, found_blind, aiko_stuck, possessed, lost guide ← the recorder's 10 Hz live frame
  - blackout ← mains off or a breaker tripped, until the lights are back (seconds in `value`)
  - undone_work ← `ShelfSabotaged` on a shelf restocked ≤ 60 s before, or the spill tactic's
    effect ≤ 15 m / ≤ 60 s from a spill you mopped
  - pa_call ← `PaSystem.SpeechStarted` · prop_trick ← a `PLAN` for crate_wall, fog, door_lock,
    camera_bolt_on, shelf_relocation or mimicry, lasting until its effect
  - clock_refused ← `PunchAttempted(false)` or the overtime tactic's refusal
  - loud_mistake ← your sprint or drop, `StoryKind.Heard` ≤ 3 s later, and her closing ≥ 1 m on it within 4 s
  - tell_then_trick ← a `StoryKind.Warning` then a (non-PA) effect ≤ 10 s later
  - shift_review ← clock-out · customer_chaos ← gave up at the till, or lost you while guided
  - manual_good **F7**, manual_bug **Left Shift + F7** (1 s tick on screen, works in builds);
    blink calibration keeps F9
- [x] `ClipMoments.cs`: merge markers ≤ 8 s apart (a lasting marker counts from its end); moment =
      union of pre/post-rolls clamped to the shift; score = Σ weights × 1.5 with a chase or catch;
      subjects, tags, `captionSeed` = narrator lines inside (≤ 12, repeats collapsed); drop < 5
      unless always kept; best first. Added after the first bot shift (one 5-minute "moment"):
      a moment is at most **45 s**, a lasting marker stretches it by at most **20 s**, and
      cooldowns for learned (20 s), PA (15 s) and loud mistakes (10 s).
- [x] `<stem>.markers.json` next to the shift's `.json`/`.html`, written with the record at clock-out;
      schema in `tools/marketing/schemas/markers.schema.json`. The report data carries
      `markers` and `moments` too.
- [x] Ticks on the F2 timeline (gold moments, weighted ticks, bug in red, the moment under the
      cursor named) and in the HTML report (a clickable strip and a "Clip moments" list).
- [x] F7 / Left Shift + F7 in `Controls.cs`, `CONTROLS.md`, README.
- [x] EditMode tests (`ClipMarkerTests`, 24): the table, merging, spans, scoring, chase boost,
      clamping, dropping/keeping, subjects/tags, caption seed, the file's JSON, and each rule.
- [x] Verified 2026-09-30 on the final branch: offline compile (0 errors, 0 warnings); EditMode
      61/61 on a clone; a batch-mode bot shift (6 min, rung F, clocked out) wrote `.markers.json`
      with 31 markers → 16 moments (longest 26 s; best: the catch, 20 s, score 54), valid against
      the schema; the HTML report's strip and list work (no console errors); F2 and the F7 tick
      checked in a windowed run (screenshots).
- [x] PR #14, merged 2026-09-30 (`bdf7587`). ✋

## Phase 3 — 3D replay recorder (Unity) ✋

Branch `feat/replay-recorder`, worktree `~/Developer/kehai-replay`. Code in
`Assets/!_Project/_Game/Replay/Scripts/` (namespace `Kehai.Replay`). Only listens; the game
code gained listen-only hooks: an `Item` registry, `AutoDoubleDoor` panel accessors,
`AikoBrain.Told`, `MazeMutation.LastMoves`, and `ShiftRecorder.Stem` (the shift's files are now
named when it starts, so the replay can stream to `<stem>.krec.part` and be renamed at the end).

- [x] `KrecFormat.cs` / `KrecWriter.cs`: one gzip stream of tagged records. Header: the shift,
      scene, Aiko's seed and rung, the maze's moves, the belief grid (3 m bins of her 1.5 m cells),
      every shelf slot (position, filled, product) and ceiling light (position, on). Then 30 Hz
      ticks: spawns, poses as **millimetre deltas** (zigzag varints), rotations as three 16-bit
      numbers (smallest three), state and visibility — **only what changed**; the view at 60 Hz
      (position, rotation, FOV, eyelids, what's in hand); events; belief frames XOR'd with the last.
- [x] `ReplayRecorder.cs`: the player (motion, carrying, holding a tool), the view, Aiko (mood,
      sees you, chasing), customers (their mark), understudies, hinge doors (locked), auto-door
      panels, every shelf unit (so relocations and the maze show), crate walls, fog, CCTV cameras
      (bolted on, dead), coffee cups, footprints, spills, bins (how full), bags (disposed), and every
      item off its shelf — tools included (the mop, the torch on/off) — loose, in hand, held (Aiko
      with the mop), or back on a shelf. Events: noises, tells (kind, place, lead), PA chime and speech, mains and
      breakers, the thought log, the narrator, shelf slots filling and emptying, ceiling lights.
      Belief map at 2 Hz with her guess and how sure she is.
- [x] `KrecReader.cs`: reads a file back into timelines; `TryPose` holds a still entity until the
      tick before it moves (samples are written only on change), `TryCamera`, `BeliefAt`; a file
      cut short (the game quit) still loads.
- [x] `PlayerBodySlot.cs`: loads `Resources/ReplayBody/PlayerBody` (see below) and plays its clips
      through the Playables API; a 1.8 m capsule (lower when crouching) until then.
- [x] Tests: `KrecTests` (varints, rotations, a written file reads back as written, a still entity
      doesn't drift, a cut file loads, ten minutes stay under 20 MB, the capsule, clips by name) and
      `ReplayRoundTripTests` (10 s of a bot shift in the real store; every pose of the player, Aiko
      and the customers comes back within 1 cm).
- [x] Verified 2026-09-30: offline compile (0 errors, 0 warnings); **EditMode 70/70** on a clone
      (the round trip: 389 poses of the player, Aiko and a customer, worst **0.72 mm**, rotations
      exact, 151 views, 21 belief frames); a full 6-minute bot shift wrote a 380 KB `.krec`
      (≈0.6 MB per 10 min) that reads back complete in 0.6 s: 252 entities (player, Aiko, 3
      customers, 14 items incl. the mop, torch, crate and what customers carried, 6 doors, 8 door
      panels, 189 shelf units, 20 footprints, 5 spills, 3 bins, a bag), 1,013 noises, 35 tells,
      12 PA events, 3,069 thoughts, 301 narrator lines, 16 slot and 81 light changes, 5,501 views,
      734 belief frames.
- [ ] PR. ✋

**Your player model (Q13), when it's ready:** export an FBX to
`Assets/!_Project/_Game/Replay/Resources/ReplayBody/PlayerBody.fbx` (local art, never
committed). In its import settings: Rig → **Humanoid**; Animation → one clip each, named so the
name contains **idle**, **walk**, **crouch** (walking crouched), **run** (or sprint) and **carry**
(walking with something in hand), each with **Loop Time** on; about 1.8 m tall, facing +Z, feet at
the origin. No Animator Controller needed. A missing clip falls back to walk or idle.

## Phase 4 — Replay player, cameras, shot render (Unity) ✋

- [ ] `ReplayPlayer` (`-replay <file>`, or from the shift report): rebuild the store; AI, NavMesh
      agents, physics and gameplay scripts off; interpolated puppets; sounds replayed in 3D.
- [ ] Timeline: play/pause, scrub, 0.1–4×, frame step, marker ticks, HUD toggle.
- [ ] Cameras: free fly (WASD, mouse, Q/E, scroll speed, FOV, depth of field) and presets POV,
      CCTV corner, chase-cam behind Aiko, orbit, top-down; **K** saves keyframes to a smooth
      path (JSON).
- [ ] "Aiko's mind" layers, renderable alone with alpha: belief heat map, her guess, view cone,
      sound rings, thought-log text.
- [ ] `ReplayRender` batch entry: `-krec -moment -shot <preset|path.json> -layers -size -fps 60 -out`;
      fixed timestep; frames piped to ffmpeg; WAV from the event track. Unattended, batch mode
      **with graphics**, refuses to start while an editor has the project open (checks
      `Temp/UnityLockfile`).
- [ ] PR. ✋

## Phase 5 — Editor (Remotion, `tools/marketing/editor`) ✋

- [ ] One composition driven by `edit.json` + JSON Schema (`tools/marketing/schemas/edit.schema.json`,
      shared with the Python validators): 9:16 and 16:9.
- [ ] Shots with trims, speed ramps, zoom/pan, split screen, picture-in-picture of Aiko's mind.
- [ ] Voice-over track per language; music ducked under voice; word-timed captions.
- [ ] Text (hooks, labels, lower thirds, end card); images, GIFs, Lottie, animated
      arrows/circles/zooms; meme templates as components; transitions; SFX. Reads `brand.json`.
- [ ] Asset library on Drive + `assets/manifest.json` (file, type, mood tags, source, licence
      required; music must be cleared for YouTube, TikTok and Instagram). Assets enter through
      `add_asset.py`, which refuses a file without a licence.
- [ ] Render a sample short and a 2-minute sample long video. ✋
- Licence note: Remotion is free for individuals and companies of up to 3 people.

## Phase 6 — Voice and writing (Python, `tools/marketing`) ✋

- [ ] TTS interface: Azure Speech backend (neural, EN + RU, word timings via `WordBoundary`, a
      distinct voice for Aiko's lines) and a switchable ElevenLabs backend.
- [ ] Claude steps, one script each, prompts in `tools/marketing/prompts/*.md`, outputs validated
      against the schemas with structured outputs (`output_config.format`), the style guide
      prompt-cached: `pick_moments`, `write_short`, `translate`, `revise` (claude-sonnet-5-5),
      `long_video` (claude-opus-5-5), `package`, `weekly_report`. `pick_moments` reads the
      week's `.markers.json` files and must include every moment with `kept: true`.
- [ ] Log tokens and cost per call to `logs/llm_costs.csv`; stop and tell the owner if a calendar
      month passes $15 (the Console workspace limit is the hard stop behind it).
- API notes for the implementer: Sonnet 5.5 and Opus 5.5 reject forced `tool_choice` → use
  structured outputs; Opus 5.5's default effort is `medium` (set it explicitly); no assistant
  prefill; handle `stop_reason: "refusal"` and include the server-side fallback beta.
- ✋

**Budget estimate** (Sonnet 5.5 $2/$10 per M tokens, Opus 5.5 $4/$20, cache reads $0.20,
cache writes 1.25×): weekly pick + 3 shorts + 3 translations + 3 packages + revisions + report
≈ $0.5–0.8/week; one long video (Opus, ~60k-token context, 4–6 calls + revisions) ≈ $1.5–3.
**≈ $4–6 a month**, well under $15.

## Phase 7 — n8n (self-hosted, npm, localhost) ✋

n8n 2.x on `node@22`, `N8N_HOST=127.0.0.1`, LaunchAgent; workflows exported to
`tools/marketing/n8n/` without credentials. Every step calls `tools/marketing` scripts through
one `run_job.py` wrapper (lock, dry-run, logging, `caffeinate -i` around heavy jobs).

- [ ] 1. **New shift:** schedule polls `shift_records/` for new `.markers.json` → pick moments →
      queue shot renders overnight (only when the editor is closed) → write 3 shorts → TTS EN + RU →
      render drafts.
- [ ] 2. **Approval:** Telegram `getUpdates` polling on a schedule (no webhook), only the owner's
      chat id accepted. Each draft: preview, caption, ✅ / ❌ / ✏️. ✏️ reply → revise → re-render →
      new preview. Version history; "undo" restores the previous version. Files > 50 MB → Drive link.
- [ ] 3. **Publish:** approved shorts to Buffer's queue (YouTube Shorts, Instagram, X or Bluesky)
      Mon/Wed/Fri. Test video support per channel; where missing, send the file + a one-tap
      checklist. TikTok file + caption to Telegram, posted by hand.
- [ ] 4. **Long video, monthly:** ✋ gates in Telegram after outline, script, rough cut, packaging;
      English master + Russian audio track + subtitles; final files to Drive for YouTube Studio.
- [ ] 5. **Housekeeping:** archive to Drive, delete rejects after 7 days, enforce the local budget,
      weekly report to Telegram.
- [ ] End-to-end dry run: a batch-mode bot shift ends with a draft short in Telegram. ✋

---

## Architecture (planned)

```
Unity (C#)                           tools/marketing (Python 3.12 via uv)        Remotion (TS)
 ShiftRecorder ─► <stem>.json/.html   run_job.py  (lock, dry-run, logs)          editor/
 ClipMarkers   ─► <stem>.markers.json llm/ pick_moments write_short translate     edit.json ─► mp4
 ReplayRecorder─► <stem>.krec             revise long_video package weekly_report
 ReplayRender  ◄─ shot request ────── tts/ azure, elevenlabs                     brand.json
               ─► shot.mov + .wav     drive.py (rclone) telegram.py buffer.py
                                      housekeeping.py  schemas/*.json  prompts/*.md
                         n8n (node@22, 127.0.0.1) schedules and chains the jobs
```

Shift records (and the `.markers.json` files) are written to
`~/Library/Application Support/TokenLimit/Kehai/shift_records/`.

**Local working folder:** `~/TokenLimit/marketing/` (outside the repo and outside `~/Desktop`):
`inbox/ shots/ audio/ drafts/ approved/ archive-staging/ logs/ state/`. Budget 10 GB and a
floor of 3 GB free disk; a job that would break either waits for housekeeping.

**Retention (proposal, Q12):** rejected drafts 7 days · approved drafts: deleted locally once
archived to Drive and posted + 3 days · shot renders: after their draft is approved or rejected
+ 3 days · `.krec`: archived to Drive after its moments are used, local copy kept 30 days ·
TTS audio: kept with its draft · logs: 90 days.

---

## Owner setup checklist

Nothing here is needed before Phase 5. Put secrets **only** in `tools/marketing/.env`
(created later from `.env.example`, gitignored) or in n8n's credential store.

- [ ] **Anthropic API** (console.anthropic.com): create a workspace (e.g. `tokenlimit-marketing`),
      set its **monthly spend limit to $15**, create an API key in it → `ANTHROPIC_API_KEY`.
- [ ] **Azure Speech, free tier F0** (portal.azure.com): create a *Speech* resource, pricing tier
      **Free F0** (one per subscription; ~0.5M neural characters a month), region e.g. East US or
      West Europe → `AZURE_SPEECH_KEY`, `AZURE_SPEECH_REGION`.
- [ ] **Telegram bot:** in Telegram, @BotFather → `/newbot` → token → `TELEGRAM_BOT_TOKEN`. Then send
      `/start` to your bot so Claude can read your chat id (`TELEGRAM_CHAT_ID`). Don't set a webhook.
- [ ] **Buffer:** account; connect **YouTube**, **Instagram** (a professional account: Business or
      Creator) and **X or Bluesky** (Free plan = 3 channels, Q9); set the posting schedule to
      Mon/Wed/Fri at your preferred times (EDT); create an API key at
      `publish.buffer.com/settings/api` → `BUFFER_API_KEY`.
- [ ] **Google Drive:** `brew install rclone`, then `rclone config` → new remote `gdrive`, type
      `drive`, scope per Q7, sign in in the browser. Create a Drive folder `TokenLimit Marketing`.
- [ ] **ffmpeg:** `brew install ffmpeg`.
- [ ] **Node for n8n:** `brew install node@22` (keg-only; your Node 25 stays the default).
- [ ] *(Later, for weekly_report)* a Google Cloud project with **YouTube Data API v3** enabled and
      an API key (read-only public stats) → `YOUTUBE_API_KEY`, plus your channel id.
- [ ] *(Optional)* ElevenLabs API key → `ELEVENLABS_API_KEY`, if you want that backend.

---

## Questions

**Answered 2026-09-29:** Q1 clip markers on **F7** (blink calibration stays F9) · Q2 the burnout
ending keeps 過労死 with **BURNED OUT** under it · Q3 settings migrated on macOS · Q4 the pitch
rewrites, and Aiko's name meaning is **never explained** · Q5 folders renamed and the project moved
out of `~/Desktop` · Q6 repo renamed `goraxyy/Kehai` · Q15 the migration and its test are the only
code that names the old game. **2026-09-30:** Q13 the player body will have idle, walk,
crouch-walk, run and carry animations; a capsule until then (path as proposed, Humanoid rig).

**Open** (each has a recommendation; answer "ok" to take it):

7. **Drive scope for rclone.** *Recommend:* `drive.file` (rclone only sees files it created) and
   assets enter the library through `add_asset.py` from a local inbox (it also records the licence).
   Alternative: full `drive` scope limited to one folder, so you can drop assets in via the web.
8. **Public URL for Buffer.** Buffer fetches videos from a public URL. *Recommend:* for each
   approved short, a Drive "anyone with the link" share that the pipeline revokes once Buffer has
   the post; tested per channel in Phase 7. Fallback if Buffer won't take Drive links: a
   Cloudflare R2 bucket (free tier, needs a Cloudflare account).
9. **X or Bluesky** for the third Buffer channel (Free plan = 3)? Or a paid Buffer plan for both.
10. **Voices.** English narrator, Russian narrator, and Aiko. *Recommend:* 3–4 Azure samples of
    each in Phase 6 for you to pick (e.g. EN: Andrew / Ava; RU: Dmitry / Svetlana; Aiko: a calm,
    formal female voice, possibly a Japanese voice speaking English). Should Aiko ever speak
    Japanese with subtitles?
11. **brand.json** needs: handle(s), website, Discord/Steam links (if any yet), colours, fonts.
    *Recommend as defaults:* colours from the shift report (`#0f1116` bg, `#ff5454` Aiko,
    `#4dd2ff` you, `#ffd640` her guess); fonts **Inter** (Latin + Cyrillic) and **Noto Sans JP**
    for 気配/愛子. Handle: is `@kehaigame` free where you want it? (You check; Claude can't sign up.)
12. **Retention and budget** as proposed above (10 GB working, 3 GB free floor)? The tools
    themselves (n8n, Remotion + its Chrome, ffmpeg, Python venv, node@22, rclone) take roughly
    2–3 GB, leaving ~14 GB free before any media.
14. **Posting times** for Mon/Wed/Fri: set in Buffer's queue (EDT). Any preference?

---

## Decisions log

| Date | Decision | By |
|---|---|---|
| 2026-09-29 | Names: Kehai (気配), Aiko (愛子), studio TokenLimit; Aiko's name meaning never explained | owner |
| 2026-09-29 | n8n runs on node@22 (n8n supports Node 20.19–24.x) | Phase 0 |
| 2026-09-29 | Clip markers on F7 / Left Shift + F7; blink calibration stays on F9 | owner (Q1) |
| 2026-09-29 | Burnout ending: 過労死 / BURNED OUT | owner (Q2) |
| 2026-09-30 | Project at `~/Developer/Kehai`, repo `goraxyy/Kehai` | owner (Q5, Q6) |
| 2026-09-30 | This plan committed with the Phase 2 PR; before that it was untracked in the main folder | Phase 2 |
| 2026-09-30 | Clip moments scoring under 5 are dropped unless they hold a manual marker; `manual_good` weighs 10, `manual_bug` 0 | Phase 2 (for review) |
| 2026-09-30 | A clip moment is at most 45 s; a long marker stretches it at most 20 s; cooldowns for learned, PA, loud mistakes | Phase 2 (for review) |

## Changelog

- 2026-09-29 — Phase 0: prerequisites checked, plan written.
- 2026-09-29 — Phase 1: rename done and verified; PR #13 opened.
- 2026-09-30 — PR #13 merged; main folder synced; Project Settings set through the Unity MCP; one
  play-mode run migrated the save data and settings. The repo was renamed, the project moved to
  `~/Developer/Kehai`, the Desktop folders renamed, Unity Hub (its `hub.db`) and Claude's memory
  re-pointed. EditMode at the new path: 37/37 (a first run hit a one-off FMOD audio error in one
  fixture's setup and caught two capitalised name spellings in this plan; both fixed).
- 2026-09-30 — Phase 2: clip markers built in `feat/clip-markers`; PR #14 merged.
- 2026-09-30 — Phase 3 started (`feat/replay-recorder`).
