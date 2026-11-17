---
phase: 01
review: 01-REVIEW.md
titles: json
findings:
  - id: CR-01
    severity: critical
    disposition: open
    title: "Mermaid class ids that are Mermaid keywords make the whole diagram unparseable"
  - id: WR-01
    severity: warning
    disposition: open
    title: "`--out` containment check is lexical only and can be bypassed by an alias of the repository path"
  - id: WR-02
    severity: warning
    disposition: open
    title: "The sidecar answer is not cross-checked against the request, so the client does not fail closed on a missing or invented file"
  - id: WR-03
    severity: warning
    disposition: open
    title: "The trial wrapper's \"repo_unchanged: yes\" ignores the object database, config, hooks and `.git` file set"
  - id: WR-04
    severity: warning
    disposition: open
    title: "`check.mjs` reports a browser launch failure as a Mermaid syntax failure"
  - id: IN-01
    severity: info
    disposition: open
    title: "A panic in one file's extraction fails the whole sidecar run instead of labelling that file `error`"
  - id: IN-02
    severity: info
    disposition: open
    title: "Inheritance edges do not show added versus removed, and base resolution by suffix can pick a wrong type"
  - id: IN-03
    severity: info
    disposition: open
    title: "Macro blanking cost is unbounded and outside the per-file parse budget"
  - id: IN-04
    severity: info
    disposition: open
    title: "Timeouts in the wrapper and the Mermaid check do not kill the process tree"
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
recorded: 2026-11-17T11:20:20.840Z
---

# Phase 01: Code Review Disposition

| Finding | Severity | Disposition | Source |
|---------|----------|-------------|--------|
| CR-01 | critical | open | - |
| WR-01 | warning | open | - |
| WR-02 | warning | open | - |
| WR-03 | warning | open | - |
| WR-04 | warning | open | - |
| IN-01 | info | open | - |
| IN-02 | info | open | - |
| IN-03 | info | open | - |
| IN-04 | info | open | - |
| IN-05 | info | open | - |
| IN-06 | info | open | - |

Dispositions: `open` (recorded, not yet triaged), `fixed`, `skipped`, `deferred`.
