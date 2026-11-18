---
phase: 01-offline-tracer
plan: 17
subsystem: safety
tags: [windows, powershell, createfilew, getfinalpathnamebyhandle, add-type, log-guard, protected-dirs, tdd, gap-closure]
requires:
  - phase: 01-offline-tracer
    provides: "HeldDirectory.Create and the findRefusal extension point, CliHooks, LinkScratch, FinalPath.Resolve (plans 01-12 to 01-16)"
provides:
  - "scripts/trace-run.ps1 holds the -LogPath directory without delete sharing through the vdiff run and writes the log by temporary name plus move through the handle's final path"
  - "VDIFF_PROTECTED_DIRS: the CLI refuses an --out inside any listed directory at both checks and in the held directory; the script passes the vdiff root in it"
  - "Test infrastructure: fake sidecar mode ok-empty-after-delay, EnvironmentScope, staged script runs that act while vdiff is busy"
  - "01-SECURITY.md rows T-01-43 and T-01-46 and the residual sentence describe the held directory and the Linux and macOS bound"
affects: [01-VERIFICATION, 01-UAT test 1 and test 3, 01-SECURITY next secure-phase run]
gap_closure: true
gap_ids: [G-01-1]

actuals:
  tokens: 12600
  tasks: 3
  commits: 8
plan_head_before: 5a1a4e227c9963b8d482cfa33c2d460f92b27b20
plan_head_after: f4f1a05dc2a7012f9a1daefd2a9bcaa57f5cef22
commits: 8

tech-stack:
  added: []
  patterns:
    - "Compile two kernel32 declarations lazily with Add-Type from a fixed single-quoted text, Windows only, only when -LogPath is given"
    - "Hold a directory handle without delete sharing and write only by the handle's final path (same rule in C# and in the script)"
    - "Stage a swap from outside the process while a fake sidecar sleeps, wait for its pid file, record outcomes in locals, await the run in the same scope"
    - "A list of protected directories travels in the environment (VDIFF_PROTECTED_DIRS) and can only add protection"

key-files:
  created:
    - tests/Vdiff.Tests/Support/EnvironmentScope.cs
  modified:
    - scripts/trace-run.ps1
    - src/Vdiff.Core/Cli/CliApp.cs
    - tests/Vdiff.FakeSidecar/Program.cs
    - tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs
    - tests/Vdiff.Tests/Safety/AliasedOutTests.cs
    - .planning/phases/01-offline-tracer/01-SECURITY.md
    - .planning/phases/01-offline-tracer/01-VALIDATION.md

key-decisions:
  - "L-1 applied: the script compiles code (Add-Type, two declarations) for the swap window only; AR-04's technical rationale no longer holds for the script and the row now rests on the product owner's sign-off (flagged, not edited)"
  - "L-2 applied: a hold that cannot be taken (type cannot be compiled, directory cannot be created) skips the log silently; only a protected verdict changes the outcome (bad-arguments, before vdiff starts)"
  - "L-3 applied: the log directory is held from before the snapshot until the log is written; the by-name containment re-check stays only where no handle is held"
  - "L-4 applied: the vdiff root reaches the CLI in VDIFF_PROTECTED_DIRS; no argument added; CliOptions and the usage text untouched"
  - "CliHooks gained a GetEnvironmentVariable member so tests supply the variable without changing the process environment"
  - "The staged script tests are async Tasks (xUnit1031) and the attacker's attempts are recorded in locals; the log-hold staged test aggregates its four facts into one assertion so every failing fact shows in one red run"

requirements-completed: [TRC-01, TRC-03]

