---
phase: 01-offline-tracer
reviewed: 2026-11-18T21:00:00Z
depth: standard
files_reviewed: 10
files_reviewed_list:
  - scripts/trace-run.ps1
  - src/Vdiff.Core/Cli/CliApp.cs
  - src/Vdiff.Core/Cli/FinalPath.cs
  - src/Vdiff.Core/Output/HeldDirectory.cs
  - src/Vdiff.Core/Output/OutputWriter.cs
  - tests/Vdiff.FakeSidecar/Program.cs
  - tests/Vdiff.Tests/Safety/AliasedOutTests.cs
  - tests/Vdiff.Tests/Safety/HeldDirectoryTests.cs
  - tests/Vdiff.Tests/Support/EnvironmentScope.cs
  - tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs
findings:
  critical: 1
  warning: 6
  info: 7
  total: 14
  necessity: 0
status: issues_found
---

# Phase 01: Code Review Report (incremental review of plans 01-16 and 01-17)

**Reviewed:** 2026-11-18
**Depth:** standard
**Files Reviewed:** 10
**Status:** issues_found

## Summary

Scope: `git diff a2993dc..HEAD` for the ten listed files (plan 01-16, the held output directory in the CLI, and plan 01-17, the held log directory and `VDIFF_PROTECTED_DIRS` in the trial script). Per the product owner's decision of 2026-11-18, only the check-to-write swap window is in scope; subst, mapped-drive and 8.3 spellings (T-01-39, AR-04) are not findings. No structural (fallow) findings and no external reviewer evidence were supplied. The necessity rubric (`necessity-review.md`) was read; nothing in the new code qualified (every guard sits on a trust boundary or is a test seam), so `necessity: 0`.

The acquisition order (anchor opened and judged first, missing directories created under the anchor's final path, leaf opened and judged, anchor released) is implemented consistently in `HeldDirectory.CreateHeld` and `Open-HeldDirectory`. Handle handling is sound: the leaf and anchor handles are disposed before `RemoveCreated` runs (so the held directories can actually be removed), every `CreateFileW` failure disposes its invalid handle, and the success path nulls the handle it hands over. `HeldDirectory.Normalize` and `ConvertFrom-FinalPathForm` agree on every shape I could construct (case-insensitive UNC prefix, `Length >` guard on the bare `\\?\UNC\`, ordinal `\\?\`, ASCII-letter drive test).

I confirmed one defect by running a build of HEAD (a copy under the session scratchpad, no repository file touched): `--out C:\` writes the five files into the process's current directory and reports success with the text `vdiff: wrote 5 files to C:\` (CR-01). The remaining findings are one possible regression for long paths, a new way for a link target to reach stderr, a divergence between the CLI and the script, and test gaps.

## Previous round

Previous review covered plan 01-15 (`53215fb..HEAD`) over seven files. Items whose files are outside this scope (`FinalPathTests.cs`, `LinkScratch.cs`) are not carried. Dispositions are checked against the code at HEAD.

| Previous id | Title (short) | Status at HEAD |
|-------------|---------------|----------------|
| WR-05 | Every non-drive-letter link target refused on Windows with a message naming the wrong problem | **Carried forward as WR-05.** `FinalPath.cs` is unchanged apart from a comment; `UnsupportedRootMessage` is unchanged. |
| WR-01 | File-symbolic-link half of the output-name test passes vacuously | **Carried forward as WR-06.** `AliasedOutTests.cs:305-308` still returns when `TryFileLink` is false. |
| IN-02 | Source-text pin and silent non-Windows passes | **Carried forward as IN-05; worse.** The new tests add more early returns. |
| IN-03 | `TooManyLinksException` thrown for unrelated conditions | **Carried forward as IN-06.** `FinalPath.cs:81` and `:107` unchanged. |
| IN-04 | Case-insensitive non-Windows file systems compared case-sensitively | **Carried forward as IN-06.** Still cited as open in every bound paragraph. |
| IN-07 | Symlink and UNC spellings of the NT-namespace escape not tested | **Carried forward as IN-07.** The `AliasedOutTests` theory still has only `globalroot` and `device-no-prefix`. |
| IN-08 | The "Bound (T-01-39 / T-01-41)" paragraph exists in several places | **Carried forward as IN-04; worse.** The swap-interval paragraph is now copied into five more places. |
| WR-02, IN-01 | Redundant `Test-NonDriveRoot`; stale handoff | Resolved previously; unchanged. |

## Critical Issues

### CR-01: `--out` at a drive root writes into the current directory and reports the root

**File:** `src/Vdiff.Core/Output/HeldDirectory.cs:186-187` (and `:214-216`)
**Issue:** `SplitAtDeepestExisting` trims trailing separators before it tests the path, so `C:\` becomes `C:`. On Windows `C:` is not the root: it is the drive-relative path, the current directory on drive C. `Directory.Exists("C:")` is true, so `C:` becomes the anchor, `Open("C:")` opens the process's current directory, and `GetFinalPathNameByHandleW` reports that directory. The files go there, and `CliApp.Run` prints `wrote 5 files to {outPath}` with the typed path.

Reproduced on this host (build of HEAD, process current directory a scratch folder, `--out C:\`):

```
vdiff: wrote 5 files to C:\
exit=0
wd:
base.model.json  diff.json  head.model.json  report.html  summary.mmd     <- the scratch folder, not C:\
```

Before plan 01-16 the same command wrote to the root (or failed with access denied). The held directory's final path is still judged, so the work tree and the vdiff root are not reachable this way; the defect is that output lands in a directory the operator did not name and the success line says otherwise. The same trim on a UNC share root (`\\server\share\`) is harmless, and on Linux `/` trims to the empty string and fails with the fixed unresolvable message. `Open-HeldDirectory` in the script does not trim, so the two implementations differ here.
**Fix:** Do not trim a root. Trim only when the path is longer than its root:

```csharp
var root = System.IO.Path.GetPathRoot(directory) ?? string.Empty;
var current = directory.Length > root.Length
    ? directory.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar)
    : directory;
