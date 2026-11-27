# Phase 3: Git Layer - Pattern Map

**Mapped:** 2026-11-26
**Files analyzed:** 22 (11 new source, 6 modified source, 1 new schema, 4 test/doc groups)
**Analogs found:** 21 / 22

All analog paths below were checked as git-tracked source (`git ls-files`). No gitignored mirror paths are used.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `src/Vdiff.Core/Git/GitObjectReader.cs` (extend: IsShallow, ParentsOf, MergeBases, ListTree, ListChanges, ListHunks; `DiffTreeArguments` to `-M50% -l1000`) | service | request-response (subprocess) | itself (`ListChanged`, `ParseDiffTree`, `Locate`) | exact |
| `src/Vdiff.Core/Git/ChangeSet.cs` (records: ChangeEntry, RenamePair, Hunk, FileHunks, ChangeSet) | model | transform | `ChangedFile`/`ChangeList` records, `GitObjectReader.cs:51-66` | exact |
| `src/Vdiff.Core/Git/TreeExporter.cs` | service | file-I/O, batch | `GitObjectReader.ReadBlobs` (plan from sizes, budget, `cat-file --batch` parse) plus `OutputWriter.WriteText` (CreateNew write) | role-match |
| `src/Vdiff.Core/Git/PathRules.cs` | utility | transform (pure) | `GitObjectReader.ValidateCommitInput`, `CliApp.ParseProtectedDirectories` (pure validators with fixed text) | partial |
| `src/Vdiff.Core/Ado/PrContext.cs` | utility | transform (env parsing) | `CliApp.ParseProtectedDirectories` | exact (env seam, fail closed) |
| `src/Vdiff.Core/Ado/AdoPullRequestClient.cs` | service | request-response (HTTP) | none in repo; shape from `BoundedProcess` limits discipline | no analog |
| `src/Vdiff.Core/Output/SkipWriter.cs` | utility | file-I/O | `OutputWriter.WriteText` (temp name plus `File.Move`) | exact |
| `docs/schema/vdiff-skip-v1.schema.json` | config (schema) | n/a | `docs/schema/vdiff-run-meta-v1.schema.json` | exact |
| `src/Vdiff.Core/Pipeline/TracerPipeline.cs` (modify) | service | transform | itself (`Run`, `BuildSide`) | exact |
| `src/Vdiff.Core/Cli/CliOptions.cs` (modify: `--pr`, `--keep-trees`) | utility | request-response | itself | exact |
| `src/Vdiff.Core/Cli/CliApp.cs` (modify: PR branch, tree lifetime, exit codes) | controller | request-response | itself | exact |
| `src/Vdiff.Core/Model/RunMeta.cs` (add trailing nullable `OtherMergeBases`) | model | transform | itself; `ExtractorRun` | exact |
| `src/Vdiff.Core/Process/BoundedProcess.cs` `GitCommand` (scrub token, `LC_ALL=C`) | config | n/a | itself (`RemovedVariables`, `BuildEnvironment`) | exact |
| `src/Vdiff.Core/Extraction/CppSidecar.cs` (pass scrubbed env) | service | subprocess | itself, line 273 | exact |
| `tests/Vdiff.Tests/Contract/SchemaFreezeTests.cs` (re-pin, fourth schema) | test | n/a | itself | exact |
| `tests/Vdiff.Tests/Support/SyntheticRepo.cs` (add `Merge`, `WriteTree`, `CommitTree`) | test support | n/a | itself (`Commit`, `AddCacheInfo`, `RunGit`) | exact |
| `tests/Vdiff.Tests/GitObjectReaderTests.cs` and new `Category=Git` tests | test | n/a | itself | exact |
| `tests/Vdiff.Tests/CliOptionsTests.cs` | test | n/a | itself | exact |
| `tests/Vdiff.Tests/Safety/HostileInputTests.cs` (stderr empty on success) | test | n/a | itself, line ~357 | exact |
| Fake ADO endpoint (`HttpMessageHandler` stub) in tests | test support | request-response | none; `CliHooks` seam style | no analog |
| `docs/how-to/private-fixtures.md`, `docs/schema/README.md` row | doc | n/a | existing docs | exact |

