---
title: visualise-diffs MVP — PR diff rendered as a class diagram
status: proposed
date: 2026-11-16
inputs:
  - docs/requirements/requirements-draft.md
  - private target-repo findings (not in this public repo; see "Confidentiality")
---

# visualise-diffs MVP plan

## Confidentiality

This repository is **public**. The target product, its repositories, PR numbers, class
names, macros, pipeline IDs and agent pools must not be committed here. Everything
target-specific lives in a config file in the target repository (`.visualise-diffs.yml`)
and in a private fixture store. Golden acceptance fixtures (U1) are kept in the target
organisation; this repo only holds synthetic fixtures that reproduce the same shapes.

## Product contract

**User:** a PR reviewer in Azure DevOps.

**Job:** understand a PR's structural change without reading every file. The pains are
large PRs, cross-stack ripple, and inheritance/API drift.

**MVP output:** for each PR, one sticky PR thread and one pipeline artifact.

- The thread holds a summary class diagram: the changed types plus 1-hop neighbours,
  capped at about 20–25 nodes with an overflow node. Above the cap it degrades to a text
  summary.
- The artifact is a full interactive HTML report with no cap.
- Both state the extraction mode used for each language and list every file that was not
  modelled.

**MVP languages and lanes:**

- **C++:** syntax-only (tree-sitter).
- **C#:** semantic via Roslyn, with a syntax-only fallback.
- **C++/CLI:** a restricted header scanner, shown as its own "managed wrapper" lane.

**Not in MVP:**

- compiler-accurate C++ (increment 2);
- ObjC/ObjC++ (phase 2);
- cross-language bridge linking (phase 2);
- diffing a dependency package's types when its version pin changes (phase 2).

## Requirements (MVP unless marked)

