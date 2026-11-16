---
phase: "1"
slug: "offline-tracer"
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
status: draft
nyquist_compliant: false
wave_0_complete: false
created: "2026-11-16"
---

# Phase 1 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution. Derived from
> `01-RESEARCH.md` § Validation Architecture.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit 2.9.3 on Microsoft.NET.Test.Sdk 17.14.1 (.NET); `cargo test` (Rust sidecar) |
| **Config file** | `tests/Vdiff.Tests/Vdiff.Tests.csproj` (plan 01-02); `extractors/cpp-ts/Cargo.toml` (plan 01-01) |
| **Quick run command** | `dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --nologo --filter "FullyQualifiedName~<Class>"` or `cargo test --locked --manifest-path extractors/cpp-ts/Cargo.toml <module>` |
| **Full suite command** | `dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --nologo` then `cargo test --locked --manifest-path extractors/cpp-ts/Cargo.toml` |
| **Estimated runtime** | ~30 s per filtered run after a one-time ~76 s cold sidecar build (collection fixture) |

A filter that matches nothing exits 0 in both runners, so every verify greps for a non-zero
passed count (`Passed! +- Failed: +0, Passed: +[1-9]`, `test result: ok\. [1-9][0-9]* passed; 0 failed`).

---

## Sampling Rate

- **After every task commit:** the task's own filtered `dotnet test` or `cargo test <module>`
- **After every plan wave:** full `dotnet test` plus full `cargo test --locked`
- **Before `/gsd-verify-work`:** both full suites green; `git ls-files` shows no output or build paths
- **Max feedback latency:** ~30 s (after the cold sidecar build)

---

## Per-Requirement Verification Map

| Requirement / criterion | Behavior | Test Type | Automated Command | Plan |
|---|---|---|---|---|
| SPK-02 / SC5 | ADR-001 Accepted; sidecar pinned, versions reported | unit + grep | `cargo test --locked ...` version guard; `grep -c "^- \*\*Status:\*\* Accepted (PO, 2026-11-16)" docs/decisions/ADR-001-cpp-tree-sitter-packaging.md` | 01-01, 01-10 |
| SPK-02 / SC5 | extraction runs through the sidecar; metadata in every model | integration | `--filter "FullyQualifiedName~TracerEndToEndTests"` | 01-03 |
| TRC-01 / SC2 | repo byte-identical before/after in every state, incl. an fsmonitor-hook repo | integration | `--filter "FullyQualifiedName~RepoUnchangedMatrixTests"` | 01-04 |
| TRC-01 | hostile args never reach git as options; abbreviated-id shadowing rejected | integration | `--filter "FullyQualifiedName~HostileInputTests"` | 01-04 |
| TRC-01 | read-only git env and `-c` overrides | unit | `--filter "FullyQualifiedName~GitObjectReaderTests"` | 01-02 |
| TRC-02 / SC1 | diff.json lists exactly the expected types and members | integration | `--filter "FullyQualifiedName~TracerEndToEndTests"` | 01-03 |
| TRC-02 | member-level states, duplicates, canonical order | unit | `--filter "FullyQualifiedName~ModelDifferTests\|FullyQualifiedName~ModelBuilderTests"` | 01-07 |
| TRC-02 | extraction shapes (enums, operators, templates, preproc, error nodes, deadline + reset) | cargo | `cargo test --locked --manifest-path extractors/cpp-ts/Cargo.toml` | 01-01, 01-06 |
| TRC-02 / SC3 | Mermaid structure and injection safety | unit | `--filter "FullyQualifiedName~MermaidWriterTests"` | 01-08 |
| TRC-02 / SC3 | real Mermaid syntax check incl. a >50,000-char diagram (maxTextSize raised) | integration | `--filter "Category=MermaidCli"` | 01-09 |
| TRC-02 | HTML self-contained; byte-identical deterministic outputs, LF, no local path | unit + integration | `--filter "FullyQualifiedName~HtmlWriterTests\|FullyQualifiedName~DeterminismTests"` | 01-08 |
| TRC-01/02 | sidecar hang, flood, exit, garbage → exit 1, nothing written | integration | `--filter "FullyQualifiedName~SidecarBoundsTests"` | 01-05 |
| TRC-03 / SC4 | trial script prints six fixed lines, no leaks | integration | `--filter "FullyQualifiedName~TraceRunScriptTests"` | 01-10 |
| D-09 hygiene | no output/build dirs tracked; no remote use in tests | integration | `--filter "FullyQualifiedName~RepoHygieneTests"` | 01-04 |

*Status per task is tracked in the plan SUMMARY files.*

---

## Wave 0 Requirements

Existing infrastructure: none (greenfield). Plans 01-01 and 01-02 create all of it
(solution, test project, `SyntheticRepo`, `RepoSnapshot`, `SidecarFixture`, fake sidecar), so no
separate Wave 0 is needed.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Real run on the PO's private local Windows clone inside the R26 budget, repo unchanged | TRC-03 / SC4 | Private target content must not enter the public repo or CI | Orchestrator runs `pwsh -NoProfile -File scripts/trace-run.ps1` with inputs supplied at run time; only the six-line pass/fail block is recorded (plan 01-10 T2) |
| npm package approval (mermaid-cli, puppeteer) | TRC-02 | Package-legitimacy gate flagged "too-new" | PO answers the 01-03 checkpoint; answer recorded as a `package-checkpoint:` line |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60 s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
