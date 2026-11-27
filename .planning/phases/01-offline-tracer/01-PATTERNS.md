# Phase 01 (gap closure 01-13+): --out containment hardening - Pattern Map

**Mapped:** 2026-11-17
**Scope:** CR-01 (extended-length / loopback UNC), WR-01 (planted hard link / file symlink at an output name), WR-02 (re-check just before write, T-01-40)
**Files analysed:** 9 (8 modified, 0 new required; optional new test file for OutputWriter)
**Analogs found:** 9 / 9 (all are the files themselves or a sibling in the same directory)

All paths below were confirmed git-tracked with `git ls-files`. No mirror paths.

## File Classification

| File | Role | Data Flow | Closest Analog | Match |
|------|------|-----------|----------------|-------|
| `src/Vdiff.Core/Cli/CliApp.cs` (`RefuseUnsafeOut`, `Run`) | controller / guard | request-response | itself (lines 42-43, 84-103) | exact |
| `src/Vdiff.Core/Cli/FinalPath.cs` | utility | transform (path walk) | itself (segment walk, lines 47-80) | exact |
| `src/Vdiff.Core/Output/OutputWriter.cs` (`WriteAll`, `WriteText`) | service | file-I/O | itself (lines 43-57) | exact |
| `scripts/trace-run.ps1` (`Resolve-FinalPath*`, `Test-SameOrUnder`, `Invoke-Trace`) | script | request-response + file-I/O | itself (lines 135-208, 289-310) | exact |
| `tests/Vdiff.Tests/Safety/AliasedOutTests.cs` | test | request-response | itself | exact |
| `tests/Vdiff.Tests/Safety/FinalPathTests.cs` | test | transform | itself | exact |
| `tests/Vdiff.Tests/Support/LinkScratch.cs` | test support | file-I/O | itself (`Link`, `Dispose`) | exact |
| `tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs` | test | process spawn | itself, section "(c2) links" lines 303-395 | exact |
| `tests/Vdiff.Tests/Safety/OutputWriterTests.cs` (optional, none exists) | test | file-I/O | `FinalPathTests.cs` (no-host unit style) | role-match |

No existing test calls `OutputWriter.WriteAll` directly (grep of `tests/` found only `Serialize` callers). WR-01 and WR-02 tests go through `CliApp.Run` in `AliasedOutTests`, or a new direct test.

## Current signatures and facts the planner needs

- `CliApp.Run(IReadOnlyList<string> args, TextWriter stdout, TextWriter stderr)` returns int. Order in `Run` (CliApp.cs 42-51): `outPath = Path.GetFullPath(options.Out)`; `RefuseUnsafeOut(outPath, location)`; `TracerPipeline.Run(...)`; render; `OutputWriter.WriteAll(outPath, ...)`. WR-02 gap: the guard runs once, before the pipeline (the slow part).
- `static void RefuseUnsafeOut(string outPath, RepoLocation location)` (private). `RepoLocation` has `Root`, `GitDir`, `CommonDir`.
- `public static bool IsSameOrUnder(string path, string directory)` calls `Path.GetFullPath` on both sides and compares on directory boundary, OrdinalIgnoreCase on Windows. Note `\\?\C:\x` and `C:\x` are different strings here, which is the CR-01 mechanism.
- `FinalPath.Resolve(string path)` returns string; `FinalPath.UnresolvableMessage = "a path could not be resolved to its final target"`; max 40 links; fails closed with `VdiffException(UnresolvableMessage)`.
- `OutputWriter.WriteAll(string outDir, ModelFile baseModel, ModelFile headModel, DiffFile diff, string mermaid, string html)`; private `WriteText(string outDir, string fileName, string text)` does `File.WriteAllBytes(Path.Combine(outDir, fileName), Utf8NoBom.GetBytes(text.ReplaceLineEndings("\n")))`. This follows a pre-planted link or hard link (WR-01). Constants: `BaseModelFile, HeadModelFile, DiffJsonFile, MermaidFile, HtmlFile`.
- Exact refusal text (single string, used in 2 throw sites, asserted by 3 test files): `--out must not lie inside the analysed repository`. Other messages: `--out exists and is a file`. Output is `vdiff: <message>\n`, exit 1. Keep the text identical so `TracerEndToEndTests.cs:241`, `HostileInputTests.cs:194`, and `AliasedOutTests.Refusal` keep passing.
- Catch order in `Run` (lines 56-67): `VdiffException` -> message; `IOException or UnauthorizedAccessException` -> `"could not write the output: " + e.Message` (this prints the .NET message, which can hold a path; a new write-time refusal should throw `VdiffException` with the fixed message, not IOException); other -> `unexpected failure (TypeName)`.

