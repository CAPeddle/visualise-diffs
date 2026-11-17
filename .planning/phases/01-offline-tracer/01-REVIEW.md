---
phase: 01-offline-tracer
reviewed: 2026-11-17T00:00:00Z
depth: standard
files_reviewed: 12
files_reviewed_list:
  - scripts/trace-run.ps1
  - src/Vdiff.Core/Cli/CliApp.cs
  - src/Vdiff.Core/Cli/FinalPath.cs
  - src/Vdiff.Core/Output/MermaidWriter.cs
  - tests/Vdiff.Tests/Output/DeterminismTests.cs
  - tests/Vdiff.Tests/Output/MermaidCliSyntaxTests.cs
  - tests/Vdiff.Tests/Output/MermaidLint.cs
  - tests/Vdiff.Tests/Output/MermaidWriterTests.cs
  - tests/Vdiff.Tests/Safety/AliasedOutTests.cs
  - tests/Vdiff.Tests/Safety/FinalPathTests.cs
  - tests/Vdiff.Tests/Support/LinkScratch.cs
  - tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs
findings:
  critical: 1
  warning: 2
  info: 4
  total: 7
  necessity: 0
status: issues_found
---

# Phase 01: Code Review Report (gap-closure re-review)

**Reviewed:** 2026-11-17
**Depth:** standard
**Files Reviewed:** 12
**Status:** issues_found

## Scope note

This report covers only the gap-closure diff `fe6c08e..HEAD` (plans 01-11 and 01-12) for the files listed above. It supersedes nothing outside that scope. The earlier full phase review stays in git at `d120fc1`; its findings and their disposition are in `01-REVIEW-DISPOSITION.md`. No structural (fallow) findings were supplied and no external reviewer evidence was supplied. The necessity rubric (`necessity-review.md`) was read; no necessity findings result (the second, resolved containment check is deliberate defence in depth, not a guard against an impossible state).

## Summary

Plan 01-11 (the `C_` class-id prefix) is correct. `ClassId` is the only place an id is produced, `Render` threads the same `ids` list into `AppendClass` and into every edge endpoint in `Edges`, and `MermaidLint` plus the writer tests now fail on a bare id or an unprefixed edge endpoint. Nothing else in `src` or `tools` reads or builds class ids. The removed "T before a leading digit" and empty-id branches are correctly dead now that every id starts with `C_`.

Plan 01-12 (link resolution before the `--out` containment test) closes the junction and symbolic-link aliasing that gap 2 reproduced. I ran `FinalPathTests`, `AliasedOutTests`, `MermaidWriterTests` and the eight `Aliased_*` script tests on both PowerShell hosts: 73 pass. The link walk itself is sound: loop and ancestor-cycle detection terminate (the shared counter is never reset on recursion), relative targets are combined against the already-resolved prefix, dangling links resolve to their missing target, and every resolver failure becomes the one fixed message.

However, the guard still has a reproducible hole. The three named alias forms the plan accepted as residual (subst drives, 8.3 names) are not the only ones: Windows path namespaces that never go through a link at all (`\\?\C:\...`, loopback UNC `\\localhost\C$\...`) pass both the lexical and the resolved test and write inside the repository. I reproduced this against `vdiff.dll`. Two further ways to land bytes in the repository (a pre-existing file link in `--out`, and a check-then-write gap) are listed as warnings.

## Critical Issues

### CR-01: `--out` written as `\\?\C:\...` or `\\localhost\C$\...` bypasses the containment guard and writes inside the analysed repository

**File:** `src/Vdiff.Core/Cli/CliApp.cs:84-103`, `src/Vdiff.Core/Cli/FinalPath.cs:49-79`, `scripts/trace-run.ps1:166-208`
**Issue:** `Path.GetFullPath` on Windows keeps the `\\?\` extended prefix and the `\\localhost\C$` UNC root verbatim. `IsSameOrUnder` therefore compares `\\?\C:\repo\out` against `C:\repo` and says "not inside". `FinalPath.Resolve` then walks from the root `\\?\C:\` (or `\\localhost\C$`), finds no link, and returns the same spelling, so the second (resolved) test also says "not inside". Both namespaces address the same files as `C:\repo`, so `Directory.CreateDirectory` and the five writes land inside the repository.

Reproduced on this machine with a synthetic repo (commit `c`, `base == head`):

```
vdiff --repo C:\...\er\repo --base c --head c --out \\?\C:\...\er\repo\out-ext
  -> vdiff: wrote 5 files to \\?\C:\...\er\repo\out-ext ; exit 0
