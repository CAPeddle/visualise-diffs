---
phase: 02-model-contract-and-fixture-harness
plan: 11
subsystem: output
tags: [csharp, run-meta, determinism, output-writer, html-report]

requires:
  - phase: 02-model-contract-and-fixture-harness
    provides: "02-01 OutputWriter.FileNames (tests iterate it, so a sixth file needs no test edits)"
provides:
  - "RunMeta, ExtractorRun and ToolVersion records; RunMeta.SchemaName vdiff-run-meta/v1; RunMeta.CurrentVdiffVersion"
  - "OutputWriter.MetaFile, FileNames (six) and CanonicalFileNames (four)"
  - "PipelineResult.Meta; every run writes run.meta.json"
  - "HtmlWriter.Render(diff, mermaid, meta): report.html reads its commit ids from RunMeta"
  - "DiffFile without Base and Head: diff.json holds no commit id"
affects: [02-03, 02-05, 02-06, 02-07, 02-08, 02-12]

actuals:
  tokens: 5700
  tasks: 2
  commits: 3
plan_head_before: 278035e30f1f87153dfee42d0254492ba3cbaccd
plan_head_after: 68e4695df074cf8f42019dedb9e96871022aa89b

tech-stack:
  added: []
  patterns:
    - "Volatile facts (commit ids, tool versions) live in run.meta.json, not in canonical files"
    - "Tool versions are taken from the sidecar response, not the model, so the model can drop its copy without touching the pipeline"

key-files:
  created:
    - src/Vdiff.Core/Model/RunMeta.cs
  modified:
    - src/Vdiff.Core/Pipeline/TracerPipeline.cs
    - src/Vdiff.Core/Output/OutputWriter.cs
    - src/Vdiff.Core/Output/HtmlWriter.cs
    - src/Vdiff.Core/Cli/CliApp.cs
    - src/Vdiff.Core/Cli/CliOptions.cs
    - src/Vdiff.Core/Model/ModelTypes.cs
    - src/Vdiff.Core/Diff/ModelDiffer.cs
    - tests/Vdiff.Tests/TracerEndToEndTests.cs
    - tests/Vdiff.Tests/Safety/HostileInputTests.cs
    - tests/Vdiff.Tests/Safety/RepoHygieneTests.cs
    - tests/Vdiff.Tests/Output/HtmlWriterTests.cs
    - tests/Vdiff.Tests/Output/MermaidWriterTests.cs
    - tests/Vdiff.Tests/Output/DeterminismTests.cs

key-decisions:
  - "RepoHygieneTests.GeneratedFileNames now aliases OutputWriter.FileNames, so run.meta.json is a flagged generated name and the list cannot drift again"
  - "RED for the HtmlWriter change is a placeholder Render(diff, mermaid, meta) overload that ignores meta, so the new tests fail on assertions rather than on a build error"
  - "Existing CLI test renamed from ..._writes_the_five_files_... to ..._writes_every_output_file_..."

patterns-established:
  - "New output files are added to OutputWriter.FileNames and WriteAll; canonical ones also to CanonicalFileNames"

requirements-completed: [R-23]

coverage:
  - id: D1
    description: "A run writes run.meta.json (vdiff-run-meta/v1) with vdiff_version (no '+'), base_commit, head_commit and one cpp-ts extractor with tree-sitter 0.27.0 and tree-sitter-cpp 0.23.4, taken from the sidecar response"
    requirement: "R-23"
    verification:
      - kind: e2e
        ref: "tests/Vdiff.Tests/TracerEndToEndTests.cs#Cli_run_on_fixture_A_writes_every_output_file_and_leaves_the_repository_untouched"
        status: pass
    human_judgment: false
  - id: D2
    description: "OutputWriter.FileNames names six files and CanonicalFileNames four; the determinism, leak and BOM/LF tests cover run.meta.json because they iterate FileNames"
    requirement: "R-23"
    verification:
      - kind: unit
        ref: "dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --filter Category!=MermaidCli (625 passed, 0 failed)"
        status: pass
    human_judgment: false
  - id: D3
    description: "report.html prints its base and head commit ids from RunMeta; two metas differ only in the commit text"
    requirement: "R-23"
    verification:
      - kind: unit
        ref: "tests/Vdiff.Tests/Output/HtmlWriterTests.cs#The_commit_ids_come_from_the_run_meta, #Two_run_metas_with_different_ids_give_pages_that_differ_only_in_the_commit_text"
        status: pass
    human_judgment: false
  - id: D4
    description: "DiffFile and diff.json carry no commit id; no writer or test reads a commit id from the diff"
    requirement: "R-23"
    verification:
      - kind: e2e
        ref: "tests/Vdiff.Tests/TracerEndToEndTests.cs (diff.json has no top-level base or head key)"
        status: pass
    human_judgment: false

