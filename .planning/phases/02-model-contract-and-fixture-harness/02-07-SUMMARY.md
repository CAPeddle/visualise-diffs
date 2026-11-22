---
phase: 02-model-contract-and-fixture-harness
plan: 07
subsystem: testing
tags: [fixtures, golden-files, yamldotnet, ratchet, accept-mode, unified-diff, line-endings]

requires:
  - phase: 02-model-contract-and-fixture-harness
    provides: "02-02 YamlDotNet 18.1.0, RepoRoot.Find(); 02-04 Ratchet.Evaluate, FixtureReport.WriteIfRequested, Shapes; 02-05 SchemaSupport.ValidateOutputs, ContractSamples; 02-06 frozen schemas"
provides:
  - "FixtureLoader.Load: strict fixture.yml loading for synthetic and private fixtures with fixed-text errors"
  - "FixtureHarness: BuildSyntheticRepo, CliArguments, RunCli, SchemaGate, Evaluate, UnifiedDiff, RunSynthetic, RunAll"
  - "AcceptMode: CI refusal, one-shape rule, two-file copy into expected/, link refusals"
  - "RepoHygieneTests.TextFileProblems and a fact that rejects CR or BOM in fixtures and schemas"
affects: [02-08, 02-09, 02-10, azure-pipelines]

actuals:
  tokens: 19700
  tasks: 3
  commits: 5
plan_head_before: 3dbb5fdc92578dcdf3b5da8f6182cd1eebbd3911
plan_head_after: 92d36f5ae0b42b06ec7767ccc896b8dc4230c984

tech-stack:
  added: []
  patterns:
    - "Harness drives CliApp.Run end to end on a repository built from the fixture's bytes; the ratchet judges byte-for-byte comparisons"
    - "Loader errors are constant strings (they become report rows); a detail goes only to the log and is null for private fixtures"
    - "Accept mode validates everything before the first copy, then RunAll always throws afterwards"

key-files:
  created:
    - tests/Vdiff.Tests/Fixtures/FixtureConfig.cs
    - tests/Vdiff.Tests/Fixtures/FixtureLoader.cs
    - tests/Vdiff.Tests/Fixtures/FixtureHarness.cs
    - tests/Vdiff.Tests/Fixtures/AcceptMode.cs
    - tests/Vdiff.Tests/Fixtures/FixtureHarnessTests.cs
  modified:
    - tests/Vdiff.Tests/Safety/RepoHygieneTests.cs

key-decisions:
  - "Accept mode refuses a fixture with no expected/ directory instead of creating it (fail closed); a missing single golden inside an existing expected/ is written"
  - "RunAll in accept mode throws even when the named shape is absent, fails to load or did not run (message: nothing was rewritten), so accept mode can never end green"
  - "SyntheticResult gained an optional third member Run (the CLI run) so RunAll can log a failed run's exit code and stderr; the two artifact-table members are unchanged"
  - "FixtureFile string and ExpectedOutputs members are non-nullable with defaults (Expected, Diff, Summary use null!); the loader rejects absent or null values before returning, so consumers never see null"
  - "Evaluate attaches a unified diff to every mismatch, including in private mode; plan 02-09 must not log MismatchDiff for private fixtures"
  - "Loader tree failures carry a base/ or head/ prefix in the detail"

patterns-established:
  - "Loader negative rows: one clean baseline Yaml(...) with a single mutation per row, each asserting its own fixed message"
  - "Fixture text in tests is built by concatenation, never a multi-line literal, so line endings never change golden bytes"

requirements-completed: []
requirements-advanced: [ACC-01]

