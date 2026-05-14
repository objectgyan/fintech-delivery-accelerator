# Phase Template — Linear Ticket Layout

Every Linear ticket in this project uses the same skeleton. Copy this into a new ticket and fill in.

```markdown
## Goal

{{One sentence — the outcome a future engineer would describe as "this ticket delivered X"}}

## Depends On

{{FT-NNN or "none"}}

## Scope

{{Bullets — what's IN this ticket. Be specific enough that scope creep is obvious.}}

## Out of Scope (optional but recommended)

{{Bullets — what's NOT in this ticket but adjacent. Pre-empts the "while you're in there..." trap.}}

## Acceptance Criteria

* {{checkbox-style bullets — observable, verifiable}}
* {{prefer "X happens when Y" over "X works"}}

## Validation

{{How will you prove the AC is met? Specific commands, scripts, endpoints to hit.}}

## Evidence

{{What goes in the ticket comments when marking Done — screenshots, log excerpts, trace IDs, output snippets}}
```

## Anti-patterns

- **"Implement X"** — too vague. What does success look like?
- **AC = "code review passes"** — code review confirms quality, not behaviour. Use observable AC.
- **"Validation: run tests"** — name *which* test or scenario specifically.

## Phase-rollup rule

When the last ticket in a phase merges, the next ticket — call it "Phase N demo verification" — does **two** things:

1. Run all scenarios from `docs/demo/phase{N}.md` end-to-end **twice**
2. Capture a fallback recording on the second clean run

Without that gate, "Phase complete" means nothing.
