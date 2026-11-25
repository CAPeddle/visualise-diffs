# Phase 3: Git Layer - Context

**Gathered:** 2026-11-25
**Status:** Ready for planning

<domain>
## Phase Boundary

Given an Azure DevOps PR context, the tool resolves the two commits to compare (head = PR source
commit, base = merge-base of the target tip and head), exports both trees, lists changed files,
finds git rename pairs with a similarity score, maps diff hunks to source spans, and skips draft
and fork PRs with exit 0 and no thread. The offline run uses the same trees. Requirements: R-01
(head/base resolution and both trees), R-04.

Not in this phase: new extraction or whole-repo extraction (Phase 4), `.visualise-diffs.yml`
(Phase 4), rename scoring and `body-changed` (Phase 5), posting threads and dry-run (Phase 8), the
pipeline template (Phase 9).

</domain>

<decisions>
## Implementation Decisions

### Providing the two trees
- **D-01:** Each commit's tree is exported from git objects into a temp directory. No
  `git worktree`, no checkout; the analysed repository's `.git`, index, refs and working tree stay
  untouched in every mode, so Phase 1 D-02's no-write rule holds.
- **D-02:** Head is always the exported PR source commit, whatever the agent has checked out (a PR
  build's `Build.SourceVersion` is the merge commit). The checkout on disk is never analysed.
- **D-03:** Offline (`--base`/`--head`) and PR mode use the same exported trees. The tracer stops
  passing blobs straight from git objects to the sidecar. Extraction still covers changed in-scope
  C++ files only in this phase.
- **D-04:** The whole tree is exported as raw stored bytes (no autocrlf, filters or LFS smudge),
  under size limits. Symlinks and submodules are not followed.
- **D-05:** The trees go in a temp directory: `Agent.TempDirectory` in PR mode, the system temp
  offline. The location passes the same containment checks as `--out` (never inside the analysed or
  the vdiff repository).
- **D-06:** The trees are always deleted, on success and failure. An opt-in `--keep-trees` keeps
  them and prints the path. No sweep of leftovers from earlier runs.
- **D-07:** A file that cannot be exported (over the per-file limit, symlink, submodule, path
  illegal on this OS, case collision) is skipped with a fixed reason and the run continues.
  In-scope code files are listed as unmodelled; out-of-scope ones are only counted.
- **D-08:** At the total size limit, changed files are exported first, then other source and project
  files, then the rest, up to a raised total; what is left out is reported. Research sets the
  figures. The order must be deterministic (R-23; a consequence, not asked).

### Missing history and unreadable files
- **D-09:** The tool never fetches, in any mode. Missing history gives exit 1 with a message naming
  the fix (a full checkout, `fetchDepth: 0`). No git credentials are needed.
- **D-10:** Any missing object means an incomplete clone: exit 1 with a "fetch a full clone"
  message, for out-of-scope files too. A submodule commit is not in the repository by design and is
  not a missing object (clarification, not asked).
- **D-11:** A file unreadable on one side for any other reason (D-07, D-08) is excluded from both
  models and listed as unmodelled with the reason and the side. The report warns that a type moved
  out of an excluded file can still be misjudged. Two consequences, not asked: the exclusion also
  covers a path left out of one exported tree (otherwise Phase 4's whole-repo extraction brings the
  false `added` types back) and both paths of a git rename pair.
- **D-12:** No merge-base: exit 1 with a clear message. Several: use the one `git merge-base`
  returns and record in `run.meta.json` that there were others (schema change, see D-16).

### PR context and skip rules
- **D-13:** PR mode is entered only by an explicit `--pr` flag (`vdiff --pr --repo <path> --out
  <dir>`). Combining it with `--base` or `--head` is an error. ADO variables never change the
  meaning of an offline run.
- **D-14:** Draft state comes from one read-only REST GET on the PR with `System.AccessToken`,
  because ADO documents no draft variable. This is the tool's first ADO contact; Phase 8 can reuse
  the client. Fork state comes from `System.PullRequest.IsFork` (taken from the docs, not asked).
  Nothing is posted.
- **D-15:** If the draft lookup fails (no token, 401/403, network, timeout), fail closed: exit 1,
  no thread.
- **D-16:** A skipped PR exits 0, with one fixed log line and a `run.meta.json` carrying a skip
  reason, and no model, diff or report files. **Conflict to resolve in planning:** the user chose
  this on the understanding that it is additive in v1. An optional skip reason is, but
  `vdiff-run-meta-v1` requires `base_commit` and `head_commit`, and a skip can be decided before a
  merge-base exists (resolving first would make a skip fail on a shallow clone, against R-04). The
  schema is pinned by SHA-256 in `SchemaFreezeTests`, so this and D-12 need a re-pin and owner
  sign-off at a `checkpoint:decision`. — **Reversibility:** one-way — changes the frozen,
  hash-pinned run-meta contract that later phases read.
- **D-17:** In PR mode the target tip comes from the merge commit (`Build.SourceVersion`), which
  pins the base across re-runs of one build: its first parent, after verifying that the second
  parent is `SourceCommitId`; otherwise fall back to `origin/<target>`.

### Renames and hunks
- **D-18:** Rename pairs and hunks are internal records on the pipeline result, tested on a scripted
  repo. No new output file or schema; Phase 5 decides what reaches `diff.json`.
- **D-19:** The file rename threshold is git's default 50%, fixed and passed explicitly so user git
  config cannot change it. The similarity score is kept on each pair.
- **D-20:** The change list and rename detection cover all changed files in the repository;
  consumers filter by scope. The rename limit is set explicitly and hitting it is reported.
- **D-21:** Hunks are zero-context line ranges kept for both sides; a pure insertion or deletion
  maps to a position on the other side. **Whitespace is ignored**, so re-indenting or a CRLF change
  never marks a member `body-changed`. Consequences: a changed file can have no hunks, and hunks can
  differ from what ADO shows. The exact git whitespace option is for research.

### Superseded earlier decisions
- ROADMAP Phase 3 criterion 1 "worktree of the base commit" is met by exported trees (D-01).
- Phase 1 criterion 2 and D-03, "everything under `--out`": temp trees are now written outside it
  and deleted unless kept (D-05, D-06). The repository-unchanged guarantee stands.
- Phase 2 D-13, "copies no target source": a private run now writes target source to temp.
  `docs/how-to/private-fixtures.md` must say so.
- Phase 1's `object-missing` unmodelled reason becomes exit 1 (D-10); existing tests change.
- Phase 1 D-02's `--no-renames` no longer applies to change listing (D-20). The hardened flags do.

### Claude's Discretion
Not asked in the discussion, left to research and planning:
- Behaviour when PR variables are missing or malformed (`SourceCommitId`, `TargetBranch`, `IsFork`,
  PR id). Failing closed with exit 1 matches D-15.
- Token handling, REST timeout and client shape; the token must never reach logs, output files or a
  git subprocess.
- How the side is encoded for an unmodelled file: `UnmodelledEntry` is closed to `path` and
  `reason`, and `reason` is a free kebab-case code, so putting the side in the code avoids a schema
  change.

### Research items (unverified, do not treat as facts)
- Parent order of ADO's merge commit (D-17). Safer form: the parent that is not `SourceCommitId`.
- That the merge-base choice matches ADO's PR diff, including criss-cross histories (D-12).
- The REST `isDraft` field and the token scope it needs (D-14).
- Documented by Microsoft: `SourceCommitId` and `TargetBranch` exist only for branch-policy YAML
  builds, and draft PRs don't trigger policy builds. A manually queued run may lack the PR id.
- Export time and size against the 5-minute P90 budget (D-04, D-08).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements and plan
- `docs/requirements/requirements.md` — glossary (base, head), R1, R4, R13, R14, R23, R26, R27, D15
- `docs/plans/2026-11-16-001-feat-mvp-diff-class-diagram-plan.md` — unit U2
- `.planning/ROADMAP.md` § Phase 3 — success criteria and the Phase 2 UAT input (2026-11-23)

### Contracts and earlier decisions
- `docs/schema/README.md` — evolution policy and presence rules
- `docs/schema/vdiff-run-meta-v1.schema.json` — frozen; affected by D-12 and D-16
- `docs/schema/vdiff-model-v1.schema.json` — `UnmodelledEntry`
- `.planning/phases/01-offline-tracer/01-CONTEXT.md` — D-02, D-03 and D-10
- `.planning/phases/02-model-contract-and-fixture-harness/02-CONTEXT.md` — D-13, D-14
- `docs/how-to/private-fixtures.md` — needs updating

### Azure DevOps
- https://learn.microsoft.com/en-us/azure/devops/pipelines/build/variables?view=azure-devops&tabs=yaml — PR variables
- https://learn.microsoft.com/en-us/azure/devops/pipelines/repos/azure-repos-git?view=azure-devops — draft, fork and shallow-fetch behaviour

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `src/Vdiff.Core/Git/GitObjectReader.cs`: hardened read-only git calls, `diff-tree` parsing, sized
  two-pass `cat-file` reads (4 MB per blob, 256 MB total), git 2.45 minimum. It reads a whole batch
  into memory, which does not suit a whole-tree export.
- `src/Vdiff.Core/Process/BoundedProcess.cs`: runs the git reader's subprocesses.
- `src/Vdiff.Core/Output/HeldDirectory.cs`, `src/Vdiff.Core/Cli/FinalPath.cs`,
  `src/Vdiff.Core/Cli/CliApp.cs`: containment checks for D-05.
- `tests/Vdiff.Tests/Support/SyntheticRepo.cs`, `RepoSnapshot.cs`: scripted repos and the
  repository-unchanged assertion.

### Established Patterns
- `GIT_NO_LAZY_FETCH=1`, `--no-optional-locks`, `core.fsmonitor=false`; plumbing commands only.
- Ordinal ordering; non-UTF-8 paths are unmodelled on both sides; fixed error text.

### Integration Points
- `src/Vdiff.Core/Pipeline/TracerPipeline.cs` (blob path to replace),
  `src/Vdiff.Core/Cli/CliOptions.cs` (`--pr`, `--keep-trees`), `src/Vdiff.Core/Model/RunMeta.cs`,
  `tests/Vdiff.Tests/Contract/SchemaFreezeTests.cs`.

</code_context>

<specifics>
## Specific Ideas

- The history error names the fix (`fetchDepth: 0`), not just the symptom.
- No ADO organisation is connected, so criterion 3's draft path can be verified only against a local
  fake endpoint for now. The verification report must say so.

</specifics>

<deferred>
## Deferred Ideas

- `fetchDepth: 0` and passing the job token in the pipeline template (Phase 9).
- Whether dry-run skips the draft lookup (Phase 8, R-28).

</deferred>

---

*Phase: 03-git-layer*
*Context gathered: 2026-11-25*
