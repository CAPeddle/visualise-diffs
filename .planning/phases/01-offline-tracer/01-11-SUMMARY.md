---
phase: 01-offline-tracer
plan: 11
subsystem: output
tags: [mermaid, class-diagram, csharp, xunit, gap-closure]

requires:
  - phase: 01-offline-tracer
    provides: MermaidWriter, MermaidLint, mermaid-cli syntax check (plans 01-08, 01-09)
provides:
  - Every Mermaid class id vdiff writes is prefixed with C_, so repository type names cannot yield a keyword id
  - End-to-end test through the real mermaid-cli parser for keyword-named global types
  - Lint rule that rejects an unprefixed class id or edge endpoint
affects: [01-12, phase-01 verification, Mermaid output format (S1 spike)]

actuals:
  tokens: 3000
  tasks: 2
  commits: 3

commits: 3
plan_head_before: 588e1b414611cb3d503e590aeba33d5b34fcaa6a
plan_head_after: 47402edb7082275a6886f128028a2d97e2713cd2

tech-stack:
  added: []
  patterns:
    - "Prefix every generated identifier instead of deny-listing keywords of a pinned third-party grammar"

key-files:
  created: []
  modified:
    - src/Vdiff.Core/Output/MermaidWriter.cs
    - tests/Vdiff.Tests/Output/MermaidCliSyntaxTests.cs
    - tests/Vdiff.Tests/Output/MermaidWriterTests.cs
    - tests/Vdiff.Tests/Output/DeterminismTests.cs
    - tests/Vdiff.Tests/Output/MermaidLint.cs

key-decisions:
  - "Prefix every class id with C_ (total) rather than deny-list the six measured keywords, because the keyword set belongs to the pinned Mermaid version and may grow"
  - "Removed the empty-name and leading-digit branches of ClassId: unreachable once the prefix guarantees a valid identifier start"

requirements-completed: [TRC-02]

duration: 25min
completed: 2026-11-17
status: complete
---

# Phase 1 Plan 11: Keyword-safe Mermaid class ids Summary

**Every Mermaid class id and inheritance-edge endpoint is now `C_` plus the sanitised fqn, so a global C++ type named link, note, click, callback, href or cssClass no longer makes summary.mmd unparseable; proven through the real mermaid-cli check.**

## Performance

- **Duration:** about 25 min (most of it two full test-suite runs of 7 to 8 minutes each)
- **Started:** 2026-11-17T12:54Z (approx.)
- **Completed:** 2026-11-17T13:19Z
- **Tasks:** 2
- **Files modified:** 5

## Accomplishments
- Closed verification gap 1 (finding CR-01): `MermaidWriter.ClassId` builds `C_` + sanitised fqn; the readable fqn stays in the class label. `AppendClass` and `Edges` needed no change because both read the shared ids list.
- New end-to-end test `Writer_output_for_keyword_named_global_types_passes_the_real_syntax_check`: a header with six keyword-named global structs and one deriving from `link` produces `class C_<name>["<name>"]:::added` lines and `C_link <|-- C_linked`, and `check.mjs` exits 0.
- Unit coverage: a six-case theory over the keyword names, a keyword-named base on an edge, and a property test reading back every class id and edge endpoint of a mixed diff and asserting the `C_` prefix (with a minimum-count guard so an empty read cannot pass).
- `MermaidLint` now requires the `C_` prefix on class ids and edge endpoints; two new broken fixtures (bare keyword id, unprefixed edge endpoint) are rejected, and the old A/B fixtures were moved to `C_A`/`C_B`.

## TDD record

- **RED (Task 1):** `Writer_output_for_keyword_named_global_types_passes_the_real_syntax_check` run before the writer change failed on its planned assertion: `Assert.Contains() Failure: Sub-string not found ... Not found: "class C_link["link"]:::added"` (the writer emitted bare `class callback[...]` first). That assertion precedes the check call, so the check exit code was recorded separately: the same unprefixed shape (`class link["link"]:::added` plus an edge) run through `tools/mermaid-check/check.mjs` exited 1 with `Parse error on line 2 ... got 'LINK'`. semanticAssessment: the target test executed and failed for the intended reason (unprefixed keyword id), not a build, fixture or discovery fault. The gsd `check tdd-red-evidence` classifier was not run (no `gsd_run` in this environment, and xUnit console output is not one of its supported formats).
- **RED (Task 2):** with the new fixtures in place and the old lint, `The_lint_rejects_hand_made_broken_input_and_names_the_problem` failed with `the lint accepted: bare keyword class id`.
- **GREEN:** the `ClassId` change (Task 1) and the lint change (Task 2) made them pass.
- The three new unit tests added in Task 2 pass on the Task 1 writer change by design (writer was already fixed); the lint fixture was the Task 2 red step.

