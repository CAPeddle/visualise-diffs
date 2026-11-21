---
phase: 02-model-contract-and-fixture-harness
reviewed: 2026-11-21T00:00:00Z
depth: standard
files_reviewed: 113
files_reviewed_list:
  - .gitattributes
  - azure-pipelines.yml
  - docs/how-to/private-fixtures.md
  - docs/schema/README.md
  - docs/schema/vdiff-diff-v1.schema.json
  - docs/schema/vdiff-model-v1.schema.json
  - docs/schema/vdiff-run-meta-v1.schema.json
  - docs/trials/private-fixtures.md
  - src/Vdiff.Core/Cli/CliApp.cs
  - src/Vdiff.Core/Cli/CliOptions.cs
  - src/Vdiff.Core/Diff/ModelDiffer.cs
  - src/Vdiff.Core/Model/BaseResolver.cs
  - src/Vdiff.Core/Model/Canonical.cs
  - src/Vdiff.Core/Model/ModelBuilder.cs
  - src/Vdiff.Core/Model/ModelTypes.cs
  - src/Vdiff.Core/Model/RunMeta.cs
  - src/Vdiff.Core/Output/HeldDirectory.cs
  - src/Vdiff.Core/Output/HtmlWriter.cs
  - src/Vdiff.Core/Output/MermaidWriter.cs
  - src/Vdiff.Core/Output/OutputWriter.cs
  - src/Vdiff.Core/Pipeline/TracerPipeline.cs
  - tests/Vdiff.Tests/Ci/PipelineYamlTests.cs
  - tests/Vdiff.Tests/Contract/CanonicalSerialisationTests.cs
  - tests/Vdiff.Tests/Contract/ContractSamples.cs
  - tests/Vdiff.Tests/Contract/DiffPresence.cs
  - tests/Vdiff.Tests/Contract/DiffPresenceTests.cs
  - tests/Vdiff.Tests/Contract/ModelPresence.cs
  - tests/Vdiff.Tests/Contract/ModelPresenceTests.cs
  - tests/Vdiff.Tests/Contract/SchemaFreezeTests.cs
  - tests/Vdiff.Tests/Contract/SchemaParityTests.cs
  - tests/Vdiff.Tests/Contract/SchemaSupport.cs
  - tests/Vdiff.Tests/Contract/SchemaValidationTests.cs
  - tests/Vdiff.Tests/Diff/ModelBuilderTests.cs
  - tests/Vdiff.Tests/Diff/ModelDifferTests.cs
  - tests/Vdiff.Tests/Extraction/BlankingEndToEndTests.cs
  - tests/Vdiff.Tests/Extraction/RichFixtureEndToEndTests.cs
  - tests/Vdiff.Tests/Fixtures/AcceptMode.cs
  - tests/Vdiff.Tests/Fixtures/CommittedFixtureTests.cs
  - tests/Vdiff.Tests/Fixtures/FixtureConfig.cs
  - tests/Vdiff.Tests/Fixtures/FixtureGoldenTests.cs
  - tests/Vdiff.Tests/Fixtures/FixtureHarness.cs
  - tests/Vdiff.Tests/Fixtures/FixtureHarnessTests.cs
  - tests/Vdiff.Tests/Fixtures/FixtureLoader.cs
  - tests/Vdiff.Tests/Fixtures/FixtureReport.cs
  - tests/Vdiff.Tests/Fixtures/PrivateFixtureTests.cs
  - tests/Vdiff.Tests/Fixtures/PrivateFixtures.cs
  - tests/Vdiff.Tests/Fixtures/Ratchet.cs
  - tests/Vdiff.Tests/Fixtures/RatchetTests.cs
  - tests/Vdiff.Tests/Fixtures/Shapes.cs
  - tests/Vdiff.Tests/GitObjectReaderTests.cs
  - tests/Vdiff.Tests/ModelAssemblyTests.cs
  - tests/Vdiff.Tests/Output/DeterminismTests.cs
  - tests/Vdiff.Tests/Output/HtmlWriterTests.cs
  - tests/Vdiff.Tests/Output/MermaidCliSyntaxTests.cs
  - tests/Vdiff.Tests/Output/MermaidLint.cs
  - tests/Vdiff.Tests/Output/MermaidWriterTests.cs
  - tests/Vdiff.Tests/Safety/AliasedOutTests.cs
  - tests/Vdiff.Tests/Safety/HostileInputTests.cs
  - tests/Vdiff.Tests/Safety/RepoHygieneTests.cs
  - tests/Vdiff.Tests/Safety/RepoUnchangedMatrixTests.cs
  - tests/Vdiff.Tests/Scope/IncludeGlobEndToEndTests.cs
  - tests/Vdiff.Tests/Support/RepoRoot.cs
  - tests/Vdiff.Tests/TracerEndToEndTests.cs
  - tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs
  - tests/Vdiff.Tests/Vdiff.Tests.csproj
  - tests/fixtures/README.md
  - tests/fixtures/cross-stack/base/Directory.Packages.props
  - tests/fixtures/cross-stack/base/app/Synthetic.App/Dashboard.cs
  - tests/fixtures/cross-stack/base/app/Synthetic.App/Synthetic.App.csproj
  - tests/fixtures/cross-stack/base/interop/EngineWrapper.h
  - tests/fixtures/cross-stack/base/interop/Synthetic.Interop.vcxproj
  - tests/fixtures/cross-stack/base/native/include/synthetic/engine.h
  - tests/fixtures/cross-stack/expected/diff.json
  - tests/fixtures/cross-stack/expected/summary.mmd
  - tests/fixtures/cross-stack/fixture.yml
  - tests/fixtures/cross-stack/head/Directory.Packages.props
  - tests/fixtures/cross-stack/head/app/Synthetic.App/Dashboard.cs
  - tests/fixtures/cross-stack/head/app/Synthetic.App/Synthetic.App.csproj
  - tests/fixtures/cross-stack/head/interop/EngineWrapper.h
  - tests/fixtures/cross-stack/head/interop/Synthetic.Interop.vcxproj
  - tests/fixtures/cross-stack/head/native/include/synthetic/engine.h
  - tests/fixtures/csharp/base/src/Synthetic.Orders/Document.cs
  - tests/fixtures/csharp/base/src/Synthetic.Orders/Foo.cs
  - tests/fixtures/csharp/base/src/Synthetic.Orders/OrderService.cs
  - tests/fixtures/csharp/base/src/Synthetic.Orders/Synthetic.Orders.csproj
  - tests/fixtures/csharp/expected/diff.json
  - tests/fixtures/csharp/expected/summary.mmd
  - tests/fixtures/csharp/fixture.yml
  - tests/fixtures/csharp/head/src/Synthetic.Orders/Document.cs
  - tests/fixtures/csharp/head/src/Synthetic.Orders/Foo.cs
  - tests/fixtures/csharp/head/src/Synthetic.Orders/Invoice.cs
  - tests/fixtures/csharp/head/src/Synthetic.Orders/OrderService.cs
  - tests/fixtures/csharp/head/src/Synthetic.Orders/Synthetic.Orders.csproj
  - tests/fixtures/extract-class/base/include/synthetic/report.h
  - tests/fixtures/extract-class/expected/diff.json
  - tests/fixtures/extract-class/expected/summary.mmd
  - tests/fixtures/extract-class/fixture.yml
  - tests/fixtures/extract-class/head/include/synthetic/formatter.h
  - tests/fixtures/extract-class/head/include/synthetic/report.h
  - tests/fixtures/rename/base/include/synthetic/circle.h
  - tests/fixtures/rename/base/include/synthetic/shape.h
  - tests/fixtures/rename/expected/diff.json
  - tests/fixtures/rename/expected/summary.mmd
  - tests/fixtures/rename/fixture.yml
  - tests/fixtures/rename/head/include/synthetic/disc.h
  - tests/fixtures/rename/head/include/synthetic/shape.h
  - tests/fixtures/small-cpp/base/include/synthetic/legacy.h
  - tests/fixtures/small-cpp/base/include/synthetic/widget.h
  - tests/fixtures/small-cpp/expected/diff.json
  - tests/fixtures/small-cpp/expected/summary.mmd
  - tests/fixtures/small-cpp/fixture.yml
  - tests/fixtures/small-cpp/head/include/synthetic/gadget.h
  - tests/fixtures/small-cpp/head/include/synthetic/widget.h
