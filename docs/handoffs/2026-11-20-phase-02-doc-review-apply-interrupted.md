---
artifact_contract: "ce-handoff/v1"
created_at: "2026-11-20T13:30:00Z"
title: "Phase 02 doc review applied halfway: D-20..D-23 recorded, plan edits interrupted by shutdown"
summary: "Finish applying the user-approved ce-doc-review outcome (3 fixes + D-20..D-23) to the Phase 2 plans; 02-03 and part of 02-12 are edited, the rest are not, so the plan set is inconsistent."
keywords: ["visualise-diffs", "phase-02", "ce-doc-review", "d-20", "d-21", "d-22", "d-23", "schema-freeze", "shutdown"]
cwd: "C:/projects/personal/visualise-diffs"
resume_focus: "Re-run the revision planner to finish applying the doc-review outcome, re-check, commit, then hand to /gsd-execute-phase 2."
repository: "CAPeddle/visualise-diffs"
repo_root_sha: "6797a240f02da158200194d0ab74aba326223672"
branch: "ccr-c12d6a45-a0eke4"
head: "6f35a3a"
---

# Phase 02: doc-review outcome half-applied

Supersedes `docs/handoffs/2026-11-20-phase-02-plan-phase-second-interruption.md`.

## State (all committed on `ccr-c12d6a45-a0eke4`, nothing pushed; the user pushes)

- `247c1ff`: `/gsd-plan-phase 2` completed. 12 plans in 8 waves; STATE.md says ready to execute.
  Requirements, decision and gap gates were all green at that point.
- The user then ran `/ce-doc-review` on the contract plans 02-03, 02-12, 02-05 and 02-06 and chose
  **"Auto-resolve with best judgment"**. That approves the three proposed fixes and my
  recommended action on all four decisions.
- `e142084`: the four decisions are recorded as **D-20..D-23** in `02-CONTEXT.md`. They are user
  decisions, one-way.
- `6f35a3a`: **WIP, inconsistent.** I stopped the revision planner for the shutdown after it had
  edited 02-03 and part of 02-12. 02-05, 02-06, 02-08, 02-10 and 02-VALIDATION.md are not yet
  updated. **Do not execute the phase until this is finished.**

## What still has to be applied

Re-run gsd-planner in revision mode. It should read the current 02-03 and 02-12 (already partly
edited) and complete the following.

**A. Three approved fixes**
1. 02-05 `FullDiff()`: every list has two or more distinct entries out of canonical order. That
   covers warnings, added and removed base changes, member modifiers before and after, an edge
   with `to`, and an association edge with `via` and no `to`. FullModel: one file entry sets
   `fallback_reason`. Add `ContractSamples.cs` to 02-06 files_modified.
2. 02-05 DiffPresence: add the package version rule (old_version for updated and removed,
   new_version for updated and added) and the `via`-only-on-association rule, with one negative
   row per direction.
3. 02-06 freeze "revise" path: allow edits to every src file that stops compiling. Add
   `Canonical.cs` to files_modified. `02-06-SUMMARY.md` is the only place the
   `schema-freeze: approved` line is recorded; add it to files_modified too.

**B. Decisions (see `02-CONTEXT.md`)**
- **D-20:** no type-level attribute pair in TypeDiff. Remove AccessBefore/AccessAfter across
  02-03, 02-12, 02-05, 02-06, 02-08 and 02-10. A type is modified only for base or member
  changes. ModelPresence and the D-19 model fields stay.
- **D-21:** repeated ids get `#<repo-relative path>`, plus `@<n>` only for a repeat within the
  same file. Same rule for member keys. Add a test that an unrelated earlier-sorting file
  leaves other ids unchanged.
- **D-22:** an Identity section with the per-language id and key grammar in the README and the
  schema descriptions, plus a freeze-review item. The C# fixture (02-10) gains `Foo`, `Foo<T>`
  and a nested type.
- **D-23:** edge identity is (kind, from, to_name, via), with `to` excluded. A modified edge
  carries `to_before`/`to_after`. The `renamed` edge state stays and means head-side ids. Add a
  test that an unrelated same-suffix type does not remove and re-add an edge.

The full planner brief I used is easy to rebuild from the two lists above. After revising:
re-measure the gate baselines and test counts, run `frontmatter.validate` and
`verify.plan-structure`, run a gsd-plan-checker pass, run the check-public-diff.sh guard, and
commit.

## Review observations not acted on (FYI, for the 02-06 freeze review or later)

- The schema hash pin reads raw bytes. Under core.autocrlf=true, hash after converting CRLF to
  LF.
- C++ out-of-line member bodies live in `.cpp` files, but a member has a single declaration span,
  so Phase 5 "body-changed" would miss them. Decide at the freeze alongside "members have no
  file".
- The hash pin can't tell an additive change from a tightening. A committed v1 corpus of the
  serialised contract samples would catch it.
- No rule says how the per-language extraction mode is derived when files mix modes.
- The run-meta schema is hash-pinned although it is volatile.
- Phases 6 and 7 writers need both models, not only the diff.
- There is no `lang` value for Objective-C++ (`.mm`).
- Enum underlying types produce base edges; Phase 5 should exclude them.

Rejected by me during synthesis (not shown to the user): removing ModelPresence, removing the
content tie-break in Canonical, and unordered schema-parity keys.

The cross-model pass did not run. Permission was denied for sending the plans to OpenAI via
Codex, so nothing covered the whole-document read.

## Machine-local leftovers (lost on reboot; nothing needed)

- Scratchpad: `C:\Users\cpeddle\AppData\Local\Temp\claude\8d115d4cf3d1104f\0c878cb8-228f-4c7e-a9bf-e1b5aed83a73\scratchpad\`
- Review bundle: `/tmp/compound-engineering-1060149/ce-doc-review/p02-contract-1795177590/bundle.md`
  (a concatenation of the four plans)
