---
phase: 01-offline-tracer
plan: 16
subsystem: safety
tags: [windows, createfilew, getfinalpathnamebyhandle, output-guard, tdd, gap-closure]
requires:
  - phase: 01-offline-tracer
    provides: "RefuseUnsafeOut, FinalPath.Resolve, OutputWriter temp-name-plus-move writes (plans 01-12 to 01-15)"
provides:
  - "HeldDirectory: the --out directory opened without delete sharing, final path read from the handle, judged, and used for every file operation"
  - "CliHooks test seam (AfterPipeline, AfterSecondCheck, AfterHeld) and CliApp.FindRefusal"
  - "15 tests (held_directory, HeldDirectoryTests) with three recorded mutation checks"
affects: [01-17 script -Out and -LogPath guard, 01-SECURITY T-01-43 row]
gap_closure: true
gap_ids: [G-01-1]

actuals:
  tokens: 10300
  tasks: 3
  commits: 6
plan_head_before: 65261d3e43d5a1e550c52c61c6f45d288d0a4f19
plan_head_after: a81726627ac417fc8c94f6f031063f1b426a273f
commits: 6

tech-stack:
  added: []
  patterns:
    - "Hold a directory handle without delete sharing and write only by the handle's final path"
    - "Hooks record outcomes and never assert, because Run turns every exception into exit 1"

key-files:
  created:
    - src/Vdiff.Core/Output/HeldDirectory.cs
    - tests/Vdiff.Tests/Safety/HeldDirectoryTests.cs
  modified:
    - src/Vdiff.Core/Cli/CliApp.cs
    - src/Vdiff.Core/Cli/FinalPath.cs
    - src/Vdiff.Core/Output/OutputWriter.cs
    - tests/Vdiff.Tests/Safety/AliasedOutTests.cs
    - .planning/phases/01-offline-tracer/01-VALIDATION.md

key-decisions:
  - "H-1 to H-8 applied as planned: handle held without delete sharing, every file operation by the handle's final path, anchor judged before anything is created, UNC final paths accepted unjudged, non-Windows narrowed by statement"
  - "CliHooks is a top-level internal record in namespace Vdiff.Core.Cli, not nested in CliApp, so tests name it as CliHooks"
  - "The test helper OsRefused treats DirectoryNotFoundException and FileNotFoundException as not refused, so an attempt that never touched the target cannot pass as a refusal"

requirements-completed: [TRC-01]

duration: 30min
completed: 2026-11-18
status: complete
---

# Phase 1 Plan 16: Held output directory Summary

**On Windows the CLI holds the --out directory without delete sharing, reads its final path from the handle, judges that path against the protected roots, and writes all five files by it, so a swap after the last check can no longer redirect a write into the analysed repository.**

## Performance

- **Duration:** about 30 min (the full suite took 10 min 21 s of it)
- **Started:** 2026-11-18T17:05:31Z
- **Completed:** 2026-11-18T17:35Z
- **Tasks:** 3
- **Files modified:** 7 (2 created)

## Accomplishments

- `HeldDirectory.Create` opens the deepest existing ancestor (the anchor) and judges its final path before anything is created, creates the missing directories under the anchor's final path, opens and judges the leaf, then releases the anchor. A refusal after creation removes the directories this call created, bottom-up and non-recursively.
- `OutputWriter.WriteAll` is internal, takes the `HeldDirectory` and creates no directory. Every temp file and move uses `HeldDirectory.Path`. The success line still names the typed path.
- `CliApp.FindRefusal` is the one containment function used by the pre-write check and by the held-directory judgement. `CliHooks` gives tests a seam inside the run.
- The final-path forms are pinned: drive-letter judged, UNC accepted unjudged, every other shape refused with the fixed unsupported-root text (eight-row table).
- One identical check-to-write paragraph sits at `CliApp.RefuseUnsafeOut`, `HeldDirectory` and `OutputWriter.WriteAll` (the three md5 sums of the paragraph are equal); the T-01-39 bound in `CliApp` and `FinalPath` gained one pointer sentence.

## Task Commits

1. **Task 1 (tracer, red then green)**
   - `3eb3aac` test: add failing tests for a swap of --out after the last check (with the CliHooks seam, no behavior change)
   - `37dd02c` feat: write the outputs through a held output directory
2. **Task 2**
   - `7b6c02b` test: pin the acquisition order and the final-path forms
   - `35d1451` feat: acquire the held directory under a judged anchor
3. **Task 3**
   - `706511b` docs: one swap-interval paragraph at the three CLI sites and the T-01-39 pointer
   - `a817266` docs: record the held-directory tests in the validation map

**Plan metadata:** the SUMMARY commit and the closing bookkeeping commit follow this file; they are not counted in `commits: 6`, which is measured from `plan_head_before` to `plan_head_after`.

## Red evidence (task 1)

Both tests were written with the seam only and run against the unchanged write path (by name).

| Test | Failing assertion | Observed |
|------|-------------------|----------|
| `A_held_directory_cannot_be_renamed_or_deleted_by_another_process_during_the_write` | "the operating system did not refuse to: ..." | all four attempts were allowed: rename the output directory, rename its parent, rename its grandparent, delete the output directory (in the seam state the output directory does not exist yet at the hook point, so the first and last attempts failed with a missing-directory error, which the helper counts as not refused; the two ancestor renames succeeded and were undone) |
| `A_link_entry_retargeted_while_the_held_directory_is_open_does_not_redirect_the_write` | `File.Exists(real/include/base.model.json)` (message `base.model.json`) | exit was 0; the write followed the re-created link entry and landed in the repository's `include` directory |