## Pattern Assignments

### `src/Vdiff.Core/Git/GitObjectReader.cs` (extend)

**Analog:** itself. Every new call must follow these four conventions.

**Pure argument builder, full-id guard, hardened prefix** (lines 109-120):
```csharp
public static IReadOnlyList<string> DiffTreeArguments(string repoPath, string baseOid, string headOid)
{
    RequireFullId(baseOid);
    RequireFullId(headOid);
    return GitCommand.Build(repoPath, ["diff-tree", "-r", "-z", "--raw", "--no-renames", "--no-abbrev", baseOid, headOid]);
}
public static IReadOnlyList<string> CatFileBatchCheckArguments(string repoPath) =>
    GitCommand.Build(repoPath, ["cat-file", "--batch-check"]);
```
Change `--no-renames` to `-M50%`, `-l1000` (call A, no `-w`). Add a second builder for call B (`-p -U0 -w --ignore-blank-lines --text --no-textconv --no-ext-diff --no-color --full-index`). Use `--end-of-options` before any user-derived revision, as `ResolveCommitArguments` does (line 106).

**Run helper and failure text** (lines 447-455). New methods call `Run(args, limits, stdin)` and `Failure("merge-base", result)`; never build a `Process` directly:
```csharp
ProcessResult Run(IReadOnlyList<string> args, ProcessLimits limits, byte[]? stdin = null) =>
    BoundedProcess.Run("git", args, limits, stdin, GitCommand.BuildEnvironment());

static VdiffException Failure(string what, ProcessResult result) { ... $"git {what} failed (exit {result.ExitCode})" ... }
```

**NUL-record parse with strict UTF-8 and ordinal sort** (lines 183-242, `ParseDiffTree`). Extend it for rename records (`R<score>`, then old path, then new path), and keep the per-side mode instead of collapsing non-regular sides to null (RESEARCH Pitfall 2). Reuse as-is:
```csharp
var fields = meta.StartsWith(':') ? meta[1..].Split(' ') : Array.Empty<string>();
...
catch (DecoderFallbackException)
{
    unmodelled.Add(new UnmodelledFile(Encoding.UTF8.GetString(pathBytes), "non-utf8-path"));
    continue;
}
...
files.OrderBy(f => f.Path, StringComparer.Ordinal).ToList()
```

**Header-by-header `cat-file` parse** (lines 349-379, `ReadBlobs` pass 2) is the template for the chunked `cat-file --batch` read in `TreeExporter`: header line split on space, oid match check, `missing` check, size cross-check, trailing `\n` check, `AsSpan(position, size).ToArray()`.

**Missing-object detection** copies the `parts[1] == "missing"` branch (line 304) but now throws exit 1 with the `fetchDepth: 0` message instead of emitting `object-missing` (D-10). Also treat `ls-tree -l` size `BAD` as missing.

**Locate and Failure for repo location** (lines 411-443): `IsShallow` should follow the `rev-parse` pattern (`GitCommand.Build(_repoPath, ["rev-parse", "--is-shallow-repository"])`, `SmallLimits`).

---

### `src/Vdiff.Core/Git/ChangeSet.cs` (model)

**Analog:** `GitObjectReader.cs:51-66`. Sealed positional records, no behaviour:
```csharp
public sealed record ChangedFile(string Path, string? BaseBlob, string? HeadBlob);
public sealed record UnmodelledFile(string Path, string Reason);
public sealed record ChangeList(IReadOnlyList<ChangedFile> Files, IReadOnlyList<UnmodelledFile> Unmodelled);
```
New records (names from RESEARCH): `Hunk(OldStart, OldCount, NewStart, NewCount)`, `RenamePair(OldPath, NewPath, ScorePercent)`, plus per-side state (`Absent`, `Regular`, `Symlink`, `Submodule`), `ChangeSet` with a rename-limit-hit flag. Use `IReadOnlyList<>` fields. Decide whether `ChangedFile`/`ChangeList` are replaced or kept for source compatibility; `TracerPipeline` and tests use them.

