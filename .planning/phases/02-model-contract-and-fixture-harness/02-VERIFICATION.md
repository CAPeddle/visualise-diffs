---
phase: 02-model-contract-and-fixture-harness
verified: 2026-11-21T14:00:00Z
status: gaps_found
score: 5/5 roadmap success criteria verified in the codebase (criterion 2 locally only; see human_verification); 1 traceability gap
covered_files:
  - .planning/phases/02-model-contract-and-fixture-harness/02-01-PLAN.md
  - .planning/phases/02-model-contract-and-fixture-harness/02-01-SUMMARY.md
  - .planning/phases/02-model-contract-and-fixture-harness/02-02-PLAN.md
  - .planning/phases/02-model-contract-and-fixture-harness/02-02-SUMMARY.md
  - .planning/phases/02-model-contract-and-fixture-harness/02-03-PLAN.md
  - .planning/phases/02-model-contract-and-fixture-harness/02-03-SUMMARY.md
  - .planning/phases/02-model-contract-and-fixture-harness/02-04-PLAN.md
  - .planning/phases/02-model-contract-and-fixture-harness/02-04-SUMMARY.md
  - .planning/phases/02-model-contract-and-fixture-harness/02-05-PLAN.md
  - .planning/phases/02-model-contract-and-fixture-harness/02-05-SUMMARY.md
  - .planning/phases/02-model-contract-and-fixture-harness/02-06-PLAN.md
  - .planning/phases/02-model-contract-and-fixture-harness/02-06-SUMMARY.md
  - .planning/phases/02-model-contract-and-fixture-harness/02-07-PLAN.md
  - .planning/phases/02-model-contract-and-fixture-harness/02-07-SUMMARY.md
  - .planning/phases/02-model-contract-and-fixture-harness/02-08-PLAN.md
  - .planning/phases/02-model-contract-and-fixture-harness/02-08-SUMMARY.md
  - .planning/phases/02-model-contract-and-fixture-harness/02-09-PLAN.md
  - .planning/phases/02-model-contract-and-fixture-harness/02-09-SUMMARY.md
  - .planning/phases/02-model-contract-and-fixture-harness/02-10-PLAN.md
  - .planning/phases/02-model-contract-and-fixture-harness/02-10-SUMMARY.md
  - .planning/phases/02-model-contract-and-fixture-harness/02-11-PLAN.md
  - .planning/phases/02-model-contract-and-fixture-harness/02-11-SUMMARY.md
  - .planning/phases/02-model-contract-and-fixture-harness/02-12-PLAN.md
  - .planning/phases/02-model-contract-and-fixture-harness/02-12-SUMMARY.md
  - azure-pipelines.yml
  - docs/how-to/private-fixtures.md
  - docs/schema/vdiff-diff-v1.schema.json
  - docs/schema/vdiff-model-v1.schema.json
  - docs/schema/vdiff-run-meta-v1.schema.json
  - docs/trials/private-fixtures.md
  - src/Vdiff.Core/Model/Canonical.cs
  - src/Vdiff.Core/Model/ModelTypes.cs
  - tests/Vdiff.Tests/Contract/CanonicalSerialisationTests.cs
  - tests/Vdiff.Tests/Contract/SchemaFreezeTests.cs
  - tests/Vdiff.Tests/Fixtures/FixtureHarness.cs
  - tests/Vdiff.Tests/Fixtures/PrivateFixtures.cs
  - tests/Vdiff.Tests/Fixtures/Ratchet.cs
