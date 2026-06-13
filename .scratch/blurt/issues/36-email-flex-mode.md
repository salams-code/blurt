# 36 — Email as a Flex-slot mode

Status: done
Type: AFK feature

## Parent

[.scratch/blurt/PRD.md](../PRD.md)

## What to build

Add **Email** as a new Flex-slot mode alongside Pur / Bullets / Custom, so a Flex
tap cycles onto it and a Flex hold dictates with it. Its default prompt turns
conversational speech into a proper, well-formed email — the user talks the way
they'd talk to a person, and the output is the same content in email form (greeting,
body, sign-off as appropriate), not a verbatim transcript.

It plugs into the editable-prompt infrastructure from issue 35: the Email prompt is
one more editable per-mode field, pre-filled with our default. The Flex overlay
flash and the live-status verb cover the new mode (a sensible label/colour and a
"emailing…"-style status).

## Acceptance criteria

- [x] Email appears in the Flex cycle; tapping reaches it, holding dictates with it.
- [x] Holding on Email produces a formatted email from conversational speech.
- [x] The Email prompt is editable in Settings (pre-filled with the default).
- [x] The Flex mode flash and the live-status overlay show Email distinctly.
- [x] Mode + default prompt live in `Blurt.Core`, unit-tested; suite green.

## Blocked by

- Issue 35 (editable per-mode prompts) — Email's prompt slots into that infrastructure.

## Comments

Built Email as the fourth Flex-slot mode, reusing the issue-35 editable-prompt
infrastructure end to end (strict TDD: each behaviour observed RED before
implementing; UI shell exempt).

**Core (unit-tested, 235 tests green):**

- `FlexSlotMode.Email` + `RefinedMode.Email` enums.
- `RefinementPrompts.Email` — the default prompt: rewrites conversational speech
  into a well-formed email (greeting / body / sign-off), language-agnostic and
  content-preserving, returns only the email text.
- `ModePrompts` resolves Email (default + stored override); `FlexSlotPrompts.For`
  maps `FlexSlotMode.Email → ModePrompts.For(RefinedMode.Email, …)` so an edit
  applies on the next dictation, no restart.
- `BlurtConfig.EmailPrompt` (defaults to the shipped prompt; in Equals/GetHashCode;
  round-trips; a pre-issue-36 config with no key deserialises to the default —
  backward-compatible). Email added to the default `FlexSlotOrder`
  (Pur → Bullets → Custom → Email) so a tap reaches it.
- `FlexSlotOverlay.Label` "✉ Email" + distinct teal dot (never a status/idle
  colour); `StatusLabel.Emailing` live-status verb (distinct, ellipsis-free).

**App shell:** tray verb selection maps Email → `StatusLabel.Emailing`; Settings
gains an editable "Email" prompt box (pre-filled from config) and the Flex-order
hint lists Email. `ParseFlexOrder` already case-insensitively parses the token.

Note: existing installs that previously persisted a 3-mode `FlexSlotOrder`
(Pur, Bullets, Custom) won't auto-gain Email in their cycle until they add it via
Settings' Mode-order field — no migration was specified by the issue. New installs
get it by default.

HITL UX check recommended: tap the Flex hotkey to confirm the cycle reaches the
teal "✉ Email" flash, hold on Email and confirm a conversational dictation comes
out as a formatted email, and confirm the Email prompt is editable in Settings and
takes effect on the next dictation.
