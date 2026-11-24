---
phase: 02-model-contract-and-fixture-harness
plan: 05
subsystem: contract
tags: [json-schema, njsonschema, draft-07, vdiff-model-v1, vdiff-diff-v1, vdiff-run-meta-v1, presence-rules]

requires:
  - phase: 02-model-contract-and-fixture-harness
    provides: "02-02 NJsonSchema 11.6.2, RepoRoot.Find(), LF rule for docs/schema/*.json; 02-03 v1 model records and Canonical; 02-11 RunMeta and run.meta.json; 02-12 v1 diff records and Canonical.EdgeIdentity"
provides:
  - "docs/schema/vdiff-model-v1.schema.json, vdiff-diff-v1.schema.json and vdiff-run-meta-v1.schema.json in the draft-07 keyword subset NJsonSchema enforces"
  - "docs/schema/README.md: keyword subset, evolution policy, presence rules, Edge resolution, Type access and partial types, Identity grammar, Edge identity, draft status line"
  - "SchemaSupport (Load, ReadText, Errors, SchemaFileFor, ValidateOutputs) for the harness plans"
  - "ContractSamples (FullModel, FullDiff, FullMeta, EmptyModel, EmptyDiff) with every optional property set and every list out of canonical order"
  - "DiffPresence.Problems(DiffFile) and ModelPresence.Problems(ModelFile): the rules the schema subset cannot express, each with a sole-problem negative row"
  - "SchemaValidationTests.AllowedKeywords and KeywordsUsed for the 02-06 freeze record"
affects: [02-06, 02-07, 02-08, 02-09, 02-10, 04, 05, 06, 07]

actuals:
  tokens: 25873
  tasks: 3
  commits: 5
plan_head_before: d89b5f474334fec911beb4c7003bcb742d141e71
plan_head_after: 3e2f22e2d88de486e5ebf3a9aefc234e8fa5dae6

tech-stack:
  added: []
  patterns:
    - "Schema files are hand-written, closed (additionalProperties false) and self-contained; the records are the source of truth for names, order and required"
    - "Presence checkers return tagged fixed-text problems; each rule has a row built from the clean sample that must report exactly that one problem"
    - "A negative theory row carries the keyword it exercises and the NJsonSchema error kind it must produce, so a guard can prove every used keyword has a rejecting document"

key-files:
  created:
    - docs/schema/vdiff-model-v1.schema.json
    - docs/schema/vdiff-diff-v1.schema.json
    - docs/schema/vdiff-run-meta-v1.schema.json
    - docs/schema/README.md
    - tests/Vdiff.Tests/Contract/SchemaSupport.cs
    - tests/Vdiff.Tests/Contract/SchemaValidationTests.cs
    - tests/Vdiff.Tests/Contract/SchemaParityTests.cs
    - tests/Vdiff.Tests/Contract/ContractSamples.cs
    - tests/Vdiff.Tests/Contract/DiffPresence.cs
    - tests/Vdiff.Tests/Contract/DiffPresenceTests.cs
    - tests/Vdiff.Tests/Contract/ModelPresence.cs
    - tests/Vdiff.Tests/Contract/ModelPresenceTests.cs
  modified: []

key-decisions:
  - "The records and the plan's Contract tables agree everywhere; nothing was bent. The schemas are a field-by-field transcription of the records in declaration order"
  - "DiffPresence treats renamed_from, rename and a member's renamed_from_key as present exactly for state renamed (both directions), where 02-03 says present only for renamed"
  - "base_file and head_file are only required when the side exists (02-03 wording); they are not forbidden on the other side, so a golden is not rejected for something the frozen text does not forbid"
  - "Negative rows assert the NJsonSchema error kind, not just a non-empty error list, so a row cannot pass for the wrong reason"

patterns-established:
  - "Sole-problem rows: a mutation never flips a state or appends an edge, which would also put a summary count off by one"
  - "Schema descriptions carry the rules the subset cannot express; the README and a checker repeat them"

requirements-completed: [R-10]

