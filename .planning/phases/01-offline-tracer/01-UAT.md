---
status: testing
phase: 01-offline-tracer
source: [01-VERIFICATION.md]
started: 2026-11-17T00:00:00Z
updated: 2026-11-17T00:00:00Z
---

## Current Test

number: 1
name: Decide the disposition of re-review findings CR-01, WR-01 and WR-02
expected: |
  Either (a) the product owner accepts that the --out guard stops accidental and link-based --out choices but not a deliberately obscure spelling (\\?\, loopback UNC, a pre-planted hard link in --out, a swap after the check), and T-01-39 is widened to name those forms in the doc comments of CliApp.RefuseUnsafeOut and FinalPath.Resolve and the trace-run.ps1 header; or (b) a gap-closure plan is raised that canonicalises both sides through a final-path call (or refuses a root starting with two backslashes on Windows), writes through a temp name plus move, and re-checks just before the write.
awaiting: user response

## Tests

### 1. Decide the disposition of re-review findings CR-01, WR-01 and WR-02
expected: Either (a) accept as residuals and widen T-01-39 to name \\?\, loopback UNC, pre-planted hard links and post-check swaps; or (b) raise a hardening gap-closure plan (final-path canonicalisation or refuse \\ roots, temp-name-plus-move writes, re-check before write) in both the CLI and trace-run.ps1.
result: [pending]

## Summary

total: 1
passed: 0
issues: 0
pending: 1
skipped: 0
blocked: 0

## Gaps
