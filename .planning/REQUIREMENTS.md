# Requirements: visualise-diffs

**Defined:** 2026-11-16
**Core Value:** A reviewer understands a PR's structural change from the diagram, without reading every file.

**Source of truth:** `docs/requirements/requirements.md`. IDs here map one-to-one:
`R-05` here is `R5` there. Full wording, decisions (D1–D18), acceptance criteria and
risks are in that file; this file adds GSD traceability only. The exception is
TRC-01 to TRC-03: they were added here by the PO revision of 2026-11-16 (tracer-first
roadmap) and have no counterpart in that file yet.

## v1 Requirements (MVP)

### Offline tracer

- [x] **TRC-01**: User can run `vdiff` offline against a local git repository with explicit base and head commits; the tool reads commit contents from git objects and leaves the repository's working tree, index, branch and refs unchanged.
- [x] **TRC-02**: User gets, for C++ code, a type- and member-level diff between the two commits written as model JSON, diff JSON, a Mermaid classDiagram summary (classDef per state, trailing member tags) and a self-contained HTML page in the output directory.
- [x] **TRC-03**: User can run the tracer against a real target PR on a private local clone and keep every output outside this public repository; any result recorded in this repo carries no target identifiers.

### Spikes (decisions that gate units)

- [ ] **SPK-01**: Spike S1 recorded — Mermaid `classDiagram` + `classDef` rendering in ADO PR comments/descriptions tested; renderer default and trailing member-tag format fixed (closes O3; gates U8, U9)
- [x] **SPK-02**: Spike S2 recorded as ADR-001 — C++ tree-sitter packaging chosen (.NET binding vs Rust sidecar with `tree-sitter` 0.27 + `tree-sitter-cpp`) on package size, Linux install, speed, macro blanking (gates U3)
- [ ] **SPK-03**: Spike S3 recorded — C# extraction runner decided (Roslyn against reference assemblies; Ubuntu vs Windows per D18; reframing pending, see O4 in PROJECT.md) (gates U4, R26 runner)

### Model and harness

- [ ] **R-10**: Every extractor emits one versioned model `vdiff-model/v1` (types, members, bases, derived edges, spans, extraction metadata)
- [ ] **R-23**: Identical inputs produce byte-identical outputs; canonical ordering, volatile metadata kept apart

### Inputs and scope

- [ ] **R-01**: Resolve head and base (merge-base) from ADO PR variables and extract a full model of both commits
- [ ] **R-02**: Read every target-specific setting from `.visualise-diffs.yml`
- [ ] **R-03**: Apply overridable default exclusions (tests opt-in, `*.Designer.cs`, generated, build output, vendored/`dist/`)
- [ ] **R-04**: Skip draft and fork PRs; no thread, exit 0

### Extraction

- [ ] **R-05**: C++ syntax-only with tree-sitter, with length-preserving blanking of configured macros
- [ ] **R-06**: C# file list from `.csproj` (SDK-style and legacy); semantic extraction with Roslyn from project file + reference assemblies, no MSBuild/VS
- [ ] **R-07**: Syntax-only C# fallback when references are insufficient, reporting unresolved count and reason
- [ ] **R-08**: C++/CLI header scanner (`ref class`, `value class`, `interface class`, `enum class`, `gcroot<>`) into a "managed wrapper" lane
- [ ] **R-09**: Mode label per language and file (`semantic`/`syntax-only`/`scanner`), fallback reasons, unmodelled files listed; never present partial as complete

### Diff

- [ ] **R-13**: Tag types, members and edges `added`/`removed`/`modified`/`renamed`/`unchanged`, plus member `body-changed`
- [ ] **R-14**: Rename matching by the weighted score (≥0.6, mutual unique best, same language and kind); member renames on identical signature; thresholds configurable, pinned by the rename fixture
- [ ] **R-15**: Changed package pin detected by a configured rule and emitted as a `package updated: name old → new` node

### Diagram and rendering

- [ ] **R-18**: Node selection — changed types, then greyed 1-hop neighbours; cap N=25 including neighbours; ranking; overflow summary nodes
- [ ] **R-19**: Degrade to a text table by namespace/project when changed types exceed 2N or Mermaid limits (50,000 chars / 500 edges) would be exceeded
- [ ] **R-20**: Mermaid `classDiagram` with a `classDef` per type state; trailing member state tags (format from S1)
- [ ] **R-21**: PNG render used when Mermaid does not render in PR comments; default from S1, configurable
- [ ] **R-22**: Self-contained offline HTML artifact: no cap, per-language tabs, search, mode banner

### Delivery and operation

