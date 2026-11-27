---
phase: 01-offline-tracer
plan: 10
subsystem: trial
tags: [powershell, trial-wrapper, private-run, read-only-proof, fixed-vocabulary, spk-02]

requires:
  - phase: 01-offline-tracer
    provides: "plans 01-04 to 01-09: the full tracer CLI, the sidecar, the writers and the local Mermaid syntax check"
provides:
  - "scripts/trace-run.ps1: a trial wrapper that snapshots git state, runs vdiff once, snapshots again and prints exactly six fixed-vocabulary lines"
  - "15 tests on synthetic repositories proving the script passes, fails closed with a fixed reason, and leaks no path, id or identity"
  - "the merged-suite check: whole .NET suite and whole cargo suite green after all earlier waves"
  - "docs/trials/phase-1-offline-tracer.md: the public record of the TRC-03 run (six-line block and date only) and the SPK-02 evidence"
affects: [phase-2, later-trials]

plan_head_before: 21724301ff8fb5208587f62b3c82b6cc493c1e7c
plan_head_after: c5e275a70b8f413216c31af93a748636a11bf544

actuals:
  tokens: 9000
  tasks: 3
  commits: 2

tech-stack:
  added: []
  patterns:
    - "Trial wrapper prints only a fixed vocabulary and never echoes a parameter, exception message or tool output"
    - "Every git call in the wrapper carries -c core.fsmonitor=false, --no-optional-locks and GIT_NO_LAZY_FETCH=1, so the wrapper's own status calls cannot run a hook or start a daemon in the analysed clone"
    - "The wrapper reaches mermaid-cli only through the fixed tools/mermaid-check/check.mjs (raised text-size limit), never directly"

key-files:
  created:
    - scripts/trace-run.ps1
    - tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs
    - docs/trials/phase-1-offline-tracer.md
  modified:
    - tests/Vdiff.Tests/Vdiff.Tests.csproj

key-decisions:
  - "The public record holds only the six-line block, the run date and generic text; no clone location, commit ids, output location, macro names, counts or timings beyond the bucket were written to any file or commit message"
  - "TRC-03 and SPK-02 are recorded as met: the single run passed, and ADR-001 (accepted 2026-11-16) is the basis of SPK-02; the ADR file was left unedited"

patterns-established:
  - "Private-target trials run through the committed wrapper with inputs typed at run time only; the repository keeps pass or fail, a time bucket and repo unchanged"

requirements-completed: [TRC-03, SPK-02]

coverage:
  - id: D1
    description: "Trial wrapper script prints exactly six fixed-vocabulary lines, is read-only by construction, refuses an output directory inside the analysed repository or this repository, and fails closed with a fixed reason"
    requirement: TRC-03
    verification:
      - kind: unit
        ref: "tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs (15 passed: clean, dirty tree, failure reasons, pass-through, fsmonitor marker, large diagram)"
        status: pass
    human_judgment: false
  - id: D2
    description: "The merged result of all earlier waves passes before the real run"
    verification:
      - kind: integration
        ref: "dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --nologo (421 passed, 0 failed); cargo test --locked --manifest-path extractors/cpp-ts/Cargo.toml (105 passed, 0 failed)"
        status: pass
    human_judgment: false
  - id: D3
    description: "One run of the tracer on a private local clone completed, repository unchanged, output files present, recorded as pass with a wall-clock bucket"
    requirement: TRC-03
    verification: []
    human_judgment: true
    rationale: "The clone and the commit ids exist only with the product owner and must never enter this public repository; the orchestrator ran the wrapper and reported the six-line block, which is the only evidence kept"
  - id: D4
    description: "SPK-02 recorded as satisfied by ADR-001 (accepted 2026-11-16) and the sidecar in use; ADR left unedited"
    requirement: SPK-02
    verification:
      - kind: other
        ref: "grep of the ADR status line returns 1; docs/trials/phase-1-offline-tracer.md SPK-02 section"
        status: pass
    human_judgment: false