## Pattern Assignments

### `src/Vdiff.Core/Cli/CliApp.cs` (guard, request-response)

**Analog:** itself. Current guard shape to extend (lines 84-103):

```csharp
static void RefuseUnsafeOut(string outPath, RepoLocation location)
{
    if (IsSameOrUnder(outPath, location.Root) || IsSameOrUnder(outPath, location.GitDir) || IsSameOrUnder(outPath, location.CommonDir))
    {
        throw new VdiffException("--out must not lie inside the analysed repository");
    }

    var resolvedOut = FinalPath.Resolve(outPath);
    if (IsSameOrUnder(resolvedOut, FinalPath.Resolve(location.Root))
        || IsSameOrUnder(resolvedOut, FinalPath.Resolve(location.GitDir))
        || IsSameOrUnder(resolvedOut, FinalPath.Resolve(location.CommonDir)))
    {
        throw new VdiffException("--out must not lie inside the analysed repository");
    }

    if (File.Exists(outPath))
    {
        throw new VdiffException("--out exists and is a file");
    }
}
```

Plan notes:
- CR-01 fix route A (preferred, matches the verifier): normalise inside `FinalPath.Step` (or a pre-step helper) by stripping `\\?\` and `\\?\UNC\` prefixes and mapping `\\localhost\X$\...` / `\\127.0.0.1\X$\...` / `\\<machine>\X$` to `X:\...`; OR route B: on Windows refuse any `Path.GetFullPath(--out)` whose root starts with two backslashes with a fixed message. Route B is the smaller change and fails closed; decide in the plan. Either way keep the existing T-01-39 bound comment and update it.
- WR-02: extract the guard so it can be called twice, e.g. `RefuseUnsafeOut` before the pipeline (fast fail, no work) and again immediately before `OutputWriter.WriteAll` at line 51. The doc comment already cites T-01-36/T-01-39; add T-01-40 there.
- Both throw sites share one message; keep it as one `const`-free literal or introduce a private const used by both.

### `src/Vdiff.Core/Cli/FinalPath.cs` (utility, transform)

**Analog:** itself. Walk core (lines 47-80): `Path.GetFullPath(path)`, `Path.GetPathRoot`, split on both separators, per segment `new DirectoryInfo(candidate).LinkTarget`, `ResolveLinkTarget(returnFinalTarget: true)`, recursion sharing `ref int links`.

Catch/translation block to reuse for any new failure (lines 40-44):

```csharp
catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException
    or NotSupportedException or TooManyLinksException)
{
    throw new VdiffException(UnresolvableMessage);
}
```

Plan notes:
- CR-01 mechanics: on .NET, `Path.GetFullPath(@"\\?\C:\a")` keeps the `\\?\` prefix and `GetPathRoot` returns `\\?\C:`; for `\\localhost\C$\a` the root is `\\localhost\C$`. So `Resolve` output differs textually from `C:\...`. Normalising here (single place) fixes both `outPath` and the repo-side values because `RefuseUnsafeOut` already resolves all four through `Resolve`. Add an `internal`/`public static string Canonicalize(string)` helper if the plan wants it unit-testable.
- Failing closed with the fixed `UnresolvableMessage` is the established convention for anything unreadable.
- A final-path Win32 call (`GetFinalPathNameByHandle` via P/Invoke) is available in C#, but the PowerShell 5.1 script cannot call it without `Add-Type`; 01-12-PLAN line 76 recorded this as the reason it was not used. Reopening that decision also resolves T-01-39 (subst and 8.3). If the plan stays with prefix stripping, T-01-39 stays an accepted limit.

### `src/Vdiff.Core/Output/OutputWriter.cs` (service, file-I/O)

**Analog:** itself (lines 43-57), shown above. No other writer in `src/` follows a different pattern (only call site is `CliApp.cs:51`).

Plan notes (WR-01): write each file to a temp name inside `outDir` (for example `Path.Combine(outDir, fileName + ".tmp-" + Guid.NewGuid().ToString("N"))`) with `FileMode.CreateNew`, then `File.Move(temp, final, overwrite: true)`. A move replaces the directory entry, so a pre-planted hard link or file symlink at the final name is replaced rather than written through; the repository file the link pointed to is not modified. Clean up the temp file in a `finally`/catch. Keep: `Utf8NoBom`, `text.ReplaceLineEndings("\n")`, constant file names, and `Directory.CreateDirectory(outDir)` first. Also decide whether to refuse a final-name entry that is a reparse point (`File.GetAttributes(...).HasFlag(FileAttributes.ReparsePoint)` or `FileInfo.LinkTarget != null`); a move already neutralises it, a refusal is optional. A hard link has no marker, so only the move protects it.

WR-02 hook: give `WriteAll` an optional re-check delegate (`Action? beforeWrite`) or do the re-check in `CliApp` between render and `WriteAll`. Prefer the `CliApp` call (keeps `OutputWriter` free of `RepoLocation`).

### `scripts/trace-run.ps1` (script, request-response; PS 5.1 + 7, ASCII only)

**Analog:** itself. Resolver (lines 166-201) mechanism: `Get-Item -LiteralPath $candidate -Force -ErrorAction Stop`, `catch [System.Management.Automation.ItemNotFoundException]`, `LinkType -eq 'SymbolicLink' -or 'Junction'`, strips a leading `\??\` from `Target`, `throw 'too many links'` (the outer catch prints reason `script-error`).

Containment call site (lines 289-310): builds `$protected` (`$script:VdiffRoot`, `--show-toplevel`, `--absolute-git-dir`, `--git-common-dir`, `$repoFull`), then:

```powershell
$outFull = Get-FullPath $Out
foreach ($dir in $protected) {
    if (Test-SameOrUnder $outFull $dir) { $F.reason = 'out-not-outside'; return }
}
```

`Test-SameOrUnder` (lines 204-208) = lexical check then `Resolve-FinalPath` of both. `Get-FullPath` (lines 135-143) uses `GetUnresolvedProviderPathFromPSPath` then `[IO.Path]::GetFullPath`.

Plan notes:
- CR-01: apply the same normalisation or two-backslash refusal inside `Get-FullPath` or at the top of `Resolve-FinalPathStep`, mirroring the C# rule exactly, and return reason `out-not-outside` (matches the existing tests). `GetUnresolvedProviderPathFromPSPath` on `\\?\C:\x` may error on some hosts; verify on both hosts and fail closed to `script-error`.
- WR-01/WR-02 in the script: the script does not write the five files itself (it spawns `vdiff.dll` with `--out $outFull`, line 322), so the C# fix covers the write. A script-side re-check just before spawning is cheap (call the `$protected` loop again after `Get-RepoSnapshot`, before `Invoke-Native`); the snapshot comparison already detects repo changes after the run (reason `repo-changed`). Keep helpers PS 5.1 safe: no `ResolveLinkTarget`, no `?.`, no `??`, no ternary, ASCII only (the header comment states this; check with a grep for non-ASCII bytes).
- Fixed vocabulary of reasons (line 9): `none|bad-arguments|out-not-outside|tool-failed|repo-changed|outputs-missing|over-budget|script-error`. Do not add a new reason; `TraceRunScriptTests.Reasons` and `LineGrammar` assert exactly this list.

### `tests/Vdiff.Tests/Safety/AliasedOutTests.cs` (test, request-response)

**Analog:** itself. Skeleton to copy for CR-01, WR-01, WR-02 cases:

```csharp
[Collection("sidecar")]
public class AliasedOutTests
{
    const string Refusal = "--out must not lie inside the analysed repository";
    static readonly string[] FiveFiles = ["base.model.json", "head.model.json", "diff.json", "summary.mmd", "report.html"];
    readonly SidecarFixture _sidecar;
    public AliasedOutTests(SidecarFixture sidecar) => _sidecar = sidecar;

