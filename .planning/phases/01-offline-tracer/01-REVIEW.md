---
phase: 01-offline-tracer
reviewed: 2026-11-18T00:00:00Z
depth: standard
files_reviewed: 7
files_reviewed_list:
  - scripts/trace-run.ps1
  - src/Vdiff.Core/Cli/CliApp.cs
  - src/Vdiff.Core/Cli/FinalPath.cs
  - tests/Vdiff.Tests/Safety/AliasedOutTests.cs
  - tests/Vdiff.Tests/Safety/FinalPathTests.cs
  - tests/Vdiff.Tests/Support/LinkScratch.cs
  - tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs
findings:
  critical: 0
  warning: 1
  info: 2
  total: 3
  necessity: 0
status: issues_found
---

# Phase 01: Code Review Report (incremental re-review of plan 01-15)

**Reviewed:** 2026-11-18
**Depth:** standard
**Files Reviewed:** 7
**Status:** issues_found

## Summary

Scope: the diff `53215fb..HEAD` for the seven listed files, which is plan 01-15's change set (commits 8d6e84a..204030c). No structural (fallow) findings and no external reviewer evidence were supplied. The necessity rubric was not applied to new code beyond the previous WR-02 check (nothing new qualified).

The change replaces the one-entry `Volume{` denylist with an allow-list on the target text the runtime reports for each link: on Windows a link is followed only when `StartsWithDriveLetter` (C#) / `-not Test-NonDriveRoot` (script) holds for that text. I traced the new `FinalPath.Step` and `Resolve-FinalPathStep` paths and found no remaining way for a GLOBALROOT, bare-device, UNC or volume-GUID reparse target to reach the combine/follow step: the .NET and PowerShell runtimes strip a leading `\??\` (and, per the plan's measurement, .NET/PowerShell 7 strip four characters from a bare device name, giving `ice\...`), and every such residue is non-drive text, so it is refused. The `\??\` strip in the script followed by the text test is consistent with the C# side. The new `RawJunction` reparse buffer layout (tag, length, reserved, substitute offset/length, print offset/length, path buffer) is correct, the junction is registered for cleanup before the control call, and links are removed as links before the scratch root is deleted.

Verification run on this Windows host (fresh Release build of HEAD): `dotnet test --filter "FullyQualifiedName~FinalPathTests|FullyQualifiedName~AliasedOutTests|FullyQualifiedName~nt_namespace"` gave 64 passed, 0 failed, 0 skipped. That includes the GLOBALROOT, bare-device and UNC junction cases at the unit level, the GLOBALROOT and bare-device cases through the CLI and through `trace-run.ps1` (`-Out` and `-LogPath`, on every host the test class enumerates), and the drive-letter control junction. The new tests pin their own premise (the reported target is asserted not to be a drive-letter path, and the junction is asserted live), so a runtime that reports a different spelling fails with an explanation instead of passing vacuously.

No critical issue found. The remaining findings are about a behaviour change that is wider than the escape it closes, and test and documentation upkeep.

## Previous round

Previous review commit: `53215fb`. Dispositions below are checked against HEAD, not taken from the plan summary.

| Previous id | Title (short) | Resolved by this diff? |
|-------------|---------------|------------------------|
| CR-01 | Non-`Volume{` NT-namespace junction bypasses both guards | **Resolved.** Allow-list on the reported target in `FinalPath.Step:98` and `trace-run.ps1:226`; `Volume{` special case and `IsVolumeGuidTarget` removed. Reproduction shapes (GLOBALROOT, bare device) and UNC now have tests that fail closed with the fixed message (`UnsupportedRootMessage` / `script-error`) and leave the repository untouched. Residual, by design and documented: subst and mapped drive letters, 8.3 names, IN-04. |
| WR-01 | File-symbolic-link half of the output-name test passes vacuously | **Not touched; still open.** `AliasedOutTests` still returns early when `TryFileLink` is false; this diff only adds tests. |
| WR-02 | Redundant second `Test-NonDriveRoot $logFull` | **Resolved.** The call is gone from `Invoke-Trace` (script line 399-408 repeats only `Test-SameOrUnder`), the matching assertion is removed from `TraceRunScriptTests` (the first-check assertion at line 326 is kept), and the comment states why. |
| IN-01 | Stale handoff committed to a public repository | **Resolved.** `docs/handoffs/phase-01-gap-closure-interrupted-by-shutdown.md` was deleted in `fac4f36`; `docs/` no longer contains `handoffs`. |
| IN-02 | Source-text pin and silent non-Windows passes | **Not resolved; slightly worse.** The order test still searches script text (now with one fewer substring). Five more Windows-only tests added by this diff (`FinalPathTests` x2 plus the new theory, `AliasedOutTests` x1 theory, `TraceRunScriptTests` x2 theories) use `if (!OperatingSystem.IsWindows()) return;`, so the Ubuntu leg reports them as passed without running them. |
| IN-03 | `TooManyLinksException` thrown for unrelated conditions | **Not resolved.** `FinalPath.cs:79` and `:105` still throw it for a null root and a null link target. Note the id was used in two senses: the doc comments this diff rewrites used "IN-03" for an older finding, "link targets written without a drive letter". That sense is now closed (such targets are refused) and the citation is removed from the three comment blocks, but the maintainer-naming finding above is unchanged. |
| IN-04 | Case-insensitive non-Windows file systems compared case-sensitively | **Not touched; still open.** Still cited as open in the rewritten comments (`FinalPath.cs:54`, `CliApp.cs:101`, `trace-run.ps1:32,195`). |
| WR-03, WR-04, IN-05, IN-06 | Recorded in the disposition file only | Outside this diff's files; not touched, still open. |

## Warnings

### WR-05: Every non-drive-letter link target is now refused on Windows, with a message that names the wrong problem

**File:** `src/Vdiff.Core/Cli/FinalPath.cs:95-101` (and `:16-22`), `scripts/trace-run.ps1:224-227`
**Issue:** The new rule refuses any Windows link whose reported target is not `X:\...`. That correctly covers the NT-namespace class, but it also refuses ordinary links that carry no escape: a relative symbolic link (`mklink /D link ..\other`), a link to a drive-relative path, and a volume mount point (reported as `Volume{...}\`). Before this diff the relative and drive-less forms were resolved against the link's parent and followed. `FinalPath.Resolve` is called for `--out` and for the work tree, git directory and common git directory, so a repository or output path that merely passes through a relative symlink or a mounted-folder volume now ends the run with exit 1. The text printed is the fixed `UnsupportedRootMessage`: "a path must begin with a drive letter on Windows; extended-length, device, UNC and volume paths are not accepted". For a relative-symlink user that is wrong: their path does begin with a drive letter, and no listed namespace is involved. The doc comment on the constant admits the message covers the relative case, but the text itself does not, and the user never sees the comment. I could not create a relative symbolic link without elevation on this host, so the relative case is established from the code and from the `A_reported_link_target_...` theory (`..\work` and `work` map to false), not from a run.

The conservative refusal is defensible: the runtime does not expose the relative flag, so an absolute symlink to `\??\GLOBALROOT\...` and a relative symlink to the directory `GLOBALROOT\...` report identical text and cannot be told apart through these APIs. The defect is the missing explanation and the absent test of the refused-but-harmless forms, not the fail-closed choice.
**Fix:** Give the link-target refusal its own fixed message (still no path text, T-01-38), for example `a link target must be a drive-letter path on Windows; this link cannot be followed safely`, and keep `UnsupportedRootMessage` for a path argument. Mirror the distinction in the script by keeping `script-error` but documenting the cause in the header. Add a `Step`-level test for a relative symbolic link guarded by `TryFileLink`/`TryDirectoryLink` with a visible skip when the privilege is missing, so the behaviour the message describes is exercised somewhere. If relative targets matter to users, the alternative is to follow a link only when `info.LinkTarget` is relative and `info.Attributes` shows a symbolic link rather than a mount point, accepting that the API cannot separate it from an absolute NT-spelled symlink; that trade belongs in the threat register (T-01-41), not in a silent choice.

## Info

### IN-07: The new tests do not exercise the symbolic-link spelling of the escape, and the CLI and script tests omit the UNC kind

**File:** `tests/Vdiff.Tests/Safety/FinalPathTests.cs:167-197`, `tests/Vdiff.Tests/Safety/AliasedOutTests.cs:219-241`, `tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs:573-655`
**Issue:** The previous CR-01 named two shapes: a junction with a GLOBALROOT target, and a symbolic link with a `\??\UNC\host\share\...` target. All new link tests use `LinkScratch.RawJunction`, because it needs no privilege. `FinalPathTests` covers a UNC substitute name, but only as a junction (a non-live one), and `NtJunctionKinds` in the script tests and the two kinds in `AliasedOutTests` leave `unc` out. The symlink path through `Step`/`Resolve-FinalPathStep` (`LinkType -eq 'SymbolicLink'`) is therefore never run by the new tests with an NT-spelled target, and the pure `StartsWithDriveLetter` theory tests the predicate, not the integration. The rule is a text test applied identically to both link types, so the risk is low, but the claim "closed for both link types" rests on reading, not on a test.
**Fix:** Add a privilege-gated symlink variant with a visible skip, and add `"unc"` to `NtJunctionKinds` and the `AliasedOutTests` theory (for `unc` skip the liveness assertion, as `FinalPathTests` already does).

### IN-08: The same ten-line "Bound (T-01-39 / T-01-41)" paragraph now exists in four places

**File:** `src/Vdiff.Core/Cli/FinalPath.cs:45-54`, `src/Vdiff.Core/Cli/CliApp.cs:91-102`, `scripts/trace-run.ps1:22-34` and `:186-195`
**Issue:** This diff rewrites and lengthens a paragraph that is copied verbatim into three files (four blocks). The previous round's paragraph was already copied; each rewrite has to touch every copy, and the plan's own acceptance greps exist to catch drift. A mismatch between copies would mislead a reader about what the guard refuses, which is the property the whole safety design depends on.
**Fix:** Keep the full statement once (on `FinalPath.Resolve`) and have `CliApp.RefuseUnsafeOut` and the script reference it in one sentence plus the threat ids, as the script header already does for the reason vocabulary.

---

_Reviewed: 2026-11-18_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
