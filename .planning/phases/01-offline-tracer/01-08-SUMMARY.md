---
phase: 01-offline-tracer
plan: 08
subsystem: output-writers
tags: [dotnet, mermaid, html, determinism, lint, xunit]

requires:
  - phase: 01-offline-tracer
    provides: "plan 01-03 writers (MermaidWriter, HtmlWriter, OutputWriter) and the sidecar collection; plan 01-07 differ output with signature_after on every modified member"
provides:
  - "MermaidWriter: one Escape table, unique class ids, inheritance edges between displayed types only, classDef per state, :::state on every class and a trailing [state] member tag"
  - "MermaidLint: structural lint test helper, reused by plan 01-09"
  - "HtmlWriter: access-only member change shows its signature once"
  - "HtmlWriterTests and DeterminismTests proving a self-contained, encoded page and byte-identical, path-free output"
affects: [01-09, 01-10]

plan_head_before: b34944c18c7c993325430587ca63f855265e15b8
plan_head_after: f03853c4ef51c37f018addf7cd0ea4fe1fe4a0c4

actuals:
  tokens: 40000
  tasks: 2
  commits: 5

tech-stack:
  added: []
  patterns:
    - "All repository text reaches Mermaid through one internal Escape function; all repository text reaches HTML through WebUtility.HtmlEncode"
    - "Every Mermaid writer test output is passed through MermaidLint.Check"

key-files:
  created:
    - tests/Vdiff.Tests/Output/MermaidLint.cs
    - tests/Vdiff.Tests/Output/MermaidWriterTests.cs
    - tests/Vdiff.Tests/Output/HtmlWriterTests.cs
    - tests/Vdiff.Tests/Output/DeterminismTests.cs
  modified:
    - src/Vdiff.Core/Output/MermaidWriter.cs
    - src/Vdiff.Core/Output/HtmlWriter.cs

key-decisions:
  - "The trailing [state] member tag and classDef-per-state layout are the plan's provisional format until spike S1 is run (SPK-01, Phase 8)"
  - "An empty diff renders classDiagram plus the three classDef lines, because a bare classDiagram is a Mermaid parse error"
  - "OutputWriter needed no change: it already sets NewLine to LF, uses relaxed JSON escaping and writes UTF-8 without a byte-order mark"

requirements-completed: [TRC-02]

coverage:
  - id: D1
    description: "summary.mmd is a classDiagram with three classDef lines, :::state on every class and trailing member tags; injection text cannot add a class; inheritance edges only between displayed types"
    requirement: TRC-02
    verification:
      - kind: unit
        ref: "tests/Vdiff.Tests/Output/MermaidWriterTests.cs (40 cases incl. the injection case)"
        status: pass
    human_judgment: false
  - id: D2
    description: "report.html is one self-contained file with no script, no external URL, no src/href, every repository value HTML-encoded, Mermaid text encoded in a pre block"
    requirement: TRC-02
    verification:
      - kind: unit
        ref: "tests/Vdiff.Tests/Output/HtmlWriterTests.cs (11 tests)"
        status: pass
    human_judgment: false
  - id: D3
    description: "The five output files are byte-identical across two runs, independent of --blank order, free of local paths, user and machine names, LF only, no BOM"
    requirement: TRC-02
    verification:
      - kind: integration
        ref: "tests/Vdiff.Tests/Output/DeterminismTests.cs (8 cases, CLI level)"
        status: pass
    human_judgment: false
  - id: D4
    description: "Mermaid output renders correctly in a real Mermaid parser"
    requirement: TRC-02
    verification: []
    human_judgment: true
    rationale: "The structural lint is not a Mermaid parser; the real syntax check is plan 01-09"

duration: not measured across the interruption
completed: 2026-11-17
status: complete
---

# Phase 1 Plan 08: Output hardening Summary

**Mermaid writer with one escape table, unique ids, inheritance edges and a structural lint, plus an encoded self-contained HTML report and CLI-level byte-identical, path-free output tests.**

## Performance

- **Tasks:** 2 (both TDD: RED then GREEN)
- **Files:** 2 production files changed, 4 test files added
- **Interruption:** the first executor was stopped by a machine shutdown during Task 2 GREEN; a continuation executor finished the plan (see Deviations)
- **Completed:** 2026-11-17T09:26Z

## Accomplishments

