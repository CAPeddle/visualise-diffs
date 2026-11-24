---
phase: 01-offline-tracer
plan: 09
subsystem: testing
tags: [mermaid-cli, puppeteer, npm, lockfile, node, xunit, syntax-check]

requires:
  - phase: 01-offline-tracer
    provides: "plan 01-03 package-checkpoint line (approved pins) and the sidecar collection; plan 01-06 richer extraction; plan 01-08 hardened MermaidWriter and MermaidLint"
provides:
  - "tools/mermaid-check: pinned, integrity-locked mermaid-cli install (12.2.0, puppeteer 25.17.0) reproducible with npm ci --ignore-scripts"
  - "check.mjs: local headless-browser Mermaid syntax check with browser detection, a raised maxTextSize/maxEdges config and distinct exit codes"
  - "MermaidCliSyntaxTests (Category=MermaidCli): real-parser proof on writer output, broken diagrams, over-50,000-character diagrams and the zero-change diagram"
affects: [01-10]

plan_head_before: 3e771fe44b075ae11a1f89c6ddf294482ef6f557
plan_head_after: 3f05cab82724becad800cc951c22db184f52d2ac

# Actuals: chars/4 over the hand-written files (check.mjs, package.json, the test file); the generated lockfile is excluded
actuals:
  tokens: 3350
  tasks: 2
  commits: 2

tech-stack:
  added: ["@mermaid-js/mermaid-cli 12.2.0 (exact)", "puppeteer 25.17.0 (exact, install scripts disabled, machine browser used)"]
  patterns:
    - "Config files for external tools are written with JSON.stringify into a fresh temp directory, forward-slash paths only"
    - "A missing prerequisite is a distinct exit code and a failing test with an install hint, never a skip"

key-files:
  created:
    - tools/mermaid-check/package.json
    - tools/mermaid-check/package-lock.json
    - tools/mermaid-check/check.mjs
    - tests/Vdiff.Tests/Output/MermaidCliSyntaxTests.cs
  modified: []

key-decisions:
  - "No change to MermaidWriter.cs: classDef, :::state and the label form class Id[\"label\"]:::state { all parse under mermaid-cli 12.2.0"
  - "check.mjs exit codes: 0 valid, 1 syntax error, 2 usage, 3 missing prerequisite (node packages or browser), 4 tool failure"

patterns-established:
  - "Large-diagram safety: the check passes a Mermaid config with maxTextSize 2000000 and a raised maxEdges so a diagram over 50,000 characters is parsed, not silently accepted"

requirements-completed: [TRC-02]

coverage:
  - id: D1
    description: "The Mermaid text vdiff writes passes a real mermaid-cli syntax check, including hard-case member text, and the check fails on a broken diagram"
    requirement: TRC-02
    verification:
      - kind: integration
        ref: "tests/Vdiff.Tests/Output/MermaidCliSyntaxTests.cs#Writer_output_for_a_header_of_hard_cases_passes_the_real_syntax_check"
        status: pass
      - kind: integration
        ref: "tests/Vdiff.Tests/Output/MermaidCliSyntaxTests.cs#A_broken_diagram_with_an_unbalanced_brace_fails_the_check_and_is_not_a_missing_prerequisite"
        status: pass
    human_judgment: false
  - id: D2
    description: "Diagrams over 50,000 characters are really parsed: a valid one passes and a broken one fails"
    requirement: TRC-02
    verification:
      - kind: integration
        ref: "tests/Vdiff.Tests/Output/MermaidCliSyntaxTests.cs#A_valid_diagram_over_fifty_thousand_characters_is_really_parsed_and_passes"
        status: pass
      - kind: integration
        ref: "tests/Vdiff.Tests/Output/MermaidCliSyntaxTests.cs#A_broken_diagram_over_fifty_thousand_characters_fails_so_the_text_size_limit_was_raised"
        status: pass
    human_judgment: false
  - id: D3
    description: "The zero-change diagram (classDiagram plus three classDef lines) passes the real check"
    requirement: TRC-02
    verification:
      - kind: integration
        ref: "tests/Vdiff.Tests/Output/MermaidCliSyntaxTests.cs#A_zero_change_diagram_of_classDiagram_and_three_classDef_lines_passes"
        status: pass
    human_judgment: false
  - id: D4
    description: "The npm install is reproducible from exact pins equal to the recorded package checkpoint, with a committed integrity-hashed lockfile and install scripts disabled"
    requirement: TRC-02
    verification:
      - kind: other
        ref: "npm --prefix tools/mermaid-check ci --ignore-scripts"
        status: pass
    human_judgment: false

duration: not timed (two executor sessions; the first was interrupted at the commit step)
completed: 2026-11-17
status: complete
---

# Phase 1 Plan 09: Local Mermaid Syntax Check Summary

**A pinned, integrity-locked mermaid-cli 12.2.0 install wrapped in `check.mjs`, with five tests proving vdiff's real Mermaid output parses and that the check fails on broken diagrams of any size.**

## Performance

- **Duration:** not timed; the work was done by two executor sessions and this one finished verification, the commits and the close-out
- **Completed:** 2026-11-17
- **Tasks:** 2
- **Files modified:** 4 created, 0 modified

## Accomplishments

