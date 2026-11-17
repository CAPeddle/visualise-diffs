---
phase: 01-offline-tracer
plan: 03
subsystem: tracer-pipeline
tags: [dotnet, cli, git-objects, tree-sitter-sidecar, mermaid, html-report, xunit]

requires:
  - phase: 01-offline-tracer
    provides: "plan 01-01 Rust sidecar (protocol v0) and plan 01-02 git object reader, scope filter, sidecar client, SyntheticRepo and RepoSnapshot"
provides:
  - "Model records and ModelBuilder (unique ids, ordinal stable ordering, timeout/error files become unmodelled entries)"
  - "ModelDiffer (types by id, members by key, summary counts, per-side unmodelled counts and warnings)"
  - "TracerPipeline.Run: in-memory, one sidecar call per commit, writes nothing"
  - "OutputWriter, MermaidWriter, HtmlWriter: the five fixed files under --out"
  - "CliOptions and CliApp.Run (exit 0 or 1, git version gate, --out containment refusal) and the vdiff executable project"
  - "SidecarFixture (always builds the sidecar, sets VDIFF_CPP_TS) and the sidecar xunit collection"
affects: [01-04, 01-05, 01-06, 01-07, 01-08, 01-09, 01-10]

plan_head_before: daace6f8b7a78ceb79307a0ce7243093aa20ed4f
plan_head_after: 67b2485db15f865bc9f614fbad365ca494d5b036

actuals:
  tokens: 15200
  tasks: 3
  commits: 4

tech-stack:
  added: []
  patterns:
    - "Pipeline builds every model and the diff in memory; --out is created only afterwards, so an earlier failure leaves it absent"
    - "Containment check on directory boundaries (OrdinalIgnoreCase on Windows, Ordinal elsewhere) against work tree, git dir and common dir"
    - "Canonical JSON: indented, NewLine LF, snake_case, nulls omitted, relaxed escaping, trailing LF, UTF-8 without BOM"
    - "Every test class that reaches the real sidecar or CliApp.Run carries [Collection(\"sidecar\")]"

key-files:
  created:
    - src/Vdiff.Core/Model/ModelTypes.cs
    - src/Vdiff.Core/Model/ModelBuilder.cs
    - src/Vdiff.Core/Diff/ModelDiffer.cs
    - src/Vdiff.Core/Pipeline/TracerPipeline.cs
    - src/Vdiff.Core/Output/OutputWriter.cs
    - src/Vdiff.Core/Output/MermaidWriter.cs
    - src/Vdiff.Core/Output/HtmlWriter.cs
    - src/Vdiff.Core/Cli/CliOptions.cs
    - src/Vdiff.Core/Cli/CliApp.cs
    - src/Vdiff.Cli/Vdiff.Cli.csproj
    - src/Vdiff.Cli/Program.cs
    - tests/Vdiff.Tests/Support/SidecarFixture.cs
    - tests/Vdiff.Tests/TracerEndToEndTests.cs
    - tests/Vdiff.Tests/ModelAssemblyTests.cs
    - tests/Vdiff.Tests/CliOptionsTests.cs
  modified:
    - vdiff.sln

key-decisions:
  - "Model JSON is labelled vdiff-model/v0-tracer and diff JSON vdiff-diff/v0-tracer; both stay provisional (Phase 2 fixes v1)"
  - "Non-UTF-8 changed paths (reported by ListChanged without a side) are recorded as unmodelled on both sides"
  - "macros_blanked records the union of the requested names and what the sidecar reports, sorted ordinal"
  - "Unmodelled entries use fixed reasons parse-timeout and parse-error for sidecar files, plus the read-side reasons from plan 01-02"
  - "Mermaid class ids are the fqn with non-identifier characters replaced by underscore, made unique with _2, _3; no edges are drawn yet (bases appear only in diff.json and report.html)"

patterns-established:
  - "Stub-returning-empty RED: the RED commit compiles against stubs that return empty results, so the test fails on the planned assertion"
  - "Tests that run the CLI take RepoSnapshot before and after (excluding out/) and compare by value"

requirements-completed: [TRC-01, TRC-02, SPK-02]

