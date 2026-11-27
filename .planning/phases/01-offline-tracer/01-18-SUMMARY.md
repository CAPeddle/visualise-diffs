---
phase: 01-offline-tracer
plan: 18
subsystem: safety
tags: [windows, drive-root, path-normalisation, held-directory, tdd, gap-closure, cr-01]
requires:
  - phase: 01-offline-tracer
    provides: "HeldDirectory.Create with SplitAtDeepestExisting (plan 01-16), the AfterHeld seam, LinkScratch, the script's Open-HeldDirectory (plan 01-17)"
provides:
  - "HeldDirectory.SplitAtDeepestExisting never trims below the root: --out given as a drive root is held as that root, not as the process's current directory on that drive"
  - "Ten drive_root tests: three root spellings, three below-root control spellings and a judgement fact (unit), one CliApp.Run case through the AfterHeld seam, two script parity cases (both PowerShell hosts)"
  - "01-SECURITY.md rows T-01-60 to T-01-62, T-01-SC (01-18), AR-18; 01-VALIDATION.md map row, manual-only row, refreshed counts"
affects: [01-VERIFICATION gap on SC2 and human_verification item 1, 01-REVIEW-DISPOSITION row CR-01, REQUIREMENTS TRC-02, 01-SECURITY next secure-phase run]
gap_closure: true
gap_ids: [CR-01]

actuals:
  tokens: 8400
  tasks: 3
  commits: 5
plan_head_before: e9ccf3ee2dda0e322cfb6f473a9ce78263b0767c
plan_head_after: 2a24052d652b0e4eab66d52f6f052ac29783bf75
commits: 5

tech-stack:
  added: []
  patterns:
    - "A trim of trailing separators is bounded by Path.GetPathRoot: the trimmed text is replaced by the root when it is shorter"
    - "A CLI case that must not write into a real root records the directory through a test seam and throws a test-local exception before the first byte"
    - "A script's own functions are extracted with the PowerShell parser into a driver run in a child host, to pin one rule in two implementations without a real run"

key-files:
  created: []
  modified:
    - src/Vdiff.Core/Output/HeldDirectory.cs
    - tests/Vdiff.Tests/Safety/HeldDirectoryTests.cs
    - tests/Vdiff.Tests/Safety/AliasedOutTests.cs
    - tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs
    - .planning/phases/01-offline-tracer/01-SECURITY.md
    - .planning/phases/01-offline-tracer/01-VALIDATION.md

key-decisions:
  - "P-1 applied: a drive-root --out is written as typed (exit 0, success line naming the typed path) or ends with exit 1 and the existing could-not-write line; no refusal text, argument, script reason or exit code added"
  - "P-2 applied: the rule is never trim below the root (start from the root when the trimmed text is shorter than Path.GetPathRoot, a null root counting as empty), not a bare length test, so C:\\/ style spellings and the slash root are covered"
  - "P-3 applied: the CLI case records held.Path through AfterHeld and throws before the first byte; no test writes into a root or changes the current directory"
  - "P-4 applied: scripts/trace-run.ps1 already held a root correctly and is unchanged; the parity is pinned by a parser-extraction driver test with a staged mutation"
  - "P-5 applied: register and validation rows added only; audit trail, counts, sign-off and the VERIFICATION, UAT, REVIEW and REVIEW-DISPOSITION files not edited"

requirements-completed: [TRC-01, TRC-02]

