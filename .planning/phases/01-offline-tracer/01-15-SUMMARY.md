---
phase: 01-offline-tracer
plan: 15
subsystem: safety
tags: [dotnet, powershell, windows-junction, reparse-point, path-containment, gap-closure]

requires:
  - phase: 01-offline-tracer
    provides: "FinalPath resolver and trace-run.ps1 link guards from plans 01-12 to 01-14; the volume GUID denylist this plan replaces"
provides:
  - "One accept-list on the reported link target (drive-letter path only) in FinalPath.Step (C#) and Resolve-FinalPathStep (script), replacing the volume GUID denylist"
  - "LinkScratch.RawJunction and LinkScratch.NtSubstituteName: unprivileged raw reparse-point junctions with any substitute name"
  - "End-to-end CLI and script (-Out, -LogPath, both PowerShell hosts) refusal tests for GLOBALROOT and bare-device junctions, resolver rows incl. UNC, an 18-row reported-target table"
  - "Re-worded T-01-39 / T-01-41 bound at the four comment sites"
affects: [phase-01-verification, 01-SECURITY, 01-UAT]

actuals:
  tokens: 9000
  tasks: 3
  commits: 7

tech-stack:
  added: []
  patterns:
    - "Accept-list on the text the runtime reports for a link target, judged before the target is combined with the link's parent"
    - "Same predicate in two implementations (StartsWithDriveLetter / Test-NonDriveRoot), tested end to end on both"
    - "Raw FSCTL_SET_REPARSE_POINT fixture with a drive-letter decoy print name to pin which name the runtime reports"

key-files:
  created: []
  modified:
    - src/Vdiff.Core/Cli/FinalPath.cs
    - src/Vdiff.Core/Cli/CliApp.cs
    - scripts/trace-run.ps1
    - tests/Vdiff.Tests/Support/LinkScratch.cs
    - tests/Vdiff.Tests/Safety/AliasedOutTests.cs
    - tests/Vdiff.Tests/Safety/FinalPathTests.cs
    - tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs
    - .planning/phases/01-offline-tracer/01-VALIDATION.md

key-decisions:
  - "R-1: a Windows link is followed only when the reported target is a drive-letter path; applied before the target is combined with the link's parent; the volume GUID special cases were removed because the one rule covers them"
  - "R-2: a relative symbolic link target on Windows is refused too (fail closed; T-01-47 accepted); Linux and macOS keep the relative branch"
  - "R-3: messages and reasons unchanged (FinalPath.UnsupportedRootMessage in the CLI, reason script-error in the script)"
  - "R-4: junction fixture is a raw reparse-point write via DllImport, no privilege, no unsafe, no csproj change; NT-namespace tests give the junction a drive-letter decoy print name"
  - "R-5: the symbolic link form that cannot be reproduced unprivileged is manual-only in 01-VALIDATION.md"
  - "R-6: WR-02 (necessity) and IN-01 included; UAT, SECURITY, REVIEW-DISPOSITION, REQUIREMENTS and STATE bookkeeping flagged to the orchestrator, not edited"

patterns-established:
  - "Allow-list on reported text, never a name list: the runtime strips the NT prefix, and the bare device spelling is reported as 'ice\\...' under .NET and PowerShell 7"

requirements-completed: [TRC-01, TRC-02, TRC-03, SPK-02]

