---
phase: 01-offline-tracer
plan: 12
subsystem: cli-safety
tags: [path-containment, symlink, junction, powershell, csharp, xunit, gap-closure]

requires:
  - phase: 01-offline-tracer
    provides: CliApp.RefuseUnsafeOut lexical guard (01-03), trace-run.ps1 (01-10), keyword-safe ids (01-11, build dependency only)
provides:
  - FinalPath.Resolve, the link-resolving path walk shared in rule by the CLI and the trial script
  - An --out reached through a symbolic link or directory junction into the repository is refused by the CLI and by trace-run.ps1
  - LinkScratch test helper that makes links outside the sandbox and removes them as links
affects: [phase-01 re-verification, TRC-01, TRC-03]

actuals:
  tokens: 6837
  tasks: 3
  commits: 5

commits: 5
plan_head_before: dbe1c48d992e4c2a58fe2b3f02f9e007fe2d314e
plan_head_after: 42b9a4905ac697af515f751441de613561c02f1a

tech-stack:
  added: []
  patterns:
    - "Containment guard compares lexical paths first, then link-resolved paths; resolver failure is a refusal, never a lexical fall-back"

key-files:
  created:
    - src/Vdiff.Core/Cli/FinalPath.cs
    - tests/Vdiff.Tests/Support/LinkScratch.cs
    - tests/Vdiff.Tests/Safety/AliasedOutTests.cs
    - tests/Vdiff.Tests/Safety/FinalPathTests.cs
  modified:
    - src/Vdiff.Core/Cli/CliApp.cs
    - scripts/trace-run.ps1
    - tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs

key-decisions:
  - "Resolve links by walking the path segment by segment (40-link budget) rather than a Win32 final-path call, because the PowerShell 5.1 script cannot make one without compiling code"
  - "Every resolver failure becomes VdiffException(FinalPath.UnresolvableMessage) with no inner message, so the CLI cannot print a .NET exception text that carries a path (T-01-38)"
  - "CliApp.IsSameOrUnder stays lexical and public; the resolved comparison is a separate step in RefuseUnsafeOut"

requirements-completed: [TRC-01, TRC-03]

duration: 50min
completed: 2026-11-17
status: complete
---

# Phase 1 Plan 12: Aliased --out containment Summary

**FinalPath.Resolve walks a path through symbolic links and junctions, and both CliApp.RefuseUnsafeOut and trace-run.ps1 now compare the resolved --out (and -LogPath) with the resolved work tree, git directory and common directory, so a junction into the repository no longer lets vdiff write into it.**

## Accomplishments
- Closed verification gap 2 (finding WR-01, threats T-01-36 and T-01-37): an `--out` that is a link to the repository root, a path below it, a link to a subdirectory, a link to the git directory, or a chain of links exits 1 with `--out must not lie inside the analysed repository`, creates nothing, and leaves the sandbox snapshot unchanged.
- The reverse direction (`--repo` reached through a link, `--out` the real path inside the repository) is refused.
- No over-refusal: an `--out` through a link to a directory outside the repository exits 0 and the five files land at the link's target.
- A link loop or an ancestor-link cycle gives exit 1 and exactly `vdiff: a path could not be resolved to its final target` on stderr, with no path and no .NET text (T-01-38).
- `trace-run.ps1` refuses `-Out` through a link with `out-not-outside` and `-LogPath` through a link with `bad-arguments` (no log written into the repository), passes for a link to an outside directory, and reports `script-error` on a loop, with its six fixed lines only, under Windows PowerShell 5.1 and PowerShell 7.

## Accepted residual (T-01-39)

subst drive letters and 8.3 short names are an accepted limit of gap 2's closure (T-01-39). The guard resolves symbolic links and directory junctions only. The limit is stated at the guard itself, in the doc comments of `FinalPath.Resolve` and `CliApp.RefuseUnsafeOut` and in the header comment of `scripts/trace-run.ps1`, so a reader of `01-VERIFICATION.md`'s broader wording ("no symlink, junction, subst or short-name resolution") finds it there. T-01-40 (a link swapped between check and write) is likewise accepted per the plan.

