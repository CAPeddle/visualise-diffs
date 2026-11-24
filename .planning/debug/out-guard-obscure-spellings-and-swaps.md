---
status: diagnosed
trigger: "out-guard-obscure-spellings-and-swaps (UAT 01-offline-tracer test 1, gap G-01-1): PO chose (b), hardening gap-closure plan for the --out guard (CLI + scripts/trace-run.ps1) against extended-length prefix, loopback UNC, pre-planted hard link in --out, swap after last check"
created: 2026-11-18T00:00:00Z
updated: 2026-11-18T00:00:00Z
---

## Current Focus

hypothesis: CONFIRMED (two-part). (1) The four named forms are already closed at HEAD by plans 01-13/01-14/01-15, except the swap interval after the last check; UAT test 1 / G-01-1 was authored from the first verification (2026-11-17 15:02) before those plans existed. (2) What is really still open is one design property: the guard compares path TEXT (lexical walk plus link replacement) and re-resolves by NAME at write time, instead of taking identity from an open handle (GetFinalPathNameByHandle) and writing relative to it. That leaves subst and mapped drive letters, one 8.3 form on Windows PowerShell 5.1, and the check-to-write window.
test: ran every named form plus extra spellings against the Release build of HEAD on this Windows host with synthetic repos (CLI, pwsh 7, Windows PowerShell 5.1); probed GetFinalPathNameByHandle for each spelling.
expecting: n/a (diagnose-only)
next_action: return ROOT CAUSE FOUND to the orchestrator (no source modified)

## Symptoms

expected: The guard that keeps vdiff's outputs (CLI --out, the five output files) and trace-run.ps1's -Out/-LogPath writes outside the analysed repository and the vdiff repository holds against deliberately obscure path spellings: the \\?\ extended-length prefix, loopback UNC (\\localhost\C$\..., \\127.0.0.1\...), a hard link pre-planted inside --out pointing into the repository, and a link swap made after the last check.
actual: User reported "(b)": the product owner rejected accepting these as residuals and chose a hardening gap-closure plan.
errors: None reported
reproduction: UAT test 1 (01-UAT.md); original re-review findings CR-01, WR-01, WR-02 (git 868a44a:.planning/phases/01-offline-tracer/01-REVIEW.md)
started: Discovered during UAT 2026-11-18 (the UAT item itself was created 2026-11-17)

## Eliminated

- hypothesis: "The four named forms (extended-length, loopback UNC, planted hard link) still write into the repository at HEAD"
  evidence: exp1/exp2 on the Release build of HEAD. CLI: \\?\, \\.\, \\localhost\C$, \\127.0.0.1\C$, \\?\UNC\localhost\C$ all exit 1 with the fixed UnsupportedRootMessage and git status clean. Hard link at summary.mmd inside a real --out: exit 0, repo file unchanged, link count 1 afterwards. Script (pwsh 7): -Out in both namespaces gives out-not-outside; hard link at -LogPath gives result pass with the repo file unchanged.
  timestamp: 2026-11-18

- hypothesis: "An 8.3 short name bypasses the CLI guard"
  evidence: --out <S>\LONGRE~1\out-83 exits 1 ("must not lie inside"). .NET (Core) Path.GetFullPath expands an existing 8.3 segment before the lexical compare. Same for pwsh 7. NOT eliminated for Windows PowerShell 5.1 -LogPath (see Evidence 9).
  timestamp: 2026-11-18

- hypothesis: "Trailing dot, trailing space, ADS (::$INDEX_ALLOCATION), dot-dot or case spellings bypass the CLI guard"
  evidence: trailing dot, dot-dot, case: refused (exit 1). Trailing space: exit 0 but wrote to a literal sibling directory named with a trailing space (extended-path semantics), git status clean, so not inside the repo. ADS: the guard PASSED (text differs from the repo path) and only the file system refused (CreateDirectory: "filename, directory name, or volume label syntax is incorrect"); fail-closed by the OS, not by the guard.
  timestamp: 2026-11-18

- hypothesis: "Repo reached through a subst drive with --out in the real spelling bypasses"
  evidence: exit 1 "must not lie inside". The protected roots come from git, which reports the real path; only the --out side is unresolved.
  timestamp: 2026-11-18

## Evidence

- timestamp: 2026-11-18
  checked: git history of 01-UAT.md, 01-13/01-14/01-15 plans, 01-VERIFICATION.md
  found: UAT test 1 first persisted at 970e6e2 (2026-11-17 15:02) from verification ca0d004; gap-closure plans 01-13/01-14 created at b856aab (2026-11-17 16:17); plan 01-15 later; UAT regenerated at 29cb08f (2026-11-18 13:26), carrying test 1 forward as pending. The CR-01/WR-01/WR-02 the UAT names are the findings of git 868a44a (re-review of plans 01-11/01-12: CR-01 = \\?\ and loopback UNC, WR-01 = pre-existing file link in --out, WR-02 = single check long before the write). They are NOT the same ids as in the current 01-REVIEW.md (re-review of 01-15), where CR-01 is the GLOBALROOT junction and is already resolved, and WR-01/WR-02 mean other things.
  implication: the (b) decision matches work already executed in 01-13/14/15; the open question is what still bypasses at HEAD.

