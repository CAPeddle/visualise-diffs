---
phase: 02-model-contract-and-fixture-harness
verified: 2026-11-24T18:00:00Z
status: passed
score: 5/5 roadmap success criteria verified in the codebase (criterion 2 locally only, criterion 5 recipe and one PENDING-only run)
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
  - docs/schema/README.md
  - docs/schema/vdiff-diff-v1.schema.json
  - docs/schema/vdiff-model-v1.schema.json
  - docs/schema/vdiff-run-meta-v1.schema.json
  - docs/trials/private-fixtures.md
  - src/Vdiff.Core/Model/Canonical.cs
  - src/Vdiff.Core/Model/ModelTypes.cs
  - src/Vdiff.Core/Model/RunMeta.cs
  - tests/Vdiff.Tests/Contract/CanonicalSerialisationTests.cs
  - tests/Vdiff.Tests/Contract/DiffPresence.cs
  - tests/Vdiff.Tests/Contract/DiffPresenceTests.cs
  - tests/Vdiff.Tests/Contract/SchemaFreezeTests.cs
  - tests/Vdiff.Tests/Fixtures/FixtureHarness.cs
  - tests/Vdiff.Tests/Fixtures/PrivateFixtures.cs
  - tests/Vdiff.Tests/Fixtures/Ratchet.cs
  - tests/fixtures/README.md
covered_digest: "v4:sha256:a5aaf64d3e3ff99038ca25770f635a2d2fdf9b9aba7d1323115428e5c3a9745b"
behavior_unverified: 0
overrides_applied: 0
re_verification:
  previous_status: human_needed
  previous_score: 5/5 roadmap criteria, previous traceability gap closed
  gaps_closed: []
  gaps_remaining: []
  regressions: []
human_verification:
  - test: "Connect azure-pipelines.yml to an Azure DevOps organisation and run it once on both legs"
    expected: "Both the windows-2025 and ubuntu-24.04 legs build, run dotnet test with Category!=MermaidCli, publish the trx results and a fixture-report artifact showing the ten RED rows; the advisory Mermaid step may fail without turning the leg red"
    why_human: "Roadmap criterion 2 says the harness 'runs in CI'. No ADO organisation is connected (D-09). 02-UAT.md test 1 reads pass but its own note says the owner accepted it without a connected run. The pipeline has only been checked statically (PipelineYamlTests) and by the same test command on Windows. The Linux and macOS legs, and the Linux fsmonitor positive control (02-VALIDATION row 999.1 (1)), have never run. The owner has accepted this deferral; that acceptance is recorded but is not a run."
  - test: "Replace the agent-picked private fixtures with owner-reviewed ones and record a real PASS or FAIL for at least one shape"
    expected: "docs/trials/private-fixtures.md shows a PASS or FAIL row for a shape whose expected files the owner reviewed; the extract-class pick is replaced"
    why_human: "Criterion 5 asks for a documented recipe. The recipe exists and was run once on a real clone: five PENDING rows dated 2026-11-23, details outside the repository. The owner's UAT note calls this provisional: fixtures were picked by an agent with placeholder expected files, not owner-reviewed, and the extract-class pick should be replaced. PENDING proves the recipe runs and leaks nothing, not that a real PR has been checked against a reviewed expectation."
---

# Phase 2: Model Contract and Fixture Harness Verification Report

**Phase Goal:** Every later phase works against one fixed `vdiff-model/v1` contract and a golden-fixture harness that proves determinism, so each stage can be turned from red to green.
**Verified:** 2026-11-24
**Status:** passed on owner acceptance (verifier verdict: human_needed, no gaps)

**Owner acceptance, 2026-11-24:** the owner chose to close Phase 2 with both human items accepted but **not run**: the pipeline has never run on Azure DevOps, and no real PR has been checked against reviewed expected files. Both stay as follow-ups. The verifier's text below is unchanged and still describes them as open.
**Re-verification:** Yes. The previous report (2026-11-22) was stale: `docs/trials/private-fixtures.md` changed, `DiffPresenceTests` gained 33 rows, and the planning documents (UAT, SECURITY, VALIDATION, REVIEW, REVIEW-DISPOSITION) were added or rewritten. Git history was rewritten for author emails only, so hashes in older text differ from the history.

## Re-verification scope