covered_digest: "v4:sha256:ab21cb1498c0e3d7466c8dd63b8d529be35e4fc88d3a1a2cce00ae88b88c5bfb"
behavior_unverified: 0
overrides_applied: 0
gaps:
  - truth: "Requirement traceability agrees with the ROADMAP contract: ACC-01 (five synthetic fixtures green in CI) is not checked off until Phase 7"
    status: failed
    reason: "REQUIREMENTS.md line 70 marks ACC-01 [x] and the traceability table (line 109) says Complete, while all ten fixture outputs are RED by design (due phase 5 and 7). ROADMAP.md lines 29-31 and 125 say ACC-01 cannot be checked off until Phase 7; REQUIREMENTS.md's own note (lines 138-139) says it is only satisfied once all five fixtures are green. Plans 02-02, 02-04, 02-07, 02-08 and 02-10 each list ACC-01 under requirements-completed, which is where the premature tick came from."
    artifacts:
      - path: ".planning/REQUIREMENTS.md"
        issue: "ACC-01 shown as [x] and Complete; the requirement text 'green in CI' is not met"
    missing:
      - "Revert ACC-01 to unchecked and set its traceability status to Partial (Phase 2: harness and fixtures delivered, all outputs red; green progressively in Phases 5 to 7; tick at Phase 7)"
      - "Do not tick ACC-01 from any Phase 2 SUMMARY requirements-completed"
human_verification:
  - test: "Connect azure-pipelines.yml to an Azure DevOps organisation and run it once on both legs"
    expected: "Both the windows-2025 and ubuntu-24.04 legs build, run dotnet test with Category!=MermaidCli, publish the trx results and a fixture-report artifact showing the ten RED rows; the advisory Mermaid step may fail without turning the leg red"
    why_human: "Roadmap criterion 2 says the harness 'runs in CI'. No ADO organisation is connected (D-09, deferred), so the pipeline has only been checked statically by PipelineYamlTests and the same test command locally on Windows. Linux and macOS legs have never run."
  - test: "Run the private-fixture recipe (docs/how-to/private-fixtures.md) once on a real clone inside the target organisation"
    expected: "One '<shape>: PASS|FAIL|PENDING <date>' line per shape, details only under VDIFF_PRIVATE_OUT, then a row added to docs/trials/private-fixtures.md"
    why_human: "Criterion 5 asks for a documented recipe, which exists and is test-covered on a stand-in clone. Nobody has followed it on a real target clone yet; docs/trials/private-fixtures.md is header-only."
  - test: "Read the C# and cpp-cli golden conventions in tests/fixtures/README.md and the csharp and cross-stack goldens"
    expected: "Owner confirms the proposed signature text, the managed-wrapper stereotype, the package-node label and 'a gcroot field to an unmodelled type makes no edge'"
    why_human: "Tests prove the goldens are canonical, schema-valid and parseable, not that these proposed conventions are what Phases 4 to 7 should produce (02-10 'Flagged assumptions')."
  - test: "Judge that the fixtures, goldens, project files and docs hold no target-organisation or target-product name"
    expected: "No target identifier anywhere in tests/fixtures, docs/how-to, docs/trials"
    why_human: "check-public-diff.sh printed clean per the summaries, but the prohibition is judged by reading; a script cannot know every target name."
---

# Phase 2: Model Contract and Fixture Harness Verification Report

**Phase Goal:** Every later phase works against one fixed `vdiff-model/v1` contract and a golden-fixture harness that proves determinism, so each stage can be turned from red to green.
**Verified:** 2026-11-21
**Status:** gaps_found (one documentation-only gap; the code goal is achieved)
**Re-verification:** No, initial verification

## Goal Achievement

The goal is achieved in the codebase. The three schemas are frozen and hash-pinned, the records, serialiser and canonical order agree with them, a seeded-shuffle test proves order independence, and a strict harness drives the real CLI over five committed fixtures and reports all ten outputs RED by design. The one failed item is bookkeeping: ACC-01 is ticked complete although its own text ("green in CI") is not met.

