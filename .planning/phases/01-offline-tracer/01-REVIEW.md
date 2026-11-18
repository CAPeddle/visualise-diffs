---
phase: 01-offline-tracer
reviewed: 2026-11-18T00:00:00Z
depth: standard
files_reviewed: 11
files_reviewed_list:
  - scripts/trace-run.ps1
  - src/Vdiff.Core/Cli/CliApp.cs
  - src/Vdiff.Core/Cli/FinalPath.cs
  - src/Vdiff.Core/Output/OutputWriter.cs
  - src/Vdiff.Core/Vdiff.Core.csproj
  - tests/Vdiff.Tests/Safety/AliasedOutTests.cs
  - tests/Vdiff.Tests/Safety/FinalPathTests.cs
  - tests/Vdiff.Tests/Support/LinkScratch.cs
  - tests/Vdiff.Tests/Support/PathSpellings.cs
  - tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs
  - docs/handoffs/phase-01-gap-closure-interrupted-by-shutdown.md
findings:
  critical: 1
  warning: 2
  info: 3
  total: 6
  necessity: 1
status: issues_found
---

# Phase 01: Code Review Report (gap-closure re-review of plans 01-13 and 01-14)

**Reviewed:** 2026-11-18
**Depth:** standard
**Files Reviewed:** 11
**Status:** issues_found

## Summary

Scope: the files changed by plans 01-13 (CLI side) and 01-14 (script side), checked against the previous findings CR-01, WR-01 and WR-02. No structural (fallow) findings and no external reviewer evidence were supplied. The necessity rubric was read.

Disposition of the previous findings, verified against the code and by running it on this Windows host (PowerShell 7.7 and Windows PowerShell 5.1, a fresh Release build of the current source):

