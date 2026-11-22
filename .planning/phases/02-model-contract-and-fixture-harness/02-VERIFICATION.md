---
phase: 02-model-contract-and-fixture-harness
verified: 2026-11-22T10:00:00Z
status: human_needed
score: 5/5 roadmap success criteria verified in the codebase (criterion 2 locally only; criterion 5 recipe only); previous traceability gap closed
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
  - .planning/phases/02-model-contract-and-fixture-harness/02-13-PLAN.md
  - .planning/phases/02-model-contract-and-fixture-harness/02-13-SUMMARY.md
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
covered_digest: "v4:sha256:ac78a0ac1a7832cacfffef159d87609b097a7ad89e6c268c03b09d8f63d6d8bc"
behavior_unverified: 0
overrides_applied: 0
re_verification:
  previous_status: gaps_found
  previous_score: 5/5 roadmap criteria, 1 traceability gap
  gaps_closed:
    - "Requirement traceability agrees with the ROADMAP contract: ACC-01 is not checked off until Phase 7"
  gaps_remaining: []
  regressions: []
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
**Verified:** 2026-11-22
**Status:** human_needed (no gaps; four items need a human or an environment this repository does not have yet)
**Re-verification:** Yes, after gap-closure plan 02-13

## Re-verification scope

The previous report (2026-11-21) found one gap: requirement tracking. REQUIREMENTS.md ticked ACC-01 Complete although all ten fixture outputs are red by design, and a revert had left R-10, R-23 and ACC-02 unticked. Plan 02-13 landed in commits 6d7a123, ca7f75b and 583cde9.

**No code changed.** `git log --since=2026-11-21T14:00:00Z -- src tests docs azure-pipelines.yml` is empty, and `git diff --stat 583cde9~3 HEAD` touches only `.planning/` (REQUIREMENTS, ROADMAP, STATE, state.json, five SUMMARYs, the new 02-13-SUMMARY). I re-checked the three schema SHA-256 prefixes (c62ea541, 1bb22e1d, 47964d1d): they still match the pins in `SchemaFreezeTests`. `tests/fixtures` still holds the five shapes, 48 tracked files. I did not rerun the test suites; the earlier results (298 tests in the Contract, Fixtures and Ci namespaces, 10 RED rows, 0 FAIL, 0 GREEN) stand because the files they cover are unchanged. The `covered_digest` differs from the previous one only because 02-13-PLAN.md and 02-13-SUMMARY.md now sit in the phase directory.

## Gap-closure check (the one previous gap)

| Check | Evidence | Result |
|-------|----------|--------|
| R-10, R-23, ACC-02 ticked and Complete | REQUIREMENTS.md lines 28, 29, 71 are `[x]`; traceability lines 107, 108, 110 read `Complete` | CLOSED |
| ACC-01 unticked, Partial | REQUIREMENTS.md line 70 is `[ ]`; line 109 reads `Partial (Phase 2: harness and five fixtures delivered, all outputs red by design; green progressively in Phases 5 to 7; tick at Phase 7)` | CLOSED |
| Note stops the premature tick recurring | REQUIREMENTS.md lines 138 to 143 say ACC-01 stays Partial until Phase 7 and that whoever ticks it in Phase 7 sets row and box directly | CLOSED |
| No Phase 2 SUMMARY claims ACC-01 as completed | `requirements-completed: []` and `requirements-advanced: [ACC-01]` in 02-02, 02-04, 02-07, 02-08, 02-10; the only grep hit for `requirements-completed:.*ACC-01` is prose in 02-13-SUMMARY line 97 describing the change | CLOSED |
| ROADMAP contract unchanged and consistent | ROADMAP lines 29 to 31, 125 and the Phase 7 criterion at 219 ("completes ACC-01") still place the tick at Phase 7; Phase 2 plan list shows 02-13 `[x]` | CONSISTENT |

**Does ACC-01 Partial-by-design satisfy this phase's obligations for that ID?** Yes. My own judgement, not the caller's. The requirement text is "Five synthetic public fixtures green in CI". The ROADMAP assigns the ID to Phase 2 but writes the contract in two places that it is "owned here but only reaches green in later phases" and "cannot be checked off until Phase 7". Phase 2's own success criterion 3 asks the harness to report each fixture as failing as intended. So the obligation Phase 2 owes for ACC-01 is the harness and five committed fixtures delivered red, with the ratchet in place to flip each output green in the commit that makes it match. That exists: ten RED rows from a real CLI run, no GREEN, no FAIL. Treating Partial as "Phase 2 obligation met, requirement not yet complete" matches the ROADMAP; ticking it would contradict the requirement's own words. The one residual risk is mechanical, not a gap: tooling that auto-completes requirements could re-tick ACC-01. The note at REQUIREMENTS.md 140 to 143 records that `requirements mark-complete` and `phase complete` leave a Partial row alone, and 02-13-SUMMARY records a scratch-copy test of that, which I did not rerun.

