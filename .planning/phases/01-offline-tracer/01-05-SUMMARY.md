---
phase: 01-offline-tracer
plan: 05
subsystem: safety-tests
tags: [dotnet, xunit, fake-sidecar, bounded-process, timeout, parse-budget, adversarial-tests]

requires:
  - phase: 01-offline-tracer
    provides: "plan 01-02 BoundedProcess and CppSidecar client, SyntheticRepo and RepoSnapshot; plan 01-03 CliApp.Run, SidecarFixture and the sidecar collection"
provides:
  - "tests/Vdiff.FakeSidecar: a console project whose misbehaviour is chosen by FAKE_SIDECAR_MODE (hang, flood-stdout, flood-stderr, exit3, garbage, truncated, wrong-schema, ok-empty, ok-timeout-file, ok-error-file, unknown-status) and which records its pid"
  - "SidecarBoundsTests: 43 cases proving the T-01-05 bounds, the child kill (T-01-29), CLI-level failure mapping and the per-file deadline end to end (T-01-32)"
  - "SidecarLimits.FromEnvironment: VDIFF_SIDECAR_TIMEOUT_SECONDS (1 to 1200) and VDIFF_PARSE_BUDGET_MS (1 to 600000) overrides; SidecarLimits.Default now reads the process environment"
affects: [01-06, 01-07, 01-08, 01-09, 01-10]

plan_head_before: 0271a3e4f75a3b6a86a519f6c7417137e4ba3c4b
plan_head_after: 6a8c063aa5d50b99985dcaaf6f9d376fb5a26e8c

actuals:
  tokens: 10500
  tasks: 2
  commits: 3

tech-stack:
  added: []
  patterns:
    - "A fake child process selected by an environment variable, so VDIFF_CPP_TS (which can only name an executable) can point at it; the test project references it with a ProjectReference and gets the apphost in its output"
    - "Process-wide environment changes go through one WithEnvironment helper that restores every variable in a finally block; the class is in the sidecar collection"
    - "RED commit compiles against a stub (FromEnvironment returning the built-in limits) so the failing tests fail on their assertions"

key-files:
  created:
    - tests/Vdiff.FakeSidecar/Vdiff.FakeSidecar.csproj
    - tests/Vdiff.FakeSidecar/Program.cs
    - tests/Vdiff.Tests/Safety/SidecarBoundsTests.cs
  modified:
    - vdiff.sln
    - tests/Vdiff.Tests/Vdiff.Tests.csproj
    - src/Vdiff.Core/Extraction/CppSidecar.cs

key-decisions:
  - "The overrides live in SidecarLimits.FromEnvironment and Default became a computed property, so CliApp.Run (not in files_modified) picks them up unchanged"
  - "Override values are parsed with NumberStyles.None: only plain digits count, so ' 5', '+5', '2.5' and '-1' are ignored like any other out-of-range text"
  - "The stdout and stderr caps cannot be overridden; only the timeout and the parse budget can"
  - "No mode of the fake sidecar reads stdin, so a hanging or exiting child with a large manifest is part of what is proven"

patterns-established:
  - "Child-killed assertion: the fake writes its pid, the test polls Process.GetProcessById until the process is gone, and fails if the pid file was never written"

requirements-completed: [TRC-01, TRC-02]

