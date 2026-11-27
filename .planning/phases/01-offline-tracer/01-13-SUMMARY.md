---
phase: 01-offline-tracer
plan: 13
subsystem: cli-output-safety
tags: [out-guard, path-namespaces, hard-link, toctou, gap-closure]
requires: [01-12]
provides:
  - "FinalPath.UnsupportedRootMessage and a Windows accept-list on the full path text"
  - "OutputWriter writes through a temp name plus File.Move (replaces planted links)"
  - "CliApp.RefuseUnsafeOut re-run immediately before OutputWriter.WriteAll; internal Run overload with a test seam"
affects: [01-14]
tech-stack:
  added: []
  patterns: ["accept-list on full path text, not on GetPathRoot", "temp name with CreateNew then move with overwrite", "internal test seam via InternalsVisibleTo"]
key-files:
  created:
    - tests/Vdiff.Tests/Support/PathSpellings.cs
  modified:
    - src/Vdiff.Core/Cli/FinalPath.cs
    - src/Vdiff.Core/Cli/CliApp.cs
    - src/Vdiff.Core/Output/OutputWriter.cs
    - src/Vdiff.Core/Vdiff.Core.csproj
    - tests/Vdiff.Tests/Support/LinkScratch.cs
    - tests/Vdiff.Tests/Safety/AliasedOutTests.cs
    - tests/Vdiff.Tests/Safety/FinalPathTests.cs
decisions:
  - "K-1..K-7 of the plan applied as written (accept-list on full path text, fixed UnsupportedRootMessage, UNC-hosted repos refused, temp name plus move, second RefuseUnsafeOut with an internal seam, volume GUID link target refused, T-01-39 re-worded at the guard)"
  - "T-01-40 is mitigated and tracked as T-01-43 (not re-registered, to avoid a duplicate threat ID)"
metrics:
  tasks: 3
  tests_before: 455
  tests_after: 474
status: complete
actuals:
  tokens: 40000
  tasks: 3
  commits: 6
commits: 6
plan_head_before: b856aab6198a29f462464ebef8a901995301a3d3
plan_head_after: ba58ca63c566b267a2645e39ea47931a230e1b9a
completed: 2026-11-17
---

# Phase 01 Plan 13: --out guard hardening on the CLI side Summary

The CLI now refuses an `--out` in any non-drive Windows namespace, replaces (instead of writing through) a link or hard link planted at an output name, and re-checks containment immediately before the write, closing CR-01, WR-01 and WR-02 of the re-review.

`commits: 6` is the measured count of `b856aab..HEAD`; it includes `47d7b94`, the handoff doc commit made between the interrupted and the resumed run. The five plan commits are listed below.

## Commits

| Task | Phase | Commit | Message |
|------|-------|--------|---------|
| 1 | GREEN (tests and rule in one commit) | 6fe8d89 | feat(01-13): refuse an --out in a non-drive Windows namespace |
| 2 | RED | f495d6b | test(01-13): add failing tests for link replacement at output names |
| 2 | GREEN | ee5a6bc | feat(01-13): write outputs through a temp name and move, replacing planted links |
| 3 | RED | 2132858 | test(01-13): add failing swap-after-check test and the Run test seam |
| 3 | GREEN | ba58ca6 | feat(01-13): re-check containment immediately before the write |

## What was built

- **Task 1 (CR-01).** `FinalPath.UnsupportedRootMessage` and, on Windows, an accept-list in `FinalPath.Step`: the full path must start with an ASCII letter, colon, backslash. It covers `--out`, the three git roots and every link target; a link whose raw target is a volume GUID path is refused with the same fixed line. `PathSpellings` (new test support) spells one directory in the extended, device, loopback UNC, extended UNC and volume GUID forms.
- **Task 2 (WR-01).** `OutputWriter.WriteText` creates `{name}.tmp-{guid}` with `FileMode.CreateNew`, then `File.Move(..., overwrite: true)`; the temp file is deleted on failure. Bytes written are unchanged. `LinkScratch` gained `HardLink` and `TryFileLink`.
- **Task 3 (WR-02).** `CliApp.Run` has an internal overload with an `afterPipeline` hook; `RefuseUnsafeOut` (now internal) runs a second time between the hook and `OutputWriter.WriteAll`. `InternalsVisibleTo Vdiff.Tests` added to `Vdiff.Core.csproj`.

## TDD record (red steps)