coverage:
  - id: D1
    description: "Three v1 schema files in the allowed keyword subset load offline and a real tracer run's base.model.json, head.model.json, diff.json and run.meta.json validate against them (D-03, R-10)"
    requirement: "R-10"
    verification:
      - kind: e2e
        ref: "tests/Vdiff.Tests/Contract/SchemaValidationTests.cs#Tracer_output_validates_against_the_v1_schemas"
        status: pass
      - kind: unit
        ref: "tests/Vdiff.Tests/Contract/SchemaValidationTests.cs#The_three_schema_files_load_offline, #Every_schema_file_uses_only_allowed_keywords"
        status: pass
    human_judgment: false
  - id: D2
    description: "Records and schemas are pinned to each other in both directions: same definitions, same property names in order, required equals the non-nullable properties, and the type-level and diff access enums equal the member access enum (D-01, D-19, D-20)"
    requirement: "R-10"
    verification:
      - kind: unit
        ref: "tests/Vdiff.Tests/Contract/SchemaParityTests.cs#Schema_and_records_agree_in_both_directions, #Enum_values_match_the_constants_and_the_three_access_enums_are_identical"
        status: pass
    human_judgment: false
  - id: D3
    description: "Every validating keyword the schemas use has a negative document that NJsonSchema rejects with the expected error kind, and a guard fails when a used keyword has no row; empty documents validate and omitted lists are rejected (D-15, T-02-13)"
    requirement: "R-10"
    verification:
      - kind: unit
        ref: "tests/Vdiff.Tests/Contract/SchemaValidationTests.cs#A_document_that_breaks_the_keyword_is_rejected (13 rows), #Every_validating_keyword_a_schema_uses_has_a_negative_row, #An_empty_model_and_an_empty_diff_validate, #A_model_or_a_diff_with_the_types_list_omitted_is_rejected, #The_full_samples_with_every_optional_property_set_validate"
        status: pass
    human_judgment: false
  - id: D4
    description: "DiffPresence enforces the diff rules the subset cannot express (member before and after fields, files per state, rename fields, counts, unchanged never listed, score formula, package versions, via, the six D-23 edge rules), each by a sole-problem row, and a modified edge with one side unresolved is clean"
    requirement: "R-10"
    verification:
      - kind: unit
        ref: "tests/Vdiff.Tests/Contract/DiffPresenceTests.cs#Breaking_one_rule_reports_that_rule_and_nothing_else (23 rows), #The_full_and_the_empty_diff_report_no_problems, #A_modified_edge_with_one_side_unresolved_is_clean"
        status: pass
    human_judgment: false
  - id: D5
    description: "ModelPresence enforces the D-19 type rules and the edge via rule by sole-problem rows; the canonical sample, the empty model and both models of a real tracer run report no problems"
    requirement: "R-10"
    verification:
      - kind: unit
        ref: "tests/Vdiff.Tests/Contract/ModelPresenceTests.cs (10 tests)"
        status: pass
      - kind: e2e
        ref: "tests/Vdiff.Tests/Contract/SchemaValidationTests.cs#Tracer_models_keep_the_model_presence_rules"
        status: pass
    human_judgment: false
  - id: D6
    description: "docs/schema/README.md documents the keyword subset, evolution policy, presence rules, per-extractor edge resolution, type access and partial types, the Identity grammar for cpp, csharp and cpp-cli, edge identity, and a draft status line; every id and key description carries Identity and the repeated-id rule"
    requirement: "R-10"
    verification:
      - kind: unit
        ref: "tests/Vdiff.Tests/Contract/SchemaValidationTests.cs#The_identity_grammar_is_documented_in_the_readme_and_the_id_and_key_descriptions"
        status: pass
    human_judgment: true
    rationale: "That the grammar is the right one for the Phase 4 and Phase 7 extractors is the 02-06 freeze review; the test proves only that it is present and names all three languages"

duration: 28min
completed: 2026-11-21
status: complete
---

# Phase 2 Plan 05: v1 schema files, parity, keyword negatives and presence checkers Summary

**Three draft-07-subset schema files (`vdiff-model/v1`, `vdiff-diff/v1`, `vdiff-run-meta/v1`) with a README holding the Identity grammar and edge identity, a tracer run that validates against all of them, parity tests in both directions, one rejecting document per keyword, and `DiffPresence` and `ModelPresence` checkers for the rules the subset cannot express.**

## Performance