coverage:
  - id: D1
    description: "The script opens the -LogPath directory without delete sharing before vdiff starts and holds it through the run: rename of it and of its parent is refused on both hosts"
    requirement: TRC-03
    verification:
      - kind: integration
        ref: "tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs#The_log_hold_blocks_a_rename_and_a_retargeted_link_entry_receives_no_log"
        status: pass
    human_judgment: false
  - id: D2
    description: "A link entry in the typed -LogPath that is deleted and re-created pointing at the analysed repository during the run receives nothing; the log lands at the requested name by temporary name plus move in the held directory, no *.tmp-* remains"
    requirement: TRC-03
    verification:
      - kind: integration
        ref: "tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs#The_log_hold_blocks_a_rename_and_a_retargeted_link_entry_receives_no_log"
        status: pass
    human_judgment: false
  - id: D3
    description: "A missing log directory chain is created under the judged anchor and the log is written below it; the hold order is pinned in the source"
    requirement: TRC-03
    verification:
      - kind: integration
        ref: "tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs#A_log_path_below_missing_directories_is_created_under_the_log_hold_and_leaves_no_temp_file"
        status: pass
      - kind: unit
        ref: "tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs#The_log_is_written_through_the_log_hold_in_source_order"
        status: pass
    human_judgment: false
  - id: D4
    description: "The CLI refuses an --out below, equal to or through a link to a VDIFF_PROTECTED_DIRS entry at both checks and in the held directory, refuses a malformed value with a fixed message that names no value, and still writes an --out outside every entry"
    requirement: TRC-01
    verification:
      - kind: integration
        ref: "tests/Vdiff.Tests/Safety/AliasedOutTests.cs#protected_directories (8 cases)"
        status: pass
    human_judgment: false
  - id: D5
    description: "A -Out retargeted into the vdiff root during a real script run receives nothing; the root travels in the environment and never in an argument"
    requirement: TRC-03
    verification:
      - kind: integration
        ref: "tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs#An_out_retargeted_into_the_vdiff_root_during_the_run_receives_nothing_protected_directories"
        status: pass
      - kind: unit
        ref: "tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs#The_vdiff_root_travels_to_vdiff_in_the_protected_directories_variable_and_never_in_an_argument"
        status: pass
    human_judgment: false
  - id: D6
    description: "The script, 01-SECURITY.md T-01-43 and T-01-46 and the validation map state the same guarantee and the same Linux and macOS bound; whether the wording is adequate is a reading task"
    verification: []
    human_judgment: true
    rationale: "Wording is a human judgement; no test can show that four places say the same thing (manual-only row in 01-VALIDATION.md)"
  - id: D7
    description: "On Linux and macOS no handle is held and the interval stays open, stated in the script, the register and the validation map"
    verification: []
    human_judgment: true
    rationale: "No handle is held off Windows, so no test can prove a closure; the bound is a written statement and the dev host is Windows"

duration: 45min
completed: 2026-11-18
status: complete
---

# Phase 1 Plan 17: Held log directory and protected vdiff root Summary

**On Windows, under both PowerShell hosts, trace-run.ps1 now holds the -LogPath directory without delete sharing through the whole vdiff run and writes the log by temporary name plus move through the handle's final path, and the CLI refuses an --out inside the vdiff root it learns from VDIFF_PROTECTED_DIRS, each proven by a swap staged from outside while a sleeping fake sidecar keeps a real run busy.**

## Performance

- **Duration:** about 45 min (the full suite took 12 min 15 s of it; the two long script-test runs about 4.5 to 5 min each)
- **Started:** 2026-11-18T17:40:16Z
- **Completed:** 2026-11-18T18:26Z
- **Tasks:** 3
- **Files modified:** 8 (1 created), 863 insertions and 99 deletions

## Accomplishments

