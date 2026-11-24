---
phase: 01
review: 01-REVIEW.md
titles: json
findings:
  - id: IN-01
    severity: info
    disposition: open
    title: "`SplitAtDeepestExisting` still accepts a non-fully-qualified path; the drive-relative case is defended only upstream"
  - id: IN-02
    severity: info
    disposition: open
    title: "The keep-the-root trim is written four times"
  - id: IN-03
    severity: info
    disposition: open
    title: "The root tests fail, not skip, when the host runs from a drive root"
  - id: CR-01
    severity: critical
    disposition: open
    title: "`--out` at a drive root writes into the current directory and reports the root"
  - id: WR-01
    severity: warning
    disposition: open
    title: "The held handle is opened without the extended-length prefix, so a long output path can now fail"
  - id: WR-02
    severity: warning
    disposition: open
    title: "A link target now reaches stderr through I/O exception text"
  - id: WR-03
    severity: warning
    disposition: open
    title: "The script's judgement by handle, its protected verdict and its final-path rule are not tested"
  - id: WR-04
    severity: warning
    disposition: open
    title: "A path separator inside the vdiff root breaks the `VDIFF_PROTECTED_DIRS` handoff"
  - id: WR-05
    severity: warning
    disposition: open
    title: "(carried forward from WR-05) A relative or drive-less link target is refused with a message that names the wrong problem"
  - id: WR-06
    severity: warning
    disposition: open
    title: "(carried forward from WR-01) The file-symbolic-link test passes vacuously without the privilege"
  - id: IN-04
    severity: info
    disposition: open
    title: "(carried forward from IN-08) The swap-interval paragraph is now copied into six comment blocks and the class summary of `HeldDirectory` contains two overlapping paragraphs"
  - id: IN-05
    severity: info
    disposition: open
    title: "(carried forward from IN-02) More tests report as passed on hosts where they do nothing"
  - id: IN-06
    severity: info
    disposition: open
    title: "(carried forward from IN-03 and IN-04) `TooManyLinksException` is thrown for unrelated conditions; case-insensitive non-Windows file systems are compared case-sensitively"
  - id: IN-07
    severity: info
    disposition: open
    title: "(carried forward from IN-07) The new tests still omit the symbolic-link and UNC spellings of the namespace escape"
  - id: IN-08
    severity: info
    disposition: open
    title: "The same ten-line \"Bound (T-01-39 / T-01-41)\" paragraph now exists in four places"
open: 15
total: 15
recorded: 2026-11-18T21:27:57.883Z
---

# Phase 01: Code Review Disposition

| Finding | Severity | Disposition | Source |
|---------|----------|-------------|--------|
| IN-01 | info | open | - |
| IN-02 | info | open | - |
| IN-03 | info | open | - |
| CR-01 | critical | fixed | plan 01-18, commit 92ffb55; confirmed fixed by the 01-18-scope review (ed3d74b1) |
| WR-01 | warning | open | - (not in the current review) |
| WR-02 | warning | open | - (not in the current review) |
| WR-03 | warning | open | - (not in the current review) |
| WR-04 | warning | open | - (not in the current review) |
| WR-05 | warning | open | - (not in the current review) |
| WR-06 | warning | open | - (not in the current review) |
| IN-04 | info | open | - (not in the current review) |
| IN-05 | info | open | - (not in the current review) |
| IN-06 | info | open | - (not in the current review) |
| IN-07 | info | open | - (not in the current review) |
| IN-08 | info | open | - (not in the current review) |

Dispositions: `open` (recorded, not yet triaged), `fixed`, `skipped`, `deferred`.
Set `deferred` by hand and put the reason in the Source cell; both are preserved. A `|` in the reason is kept as prose and escaped on the next run.
Re-running the gate keeps every row it can. A row the current review no longer reports is kept and its Source cell flagged, so a finding does not leave this record silently. ONE exception: when a finding id is REUSED by a different finding, the earlier decision cannot keep a row — the id is taken — and it is dropped. A RECORDED decision (anything but `open`) is named on the console when that happens; a row still at `open` is replaced silently, because `open` records no decision to lose.
