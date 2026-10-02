#!/bin/bash
# Sets up n8n for the Kehai pipeline (BUILD_PLAN.md, Phase 7), on this Mac only.
#
#   tools/marketing/n8n/setup.sh                 install n8n if needed, write its settings, import and publish the workflows
#   tools/marketing/n8n/setup.sh --launch-agent  …and start it at login (com.tokenlimit.kehai.n8n)
#   tools/marketing/n8n/setup.sh --remove-agent  stop it starting at login
#   tools/marketing/n8n/run.sh                   start it by hand (http://127.0.0.1:5678)
#
# n8n lives in ~/TokenLimit/n8n (its app in app/, its database and key in .n8n/), on Node 24
# (Homebrew's node@24: n8n 2.41 needs Node 24 or newer; the Mac's default Node stays as it is). It listens on
# 127.0.0.1 only. The workflows hold no secrets: every step reads tools/marketing/.env.
# KEHAI_DRY_RUN=1 in ~/TokenLimit/n8n/env makes produce, publish and long dry runs.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
tools="$(cd "$here/.." && pwd)"
home_n8n="${KEHAI_N8N_HOME:-$HOME/TokenLimit/n8n}"
node24="/opt/homebrew/opt/node@24/bin"
agent="$HOME/Library/LaunchAgents/com.tokenlimit.kehai.n8n.plist"
version="2.41.6"           # the n8n these workflows were tested with
logs="${KEHAI_MARKETING:-$HOME/TokenLimit/marketing}/logs"

say() { echo "n8n setup: $*"; }

if [ "${1:-}" = "--remove-agent" ]; then
  launchctl bootout "gui/$(id -u)" "$agent" 2>/dev/null || true
  rm -f "$agent"
  say "the LaunchAgent is gone; n8n no longer starts at login"
  exit 0
fi

[ -x "$node24/node" ] || { say "Node 24 isn't installed: brew install node@24"; exit 3; }
export PATH="$node24:$PATH"
mkdir -p "$home_n8n/app" "$logs"
if [ ! -x "$home_n8n/app/node_modules/.bin/n8n" ]; then
  say "installing n8n into $home_n8n/app (about 2.6 GB)"
  (cd "$home_n8n/app" && { [ -f package.json ] || npm init -y >/dev/null; } && npm install --no-fund --no-audit "n8n@$version")
fi

timezone="$(python3 -c "import json;print(json.load(open('$tools/pipeline.json'))['timezone'])")"
if [ ! -f "$home_n8n/env" ]; then
  cat > "$home_n8n/env" <<ENV
# n8n's settings for the Kehai pipeline (read by run.sh and the LaunchAgent). No secrets here.
N8N_USER_FOLDER=$home_n8n
N8N_LISTEN_ADDRESS=127.0.0.1
N8N_HOST=127.0.0.1
N8N_PORT=5678
N8N_PROTOCOL=http
N8N_SECURE_COOKIE=false
NODES_EXCLUDE=[]
GENERIC_TIMEZONE=$timezone
N8N_DIAGNOSTICS_ENABLED=false
N8N_VERSION_NOTIFICATIONS_ENABLED=false
N8N_TEMPLATES_ENABLED=false
N8N_PERSONALIZATION_ENABLED=false
EXECUTIONS_DATA_PRUNE=true
EXECUTIONS_DATA_MAX_AGE=336
KEHAI_TOOLS=$tools
KEHAI_DRY_RUN=1
ENV
  say "wrote $home_n8n/env (dry runs on: set KEHAI_DRY_RUN=0 there when the keys are in .env)"
fi
# This repo and the timezone may have moved since the file was written: keep them current.
sed -i '' -e "s|^KEHAI_TOOLS=.*|KEHAI_TOOLS=$tools|" -e "s|^GENERIC_TIMEZONE=.*|GENERIC_TIMEZONE=$timezone|" "$home_n8n/env"
set -a; . "$home_n8n/env"; set +a

n8n="$home_n8n/app/node_modules/.bin/n8n"
ids=()
for f in "$here"/workflows/*.json; do
  ids+=("$(python3 -c "import json,sys;print(json.load(open(sys.argv[1]))['id'])" "$f")")
done
say "importing the workflows"
# A published workflow can't be overwritten from the CLI: unpublish ours (only ours) first.
for id in "${ids[@]}"; do "$n8n" unpublish:workflow --id="$id" >/dev/null 2>&1 || true; done
"$n8n" import:workflow --separate --input="$here/workflows/" 2>&1 | grep -E "Import|error|Error" | tail -3
for id in "${ids[@]}"; do "$n8n" publish:workflow --id="$id" >/dev/null 2>&1 && say "published $id" || say "couldn't publish $id"; done

if [ "${1:-}" = "--launch-agent" ]; then
  mkdir -p "$(dirname "$agent")"
  cat > "$agent" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>Label</key><string>com.tokenlimit.kehai.n8n</string>
  <key>ProgramArguments</key>
  <array><string>/bin/bash</string><string>$here/run.sh</string></array>
  <key>RunAtLoad</key><true/>
  <key>KeepAlive</key><true/>
  <key>StandardOutPath</key><string>$logs/n8n.log</string>
  <key>StandardErrorPath</key><string>$logs/n8n.log</string>
</dict>
</plist>
PLIST
  launchctl bootout "gui/$(id -u)" "$agent" 2>/dev/null || true
  launchctl bootstrap "gui/$(id -u)" "$agent"
  say "n8n starts at login now (LaunchAgent com.tokenlimit.kehai.n8n); open http://127.0.0.1:5678"
else
  say "done. Start it with $here/run.sh, or again with --launch-agent to start it at login"
fi