coverage:
  - id: D1
    description: "vdiff --out below a junction whose substitute name is the GLOBALROOT or bare-device form of the repository is refused (exit 1, fixed unsupported-root line, nothing created, repository unchanged)"
    requirement: TRC-01
    verification:
      - kind: integration
        ref: "tests/Vdiff.Tests/Safety/AliasedOutTests.cs#An_out_through_a_junction_with_an_nt_namespace_target_is_refused_with_the_fixed_message"
        status: pass
    human_judgment: false
  - id: D2
    description: "trace-run.ps1 -Out and -LogPath below such a junction end with script-error, six fixed lines, nothing written into the repository, on pwsh and Windows PowerShell 5.1"
    requirement: TRC-03
    verification:
      - kind: integration
        ref: "tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs#Aliased_out_through_a_junction_with_an_nt_namespace_target_fails_closed_with_script_error"
        status: pass
      - kind: integration
        ref: "tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs#Aliased_log_path_through_a_junction_with_an_nt_namespace_target_fails_closed_with_script_error"
        status: pass
    human_judgment: false
  - id: D3
    description: "FinalPath.Resolve refuses every non-drive reported link target on Windows (globalroot, device-no-prefix, unc rows, 18-row reported-target table) and still follows a raw drive-letter junction like a mklink junction"
    requirement: TRC-01
    verification:
      - kind: unit
        ref: "tests/Vdiff.Tests/Safety/FinalPathTests.cs#A_junction_with_an_nt_namespace_target_is_refused_on_windows"
        status: pass
      - kind: unit
        ref: "tests/Vdiff.Tests/Safety/FinalPathTests.cs#A_raw_junction_with_a_drive_letter_target_is_followed_like_a_plain_junction"
        status: pass
      - kind: unit
        ref: "tests/Vdiff.Tests/Safety/FinalPathTests.cs#A_reported_link_target_is_accepted_only_when_it_is_a_drive_letter_path"
        status: pass
    human_judgment: false
  - id: D4
    description: "The doc comments at the four guard sites state exactly what is refused and what stays unresolved (T-01-39, T-01-41, IN-04) and no longer list link targets written without a drive letter as open (failed truth 16)"
    requirement: TRC-01
    verification:
      - kind: other
        ref: "grep checks of the plan verify block (GLOBALROOT, T-01-39, T-01-41, IN-04 cited per file; the old finding id absent; script ASCII only)"
        status: pass
    human_judgment: true
    rationale: "Wording is a human judgement; the greps prove only that the citations are present (listed in the manual-only table of 01-VALIDATION.md)"
  - id: D5
    description: "A Windows symbolic link (not a junction) with a relative or NT-absolute target is refused"
    requirement: TRC-01
    verification:
      - kind: unit
        ref: "tests/Vdiff.Tests/Safety/FinalPathTests.cs#A_reported_link_target_is_accepted_only_when_it_is_a_drive_letter_path (text level only)"
        status: pass
    human_judgment: true
    rationale: "Creating such a link needs a privilege the dev host lacks; the end-to-end check is manual-only in 01-VALIDATION.md"

duration: 45min
completed: 2026-11-18
plan_head_before: 870461e86e7d6d150fe68e9074325c095afd40b3
plan_head_after: 92b6b0019f2066bfbc87d45064848928cb961a25
commits: 7
status: complete
---

# Phase 1 Plan 15: Reported-target accept-list for links Summary

**An allow-list on the link-target text the runtime reports (drive-letter paths only) in both FinalPath.Step and trace-run.ps1, so a junction spelled through GLOBALROOT, a bare device name or UNC can no longer carry --out, -Out or -LogPath into the analysed repository; proven with raw reparse-point junctions on the CLI and both PowerShell hosts.**

## Performance

- **Duration:** about 45 min (the full suite alone took 9 m 57 s)
- **Started:** 2026-11-18 (first task commit 12:18 +01:00)
- **Completed:** 2026-11-18T11:44Z
- **Tasks:** 3 (task 1 type tracer, TDD)
- **Files modified:** 8 plus the removed handoff (9 per `git diff --stat`)

## Accomplishments

- Closed the root cause of verification gaps 1 and 2 (failed truths 2, 7 and 16): the one-entry volume GUID denylist is replaced by one accept-list, applied to the reported link target before it is combined with the link's parent, in C# and in the script.
- `LinkScratch.RawJunction` writes a mount-point reparse buffer with `DeviceIoControl` (no privilege, no `unsafe`, no csproj change); `NtSubstituteName` builds the `globalroot`, `device-no-prefix`, `unc` and `drive` spellings from `QueryDosDeviceW` at run time, so no machine value is committed.
- 28 new tests on Windows (CLI 2, resolver 22, script 4); every one proves the junction live first (or, for `unc`, asserts the reported text), and the NT-namespace fixtures carry a drive-letter decoy print name to pin that readers report the substitute name.
- WR-02 (necessity): the log block after the vdiff run no longer repeats the pure-text namespace test; the file-system containment re-check stays and still precedes `Write-LogFile` in source order.
- IN-01: the stale public handoff is no longer tracked (it stays in history).
- The T-01-39 / T-01-41 bound is re-worded identically at four places and no longer lists link targets written without a drive letter as open.

