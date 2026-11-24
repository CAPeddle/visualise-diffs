---
phase: "02"
slug: "model-contract-and-fixture-harness"
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
# audit-milestone §5.5 distinguishes NOT-VALIDATED (draft) from PARTIAL (validated + nyquist_compliant: false) (#2117)
status: validated
nyquist_compliant: false
wave_0_complete: true
created: "2026-11-20"
---

# Phase 02 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit 2.9.3, Microsoft.NET.Test.Sdk 17.14.1, xunit.runner.visualstudio 3.1.4 (C#); cargo test (Rust sidecar) |
| **Config file** | none (csproj-driven); `Category` traits select subsets |
| **Quick run command** | `dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj -c Release --filter "Category=Contract\|Category=Fixtures"` |
| **Full suite command** | `dotnet test vdiff.sln -c Release --filter "Category!=MermaidCli"` then `cargo test --locked --manifest-path extractors/cpp-ts/Cargo.toml` |
| **Estimated runtime** | ~30 seconds quick (sidecar built); full suite a few minutes |

New traits: `Contract` (schema, parity, canonical), `Fixtures` (harness, ratchet, golden self-checks), `PrivateFixtures` (private entry point). `MermaidCli` already exists.

---

## Sampling Rate

- **After every task commit:** Run the quick run command
- **After every plan wave:** Run the full suite command
- **Before `/gsd-verify-work`:** Full suite must be green, plus a local harness run showing all five fixtures RED (pending)
- **Max feedback latency:** 30 seconds

---

## Per-Task Verification Map

Filled in by the planner and executor per task. Requirement → test map from RESEARCH.md:

