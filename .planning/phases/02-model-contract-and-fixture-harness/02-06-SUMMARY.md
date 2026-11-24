---
phase: 02-model-contract-and-fixture-harness
plan: 06
subsystem: contract
tags: [determinism, canonical-ordering, seeded-shuffle, schema-freeze, sha256-pin, vdiff-model-v1, vdiff-diff-v1, vdiff-run-meta-v1]

requires:
  - phase: 02-model-contract-and-fixture-harness
    provides: "02-03 Canonical and the model records; 02-05 the three schema files, README, ContractSamples and the presence checkers; 02-12 the diff records and ModelDiffer"
provides:
  - "CanonicalSerialisationTests: seeded Fisher-Yates proof that collections in any order serialise to identical bytes for the model, the diff and the run metadata (success criterion 4, R-23)"
  - "A reflection guard that runs its own 25 seeds and names any reachable list that was never permuted"
  - "The recorded owner answer that froze vdiff-model/v1, vdiff-diff/v1 and vdiff-run-meta/v1"
  - "SchemaFreezeTests: SHA-256 pins of the three schema files over LF-normalised text, a file-set guard and a README status check"
  - "docs/schema/README.md status line: frozen on 2026-11-21"
affects: [02-07, 02-08, 02-09, 02-10, 04, 05, 06, 07]

actuals:
  tokens: 4300
  tasks: 3
  commits: 4
plan_head_before: 0fa93d74e5c163d2799eee094be5b118602f9322
plan_head_after: af8ff73cafacfe3dc092d4845e0ffbf9b6e4e388

tech-stack:
  added: []
  patterns:
    - "A shuffle proof needs a coverage guard: the guard runs its own seeds into a local set so it passes alone and in any order"
    - "A frozen file is pinned by SHA-256 over text with CRLF turned into LF, with the evolution policy in the failure message"

key-files:
  created:
    - tests/Vdiff.Tests/Contract/CanonicalSerialisationTests.cs
    - tests/Vdiff.Tests/Contract/SchemaFreezeTests.cs
  modified:
    - docs/schema/README.md

key-decisions:
  - "The three schema files froze exactly as plan 02-05 wrote them; the owner's only change was README wording"
  - "The freeze hash is computed over the file text with CRLF normalised to LF, not over raw bytes (the .gitattributes eol=lf rule already keeps the files LF; this is defence in depth)"
  - "No change to Canonical.cs or ContractSamples.cs was needed: every behaviour passed on a green tree, so the comparers already break ties by content and every sample list was already permutable"

patterns-established:
  - "Non-vacuity check for an ordering test: break one comparer, see the test fail, revert"

requirements-completed: [R-23, R-10]

duration: 2 sessions, split by the blocking freeze checkpoint
completed: 2026-11-21
status: complete
---

# Phase 2 Plan 06: Canonical serialisation proof and schema freeze Summary

**Collections in any order now serialise to identical bytes (25 seeds over the model, diff and run metadata, with a guard that fails on an unpermuted list), and the three v1 schema files are frozen by the owner's answer and pinned by SHA-256.**

## Accomplishments

- Task 1 (tracer, `cf065b2`): `CanonicalSerialisationTests` with 29 tests. A seeded Fisher-Yates shuffle runs over every list in `ContractSamples.FullModel()`, `FullDiff()` and `FullMeta()` for seeds 1 to 25 and compares the `OutputWriter.Serialize` bytes with the unshuffled record. The guard fact `The_25_seeds_permute_every_reachable_list` runs its own seeds into a local set and passes when run alone. Builder order, differ order and the content tie-break (two types, two members, two diff types equal on every canonical key) are separate facts. Non-vacuity was checked by breaking the `Edges` sort: 13 tests failed, then the change was reverted.
- Task 2 (checkpoint, answered): the owner reviewed the three schema files and the README and answered "freeze" on 2026-11-21, with README wording fixes. Recorded under `## Schema freeze`.
- Task 3 (`af8ff73`): README status flipped to `Status: frozen on 2026-11-21 after review (plan 02-06).` and `SchemaFreezeTests` added (5 tests: three hash pins, the file-set guard, the README status check).

## Schema freeze

schema-freeze: approved 2026-11-21

readme-fix: `docs/schema/README.md` "Presence rules" now says `renamed_from`, `rename` and a member's `renamed_from_key` are present exactly for state `renamed` and absent otherwise (commit `291788b`), as `DiffPresence` enforces in both directions; it said "only for" before.

The README edit changed wording only: no schema file, Identity rule, evolution-policy sentence or rule meaning changed, so it is not a `revision`. I checked every other presence rule in the README against `DiffPresence` and `ModelPresence` (type files, member before and after fields, package versions, via, edge rules, counts, unchanged-never-listed, the five ModelPresence rules) and found no other understated rule. `base_file` and `head_file` are stated as "present when the side exists" and the checker enforces exactly that, not absence on the other side.

Pinned hashes (SHA-256 of the LF-normalised text):

| File | SHA-256 |
|------|---------|
| `docs/schema/vdiff-model-v1.schema.json` | `C62EA541...EC66A72` |
| `docs/schema/vdiff-diff-v1.schema.json` | `1BB22E1D...F30C479A` |
| `docs/schema/vdiff-run-meta-v1.schema.json` | `47964D1D...EA2F2A` |

## Verification

- `CanonicalSerialisationTests`: 29 passed; the guard fact passes alone.
- `SchemaFreezeTests`: 5 passed.
- Full suite once after the last code commit, `dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --filter "Category!=MermaidCli"`: Failed 0, Passed 745, Total 745 (17 min 1 s). This includes DeterminismTests.
- `git ls-files --eol` shows the three schema files `i/lf w/lf` with `attr/text eol=lf`.

## Deviations from Plan

None - plan executed exactly as written. The only owner-directed change was the README wording fix at the checkpoint, listed above.

## Deferred Items

- `DiffFile`'s doc comment in `src/Vdiff.Core/Model/ModelTypes.cs` is stale ("the summary and the changed types only"; the diff also carries packages and edges). Not edited here, because `ModelTypes.cs` is outside this path.
- `docs/schema/README.md` still ends the "Type access and partial types" section with an "Open item for the freeze review" paragraph (optional `MemberModel.file`). The owner froze without adding it; it stays a documented additive option inside v1. The paragraph name now reads as stale. Task 3 limited README edits to the status line and the owner's wording fix, so it was left alone; a later phase that settles the question can reword it.

## Known Stubs

None.

## Threat Flags

None. This plan adds tests and a status line only.

## Self-Check: PASSED

- `tests/Vdiff.Tests/Contract/CanonicalSerialisationTests.cs`, `tests/Vdiff.Tests/Contract/SchemaFreezeTests.cs`: present.
- Commits `cf065b2`, `c131be1`, `291788b`, `af8ff73` are ancestors of HEAD.
- Exactly one line starting `schema-freeze: approved` in this file.
