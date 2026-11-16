---
phase: 01-offline-tracer
plan: 02
subsystem: core-read-path
tags: [dotnet, git-plumbing, process-isolation, xunit, tree-sitter-client]

requires:
  - phase: none
    provides: "wave 1; disjoint from plan 01-01 (the Rust sidecar), whose protocol this client mirrors"
provides:
  - "vdiff.sln, global.json, Vdiff.Core and Vdiff.Tests projects (net10.0)"
  - "BoundedProcess (timeout, stdout/stderr caps, concurrent drain, tree kill, tolerant stdin writer) and GitCommand (shared read-only git prefix and hardened environment)"
  - "GitObjectReader: git version gate, ResolveCommit with shadowing check, ListChanged (diff-tree raw), ReadBlobs (cat-file batch with per-blob and total budgets), Locate/ToplevelOrGitDir"
  - "ScopeFilter (C++ extensions, ordinal case-sensitive globs)"
  - "CppSidecar client: Locate, BuildRequest, ParseResponse, Extract"
  - "Test support: SyntheticRepo (isolated deterministic sandbox) and RepoSnapshot (whole-sandbox state)"
affects: [01-03, 01-04, 01-05, 01-06, 01-07, 01-08, 01-09, 01-10]

actuals:
  tokens: 25000
  tasks: 3
  commits: 7

tech-stack:
  added: [Microsoft.Extensions.FileSystemGlobbing 10.0.15, xunit 2.9.3, xunit.runner.visualstudio 3.1.4, Microsoft.NET.Test.Sdk 17.14.1]
  patterns:
    - "Every git call is an argument array: git -c core.fsmonitor=false --no-optional-locks -C <repo> <plumbing verb>, environment minus the GIT_DIR family plus GIT_NO_LAZY_FETCH=1"
    - "Pure static argument builders and parsers (ParseDiffTree, GitVersion.Parse, *Arguments) tested without a process"
    - "Stable OrderBy with StringComparer.Ordinal for all output ordering; InvariantGlobalization on"
    - "Stand-in child processes for client tests: git with a shell alias, no sidecar binary needed"

key-files:
  created:
    - global.json
    - vdiff.sln
    - src/Vdiff.Core/Vdiff.Core.csproj
    - src/Vdiff.Core/VdiffException.cs
    - src/Vdiff.Core/Process/BoundedProcess.cs
    - src/Vdiff.Core/Git/GitObjectReader.cs
    - src/Vdiff.Core/Scope/ScopeFilter.cs
    - src/Vdiff.Core/Extraction/CppSidecar.cs
    - tests/Vdiff.Tests/Vdiff.Tests.csproj
    - tests/Vdiff.Tests/Support/SyntheticRepo.cs
    - tests/Vdiff.Tests/Support/RepoSnapshot.cs
    - tests/Vdiff.Tests/Support/SupportTests.cs
    - tests/Vdiff.Tests/GitObjectReaderTests.cs
    - tests/Vdiff.Tests/CppSidecarProtocolTests.cs
  modified: []

key-decisions:
  - "ToplevelOrGitDir is built on Locate(): one rev-parse call with --is-bare-repository, --absolute-git-dir and --git-common-dir, then --show-toplevel only for a non-bare repository (it fails in a bare one)"
  - "CppSidecar.Locate throws when VDIFF_CPP_TS is set but names a missing file, rather than silently falling through to another binary"
  - "BuildRequest refuses a parse budget outside 1 to 600000 ms, the range the sidecar enforces"
  - "SyntheticRepo stages on Write/Delete and Commit only commits the index, because git add -A would stage the removal of plumbing-made (cacheinfo) entries that have no work-tree file"

patterns-established:
  - "GitCommand.Prefix and GitCommand.BuildEnvironment are the single definition of read-only git; RepoSnapshot uses them too"
  - "Reasons for unmodelled files are fixed strings: non-utf8-path, too-large, object-missing, total-too-large, not-a-blob"

requirements-completed: [TRC-01, TRC-02]