## Goal Achievement

The goal is achieved in the codebase. The three schemas are frozen and hash-pinned, the records, serialiser and canonical order agree with them, a seeded-shuffle test proves order independence, and a strict harness drives the real CLI over five committed fixtures and reports all ten outputs RED by design.

### Observable Truths (ROADMAP success criteria)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | `vdiff-model/v1` defined (types, members, bases, derived edges, spans, extraction metadata); volatile metadata apart from the canonical body; extractor output validates | VERIFIED | `docs/schema/vdiff-model-v1.schema.json` has `types`, `members`, `bases`, `edges`, `SourceSpan`, `ExtractionInfo`; `ModelFile` and `DiffFile` carry no commit id or tool version (those live in `RunMeta.cs` and `run.meta.json`). Tracer-output-validates and canonical-vs-run-meta separation tests passed in the previous run; the covered files are unchanged. Only the C++ extractor exists, so "any extractor" is proven for C++ plus full-sample documents for all three language values; C# and cpp-cli extractors validate when Phase 4 lands them. |
| 2 | Harness compares output with expected `diff.json` and summary and runs in CI | VERIFIED locally; CI run UNCERTAIN (human item 1) | `FixtureHarness.RunSynthetic` builds a two-commit repo, calls `CliApp.Run`, byte-compares `diff.json` and `summary.mmd` through `Ratchet.Evaluate`. `azure-pipelines.yml` passes `PipelineYamlTests` statically. It has never run on Azure DevOps (D-09). |
| 3 | Five synthetic fixtures in the repo; CI harness reports each failing as intended | VERIFIED | Exactly small-cpp, csharp, cross-stack, extract-class, rename under `tests/fixtures`; ten RED rows (diff due phase 5, summary due phase 7), no FAIL or GREEN. |
| 4 | Same model serialised twice with collections in any order gives byte-identical JSON | VERIFIED | `CanonicalSerialisationTests` (29 tests): seeded Fisher-Yates over every list for seeds 1 to 25, byte comparison through `OutputWriter.Serialize`, reflection guard against unpermuted lists. Behaviourally proven. |
| 5 | Documented private-fixture recipe lets five real PRs be checked inside the target organisation without content entering the repo | VERIFIED (recipe and guards); real run UNCERTAIN (human item 2) | `docs/how-to/private-fixtures.md` and `PrivateFixtures.cs` with refusal, leak and grammar tests; `docs/trials/private-fixtures.md` header-only as intended. |

**Score:** 5/5 roadmap criteria verified in the codebase; 0 behavior-unverified; 0 gaps. Human items 4.

### Required Artifacts

| Artifact | Status | Details |
|----------|--------|---------|
| `docs/schema/*.json` (3) | VERIFIED | exist, substantive, hash pins re-checked today |
| `src/Vdiff.Core/Model/Canonical.cs`, `ModelTypes.cs`, `RunMeta.cs` | VERIFIED | wired through `OutputWriter.Serialize`, `ModelBuilder`, `ModelDiffer`, `TracerPipeline` |
| `tests/Vdiff.Tests/Fixtures/*` | VERIFIED | `CommittedFixtureTests` calls `FixtureHarness.RunAll` over `tests/fixtures` |
| `tests/fixtures/<five shapes>` with goldens | VERIFIED | 48 tracked files, re-counted today |
| `azure-pipelines.yml` | VERIFIED statically | see human item 1 |
| `docs/how-to/private-fixtures.md`, `docs/trials/private-fixtures.md` | VERIFIED | |
| `.planning/REQUIREMENTS.md` traceability | VERIFIED | see gap-closure check |

### Key Link Verification

| From | To | Status | Details |
|------|----|--------|---------|
| `FixtureHarness.RunCli` | `CliApp.Run` | WIRED | harness tests the product entry point |
| `FixtureHarness.Evaluate` | `Ratchet.Evaluate`, `SchemaSupport.ValidateOutputs` | WIRED | |
| `OutputWriter.Serialize` | `Canonical.Canonicalise` | WIRED | model, diff and meta each |
| `PrivateFixtures.RunShape` | `FixtureHarness.CliArguments/RunCli/Evaluate` | WIRED | per 02-09 and `PrivateFixtureTests` |
| `azure-pipelines.yml` test step | `VDIFF_FIXTURE_REPORT` / `FixtureReport.WriteIfRequested` | WIRED | |

### Data-Flow Trace (Level 4)