- **WR-01 (planted file link at an output name): closed on the CLI side.** `OutputWriter.WriteText` creates a random `*.tmp-*` name with `FileMode.CreateNew` and `FileShare.None`, then `File.Move(..., overwrite: true)`, which replaces the directory entry rather than writing through it. Failure cleanup deletes the temp file and rethrows the original exception. The hard-link test covers all five names. The file-symbolic-link half of the claim is not exercised on this host (see WR-01 below). The script side (`Write-LogFile`) also does what it says: I ran it on both hosts for a new file, a new nested directory, and a pre-existing file; all write correctly, so the `catch [System.IO.FileNotFoundException]` filters do match on 5.1.
- **WR-02 (check once, minutes before the write): mitigated as documented.** `RefuseUnsafeOut` now runs again immediately before `WriteAll`, and the script re-checks `$logFull` after the vdiff run. The remaining window is stated in the doc comments. The swap test drives the internal `Run` seam and does prove the second check fires.
- **CR-01 (non-drive namespaces): closed for the spellings the tests cover, but not closed.** Direct `\\?\`, `\\.\`, UNC and extended-UNC spellings are refused, and so is a link whose target is `Volume{...}`. The same defect class is still reachable through a link whose NT-namespace target is anything other than `Volume{`. Reproduced on both implementations (CR-01 below).

## Critical Issues

### CR-01: A directory junction with a non-`Volume{` NT-namespace target bypasses both containment guards; vdiff and trace-run.ps1 write inside the analysed repository

**File:** `src/Vdiff.Core/Cli/FinalPath.cs:82-98, 115-116`, `scripts/trace-run.ps1:210-220`
**Issue:** Both resolvers special-case exactly one NT-namespace spelling (`Volume{`) and otherwise trust what the runtime reports as the link target.

- .NET (`DirectoryInfo.LinkTarget` / `ResolveLinkTarget`) and PowerShell (`Get-Item .Target`) both strip the `\??\` prefix from a junction's substitute name unconditionally. For a target such as `\??\GLOBALROOT\Device\HarddiskVolume3\<path to repo>` the reported target is the relative-looking text `GLOBALROOT\Device\HarddiskVolume3\<path to repo>`.
- `IsVolumeGuidTarget` does not match it. `ResolveLinkTarget` then treats it as relative to the link's parent, so `target.FullName` is a drive-letter path (`C:\...\scratch\GLOBALROOT\Device\...`) that passes `StartsWithDriveLetter`, matches nothing in the repository, and `Step` returns a lexical path that is not under the repository. In the script the same happens: `IsPathRooted` is false, so the target is combined with the link's parent.
- Creating such a junction needs no privilege (a plain `FSCTL_SET_REPARSE_POINT` on an empty directory). The same path is open to a symbolic link with a `\??\UNC\host\share\...` target, which the script turns into `UNC\host\share\...` and treats as relative.

Reproduced on this machine against a synthetic empty repository (`base == head`), with a junction `scratch\gr` whose substitute name was `\??\GLOBALROOT\Device\HarddiskVolume3\<repo path>`. `Get-Item gr` reports `LinkType: Junction`, `Target: GLOBALROOT\Device\HarddiskVolume3\...` on both PowerShell hosts; `DirectoryInfo.LinkTarget` reports the same.

```
vdiff.dll --repo <repo> --base c --head c --out <scratch>\gr\outx
  -> vdiff: wrote 5 files to <scratch>\gr\outx ; exit 0
git status --short  ->  ?? outx/

trace-run.ps1 -Repo <repo> ... -Out <elsewhere> -LogPath <scratch>\gr\vdiff.log   (pwsh and powershell 5.1)
  -> vdiff.log created inside <repo>; result: fail, reason: repo-changed
```

So the roadmap guarantee ("the analysed repository is never changed") is still broken by one more spelling of the same alias, and the doc comments ("a link whose target is a volume GUID path [is] refused, not resolved") describe a blacklist of one form while implying the namespace class is closed. The script only detects the damage afterwards, as `repo-changed`.

**Fix:** Replace the blacklist with an allow-list on the raw target, in both implementations. On Windows, after the single `\??\` strip, accept a link target only when it is drive-rooted (`^[A-Za-z]:\\`) or is a genuinely relative symbolic-link target; refuse a junction (always absolute) whose reported target is not drive-rooted, and refuse any target whose first segment is an NT namespace name (`GLOBALROOT`, `UNC`, `Volume{`, `Device`). Minimal version:

```csharp
// FinalPath.Step, replacing the IsVolumeGuidTarget call
if (OperatingSystem.IsWindows() && !IsAcceptableWindowsTarget(info.LinkTarget))
{
    throw new VdiffException(UnsupportedRootMessage);
}

static bool IsAcceptableWindowsTarget(string raw)
{
    var t = raw.TrimStart('\\', '?', '.');
    if (t.StartsWith("Volume{", StringComparison.OrdinalIgnoreCase)) return false;
    var first = t.Split('\\', 2)[0];
    return !(first.Equals("GLOBALROOT", StringComparison.OrdinalIgnoreCase)
          || first.Equals("UNC", StringComparison.OrdinalIgnoreCase)
          || first.Equals("Device", StringComparison.OrdinalIgnoreCase));
}
```

```powershell
# Resolve-FinalPathStep, next to the Volume{ test
if ($target -match '^(?i)(GLOBALROOT|UNC|Device|Volume\{)') { throw 'nt namespace link target' }
```

Add a test that builds such a junction without privilege (the `FSCTL_SET_REPARSE_POINT` route, or `mklink /J` followed by a reparse rewrite) for both `FinalPathTests`/`AliasedOutTests` and `TraceRunScriptTests` (`-Out` and `-LogPath`), asserting `UnsupportedRootMessage` and `script-error` respectively, alongside the existing `Volume{` cases. Also update the T-01-41 wording so it says what is refused.

## Warnings

### WR-01: The file-symbolic-link half of the output-name test passes vacuously on a host without the privilege

**File:** `tests/Vdiff.Tests/Safety/AliasedOutTests.cs:272-295`, `tests/Vdiff.Tests/Support/LinkScratch.cs:83-96`
**Issue:** `A_file_symbolic_link_planted_at_an_output_name_is_replaced_and_its_target_is_unchanged` returns early with a pass when `TryFileLink` returns false. On Windows that needs Developer Mode or elevation; on this development host `New-Item -ItemType SymbolicLink` fails with "Administrator privilege required", so the claim in `OutputWriter`'s doc comment and in T-01-42 ("a hard link or file symbolic link planted at a final name is replaced") is proven here only for hard links. xUnit reports the test as passed, so a missing privilege is indistinguishable from success.
**Fix:** Make the skip visible (xUnit v3 `Assert.Skip`, or a `SkippableFact`-style dynamic skip with a reason), and note in the test that the Ubuntu leg exercises it. The behaviour is very likely correct (`MoveFileEx`/`rename` replace the entry), but it should not be reported as covered.

### WR-02: Necessity: a second `Test-NonDriveRoot $logFull` after the vdiff run re-checks a string that cannot have changed

**File:** `scripts/trace-run.ps1:391`, pinned by `tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs:322`
**Category:** necessity
**Issue:** The guarded state (a log path that became non-drive-rooted between the first check and the write) cannot occur: `$logFull` is assigned once (line 360) from the parameter and never reassigned, and `Test-NonDriveRoot` is a pure text test with no file-system input (line 168-171). The second check can never differ from the first. The check that does carry the T-01-46 value is the second `Test-SameOrUnder`, which reads the file system. This is not defence in depth: that rule applies to a layer that re-reads mutable state. The source-order test then pins the redundant call (`Assert.Contains("Test-NonDriveRoot $logFull", between)`), so the dead check is now load-bearing for the test suite.
**Fix:** Drop the second `Test-NonDriveRoot` call and the matching assertion; keep the second `Test-SameOrUnder`.

## Info

### IN-01: Stale handoff committed to a public repository

**File:** `docs/handoffs/phase-01-gap-closure-interrupted-by-shutdown.md:1-47`
**Issue:** The handoff describes plan 01-13 as "in progress", 01-14 as "not started" and the phase gates as "not run", and tells a reader to re-dispatch an executor. HEAD already contains both plans' commits and SUMMARYs, so the document is false for any new reader. It also carries a machine-local path (`C:/projects/shared-copilot/docs/investigations/.../check-public-diff.sh`), a GitHub account name and a session shutdown narrative, none of which belong in a public repository's tracked docs.
**Fix:** Delete the file (it remains in git history), or mark it superseded at the top; do not leave it as the resume point.

### IN-02: Source-text pin and silent non-Windows passes in the script tests

**File:** `tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs:306-332, 480-482, 515-517, 549-551, 578-580, 602-604`
**Issue:** `Log_write_is_preceded_by_a_second_containment_check_in_source_order` asserts on substrings of the script text, so a comment containing `Test-SameOrUnder $logFull` satisfies it and a refactor that keeps behaviour breaks it; it proves ordering of text, not of execution. Separately, the class summary says a missing host "fails the test; it never skips", yet five tests `return` silently on non-Windows and report as passed. Both overstate coverage.
**Fix:** Keep the order test only as a documented backstop (the comment already says so) and strip comments before searching; mark the Windows-only theories with a visible skip instead of an early `return`.

### IN-03: `TooManyLinksException` is thrown for unrelated conditions

**File:** `src/Vdiff.Core/Cli/FinalPath.cs:73, 96`
**Issue:** The internal signal is thrown for a null `GetPathRoot` and for a null `ResolveLinkTarget`, neither of which is a link budget overrun. Both are converted to the same fixed message, so behaviour is right, but the name misleads the next maintainer.
**Fix:** Rename it (for example `UnresolvableLinkException`) or throw it with a reason string that is never printed.

---

_Reviewed: 2026-11-18_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
