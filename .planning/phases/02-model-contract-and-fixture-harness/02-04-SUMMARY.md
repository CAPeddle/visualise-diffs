---
phase: 02-model-contract-and-fixture-harness
plan: 04
subsystem: testing
tags: [csharp, xunit, test-portability, ratchet, fixtures]

requires:
  - phase: 02-model-contract-and-fixture-harness
    provides: "02-01 OutputWriter.FileNames (tests no longer name output files)"
provides:
  - "Backlog 999.1 closed: both fsmonitor tests set the execute bit off Windows; GitObjectReaderTests.SamePath resolves links"
  - "Vdiff.Tests.Fixtures.Ratchet.Evaluate, OutputCheck, RatchetOutcome (expected-failure ratchet)"
  - "Vdiff.Tests.Fixtures.FixtureReport.Render / WriteIfRequested / Variable (VDIFF_FIXTURE_REPORT)"
  - "Vdiff.Tests.Fixtures.Shapes.All / IsShape (five shape names)"
affects: [02-07, 02-08, 02-09, 02-10, azure-pipelines]

actuals:
  tokens: 4800
  tasks: 2
  commits: 3
plan_head_before: d2d66ecfad5509b062492e8130d03cfd34bfd08f
plan_head_after: 887114adeba41792008b692b222e1ef2ca1865f5

tech-stack:
  added: []
  patterns:
    - "Pure static helper plus theory rows (as RepoHygieneTests.TrackedPathProblems); environment access passed in as a Func so tests change no process state"
    - "Ratchet branches on state alone; due is display text only"
    - "Every ratchet detail is fixed text (no path, SHA, type name or echoed value)"

key-files:
  created:
    - tests/Vdiff.Tests/Fixtures/Shapes.cs
    - tests/Vdiff.Tests/Fixtures/Ratchet.cs
    - tests/Vdiff.Tests/Fixtures/FixtureReport.cs
    - tests/Vdiff.Tests/Fixtures/RatchetTests.cs
  modified:
    - tests/Vdiff.Tests/Safety/RepoUnchangedMatrixTests.cs
    - tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs
    - tests/Vdiff.Tests/GitObjectReaderTests.cs
    - .planning/ROADMAP.md

key-decisions:
  - "Report separator line is '| --- | --- | --- | --- | --- |'; Render of no rows is header plus separator only"
  - "RED commit carries throwing stubs for Shapes, Ratchet and FixtureReport so the tests compile and fail on behaviour rather than on a build error"
  - "Ratchet matches state with ordinal literals only: 'Pending' or 'GREEN' is an unknown state, not a case-folded match"

patterns-established:
  - "FixtureReport.WriteIfRequested takes the environment getter as a parameter; callers pass Environment.GetEnvironmentVariable"

requirements-completed: []
requirements-advanced: [ACC-01]

coverage:
  - id: D1
    description: "Both fsmonitor tests set the execute bit with File.SetUnixFileMode when not on Windows; Windows behaviour unchanged"
    requirement: "ACC-01"
    verification:
      - kind: unit
        ref: "dotnet test --filter FullyQualifiedName~A_core_fsmonitor_hook_is_never_run (Windows: pass)"
        status: pass
    human_judgment: true
    rationale: "The Linux positive control can only be observed on a Linux machine or the Linux CI leg, which is not connected in this phase (D-09); record at phase UAT"
  - id: D2
    description: "GitObjectReaderTests.SamePath compares through FinalPath.Resolve; no change for paths without links on Windows"
    requirement: "ACC-01"
    verification:
      - kind: unit
        ref: "dotnet test --filter FullyQualifiedName~GitObjectReaderTests (Windows: pass)"
        status: pass
    human_judgment: true
    rationale: "The /var to /private/var case exists only on macOS, which has no CI leg (D-11); manual run on a Mac"
  - id: D3
    description: "Backlog 999.1 Test 10 marked done in ROADMAP.md with the closing plan"
    verification:
      - kind: other
        ref: "grep -cF -- '- [x] Test 10:' .planning/ROADMAP.md returns 1"
        status: pass
    human_judgment: false
  - id: D4
    description: "Ratchet.Evaluate truth table and vacuity rows: a failed tool, missing golden, missing output or schema-invalid output is FAIL for every state, never RED"
    requirement: "ACC-01"
    verification:
      - kind: unit
        ref: "tests/Vdiff.Tests/Fixtures/RatchetTests.cs (46 cases, 0 failed)"
        status: pass
    human_judgment: false
  - id: D5
    description: "FixtureReport.Render table and WriteIfRequested (LF, UTF-8 no BOM, no-op when unset); Shapes.All and IsShape"
    verification:
      - kind: unit
        ref: "tests/Vdiff.Tests/Fixtures/RatchetTests.cs"
        status: pass
    human_judgment: false

