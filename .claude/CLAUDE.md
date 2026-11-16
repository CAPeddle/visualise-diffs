<!-- GSD:project-start source:PROJECT.md -->

## Project

**visualise-diffs**

An Azure DevOps pipeline tool (`vdiff`, a C#/.NET 10 CLI) that renders a pull request's
git diff as a class diagram for PR reviewers. It extracts a class model of the base and
head commits, diffs them, and shows the changed types plus their 1-hop neighbours: a
compact Mermaid (or PNG) summary in one sticky PR thread, and a full offline HTML report
as a pipeline artifact. The target product has a C++ core, a C# frontend and a macOS
Cocoa frontend.

**Core Value:** A reviewer understands a PR's structural change (which types, members and edges changed
shape) from the diagram, without reading every file.

### Constraints

- **Tech stack**: C#/.NET 10 CLI `vdiff`, Roslyn in-process; every extractor emits `vdiff-model/v1` JSON (D14)
- **C++ parsing**: tree-sitter syntax-only in the MVP; no home-grown parser (D11)
- **In-scope code**: builds only on Windows (.vcxproj) and macOS (Xcode) (D9)
- **Runner**: separate job; Ubuntu default with Windows fallback (D18) — see open question O4 below
- **Time**: 20-minute hard timeout, P90 under 5 minutes, never extends the PR build's wall-clock (R26)
- **Safety**: never block a PR; exit 0 on publish or skip, 1 on tool failure (R27)
- **Privacy**: no posting to ADO or external renderers with target content without explicit PO approval

<!-- GSD:project-end -->

<!-- GSD:stack-start source:STACK.md -->

## Technology Stack

Technology stack not yet documented. Will populate after codebase mapping or first phase.
<!-- GSD:stack-end -->

<!-- GSD:conventions-start source:CONVENTIONS.md -->

## Conventions

Conventions not yet established. Will populate as patterns emerge during development.
<!-- GSD:conventions-end -->

<!-- GSD:architecture-start source:ARCHITECTURE.md -->

## Architecture

Architecture not yet mapped. Follow existing patterns found in the codebase.
<!-- GSD:architecture-end -->

<!-- GSD:skills-start source:skills/ -->

## Project Skills

No project skills found. Add skills to any of: `.claude/skills/`, `.agents/skills/`, `.cursor/skills/`, `.github/skills/`, or `.codex/skills/` with a `SKILL.md` index file.
<!-- GSD:skills-end -->

<!-- GSD:workflow-start source:GSD defaults -->

## GSD Workflow Enforcement

Before using Edit, Write, or other file-changing tools, start work through a GSD command so planning artifacts and execution context stay in sync.

Use these entry points:
- `/gsd-fast` for a trivial task inline, with no subagents and no PLAN.md
- `/gsd-quick` for small fixes, doc updates, and ad-hoc tasks
- `/gsd-debug` for investigation and bug fixing
- `/gsd-execute-phase` for planned phase work

Do not make direct repo edits outside a GSD workflow unless the user explicitly asks to bypass it.
<!-- GSD:workflow-end -->

<!-- GSD:profile-start -->

## Developer Profile

> Profile not yet configured. Run `/gsd-profile-user` to generate your developer profile.
> This section is managed by `generate-claude-profile` -- do not edit manually.
<!-- GSD:profile-end -->