- `tools/mermaid-check` installs `@mermaid-js/mermaid-cli` 12.2.0 and `puppeteer` 25.17.0 at exact versions (equal to the `package-checkpoint: approved` line in the plan 01-03 summary) with install scripts disabled. `npm ci --ignore-scripts` reproduces the install from the committed lockfile; `node_modules` is not tracked.
- `check.mjs` finds the machine's own Chrome, Chromium or Edge (or `VDIFF_MERMAID_BROWSER`), writes its Puppeteer and Mermaid configs with `JSON.stringify` into a fresh temp directory, runs mermaid-cli through `process.execPath` with no shell and a 120 s timeout, and deletes the temp directory. The Mermaid config raises `maxTextSize` to 2,000,000 and `maxEdges`, so a diagram over 50,000 characters is parsed instead of being reported valid unparsed (T-01-33). `--no-sandbox` is added only when `VDIFF_MERMAID_NO_SANDBOX` is set (T-01-22). Nothing is sent to any service (T-01-21).
- Exit codes: 0 valid, 1 syntax error, 2 usage, 3 missing prerequisite (installed packages or a browser), 4 tool failure. Exit 3 is distinct from a syntax failure.
- Five tests in `MermaidCliSyntaxTests` (Category=MermaidCli, `sidecar` collection) cover hard-case writer output (operators, templates, braces, destructors, function-pointer field, enum class), a broken diagram, valid and broken generated diagrams over 50,000 characters, and the zero-change diagram. A missing prerequisite fails the test with the install hint and never skips.

## Task Commits

1. **Task 1: Install pinned mermaid-cli and write the local check script** - `f053f8e` (feat)
2. **Task 2: Syntax-check tests on writer output and on a broken diagram** - `3f05cab` (test)

**Plan metadata:** the docs commit that follows this file (docs: complete plan)

_TDD note: Task 2 is marked tdd, but its tests passed on their first run because `check.mjs` already shipped in Task 1. This is not an invalid RED: the tests exercise a tool, not new production code, and the broken-diagram cases (unbalanced brace, small and over 50,000 characters) prove the check can fail._

## Files Created/Modified

- `tools/mermaid-check/package.json` - private package, exact pins
- `tools/mermaid-check/package-lock.json` - integrity-hashed dependency graph
- `tools/mermaid-check/check.mjs` - local syntax check with browser detection and distinct exit codes
- `tests/Vdiff.Tests/Output/MermaidCliSyntaxTests.cs` - five tests that run the check

## Decisions Made

- `MermaidWriter.cs` needed no change: mermaid-cli 12.2.0 accepts `classDef`, `:::state` and the label form `class Id["label"]:::state {`, so the 40-line fix budget was unused.
- Exit code 4 was added for a tool failure that is neither a syntax error nor a missing prerequisite.

## Deviations from Plan

None in the work itself. One process event is recorded here.

### Target-name guard exception, approved by the product owner

The repository's public-diff guard flagged 7 lines in `tools/mermaid-check/package-lock.json` when Task 1 was staged. All 7 hits were one public npm package that mermaid-cli pulls in transitively (a d3 library module, with its type package), plus that tarball's integrity hash, which contains an unrelated substring. The previous executor stopped at a checkpoint; the product owner answered "Approve, commit lockfile".

This executor re-ran the guard on the staged diff, confirmed that every hit line was one of those package lines, then ran the guard with the lockfile excluded (clean), and committed Task 1 with hooks enabled (no `--no-verify`). The lockfile, the target list and the guard script were not edited. Every other commit in this plan (Task 2, this summary, the docs commit) went through the normal guard and was clean.

**Total deviations:** 0 auto-fixed, 1 approved guard exception.

## Verification

- `npm --prefix tools/mermaid-check ci --ignore-scripts` reproduces the install; pins equal the recorded checkpoint line.
- `dotnet test --filter "Category=MermaidCli"`: 5 passed.
- Full `dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --nologo`: 406 passed, 0 failed, 0 skipped (baseline was 401; the 5 new tests account for the difference).
- `cargo test` in `extractors/cpp-ts`: 105 passed, 0 failed.
- `git ls-files tools/mermaid-check` lists `package.json`, `package-lock.json` and `check.mjs` and no `node_modules` path.

## Issues Encountered

- `cargo test` rewrote the package order in `extractors/cpp-ts/Cargo.lock` (cargo-version ordering only, no dependency change). The file was restored with `git checkout -- extractors/cpp-ts/Cargo.lock` so it is not part of this plan.

## Known Stubs

None.

## Threat Flags

None. The check runs a local headless browser on synthetic output only; no new network endpoint, auth path or trust boundary was added beyond the T-01-21, T-01-22, T-01-33 and T-01-SC entries already in the plan's threat model.

## User Setup Required

None. A developer who wants to run the Category=MermaidCli tests needs node 22.13 or newer, a local Chrome, Chromium or Edge, and `npm --prefix tools/mermaid-check ci --ignore-scripts` once.

## Next Phase Readiness

- Plan 01-10's trial script can reuse `check.mjs` for its `mermaid_syntax` field and inherits the over-50,000-character fix.
- Use on private output stays local only.

## Self-Check: PASSED

- `tools/mermaid-check/package.json`, `package-lock.json`, `check.mjs` and `tests/Vdiff.Tests/Output/MermaidCliSyntaxTests.cs` exist.
- Commits `f053f8e` and `3f05cab` are ancestors of HEAD.

---
*Phase: 01-offline-tracer*
*Completed: 2026-11-17*