Report rows come from real `Evaluate` outcomes over a real CLI run, not hardcoded. FLOWING. RED comes from a real byte mismatch.

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| No code change since previous verification | `git log --since=2026-11-21T14:00:00Z -- src tests docs azure-pipelines.yml` | empty | PASS |
| Schema pins | sha256 of the three schema files (CR stripped) vs `SchemaFreezeTests` constants | c62ea541, 1bb22e1d, 47964d1d match | PASS |
| Five fixture shapes | `ls tests/fixtures`, `git ls-files tests/fixtures` | five directories plus README, 48 files | PASS |
| Test suites | not rerun | previous run: 298 passed in Contract, Fixtures, Ci; 4 in committed-fixture and separation tests; 0 failed | CARRIED FORWARD (files unchanged) |

### Probe Execution

Step 7c: SKIPPED. The phase declares no `probe-*.sh` scripts; verification is by xUnit.

### Requirements Coverage

| Requirement | Source Plans | Description | Status | Evidence |
|-------------|--------------|-------------|--------|----------|
| R-10 | 02-03, 02-05, 02-06, 02-12, 02-13 | Every extractor emits one versioned model `vdiff-model/v1` | SATISFIED | schema, records, validation tests, freeze; REQUIREMENTS.md `[x]` Complete |
| R-23 | 02-01, 02-03, 02-06, 02-11, 02-12, 02-13 | Byte-identical outputs; canonical ordering; volatile metadata apart | SATISFIED | criteria 1 and 4; REQUIREMENTS.md `[x]` Complete |
| ACC-01 | 02-02, 02-04, 02-07, 02-08, 02-10, 02-13 | Five synthetic fixtures green in CI | PARTIAL BY DESIGN: Phase 2 obligation (harness and five red fixtures) met; requirement ticks at Phase 7 | REQUIREMENTS.md `[ ]` Partial; ROADMAP coverage note; ten RED rows |
| ACC-02 | 02-09, 02-13 | Private-fixture recipe documented | SATISFIED (documentation); first real run pending (human item 2) | criterion 5; REQUIREMENTS.md `[x]` Complete |

All four phase requirement IDs (R-10, R-23, ACC-01, ACC-02) appear in plan frontmatter and are accounted for in REQUIREMENTS.md. No orphans: REQUIREMENTS.md maps exactly these four to Phase 2. ACC-01 is not a gap because its non-Complete status is the documented ROADMAP contract, and the tracking now says so.

### Anti-Patterns Found

No debt markers (`TBD`, `FIXME`, `XXX`) or stub implementations found in the phase's core files (unchanged since the previous scan). Open code-review dispositions (02-REVIEW.md: 0 critical, 5 warning, 7 info; all `open` in 02-REVIEW-DISPOSITION.md) do not defeat a must-have:

| Finding | Defeats a must-have? | Notes |
|---------|----------------------|-------|
| WR-01, WR-02 MermaidWriter base resolution and removed-base edges | No | Phase 7 territory; summaries are pending and RED. Fix or track before Phase 7 turns summaries green. |
| WR-03 non-public C# accessibilities drawn as `+` | No | Latent until the C# extractor (Phase 4). |
| WR-04 malformed output aborts `RunAll` instead of a FAIL row | No | Worth fixing before the pipeline is connected. |
| WR-05 private mode leaves stale detail files | No | Fix before the first real private run. |
| IN-05 README "open item" paragraph; `SourceSpan` "counted from 1" vs `minimum: 0` | No | Fixing the schema description changes the pinned hash; decide before extractors depend on it. |

### Human Verification Required

1. **Run the pipeline on Azure DevOps.** Connect `azure-pipelines.yml`, run both legs. Expected: both legs build and test, fixture-report artifact shows ten RED rows. Why human: no organisation connected (D-09); Linux and macOS legs have never executed.
2. **Run the private-fixture recipe on a real clone** in the target organisation and record one row. Why human: only stand-in clones tested; the record file is header-only.
3. **Confirm proposed C# and cpp-cli golden conventions** (signature text, managed-wrapper stereotype, package-node label, gcroot edge rule). Why human: tests prove form, not intent.
4. **Judge that no target identifier is in tracked fixtures or docs.** Why human: a script cannot know every target name.

### Gaps Summary

None. The single previous gap (requirement tracking) is closed and verified against REQUIREMENTS.md, ROADMAP.md and the five SUMMARY files. The status is `human_needed` only because the four items above need an Azure DevOps organisation, a real target clone, or an owner's reading; they are not silent passes. The five open review warnings should be triaged (WR-04 and WR-05 before the pipeline connects and the first private run; WR-01 to WR-03 before Phase 7). The Phase 2 checkbox at ROADMAP line 44 is still `[ ]`; ticking it is left to the orchestrator.

---

_Verified: 2026-11-22_
_Verifier: Claude (gsd-verifier)_