- `Open-HeldDirectory` mirrors `HeldDirectory.Create`: it opens the deepest existing ancestor (the anchor) and judges its final path before anything is created, creates the missing chain under the anchor's final path, opens and judges the leaf, then releases the anchor; a refusal removes what this call created. The two native declarations (`CreateFileW`, `GetFinalPathNameByHandleW`) are compiled with `Add-Type` from a fixed single-quoted text, lazily, only when `-LogPath` is given on Windows.
- `Write-LogFile` takes the held path, the leaf name and the text: a directory or reparse point at the final name means no log, a plain file or hard link is removed by name, the text goes to `<leaf>.tmp-<guid>` (create-new, no sharing) and is moved to the final name by a plain move, all through the final path. Nothing writes by the typed path.
- `Invoke-Trace` holds the log directory from before the repository snapshot until a `finally` closes it. A protected verdict ends the run with `bad-arguments` before vdiff starts; any other hold failure skips the log. The post-run by-name containment re-check stays only where no handle is held.
- The CLI reads `VDIFF_PROTECTED_DIRS` right after the options (at most 8 absolute entries, platform path separator), refuses an `--out` inside any entry at the text check, the resolved check and the held directory's judgement, and refuses a malformed value with one fixed message that names the variable and not the value. The script passes the vdiff root there (`CliOptions` and the usage text are untouched).
- Test infrastructure: fake sidecar mode `ok-empty-after-delay`, `EnvironmentScope`, a staged-run helper that waits for the fake's pid file, and `RunScript` with an optional script path.
- `01-SECURITY.md` T-01-43 and T-01-46 rows and the residual sentence rewritten; the script carries the swap-interval paragraph and the T-01-39 pointer sentence in both blocks; the validation map has the new rows and a sign-off of 555.

## Task Commits

1. **Task 1 (tracer, red then green)**
   - `fc02963` test: add the delayed fake sidecar mode, the environment scope and the staged-run helper (resume boundary; 92 tests in the two filtered classes passed unchanged)
   - `c0dc5e6` test: add failing tests for a log directory swapped during the run
   - `390fdd1` feat: write the log through a held directory
2. **Task 2**
   - `4e58714` test: add failing tests for the vdiff root as a protected directory (with the constants and the `CliHooks` member as the seam, no behavior change)
   - `75bf2c1` feat: protect the vdiff root for --out through VDIFF_PROTECTED_DIRS
3. **Task 3**
   - `4fc1039` docs: one swap-interval paragraph in the script and the T-01-39 pointer
   - `d651778` docs: update the T-01-43 and T-01-46 register rows
   - `f4f1a05` docs: record the log-hold and protected-directory tests in the validation map

**Plan metadata:** the SUMMARY commit and the closing bookkeeping commit follow this file; they are not counted in `commits: 8`, which is measured from `plan_head_before` to `plan_head_after`.

## Red evidence

### Task 1 (run at HEAD of the infrastructure commit, `--filter "FullyQualifiedName~log_hold"`: 3 failed, 2 passed)

| Test | Host | Failing assertion | Observed |
|------|------|-------------------|----------|
| `The_log_hold_blocks_a_rename_and_a_retargeted_link_entry_receives_no_log` | pwsh and powershell | the aggregated hold assertion: "the operating system did not refuse to rename the log directory; the operating system did not refuse to rename the parent of the log directory; the log did not appear at the requested name in the held directory" | the pid file appeared (the run was inside vdiff), the staged attack itself did not fail, the run passed with six lines, then both renames succeeded (and were undone) and, after the link entry was re-created pointing at the repository, the by-name containment re-check saw the retargeted link and skipped the log, so `L/vdiff.log` did not exist. Nothing was written into the repository |
| `The_log_is_written_through_the_log_hold_in_source_order` | n/a | "the log directory must be opened and held before the vdiff run" | no `Open-HeldDirectory` in the script |
| `A_log_path_below_missing_directories_...` (both hosts) | pwsh and powershell | passed at HEAD, as planned | the by-name write created the chain |

The updated `Log_write_is_preceded_by_a_second_containment_check_in_source_order` was not in the red filter (its name carries no `log_hold`); it looks up `Write-LogFile $logHold.Path` and was green after the feat commit (it is part of the 54 `TraceRunScriptTests`).

### Task 2 (`--filter "FullyQualifiedName~protected_directories"`: 10 failed, 1 passed)

Every CLI case except the control failed on `Assert.Equal(1, exit)` with exit 0: the variable was ignored and the five files were written below the protected directory (or, for the malformed values, the run simply proceeded). The control `An_out_outside_every_protected_directories_entry_is_still_written` passed, as planned. The two staged script cases (pwsh, powershell) failed on `Assert.Equal(1, result.ExitCode)` (actual 0): the CLI wrote into the stand-in root's `run` directory and the script reported pass. The text pin failed on "Filter not matched in collection" (no `VDIFF_PROTECTED_DIRS` in the script). The seam (the constants and the `CliHooks` member) was committed with the red tests so the failures are behavioral, not compile errors.

