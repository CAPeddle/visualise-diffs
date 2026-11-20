# Phase 2: Model Contract and Fixture Harness - Research

**Researched:** 2026-11-20
**Domain:** versioned JSON contracts (JSON Schema), canonical/deterministic serialisation in .NET 10, golden-file fixture harness with an expected-failure ratchet, Azure Pipelines CI for .NET + Rust on Microsoft-hosted agents
**Confidence:** MEDIUM overall. HIGH for in-repo facts and the probes run this session (validator behaviour, YAML library, `git diff --no-index`, CRLF behaviour). MEDIUM for CI facts, which come from official docs fetched this session; the `classify-confidence` seam rates `webfetch` as LOW even with `--verified`, so every CI fact is `[CITED]` and the first real pipeline run (not possible in this phase, D-09) is the real check.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

### Model and diff contract
- **D-01:** The source of truth for `vdiff-model/v1` is a committed JSON Schema file (suggested
  `docs/schema/vdiff-model-v1.schema.json`). Tests validate every model the tool writes against
  it. The C# records in `src/Vdiff.Core/Model/` stay in step with the schema, and a test enforces
  that (for example, by validating a model serialised from records that set every field). The
  schema covers types, members, bases, derived edges, spans and extraction metadata (R-10),
  including fields Phase 4 fills: per-file and per-language mode `semantic` / `syntax-only` /
  `scanner`, the fallback reason, unmodelled files and the managed-wrapper lane (R-08, R-09).
  — **Reversibility:** one-way — once Phases 4 to 7 build on the schema, a change needs a
  versioned migration of every extractor, the goldens and the private fixtures.
- **D-02:** `vdiff-diff/v1` is frozen in this phase too, with its own JSON Schema beside the
  model's. It covers everything Phase 5 must produce: states `added`, `removed`, `modified`,
  `renamed`, `unchanged`; member `body-changed`; edge diffs; rename details (the score and its
  components, per R-14); and the `package updated: name old → new` node (R-15). The hand-written
  goldens use this shape, and Phase 5 implements to it. — **Reversibility:** one-way — the five
  goldens and the private fixtures' expected outputs are written against it.
- **D-03:** The tracer's C++ path is migrated to emit `vdiff-model/v1` and `vdiff-diff/v1` in
  this phase. `vdiff-model/v0-tracer` and `vdiff-diff/v0-tracer` are deleted, with no
  compatibility shim. Fields the tracer can't fill yet stay empty or absent, as the schema allows,
  and C++ is labelled `syntax-only`. The existing Phase 1 tests are updated to the new shape, and
  the tracer's output validates against both schemas. That makes the tracer the first extractor
  that proves success criterion 1.
- **D-04:** The canonical model body must compare byte-for-byte, with no field-stripping logic
  spread across comparers. Volatile metadata (vdiff version, tree-sitter versions, timings) is
  kept apart from the canonical body. How it is kept apart is Claude's choice (see below).

### Fixture layout and goldens
- **D-05:** Each synthetic fixture lives at `tests/fixtures/<shape>/`, with `base/` and `head/`
  trees of plain source files and a `fixture.yml` (config: include globs, macros to blank and the
  other settings later read from `.visualise-diffs.yml`, plus the expected-pass phases from D-10).
  The harness builds a two-commit temporary git repository from the two trees, extending the
  `SyntheticRepo` test support (fixed author, committer and dates, so commit SHAs and every output
  are deterministic). A file moved between `base/` and `head/` gives a natural git rename pair.
  Fixture content is synthetic and public, with no target names (D15).
- **D-06:** The expected outputs are written by hand, spec-first, from each fixture's intent. They
  are the specification that later phases must meet. An opt-in accept mode, switched on by an
  environment variable, rewrites the goldens from tool output. It is for deliberate, reviewed
  updates only and never runs by default or in CI.
- **D-07:** In Phase 2 each fixture carries `expected/diff.json` and `expected/summary.mmd`
  (success criterion 2). The node-set golden is added in Phase 6, when the selector's output shape
  exists. The summary goldens use the provisional trailing-tag format from Phase 1 (01-08) and may
  be revised once spike S1 records the final format.
- **D-08:** The harness compares exact bytes, which matches R-23. On a mismatch it prints a
  unified diff of the expected and actual files, so a red fixture shows what is missing.

### CI and the expected-failure ratchet
- **D-09:** CI is Azure Pipelines: a committed `azure-pipelines.yml` that builds the .NET
  solution and the Rust sidecar and runs the tests and the harness. **Connecting it to an ADO
  organisation is not part of this phase.** The YAML is committed and the harness is run locally,
  so "runs in CI" is verified once it is connected. The YAML names only Microsoft-hosted images
  (for example `windows-latest`, `ubuntu-latest`) and never a target organisation, project, pool
  or ID (D15).
- **D-10:** "Each fixture reports failing, as intended" is implemented as an expected-failure
  ratchet. `fixture.yml` records, for each output (`diff`, `summary`, and later `nodes`), whether
  it is still pending or the phase in which it is due to pass. The harness passes when a pending
  output fails to match. It fails when a pending output unexpectedly matches, or when an output
  that is due fails. The pipeline stays green, and the harness report lists each fixture and
  output as RED (pending) or GREEN. Phases 5 to 7 flip the flags to turn fixtures green, and ACC-01
  is checked off when all five are green.
- **D-11:** The pipeline has two legs, Windows and Linux. The backlog 999.1 test-portability
  fixes are folded into this phase because they are exactly what blocks the Linux leg:
  (1) give the fsmonitor hook an execute bit when not on Windows (`File.SetUnixFileMode`) in
  `TraceRunScriptTests.A_core_fsmonitor_hook_is_never_run_by_the_script` and
  `RepoUnchangedMatrixTests.A_core_fsmonitor_hook_is_never_run_by_a_vdiff_run_or_by_the_snapshot`;
  (2) make `GitObjectReaderTests.SamePath` (`tests/Vdiff.Tests/GitObjectReaderTests.cs:38`)
  resolve `/var` to `/private/var` so it also holds on macOS. Phase 999.1 is closed when these
  land. There is no macOS leg.

### Private-fixture recipe
- **D-12:** The recipe is `docs/how-to/private-fixtures.md` plus a harness entry point (for
  example a `VDIFF_FIXTURES_DIR` environment variable or a script) that runs the same comparison
  code on a fixture directory outside the repository. Private and synthetic fixtures share the
  harness, so a private run tests exactly what CI tests.
- **D-13:** A private fixture uses the same `fixture.yml` and `expected/` layout, but in place of
  `base/` and `head/` trees it names a local clone path and the base and head SHAs. The harness
  reads git objects read-only, as the tracer does (Phase 1 D-02), and copies no target source.
  Private fixtures and their expected outputs are stored in the target organisation, never in
  this repository.
- **D-14:** A private run writes its output to an `--out` outside this repository. The existing
  repository-root protection (`VDIFF_PROTECTED_DIRS`, `src/Vdiff.Core/Cli/CliApp.cs:43`) refuses
  this repo as a destination. The only thing recorded back here is pass, fail or pending per
  shape (the five shape names) with a date: no SHAs, paths, type names or counts. This is the
  same rule as the TRC-03 trial record.

### Claude's Discretion
- How volatile metadata is kept apart (D-04): a separate `*.model.meta.json` file or a section
  that one shared canonicaliser removes. Either is acceptable, provided the canonical body
  compares byte-for-byte and no comparer needs its own stripping logic. A separate file was
  suggested as the simpler route.
- Where the commit SHAs sit (canonical or volatile). They are deterministic for synthetic
  fixtures (fixed dates and identity) but differ for every real PR.
- How the harness drives the tool: through `CliApp` end to end, or through `TracerPipeline` plus
  the writers. How a fixture's `config` maps to today's CLI flags (`--include`, `--blank`) until
  `.visualise-diffs.yml` exists in Phase 4.
- The JSON Schema validator library (it must be pinned, and work offline on both CI legs) and the
  schema's draft version.
- The exact content of each of the five fixtures, provided each is small, synthetic, and shows its
  shape clearly. The C# and cross-stack fixtures will produce no matching output until Phase 4,
  which the ratchet records as pending.
- Whether the pipeline also runs the local Mermaid syntax check (`tools/mermaid-check`, which needs
  Node and npm) on the summary goldens.

### Deferred Ideas (OUT OF SCOPE)
- Connecting `azure-pipelines.yml` to an ADO organisation (personal org with hosted agents was the
  suggested route). Do this when an org is ready; until then "runs in CI" is verified locally.
- A macOS CI leg.
- Node-set goldens (Phase 6) and the S1-driven revision of the summary goldens (Phase 7 / S1).
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| R-10 | Every extractor emits one versioned model `vdiff-model/v1` (types, members, bases, derived edges, spans, extraction metadata) | "Proposed v1 shapes" tables; NJsonSchema validator + keyword allow-list; records-to-schema parity test; tracer migration (D-03) |
| R-23 | Identical inputs produce byte-identical outputs; canonical ordering, volatile metadata kept apart | `Canonical` ordering table; `run.meta.json` split; seeded-shuffle test with a reflection coverage guard; same-trees/different-commits test |
| ACC-01 | Five synthetic public fixtures green in CI (red-by-design in this phase, via the ratchet) | Harness design (CliApp end to end), fixture.yml shape, ratchet truth table, five fixture sketches, goldens self-checks, `azure-pipelines.yml` skeleton |
| ACC-02 | Private-fixture recipe documented so five real PRs of the same shapes can be checked inside the target organisation (D15) | Private mode design (same code path, `VDIFF_FIXTURES_DIR`, out-of-repo `--out`, shape-name allow-list, no diff text on console), recipe outline |
</phase_requirements>

## Summary

The phase is mostly a contract-and-plumbing phase on top of a working Phase 1 tracer, and the biggest risks are not in the libraries but in the repository's own guards. Three existing facts shape the plan. First, `RepoHygieneTests.No_generated_output_or_build_directory_is_tracked` refuses any tracked file named `diff.json` or `summary.mmd` (and any tracked directory named `out`, `bin`, `obj` or `target`), so the very first golden file fails that test unless the guard and its theory rows change in the same commit. Second, the output set is hard-coded as "five files" in about ten test files plus the CLI, so splitting volatile metadata into its own file has a measurable blast radius that should be absorbed by one shared file-name constant. Third, `.gitattributes` has no rule for `.h`, `.cpp`, `.json`, `.mmd` or `.yml`; under this machine's `core.autocrlf=true` a Windows checkout of fixture sources would hand CRLF bytes to `SyntheticRepo.Write`, changing byte spans and commit SHAs between the two CI legs (probed below).