duration: 28min
completed: 2026-11-21
status: complete
---

# Phase 2 Plan 11: run.meta.json and the volatile-metadata split Summary

**Every run writes a sixth file, `run.meta.json` (`vdiff-run-meta/v1`: vdiff version, both commit ids, cpp-ts tree-sitter versions from the sidecar response); `report.html` reads its commit ids from `RunMeta`, and `diff.json` no longer holds any.**

## Performance

- **Duration:** 28 min (the full suite alone takes 13 min; one filtered run of the four `FileNames`-iterating classes took 8 min)
- **Started:** 2026-11-21T06:58:18Z
- **Completed:** 2026-11-21T07:26:00Z
- **Tasks:** 2 (task 2 as RED, GREEN)
- **Files modified:** 14 (1 created, 13 modified)

## Accomplishments

- `RunMeta`, `ExtractorRun` and `ToolVersion` records; `CurrentVdiffVersion` reads the assembly informational version up to the first `+`, falling back to `0.0.0`.
- `TracerPipeline.BuildSide` returns the sidecar response beside the model; `Run` builds `PipelineResult.Meta` from the head response's `TreeSitter` and `TreeSitterCpp`.
- `OutputWriter` gains `MetaFile`, `FileNames` of six names in write order, `CanonicalFileNames` of four, and `WriteAll(..., RunMeta meta)`. The CLI success line reads `wrote 6 files` through the 02-01 constant; the usage text names six files.
- `HtmlWriter.Render(diff, mermaid, meta)` prints `meta.BaseCommit` and `meta.HeadCommit` with the same markup and encoding. `DiffFile` lost `Base` and `Head`; `ModelDiffer` builds it without them, in the same commit.
- Tests that read commit ids or tree-sitter versions now read them from `RunMeta` or `run.meta.json`. `ModelFile` is untouched: it keeps its commit and tool-version fields until 02-03.

## Task Commits

1. **Task 1 (tracer): run.meta.json written by every run** - `5b0167a` (feat)
2. **Task 2 RED: failing test for report.html reading RunMeta** - `8fbbe67` (test)
3. **Task 2 GREEN: report.html reads RunMeta, diff drops commit ids** - `68e4695` (feat)

**Plan metadata:** committed separately as `docs(02-11): complete run.meta.json plan`.

## TDD Gate Compliance (task 2)

- **RED** (`8fbbe67`, `test(02-11)`): two new `HtmlWriterTests` cases plus a placeholder `Render(diff, mermaid, meta)` overload that ignores `meta`. Run: `Failed: 2, Passed: 11, Total: 13`. Both target tests failed on their assertions (`Assert.Contains` on the meta-supplied commit text, `Assert.NotEqual` on two pages), not on a build error and not at zero-test discovery. Semantic assessment: the target tests ran and failed for the planned reason (the page still read the diff). `workflow.tdd_mode` is not enabled and the plan is `type: execute`, so `gsd_run check tdd-red-evidence` was not run and no evidence file was written.
- **GREEN** (`68e4695`, `feat(02-11)`): the real overload, `DiffFile` without commits, `ModelDiffer` and test helpers updated; 72 of 72 in the four classes, then the full suite.
- **REFACTOR:** none needed.

## Tracer gate

Task 1 is `type="tracer"`. Its `<verify>` is automated-only and `human_verify_mode` is `end-of-phase`, so the gate was a re-run of the verification: the three named classes plus `RepoHygieneTests` passed (87 of 87), and the other four classes that iterate `FileNames` (`AliasedOutTests`, `IncludeGlobEndToEndTests`, `RepoUnchangedMatrixTests`, `TraceRunScriptTests`) passed (113 of 113) before the commit. Tracer verified end-to-end, expansion (task 2) followed.

## Verification

