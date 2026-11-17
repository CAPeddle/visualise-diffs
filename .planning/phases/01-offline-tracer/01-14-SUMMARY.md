---
phase: 01-offline-tracer
plan: 14
subsystem: trial-script-output-safety
tags: [out-guard, path-namespaces, volume-guid, hard-link, log-write, gap-closure, powershell]
requires: [01-13]
provides:
  - "Test-NonDriveRoot in scripts/trace-run.ps1 (same accept-list as FinalPath.Step), applied in Resolve-FinalPathStep and to -Out and -LogPath"
  - "A Volume{ link target throws in the script resolver (script-error), mirroring the C# resolver"
  - "Write-LogFile: the diagnostic log replaces a planted hard link instead of writing through it; a second containment check runs after the vdiff run"
  - "T-01-39 bound re-worded in the script header and above Resolve-FinalPath, citing T-01-41, IN-03 and IN-04"
  - "REQUIREMENTS.md shows SPK-02 checked and Complete"
affects: []
tech-stack:
  added: []
  patterns: ["accept-list on full path text (-cnotmatch on a hex-escaped backslash)", "delete the entry by name, then FileMode.CreateNew", "attributes read without following (GetAttributes) to see dangling links"]
key-files:
  created: []
  modified:
    - scripts/trace-run.ps1
    - tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs
    - .planning/REQUIREMENTS.md
decisions:
  - "S-1..S-6 of the plan applied as written, with one implementation choice inside S-4: Write-LogFile reads [IO.File]::GetAttributes (Directory or ReparsePoint means no log) instead of Get-Item LinkType, because attributes are read without following and also see a dangling link on Windows PowerShell 5.1"
  - "Test-NonDriveRoot uses -cnotmatch with the backslash written as \\x5C: case-sensitive so a non-ASCII letter that folds to an ASCII one (for example the Kelvin sign) cannot pass as a drive letter, and \\x5C so no layer can collapse a doubled backslash"
status: complete
actuals:
  tokens: 36000
  tasks: 3
  commits: 5
commits: 5
plan_head_before: bbfff069707350449962d30fc4bb3796085ba0d8
plan_head_after: cc570a192c1135ed7855da85d332cf038b57dcf5
completed: 2026-11-17
---

# Phase 01 Plan 14: --out and log guard hardening on the script side Summary

`scripts/trace-run.ps1` now refuses an `-Out` or `-LogPath` in any non-drive Windows namespace, fails closed on a junction whose target is a volume GUID path, writes its log by replacing (not following) a planted hard link, and checks the log path again after the vdiff run, closing CR-01, WR-01 and WR-02 on the script side; SPK-02 is shown as Complete.

`commits: 5` is the measured count of `bbfff06..HEAD` at the time this file was written; the SUMMARY commit follows it.

## Commits

| Task | Phase | Commit | Message |
|------|-------|--------|---------|
| 1 | RED | 935519d | test(01-14): add failing script tests for non-drive namespaces and volume GUID links |
| 1 | GREEN | b5cfa24 | feat(01-14): refuse a non-drive namespace and a volume GUID link target in trace-run.ps1 |
| 2 | RED | 1879d33 | test(01-14): add failing test for a hard link planted at the -LogPath entry |
| 2 | GREEN | 116d2a5 | feat(01-14): write the trace-run log without following a planted link |
| 3 | docs | cc570a1 | docs(01-14): mark SPK-02 complete in REQUIREMENTS.md |

## What was built

- **Task 1 (CR-01).** `Test-NonDriveRoot` (Windows only: the full path text must start with an ASCII letter, a colon and a backslash). It runs inside `Resolve-FinalPathStep` (so every path and link target, including the protected directories), and explicitly on `$outFull` (reason `out-not-outside`) and `$logFull` (reason `bad-arguments`) before the containment loops. A link target beginning with `Volume{` throws, so the run ends with `script-error`. `$repoFull` joins `$protected` only when it is a drive path; the three git-reported roots cover a repository addressed through another namespace. Reason vocabulary unchanged; precedence is written in the header.
- **Task 2 (WR-01, WR-02).** `Write-LogFile` creates the parent directory, reads the entry's attributes without following it, returns without writing for a directory or reparse point, removes a plain file or hard link with `[IO.File]::Delete` (directory entry only), then opens with `FileMode.CreateNew` and writes UTF-8 without BOM. `Invoke-Trace` repeats the namespace test and the containment loop for `$logFull` after the vdiff run and before the write, inside the existing swallow-all try/catch.
- **Task 3.** SPK-02 checkbox checked and its traceability cell set to `Complete` (two lines changed).

## TDD record (red steps)