### Observable Truths (ROADMAP success criteria)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | `vdiff-model/v1` defined (types, members, bases, derived edges, spans, extraction metadata); volatile metadata apart from the canonical body; extractor output validates | VERIFIED | `docs/schema/vdiff-model-v1.schema.json` has `types`, `members`, `bases`, `edges` (base and association), `SourceSpan`, `ExtractionInfo`; `ModelFile` and `DiffFile` records in `src/Vdiff.Core/Model` carry no commit id or tool version (grep: those live only in `RunMeta.cs`, written to `run.meta.json`). `Tracer_output_validates_against_the_v1_schemas` and `Same_trees_committed_twice_give_identical_canonical_files_and_differ_only_in_run_meta` passed in my run. Only the C++ extractor exists, so "any extractor" is proven for C++ plus full-sample documents for all three language values; C# and cpp-cli extractors validate when Phase 4 lands them. |
| 2 | Harness compares output with expected `diff.json` and summary for a fixture and runs in CI | VERIFIED locally; CI run UNCERTAIN (human item 1) | `FixtureHarness.RunSynthetic` builds a two-commit repo from the fixture bytes, calls `CliApp.Run`, and byte-compares `diff.json` and `summary.mmd` against `expected/` through `Ratchet.Evaluate`. 298 tests in the Contract, Fixtures and Ci namespaces passed. `azure-pipelines.yml` (two legs, `--locked` cargo, `dotnet test --filter Category!=MermaidCli`, `VDIFF_FIXTURE_REPORT`, artifact publish) passes `PipelineYamlTests`, including 14 negative rows. It has never run on Azure DevOps (D-09, no organisation connected). |
| 3 | Five synthetic fixtures in the repo; CI harness reports each failing as intended | VERIFIED | `tests/fixtures` holds exactly small-cpp, csharp, cross-stack, extract-class, rename (`All_five_shapes_have_a_fixture`, passed). I ran `CommittedFixtureTests` with `VDIFF_FIXTURE_REPORT`: ten rows, all RED (`pending, due phase 5` for diff, `due phase 7` for summary), no FAIL or GREEN. I read `rename/expected/diff.json`: substantive (score_milli 750 = 0.5x0.5 + 0.2 + 0.2 + 0.1, member rename, base edge), not a stub. |
| 4 | Same model serialised twice with collections in any order gives byte-identical JSON | VERIFIED | `CanonicalSerialisationTests` (29 tests, passed): seeded Fisher-Yates over every list of full model, diff and run-meta samples for seeds 1 to 25, byte comparison through `OutputWriter.Serialize`, plus a reflection guard that fails if any reachable list was never permuted, plus builder and differ order tests and a content tie-break test. `OutputWriter.Serialize` calls `Canonical.Canonicalise` for all three record types (grep). This is a behavioural test, so the truth is behaviourally proven, not just present. |
| 5 | Documented private-fixture recipe lets five real PRs be checked inside the target organisation without content entering the repo | VERIFIED (recipe and guards); real run UNCERTAIN (human item 2) | `docs/how-to/private-fixtures.md` covers fixture format, env vars, console grammar, detail files, every refusal, recording. `PrivateFixtures.cs` reuses `CliArguments`, `RunCli`, `Evaluate`; refusal, leak and grammar tests passed (PrivateFixtureTests in the 298). `docs/trials/private-fixtures.md` is header-only, as intended, and a test pins its grammar. |

**Score:** 5/5 roadmap criteria verified in the codebase; 0 behavior-unverified. Gap count 1 (traceability). Human items 4.

### Plan-level must-haves spot-checked

| Claim (SUMMARY) | Result |
|-----------------|--------|
| Schemas frozen and SHA-256 pinned | Confirmed independently: `tr -d '\r' | sha256sum` of the three files gives c62ea541..., 1bb22e1d..., 47964d1d..., matching `SchemaFreezeTests` pins. Freeze answer "approved 2026-11-21" is recorded in 02-06-SUMMARY (owner answer, not verifiable from code). |
| Harness cannot end green in accept mode, refuses CI | `Apply_*` and `RunAll_in_accept_mode_*` tests in FixtureHarnessTests passed. |
| Pending output that unexpectedly matches is FAIL | `Ratchet.Evaluate` line 56; `A_green_fixture_marked_pending_is_FAIL_because_it_unexpectedly_matches` passed. |
| 901 tests green on the full suite | Not rerun (22 min); I had no reason to doubt it. My targeted subset (Contract, Fixtures, Ci: 298; CommittedFixtureTests plus the run-meta separation test: 4) passed with 0 failures. |

### Required Artifacts