Two earlier red runs failed for the wrong reason and were fixed first: the helper filter let a `DirectoryNotFoundException` escape the hook (run exit 1, "could not write the output"), and the exit assertion sat after the outcome assertions.

## Mutation checks (task 2, nothing committed, every mutant restored with `git checkout --`)

| Mutant | Red | Stayed green | Observed |
|--------|-----|--------------|----------|
| (a) keep the hold, write by the typed path (`Create` called with the typed path and returning it as `Path`) | `A_link_entry_retargeted_while_the_held_directory_is_open_does_not_redirect_the_write` | the rename test, the swap test | `real/include/base.model.json` missing: the files went through the re-created link entry |
| (b) skip every `findRefusal` call | `A_swap_before_the_held_directory_is_opened_is_refused_by_its_final_path`, `A_protected_existing_ancestor_is_refused_before_any_directory_is_created`, `A_refusal_after_the_directories_exist_removes_the_ones_it_created` | the rename and retarget tests | the swap test saw empty stderr (exit 0): files written into the repository through the link |
| (c) release the handle right after the judgement | `A_held_directory_cannot_be_renamed_or_deleted_by_another_process_during_the_write`, `Dispose_releases_the_directory_so_it_can_be_renamed_again` | the retarget and swap tests | "did not refuse to: rename the output directory; rename its parent; rename its grandparent; delete the output directory" |

The red run of the swap test against the task 1 implementation (before the acquisition order existed) failed because a directory named `aliased-out` had been created inside the repository through the link before the leaf refusal; the acquisition order closes that.

## Test counts

- `held_directory` filter alone: 2 passed after task 1 (red 2 failed before).
- `held_directory|HeldDirectoryTests` filter: 15 passed (3 AliasedOutTests, 4 HeldDirectoryTests facts, 8 table rows).
- `AliasedOutTests|FinalPathTests`: 62 after task 1, 63 after task 2 (as projected).
- Regression filters: `TracerEndToEndTests|DeterminismTests|RepoUnchangedMatrixTests|HostileInputTests` 63 passed; `SidecarBoundsTests|IncludeGlobEndToEndTests|RichFixtureEndToEndTests` 56 passed.
- Full suite `dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --nologo`: 539 passed, 0 failed (projection 539). The cargo count was not re-run (the Rust sidecar is untouched).

## Decisions Made

H-1 to H-8 were followed. Two small implementation choices:

- `CliHooks` is a top-level internal record (the plan said "an internal sealed record"; nesting it in the static class would have forced `CliApp.CliHooks` at every use).
- `OsRefused` (tests) counts only a sharing violation or access denied as a refusal; a missing-directory error is not one.

## Deviations from Plan

None - plan executed exactly as written. One test-hygiene note: during the red runs of task 2, tests whose `Create` returned a live handle (the implementation had not yet thrown) left a `vdiff-links-*` directory in the temp area with one open handle at the time; I removed those two directories (plain empty directories, no links, non-recursive `rmdir`) and the count of `vdiff-links-*` entries was 0 after every green run. With the implementation in place `Create` throws and disposes, so a green run leaves none.

## Incidental

None observed. No test in this plan exercises subst, mapped drive or DOS 8.3 spellings, and no run showed the held-directory judgement changing a verdict for one. AR-04 is not edited.

## Flagged to the orchestrator (not edited here)

- The T-01-43 row and its residual sentence in `01-SECURITY.md` are plan 01-17's to edit.
- Threat ids T-01-51 to T-01-55 (this plan's register) need rows at the next secure-phase run.
- `01-UAT.md` test 1 stays open until plan 01-17 lands.
- `01-VERIFICATION.md` and `REQUIREMENTS.md` bookkeeping is the orchestrator's.
- The script's `-Out` guard against the vdiff repository root and its log write are plan 01-17 (H-7; the extension point is the `findRefusal` function passed to `HeldDirectory.Create`).

T-01-39 still open after plan 01-16: subst and mapped drive letters, DOS 8.3 short names, and review finding IN-04.

Swap interval after plan 01-16: closed on Windows for the CLI's output directory (held, final path judged, files written by the final path); open for a process able to rename a directory on Linux and macOS; the vdiff repository root and the script log write are plan 01-17.

## Known Stubs

None.

## Threat Flags

None beyond the plan's threat model: the two `kernel32` declarations (`CreateFileW`, `GetFinalPathNameByHandleW`) are the planned surface (T-01-SC). No package or manifest was touched.

## Self-Check: PASSED

- Files exist: `src/Vdiff.Core/Output/HeldDirectory.cs`, `tests/Vdiff.Tests/Safety/HeldDirectoryTests.cs`, this SUMMARY.
- Commits present on the branch: 3eb3aac, 37dd02c, 7b6c02b, 35d1451, 706511b, a817266.
- Acceptance greps: `Directory.CreateDirectory` in OutputWriter outside comments 0; `OutputWriter.WriteAll(outPath` in CliApp 0; `DllImport` in HeldDirectory 2; the open-reparse-point constant outside comments 0; `CliHooks` in CliApp 4; `vdiff-links-*` in the temp area 0; `Check-to-write interval (T-01-43, T-01-46)` 3 files x 1; `separate bound (T-01-43, T-01-46)` 2; `is not closed` 0; `T-01-40` 0; T-01-39, T-01-41 and IN-04 cited in both CliApp and FinalPath.
- Not-edited gate: `01-SECURITY.md`, `01-UAT.md`, `01-VERIFICATION.md`, `01-REVIEW-DISPOSITION.md` show no diff against `plan_head_before`.
- The private target-name guard reported clean before every commit.
