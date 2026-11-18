---
phase: 01
review: 01-REVIEW.md
titles: json
findings:
  - id: WR-05
    severity: warning
    disposition: open
    title: "Every non-drive-letter link target is now refused on Windows, with a message that names the wrong problem"
  - id: IN-07
    severity: info
    disposition: open
    title: "The new tests do not exercise the symbolic-link spelling of the escape, and the CLI and script tests omit the UNC kind"
  - id: IN-08
    severity: info
    disposition: open
    title: "The same ten-line \"Bound (T-01-39 / T-01-41)\" paragraph now exists in four places"
  - id: CR-01
    severity: critical
    disposition: open
    title: "A directory junction with a non-`Volume{` NT-namespace target bypasses both containment guards; vdiff and trace-run.ps1 write inside the analysed repository"
  - id: WR-01
    severity: warning
    disposition: open
    title: "The file-symbolic-link half of the output-name test passes vacuously on a host without the privilege"
  - id: WR-02
    severity: warning
    disposition: open
    title: "Necessity: a second `Test-NonDriveRoot $logFull` after the vdiff run re-checks a string that cannot have changed"
  - id: IN-01
    severity: info
    disposition: open
    title: "Stale handoff committed to a public repository"
  - id: IN-02
    severity: info
    disposition: open
    title: "Source-text pin and silent non-Windows passes in the script tests"
  - id: IN-03
    severity: info
    disposition: open
    title: "`TooManyLinksException` is thrown for unrelated conditions"
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
open: 14
total: 14
recorded: 2026-11-18T12:11:56.374Z
---

# Phase 01: Code Review Disposition

| Finding | Severity | Disposition | Source |
|---------|----------|-------------|--------|
| WR-05 | warning | open | - |
| IN-07 | info | open | - |
| IN-08 | info | open | - |
| CR-01 | critical | open | - (not in the current review) |
| WR-01 | warning | open | - (not in the current review) |
| WR-02 | warning | open | - (not in the current review) |
| IN-01 | info | open | - (not in the current review) |
| IN-02 | info | open | - (not in the current review) |
| IN-03 | info | open | - (not in the current review) |
| IN-04 | info | open | - (not in the current review) |
| WR-03 | warning | open | - (not in the current review) |
| WR-04 | warning | open | - (not in the current review) |
| IN-05 | info | open | - (not in the current review) |
| IN-06 | info | open | - (not in the current review) |

Dispositions: `open` (recorded, not yet triaged), `fixed`, `skipped`, `deferred`.
Set `deferred` by hand and put the reason in the Source cell; both are preserved. A `|` in the reason is kept as prose and escaped on the next run.
Re-running the gate keeps every row it can. A row the current review no longer reports is kept and its Source cell flagged, so a finding does not leave this record silently. ONE exception: when a finding id is REUSED by a different finding, the earlier decision cannot keep a row — the id is taken — and it is dropped. A RECORDED decision (anything but `open`) is named on the console when that happens; a row still at `open` is replaced silently, because `open` records no decision to lose.