| Requirement | Behavior | Test Type | Automated Command | File Exists | Status |
|-------------|----------|-----------|-------------------|-------------|--------|
| R-10 | Tracer output (base model, head model, diff) validates against the v1 schemas | integration | `dotnet test --filter "FullyQualifiedName~SchemaValidationTests"` | ✅ | ✅ green |
| R-10 | Full-fat record instance validates; one rejected negative document per keyword; allow-listed keywords only (D-15) | unit | `--filter "FullyQualifiedName~SchemaValidationTests"` | ✅ | ✅ green |
| R-10 | Record properties equal schema properties (both directions): model, diff, run meta; the type-level access enum and the `MemberDiff` `access_before`/`access_after` enums equal the member access enum (D-19, D-20) | unit | `--filter "FullyQualifiedName~SchemaParityTests"` | ✅ | ✅ green |
| R-10 | Diff rules the schema subset cannot express: before/after presence per side, `base_file`/`head_file` per state, rename fields only for `renamed`, counts equal listed items, no listed type, member or edge in state `unchanged` (one negative row each, including an unchanged member), integer rename score, `old_version` exactly for `updated`/`removed` and `new_version` exactly for `updated`/`added`, `via` exactly on association edges (one sole-problem row per field and direction), and the D-23 edge rules: `to_before`/`to_after` only on a `modified` edge, at least one present, never equal, no `to`, no shared identity (one sole-problem row each; a `modified` edge with one side unresolved is clean); `FullDiff` holds a modified edge with both fields, every list in it has at least two distinct elements out of canonical order, and `TypeDiff` has no type-level change field (D-20) | unit | `--filter "FullyQualifiedName~DiffPresenceTests"` | ✅ | ✅ green |
| R-10 | Model type rules the schema subset cannot express (D-19): `files` present exactly when `partial`, in ordinal order, without repeats, `file` equal to `files[0]`, no type-level access `none` (one sole-problem negative row each); `via` exactly on association edges (one sole-problem row in each direction); the canonical `FullModel`, the `EmptyModel` and both models of a real tracer run report no problems | unit + integration | `--filter "FullyQualifiedName~ModelPresenceTests"` (at least 10); `--filter "FullyQualifiedName~Tracer_models_keep_the_model_presence_rules"` (`Passed: 1`) | ✅ | ✅ green |
| R-10 | A repeated type id in one language takes `#<path>` (plus `@<n>` when one file repeats it); adding an unrelated earlier-sorting file leaves every other id unchanged (D-21) | unit | `--filter "FullyQualifiedName~ModelBuilderTests"` | ✅ update | ✅ green |
| R-10 | The id and key grammar is documented in README § Identity and per language in the `TypeModel.id`, `MemberModel.key`, `TypeDiff.id` and `MemberDiff.key` descriptions, with the other `id` properties pointing to them (D-22) | unit | `--filter "FullyQualifiedName~SchemaValidationTests"` | ✅ | ✅ green |
| R-10 | The v1 diff reports no type-level change: an access-only, modifiers-only or partial-and-files-only change leaves a type unchanged (D-20) | unit | `--filter "FullyQualifiedName~ModelDifferTests"` | ✅ update | ✅ green |
| R-10 | Edge identity leaves out `to`: an unrelated same-suffix type changes an edge's `to` and not its identity (D-23) | unit | `--filter "FullyQualifiedName~An_unrelated_type_with_the_same_suffix_changes_an_edges_target_but_not_its_identity"` (`Passed: 1`) | ✅ | ✅ green |
| R-23 | Seeded shuffles of every collection give byte-identical JSON; reflection guard over list properties (including `TypeModel.Files`, D-19) that runs its own 25 seeds into a local set and passes when run alone, so it does not depend on test order | unit | `--filter "FullyQualifiedName~CanonicalSerialisationTests"`; `--filter "FullyQualifiedName~CanonicalSerialisationTests.The_25_seeds_permute_every_reachable_list"` (alone, `Passed: 1`) | ✅ | ✅ green |
| R-23 | Two CLI runs give identical canonical files; different commits over the same trees give identical canonical files and a different `run.meta.json`; no local identity | integration | `--filter "FullyQualifiedName~DeterminismTests"` | ✅ update | ✅ green |
| ACC-01 | Ratchet truth table (four cases plus tool failure, missing output, schema-invalid output, missing golden) | unit | `--filter "FullyQualifiedName~Ratchet"` | ✅ | ✅ green |
| ACC-01 | Five fixtures report RED (pending) and the harness passes; a temporary green fixture passes; report table | integration | `--filter "FullyQualifiedName~FixtureHarnessTests"` | ✅ | ✅ green |
| ACC-01 | Goldens validate, round-trip byte-identically, pass MermaidLint, contain no CR or BOM; hygiene guard accepts only `expected/` golden names | unit | `--filter "FullyQualifiedName~FixtureGoldenTests\|FullyQualifiedName~RepoHygieneTests"` | ✅ | ✅ green |
| ACC-01 | Accept mode refuses under `TF_BUILD`/`CI`; writes only under `expected/` | unit | `--filter "FullyQualifiedName~FixtureHarnessTests"` | ✅ | ✅ green |
| ACC-01 | `azure-pipelines.yml` names hosted images only, with no org, pool, service connection or token; cargo uses `--locked` | unit | `--filter "FullyQualifiedName~PipelineYamlTests"` | ✅ | ✅ green |
| ACC-02 | Private mode on a synthetic stand-in: same comparison code, `--out` in this repo refused, console shows only `<shape>: PASS\|FAIL\|PENDING <date>`, shape allow-list, early return when unset | integration | `--filter "FullyQualifiedName~PrivateFixtureTests"` | ✅ | ✅ green |
| ACC-02 | `docs/how-to/private-fixtures.md` exists and names `VDIFF_FIXTURES_DIR` and `VDIFF_PRIVATE_OUT` | unit | `--filter "FullyQualifiedName~PrivateFixtureTests"` | ✅ | ✅ green |
| 999.1 (1) | fsmonitor tests' positive control holds on Linux | integration | Linux CI leg: `dotnet test --filter "FullyQualifiedName~fsmonitor"` | ✅ update | ⬜ pending (Windows green; Linux leg never run) |

### By plan and task (planned 2026-11-20, revised after plan check: 02-01 and 02-03 split, 02-11 and 02-12 added)

