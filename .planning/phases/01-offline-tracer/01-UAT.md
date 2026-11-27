---
status: complete
phase: 01-offline-tracer
source: [01-VERIFICATION.md]
started: 2026-11-17T00:00:00Z
updated: 2026-11-19T14:41:33.298Z
---

## Current Test

[testing complete]

## Tests

### 1. Decide the disposition of re-review findings CR-01, WR-01 and WR-02
expected: Either (a) accept as residuals and widen T-01-39 to name \\?\, loopback UNC, pre-planted hard links and post-check swaps; or (b) raise a hardening gap-closure plan (final-path canonicalisation or refuse \\ roots, temp-name-plus-move writes, re-check before write) in both the CLI and trace-run.ps1.
result: issue
reported: "(b)"
severity: major

### 2. File symbolic link planted at an output name is replaced (privileged host)
expected: On a host that can create file symbolic links (Linux, macOS, or Windows with Developer Mode or an elevated shell), `dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --filter FullyQualifiedName~A_file_symbolic_link_planted` creates the link (does not return early), the run exits 0, the victim file is unchanged and the output is a regular file afterwards
result: pass

### 3. Staged link swap during a trace-run.ps1 run
expected: With -LogPath in a real directory and a -VdiffDll that sleeps about 20 s, replacing the log's parent by a junction into the analysed repository during the sleep leaves no log file in the repository, the six output lines keep their shape, and the repository snapshot is unchanged
result: pass

### 4. Windows symbolic link (not junction) with a relative or NT-absolute target, CLI and script
expected: On a capable host, `mklink` (no /J) with a relative target, and again with an NT-absolute target, into a sandbox repository: `vdiff --out` below the link exits 1 with the fixed unsupported-root line and creates nothing; `trace-run.ps1 -Out` / `-LogPath` below it end with script-error and six fixed lines; repository snapshot unchanged
result: pass

### 5. Re-run the trial wrapper once on the private local clone with the hardened scripts/trace-run.ps1 (recommended, not required)
expected: Six-line block: result pass, reason none, under budget, repo_unchanged yes, outputs_present yes, mermaid_syntax ok; only that block recorded
result: pass

### 6. Product-owner sign-off on the accepted residuals AR-04 (T-01-39) and AR-13 (T-01-47)
expected: Explicit acceptance recorded against AR-04 (subst and mapped drive letters, DOS 8.3 short names, IN-04) and AR-13 (every non-drive-letter Windows link target is refused, including a relative symbolic link), which today carry plan-level acceptance only
result: pass

### 7. Seven judgment-tier prohibitions (plans 01-13, 01-14, 01-15) flagged unverified-prohibition
expected: Reviewer confirms or overrules each non-authoritative verdict listed in the Prohibitions section of 01-VERIFICATION.md (all seven read as honoured)
result: pass

### 8. Re-verify 01-UAT.md test 1 (G-01-1) against plans 01-16, 01-17 and now 01-18, and optionally repeat test 3 by hand
expected: Product owner confirms the swap-window scope is closed on Windows as described in 01-17-SUMMARY.md (held directory, final-path writes, VDIFF_PROTECTED_DIRS) and that the stated Linux and macOS bound is acceptable. 01-UAT.md still records test 1 as result: issue; this verification does not edit it.
result: pass

### 9. Re-confirm the AR-04 sign-off (UAT test 6) on its actual basis
expected: Recorded acknowledgement that AR-04 no longer rests on 'a 5.1 script cannot call a final-path function without compiling code' (plan 01-17 compiles two declarations with Add-Type for the swap window) and that subst, mapped drive letters, DOS 8.3 names and IN-04 are still accepted on the owner's sign-off alone
result: pass

### 10. Run the suite on a Linux host and a macOS host
expected: On a Linux host and on a macOS host (CI leg or local, pwsh installed), each of these passes: `dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --filter "FullyQualifiedName~held_directory|FullyQualifiedName~HeldDirectoryTests"` (the swap-before-the-hold and retarget tests execute, not return early; truth 34); `--filter "FullyQualifiedName~TraceRunScriptTests"` (the log-below-missing-directories test runs, the staged log_hold test returns early by design; truth 45); `--filter "FullyQualifiedName~drive_root"` (the slash root is held as the slash; truth 55). Source: behavior_unverified_items[1]-[3] in 01-VERIFICATION.md
result: pass
evidence: "uat-runs/01-UAT-10-linux.md and uat-runs/01-UAT-10-macos.md; truths 34, 45 and 55 observed on WSL2 and macOS arm64 (see the Reviewer evaluation in each)"

### 11. Protected verdict by handle, end to end, and skip-on-failure of the hold
expected: Either (a) run end to end: with Open-HeldDirectory extracted into a child host (pattern: the drive_root driver in TraceRunScriptTests) and a protected list naming the log directory, the script ends with reason bad-arguments and six fixed lines before vdiff starts; and with the type made to fail compiling, a -LogPath run starts vdiff normally, writes no log and reports pass (truth 40). Or (b) accept the gap and take review finding WR-03 (add that test) as a follow-up. Source: behavior_unverified_items[0] in 01-VERIFICATION.md
result: pass
evidence: "option (a): uat-runs/01-UAT-11-windows.md (run-uat-11.ps1, C1-C3 pass on pwsh 7.7.0 and PowerShell 5.1 at 914b31a); WR-03 (a repository test) stays open"