---

### `src/Vdiff.Core/Git/TreeExporter.cs` (service, file-I/O, batch)

**Analog:** `GitObjectReader.ReadBlobs` (plan from sizes, deterministic budget) and `OutputWriter.WriteText` (safe write).

**Deterministic budget pattern** (lines 292-333): sort by ordinal path, running `total`, a sticky `budgetExhausted` flag so the first entry that passes a limit and every later one in order is left out. Reuse for D-08 tiers (tier index first, then ordinal path inside a tier).
```csharp
else if (budgetExhausted || total + size > maxTotalBytes)
{
    budgetExhausted = true;
    skipReason[i] = "total-too-large";
}
```

**Create-new file write** (`OutputWriter.cs:109`): `new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)`. For the export, write directly to the final path with `CreateNew` (fresh root, no pre-existing entries, no overwrite), and assert `Path.GetFullPath(dest)` starts with `root + separator` before every write (Pitfall 9).

**Chunking:** one `BoundedProcess.Run` per chunk (at most 64 MiB or about 2,000 entries), `ProcessLimits(TimeSpan.FromMinutes(5), contentBytes + count * PerObjectOutputOverhead + 4096, 256 * 1024)` exactly as `ReadBlobs` computes at lines 341-342.

**Temp root lifetime:** create through `HeldDirectory.Create(path, p => CliApp.FindRefusal(p, location, protectedDirs))` (see CliApp section). Dispose the hold before `Directory.Delete`, with the retry loop from `SyntheticRepo.Dispose` (`SyntheticRepo.cs:136-160`: five attempts, `File.SetAttributes(entry, FileAttributes.Normal)`, `Thread.Sleep(100)`). Delete failure is a stdout warning, not a run failure.

---

### `src/Vdiff.Core/Git/PathRules.cs` (utility, pure)

**Analog:** pure validators with fixed text and no value echo: `GitObjectReader.ValidateCommitInput` (lines 125-131) and `CliApp.ParseProtectedDirectories` (lines 223-239). Take an injected `isWindows` flag so Windows-only rules are unit-testable on any host (RESEARCH Environment Availability). OS-independent rules (traversal, `.git` component, NUL, case and normalisation collisions) always apply; Windows-only rules (reserved names, trailing dot/space, `:<>"|?*`) only when the flag is set. Return a reason code from the closed list in RESEARCH "Unmodelled reason encoding", never throw for a single bad path.

---

### `src/Vdiff.Core/Ado/PrContext.cs` (utility, env parsing)

**Analog:** `CliApp.ParseProtectedDirectories` (lines 223-239) and the seam at line 75.

**Env seam and fixed-text fail-closed** (copy the shape):
```csharp
var protectedDirectories = ParseProtectedDirectories(hooks?.GetEnvironmentVariable ?? Environment.GetEnvironmentVariable);
...
internal static IReadOnlyList<string> ParseProtectedDirectories(Func<string, string?> getEnvironmentVariable)
{
    var value = getEnvironmentVariable(ProtectedDirectoriesVariable);
    ...
    throw new VdiffException(ProtectedDirectoriesMalformed); // names the variable, never the value
}
```
`PrContext.FromEnvironment(Func<string,string?>)` reads the names in RESEARCH "Prescriptions", validates with the regexes there, reuses `GitObjectReader.ValidateCommitInput` for commit ids, and throws `VdiffException` with fixed messages that never echo a value. Use `[GeneratedRegex(..., RegexOptions.CultureInvariant)]` as in `CliOptions.cs:136-137`.

---

### `src/Vdiff.Core/Ado/AdoPullRequestClient.cs` (service, HTTP)