- timestamp: 2026-11-18
  checked: UAT test 6 (01-UAT.md line 37-39)
  found: Product-owner sign-off on AR-04 (T-01-39: subst and mapped drive letters, DOS 8.3 short names, IN-04) is recorded as `pass` in the same UAT where test 1 got "(b)".
  implication: CONFLICT to surface: if G-01-1 is meant to include subst/mapped/8.3 spellings, test 6's acceptance must be withdrawn; if it is meant to be the four named forms only, three are closed and only the swap window remains.

- timestamp: 2026-11-18
  checked: exp1 (CLI, Release build at HEAD, synthetic repo)
  found: baseline outside --out writes 5 files. \\?\<repo>\out, \\.\<repo>\out, \\localhost\C$\..., \\127.0.0.1\C$\..., \\?\UNC\localhost\C$\... all exit 1 with UnsupportedRootMessage; nothing created.
  implication: forms 1 and 2 are closed in the CLI by FinalPath.Step lines 74-77 (StartsWithDriveLetter, lines 121-122), reached from CliApp.RefuseUnsafeOut line 116.

- timestamp: 2026-11-18
  checked: exp2 (hard link, script spellings)
  found: CLI hard link at summary.mmd -> repo README: exit 0, README unchanged, link count afterwards 1 (entry replaced). Script -LogPath hard link: pass, README unchanged. Script -Out \\?\ and \\localhost\C$ on pwsh 7: out-not-outside.
  implication: form 3 neutralised by OutputWriter.WriteText (temp name, FileMode.CreateNew, File.Move overwrite; lines 63-90) and by Write-LogFile (lines 300-316: GetAttributes, Delete entry, CreateNew).

- timestamp: 2026-11-18
  checked: exp3/exp5 (subst drive Q: -> scratch root, CLI and script)
  found: CLI --out Q:\LongRepositoryName\out-subst: exit 0, git status shows `?? out-subst/` (five files written INSIDE the repository). Script -Out Q:\...: reason repo-changed (repo_unchanged: no), git status `?? out-s1/`. Script -LogPath Q:\...\log-s2.txt: reason repo-changed, git status `?? log-s2.txt` (log written inside the repo). The script notices only after the write.
  implication: subst drive letters are a live bypass in CLI and script (accepted residual T-01-39 / AR-04, UAT test 6).

- timestamp: 2026-11-18
  checked: exp10 (net use Y: \\localhost\C$\<scratch>, CLI)
  found: --out Y:\LongRepositoryName\out-mapped: exit 0, git status `?? out-mapped/`. Mapping and subst removed afterwards (verified).
  implication: mapped drive letters (the second AR-04 form) bypass too.

- timestamp: 2026-11-18
  checked: exp4/exp11 (GetFinalPathNameByHandle, flags 0, handle opened with FILE_FLAG_BACKUP_SEMANTICS, share 7)
  found: subst Q:\LongRepositoryName -> \\?\C:\...\LongRepositoryName (real path); 8.3 LONGRE~1 -> long name; upper-case spelling -> real case; \\?\C:\... -> \\?\C:\...; loopback \\localhost\C$\... -> \\?\UNC\localhost\C$\... (NOT folded to the local drive); mapped Y: -> \\?\UNC\localhost\C$\... .
  implication: a final-path call folds subst, 8.3, case and \\?\C:, but cannot fold loopback UNC or a mapped drive; those stay `\\?\UNC\...`. So the two arms of (b) are complementary: canonicalise AND refuse a non-drive-letter result.

- timestamp: 2026-11-18
  checked: exp6/exp8 ([IO.Path]::GetFullPath of <S>\LONGRE~1\<leaf>, pwsh 7 vs Windows PowerShell 5.1)
  found: pwsh 7 (.NET Core) expands the existing 8.3 segment for every leaf. Windows PowerShell 5.1 (.NET Framework) expands it only for some leaves: expanded=True for log83.txt and summary.mmd; expanded=False for x, out83, out-s1, a.b.c, vdifflog. GetUnresolvedProviderPathFromPSPath never expands.
  implication: the "same rule, two implementations" in the script behaves differently per host; Get-FullPath (trace-run.ps1 lines 146-154) relies on GetFullPath for 8.3 and 5.1 does not do it reliably.

