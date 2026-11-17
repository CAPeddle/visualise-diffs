---
phase: 01-offline-tracer
plan: 04
subsystem: safety-tests
tags: [dotnet, xunit, adversarial-tests, read-only-guarantee, fsmonitor, public-repo-guards]

requires:
  - phase: 01-offline-tracer
    provides: "plan 01-02 SyntheticRepo, RepoSnapshot and GitCommand.Prefix; plan 01-03 CliApp.Run, SidecarFixture and the sidecar collection"
provides:
  - "RepoUnchangedMatrixTests: ROADMAP criterion 2 proven in eight scenarios (neither commit checked out with dirty tree, stash and linked worktree; detached head; subdirectory --repo; bare repository; repeated runs; out-only writes; fsmonitor hook; fsmonitor daemon)"
  - "HostileInputTests: 44 cases (hostile --base/--head, shadowing branch, --repo, --out containment, --blank and --include limits, flag misuse, odd paths, binary bytes, oversized and missing blobs)"
  - "RepoHygieneTests: ignore-rule, tracked-file and synthetic-only guards for this public repository, with the scanners themselves tested in both directions"
  - "CliOptions.MaxBlankNameLength (128): a --blank macro name longer than that is refused"
affects: [01-05, 01-06, 01-07, 01-08, 01-09, 01-10]

plan_head_before: cc85a3a37d7b45ed1471cef004ec1dec33936148
plan_head_after: a545b7308f416908be8b4e6d12b89c4127e5aaa8

actuals:
  tokens: 11500
  tasks: 3
  commits: 4

tech-stack:
  added: []
  patterns:
    - "Positive control before a negative assertion: the fsmonitor scenarios first show that an unguarded git call does run the hook and start the daemon, so 'it did not run' means something"
    - "Guards whose pass signal is 'no matches' carry theories that must match and theories that must not"
    - "Relative --out tests use Path.GetRelativePath from the current directory instead of changing the process-wide current directory"

key-files:
  created:
    - tests/Vdiff.Tests/Safety/RepoUnchangedMatrixTests.cs
    - tests/Vdiff.Tests/Safety/HostileInputTests.cs
    - tests/Vdiff.Tests/Safety/RepoHygieneTests.cs
  modified:
    - src/Vdiff.Core/Cli/CliOptions.cs

key-decisions:
  - "A --blank macro name is capped at 128 characters (plan behavior list); the cap is a constant next to the existing 256-name cap"
  - "The synthetic-only scanner flags a remote verb only as the first argument of a git call (Git(\"fetch\"), [\"-C\", dir, \"push\"], \"git fetch\" command lines), because `stash push` is local and must stay allowed"
  - "Ignore-rule guard uses git check-ignore -v and requires the matching rule to come from a .gitignore file, so a personal exclude file cannot make the guard pass"

patterns-established:
  - "Hygiene guard failures name the offending path or line and the threat id"

requirements-completed: [TRC-01]

coverage:
  - id: D1
    description: "The analysed repository is identical before and after a run in every state the roadmap names: neither commit checked out, dirty tree with staged, unstaged, untracked and ignored files, a stash entry, a linked worktree, detached HEAD, subdirectory --repo and a bare repository"
    requirement: TRC-01
    verification:
      - kind: e2e
        ref: "tests/Vdiff.Tests/Safety/RepoUnchangedMatrixTests.cs (eight scenarios)"
        status: pass
    human_judgment: false
  - id: D2
    description: "A repository with core.fsmonitor set to a marker-writing hook, or to true on Windows, is not touched: the hook never runs, no daemon starts, no .git/fsmonitor--daemon entry appears (T-01-30)"
    requirement: TRC-01
    verification:
      - kind: e2e
        ref: "tests/Vdiff.Tests/Safety/RepoUnchangedMatrixTests.cs#A_core_fsmonitor_hook_is_never_run_by_a_vdiff_run_or_by_the_snapshot and #A_core_fsmonitor_true_setting_never_starts_a_daemon_on_windows"
        status: pass
    human_judgment: false
  - id: D3
    description: "A branch named like the head commit's 7-character abbreviation never changes which commit is analysed (T-01-31)"
    requirement: TRC-01
    verification:
      - kind: e2e
        ref: "tests/Vdiff.Tests/Safety/HostileInputTests.cs#A_branch_named_like_the_head_abbreviation_never_changes_which_commit_is_analysed"
        status: pass
    human_judgment: false
  - id: D4
    description: "Hostile --base, --head, --repo, --out, --blank and --include values are refused with exit 1 and a vdiff: message, before any harmful effect, and never reach git as an option"
    requirement: TRC-01
    verification:
      - kind: e2e
        ref: "tests/Vdiff.Tests/Safety/HostileInputTests.cs (argument cases)"
        status: pass
    human_judgment: false
  - id: D5
    description: "Hostile repository content (odd paths, binary bytes, a blob over 4 MiB, a deleted loose object) never crashes a run or writes outside --out, and is reported as unmodelled with reason too-large or object-missing"
    requirement: TRC-01
    verification:
      - kind: e2e
        ref: "tests/Vdiff.Tests/Safety/HostileInputTests.cs (content cases)"
        status: pass
    human_judgment: false
  - id: D6
    description: "Public-repository guards fail loudly: output and build directories are ignored by a committed .gitignore, no generated output or build directory is tracked, and test and script sources cannot reach a remote"
    requirement: TRC-01
    verification:
      - kind: unit
        ref: "tests/Vdiff.Tests/Safety/RepoHygieneTests.cs (three guards plus scanner theories)"
        status: pass
    human_judgment: false