duration: 45min
completed: 2026-11-17
status: complete
---

# Phase 1 Plan 10: Private-clone trial wrapper and record Summary

**A tested read-only PowerShell wrapper ran the tracer once on a private local clone and recorded only a pass, a time bucket and "repository unchanged: yes", closing TRC-03 and SPK-02 without any target identifier in this repository.**

## Performance

- **Duration:** 45 min (across the Task 1 executor, the orchestrator-run Task 2 and this continuation)
- **Completed:** 2026-11-17T11:11:00Z
- **Tasks:** 3 (Task 2 was a human-action step performed by the orchestrator)
- **Files modified:** 4 (3 created, 1 modified)

## Accomplishments

- `scripts/trace-run.ps1` runs under Windows PowerShell 5.1 and PowerShell 7, validates its arguments, refuses an output directory inside the analysed repository (work tree or git directory) or inside this repository, hashes a before and after git snapshot, times only the vdiff call and prints six fixed lines.
- 15 tests on synthetic repositories cover a clean run, a dirty tree with neither commit checked out, every failure reason, `-Blank` and `-Include` pass-through, an fsmonitor hook that must not fire, and a diagram larger than 50,000 characters.
- The merged suites pass: 421 .NET tests and 105 cargo tests, 0 failures.
- The TRC-03 run was performed by the orchestrator with inputs typed at run time. Result: pass, reason none, wall clock under 1 minute, repository unchanged yes, outputs present yes, Mermaid syntax ok. Run date 2026-11-17. This is a single run against the R26 budget, not a P90.
- `docs/trials/phase-1-offline-tracer.md` records the block, the date, the SPK-02 evidence, the pending items and the two known C++ extraction gaps.

## Task Commits

1. **Task 1: Trial wrapper script and the merged-suite check** - `094dc23` (feat)
2. **Task 2: Orchestrator runs the tracer on the private clone** - no commit (human-action; nothing about the run is committed beyond Task 3's block)
3. **Task 3: Record the outcome and close SPK-02** - `c5e275a` (docs)

**Plan metadata:** recorded in the docs commit that follows this file.

## Files Created/Modified

- `scripts/trace-run.ps1` - the trial wrapper (snapshot, run, snapshot, six coarse fields)
- `tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs` - proof on synthetic repositories that the script passes, fails closed and leaks nothing
- `tests/Vdiff.Tests/Vdiff.Tests.csproj` - project reference to `Vdiff.Cli` so `vdiff.dll` lands in the test output
- `docs/trials/phase-1-offline-tracer.md` - public record, fixed vocabulary only

## Decisions Made

See `key-decisions`. No deviation from the plan's design.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None in this continuation. Task 3's identifier scan (six-line count 6, commit-id-like count 0, path count 0, ADR status count 1, SUMMARY path count 0) returned the expected values on the first run.

## Known Stubs

None.

## Authentication Gates

None. Task 2 was a human-action checkpoint for private inputs, not an authentication gate; the orchestrator resolved it.

## Threat Flags

None. T-01-23 to T-01-26 are mitigated by the fixed six-line vocabulary, the grammar-and-no-leak tests, the before and after git snapshot with fsmonitor disabled, and the output-directory refusal. T-01-SC: no new package installs.

## Next Phase Readiness

Phase 1 plan set is complete. ROADMAP criteria 4 (TRC-03) and 5 (SPK-02) are met. Known C++ extraction gaps (anonymous-struct typedef, macro call with arguments in a class body) are labelled in the trial record and not fixed in Phase 1. The Ubuntu-runner question is deferred until after this first pass.

## Self-Check: PASSED

- Files present: `scripts/trace-run.ps1`, `tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs`, `docs/trials/phase-1-offline-tracer.md`.
- Commits present: `094dc23`, `c5e275a` (2 measured from the plan-start ledger before this SUMMARY's own commit).
- ADR-001 unedited; status line count 1.