    static (string BaseId, string HeadId) BuildSmallHistory(SyntheticRepo repo) { /* writes include/a.h twice, repo.Commit("base"/"head") */ }
    static int RunCli(IReadOnlyList<string> args, out string stdout, out string stderr)   // CliApp.Run with StringWriters
    static List<string> Args(string repoPath, string baseId, string headId, string outPath) =>
        ["--repo", repoPath, "--base", baseId, "--head", headId, "--out", outPath];
    static void AssertRefusedAndUntouched(SyntheticRepo repo, RepoSnapshot before, int exit, string stderr, string created)
    // Theory body pattern (lines 62-76): using var repo = new SyntheticRepo(); using var links = new LinkScratch();
    //   var before = RepoSnapshot.Take(repo.Root, repo.OutDir); var exit = RunCli(...); AssertRefusedAndUntouched(...)
}
```

Plan notes:
- CR-01 tests: `outPath = @"\\?\" + Path.Combine(repo.RepoDir, "aliased-out")` and `@"\\localhost\" + drive + "$" + rest`. Guard with `if (!OperatingSystem.IsWindows()) return;` style used elsewhere (check `Skip` conventions before adding; the repo's script tests say "never skips", so prefer an early return or a Windows-only theory data). Loopback UNC needs the admin share `C$`; where it is unavailable the fail-closed result (`UnresolvableMessage`) or the refusal are both acceptable, so assert exit 1 and untouched repo, not the exact text, for that case.
- WR-01 tests: pre-plant `Path.Combine(repo.OutDir, "diff.json")` as a hard link (or file symlink) to a file inside `repo.RepoDir`, run CLI with `--out repo.OutDir`, assert exit 0, assert the repository file content and `RepoSnapshot` unchanged, and that `diff.json` in OutDir is now a regular file with new content. `RepoSnapshot.Take(repo.Root, repo.OutDir)` is the unchanged-repo oracle.
- WR-02 test: needs a seam to swap the out directory for a link between guard and write. Simplest is a direct `OutputWriter`/guard unit test with the re-check call, since `CliApp.Run` has no hook. Plan the seam (internal method plus `InternalsVisibleTo`, or an `Action` parameter) explicitly; check whether `InternalsVisibleTo` already exists in the Core csproj before relying on it.