## Task Commits

1. **Task 1 (tracer, TDD): GLOBALROOT junction refused end to end**
   - RED: `037c6bb` (test)
   - GREEN: `7000078` (feat)
2. **Task 2: every NT-namespace shape and the reported-target rule pinned; WR-02**
   - `a13c99b` (test), `41c7142` (refactor, WR-02)
3. **Task 3: bound re-worded, handoff removed, validation map**
   - `768eeb3` (docs, comments), `4f24f55` (docs, handoff removal), `92b6b00` (docs, validation map)

**Plan metadata:** the SUMMARY commit follows this file (docs: complete plan).

## RED evidence (task 1)

Command: `dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --nologo --filter "FullyQualifiedName~nt_namespace_target"` before any production change. `Failed: 3, Passed: 0, Total: 3`. The runner is xunit's console output, which the `gsd_run check tdd-red-evidence` formats do not cover, so the semantic assessment was done by hand from the real output:

| Test | Host | Failing assertion | Observed |
|------|------|-------------------|----------|
| `AliasedOutTests.An_out_through_a_junction_with_an_nt_namespace_target_is_refused_with_the_fixed_message(globalroot)` | n/a | `Assert.Equal(1, exit)` in `AssertUnsupportedRootAndUntouched` | expected 1, actual 0: vdiff accepted the junction and wrote into the repository |
| `TraceRunScriptTests.Aliased_out_through_a_junction_with_an_nt_namespace_target_fails_closed_with_script_error` | pwsh | reason assertion (message names the kind) | `globalroot: expected script-error but the reason was repo-changed` |
| same | powershell (5.1) | same | `globalroot: expected script-error but the reason was repo-changed` |

Semantic assessment: all three tests ran, passed the fixture-liveness assertion (a tracked file was visible through the junction) and failed on the planned refusal assertion for the intended reason, the exact behaviour the verifier reported. Not a helper exception, not a liveness failure.

GREEN: the same filter then passed 3 of 3; `AliasedOutTests` plus `FinalPathTests` 37 of 37; `TraceRunScriptTests.Aliased` 22 of 22 (20 earlier plus 2 new).

## Mutation (non-vacuity) check (task 2)

Nothing from it was committed; both files were restored with `git checkout --` and `git diff --quiet` confirmed the C# file clean before the task commits.

- **C# side.** Rule weakened to "refuse only a reported target that begins with GLOBALROOT". Filter `(FinalPathTests|AliasedOutTests)&nt_namespace_target`: 3 failed, 2 passed. Failed: `FinalPathTests ... (unc)`, `FinalPathTests ... (device-no-prefix)` (both: no exception thrown) and `AliasedOutTests ... (device-no-prefix)` (exit code differs). Passed: the two `globalroot` rows.
- **Script side.** Same weakening in `Resolve-FinalPathStep`. Filter `TraceRunScriptTests&nt_namespace_target`: 4 failed, 0 passed, on both hosts. `-Out` theory on pwsh and powershell: `device-no-prefix: expected script-error but the reason was tool-failed` (the script let the junction through and the real CLI then refused it). `-LogPath` theory on both hosts: failed on the first assertion that differs (the script no longer fails closed for `device-no-prefix`).

## Files Created/Modified

- `src/Vdiff.Core/Cli/FinalPath.cs` - accept-list on `info.LinkTarget` via `StartsWithDriveLetter` (now `internal`); `IsVolumeGuidTarget` removed; re-worded bound
- `src/Vdiff.Core/Cli/CliApp.cs` - re-worded bound in the `RefuseUnsafeOut` doc comment
- `scripts/trace-run.ps1` - `Test-NonDriveRoot $target` in `Resolve-FinalPathStep`; volume GUID special case and redundant log namespace re-test removed; re-worded bound at the header and above `Resolve-FinalPath` (ASCII only, reason vocabulary unchanged)
- `tests/Vdiff.Tests/Support/LinkScratch.cs` - `RawJunction`, `NtSubstituteName` and three `DllImport` declarations
- `tests/Vdiff.Tests/Safety/AliasedOutTests.cs`, `FinalPathTests.cs`, `tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs` - new tests; the source-order test no longer asserts the removed second namespace test
- `.planning/phases/01-offline-tracer/01-VALIDATION.md` - map rows, manual-only symbolic link row, sign-off count 524
- `docs/handoffs/phase-01-gap-closure-interrupted-by-shutdown.md` - removed