What changed in `src`, `tests`, `docs`, `azure-pipelines.yml`, `extractors` and `.gitattributes` between 43a9501 (the previous report) and HEAD 510c098: only `docs/trials/private-fixtures.md` (five PENDING rows) and `tests/Vdiff.Tests/Contract/DiffPresenceTests.cs` (+77 lines, test-only). No product source changed.

Checked today, not taken from any summary:

| Check | Result |
|-------|--------|
| `dotnet test ... --filter "Category=Contract\|Category=Fixtures"` (Release) | 330 passed, 0 failed |
| `DiffPresenceTests` / `SchemaValidationTests` / `SchemaParityTests` run alone | 58 / 24 / 4 passed (previous record 25 / 23 / 4; the floor of 49 would have hidden a drop, the counts did not drop) |
| `CommittedFixtureTests` with `VDIFF_FIXTURE_REPORT` set | 3 passed; the report holds ten rows, all RED (diff due phase 5, summary due phase 7), no FAIL, no GREEN |
| Schema pins | SHA-256 (CR stripped) of the three schema files starts c62ea541, 1bb22e1d, 47964d1d, equal to the constants in `SchemaFreezeTests` lines 15 to 17 |
| `git ls-files tests/fixtures` | 48 files; five shapes (small-cpp, csharp, cross-stack, extract-class, rename) plus README |
| Caller's regression logs, read not rerun | `full.log`: 934 passed, 0 failed (10 m 45 s); `cargo1.log`: 105 passed, 0 failed. I did not repeat the full suite. They were produced at the test-only state later committed as 27f03f8, which is the only change under `tests/` since the previous report. |
| Target-name scan, `git grep -i` for the two target identifiers over every tracked file | 0 hits outside `.planning`, 0 hits in `.planning` |
| Debt markers (`TBD`, `FIXME`, `XXX`) in `src`, `tests`, `docs`, `azure-pipelines.yml` | one hit, `PrivateFixtureTests.cs:553`, which is a test row that must be rejected as an invalid status. Not a debt marker. |

## Goal Achievement

The goal is achieved in the codebase. Three schemas are frozen and hash-pinned, the records and the canonical serialiser agree with them, a seeded-shuffle test proves order independence, and a strict harness drives the real CLI over five committed fixtures and reports all ten outputs RED by design.

### Observable Truths (ROADMAP success criteria)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | `vdiff-model/v1` defined (types, members, bases, derived edges, spans, extraction metadata); volatile metadata apart from the canonical body; extractor output validates | VERIFIED | The three schema files exist and are pinned; `SchemaParityTests` (4), `SchemaValidationTests` (24) and `ModelPresence`/`DiffPresence` checkers pass in the Contract run. Commit ids and tool versions live in `RunMeta.cs` and `run.meta.json`, not in `ModelFile` or `DiffFile`. Only the C++ extractor exists, so "any extractor" is proven for the tracer plus full-sample documents for all three language values; the C# and cpp-cli extractors (Phase 4) must validate when they land. |
| 2 | Harness compares output with expected `diff.json` and summary and runs in CI | VERIFIED locally; CI run UNCERTAIN (human item 1) | `FixtureHarness.RunSynthetic` builds a two-commit repo and calls `CliApp.Run`; `Ratchet.Evaluate` byte-compares `diff.json` and `summary.mmd`. `azure-pipelines.yml` passes `PipelineYamlTests` statically. It has never run on Azure DevOps. |
| 3 | Five synthetic fixtures in the repo; CI harness reports each failing as intended | VERIFIED | Five shapes, ten RED rows from a real CLI run today, 0 FAIL, 0 GREEN. |
| 4 | Same model serialised twice with collections in any order gives byte-identical JSON | VERIFIED (behavioural) | `CanonicalSerialisationTests` ran and passed in the Contract filter: seeded shuffles of every list, byte comparison through `OutputWriter.Serialize`, reflection guard over list properties. A behaviour-dependent truth with a passing test, so not merely present. |
| 5 | Documented private-fixture recipe lets five real PRs be checked inside the target organisation without content entering the repo | VERIFIED (recipe, guards, one real run to PENDING); real PASS/FAIL UNCERTAIN (human item 2) | `docs/how-to/private-fixtures.md` and `PrivateFixtures.cs` with refusal, leak and grammar tests (`PrivateFixtureTests` in the Fixtures run). `docs/trials/private-fixtures.md` now holds five PENDING rows dated 2026-11-23, shape, status and date only. |

