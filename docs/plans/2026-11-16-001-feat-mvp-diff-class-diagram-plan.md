---
title: visualise-diffs MVP: a PR's diff rendered as a class diagram
status: proposed
date: 2026-11-16
requirements: docs/requirements/requirements.md
---

# visualise-diffs MVP plan

All requirement IDs (R*), decision IDs (D*) and acceptance criteria come from
[../requirements/requirements.md](../requirements/requirements.md). This plan adds the
architecture, the order of work, and the definition of done for each unit.

**This repository is public (D15).** Synthetic fixtures live here. Real fixtures and every
target-specific setting stay in the target organisation.

## MVP in one paragraph

A separate Ubuntu job runs on every non-draft PR (R4, R26). It:

1. extracts a model of the base and head commits:
   - C++ syntax-only with tree-sitter (R5);
   - C# with Roslyn, falling back to syntax-only (R6, R7);
   - C++/CLI with a scanner (R8);
2. diffs the two models and matches renames (R13, R14);
3. selects up to 25 nodes (the changed types plus greyed neighbours), or degrades to a
   text summary (R18, R19);
4. publishes the result:
   - a Mermaid summary in one sticky PR thread, or a PNG if Mermaid does not render
     there (R20, R21, R24);
   - a full offline HTML report as an artifact (R22).

The job labels every extraction mode (R9) and never blocks the PR (R27).

## Architecture

```
          ADO PR job (ubuntu; windows fallback per D18)
                 │
        ┌────────┴─────────┐
        │  git layer (U2)  │  head, merge-base, changed files, renames, hunks, worktree
        └────────┬─────────┘
   base tree     │     head tree
        ┌────────┴──────────────────────────────┐
        │ extractors → vdiff-model/v1 (R10)      │
        │  C++ tree-sitter (U3) · C# Roslyn (U4) │
        │  C++/CLI scanner (U5)                  │
        └────────┬──────────────────────────────┘
        ┌────────┴─────────┐
        │ differ (U6)      │  states, rename scoring, body-changed, pin node
        └────────┬─────────┘
        ┌────────┴─────────┐
        │ selector (U7)    │  1-hop, cap 25, ranking, overflow, text degrade
        └────────┬─────────┘
        ┌────────┴─────────┐
        │ renderers (U8)   │  Mermaid · PNG · HTML
        └────────┬─────────┘
        ┌────────┴─────────┐
        │ publisher (U9)   │  sticky thread · artifact · dry-run
        └──────────────────┘
```

**Stack (D14):** a C#/.NET 10 CLI named `vdiff`, with Roslyn in-process. The C++
extractor sits behind the model contract, so how it is packaged (a .NET tree-sitter
binding or a native sidecar) is decided by spike S2 without affecting anything else.

**Ideas reused from `architecture_analyser`:** these are design ideas, not a code
dependency.
- status discipline (complete, degraded, unsupported);
- canonical ordering for determinism;
- rename-candidate scoring;
- bounded subprocess handling.

## Spikes

Each spike is time-boxed to about one day and settles a decision before the units that
depend on it start.

| Spike | Question | Gates | Done when |
|---|---|---|---|
| **S1** | Does a Mermaid `classDiagram` with `classDef` render in Azure DevOps PR comments and descriptions? Which trailing member-tag format reads best (R20)? This is a manual test on a scratch PR in the target organisation, run by the PO or an agent there. | U8, U9 | O3 closed; PNG default set; tag format recorded |
| **S2** | Should the C++ tree-sitter extractor be a .NET binding or a native sidecar (Rust `tree-sitter` 0.27 with `tree-sitter-cpp`)? Compare package size, installation on a Linux agent, speed, and how easy macro blanking is. | U3 | ADR-001 recorded |
| **S3** | Does Roslyn compile the target's C# on Ubuntu against the target-framework reference assemblies (`Microsoft.NETFramework.ReferenceAssemblies` for legacy .NET Framework)? | U4, R26 runner choice | Type count within ±2% of the private Windows baseline, otherwise D18 Windows fallback chosen |

## Implementation units

| Unit | Work | Requirements | Done when |
|---|---|---|---|
| **U1** | Model schema plus a golden-fixture harness: given base, head and config, compare the output with the expected `diff.json` and expected summary. Add the five synthetic fixtures and document the recipe for private fixtures. | R10, R23, Acceptance 1–2 | Harness runs in CI; 5 fixtures red |
| **U2** | Git layer: read the PR variables, compute the merge-base, create a worktree for the base commit, list changed files, find rename pairs with similarity, and map hunks to spans. Skip draft and fork PRs. | R1, R4 | Unit tests on a scripted repo |
| **U3** | C++ syntax extractor: classes, structs, enums and nested types; bases; members with access sections; templates kept as names; macro blanking that preserves length; globs and default exclusions. | R2, R3, R5, R9 | Synthetic C++ fixtures pass; private sample ≥95% members correct |
| **U4** | C# extractor: read `.csproj` compile items, build the Roslyn compilation, extract types, members, accessibility, partials and generics; fall back to syntax-only and report the unresolved count. | R6, R7, R9 | Synthetic C# fixtures pass; deterministic |
| **U5** | C++/CLI scanner lane. | R8 | The cross-stack fixture shows the wrapper type in its own lane |
| **U6** | Differ: assign states, score renames, mark body-changed members, diff edges, emit the package-pin node. | R13–R15 | All 5 fixtures' `diff.json` green |
| **U7** | Selector: 1-hop neighbourhood, 25-node cap that includes neighbours, ranking, overflow nodes, degrading to a text table. | R18, R19 | Expected node sets match; a 600-type change degrades to text |
| **U8** | Renderers: Mermaid (with the S1 tag format), PNG (mermaid-cli), and a self-contained HTML report. | R20–R22 | Golden summaries match; HTML opens offline |
| **U9** | Publisher: find or update the sticky thread by its marker, handle the no-change case, link the artifact, provide a dry-run mode. | R24, R25, R28 | Works against a scratch PR (manual) |
| **U10** | Pipeline template (separate job, `continueOnError`, 20-minute timeout), a `.visualise-diffs.yml` reference, and the README. | R26, R27 | Trial on the target PR pipeline: P90 under 5 min, no added wall-clock |

**Order:** S1, S2 and S3 run in parallel. Then U1 → U2 → (U3 ∥ U4 ∥ U5) → U6 → U7 → U8
→ U9 → U10.

## After the MVP

- **Increment 2 (R11):**
  - Clang is used for analysis only. The projects have no clang build support and none is
    added; they keep building with MSVC alone.
  - Make clang-cl available to the job on the Windows agents. Whether it is already
    installed is unverified; the LLVM that ships with Visual Studio is one option.
  - Verify clang-cl parse fidelity on every in-scope project. So far only one core
    project has been trialled.
  - Generate a compile database with a script that runs MSBuild `GetClCommandLines` and
    adds the include paths.
  - Build a clang-cl extractor. The options to decide between are clang-uml JSON,
    filtered `-ast-dump=json`, and ClangSharp.
  - Upgrade the mode file by file, inside the same model.
- **Phase 2 (R12, R16, R17):** ObjC/ObjC++, diffing the dependency package's types, and
  interface-based linking across language bridges.