- **Duration:** 28 min of wall clock, of which the full suite took 15 min 34 s
- **Started:** 2026-11-21T08:20:18Z
- **Completed:** 2026-11-21T08:48:11Z
- **Tasks:** 3 (task 1 tracer, tasks 2 and 3 each as RED, GREEN)
- **Files modified:** 12 (all created)

## Accomplishments

- The three schema files are hand-written in declaration order from the v1 records: closed objects, `required` equal to the non-nullable properties, single-value `enum` for each schema name, `maximum` 1000 on `score_milli`, no `$id`, only local `$ref`. The diff schema has 15 closed objects and the model schema 10.
- `docs/schema/README.md` has the sections the plan lists (keyword subset, evolution policy, presence rules, Edge resolution, Type access and partial types, Identity, Edge identity) and the line `Status: draft, pending the freeze review in plan 02-06.`. The `id` and `key` descriptions in both schema files carry the compact grammar, and the model and diff `TypeModel.id`, `MemberModel.key`, `TypeDiff.id` and `MemberDiff.key` name `cpp`, `csharp` and `cpp-cli`.
- A real two-commit tracer run (`synthetic` namespace, a base that resolves inside the file, a member that changes access, an added header, a removed header) validates for `base.model.json`, `head.model.json`, `diff.json` and `run.meta.json`, and the head model has a `base` edge with a `to`.
- `SchemaParityTests` compares reachable records with definitions, property names in order and `required` against `NullabilityInfoContext`, for the model, diff and run-meta schemas; an enum fact proves the type-level `access` and the `MemberDiff` `access_before`/`access_after` enums equal the member access enum.
- The keyword allow-list, the walker's own negative fact, a guard (every used validating keyword has a row) and 13 negative rows, each asserting the NJsonSchema error kind.
- `DiffPresence` (11 rules grouped as member presence, files, rename fields, counts, unchanged-listed, score, package versions, via, and the six D-23 edge rules) and `ModelPresence` (D-19 type rules and the edge `via` rule), each rule proven by a row that must be the only problem reported.

## Task Commits

1. **Task 1 (tracer): three schema files, schema helper, tracer output validated** - `6cd5b31` (feat)
2. **Task 2 RED: failing tests for diff presence, parity and keyword negatives** - `efb7dda` (test)
3. **Task 2 GREEN: DiffPresence rules** - `9da01d3` (feat)
4. **Task 3 RED: failing tests for model presence and the tracer models** - `5719b49` (test)
5. **Task 3 GREEN: ModelPresence rules** - `3e2f22e` (feat)

**Plan metadata:** committed separately as `docs(02-05): complete v1 schema files plan` and `docs(02-05): update state and roadmap`.

## Test counts (orchestrator check, measured with `--no-build` after the last code commit)