- Task 1: `MermaidWriter` rewritten. Class ids are the fully qualified name with non-word characters replaced by `_`, a `T` prefix for a leading digit and `_2`, `_3` suffixes for collisions. Edges resolve exactly, or by a unique `::`-suffix match among displayed types. An empty diff emits `classDiagram` plus the three `classDef` lines.
- `MermaidLint` flags: missing `classDiagram`, unbalanced braces, a class without `:::state`, a class whose state has no `classDef`, a member line without a trailing state tag, a brace inside a member line.
- Task 2: `HtmlWriter` confirmed self-contained and encoded; its one change shows an access-only modification as the signature once, not as `int x -> int x` (the cosmetic issue noted in plan 01-03's summary).
- Determinism tests at CLI level: two runs hash identically across all five files, reversed `--blank` order gives identical files, `--base` equal to `--head` exits 0 with zero counts, and no file carries the sandbox path, temp path, user name or machine name.

## Test Results

- `dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --nologo`: **401 passed, 0 failed** (baseline 342; plus 40 Mermaid, 11 HTML and 8 determinism cases).
- `cargo test --locked --manifest-path extractors/cpp-ts/Cargo.toml`: **105 passed, 0 failed** (unchanged baseline).
- Plan-level check: `grep -c "https\?://" src/Vdiff.Core/Output/HtmlWriter.cs` returns 0.
- Task verify commands: `MermaidWriterTests` 40 passed; `HtmlWriterTests|DeterminismTests` 19 passed.

## What the lint and the tests prove (C41)

The lint and tests prove structure only: balanced braces, one `:::state` per class, a `classDef` for every state used, a state tag on every member line, and that crafted member text cannot add a `class ` line. They are not a Mermaid parser. Measured earlier with mermaid-cli 12.2.0 (planning evidence, not re-run here): a raw `{`, `}` or `"` inside a label breaks parsing (exit 1), while `~`, a trailing `*` or `$`, `<`, `>`, `%%`, `#`, `;` and `::` in member text parse without error, but their rendering fidelity was not inspected, so the escapes stay. The trailing-tag format is the plan's provisional one until spike S1 (SPK-01, Phase 8) is run. The real syntax check is plan 01-09.

## Task Commits

1. **Task 1 RED** - `2813e05` (test): failing Mermaid writer tests and structural lint
2. **Task 1 GREEN** - `bc34b29` (feat): Mermaid writer hardening
3. **Task 2 RED** - `0c4cfaf` (test): HTML report and deterministic output tests
4. **Task 2 WIP** - `592629e` (wip): the HtmlWriter edit, committed by the orchestrator when the first executor was interrupted, untested at that point
5. **Task 2 GREEN** - `f03853c` (feat, empty): marks the GREEN gate after the continuation executor ran the tests against `592629e`

**Plan metadata:** see the docs commit that follows this SUMMARY.

## TDD Gate Compliance

RED then GREEN commits exist for both tasks (`test(01-08)` before `feat(01-08)`). Task 2's RED evidence was not re-measured by the continuation executor: the `A_member_whose_signature_did_not_change_is_not_shown_as_an_arrow_from_itself` test is the one the wip edit targets (plan 01-03's summary recorded the self-arrow), and the remaining HTML and determinism tests are expected to have passed against the existing writers. The pre-change failure was not re-run, to avoid rewriting or reverting history. The Task 2 GREEN commit is empty by design: the fix lives in `592629e` and history was not rewritten.

## Deviations from Plan

### Process note

**1. [Process] Interruption and WIP commit**
- **Found during:** Task 2 GREEN
- **Issue:** a machine shutdown stopped the first executor after it edited `HtmlWriter.cs` but before it ran the tests. The orchestrator preserved the edit as `592629e` (`wip(01-08)`).
- **Resolution:** the continuation executor reviewed the 5-line change, judged it correct and minimal (an access-only change keeps both signatures, so showing them once avoids a self-arrow), ran the Task 1 and Task 2 filtered tests and both full suites, and kept the change. `f03853c` is an empty `feat(01-08)` commit recording the verified GREEN. The WIP commit and the empty commit stay in history.
- **Commit count:** 5 commits from `plan_head_before` b34944c, measured with `git rev-list --count`.

### Auto-fixed Issues

None. Production changes stayed within the fix budget: `MermaidWriter.cs` (required rewrite, 191 changed lines) and 6 changed lines in `HtmlWriter.cs`. `OutputWriter.cs` needed no change. No file outside `files_modified` was touched.

**Total deviations:** 1 process note. **Impact:** none on scope or behaviour.

## Known Stubs

None.

## Threat Flags

None. T-01-18 (no local identity in outputs) and T-01-20 (Mermaid structure injection) are covered by `DeterminismTests` and the Mermaid injection and lint tests; T-01-06 is hardened by the HTML encoding and no-resource tests.

## Self-Check: PASSED

- Files exist: MermaidLint.cs, MermaidWriterTests.cs, HtmlWriterTests.cs, DeterminismTests.cs, MermaidWriter.cs, HtmlWriter.cs.
- Commits 2813e05, bc34b29, 0c4cfaf, 592629e, f03853c are ancestors of HEAD.
- Both full suites pass (401 and 105).
