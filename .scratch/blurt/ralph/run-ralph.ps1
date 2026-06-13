# Ralph loop over the Blurt AFK backlog (PowerShell 5.1) — the no-plugin / educational
# path that shows what the loop does under the hood. With the ralph-loop plugin
# installed, prefer launching from a Claude Code session instead (see README.md).
#
# NOTE: this script shells out to a `claude` CLI on PATH. In this setup `claude` is not
# on PATH by default (it ships inside the VSCode extension), so the plugin route in
# README.md is the recommended way to run the loop.

$ErrorActionPreference = "Stop"
$env:DOTNET_ROOT = "$env:USERPROFILE\.dotnet"
$prompt = Get-Content "$PSScriptRoot\PROMPT.md" -Raw
$max = 12

git switch ralph/backlog; if (-not $?) { git switch -c ralph/backlog }

for ($i = 1; $i -le $max; $i++) {
  Write-Host "-- Ralph $i/$max --"
  $out = claude -p $prompt --permission-mode acceptEdits `
    --allowedTools "Read,Edit,Write,Glob,Grep,Bash"
  Write-Host $out
  if ($out -match "ALL_ISSUES_DONE") { Write-Host "Backlog clear -- stopping."; break }
}
