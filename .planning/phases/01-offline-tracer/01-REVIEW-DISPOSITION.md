---
phase: 01
review: 01-REVIEW.md
titles: json
findings:
  - id: CR-01
    severity: critical
    disposition: open
    title: "`--out` written as `\\\\?\\C:\\...` or `\\\\localhost\\C$\\...` bypasses the containment guard and writes inside the analysed repository"
  - id: WR-01
    severity: warning
    disposition: open
    title: "A pre-existing file link in `--out` redirects a write into the repository"
  - id: WR-02
    severity: warning
    disposition: open
    title: "Containment is checked once, minutes before the write"
  - id: IN-01
    severity: info
    disposition: open
    title: "The `IOException` branch still prints .NET exception text with full paths"
  - id: IN-02
    severity: info
    disposition: open
    title: "Relative link targets are never exercised on Windows"
  - id: IN-03
    severity: info
    disposition: open
    title: "Rooted-without-drive and drive-relative link targets resolve against the wrong drive"
  - id: IN-04
    severity: info
    disposition: open
    title: "Case-insensitive file systems outside Windows compare case-sensitively"
  - id: WR-03
    severity: warning
    disposition: open
    title: "The trial wrapper's \"repo_unchanged: yes\" ignores the object database, config, hooks and `.git` file set"
  - id: WR-04
    severity: warning
    disposition: open
    title: "`check.mjs` reports a browser launch failure as a Mermaid syntax failure"
  - id: IN-05
    severity: info
    disposition: open
    title: "The report page has no Content-Security-Policy"
  - id: IN-06
    severity: info
    disposition: open
    title: "Output writing is not atomic, and every `IOException` is reported as an output failure"
open: 11
total: 11
recorded: 2026-11-17T13:52:22.788Z
---

# Phase 01: Code Review Disposition

| Finding | Severity | Disposition | Source |
|---------|----------|-------------|--------|
| CR-01 | critical | open | - |
| WR-01 | warning | open | - |
| WR-02 | warning | open | - |
| IN-01 | info | open | - |
| IN-02 | info | open | - |
| IN-03 | info | open | - |
| IN-04 | info | open | - |
| WR-03 | warning | open | - (not in the current review) |
| WR-04 | warning | open | - (not in the current review) |
| IN-05 | info | open | - (not in the current review) |
| IN-06 | info | open | - (not in the current review) |

Dispositions: `open` (recorded, not yet triaged), `fixed`, `skipped`, `deferred`.
Set `deferred` by hand and put the reason in the Source cell; both are preserved. A `|` in the reason is kept as prose and escaped on the next run.
Re-running the gate keeps every row it can. A row the current review no longer reports is kept and its Source cell flagged, so a finding does not leave this record silently. ONE exception: when a finding id is REUSED by a different finding, the earlier decision cannot keep a row — the id is taken — and it is dropped. A RECORDED decision (anything but `open`) is named on the console when that happens; a row still at `open` is replaced silently, because `open` records no decision to lose.