## TDD record

- **RED (Task 1), commit `ff5d447`:** with `LinkScratch` and `AliasedOutTests` in place and no production change, `AliasedOutTests` gave `Failed: 6, Passed: 2, Total: 8`. The five aliased theory cases and the chain case failed on `Assert.Equal(1, exit)` with `Actual: 0` (the tool wrote `aliased-out` into the repository); the reverse-direction and outside-link cases passed as the plan predicted (guards). semanticAssessment: the target tests executed and failed on the planned assertion for the intended reason (an aliased `--out` accepted), not a build, fixture or discovery fault. The gsd `check tdd-red-evidence` classifier was not run (no `gsd_run` in this environment, and xUnit console output is not one of its supported formats), as in plan 01-11.
- **GREEN (Task 1), commit `8445196`:** `FinalPath` plus the resolved comparison in `RefuseUnsafeOut`; `AliasedOutTests` `Passed: 8`; `CliOptionsTests`, `HostileInputTests`, `TracerEndToEndTests` `Passed: 61`.
- **Task 2, commit `f777120`:** the eight `FinalPathTests` facts and the ninth CLI case (loop) were written against the existing resolver and passed on the first run (`Passed: 17` for `FinalPathTests` + `AliasedOutTests`); no resolver defect was found, so `FinalPath.cs` was not changed. This commit is a test-only pin of behavior, not a red step.
- **RED (Task 3), commit `e73fb1c`:** the four `Aliased_` script tests on both hosts gave `Failed: 6, Passed: 2, Total: 8`. The `-Out` test reported `tool-failed` instead of `out-not-outside` (the CLI guard of Task 1 refused it, the script did not), the `-LogPath` test exited 0 (the script wrote its log into the repository), and the loop test reported `tool-failed` instead of `script-error`; the outside-link test passed on both hosts (guard). semanticAssessment: planned assertions, intended reason.
- **GREEN (Task 3), commit `42b9a49`:** `Resolve-FinalPath` in `Test-SameOrUnder`; `Aliased_` filter `Passed: 8`; whole `TraceRunScriptTests` `Passed: 23`.

Tracer gate (Task 1): the tracer verify (eight AliasedOutTests plus the 61 lexical/hostile/end-to-end tests) passed before Tasks 2 and 3 started; expansion continued.

## Task Commits

1. **Task 1** (tracer, tdd): `ff5d447` test (LinkScratch, AliasedOutTests, red), `8445196` feat (FinalPath, RefuseUnsafeOut)
2. **Task 2** (tdd): `f777120` test (FinalPathTests, loop CLI case)
3. **Task 3** (tdd): `e73fb1c` test (four `Aliased_` script tests, red), `42b9a49` feat (script resolver)

## Verification

- Plan verify commands: `AliasedOutTests` `Passed: 9`; `FinalPathTests` `Passed: 8`; `TraceRunScriptTests.Aliased_` `Passed: 8` (4 tests on 2 hosts); `TraceRunScriptTests` `Passed: 23`.
- Plan-level: `dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --nologo` after the last commit: **455 passed, 0 failed** (430 after plan 01-11, plus 17 for FinalPath/AliasedOut and 8 for the script tests). `cargo test` not run: the sidecar crate is untouched.
- Non-Windows run: `FinalPathTests` (8 passed) and `AliasedOutTests` (9 passed) were also run under WSL Ubuntu with .NET 10.0.115 from a copy of the tree in the WSL home directory, so the symbolic-link and relative-target branches of `LinkScratch` and `FinalPath` were exercised on Linux. The script tests ran on Windows only (Windows PowerShell 5.1 and PowerShell 7).
- Acceptance greps: `FinalPath.Resolve` in non-comment `CliApp.cs` code: 4; `T-01-39` in `CliApp.cs` and `FinalPath.cs`: 1 each; `Resolve-FinalPath` in non-comment `trace-run.ps1`: 5; non-ASCII characters in `trace-run.ps1`: 0; all touched files have 0 CR characters.
- No `vdiff-links-*` directory remained under the temp directory after any run.
- Target-name guard (`check-public-diff.sh`, private, outside the repo) ran clean on each staged diff before all five commits.

