---
phase: "02"
slug: "model-contract-and-fixture-harness"
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
# audit-milestone §5.5 distinguishes NOT-VALIDATED (draft) from PARTIAL (validated + nyquist_compliant: false) (#2117)
status: draft
nyquist_compliant: false
wave_0_complete: false
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
| R-10 | Tracer output (base model, head model, diff) validates against the v1 schemas | integration | `dotnet test --filter "FullyQualifiedName~SchemaValidationTests"` | ❌ W0 | ⬜ pending |
| R-10 | Full-fat record instance validates; one rejected negative document per keyword; allow-listed keywords only (D-15) | unit | `--filter "FullyQualifiedName~SchemaValidationTests"` | ❌ W0 | ⬜ pending |
| R-10 | Record properties equal schema properties (both directions): model, diff, run meta | unit | `--filter "FullyQualifiedName~SchemaParityTests"` | ❌ W0 | ⬜ pending |
| R-23 | Seeded shuffles of every collection give byte-identical JSON; reflection guard over list properties | unit | `--filter "FullyQualifiedName~CanonicalSerialisationTests"` | ❌ W0 | ⬜ pending |
| R-23 | Two CLI runs give identical canonical files; different commits over the same trees give identical canonical files and a different `run.meta.json`; no local identity | integration | `--filter "FullyQualifiedName~DeterminismTests"` | ✅ update | ⬜ pending |
| ACC-01 | Ratchet truth table (four cases plus tool failure, missing output, schema-invalid output, missing golden) | unit | `--filter "FullyQualifiedName~Ratchet"` | ❌ W0 | ⬜ pending |
| ACC-01 | Five fixtures report RED (pending) and the harness passes; a temporary green fixture passes; report table | integration | `--filter "FullyQualifiedName~FixtureHarnessTests"` | ❌ W0 | ⬜ pending |
| ACC-01 | Goldens validate, round-trip byte-identically, pass MermaidLint, contain no CR or BOM; hygiene guard accepts only `expected/` golden names | unit | `--filter "FullyQualifiedName~FixtureGoldenTests\|FullyQualifiedName~RepoHygieneTests"` | ❌ W0 / ✅ update | ⬜ pending |
| ACC-01 | Accept mode refuses under `TF_BUILD`/`CI`; writes only under `expected/` | unit | `--filter "FullyQualifiedName~FixtureHarnessTests"` | ❌ W0 | ⬜ pending |
| ACC-01 | `azure-pipelines.yml` names hosted images only, with no org, pool, service connection or token; cargo uses `--locked` | unit | `--filter "FullyQualifiedName~PipelineYamlTests"` | ❌ W0 | ⬜ pending |
| ACC-02 | Private mode on a synthetic stand-in: same comparison code, `--out` in this repo refused, console shows only `<shape>: PASS\|FAIL\|PENDING <date>`, shape allow-list, early return when unset | integration | `--filter "FullyQualifiedName~PrivateFixtureTests"` | ❌ W0 | ⬜ pending |
| ACC-02 | `docs/how-to/private-fixtures.md` exists and names `VDIFF_FIXTURES_DIR` and `VDIFF_PRIVATE_OUT` | unit | `--filter "FullyQualifiedName~PrivateFixtureTests"` | ❌ W0 | ⬜ pending |
| 999.1 (1) | fsmonitor tests' positive control holds on Linux | integration | Linux CI leg: `dotnet test --filter "FullyQualifiedName~fsmonitor"` | ✅ update | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/Vdiff.Tests/Contract/SchemaValidationTests.cs`, `SchemaParityTests.cs`, `CanonicalSerialisationTests.cs`
- [ ] `tests/Vdiff.Tests/Fixtures/` (loader, harness, ratchet, report, golden self-checks, private mode)
- [ ] `tests/Vdiff.Tests/Ci/PipelineYamlTests.cs`
- [ ] `docs/schema/*.schema.json` (model, diff, run meta) and `tests/fixtures/<shape>/` (five fixtures, ten goldens)
- [ ] NJsonSchema 11.6.2 and YamlDotNet 18.1.0 in `Vdiff.Tests.csproj` (after `checkpoint:human-verify`)
- [ ] `.gitattributes` rules and the `RepoHygieneTests` change, in the same commit as the first golden
- [ ] The "five files" sites (RESEARCH.md Pitfall 6) behind one shared constant

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| `SamePath` resolves `/var` to `/private/var` | 999.1 (2) | No macOS CI leg (D-11) | Run `GitObjectReaderTests.ToplevelOrGitDir_*` on a Mac |
| Pipeline runs green on hosted Windows and Linux agents | ACC-01 | No ADO organisation connected (D-09) | Connect `azure-pipelines.yml` to an org and queue a run |
| Five real PRs checked in the target organisation | ACC-02 | Private content never enters this repo (D-13) | Follow `docs/how-to/private-fixtures.md`; record pass/fail/pending per shape with a date |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