- [ ] **R-24**: One PR thread via `System.AccessToken`, found by a hidden marker and updated in place, linking the artifact
- [ ] **R-25**: No structural change → post nothing; an existing thread is updated to "no structural changes"
- [ ] **R-26**: Separate pipeline job (Ubuntu default, Windows fallback per D18); hard limit 20 min, P90 under 5 min
- [ ] **R-27**: Never block a PR: `continueOnError`, 20-min timeout, existing thread untouched on crash, pipeline warning; exit 0/1
- [ ] **R-28**: Dry-run mode writes thread body, PNG and HTML locally without contacting ADO

### Acceptance

- [ ] **ACC-01**: Five synthetic public fixtures green in CI (small C++, C#, cross-stack with C++/CLI, extract-class refactor, rename)
- [ ] **ACC-02**: Private-fixture recipe documented so five real PRs of the same shapes can be checked inside the target organisation (D15)
- [ ] **ACC-03**: Over 10 consecutive target PR runs, no added PR-build wall-clock and P90 job time under 5 minutes

## v2 Requirements

### Increment 2 — semantic C++

- **R-11**: Upgrade C++ to `semantic` file by file via a scripted compile DB from MSBuild `GetClCommandLines`, parsed (no compile) by clang-cl in MSVC-compatibility mode; analysis only

### Phase 2

- **R-12**: ObjC/ObjC++ extraction from a compile DB derived from the macOS build log, tree-sitter-objc fallback
- **R-16**: Diff the dependency package's types between two pins
- **R-17**: Link C++ interfaces to implementing C++/CLI and ObjC++ classes by base name

## Out of Scope

| Feature | Reason |
|---------|--------|
| Code delivered via Conan packages (incl. CMake-built subtrees) | PRs in scope never touch it (D9) |
| A home-grown C++ parser | tree-sitter now, clang-cl in Inc2 |
| A clang build configuration for the target projects | Clang is analysis-only (D11) |
| Onboarding use case | Audience is PR reviewers (D2) |
| Blocking PRs | Tool is advisory (R27) |
| Swift | The product has no Swift (D3) |

## Traceability

Phases are defined in `.planning/ROADMAP.md`. Each requirement maps to exactly one phase.

| Requirement | Phase | Status |
|-------------|-------|--------|
| TRC-01 | Phase 1 | Complete |
| TRC-02 | Phase 1 | Complete |
| TRC-03 | Phase 1 | Complete |
| SPK-02 | Phase 1 | Complete |
| R-10 | Phase 2 | Pending |
| R-23 | Phase 2 | Pending |
| ACC-01 | Phase 2 | Pending |
| ACC-02 | Phase 2 | Pending |
| R-01 | Phase 3 | Pending |
| R-04 | Phase 3 | Pending |
| R-02 | Phase 4 | Pending |
| R-03 | Phase 4 | Pending |
| R-05 | Phase 4 | Pending |
| R-06 | Phase 4 | Pending |
| R-07 | Phase 4 | Pending |
| R-08 | Phase 4 | Pending |
| R-09 | Phase 4 | Pending |
| SPK-03 | Phase 4 | Pending |
| R-13 | Phase 5 | Pending |
| R-14 | Phase 5 | Pending |
| R-15 | Phase 5 | Pending |
| R-18 | Phase 6 | Pending |
| R-19 | Phase 6 | Pending |
| R-20 | Phase 7 | Pending |
| R-21 | Phase 7 | Pending |
| R-22 | Phase 7 | Pending |
| R-24 | Phase 8 | Pending |
| R-25 | Phase 8 | Pending |
| R-28 | Phase 8 | Pending |
| SPK-01 | Phase 8 | Pending |
| R-26 | Phase 9 | Pending |
| R-27 | Phase 9 | Pending |
| ACC-03 | Phase 9 | Pending |

**Notes:**
- ACC-01 is owned by Phase 2 (harness and fixtures) but is only satisfied once all five
  fixtures are green, which happens progressively in Phases 5 to 7.
- R-01 is owned by Phase 3 (head/base resolution and both trees); extraction of both
  commits happens in Phase 4.
- R-09 spans the plan's U3 and U4 and is owned once, by Phase 4.
- The Phase 1 tracer produces a thin first cut of R-05, R-10, R-13, R-20 and R-22. Those
  requirements stay owned by the phases that harden them (Phases 4, 2, 5, 7 and 7); the
  TRC requirements cover only what the tracer itself delivers.
- SPK-02 is owned by Phase 1: ADR-001 is Accepted (Rust sidecar) and is the basis of the
  tracer's C++ extraction.
- SPK-03 is owned by Phase 4 (attached to the C# plan) and is on hold pending PO decision
  O4 (see PROJECT.md); the PO's reading of O4 is not yet confirmed.
- SPK-01 is owned by Phase 8. It is a manual spike with no code dependency, so it can run
  at any time, and it must be recorded before Phase 7 closes.

**Coverage:**
- v1 requirements: 33 total
- Mapped to phases: 33
- Unmapped: 0 ✓

---
*Requirements defined: 2026-11-16*
*Last updated: 2026-11-16 after roadmap revision (tracer-first)*