- timestamp: 2026-11-18
  checked: exp7/exp9 (8.3 spelling through trace-run.ps1 on both hosts)
  found: powershell.exe 5.1 -LogPath <S>\LONGRE~1\vdifflog: reason repo-changed, git status `?? vdifflog` (log written inside the repo). pwsh 7 same input: bad-arguments (refused). 5.1 -Out <S>\LONGRE~1\out83: script guard passes but vdiff (.NET 10) refuses, reason tool-failed, repo unchanged. 5.1 -LogPath <S>\LONGRE~1\log83.txt: refused (expanded).
  implication: 8.3 is a live bypass for -LogPath on Windows PowerShell 5.1 (script alone writes the log); for -Out the CLI's own guard saves it.

- timestamp: 2026-11-18
  checked: code, form 4 (swap after the last check), CLI
  found: CliApp.cs line 60 is the last containment check; RefuseUnsafeOut (lines 109-128) returns void, so line 61 passes the LEXICAL outPath to OutputWriter.WriteAll. OutputWriter.WriteAll line 46 Directory.CreateDirectory(outDir) and each of the five WriteText calls (lines 47-51, argument Serialize(...) evaluated between writes) re-resolve outDir by name: line 66 Path.Combine(outDir, fileName), line 70 FileStream CreateNew, line 75 File.Move. A directory or ancestor swapped for a junction into the repo after line 60 receives the files, and File.Move(overwrite: true) replaces a same-named tracked file. The window includes JSON serialisation of the base, head and diff models, so it grows with model size and is not "a millisecond". Documented as accepted residual (CliApp.cs lines 103-107, T-01-43).
  implication: re-check is a narrowing, not a closure. Only identity from a held handle closes it.

- timestamp: 2026-11-18
  checked: code, form 4, script
  found: -Out is judged once at trace-run.ps1 lines 361-365 against $protected (lines 348-359: vdiff root, work tree, git dir, common dir, raw -Repo); line 385 hands $outFull to vdiff. vdiff's own checks (CliApp.cs 111-119) know only the analysed repository's Root/GitDir/CommonDir, so the vdiff-repository root has no check after line 365 (and the CLI by itself has no vdiff-repo guard). -LogPath: first check lines 367-373; re-check lines 399-408 (Test-SameOrUnder, fully re-resolved); then Write-LogFile (lines 300-316) does New-Item for a missing parent (302), GetAttributes (304), [IO.File]::Delete(path) (310), [IO.File]::Open(path, CreateNew) (313), every step by name. Not temp-name plus move, although the gap text asks for it. Between 403 and 313 a parent swapped for a junction receives the log; a symbolic link planted between 310 and 313 is followed by CreateNew when dangling (the function's own comment says so).
  implication: the script's write is the weaker of the two implementations; UAT test 3 (pass) only exercises a swap during the 20 s run, before the re-check.

## Resolution

root_cause: |
  Two layers.
  (A) Stale gap. UAT test 1 / G-01-1 was written on 2026-11-17 from the first verification, before plans 01-13, 01-14 and 01-15 were planned and executed. Those plans already implement the three items of option (b): refusal of any Windows path or link target whose text is not `X:\...` (FinalPath.Step 74-77 and 98-101, StartsWithDriveLetter 121-122; Test-NonDriveRoot 172-175 used at 203, 226, 362, 369), temp-name plus File.Move writes (OutputWriter.WriteText 63-90; Write-LogFile 300-316 uses delete-by-name plus CreateNew instead), and a second containment check before the write (CliApp.cs 60; script 399-408). Verified live: extended-length, device, loopback UNC and planted hard link are refused or neutralised in the CLI and the script. The UAT item was carried forward unchanged at 29cb08f and answered "(b)" again.
  (B) What is really still open is one design property. The guard compares path TEXT (a segment walk that replaces link segments, FinalPath.Step; Resolve-FinalPathStep) and re-resolves by NAME at write time, instead of taking identity from an open handle. Consequences, all reproduced at HEAD: (1) a subst or mapped drive letter spelling of the repository is a different string and is not a link, so CLI and script write inside the repository (script detects only afterwards); (2) the script's Get-FullPath relies on .NET GetFullPath to expand 8.3 names and Windows PowerShell 5.1 does not for every leaf, so -LogPath <8.3 name>\vdifflog writes inside the repository on 5.1; (3) the check-to-write interval is non-atomic by construction: RefuseUnsafeOut returns void and the write re-resolves the lexical path (CliApp.cs 60-61, OutputWriter.cs 46-75), -Out has no pre-write check against the vdiff root, and Write-LogFile is check-then-by-name.
fix: not applied (diagnose-only)
verification: not applicable (diagnose-only); evidence above is from running the Release build of HEAD against synthetic repos in the session scratchpad; all subst and net use mappings removed afterwards
files_changed: []