- **Task 1 RED** (`935519d`; 6 failed, 28 passed of 34, run on both hosts). `Aliased_out_in_a_non_drive_namespace_is_refused_with_out_not_outside`: expected `out-not-outside`, got `tool-failed` under PowerShell 7 and `script-error` under Windows PowerShell 5.1 (the first spelling already fails differently there). `Aliased_out_through_a_junction_with_a_volume_guid_target_fails_closed_with_script_error`: expected `script-error`, got `tool-failed` on both hosts (the C# CLI refused, the script did not). `Aliased_log_path_in_a_non_drive_namespace_is_refused_with_bad_arguments`: expected exit 1, got exit 0 on both hosts, so the script accepted the log path and wrote the log into the repository (its status stayed clean because the sandbox `.gitignore` ignores `*.log`, so the plan's expected `repo-changed` did not occur; the test still fails on the planned refusal and on the file-exists assertion). The sibling, extended `-Repo` mirror and `out-omitted` cases passed, as the plan predicted. Semantic assessment: each target test executed and failed on the planned assertion for the intended reason.
- **Task 1 GREEN** (`b5cfa24`): TraceRunScriptTests 34 passed (23 before plus 11).
- **Task 2 RED** (`1879d33`; 2 failed of 2): the run reported pass, but `README.md` now held the log text (`Expected: "# Sample\n"`, `Actual: "vdiff: wrote 5 files to ..."`), so the planted hard link was written through into the tracked file. The plan expected `repo-changed`; the script's snapshot did not see the change in the test (the repository file was overwritten through another NTFS name for it, and the after-run `git status` was unchanged in that run, although an identical manual run reported `repo-changed`). So the run-level result cannot be relied on to catch this write: the log guard is the defence. Semantic assessment: intended failure on the README content assertion.
- **Task 2 GREEN** (`116d2a5`): TraceRunScriptTests 36 passed.
- Task 3 is bookkeeping (no TDD).
- The task 1 RED and GREEN are separate commits, as the 01-13 SUMMARY asked.

## Verification

- Full suite: `dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --nologo` -> Passed 487, Failed 0, Skipped 0 (474 before plus 13 added here: 12 from six new theories over two hosts, plus the `out-omitted` row; matches the plan's 13). Duration 6 m 49 s.
- Acceptance greps: `Test-NonDriveRoot` in code lines 5 (needs at least 4); `T-01-41` 4; `IN-04` 2; `T-01-39` 2; the header reason vocabulary line 1; non-ASCII characters 0; `WriteAllText` in code lines 0; `Write-LogFile` in code lines 2; `Test-SameOrUnder $logFull` between the vdiff run and the after snapshot 1 (source-order gate for T-01-46); `ls $TEMP | grep -c '^vdiff-links-'` 0.
- `grep -c T-01-41` per file: CliApp.cs 1, FinalPath.cs 3, trace-run.ps1 4 (none is zero).
- The public-repo target-name guard printed "clean" on every staged diff. `01-UAT.md`, `01-VERIFICATION.md` and `01-REVIEW-DISPOSITION.md` are untouched.

T-01-39 still open after plans 01-13 and 01-14: subst and mapped drive letters, DOS 8.3 short names, and review findings IN-03 and IN-04. Other Windows namespaces and volume GUID link targets are refused, not resolved (T-01-41).

## Deviations from Plan

None in behaviour. Notes:

- S-4 mechanism detail: `Write-LogFile` uses `[IO.File]::GetAttributes` rather than `Get-Item ... LinkType`, for the reason given under decisions. The effect is the same set of outcomes (directory, symbolic link, junction: no log; plain file or hard link: replaced), and a dangling file symbolic link is also skipped.
- The task 2 and task 1 script changes were staged separately: the full script was written once, the log parts were held back for the task 1 commit, and restored for task 2, so each GREEN commit contains only its own task.
- Two red-step observations differ from the plan's predictions (the namespace -LogPath case and the hard link case did not report `repo-changed`); both are recorded above. Neither changes the plan's truths.

## Known Stubs

None.

## Threat Flags

None beyond the plan's register (T-01-44, T-01-45, T-01-46). Residuals: a file symbolic link at the log name is covered by the rule and the plan 01-13 C# test, not by a script test (the privilege is not held on the dev host); the junction and link forms at the log name are not asserted dynamically; the interval between the second log check and the write is not closed (T-01-46); the script's own repository snapshot does not reliably detect a write made through a hard link (observed in the task 2 red step), so the guards rather than the after-run snapshot carry that case. Not run under WSL or Linux: the namespace tests return early there, and the hard link test is the only one of the new tests that asserts on a non-Windows host.

## Self-Check: PASSED

- Commits 935519d, b5cfa24, 1879d33, 116d2a5, cc570a1 are ancestors of HEAD.
- Files present: scripts/trace-run.ps1, tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs, .planning/REQUIREMENTS.md.
