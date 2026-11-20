# Phase 2: Model Contract and Fixture Harness - Context

**Gathered:** 2026-11-19
**Status:** Ready for planning

<domain>
## Phase Boundary

Fix one versioned model contract, `vdiff-model/v1`, and one diff contract, `vdiff-diff/v1`, as
committed JSON Schema files. Migrate the Phase 1 tracer to emit them, and prove byte-identical
serialisation (R-23). Add a golden-fixture harness with five synthetic fixtures (small C++, C#,
cross-stack with C++/CLI, extract-class refactor, rename) whose expected `diff.json` and
`summary.mmd` are written by hand now and turn green in Phases 5 to 7 (ACC-01). Add an Azure
Pipelines definition that runs the build, the tests and the harness on Windows and Linux, and a
documented private-fixture recipe with a harness entry point for real PRs held in the target
organisation (ACC-02). Requirements: R-10, R-23, ACC-01, ACC-02.

Not in this phase: any new extraction (C#, C++/CLI, `.visualise-diffs.yml` reading are Phase 4),
rename scoring, `body-changed` and edge diffing (Phase 5), the node selector and its golden
(Phase 6), the final renderers (Phase 7), connecting the pipeline to an ADO organisation.

</domain>

<decisions>
## Implementation Decisions

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

### Decisions taken after research (2026-11-20, from 02-RESEARCH.md open questions)
- **D-15:** The JSON Schema validator is NJsonSchema 11.6.2 (MIT), in the test project only.
  Schemas use the draft-07 subset it enforces: single-value `enum` instead of `const`, and no
  `if/then` or `dependentRequired`. A keyword allow-list test runs over the schema files, with at
  least one negative document for each keyword used. JsonSchema.Net was rejected because of its
  OSMF EULA.
- **D-16:** `vdiff-model/v1` has a top-level `edges` array. The Phase 2 tracer already emits
  `base` edges, resolved by a unique exact match or `::`-suffix match (the rule in
  `MermaidWriter.Edges`). Each fixture also holds a feature the tracer can't produce, so this
  turns no fixture green by accident. — **Reversibility:** one-way (part of the frozen schema).
  - **D-16 clarification (user, 2026-11-20, after plan-check iteration 3):** the exact or
    `::`-suffix rule governs the syntax-only C++ extractors (the tracer and the tree-sitter
    sidecar). The frozen schema carries the edge shape (`from`, optional `to`, `to_name`), not
    a resolution algorithm. The Roslyn C# extractor (Phase 4) resolves bases semantically, so
    `to` is the resolved symbol's full id (for example `class Synthetic.Orders.Document` for a
    written base `Document`). C# fixtures are written in idiomatic, unqualified C#.
- **D-19 (user, 2026-11-20, after the final plan check):** `TypeModel` in `vdiff-model/v1`
  carries an optional type-level `access` (reusing the member access enum) and a `partial`
  flag, so Phase 4 (accessibility, partials) and Phase 6 (public API ranked before internal)
  need no v2. A partial type spanning several files lists its files. The C++ tracer fills what
  it can and omits the rest. Both fields are shown at the 02-06 freeze review.
  — **Reversibility:** one-way (part of the frozen schema).
- **D-17:** Types are keyed by `(language, id)`. The managed-wrapper lane is the `language`
  value `cpp-cli`, with no separate `lane` field; the lane label is rendered from it.
  — **Reversibility:** one-way (part of the frozen schema).
- **D-18:** The D-10 ratchet reads `state: pending | green` for each output. A `due: <phase>`
  number sits alongside it as documentation only. The harness branches on `state` alone and
  takes no current-phase input.

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

### Folded Todos
- Backlog Phase 999.1 (Phase 01 deferred UAT follow-up, Test 10: off-Windows test portability).
  Folded through D-11, because the Linux CI leg needs it.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements and plan
- `docs/requirements/requirements.md` — the single source of requirements; R10 (model contents),
  R23 (determinism), D15 (public repository), Acceptance 1 to 3 (synthetic fixtures, private
  fixtures, determinism). It wins over `.planning/REQUIREMENTS.md` where they differ.
- `docs/plans/2026-11-16-001-feat-mvp-diff-class-diagram-plan.md` — unit U1 (model schema plus
  golden-fixture harness, "harness runs in CI; 5 fixtures red"). The architecture shows where the
  model sits between the extractors and the differ.
- `.planning/ROADMAP.md` § Phase 2 — the five success criteria, the coverage note on ACC-01
  turning green progressively (diff in Phase 5, nodes in 6, summaries in 7), and backlog 999.1.