coverage:
  - id: D1
    description: "Bounded process runner and shared read-only git prefix and environment"
    requirement: TRC-01
    verification:
      - kind: unit
        ref: "tests/Vdiff.Tests/Support/SupportTests.cs (Run_*, GitCommand_*)"
        status: pass
    human_judgment: false
  - id: D2
    description: "Synthetic repository sandbox and whole-sandbox RepoSnapshot (detached HEAD and bare repository do not throw)"
    requirement: TRC-01
    verification:
      - kind: unit
        ref: "tests/Vdiff.Tests/Support/SupportTests.cs (Snapshot_*, Two_sandboxes_*, Git_configuration_*, Plumbing_*, Dispose_*)"
        status: pass
    human_judgment: false
  - id: D3
    description: "Read-only git object reader: commit id validation and shadowing, version gate, diff-tree and cat-file reading with limits"
    requirement: TRC-01
    verification:
      - kind: unit
        ref: "tests/Vdiff.Tests/GitObjectReaderTests.cs"
        status: pass
    human_judgment: false
  - id: D4
    description: "Scope filter per D-10 with ordinal globs"
    requirement: TRC-02
    verification:
      - kind: unit
        ref: "tests/Vdiff.Tests/GitObjectReaderTests.cs (ScopeFilter_*)"
        status: pass
    human_judgment: false
  - id: D5
    description: "Sidecar client: manifest, response checks with per-file status, locator, bounded Extract"
    requirement: TRC-02
    verification:
      - kind: unit
        ref: "tests/Vdiff.Tests/CppSidecarProtocolTests.cs"
        status: pass
    human_judgment: false
  - id: D6
    description: "Byte-compatibility of the client with the real Rust sidecar (plan 01-01)"
    verification: []
    human_judgment: true
    rationale: "Tests use canned bytes and stand-in processes; the real binary is built in parallel and first exercised end to end by plan 01-03"

duration: "approx. 25 min (start time not recorded; estimated from commit timestamps)"
completed: 2026-11-16
status: complete
plan_head_before: 58e8463f3a35fd8d1cf6d261b2409566ba4dd454
plan_head_after: 0a38e227239c00861bf35ae10b56c366efb215b6
---

# Phase 1 Plan 02: .NET core read path Summary

**Solution scaffold plus a read-only git object reader, a bounded process runner, a scope filter, the sidecar client and isolated synthetic-repository test support, with 104 passing tests.**

## Performance

- **Duration:** approx. 25 min (start not recorded)
- **Completed:** 2026-11-16
- **Tasks:** 3 (7 commits: three RED, three GREEN, one fix)
- **Files created:** 14 (source, tests, solution and project files)

## Accomplishments

- Every git call goes through one definition (`GitCommand`): argument array, `-c core.fsmonitor=false`, `--no-optional-locks`, GIT_DIR family removed, `GIT_NO_LAZY_FETCH=1`; git older than 2.45 is refused.
- `GitObjectReader` reads changes (`diff-tree -r -z --raw --no-renames --no-abbrev`) and blobs (`cat-file --batch-check` then `--batch`) from objects only, judging each side by its own mode, dropping mode-only changes, gitlinks and symlinks, and reporting oversized (4 MiB), missing, non-UTF-8-path and over-budget (256 MiB total) files as unmodelled with a reason.
- `ResolveCommit` accepts only 7 to 64 hex characters and refuses an abbreviation shadowed by a branch name.
- `CppSidecar` builds the stdin manifest byte-exactly (including `parse_budget_ms`), parses and schema-checks the response with per-file status, and fails closed.
- `SyntheticRepo` and `RepoSnapshot` give every later plan isolated deterministic sandboxes and a before/after state comparison.

## Task Commits

1. **Task 1: Solution scaffold, bounded process runner, synthetic test support** - RED `f699734` (test), GREEN `636fe1b` (feat)
2. **Task 2: Read-only git object reader and scope filter** - RED `0412619` (test), GREEN `c44276f` (feat)
3. **Task 3: Sidecar client** - RED `2d62406` (test), GREEN `2947c36` (feat), follow-up `0a38e22` (fix)

**Plan metadata:** the commit that adds this SUMMARY (`docs(01-02)`); it is not counted in `commits: 7` above, which is measured from `plan_head_before` to `plan_head_after`.

## Files Created/Modified

- `global.json`, `vdiff.sln`, `src/Vdiff.Core/Vdiff.Core.csproj`, `tests/Vdiff.Tests/Vdiff.Tests.csproj` - scaffold (SDK 10.0.100 latestFeature, `.sln` format, InvariantGlobalization).
- `src/Vdiff.Core/Process/BoundedProcess.cs` - `BoundedProcess`, `ProcessLimits`, `ProcessResult`, `GitCommand`.
- `src/Vdiff.Core/Git/GitObjectReader.cs` - reader, `GitVersion`, result records.
- `src/Vdiff.Core/Scope/ScopeFilter.cs` - extension and glob filter.
- `src/Vdiff.Core/Extraction/CppSidecar.cs` - client and response records.
- `src/Vdiff.Core/VdiffException.cs` - expected-failure exception (message only).
- `tests/Vdiff.Tests/Support/{SyntheticRepo,RepoSnapshot,SupportTests}.cs`, `tests/Vdiff.Tests/{GitObjectReaderTests,CppSidecarProtocolTests}.cs` - 22 + 56 + 26 test cases (104 in the assembly).

## Decisions Made

See `key-decisions` above. In addition: unknown JSON properties from the sidecar are ignored; the `StderrTail` of a result is the true tail (last cap bytes) of stderr.

