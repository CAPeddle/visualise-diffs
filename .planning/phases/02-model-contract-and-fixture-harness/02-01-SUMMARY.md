---
phase: 02-model-contract-and-fixture-harness
plan: 01
subsystem: output
tags: [csharp, refactor, tests, output-writer]

requires:
  - phase: 01-offline-tracer
    provides: OutputWriter with five fixed output files, CliApp success line, test suite asserting the output set
provides:
  - OutputWriter.FileNames, the one list naming every file a run writes
  - CLI success line whose file count comes from that list
  - Seven test files reading the list instead of spelling out five names
affects: [02-11, 02-05, 02-07]

actuals:
  tokens: 4500
  tasks: 1
  commits: 1
plan_head_before: 41388baeb2c543c58e30cc8b51a332299815b187
plan_head_after: f67adad6a6421de76162845b94d562dcd5f3e995

tech-stack:
  added: []
  patterns:
    - "Output set is named once (OutputWriter.FileNames); tests derive expectations from it"

key-files:
  created: []
  modified:
    - src/Vdiff.Core/Output/OutputWriter.cs
    - src/Vdiff.Core/Output/HeldDirectory.cs
    - src/Vdiff.Core/Cli/CliApp.cs
    - tests/Vdiff.Tests/Output/DeterminismTests.cs
    - tests/Vdiff.Tests/TracerEndToEndTests.cs
    - tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs
    - tests/Vdiff.Tests/Safety/AliasedOutTests.cs
    - tests/Vdiff.Tests/Scope/IncludeGlobEndToEndTests.cs
    - tests/Vdiff.Tests/Safety/HostileInputTests.cs
    - tests/Vdiff.Tests/Safety/RepoUnchangedMatrixTests.cs

key-decisions:
  - "Tests that compare a sorted directory listing build their expectation as OutputWriter.FileNames.Order(StringComparer.Ordinal); exact equality stays exact equality"
  - "TraceRunScriptTests keeps a WroteLine constant ('wrote ' + count + ' files') so the three log-text assertions share one definition"

patterns-established:
  - "New output files are added to OutputWriter.FileNames and WriteAll; no test names output files"

requirements-completed: [R-23]

coverage:
  - id: D1
    description: "OutputWriter.FileNames names every output file once; the CLI success line takes N from it; run output is unchanged"
    requirement: "R-23"
    verification:
      - kind: unit
        ref: "dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --filter Category!=MermaidCli (559 passed, 0 failed)"
        status: pass
    human_judgment: false
  - id: D2
    description: "Every test that asserted the output set or the success-line count reads OutputWriter.FileNames with its meaning unchanged"
    requirement: "R-23"
    verification:
      - kind: unit
        ref: "tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs, Output/DeterminismTests.cs, Safety/AliasedOutTests.cs and four more"
        status: pass
    human_judgment: false

duration: 23min
completed: 2026-11-21
status: complete
---

# Phase 2 Plan 01: Shared output-file constant Summary

**`OutputWriter.FileNames` names the five output files once; the CLI success line and seven test files read it, so plan 02-11's sixth file needs no test edits.**

## Performance

- **Duration:** 23 min (the full suite alone takes about 11 min)
- **Started:** 2026-11-21T05:57:00Z
- **Completed:** 2026-11-21T06:20:00Z
- **Tasks:** 1
- **Files modified:** 10

## Accomplishments

- Added `public static readonly IReadOnlyList<string> FileNames` to `OutputWriter` (base model, head model, diff, mermaid, html, in write order).
- `CliApp.Run` now writes `vdiff: wrote {OutputWriter.FileNames.Count} files to ...`; `scripts/trace-run.ps1` is untouched and still copies that line.
- Replaced the hard-coded five-name arrays and the `wrote 5 files` literals in seven test files; `FiveFiles` is gone (renamed `ExpectedFiles`).
- Reworded the `OutputWriter.WriteAll` and `HeldDirectory` comments so none repeats the count.
- Output is byte-for-byte what it was: a run still writes the same five files.

## Task Commits

1. **Task 1: One constant names the output files; every test reads it** - `f67adad` (refactor)

**Plan metadata:** committed separately as `docs(02-01): complete shared output-file constant plan`.

## Files Created/Modified

- `src/Vdiff.Core/Output/OutputWriter.cs` - adds `FileNames`; doc comments no longer count files
- `src/Vdiff.Core/Output/HeldDirectory.cs` - comment only
- `src/Vdiff.Core/Cli/CliApp.cs` - success line count from `FileNames.Count`
- `tests/Vdiff.Tests/Output/DeterminismTests.cs`, `TracerEndToEndTests.cs`, `Safety/HostileInputTests.cs`, `Safety/RepoUnchangedMatrixTests.cs`, `Scope/IncludeGlobEndToEndTests.cs` - directory-listing expectations built from `FileNames`
- `tests/Vdiff.Tests/Safety/AliasedOutTests.cs` - `ExpectedFiles` and `OutputNames` alias `FileNames`
- `tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs` - `ExpectedFiles` and `WroteLine` built from `FileNames`

## Decisions Made

- Sorted-listing comparisons use `OutputWriter.FileNames.Order(StringComparer.Ordinal)`; the write-order list is used directly where tests iterate names.
- The three `"wrote 5 files"` assertions share one `WroteLine` constant in `TraceRunScriptTests`.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

- The first full-suite run reported one failure, `TraceRunScriptTests.Aliased_out_in_a_sibling_directory_whose_name_extends_the_repository_name_still_passes(host: "powershell")`, and was cut off by the shell timeout before printing a summary. The test passed when run alone (2 of 2) and the second complete run passed 559 of 559, so it reads as a load-related flake in a PowerShell-hosted trace test, not a result of this refactor. Worth watching if it recurs.
- `tests/Vdiff.Tests/Safety/RepoHygieneTests.cs` also lists the five output names (`GeneratedFileNames`, a gitignore-hygiene check). It was not in this plan's file list and was left alone. Plan 02-11 should decide whether `run.meta.json` needs adding there or whether that list should read `FileNames` too.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Ready for 02-11 (`run.meta.json`): adding the sixth file means appending it to `FileNames` and `WriteAll`; the suite's output-set assertions follow.
- 02-02 may now run (same wave); builds were run to completion here, so `obj/` and `bin/` are free.

## Self-Check: PASSED

- `OutputWriter.cs` holds `public static readonly IReadOnlyList<string> FileNames` (count 1); `CliApp.cs` holds `FileNames.Count` (count 1); `FiveFiles` count in `TraceRunScriptTests.cs` is 0; `wrote 5 files` appears nowhere in `src` or `tests`.
- Commit `f67adad` is on the branch; `git rev-list --count` from the recorded base gives 1.
- Plan verification: `dotnet test ... --filter "Category!=MermaidCli"` printed `Passed! - Failed: 0, Passed: 559`; no tracked `bin/` or `obj/` files.

---
*Phase: 02-model-contract-and-fixture-harness*
*Completed: 2026-11-21*