### Requirements that shape the frozen contracts
- `docs/requirements/requirements.md` R8, R9 — the managed-wrapper lane, the mode labels, fallback
  reasons and unmodelled files, which the v1 model must have room for.
- `docs/requirements/requirements.md` R13, R14, R15 — diff states, `body-changed`, the rename
  score and its thresholds, and the package-pin node, which `vdiff-diff/v1` must express.
- `docs/requirements/requirements.md` R18 to R20 — the selector and summary format that later
  goldens follow.

### Phase 1 contracts being replaced
- `.planning/phases/01-offline-tracer/01-CONTEXT.md` — D-02 (read-only git access), D-07 (tracer
  diff rules), D-08 (output files, provisional `v0-tracer` schema).
- `src/Vdiff.Core/Model/ModelTypes.cs` — the current `v0-tracer` records that v1 replaces.
- `docs/decisions/ADR-001-cpp-tree-sitter-packaging.md` — the Rust sidecar behind the model
  contract; its JSON feeds `ModelBuilder`.
- `docs/spikes/S1-mermaid-in-ado-pr.md` — the spike that may revise the summary goldens' tag
  format.
- `docs/trials/phase-1-offline-tracer.md` — the pass/fail-only public record pattern that D-14
  reuses.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `tests/Vdiff.Tests/Support/SyntheticRepo.cs`: builds temporary git repositories with fixed
  author, committer and dates. The fixture harness builds each two-commit fixture repo with it.
- `src/Vdiff.Core/Output/OutputWriter.cs`: the shared `JsonSerializerOptions` (snake_case,
  indented, nulls omitted, trailing LF, UTF-8 without a BOM) and the temp-name-plus-move writes.
  v1 serialisation should keep this one serialiser.
- `tests/Vdiff.Tests/Output/DeterminismTests.cs`: already checks that runs are byte-identical and
  free of local paths at CLI level. Extend it for the "collections in any order give identical
  JSON" criterion.
- `tools/mermaid-check` plus `tests/Vdiff.Tests/Output/MermaidCliSyntaxTests.cs`: the pinned
  mermaid-cli syntax check for summaries.
- `src/Vdiff.Core/Cli/CliApp.cs` (`VDIFF_PROTECTED_DIRS`): the protection that keeps private-run
  output out of this repository.

### Established Patterns
- Canonical ordinal ordering of every collection (01-07), and every Mermaid class id prefixed
  with `C_` (01-11).
- Read-only git object access with hardened flags (Phase 1 D-02); private fixtures must keep it.
- Public-repository hygiene: target-name guard script run on every staged diff (see memory note);
  synthetic content only.
- `.gitattributes` pins shell scripts to LF; `azure-pipelines.yml` and fixture files should stay
  LF too, and golden files must be byte-exact (consider `-text` or `eol=lf` for
  `tests/fixtures/**/expected/*`).

### Integration Points
- `src/Vdiff.Core/Model/ModelBuilder.cs` and `src/Vdiff.Core/Diff/ModelDiffer.cs`: produce the v1
  shapes after the migration.
- `src/Vdiff.Core/Output/MermaidWriter.cs` and `HtmlWriter.cs`: read the diff and must follow the
  v1 field names.
- `src/Vdiff.Core/Pipeline/TracerPipeline.cs`: the entry point the harness can drive.
- `tests/Vdiff.Tests/Safety/RepoUnchangedMatrixTests.cs`, `tests/Vdiff.Tests/Trial/TraceRunScriptTests.cs`
  and `tests/Vdiff.Tests/GitObjectReaderTests.cs`: where the 999.1 fixes land.

</code_context>

<specifics>
## Specific Ideas

- The harness report should read as a table, one row per fixture and output with RED (pending) or
  GREEN, so a later phase can see at a glance which outputs it has turned green.
- Golden updates go only through the explicit accept mode followed by a reviewed diff, never by a
  test silently rewriting files.

</specifics>

<deferred>
## Deferred Ideas

- Connecting `azure-pipelines.yml` to an ADO organisation (personal org with hosted agents was the
  suggested route). Do this when an org is ready; until then "runs in CI" is verified locally.
- A macOS CI leg.
- Node-set goldens (Phase 6) and the S1-driven revision of the summary goldens (Phase 7 / S1).

</deferred>

---

*Phase: 02-model-contract-and-fixture-harness*
*Context gathered: 2026-11-19*
