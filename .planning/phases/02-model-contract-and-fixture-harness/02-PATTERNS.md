# Phase 2: Model Contract and Fixture Harness - Pattern Map

**Mapped:** 2026-11-20
**Files analyzed:** 27 new or modified (grouped where they share a pattern)
**Analogs found:** 22 / 27 (all analog paths verified git-tracked via `git ls-files`)

Note: the repository has no `docs/schema/`, `docs/how-to/`, `azure-pipelines.yml` or `tests/fixtures/` yet, and no YAML or JSON-Schema code. Those rows are in "No Analog Found".

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `src/Vdiff.Core/Model/ModelTypes.cs` (v1 records, v0 deleted) | model | transform | itself (`ModelTypes.cs` v0) | exact (in-place rewrite) |
| `src/Vdiff.Core/Model/Canonical.cs` (new ordering module) | utility | transform | `src/Vdiff.Core/Diff/ModelDiffer.cs` (`Index`/`CompareTypes`, total ordinal order) | role-match |
| `src/Vdiff.Core/Model/RunMeta.cs` (volatile metadata record) | model | transform | `ModelTypes.cs` records (`ExtractionInfo`, `DiffFile`) | role-match |
| `src/Vdiff.Core/Output/OutputWriter.cs` (FileNames, run.meta.json, canonicalise in `Serialize`) | service | file-I/O | itself | exact (in-place edit) |
| `src/Vdiff.Core/Model/ModelBuilder.cs`, `Diff/ModelDiffer.cs`, `Output/MermaidWriter.cs`, `HtmlWriter.cs`, `Pipeline/TracerPipeline.cs`, `Cli/CliApp.cs`, `Cli/CliOptions.cs`, `Output/HeldDirectory.cs` (v1 migration) | service | transform / request-response | themselves | exact (in-place edit) |
| `docs/schema/vdiff-model-v1.schema.json`, `vdiff-diff-v1.schema.json`, `vdiff-run-meta-v1.schema.json` | config (contract) | n/a | none in repo; shapes from RESEARCH "Proposed v1 Shapes" | no analog |
| `tests/Vdiff.Tests/Contract/SchemaValidationTests.cs`, `SchemaParityTests.cs`, `CanonicalSerialisationTests.cs` | test | request-response | `tests/Vdiff.Tests/Output/DeterminismTests.cs` | role-match |
| `tests/Vdiff.Tests/Fixtures/FixtureLoader.cs`, `FixtureHarness.cs`, `Ratchet.cs`, `FixtureReport.cs` | test support | batch / file-I/O | `tests/Vdiff.Tests/Support/SyntheticRepo.cs` + `DeterminismTests.Run` | role-match |
| `tests/Vdiff.Tests/Fixtures/FixtureHarnessTests.cs`, `FixtureGoldenTests.cs`, `PrivateFixtureTests.cs` | test | file-I/O | `DeterminismTests.cs`, `RepoUnchangedMatrixTests.cs` (env scope + early return) | role-match |
| `tests/Vdiff.Tests/Ci/PipelineYamlTests.cs` | test | file-I/O | `tests/Vdiff.Tests/Safety/RepoHygieneTests.cs` (`FindRoot`, scans tracked files) | role-match |
| `tests/Vdiff.Tests/Safety/RepoHygieneTests.cs` (guard + theory rows) | test | file-I/O | itself | exact (in-place edit) |
| `tests/Vdiff.Tests/Vdiff.Tests.csproj` (NJsonSchema, YamlDotNet) | config | n/a | itself | exact |
| `.gitattributes` (+ fixtures, schema, pipeline rules) | config | n/a | itself | exact |
| `tests/fixtures/<shape>/{fixture.yml,base/,head/,expected/}` x5 | fixture data | n/a | `DeterminismTests.cs` `BaseHeader`/`HeadHeader` (content style only) | partial |
| `azure-pipelines.yml` | config | n/a | none | no analog |
| `docs/how-to/private-fixtures.md` | docs | n/a | `docs/trials/phase-1-offline-tracer.md` (pass/fail-only record rule) | partial |
| 999.1 fixes: `Trial/TraceRunScriptTests.cs:1132`, `Safety/RepoUnchangedMatrixTests.cs:211`, `GitObjectReaderTests.cs:38` | test | file-I/O | `FinalPath.Resolve` (`src/Vdiff.Core/Cli/FinalPath.cs:59`) | exact |
| Existing tests updated to the v1 shape and the new file set (about 10 files, listed in RESEARCH Pitfall 6) | test | n/a | themselves | exact |

## Pattern Assignments

### `src/Vdiff.Core/Model/ModelTypes.cs` (model, transform)

**Analog:** the current file. Keep v0 field names where they survive (RESEARCH Proposed v1 Shapes).