**Score:** 5/5 roadmap criteria verified in the codebase; 0 behavior-unverified; 0 gaps. Human items: 2.

### Judgement (a): owner-accepted UAT items

I applied my own rules, not the UAT status. 02-UAT.md records four passes and no issues. Two of the four rest on less than the expected text says:

- **Test 1 (pipeline).** Result `pass`, but the note says "Owner-accepted 2026-11-23 without a connected Azure DevOps run". `02-SECURITY.md` N-1 flagged the same mismatch. Roadmap criterion 2 reads "runs in CI". An external-service behaviour that has never run falls under "always needs human" in the verification rules. Never run: both hosted legs, the Linux and macOS legs, the Linux fsmonitor positive control, the pipeline's advisory Mermaid step, and the fixture-report artifact publication. What does exist is static (`PipelineYamlTests`), the same `dotnet test` filter run locally on Windows, and the offline demo run.
- **Test 2 (private fixtures).** Result `pass`, note "Provisional". The recipe ran once on a real clone and produced five PENDING lines with details kept outside the repository, which shows the recipe works and leaks nothing. It does not show a real PR checked against an expectation anyone reviewed: the fixtures were picked by an agent, expected files are placeholders, and the owner wants the extract-class pick replaced. The record file therefore holds no PASS or FAIL.

Tests 3 and 4 are resolved. Test 3 is an owner opinion on proposed conventions. Test 4 was an owner scan; my own `git grep` found no target identifier in any tracked file. A script cannot prove the absence of every target name, so test 4 is the owner's judgement, which the UAT records.

Status choice: `human_needed`, not `passed`. An owner's acceptance is a decision about what to proceed on; it does not change what has been observed. Step 9 allows `passed` only when the human-verification list is empty, and both items above are unrun checks for which the criterion's own wording is the benchmark. Not `gaps_found` either: nothing is missing or broken in the code, and the owner has knowingly deferred both. There is no `overrides:` entry in this report, because no override was written with the owner's name and an accepted-at time against these two must-haves. If the owner wants `passed`, adding an override for each of the two (criterion 2 and criterion 5 wording, accepted_by, accepted_at, reason) is the explicit way, and it would carry the deferral into the record rather than hide it.

### Judgement (b): `base_file` and `head_file` absence

**Conclusion: an observation, not a gap against any must-have.**

Evidence:

- `tests/Vdiff.Tests/Contract/DiffPresence.cs:35-43` fails a removed, modified or renamed type with no `base_file`, and an added, modified or renamed type with no `head_file`. It never fails a type that carries a file for a side that does not exist. An `added` type with a `base_file`, or a `removed` type with a `head_file`, produces no problem.
- The wording that the checker implements is "present when", not "present exactly". `docs/schema/README.md:58-59` says "`base_file` is present when a type exists on the base side ... and `head_file` when it exists on the head side". The `TypeDiff` description in `docs/schema/vdiff-diff-v1.schema.json` (line 192) and the plan task text (`02-05-PLAN.md` line 270, "the file rule as 02-03 states it ... present when the base side exists") use the same one-directional wording. The sibling rules in the same list say "exactly" (member before/after fields, `renamed_from`, package versions, `via`) and are enforced both ways. So the checker matches the documented rule; the rule is the weaker of the two.
- No roadmap criterion, requirement or plan `must_haves` truth states absence. The 02-05 truth and the 02-VALIDATION row both say "`base_file`/`head_file` per state", which is ambiguous but is pinned by the task text above.
- The five goldens all conform: every `base_file`/`head_file` in `tests/fixtures/*/expected/diff.json` sits on a side that exists.
- Nothing in `src` writes either field yet (`ModelDiffer` fills no type diffs until Phase 5), so no producer can violate absence today.

Why it still deserves a line in the backlog: Phase 5 is the first producer, and the schema is frozen. Absence can be enforced test-side without touching the schema hash: add two rows to `DiffPresence` and `DiffPresenceTests` (`added` with `base_file`, `removed` with `head_file`), then change "present when" to "present exactly when" in README line 58-59 (the README is not hash-pinned; the schema description at line 192 is, so leave it). If the owner prefers the weaker rule, say so in the README ("absence is not checked"). Either choice costs less before Phase 5 than after.

