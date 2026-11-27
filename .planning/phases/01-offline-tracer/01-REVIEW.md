---
phase: 01-offline-tracer
reviewed: 2026-11-18T22:00:00Z
depth: standard
files_reviewed: 4
files_reviewed_list:
  - src/Vdiff.Core/Output/HeldDirectory.cs
  - tests/Vdiff.Tests/Safety/HeldDirectoryTests.cs
  - tests/Vdiff.Tests/Safety/AliasedOutTests.cs
  - tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs
findings:
  critical: 0
  warning: 0
  info: 3
  total: 3
  necessity: 0
status: issues_found
---

# Phase 01: Code Review Report (incremental review of plan 01-18)

**Reviewed:** 2026-11-18
**Depth:** standard
**Files Reviewed:** 4
**Status:** issues_found

## Summary

Scope: plan 01-18, reviewed incrementally since `2f3e477` (`git diff 2f3e477..HEAD` for the four listed files). The change is the drive-root fix in `HeldDirectory.SplitAtDeepestExisting` plus the tests that pin it. No structural (fallow) findings and no external reviewer evidence were supplied. The necessity rubric was not applied beyond a read of the change (no guard in the diff is unreachable or duplicated elsewhere), so `necessity: 0`.

**CR-01 outcome: FIXED.** Trailing separators are now trimmed only down to `Path.GetPathRoot(directory)`; when the trimmed text is shorter than the root, the root itself becomes the anchor. `--out C:\` therefore anchors at `C:\`, not at the drive-relative `C:` (the process's current directory on drive C). This is verified two ways:

1. By running the same arithmetic the code performs (`GetPathRoot`, `TrimEnd`, length compare, `Directory.Exists`) in `pwsh` on .NET for every spelling in the brief. `C:\`, `C:\\`, `C:/` and `C:\\/` all give root `C:\` and anchor `C:\`; `\\?\C:\` and `\\?\C:\\` give root and anchor `\\?\C:\`; `\\server\share\` and `\\server\share\\` give root and anchor `\\server\share` (the trim never goes below it, and it never did); POSIX `/` gives root `/` where the old code produced the empty string.
2. By running the new tests (10 cases: three `HeldDirectoryTests` theories and the refusal test, the `AliasedOutTests` fact, and the script-parity theory on both PowerShell hosts). All pass. By inspection each discriminates: with the old code the anchor for a root spelling is `C:`, `Open("C:")` opens the current directory, `held.Path` equals the current directory, and the `SamePath(held.Path, currentDirectory)` / `recorded` assertions fail. They derive the root from the process's own current directory, so the pass is not vacuous on whichever drive the host runs from.

Edge cases from the brief, as the code behaves at HEAD:

| Input to `SplitAtDeepestExisting` | Root | Anchor | Reaches it from the CLI? |
|---|---|---|---|
| `C:\`, `C:\\`, `C:/`, `C:\\/` | `C:\` | `C:\` (fixed) | Yes, as `C:\` (`FinalPath.Step` returns the root of `GetFullPath`) |
| `\\server\share\` | `\\server\share` | `\\server\share` | No: `FinalPath.Step` refuses a non-drive-letter path first |
| `\\?\C:\` | `\\?\C:\` | `\\?\C:\` | No: same refusal |
| `C:` / `C:foo` (drive-relative) | `C:` | `C:` / `C:foo` (still the drive's current directory) | No: `Path.GetFullPath` in `CliApp.Run` resolves it before `HeldDirectory` sees it (see IN-01) |
| `/` (POSIX), `//` | `/` | `/` | Yes; was an unresolvable failure before |
| `//` on Windows | `\\` | does not exist, ends in the fixed unresolvable message | No (refused earlier) |

The script side needed no change: `Get-FullPath` already keeps a root (`$full.Length -gt $root.Length` guard) and `Open-HeldDirectory` does not trim. The new parity test confirms the CLI and `Open-HeldDirectory` hold the same root. Safety of the tests on a real machine: the drive-root tests open the root read-only with an empty remainder (nothing created), and the CLI test throws from `AfterHeld` before `OutputWriter.WriteAll`, so no file is written into a real root.

