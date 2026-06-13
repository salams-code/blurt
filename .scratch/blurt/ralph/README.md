# Ralph loop — Blurt AFK backlog

A minimal [Ralph loop](https://awesomeclaude.ai/ralph-wiggum): one prompt
(`PROMPT.md`), fed to a fresh-context agent over and over, doing **one issue per run**
until the backlog is clear. State lives in the issue files + git, not in conversation.

## What it will work through

As of 2026-06-13 the `ready-for-agent` chain is:

```
35 editable prompts (free)
  ├─▶ 36 Email ──────▶ 39 translate-via-Shift
  └─▶ 37 reset+backup ─▶ 38 backup-UI
```

Pick order (lowest free number first): **35 → 36 → 37 → 38 → 39**. The loop stops by
printing `ALL_ISSUES_DONE` when nothing `ready-for-agent` is left unblocked.
`ready-for-human` issues (34, 40, 41) are invisible to the loop.

## How to run (recommended: the plugin, in a NEW session)

Open a fresh Claude Code session in the repo, on the loop branch, then:

```
git switch -c ralph/backlog        # once; the loop also ensures it
```

```
/ralph-loop:ralph-loop "$(Get-Content .scratch/blurt/ralph/PROMPT.md -Raw)" --completion-promise "ALL_ISSUES_DONE" --max-iterations 12
```

Confirm the exact flag names with `/ralph-loop:help` (plugin versions vary). The two
load-bearing options are the **completion promise** (`ALL_ISSUES_DONE`, the stop token
`PROMPT.md` prints when done) and **`--max-iterations`** (the runaway guard — always
set it).

## How to run (no plugin: the scripts)

`run-ralph.ps1` / `run-ralph.sh` are the same loop hand-rolled, for learning. They
shell out to a `claude` CLI on PATH — which isn't the default here — so they're mainly
to read, not necessarily to run.

## Safety

- Runs on branch `ralph/backlog`; `main` is untouched. Review the diff before merging.
- The loop **never pushes** and **never `dotnet publish`** (publish locks the running
  `Blurt.exe`). `dotnet test` green is the done-gate.
- `--max-iterations` caps cost. 12 covers the 5-issue chain with headroom for a red-test
  fix-up run or two.