| ID | The tool shall … |
|---|---|
| R1 | Resolve base (merge-base) and head from Azure DevOps PR variables, and extract a full model of both commits. Whole-repo extraction is required so that 1-hop neighbours (derived types, associations) can be found. |
| R2 | Diff the two models and tag every type, member and edge as `added`, `removed`, `modified`, `renamed` or `unchanged`. A member is `modified` if its signature, visibility, static/virtual/override or type changed, and `body-changed` if a diff hunk overlaps its span. |
| R3 | Match renamed or moved types and members as one `renamed` node. Matching uses member-set similarity plus git rename detection; git `-M` alone is not enough. |
| R4 | Build the diagram from the changed types plus 1-hop neighbours (bases, derived types, field/association targets), greyed. Cap the summary at a configurable N (default 25) with an "N more" node. Above a second threshold, degrade to a per-namespace/per-project text summary. |
| R5 | Render the summary as Mermaid `classDiagram` with `classDef` per change state. Member states use text markers (`+ added`, `- removed`, `~ modified`). |
| R6 | Render a PNG of the summary, used when Mermaid does not render in the organisation's PR comments. The switch is a config setting. |
| R7 | Publish a self-contained HTML report (no network access needed) as a pipeline artifact, and link it from the thread. |
| R8 | Post or update **one** sticky PR thread using `System.AccessToken`, identified by a hidden marker. |
| R9 | Report the extraction mode per language (`semantic`, `syntax-only`, `scanner`), the fallback reason, and degraded/unparsed files, in both outputs. Never present partial extraction as complete. |
| R10 | Read target specifics from `.visualise-diffs.yml`: export macros to blank, include/exclude globs, project list, package-pin rules, caps, renderer switch. |
| R11 | Blank configured export/annotation macros in a length-preserving way before the C++ syntax parse, so source spans stay valid. |
| R12 | Build C# file lists from `.csproj` compile items (SDK-style and legacy), not directory scans. Compile the C# with Roslyn against target-framework reference assemblies (no MSBuild, no Visual Studio). Fall back to syntax-only when references cannot be resolved, and report the unresolved-symbol count. |
| R13 | Scan C++/CLI headers for `ref class`, `value class`, `interface class`, `enum class` and `gcroot<>` members, and model them in a separate lane. |
| R14 | When a configured package-pin file changes a pinned version, show a `package updated: name old → new` node. (Phase 2: diff the package's types.) |
| R15 | Produce byte-identical output for identical inputs. All collections are ordered canonically; volatile metadata is kept in a separate section. |
| R16 | Run as a separate Ubuntu job finishing in under 20 minutes (target under 5), so it never extends the existing build's critical path. |
| R17 | Exit 0 whenever a diagram or summary was published, including degraded runs. Use non-zero exit codes only for tool failures. MVP never gates a PR. |
| R18 | (Increment 2) Upgrade C++ to compiler-accurate extraction where a compile DB can be produced (MSBuild `GetClCommandLines` → clang-cl), keeping the same model contract. |
| R19 | (Phase 2) Extract ObjC/ObjC++ (xcodebuild `-verbose` log → compile DB → libclang on a macOS agent; tree-sitter-objc fallback), and link C++ interfaces to their frontend implementers. |

## Architecture

```
          ADO PR job (ubuntu)
                 │
        ┌────────┴─────────┐
        │  git layer (U2)  │  base/head, changed files, renames, hunks, worktrees
        └────────┬─────────┘
   base tree     │     head tree
        ┌────────┴──────────────────────────────┐
        │ extractors → vdiff-model/v1 JSON       │
        │  C++ syntax (U3) · C# Roslyn (U4)      │
        │  C++/CLI scanner (U5)                  │
        └────────┬──────────────────────────────┘
        ┌────────┴─────────┐
        │ differ (U6)      │  identity, rename, member/edge/body states
        └────────┬─────────┘
        ┌────────┴─────────┐
        │ scoper (U7)      │  changed + 1-hop, caps, overflow, summary
        └────────┬─────────┘
        ┌────────┴─────────┐
        │ renderers (U8)   │  Mermaid · PNG · HTML
        └────────┬─────────┘
        ┌────────┴─────────┐
        │ ADO publisher(U9)│  sticky thread · artifact · pin-bump node
        └──────────────────┘
```

**Stack:** a C#/.NET 10 CLI (`vdiff`) for orchestration, differ, scoper, renderers and
publisher, with Roslyn in-process. The reasons:

- C# semantics need Roslyn, which runs only on .NET.
- The C++ paths (tree-sitter now, clang-cl later) are external processes behind a JSON
  contract. That removes the in-process-libclang reason `architecture_analyser` gave for
  choosing Rust.

The C++ syntax extractor's packaging is decided by spike S2, which picks between a .NET
tree-sitter binding and a small native sidecar. Either way it emits `vdiff-model/v1`.

**Flip condition:** if increment 2 needs a custom libclang walker, re-evaluate. ClangSharp
is the .NET option; the `clang` crate is the Rust one.

**Model contract (`vdiff-model/v1`):**

- **Types:** `id`, `lang`, `lane`, `kind`, `qualifiedName`, `file`, `span`, `modifiers`,
  `bases[]`, `members[]`.
- **Members:** `name`, `kind`, `signature`, `visibility`, `static`, `virtual`/`override`,
  `type`, `span`.
- **Derived edges:** `inherits`, `implements`, `association` (from field/property types).
- **Run data:** `extraction` (mode, reason, degraded files) and `run_metadata`.

**Reuse from `architecture_analyser`:** take the ideas, not a code dependency.

- the status discipline (complete/degraded/unsupported);
- canonical ordering and determinism;
- rename-candidate scoring (kind, signature, namespace, path);
- bounded subprocess handling.

## Implementation units

**S0–S3 are spikes.** They are short and time-boxed, and their results can change
U3/U8/U9.

| Unit | Work | Done when |
|---|---|---|
| **S1** | Post the sample `classDiagram` with `classDef` on a scratch PR in the target org (a manual step by the PO) | Mermaid renders yes/no in comments and descriptions; R6 default set |
| **S2** | Prototype the C++ tree-sitter extractor in two ways: a .NET binding, and a native sidecar (Rust `tree-sitter` 0.27 + `tree-sitter-cpp`). Compare packaging size, Linux agent install, speed, and the effort of macro blanking | Pick one, recorded as ADR-001 |
| **S3** | Confirm that Roslyn compiles against reference assemblies on Linux for the target framework (including legacy .NET Framework). Use the `Microsoft.NETFramework.ReferenceAssemblies` packages | Type count matches the private Windows baseline within ±2% |
| **U1** | Define the model JSON schema. Build the golden-fixture harness: given base/head plus config, compare against an expected `diff.json` and expected Mermaid. Add synthetic public fixtures (one each: C++ small, C#, mixed + C++/CLI, extract-class refactor, rename). Document the private fixture recipe | Harness runs in CI and the 5 synthetic fixtures are red |
| **U2** | Git layer: merge-base resolution from ADO variables (`System.PullRequest.*`), `git worktree` for base, changed files, rename pairs with similarity, and hunks mapped to file spans | Unit tests on a scripted repo |
| **U3** | C++ syntax extractor: classes/structs/enums, nested types, bases, members with access sections, templates kept as names, length-preserving macro blanking, include/exclude globs | Synthetic C++ fixtures extract correctly; private sample ≥95% members correct |
| **U4** | C# extractor: csproj compile items, Roslyn compilation, types/members/accessibility/partials/generics; syntax-only fallback with the unresolved count | Synthetic C# fixtures pass; deterministic output |
| **U5** | C++/CLI scanner lane (R13) | Mixed fixture shows the wrapper type in its own lane |
| **U6** | Differ: identity by `lang + qualifiedName`, member identity by name + normalised signature; rename matching (R3); body-changed from hunks; edge diff | All 5 synthetic fixtures' `diff.json` green |
| **U7** | Scoper: 1-hop neighbourhood, ranking (changed > public API > neighbours), cap + overflow, text-summary degrade, pin-bump node (R14) | Fixtures match expected node sets; a 600-type change degrades to a summary |
| **U8** | Renderers: Mermaid (within 50k characters / 500 edges), PNG (mermaid-cli), self-contained HTML (inline Mermaid/elk, search, per-language tabs, mode banner) | Golden Mermaid matches; HTML opens offline |
| **U9** | ADO publisher: find/update the sticky thread by marker, attach the PNG if configured, link the artifact. Includes a dry-run mode that writes `comment.md` locally | Works against a scratch PR (manual) |
| **U10** | `azure-pipelines` template (separate Ubuntu job, PR trigger), `.visualise-diffs.yml` reference, README | A trial run on the target PR pipeline finishes in under 5 min |

**Increment 2 (after MVP acceptance):**

- compile DB via `GetClCommandLines` on a Windows agent;
- a clang-cl C++ extractor (decide among clang-uml JSON, `-ast-dump=json` filtered, and
  ClangSharp);
- a per-file mode upgrade in the same model.

**Phase 2:** ObjC/ObjC++, interface-based bridge linking, package type diff.

## Acceptance

1. All 5 synthetic fixtures are green in this repo's CI.
2. All 5 private fixtures (the shapes are small C++, C#, cross-stack, extract-class
   refactor, rename) match their expected diagrams in a run inside the target org. The
   following are allowed in syntax-only mode, as long as the run is labelled:
   - free functions shown as notes;
   - call edges omitted.
3. Identical inputs produce byte-identical outputs.
4. Over 10 consecutive target PR runs: no run extends the PR build's wall-clock, and the
   P90 job time is under 5 min.

## Risks

| Risk | Mitigation |
|---|---|
| Mermaid does not render in ADO PR comments | S1 first; PNG fallback (R6) |
| Syntax-only C++ misses macro-generated or template-heavy types | Configurable macro blanking; mode banner; increment 2 |
| Engine changes arrive as dependency pin bumps | Pin-bump node (R14); package diff in phase 2 |
| Large refactors make unreadable diagrams | Caps, overflow, text summary, full HTML artifact |
| Leaking target internals into this public repo | Config in the target repo; private fixtures; review every commit for names |
