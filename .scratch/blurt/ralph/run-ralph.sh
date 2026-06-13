#!/usr/bin/env bash
# Ralph loop over the Blurt AFK backlog (Git Bash) — the no-plugin / educational path.
# With the ralph-loop plugin installed, prefer launching from a Claude Code session
# instead (see README.md). Needs a `claude` CLI on PATH.
set -uo pipefail
export DOTNET_ROOT="$USERPROFILE/.dotnet"
MAX="${1:-12}"
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

git switch ralph/backlog 2>/dev/null || git switch -c ralph/backlog

for ((i=1; i<=MAX; i++)); do
  echo "-- Ralph $i/$MAX --"
  out=$(claude -p "$(cat "$DIR/PROMPT.md")" \
        --permission-mode acceptEdits \
        --allowedTools "Read,Edit,Write,Glob,Grep,Bash")
  echo "$out"
  grep -q "ALL_ISSUES_DONE" <<<"$out" && { echo "Backlog clear -- stopping."; break; }
done