**Record style** (lines 6-20, 33-48): positional `sealed record`, `IReadOnlyList<T>`, nullable `string?` for fields omitted from JSON, `const SchemaName` inside the file record, and an `init` property for later-added fields.
```csharp
public sealed record TypeModel(
    string Id, string Fqn, string Kind, string Name, string Scope, string File,
    IReadOnlyList<string> Bases, SourceSpan Span, IReadOnlyList<MemberModel> Members);

public sealed record ModelFile(string Schema, string Commit, ExtractionInfo Extraction, ...)
{
    public const string SchemaName = "vdiff-model/v0-tracer";   // becomes "vdiff-model/v1"
    public IReadOnlyList<string> Duplicates { get; init; } = [];
}
```
**Optional fields** (lines 60-68): `MemberDiff` uses `string? AccessBefore` etc.; null is omitted by `DefaultIgnoreCondition.WhenWritingNull`, so schema `required` lists must exclude every nullable record property.

**States constants** (lines 92-99): extend `States` with `Renamed`, `BodyChanged = "body-changed"`.

**v1 deltas to apply:** drop `Commit`, `ExtractionInfo.TreeSitter*` (they move to `RunMeta`); drop `DiffFile.Base/Head`; add `Edges`, `Language` on types, `Modifiers`, per-file `Mode`/`FallbackReason`. Record property names must equal schema `properties` keys after `SnakeCaseLower` (parity test).

---

### `src/Vdiff.Core/Model/Canonical.cs` (utility, transform)

**Analog:** `src/Vdiff.Core/Diff/ModelDiffer.cs` (doc comment lines 12-17, `Index(..., CompareTypes)`, and `StringComparer.Ordinal` ordering at line 27).
```csharp
var ids = baseTypes.Keys.Concat(headTypes.Keys)
    .Distinct(StringComparer.Ordinal).OrderBy(i => i, StringComparer.Ordinal);
```
Move the comparers (`CompareTypes` and the member and base ordering) out of `ModelDiffer`/`ModelBuilder` into this one module so there is one ordering definition; use the ordering table in RESEARCH Pattern 2. Make every comparer end with an ordinal compare of the element's serialised JSON. Always use `StringComparer.Ordinal`, never culture compare (the test project sets `InvariantGlobalization`).

---

### `src/Vdiff.Core/Output/OutputWriter.cs` (service, file-I/O)

**Analog:** itself.

**Serialiser** (lines 26-36): the single shared options object; keep it, and call `Canonical.Canonicalise` inside `Serialize` for `ModelFile` and `DiffFile`.
```csharp
static readonly JsonSerializerOptions JsonOptions = new()
{
    WriteIndented = true, NewLine = "\n",
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
};
public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions) + "\n";
```
**File names** (lines 15-19): constants today; add `public static readonly string[] FileNames` for the five canonical files plus `MetaFile = "run.meta.json"`, so the ten-plus tests stop hard-coding the list.

**Write path** (lines 53-61 and 72-99): add one `WriteText(held.Path, MetaFile, Serialize(meta))` line in `WriteAll`; reuse `WriteText` (temp name with `FileMode.CreateNew`, then `File.Move(overwrite: true)`) unchanged.

---

### `tests/Vdiff.Tests/Fixtures/FixtureHarness.cs` (test support, batch/file-I/O)

**Analogs:** `DeterminismTests.cs` (CLI driving) and `SyntheticRepo.cs` (two-commit repo).

**Drive the CLI end to end** (`DeterminismTests.cs` lines 73-84):
```csharp
var args = new List<string> { "--repo", repo.RepoDir, "--base", baseId, "--head", headId, "--out", outDir };
args.AddRange(extra);                       // config.include -> repeated --include, config.blank -> repeated --blank
using var stdout = new StringWriter();
using var stderr = new StringWriter();
var exit = CliApp.Run(args, stdout, stderr);
```
Unlike this analog, a non-zero exit or non-empty stderr must map to FAIL, never "pending" (Ratchet vacuity guard).

**Build the two-commit repo** (`DeterminismTests.cs` lines 58-69, 60-67 pattern; `SyntheticRepo.Write/Delete/Commit` at lines 74-101): write every `base/` file with `File.ReadAllBytes` (never `ReadAllText`), `repo.Commit("base")`, `repo.Delete` the tracked files absent from `head/`, write every `head/` file, `repo.Commit("head")`. `SyntheticRepo` fixes author, committer, dates and `core.autocrlf=false` (lines 37-46, 93-101), so SHAs are deterministic.

**Sidecar collection rule** (`SidecarFixture.cs` lines 12-16): any class that calls `CliApp.Run` or sets env vars carries `[Collection("sidecar")]`.