## TDD Gate Compliance

Each task has a `test(01-02)` commit followed by a `feat(01-02)` commit. The RED evidence is weaker than the format-based classifier requires and is stated plainly here:

- RED was produced by committing the full tests against stub production code that throws `NotImplementedException` (22, 56 and 24 failing cases respectively). The target tests failed because the behaviour was absent, but the failures are `NotImplementedException`, not assertions on planned values, and for Task 1 some failures came from the `SyntheticRepo` constructor reaching a stub rather than from an assertion.
- `gsd_run check tdd-red-evidence` was not run (the project does not enable `workflow.tdd_mode`), so there is no `RED_EVIDENCE_OK` verdict.
- A `refactor(01-02)` commit was not needed.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Config-origin assertion in a support test**
- **Found during:** Task 1 GREEN
- **Issue:** `Git_configuration_comes_only_from_the_sandbox` assumed every `--show-origin` entry is an absolute path under the sandbox; git prints the repository config as `file:.git/config` and flags as `command line:`.
- **Fix:** accept those two origins and still require every other origin to lie inside the sandbox (a global or system config would be an absolute path outside it).
- **Files modified:** `tests/Vdiff.Tests/Support/SupportTests.cs`
- **Committed in:** `636fe1b` (a test edited inside the GREEN commit, not the RED commit)

**2. [Rule 2 - Missing critical] Empty sidecar output lost the stderr tail; unusable parse budget reached the sidecar**
- **Found during:** Task 3 review, after the GREEN commit
- **Issue:** the empty-output path went through `ParseResponse` and dropped the stderr tail the behaviour list requires; a sub-millisecond `ParseBudget` serialised as `parse_budget_ms: 0`, which the sidecar rejects with exit 2.
- **Fix:** `Extract` checks for empty output and carries the tail; `BuildRequest` refuses a budget outside 1 to 600000 ms. Tests extended.
- **Files modified:** `src/Vdiff.Core/Extraction/CppSidecar.cs`, `tests/Vdiff.Tests/CppSidecarProtocolTests.cs`
- **Committed in:** `0a38e22`

### Design choices beyond the plan text (not defects)

- `ToplevelOrGitDir` is two `rev-parse` calls (an `--is-bare-repository` probe first) instead of one, because `--show-toplevel` fails in a bare repository.
- Extra public surface: `GitCommand.Build` and `GitCommand.Run`, `RepoLocation` and `GitObjectReader.Locate()`, the `not-a-blob` reason, a `workingDirectory` parameter on `BoundedProcess.Run`, and `SidecarLimits.Default`.
- `Locate` throws when `VDIFF_CPP_TS` is set but the file is missing (the plan could be read as falling through).
- The cargo release build is located at `<root>/extractors/cpp-ts/target/release/vdiff-cpp-ts[.exe]`; no later plan spells out a different path.

**Total deviations:** 2 auto-fixed (1 Rule 1, 1 Rule 2). **Impact on plan:** none on scope; both fixes are inside this plan's files.

## Issues Encountered

- `bin/` and `obj/` stay untracked in this worktree on purpose: `.gitignore` is owned by plan 01-01, and only the listed files were staged. The orchestrator will see them as untracked until 01-01's `.gitignore` is merged.
- Not covered by a test here: the tree kill of a grandchild is exercised only through the `sh`/`sleep` alias case (it times out and returns within the bound); plan 01-05 proves it with a fake sidecar. The reader's own `git --version` call is not asserted to happen once before other calls; it is the constructor.
- `REQUIREMENTS.md` was not edited: TRC-01 and TRC-02 are only partly delivered by this plan (the end-to-end tracer is 01-03), so marking them complete is left to the orchestrator's shared-ID gate.

## Known Stubs

None. (The RED-phase stubs were replaced in the GREEN commits.)

## Threat Flags

None beyond the plan's threat register. T-01-01, T-01-02, T-01-30, T-01-31, T-01-05, T-01-07 and T-01-08 mitigations are implemented here; hostile-argument and marker-hook scenario tests are plan 01-04's.

## Next Phase Readiness

Ready for plan 01-03: the reader, scope filter, client and test support are usable as named in that plan. The client is untested against the real sidecar until 01-01 and 01-03 meet.

## Self-Check: PASSED

- All 14 files listed under `key-files.created` exist on disk.
- Commits `f699734`, `636fe1b`, `0412619`, `c44276f`, `2d62406`, `2947c36` and `0a38e22` are reachable from HEAD (`git rev-list --count` from the base gives 7).
- `dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --nologo` passes in full (104 passed, 0 failed); `git ls-files` lists no `bin/` or `obj/` path; `grep -c '"rollForward"' global.json` gives 1.
- `check-public-diff.sh` reported no target names before every commit.