| Artifact | Status | Details |
|----------|--------|---------|
| `docs/schema/*.json` (3) | VERIFIED | exist, substantive, hash-pinned, validated against by tests and harness |
| `docs/schema/README.md` | VERIFIED (with IN-05 wording defect) | status "frozen", Identity grammar present; still carries an "Open item for the freeze review" paragraph |
| `src/Vdiff.Core/Model/Canonical.cs`, `ModelTypes.cs`, `RunMeta.cs` | VERIFIED | wired through `OutputWriter.Serialize`, `ModelBuilder`, `ModelDiffer`, `TracerPipeline` |
| `tests/Vdiff.Tests/Fixtures/*` (loader, harness, ratchet, accept, report, private) | VERIFIED | wired: `CommittedFixtureTests` calls `FixtureHarness.RunAll` over `tests/fixtures` |
| `tests/fixtures/<five shapes>` with goldens | VERIFIED | 48 tracked files; each shape has `fixture.yml`, base/head trees, `expected/diff.json`, `expected/summary.mmd` |
| `azure-pipelines.yml` | VERIFIED statically | see human item 1 |
| `docs/how-to/private-fixtures.md`, `docs/trials/private-fixtures.md` | VERIFIED | |

### Key Link Verification

| From | To | Status | Details |
|------|----|--------|---------|
| `FixtureHarness.RunCli` | `CliApp.Run` | WIRED | line 98; the harness tests the product entry point, not a copy |
| `FixtureHarness.Evaluate` | `Ratchet.Evaluate`, `SchemaSupport.ValidateOutputs` | WIRED | lines 112, 124 |
| `OutputWriter.Serialize` | `Canonical.Canonicalise` | WIRED | model, diff and meta each |
| `PrivateFixtures.RunShape` | `FixtureHarness.CliArguments/RunCli/Evaluate`, `VDIFF_PROTECTED_DIRS` | WIRED | per 02-09 and `PrivateFixtureTests` |
| `azure-pipelines.yml` test step | `VDIFF_FIXTURE_REPORT` / `FixtureReport.WriteIfRequested` | WIRED | env var set in the step; `PipelineYamlTests` requires the `Category!=MermaidCli` filter on that step |

### Data-Flow Trace (Level 4)

Report rows come from real `Evaluate` outcomes over a real CLI run (not hardcoded): `FixtureReport.Render` over `outcomes.Select(o => o.Row)`. FLOWING. The RED status is produced by a real byte mismatch (`Matched` false), and the csharp rows are RED because today's tracer emits an empty C# diff, which is the expected state.

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Contract, Fixtures, Ci test namespaces | `dotnet test --filter "FullyQualifiedName~Vdiff.Tests.Contract|...Fixtures|...Ci"` | Passed 298, Failed 0 (2 m 20 s) | PASS |
| Ten RED rows | `VDIFF_FIXTURE_REPORT=... dotnet test --filter CommittedFixtureTests` | 10 RED, 0 FAIL, 0 GREEN; 4 tests passed | PASS |
| Canonical-vs-volatile separation | `Same_trees_committed_twice_give_identical_canonical_files_and_differ_only_in_run_meta` | passed | PASS |
| Schema pins | independent sha256 of the three files | match the constants | PASS |

### Probe Execution

Step 7c: SKIPPED. The phase declares no `probe-*.sh` scripts; verification is by xUnit.

### Requirements Coverage

| Requirement | Source Plans | Description | Status | Evidence |
|-------------|--------------|-------------|--------|----------|
| R-10 | 02-03, 02-05, 02-06, 02-12 | Every extractor emits one versioned model `vdiff-model/v1` | SATISFIED | schema, records, validation tests, freeze |
| R-23 | 02-01, 02-03, 02-06, 02-11, 02-12 | Byte-identical outputs; canonical ordering; volatile metadata apart | SATISFIED | criterion 1 and 4 evidence |
| ACC-01 | 02-02, 02-04, 02-07, 02-08, 02-10 | Five synthetic fixtures green in CI | NOT SATISFIED, PARTIAL (see below) | harness and fixtures delivered, all ten outputs RED |
| ACC-02 | 02-09 | Private-fixture recipe documented | SATISFIED (documentation); first real run pending | criterion 5 |