All commands run as `dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --nologo --filter "<filter>"` unless shown in full; each plan's `<verify>` block holds the exact command and its failure signal. Per-task commands filter to the test classes the task edits or whose output it changes, to stay inside the 30-second feedback budget; a filtered run still builds the whole test assembly, so compile-atomic record changes are covered. The full suite (`Category!=MermaidCli`) runs in each plan's `<verification>` and after every wave; 02-02 and 02-04 gained it after the final plan check, as `... --filter "Category!=MermaidCli" 2>&1 | grep -E "Passed! +- Failed: +0, Passed: +[1-9]"` with a fails-when line. After that check, D-19 added the type-level access, partial and files fields: 02-03-1 and 02-12-1 carry the new tracer and differ cases inside their existing filters, and 02-05-2's enum-parity row covers the schema side. The D-19 plan check then fixed two contract details: `none` is never a type-level access (absence is the only encoding), and every model type rule has a Phase 2 test (02-05-3, `ModelPresence`). 02-05-3, 02-06-1 and 02-06-3 join their two test runs in one `&&` chain inside a single `<automated>`, so neither run can be skipped. A later doc review added the 02-03 package-version and `via` presence rules to the checkers, one sole-problem row per field and direction (02-05-3's floor is 10, because `ModelPresence` checks `via` too), had `ContractSamples` give every list at least two distinct elements out of canonical order so 02-06-1's guard can pass, and made `02-06-SUMMARY.md` the only record of the freeze answer. The ce-doc-review decisions D-20 to D-23 then changed the set: the v1 diff has no type-level change (so `DiffPresence` has no type-pair rows, and 02-12-1's differ cases check that an access-only, modifiers-only or partial-and-files-only change leaves a type unchanged); repeated ids take a path suffix (02-03-1); the id and key grammar is documented and checked (02-05-1's Identity fact); and an edge's identity leaves out `to` (02-12-2: `Canonical.EdgeIdentity` and a fact on two real builder models; 02-05-2: `to_before`/`to_after` and six D-23 presence rows). 02-05-1's floor is 4 and 02-05-2's floor is 49 against a minimum of 52 (SchemaValidationTests 23, SchemaParityTests 4, DiffPresenceTests 25).

| Task | Wave | Requirement | Filter or check | Status |
|------|------|-------------|-----------------|--------|
| 02-01-1 | 1 | R-23 | `FullyQualifiedName~DeterminismTests\|…\|FullyQualifiedName~TraceRunScriptTests`; no hard-coded five-file success line | ✅ green |
| 02-02-1 | 1 | ACC-01 | package legitimacy checkpoint (manual, blocking) | ✅ recorded (manual checkpoint) |
| 02-02-2 | 1 | ACC-01 | `FullyQualifiedName~PipelineYamlTests` | ✅ green |
| 02-11-1 | 2 | R-23 | `FullyQualifiedName~TracerEndToEndTests\|FullyQualifiedName~DeterminismTests\|FullyQualifiedName~HostileInputTests` | ✅ green |
| 02-11-2 | 2 | R-23 | `FullyQualifiedName~HtmlWriterTests\|FullyQualifiedName~MermaidWriterTests\|FullyQualifiedName~DeterminismTests\|FullyQualifiedName~TracerEndToEndTests`; no commit field left in the diff record | ✅ green |
| 02-04-1 | 2 | 999.1 (D-11) | `FullyQualifiedName~GitObjectReaderTests\|FullyQualifiedName~A_core_fsmonitor_hook_is_never_run` | ✅ green (Windows only) |
| 02-04-2 | 2 | ACC-01 | `FullyQualifiedName~RatchetTests` | ✅ green |
| 02-03-1 | 3 | R-10 | `FullyQualifiedName~ModelBuilderTests\|FullyQualifiedName~ModelAssemblyTests\|FullyQualifiedName~ModelDifferTests\|FullyQualifiedName~BlankingEndToEndTests\|FullyQualifiedName~RichFixtureEndToEndTests\|FullyQualifiedName~TracerEndToEndTests`; no provisional model schema name in `src`/`tests` | ✅ green |
| 02-03-2 | 3 | R-10, R-23 | `FullyQualifiedName~ModelBuilderTests\|FullyQualifiedName~ModelDifferTests\|FullyQualifiedName~TracerEndToEndTests\|FullyQualifiedName~DeterminismTests` | ✅ green |
| 02-12-1 | 4 | R-10 | `FullyQualifiedName~ModelDifferTests\|FullyQualifiedName~ModelBuilderTests\|FullyQualifiedName~MermaidWriterTests\|FullyQualifiedName~HtmlWriterTests\|FullyQualifiedName~DeterminismTests\|FullyQualifiedName~RichFixtureEndToEndTests\|FullyQualifiedName~IncludeGlobEndToEndTests\|FullyQualifiedName~TracerEndToEndTests`; no provisional schema name in `src`/`tests` | ✅ green |
| 02-12-2 | 4 | R-23, R-10 | `FullyQualifiedName~ModelDifferTests\|FullyQualifiedName~ModelBuilderTests\|FullyQualifiedName~MermaidWriterTests\|FullyQualifiedName~HtmlWriterTests\|FullyQualifiedName~DeterminismTests\|FullyQualifiedName~TracerEndToEndTests`; `FullyQualifiedName~Same_trees_committed_twice_give_identical_canonical_files_and_differ_only_in_run_meta`; `FullyQualifiedName~An_unrelated_type_with_the_same_suffix_changes_an_edges_target_but_not_its_identity` | ✅ green |
| 02-05-1 | 5 | R-10 | `FullyQualifiedName~SchemaValidationTests` (at least 4) | ✅ green |
| 02-05-2 | 5 | R-10 | `FullyQualifiedName~SchemaParityTests\|FullyQualifiedName~SchemaValidationTests\|FullyQualifiedName~DiffPresenceTests` (at least 49) | ✅ green |
| 02-05-3 | 5 | R-10 | `FullyQualifiedName~ModelPresenceTests` (at least 10) `&&` `FullyQualifiedName~Tracer_models_keep_the_model_presence_rules` (`Passed: 1`) | ✅ green |
| 02-06-1 | 6 | R-23 | `FullyQualifiedName~CanonicalSerialisationTests` `&&` `FullyQualifiedName~CanonicalSerialisationTests.The_25_seeds_permute_every_reachable_list` run alone (`Passed: 1`), one chain | ✅ green |
| 02-06-2 | 6 | R-10 | schema-freeze decision (manual, blocking) | ✅ recorded (manual checkpoint) |
| 02-06-3 | 6 | R-10 | `FullyQualifiedName~SchemaFreezeTests` `&&` `Category=Contract`, one chain | ✅ green |
| 02-07-1..3 | 6 | ACC-01 | `FullyQualifiedName~FixtureHarnessTests\|FullyQualifiedName~RepoHygieneTests` | ✅ green |
| 02-08-1..3 | 7 | ACC-01 | `FullyQualifiedName~FixtureGoldenTests\|FullyQualifiedName~CommittedFixtureTests\|FullyQualifiedName~RepoHygieneTests`; RED rows in `out/fixture-report.md` (via `VDIFF_FIXTURE_REPORT`); `FullyQualifiedName~Golden_summary_passes_the_real_syntax_check` (local run, recorded in the SUMMARY; the CI step is advisory) | ✅ green |
| 02-09-1..3 | 7 | ACC-02 | `FullyQualifiedName~PrivateFixtureTests`; `docs/trials/private-fixtures.md` header-only | ✅ green (header-only check superseded by the UAT rows) |
| 02-10-1..3 | 8 | ACC-01 | ten RED rows in `out/fixture-report.md`; `FullyQualifiedName~All_five_shapes_have_a_fixture`; `FullyQualifiedName~Golden_summary_passes_the_real_syntax_check` (five rows, local run, recorded in the SUMMARY) | ✅ green |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [x] `tests/Vdiff.Tests/Contract/SchemaValidationTests.cs`, `SchemaParityTests.cs`, `DiffPresenceTests.cs`, `ModelPresenceTests.cs`, `CanonicalSerialisationTests.cs`
- [x] `tests/Vdiff.Tests/Fixtures/` (loader, harness, ratchet, report, golden self-checks, private mode)
- [x] `tests/Vdiff.Tests/Ci/PipelineYamlTests.cs`
- [x] `docs/schema/*.schema.json` (model, diff, run meta) and `tests/fixtures/<shape>/` (five fixtures, ten goldens)
- [x] NJsonSchema 11.6.2 and YamlDotNet 18.1.0 in `Vdiff.Tests.csproj` (after `checkpoint:human-verify`)
- [x] `.gitattributes` rules and the `RepoHygieneTests` change, in the same commit as the first golden (the `tests/fixtures/** eol=lf` rule landed one step earlier, in plan 02-02; the goldens were never committed without it)
- [x] The "five files" sites (RESEARCH.md Pitfall 6) behind one shared constant

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| `SamePath` resolves `/var` to `/private/var` | 999.1 (2) | No macOS CI leg (D-11) | Run `GitObjectReaderTests.ToplevelOrGitDir_*` on a Mac |
| Pipeline runs green on hosted Windows and Linux agents | ACC-01 | No ADO organisation connected (D-09) | Connect `azure-pipelines.yml` to an org and queue a run |
| Golden summaries pass the pinned mermaid-cli parser in CI | ACC-01 | The pipeline's Linux Mermaid step is advisory in Phase 2 (`continueOnError`, RESEARCH Pitfall 12) and no ADO organisation is connected (D-09) | The local run of `Golden_summary_passes_the_real_syntax_check` recorded in the 02-08 and 02-10 SUMMARYs is the Phase 2 proof; after the first connected Linux run, decide whether to make the step blocking |
| Five real PRs checked in the target organisation | ACC-02 | Private content never enters this repo (D-13) | Follow `docs/how-to/private-fixtures.md`; record pass/fail/pending per shape with a date |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [ ] Feedback latency < 30s (quick run measured at 56 s on 2026-11-24)
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** validated 2026-11-24, partial: not Nyquist compliant. The quick run misses the latency target, and the Linux, macOS, hosted-pipeline and reviewed private-fixture checks are still unrun.

## Validation Audit 2026-11-24

| Metric | Count |
|---|---|
| Gaps found | 1 |
| Resolved | 1 |
| Escalated | 0 |