### Required Artifacts

| Artifact | Status | Details |
|----------|--------|---------|
| `docs/schema/*.json` (3) | VERIFIED | exist, substantive, hash pins matched today |
| `src/Vdiff.Core/Model/Canonical.cs`, `ModelTypes.cs`, `RunMeta.cs` | VERIFIED | used by `OutputWriter.Serialize`, `ModelBuilder`, `ModelDiffer`, `TracerPipeline`; exercised by the passing Contract tests |
| `tests/Vdiff.Tests/Contract/DiffPresence.cs`, `DiffPresenceTests.cs` | VERIFIED | checker implements the documented rules; 58 test rows pass, one sole-problem row per rule field and direction |
| `tests/Vdiff.Tests/Fixtures/*` | VERIFIED | `CommittedFixtureTests` calls `FixtureHarness.RunAll` over `tests/fixtures` |
| `tests/fixtures/<five shapes>` with goldens | VERIFIED | 48 tracked files |
| `azure-pipelines.yml` | VERIFIED statically | see human item 1 |
| `docs/how-to/private-fixtures.md`, `docs/trials/private-fixtures.md` | VERIFIED | recipe present; record file holds five PENDING rows in the allowed grammar (guarded by `PrivateFixtureTests`) |
| `.planning/REQUIREMENTS.md` traceability | VERIFIED | R-10, R-23, ACC-02 `[x]` and Complete (lines 28, 29, 71, 107, 108, 110); ACC-01 `[ ]` and Partial (lines 70, 109) with the note at 138 to 143 |

### Key Link Verification

| From | To | Status | Details |
|------|----|--------|---------|
| `FixtureHarness.RunCli` | `CliApp.Run` | WIRED | harness drives the product entry point |
| `FixtureHarness.Evaluate` | `Ratchet.Evaluate`, `SchemaSupport.ValidateOutputs` | WIRED | |
| `OutputWriter.Serialize` | `Canonical.Canonicalise` | WIRED | model, diff and run meta each |
| `PrivateFixtures.RunShape` | `FixtureHarness.CliArguments/RunCli/Evaluate` | WIRED | `PrivateFixtureTests` |
| `azure-pipelines.yml` test step | `VDIFF_FIXTURE_REPORT` / `FixtureReport.WriteIfRequested` | WIRED | report file produced today with the same variable |

### Data-Flow Trace (Level 4)

Report rows come from real `Evaluate` outcomes over a real CLI run, not hardcoded literals; today's ten RED rows came from a fresh run. FLOWING.

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Contract and fixtures suites | `dotnet test ... --filter "Category=Contract\|Category=Fixtures"` | 330 passed, 0 failed | PASS |
| Ten RED rows | `CommittedFixtureTests` with `VDIFF_FIXTURE_REPORT` | ten RED, 0 FAIL, 0 GREEN | PASS |
| Schema freeze | sha256 of three schema files vs `SchemaFreezeTests` | match | PASS |
| Per-class counts | `--filter FullyQualifiedName~<class>` | DiffPresence 58, SchemaValidation 24, SchemaParity 4 | PASS |
| Full workspace suites | not rerun; caller's logs | 934/934 .NET, 105/105 cargo | CARRIED FORWARD (read from logs) |

### Probe Execution

Step 7c: SKIPPED. The phase declares no `probe-*.sh` scripts; verification is by xUnit.

### Requirements Coverage

| Requirement | Source Plans | Description | Status | Evidence |
|-------------|--------------|-------------|--------|----------|
| R-10 | 02-03, 02-05, 02-06, 02-12, 02-13 | Every extractor emits one versioned model `vdiff-model/v1` | SATISFIED | schema, records, validation and presence tests, freeze pins; REQUIREMENTS.md `[x]` Complete |
| R-23 | 02-01, 02-03, 02-06, 02-11, 02-12, 02-13 | Byte-identical outputs; canonical ordering; volatile metadata apart | SATISFIED | criteria 1 and 4; `CanonicalSerialisationTests` pass; REQUIREMENTS.md `[x]` Complete |
| ACC-01 | 02-02, 02-04, 02-07, 02-08, 02-10, 02-13 | Five synthetic fixtures green in CI | PARTIAL BY DESIGN: Phase 2 obligation (harness and five red fixtures) met; requirement ticks at Phase 7 | ten RED rows today; REQUIREMENTS.md `[ ]` Partial; ROADMAP coverage note and Phase 7 criterion |
| ACC-02 | 02-09, 02-13 | Private-fixture recipe documented | SATISFIED (documentation); real PASS/FAIL pending (human item 2) | criterion 5; REQUIREMENTS.md `[x]` Complete |