- Task 1 filtered run: `Passed: 87, Failed: 0`; the four other `FileNames` classes: `Passed: 113, Failed: 0`.
- Task 2 filtered run (HtmlWriterTests, MermaidWriterTests, DeterminismTests, TracerEndToEndTests): `Passed: 72, Failed: 0`.
- Full suite `dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --filter "Category!=MermaidCli"`: `Passed: 625, Failed: 0, Total: 625` (12 min 50 s), run after the last code commit. Up from 623: two new `HtmlWriterTests` cases. The `TraceRunScriptTests` flake did not recur.
- `cargo test --locked --manifest-path extractors/cpp-ts/Cargo.toml`: 105 passed, 0 failed (sidecar unchanged).
- No tracked `bin/` or `obj/` files. All acceptance greps for both tasks returned the expected counts. Public-diff guard printed `clean` before all three commits. Every edited file holds LF only (CR count 0).

## Decisions Made

- `run.meta.json` is a generated name for the repo-hygiene check, and the list now reads `OutputWriter.FileNames` (see the deviation below).
- The RED placeholder overload is the only stub; GREEN replaced it.
- The CLI end-to-end test lost "five" from its name, since the number is no longer true.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Consistency] RepoHygieneTests.GeneratedFileNames reads OutputWriter.FileNames**
- **Found during:** Task 1 (carry-over from 02-01's Issues Encountered)
- **Issue:** `tests/Vdiff.Tests/Safety/RepoHygieneTests.cs` kept its own five-name list, so `run.meta.json` would not have been flagged as a generated name. The file is not in this plan's `files_modified`.
- **Fix:** `GeneratedFileNames` is now `OutputWriter.FileNames` (one `using` added), so `run.meta.json` is flagged and the list cannot drift again. Plan 02-08's later hygiene change (accept `diff.json` and `summary.mmd` only under `tests/fixtures/<shape>/expected/`, still flag `run.meta.json`) works unchanged on top of this.
- **Files modified:** tests/Vdiff.Tests/Safety/RepoHygieneTests.cs
- **Verification:** `RepoHygieneTests` passed in the Task 1 filtered run and the full suite.
- **Committed in:** `5b0167a`

**2. [Rule 3 - Consistency] Existing CLI test renamed**
- **Found during:** Task 1
- **Issue:** `Cli_run_on_fixture_A_writes_the_five_files_...` would be false with six files. Its only references outside the file are two historical `ref:` strings in the 01-03 SUMMARY, which are records and were left alone.
- **Fix:** renamed to `Cli_run_on_fixture_A_writes_every_output_file_and_leaves_the_repository_untouched`; Test 1's run.meta.json assertions were added inside it.
- **Files modified:** tests/Vdiff.Tests/TracerEndToEndTests.cs
- **Committed in:** `5b0167a`

---

**Total deviations:** 2 auto-fixed (both Rule 3 consistency). **Impact on plan:** one extra file outside the 13 listed; no scope creep.

## Issues Encountered

None. The test files `MermaidWriterTests.cs` holds non-ASCII text, so the patch script had to read and write it as UTF-8; the first attempt aborted before touching it and was rerun correctly.

## Known Stubs

None. The RED-phase overload was replaced in the GREEN commit.

## Threat Flags

None. `run.meta.json` is the threat model's T-02-01 surface and is covered: the leak test iterates `FileNames`, and `RunMeta` holds only commit ids, tool versions and the vdiff version with `+` build metadata removed.

## Next Phase Readiness

- 02-03 can drop `ModelFile.Commit` and the `ExtractionInfo` tree-sitter fields; the pipeline already takes versions from the sidecar response.
- 02-05 can add the run-meta schema (`RunMeta` field order is the JSON key order); 02-12 can add the same-trees-different-commits separation test over `CanonicalFileNames`.
- Until 02-03, commit ids exist both in `run.meta.json` and in the model files, as the plan intends.
- No builds or tests are running; `obj/` and `bin/` are free.

## Self-Check: PASSED

- `src/Vdiff.Core/Model/RunMeta.cs` exists.
- Commits `5b0167a`, `8fbbe67`, `68e4695` are on the branch; `git rev-list --count` from the recorded base gives 3.
- Plan verification run (full suite 625 of 625, cargo 105 of 105, no tracked bin/obj).

---
*Phase: 02-model-contract-and-fixture-harness*
*Completed: 2026-11-21*
