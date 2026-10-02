#!/bin/bash
# Starts n8n for the Kehai pipeline with its settings (setup.sh writes them): http://127.0.0.1:5678
set -euo pipefail
home_n8n="${KEHAI_N8N_HOME:-$HOME/TokenLimit/n8n}"
export PATH="/opt/homebrew/opt/node@24/bin:$HOME/.local/bin:/opt/homebrew/bin:/usr/bin:/bin:/usr/sbin:/sbin"
set -a; . "$home_n8n/env"; set +a
exec "$home_n8n/app/node_modules/.bin/n8n" start
