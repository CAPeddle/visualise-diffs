# visualise-diffs

## What This Is

An Azure DevOps pipeline tool (`vdiff`, a C#/.NET 10 CLI) that renders a pull request's
git diff as a class diagram for PR reviewers. It extracts a class model of the base and
head commits, diffs them, and shows the changed types plus their 1-hop neighbours: a
compact Mermaid (or PNG) summary in one sticky PR thread, and a full offline HTML report
as a pipeline artifact. The target product has a C++ core, a C# frontend and a macOS
Cocoa frontend.

## Core Value

A reviewer understands a PR's structural change (which types, members and edges changed
shape) from the diagram, without reading every file.

## Requirements

The single source of requirements is `docs/requirements/requirements.md` (R1–R28,
D1–D18, glossary, acceptance, risks). `.planning/REQUIREMENTS.md` mirrors it for GSD
traceability; where they differ, `docs/requirements/requirements.md` wins.

### Validated

(None yet — ship to validate)

### Active

- [ ] Resolve head and merge-base from ADO PR variables; extract a whole-repo model of both (R1)
- [ ] All target-specific settings come from `.visualise-diffs.yml` (R2), with overridable default exclusions (R3)
- [ ] Skip draft and fork PRs (R4)
- [ ] C++ extracted syntax-only with tree-sitter, with length-preserving macro blanking (R5)
- [ ] C# extracted semantically with Roslyn from `.csproj` + reference assemblies, falling back to syntax-only (R6, R7)
- [ ] C++/CLI extracted by a header scanner into a "managed wrapper" lane (R8)
- [ ] Every file and language labelled `semantic` / `syntax-only` / `scanner`, fallbacks explained (R9)
- [ ] One versioned model, `vdiff-model/v1`, from every extractor (R10)
- [ ] Type/member/edge diff states including `body-changed` and scored rename matching (R13, R14)
- [ ] Package-pin change shown as one node (R15)
- [ ] Node selection: changed types + greyed 1-hop neighbours, cap 25 including neighbours, overflow summary, text degrade (R18, R19)
- [ ] Mermaid `classDiagram` with `classDef` per state and trailing member tags; PNG fallback; offline HTML artifact (R20–R22)
- [ ] Byte-identical output for identical inputs (R23)
- [ ] One sticky PR thread found by marker and updated in place; nothing posted for no structural change (R24, R25)
- [ ] Separate non-blocking job, 20-minute limit, P90 under 5 minutes (R26, R27)
- [ ] Dry-run mode writing everything locally (R28)

### Out of Scope

- Semantic C++ via clang-cl analysis (R11) — increment 2; MVP C++ is syntax-only because the projects have no clang build support
- ObjC/ObjC++ extraction (R12) — phase 2 (D7)
- Diffing dependency-package types (R16) — phase 2 (D12)
- Cross-bridge type linking (R17) — phase 2 (D10)
- Code delivered through Conan packages, including CMake-built subtrees — PRs in scope never touch it (D9)
- Onboarding use case — audience is PR reviewers only (D2)
- Writing our own C++ parser — tree-sitter now, clang-cl in increment 2
- Blocking a PR on tool failure — the job is advisory (R27)

## Context

- The repository is **public** (D15). Target product names, repo/org names, PR numbers,
  class names, macros, pipeline IDs, agent pools and target-measured figures never enter
  it. Target specifics live in `.visualise-diffs.yml` in the target repo and in private
  fixtures held in the target organisation. Every commit diff is grepped for target names.
- Private target findings exist outside this repo. They inform decisions; their content is
  not copied here.
- Prior design ideas reused from `architecture_analyser` (no code dependency): status
  discipline, canonical ordering, rename-candidate scoring, bounded subprocesses.
- Planning docs: `docs/plans/2026-11-16-001-feat-mvp-diff-class-diagram-plan.md`
  (architecture, spikes S1–S3, units U1–U10). Spike records in `docs/spikes/`, ADRs in
  `docs/decisions/`.
- Mermaid syntax constraint: leading `+ - # ~` are visibility syntax and `~T~` is generics
  syntax, so member change states are trailing tags (e.g. `+Foo(int) void [added]`).

## Constraints

- **Tech stack**: C#/.NET 10 CLI `vdiff`, Roslyn in-process; every extractor emits `vdiff-model/v1` JSON (D14)
- **C++ parsing**: tree-sitter syntax-only in the MVP; no home-grown parser (D11)
- **In-scope code**: builds only on Windows (.vcxproj) and macOS (Xcode) (D9)
- **Runner**: separate job; Ubuntu default with Windows fallback (D18) — see open question O4 below
- **Time**: 20-minute hard timeout, P90 under 5 minutes, never extends the PR build's wall-clock (R26)
- **Safety**: never block a PR; exit 0 on publish or skip, 1 on tool failure (R27)
- **Privacy**: no posting to ADO or external renderers with target content without explicit PO approval

## Open Questions

- **O3** — Does Mermaid `classDiagram` + `classDef` render in ADO PR comments? Spike S1
  (`docs/spikes/S1-mermaid-in-ado-pr.md`) closes it; gates U8.
- **O4** — The PO said (2026-11-16) that S3's premise came from a mis-report and that the
  target C# is built on Windows. The wording here is an interpretation, not yet confirmed.
  The effect on S3 and on the Ubuntu-default runner (R26, D18) waits for a PO decision.

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Audience is PR reviewers (D1) | Review comprehension is the pain | — Pending |
| MVP languages C++ (syntax-only), C# (Roslyn + fallback), C++/CLI (scanner) (D7, D11, D13) | No clang build support; Roslyn available in-process | — Pending |
| C#/.NET 10 orchestrator with Roslyn in-process (D14) | Roslyn in-process; flip only if Inc2 needs a custom libclang walker | — Pending |
| Sticky PR thread + HTML artifact (D4) | Summary where reviewers look; full detail offline | — Pending |
| Changed types + greyed 1-hop neighbours, cap 25 incl. neighbours (D6, D17) | Bounded, readable diagrams | — Pending |
| No thread for no structural change (D16) | Avoid noise | — Pending |
| Trailing member tags for change state | Leading symbols are Mermaid syntax | — Pending S1 |
| GSD bootstrapped from existing docs, questioning/research skipped | Seven PO rounds and a target investigation already done | ✓ Good |

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd-transition`):
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd-complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state

---
*Last updated: 2026-11-16 after initialization*
