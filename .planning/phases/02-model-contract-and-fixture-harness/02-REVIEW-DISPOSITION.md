---
phase: 02
review: 02-REVIEW.md
titles: json
findings:
  - id: WR-01
    severity: warning
    disposition: open
    title: "MermaidWriter base resolution has drifted from BaseResolver (language, duplicates, separator)"
  - id: WR-02
    severity: warning
    disposition: open
    title: "Removed bases are drawn as ordinary inheritance edges"
  - id: WR-03
    severity: warning
    disposition: open
    title: "Member visibility maps internal, protected-internal and private-protected to the public marker"
  - id: WR-04
    severity: warning
    disposition: open
    title: "A malformed output file crashes the harness instead of producing the promised FAIL row"
  - id: WR-05
    severity: warning
    disposition: open
    title: "Private mode leaves stale diagnostic files from earlier runs in VDIFF_PRIVATE_OUT"
  - id: IN-01
    severity: info
    disposition: open
    title: "Writers do not yet cover the v1 states and counts the schema already allows"
  - id: IN-02
    severity: info
    disposition: open
    title: "Duplicated documentation blocks and repo-root finders"
  - id: IN-03
    severity: info
    disposition: open
    title: "Numeric limits repeated in message and usage text"
  - id: IN-04
    severity: info
    disposition: open
    title: "Model namespace depends on Output; canonical order is tied to the serialiser options"
  - id: IN-05
    severity: info
    disposition: open
    title: "Schema documents contradict themselves in two places"
  - id: IN-06
    severity: info
    disposition: open
    title: "Test hygiene: a literal invisible character, a stale name, a username-dependent assertion"
  - id: IN-07
    severity: info
    disposition: open
    title: "PrivateFixtures.RunShape overwrites VDIFF_PROTECTED_DIRS and mishandles two path shapes"
open: 12
total: 12
recorded: 2026-11-21T11:42:56.707Z
---

# Phase 02: Code Review Disposition

| Finding | Severity | Disposition | Source |
|---------|----------|-------------|--------|
| WR-01 | warning | open | - |
| WR-02 | warning | open | - |
| WR-03 | warning | open | - |
| WR-04 | warning | open | - |
| WR-05 | warning | open | - |
| IN-01 | info | open | - |
| IN-02 | info | open | - |
| IN-03 | info | open | - |
| IN-04 | info | open | - |
| IN-05 | info | open | - |
| IN-06 | info | open | - |
| IN-07 | info | open | - |

Dispositions: `open` (recorded, not yet triaged), `fixed`, `skipped`, `deferred`.
Set `deferred` by hand and put the reason in the Source cell; both are preserved. A `|` in the reason is kept as prose and escaped on the next run.
Re-running the gate keeps every row it can. A row the current review no longer reports is kept and its Source cell flagged, so a finding does not leave this record silently. ONE exception: when a finding id is REUSED by a different finding, the earlier decision cannot keep a row — the id is taken — and it is dropped. A RECORDED decision (anything but `open`) is named on the console when that happens; a row still at `open` is replaced silently, because `open` records no decision to lose.
