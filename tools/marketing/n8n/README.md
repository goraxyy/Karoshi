# n8n: the pipeline's schedule

n8n runs on this Mac only (http://127.0.0.1:5678) and does one thing: it starts `run_job.py`
jobs on a schedule. Every decision is in the Python (`tools/marketing`), every message to the
owner goes through Telegram, and the workflows hold no secrets.

| Workflow | When | Job |
|---|---|---|
| Kehai · Telegram | every minute | the owner's taps and replies; alerts out |
| Kehai · work | every 5 minutes | queued revisions, undos, the long video's stages |
| Kehai · produce | 1, 3 and 5 a.m. | new shifts announced; the week's picks (on `pick_day`); every short along its steps; Unity renders only while the editor is closed |
| Kehai · publish | 9 a.m. | on posting days: the next approved short to Buffer, TikTok by hand |
| Kehai · housekeeping | 4:30 a.m. | Buffer statuses, the Drive archive, retention, storage |
| Kehai · weekly report | Sundays 8 p.m. | the report, to Telegram |
| Kehai · long video | 10 a.m. | on `long.day`: the month's long video starts |

Times are in `pipeline.json`'s timezone. Each workflow also has a **Run now** trigger.

## Setting it up

```bash
brew install node@24                      # n8n 2.41 needs Node 24+; your default Node stays
tools/marketing/n8n/setup.sh              # installs n8n into ~/TokenLimit/n8n, imports and publishes the workflows
tools/marketing/n8n/run.sh                # start it by hand, or:
tools/marketing/n8n/setup.sh --launch-agent   # start it at login (--remove-agent to stop that)
```

The first visit to http://127.0.0.1:5678 asks you to create n8n's owner account (local only).
Settings are in `~/TokenLimit/n8n/env`; **`KEHAI_DRY_RUN=1`** there (the default) makes
produce, publish and long dry runs: change it to 0 once the keys are in `tools/marketing/.env`.
Run `setup.sh` again after the workflows change (or after moving the repo).

## Trying it without keys

Without `TELEGRAM_BOT_TOKEN` the bot writes to `<working folder>/state/telegram_outbox.jsonl`,
and you play the owner with `run_job.py fake`:

```bash
uv run run_job.py status
uv run run_job.py fake tap a:2026-w40-found-by-ear:1 --message 1003   # tap ✅ on that preview
uv run run_job.py fake text "make the hook about hearing" --reply-to 1004
uv run run_job.py telegram                                            # handle them
```

`KEHAI_LLM_REPLAY=<folder>` answers the Claude steps from files, `KEHAI_TTS_BACKEND=say` speaks
with the macOS voices, `KEHAI_PICK_NOW=1` picks the week's shorts on the next produce, and
`KEHAI_PIPELINE=<file>` uses other settings (e.g. one short a week for a trial).