findings:
  critical: 0
  warning: 5
  info: 7
  total: 12
  necessity: 0
status: issues_found
---

# Phase 02: Code Review Report

**Reviewed:** 2026-11-21
**Depth:** standard
**Files Reviewed:** 113
**Status:** issues_found

## Summary

Reviewed the model contract (records, schemas, canonical order), the builder and differ, the writers, the
CLI/output-safety path, the fixture harness (loader, ratchet, accept mode, private mode, report), the CI
pipeline and its static test, and the contract and fixture tests. The three schema files hash to the pins in
`SchemaFreezeTests` (checked outside the test run; all three match). The schema, the records and the
serialiser agree on property names, key order, canonical order and required lists. I found no security
defect and no data-loss bug in the CLI/output path; the hardening code (`CliApp`, `HeldDirectory`,
`OutputWriter`) is consistent with its documented bounds.

The defects are in places the unit tests do not reach:

- `MermaidWriter` resolves bases with its own copy of the rule, and that copy has drifted from
  `BaseResolver` (language, duplicate fqns, separator), and it draws removed bases as live edges.
- The visibility mapping collapses three C# access values into the public marker.
- The harness lets a malformed output file crash the run instead of producing the FAIL row its own
  contract promises.
- Private mode leaves stale diagnostic files behind in `VDIFF_PRIVATE_OUT`.