coverage:
  - id: D1
    description: "Strict fixture.yml loader for synthetic and private fixtures; 15 YAML-level, size, tree, link and private negatives each with their own fixed message, private details null"
    requirement: "ACC-01"
    verification:
      - kind: unit
        ref: "tests/Vdiff.Tests/Fixtures/FixtureHarnessTests.cs (loader negatives, tree limits, link, private fixtures)"
        status: pass
    human_judgment: false
  - id: D2
    description: "Harness builds the two-commit repository, drives CliApp.Run and judges GREEN, RED and FAIL on real runs; a green temporary fixture passes and the same fixture marked pending fails as unexpectedly matches"
    requirement: "ACC-01"
    verification:
      - kind: e2e
        ref: "FixtureHarnessTests#A_green_fixture_with_identical_trees_passes_green_end_to_end, #A_green_fixture_marked_pending_is_FAIL_because_it_unexpectedly_matches, #Pending_outputs_that_differ_from_their_goldens_are_RED_and_carry_a_unified_diff"
        status: pass
    human_judgment: false
  - id: D3
    description: "Tool failure, missing golden, missing output, stderr text and a schema-invalid run are FAIL in every state"
    requirement: "ACC-01"
    verification:
      - kind: e2e
        ref: "FixtureHarnessTests#A_failed_tool_run_is_FAIL_and_never_RED, #A_missing_golden_is_FAIL_while_the_other_output_is_still_judged, #A_missing_output_is_FAIL_in_every_state, #Any_text_on_stderr_is_FAIL_even_with_exit_code_zero, #The_schema_gate_rejects_a_run_meta_with_another_schema_name"
        status: pass
    human_judgment: false
  - id: D4
    description: "Unified diff by git diff --no-index under an isolated configuration, capped at 200 lines plus a count; RunAll logs it and writes the VDIFF_FIXTURE_REPORT table"
    requirement: "ACC-01"
    verification:
      - kind: unit
        ref: "FixtureHarnessTests#A_long_unified_diff_is_cut_to_200_lines_and_a_final_count, #RunAll_judges_every_fixture_logs_each_mismatch_and_writes_the_report"
        status: pass
    human_judgment: false
  - id: D5
    description: "Accept mode: refused in CI and for anything but one shape name, writes only diff.json and summary.mmd into expected/, refuses links, always fails the run"
    requirement: "ACC-01"
    verification:
      - kind: unit
        ref: "FixtureHarnessTests (Accept_mode_is_refused_in_CI..., Apply_*, RunAll_in_accept_mode_*, RunAll_refuses_accept_mode_*)"
        status: pass
    human_judgment: false
  - id: D6
    description: "Fixture and schema files are guarded against CR and BOM over working-tree bytes"
    requirement: "ACC-01"
    verification:
      - kind: unit
        ref: "RepoHygieneTests#Fixture_and_schema_files_have_no_carriage_return_or_bom, #The_text_file_guard_flags_a_carriage_return_or_a_byte_order_mark"
        status: pass
    human_judgment: false
  - id: D7
    description: "Whether the accept-mode workflow (local run, then reviewed git diff) is usable for maintainers"
    verification: []
    human_judgment: true
    rationale: "Usability of the maintainer flow and the README wording that will describe it (plan 02-08) cannot be asserted by a test"

duration: 29min
completed: 2026-11-21
status: complete
---

# Phase 2 Plan 07: Golden-fixture harness, accept mode and the CR/BOM guard Summary

**A strict fixture.yml loader, a harness that builds a deterministic two-commit repository and drives the real CLI, byte-exact goldens judged by the ratchet with unified diffs and a published report, an accept mode that cannot run in CI or end green, and a CR/BOM guard over fixtures and schemas.**

## Performance

- **Duration:** about 29 min wall clock, 14 min 36 s of it the full suite
- **Started:** 2026-11-21T09:18:05Z
- **Completed:** 2026-11-21T09:46:50Z
- **Tasks:** 3 (task 1 tracer, tasks 2 and 3 each RED then GREEN)
- **Files modified:** 6 (5 created, 1 modified)

## Accomplishments

- `FixtureLoader.Load` uses YamlDotNet's default deserialiser (no `IgnoreUnmatchedProperties`), reads bytes (no `ReadAllText`), enforces the 64 KiB cap, shape equals directory name, state and due rules, and for synthetic fixtures walks the trees itself (links, build-output names, 256 KiB per file, 200 files). Private fixtures need an absolute existing `repo` and full commit ids, refuse `base/`/`head/`, and never carry a detail.
- `FixtureHarness` builds the repository from the stored bytes (deleting base files absent from head), drives `CliApp.Run` with `--repo/--base/--head/--out` plus the config's include and blank entries, and turns each comparison into a row via `Ratchet.Evaluate`.
- The tracer slice confirmed the plan's central truth on a real run: a tree with no C++ source exits 0, writes nothing to stderr, and emits exactly `Serialize(EmptyDiff())` and the three-classDef diagram. A temporary green fixture passes GREEN, and the same fixture marked pending fails as "unexpectedly matches".
- `UnifiedDiff` runs `git diff --no-index` on copies under fixed names with `GIT_CONFIG_NOSYSTEM` and an empty `GIT_CONFIG_GLOBAL`, cut to 200 lines plus `... (N more lines)`.
- `RunAll` logs each mismatch diff and a failed run's exit code and stderr, turns a stray directory or unloadable fixture into FAIL rows, and writes the report through `FixtureReport.WriteIfRequested`.
- `AcceptMode` and its wiring: refusal under `TF_BUILD` or `CI`, one exact shape name, validate-then-copy of two files, link refusals, and `RunAll` always throws after writing the report.
- `RepoHygieneTests.TextFileProblems` plus a fact over tracked `tests/fixtures/` files and top-level `docs/schema/*.json` (the three schema files satisfy the scanned >= 3 floor today).

## Task Commits

1. **Task 1 (tracer): loader, harness core, green end to end** - `4c5f095` (feat)
2. **Task 2 RED: failing tests for diff, RunAll, report, loader negatives** - `c377e66` (test)
3. **Task 2 GREEN: unified diff, RunAll, report** - `8c405d1` (feat)
4. **Task 3 RED: failing tests for accept mode and the CR/BOM guard** - `0bca0f4` (test)
5. **Task 3 GREEN: accept mode and the guard** - `92d36f5` (feat)