For the validator, the licence-clean choice that works offline on both legs is NJsonSchema 11.6.2 (MIT) with schemas written in the draft-07 keyword subset. The probe showed NJsonSchema silently ignores `const`, `if/then` and `dependentRequired` (an invalid document is accepted) and rejects a valid document when `oneOf` branches use `const`, so the schema must be restricted to an allow-listed keyword set, and a test must pin that allow-list and carry one negative document per keyword used. JsonSchema.Net 9.7.0 handled every probe correctly but ships under an "Open Source Maintenance Fee" EULA that applies to revenue-generating users; that is logged as a user decision, not settled here.

The harness should drive `CliApp.Run` end to end (it carries the `VDIFF_PROTECTED_DIRS` refusal that D-14 relies on, and it is what a private run must exercise), build the two-commit repository with `SyntheticRepo`, parse `fixture.yml` with YamlDotNet 18.1.0 in its default strict mode, render mismatches with `git diff --no-index` (no new dependency), and evaluate the ratchet with a pure function covering all four match/state combinations, where a tool failure, a missing output or a schema-invalid output always fails and is never "pending". Volatile metadata (commit ids, tool versions) goes into a single `run.meta.json`; this is also what makes hand-written goldens possible at all, because a golden cannot contain a commit SHA it cannot know.

**Primary recommendation:** Freeze `vdiff-model/v1`, `vdiff-diff/v1` and `vdiff-run-meta/v1` as draft-07-subset JSON Schemas in `docs/schema/`, validate with NJsonSchema 11.6.2 in the test project only, add one `Canonical` ordering module used by the builder, the differ and the serialiser, move commits and tool versions to `run.meta.json`, and build the harness as an xunit class in the existing `sidecar` collection that calls `CliApp.Run` and applies the four-case ratchet.

## Project Constraints (from CLAUDE.md)

Sources: `C:\Users\cpeddle\.claude\CLAUDE.md`, `C:\projects\CLAUDE.md`, `.claude/CLAUDE.md` (project), and the memory note.

- **Constraints (project CLAUDE.md):** C#/.NET 10 CLI `vdiff`, Roslyn in-process, every extractor emits `vdiff-model/v1` JSON (D14); C++ is tree-sitter syntax-only, no home-grown parser (D11); never block a PR, exit 0 on publish or skip, 1 on tool failure (R27); **privacy: no posting to ADO or external renderers with target content without explicit PO approval**; repository is public (D15), so no target org names, SHAs, paths or type names anywhere committed.
- **GSD workflow:** file changes go through a GSD command (`/gsd-execute-phase` for planned phase work). Research output is not committed by this agent.
- **Line endings (user CLAUDE.md):** committed content is LF; never leave a file with mixed endings; shell scripts and hooks must be LF on disk; do not add `* text=auto` to an existing repo casually. `.gitattributes` here has no catch-all on purpose; add narrow path rules only.
- **File discovery:** use Glob, not `find | sort` (Defender ASR false positive on `sort.exe`). Applies to executors too.
- **Subagent mandate:** build-and-verify runs, multi-file sweeps, log parsing over 100 lines and third-party doc lookups go to subagents. Plans should say so for the executor.
- **Memory note:** pushes and admin need the `CAPeddle` gh account; run `check-public-diff.sh` (target-name guard) before commits. Plans that commit should include that guard.
- **Serena / graphify:** not applicable here (no graph present, and this repo is outside Serena's scope).
- **Documented solutions:** none apply to this repo.

## Architectural Responsibility Map

There is no browser/SSR/CDN tier here; the tiers are the repository's own layers.

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Contract definition (JSON Schemas) | `docs/schema/*.schema.json` (data) | `src/Vdiff.Core/Model` records | Schema is the source of truth (D-01); records mirror it |
| Canonical ordering and serialisation | `Vdiff.Core` (`Canonical` + `OutputWriter`) | — | Must apply at the serialisation boundary so any input order gives the same bytes (success criterion 4) |
| Volatile metadata (commits, tool versions) | `Vdiff.Core` pipeline + writer (`run.meta.json`) | HtmlWriter (reads it) | Keeps the five canonical files free of per-run facts (D-04) |
| Schema validation | Test project (`Vdiff.Tests`) | — | Product never validates at runtime; keeps the validator out of the shipped tool |
| Fixture harness (build repo, run CLI, compare, ratchet, report) | Test project, `sidecar` collection | `SyntheticRepo` support | Runs the real `CliApp`; shares code between synthetic and private modes |
| Golden files and `fixture.yml` | `tests/fixtures/<shape>/` (data) | `.gitattributes` | Byte-exact LF content; guarded by hygiene tests |
| CI definition | `azure-pipelines.yml` (repo root) | — | Two hosted legs; no org/pool names (D15) |
| Private-fixture recipe | `docs/how-to/private-fixtures.md` | harness env vars | Same comparison code; results recorded as pass/fail/pending only (D-14) |
| 999.1 portability fixes | Test project | `FinalPath` (Core, reused) | Test-only changes; `SamePath` reuses `FinalPath.Resolve` |

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| xunit + Microsoft.NET.Test.Sdk + xunit.runner.visualstudio | 2.9.3 / 17.14.1 / 3.1.4 (already pinned) | Test framework for contract tests and the harness | Existing; `[Collection("sidecar")]` serial collection already owns the Rust sidecar build and process-wide env vars `[VERIFIED: tests/Vdiff.Tests/Vdiff.Tests.csproj:12-14]` |
| NJsonSchema | 11.6.2 (published 2026-11-11, MIT) | JSON Schema validation of tool output and goldens, test project only | MIT; works offline (probe below); validates draft-07 subset correctly when `const`/`if`/`dependentRequired` are avoided `[ASSUMED]` (NuGet package provenance, see audit) |
| YamlDotNet | 18.1.0 (published 2026-06-26, MIT, nuget "verified") | Parse `fixture.yml` into typed POCOs | Default deserialiser is strict about unknown keys (probe below), which catches typos in fixture config; Phase 4 will need a YAML reader for `.visualise-diffs.yml` anyway `[ASSUMED]` (provenance) |
| `git diff --no-index` | git 2.55 locally, hosted images 2.56 | Unified diff for D-08 mismatches | Git is already a hard dependency (minimum 2.45); no diff library needed (probe below) |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `Vdiff.Tests.Support.SyntheticRepo` | in repo | Build the two-commit fixture repo with fixed identity and dates | Every synthetic fixture run `[VERIFIED: tests/Vdiff.Tests/Support/SyntheticRepo.cs:15,93-101]` |
| `Vdiff.Tests.Support.EnvironmentScope` | in repo | Set/restore process env vars (`VDIFF_PROTECTED_DIRS`, accept mode) | Private mode and accept-mode tests; requires `[Collection("sidecar")]` `[VERIFIED: tests/Vdiff.Tests/Support/EnvironmentScope.cs:1-30]` |
| `MermaidLint` | in repo (`tests/Vdiff.Tests/Output/MermaidLint.cs`) | Structural check of golden `summary.mmd` | A golden self-check test (no Node needed) |
| `FinalPath.Resolve` | in repo (`src/Vdiff.Core/Cli/FinalPath.cs:59`) | Resolve `/var` to `/private/var` in `SamePath` | 999.1 fix (2) `[VERIFIED: grep result, FinalPath.cs:59 `public static string Resolve(string path)`]` |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| NJsonSchema | JsonSchema.Net 9.7.0 | Correct on every probe including `const`/`if`/`dependentRequired` and supports 2020-12, but its package licence is `OSMFEULA.txt` (a maintenance-fee agreement for revenue-generating users). See Assumptions Log A5; needs a user decision |
| NJsonSchema | Corvus.Json.Validator 5.8.12 | **Not probed.** Only the version list was read. Code-generation based and heavier; do not choose without running the same probe documents and reading its nuspec licence |
| NJsonSchema | Node `ajv` via `tools/` | Adds a Node dependency to the contract tests; mermaid-check already needs Node but is optional. Rejected: the contract test must run wherever `dotnet test` runs |
| NJsonSchema | A small hand-written checker over `JsonDocument` | Zero dependency but is exactly the "hand-roll" the Don't Hand-Roll rule forbids; the keyword subset is small but `$ref`, `pattern`, `uniqueItems` and error reporting add up |
| `git diff --no-index` | DiffPlex | New dependency for something git already does |
| YamlDotNet | Fixture config as JSON | D-05 names `fixture.yml`; keep YAML |
| draft 2020-12 | draft-07 | NJsonSchema's 2020-12 support is unverified (an upstream issue reports failures of the official 2020-12 suite, outcome unknown). Draft-07 subset is probed and sufficient |

**Installation (test project only; the shipped tool gets no new dependency):**
```bash
dotnet add tests/Vdiff.Tests/Vdiff.Tests.csproj package NJsonSchema --version 11.6.2
dotnet add tests/Vdiff.Tests/Vdiff.Tests.csproj package YamlDotNet --version 18.1.0
```
Pin exact versions in the csproj (`Version="11.6.2"`, not a range). NJsonSchema brings Newtonsoft.Json 13.0.3, which already sits in the test bin `[VERIFIED: tests\Vdiff.Tests\bin\Debug\net10.0\Newtonsoft.Json.dll exists]`.

**Version verification (run this session):**
```
NuGet flat-container index: NJsonSchema last = 11.6.2; YamlDotNet last = 18.1.0; JsonSchema.Net last = 9.7.0
NuGet registration: NJsonSchema 11.6.2 published 2026-11-11T15:55:02Z; YamlDotNet 18.1.0 published 2026-06-26T22:53:00Z
NuGet search: NJsonSchema 430,653,824 total downloads; YamlDotNet 633,895,275 (verified owner); JsonSchema.Net 64,911,660
```

## Package Legitimacy Audit

The legitimacy seam does not support NuGet. Exact output:

```
Error: Usage: gsd-tools package-legitimacy check --ecosystem <npm|pypi|crates> <pkg1> ...
```

So no NuGet package can earn `[VERIFIED: npm registry]`; all are `[ASSUMED]` by the package-provenance rule, and the planner must gate each install behind a `checkpoint:human-verify` task. The registry facts below were read from the NuGet API this session and are evidence for that human check, not a substitute for it.

| Package | Registry | Age (this version) | Downloads | Source Repo | Verdict | Disposition |
|---------|----------|--------------------|-----------|-------------|---------|-------------|
| NJsonSchema 11.6.2 | NuGet | published 2026-11-11 (9 days old; the project is long established) | 430,653,824 total | project URL njsonschema.org (nuspec `projectUrl`) | n/a (seam unsupported) `[ASSUMED]` | Approved subject to `checkpoint:human-verify` |
| YamlDotNet 18.1.0 | NuGet | published 2026-06-26 | 633,895,275 total | github.com/aaubry/YamlDotNet | n/a (seam unsupported) `[ASSUMED]` | Approved subject to `checkpoint:human-verify` |
| JsonSchema.Net 9.7.0 | NuGet | — | 64,911,660 total | github.com/json-everything/json-everything | n/a; **licence flag** (see A5) | NOT recommended pending a user decision |

**Packages removed due to [SLOP] verdict:** none (no verdicts available for NuGet).
**Packages flagged as suspicious [SUS]:** none from the seam. For information, the seam rated the already-pinned npm package `@mermaid-js/mermaid-cli` as `SUS` (npm, published 2026-11-02, 876,271 weekly downloads, `postinstall: null`); it is not new in this phase and the repo pins it in `tools/mermaid-check/package-lock.json` with `--ignore-scripts`.
**New Rust crates:** none. **New npm packages:** none.

## Architecture Patterns

### System Architecture Diagram

```
 tests/fixtures/<shape>/                      private: $VDIFF_FIXTURES_DIR/<shape>/
   fixture.yml (config + ratchet)               fixture.yml (+ repo, base, head)
   base/  head/  (LF source trees)              expected/  (diff.json, summary.mmd)
   expected/{diff.json,summary.mmd}                   |
        |                                             |
        v                                             v
 FixtureLoader (YamlDotNet, strict, size cap, shape-name + path checks)
        |
        +-- synthetic --> SyntheticRepo: commit base tree, replace with head tree, commit
        |                  (fixed identity/dates; bytes copied unchanged)
        +-- private ----> read-only clone path + base/head SHAs (nothing copied)
        v
 args = --repo --base --head --out [--include..] [--blank..]     (config -> flags)
        |
        v
 CliApp.Run  ------------------------------------------------------------+
   (VDIFF_PROTECTED_DIRS refusal, git read-only, sidecar, pipeline)       |  exit != 0  --> FAIL (never "pending")
        |                                                                  |
        v                                                                  |
 out/: base.model.json head.model.json diff.json summary.mmd report.html   |
       run.meta.json (volatile: commits, tool versions)                    |
        |                                                                  |
        v                                                                  |
 Schema gate: model files + diff.json validate (NJsonSchema)  --invalid--> FAIL
        |
        v
 Byte compare  expected/<output>  vs  out/<output>      (SequenceEqual)
        |
        v
 Ratchet.Evaluate(state: pending|green, matched) --> PASS/FAIL + RED/GREEN
        |                         |
        |                         +-- mismatch detail: git diff --no-index (synthetic: console;
        |                             private: file under the out dir only)
        v
 FixtureReport table (one row per fixture and output) --> fixture-report.md --> pipeline artifact
```

### Recommended Project Structure
```
azure-pipelines.yml                       # two-leg pipeline, hosted images only
.gitattributes                            # + tests/fixtures/** text eol=lf, docs/schema/*.json, azure-pipelines.yml
docs/schema/
  vdiff-model-v1.schema.json              # draft-07 keyword subset
  vdiff-diff-v1.schema.json
  vdiff-run-meta-v1.schema.json
docs/how-to/private-fixtures.md           # ACC-02 recipe
src/Vdiff.Core/Model/
  ModelTypes.cs                           # v1 records (v0-tracer deleted)
  Canonical.cs                            # one ordering module: comparers + Canonicalise(ModelFile/DiffFile)
  RunMeta.cs                              # volatile metadata record
src/Vdiff.Core/Output/OutputWriter.cs     # + FileNames constant, run.meta.json, Serialize canonicalises
tests/fixtures/
  small-cpp/ csharp/ cross-stack/ extract-class/ rename/
    fixture.yml  base/  head/  expected/{diff.json,summary.mmd}
tests/Vdiff.Tests/
  Contract/  SchemaValidationTests.cs  SchemaParityTests.cs  CanonicalSerialisationTests.cs
  Fixtures/  FixtureLoader.cs  FixtureHarness.cs  Ratchet.cs  FixtureReport.cs
             FixtureHarnessTests.cs  FixtureGoldenTests.cs  PrivateFixtureTests.cs
  Ci/        PipelineYamlTests.cs
```

### Pattern 1: Schema-first contract with a two-way parity test
**What:** The schema is authoritative. Every object schema sets `additionalProperties: false` and lists `required`. A reflection test maps each schema object definition to a record type and asserts the schema's `properties` keys equal the record's public properties converted with `JsonNamingPolicy.SnakeCaseLower.ConvertName`, in both directions. A second test serialises a "full-fat" instance (every optional field set, every list non-empty) and validates it.
**When to use:** For `ModelFile`, `DiffFile` and `RunMeta`. Together they catch a record gaining a field with no schema entry (full-fat instance fails `additionalProperties`) and a schema field with no record (parity fails), which the full-fat test alone would miss.
**Example:**
```csharp
// Source: pattern; JsonNamingPolicy is System.Text.Json (in-repo serialiser uses SnakeCaseLower, OutputWriter.cs:26-33)
static IEnumerable<string> RecordProps(Type t) =>
    t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
     .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() is null)
     .Select(p => JsonNamingPolicy.SnakeCaseLower.ConvertName(p.Name))
     .Order(StringComparer.Ordinal);
// compare with: schema.Definitions[name].Properties.Keys.Order(StringComparer.Ordinal)
```

### Pattern 2: One canonicaliser at the serialisation boundary
**What:** `Canonical.Canonicalise(ModelFile)` and `Canonical.Canonicalise(DiffFile)` return copies with every collection in a total ordinal order; `OutputWriter.Serialize(ModelFile|DiffFile)` calls it. `ModelBuilder` and `ModelDiffer` use the same comparers so there is one ordering definition, not three. Make every comparer total by ending with an ordinal compare of each element's serialised JSON, so two elements that tie on all keys are byte-identical anyway.
**Why:** `OutputWriter.Serialize` today is a plain generic serialiser `[VERIFIED: OutputWriter.cs:36 `public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions) + "\n";`]`; ordering lives only in `ModelBuilder`/`ModelDiffer`, so success criterion 4 (collections supplied in any order) is false today.
**Ordering table (proposed canonical keys):**