All four phase requirement IDs appear in plan frontmatter. No orphaned IDs: REQUIREMENTS.md maps exactly R-10, R-23, ACC-01, ACC-02 to Phase 2.

**ACC-01 judgement.** Marking it complete now is incorrect. The requirement text is "Five synthetic public fixtures green in CI". Phase 2 owns the harness and the fixtures, which it delivered red (ROADMAP criterion 3 asks for exactly that). ROADMAP's coverage note says ACC-01 "cannot be checked off until Phase 7", REQUIREMENTS.md's own note agrees, and the report above shows ten RED rows and no GREEN. The tick in REQUIREMENTS.md (line 70 and traceability line 109) contradicts both documents. The phase goal itself ("so each stage can be turned from red to green") does not need ACC-01 satisfied, so this is a gap in tracking, not in the code.

### Anti-Patterns Found

No debt markers (`TBD`, `FIXME`, `XXX`) or stub implementations found in the phase's core files. Review (02-REVIEW.md: 0 critical, 5 warning, 7 info; all 12 `open` in 02-REVIEW-DISPOSITION.md) considered against the must-haves:

| Finding | Defeats a must-have? | Notes |
|---------|----------------------|-------|
| WR-01 MermaidWriter base resolution drifted from `BaseResolver` | No | Writer behaviour, Phase 7 territory. The committed csharp golden summary expects a `Document` base edge the writer's `::`-only rule would not draw, but that output is pending and RED (due 7), so no current conflict. Should be fixed or tracked before Phase 7 turns summaries green. |
| WR-02 removed bases drawn as live edges | No | Same area. Fix needs a README rule decision first. |
| WR-03 internal, protected-internal, private-protected drawn as `+` | No | Latent until the C# extractor (Phase 4). |
| WR-04 malformed output file throws out of the harness instead of a FAIL row | No, but it weakens criterion 2/3 resilience | An output that is not JSON aborts `RunAll` without a report row. Real runs produce valid JSON today. Worth fixing before the pipeline is connected. |
| WR-05 private mode leaves stale `diff.diff`, `cli-stderr.txt`, `error.txt` | No, touches criterion 5 | The how-to tells maintainers to read those files for the current reason; stale content could mislead and keeps target content on disk longer. Fix before the first real private run. |
| IN-05 README "open item" paragraph and `SourceSpan` "counted from 1" vs `minimum: 0` | No | Documentation inside a frozen file; fixing the schema description changes the pinned hash. Decide before extractors depend on it. |

Dispositions are still `open`; none is triaged. I count none as a blocker to the phase goal.

### Human Verification Required

1. **Run the pipeline on Azure DevOps.** Test: connect `azure-pipelines.yml`, run both legs. Expected: both legs green, fixture-report artifact shows ten RED rows. Why human: no organisation connected (D-09); Linux and macOS legs have never executed.
2. **Run the private-fixture recipe on a real clone** in the target organisation and record one row. Why human: only stand-in clones tested; the record file is header-only.
3. **Confirm proposed C# and cpp-cli golden conventions** (signature text, managed-wrapper stereotype, package-node label, gcroot edge rule). Why human: tests prove form, not intent.
4. **Judge that no target identifier is in tracked fixtures or docs.** Why human: a script cannot know every target name.

### Gaps Summary

One gap, documentation only: `REQUIREMENTS.md` ticks ACC-01 and marks it Complete although the ten fixture outputs are RED and every planning document says it cannot be checked off before Phase 7. Revert the tick, mark ACC-01 Partial in the traceability table, and leave the Phase 2 ROADMAP checkbox to the orchestrator once this is done. No code change is needed. The remaining items are human or deferred, not silent passes: CI run on a connected pipeline (including Linux), a real private run, owner confirmation of proposed golden conventions, and the identifier read. The five review warnings are open and should be triaged (WR-04 and WR-05 before the pipeline connects and the first private run; WR-01 to WR-03 before Phase 7).

---

_Verified: 2026-11-21_
_Verifier: Claude (gsd-verifier)_