coverage:
  - id: D1
    description: "--out given as a drive root (three spellings) is held as that root and never as the process's current directory"
    requirement: TRC-02
    verification:
      - kind: unit
        ref: "tests/Vdiff.Tests/Safety/HeldDirectoryTests.cs#A_drive_root_is_held_as_the_root_and_never_as_the_current_directory"
        status: pass
    human_judgment: false
  - id: D2
    description: "CliApp.Run with --out at the drive root reaches the held directory as that root and stops before the first byte"
    requirement: TRC-02
    verification:
      - kind: integration
        ref: "tests/Vdiff.Tests/Safety/AliasedOutTests.cs#An_out_given_as_a_drive_root_reaches_the_held_directory_as_that_root_and_not_the_current_directory"
        status: pass
    human_judgment: false
  - id: D3
    description: "Directories one step below the root keep their behaviour (no, one and one alternate trailing separator); the final-path judgement still receives the root and a refusal creates nothing"
    requirement: TRC-01
    verification:
      - kind: unit
        ref: "tests/Vdiff.Tests/Safety/HeldDirectoryTests.cs#A_directory_below_a_drive_root_is_held_without_its_trailing_separator_and_not_widened_to_the_root"
        status: pass
      - kind: unit
        ref: "tests/Vdiff.Tests/Safety/HeldDirectoryTests.cs#A_drive_root_is_judged_as_the_root_by_the_final_path_function_and_a_refusal_creates_nothing"
        status: pass
    human_judgment: false
  - id: D4
    description: "The script's own Open-HeldDirectory and Get-FullPath hold a drive root as the root on both PowerShell hosts and agree with the CLI side"
    requirement: TRC-02
    verification:
      - kind: integration
        ref: "tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs#The_script_holds_a_drive_root_as_the_root_and_agrees_with_the_cli_drive_root"
        status: pass
    human_judgment: false
  - id: D5
    description: "A real run with --out at a drive root writes the five files into that root and none into the current directory (no test writes into a real root)"
    requirement: TRC-02
    verification: []
    human_judgment: true
    rationale: "Deliberately not automated so the development machine's root stays clean; recorded as a manual-only row in 01-VALIDATION.md"

duration: "about 70 min, most of it two full-suite-scale test runs"
completed: 2026-11-18
status: complete
---

# Phase 1 Plan 18: Drive-root --out held as the root Summary