### Mutation check (task 2 step 6, not committed)

With the step 5 change staged, I replaced the script's `@{ VDIFF_PROTECTED_DIRS = $protectedForChild }` argument by `$null` in the working tree and re-ran the two staged script cases: both hosts went red (`Expected: 1, Actual: 0`: the run passed, so vdiff wrote its five files through the retargeted link into the stand-in root's `run` directory). `git checkout -- scripts/trace-run.ps1` restored the staged implementation: working tree equals index (`git diff --quiet` exit 0), the staged change still differs from HEAD (`git diff --cached --quiet` exit 1), and the script had one `VDIFF_PROTECTED_DIRS` line again.

## Test counts

- `log_hold` filter: 5 passed (projection 5): 2 staged, 2 missing-directory, 1 source pin.
- `TraceRunScriptTests`: 54 passed after task 1 (projection 54), 57 after task 2 (projection 57). The baseline was 49, as projected.
- `protected_directories` filter: 11 passed (projection 11): 8 CLI cases, 3 script cases.
- `AliasedOutTests|FinalPathTests|HeldDirectoryTests|CliOptionsTests`: 97 passed after task 2.
- `SidecarBoundsTests|RepoHygieneTests`: 75 passed after task 1; `SidecarBoundsTests|TraceRunScriptTests` before any change: 92 passed.
- Full suite `dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --nologo`: 555 passed, 0 failed (projection 555), 12 min 15 s. The cargo suite was not re-run (the Rust sidecar is untouched).
- `ls $TEMP | grep -c '^vdiff-links-'` was 0 after every run, red runs included.

## Decisions Made

L-1 to L-7 were followed as planned. Implementation choices beyond the plan:

- Locals and names: `$protectedForChild` holds the vdiff root on its own line so no line that builds or passes `$vdiffArgs` names the root (the acceptance grep and the text pin both read lines containing `$vdiffArgs`).
- `Open-HeldDirectory` rethrows the original error record (`throw $failure`), not a bare `throw`, because the cleanup loop contains nested try/catch blocks that would change what a bare `throw` rethrows.
- The staged tests are `async Task` and `await` the script task (xUnit1031 warned on the blocking form); the awaiting happens inside the `try` that owns the environment scope, so a live run never outlives `LinkScratch.Dispose`.
- The red-evidence classifier (`gsd_run check tdd-red-evidence`) was not run: `dotnet test` output is not one of its supported report formats, the plan is `type: execute` and not `type: tdd`, and no logger package may be installed. The red evidence is the recorded assertions above, inspected by hand.

## Deviations from Plan

### Environment and process

**1. [Rule 3 - Blocking] The configured branch is reported as protected by the commit guard**
- **Found during:** step 0 before the first commit
- **Issue:** `git.base-branch --is-protected` returned `true` for `ccr-c12d6a45-a0eke4`, the branch `origin/HEAD` points at (no `git.allow_default_branch_commits` override in `.planning/config.json`). The guard says to re-home, not to commit there.
- **Fix:** created the local branch `gsd/phase-01-plan-17` from `5a1a4e2` and made all eight commits there; `ccr-c12d6a45-a0eke4` is untouched at `5a1a4e2`. HEAD is left on the new branch. Merging it back (a fast-forward) or setting the override is the orchestrator's call.
- **Files modified:** none
- **Verification:** `git rev-parse --abbrev-ref HEAD` is `gsd/phase-01-plan-17`; guard result `false` before each commit

**2. Commit trailer**
- The dispatch note asked for `Co-Authored-By: Claude Opus 5.5`; the session attribution reminder asks for `Claude Sonnet 5.5`. I used the reminder's line (and the shared `Claude-Session` line) on all eight commits; the 01-16 commits carry the Opus line. Flagged so the orchestrator can decide whether to amend.

### Auto-fixed Issues

None - the plan's code and tests worked as written on the first green run (5, 57 and 555 passed as projected).

---