```

Add a `HeldDirectoryTests` case for a root input (assert the returned `Path` equals the root, or that creation is refused with a fixed message when the root is not writable), and one for `--out` given as the drive root through `CliApp.Run` with a scratch current directory.

## Warnings

### WR-01: The held handle is opened without the extended-length prefix, so a long output path can now fail

**File:** `src/Vdiff.Core/Output/HeldDirectory.cs:216` (`CreateFileW(directory, ...)`), `scripts/trace-run.ps1:324`
**Issue:** `Open` passes the plain drive-letter path straight to `CreateFileW`. .NET file APIs add the `\\?\` prefix themselves for paths over 259 characters; a direct P/Invoke does not, and the prefix that `GetFinalPathNameByHandleW` returned is deliberately stripped by `Normalize` before the path is reused. A long `--out` (a deep checkout directory under an agent work folder is plausible) therefore fails with the fixed text "a path could not be resolved to its final target", where before 01-16 it was written. Whether it fails for the CLI depends on the host: it works when `LongPathsEnabled` is set and `dotnet.exe` is long-path aware (both true on this host, so I could not reproduce it for the CLI); it fails on a host with `LongPathsEnabled=0`, which is the Windows default. I did reproduce the script side: on this host a 377-character existing directory opens with `CreateFileW` from Windows PowerShell 5.1 but fails with error 3 from PowerShell 7 (`pwsh`), so under `pwsh` the log hold is skipped silently for a long `-LogPath` directory and no log is written.
**Fix:** Prefix the path for the handle call only: `\\?\` + path for a drive-letter path, `\\?\UNC\` + path[2..] for a UNC path (the full path is already absolute and normalised by `Path.GetFullPath`). Keep `Normalize` stripping the prefix from the result. Mirror in `Open-DirectoryHandle`. Add a test that holds a directory deeper than 260 characters.

### WR-02: A link target now reaches stderr through I/O exception text

**File:** `src/Vdiff.Core/Cli/CliApp.cs:117-120`, `src/Vdiff.Core/Output/OutputWriter.cs:72-99`, `src/Vdiff.Core/Output/HeldDirectory.cs:112`
**Issue:** `HeldDirectory.Path` is the final physical path read from the handle, and every create, write and move now goes through it. An `IOException` or `UnauthorizedAccessException` from `Directory.CreateDirectory`, the `FileStream` or `File.Move` carries that physical path in its message, and `CliApp.Run` prints it verbatim (`"could not write the output: " + e.Message`). Before 01-16 the message held the path the operator typed. Now an `--out` reached through a link or junction reports the link's target on stderr (observed: `Access to the path 'C:\base.model.json.tmp-<guid>' is denied` with the physical path). The rest of this design goes to some length to keep paths out of messages (T-01-38, `UnresolvableMessage`, `ProtectedDirectoriesMalformed` naming no value); this is the one remaining path-bearing message, and the held directory made it show a path the operator never typed.
**Fix:** Replace the `IOException`/`UnauthorizedAccessException` branch with a fixed text, for example `"could not write the output"`, optionally with the exception type name as the last catch does. If the detail is wanted for diagnosis, write it only where the operator asked for it. Add a test that forces a write failure through a link (the existing `A_failed_write_leaves_no_temporary_file_behind` only checks `StartsWith("vdiff: ")`) and asserts the physical path is absent from stderr.

### WR-03: The script's judgement by handle, its protected verdict and its final-path rule are not tested

**File:** `scripts/trace-run.ps1:349-375`, `:424-433`, `:571-577`; `tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs:405-455`
**Issue:** The new log-hold tests prove that a held directory refuses a rename and that a retargeted link entry receives no log. Nothing proves the part that makes the hold safe:
- `Get-JudgedFinalPath` returning `held-directory-protected` (line 373) and the `Invoke-Trace` branch that turns it into `bad-arguments` (line 574) are never reached. The existing aliased-log tests (`Aliased_log_path_through_a_directory_link_into_the_repository...`, line 718) end at the first check, `Test-SameOrUnder $logFull`, before `Open-HeldDirectory` is called. Deleting the handle judgement would leave every test green.
- The non-Windows leaf and anchor judgements (lines 424 and 432) have no test on any host.
- `ConvertFrom-FinalPathForm` has no direct test. `HeldDirectoryTests` pins `HeldDirectory.Normalize` with eight cases, but the script copy ("same rule, two implementations", lines 344-348) is only exercised by a plain drive-letter final path, so the UNC, volume, device and empty branches could drift from the C# ones without a failing test.
- The bad-arguments outcome of a hold failure is therefore untested end to end, as the review brief noted.
**Fix:** Test the functions without running the whole script: have the test extract `ConvertFrom-FinalPathForm`, `Test-UnderAny` and `Get-JudgedFinalPath` (or the whole helper region) with the PowerShell parser (`[System.Management.Automation.Language.Parser]::ParseFile`) into a child `pwsh`/`powershell` and call them with the eight `Normalize` inputs from `HeldDirectoryTests`, asserting the same path, the same judged flag and a throw for the same shapes. For the verdict itself, the verdict needs a way to hit `Open-HeldDirectory` with a protected directory that the first check did not already catch. The staged fake-sidecar delay comes after the hold is opened, so it cannot do that; the simplest deterministic route is to call the extracted `Open-HeldDirectory` in the child host with a protected list that names the log directory itself, and assert the `held-directory-protected` throw and that nothing was created.

### WR-04: A path separator inside the vdiff root breaks the `VDIFF_PROTECTED_DIRS` handoff

**File:** `scripts/trace-run.ps1:591-594`, `src/Vdiff.Core/Cli/CliApp.cs:231-236`
**Issue:** The script passes `$script:VdiffRoot` as the whole value of `VDIFF_PROTECTED_DIRS`. The CLI splits that value on `Path.PathSeparator` (`;` on Windows, `:` elsewhere). Both characters are legal in directory names, so a vdiff checkout at, say, `C:\work;old\vdiff` or `/srv/a:b/vdiff` yields two entries, the second of which is not fully qualified, and every run then fails with `ProtectedDirectoriesMalformed`. The script reports `tool-failed` with no hint, and `outputs_present: no`. The guard fails closed, so this is not a bypass, but the failure is permanent for that checkout and attributed to the wrong cause.
**Fix:** In the script, test the root for the platform's path separator before the run and end with `script-error` (or `bad-arguments`) so the reason is accurate, or reject the character in `Test-NonDriveRoot`'s neighbour check. Document the limit in the variable's description in `CliApp`.

### WR-05: (carried forward from WR-05) A relative or drive-less link target is refused with a message that names the wrong problem

**File:** `src/Vdiff.Core/Cli/FinalPath.cs:95-101` and `:16-22`; `scripts/trace-run.ps1:224-228`
**Issue:** Unchanged since the previous round. On Windows every link whose reported target is not `X:\...` is refused, including harmless relative symbolic links and volume mount points, with `UnsupportedRootMessage` ("a path must begin with a drive letter on Windows; ..."). A user whose path does begin with a drive letter is told it must. The fail-closed choice is defensible (the runtime cannot separate an NT-spelled absolute target from a relative one); the missing explanation and the absence of a test for a refused-but-harmless form are the defect. The held directory adds a second route to the same message: `Normalize` throws `UnsupportedRootMessage` for a final path in the volume form (a folder-mounted volume without a drive letter), which the typed drive-letter path cannot explain either.
**Fix:** Give a link-target refusal and a final-path-form refusal their own fixed texts (still no path), keep `UnsupportedRootMessage` for a path argument, and cover one refused-but-harmless form in a test guarded by `TryDirectoryLink`.

### WR-06: (carried forward from WR-01) The file-symbolic-link test passes vacuously without the privilege

**File:** `tests/Vdiff.Tests/Safety/AliasedOutTests.cs:296-319`
**Issue:** `A_file_symbolic_link_planted_at_an_output_name_is_replaced_and_its_target_is_unchanged` returns early (line 305-308) when `TryFileLink` fails, which is the normal case on an unelevated Windows account. The test then reports as passed without having run. The hard-link half runs everywhere, so the suite shows the output-name replacement as covered for both link kinds when only one is.
**Fix:** Make the skip visible (xunit `Assert.Skip` or a `SkippableFact`-style conditional) so the report says "skipped", not "passed".

## Info

### IN-01: The non-Windows creation path differs from the Windows one in two small ways

**File:** `src/Vdiff.Core/Output/HeldDirectory.cs:145-150` against `:107-115`; `scripts/trace-run.ps1:426-431` against `:409-415`
**Issue:** `CreateHeld` records a directory in `created` only after `Directory.Exists` said it was missing; `CreateNarrowed` (and the script's non-Windows branch) calls `CreateDirectory` and records the path unconditionally. If another process creates the same directory between `SplitAtDeepestExisting` and the call, `CreateDirectory` returns quietly and a later refusal then removes a directory this call did not create (empty directories only, since `Directory.Delete` is not recursive). Separately, `CreateNarrowed` returns `resolvedDirectory` (the path resolved before creation, re-resolved only to be judged), while the script returns the re-resolved leaf. The two name the same place unless a link was swapped in, so there is no observable difference today, but the C# and script copies are documented as the same rule.
**Fix:** Guard with `Directory.Exists` in the narrowed branch, and return the re-resolved leaf in both.

### IN-02: The limit of 8 is written three times and its boundary is untested

**File:** `src/Vdiff.Core/Cli/CliApp.cs:49-50`, `:55`; `tests/Vdiff.Tests/Safety/AliasedOutTests.cs:572-603`
**Issue:** `MaxProtectedDirectories = 8` and the text `"... at most 8 absolute directories ..."` are independent literals, so changing the constant leaves a wrong message. The test theory covers nine entries (refused) but not eight (accepted), so an off-by-one in `> MaxProtectedDirectories` is not pinned from the accepting side.
**Fix:** Build the message from the constant with `$"... at most {MaxProtectedDirectories} ..."`, made `static readonly` rather than `const`, and add an eight-entry case that is accepted.

### IN-03: The staged tests race a fixed five-second window, and `EnvironmentScope` leaks on a partial construction

**File:** `tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs:92-100`, `:405-455`, `:560-620`; `tests/Vdiff.Tests/Support/EnvironmentScope.cs:14-21`
**Issue:** The fake sidecar sleeps 5000 ms and the test must finish its rename, delete and link swap inside that window. On a slow or scanned machine an overrun makes the swap land after the script has finished; the test then fails (the rename is no longer refused, or the run no longer fails with `tool-failed`) rather than passing vacuously, so it is a flake, not a hole. Raising `delayMs` for the CI leg, or waiting on a second marker file, removes it. `EnvironmentScope`'s constructor assigns variables one at a time; if `SetEnvironmentVariable` throws on a later key, the object is never constructed, `using` never disposes it, and the earlier assignments stay in the process environment for the remaining tests of the collection. Also, `CliApp.Run` reads the real `VDIFF_PROTECTED_DIRS` whenever a test passes no hook, so a developer who has the variable set sees unrelated tests fail with the malformed-variable text.
**Fix:** Restore in a `try`/`catch` inside the constructor, and have the tests that call `CliApp.Run` without hooks pass a hook that returns null for the variable.

### IN-04: (carried forward from IN-08) The swap-interval paragraph is now copied into six comment blocks and the class summary of `HeldDirectory` contains two overlapping paragraphs

**File:** `src/Vdiff.Core/Output/HeldDirectory.cs:7-20`, `src/Vdiff.Core/Output/OutputWriter.cs:44-51`, `src/Vdiff.Core/Cli/CliApp.cs:156-163`, `src/Vdiff.Core/Cli/FinalPath.cs:54-56`, `scripts/trace-run.ps1:35`, `:554-563`
**Issue:** The "Check-to-write interval (T-01-43, T-01-46)" paragraph is repeated verbatim in `HeldDirectory`, `OutputWriter.WriteAll` and `CliApp.RefuseUnsafeOut`, and a long variant sits in the script; the Bound (T-01-39) paragraph is still repeated in four. `HeldDirectory`'s class summary starts with one paragraph (lines 7-11) and then restates it (lines 12-19), so the second half reads as a stale draft. Each rewrite has to touch every copy and nothing checks they agree.
**Fix:** Keep the statement once, on `HeldDirectory`, and have the others cite it in one sentence with the threat ids. Delete the first summary paragraph of the class.

### IN-05: (carried forward from IN-02) More tests report as passed on hosts where they do nothing

**File:** `tests/Vdiff.Tests/Safety/HeldDirectoryTests.cs:68-71`, `tests/Vdiff.Tests/Safety/AliasedOutTests.cs:167`, `:185`, `:203`, `:224`, `:399`, `:516`, `tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs:405-410`
**Issue:** The new Windows-only tests (`Dispose_releases_the_directory...`, the held-directory rename and delete test, the staged log-hold test, the namespace tests) end with `if (!OperatingSystem.IsWindows()) return;`. The Linux leg reports them as passed. The log-hold test in particular has a non-Windows half that could run (a retargeted link entry still must not redirect the log, because the log goes to the resolved path), and it is skipped with the rest.
**Fix:** Use a visible skip, and run the retarget assertion on the other hosts.

### IN-06: (carried forward from IN-03 and IN-04) `TooManyLinksException` is thrown for unrelated conditions; case-insensitive non-Windows file systems are compared case-sensitively

**File:** `src/Vdiff.Core/Cli/FinalPath.cs:81`, `:107`; `src/Vdiff.Core/Cli/CliApp.cs:248`; `scripts/trace-run.ps1:58`
**Issue:** Unchanged. The link-budget exception also stands for a null root and a null link target, which misleads a maintainer reading `Step`. The case-sensitivity choice on macOS (a case-insensitive volume by default) is still only cited as open in comments. Note that the held directory raises the stakes slightly on macOS and Linux: with no handle held, the protected-directory comparison there is the only judgement, and it is case-sensitive.
**Fix:** Throw a dedicated exception (or `VdiffException(UnresolvableMessage)`) for the null cases; decide IN-04 and record it in the threat register instead of in four comment blocks.

### IN-07: (carried forward from IN-07) The new tests still omit the symbolic-link and UNC spellings of the namespace escape

**File:** `tests/Vdiff.Tests/Safety/AliasedOutTests.cs:219-241`
**Issue:** Unchanged: the NT-junction theory has `globalroot` and `device-no-prefix` and no `unc`, and every link there is a junction made by `RawJunction`. The symbolic-link branch of `FinalPath.Step` is not exercised with an NT-spelled target.
**Fix:** Add a privilege-gated symlink variant with a visible skip and an `unc` kind (skip the liveness assertion for it).

---

_Reviewed: 2026-11-18_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