### `tests/Vdiff.Tests/Safety/FinalPathTests.cs` (test, transform)

**Analog:** itself. Helpers to copy (lines 14-25):

```csharp
static readonly StringComparison Comparison =
    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
static void AssertSamePath(string expected, string actual) => Assert.True(
    string.Equals(expected, actual, Comparison), $"expected '{expected}' but got '{actual}'");
static string MakeReal(LinkScratch links, string name)
{
    var path = Path.Combine(links.Root, name);
    Directory.CreateDirectory(path);
    return path;
}
```

Test shape: `using var links = new LinkScratch(); var real = MakeReal(links, "real"); var resolvedReal = FinalPath.Resolve(real); ... AssertSamePath(resolvedReal, FinalPath.Resolve(@"\\?\" + real));`. Expected values are always derived from `FinalPath.Resolve` of a real directory, never from the raw temp path. Failure case pattern (lines 109-111): `Assert.Throws<VdiffException>(() => FinalPath.Resolve(...))` then `Assert.Equal(FinalPath.UnresolvableMessage, e.Message)`.

### `tests/Vdiff.Tests/Support/LinkScratch.cs` (test support, file-I/O)

**Analog:** itself. Only directory links exist today. Windows: `BoundedProcess.Run("cmd.exe", ["/c", "mklink", "/J", link, target], new ProcessLimits(TimeSpan.FromSeconds(30), 1024*1024, 1024*1024))`, non-zero exit throws `InvalidOperationException($"mklink /J exited {code}: {stderrTail}")`. Elsewhere `Directory.CreateSymbolicLink(link, relativeTarget ? Path.GetRelativePath(Root, target) : target)`. Every link is recorded in `_links` and removed by `Dispose` first, non-recursively (`Directory.Delete(link)` on Windows, `File.Delete` elsewhere), then `Root` is deleted recursively.