**Total deviations:** 1 blocking process issue handled (branch re-homing), 1 trailer mismatch flagged.
**Impact on plan:** none on behavior or scope.

## Issues Encountered

- The first red run of task 2's seam was written before the `CliHooks` member existed in `CliApp`; the seam commit carries the constants and the member so the red run is behavioral.
- During the planning of the commit split, the full test file had to be set aside and the infrastructure-only part committed first (the resume boundary the plan asks for); the full file was restored byte for byte from a scratch copy.

## Incidental

None observed. No test exercises subst, mapped drive or DOS 8.3 spellings, and no run showed the held-directory judgement changing a verdict for a Windows PowerShell 5.1 short-name `-LogPath`. AR-04 is not edited. L-6 holds: the judgement compares what the operating system reports for the handle with the protected roots as `Resolve-FinalPath` renders them, and for a spelling the text walk does not fold the two can differ; nothing here claims otherwise.

## Flagged to the orchestrator (not edited here)

- **AR-04's technical rationale no longer holds for the swap window (L-1).** The row says the 5.1 script "cannot make a Win32 final-path call without compiling code"; the script now compiles two declarations with `Add-Type` for the swap window and reads final paths from a handle. The row is unedited and rests on the product owner's sign-off (`01-UAT.md` test 6).
- **Branch:** the work is on `gsd/phase-01-plan-17`, not on `ccr-c12d6a45-a0eke4` (see deviation 1).
- Threat ids T-01-51 to T-01-59 (plans 01-16 and 01-17) need rows at the next secure-phase run, and the two rows edited here (T-01-43, T-01-46) need re-auditing.
- The severity of T-01-43 and T-01-46 was left at low while the new T-01-51 and T-01-56 are rated medium; whether the older rows should follow is for the next secure-phase run and the product owner.
- `01-UAT.md` test 1 and test 3 and gap G-01-1 can be re-verified; `01-VERIFICATION.md` and `REQUIREMENTS.md` bookkeeping is the orchestrator's.
- The `Add-Type` compile cost was not measured in the suite; the planning-time probe gave about 220 ms on 5.1 and 440 ms on 7.

T-01-39 still open after plan 01-17: subst and mapped drive letters, DOS 8.3 short names, and review finding IN-04.

Swap interval after plan 01-17: closed on Windows for the CLI's output directory and for the script's log directory (held, final path judged, files written by the final path), and for -Out against the vdiff root through VDIFF_PROTECTED_DIRS; open for a process able to rename a directory on Linux and macOS.

## Known Stubs

None.

## Threat Flags

None beyond the plan's threat model (T-01-56 to T-01-59, T-01-SC): the two `kernel32` declarations compiled by `Add-Type` are the planned surface (T-01-57), and `VDIFF_PROTECTED_DIRS` is the planned environment input (T-01-58). No package, manifest or lockfile was touched.

## Self-Check: PASSED

- Files exist: `tests/Vdiff.Tests/Support/EnvironmentScope.cs`; the modified files are in the commits below.
- Commits present on the branch: fc02963, c0dc5e6, 390fdd1, 4e58714, 75bf2c1, 4fc1039, d651778, f4f1a05 (`git rev-list --count` from `plan_head_before` is 8).
- Acceptance greps: `Write-LogFile $logFull` outside comments 0; `Add-Type` outside comments 1; `ok-empty-after-delay` in the fake sidecar 2; `log_hold` in the script tests 3; non-ASCII in the script 0; the reason vocabulary line 1; `VDIFF_PROTECTED_DIRS` in the script 1 and in `CliApp.cs` 2; `protect` in `CliOptions.cs` 0; `$vdiffArgs` lines naming `VdiffRoot` 0; `Check-to-write interval (T-01-43, T-01-46)` in the script 1; `separate bound (T-01-43, T-01-46)` 2; `millisecond` and `is not closed` in the register 0; `T-01-43` and `T-01-46` rows 2; `vdiff-links-*` in the temp area 0; the infrastructure commit subject appears once.
- The AR-04 and not-edited gates are run from `plan_head_before` after this file is written (see the final report).
- The private target-name guard reported clean before every commit.