coverage:
  - id: D1
    description: "A sidecar that hangs, floods stdout, exits non-zero, or returns garbage, truncated JSON, a wrong schema or an unknown file status is a tool failure (VdiffException at the client, exit 1 at the CLI) that kills the child and writes nothing to --out, with the analysed repository unchanged"
    requirement: TRC-02
    verification:
      - kind: e2e
        ref: "tests/Vdiff.Tests/Safety/SidecarBoundsTests.cs (client-level cases, A_hanging_sidecar_makes_the_cli_exit_1_within_seconds_naming_the_timeout_and_writes_nothing, A_failing_sidecar_makes_the_cli_exit_1_and_writes_nothing)"
        status: pass
    human_judgment: false
  - id: D2
    description: "A stderr flood is drained and its tail bounded to the cap, and a valid response after it is accepted; a child that ignores a 32 MiB manifest and exits neither deadlocks nor surfaces an IOException"
    requirement: TRC-01
    verification:
      - kind: unit
        ref: "tests/Vdiff.Tests/Safety/SidecarBoundsTests.cs#A_stderr_flood_is_drained_and_a_valid_response_after_it_is_accepted, #The_stderr_tail_of_a_flood_is_bounded_to_its_cap_and_keeps_the_end, #A_child_that_ignores_a_large_manifest_and_exits_at_once_neither_deadlocks_nor_throws_an_IOException"
        status: pass
    human_judgment: false
  - id: D3
    description: "VDIFF_SIDECAR_TIMEOUT_SECONDS and VDIFF_PARSE_BUDGET_MS are validated (out-of-range, non-numeric and empty values ignored, bounds accepted), read by the CLI, and named in the locator error text"
    requirement: TRC-01
    verification:
      - kind: unit
        ref: "tests/Vdiff.Tests/Safety/SidecarBoundsTests.cs (override theories, The_default_limits_read_the_process_environment, The_not_found_error_names_both_operational_settings)"
        status: pass
    human_judgment: false
  - id: D4
    description: "A file the real sidecar labels timeout (a 3 MiB generated header with VDIFF_PARSE_BUDGET_MS=50) is listed unmodelled with reason parse-timeout and counted in summary.unmodelled.head, the run exits 0, and with the default budget the same file is modelled"
    requirement: TRC-02
    verification:
      - kind: e2e
        ref: "tests/Vdiff.Tests/Safety/SidecarBoundsTests.cs#A_file_the_real_sidecar_labels_timeout_is_unmodelled_with_the_run_still_succeeding and #With_the_default_parse_budget_the_same_large_file_is_modelled"
        status: pass
    human_judgment: false

duration: 11min
completed: 2026-11-17
status: complete
---

# Phase 1 Plan 05: Sidecar bounds Summary

**A fake sidecar that hangs, floods, fails and lies is rejected at the client and the CLI with nothing written and the child killed, and two validated environment overrides (timeout 1-1200 s, parse budget 1-600000 ms) make the timeout and the per-file deadline provable, including a real-sidecar run where a 3 MiB header becomes `parse-timeout` and the run still exits 0.**

## Performance

- **Duration:** about 11 min (start 2026-11-17T05:36Z, end 05:48Z)
- **Tasks:** 2, **Commits:** 3 (one test commit, one RED commit, one GREEN commit), measured from `plan_head_before` to `plan_head_after`
- **Files:** 3 created, 3 modified (one source file)
- **Tests:** 252 passed in the .NET assembly (209 before this plan, 43 added), 65 passed in cargo

## Accomplishments

- **Task 1:** `tests/Vdiff.FakeSidecar` (net10.0 console, in `vdiff.sln`, referenced by the test project; the apphost lands in the test output) and the client-level half of `SidecarBoundsTests`: 15 cases. The hang case fails in about 3 s naming `time limit of 3` and the child pid is confirmed gone; the stdout flood names `more than 1048576 bytes to stdout` and the child is gone; the stderr flood (8 MiB then a valid answer) succeeds and a direct `BoundedProcess` run shows the tail at most 64 KiB and still ending with the last marker; `exit3` carries the stderr text and never the file content; `garbage`, `truncated`, `wrong-schema` and `unknown-status` fail; a 32 MiB manifest sent to a child that exits at once completes without an `IOException` or deadlock, and the same with a failing child is decided by the exit code; `ok-timeout-file` and `ok-error-file` hand their status to the caller. No production defect was found: every bound already held.
- **Task 2:** 28 more cases. The overrides (`SidecarLimits.FromEnvironment`, `Default` reads the process environment, locator text names both), the CLI hang case (exit 1 in under 10 s, `vdiff:` message naming the 2 s limit, `--out` absent, child gone, repository snapshot equal), exit3, garbage, truncated, wrong-schema and flood at the CLI (exit 1, nothing written, snapshot equal), and the deadline end to end with the real sidecar.
- Production change: 28 lines added and 3 changed in `CppSidecar.cs` (the fix budget is 80), and nothing in `BoundedProcess.cs`.

