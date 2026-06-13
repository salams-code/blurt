# 36 — Email as a Flex-slot mode

Status: ready-for-agent
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

- [ ] Email appears in the Flex cycle; tapping reaches it, holding dictates with it.
- [ ] Holding on Email produces a formatted email from conversational speech.
- [ ] The Email prompt is editable in Settings (pre-filled with the default).
- [ ] The Flex mode flash and the live-status overlay show Email distinctly.
- [ ] Mode + default prompt live in `Blurt.Core`, unit-tested; suite green.

## Blocked by

- Issue 35 (editable per-mode prompts) — Email's prompt slots into that infrastructure.