All four Phase 2 requirement IDs (R-10, R-23, ACC-01, ACC-02) appear in plan frontmatter (02-01 to 02-13) and are accounted for in REQUIREMENTS.md. No orphans: REQUIREMENTS.md maps exactly these four to Phase 2. ACC-01 is not a gap, because its non-Complete status is the ROADMAP contract ("owned here but only reaches green in later phases"; Phase 7 completes it) and the tracking records that.

### Anti-Patterns Found

No stubs or debt markers in the phase's files. The code review (`02-REVIEW.md`, 0 critical, 6 warning, 9 info) has every finding at disposition `open` in `02-REVIEW-DISPOSITION.md` (15 open of 15; none triaged). None defeats a must-have:

| Finding | Defeats a must-have? | Notes |
|---------|----------------------|-------|
| WR-01, WR-02 MermaidWriter base resolution and removed bases drawn as live edges | No | Summaries are RED and due in Phase 7; fix or track before Phase 7 turns them green |
| WR-03 member visibility collapses `internal`, `protected-internal`, `private-protected` to `+` | No | Latent until the C# extractor (Phase 4) |
| WR-04 malformed output aborts `RunAll` instead of giving a FAIL row | No | Touches the harness's promise under criterion 3 for a failure case that no extractor can produce today; fix before the pipeline connects |
| WR-05 private mode leaves stale diagnostic files | No | Fix before the next private run, which will be the first owner-reviewed one (human item 2) |
| WR-06 `--blank` check accepts a trailing line feed | No | Input-validation gap, outside the Phase 2 goal |
| IN-05 README open item; `SourceSpan` "counted from 1" vs `minimum: 0` | No | Fixing the schema description changes the pinned hash; decide before extractors depend on it |
| IN-01 to IN-04, IN-06 to IN-09 | No | Documentation, duplication and test hygiene |

### Document consistency (informational)

- `.planning/ROADMAP.md` line 124 still reads "13/13 plans executed in 8 waves; gap-closure plan 02-13 pending", while line 157 shows 02-13 `[x]` and its SUMMARY exists. The Phase 2 checkbox at line 44 is still `[ ]`. Both are for the orchestrator to settle after the human items are decided.
- `02-SECURITY.md` N-1 (UAT test 1 pass without a run reference) is covered by judgement (a) above.
- `02-VALIDATION.md` says `nyquist_compliant: false`: the quick run measured 56 s against a 30 s target, and the Linux, macOS, hosted-pipeline and reviewed-private-fixture checks are unrun. This agrees with the two human items.
- Deferred follow-ups in `02-UAT.md` (partial-clone base files, Phase 3; `std::function` data member and `[modified]` tags in the summary, Phase 7) belong to later phases and are not gaps here.

### Human Verification Required

1. **Run the pipeline on Azure DevOps.** Connect `azure-pipelines.yml`, run both legs. Expected: both build and test, fixture-report artifact shows ten RED rows, the advisory Mermaid step may fail without turning the leg red. Why human: no organisation is connected (D-09); owner-accepted without a run on 2026-11-23, the first connected run is still to happen; Linux and macOS never executed.
2. **Replace the agent-picked private fixtures and record a real result.** Owner reviews expected files, the extract-class pick is replaced, the recipe is rerun, and a PASS or FAIL row replaces a PENDING row. Why human: the one real run produced five PENDING rows from agent-picked fixtures with placeholder expected files; the owner marked it provisional.

### Gaps Summary

None. Every roadmap criterion has code, tests and wiring that I checked today, and the three schema hashes, the ten RED rows and 330 contract and fixture tests agree with the claims. The status is `human_needed` because two criteria (2, "runs in CI", and 5, five real PRs) depend on an environment or an owner-reviewed run that does not exist yet; the owner has accepted that deferral, and I have recorded it without turning it into a pass. The `base_file`/`head_file` absence point is an observation with a cheap, test-side remedy to schedule before Phase 5.

---

_Verified: 2026-11-24_
_Verifier: Claude (gsd-verifier)_