## Task Commits

1. **Task 1:** `721cbee` (test) - fake sidecar, solution and project references, 15 client-level cases; all passed on the first valid run.
2. **Task 2 RED:** `117dcbc` (test) - override, CLI and deadline tests against a stub `FromEnvironment`; 8 failures on planned assertions (overrides not read, hang test names 120 s instead of 2, locator text lacks the setting names, the 50 ms budget leaves the large file modelled).
3. **Task 2 GREEN:** `6a8c063` (feat) - `FromEnvironment`, `Default` from the environment, locator text; 43 of 43 pass.

## TDD Notes

- RED `117dcbc`: the `SidecarLimits.FromEnvironment` stub returned the built-in limits, so the tests compiled and the target tests ran and failed on their assertions. Observed: `Expected 00:00:07, Actual 00:02:00` (default limits ignore the environment), `Expected 00:00:00.001, Actual 00:00:10` (budget override), `Sub-string not found` for `time limit of 2` in the hang run, and `Assert.Single() Failure: The collection did not contain any matching items` for the large file at a 50 ms budget. Semantic assessment: valid RED, not a compile, fixture or discovery failure. The "ignored" theory rows and `With_the_default_parse_budget_the_same_large_file_is_modelled` passed already, as expected, since they describe the default.
- `gsd_run check tdd-red-evidence` was not run: the plan is `type: execute` and the project does not enable `workflow.tdd_mode`.
- Task 1 has no RED commit: it proves behaviour that already existed (see 01-04's note on attack tests). The failure direction is covered by the pid-file check (the test fails if the fake never started) and by the exact message assertions.

## Deviations from Plan

None - plan executed exactly as written. `Default` changed from a stored property to a computed one (same type, same name), which is the mechanism that lets the CLI pick up the overrides without touching `CliApp.cs`.

## Authentication Gates

None.

## Issues Encountered

- The first Task 1 test run failed 15 of 15 because the `ProjectReference` to the fake sidecar was missing from `Vdiff.Tests.csproj` (a `sed` edit that matched nothing); added with the edit tool and the apphost then appeared in the test output as the plan predicted.
- A stray empty `python -` heredoc in one shell command hung until the tool moved it to the background; it created nothing in the repository.

## Known Stubs

None. The RED stub was replaced in `6a8c063`.

## Known Limitations (for later plans)

- The memory-bound claim for the stdout flood rests on `BoundedProcess` stopping at the cap (the test checks the message and the kill, not heap size).
- `AssertChildIsGone` identifies the child by pid; pid reuse inside the 10 s polling window would be a false negative, which is not realistic here.
- The deadline test depends on a 3 MiB header taking well over 50 ms to parse; the margin reasoning is in the test comment. The small header is tolerated as `parse-timeout` on a heavily loaded machine.
- The full .NET suite now takes about 2 min 40 s.

## Threat Flags

None. T-01-05, T-01-29 and T-01-32 are now demonstrated by tests; T-01-SC accepted as planned (no package installs).

## Next Phase Readiness

Ready for plans 01-06 to 01-08 (disjoint files). `SidecarLimits.Default` now honours the two environment variables, which plan 01-10 can name in operational documentation.

## Self-Check: PASSED

- Created files exist: `tests/Vdiff.FakeSidecar/Vdiff.FakeSidecar.csproj`, `tests/Vdiff.FakeSidecar/Program.cs`, `tests/Vdiff.Tests/Safety/SidecarBoundsTests.cs`; `git ls-files tests/Vdiff.FakeSidecar` lists only those two files (no `bin/` or `obj/`).
- Commits `721cbee`, `117dcbc` and `6a8c063` are reachable from HEAD; `git rev-list --count` from `plan_head_before` gives 3.
- `dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --nologo`: 252 passed, 0 failed. `cargo test --locked --manifest-path extractors/cpp-ts/Cargo.toml`: 65 passed. `SidecarBoundsTests` filter: 43 passed (task 1 needs at least 10, task 2 at least 16).
- `check-public-diff.sh` reported no target names before every commit.
