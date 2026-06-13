# Ralph loop — Blurt AFK backlog

You are a Claude Code agent running **one iteration** of an autonomous loop over the
Blurt issue backlog. Your context is fresh each run; durable state lives in the repo
(the issue files, the code, git history). Do **ONE** issue this run, then stop — the
loop restarts you with fresh context for the next.

Orientation: this is a .NET 8 / C# Windows tray app. Read `CLAUDE.md` for the
build/test/publish rules and `docs/agents/issue-tracker.md` for the tracker layout.
Issues live as markdown under `.scratch/blurt/issues/`.

## 0. Prepare (once per run)

- Work on branch `ralph/backlog`. If you're not on it:
  `git switch ralph/backlog` (or `git switch -c ralph/backlog` to create it).
- If `git status` shows pre-existing uncommitted changes that are **not** yours from
  this run, commit them first as `chore(tracker): pre-loop bookkeeping` so your
  per-issue commit stays clean.

## 1. Pick the next issue

Scan `.scratch/blurt/issues/*.md`. Choose the **lowest-numbered** file where BOTH:

- its `Status:` line **begins with** `ready-for-agent`, and
- every issue listed under its `## Blocked by` has a `Status:` that **begins with**
  `done`. A `Blocked by` of "None" / "can start immediately" counts as satisfied.

(Ignore `ready-for-human`, `needs-*`, `wontfix`, `superseded` — those are not yours.)

If no file qualifies, print exactly `ALL_ISSUES_DONE` on its own line and stop. Do
nothing else.

## 2. Implement it

Satisfy **every** `- [ ]` checkbox under `## Acceptance criteria`, **including any
backward-compatibility criteria the issue itself lists** (do not invent constraints of
your own — honour what the issue specifies). Follow the repo conventions: pure,
decidable logic goes in **`Blurt.Core`** with unit tests; the WPF/App layer is the thin
shell over it.

## 3. Verify — this is the gate, not your own judgement

```powershell
$env:DOTNET_ROOT = "$env:USERPROFILE\.dotnet"
& "$env:USERPROFILE\.dotnet\dotnet.exe" test tests/Blurt.Core.Tests --nologo
```

The suite must be **green**. If it is red, fix it this run. Never proceed past red tests.

## 4. Record & commit

Only once the suite is green:

- Tick the `- [x]` boxes you satisfied.
- Set the file's `Status:` to `done`.
- Append a `## Comments` note: what you built, and — if the issue touched anything a
  user sees or feels (Settings, overlay, tray, hotkey, network) — a one-line
  `HITL UX check recommended: <what to eyeball>` so the human knows what to verify.
- `git add -A && git commit -m "Issue NN: <slug> — <what changed>"`.
  **One commit per issue.**

## 5. Stop

Print a one-line summary of what you did. The loop restarts you with fresh context for
the next issue.

## Hard rules

- **Never `git push`.**
- **Never `dotnet publish`** — it locks the running `Blurt.exe` and is not part of an
  issue's done-criteria. Building and `dotnet test` are enough.
- If you get stuck on the same issue across two runs, set its `Status:` to
  `ready-for-human`, append a `## Comments` note explaining the blocker, and stop so a
  human can look.