## Files Created/Modified
- `src/Vdiff.Core/Cli/FinalPath.cs` - `Resolve` and `UnresolvableMessage`; walk from the root, follow links (budget 40), stop at the first missing segment, fail closed
- `src/Vdiff.Core/Cli/CliApp.cs` - `RefuseUnsafeOut` keeps the lexical test and adds the resolved one against the three git roots; doc comment states the T-01-39 bound
- `scripts/trace-run.ps1` - `Test-LexicallySameOrUnder` (old body), `Resolve-FinalPath`/`Resolve-FinalPathStep` (Get-Item `LinkType`/`Target`, no PowerShell 7 only API), `Test-SameOrUnder` compares lexically then resolved; header names the limit
- `tests/Vdiff.Tests/Support/LinkScratch.cs` - junction on Windows (`mklink /J`), symbolic link elsewhere; removes links first, as links
- `tests/Vdiff.Tests/Safety/AliasedOutTests.cs`, `FinalPathTests.cs`, `tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs` - the cases listed in the plan

## Decisions Made
- Segment walk with a link budget instead of a final-path Win32 call (see key-decisions); the residual is the T-01-39 limit above.
- `Get-Item -ErrorAction Stop` with a catch of `ItemNotFoundException` only (not `SilentlyContinue`), so an access failure is not mistaken for a missing path and falls through to the fail-closed `script-error`. A dangling junction is found by `Get-Item -Force` on both hosts, so it is followed to its missing target (checked by hand on both hosts, and pinned in C# by `A_link_to_a_missing_target_resolves_to_that_missing_path`).
- A `\??\` prefix on a junction target is stripped defensively in the script (not observed on either host).

## Deviations from Plan

None - plan executed exactly as written. Procedural notes, neither a code deviation:
- Task 1 and Task 3 each produced two commits (test then feat) following the TDD reference's RED/GREEN commit scope for `tdd="true"` tasks; the "only the named files" acceptance holds per commit.
- The test-only Task 2 passed on first run; the plan expected this (the resolver was already written in Task 1), so no GREEN step exists for it.

## Issues Encountered
- A `python -` heredoc used to patch `trace-run.ps1` mangled the `'\??\'` literal through Python escaping; it was caught by reading the patched file back and corrected with an Edit before any test ran or commit was made.

## Known Stubs

None.

## Threat Flags

None. No new network endpoint, auth path or schema change. T-01-36, T-01-37 and T-01-38 are mitigated as planned; T-01-39 and T-01-40 are accepted as planned.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Both gaps of `01-VERIFICATION.md` (gap 1 by plan 01-11, gap 2 by this plan) are closed at code and test level; the phase is ready for re-verification.
- Out of scope and unchanged: links already present at the five output file names (WR-01's optional hardening), atomic output (IN-06) and the other review warnings.

---
*Phase: 01-offline-tracer*
*Completed: 2026-11-17*

## Self-Check: PASSED

- FOUND: src/Vdiff.Core/Cli/FinalPath.cs, tests/Vdiff.Tests/Support/LinkScratch.cs, tests/Vdiff.Tests/Safety/AliasedOutTests.cs, tests/Vdiff.Tests/Safety/FinalPathTests.cs
- FOUND commits: ff5d447, 8445196, f777120, e73fb1c, 42b9a49 (all ancestors of HEAD; `git rev-list --count dbe1c48..HEAD` = 5)
