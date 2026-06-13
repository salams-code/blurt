# 26 — Recent-dictations history in the tray menu (recover a lost paste)

Status: done (HITL-verified 2026-06-13)
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

- [x] The last 3 dictation results are retained (newest first, oldest evicted) in memory only.
- [x] The tray right-click menu shows a "Recent dictations" submenu; selecting an entry puts that text on the clipboard.
- [x] History is never persisted to disk and is empty on a fresh launch.
- [x] Ring-buffer logic is unit-tested in `Blurt.Core`; suite stays green; app builds.

## Triage decisions (2026-06-12, agent — per the issue's own recommendations)

- Click action: **copy to clipboard** (the safe default). No re-inject.
- Count: **fixed 3** (`RecentDictations.Capacity`), not configurable.
- Failure-case hint: **not now** — the InjectionBlocked notice already says the
  text is on the clipboard; the history is the silent second net. Revisit if
  HITL shows people don't find the submenu.

## Comments

**2026-06-12 (agent):** Built TDD (red-green per behaviour):

- Core `RecentDictations` — capacity 3, newest first, eviction, blank results
  ignored, plus a pure `Preview()` (whitespace collapse + 48-char ellipsis cut)
  for the menu labels. 7 unit tests.
- Core `DictationPipeline` got an optional `onResult` sink reporting the final
  (post-refinement) text — also on a blocked paste (clipboard is volatile, the
  history is the recovery net), never for non-speech. 3 unit tests.
- App: "Recent dictations" submenu atop the tray menu, rebuilt on every open;
  entries copy back to the clipboard fail-soft; "(no dictations yet)" disabled
  placeholder when empty. Both pipeline call sites pass `onResult`.
- RAM-only: the buffer lives in `TrayApplicationContext`; nothing is persisted.

Suite green (173/173). HITL check: `Blurt.exe --traymenu` seeds three sample
entries and pops the menu (screen was off during the agent run, so no
screenshot — the dev arg makes the manual check a 10-second job).

## Blocked by

- None. Builds on 03 (text injection) and 13 (fail-soft) — both done. Pairs with
  issue 21 (flex-slot) only incidentally.
