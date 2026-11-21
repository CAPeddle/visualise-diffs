---
phase: 02-model-contract-and-fixture-harness
plan: 02
subsystem: testing
tags: [azure-pipelines, njsonschema, yamldotnet, gitattributes, xunit]

requires:
  - phase: 01-offline-tracer
    provides: Vdiff.Tests project, RepoHygieneTests root search, Rust sidecar and mermaid-check tool paths
provides:
  - NJsonSchema 11.6.2 and YamlDotNet 18.1.0 pinned in the test project only
  - azure-pipelines.yml, two Microsoft-hosted legs (windows-2025, ubuntu-24.04), not yet connected to an organisation
  - PipelineYamlTests.PipelineProblems, a static checker with a negative case per rule
  - RepoRoot.Find(), the never-skip checkout-root helper for later plans
  - .gitattributes LF rules for fixtures, schemas and the pipeline file
affects: [02-05, 02-07, 02-08, 02-09, 02-10]

actuals:
  tokens: 8000
  tasks: 2
  commits: 1
plan_head_before: 22c25f76a07b5b07c741a41ef0bb1c1058f008f0
plan_head_after: 8745669836b2cb64835e6b1cb77b09e92f54f17c

tech-stack:
  added: [NJsonSchema 11.6.2 (tests only), YamlDotNet 18.1.0 (tests only)]
  patterns:
    - "Pipeline shape is checked statically by a pure PipelineProblems(string) function with rule-tagged messages"
    - "Negative theory mutates a known-clean baseline once per case, so each rule is proven in isolation"

key-files:
  created:
    - azure-pipelines.yml
    - tests/Vdiff.Tests/Ci/PipelineYamlTests.cs
    - tests/Vdiff.Tests/Support/RepoRoot.cs
  modified:
    - tests/Vdiff.Tests/Vdiff.Tests.csproj
    - .gitattributes

key-decisions:
  - "Negative cases are single-defect mutations of an inline clean baseline (not unrelated fragments), and each asserts a problem carrying its own rule tag"
  - "Script statements are split on newline, &&, || and ;, so a second cargo or npm command in one script is checked on its own"
  - "Pool given as a bare scalar (pool: Name) is treated as a pool name, as well as a mapping with a name key"
  - "Forbidden keys match case-insensitively; the System access token check also covers SYSTEM_ACCESSTOKEN"

patterns-established:
  - "Tests locate the checkout through RepoRoot.Find(), which throws instead of skipping"

requirements-completed: [ACC-01]

coverage:
  - id: D1
    description: "NJsonSchema and YamlDotNet pinned at exact versions in Vdiff.Tests only; Vdiff.Core and Vdiff.Cli unchanged"
    requirement: "ACC-01"
    verification:
      - kind: other
        ref: "grep of the three csproj files; dotnet restore resolved NJsonSchema/11.6.2 and YamlDotNet/18.1.0 in project.assets.json"
        status: pass
    human_judgment: false
  - id: D2
    description: "azure-pipelines.yml with two hosted legs, locked cargo, exact npm ci, trx and fixture report publishing, advisory Linux Mermaid step"
    requirement: "ACC-01"
    verification:
      - kind: unit
        ref: "tests/Vdiff.Tests/Ci/PipelineYamlTests.cs#Committed_pipeline_has_no_problems"
        status: pass
    human_judgment: true
    rationale: "The pipeline cannot run until an Azure DevOps organisation is connected (D-09); a real run of both legs is deferred to phase UAT"
  - id: D3
    description: "Every PipelineProblems rule fires on a single-defect case; committed file is LF-only and ends with a line feed"
    requirement: "ACC-01"
    verification:
      - kind: unit
        ref: "dotnet test --filter FullyQualifiedName~PipelineYamlTests (18 passed, 0 failed)"
        status: pass
    human_judgment: false
  - id: D4
    description: ".gitattributes pins tests/fixtures/**, docs/schema/*.json and azure-pipelines.yml to LF, no catch-all text=auto"
    requirement: "ACC-01"
    verification:
      - kind: other
        ref: "grep -v '^#' .gitattributes | grep -c eol=lf returns 10; git ls-files --eol on azure-pipelines.yml shows i/lf w/lf attr/text eol=lf"
        status: pass
    human_judgment: false

duration: 40min
completed: 2026-11-21
status: complete
---

# Phase 2 Plan 02: Package gate, two-leg pipeline and LF rules Summary

**NJsonSchema 11.6.2 and YamlDotNet 18.1.0 pinned in the test project only, a two-leg Microsoft-hosted azure-pipelines.yml guarded by a rule-tagged static checker with 15 negative cases, a never-skip RepoRoot helper, and LF rules for fixtures, schemas and the pipeline file.**