## Task Commits

1. **Task 1: keyword-named global types pass the real syntax check, C_ prefix** (tracer, tdd)
   - `1281ba9` test(01-11): failing end-to-end test (RED)
   - `74f66ac` feat(01-11): prefix every Mermaid class id with C_ (GREEN, plus the writer and determinism assertions moved to the prefixed form)
2. **Task 2: unit coverage for keyword ids and a lint that requires the prefix** - `47402ed` (test)

**Plan metadata:** committed separately (docs: complete plan).

Tracer gate: the tracer verify (new end-to-end test, `Category=MermaidCli`, writer, determinism and HTML suites) passed in the full run before Task 2 started; expansion continued.

## Verification

- Task 1 verify commands: new end-to-end test 1 of 1 passed; `Category=MermaidCli` all passed (6 tests, up from 5); writer, determinism and HTML suites passed.
- Task 2 verify commands: keyword theory `Passed: 6`; property + keyword-base + lint-fixture tests `Passed: 3`.
- Plan-level: `dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --nologo` after the final commit: **430 passed, 0 failed** (421 at verification time, 422 after Task 1, +8 new tests in Task 2: 6 theory cases, 1 base-edge test, 1 property test).
- Acceptance greps: `"C_"` present in non-comment writer code (1), `C_shop__Gadget <|-- C_shop__Widget` in DeterminismTests (1), `cssClass` in both new test files, `C_[A-Za-z0-9_]` in MermaidLint (1). All five touched files have 0 CR characters (LF only).
- Target-name guard (`check-public-diff.sh`, private, outside the repo) ran clean on each staged diff before all three commits.
- `cargo test` not run: the sidecar crate is untouched, as the plan states.

## Files Created/Modified
- `src/Vdiff.Core/Output/MermaidWriter.cs` - `ClassId` emits `C_` + sanitised fqn; doc comment records why a prefix beats a deny-list
- `tests/Vdiff.Tests/Output/MermaidCliSyntaxTests.cs` - keyword-header end-to-end test through `check.mjs`
- `tests/Vdiff.Tests/Output/MermaidWriterTests.cs` - id form updated; keyword theory, keyword-base edge test, all-ids-prefixed property test; two new lint fixtures
- `tests/Vdiff.Tests/Output/DeterminismTests.cs` - edge assertion uses prefixed ids
- `tests/Vdiff.Tests/Output/MermaidLint.cs` - id pattern now `C_[A-Za-z0-9_]*` for class lines and edge endpoints; header comment explains why

## Decisions Made
- Prefix every id (`C_`) instead of a keyword deny-list: total by construction and independent of future Mermaid upgrades (decision recorded in the plan, followed as written).
- The empty-name (`_`) and leading-digit (`T`) branches were removed rather than kept as dead code; the id tests now expect `C___a` for `::a` and `C_9lives` for `9lives`.

## Deviations from Plan

None - plan executed exactly as written. Two procedural notes, neither a code deviation:
- Task 1 produced two commits (test then feat) instead of one, following the TDD reference's RED/GREEN commit scope for a `tdd="true"` task; the plan's "only these four files" acceptance still holds.
- The RED run failed on the prefix `Assert.Contains` before reaching the check call, so the check's exit code 1 was recorded by running `check.mjs` directly on the equivalent unprefixed diagram (see the TDD record).

## Issues Encountered
- A malformed throwaway shell command (an empty Python heredoc fallback) started a Python REPL that looped writing to a background task output file and reached roughly 690 MB. It was my own process; it was identified by creation time and command line, terminated, and its output file deleted. It touched nothing in the repository.

## Known Stubs

None.

## Threat Flags

None. No new network endpoint, auth path, file access pattern or schema change. T-01-34 and T-01-35 are mitigated as planned.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Gap 1 of `01-VERIFICATION.md` is closed at code and test level. Plan 01-12 (gap 2, aliased `--out`) is independent and may now run; it shares the test assembly build, so it should run after this plan as stated.
- The class-id and member-tag format remains provisional until spike S1.

---
*Phase: 01-offline-tracer*
*Completed: 2026-11-17*

## Self-Check: PASSED

- FOUND: src/Vdiff.Core/Output/MermaidWriter.cs, tests/Vdiff.Tests/Output/MermaidCliSyntaxTests.cs, tests/Vdiff.Tests/Output/MermaidWriterTests.cs, tests/Vdiff.Tests/Output/DeterminismTests.cs, tests/Vdiff.Tests/Output/MermaidLint.cs
- FOUND commits: 1281ba9, 74f66ac, 47402ed (all ancestors of HEAD; `git rev-list --count 588e1b4..HEAD` = 3)