## Tracer gate

Task 1 is `type="tracer"`, `human_verify_mode` is `end-of-phase` and its `<verify>` is automated-only, so the gate was a re-run. The first run passed with 2 FixtureHarnessTests (34 with RepoHygieneTests), and the same tests passed again in every later run. Tracer verified end to end, expansion followed.

## TDD Gate Compliance

`workflow.tdd_mode` is not enabled and the plan is `type: execute`, so `gsd_run check tdd-red-evidence` was not run and no evidence file was written. RED stubs return neutral values so failures are assertion failures, not compile or NotImplemented errors.

- **Task 2 RED** (`c377e66`): `Failed: 16, Passed: 67, Total: 83` (with RepoHygieneTests). The 16 failures are the target tests failing on their assertions: the stub `UnifiedDiff` returns empty, the stub `RunAll` returns nothing, `Evaluate` carried no mismatch diff, and the loader detail had no `base/`/`head/` prefix. Semantic assessment: every target ran and failed for the planned reason. The loader negatives, tool-failure, missing-golden and schema-gate tests passed already against Task 1 code (the plan expected this); they are regression coverage, not RED evidence.
- **Task 2 GREEN** (`8c405d1`): `Passed: 83, Failed: 0`.
- **Task 3 RED** (`0bca0f4`): `Failed: 28, Passed: 97, Total: 125`. The 28 failures are the refusal rows that expect a message, the shape-request rows, every Apply and accept-mode end-to-end test and the flagged hygiene rows, all on assertions. The refusal rows that expect null, the clean-byte rows and the hygiene fact passed vacuously against the neutral stubs; they are not RED evidence.
- **Task 3 GREEN** (`92d36f5`): `Passed: 125, Failed: 0`.
- **REFACTOR:** none.

## Verification

- Final filtered run (FixtureHarnessTests plus RepoHygieneTests): `Passed: 125, Failed: 0`. By class: FixtureHarnessTests 85 (plan floor 16), RepoHygieneTests 40 (32 before; plan floor for both together 40).
- Full suite once after the last code commit, `dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --filter "Category!=MermaidCli"`: `Passed: 838, Failed: 0, Total: 838` (14 m 36 s). 745 before this plan plus 93. The `TraceRunScriptTests` flake did not recur.
- Acceptance greps: `UnderscoredNamingConvention` 1, `IgnoreUnmatchedProperties` 0, `ReadAllText` in the loader 0, `CliApp.Run` 2, `Ratchet.Evaluate` 2, `--no-index` 2, `GIT_CONFIG_GLOBAL` 1, `WriteIfRequested` 1, `the tool run failed` 2, `"TF_BUILD"` 1, `"VDIFF_FIXTURE_ACCEPT"` 1, `accept mode rewrote` 2, `TextFileProblems` 3.
- `check-public-diff.sh --cached` printed `clean` before every commit. All new and edited files hold LF only (CR count 0).

## Deviations from Plan

None - plan executed exactly as written. The choices the plan left open are listed under key-decisions (accept mode fail-closed rules, the optional `Run` member, non-nullable POCO members, the `base/`/`head/` detail prefix).

## Issues Encountered

- A first attempt to patch C# with a bash heredoc failed to parse (the same class of problem 02-03, 02-05 and 02-12 hit); the patch was moved to a script file and applied. No code was affected.
- A static test helper used the primary-constructor `output` parameter (CS9105); it became an instance method.

## Known Stubs

None. The RED-phase stubs (`UnifiedDiff`, `RunAll`, `AcceptMode`, `TextFileProblems`) were replaced in their GREEN commits.

## Threat Flags

None. T-02-19 to T-02-23 are covered: loader negatives (size, unknown key, shape, state), the manual tree walk, accept mode's refusals and always-fail rule, FAIL-in-every-state tests and the CR/BOM fact. Note for 02-09: `FixtureOutcome.MismatchDiff` holds target content when `Evaluate` runs over a private fixture, so a private caller must not log it.

## Next Phase Readiness

- 02-08 can add the first committed fixtures and call `FixtureHarness.RunAll` over `tests/fixtures`; it must change `RepoHygieneTests.TrackedPathProblems` in the same commit as the first golden (left untouched here).
- 02-09 can reuse `CliArguments`, `RunCli` and `Evaluate`, and should refuse `VDIFF_FIXTURE_ACCEPT` itself.
- No builds or tests are running; `obj/` and `bin/` are free.

## Self-Check: PASSED

- Files exist: FixtureConfig.cs, FixtureLoader.cs, FixtureHarness.cs, AcceptMode.cs, FixtureHarnessTests.cs, and the modified RepoHygieneTests.cs.
- Commits `4c5f095`, `c377e66`, `8c405d1`, `0bca0f4`, `92d36f5` are ancestors of HEAD; `git rev-list --count` from the recorded base gives 5.