**No analog.** Use RESEARCH "REST client shape" directly. Borrow only conventions: take its collaborator (`HttpMessageHandler`) by constructor like `HeldDirectory.Create` takes `findRefusal`; fixed error text carrying at most a numeric status; throw `VdiffException`. Test seam: a new optional member on the `CliHooks` record (`CliApp.cs:21-25`), for example `Func<HttpMessageHandler>? CreateAdoHandler = null`, null in production. Remember the hook comment: a hook records, never asserts.

---

### `src/Vdiff.Core/Output/SkipWriter.cs` (utility, file-I/O)

**Analog:** `OutputWriter.WriteText` (`OutputWriter.cs:102-129`). Copy the whole method shape (LF-normalised UTF-8 without BOM, random `.tmp-<guid>` name via `FileMode.CreateNew`, `File.Move(temp, final, overwrite: true)`, best-effort temp delete in `catch`). Reuse rather than duplicate if `WriteText` can be made `internal`. Only `skip.json` is written on a skip: no `run.meta.json`, model, diff or report (D-16). `OutputWriter.FileNames` (line 23) stays the six-file list; do not add `skip.json` to it.

---

### `docs/schema/vdiff-skip-v1.schema.json` (new schema)

**Analog:** `docs/schema/vdiff-run-meta-v1.schema.json`. Copy: `$schema` draft-07, `title`, `description`, `"additionalProperties": false`, `required`, a `schema` property with a one-value `enum`, and only keywords in the `docs/schema/README.md` subset. Properties: `schema` (`vdiff-skip/v1`), `reason` (enum `fork`, `draft`), likely `vdiff_version`. Consequences to plan for: add a pin in `SchemaFreezeTests`, a `SchemaSupport` constant, a README table row, and update the "exactly three schemas" assertions (`SchemaFreezeTests.cs:37-49`, `SchemaValidationTests.cs:74` `Assert.Equal(3, SchemaSupport.AllSchemaFiles.Count)`).

---

### `src/Vdiff.Core/Model/RunMeta.cs` (add optional property)

**Analog:** itself (lines 15-20). Add a trailing nullable parameter; position in the record is position in JSON, and nullable means optional to `SchemaParityTests` (`IsOptional`):
```csharp
public sealed record RunMeta(
    string Schema, string VdiffVersion, string BaseCommit, string HeadCommit,
    IReadOnlyList<ExtractorRun> Extractors,
    IReadOnlyList<string>? OtherMergeBases = null)
```
Pass `null`, not an empty list, for one merge-base (`WhenWritingNull` would write `[]`). Matching schema property: `other_merge_bases`, array of strings with the `^[0-9a-f]{40}([0-9a-f]{24})?$` pattern used for `base_commit`, not in `required`. Update `Canonical.Canonicalise(RunMeta)` (`Canonical.cs:53-65`) to sort ordinally, `ContractSamples.FullMeta()`, and re-pin `RunMetaSchemaSha256` (`SchemaFreezeTests.cs:17`) once, with the reason in the commit message.

---

### `src/Vdiff.Core/Process/BoundedProcess.cs` `GitCommand` and `CppSidecar.cs`

**Analog:** itself. Token scrub and locale pin go in the same places the existing hardening lives:
```csharp
static readonly string[] RemovedVariables =
[ "GIT_DIR", "GIT_WORK_TREE", "GIT_INDEX_FILE", "GIT_COMMON_DIR", "GIT_NAMESPACE", "GIT_EXTERNAL_DIFF", ];
...
env["GIT_OPTIONAL_LOCKS"] = "0";
env["GIT_TERMINAL_PROMPT"] = "0";
env["GIT_NO_LAZY_FETCH"] = "1";
```
Add `SYSTEM_ACCESSTOKEN` and `AZURE_DEVOPS_EXT_PAT` to `RemovedVariables`; add `env["LC_ALL"] = "C"` and an empty `LANGUAGE` (rename-limit warning is matched on English stderr). `CppSidecar.Extract` (line 273) calls `BoundedProcess.Run(command.File, command.LeadingArgs, processLimits, request)` with no `env`, so it inherits the token: pass a scrubbed environment as the fifth argument (same signature `GitObjectReader.Run` uses). Test with a fake child that dumps its environment.