Review limits: the hand-written fixture sources and goldens were checked for internal consistency only
(rename score arithmetic, language ordering, member ordering, summary class ids); they are data. The large
test files `AliasedOutTests`, `TraceRunScriptTests`, `RepoUnchangedMatrixTests`, `HostileInputTests`,
`TracerEndToEndTests` and the end-to-end extraction tests were skimmed for reliability patterns, not read
line by line. No code was built or run.

## Warnings

### WR-01: MermaidWriter base resolution has drifted from BaseResolver (language, duplicates, separator)

**File:** `src/Vdiff.Core/Output/MermaidWriter.cs:123-165`
**Issue:** `Edges` re-implements the base-resolution rule that `BaseResolver.Resolve` owns
(`src/Vdiff.Core/Model/BaseResolver.cs:44-72`), and the copy differs in three ways that change what the
reviewer sees:

1. It ignores language. `byFqn` is keyed on `Fqn` alone, so once a `csharp` and a `cpp` type with the same
   fqn are both displayed (the model and differ explicitly support this, D-17), a base written on one
   language's type resolves to the other language's class. `BaseResolver` filters `sameLanguage` first.
2. It treats a repeated exact fqn as resolved. `byFqn.TryAdd(...)` keeps the first entry. Two displayed
   types with one fqn arise today from the D-21 duplicate-id rule (`class shop::X#a.h`, `class shop::X#b.h`).
   `BaseResolver` calls that ambiguous and returns null; the model edge for the same base has no `to`, but
   the diagram draws an edge to whichever type sorts first.
3. The suffix test is hard-coded to `"::"`. A C# base written `Document` never matches
   `Synthetic.Orders.Document`. The committed `tests/fixtures/csharp/expected/summary.mmd:21` expects exactly
   that edge, so the writer and its own golden already disagree on the rule.

Points 1 and 2 are reachable with the C++ tracer today (point 2) or as soon as a second language lands
(point 1). The result is an edge in the PR summary that the model says does not exist.
**Fix:** Resolve through the model instead of re-deriving. Either pass the resolved edges into the writer
(diff `edges` once Phase 5 fills them, or the model edges), or call a shared helper that takes the language:

```csharp
// BaseResolver: expose the candidate-matching rule over any list with a language and an fqn
public static int? ResolveIndex(string written, string language, IReadOnlyList<(string Language, string Fqn)> candidates, int selfIndex)

// MermaidWriter.Edges: use it, so language, ambiguity and self-reference follow one rule
var target = BaseResolver.ResolveIndex(written, shown[derived].Language,
    shown.Select(t => (t.Language, t.Fqn)).ToList(), derived);
if (target is { } t && t != derived) edges.Add("  " + ids[t] + " <|-- " + ids[derived]);
```

Add a MermaidWriter test with a `cpp` and a `csharp` type of the same fqn and a derived type in one of them,
and one with two displayed same-language types of one fqn.

### WR-02: Removed bases are drawn as ordinary inheritance edges