### 12. A real drive-root run on a scratch volume (manual-only row of 01-VALIDATION.md, TRC-02 / CR-01)
expected: On a volume the owner controls, run vdiff with --out set to that volume's root from an unrelated current directory: five files appear in the root and none in the current directory
result: pass
evidence: "uat-runs/01-UAT-12-windows.md (run-uat-12.ps1 at e65ee44): --out T:\ from T:\sub on a subst drive; five files in T:\, none in T:\sub. T: is subst-backed by a folder on C:, not a separate physical volume"

### 13. Re-run the trial wrapper once on the private clone with the current scripts/trace-run.ps1 (recommended)
expected: Six-line block: pass, none, under budget, repo_unchanged yes, outputs_present yes, mermaid_syntax ok; only that block recorded
result: pass
evidence: "2026-11-19 re-run by the product owner with the rebuilt Release CLI at e65ee44 and later: result pass, reason none, wall_clock under-1-min, repo_unchanged yes, outputs_present yes, mermaid_syntax ok (block recorded in docs/trials/phase-1-offline-tracer.md)"

### 14. Eleven judgment-tier prohibitions (plans 01-16, 01-17, 01-18) flagged unverified-prohibition
expected: Reviewer confirms or overrules each non-authoritative verdict in the Prohibitions section of 01-VERIFICATION.md (all read as honoured; note WR-02 under the 01-16 message prohibition and under the 01-18 path prohibition)
result: pass
evidence: "owner confirmed all eleven as honoured on 2026-11-19; WR-02 (I/O error branch prints the held path) accepted as within the letter and stays open in 01-REVIEW-DISPOSITION.md"

## Summary

total: 14
passed: 13
issues: 1
pending: 0
skipped: 0
blocked: 0

## Gaps

- gap_id: G-01-1
  truth: "The --out guard (CLI CliApp.RefuseUnsafeOut / FinalPath.Resolve and scripts/trace-run.ps1 -Out/-LogPath) refuses deliberately obscure spellings too: both sides canonicalised through a final-path call (or a root starting with two backslashes refused on Windows), writes go through a temp name plus move, and the target is re-checked just before the write (closes CR-01, WR-01, WR-02)"
  status: resolved
  resolved_by: 01-17-PLAN.md
  resolved_at: 2026-11-19
  reason: "User reported: (b) — product owner chose a hardening gap-closure plan over accepting device-path (backslash-backslash-question-backslash) prefixes, loopback UNC, pre-planted hard links and post-check swaps as residuals"
  severity: major
  test: 1
  root_cause: "Test 1 cites CR-01/WR-01/WR-02 from the superseded review at 868a44a; plans 01-13/01-14/01-15 already close the four named forms at HEAD (extended-length and loopback-UNC roots refused as non-drive; planted hard link neutralised by temp-name plus move in OutputWriter and delete-then-CreateNew in Write-LogFile). Still open is one design property: the guard compares resolved path text and re-resolves by name at write time instead of taking identity from an open handle, so (1) a swap after the last check (CliApp.cs:60 to OutputWriter.WriteAll, including the serialisation window; trace-run.ps1 -LogPath after the re-check) is narrowed, not closed; (2) subst and mapped drive letters in --out pass the CLI guard and write inside the repository (reproduced); (3) on Windows PowerShell 5.1 an extensionless 8.3 -LogPath writes inside the repository; (4) the CLI has no vdiff-repository guard after the script's single -Out check. Items 2-3 are the AR-04 residuals signed off in test 6, so G-01-1 scope conflicts with that acceptance."
  artifacts:
    - path: "src/Vdiff.Core/Cli/FinalPath.cs"
      issue: "text segment walk; no handle-based final-path identity"
    - path: "src/Vdiff.Core/Cli/CliApp.cs"
      issue: "RefuseUnsafeOut (109-128) returns void; lexical outPath handed to WriteAll at 61"
    - path: "src/Vdiff.Core/Output/OutputWriter.cs"
      issue: "WriteAll/WriteText (43-90) re-resolve each output by name"
    - path: "scripts/trace-run.ps1"
      issue: "Get-FullPath relies on GetFullPath for 8.3 (fails on 5.1); Write-LogFile all by name; -Out checked once against vdiff root"
  missing:
    - "SCOPE (PO decision 2026-11-18): swap window only. Subst, mapped drive letters and 5.1 8.3 names stay accepted under AR-04 (test 6 sign-off stands); canonicalisation through GetFinalPathNameByHandle for those forms is OUT of scope"
    - "Pass resolved/held directory from RefuseUnsafeOut to WriteAll; hold out-dir handle without FILE_SHARE_DELETE, verify each temp file handle before writing"
    - "Close the -LogPath check-to-write window (temp name plus move into a held or handle-verified directory, or route the log write through the CLI); add a pre-write vdiff-root guard for -Out"
    - "Tests: held-handle rename/replace test during the write; update T-01-43/46 and the duplicated Bound comments"
  debug_session: ".planning/debug/out-guard-obscure-spellings-and-swaps.md"

## Deferred Follow-Ups

- test: 10
  idea: "Off-Windows test portability, found in the test 10 runs (not a product defect): (1) TraceRunScriptTests.A_core_fsmonitor_hook_is_never_run_by_the_script and RepoUnchangedMatrixTests.A_core_fsmonitor_hook_is_never_run_by_a_vdiff_run_or_by_the_snapshot write the hook without the execute bit, so their positive control fails on Linux and macOS and C47 is unverified off Windows (set File.SetUnixFileMode on the hook when not on Windows); (2) GitObjectReaderTests.ToplevelOrGitDir_* fail on macOS because SamePath (GitObjectReaderTests.cs:38) does not resolve /var to /private/var"
  deferred_at: 2026-11-19