duration: 28min
completed: 2026-11-17
status: complete
---

# Phase 1 Plan 04: Attack the tracer Summary

**The tracer holds under attack: eight repository states prove the read-only guarantee (including a live fsmonitor hook and daemon), 44 hostile-input cases pass after one three-line fix, and three failing-by-design guards keep generated output and remote access out of this public repository.**

## Performance

- **Duration:** about 28 min (end 2026-11-17T05:33Z)
- **Tasks:** 3, **Commits:** 4 (three test commits and one fix), measured from `plan_head_before` to `plan_head_after`
- **Files:** 3 test files created, 1 source file modified
- **Tests:** 209 passed in the .NET assembly (125 before this plan: 8 + 44 + 32 added), 65 passed in cargo

## Accomplishments

- **Task 1 (matrix):** `RepoUnchangedMatrixTests`, eight scenarios, each with a before and after `RepoSnapshot` compared field by field. The fsmonitor scenarios carry positive controls: an unguarded `git status` is shown to run the marker hook, and (on Windows) to start the daemon, in a second repository so the daemon's leftover directory cannot mask the assertions. Every scenario also checks exit 0, empty stderr and exactly the five output files. No production change was needed: T-01-02 and T-01-30 already hold.
- **Task 2 (hostile input):** `HostileInputTests`, 44 cases. Commit ids, `--repo`, `--out` (inside the work tree, at the root, in `.git`, an existing file, relative paths whose `..` resolve inside the repository, also with `--repo` as a subdirectory), `--blank`, `--include`, flag misuse, `--help`, and repository content (spaces, non-ASCII, leading dash, a 200-character name built by plumbing, random binary bytes, a blob over 4 MiB reported `too-large`, a deleted loose object reported `object-missing`). The shadowing case accepts either outcome the plan allows and asserts the branch tip is never analysed.
- **Task 3 (hygiene):** `RepoHygieneTests` runs against this repository. The ignore guard requires the matching rule to come from a `.gitignore`. The scanners (`TrackedPathProblems`, `RemoteAccessProblems`) are public static and covered by theories that must match and theories that must not. Each guard was verified once to fail by temporarily adding a tracked `golden/diff.json`, a test file containing a remote address, and removing the `/vdiff-out*/` ignore rule; all three failed with a named path or line, and everything was reverted.

## Task Commits

1. **Task 1:** `734d763` (test) - repository-unchanged matrix.
2. **Task 2 RED:** `849c3b1` (test) - hostile input tests; the 129-character `--blank` case fails without the cap (observed: expected exit 1, got 0).
3. **Task 2 GREEN:** `e41dbe0` (fix) - `CliOptions.MaxBlankNameLength = 128`, enforced beside the identifier check.
4. **Task 3:** `a545b73` (test) - hygiene guards.

## TDD Notes

