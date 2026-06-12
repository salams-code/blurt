# 26 — Recent-dictations history in the tray menu (recover a lost paste)

Status: proposed — awaiting triage (feature request, 2026-06-12)
Type: AFK Core (ring buffer, unit-tested) + App UI (tray submenu) / HITL UX check

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## Why

Dictation injects the text wherever the cursor is **at paste time**. If the user
moved the cursor, switched window, or there was no text field focused, the result
is pasted into the void and **lost** — the user has to re-dictate. There's no way
to recover the last result.

## What to build

Keep the **last 3 dictation results** and expose them from the **tray icon
right-click menu**, so a lost or mis-targeted paste can be recovered.

- **Core:** a small fixed-capacity (3) ring buffer of recent dictation outputs
  (the final, post-refinement text that was injected). Newest first; oldest
  evicted. Pure and unit-tested (capacity, ordering, eviction, empty state).
- **App:** a "Recent dictations" submenu in `TrayApplicationContext`'s context
  menu, listing the entries with a short truncated preview. Selecting one
  **copies it back to the clipboard** (safe default — the user then pastes it
  wherever they actually want, rather than blindly re-injecting into whatever has
  focus now).
- **Privacy:** in-memory only — **never written to disk**; cleared on app exit.
  Dictation content can be sensitive.

## Design questions for triage

- Click action: **copy to clipboard** (safe, recommended) vs. **re-inject at
  cursor** (matches dictation but risks the same void-paste problem) — or offer
  both (e.g. click = copy, with a "paste" affordance)?
- Count: fixed 3 (user's ask) or configurable?
- Should the failure case (issue 13 fail-soft: nothing focused / inject failed)
  proactively surface "your dictation is in Recent dictations"?

## Acceptance criteria (draft — refine in triage)

- [ ] The last 3 dictation results are retained (newest first, oldest evicted) in memory only.
- [ ] The tray right-click menu shows a "Recent dictations" submenu; selecting an entry puts that text on the clipboard.
- [ ] History is never persisted to disk and is empty on a fresh launch.
- [ ] Ring-buffer logic is unit-tested in `Blurt.Core`; suite stays green; app builds.

## Blocked by

- None. Builds on 03 (text injection) and 13 (fail-soft) — both done. Pairs with
  issue 21 (flex-slot) only incidentally.