**Private mode env handling:** wrap with `EnvironmentScope` (`Support/EnvironmentScope.cs` lines 14-29), setting `CliApp.ProtectedDirectoriesVariable` (`CliApp.cs:43`) to this repo's root so `--out` inside the repo is refused:
```csharp
using var scope = new EnvironmentScope(new Dictionary<string, string?> {
    [CliApp.ProtectedDirectoriesVariable] = repoRoot });
```
**Repo root discovery:** copy `RepoHygieneTests.FindRoot` (lines 30-42): walk up to `vdiff.sln`, throw `XunitException` rather than skipping.

**Mismatch diff (D-08):** run `git diff --no-index --no-color` through `BoundedProcess.Run("git", [.. GitCommand.Prefix, ...], limits, null, env)` as in `RepoHygieneTests.Git` (lines 44-59), including the `XunitException` wrap if git cannot run. Only `diff`, `rev-parse`, `ls-files` verbs: the hygiene guard bans `fetch|pull|push|remote|ls-remote|submodule` as a first argument (lines 131-139).

---

### `tests/Vdiff.Tests/Fixtures/Ratchet.cs` (utility, transform)

**Analog:** none for logic; style from the small static pure helpers such as `RepoHygieneTests.TrackedPathProblems` (lines 109-124: `public static` pure function returning a list of reasons, driven by a `[Theory]` over rows). Implement `Ratchet.Evaluate(state, matched, toolOk)` with the four-case truth table plus the "tool failed, output missing, schema invalid, golden missing" FAIL row (RESEARCH Pattern 5), and unit-test every row as `[Theory]` `InlineData`.

---

### `tests/Vdiff.Tests/Contract/*Tests.cs` (test, request-response)

**Analog:** `DeterminismTests.cs`: `[Collection("sidecar")]` class (line 16), local `ReadOptions` for `SnakeCaseLower` deserialisation (line 22), a static `Build(SyntheticRepo)` helper, and byte-hash comparisons (lines 86-90).
```csharp
static readonly JsonSerializerOptions ReadOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
static string[] Hashes(string outDir) =>
    FileNames.Select(n => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(outDir, n))))).ToArray();
```
Extend `Hashes` to the five canonical files only; keep `run.meta.json` in the local-identity-leak test (`DeterminismTests.cs` lines 191-228) and the BOM/LF test (lines 231-247). Schema-only tests (no CLI) do not need the sidecar collection. Every keyword used by a schema needs at least one negative document that must be rejected (D-15). The seeded Fisher-Yates shuffle test uses reflection over record collections, with a coverage guard.

---

### `tests/Vdiff.Tests/Safety/RepoHygieneTests.cs` (guard edit)

**Analog:** itself. Change `TrackedPathProblems` (lines 109-124) and the two `GeneratedFileNames`/`GeneratedDirectoryNames` arrays (lines 25-28) in the same commit as the first golden: allow exactly `tests/fixtures/<shape>/expected/diff.json` and `.../expected/summary.mmd`. Keep an existing flagged row (`tests/fixtures/base.model.json`, around line 259) and add flagged rows for `tests/fixtures/small-cpp/head/diff.json` and `.../expected/report.html`. Add the CR/BOM-free fixture check beside the existing tracked-file scan (`Git(root, "ls-files", "-z")`, lines 93-100).

---

### `tests/Vdiff.Tests/Ci/PipelineYamlTests.cs` (test, file-I/O)

**Analog:** `RepoHygieneTests` structure (`FindRoot`, read-only scan, never skip). Assert `azure-pipelines.yml` names only `windows-latest`/`ubuntu-latest` and contains none of: `organization`, `project:`, `pool:` with a `name:` key, or any target string. Parse with YamlDotNet strict, or plain text scan.

---

### 999.1 portability fixes (test, file-I/O)

**Analog:** the test bodies themselves.

1. Hook execute bit: `RepoUnchangedMatrixTests.cs` lines 219-229 and the twin at `Trial/TraceRunScriptTests.cs` ~1141-1150. After `File.WriteAllBytes(hook, ...)` add:
```csharp
if (!OperatingSystem.IsWindows())
    File.SetUnixFileMode(hook, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
```
The positive control (lines 232-238) proves the hook ran.
2. `GitObjectReaderTests.cs:38`: replace
```csharp
static string SamePath(string path) => Path.GetFullPath(path).TrimEnd('\\', '/');
```
with a version that passes through `FinalPath.Resolve(path)` (`src/Vdiff.Core/Cli/FinalPath.cs:59`, `public static string Resolve(string path)`), then `TrimEnd('\\', '/')`. The `Vdiff.Core.Cli` namespace import is needed (the file imports `Vdiff.Core`, `Vdiff.Core.Git`, ... but not `Vdiff.Core.Cli`).