- **Task 1.** The earlier (interrupted) executor ran red first and recorded in the commit message: 11 tests failed before the rule, 31 pass after. Tests and rule were committed together in `6fe8d89`, so there is no separate `test(...)` commit for task 1. This is a TDD gate gap (RED observed, not committed separately); flagged here rather than rewritten. At the start of this resumed run, task 1 was re-confirmed green: of the 34 AliasedOutTests plus FinalPathTests, 33 passed and only the task 2 RED test failed.
- **Task 2.** RED (`f495d6b`): `A_hard_link_planted_at_every_output_name_is_replaced_and_the_repository_files_are_unchanged` failed because the repository victim file was overwritten (`Expected: "original 1"`, `Actual: "{ "schema": "vdiff-model/v0-tracer", ...`); re-confirmed at the start of this run (1 failed, 33 passed of 34 in AliasedOutTests+FinalPathTests). Semantic assessment: the target test executed and failed on the planned assertion (victim content changed), the intended reason. `A_failed_write_leaves_no_temporary_file_behind` passed before the change (as the plan predicted). `A_file_symbolic_link_planted...` returns early on this host when `File.CreateSymbolicLink` is refused; it was not verified here whether the privilege is held, and it was **not run under WSL**, so it may have asserted nothing on this host. GREEN `ee5a6bc`: AliasedOutTests 19 passed; TracerEndToEnd, Determinism, RepoUnchangedMatrix and HtmlWriter 30 passed.
- **Task 3.** RED (`2132858`): `An_out_swapped_for_a_link_into_the_repository_after_the_first_check_is_refused_before_the_write` failed with `Expected: 1, Actual: 0` (five files written through the swapped junction; the hook ran, asserted). `A_protected_root_in_a_non_drive_namespace_is_refused_even_when_out_is_a_plain_path` passed (a guard once task 1 landed). Semantic assessment: intended failure. GREEN `ba58ca6`: AliasedOutTests 21 passed.

## Verification

- Full suite: `dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --nologo` -> Passed 474, Failed 0, Skipped 0 (455 before plus 19 added: 12 in AliasedOutTests, 7 in FinalPathTests).
- `ls $TEMP | grep -c '^vdiff-links-'` -> 0 (no scratch link directory left).
- Acceptance greps: no `File.WriteAllBytes` in OutputWriter outside comments (0); `File.Move` 1, `FileMode.CreateNew` 2, `T-01-42` 1; the second `RefuseUnsafeOut(outPath, location)` sits between `TracerPipeline.Run(` and `OutputWriter.WriteAll(` (1), 3 non-comment call/definition sites in CliApp; `InternalsVisibleTo` 1; `T-01-40` 2 and `T-01-43` 2 in CliApp; `UnsupportedRootMessage` 4, `T-01-41` 3, `IN-04` 1 in FinalPath; `T-01-39` cited once each in FinalPath and CliApp.
- The public-repo target-name guard printed "clean" on every staged diff.
- WSL: not run (the symbolic-link and relative-target tests assert only on a host that can create the link; this is open for a privileged or Linux run).

T-01-39 still open after plans 01-13 and 01-14: subst and mapped drive letters, DOS 8.3 short names, and review findings IN-03 and IN-04. Other Windows namespaces and volume GUID link targets are refused, not resolved (T-01-41).

## Deviations from Plan

None in behaviour. Process notes:

- Task 1 was committed as one `feat` commit with its tests (earlier executor), not as a separate RED commit; see the TDD record.
- Task 3's RED commit `2132858` includes the seam and the csproj item together with the failing test, because the test does not compile without them (the plan's step 2 describes the seam as a no-behaviour-change prerequisite).
- Test counts per task: the plan expected 19 AliasedOutTests after task 2 and 21 after task 3; both observed.

## Known Stubs

None.

## Threat Flags

None beyond the plan's register (T-01-41, T-01-42, T-01-43). Residuals as stated at the code: a process killed between the temp create and the move can leave one `*.tmp-*` file in `--out`; the interval between the second check and the first write is not closed.

## Self-Check: PASSED

- Commits 6fe8d89, f495d6b, ee5a6bc, 2132858, ba58ca6 are ancestors of HEAD.
- Files present: src/Vdiff.Core/Cli/FinalPath.cs, CliApp.cs, src/Vdiff.Core/Output/OutputWriter.cs, src/Vdiff.Core/Vdiff.Core.csproj, tests/Vdiff.Tests/Support/PathSpellings.cs, LinkScratch.cs, tests/Vdiff.Tests/Safety/AliasedOutTests.cs, FinalPathTests.cs.