vdiff --repo C:\...\er\repo --base c --head c --out \\localhost\C$\...\er\repo\out-unc
  -> vdiff: wrote 5 files to \\localhost\C$\...\er\repo\out-unc ; exit 0
git status --short  ->  ?? out-ext/   ?? out-unc/
```

This is the same defect class as the original WR-01 ("an alias of the repository path"), is not a symlink or junction, and is not covered by the T-01-39 residual, which names only subst drive letters and 8.3 short names. The doc comments and the 01-12 SUMMARY therefore under-state the remaining gap. `trace-run.ps1` has the same hole: `Get-FullPath` keeps both prefixes, so `-Out \\?\C:\repo\out` passes `Test-SameOrUnder`, vdiff writes into the repo, and the script only notices afterwards (`repo_unchanged: no`, reason `repo-changed`), after the write has happened. The same applies to a link whose target is a volume GUID path (`\??\Volume{guid}\...`): the script strips `\??\`, gets a non-rooted `Volume{guid}\...`, combines it with the link's parent, and returns a garbage path that matches nothing (fail open, not fail closed).

**Fix:** Normalise the namespace before both comparisons, or fail closed on any form the resolver cannot canonicalise. The robust option is to compare identity rather than spelling: open the deepest existing ancestor of `--out` and of each protected directory and take the final path (`GetFinalPathNameByHandle`, then strip `\\?\` / map `\\?\UNC\`), which also fixes subst and 8.3 in the same stroke. If a P/Invoke is unwanted (the script runs on 5.1 without compiled code), the minimum is to refuse, on Windows, any `--out`/`-Out`/`-LogPath` that starts with `\\?\`, `\\.\`, or a UNC root, and any link target that does not parse as `X:\` or a plain UNC path:

```csharp
if (OperatingSystem.IsWindows())
{
    var root = Path.GetPathRoot(Path.GetFullPath(path)) ?? "";
    if (root.StartsWith(@"\\", StringComparison.Ordinal))   // \\?\, \\.\, \\server\share
    {
        throw new VdiffException(UnresolvableMessage);
    }
}
```

Add tests for `\\?\<repo>\out`, `\\localhost\<drive>$\...\out`, and the PowerShell equivalents, and add the forms to the T-01-39 text if they are deliberately left as residual.

## Warnings

### WR-01: A pre-existing file link in `--out` redirects a write into the repository

**File:** `src/Vdiff.Core/Output/OutputWriter.cs:47-62` (reached from `src/Vdiff.Core/Cli/CliApp.cs:51`)
**Issue:** The guard resolves the `--out` directory only. `OutputWriter.WriteText` calls `File.WriteAllBytes(Path.Combine(outDir, name), ...)`, which follows a file symbolic link and writes through a hard link. If `--out` is a real directory outside the repo that already contains `summary.mmd` (or any of the five names) as a link or hard link to a repo file, the tool overwrites that repo file. Reproduced: `mklink /H out\summary.mmd repo\README.txt`, run vdiff with `--out out`, exit 0, `git status` shows ` M README.txt` (content replaced by the Mermaid text). This is operator-planted, but the roadmap criterion is "nothing outside --out is written, the analysed repository is never changed" and nothing in the guard or its documented residual covers it.
**Fix:** Before each write, refuse an existing target that is a reparse point or has more than one hard link, or write to a temp name in `outDir` and `File.Move(temp, final, overwrite: true)` (replaces the directory entry instead of writing through it):

```csharp
var target = Path.Combine(outDir, fileName);
var info = new FileInfo(target);
if (info.Exists && info.LinkTarget is not null)
{
    throw new VdiffException("--out holds a link where an output file goes");
}
var temp = Path.Combine(outDir, fileName + ".tmp-" + Guid.NewGuid().ToString("N"));
File.WriteAllBytes(temp, bytes);
File.Move(temp, target, overwrite: true);
```

### WR-02: Containment is checked once, minutes before the write

**File:** `src/Vdiff.Core/Cli/CliApp.cs:43-51`
**Issue:** `RefuseUnsafeOut` runs before `TracerPipeline.Run`, which may take up to the 20-minute budget; `OutputWriter.WriteAll` then calls `Directory.CreateDirectory(outPath)` on the lexical path. A link (or a directory the operator renames or replaces) that changes between the check and the write defeats the guard, and the resolved result is thrown away rather than used for the write. Likewise `trace-run.ps1` checks at lines 300-310 and writes the log at line 336 after a run of up to 25 minutes. The 01-12 plan does not list this as accepted residual.
**Fix:** Cheapest: call `RefuseUnsafeOut(outPath, location)` again immediately before `OutputWriter.WriteAll` (and have the script re-run its `Test-SameOrUnder` for `$logFull` just before `WriteAllText`). Better: have `RefuseUnsafeOut` return the resolved out path and write to that, so the checked and written paths are the same string.

## Info

### IN-01: The `IOException` branch still prints .NET exception text with full paths

**File:** `src/Vdiff.Core/Cli/CliApp.cs:60-62`
**Issue:** T-01-38 ("an unresolvable link making the CLI print a .NET exception message that carries a full path") is closed for the resolver only. Any other `IOException` or `UnauthorizedAccessException` still reaches `Fail(stderr, "could not write the output: " + e.Message)`, which prints the path (observed above: `The filename, directory name, or volume label syntax is incorrect. : 'C:\...'`). The path is the operator's own, so this is low risk, but the threat register entry reads as if the leak is gone.
**Fix:** Print a fixed message (`"could not write the output"`) and the exception type name, as the generic branch already does, or narrow the T-01-38 wording to "resolver failures".

### IN-02: Relative link targets are never exercised on Windows

**File:** `tests/Vdiff.Tests/Safety/FinalPathTests.cs:78-88`, `tests/Vdiff.Tests/Support/LinkScratch.cs:33-52`, `scripts/trace-run.ps1:193`
**Issue:** `LinkScratch.Link` makes an absolute-target junction on Windows and ignores `relativeTarget`, so `A_relative_link_target_is_resolved_from_the_links_own_directory` is identical to the absolute-target test on the development OS, and the PowerShell branch `if (-not [IO.Path]::IsPathRooted($target)) { Combine($prefix, $target) }` is only covered on non-Windows hosts (if the test suite runs there at all). The code reads correct, but it is unproven where this project is developed.
**Fix:** On Windows, create the relative case with a real symlink when the process may (`Directory.CreateSymbolicLink`, works in Developer Mode) and skip-with-reason only if the privilege is missing; or state in the test that the branch is covered by the Ubuntu leg of the pipeline.

### IN-03: Rooted-without-drive and drive-relative link targets resolve against the wrong drive

**File:** `scripts/trace-run.ps1:190-194`, `src/Vdiff.Core/Cli/FinalPath.cs:66-67`
**Issue:** A symbolic link target of `\foo` or `C:foo` counts as rooted (`IsPathRooted` is true) and is then expanded with the process's current drive or directory, not the link's. Rare (needs a hand-made symlink), but the result is a silent mis-resolution rather than a refusal, which the stated rule ("never a guess") forbids.
**Fix:** In both implementations, when the target is rooted but `Path.GetPathRoot(target)` is not a full `X:\` or UNC root, combine it with the link's resolved prefix root or throw.

### IN-04: Case-insensitive file systems outside Windows compare case-sensitively

**File:** `src/Vdiff.Core/Cli/CliApp.cs:112`, `scripts/trace-run.ps1:46`
**Issue:** `IsSameOrUnder` uses `Ordinal` on every non-Windows OS. On macOS (APFS default is case-insensitive), `--out /Users/x/Repo/out` against a repo at `/Users/x/repo` is "outside". The resolver does not canonicalise case either. The product has a macOS frontend, so this will matter when the tool runs there. Not a regression of this diff, but the new resolved check inherits it.
**Fix:** Compare with `OrdinalIgnoreCase` when the volume is case-insensitive (probe by checking whether the lower-cased and upper-cased spelling of the repo root both exist), or canonicalise case through the final-path call suggested in CR-01.

---

_Reviewed: 2026-11-17_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