---

### `.gitattributes` and `Vdiff.Tests.csproj`

**Analog:** themselves. Rule style (`.gitattributes` lines 4-10): `<glob> text eol=lf`, no catch-all. Add `tests/fixtures/** text eol=lf`, `docs/schema/*.json text eol=lf`, `azure-pipelines.yml text eol=lf`. Package style (`Vdiff.Tests.csproj` lines 11-15): exact pinned versions, no ranges:
```xml
<PackageReference Include="NJsonSchema" Version="11.6.2" />
<PackageReference Include="YamlDotNet" Version="18.1.0" />
```
The shipped `Vdiff.Core` and `Vdiff.Cli` projects get no new dependency. Gate each install behind a `checkpoint:human-verify` (the legitimacy seam does not cover NuGet).

---

### `tests/fixtures/<shape>/` (fixture data)

No structural analog. Content style: the synthetic C++ in `DeterminismTests.cs` lines 26-56 (a namespace, an export macro blanked through `config.blank`, a derived class, an added operator, a removed header). Use `Synthetic`-style generic names only. `fixture.yml` shape is fixed in RESEARCH Pattern 5 (`shape`, `description`, `config.include|blank`, `expected.diff|summary: {state: pending|green, due: N}`), with strict keys. Summary goldens follow the Phase 1 trailing-tag format emitted by `MermaidWriter` (start-of-file shape pinned at `DeterminismTests.cs:137`: `classDiagram\n  classDef added ...`). Each real fixture needs one feature the tracer cannot emit (RESEARCH Pitfall 10).

---

## Shared Patterns

### Public-repository hygiene (D15)
**Source:** `tests/Vdiff.Tests/Safety/RepoHygieneTests.cs` and the memory note (`check-public-diff.sh` before commits). **Apply to:** every file in this phase, especially fixtures, `azure-pipelines.yml`, `docs/how-to/private-fixtures.md`. No target-organisation or product names, SHAs, paths or type names, and no `git@`/`ssh://` text anywhere under `tests/` or `scripts/`.

### Never-skip guards
**Source:** `RepoHygieneTests.FindRoot` (lines 40-41) and `SidecarFixture` (a failed build fails the run). **Apply to:** all harness and CI tests. Missing prerequisite means a failing assertion. The one exception is the private-fixture test, which returns early when `VDIFF_FIXTURES_DIR` is unset (documented precedent: 01-UAT-10); do not use `Skip`.

### Deterministic ordering and bytes
**Source:** `OutputWriter.cs` lines 26-36, 72-99 and `ModelDiffer.cs` lines 12-17, 27. **Apply to:** model, diff, goldens. Ordinal comparers only, LF, UTF-8 without a BOM, trailing LF, null omitted, integers for scores.

### Sidecar collection and env vars
**Source:** `Support/SidecarFixture.cs` lines 12-16, `Support/EnvironmentScope.cs`. **Apply to:** every test that calls `CliApp.Run` or sets env vars: `[Collection("sidecar")]`, `EnvironmentScope` for variables.

### Read-only git access
**Source:** `Vdiff.Core.Process.GitCommand.Prefix` (`["-c","core.fsmonitor=false","--no-optional-locks"]`, asserted at `Support/SupportTests.cs:71`) and `BoundedProcess.Run`. **Apply to:** all harness git calls, including private mode.

## No Analog Found

| File | Role | Data Flow | Reason |
|---|---|---|---|
| `docs/schema/*.schema.json` (3 files) | contract | n/a | No JSON Schema in the repo; use RESEARCH "Proposed v1 Shapes" and the keyword allow-list (draft-07 subset, single-value `enum` not `const`, `additionalProperties: false`) |
| `azure-pipelines.yml` | CI config | n/a | No pipeline file exists; use RESEARCH skeleton, hosted images only |
| `Fixtures/FixtureLoader.cs` YAML parsing | utility | file-I/O | No YAML code exists; YamlDotNet strict POCO per RESEARCH Pattern 5 |
| `.planning`-external private recipe `docs/how-to/private-fixtures.md` | docs | n/a | `docs/how-to/` does not exist; follow the pass/fail-only rule of `docs/trials/phase-1-offline-tracer.md` |
| NJsonSchema validation helper and the schema parity reflection test | test | transform | No schema or reflection test precedent; RESEARCH Pattern 1 snippet |

## Metadata

**Analog search scope:** `src/Vdiff.Core`, `tests/Vdiff.Tests`, `.gitattributes`, the csproj files, `docs/`, `tools/`
**Files scanned:** about 55 tracked source and test files listed; 9 read in full or in the relevant range
**Pattern extraction date:** 2026-11-20
