---
status: diagnosed
phase: 01-offline-tracer
source: [01-VERIFICATION.md]
started: 2026-11-17T00:00:00Z
updated: 2026-11-18T14:45:00Z
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

## Summary

total: 7
passed: 6
issues: 1
pending: 0
skipped: 0
blocked: 0

## Gaps

- gap_id: G-01-1
  truth: "The --out guard (CLI CliApp.RefuseUnsafeOut / FinalPath.Resolve and scripts/trace-run.ps1 -Out/-LogPath) refuses deliberately obscure spellings too: both sides canonicalised through a final-path call (or a root starting with two backslashes refused on Windows), writes go through a temp name plus move, and the target is re-checked just before the write (closes CR-01, WR-01, WR-02)"
  status: failed
  reason: "User reported: (b) — product owner chose a hardening gap-closure plan over accepting device-path (backslash-backslash-question-backslash) prefixes, loopback UNC, pre-planted hard links and post-check swaps as residuals"
  severity: major
  test: 1
  root_cause: "Test 1 cites CR-01/WR-01/WR-02 from the superseded review at 90b26a4; plans 01-13/01-14/01-15 already close the four named forms at HEAD (extended-length and loopback-UNC roots refused as non-drive; planted hard link neutralised by temp-name plus move in OutputWriter and delete-then-CreateNew in Write-LogFile). Still open is one design property: the guard compares resolved path text and re-resolves by name at write time instead of taking identity from an open handle, so (1) a swap after the last check (CliApp.cs:60 to OutputWriter.WriteAll, including the serialisation window; trace-run.ps1 -LogPath after the re-check) is narrowed, not closed; (2) subst and mapped drive letters in --out pass the CLI guard and write inside the repository (reproduced); (3) on Windows PowerShell 5.1 an extensionless 8.3 -LogPath writes inside the repository; (4) the CLI has no vdiff-repository guard after the script's single -Out check. Items 2-3 are the AR-04 residuals signed off in test 6, so G-01-1 scope conflicts with that acceptance."
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
    - "Decide scope: swap window only, or also subst/mapped/5.1-8.3 (withdraws test 6 AR-04 acceptance)"
    - "Canonicalise --out and protected roots through GetFinalPathNameByHandle on an opened ancestor; refuse non-drive results (incl. UNC)"
    - "Pass resolved/held directory from RefuseUnsafeOut to WriteAll; hold out-dir handle without FILE_SHARE_DELETE, verify each temp file handle before writing"
    - "Harden or route -LogPath through the CLI; do not rely on GetFullPath for 8.3 on 5.1; add a pre-write vdiff-root guard for -Out"
    - "Tests: subst fixture, loopback net use (visible skip), 5.1 8.3 -LogPath, held-handle rename test; update T-01-39/43/46, AR-04 and the duplicated Bound comments"
  debug_session: ".planning/debug/out-guard-obscure-spellings-and-swaps.md"
