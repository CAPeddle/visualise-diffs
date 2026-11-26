---
phase: "3"
slug: "git-layer"
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
# audit-milestone §5.5 distinguishes NOT-VALIDATED (draft) from PARTIAL (validated + nyquist_compliant: false) (#2117)
status: draft
nyquist_compliant: false
wave_0_complete: false
created: "2026-11-26"
---

# Phase 3 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Source: `03-RESEARCH.md` § Validation Architecture.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit 2.9.3, Microsoft.NET.Test.Sdk 17.14.1 (`tests/Vdiff.Tests/Vdiff.Tests.csproj`) |
| **Config file** | none beyond the csproj; traits select categories (`Contract`, `MermaidCli` exist; `Category=Git` is new in this phase) |
| **Quick run command** | `dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj -c Release --filter "Category=Git"` |
| **Full suite command** | `dotnet test vdiff.sln -c Release --filter "Category!=MermaidCli"` then `cargo test --locked --manifest-path extractors/cpp-ts/Cargo.toml` |
| **Estimated runtime** | quick: under 60 s; full: ~670 s (baseline 934 passed, 11 m 8 s, Debug, Windows) |

---

## Sampling Rate

- **After every task commit:** Run the quick run command for the touched layer (`Category=Git`, or the named `FullyQualifiedName~` filter).
- **After every plan wave:** Run the full suite command.
- **Before `/gsd-verify-work`:** Full suite green, `cargo test` green, and the fixture harness still reports the five fixtures exactly as before (the ratchet must not flip).
- **Max feedback latency:** 60 seconds for per-task runs.

Rules carried from research: any test class that calls `CliApp.Run` or the real sidecar, or sets process-wide environment variables, carries `[Collection("sidecar")]`. PR-mode tests supply variables through `CliHooks.GetEnvironmentVariable`, never the process environment. Every Phase 3 plan ends with the baseline pass count (934) plus its new tests.

---

## Per-Task Verification Map

Filled in by the planner and executor once plan and task IDs exist. Requirement-to-test map from research:

| Req / Criterion | Behaviour | Plan / Task | Test Type | Automated Command | File Exists | Status |
|-----------------|-----------|-------------|-----------|-------------------|-------------|--------|
| R-01 / SC1 | head = SourceCommitId; base = merge-base(target tip, head); target tip = the merge commit's first parent after verifying its second parent is SourceCommitId (D-17 as locked; research's order-independent variant not adopted); `origin/<target>` fallback; no merge-base exits 1; criss-cross records other bases | 03-08 T1, T3 | integration | `--filter "FullyQualifiedName~PrAnalysisTests\|FullyQualifiedName~MergeBaseTests"` | ❌ W0 | ⬜ pending |
| R-01 / SC1 | both trees exported raw (CRLF/BOM kept), checkout and index untouched, trees deleted on success and failure, `--keep-trees` keeps and prints | 03-01 T1, T2 | integration | `--filter "FullyQualifiedName~TreeExportTests\|FullyQualifiedName~TreeLocationTests"` | ❌ W0 | ⬜ pending |
| R-01 / SC1 | partial or shallow clone: `BAD`/`missing` object exits 1; shallow message names `fetchDepth: 0`; nothing fetched | 03-01 T1, 03-06 T1, T2 | integration | `--filter "FullyQualifiedName~IncompleteCloneTests\|FullyQualifiedName~HostileInputTests"` | ❌ W0 | ⬜ pending |
| UAT input | a file unreadable on one side never turns its types `added`/`removed`; every in-scope export skip is excluded on both sides with `<side>-<reason>` | 03-07 T1 | integration | `--filter "FullyQualifiedName~UnreadableSideTests"` | ❌ W0 | ⬜ pending |
| R-01 / SC2 | change list incl. whitespace-only files, rename pairs with score, rename limit, hunk ranges, CRLF-only change, `-diff` attribute neutralised | 03-04 T1, T2 | unit + integration | `--filter "FullyQualifiedName~ChangeSetTests\|FullyQualifiedName~HunkTests"` | ❌ W0 | ⬜ pending |
| R-04 / SC3 | fork PR skipped: exit 0, `skip.json` only, zero HTTP requests | 03-09 T1, T2 | integration (fake handler) | `--filter "FullyQualifiedName~PrSkipTests\|FullyQualifiedName~DraftLookupTests"` | ❌ W0 | ⬜ pending |
| R-04 / SC3 | draft PR skipped: exit 0, `skip.json` schema-valid, no other outputs | 03-09 T2 | integration (fake handler) | `--filter "FullyQualifiedName~DraftLookupTests"` | ❌ W0 | ⬜ pending |
| R-04 | lookup failures exit 1 with fixed message; token never in stdout, stderr, files or child environment | 03-03 T1, 03-09 T3 | integration | `--filter "FullyQualifiedName~DraftLookupTests\|FullyQualifiedName~ChildEnvironmentTests"` | ❌ W0 | ⬜ pending |
| D-12 | `other_merge_bases` only with several bases; schema-valid; run-meta hash re-pinned once | 03-02 T2, 03-08 T3 | contract + integration | `--filter "Category=Contract\|FullyQualifiedName~PrAnalysisTests"` | update existing | ⬜ pending |
| D-16 | `skip.json` contract: fourth schema pinned, parity, negative row | 03-02 T3 | contract | `--filter "Category=Contract"` | update existing | ⬜ pending |
| D-13 | `--pr` with `--base`/`--head` is an error; offline mode unchanged; PR variables fail closed | 03-08 T1, T2 | unit | `--filter "FullyQualifiedName~CliOptionsTests\|FullyQualifiedName~PrContextTests"` | update existing | ⬜ pending |
| R-23 | different temp base directory gives byte-identical outputs; no absolute path in outputs | 03-01 T2 | integration | `--filter "FullyQualifiedName~DeterminismTests"` | extend existing | ⬜ pending |
| D-05/D-06 | temp root inside analysed repo, vdiff repo or through a link is refused | 03-01 T2, 03-08 T1 | integration | `--filter "FullyQualifiedName~TreeLocationTests\|FullyQualifiedName~PrAnalysisTests"` | ❌ W0 | ⬜ pending |
| Security | hostile tree (`..`, `.git`, reserved names, collisions, non-UTF-8) exports nothing outside the root | 03-05 T1 | unit + integration | `--filter "FullyQualifiedName~PathRulesTests\|FullyQualifiedName~TreeExportTests"` | ❌ W0 | ⬜ pending |
| D-08 | tiered total and file-count limits, deterministic skips, out-of-scope count line | 03-05 T2 | unit + integration | `--filter "FullyQualifiedName~ExportPlanTests\|FullyQualifiedName~TreeExportTests"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `SyntheticRepo` extensions: `WriteTree`/`CommitTree`, `Merge` with an advancing clock, `Branch`, helpers for a `blob:none` clone and a depth-1 clone of the sandbox (local `file://` with `uploadpack.allowFilter`)
- [ ] `tests/Vdiff.Tests/Git/` test classes named above, tagged `Category=Git`
- [ ] A fake ADO `HttpMessageHandler` that records requests, and a fake child that dumps its environment for the token test
- [ ] Updates to existing tests: `HostileInputTests`, `GitObjectReaderTests`, `ModelBuilderTests`, `ModelDifferTests`, `CliOptionsTests`, `RepoUnchangedMatrixTests`, `TracerEndToEndTests`, `SchemaValidationTests`, `SchemaFreezeTests`
- [ ] `docs/how-to/private-fixtures.md` updated: private runs write target source to a deleted temp directory; a full clone is required

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Draft/fork detection against a real Azure DevOps organisation | R-04 | No organisation is connected; automated tests use a fake handler only | At the first connected pipeline trial, open a draft PR and a fork PR and confirm each run exits 0 with `skip.json` and no thread. Also queue the draft PR's build validation by hand from the PR (expected: exit 0, `skip.json`), and queue the pipeline by hand for the draft PR's branch (expected: no PR variables, exit 1 with the not-a-pull-request-build message, nothing posted); criterion 3 holds for the second run shape only once Phase 9 runs the step when `Build.Reason` is `PullRequest` (03-09 records this) |
| ADO merge-commit parent order, `origin/<target>` presence, Build Service read permission | R-01 | Needs a real agent checkout | At the first connected trial, record `git log -1 --format=%P` of `Build.SourceVersion` and `git branch -r` in the job log |
| R-26 P90 timing on a full-size target | R-01 | Synthetic probe only (~1,170 files/s) | Measure tree export time and the hunk call's time (03-04 call B, which runs over every changed file and exits 1 past 5 minutes or 256 MiB) at the first connected trial |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