| Collection | Order |
|-----------|-------|
| `extraction.languages` | language |
| `extraction.languages[].macros_blanked` | ordinal (set) |
| `files` | path |
| `unmodelled` | path, reason |
| `types` | language, id, file, span.start_byte |
| `types[].bases` | ordinal (set; see Pitfall 7) |
| `types[].modifiers` | ordinal (set) |
| `types[].members` | key, span.start_byte, span.end_byte |
| `edges` | kind, from, to, to_name |
| `duplicates` | ordinal |
| diff `types` | language, id |
| diff `types[].members` | key |
| diff `types[].bases.added/removed` | ordinal |
| diff `edges` | kind, from, to, to_name |
| diff `packages` | name |
| diff `warnings` | ordinal (changes today's errors-first order; see Pitfall 8) |

### Pattern 3: Volatile metadata in one separate file
**What:** A single `run.meta.json` (`vdiff-run-meta/v1`) holds what varies without the content varying: base and head commit ids, the tool version and the extractor tool versions (tree-sitter, tree-sitter-cpp today). The five canonical files carry none of it. Recommended over per-side `*.model.meta.json` because the tree-sitter versions are per-run, not per-side, and one file means one added output name instead of two.
**Why commit ids are volatile:** a hand-written golden cannot contain a SHA it cannot know. The synthetic SHA depends on tree bytes, parent, identity, dates and message; one change to `SyntheticRepo` would invalidate every golden. Identical trees committed with a different message also give different SHAs, so "identical inputs give identical output" (R-23) implies commits are not part of the canonical body. This is also why the diff must not carry `base`/`head` commit ids (they are in `run.meta.json`).
**Evidence of the problem in the current code:** `DiffFile` has `Base`/`Head` strings `[VERIFIED: ModelTypes.cs:81-87 `public sealed record DiffFile(string Schema, string Base, string Head, DiffSummary Summary, IReadOnlyList<string> Warnings, IReadOnlyList<TypeDiff> Types)`]`; `ModelFile` has `Commit` `[VERIFIED: ModelTypes.cs:33-39]`; `ExtractionInfo` has `TreeSitter`/`TreeSitterCpp` `[VERIFIED: ModelTypes.cs:22-27]`.

### Pattern 4: Harness drives `CliApp.Run` end to end
**What:** The harness calls `CliApp.Run(args, stdout, stderr)` and reads the files from `--out`.
**Why not `TracerPipeline` + writers:** the `VDIFF_PROTECTED_DIRS` refusal (D-14) lives in `CliApp.Run` `[VERIFIED: CliApp.cs:43 `public const string ProtectedDirectoriesVariable = "VDIFF_PROTECTED_DIRS";` and CliApp.cs:75 `ParseProtectedDirectories(...)`]`, so driving the pipeline directly would bypass it for private runs. End to end also compares the bytes the CLI really writes (including `OutputWriter`'s LF/UTF-8 handling) and exercises the same code a private run uses. Cost: the class must be in `[Collection("sidecar")]` (it reaches the real sidecar), which serialises it.
**Config mapping today:** `include: [globs]` becomes repeated `--include` (at most 64, each 1 to 512 chars `[VERIFIED: CliOptions.cs:22-23 `MaxIncludeGlobs = 64`, `MaxGlobLength = 512`]`); `blank: [NAMES]` becomes repeated `--blank` (ASCII identifiers, at most 256, each at most 128 `[VERIFIED: CliOptions.cs:20-21]`). Strict `fixture.yml` `config` keys for now are exactly `include` and `blank`; Phase 4 adds keys with the `.visualise-diffs.yml` reader. Rejecting unknown keys now prevents a typo like `blnak:` from silently running a fixture with no blanking.

### Pattern 5: The ratchet is a pure function with a four-case truth table
```
state    matched   outcome         report
pending  false     PASS            RED (pending, due phase N)
pending  true      FAIL            "unexpectedly matches: flip to green in fixture.yml"
green    true      PASS            GREEN
green    false     FAIL            unified diff
(any)    tool exit != 0 | output file missing | output fails its schema | golden missing  ->  FAIL
```
The last row is the vacuity guard: without it a crashing tool makes every fixture "pending and mismatching", i.e. passing. D-10 says "pending or the phase in which it is due"; this research reads that as `state: pending|green` plus `due: <phase number>` kept as documentation shown in the report, so the harness needs no "current phase" input (Assumptions Log A4; confirm with the user if "due" was meant to be evaluated against a phase counter).

`fixture.yml` (synthetic) shape:
```yaml
shape: small-cpp                  # must equal the directory name; one of the five names
description: one sentence, synthetic
config:
  include: ["include/**"]         # optional
  blank: [EXPORT_API]             # optional
expected:
  diff:    { state: pending, due: 5 }
  summary: { state: pending, due: 7 }
```
Private `fixture.yml` replaces trees with `repo: <local clone path>`, `base: <sha>`, `head: <sha>` (D-13), and the same `config` and `expected`.

### Pattern 6: Build the fixture repository by replacing the tree
Commit every file of `base/`; then delete tracked files absent from `head/` (`repo.Delete`), write every file of `head/` (`repo.Write(relative, bytes)`), commit. A moved file is a delete plus an add; git sees a rename pair by content similarity, though the tracer lists changes with `--no-renames` `[VERIFIED: GitObjectReader.cs:113]` so Phase 3 is where renames matter. Copy bytes verbatim (`File.ReadAllBytes`); never `ReadAllText`.

### Pattern 7: Private mode is the same code with three extra restrictions
- Entry point: `VDIFF_FIXTURES_DIR` (fixtures root) and `VDIFF_PRIVATE_OUT` (a directory outside this repository); the harness sets `VDIFF_PROTECTED_DIRS` to this repository's root via `EnvironmentScope` so `CliApp` refuses an `--out` inside it (D-14).
- A private fixture directory name must be one of the five shape names, or the harness refuses to run it; otherwise a directory named after a target identifier would be printed.
- Console output is only `<shape>: PASS|FAIL|PENDING <yyyy-MM-dd>`. Assertion messages in private mode are generic and name no path, SHA, type or count; the unified diff is written to a file under `VDIFF_PRIVATE_OUT` and only its existence is mentioned. (Synthetic mode may print the diff, D-08.)
- With `VDIFF_FIXTURES_DIR` unset the private test returns early by design (precedent: `01-UAT-10-linux.md` documents tests that return early by design and report Passed). Do not use `Skip`.
- No wrapper script in Phase 2: `RepoHygieneTests` scans `scripts/` for remote verbs and `trace-run.ps1` carries two-host parity tests. Document the invocation instead: set the two variables, then `dotnet test --filter "Category=PrivateFixtures"`.

### Anti-Patterns to Avoid
- **Stripping fields in comparers:** D-04 forbids it. The separate `run.meta.json` is the structural fix.
- **`const`, `if/then`/`else`, `dependentRequired`, `unevaluated*` in schemas:** NJsonSchema ignores or mishandles them (probe). Use single-value `enum` instead of `const`.
- **Floating-point values in goldens:** see Pitfall 5.
- **`ReadAllText` on fixtures or goldens:** hides BOM and CRLF differences; compare bytes.
- **A pending state that tolerates tool failure:** see the ratchet truth table.
- **`Skip` on the private test:** use early return, as the repo already does.

## Proposed v1 Shapes (traceable to R8 to R15)

This is the recommended shape, not a locked one (Claude's Discretion). Names are snake_case because the serialiser uses `SnakeCaseLower` with declaration order `[VERIFIED: OutputWriter.cs:26-33]`. Today's v0 records are in `src/Vdiff.Core/Model/ModelTypes.cs` and v1 should keep v0 field names where they survive, to limit churn in Phase 1 tests.

**Evolution policy (put in `docs/schema/README` or the schema `description`):** adding an enum value or an optional property is a backwards-compatible relaxation and may happen inside `v1` (old documents still validate); renames, removals and tightening need `v2` and a migration. This is what keeps the one-way decision (D-01, D-02) cheap, and it lets the schemas use closed enums for states, modes, access and kinds.

**Model (`vdiff-model/v1`)**

| Field | Shape | Requirement |
|-------|-------|-------------|
| `schema` | `enum ["vdiff-model/v1"]` (not `const`) | R10 |
| `extraction.languages[]` | `{language: cpp\|csharp\|cpp-cli, extractor: string, mode: semantic\|syntax-only\|scanner, fallback_reason?: string, unresolved_symbols?: int>=0, macros_blanked: string[]}` | R9 per-language mode and reason; R7 unresolved count; R5 macros |
| `files[]` | `{path, language, mode, fallback_reason?, error_nodes: int>=0}` | R9 per-file mode |
| `unmodelled[]` | `{path, reason}`; `reason` matches `^[a-z][a-z0-9-]*$` (open set, not an enum) | R9 "list files that could not be modelled" |
| `types[]` | `{id, fqn, kind: class\|struct\|enum\|interface\|union\|record\|delegate, name, scope, language, file, modifiers: string[], bases: string[], span, members[]}` | R10; R8 managed-wrapper lane is `language: cpp-cli` (Open Question O1); R14 same-language rule needs `language` |
| `types[].members[]` | `{key, name, kind: method\|field\|alias\|enumerator\|property\|event\|constructor, access: public\|protected\|private\|internal\|protected-internal\|private-protected\|none, modifiers: string[], signature, span}` | R13 needs static/virtual/override and visibility to detect `modified` |
| `edges[]` | `{kind: base\|association, from: type id, to?: type id, to_name: string, via?: member key}`; `to` present only when the target resolves uniquely | R10 "derived edges"; R18 neighbours are bases, derived types and association targets (Open Question O2) |
| `duplicates` | `string[]`, always written | keeps the 01-07 behaviour |
| `span` | `{start_byte, end_byte, start_line, end_line}` all int>=0 | unchanged from v0 |

Values the tracer already produces, read this session: unmodelled reasons `"object-missing"`, `"not-a-blob"`, `"too-large"`, `"total-too-large"` `[VERIFIED: src/Vdiff.Core/Git/GitObjectReader.cs:306,317,321,326]`, `"non-utf8-path"` `[VERIFIED: GitObjectReader.cs:232 `new UnmodelledFile(Encoding.UTF8.GetString(pathBytes), "non-utf8-path")`]`, `"parse-timeout"` and `"parse-error"` `[VERIFIED: ModelBuilder.cs:14-15 `public const string ReasonParseTimeout = "parse-timeout"; public const string ReasonParseError = "parse-error";`]`; type kinds `class`, `struct`, `enum` `[VERIFIED: extractors/cpp-ts/src/protocol.rs:155 `/// "class", "struct" or "enum".`]`; member kinds `method`, `field`, `alias`, `enumerator` and access `public`, `protected`, `private`, `none` `[VERIFIED: protocol.rs:171-173 `/// "method", "field", "alias" or "enumerator".` and `/// "public", "protected" or "private"; "none" for an enumerator.`]`. The extra kinds, access values and `modifiers` are additions for Phase 4 and later; the tracer emits empty arrays for them.

**Run metadata (`vdiff-run-meta/v1`):** `{schema, vdiff_version, base_commit, head_commit, extractors: [{name, versions: {<tool>: <version>}}]}`. Volatile by definition; may later hold timings. Excluded from the canonical byte-equality checks but included in the local-identity leak check.

**Diff (`vdiff-diff/v1`)**

| Field | Shape | Requirement |
|-------|-------|-------------|
| `schema` | `enum ["vdiff-diff/v1"]` | R10 |
| `summary` | `{types: {added, removed, modified, renamed, unchanged}, members: {added, removed, modified, renamed, body_changed}, edges: {added, removed, modified, renamed, unchanged}, unmodelled: {base, head}}` (ints) | R13 |
| `warnings` | `string[]` | existing |
| `packages[]` | `{name, state: updated\|added\|removed, old_version?, new_version?}` | R15 "package updated: name old → new" (the arrow is a rendering, not stored) |
| `types[]` | `{id, fqn, kind, language, state: added\|removed\|modified\|renamed\|unchanged, base_file?, head_file?, renamed_from?: {id, fqn}, rename?: {score_milli: int 0..1000, member_jaccard: {shared: int, total: int}, same_file_or_rename_pair: bool, same_namespace: bool, same_bases: bool}, bases: {added[], removed[]}, members[]}` | R13, R14 |
| `types[].members[]` | `{key, name, state: added\|removed\|modified\|renamed\|unchanged\|body-changed, access_before?, access_after?, signature_before?, signature_after?, modifiers_before?, modifiers_after?, renamed_from_key?}` | R13 `body-changed` |
| `edges[]` | `{kind, from, to?, to_name, state}` listing changed edges only (unchanged edges are counted in `summary`, matching how unchanged types are counted not listed today `[VERIFIED: ModelDiffer.cs:6-18 doc comment "Unchanged types are counted, not listed"]`) | R13 edge states |

`unchanged` is in the state enums for symmetry with D-02 but never listed in v1; the writers already filter it (`MermaidWriter.cs:27`).

**The rename score as integers:** R14 defines `Score = 0.5 x Jaccard(member names) + 0.2 x (same file or git rename pair) + 0.2 x (same namespace) + 0.1 x (same base set)` with a 0.6 threshold `[VERIFIED: docs/requirements/requirements.md:99-100]`. Store `member_jaccard` as shared/total counts and `score_milli` as an integer (0 to 1000, documented rounding), threshold 600. A hand-written golden with `0.8` and a computed `0.7999999999999999` would never match; integers make the goldens writable by hand and exact.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| JSON Schema validation | A keyword interpreter over `JsonDocument` | NJsonSchema 11.6.2 (test project) | `$ref`, `pattern`, `uniqueItems`, error paths; the keyword allow-list test covers its gaps |
| Unified diff of expected vs actual | A diff algorithm | `git diff --no-index --no-color` run through `BoundedProcess` with the isolated git environment | Verified output below; git is already required |
| YAML parsing | A line parser for `fixture.yml` | YamlDotNet strict POCO deserialisation | Quoting, anchors, unknown-key rejection |
| Deterministic commit ids | Computing SHAs for goldens | Keep commit ids out of the canonical body (`run.meta.json`) | Goldens cannot know SHAs |
| Test-result publishing | Custom report upload | `PublishTestResults@2` for trx plus a `PublishPipelineArtifact@1` for the fixture report `[ASSUMED]` task input names | Standard tasks |
| Path resolution through links | A new realpath helper | Existing `FinalPath.Resolve` | 999.1 fix (2) reuses code that already follows links with a budget |
| Random permutations in tests | A property-testing package | A seeded Fisher-Yates over each list via reflection | Fixed seed keeps the test deterministic; no new dependency |

**Key insight:** the contract is small; what goes wrong is silent vacuity (a validator that accepts anything, a ratchet that treats crashes as pending, a golden that cannot be byte-exact). Every pattern above has a negative test for that reason.

## Runtime State Inventory

D-03 deletes `v0-tracer` and migrates the tracer to v1, so this is a migration phase. All five categories, answered explicitly:

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None. Output is written to a user-chosen `--out` per run; nothing in this repo stores model or diff JSON (the hygiene tests forbid tracked generated output, `RepoHygieneTests.cs:25-26`). Verified by grep: `base.model.json`/`diff.json` appear only in code and tests. | None. Old `--out` directories on developer machines hold `v0-tracer` files; they are disposable. |
| Live service config | None. No ADO organisation is connected (D-09); no external service holds the schema name. | None. |
| OS-registered state | None. No scheduled task, service or pm2 entry references the output names or schema strings. | None. |
| Secrets/env vars | `VDIFF_PROTECTED_DIRS`, `VDIFF_CPP_TS`, `VDIFF_SIDECAR_TIMEOUT_SECONDS`, `VDIFF_PARSE_BUDGET_MS` keep their names. New: `VDIFF_FIXTURES_DIR`, `VDIFF_PRIVATE_OUT`, accept-mode switch, optional `VDIFF_FIXTURE_REPORT`. No secret involved. | Code edit only (new names), documented in the recipe. |
| Build artifacts / installed packages | `tests/**/bin`, `obj` and `extractors/cpp-ts/target/` exist locally (gitignored). They hold compiled v0 code but rebuild from source; `SidecarFixture` always runs `cargo build --release --locked`. | None. |

**Sidecar protocol strings stay at `v0`:** `CppSidecar.RequestSchema = "vdiff-cpp-ts-request/v0"` and `ResponseSchema = "vdiff-cpp-ts/v0"` `[VERIFIED: CppSidecar.cs:98-99]`. The model contract changes, the Rust-to-C# wire protocol does not, so no Rust change is needed. `ModelBuilder` maps the sidecar answer into v1.

## Common Pitfalls

### Pitfall 1: The hygiene guard rejects the goldens
**What goes wrong:** `TrackedPathProblems` flags any tracked path whose last segment is `base.model.json`, `head.model.json`, `diff.json`, `summary.mmd` or `report.html`, and any directory segment `out`, `bin`, `obj`, `target`, `node_modules` `[VERIFIED: RepoHygieneTests.cs:25-28,109-124]`. `tests/fixtures/small-cpp/expected/diff.json` fails `No_generated_output_or_build_directory_is_tracked`. The failure message even says "a later phase that adds golden files with these names must change this guard in the same commit" (line 104).
**How to avoid:** change `TrackedPathProblems` in the same commit as the first golden: allow exactly `tests/fixtures/<shape>/expected/diff.json` and `tests/fixtures/<shape>/expected/summary.mmd`. Update both theories: add the allowed paths to `The_tracked_file_guard_accepts_ordinary_sources`; keep `tests/fixtures/base.model.json` (line 259) flagged and add flagged rows for `tests/fixtures/small-cpp/head/diff.json`, `tests/fixtures/small-cpp/expected/report.html` and `tests/fixtures/small-cpp/expected/head.model.json`. Fixture trees must not contain directories named `bin`, `obj`, `target` or `out`, which `.gitignore` also ignores anywhere (`bin/`, `obj/`, `target/`).
**Warning signs:** `RepoHygieneTests` red right after adding fixtures.

### Pitfall 2: The synthetic-only guard scans the new harness
**What goes wrong:** `Test_and_script_sources_cannot_reach_a_remote` scans every `tests/**/*.cs` (except itself) and `scripts/*` for a remote verb as the first argument of a git call (`fetch`, `pull`, `push`, `remote`, `ls-remote`, `submodule`) and for `git@` or `ssh://` `[VERIFIED: RepoHygieneTests.cs:131-139,172-202]`.
**How to avoid:** harness code that calls git uses `diff`, `rev-parse`, `ls-files` and similar only; comments are exempt from the verb check but not from the address check. Do not write a private-fixture example containing `git@`.

### Pitfall 3: CRLF in fixture inputs silently changes spans and commit SHAs
**What goes wrong:** `.gitattributes` has rules only for `*.sh *.cs *.rs *.ps1 *.mjs Cargo.lock package-lock.json` `[VERIFIED: .gitattributes]`. Probe, with `core.autocrlf=true` (this machine's global setting, `file:C:/Users/cpeddle/AppData/Local/Programs/Git/etc/gitconfig	true`): a file with an `eol=lf` rule checked out with CR count 0; the same content without a rule checked out with CR count 2. A fixture `.h` or `.cpp` checked out as CRLF is copied by `SyntheticRepo.Write` with its CRs, so blob bytes, byte spans and commit SHAs differ from the Linux leg.
**How to avoid:** add `tests/fixtures/** text eol=lf` (covers `.h`, `.cpp`, `.cs`, `.yml`, `.json`, `.mmd`), plus `docs/schema/*.json text eol=lf` and `azure-pipelines.yml text eol=lf`. Add a hygiene test: no fixture file contains CR or starts with a BOM. Do not add `* text=auto`.
**Warning signs:** goldens that pass on one OS and fail on the other; byte offsets off by the line count.

### Pitfall 4: NJsonSchema silently ignores keywords
**What goes wrong:** probe results (NJsonSchema 11.6.2, each keyword alone, one valid and one invalid document):
```
const-only:        valid-doc=ok   invalid-doc=ACCEPTED(vacuous)
enum-only:         valid-doc=ok   invalid-doc=rejected
const-2020:        valid-doc=ok   invalid-doc=ACCEPTED(vacuous)
oneOf-const:       valid-doc=REJ  invalid-doc=rejected
if-then:           valid-doc=ok   invalid-doc=ACCEPTED(vacuous)
dependentRequired: valid-doc=ok   invalid-doc=ACCEPTED(vacuous)
uniqueItems:       valid-doc=ok   invalid-doc=rejected
not:               valid-doc=ok   invalid-doc=rejected
```
An earlier 8-document run on a schema using `type`, `required`, `additionalProperties:false`, `enum`, `pattern`, `minimum`, `uniqueItems`, `$ref` to `#/definitions/..` and `oneOf` of single-value `enum`s gave the expected accept/reject on all 8 documents with both NJsonSchema and JsonSchema.Net.
**How to avoid:** allow-list keywords for the schemas: `$schema`, `title`, `description`, `type`, `properties`, `required`, `additionalProperties`, `items`, `enum`, `pattern`, `minimum`, `maximum`, `minLength`, `uniqueItems`, `definitions`, `$ref` (local `#/definitions/...` only), `oneOf`/`anyOf`/`not` (branches built from `enum`/`type`/`$ref`, never `const`). A test walks each schema file and fails on any keyword outside the list. For every keyword used, keep at least one negative document that must be rejected (a validator that accepts everything passes every positive test).

### Pitfall 5: Floating point in goldens
**What goes wrong:** a computed double score serialises as the shortest round-trip string and may differ from the hand-written decimal. **How to avoid:** integers (`score_milli`, Jaccard as shared/total), see the v1 diff table.

### Pitfall 6: "Five files" is asserted in about ten places
**What goes wrong:** adding `run.meta.json` breaks exact-listing assertions. Blast radius (line numbers from the grep this session):
- Code: `OutputWriter.cs:15-19` (name constants), `OutputWriter.cs:53-61` (`WriteAll`), `CliApp.cs:108` (call) and `CliApp.cs:110` (`"vdiff: wrote 5 files to {outPath}\n"`), `CliOptions.cs:36` (usage text names the five files), `HeldDirectory.cs:8` (doc comment), `HtmlWriter.cs:36` (prints `diff.Base`/`diff.Head`, which move to the metadata).
- Script: `scripts/trace-run.ps1:626` lists the five names but only checks that each exists (`Test-Path` loop at 628-630), so extra files do not break it; the log assertion strings do (below).
- Tests with exact-set lists: `DeterminismTests.cs:20` and `:88` (asserts the directory listing equals the list), `TracerEndToEndTests.cs:187`, `Trial/TraceRunScriptTests.cs:213` (`FiveFiles`), `Safety/AliasedOutTests.cs:20` and `:264`, `Scope/IncludeGlobEndToEndTests.cs:18`, `Safety/HostileInputTests.cs:359`, `Safety/RepoUnchangedMatrixTests.cs:17`.
- Tests asserting the literal `wrote 5 files`: `TraceRunScriptTests.cs:491,527,1040` (the script copies the CLI line into its log).
- Tests that assert the commit on a model: `TracerEndToEndTests.cs:153-154` (`result.Base.Commit`, `result.Head.Commit`).
- Tests that deserialise models with v0 records: `BlankingEndToEndTests.cs:88,106,114,138,154,184,189`, `RichFixtureEndToEndTests.cs:135`, `SidecarBoundsTests.cs:476,501`, `HostileInputTests.cs:391,427,446`, `IncludeGlobEndToEndTests.cs` lines 75-122 and 162-163, plus `ModelBuilderTests`/`ModelDifferTests`.
**How to avoid:** introduce `OutputWriter.FileNames` (canonical files) and `OutputWriter.MetaFile` once, and make every test above read them; derive the message count from the array length. Keep the message text pattern `wrote N files` and update the three script-log assertions. In the determinism hash test, hash the five canonical files for cross-run equality and keep `run.meta.json` in the local-identity leak test (`DeterminismTests.cs:191-228`) and the BOM/LF/trailing-LF test (`:231-247`). Add the separation proof: commit the same trees twice with different commit messages (different SHAs), run both, and assert the five canonical files are byte-identical while `run.meta.json` differs only in the commit ids.

### Pitfall 7: Base order is meaningful in C++ but a set in the differ
**What goes wrong:** `ModelBuilder` keeps `[.. type.Bases]` in source order `[VERIFIED: ModelBuilder.cs:62]` while `ModelDiffer` treats bases as sets (`Sorted(...)`, `ModelDiffer.cs:84-85`). Success criterion 4 demands order independence, so the canonical model must sort `bases`. Decision: treat bases as a sorted ordinal set (the class diagram does not use their order). Record it in the schema description; update any Phase 1 test that asserts base order.

### Pitfall 8: Canonical `warnings` order differs from today's
**What goes wrong:** the differ emits warnings per side in a fixed order (parse errors, not modelled, duplicates) `[VERIFIED: ModelDiffer.cs:126-147 doc comment]`; a global ordinal sort reorders them. Update `ModelDifferTests` and `HtmlWriterTests` expectations, or make warnings structured; ordinal sort is the cheaper choice.

### Pitfall 9: Unique ids across languages
**What goes wrong:** an id is `kind + " " + fqn` `[VERIFIED: ModelBuilder.cs:60 `var id = Unique(seenTypeIds, type.Kind + " " + fqn);`]`. A C++ `class Foo` and a C# `class Foo` at global scope collide in one model file. This is one-way once frozen. Recommendation: type identity is `(language, id)` in the model and the diff, ids keep their v0 form (so the Phase 1 tests and the Mermaid class ids stay stable), and `Unique` is applied per language. See Open Question O3.

### Pitfall 10: Ratchet vacuity in the first phase
**What goes wrong:** no real fixture is GREEN in Phase 2, so the "matches" branch is never exercised by real data, and a harness bug in that branch would stay hidden until Phase 5.
**How to avoid:** a self-test using a temporary fixture that really is green today: `base == head` (no change), expected `diff.json` is the empty-diff v1 document and expected `summary.mmd` is the three-`classDef` empty diagram (the existing test pins that shape: `DeterminismTests.cs:137` `Assert.StartsWith("classDiagram\n  classDef added ", mermaid)`). Run all four truth-table cases through `Ratchet.Evaluate` as unit rows, and run the green case end to end. Also make every real fixture contain at least one feature the tracer cannot produce, so "pending" is honest and a pending output cannot accidentally match: a `body-changed` member (small-cpp and extract-class), a rename (rename), an edge diff (extract-class), a package node and a managed-wrapper type (cross-stack). The small-cpp fixture needs this most, because its added/removed/modified content is exactly what the tracer already emits.

### Pitfall 11: Hand-written goldens can be subtly non-canonical
**How to avoid:** a golden self-check test for every fixture: (a) `expected/diff.json` validates against the diff schema; (b) it round-trips: deserialise into `DiffFile`, `OutputWriter.Serialize`, and the bytes equal the file (proves ordering, indentation, key order, trailing LF); (c) `expected/summary.mmd` passes `MermaidLint.Check` and has no CR, no BOM, ends with LF. These run now, in Phase 2, so a bad golden is a red test immediately rather than a confusing mismatch in Phase 5.

### Pitfall 12: The `MermaidCli` tests fail instead of skipping
**What goes wrong:** `MermaidCliSyntaxTests` fail with "A prerequisite of the Mermaid check is missing" when mermaid-cli or a browser is absent `[VERIFIED: tests/Vdiff.Tests/Output/MermaidCliSyntaxTests.cs:5-8 and :96-101; 01-UAT-10-linux.md shows 6 failures for this reason]`. A CI leg that does not install them goes red.
**How to avoid:** the main test step runs `--filter "Category!=MermaidCli"` (the trait is `[Trait("Category", "MermaidCli")]` `[VERIFIED: MermaidCliSyntaxTests.cs:17]`). A separate Linux-only step runs `npm --prefix tools/mermaid-check ci --ignore-scripts`, then `--filter "Category=MermaidCli"` with `VDIFF_MERMAID_NO_SANDBOX=1` (the check script supports that variable `[VERIFIED: tools/mermaid-check/check.mjs:18]`) and a system Chrome (present on the hosted images, per the image readmes). Because it cannot be exercised before an organisation is connected, mark that step `continueOnError: true` initially and flip it after the first real run.

### Pitfall 15: Hosted image labels are moving
**What goes wrong:** per the hosted-agents page, `windows-latest` is about to point at `windows-2025-vs2026`, and `windows-2025` will also begin using Visual Studio 2026; a pipeline that assumed VS2022 tooling could break on the first real run. The VS2026 readme lists Rust 1.99.1, the VC.Tools component, Node 22.23.4 and Chrome 156, so the pipeline should still work.
**How to avoid:** the skeleton pins `windows-2025` and `ubuntu-24.04` (D-09 allows any Microsoft-hosted label; "for example" `windows-latest`). Print `dotnet --info`, `cargo --version` and `git --version` as the first step so a drift shows up in the log, and re-read the image readmes when an organisation is connected.

### Pitfall 13: Accept mode must never run in CI
**How to avoid:** accept mode refuses to run when `TF_BUILD` is set (Azure Pipelines sets it `[ASSUMED]`) or when the switch is combined with anything but an explicit single shape; it writes only under the fixture's `expected/`. Add a unit test for the refusal. Reviewed diff afterwards is the human step (D-06).

### Pitfall 14: Private-mode leaks through test output
**What goes wrong:** an xunit assertion message or the unified diff would print target type names to a console or log. **How to avoid:** Pattern 7 restrictions, plus a test that runs private mode on a synthetic stand-in (a synthetic repo outside the checkout with a private-style `fixture.yml`) and asserts the captured console text contains no SHA, no path and no digit-run longer than the date.

## Code Examples

### Strict `fixture.yml` loading
```csharp
// Source: probe run this session (YamlDotNet 18.1.0). Unknown key 'bogus' threw:
//   YamlException: Property 'bogus' not found on type 'Fx'.
var yaml = new DeserializerBuilder()
    .WithNamingConvention(UnderscoredNamingConvention.Instance)
    .Build();                                  // strict by default; do NOT call IgnoreUnmatchedProperties()
var fixture = yaml.Deserialize<FixtureConfig>(text);   // cap the file size first (64 KiB)
```

### Unified diff via git (verified output)
```
$ git diff --no-index --no-color --no-ext-diff --no-textconv -- expected.json actual.json   (exit 1 on difference, 0 identical)
diff --git a/expected.json b/actual.json
index 8d6b85c..43cff6b 100644
--- a/expected.json
+++ b/actual.json
@@ -1,3 +1,3 @@
 {
-  "a": 1
+  "a": 2
 }
```
Run it through `BoundedProcess` with `GitCommand.BuildEnvironment()`, `GIT_CONFIG_NOSYSTEM=1` and `GIT_CONFIG_GLOBAL` pointing at an empty file (as `SyntheticRepo` does, `SyntheticRepo.cs:33-38`), so a user's diff or colour config cannot change the bytes. Copy the two files to a temp directory under fixed names first so absolute paths do not appear in the header. Cap the printed text (for example 200 lines).

### NJsonSchema validation (probe pattern)
```csharp
// Source: probe run this session, NJsonSchema 11.6.2, offline-safe (dead-proxy run passed)
var schema = await JsonSchema.FromJsonAsync(schemaText);
IReadOnlyCollection<ValidationError> errors = schema.Validate(jsonText);   // empty means valid
```
Offline check: with `HTTP_PROXY`, `HTTPS_PROXY` and `ALL_PROXY` pointing at `http://127.0.0.1:9` the probe still validated correctly, so no network call is made for schemas whose `$ref`s are local. Keep every `$ref` local (`#/definitions/...`) and omit `$id` URLs.

### The 999.1 fixes
```csharp
// Fix (1) in both fsmonitor tests, right after File.WriteAllBytes(hook, ...):
if (!OperatingSystem.IsWindows())
{
    File.SetUnixFileMode(hook, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
}
// Source: root cause in 01-UAT-10-linux.md: "writes its hook with File.WriteAllBytes and never sets the execute bit.
// Git cannot run a non-executable core.fsmonitor hook on Unix, so the positive control fails"

// Fix (2) GitObjectReaderTests.cs:38. Currently:
//   static string SamePath(string path) => Path.GetFullPath(path).TrimEnd('\\', '/');
// Proposed (needs `using Vdiff.Core.Cli;`), applied to both sides of every comparison:
static string SamePath(string path) => FinalPath.Resolve(path).TrimEnd('\\', '/');
```
The `/private/var` fix has no CI leg (D-11, no macOS), so it is verifiable only on a Mac: mark it manual-only in the validation map. On Windows and Linux it must remain a no-op.

### `azure-pipelines.yml` skeleton (no organisation, project, pool or ID)
```yaml
# Microsoft-hosted images only (D-09, D15). Source: Azure Pipelines hosted-agent and task docs, fetched 2026-11-20.
trigger:
  branches:
    include: [main]
pr:
  branches:
    include: ['*']

jobs:
- job: build_and_test
  displayName: Build, test and fixture harness
  timeoutInMinutes: 45
  strategy:
    matrix:
      windows:
        imageName: windows-2025        # windows-latest is moving to the VS2026 image (see Pitfall 15)
      linux:
        imageName: ubuntu-24.04        # ubuntu-latest currently maps here
  pool:
    vmImage: $(imageName)
  steps:
  - checkout: self
    persistCredentials: false
  - task: UseDotNet@2
    displayName: .NET SDK from global.json
    inputs:
      packageType: sdk
      useGlobalJson: true
  - script: |
      dotnet --info
      cargo --version
      git --version
    displayName: Tool versions
  - script: cargo build --release --locked --manifest-path extractors/cpp-ts/Cargo.toml
    displayName: Build the Rust sidecar
  - script: cargo test --locked --manifest-path extractors/cpp-ts/Cargo.toml
    displayName: Test the Rust sidecar
  - script: dotnet build vdiff.sln -c Release
    displayName: Build the solution
  - script: >
      dotnet test vdiff.sln -c Release --no-build
      --filter "Category!=MermaidCli"
      --logger "trx;LogFileName=tests.trx"
      --results-directory $(Agent.TempDirectory)/results
    displayName: Tests and fixture harness
    env:
      VDIFF_FIXTURE_REPORT: $(Build.ArtifactStagingDirectory)/fixture-report.md
  - task: PublishTestResults@2
    condition: succeededOrFailed()
    inputs:
      testResultsFormat: VSTest
      testResultsFiles: '$(Agent.TempDirectory)/results/*.trx'
  - task: PublishPipelineArtifact@1
    condition: succeededOrFailed()
    inputs:
      targetPath: $(Build.ArtifactStagingDirectory)/fixture-report.md
      artifact: fixture-report-$(imageName)
  - script: |
      npm --prefix tools/mermaid-check ci --ignore-scripts
      dotnet test vdiff.sln -c Release --no-build --filter "Category=MermaidCli"
    displayName: Mermaid syntax check on the summaries (Linux)
    condition: and(succeeded(), eq(variables['Agent.OS'], 'Linux'))
    continueOnError: true
    env:
      VDIFF_MERMAID_NO_SANDBOX: '1'
```
Facts behind it, all `[CITED]` from official pages fetched this session: the hosted images listed for ADO are `windows-2025` (also `windows-latest`), `windows-2025-vs2026`, `ubuntu-24.04` (also `ubuntu-latest`) and `ubuntu-26.04` (learn.microsoft.com hosted-agents page); `UseDotNet@2` with `useGlobalJson: true` reads `sdk.rollForward` and, with `latestFeature` as in this repo's `global.json`, installs the latest 10.0.x `[VERIFIED: global.json `"version": "10.0.100", "rollForward": "latestFeature"`]`; the Ubuntu 24.04 and Windows 2025 image readmes list .NET SDKs up to 10.0.404, Rust/Cargo 1.99.1, Node 22.x, PowerShell 7.6.x, Git 2.56 and Chrome; the Windows images list the VC.Tools components that the `cc` crate needs to build tree-sitter's C sources. A test (`PipelineYamlTests`) should parse the YAML and assert: every `vmImage` or matrix image value is in an allow-list of Microsoft-hosted labels, there is no `pool: name:`, no `organization`, `project`, `serviceConnection` or `System.AccessToken`, and the cargo commands carry `--locked`.

### Fixture sketches (Claude's Discretion; each small, synthetic, public, no `bin`/`obj`/`target`/`out` directories)
| Shape | Tracer can produce | What it contains that the tracer cannot (keeps it honestly pending) | Expected `due` |
|-------|--------------------|-------------------------------------------------------------------|----------------|
| `small-cpp` | added, removed, modified types and members | one inline method whose body changes (`body-changed`) | diff 5, summary 7 |
| `csharp` | nothing (C# is out of the tracer's scope) | whole fixture; a `.csproj` in the tree | diff 5, summary 7 (extractor in Phase 4) |
| `cross-stack` | nothing for C++/CLI | a `ref class` wrapper (`cpp-cli`) over a native class, a `gcroot<>` member, a package pin change | diff 5, summary 7 |
| `extract-class` | added/removed members | members moved into a new class, new association edge, a `body-changed` member | diff 5, summary 7 |
| `rename` | delete plus add only | a type renamed with similar members (rename score, `renamed_from`) | diff 5, summary 7 |

All `expected.diff` and `expected.summary` entries start `state: pending`. Fixture source files are LF, the C++ blanking macros appear in `config.blank` where used.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `vdiff-model/v0-tracer`, `vdiff-diff/v0-tracer` provisional shapes with commits and tool versions inline | `v1` contracts as committed schemas, volatile data in `run.meta.json` | this phase | Goldens become writable by hand; one-way freeze |
| Ordering only inside builder/differ | One `Canonical` module applied at serialisation | this phase | Criterion 4 becomes testable |
| Fixtures-as-code only (tests build repos inline) | Fixture directories with trees, goldens and a ratchet | this phase | Later phases flip flags instead of writing tests |

**Deprecated/outdated:**
- `windows-latest` will move to the Visual Studio 2026 image "in the coming weeks" `[CITED: learn.microsoft.com hosted-agents page]`; Ubuntu 22.04 is deprecated (removal scheduled 2027-04-17). The skeleton pins `windows-2025` and `ubuntu-24.04`; that page says `windows-2025` will also begin using VS2026, so re-check the image readme at connection time.
- JsonSchema.Net and the other json-everything libraries are distributed under an "Open Source Maintenance Fee" EULA from their 5.x/9.x NuGet releases on (see A5).

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | NJsonSchema 11.6.2 and YamlDotNet 18.1.0 are the right packages and are not slopsquats (the legitimacy seam does not support NuGet; evidence is registry metadata only) | Standard Stack, Package Legitimacy Audit | Supply-chain risk; mitigated by a `checkpoint:human-verify` before install |
| A2 | `windows-latest` / `windows-2025` will use VS2026 and still has the VC tools, Rust, Node and Chrome the pipeline relies on | CI skeleton | Windows leg fails at first real run; the VS2026 readme (fetched) lists Rust 1.99.1, VC.Tools and Chrome, so low risk |
| A3 | Chrome on the hosted Ubuntu image needs `--no-sandbox` for puppeteer | Pitfall 12 | Mermaid step fails; it is `continueOnError` initially |
| A4 | D-10's "pending or the phase in which it is due" means `state: pending\|green` plus a documentary `due` number | Pattern 5 | If the user meant a phase counter the harness compares against, the harness needs a current-phase input; small change |
| A5 | JsonSchema.Net's OSMF EULA could apply to the target organisation's use (see "Open Questions" Q1). Quote from `OSMFEULA.txt` in the 9.7.0 package: "The Fee applies only to Users that use the Software as part of revenue-generating activities and have an annual gross revenue greater than or equal to US$10,000." It also says: "This Agreement applies only to the Binary Release and does not limit User's ability to access, modify, or distribute the Software's source code or self-compiled binaries." This research does not decide the legal reading. | Alternatives Considered | A fee or compliance obligation the user did not choose; NJsonSchema (MIT) avoids the question |
| A6 | "Derived edges" in R10 means edges computed from bases and (later) member-type associations, stored as a top-level `edges` array | Proposed v1 shapes | If it meant "derived types" as a reverse index only, the `edges` shape changes before the freeze |
| A7 | `TF_BUILD` is set on Azure Pipelines agents, so accept mode can refuse on it | Pitfall 13 | The CI guard on accept mode would not trigger; also add `CI` and an explicit opt-in |
| A8 | `PublishTestResults@2` and `PublishPipelineArtifact@1` input names (`testResultsFormat`, `testResultsFiles`, `targetPath`, `artifact`) are as written | CI skeleton | YAML fails validation on first connection; trivial fix |
| A9 | Using `language` (`cpp-cli` for the managed-wrapper lane) rather than a separate `lane` field satisfies R8 | Proposed v1 shapes, O1 | If the PO wants "lane" as a first-class name in JSON, add an alias before the freeze |
| A10 | Closed enums plus the "additive relaxation inside v1" policy is acceptable for a one-way decision | Proposed v1 shapes | If the user reads D-01 as forbidding any schema edit after Phase 2, open sets (patterns) must replace enums for kinds |
| A11 | The Windows hosted agent's bundled git behaves like the local one for `git diff --no-index` | Code Examples | Mismatch output differs slightly; harness only prints it |
| A12 | Corvus.Json.Validator would also work (not probed); listed only as an unverified alternative | Alternatives Considered | None unless chosen |

## Open Questions (RESOLVED)

Numbering note: the Proposed v1 Shapes and Pitfalls sections refer to questions by the labels below (O1 = Q1, and so on; the shape discussion's "O1 managed-wrapper", "O2 edges" and "O3 ids" are items 4, 2 and 3 here).

Resolution note (2026-11-20): Q1 to Q4 were settled by the user after research (CONTEXT.md D-15 to D-17). Q5 needs no decision for the Phase 2 contract and is deferred to Phase 6 planning, for the reason given under it.

1. **JsonSchema.Net versus NJsonSchema (licence).** RESOLVED by D-15.
   - What we know: NJsonSchema is MIT and passed every probe within the keyword allow-list. JsonSchema.Net is functionally better (full 2020-12, no silent ignoring) but its NuGet licence file is the OSMF EULA quoted in A5; private-fixture runs happen inside the target organisation.
   - What's unclear: whether the user or the organisation accepts or exempts that fee; this research does not give a legal reading.
   - Recommendation: build on NJsonSchema with the allow-list; ask the user to confirm A5 only if they want JsonSchema.Net.
   - RESOLVED (D-15): NJsonSchema 11.6.2, in the test project only, with schemas in the draft-07 subset it enforces and a keyword allow-list test. JsonSchema.Net was rejected because of its OSMF EULA.

2. **Derived edges (A6).** RESOLVED by D-16.
   - What we know: R10 says "bases and derived edges"; R18 names bases, derived types and association targets as neighbours; ModelBuilder today records only `bases` as written.
   - What's unclear: whether the tracer should already emit `base` edges.
   - Recommendation: define `edges` in the schema now; have `ModelBuilder` emit `base` edges resolved by unique exact or `::`-suffix match (the rule `MermaidWriter.Edges` uses at `MermaidWriter.cs:122-160`). That does not make fixtures accidentally green, because every fixture also holds a feature the tracer cannot produce (Pitfall 10).
   - RESOLVED (D-16): `vdiff-model/v1` has a top-level `edges` array, and the Phase 2 tracer emits `base` edges resolved by a unique exact or `::`-suffix match.

3. **Cross-language id collisions (Pitfall 9).** RESOLVED by D-17.
   - Recommendation: key types by `(language, id)`; confirm before the freeze because it is one-way.
   - RESOLVED (D-17): types are keyed by `(language, id)`.

4. **Managed-wrapper lane naming (A9, `language: cpp-cli`).** RESOLVED by D-17.
   - Recommendation: keep one field; render the lane name from it.
   - RESOLVED (D-17): the lane is the `language` value `cpp-cli`, with no separate `lane` field; the lane label is rendered from it.

5. **Where the unchanged edges for neighbours come from in Phase 6.** RESOLVED for Phase 2 (no v1 change); the selector-side choice is deferred to Phase 6 planning.
   - What we know: v1 diff lists only changed edges; unchanged edges are counted.
   - What's unclear: whether the selector reads the models for neighbour edges (likely) or needs them in the diff.
   - Recommendation: the selector reads both models; no change to v1.
   - RESOLVED (no decision needed now): the frozen v1 contract does not depend on the answer. Each model already carries the full `edges` array (D-16), so a selector that reads both models has every neighbour edge. If Phase 6 instead wants unchanged edges listed in the diff, that needs a new `unchanged` value in the `EdgeDiff.state` enum and a relaxed "counted, never listed" rule for edges. Every existing document stays valid under that change, so the evolution policy keeps it inside v1 (`docs/schema/README.md`, plans 02-05 and 02-06). Which route the selector takes is decided when Phase 6 is planned.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | build, tests | yes | 10.0.401 (`global.json` 10.0.100, `latestFeature`) | none needed |
| cargo / rustc | sidecar build and `SidecarFixture` | yes | 1.97.1 / 1.97.1 | none needed |
| git | tool, tests, `git diff --no-index` | yes | 2.55.0 (minimum 2.45, D-11 of Phase 1) | none |
| PowerShell (`pwsh`) | `TraceRunScriptTests` | yes | 7.7.0 (hosted images ship 7.6.x) | none |
| Node / npm | optional Mermaid check | yes | v26.12.1 / 12.2.1 (`engines` needs >=22.13) | skip the Mermaid step |
| Chrome/Chromium/Edge | Mermaid check only | not probed locally | — | skip the Mermaid step |
| Azure DevOps organisation | running the pipeline | no (D-09, deferred) | — | harness run locally; YAML checked by `PipelineYamlTests` |
| macOS | `/private/var` check | no | — | manual-only |

**Missing dependencies with no fallback:** none for Phase 2 deliverables.
**Missing dependencies with fallback:** an ADO organisation (YAML cannot be executed; verified locally and statically), a Mac (999.1 fix 2 stays manual-only).

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit 2.9.3 with Microsoft.NET.Test.Sdk 17.14.1 and xunit.runner.visualstudio 3.1.4 `[VERIFIED: Vdiff.Tests.csproj:12-14]` |
| Config file | none (csproj-driven); traits select subsets |
| Quick run command | `dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj -c Release --filter "Category=Contract\|Category=Fixtures"` |
| Full suite command | `dotnet test vdiff.sln -c Release --filter "Category!=MermaidCli"` then `cargo test --locked --manifest-path extractors/cpp-ts/Cargo.toml` |

Add `[Trait("Category", "Contract")]` to schema, parity and canonical tests, `[Trait("Category", "Fixtures")]` to the harness and golden self-checks, `[Trait("Category", "PrivateFixtures")]` to the private entry point. The suite already uses a `Category` trait for `MermaidCli`.

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| R-10 | Tracer output (base model, head model, diff) validates against the v1 schemas | integration | `dotnet test --filter "FullyQualifiedName~SchemaValidationTests"` | Wave 0 |
| R-10 | Full-fat record instance validates; every schema keyword has a rejected negative document; schema uses only allow-listed keywords | unit | `--filter "FullyQualifiedName~SchemaValidationTests"` | Wave 0 |
| R-10 | Record properties equal schema properties (both directions) for model, diff, run meta | unit | `--filter "FullyQualifiedName~SchemaParityTests"` | Wave 0 |
| R-23 | Seeded shuffles of every collection give byte-identical JSON; reflection guard that every list property is covered by the shuffler and a comparer | unit | `--filter "FullyQualifiedName~CanonicalSerialisationTests"` | Wave 0 |
| R-23 | Two CLI runs give identical canonical files; same trees with different commits give identical canonical files and a different `run.meta.json`; no local identity in any file including meta | integration | `--filter "FullyQualifiedName~DeterminismTests"` | exists, update |
| ACC-01 | Ratchet truth table (four cases plus tool-failure, missing output, schema-invalid, missing golden) | unit | `--filter "FullyQualifiedName~Ratchet"` | Wave 0 |
| ACC-01 | Five fixtures report RED (pending) and the harness passes; a temporary green fixture passes end to end; report table lists each fixture and output | integration | `--filter "FullyQualifiedName~FixtureHarnessTests"` | Wave 0 |
| ACC-01 | Goldens validate, round-trip byte-identically, pass `MermaidLint`, have no CR or BOM; hygiene guard accepts only `expected/` golden names | unit | `--filter "FullyQualifiedName~FixtureGoldenTests\|FullyQualifiedName~RepoHygieneTests"` | Wave 0 / exists, update |
| ACC-01 | Accept mode refuses under `TF_BUILD`, writes only under `expected/` | unit | `--filter "FullyQualifiedName~FixtureHarnessTests"` | Wave 0 |
| ACC-01 | `azure-pipelines.yml` names only hosted images, no org/pool/service-connection/token, cargo uses `--locked` | unit | `--filter "FullyQualifiedName~PipelineYamlTests"` | Wave 0 |
| ACC-02 | Private mode on a synthetic stand-in: same comparison code, `--out` inside this repo refused, console shows only `<shape>: PASS\|FAIL\|PENDING <date>`, shape-name allow-list enforced, early return when unset | integration | `--filter "FullyQualifiedName~PrivateFixtureTests"` | Wave 0 |
| ACC-02 | `docs/how-to/private-fixtures.md` exists and names `VDIFF_FIXTURES_DIR` and `VDIFF_PRIVATE_OUT` | unit | `--filter "FullyQualifiedName~PrivateFixtureTests"` | Wave 0 |
| 999.1 (1) | fsmonitor tests' positive control holds on Linux | integration | Linux CI leg (`dotnet test --filter "FullyQualifiedName~fsmonitor"`) | exists, update; verified on the Linux leg once connected |
| 999.1 (2) | `SamePath` resolves `/var` to `/private/var` | manual-only | run `GitObjectReaderTests.ToplevelOrGitDir_*` on a Mac | exists, update; manual-only (no macOS leg, D-11) |

### Sampling Rate
- **Per task commit:** the quick command above (target under 30 s once the sidecar is built).
- **Per wave merge:** the full suite command.
- **Phase gate:** full suite green, plus a local run of the harness showing five fixtures RED (pending), before `/gsd-verify-work`.

### Wave 0 Gaps
- [ ] `tests/Vdiff.Tests/Contract/SchemaValidationTests.cs`, `SchemaParityTests.cs`, `CanonicalSerialisationTests.cs`
- [ ] `tests/Vdiff.Tests/Fixtures/` (loader, harness, ratchet, report, golden self-checks, private mode)
- [ ] `tests/Vdiff.Tests/Ci/PipelineYamlTests.cs`
- [ ] `docs/schema/*.schema.json` (three files) and `tests/fixtures/<shape>/` (five fixtures, ten goldens)
- [ ] Framework additions: NJsonSchema 11.6.2 and YamlDotNet 18.1.0 in `Vdiff.Tests.csproj` (after `checkpoint:human-verify`)
- [ ] `.gitattributes` rules and the `RepoHygieneTests` change, in the same commit as the first golden
- [ ] Update the ten "five files" sites listed in Pitfall 6 behind one shared constant

## Security Domain

`security_enforcement` is enabled (absent or true in `.planning/config.json`; `security_asvs_level` is 1).

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | no auth in this phase; the YAML uses no `System.AccessToken` or service connection (asserted by `PipelineYamlTests`) |
| V3 Session Management | no | none |
| V4 Access Control | yes (narrow) | private output must stay outside this repository: `VDIFF_PROTECTED_DIRS` refusal in `CliApp`, set by the harness |
| V5 Input Validation | yes | strict typed YamlDotNet deserialisation with a size cap; shape-name allow-list; relative-path containment when reading `base/`/`head/` trees (reject `..`, rooted paths, symlinks and reparse points); schema validation of tool output |
| V6 Cryptography | no | no crypto; commit ids are git's |
| V12 Files and Resources | yes | golden writes only under `expected/`; temp repos under the temp directory; outputs by `HeldDirectory` as today |
| V14 Configuration | yes | pinned NuGet versions, `cargo --locked`, `npm ci --ignore-scripts`, `persistCredentials: false`, no secrets in YAML |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Target identifiers or source leaking into the public repository via fixtures, goldens or logs | Information disclosure | Synthetic fixtures only; private fixtures live outside the repo; shape-name allow-list; no diff text on the console in private mode; pass/fail/pending plus date as the only record (D-14); run `check-public-diff.sh` before commits |
| Hostile `fixture.yml` (oversized, unknown keys, YAML tags) | Tampering / DoS | strict POCO deserialisation (rejects unknown keys, verified), size cap, no polymorphic types |
| Path traversal or link-following in fixture trees | Tampering | normalise and contain relative paths; skip nothing silently, fail on links |
| Accept mode overwriting goldens in CI | Tampering | refuse under `TF_BUILD`/`CI`; writes only under `expected/` |
| Supply-chain substitution of validator or YAML package | Tampering | exact pins, `checkpoint:human-verify`, no install scripts |
| A validator that accepts everything gives false assurance | Repudiation | keyword allow-list test plus a negative document per keyword |
| CI definition naming an organisation or pool | Information disclosure | `PipelineYamlTests` allow-list of hosted image labels |

## Sources

### Primary (HIGH confidence for in-repo facts)
- Repository files read this session with line ranges quoted above: `ModelTypes.cs`, `ModelBuilder.cs`, `ModelDiffer.cs`, `OutputWriter.cs`, `MermaidWriter.cs`, `TracerPipeline.cs`, `CliApp.cs`, `CliOptions.cs`, `CppSidecar.cs`, `GitObjectReader.cs` (reason strings), `SyntheticRepo.cs`, `EnvironmentScope.cs`, `SidecarFixture.cs`, `DeterminismTests.cs`, `RepoHygieneTests.cs`, `MermaidCliSyntaxTests.cs`, `protocol.rs`, `.gitattributes`, `global.json`, `docs/requirements/requirements.md`, `.planning/phases/01-offline-tracer/uat-runs/01-UAT-10-linux.md`.
- Probes run in the session scratchpad: NJsonSchema/JsonSchema.Net accept-reject matrix (draft-07 and 2020-12, offline), NJsonSchema per-keyword probe, YamlDotNet strict deserialisation, `git diff --no-index`, CRLF behaviour under `core.autocrlf=true`.
- NuGet API: flat-container version lists, nuspec files, registration publish dates, search download counts (NJsonSchema, YamlDotNet, JsonSchema.Net, Corvus).
- `OSMFEULA.txt` extracted from the JsonSchema.Net 9.7.0 package.

### Secondary (MEDIUM confidence; official docs fetched, seam tier LOW for the fetch provider)
- https://learn.microsoft.com/en-us/azure/devops/pipelines/agents/hosted?view=azure-devops (image labels, `windows-latest` move to VS2026, Ubuntu 22.04 deprecation)
- https://learn.microsoft.com/en-us/azure/devops/pipelines/tasks/reference/use-dotnet-v2?view=azure-pipelines (`useGlobalJson`, `rollForward`)
- https://learn.microsoft.com/en-us/azure/devops/pipelines/tasks/reference/dotnet-core-cli-v2?view=azure-pipelines (fetched; the skeleton uses plain `script:` steps instead)
- https://github.com/actions/runner-images (Ubuntu2404, Windows2025 and Windows2025-VS2026 readmes: .NET 10.0.x SDKs, Rust 1.99.1, Node 22.x, PowerShell 7.6.x, Git 2.56, Chrome, VC.Tools)

### Tertiary (LOW confidence)
- Web search results on NJsonSchema draft 2020-12 support (an upstream issue reports suite failures, outcome unknown) and on the json-everything maintenance-fee model; used only to decide what to probe.

## Metadata

**Confidence breakdown:**
- Standard stack: MEDIUM. NJsonSchema and YamlDotNet behaviour is probed; package provenance is `[ASSUMED]` because the legitimacy seam does not support NuGet. Corvus is unprobed.
- Architecture: HIGH for harness, ratchet and canonicalisation (grounded in the code read); MEDIUM for the v1 field-level shape, which is a recommendation within Claude's Discretion and has three flagged freeze-time questions (O1 to O3).
- Pitfalls: HIGH for the in-repo ones (hygiene guard, five-file sites, CRLF, keyword ignoring were all observed); MEDIUM for hosted-agent behaviour.
- CI: MEDIUM. Official docs read, nothing executed on a hosted agent.

**Research date:** 2026-11-20
**Valid until:** 2026-12-20 for the schema and harness design; 2026-11-27 for the hosted-image labels (they are changing now).