## Previous round

The previous report (plans 01-16 and 01-17, ten files) raised CR-01 and WR-01 to WR-06 and IN-01 to IN-07. Only CR-01 sits in this change; it is fixed. The other items concern lines this diff did not touch (`HeldDirectory.cs` outside `SplitAtDeepestExisting`, `FinalPath.cs`, `CliApp.cs`, `trace-run.ps1`, `EnvironmentScope.cs`, `FakeSidecar`) and were not re-assessed here; this report does not claim they are resolved. The `HeldDirectory` class summary still has two overlapping paragraphs (previous IN-04), unchanged by this diff.

## Info

### IN-01: `SplitAtDeepestExisting` still accepts a non-fully-qualified path; the drive-relative case is defended only upstream

**File:** `src/Vdiff.Core/Output/HeldDirectory.cs:188-207`
**Issue:** The new doc comment says a drive-relative `C:` names the current directory and must not be treated as the root, but the function does nothing for that form: `GetPathRoot("C:")` is `C:`, so the trim leaves `C:` alone and `Directory.Exists("C:")` is true. Called directly with `C:` it anchors on drive C's current directory (the CR-01 symptom), and with `C:foo` it anchors there and appends `foo`. This cannot happen today because `CliApp.Run` passes `FinalPath.Resolve(Path.GetFullPath(...))`, which is always `X:\...` on Windows. The class is `internal`, so only a future second caller could hit it. Not a defect now; the fix for CR-01 holds for the input shapes that can arrive.
**Fix:** Make the precondition explicit so a future caller fails closed rather than writing to the current directory:

```csharp
if (!System.IO.Path.IsPathFullyQualified(directory))
{
    throw new VdiffException(FinalPath.UnresolvableMessage);
}
```

and add a `HeldDirectoryTests` row for `C:` (Windows) asserting the fixed refusal.

### IN-02: The keep-the-root trim is written four times

**File:** `src/Vdiff.Core/Output/HeldDirectory.cs:191-196`, `tests/Vdiff.Tests/Safety/HeldDirectoryTests.cs:127-132`, `tests/Vdiff.Tests/Safety/AliasedOutTests.cs:668-673`, `tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs:1111-1116`
**Issue:** The same three-line "trim but never below the root" routine is in the product and three times in the tests (two of them under the identical name `TrimKeepingRoot`, one as `TrimTrailingSeparatorsKeepingRoot`). The test copies are only used to normalise strings for comparison, and the product call under test supplies `held.Path` independently, so this is not a vacuous-test problem, but each future change to the rule has to be made in four places.
**Fix:** Put one `TrimKeepingRoot` in `tests/Vdiff.Tests/Support/` and call it from the three test classes.

### IN-03: The root tests fail, not skip, when the host runs from a drive root

**File:** `tests/Vdiff.Tests/Safety/HeldDirectoryTests.cs:134-139`, `tests/Vdiff.Tests/Safety/AliasedOutTests.cs:685-690`
**Issue:** `CurrentDriveRoot` and the first assertion of the `AliasedOutTests` fact assert that the process's current directory is not the drive root, because otherwise the rows cannot tell a root from the current directory. A runner whose working directory is `C:\` (or `/` in a container) gets a red test whose message blames the host. The assertion is correct that the rows would not discriminate; failing rather than skipping makes an environment property look like a product defect. Related and still true: the tests exercise only the current drive's `X:\` forms; `\\?\X:\` and UNC roots are not covered at `HeldDirectory.Create` (they are refused earlier by `FinalPath.Step`, so this is acceptable).
**Fix:** Use a visible skip (`Assert.Skip("test host runs from the drive root")`) so the report says skipped, not failed.

---

_Reviewed: 2026-11-18_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
