---
gsd_state_version: "1.0"
current_phase: 01
current_phase_name: Offline Tracer
status: executing
stopped_at: Completed 01-17-PLAN.md
last_updated: "2026-11-18T20:19:40.179Z"
last_activity: 2026-11-18
last_activity_desc: Phase 01 execution started
state_head: 90ba8ae62065800dc820e6b2bbe543fb26e07d5a
progress:
  total_phases: 9
  completed_phases: 0
  total_plans: 18
  completed_plans: 17
  percent: 0
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-11-16)

**Core value:** A reviewer understands a PR's structural change (which types, members and edges changed shape) from the diagram, without reading every file.
**Current focus:** Phase 01 — Offline Tracer

## Current Position

Phase: 01 (Offline Tracer) — READY TO EXECUTE
Plan: 17 of 17
Status: Ready to execute
Last activity: 2026-11-18 — Phase 01 execution started

Progress: [░░░░░░░░░░] 0%

## Performance Metrics

**Velocity:**
- Total plans completed: 0
- Average duration: - min
- Total execution time: 0.0 hours

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| - | - | - | - |

**Recent Trend:**
- Last 5 plans: -
- Trend: -

*Updated after each plan completion*
**Per-Plan Metrics:**

| Plan | Duration | Tasks | Files |
|------|----------|-------|-------|
| Phase 01 P03 | 13 min | 3 tasks | 16 files |
| Phase 01 P04 | 28 min | 3 tasks | 4 files |
| Phase 01 P05 | 11 min | 2 tasks | 6 files |
| Phase 01 P06 | 15 min | 3 tasks | 6 files |
| Phase 01 P07 | 9 min | 2 tasks | 9 files |
| Phase 01 P08 | interrupted, ~40 min continuation | 2 tasks | 6 files |
| Phase 01 P09 | not timed | 2 tasks | 4 files |
| Phase 01 P10 | 45min | 3 tasks | 4 files |
| Phase 01 P11 | 25 min | 2 tasks | 5 files |
| Phase 01 P12 | 50min | 3 tasks | 7 files |
| Phase 01 P13 | resumed run | 3 tasks | 8 files |
| Phase 01 P14 | 40min | 3 tasks | 3 files |
| Phase 01 P15 | 45 min | 3 tasks | 9 files |
| Phase 01 P16 | 30 min | 3 tasks | 7 files |
| Phase 01 P17 | 46 min | 3 tasks | 8 files |

## Accumulated Context

### Decisions

Decisions are logged in PROJECT.md Key Decisions table.
Recent decisions affecting current work:

- [Roadmap revision]: PO feedback (2026-11-16) "first test offline against a repository" made Phase 1 the Offline Tracer: the thinnest end-to-end `vdiff` run against a local git repository, with no Azure DevOps contact. Phases 2 to 9 harden what it starts; Phase 2 depends on Phase 1
- [Roadmap]: After Phase 1, phases follow the plan's unit order (U1 to U10); U3, U4 and U5 are one phase (Phase 4) with three parallel plans
- [Roadmap]: Spikes sit in the phase they gate: S2 in Phase 1 (ADR-001, basis of the tracer), S3 in Phase 4 (C# plan), S1 in Phase 8 (Publisher)
- [Roadmap]: S1 has no code dependency and can run at any time, but it must be recorded before Phase 7 closes; local Mermaid rendering is not gated by it
- [Roadmap]: TRC-01 to TRC-03 are new v1 requirements owned by Phase 1; R-05, R-10, R-13, R-20 and R-22 keep their original owner phases and are hardened there
- [Roadmap]: R-09 is owned by Phase 4 (extractors), not split across U3 and U4
- [Roadmap]: ACC-01 is owned by Phase 2 but reaches green only in Phases 5 to 7
- [Phase 01]: Plan 01-03: model and diff JSON stay provisional (vdiff-model/v0-tracer, vdiff-diff/v0-tracer); non-UTF-8 changed paths are unmodelled on both sides; Mermaid draws no base edges yet
- [Phase 01]: 01-04: --blank macro names are capped at 128 characters (CliOptions.MaxBlankNameLength)
- [Phase 01]: 01-04: synthetic-only guard flags a remote verb only as the first argument of a git call; ignore-rule guard requires a committed .gitignore as the source
- [Phase 01]: Plan 01-05: sidecar timeout and parse-budget overrides live in SidecarLimits.FromEnvironment; Default reads the process environment so CliApp is unchanged; caps are not overridable
- [Phase 01]: 01-06: sidecar depth cap of 128 on every recursive walk; each skipped subtree adds 1 to the file's error_nodes (main.rs adds extract_with_skips' count), so no protocol or C# change; a declaration is entered without counting a level
- [Phase 01]: 01-06: enumerators are members of kind enumerator with access none, enum underlying type is in bases, aliases are kind alias, conversion operators are methods named operator <type>; sidecar JSON shape unchanged, 01-08 output code should expect these values
- [Phase 01]: 01-07: duplicate member entries read 'member <key> of <type id> (N definitions)'; modified members keep both access and signature pairs; differ picks the first repeated key in total ordinal order
- [Phase 01]: 01-08: Mermaid trailing [state] tag and classDef-per-state format stays provisional until spike S1; an empty diff renders classDiagram plus three classDef lines
- [Phase 01]: 01-09: mermaid-cli 12.2.0 / puppeteer 25.17.0 pinned; check.mjs raises maxTextSize/maxEdges so >50,000-char diagrams are really parsed; MermaidWriter needed no change
- [Phase 01]: 01-10: Public trial record holds only the six-line block, run date and generic text; TRC-03 and SPK-02 recorded as met (single run passed, ADR-001 accepted 2026-11-16)
- [Phase 01]: 01-11: every Mermaid class id is prefixed C_ (total prefix instead of a keyword deny-list) so repository type names cannot equal a Mermaid keyword
- [Phase 01]: 01-12: resolve links by a segment walk (40-link budget, fail closed with a fixed message) in CliApp.RefuseUnsafeOut and trace-run.ps1; subst drives and 8.3 short names are an accepted limit (T-01-39)
- [Phase 01]: 01-13: --out accept-list on full path text with fixed UnsupportedRootMessage; outputs written via temp name + File.Move; containment re-checked before write (T-01-40 mitigated, tracked as T-01-43)
- [Phase 01]: 01-14: Write-LogFile reads GetAttributes (not Get-Item LinkType) so a dangling link is seen on Windows PowerShell 5.1; Test-NonDriveRoot uses -cnotmatch with the backslash as x5C
- [Phase 01]: 01-15 R-1/R-2: on Windows a link is followed only when its reported target is a drive-letter path (accept-list, C# and script); relative symlink targets refused too (T-01-47 accepted); volume GUID denylist removed
- [Phase 01]: 01-15 R-4/R-5: junction fixture is a raw FSCTL_SET_REPARSE_POINT write (no privilege) with a drive-letter decoy print name; Windows symbolic link form is manual-only in 01-VALIDATION.md
- [Phase 01]: 01-16: hold --out without delete sharing on Windows and write only by the handle's final path; Linux/macOS bounded by statement (H-1..H-8)
- [Phase 01]: 01-17: log directory held without delete sharing and written by final path in trace-run.ps1 (Add-Type, Windows only); VDIFF_PROTECTED_DIRS carries the vdiff root to the CLI; Linux and macOS bounded by statement (L-1..L-7)

### Pending Todos

None yet.

### Blockers/Concerns

- [Phase 4] SPK-03 (S3) is on hold pending PO decision O4. The PO said S3's premise came from a mis-report and that the target C# is built on Windows; what that means for S3 has not been confirmed. It blocks the C# plan in Phase 4 and the runner choice in Phase 9. It does not block Phases 1 to 3, or the C++ and C++/CLI plans in Phase 4. Update 2026-11-16: the PO named a local Windows clone as the first-pass test repository; the orchestrator reads that as the first pass running locally on Windows, with the Ubuntu-runner question deferred until after it.
- [Phase 8] SPK-01 (S1) is prepared but not run: `docs/spikes/S1-mermaid-in-ado-pr.md` is "ready to run (manual)" and needs the PO or an agent in the target organisation. It has no code dependency and can run any time; it must be recorded before Phase 7 closes, and it gates the PR-thread renderer default in Phase 8. It does not gate local Mermaid rendering.
- [Phase 1] SPK-02 (S2): ADR-001 accepted by the PO on 2026-11-16 (Rust sidecar). The tracer in Phase 1 is built on it, and it gates the C++ plan in Phase 4.
- [Phase 1] TRC-03 needs a private local clone of the target repository and the base and head commits of one small real PR, supplied by the PO. Its location and content must not be recorded here, and every output stays outside this repository.
- [All] Repository is public (D15): no target names, identifiers or target-measured figures may be committed. Private results are recorded here without identifiers.

## Deferred Items

Items acknowledged and deferred at milestone close, most recent first:

| Category | Item | Status | Deferred At | Milestone |
|----------|------|--------|-------------|-----------|
| *(none)* | | | | |

## Session Continuity

Last session: 2026-11-18T18:27:14.160Z
Stopped at: Completed 01-17-PLAN.md
Resume file: None