Plan notes: add `FileLink(string path, string target)` and `HardLink(string path, string target)` to this class (WR-01 needs them; nothing in `tests/` creates file links or hard links today):
- Hard link: Windows `cmd.exe /c mklink /H <link> <target>` (no privilege needed, same volume); POSIX has no .NET API, use `ln` through `BoundedProcess.Run("ln", [target, link], ...)`. Hard links must be created at an arbitrary path (inside the sandbox OutDir), not only under `Root`, so give the method an explicit full link path.
- File symlink: `File.CreateSymbolicLink(path, target)` needs privilege or Developer Mode on Windows; fall back to testing the hard link only on Windows when it throws, and say so in the test name. Cleanup rule: a hard link inside `repo.OutDir` is a normal file and `SyntheticRepo.Dispose` removes it; a file symlink is removed with `File.Delete` (does not follow).
- Keep the hygiene rule in the class doc comment: never link to the checkout or an ancestor of a sandbox, because `RepoSnapshot` and `SyntheticRepo.Dispose` recurse into junctions.

### `tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs` (test, process spawn)

**Analog:** itself. Both hosts are exercised through (lines 37-44, 64-82):

```csharp
public static IEnumerable<object[]> Hosts()
{
    yield return ["pwsh"];
    if (OperatingSystem.IsWindows())
    {
        yield return ["powershell"];
    }
}

static ProcessResult RunScript(string host, IEnumerable<string> scriptArguments, bool skipMermaid, TimeSpan timeout)
{
    var args = new List<string> { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
        "-File", Path.Combine(RepositoryRoot(), "scripts", "trace-run.ps1") };
    args.AddRange(scriptArguments);
    args.AddRange(["-VdiffDll", VdiffDll()]);
    if (skipMermaid) args.Add("-SkipMermaid");
    return BoundedProcess.Run(host, args, new ProcessLimits(timeout, 1024 * 1024, 1024 * 1024));
}
static List<string> CoreArguments(string repoPath, string baseId, string headId, string outPath) =>
    ["-Repo", repoPath, "-Base", baseId, "-Head", headId, "-Out", outPath];
```

Test method pattern to copy for a CR-01 script case (from lines 305-332):

```csharp
[Theory]
[MemberData(nameof(Hosts))]
public void <Name>(string host)
{
    using var repo = new SyntheticRepo();
    using var links = new LinkScratch();
    var (c1, c2, _) = BuildHistory(repo);
    var before = RepoSnapshot.Take(repo.Root, repo.OutDir);
    var result = RunScript(host, CoreArguments(repo.RepoDir, c1, c2, @"\\?\" + Path.Combine(repo.RepoDir, "out-here")), skipMermaid: true, TimeSpan.FromMinutes(5));
    var values = ParseSixLines(result.StdoutText);
    Assert.Equal(1, result.ExitCode);
    Assert.Equal("fail", values[0]);
    Assert.Equal("out-not-outside", values[1]);
    Assert.Equal("unknown", values[3]);
    Assert.Equal("no", values[4]);
    Assert.Equal("skipped", values[5]);
    AssertNothingLeaks(result, repo.RepoDir, repo.OutDir, c1, c2);
    Assert.Empty(Directory.EnumerateDirectories(repo.RepoDir, "out-here", SearchOption.AllDirectories));
    Assert.Equal(before, RepoSnapshot.Take(repo.Root, repo.OutDir));
}
```