**`HeldDirectory.SplitAtDeepestExisting` no longer trims a drive root to its drive-relative form (`C:`, the process's current directory), so `--out C:\` is held as `C:\` and the success line names a directory the files are in; closes review finding CR-01.**

## Accomplishments

- One guard in one private function: the trailing-separator trim now falls back to `Path.GetPathRoot` when the trimmed text is shorter than the root (`src/Vdiff.Core/Output/HeldDirectory.cs:182-206`). Only `HeldDirectory.cs` changed under `src/` and `scripts/`.
- Ten `drive_root` tests (7 unit, 1 CLI, 2 script) plus register rows T-01-60 to T-01-62, T-01-SC (01-18) and AR-18 and the validation-map rows.
- Full suite on Windows: 565 passed, 0 failed (555 plus ten new tests, as projected).

Drive-root outcome after plan 01-18: --out given as a drive root is held as that root; the five files are written into it as typed, or the run ends with exit 1 and the existing could-not-write line; nothing lands in the current directory.

T-01-39 still open after plan 01-18: subst and mapped drive letters, DOS 8.3 short names, and review finding IN-04.

## Task commits

| Task | Commit | Message |
|------|--------|---------|
| 1 (red) | 1567e09 | test(01-18): add failing tests for a drive-root --out held as the current directory |
| 1 (green) | 92ffb55 | fix(01-18): never trim a drive root to its drive-relative form in HeldDirectory |
| 2 | 58c186a | test(01-18): pin that the script holds a drive root as the root, like the CLI |
| 3 | 15d7100 | docs(01-18): add the T-01-60 to T-01-62 register rows |
| 3 | 2a24052 | docs(01-18): record the drive_root tests in the validation map |

`commits: 5` is measured from the ledger (`plan_head_before` to the last task commit); the SUMMARY commit follows and is not counted.

## Red evidence (task 1)

`--filter "FullyQualifiedName~drive_root"` at the base, tests only: 5 failed, 3 passed (Windows).

| Test | Assertion that failed | Path the test saw |
|------|----------------------|-------------------|
| `A_drive_root_is_held_as_the_root_and_never_as_the_current_directory` (root) | "the held directory is the process's current directory, not the typed root" | the process's current directory (the test output directory) |
| same, `root-with-alternate-separator` | same message | the process's current directory |
| same, `root-with-two-trailing-separators` | same message | the process's current directory |
| `A_drive_root_is_judged_as_the_root_by_the_final_path_function_and_a_refusal_creates_nothing` | `Assert.Throws` failure, no exception: the function was given the current directory, not the root, so it never refused | the current directory |
| `An_out_given_as_a_drive_root_reaches_the_held_directory_as_that_root_and_not_the_current_directory` | "the held directory is the process's current directory, not the typed root" (hook ran, exit 1 from the sentinel) | the process's current directory |

The three below-root control rows passed at the base and after the fix. After the fix: `drive_root` 8 passed (before the script test), held-directory filter 24 passed, `AliasedOutTests|FinalPathTests|HeldDirectoryTests` 91 passed. Non-Windows hosts were not observed (the red text there is the fixed unresolvable line, with the hook never run).

## Script parity (task 2)

- Control: `The_script_holds_a_drive_root_as_the_root_and_agrees_with_the_cli_drive_root` passed on both hosts (pwsh and Windows PowerShell 5.1) before any mutation, so `scripts/trace-run.ps1` already held a root correctly and was not changed.
- Mutation (staged, not committed): `Get-FullPath`'s guard `if ($full.Length -gt $root.Length)` replaced by `if ($true)`. Result: `Failed: 2`; on both hosts the first failing assertion was "Open-HeldDirectory did not hold the root", with the process's current directory as the value (the `Get-FullPath` value, `C:`, sits behind that assertion and was not reached). Restored with `git checkout -- scripts/trace-run.ps1`; the working tree and the index equal HEAD for the script, and the filter passed again (2 passed).
- `TraceRunScriptTests` 59 passed (57 plus 2).

## Deviations from Plan

None - plan executed exactly as written. Test counts matched the projections (8, 10, 24, 91, 59, 565).

Authoring note: the first draft of the script test used `Assert.Equal(string, string, StringComparison)`, which this xunit version lacks; it was changed to `Assert.True(string.Equals(...))` with messages before the first run. Not a plan deviation.

## Incidental

None observed beyond the following. The commit guard's protected-branch query (`git.base-branch`) was not available as a shell function in this run; the fallback name check applied and the branch `ccr-c12d6a45-a0eke4` is not protected. `.planning/config.json` (line-ending-only change) was never staged.

## Advisories left out (the gap closes CR-01 only)

WR-01 (extended-length prefix for the handle), WR-02 (link target in I/O exception text), WR-03 (tests for the script's handle verdict and final-path rule; this plan's parser-extraction driver is a means for one root case and is not the WR-03 deliverable), WR-04, WR-05, WR-06, IN-01 to IN-07 and the stale IN-08 row.

## Flagged to the orchestrator

- `01-REVIEW-DISPOSITION.md` row CR-01 can become `fixed` through the review gate (not edited here).
- `01-VERIFICATION.md` gap on SC2 and human_verification item 1 can be re-verified; TRC-02 can be marked complete in `REQUIREMENTS.md` after that.
- The register rows T-01-60 to T-01-62, T-01-SC (01-18) and AR-18 need the next secure-phase run to re-count (76 plus four) and to re-measure the `HeldDirectory.cs` line citations of T-01-51 to T-01-59 (they name lines at `ed8ee27`; the fix shifts the lines below `SplitAtDeepestExisting` by 11).
- The non-Windows run of the new tests is not observed on this host.

## Known Stubs

None.

## Threat Flags

None. No new network endpoint, auth path, file access pattern or schema change was introduced; the change narrows a path normalisation.

## Requirements bookkeeping

`requirements-completed: [TRC-01, TRC-02]` is copied from the plan. `REQUIREMENTS.md` was deliberately not changed: both rows read "Gaps Found" and the plan states TRC-02 is marked complete after the phase is re-verified, so the orchestrator's re-verification owns that edit.

## Self-Check: PASSED

- All five task commits (1567e09, 92ffb55, 58c186a, 15d7100, 2a24052) are ancestors of HEAD.
- No file was created other than this SUMMARY; every modified file listed in key-files exists.
- Plan-level gates re-run: only `HeldDirectory.cs` changed under `src` and `scripts`; `01-SECURITY.md` gained exactly five rows and lost no line; `01-UAT.md`, `01-VERIFICATION.md`, `01-REVIEW.md` and `01-REVIEW-DISPOSITION.md` unchanged; `scripts/trace-run.ps1` has no non-ASCII character; no `vdiff-links-*` directory left in the temp area; no test calls `SetCurrentDirectory`.
- Full suite: 565 passed, 0 failed.