---

### `src/Vdiff.Core/Pipeline/TracerPipeline.cs` (modify)

**Analog:** itself. Keep `Run`'s shape (pure, writes nothing, returns `PipelineResult`) and `BuildSide`'s call into `CppSidecar.Extract(... new SidecarInput(path, bytes) ...)`; replace the `reader.ReadBlobs` source (line 73) with bytes read from the exported tree roots, and replace `reader.ListChanged` (line 28) with the `ChangeSet`. Keep the pathLevel plus per-side unmodelled merge:
```csharp
return (ModelBuilder.Build(response, [.. pathLevel, .. read.Unmodelled], blank), response);
```
D-11 exclusion set (unreadable on one side, rename partners) must feed both sides' unmodelled lists with `<side>-<reason>` codes so counts stay equal in `ModelDiffer`. `PipelineResult` gains the internal rename and hunk records (D-18); `RunMeta` construction (lines 48-59) gains `OtherMergeBases`. Do not make this method read the environment or the clock.

---

### `src/Vdiff.Core/Cli/CliOptions.cs` (modify)

**Analog:** itself. Today every flag needs a value (lines 57-60) and the allowed set is line 52. Add a value-less boolean set (`--pr`, `--keep-trees`) handled before the value check, keep the once-only rule (`single.TryAdd`), and keep `Printable` for echoed unknown names. Required flags become conditional: offline requires `--base`/`--head`; `--pr` forbids them (D-13); `--repo` and `--out` always required. Record gains `bool Pr, bool KeepTrees`; update `Usage` and `CliOptionsTests`. Note `new CliOptions("", "", "", "", [], [], Help: true)` at line 43 needs the new fields.

---

### `src/Vdiff.Core/Cli/CliApp.cs` (modify)

**Analog:** itself.

**Exit-code contract and catch ladder** (lines 113-124): `VdiffException` to `Fail(stderr, message)` exit 1; IO errors; unexpected. A skip returns 0 with one fixed stdout line (not stderr). Informational output (skipped-file counts, rename-limit notice, `--keep-trees` path) goes to `stdout` only (HostileInputTests asserts empty stderr on success).

**Order of work to keep** (lines 74-108): arguments, env seam, repo check, `new GitObjectReader` (git version), `Locate`, resolve ids, `RefuseUnsafeOut`, pipeline, second `RefuseUnsafeOut`, `HeldDirectory.Create(resolvedOut, p => FindRefusal(p, location, protectedDirectories))`, write. In PR mode insert before any history work: PrContext validation, fork skip, draft GET (RESEARCH Pattern 3). Tree temp root: `Agent.TempDirectory` (`AGENT_TEMPDIRECTORY`) in PR mode, `Path.GetTempPath()` offline, judged with the same `FindRefusal`, deleted in `finally` unless `--keep-trees`:
```csharp
using var held = HeldDirectory.Create(resolvedOut, p => FindRefusal(p, location, protectedDirectories));
```

---

### Tests

**`SyntheticRepo` additions** (analog: itself, `SyntheticRepo.cs`). `Commit` advances a fixed clock per call (lines 93-101), `AddCacheInfo` makes symlink/gitlink entries by plumbing (133-134), `RunGit` is the private stdin-capable runner (62-68), `WriteBlob` shows the stdin pattern (117-126). Add `Merge(message)` that advances the clock like `Commit` (Pitfall 14), and `WriteTree`/`CommitTree` using `RunGit` with stdin for `mktree`/`commit-tree` (hostile trees; `git mktree -z` for newline names).

