# Phase 3: Git Layer - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-11-25
**Phase:** 03-git-layer
**Areas discussed:** Providing the two trees, Missing history and partial clones, PR context and skip rules, Renames and hunk mapping

---

## Providing the two trees

| Question | Options | Selected |
|---|---|---|
| How to provide trees | Export to temp dirs (Recommended) / Real git worktrees / Mode-dependent | Export to temp dirs |
| Head tree source | Always export PR source commit (Recommended) / Reuse checkout when it equals head / Use the merge commit as head | Always export PR source commit |
| Offline run switches too | Yes, one path for both (Recommended) / Not yet, switch in Phase 4 | Yes, one path for both |
| How much to export | Whole tree, raw bytes, bounded (Recommended) / Source files by extension only / You decide | Whole tree, raw bytes, bounded |
| Where | Temp directory, ADO-aware (Recommended) / Under --out / New --work flag, required | Temp directory, ADO-aware |
| Deletion | Always, with opt-in keep (Recommended) / Always, no keep option / Also sweep stale leftovers | Always, with opt-in keep |
| Unexportable file | Continue, list in-scope ones (Recommended) / Continue, list everything / Fail the run | Continue, list in-scope ones |
| Total limit | In-scope first, then stop (Recommended) / Fail with exit 1 / No total limit | In-scope first, then stop |

---

## Missing history and partial clones

| Question | Options | Selected |
|---|---|---|
| May the tool fetch | Never fetch, fail clearly (Recommended) / Fetch in PR mode only / Separate fetch step in the template | Never fetch, fail clearly |
| File unreadable on one side | Exclude it on both sides (Recommended) / Keep readable side, mark unknown / Fail the run | Exclude it on both sides |
| Objects missing (partial clone) | Any missing object fails (Recommended) / Fail only if in-scope objects are missing / Continue with exclusions | Any missing object fails |
| Merge-base none or several | None fails, several picks git's first (Recommended) / Both fail / Fall back to target tip | None fails, several picks git's first |

---

## PR context and skip rules

| Question | Options | Selected |
|---|---|---|
| Draft detection | REST lookup with the job token (Recommended) / Pipeline passes --draft / Rely on ADO's trigger rule | REST lookup with the job token |
| Draft lookup fails | Fail closed, exit 1 (Recommended) / Assume not draft, warn / Skip, exit 0 | Fail closed, exit 1 |
| Entering PR mode | Explicit --pr flag (Recommended) / Subcommands / Auto-detect from environment | Explicit --pr flag |
| What a skip leaves | Log line and run.meta.json (Recommended) / Log line only | Log line and run.meta.json |
| Target tip | Merge commit's first parent (Recommended) / origin/<target> ref / You decide | Merge commit's first parent (verify second parent is source commit, else origin/<target>) |

---

## Renames and hunk mapping

| Question | Options | Selected |
|---|---|---|
| Where rename pairs and hunks go | Internal only (Recommended) / New changes.json output | Internal only |
| File similarity threshold | Git default, 50%, fixed (Recommended) / Stricter, for example 70% / Configurable now | Git default, 50%, fixed |
| Change list coverage | All changed files (Recommended) / In-scope files only | All changed files |
| How hunks are computed | Zero-context, both sides, whitespace counts (Recommended) / As recommended, ignoring whitespace / Ignore line endings only | As recommended, ignoring whitespace |

**Notes:** The user chose to ignore whitespace in hunks over the recommended option. The skip and
merge-base records in run.meta.json were presented without noting that the frozen schema requires
both commit ids; that was noticed afterwards (CONTEXT D-16).

## Claude's Discretion

Not asked: missing or malformed PR variables, token handling, encoding of an unmodelled file's side.

## Deferred Ideas

- fetchDepth: 0 and the job token in the pipeline template (Phase 9).
- Whether dry-run skips the draft lookup (Phase 8).