| Class | Planned | Actual |
|-------|---------|--------|
| DiffPresenceTests | 25 | 25 |
| SchemaValidationTests | 23 (plus task 3's tracer fact) | 24 |
| SchemaParityTests | 4 | 4 |
| ModelPresenceTests | at least 10 | 10 |
| The four classes together | at least 52 (task 2 gate floor 49) | 63 |

- The task 2 gate run (`SchemaParityTests|SchemaValidationTests|DiffPresenceTests`) printed `Passed: 52` at `9da01d3`: 23 + 4 + 25, exactly the plan's count. Task 3 then added `Tracer_models_keep_the_model_presence_rules` to `SchemaValidationTests`, which makes 24. There is no per-class shortfall.
- `--filter "Category=Contract"` prints `Passed: 81`, not 63: the trait is also on `PipelineYamlTests` (18, plan 02-02). 63 + 18 = 81.
- Full suite `--filter "Category!=MermaidCli"` at `3e2f22e`: `Passed: 711, Failed: 0, Total: 711` (15 m 34 s). 648 after 02-12 plus these 63. The `TraceRunScriptTests` flake did not recur.

## TDD Gate Compliance

`workflow.tdd_mode` is not enabled and the plan is `type: execute`, so `gsd_run check tdd-red-evidence` was not run and no evidence file was written.

- **Task 2 RED** (`efb7dda`): `ContractSamples`, the parity and keyword tests and 25 `DiffPresence` tests against a stub `Problems` that returns an empty list. Run: `Failed: 23, Passed: 29, Total: 52`. All 23 failures are the sole-problem rows, each `Assert.Single() Failure: The collection was empty`. Semantic assessment: every target ran and failed on the planned assertion (the checker reports nothing), not on compile, discovery or setup. The 29 that passed are the 23 `SchemaValidationTests`, 4 `SchemaParityTests` and the two clean-sample `DiffPresenceTests` facts. The schema facts passed at once because task 1 had already written the schemas, and the two clean checks passed vacuously against the stub; neither is RED evidence.
- **Task 2 GREEN** (`9da01d3`): `DiffPresence`. `Passed: 52, Failed: 0`.
- **Task 3 RED** (`5719b49`): `ModelPresenceTests` (2 clean checks, 8 rows) and the tracer fact against a stub. Run: `Failed: 8, Passed: 3, Total: 11`. All 8 failures are `Assert.Single() Failure: The collection was empty`. Semantic assessment: same as above. The two clean checks and `Tracer_models_keep_the_model_presence_rules` passed vacuously against the stub, so the tracer fact only became a real check at GREEN.
- **Task 3 GREEN** (`3e2f22e`): `ModelPresence`. `ModelPresenceTests` `Passed: 10`, the tracer fact alone `Passed: 1`.
- **REFACTOR:** none.

## Tracer gate

Task 1 is `type="tracer"`, its `<verify>` is automated-only and `human_verify_mode` is `end-of-phase`, so the gate was a re-run: `SchemaValidationTests` passed 4 of 4, and the closed-object count in the diff schema is 15. Tracer verified end to end, expansion (tasks 2 and 3) followed.

## Records against the plan's Contract tables

No disagreement was found between `ModelTypes.cs`, `RunMeta.cs` and the 02-03 Contract tables, so no schema bends either. Beyond the tables the schemas add the constraints below. None is a new keyword, and all hold for every value the tracer and the plans' samples produce, but each is stricter than the table text and goes on the 02-06 freeze review list.

- `minLength` 1 on `TypeDiff.id`, `TypeDiff.fqn`, `MemberDiff.key`, `MemberDiff.name`, `EdgeDiff.to_name`, `EdgeDiff.via`, `TypeDiff.base_file`, `TypeDiff.head_file` and the run-meta `ExtractorRun.name`, `ToolVersion.tool` and `ToolVersion.version` (the tables mark only the model-side counterparts and `RenameSource` as non-empty).
- `uniqueItems` and the code pattern on `MemberDiff.modifiers_before` and `modifiers_after`, matching the model's member `modifiers`.
- `SourceSpan` is described as "lines counted from 1, end byte exclusive". That is what the sidecar writes (`start_position().row + 1`); the table says only `>= 0`, and the schema keeps `minimum` 0.
- `DiffPresence` requires `renamed_from`, `rename` and `renamed_from_key` for state renamed and forbids them otherwise ("exactly"), where 02-03 words it "only for".

## Closed values checked against the extractors

The schemas close several value sets that the tracer run in task 1 cannot exercise and the string-typed records cannot pin, so these were compared against the producers:

- Type kinds from the sidecar are `class`, `struct`, `enum`; member kinds `method`, `field`, `alias`, `enumerator`; member access `public`, `protected`, `private`, and `none` for an enumerator. All are in the schema enums (`none` is why it stays legal for members).
- Every unmodelled reason built in `src/Vdiff.Core` (`non-utf8-path`, `object-missing`, `not-a-blob`, `too-large`, `total-too-large`, `parse-timeout`, `parse-error`) matches `^[a-z][a-z0-9-]*$`. The tracer sets no `fallback_reason`.
- `macros_blanked` is `Distinct`ed in `ModelBuilder` before it reaches the model, so `uniqueItems` holds for a repeated `--blank`.

## Files Created/Modified

- `docs/schema/vdiff-model-v1.schema.json`, `vdiff-diff-v1.schema.json`, `vdiff-run-meta-v1.schema.json` - the three contracts
- `docs/schema/README.md` - keyword subset, evolution policy, presence rules, Identity grammar, edge identity, status
- `tests/Vdiff.Tests/Contract/SchemaSupport.cs` - `Load`, `ReadText`, `Errors`, `SchemaFileFor`, `ValidateOutputs`
- `tests/Vdiff.Tests/Contract/ContractSamples.cs` - full and empty samples
- `tests/Vdiff.Tests/Contract/SchemaValidationTests.cs`, `SchemaParityTests.cs`, `DiffPresenceTests.cs`, `ModelPresenceTests.cs` - the contract tests
- `tests/Vdiff.Tests/Contract/DiffPresence.cs`, `ModelPresence.cs` - the checkers

## Decisions Made

See `key-decisions`. In addition: `SchemaSupport.ReadText` was added (not in the plan's API table) because the keyword walker and the identity fact read the schema text, and `Errors` flattens `ChildSchemaValidationError` children so a failure inside an array item reports its leaf kind and path.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Acceptance] ContractSamples did not name `ToBefore`**
- **Found during:** Task 2 acceptance greps
- **Issue:** `grep -c "ToBefore" ContractSamples.cs` returned 0 because the modified edge was built with positional arguments.
- **Fix:** the modified edge now sets `To`, `ToBefore`, `ToAfter`, `ToName`, `Via` and `State` by name.
- **Files modified:** tests/Vdiff.Tests/Contract/ContractSamples.cs
- **Committed in:** `5719b49` (Task 3 RED), after Task 2's GREEN `9da01d3`. The file is in task 2's list, not task 3's.

**2. [Rule 2 - Missing coverage] `SchemaValidationTests` reaches 24, not 23**
- **Found during:** Task 3
- **Issue:** the plan's task 2 floor counts 23 for `SchemaValidationTests` and task 3 adds one more fact to the same class.
- **Fix:** none needed; reported in the count table.

---

**Total deviations:** 2 (1 acceptance, 1 count note). **Impact on plan:** none beyond the stricter-than-table constraints listed above.

## Issues Encountered

- A bash heredoc holding C# string literals failed to parse (the same class of problem 02-03 and 02-12 hit); the patch was moved to files in the scratchpad and applied with a script.
- The full suite was started with a trailing `&` inside a background command, so the first completion notice arrived at once; only the output file's `Passed!` line and `done` sentinel were trusted.

## Known Stubs

None. The RED-phase stubs of `DiffPresence` and `ModelPresence` were replaced in their GREEN commits.

## Deferred Items

- The README open item stays open for the 02-06 freeze review: `MemberModel` has no `file`, so a member declared in another part of a partial type cannot be placed in its file from the model alone (an optional member `file` would be additive inside v1).
- `DiffFile`'s doc comment in `ModelTypes.cs` still reads "the summary and the changed types only" (carried from 02-12); no source change was made in this plan.

## Threat Flags

None. T-02-13 (allow-list, one rejecting row per keyword, coverage guard), T-02-14 (parity in both directions, full samples that must validate), T-02-15 (`SchemaSupport.Errors` reports kind and path only) and T-02-39 (both presence checkers with a sole-problem row per rule, run on the tracer's real models) are covered.

## Next Phase Readiness

- 02-06 can freeze: the schema files, README and the keyword record (`AllowedKeywords`, `KeywordsUsed`) are in place. The freeze review has the list under "Records against the plan's Contract tables" and the draft status line to flip.
- 02-07 and later can call `SchemaSupport.ValidateOutputs(outDir)`; 02-08 and 02-10 can call `DiffPresence.Problems` on every golden and cite README Identity; Phase 4 extractor tests can call `ModelPresence.Problems`.
- No builds or tests are running; `obj/` and `bin/` are free.

## Self-Check: PASSED

- All 12 created files exist and hold LF only (CR count 0).
- Commits `6cd5b31`, `efb7dda`, `9da01d3`, `5719b49`, `3e2f22e` are ancestors of HEAD; `git rev-list --count` from the recorded base `d89b5f4` gives 5.
- Acceptance greps for all three tasks were re-run on the final files (diff schema closed objects 15, model 10, `access_before` 1, `to_before` 1, `to_after` 1, `^## Identity` 1, `#<path>` 3, `ToBefore` in the samples at the final state, `Canonical.Canonicalise` in `ModelPresenceTests` 1). The plan verification ran: full suite 711 of 711, `git ls-files docs/schema` lists the three schemas and the README.

---
*Phase: 02-model-contract-and-fixture-harness*
*Completed: 2026-11-21*