duration: 75min
completed: 2026-11-21
status: complete
---

# Phase 2 Plan 04: Off-Windows test fixes and expected-failure ratchet Summary

**Backlog 999.1 test-portability fixes (execute bit on the fsmonitor hook, link-resolving `SamePath`) plus a unit-tested `Ratchet.Evaluate` where a crashed or incomplete run is FAIL for every state and can never read as "red by design".**

## Performance

- **Duration:** about 75 min, 13 min of it the full suite
- **Tasks:** 2 (task 2 as RED, GREEN)
- **Files modified:** 8 (4 created, 4 modified)

## Accomplishments

- Both fsmonitor tests call `File.SetUnixFileMode(hook, UserRead | UserWrite | UserExecute)` when `!OperatingSystem.IsWindows()`, so the positive control can hold on Linux (T-02-11).
- `GitObjectReaderTests.SamePath` now goes through `FinalPath.Resolve` (with `using Vdiff.Core.Cli;`), mapping `/var` to `/private/var` on macOS.
- ROADMAP backlog 999.1 `Test 10` ticked, with "Closed in Phase 2 by plan 02-04 (D-11)."
- `Ratchet.Evaluate` implements the four-case table, checks tool, golden, output, schema in that order, and returns fixed-text details only (T-02-10, T-02-12).
- `FixtureReport.Render` / `WriteIfRequested` and `Shapes.All` / `IsShape` exist for plan 02-07.

## TDD Gate Compliance

- **RED** (`5171333`, `test(02-04)`): `RatchetTests` (46 cases) plus throwing stubs of `Shapes`, `Ratchet` and `FixtureReport`. Run: `Failed: 43, Passed: 3, Total: 46`. The 43 failures were the target tests failing on `NotImplementedException` and on the empty `Shapes.All` stub. The 3 passes were the `IsShape` rows expecting false, which a stub returning false satisfies. Semantic assessment: the target tests ran and failed for the planned reason (behaviour absent); no build error, no zero-test discovery. `workflow.tdd_mode` is not enabled and the plan is `type: execute`, so `gsd_run check tdd-red-evidence` was not run and no evidence file was written.
- **GREEN** (`887114a`, `feat(02-04)`): the three implementations; `Failed: 0, Passed: 46`.
- **REFACTOR:** none needed.

## Task Commits

1. **Task 1: Backlog 999.1 portability fixes and closure** - `9356f1e` (fix)
2. **Task 2 RED: failing ratchet, report and shape tests** - `5171333` (test)
3. **Task 2 GREEN: ratchet, report and shapes** - `887114a` (feat)

## Verification

- `GitObjectReaderTests` plus both fsmonitor tests: `Passed: 58, Failed: 0` (plan floor 3).
- `RatchetTests`: `Passed: 46, Failed: 0` (plan floor 20).
- Full suite `dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --filter "Category!=MermaidCli"`: `Passed: 623, Failed: 0, Total: 623` (13 min 14 s), run after the last code commit.
- `grep -rn SetUnixFileMode tests --include=*.cs` count 2; `grep -cF -- "- [x] Test 10:" .planning/ROADMAP.md` returns 1; acceptance greps for `Evaluate`, `"VDIFF_FIXTURE_REPORT"` and `"extract-class"` each return 1.
- Public-diff guard printed `clean` before all three commits. Edited and new files hold LF only (CR count 0).

## Deviations from Plan

None - plan executed exactly as written. The only liberty taken is the RED-phase stubs, which the plan's "see it fail to compile or pass" wording allows and which keep the RED run an assertion-level failure.

## Issues Encountered

None. The known `TraceRunScriptTests` flake did not recur in the full run.

## Known Stubs

None. The RED-phase stubs were replaced in the GREEN commit.

## Threat Flags

None. No new network, auth or file-access surface; `WriteIfRequested` writes only to a path the caller's environment names, and nothing calls it yet.

## Human checks outstanding (record at phase UAT)

- Linux: `dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --filter "FullyQualifiedName~A_core_fsmonitor_hook_is_never_run"`; both tests pass and the positive control holds.
- macOS: `--filter "FullyQualifiedName~GitObjectReaderTests.ToplevelOrGitDir"`; the tests pass.

## Next Phase Readiness

- 02-07 can consume `Ratchet.Evaluate`, `FixtureReport.WriteIfRequested(rows, Environment.GetEnvironmentVariable)` and `Shapes.All`.
- 02-11 shares `obj/` and `bin/` in this working tree; no builds are running from this plan.

## Self-Check: PASSED

- Files exist: Shapes.cs, Ratchet.cs, FixtureReport.cs, RatchetTests.cs under `tests/Vdiff.Tests/Fixtures/`.
- Commits `9356f1e`, `5171333`, `887114a` are on the branch; `git rev-list --count` from the recorded base gives 3.