**File:** `src/Vdiff.Core/Output/MermaidWriter.cs:134`
**Issue:** `shown[derived].Bases.Added.Concat(shown[derived].Bases.Removed)` feeds both lists into the same
`base <|-- derived` line, with no state marker. A type that stops inheriting from `Old` (when `Old` is also
displayed) is drawn as `Old <|-- Derived`, identical to a live relationship. A base swap draws two
indistinguishable edges. The reviewer cannot tell an added base from a removed one, which is the structural
change the diagram exists to show (Core Value in `.claude/CLAUDE.md`). The tests pin the behaviour
(`MermaidWriterTests.Class_blocks_come_first_in_id_order_...` draws `Base <|-- Alpha` for a removed base;
`An_exact_fqn_wins_over_a_suffix_match_and_a_pair_is_drawn_once` merges an added and a removed `Base`), so a
fix has to change them. `tests/fixtures/README.md` rule 9 specifies a dotted style (`..>`) only for removed
associations and says nothing for removed inheritance.
**Fix:** Decide the rule in the golden README first, then implement it. Minimum: draw only `Bases.Added`
(and any base unchanged on both sides, once edges exist) as `<|--`, and for a removed base either omit the
edge or use a distinct dashed link, for example `  C_Old <|.. C_Derived : removed`, with the matching
`MermaidLint` pattern. If the current merged behaviour is intended for Phase 2, record it as a known
limitation in the README, because it contradicts the "removed" colouring the writer applies to classes.

### WR-03: Member visibility maps internal, protected-internal and private-protected to the public marker

**File:** `src/Vdiff.Core/Output/MermaidWriter.cs:84-89`
**Issue:** `Visibility` handles `protected` and `private` and sends every other value, including the model's
`internal`, `protected-internal`, `private-protected` and `none`, to `+`. The schema enum has seven values
(`docs/schema/vdiff-model-v1.schema.json:87`, `:157`), and `SchemaParityTests` pins all seven. A C# member
that goes from `public` to `internal` or to `private-protected` is a `[modified]` member whose line shows the
same `+` before and after (only `AccessAfter` is drawn), so the reviewer sees a change with nothing that
explains it, and a narrowing is drawn as public. Latent today (the tracer emits `public`, `protected`,
`private`), reachable as soon as the C# extractor lands in Phase 4.
**Fix:** Map every value explicitly and fail loudly on an unknown one in tests:

```csharp
static char Visibility(string? access) => access switch
{
    "public" or "none" or null => '+',
    "protected" or "protected-internal" => '#',
    "private" or "private-protected" => '-',
    "internal" => '~',          // Mermaid's package/internal marker
    _ => '+',
};
```

Update the member-visibility theory in `MermaidWriterTests` and the README rule 6 (it currently lists only
three markers), and extend `MermaidLint.MemberLine` (`[+#\-]`) to accept `~`.

### WR-04: A malformed output file crashes the harness instead of producing the promised FAIL row

**File:** `tests/Vdiff.Tests/Fixtures/FixtureHarness.cs:103-112` (via `tests/Vdiff.Tests/Contract/SchemaSupport.cs:36-45,90`)
**Issue:** `Evaluate` documents that "a run that failed, a missing golden, a missing output or an output that
fails its schema is FAIL in every state". `SchemaGate` calls `SchemaSupport.ValidateOutputs`, which calls
`JsonSchema.Validate(string)`. NJsonSchema parses the text first, so an output file that is not JSON (a
truncated write, a serialiser regression) throws a `JsonReaderException` out of `Evaluate`. In `RunAll` that
exception is not caught, so the run aborts without a report row, without the report file
(`FixtureReport.WriteIfRequested` is after the loop) and without judging the remaining fixtures. In private
mode it is caught per shape, but the shape then reports only `the shape could not be run`, losing the
schema reason. `RunSynthetic`'s accept-mode `sound` check calls the same gate, so accept mode can also throw
an unrelated exception type.
**Fix:** Make `ValidateOutputs` total: catch the parse failure per file and report it as a problem line.

```csharp
IReadOnlyList<string> errors;
try { errors = Errors(schemaFile, File.ReadAllText(path)); }
catch (Exception e) when (e is Newtonsoft.Json.JsonException or IOException or UnauthorizedAccessException)
{ problems.Add($"{name}: not valid JSON"); continue; }
problems.AddRange(errors.Select(e => $"{name}: {e}"));
```