Success-path analog (lines 358-375): `AssertPassed(result, values)`, `Assert.Equal(FiveFiles, OutputFiles(Path.Combine(repo.OutDir, "run")))`. Fail-closed analog (lines 377-395): reason `script-error`. Include `AssertNothingLeaks(result, <every path used>, c1, c2)` so no new path text reaches the six lines. A script WR-01 case needs a planted hard link in `repo.OutDir` and then a passing run, with repo bytes unchanged.

## Shared Patterns

### Fixed-message fail-closed (no path in output)
**Source:** `FinalPath.cs` lines 14, 40-44; asserted in `AliasedOutTests.cs` line 120 (`"vdiff: " + FinalPath.UnresolvableMessage + "\n"`, stdout empty).
**Apply to:** any new resolver/normaliser failure and any write-time refusal. Throw `VdiffException` with a fixed string; do not let an `IOException` message (which `Run` prints with `e.Message`) carry a path (T-01-38).

### Single refusal text
**Source:** `CliApp.cs` lines 88 and 96. **Apply to:** the second (pre-write) check and the CR-01 refusal, so all existing assertions on the message still hold.

### Scratch repo and snapshot oracle
**Source:** `SyntheticRepo` (`Root`, `RepoDir`, `OutDir`, `Write`, `Commit`, `Dispose`) and `RepoSnapshot.Take(repo.Root, repo.OutDir)` equality. **Apply to:** every new test; assert the repository is byte-for-byte unchanged and `OutDir` is empty on refusal.

### Script/C# rule parity
**Source:** comment at `trace-run.ps1` 159-165 ("The same rule as FinalPath.Resolve"). **Apply to:** keep the CR-01 rule identical in both and update the T-01-39 bound text in `FinalPath.cs`, `CliApp.cs` and the script header.

## No Analog Found

| Item | Role | Reason |
|------|------|--------|
| Hard-link / file-symlink helpers | test support | `LinkScratch` only makes directory links; no hard-link code anywhere in `tests/` (grep for `CreateHardLink`, `mklink /H`, `FileLinkTarget` found nothing). Use the plan notes under LinkScratch. |
| Temp-file-plus-move write | service | No other atomic-write code in `src/`; `OutputWriter.WriteText` is the only writer. Use standard `File.Move(src, dst, overwrite: true)`. |
| Direct `OutputWriter.WriteAll` test | test | No test calls it; `CliApp.Run` is the only path. |
| Write-time seam for WR-02 test | test | `CliApp.Run` has no injection point; the plan must add one or test the re-check as a unit. |

## Metadata

**Analog search scope:** `src/Vdiff.Core/Cli`, `src/Vdiff.Core/Output`, `scripts/`, `tests/Vdiff.Tests/{Safety,Support,Trial}`, grep over all `*.cs`/`*.ps1`.
**Files read:** FinalPath.cs, OutputWriter.cs, CliApp.cs (20-139), AliasedOutTests.cs, LinkScratch.cs, FinalPathTests.cs (1-60), TraceRunScriptTests.cs (1-120, 295-395), trace-run.ps1 (120-250, 285-325), 01-12-PLAN.md (grep of T-01-36..40 and tasks).
**Not read (not needed):** 01-CONTEXT.md, 01-RESEARCH.md, 01-VERIFICATION.md in full; scope was taken from the task statement.