## Package checkpoint

package-checkpoint: approved njsonschema=11.6.2 yamldotnet=18.1.0

The orchestrator relayed the product owner's recorded answer ("approved", 2026-11-21) after showing the nuget.org URLs and registry facts. `dotnet restore` resolved exactly `NJsonSchema/11.6.2` and `YamlDotNet/18.1.0` (checked in `tests/Vdiff.Tests/obj/project.assets.json`); no other version was resolved and nothing was substituted.

## Performance

- **Duration:** about 40 min, most of it the 12 min 43 s full-suite run
- **Completed:** 2026-11-21T06:37Z
- **Tasks:** 2 (Task 1 was the recorded checkpoint, Task 2 the commit)
- **Files modified:** 5

## Accomplishments
- Both packages pinned with exact `Version=` in `Vdiff.Tests.csproj`; `Vdiff.Core.csproj` and `Vdiff.Cli.csproj` each count 0 for either name.
- `azure-pipelines.yml`: `build_and_test` job, 45-minute timeout, matrix `windows-2025` and `ubuntu-24.04`, `persistCredentials: false`, `UseDotNet@2` from global.json, cargo build and test with `--locked`, solution build, tests with `Category!=MermaidCli` and `VDIFF_FIXTURE_REPORT`, trx and fixture-report publishing, and a Linux-only advisory Mermaid step (`continueOnError: true`, `VDIFF_MERMAID_NO_SANDBOX`, `npm --prefix tools/mermaid-check ci --ignore-scripts`).
- `PipelineYamlTests`: `PipelineProblems` walks the YamlStream and enforces the image allow-list, one Windows and one Ubuntu leg, no pool name, no organisation/project/connection/group keys, no System access token, `persistCredentials: false` on every checkout, `--locked` on every cargo build/test, the exact npm command, and the Mermaid filter on the step that sets `VDIFF_FIXTURE_REPORT`. 18 tests: committed-file fact, LF-only fact, baseline-clean fact, 15 negative theory cases.
- `.gitattributes` gains the three LF rules; no `* text=auto`. No tracked file matches the new patterns yet, so nothing needs renormalising.

## Task Commits

1. **Task 1: Package legitimacy confirmation** - no commit (human checkpoint; answer recorded above)
2. **Task 2: Install pinned packages, pipeline, static guard, LF rules** - `8745669` (feat)

**Plan metadata:** committed after this file (docs: complete plan)

## Files Created/Modified
- `tests/Vdiff.Tests/Vdiff.Tests.csproj` - exact NJsonSchema and YamlDotNet pins
- `tests/Vdiff.Tests/Support/RepoRoot.cs` - `RepoRoot.Find()`, throws `XunitException` rather than skipping
- `tests/Vdiff.Tests/Ci/PipelineYamlTests.cs` - `PipelineProblems` checker and its tests
- `azure-pipelines.yml` - two-leg hosted pipeline, LF
- `.gitattributes` - three new `eol=lf` rules

## Decisions Made
See `key-decisions` above. All are strictenings of the plan's rules, not departures from them.

## Deviations from Plan

None - plan executed exactly as written.

## Verification

- `dotnet test --filter FullyQualifiedName~PipelineYamlTests`: Passed 18, Failed 0.
- Full suite, `--filter "Category!=MermaidCli"`, run after 02-01's last commit: Passed 577, Failed 0, Skipped 0 (559 from 02-01 plus these 18).
- Acceptance greps: YamlDotNet pin 1, NJsonSchema `Version="11.6.2"` 1, shipped csproj files 0 and 0, `ubuntu-24.04` 1, `--locked` 2, `tests/fixtures/** text eol=lf` 1, eol=lf rule lines 10, no tracked bin/obj files.
- `check-public-diff.sh --cached` printed `clean` before the commit.
- Not checkable in Phase 2: running the pipeline on both legs. Record "deferred (D-09)" at phase UAT; decide after the first green Linux run whether the Mermaid step becomes blocking.

## Issues Encountered

None.

## Known Stubs

None.

## Threat Flags

None - the only new surface is the pipeline definition, which T-02-03, T-02-04 and T-02-06 already cover.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Plans 02-05 and 02-07 can use NJsonSchema and YamlDotNet and `RepoRoot.Find()`.
- The LF rules are in place before the first fixture or schema file exists.
- Plan 02-07 must write the report file named by `VDIFF_FIXTURE_REPORT`; the pipeline already sets it and publishes it.

## Self-Check: PASSED

- Created files exist: azure-pipelines.yml, PipelineYamlTests.cs, RepoRoot.cs.
- Commit 8745669 is an ancestor of HEAD.

---
*Phase: 02-model-contract-and-fixture-harness*
*Completed: 2026-11-21*