Add a `FixtureHarnessTests` case that writes a truncated `diff.json` and expects a FAIL outcome.

### WR-05: Private mode leaves stale diagnostic files from earlier runs in VDIFF_PRIVATE_OUT

**File:** `tests/Vdiff.Tests/Fixtures/PrivateFixtures.cs:157-193`
**Issue:** `RunShapes` reuses `privateOut/<shape>/` across runs and only ever writes files, never removes
them. `<shape>/diff.diff`, `<shape>/summary.diff`, `<shape>/cli-stderr.txt` and `<shape>/error.txt` are
written only when the current run produces that condition. After a shape goes from FAIL or PENDING to PASS,
the old diff, stderr text and `error.txt` stay next to the new `out/`, and the how-to tells the maintainer
to "read these files to find out why a shape is FAIL or what makes it PENDING"
(`docs/how-to/private-fixtures.md:123`). They will read a previous run's content as the current reason. The
stale files also keep target content on disk longer than the run that produced them.
**Fix:** Clear the per-shape diagnostics before each shape runs (not `out/`, which the CLI overwrites by
design):

```csharp
foreach (var stale in new[] { "diff.diff", "summary.diff", "cli-stderr.txt", "error.txt" })
{
    File.Delete(Path.Combine(shapeOut, stale));   // File.Delete ignores a missing file
}
```

Add a `PrivateFixtureTests` case that runs a FAIL, then a PASS, and asserts those files are gone.

## Info

### IN-01: Writers do not yet cover the v1 states and counts the schema already allows

**File:** `src/Vdiff.Core/Output/HtmlWriter.cs:42-47`, `src/Vdiff.Core/Output/MermaidWriter.cs:45-47`
**Issue:** `HtmlWriter` prints types added/removed/modified/unchanged and members added/removed/modified, and
omits `renamed`, `body_changed` and every edge and package count, so once Phase 5 fills them the line stops
adding up. Its CSS has no `renamed` or `body-changed` rule, though both reach `class="..."`. `MermaidWriter`
emits three `classDef` lines and writes `:::<state>` for any type state, so a `renamed` type gets a class with
no definition, while the committed goldens and `tests/fixtures/README.md` specify five. This is the
documented Phase 7 gap and is harmless while the counts are zero; it needs a tracking note so it is not
discovered after Phase 5 changes the differ.
**Fix:** Add a Phase 5/7 checklist item (writers: renamed/body-changed counts, `renamed` and `context`
classDefs, package nodes) to the plan that fills those counts, or make both writers fail a test when a state
outside {added, removed, modified} reaches them.

### IN-02: Duplicated documentation blocks and repo-root finders

**File:** `src/Vdiff.Core/Cli/CliApp.cs:156-163`, `src/Vdiff.Core/Output/HeldDirectory.cs:12-19`,
`src/Vdiff.Core/Output/OutputWriter.cs:67-74`; `tests/Vdiff.Tests/Support/RepoRoot.cs:9`,
`tests/Vdiff.Tests/Safety/RepoHygieneTests.cs:31`, `tests/Vdiff.Tests/Output/MermaidCliSyntaxTests.cs:72`
**Issue:** The "Check-to-write interval (T-01-43, T-01-46)" paragraph is pasted verbatim into three XML doc
comments; the next security change will update one and leave two stale. The same "walk up to `vdiff.sln`"
loop exists three times, although `RepoRoot.Find()` is the shared helper (and `MermaidCliSyntaxTests` throws a
different exception type for the same failure).
**Fix:** Keep the paragraph on `HeldDirectory` and `<see cref>` it from the other two. Delete
`RepoHygieneTests.FindRoot` and `MermaidCliSyntaxTests.RepositoryRoot` and call `RepoRoot.Find()`.

### IN-03: Numeric limits repeated in message and usage text

**File:** `src/Vdiff.Core/Cli/CliApp.cs:49-50,55`, `src/Vdiff.Core/Cli/CliOptions.cs:20-23,32-33`
**Issue:** `ProtectedDirectoriesMalformed` says "at most 8" next to `MaxProtectedDirectories = 8`, and `Usage`
says "at most 256" and "at most 64" next to `MaxBlankNames` and `MaxIncludeGlobs`. Changing a constant
leaves the text wrong.
**Fix:** Build the strings from the constants with interpolation (`$"... at most {MaxProtectedDirectories} ..."`;
`Usage` as a `static readonly string`), or add a test that parses the numbers out of the text.

