# visualise-diffs — requirements draft (gathering in progress)

Status: **draft**. Rounds 1–3 with the PO are done. Two items are open until the
target-repo investigation in [../prompts/target-repo-investigation.md](../prompts/target-repo-investigation.md)
reports back: the tool's stack and how C++ build context is obtained.

## Problem

PR reviewers working on a product with a C++ core, a C# frontend and a macOS Cocoa
frontend cannot see the structural impact of a change in the file-by-file diff.

- **P1 — Large PRs are unreadable.** You cannot tell which types actually changed shape.
- **P2 — Cross-stack ripple.** C++ core changes propagate through the C API/P-Invoke,
  C++/CLI/SWIG and ObjC++ wrappers, and reviewers miss it.
- **P3 — Inheritance and API drift.** New bases and changed virtuals or public surface
  go unnoticed.

The primary user is the **PR reviewer**. Success means a reviewer understands a PR's
structural change from the diagram without reading every file.

## Decisions so far

| # | Decision | Source |
|---|---|---|
| D1 | The audience is PR reviewers, for review comprehension | PO round 1 |
| D2 | Pains in scope: P1, P2, P3 (onboarding is out of scope) | PO round 1 |
| D3 | Bridges in use: flat C API + P/Invoke, C++/CLI or SWIG, ObjC++ (.mm) wrappers. No Swift interop | PO round 1 |
| D4 | Delivery: a compact Mermaid/PNG summary in a sticky PR comment **and** a full HTML/SVG report as a pipeline artifact | PO round 2 |
| D5 | Extraction is hybrid: syntax-only by default, compiler-accurate where build context exists, with the mode reported per language | PO round 2 |
| D6 | Diagram scope: changed types + 1-hop neighbours (bases, derived types, associated types), shown greyed | PO round 2 |
| D7 | MVP languages: C++ and C#. ObjC/ObjC++ is phase 2 | PO round 3 |
| D8 | Granularity: types, members (incl. signature/visibility/virtual) and edges (inheritance, association) | PO round 3 |
| D9 | Builds: Windows uses MSVC/MSBuild `.vcxproj` (no compile_commands.json); Mac uses Xcode | PO round 3 |
| D10 | Cross-stack bridge linking is phase 2 | PO round 3 |
| D11 | MVP C++ is syntax-only (tree-sitter + export-macro blanking) on Ubuntu. Compiler-accurate C++ (MSBuild `GetClCommandLines` → clang-cl, measured at 99% of TUs clean) is increment 2 | PO round 4, after the target findings |
| D12 | MVP scope is the main product repo only. A changed dependency-package pin is shown as a "package updated old→new" node. Diffing the package's types is phase 2 | PO round 4 |
| D13 | C++/CLI is in the MVP as a header-scanner "managed wrapper" lane | PO round 4 |
| D14 | Tool stack is a C#/.NET orchestrator with Roslyn in-process. The C++ extractor sits behind a JSON model contract, and spike S2 picks its packaging. Flip condition: increment 2 needs a custom libclang walker | Target findings §H |
| D15 | This repo is public, so all target specifics go in `.visualise-diffs.yml` in the target repo and in private fixtures | Repo visibility check |

The MVP plan is in [../plans/2026-11-16-001-feat-mvp-diff-class-diagram-plan.md](../plans/2026-11-16-001-feat-mvp-diff-class-diagram-plan.md).

## Draft MVP requirements

- **R1.** Given a PR (base and head commit), the tool shall extract a class model for
  C++ and C# at both commits and compute a diff. The diff tags each type, member and
  edge as added, removed, modified or unchanged.
- **R2.** The tool shall match renamed or moved types and members as rename candidates
  rather than as a remove plus an add.
- **R3.** The tool shall render the changed types plus 1-hop neighbours as a class
  diagram, with distinct styling for each change state.
- **R4.** The tool shall post or update a single sticky PR thread containing a summary
  diagram that stays within the renderer's limits. Mermaid's defaults are 50,000
  characters and 500 edges. When the limits are exceeded, the summary groups types per
  namespace or project and shows "N more…".
- **R5.** The tool shall publish a full HTML/SVG report as a pipeline artifact and
  link to it from the PR thread.
- **R6.** The tool shall state the extraction mode for each language (compiler-accurate
  or syntax-only) and any degraded files in both outputs. It shall never present
  partial extraction as complete.
- **R7.** The tool shall run as an Azure DevOps pipeline step within a time budget.
  The budget is **TBD**, set by investigation section F5.
- **R8.** Output shall be deterministic for identical inputs.

## Open items: status after the target investigation

- **O1:** resolved for increment 2 (D11).
- **O2:** resolved (D14).
- **O3:** still open; spike S1.
- **O4:** resolved (D13).
- **O5:** resolved. A separate Ubuntu job avoids agent contention.
- **O6:** resolved. Cap at about 25 nodes; job time under 20 min, target under 5.

The original wording of each item is kept below.

- **O1.** How to get compiler-accurate C++ from MSVC/MSBuild: Clang Power Tools,
  a binlog-to-compile-DB conversion, or a design-time build. Also the clang-cl parse
  fidelity this yields.
- **O2.** Tool stack: C#/.NET or Rust. See "Research" below.
- **O3.** Does Mermaid render in ADO PR *comments* in this org? Documentation only
  confirms the wiki, .md file previews, and fenced blocks as of Sprint 274. If it does
  not render, fall back to a PNG uploaded through PR attachments.
- **O4.** How to present C++/CLI, which neither Roslyn nor clang parses.
- **O5.** Agent pools (Windows/macOS/Linux) and whether LLVM is installed on them.
- **O6.** Time budget and diagram size budget, from PR statistics.

## Research summary

### Sibling repo `architecture_analyser` (Rust, MIT)

- **Already has:**
  - libclang C++ and Roslyn C# extraction;
  - hashed native-ID identity (USR / DocumentationCommentId);
  - a snapshot diff with rename-candidate matching;
  - complete/degraded/unsupported status and an exit-code model.
- **Lacks:**
  - fields, access specifiers, templates, a containment edge and an edge diff;
  - ObjC;
  - any diagram output.
- **Why it uses Rust (ADR 0001 §1):** in-process libclang via the `clang` crate,
  single-binary CI deployment, deterministic serde output. That reason holds only if
  we write our own libclang walker.
- **Worth reusing whatever the stack:** the identity scheme, the status discipline,
  rename scoring, and the bounded subprocess handling.

### Open-source tools

- **clang-uml** (Apache-2.0, 0.6.3, active): C++ and ObjC class model as JSON,
  PlantUML or Mermaid. Requires a compile DB. Has no diff mode, so we diff its JSON.
- **Roslyn:** C# extraction, recommended over third-party PlantUML generators.
- **tree-sitter** (cpp, c-sharp, objc, swift): the no-build fallback lane.
  stack-graphs was archived in September 2025.
- **Rendering:** Mermaid for the inline summary (class-level styling only, so member
  changes are shown with markers). PlantUML/Graphviz/elkjs for the full SVG/HTML.
- **Prior art (UX reference only):** CodeBoarding-action (an LLM-driven,
  component-level Mermaid diff comment), delta-typescript-graph-action, Striffs.
- **AST diff tools** (GumTree, difftastic): not a fit. Diffing two extracted models is
  simpler and more robust.

### Top technical risks

1. **C++ build context on MSVC.** Needs a compile DB, clang-cl compatibility,
   generated headers and PCH.
2. **ObjC on non-macOS agents.** The macOS SDK headers are not available there.
3. **Diagram size explosion on large PRs.**