**Test class shape** (analog: `GitObjectReaderTests.cs:11-54`): `using var repo = new SyntheticRepo();`, `repo.Write`, `repo.Commit`, `new GitObjectReader(repo.RepoDir)`, `Assert.Throws<VdiffException>` for rejects, `[Theory]/[InlineData]` for input tables, a local `Raw(params object[])` helper for byte fixtures. New pure git-layer classes get `[Trait("Category", "Git")]`; classes that call `CliApp.Run`, the real sidecar, or process-wide env carry `[Collection("sidecar")]`. PR-mode tests supply variables through `CliHooks.GetEnvironmentVariable`, never the process environment.

**Frozen-contract tests** (analog: `SchemaFreezeTests.cs`): pins are `const string ...Sha256` plus a `TheoryData` row; hash is over LF-normalised text (`LfHash`). Add a skip-schema pin and row; re-pin run-meta once.

**Offline migration:** existing tests asserting `object-missing` unmodelled become exit-1 tests (D-10); tests relying on `--no-renames` change-list behaviour change (D-20).

## Shared Patterns

### Hardened read-only git
**Source:** `src/Vdiff.Core/Process/BoundedProcess.cs:258-316` (`GitCommand`)
**Apply to:** every new git call (merge-base, rev-list, ls-tree, cat-file, diff-tree). Always `GitCommand.Build(repoPath, [...])` plus `GitCommand.BuildEnvironment()`, through `BoundedProcess.Run` with an explicit `ProcessLimits`; plumbing only; never fetch (D-09).

### Fixed-text failure, no value echo
**Source:** `GitObjectReader.Failure` (lines 450-455), `CliApp.Fail` (127-131), `ParseProtectedDirectories` (223-239)
**Apply to:** PrContext, AdoPullRequestClient, TreeExporter, missing-history messages. Messages name the fix (`fetchDepth: 0`), never a token, URL, org, project or raw env value.

### Containment of anything written outside `--out`
**Source:** `CliApp.RefuseUnsafeOut`/`FindRefusal` (lines 165-215), `HeldDirectory.Create` (58-64)
**Apply to:** temp tree root (D-05). `FindRefusal(path, location, protectedDirectories)` is the single shared judge; do not write new containment logic.

### Safe file write
**Source:** `OutputWriter.WriteText` (lines 102-129)
**Apply to:** `skip.json`; tree files use `FileMode.CreateNew` under the same no-overwrite thinking.

### Ordinal, deterministic ordering
**Source:** `StringComparer.Ordinal` in `ParseDiffTree` (line 240) and `ReadBlobs` (line 270)
**Apply to:** change list, hunks, tiers in the export plan, `other_merge_bases` (R-23). Never trust git's output order.

### Test seam through `CliHooks`
**Source:** `CliApp.cs:21-25`
**Apply to:** ADO handler injection and the env reader. Hooks record, never assert.

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `src/Vdiff.Core/Ado/AdoPullRequestClient.cs` | service | HTTP request-response | No network code exists yet; use RESEARCH "REST client shape" |
| Fake ADO `HttpMessageHandler` stub (tests) | test support | HTTP | No prior HTTP tests; hand-write a small stub, inject via a new `CliHooks` member |

## Metadata

**Analog search scope:** `src/Vdiff.Core/**`, `tests/Vdiff.Tests/**` (Support, Contract, Safety, top-level), `docs/schema/`
**Files read:** GitObjectReader.cs, CliOptions.cs, CliApp.cs, TracerPipeline.cs, HeldDirectory.cs, RunMeta.cs, BoundedProcess.cs (GitCommand section), OutputWriter.cs (WriteText), CppSidecar.cs (Extract), vdiff-run-meta-v1.schema.json, SyntheticRepo.cs, SchemaFreezeTests.cs, GitObjectReaderTests.cs (head)
**Not read (RESEARCH second page, lines 429-514, validation map):** the planner should read it for the requirement-to-test table.
**Pattern extraction date:** 2026-11-26