- Task 1 and Task 3 are attack and guard tests over behaviour that already existed; they passed on first valid run (after fixing mistakes in the tests themselves, listed under Issues Encountered), so they have no RED commit. Their failure direction was proven another way: positive controls for the fsmonitor scenarios, and the temporary-mutation check for the hygiene guards.
- Task 2 produced a genuine RED: `849c3b1` contains `A_blank_name_of_129_characters_is_refused_and_128_is_accepted`, which failed on the planned assertion (exit 0 instead of 1). `e41dbe0` is the GREEN. No refactor was needed.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing critical functionality] --blank macro names had no length cap**
- **Found during:** Task 2
- **Issue:** `CliOptions` capped the number of `--blank` names (256) but not their length; a 129-character name was accepted although the plan's behavior list requires refusal.
- **Fix:** added `MaxBlankNameLength = 128` and the length check next to the identifier check; the error message names the limit.
- **Files modified:** `src/Vdiff.Core/Cli/CliOptions.cs` (3 lines added, 2 changed; the fix budget is 80)
- **Verification:** the 129-character case failed before and passes after; 128 characters is accepted.
- **Commit:** `e41dbe0`

### Additions beyond the plan text (not defects)

- `RepoHygieneTests` has 32 test cases rather than 3: the three guards plus theories that prove the scanners match what they must and ignore what they must not (the user's rule that a gate whose pass signal is "no matches" is tested in both directions).
- The `--include` count case checks the 64-glob boundary and the 65-glob refusal; the `--blank` count case checks 256 and 257.

**Total deviations:** 1 auto-fixed (Rule 2). **Impact:** none beyond the one validated cap; `files_modified` respected (`CliApp.cs` and `GitObjectReader.cs` needed no change).

## Authentication Gates

None.

## Issues Encountered

- Test-authoring slips, all fixed before the commit that contains them: a wrong sanity assertion (`A ` instead of `M ` for a staged edit), a 200-character name built as 208 characters, a hook-daemon control that left `.git/fsmonitor--daemon` behind (moved to a second repository), and a generated test file where a shell heredoc and Python turned `\n` and `\b` into raw characters (caught by compiler errors and a CR/backspace check; the committed files have 0 CR and 0 backspace characters).
- The first synthetic-only scanner pattern flagged `stash push`; it now flags a remote verb only as the first argument of a git call.
- The Windows daemon scenario returns early on other platforms and so passes without testing there; the marker-hook scenario is the cross-platform one.

## Known Stubs

None.

## Known Limitations (for later plans)

- T-01-10 stays accepted: `--out` that is a symlink or junction into the repository is not resolved; the tests compare paths textually.
- The relative `--out` test derives its relative path from the current directory with `Path.GetRelativePath` rather than changing the process-wide current directory, which would race with test classes outside the `sidecar` collection.
- The tracked-file guard bans the names `diff.json`, `summary.mmd`, `report.html`, `base.model.json` and `head.model.json` and the directories `out`, `bin`, `obj`, `target` and `node_modules` anywhere in the tree; a later phase that adds golden files with those names must change the guard in the same commit.
- The full .NET suite now takes about two minutes because each hostile case snapshots the sandbox twice.

## Threat Flags

None. T-01-01, T-01-02, T-01-28, T-01-30 and T-01-31 are now proven by tests; T-01-13, T-01-11 and T-01-12 are mitigated by the content cases and the hygiene guards; T-01-10 and T-01-SC are accepted as planned.

## Next Phase Readiness

Ready for plans 01-05 to 01-08. The new test classes carry `[Collection("sidecar")]` where they run the CLI and touch no files owned by those plans; `HostileInputTests` relies on, and now enforces, the `--blank` and `--include` caps that plan 01-07 asserts.

## Self-Check: PASSED

- The three created test files exist on disk and `src/Vdiff.Core/Cli/CliOptions.cs` carries the new constant.
- Commits `734d763`, `849c3b1`, `e41dbe0` and `a545b73` are reachable from HEAD; `git rev-list --count` from `plan_head_before` gives 4.
- `dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --nologo`: 209 passed, 0 failed. `cargo test --locked --manifest-path extractors/cpp-ts/Cargo.toml`: 65 passed.
- Per-task filters: `RepoUnchangedMatrixTests` 8 passed (needs at least 7), `HostileInputTests` 44 passed (needs at least 12), `RepoHygieneTests` 32 passed (needs at least 3).
- `git diff --stat` from `plan_head_before` touches only the three planned test files and `CliOptions.cs`; no other file.
- `check-public-diff.sh` reported no target names before every commit.