coverage:
  - id: D1
    description: "vdiff run on a synthetic two-commit C++ repository exits 0 and writes base.model.json, head.model.json, diff.json, summary.mmd and report.html under --out; diff.json lists exactly the expected types and members"
    requirement: TRC-01
    verification:
      - kind: e2e
        ref: "tests/Vdiff.Tests/TracerEndToEndTests.cs#Cli_run_on_fixture_A_writes_the_five_files_and_leaves_the_repository_untouched"
        status: pass
      - kind: e2e
        ref: "tests/Vdiff.Tests/TracerEndToEndTests.cs#Pipeline_on_fixture_A_returns_the_expected_diff_through_the_real_sidecar"
        status: pass
    human_judgment: false
  - id: D2
    description: "The analysed repository is byte-identical after a run (sandbox hash, git status, HEAD, branch, refs, stash, worktrees); an --out inside the repository, its .git or the repository root is refused with exit 1"
    requirement: TRC-01
    verification:
      - kind: e2e
        ref: "tests/Vdiff.Tests/TracerEndToEndTests.cs#Cli_run_on_fixture_A_writes_the_five_files_and_leaves_the_repository_untouched"
        status: pass
      - kind: e2e
        ref: "tests/Vdiff.Tests/TracerEndToEndTests.cs#Cli_refuses_an_out_directory_inside_the_repository"
        status: pass
    human_judgment: false
  - id: D3
    description: "C++ extraction runs through the Rust sidecar pinned to tree-sitter 0.27.0 and tree-sitter-cpp 0.23.4; each model records versions, mode, macros blanked and per-file error-node count"
    requirement: SPK-02
    verification:
      - kind: e2e
        ref: "tests/Vdiff.Tests/TracerEndToEndTests.cs (AssertExtractionMetadata on base and head models, in memory and as written)"
        status: pass
    human_judgment: false
  - id: D4
    description: "A sidecar file with status timeout or error is never modelled: it becomes a parse-timeout or parse-error unmodelled entry counted in summary.unmodelled and the warnings"
    requirement: TRC-02
    verification:
      - kind: unit
        ref: "tests/Vdiff.Tests/ModelAssemblyTests.cs#A_timed_out_or_failed_file_is_unmodelled_never_modelled and #Unmodelled_entries_are_counted_in_the_diff_summary_and_the_warnings"
        status: pass
    human_judgment: false
  - id: D5
    description: "report.html is one self-contained file with no script element and no external URL; Mermaid text is HTML-encoded"
    requirement: TRC-01
    verification:
      - kind: e2e
        ref: "tests/Vdiff.Tests/TracerEndToEndTests.cs (report.html assertions)"
        status: pass
    human_judgment: false
  - id: D6
    description: "Command line surface: required flags once, repeatable --blank and --include with limits, --help, commit ids 7 to 64 hex characters, git 2.45 gate in the reader constructor"
    requirement: TRC-01
    verification:
      - kind: unit
        ref: "tests/Vdiff.Tests/CliOptionsTests.cs"
        status: pass
    human_judgment: false
  - id: D7
    description: "Rendering quality of summary.mmd in Azure DevOps and a real Mermaid parser (S1 spike, plans 01-08 and 01-09)"
    verification: []
    human_judgment: true
    rationale: "This plan asserts the text shape only; no Mermaid parser was run against it here."

duration: 13min
completed: 2026-11-17
status: complete
---

# Phase 1 Plan 03: End-to-end tracer Summary

**One `vdiff --repo --base --head --out` run reads two commits from git objects, extracts C++ types and members through the pinned Rust sidecar, diffs them and writes base.model.json, head.model.json, diff.json, summary.mmd and report.html under --out, leaving the analysed repository byte-identical.**

## Performance

- **Duration:** about 13 min of agent time (start 2026-11-17T05:02:43Z, SUMMARY at about 05:17Z)
- **Tasks:** 3 (task 3 is the package checkpoint, answered by the orchestrator; no code)
- **Commits:** 4 code commits, measured from `plan_head_before` to `plan_head_after`
- **Tests:** 125 passed in the .NET assembly (104 before this plan), 65 passed in cargo

## Accomplishments

- Task 1 (TDD): model records, `ModelBuilder`, `ModelDiffer` and `TracerPipeline`, proven on Fixture A through the real sidecar: three changed types in order `class shop::Gadget` (modified), `class shop::Widget` (added, base Gadget) and `struct shop::Legacy` (removed), with the expected member states, summary counts and ADR-001 metadata.
- Task 2 (tracer): the three writers, `CliOptions`, `CliApp`, the `vdiff` executable project and the end-to-end test. The tracer gate was applied in the default mode (end-of-phase, automated-only verify): all four `<verify>` commands were re-run on the committed tree and passed, so expansion continued.
- A manual run of the built tool on a scratch two-commit repository outside the project (Release build, `dotnet run`) exited 0 and produced the five files; the Mermaid text was `classDiagram` with `:::modified` and `[added]` / `[modified]` member tags.
- `SidecarFixture` always runs `cargo build --release --locked` first, fails the run on a build error and sets `VDIFF_CPP_TS`, which is the rule for later plans' test classes.

## Task Commits

1. **Task 1 RED:** `66b1622` (test) - fails with `Actual: string[] []` against stubs returning empty results.
2. **Task 1 GREEN:** `a1b7a07` (feat) - builder, differ, pipeline, plus four unit tests on hand-made sidecar answers.
3. **Task 2:** `df7b990` (feat) - writers, CLI, executable project, solution entry, end-to-end and CLI tests.
4. **Rename:** `67b2485` (refactor) - `ModelBuilderTests` renamed to `ModelAssemblyTests` (see Deviations).
5. **Task 3:** no commit; the answer is recorded below.