## Decisions Made

R-1 to R-6 as listed in the frontmatter, applied exactly as planned. One implementation note on R-4: `RawJunction` registers the directory in the scratch link list before the control call, and `Dispose` removes it first with a non-recursive `Directory.Delete`, so a failed write still leaves nothing behind; `ls "$TEMP" | grep -c '^vdiff-links-'` printed 0 after the filtered runs and after the full suite.

## Deviations from Plan

None - plan executed exactly as written, with two measurement notes (no behaviour change):

- **Count mismatch, task 2 second verify command.** The plan projected 100 passed for `TraceRunScriptTests|AliasedOutTests|FinalPathTests` (40 + 23 + 37); the observed count is 109, failed 0. Cause: the plan counted 40 tests in `TraceRunScriptTests`, which actually has 49 on Windows (`--list-tests`: 49 / 23 / 37); the CLI and resolver counts match the plan, and the narrower `TraceRunScriptTests.Aliased` count (22) matches too, so the planning-time figure for the non-`Aliased` script tests was low. All 109 pass.
- **Full suite:** 524 passed, 0 failed, 9 m 57 s on Windows, equal to the projected 496 + 28. The cargo count (105) was not re-run: the Rust sidecar is untouched.

**Total deviations:** 0 auto-fixed.

## Known Stubs

None.

## Threat Flags

None. No new network endpoint, auth path or schema; the only new file-system surface is the test-only raw junction helper, covered by T-01-48 (removed first, as a link, non-recursively; points only at a sandbox repository or a real directory inside the same scratch root).

## Issues Encountered

None in the plan's work. One tooling note: a stray `python3` probe in the shell hung (Windows app-execution alias); it was abandoned and the mutation edit was made with the Edit tool instead.

## TDD Gate Compliance

RED `037c6bb` (`test(01-15)`) precedes GREEN `7000078` (`feat(01-15)`); REFACTOR `41c7142` (`refactor(01-15)`) is the WR-02 change. Gate sequence intact.

## Residual

T-01-39 still open after plan 01-15: subst and mapped drive letters, DOS 8.3 short names, and review finding IN-04.

## Flagged to the orchestrator (R-6, not edited here)

- `01-UAT.md` test 1 is still `[pending]`; plans 01-13, 01-14 and 01-15 resolve it.
- `01-SECURITY.md`: T-01-41 and T-01-44 were re-opened by the verification and need the next `/gsd-secure-phase`; the AR-04 wording lists a residual (link targets written without a drive letter) that this plan closes on Windows; T-01-47 to T-01-50 are new rows for it.
- `01-REVIEW-DISPOSITION.md`: WR-02 (necessity) and IN-01 are now done; WR-01, IN-02, IN-03, IN-04, WR-03, WR-04, IN-05 and IN-06 stay open as scoped.
- `REQUIREMENTS.md` status stays at Gaps Found (commit aa94c91); the orchestrator sets Complete after re-verification.
- `01-VERIFICATION.md` is unchanged; a re-verification should flip failed truths 2, 7 and 16.
- Two behaviour-unverified items stay human checks: file symbolic link replacement on a capable host, and a staged swap during a script run. The Windows symbolic link (not junction) with a relative or NT-absolute target is a new manual-only row in `01-VALIDATION.md`.
- The stale handoff content remains in git history (T-01-50 residual); a history rewrite is the owner's call.

## Next Phase Readiness

Gap-closure plan 01-15 is the last plan of phase 01; ready for re-verification (`/gsd-verify-work` or the phase verifier).

---
*Phase: 01-offline-tracer*
*Completed: 2026-11-18*

## Self-Check: PASSED

All seven task commits, the SUMMARY commit and the state commit are ancestors of HEAD; the key files exist; no tracked file remains under docs/handoffs; the residual sentence is present.