### IN-04: Model namespace depends on Output; canonical order is tied to the serialiser options

**File:** `src/Vdiff.Core/Model/Canonical.cs:2,64,194`
**Issue:** `Canonical` (Model) uses `OutputWriter.JsonOptions` (Output) for its final tie-break, while
`OutputWriter` calls `Canonical.Canonicalise`, so the two namespaces reference each other. It also means a
change to a serialiser option (indentation, naming policy, null handling) can change the order of tied
elements in every output. It is deliberate and documented ("the shared options"), but it is a coupling the
contract does not state. Two elements only reach the tie-break when every canonical key is equal; the
builder already makes ids and keys unique, so in practice this is a hand-built-input path.
**Fix:** Give `Canonical` its own private `JsonSerializerOptions` for tie-breaking (compact, snake_case, nulls
omitted), and note in `docs/schema/README.md` that tied elements are ordered by that fixed text. This also
removes the cycle.

### IN-05: Schema documents contradict themselves in two places

**File:** `docs/schema/README.md:3,116-118`, `docs/schema/vdiff-model-v1.schema.json:222-241`
**Issue:** The README is marked "Status: frozen on 2026-11-21 after review" yet still carries "Open item for
the freeze review: MemberModel has no `file`...". And `SourceSpan`'s description says "Lines are counted from
1" while `start_line` and `end_line` allow `minimum: 0`; a zero-line span validates. Tightening the minimum
after the freeze needs v2, so the description is the thing to fix before it spreads into extractors.
**Fix:** Move the open item to a "Known gaps (additive inside v1)" heading or delete it if the review
settled it. For `SourceSpan`, either say lines "are counted from 1 by every extractor; 0 is accepted but
never written", or fix it now by updating the pinned hash in `SchemaFreezeTests` in the same commit.

### IN-06: Test hygiene: a literal invisible character, a stale name, a username-dependent assertion

**File:** `tests/Vdiff.Tests/Output/DeterminismTests.cs:245`, `:92`, `:209-215`
**Issue:** (a) Line 245 asserts `DoesNotContain('﻿', ...)` with a literal U+FEFF embedded in the source,
which is invisible in review and trips content scanners. (b) `Two_runs_on_the_same_input_give_five_byte_identical_files`
checks six files (`OutputWriter.FileNames`). (c) `No_output_file_contains_...` adds `Environment.UserName` and
`MachineName` of three or more characters and does a case-insensitive substring search over outputs that hold
words like `operator`, `Gadget`, `Widget`, `Legacy`; a user or host named `ope`, `gad`, `leg`, `tab` or similar
fails the test with a message about a "local identity". CI agents use fixed names, but a developer machine
does not.
**Fix:** (a) `'﻿'`. (b) Rename to `..._give_byte_identical_files`. (c) Search for the names only with
word boundaries or at path-like positions (`\\name\\`, `/name/`, `name@`), and skip any name that occurs in
the baseline output.

### IN-07: PrivateFixtures.RunShape overwrites VDIFF_PROTECTED_DIRS and mishandles two path shapes

**File:** `tests/Vdiff.Tests/Fixtures/PrivateFixtures.cs:52,111-121`
**Issue:** `new EnvironmentScope({ [VDIFF_PROTECTED_DIRS] = repoRoot })` replaces any value the operator set,
so extra protected directories a maintainer configured are dropped for the run (the CLI reads only the
variable). A checkout path containing the platform path separator (`;` on Windows is a legal folder
character) makes `ParseProtectedDirectories` split it and refuse, so every shape then fails with the generic
"tool run failed". `IsInside` fails closed on any path `FinalPath.Resolve` refuses, which includes a UNC
share, the usual place for an organisation's results folder; the user then gets "must name a directory
outside this repository", which is not the reason.
**Fix:** Append to the existing value (`existing + Path.PathSeparator + repoRoot` when it is non-empty),
refuse a `repoRoot` that contains the separator with its own fixed message, and have `Refusal` distinguish an
unresolvable path from an inside path with a second fixed message that names only the variable.

---

_Reviewed: 2026-11-21_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