## TDD Notes

- RED `66b1622`: the target test `Pipeline_on_fixture_A_returns_the_expected_diff_through_the_real_sidecar` ran (the sidecar was built and located) and failed on the planned assertion: expected the three type ids, got an empty array. Semantic assessment: valid RED, not a compile, fixture or discovery failure.
- `gsd_run check tdd-red-evidence` was not run: the plan is `type: execute` and the project does not enable `workflow.tdd_mode`.
- GREEN `a1b7a07` passed on the first run. No REFACTOR commit was needed.

## Package checkpoint

package-checkpoint: approved mermaid-cli=12.2.0 puppeteer=25.17.0

The answer was supplied by the orchestrator from the product owner's direct answer, asked in the orchestrator session on 2026-11-16 per D-11. I did not ask or verify the packages myself, and nothing is installed by this plan; plan 01-09 reads the line above and installs only those two versions.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Test class name would collide with plan 01-07**
- **Found during:** close-out check of later plans
- **Issue:** this plan's extra unit-test class `ModelBuilderTests` has the name plan 01-07 gives its own file (`tests/Vdiff.Tests/Diff/ModelBuilderTests.cs`), which risks a duplicate type.
- **Fix:** renamed file and class to `ModelAssemblyTests`.
- **Files modified:** `tests/Vdiff.Tests/ModelAssemblyTests.cs`
- **Commit:** `67b2485`

### Additions beyond the plan text (not defects)

- Two extra test files not listed in `files_modified`: `tests/Vdiff.Tests/ModelAssemblyTests.cs` (builder and differ on canned answers, covering the timeout/error, duplicate-id and ordinal-ordering truths without a process) and `tests/Vdiff.Tests/CliOptionsTests.cs` (flag validation, help, containment on directory boundaries).
- Extra public surface: `CliApp.IsSameOrUnder`, `States`, `OutputWriter.Serialize`, `CliOptions.Usage` and its limit constants, `TracerPipeline`'s `PipelineResult` record, and the `UnmodelledCounts`, `BaseChanges` and similar record types in `ModelTypes.cs`.
- The output-file name constant for diff.json is `OutputWriter.DiffJsonFile` (not `DiffFile`, which is the record type).
- `dotnet run ... --nologo -- --help` (the plan's verify command) passes `--nologo` through to the app, which sees `--help` first and exits 0; the count is 2 lines.

**Total deviations:** 1 auto-fixed (Rule 3). **Impact:** none on scope or behaviour.

## Authentication Gates

None.

## Issues Encountered

- Python and heredoc escapes in the shell tool turned `'\r'` and `'\n'` in a generated test file into raw characters; it was caught before any commit by a CR count of the file and repaired. All committed files have LF only (`.gitattributes` pins `*.cs` to LF).
- Git prints "LF will be replaced by CRLF" for the new csproj; this is informational under `core.autocrlf=true`.

## Known Stubs

None. The RED-phase stubs were replaced in `a1b7a07`.

## Known Limitations (for later plans)

- `--out` containment compares normalised full paths; it does not resolve symlinks or junctions (hardening belongs to plans 01-04 and 01-10).
- `report.html` shows `int count_ -> int count_` for a member whose access changed but whose signature did not (cosmetic).
- Mermaid output draws no base-class edges yet and was not run through a Mermaid parser (plans 01-08 and 01-09).
- Non-UTF-8 paths count as unmodelled on both sides because `ListChanged` does not say which side they came from.

## Threat Flags

None beyond the plan's register. T-01-03, T-01-06 and T-01-28 mitigations are implemented here: synthetic fixtures only, HTML-encoded values with no script element or external URL, and containment checked before any write with fixed output file names. T-01-SC is addressed by the checkpoint answer above.

## Next Phase Readiness

Ready for plans 01-04 to 01-10: `CliApp.Run`, `TracerPipeline.Run`, `ModelBuilder.Build`, `ModelDiffer.Diff`, the three writers and `SidecarFixture` have the names and shapes the plan fixed.

## Self-Check: PASSED

- All 15 created files and the modified `vdiff.sln` exist on disk.
- Commits `66b1622`, `a1b7a07`, `df7b990` and `67b2485` are reachable from HEAD (`git rev-list --count` from the base gives 4).
- `dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --nologo`: 125 passed, 0 failed. `cargo test --locked --manifest-path extractors/cpp-ts/Cargo.toml`: 65 passed. The `TracerEndToEndTests` filter gives 3 passed. `--help` count is 2.
- `git ls-files` lists no `diff.json`, `summary.mmd`, `report.html`, `*.model.json`, `target/`, `bin/` or `obj/` path.
- `check-public-diff.sh` reported no target names before every commit.
