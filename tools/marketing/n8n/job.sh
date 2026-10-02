#!/bin/bash
# What every n8n workflow runs: one run_job.py subcommand, from this repo's tools/marketing, with
# the tools it needs on PATH (n8n's environment, under launchd, has almost none).
#
#   tools/marketing/n8n/job.sh produce | telegram | work | publish | housekeeping | report | long
#
# KEHAI_DRY_RUN=1 (set in n8n's env file) adds --dry-run to the jobs that have one.
set -uo pipefail
here="$(cd "$(dirname "$0")/.." && pwd)"
export PATH="$HOME/.local/bin:/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin"
cd "$here" || exit 1
args=("$@")
if [ "${KEHAI_DRY_RUN:-0}" = "1" ]; then
  case "$1" in produce|publish|long) args+=(--dry-run) ;; esac
fi
exec uv run --quiet run_job.py "${args[@]}"
