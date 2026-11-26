# Phase 3: Git Layer - Research

**Researched:** 2026-11-26
**Domain:** git plumbing from C# (merge-base, tree export, rename detection, hunks), Azure DevOps PR variables and one read-only REST call, bounded and safe file export
**Confidence:** MEDIUM overall (git behaviour: HIGH, probed on git 2.55.0.windows.3 this session; Azure DevOps behaviour: MEDIUM, vendor docs only, no organisation is connected)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Providing the two trees**
- **D-01:** Each commit's tree is exported from git objects into a temp directory. No `git worktree`, no checkout; the analysed repository's `.git`, index, refs and working tree stay untouched in every mode, so Phase 1 D-02's no-write rule holds.
- **D-02:** Head is always the exported PR source commit, whatever the agent has checked out (a PR build's `Build.SourceVersion` is the merge commit). The checkout on disk is never analysed.
- **D-03:** Offline (`--base`/`--head`) and PR mode use the same exported trees. The tracer stops passing blobs straight from git objects to the sidecar. Extraction still covers changed in-scope C++ files only in this phase.
- **D-04:** The whole tree is exported as raw stored bytes (no autocrlf, filters or LFS smudge), under size limits. Symlinks and submodules are not followed.
- **D-05:** The trees go in a temp directory: `Agent.TempDirectory` in PR mode, the system temp offline. The location passes the same containment checks as `--out` (never inside the analysed or the vdiff repository).
- **D-06:** The trees are always deleted, on success and failure. An opt-in `--keep-trees` keeps them and prints the path. No sweep of leftovers from earlier runs.
- **D-07:** A file that cannot be exported (over the per-file limit, symlink, submodule, path illegal on this OS, case collision) is skipped with a fixed reason and the run continues. In-scope code files are listed as unmodelled; out-of-scope ones are only counted.
- **D-08:** At the total size limit, changed files are exported first, then other source and project files, then the rest, up to a raised total; what is left out is reported. Research sets the figures. The order must be deterministic (R-23; a consequence, not asked).

**Missing history and unreadable files**
- **D-09:** The tool never fetches, in any mode. Missing history gives exit 1 with a message naming the fix (a full checkout, `fetchDepth: 0`). No git credentials are needed.
- **D-10:** Any missing object means an incomplete clone: exit 1 with a "fetch a full clone" message, for out-of-scope files too. A submodule commit is not in the repository by design and is not a missing object (clarification, not asked).
- **D-11:** A file unreadable on one side for any other reason (D-07, D-08) is excluded from both models and listed as unmodelled with the reason and the side. The report warns that a type moved out of an excluded file can still be misjudged. Two consequences, not asked: the exclusion also covers a path left out of one exported tree (otherwise Phase 4's whole-repo extraction brings the false `added` types back) and both paths of a git rename pair.
- **D-12:** No merge-base: exit 1 with a clear message. Several: use the one `git merge-base` returns and record in `run.meta.json` that there were others, as a new **optional** run-meta property (additive inside v1 per `docs/schema/README.md`), with one deliberate re-pin of the run-meta hash in `SchemaFreezeTests`. Owner sign-off 2026-11-26.

**PR context and skip rules**
- **D-13:** PR mode is entered only by an explicit `--pr` flag (`vdiff --pr --repo <path> --out <dir>`). Combining it with `--base` or `--head` is an error. ADO variables never change the meaning of an offline run.
- **D-14:** Draft state comes from one read-only REST GET on the PR with `System.AccessToken`, because ADO documents no draft variable. This is the tool's first ADO contact; Phase 8 can reuse the client. Fork state comes from `System.PullRequest.IsFork` (taken from the docs, not asked). Nothing is posted.
- **D-15:** If the draft lookup fails (no token, 401/403, network, timeout), fail closed: exit 1, no thread.
- **D-16:** A skipped PR exits 0, with one fixed log line and a separate `skip.json` carrying the skip reason, validated by its own new small schema; no `run.meta.json`, model, diff or report files. `vdiff-run-meta/v1` is untouched. **Resolved by the owner 2026-11-26** (option A): a skip is decided before a merge-base exists, and run-meta requires `base_commit` and `head_commit`, so writing run-meta on a skip would need a v2 migration (rejected) or resolving the merge-base first, which fails a shallow-clone skip against R-04 (rejected). Phases 8 and 9 read `skip.json` to detect a skip. — **Reversibility:** two-way — a new contract file; run-meta stays frozen.
- **D-17:** In PR mode the target tip comes from the merge commit (`Build.SourceVersion`), which pins the base across re-runs of one build: its first parent, after verifying that the second parent is `SourceCommitId`; otherwise fall back to `origin/<target>`.

**Renames and hunks**
- **D-18:** Rename pairs and hunks are internal records on the pipeline result, tested on a scripted repo. No new output file or schema; Phase 5 decides what reaches `diff.json`.
- **D-19:** The file rename threshold is git's default 50%, fixed and passed explicitly so user git config cannot change it. The similarity score is kept on each pair.
- **D-20:** The change list and rename detection cover all changed files in the repository; consumers filter by scope. The rename limit is set explicitly and hitting it is reported.
- **D-21:** Hunks are zero-context line ranges kept for both sides; a pure insertion or deletion maps to a position on the other side. **Whitespace is ignored**, so re-indenting or a CRLF change never marks a member `body-changed`. Consequences: a changed file can have no hunks, and hunks can differ from what ADO shows. The exact git whitespace option is for research.

**Superseded earlier decisions**
- ROADMAP Phase 3 criterion 1 "worktree of the base commit" is met by exported trees (D-01).
- Phase 1 criterion 2 and D-03, "everything under `--out`": temp trees are now written outside it and deleted unless kept (D-05, D-06). The repository-unchanged guarantee stands.
- Phase 2 D-13, "copies no target source": a private run now writes target source to temp. `docs/how-to/private-fixtures.md` must say so.
- Phase 1's `object-missing` unmodelled reason becomes exit 1 (D-10); existing tests change.
- Phase 1 D-02's `--no-renames` no longer applies to change listing (D-20). The hardened flags do.

**Owner review of agent additions:** On 2026-11-26 the owner accepted, as written, the "not asked" consequences in D-08, D-10, D-11 and D-14, the Claude's Discretion items below, and the Superseded list above.

### Claude's Discretion
Not asked in the discussion, left to research and planning:
- Behaviour when PR variables are missing or malformed (`SourceCommitId`, `TargetBranch`, `IsFork`, PR id). Failing closed with exit 1 matches D-15.
- Token handling, REST timeout and client shape; the token must never reach logs, output files or a git subprocess.
- How the side is encoded for an unmodelled file: `UnmodelledEntry` is closed to `path` and `reason`, and `reason` is a free kebab-case code, so putting the side in the code avoids a schema change.

### Deferred Ideas (OUT OF SCOPE)
- `fetchDepth: 0` and passing the job token in the pipeline template (Phase 9).
- Whether dry-run skips the draft lookup (Phase 8, R-28).
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| R-01 | Resolve head and base (merge-base) from ADO PR variables and extract a full model of both commits (extraction itself is Phase 4; this phase owns head/base resolution and both trees) | PR variable names and availability (section "Azure DevOps facts"); merge-commit parent rule and fallback; `merge-base --all`; shallow/partial detection; tree listing + chunked raw-byte export; change list with renames and hunks; pipeline switched from blob reads to exported trees |
| R-04 | Skip draft and fork PRs; no thread, exit 0 | `System.PullRequest.IsFork` (no network), REST `GET .../pullrequests/{id}` with `isDraft`, check order fork then draft then history, `skip.json` + new schema, fail-closed rules, token-handling rules |
</phase_requirements>

## Summary

The phase replaces the tracer's "read changed blobs straight from git objects" path with a git layer that (1) resolves what to compare, (2) exports both whole trees as raw bytes into a temp directory, (3) produces a change list with git rename pairs and zero-context hunks, and (4) skips draft and fork PRs. Everything git-side can be done with plumbing already used by the codebase (`rev-parse`, `rev-list`, `merge-base`, `ls-tree`, `cat-file`, `diff-tree`) through `BoundedProcess`/`GitCommand`; no new package is needed. The one new network contact is a single read-only Azure DevOps REST GET through the built-in `HttpClient`.

Probing git 2.55 this session produced six findings the plan must carry. (a) `diff-tree -w` silently removes whitespace-only files from the raw list, so the change list/renames and the hunks need two separate calls. (b) `.gitattributes` `-diff` turns a file's hunks into "Binary files differ", and the attribute source is the checkout or user config, not the commit, so hunks must use `--text`. (c) Patch headers stay C-quoted under `-z`; do not parse paths from them, key each hunk set by the `index <old>..<new>` blob pair printed by `--full-index`. (d) In a `blob:none` clone with lazy fetch disabled, `ls-tree -l` exits 0 and prints size `BAD` for a missing blob, `cat-file --batch-check` prints `missing` and exits 0, `diff-tree -p` exits 128, `diff-tree --raw -M` exits 0. Detection must therefore be explicit (BAD or `missing`), not exit-code based. (e) With the rename limit exceeded git still exits 0 and only warns on stderr in the user's locale. (f) In a depth-1 PR checkout the merge commit shows no parents and `merge-base` exits 1, so D-17's merge-commit route cannot work there and the D-09 exit-1 message must fire.

Two existing-code facts change the design. `BoundedProcess.Run` buffers a child's whole stdout (capped), so a whole-tree export must run `cat-file --batch` in bounded chunks rather than one streaming call. And `GitCommand.BuildEnvironment` and `CppSidecar.Extract` pass the whole process environment to children, so `SYSTEM_ACCESSTOKEN` (which D-14 requires in the process environment) would reach every git and sidecar child unless it is scrubbed. The frozen-schema tests also constrain the work: adding `skip.json`'s schema breaks two "exactly three schemas" assertions, and D-12's optional run-meta property must satisfy `SchemaParityTests` nullability and property-order rules.

**Primary recommendation:** Build it as four layers with their own tests: (1) `GitObjectReader` additions (history, merge-base, tree listing, change set with renames and hunks), (2) a `TreeExporter` that plans deterministically from `ls-tree -l` sizes and writes via chunked `cat-file --batch`, (3) a `PrContext` + `AdoPullRequestClient` + `skip.json` writer behind `--pr`, (4) the pipeline/CLI switch and test migration. Order the checks fork, then draft GET, then any history operation, so a skip works on a shallow clone.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Head/base resolution, merge-base, history completeness checks | Git layer (`Vdiff.Core/Git`) | CLI (`CliApp`) for exit codes and messages | Pure git plumbing; CLI only maps failures to fixed messages |
| Tree listing and raw export to temp | Git layer (`TreeExporter`) | Output/Cli containment helpers (`HeldDirectory`, `FinalPath`, `CliApp.FindRefusal`) | Export is git object reading plus safe file creation; containment reuse keeps D-05 identical to `--out` |
| Change list, rename pairs, hunks | Git layer | Pipeline (consumes as internal records, D-18) | Derived from `diff-tree`; no output file this phase |
| PR variable parsing and validation | PR context (`Vdiff.Core/Pipeline` or new `Ado`) | CLI | Environment read through the existing `CliHooks.GetEnvironmentVariable` seam |
| Draft lookup | ADO client (new `Vdiff.Core/Ado`) | PR context for URL parts | Only network contact; Phase 8 reuses the client |
| Skip decision and `skip.json` | CLI orchestration + output writer + new schema | PR context, ADO client | Decided before any history operation (D-16) |
| Tracer extraction from exported trees | Pipeline (`TracerPipeline`) | Sidecar client (unchanged protocol) | Reads exported files, applies D-11 exclusions, feeds the existing sidecar protocol |
| Temp tree lifetime (create, hold, delete, keep) | CLI orchestration | `HeldDirectory` | try/finally in `CliApp`; `--keep-trees` prints the path |

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| git CLI (plumbing) | min 2.45 (existing gate); probed on 2.55.0.windows.3 | merge-base, ls-tree, cat-file, diff-tree, rev-list | Already the codebase pattern; `GitVersion.Minimum => new(2, 45, 0)` (`src/Vdiff.Core/Git/GitObjectReader.cs:11`) [VERIFIED: read this session] |
| .NET 10 BCL: `System.Net.Http.HttpClient`, `System.Text.Json` | SDK 10.0.401 present; `global.json` pins `10.0.100` rollForward `latestFeature` | The one REST GET and JSON parse | No new dependency; `CppSidecar` already uses `System.Text.Json` |
| xunit | 2.9.3 (existing) | Tests | `tests/Vdiff.Tests/Vdiff.Tests.csproj` pins it [VERIFIED: read this session] |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| NJsonSchema | 11.6.2 (existing test dependency) | Validate `skip.json` against its new schema | Same pattern as the three frozen schemas |
| `HttpMessageHandler` stub (test-only, hand-written) | n/a | Fake ADO endpoint | Inject through a new `CliHooks` member, same seam style as `GetEnvironmentVariable` |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| git CLI plumbing | LibGit2Sharp | Rejected: new native dependency, and the repo's hardening (no lazy fetch, no fsmonitor, no optional locks) is expressed as git CLI flags and environment |
| `cat-file --batch` export | `git archive` | Rejected (verified): `git archive` applies `.gitattributes` `export-ignore` and eol conversion; probe output showed `t.txt` extracted with CRLF and `excl.txt` missing. Violates D-04 raw bytes |
| `cat-file --batch` export | `checkout-index`/`read-tree` into a temp index | Rejected: runs smudge filters and eol conversion; needs a writable index file |
| Chunked `BoundedProcess` calls | A new streaming process primitive | Chunking reuses tested bounds and memory caps; a streaming primitive duplicates timeout and kill-tree logic. Recommended: chunked |

**Installation:** none. **Package Legitimacy Audit:** this phase installs no external packages. Packages removed (SLOP): none. Flagged (SUS): none.

## Architecture Patterns

### System Architecture Diagram

```
 args --pr / --base --head -----> CliOptions.Parse (value-less flags: --pr, --keep-trees)
                                        |
              offline                   |                      PR mode (D-13)
   explicit base,head ids               |        env via CliHooks.GetEnvironmentVariable
            |                           |                      |
            |                           |            PrContext.FromEnvironment (validate, fail closed)
            |                           |                      |
            |                           |            IsFork == True? --yes--> skip.json (reason fork), exit 0
            |                           |                      | no
            |                           |            AdoPullRequestClient.GetPullRequest (token scrubbed after read)
            |                           |              | 200 + isDraft true --> skip.json (draft), exit 0
            |                           |              | lookup failure --> exit 1 (D-15)
            |                           |                      | not draft
            |                           |            history: shallow? resolve head; merge commit parents;
            |                           |            target tip; merge-base --all (none -> exit 1; several -> record)
            +---------------------------+----------> baseId, headId, otherMergeBases
                                        |
                       completeness: ls-tree -l both trees (BAD => exit 1), cat-file --batch-check all blob ids
                                        |
                 diff-tree --raw -M50% -l N  (no -w)  -> ChangeSet + RenamePairs(score) + rename-limit flag
                 diff-tree --raw -p -U0 -w --text --full-index (hunks keyed by blob pair)
                                        |
                       TreeExporter: plan (tiers, limits, path rules) -> chunked cat-file --batch -> temp/base, temp/head
                                        |   (temp location judged like --out; deleted in finally unless --keep-trees)
                       exclusion set (unreadable on a side + rename partners) -> unmodelled entries with side
                                        |
                       TracerPipeline: changed in-scope C++ files read from exported trees -> sidecar -> models -> diff
                                        |
                       OutputWriter (six files, run.meta.json gains optional other_merge_bases)
```

### Recommended Project Structure
```
src/Vdiff.Core/
├── Git/
│   ├── GitObjectReader.cs     # extend: IsShallow, ParentsOf, MergeBases, ListTree, ListChanges (renames), ListHunks
│   ├── ChangeSet.cs           # new records: ChangeEntry, RenamePair, Hunk, FileHunks, ChangeSet
│   ├── TreeExporter.cs        # new: plan + chunked export + path rules + collision detection
│   └── PathRules.cs           # new: per-component validation (pure, unit-testable)
├── Ado/
│   ├── PrContext.cs           # new: env parsing and validation (pure)
│   └── AdoPullRequestClient.cs# new: one GET, injected HttpMessageHandler
├── Pipeline/
│   └── TracerPipeline.cs      # change: takes exported trees + ChangeSet instead of a reader
├── Output/
│   └── SkipWriter.cs          # new: skip.json through the same temp-name-plus-move path
├── Cli/                       # CliOptions (--pr, --keep-trees), CliApp (PR branch, tree lifetime)
docs/schema/vdiff-skip-v1.schema.json   # new, fourth schema
```

### Pattern 1: Two diff-tree calls, joined by blob pair
**What:** Call A lists every changed file (no `-w`) with rename scores; call B lists hunks (with `-w`) and is joined to A by `index <old>..<new>` blob ids, never by header paths.
**When to use:** Always for D-19/D-20/D-21.
**Example:**
```bash
# Source: probed on git 2.55.0.windows.3 this session
# A: change list + renames (NUL records; rename = R<score>, old path, new path)
git -c core.fsmonitor=false --no-optional-locks -C <repo> diff-tree -r -z --raw -M50% -l1000 --no-abbrev <base> <head>
#   :100644 100644 f625.. b8af.. R084\0old.txt\0new.txt\0
# B: hunks. -w drops whitespace-only files from this output, which is why A is separate.
git ... diff-tree -r -z --raw -p -U0 -w --ignore-blank-lines --text --no-textconv --no-ext-diff --no-color \
    --diff-algorithm=myers --full-index -M50% -l1000 <base> <head>
#   header per file: "index <40hex>..<40hex> 100644"; hunk header: "@@ -3,0 +4 @@"
```
Hunk header forms seen: pure insertion `@@ -3,0 +4 @@` (old side is a position: after old line 3); pure deletion `@@ -2 +1,0 @@` (new side is a position: after new line 1); omitted count means 1. Whole-file add is `@@ -0,0 +1,N @@`.

### Pattern 2: Plan first from sizes, then export in bounded chunks
**What:** `ls-tree -r -z -l <commit>` gives mode, type, oid, size and path per entry without reading content (30,000 entries in about 0.6 s probed on this host). The export plan (tiers, per-file and total limits, path validation, collisions) is computed from that list alone, so it is deterministic and testable without touching disk. Selected oids are then read with `cat-file --batch` in chunks of at most 64 MiB of content (the existing `SidecarLimits` stdout cap is also 64 MiB), each chunk one `BoundedProcess.Run`, and written with `FileMode.CreateNew`.
**When to use:** D-04, D-08.

### Pattern 3: Check order in PR mode
Validate variables (fail closed) -> fork skip (no network) -> draft GET -> only then history work. A shallow depth-1 checkout then still skips draft and fork PRs correctly (R-04) and fails with the `fetchDepth: 0` message only for PRs that need analysis.

### Anti-Patterns to Avoid
- **Parsing paths out of `diff --git` headers.** Quoted for newline and quote characters even under `-z` (probe), so path parsing there is wrong for hostile names. Use the raw `-z` records for paths.
- **Combining `--raw` and `-w` for the change list.** Drops whitespace-only changes (D-20 wants them listed with zero hunks).
- **Treating a symlink or gitlink side as "absent".** Today `ParseDiffTree` maps a non-regular side to a null blob, which reads as add or delete. A file that becomes a symlink in head would then show its types as `removed`: the same false-state class as the UAT bug. Keep mode and kind per side.
- **Detecting a partial clone by exit code.** Exit codes differ per command (see Pitfall 3).
- **Letting the token live in the process environment after it is read.**

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Rename pair detection and similarity | A C# similarity scorer | `diff-tree -M50% -l1000` | Score comes from git (`R084`); Phase 5's scoring is separate and uses this as one input |
| Hunk computation | A C# line diff | `diff-tree -p -U0 -w --text` | Whitespace and CRLF handling are git options |
| Merge-base, criss-cross choice | Own ancestor walk | `git merge-base --all`, first line | Matches git; first of `--all` equals the default `merge-base` output (probed criss-cross: default equalled the first `--all` line) |
| Empty-tree id | A hard-coded hash | `git hash-object -t tree --stdin </dev/null` (no `-w`) | Hash-algorithm independent; writes nothing (object count unchanged in probe) |
| Path containment for the temp trees | New containment logic | `HeldDirectory.Create(path, p => CliApp.FindRefusal(p, location, protectedDirs))` and `FinalPath.Resolve` | D-05 says "same checks as `--out`" |
| Output file write (`skip.json`) | Direct `File.WriteAllText` | The existing temp-name-plus-`File.Move` writer in `OutputWriter` | Hard-link/symlink replacement hardening already exists |
| Schema validation for `skip.json` | Hand checks | NJsonSchema + the README keyword subset | Same discipline as the frozen schemas |

**Key insight:** every hard part (similarity, diffing, merge-base, path containment, bounded process handling) already exists either in git or in this repository; the new code is orchestration, safe export and fail-closed rules.

## Azure DevOps facts

| Fact | Source | Tag |
|------|--------|-----|
| `System.PullRequest.PullRequestId`, `SourceBranch`, `SourceCommitId`, `TargetBranch` are initialised only if the build ran because of a Git PR affected by a branch policy; YAML-only for the last three | variables page | [CITED: learn.microsoft.com/azure/devops/pipelines/build/variables] |
| `System.PullRequest.TargetBranch` is `refs/heads/main` for Azure Repos | same | [CITED: same] |
| `System.PullRequest.IsFork`: "If the pull request is from a fork of the repository, this variable is set to `True`. Otherwise, it's set to `False`." Not tied to the branch-policy condition in the row | same | [CITED: same] |
| "The `Build.SourceVersion` commit for a PR build is the merge commit (not the commit on the source branch)" | same (`Build.SourceVersionMessage` row) | [CITED: same] |
| YAML variables are exposed as environment variables by uppercasing and replacing dots: "`Build.ArtifactStagingDirectory` becomes the variable `BUILD_ARTIFACTSTAGINGDIRECTORY`" | same | [CITED: same] |
| `System.AccessToken` must be mapped explicitly in YAML (`env: SYSTEM_ACCESSTOKEN: $(System.AccessToken)`) and is sent as `Authorization = "Bearer $env:SYSTEM_ACCESSTOKEN"` | PowerShell scripts page | [CITED: learn.microsoft.com/azure/devops/pipelines/scripts/powershell] |
| Doc example builds URLs from `SYSTEM_TEAMFOUNDATIONCOLLECTIONURI` and `SYSTEM_TEAMPROJECTID`; the variables page documents `System.CollectionUri` (`https://dev.azure.com/fabrikamfiber/`) and `Build.Repository.ID` | both pages | [CITED: same two pages] |
| `Agent.TempDirectory`: "A temporary folder that is cleaned after each pipeline job" (`/home/vsts/work/_temp` on Ubuntu) | variables page | [CITED: same] |
| Draft PRs do not trigger a pipeline "even if you configure a branch policy"; but for a draft "Build validation policies don't run automatically. You can queue build validations manually" | azure-repos-git page; pull-requests page | [CITED: learn.microsoft.com/azure/devops/pipelines/repos/azure-repos-git ; learn.microsoft.com/azure/devops/repos/git/pull-requests] |
| New pipelines can have shallow fetch on by default with depth 1; `fetchDepth: 0` "fetches all history and overrides the Shallow fetch setting"; default checkout command ends `--depth=1` | azure-repos-git page | [CITED: learn.microsoft.com/azure/devops/pipelines/repos/azure-repos-git] |
| REST: `GET https://dev.azure.com/{organization}/{project}/_apis/git/repositories/{repositoryId}/pullrequests/{pullRequestId}?api-version=7.1`; scope `vso.code`; `GitPullRequest` has `isDraft` ("Draft / WIP pull request."), `forkSource`, `hasMultipleMergeBases`, `lastMergeCommit`, `lastMergeSourceCommit`, `lastMergeTargetCommit`, `pullRequestId`, `status` | REST reference 7.1 | [CITED: learn.microsoft.com/rest/api/azure/devops/git/pull-requests/get-pull-request?view=azure-devops-rest-7.1] |
| The Files tab "detects diffs by a three-side comparison ... the last commit in the target branch, the last commit in the source branch, and their common merge base ... the best common ancestor". ADO flags "Multiple merge bases detected" for criss-cross and other shapes, and "might display the message even when `git merge-base` reports only one merge base" | merge strategies page | [CITED: learn.microsoft.com/azure/devops/repos/git/merging-with-squash] |
| Parent order of ADO's PR merge commit (first = target tip) | no authoritative source found | [ASSUMED] |
| `origin/<target>` exists on the agent after checkout | not documented for the checkout step | [ASSUMED] |
| `isDraft` is omitted or false for a non-draft PR in the JSON body | not documented | [ASSUMED] |
| An unauthenticated ADO REST call can return a non-JSON sign-in page with a 2xx/3xx status rather than a clean 401 | general experience, not read this session | [ASSUMED] |
| The project Build Service identity has read access to the repository by default | not verified | [ASSUMED] |

Note on tiers: the research seam classifies WebSearch/WebFetch provider results as LOW by default (`classify-confidence --provider websearch` returned `LOW`). The `[CITED]` rows above were read directly on vendor pages this session; treat them as MEDIUM, and the `[ASSUMED]` rows as needing the first connected run.

**Prescriptions (derived):**
- Environment names read: `SYSTEM_PULLREQUEST_PULLREQUESTID`, `SYSTEM_PULLREQUEST_SOURCECOMMITID`, `SYSTEM_PULLREQUEST_TARGETBRANCH`, `SYSTEM_PULLREQUEST_ISFORK`, `BUILD_SOURCEVERSION`, `SYSTEM_COLLECTIONURI` (fall back to `SYSTEM_TEAMFOUNDATIONCOLLECTIONURI`), `SYSTEM_TEAMPROJECTID`, `BUILD_REPOSITORY_ID`, `SYSTEM_ACCESSTOKEN`, `AGENT_TEMPDIRECTORY`. Name mapping follows the cited uppercase-and-underscore rule.
- Validation (fail closed, exit 1, fixed messages that never echo a value): PR id `^[0-9]{1,9}$`; commit ids through the existing `GitObjectReader.ValidateCommitInput`; `IsFork` accepts `True`/`False` case-insensitively and nothing else; `TargetBranch` must match `^refs/heads/[A-Za-z0-9._/-]+$`, contain no `..`, and not start with `-` after stripping; collection URI must be `https` and end with `/`; project id and repository id must be GUID-shaped.
- Merge-commit rule (order-independent, safer than D-17's wording): run `git rev-list --parents -n 1 --end-of-options <Build.SourceVersion>`; if it has exactly two parents and one equals `SourceCommitId`, the other is the target tip. Otherwise try `refs/remotes/origin/<name>`; otherwise exit 1 naming `fetchDepth: 0`.
- A manually queued build or a non-PR build lacks the PR variables: `--pr` then exits 1 with "this is not a pull-request build". Phase 9 should condition the step on `Build.Reason == PullRequest`; draft PRs can still reach the tool through a manually queued build validation, so the draft lookup is not redundant.

## REST client shape (Claude's Discretion, prescriptive)

- One class `AdoPullRequestClient` taking an `HttpMessageHandler` (production: `SocketsHttpHandler`/`HttpClientHandler` with `AllowAutoRedirect = false`); test seam through a new `CliHooks` member, matching `GetEnvironmentVariable`.
- Request: GET only; `Authorization: Bearer <token>`; `Accept: application/json`; URL built as `{collectionUri}{projectId}/_apis/git/repositories/{repositoryId}/pullrequests/{id}?api-version=7.1`; `HttpClient.Timeout` 30 s; no retry (D-15 fail closed).
- Accept only status 200 with a JSON content type; body read through a 2 MiB cap; require `pullRequestId` equal to the expected id; read `isDraft` (absent means false only after the checks above) and treat a present `forkSource` as a fork (skip) as a second signal. Any 3xx, 4xx, 5xx, timeout, wrong content type or parse failure is exit 1 with a fixed message carrying at most the numeric status.
- The token: read `SYSTEM_ACCESSTOKEN` once, then call `Environment.SetEnvironmentVariable("SYSTEM_ACCESSTOKEN", null)` in the real process; additionally add it (and `AZURE_DEVOPS_EXT_PAT`) to `GitCommand`'s removed variables and pass a scrubbed environment from `CppSidecar.Extract`. Evidence for the gap: `GitCommand.BuildEnvironment` copies `Environment.GetEnvironmentVariables()` and removes only six `GIT_*` names, and `CppSidecar.Extract` calls `BoundedProcess.Run(command.File, command.LeadingArgs, processLimits, request)` with no `env` argument, which inherits everything (`src/Vdiff.Core/Process/BoundedProcess.cs:262-265,284-300`; `src/Vdiff.Core/Extraction/CppSidecar.cs:273`) [VERIFIED: read this session]. Test: a fake child that dumps its environment must not contain the variable.
- Never print the URL, the org or project names, or the token. The fork check runs first, so a fork PR never sends the token anywhere.

## Common Pitfalls

### Pitfall 1: `-w` removes raw records
**What goes wrong:** `diff-tree --raw -p -w` omitted `ws.h` and `crlf.h` (whitespace-only and CR-only changes) from the raw section, so a one-call design loses D-20's "all changed files".
**How to avoid:** Call A without `-w` for the list; call B with `-w` for hunks; join by blob pair. A whitespace-only file then appears in A and has zero hunks in B.
**Warning signs:** a changed-file count that differs from `git diff-tree --name-only` without `-w`.

### Pitfall 2: Non-regular sides read as add/delete
**What goes wrong:** `ParseDiffTree` (`GitObjectReader.cs:213-218`) turns a symlink or gitlink side into a null blob. A regular file that becomes a symlink then looks deleted in head.
**How to avoid:** Keep `BaseMode`/`HeadMode` and a per-side state (`Absent`, `Regular`, `Symlink`, `Submodule`). Non-regular and non-absent means "unreadable on that side" (D-07, D-11): exclude on both sides. Only a regular side with an oid is readable.

### Pitfall 3: Missing objects look different in every command
**What goes wrong:** Probed in a `blob:none` clone with `GIT_NO_LAZY_FETCH=1`: `ls-tree -r -z -l` exit 0, size column `BAD`; `cat-file --batch-check` exit 0, line `<oid> missing`; `cat-file blob` exit 128; `diff-tree --raw -M` exit 0; `diff-tree -p` exit 128 with "could not fetch ... from promisor remote"; `ls-tree` of a missing tree exit 128 "not a tree object". Relying on exit codes misses the two exit-0 cases.
**How to avoid:** After resolving both commits, list both trees with `-l` and fail (exit 1, D-10 message) on any `BAD` size; run `cat-file --batch-check` over the union of blob oids and fail on any `missing`; map exit 128 from `ls-tree`/`merge-base` to the same message when the repository is shallow or partial. Do this before the diff calls so no diff runs on an incomplete clone. A gitlink has size `-` and is not an object to check.

### Pitfall 4: Depth-1 PR checkout hides the merge commit's parents
**What goes wrong:** Probed: `git log -1 --format=%P` empty, `HEAD^1` exit 1, `merge-base` exit 1, `--is-shallow-repository` `true`.
**How to avoid:** Detect `--is-shallow-repository` and emit the message naming `fetchDepth: 0` for: head not resolvable, merge-commit parents not visible and no `origin/<target>`, no merge-base. Because fork and draft checks run first, a shallow checkout still skips correctly.

### Pitfall 5: `.gitattributes` changes hunks and depends on the checkout
**What goes wrong:** `*.h -diff` made `diff-tree -p -w` print "Binary files differ" with no hunk. `--attr-source=<empty tree>` did not help for `.git/info/attributes` or `core.attributesFile` (probed), and attributes otherwise come from the work tree, which D-02 says is never analysed.
**How to avoid:** Pass `--text`, `--no-textconv`, `--no-ext-diff` on call B. Probed: with `--text` the `-diff` attribute, an info/attributes entry, a `core.attributesFile` entry and a `diff.<drv>.textconv` driver no longer affected hunks. Genuine binary files then produce line hunks, bounded by the stdout cap.

### Pitfall 6: Rename limit is silent and localised
**What goes wrong:** With `-l5` and 12 renames git exited 0, printed the English warning "exhaustive rename detection was skipped due to too many files." on stderr and reported the files as plain D/A. In a non-English locale the text differs.
**How to avoid:** Add `LC_ALL=C` and an empty `LANGUAGE` to the git child environment (also makes error text predictable), run call A with explicit `-M50% -l1000`, and treat stderr containing `rename detection was skipped` as "rename limit hit": record a flag on the `ChangeSet` and print one fixed stdout line. `1000` is the figure to choose; it is a recommendation, [ASSUMED] adequate. The `-l` value is ours, so user config `diff.renameLimit` is irrelevant.

### Pitfall 7: `BoundedProcess` buffers stdout
**What goes wrong:** `DrainStdout` writes into a `MemoryStream` and kills the child past the cap, so a single `cat-file --batch` over a whole tree cannot be used. Existing `ReadBlobs` already assumes ≤ 256 MiB in memory.
**How to avoid:** Chunk the plan (≤ 64 MiB of content or about 2,000 entries per call), one `BoundedProcess.Run` per chunk, parse headers as `ReadBlobs` does, write each file, drop the buffer.

### Pitfall 8: Informational output must not use stderr
`HostileInputTests.AssertRunSucceedsAndStaysInsideOut` asserts `Assert.Equal("", stderr)` on success (`tests/Vdiff.Tests/Safety/HostileInputTests.cs:357`). Rename-limit notices, skipped-file counts, `--keep-trees` paths and the skip line go to stdout.

### Pitfall 9: Hostile tree entries
`git mktree` + `git commit-tree` built a commit whose tree contains `..`, `.git`, and `Upper.h`/`upper.h` (probed; `git fsck` only warns `hasDotdot`, `hasDotgit`). A fetch does not fsck by default. Exporting `ls-tree` paths blindly is a path-traversal and hook-planting risk.
**How to avoid:** per-component validation in `PathRules` before any file operation (see Security Domain); after composing the destination path, assert `Path.GetFullPath(dest)` starts with `root + separator`. Never create symlinks (D-04), so there is no write-through-link vector. The root is a fresh directory created by us, so no pre-existing entries.

### Pitfall 10: OS-dependent skips against shared goldens
D-07's "path illegal on this OS" differs between the Windows and Linux CI legs, while both legs compare the same goldens. Rule: make skip rules OS-independent wherever the file is unusable anywhere (traversal, `.git` components, NUL, case/Unicode-normalisation collisions, over-limit), and apply Windows-only rules (reserved device names, trailing dot/space, `:`, `<>"|?*`) only when `OperatingSystem.IsWindows()`, with a catch-all `IOException` fallback code. Fixtures must therefore avoid Windows-reserved names in cross-OS goldens. [ASSUMED] that the target repo (Windows and macOS builds only, D9) has no case-collisions.

### Pitfall 11: Frozen-contract tests
- `SchemaFreezeTests.Exactly_the_three_pinned_schema_files_exist` compares the pinned list with `SchemaSupport.AllSchemaFiles` and the directory listing; `SchemaValidationTests` line 74 asserts `Assert.Equal(3, SchemaSupport.AllSchemaFiles.Count)`. A fourth schema file fails both until updated (`tests/Vdiff.Tests/Contract/SchemaFreezeTests.cs:37-49`, `SchemaValidationTests.cs:74`) [VERIFIED: read/grepped this session].
- `SchemaParityTests` treats a record property as optional when its type is nullable (`IsOptional` at line 64-65) and compares `required` to the non-nullable properties; a property's position in the record is its position in JSON (`docs/schema/README.md`). The D-12 property must be a trailing nullable property on `RunMeta`, absent from `required`, with an `items` + `pattern` definition. `Canonical.Canonicalise(RunMeta)` (`Canonical.cs:53-65`) must also order it ordinally; `ContractSamples.FullMeta()` should carry it; `CanonicalSerialisationTests` walks `typeof(RunMeta)`.
- The run-meta hash pin is `const string RunMetaSchemaSha256 = "47964D1D2C017128E930FBA5202CD06F176491DA38314A2B8CC07E6882EA2F2A";` (line 17); D-12 authorises one re-pin. The skip schema adds a new pin and a README table row.
- `JsonIgnoreCondition.WhenWritingNull` writes an empty list as `[]`; use `null` (not empty) when there is a single merge-base so the property is absent.

### Pitfall 12: Value-less flags
`CliOptions.Parse` requires a value after every flag (`i + 1 >= args.Count || args[i + 1].StartsWith("--")` throws "needs a value", `CliOptions.cs:57-60`) and accepts only `--repo --base --head --out --blank --include` (line 52). `--pr` and `--keep-trees` need a set of boolean flags; PR mode must not require `--base/--head`, and offline mode must still require them. `CliOptionsTests` and the `Usage` text change.

### Pitfall 13: Windows tree deletion while held
If the temp root is held through `HeldDirectory` (no delete sharing), `Directory.Delete` fails until the handle is released. Dispose the hold first, then delete with the same retry loop `SyntheticRepo.Dispose` uses. A failed delete is a stdout warning, not a run failure.

### Pitfall 14: Criss-cross test fixtures need distinct timestamps
`SyntheticRepo.Commit` advances a fixed clock per call, but merges made through `Git("merge", ...)` reuse the last env dates. In my first criss-cross probe both merges produced the same commit id, so no second merge-base existed. Add a `Merge(message)` helper that advances the clock, and build the two merges with different dates and messages. A correct criss-cross produced two lines from `merge-base --all`.

## Code Examples

### Tree listing record parse (`ls-tree -r -z -l`)
```text
# Source: probed output, git 2.55.0.windows.3
100644 blob f719efd4...<40hex>     BAD<TAB>a.h<NUL>     # missing blob (partial clone, lazy fetch off)
100644 blob 2bdf67ab...<40hex>       2<TAB>c.txt<NUL>   # normal: size right-aligned, then TAB, then path
# gitlink: mode 160000, type commit, size "-"; symlink: mode 120000, type blob
```
Parse by splitting at the first TAB; the left part by whitespace into mode, type, oid, size.

### Hunk join
```csharp
// Source: design from probed output; names are proposals, not existing types
public sealed record Hunk(int OldStart, int OldCount, int NewStart, int NewCount);   // count 0 = a position after OldStart/NewStart
public sealed record RenamePair(string OldPath, string NewPath, int ScorePercent);  // from "R084"
// ChangeEntry keeps BaseMode, HeadMode, BaseBlob, HeadBlob, per-side state; hunks are looked up by (BaseBlob, HeadBlob)
// from the "index <old>..<new> <mode>" line of each "diff --git" block (block boundary = line starting "diff --git ").
```

### Merge-base selection
```bash
# Source: probed. exit 1 = no merge base; exit 128 = bad object name; criss-cross returned 2 lines
git ... merge-base --all <targetTip> <head>
# chosen = first line (equal to plain `merge-base` output in the probe); others -> run.meta other_merge_bases (sorted ordinally)
```

### Test builder for hostile trees (no working tree needed)
```bash
# Source: probed. mktree accepts names that git add/update-index refuse
printf '100644 blob <oid>\t..\n100644 blob <oid>\t.git\n100644 blob <oid>\tUpper.h\n100644 blob <oid>\tupper.h\n' | git mktree
git commit-tree <tree> -m hostile        # SyntheticRepo needs WriteTree/CommitTree helpers (stdin through RunGit)
# special names with a real newline/quote need `git mktree -z`; the Windows work tree remaps those characters, so use plumbing only
```

## Export figures and ordering (D-08; Claude's recommendations, all [ASSUMED] until the owner confirms)

| Setting | Recommendation | Basis |
|---------|---------------|-------|
| Per-file limit | 4 MiB (keep `DefaultMaxBlobBytes = 4L * 1024 * 1024`) | Existing constant `GitObjectReader.cs:76`; also the sidecar-facing limit |
| Per-side total | 1 GiB (raise from `DefaultMaxTotalBytes = 256L * 1024 * 1024`, line 77) | D-08 says "a raised total"; hosted-agent disk size not verified |
| Per-side file count | 200,000 | File creation, not bytes, dominates time (below) |
| Order | tier 0: changed paths of either side; tier 1: source/project/config files by extension list; tier 2: the rest; ordinal path order inside each tier; first entry that would pass either limit and all later entries are left out (existing `budgetExhausted` pattern) | D-08, R-23 |
| Tier-1 extensions | `.h .hpp .hh .hxx .inl .ipp .c .cc .cpp .cxx .cs .csproj .vcxproj .props .targets .sln .slnx .vcxproj.filters .yml` plus `Directory.Build.*`, `packages.config` | Proposal; `ScopeFilter` currently knows only the C++ extensions, Phase 4 widens scope |

Measured this session (rough, one Windows dev host with Defender, synthetic content, MSYS pipes in the loop, not representative of the Ubuntu agent): `ls-tree -r -z -l` 30,000 files 0.64 s; `cat-file --batch-check` on 30,000 oids 0.52 s; writing 30,000 files of about 7.6 KB each through `cat-file --batch` took 25.7 s (about 1,170 files/s). Extrapolated, a 100,000-file tree is about 85 s per side on that host before any parallelism. The Linux figure is unmeasured. **The 5-minute P90 (R-26) cannot be certified from this**; it needs a measurement at the first connected trial (ACC-03). Cheap mitigations that keep determinism: plan is precomputed, so writes can run on 4 workers; skip content reads for unchanged duplicate oids (read once, write many).

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Tracer reads changed blobs from git objects per side (`ReadBlobs`) | Whole trees exported once, tracer reads exported files | This phase (D-03) | `object-missing` becomes exit 1; sidecar protocol unchanged (`SidecarInput(path, bytes)`) |
| `diff-tree --no-renames` | `-M50% -l1000` explicit | This phase (D-19, D-20) | `DiffTreeArguments` (line 113) must change; the hardened prefix stays |
| `GIT_NO_LAZY_FETCH=1` guards fetch | Same, plus explicit BAD/missing detection | This phase | Partial clones fail early instead of producing false `added` types |
| Pipeline checkout default depth 1 for new pipelines | `fetchDepth: 0` required | Sept 2022 default change | Phase 9 template; Phase 3 error text names it |

**Deprecated/outdated in this repo after the phase:** the `object-missing`, `too-large`, `total-too-large` unmodelled reasons as bare codes (become side-prefixed or exit 1), `ReadBlobs` as the pipeline's read path (may remain for the missing-object preflight or be removed), the Phase 1 criterion "everything under `--out`".

## Unmodelled reason encoding (Claude's Discretion, prescriptive)

`UnmodelledEntry` is `public sealed record UnmodelledEntry(string Path, string Reason);` (`src/Vdiff.Core/Model/ModelTypes.cs:91`) and the schema pins `"reason": { "type": "string", "pattern": "^[a-z][a-z0-9-]*$" }` (`docs/schema/vdiff-model-v1.schema.json:318-321`) [VERIFIED: read this session]. Encode as `<side>-<reason>` with side in `base`, `head`, `both` and reason in `too-large`, `total-too-large`, `symlink`, `submodule`, `path-unsafe`, `path-invalid-on-os`, `case-collision`, `not-a-blob`, e.g. `head-too-large`, `both-case-collision`. A rename partner excluded because its pair is excluded uses the same code as the file that caused it. Existing sidecar codes `parse-timeout`, `parse-error`, `non-utf8-path` stay unprefixed. An excluded in-scope path appears in both models' `unmodelled` lists (D-11); `ModelDiffer` already counts per side, so the counts stay equal. D-11's "the report warns that a type moved out of an excluded file can still be misjudged" has no home yet: the differ's only warning is `{side}: N file(s) not modelled` (`ModelDiffer.cs:164-166`); adding a fixed second warning string changes `diff.json` warnings and any golden that lists them (all fixture goldens are RED/pending today). Flag for the plan.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | ADO's PR merge commit has the target tip as first parent | Azure DevOps facts | Low: the prescribed rule (the parent that is not `SourceCommitId`, exactly two parents) is order-independent |
| A2 | `origin/<target>` exists on the agent when the merge commit route fails | Azure DevOps facts | Medium: without it the run exits 1 with the `fetchDepth: 0` message instead of continuing |
| A3 | `isDraft` is absent or false in the PR JSON for a non-draft PR | REST client shape | Medium: if the field is sometimes absent for drafts too, drafts would be analysed; verify on the first connected run |
| A4 | Unauthenticated ADO REST can return a non-JSON 2xx/3xx page | REST client shape | Low: the client requires 200 plus JSON anyway |
| A5 | The Build Service identity can read the repository with default settings | Azure DevOps facts | Medium: the draft lookup would exit 1 on every PR build until permissions are granted (Phase 9 template documentation) |
| A6 | Export limits (1 GiB, 200,000 files, rename limit 1000), tier-1 extension list | Export figures | Medium: wrong limits either truncate useful trees or exceed agent disk/time; owner confirmation requested |
| A7 | `-w --ignore-blank-lines` is the whitespace option the owner wants (probed to work; blank-only insertions give no hunk) | Pattern 1 | Low: `-w` alone also satisfies D-21's examples; the blank-line addition goes beyond what D-21 states |
| A8 | Target repo has no case-collision paths and no Windows-reserved names in cross-OS goldens | Pitfall 10 | Low |
| A9 | Hosted Ubuntu export is several times faster than the measured Windows host | Export figures | High for R-26 if wrong; measure at trial |
| A10 | `LC_ALL=C` makes git's rename-limit warning English on Windows and Linux agents | Pitfall 6 | Low: probe showed the Windows host printed English with `LC_ALL=de_DE.UTF-8` set (the locale was apparently not installed), so a localised run was not truly observed |

## Open Questions

1. **Where are the counts of skipped out-of-scope files reported (D-07 "only counted")?**
   - Known: `UnmodelledEntry` lists in-scope only by definition; run-meta gets exactly one new optional property (D-12).
   - Unclear: a place for counts.
   - Recommendation: a fixed stdout summary line and an internal `ExportResult` record; no schema change. Owner confirmation if a persisted count is wanted.
2. **Hunk-output cap breach policy.** `BoundedProcess` throws when stdout exceeds the cap. Recommendation: cap 256 MiB, breach is exit 1 with a fixed "diff output too large" message; alternative of restricting call B to in-scope extensions changes rename pairing inside the pathspec.
3. **Merge-commit parent order and whether `origin/<target>` exists** (both A1/A2): resolve on the first connected pipeline run; the order-independent rule makes A1 harmless.
4. **`isDraft` semantics and token permission** (A3, A5): same; D-15 already makes failure safe.
5. **Whitespace option** (A7): confirm `-w --ignore-blank-lines` versus `-w` alone.
6. **`hasMultipleMergeBases`**: the PR JSON carries it and ADO itself warns about multiple bases even when git reports one. Not needed by this phase (D-12 uses git's answer); noting it for the verification report so a reviewer is not surprised by a diff that differs from ADO's Files tab.
7. **D-16 versus the discussion log:** the log still says run.meta.json on a skip; `CONTEXT.md` D-16 (`skip.json`) governs, the log is audit-only.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| git | all git work | yes | 2.55.0.windows.3 (min 2.45) | none needed |
| .NET SDK | build and tests | yes | 10.0.401 | none needed |
| cargo/Rust | `SidecarFixture` builds the sidecar for `CliApp` tests | yes | 1.97.1 | none needed |
| python3 | probing only (not used by the product) | yes | n/a | not required |
| An Azure DevOps organisation | live draft lookup, `isDraft` semantics, merge-commit order | no | n/a | local fake endpoint through an injected `HttpMessageHandler`; the verification report must say the live path is unproven (CONTEXT specifics) |
| Linux/macOS runner | OS-dependent skip rules, export timing | no | n/a | unit-test `PathRules` with an injected "is Windows" flag so Windows-only rules are tested on any host |

**Missing dependencies with no fallback:** none for building and testing. **With fallback:** the ADO organisation (fake endpoint) and non-Windows hosts (parameterised rules).

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit 2.9.3, Microsoft.NET.Test.Sdk 17.14.1 (`tests/Vdiff.Tests/Vdiff.Tests.csproj`) |
| Config file | none beyond the csproj; traits select categories (`Contract`, `MermaidCli` exist); proposed new trait `Category=Git` for the pure git-layer classes (no sidecar needed, fast) |
| Quick run command | `dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj -c Release --filter "Category=Git"` (new); Phase 2's quick command was `--filter "Category=Contract\|Category=Fixtures"` and measured 56 s against a 30 s target |
| Full suite command | `dotnet test vdiff.sln -c Release --filter "Category!=MermaidCli"` then `cargo test --locked --manifest-path extractors/cpp-ts/Cargo.toml` (from `02-VALIDATION.md`) |
| Baseline | A full Debug run was started this session; its result is stated in the hand-off message, not here, because it had not finished when this file was written |

Rules to carry: any test class that calls `CliApp.Run` or the real sidecar, or sets process-wide environment variables, carries `[Collection("sidecar")]` (`SidecarFixture` rule). PR-mode tests must supply variables through `CliHooks.GetEnvironmentVariable` (existing seam), never the process environment.

### Phase Requirements -> Test Map
| Req / Criterion | Behaviour | Test Type | Automated Command | File Exists? |
|-----------------|-----------|-----------|-------------------|-------------|
| R-01 / SC1 | head = SourceCommitId; base = merge-base of target tip and head on a scripted repo with a merge commit as `Build.SourceVersion`; base commit differs from the target tip when the target advanced | integration | `--filter "FullyQualifiedName~MergeBase"` | Wave 0 (new `GitHistoryTests`) |
| R-01 / SC1 | order-independent parent rule; fallback to `origin/<target>`; no merge-base exits 1; criss-cross picks first of `--all` and records the others | integration | same | Wave 0 |
| R-01 / SC1 | both trees exported raw (CRLF and BOM preserved, no filters), checkout and index untouched (`RepoSnapshot.Take` before/after), trees deleted on success and failure, `--keep-trees` keeps and prints | integration | `--filter "FullyQualifiedName~TreeExport"` | Wave 0 |
| R-01 / SC1 | partial or damaged clone: any `BAD`/`missing` object exits 1 with the history message; shallow clone message names `fetchDepth: 0`; nothing fetched | integration | `--filter "FullyQualifiedName~IncompleteClone"` (build a `blob:none` clone from a local source with `uploadpack.allowFilter`, as probed) | Wave 0; replaces `HostileInputTests` object-missing test (line 432) |
| UAT input | a file unreadable on one side (symlink in head, over-limit, case collision) never turns its types `added` or `removed`; excluded on both models with `<side>-<reason>` | integration | `--filter "FullyQualifiedName~UnreadableSide"` | Wave 0 |
| R-01 / SC2 | change list incl. whitespace-only file (zero hunks), rename pair with score (`R084` style), rename limit flag via explicit small `-l`, hunk ranges for insertion, deletion, replacement, whole-file add, CRLF-only change (no hunk), `-diff` attribute neutralised | unit + integration on a scripted repo | `--filter "Category=Git&FullyQualifiedName~ChangeSet"` | Wave 0 |
| R-04 / SC3 | fork PR: skip, exit 0, `skip.json` only in `--out`, no HTTP call made (handler asserts zero requests) | integration (fake handler) | `--filter "FullyQualifiedName~PrSkip"` | Wave 0 |
| R-04 / SC3 | draft PR: skip, exit 0, `skip.json` valid against `vdiff-skip-v1.schema.json`, no `run.meta.json`/model/diff/report | integration (fake handler) | same | Wave 0 |
| R-04 | lookup failures (no token, 401, 403, 203 HTML, timeout, bad JSON, wrong `pullRequestId`) exit 1 with a fixed message and no output; the token never appears in stdout, stderr, files or a child's environment | integration | `--filter "FullyQualifiedName~DraftLookup"` | Wave 0 |
| R-04 | skip on a depth-1 checkout works (check order) | integration | same | Wave 0 |
| D-12 | `other_merge_bases` present only with several bases, schema-valid, canonical order; run-meta hash re-pinned once | contract | `--filter "Category=Contract"` | updates `SchemaFreezeTests`, `SchemaParityTests`, `ContractSamples`, `CanonicalSerialisationTests` |
| D-13 | `--pr` with `--base` or `--head` is an error; offline unchanged; missing PR variables exit 1 | unit | `--filter "FullyQualifiedName~CliOptions"` | updates `CliOptionsTests` |
| R-23 | same inputs, different temp base directory: byte-identical outputs; no absolute path in any output | integration | existing `DeterminismTests` extended | exists, extend |
| D-05/D-06 | temp root inside the analysed repo, the vdiff repo (via `VDIFF_PROTECTED_DIRS`) or through a link is refused | integration | `--filter "FullyQualifiedName~TreeLocation"` | Wave 0 |
| Security | hostile tree (`..`, `.git`, reserved names, collisions, non-UTF-8 path) exports nothing outside the root | integration | `--filter "FullyQualifiedName~PathRules"` | Wave 0 |

All three success criteria are proven by the tests above on scripted repositories; criterion 3 against a fake endpoint only (no organisation connected; the verification report states this).

### Sampling Rate
- **Per task commit:** the quick run command for the touched layer.
- **Per wave merge:** full suite command.
- **Phase gate:** full suite green plus `cargo test`, and the existing fixture harness still reports all five fixtures RED/pending exactly as before (the ratchet must not flip).

### Wave 0 Gaps
- [ ] `SyntheticRepo`: `WriteTree(mktreeInput)`/`CommitTree`, `Merge(message)` with an advancing clock, `Branch`, a helper that builds a `blob:none` clone and a depth-1 clone of the sandbox (local `file://` with `uploadpack.allowFilter`)
- [ ] `tests/Vdiff.Tests/Git/` classes named above; `Category=Git` trait
- [ ] A fake ADO handler (`HttpMessageHandler` subclass recording requests) and a fake-child environment dump for the token test
- [ ] Update, not add: `HostileInputTests` (too-large reason becomes side-coded; object-missing becomes exit 1), `GitObjectReaderTests` (`DiffTreeArguments` no longer `--no-renames`; `ReadBlobs` tests if the method is removed), `ModelBuilderTests` line 126 and `ModelDifferTests` line 336 reason lists, `CliOptionsTests`, `RepoUnchangedMatrixTests`, `TracerEndToEndTests`, `SchemaValidationTests` line 74, `SchemaFreezeTests`
- [ ] `docs/how-to/private-fixtures.md`: currently says "Nothing is checked out and nothing is copied" (line 33); must say a private run writes target source to a temp directory that is deleted afterwards, and that D-10 requires a full clone (a `blob:none` clone, as used in the Phase 2 UAT, now exits 1)

## Security Domain

### Applicable ASVS Categories (ASVS level 1, per `security_asvs_level`)
| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | partly (token use) | `System.AccessToken` Bearer, read once, scrubbed, never logged |
| V4 Access Control | yes (file system) | Containment of temp trees via `HeldDirectory` + `FinalPath` + protected directories |
| V5 Input Validation | yes | Strict regex/shape checks on every PR variable; per-component path rules; existing `ValidateCommitInput`; refs verified with `--end-of-options` |
| V6 Cryptography | no | TLS only through the platform `HttpClient`; no custom crypto |
| V7 Error handling/logging | yes | Fixed messages, no URL, no token, no file content, no exception text containing paths (existing pattern) |
| V9 Communications | yes | `https` collection URI only, no redirect following |
| V12 Files and resources | yes | Size and count limits, no symlink creation, `FileMode.CreateNew`, deletion in finally |

### Known Threat Patterns
| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Path traversal / hook planting via tree entry `..`, `.git`, `.git` variants (`.GIT`, `git~1`, NTFS stream `.git::$INDEX_ALLOCATION`) | Tampering | Reject empty, `.`, `..`, any `.git` component compared case-insensitively and with NTFS/HFS variants; reject NUL; reject backslash and `:` on Windows; assert final path under the root |
| Case/Unicode collision overwrites a file on case-insensitive disks | Tampering | Detect collisions with ordinal-ignore-case plus NFC folding in the plan; first path in ordinal order wins, later ones skipped with `case-collision` (platform-independent so goldens agree) |
| Token reaches a git child or the sidecar | Information disclosure | Scrub from the process environment after reading, strip in `BuildEnvironment` and the sidecar env, test with an env-dumping child |
| Token sent to an attacker-chosen host via a forged collection URI | Information disclosure | `https` only, URL from the platform variable, fork check before any request, no redirects |
| Argument/option injection through a branch name or commit id | Tampering | Shape regex, `--end-of-options`, existing `ValidateCommitInput` |
| Log injection through PR variables | Tampering | Never echo variable values; existing `Printable` approach |
| Target source left on disk | Information disclosure | Delete in `finally`; `--keep-trees` opt-in; `Agent.TempDirectory` is cleaned per job [CITED] |
| Disk or time exhaustion by a huge tree | Denial of service | Per-file, per-side total and file-count limits; chunked export; subprocess timeouts through `BoundedProcess` |
| Forged `.gitattributes` hiding hunks | Tampering | `--text`, `--no-textconv`, `--no-ext-diff` on the hunk call |

## Project Constraints (from CLAUDE.md)

- Stack and safety: C#/.NET 10 CLI, tree-sitter C++ sidecar; never block a PR; exit 0 on publish or skip, exit 1 on tool failure (R27); 20-minute hard timeout, P90 under 5 minutes (R26).
- Privacy: no posting to ADO or external renderers with target content without explicit PO approval; this phase posts nothing and its one REST call is a read-only GET carrying only the token.
- This repository is public (D15): no target names, identifiers or target-measured figures committed; run `check-public-diff.sh` on the staged diff before each commit (memory note). This document carries none; its measured figures are from synthetic repositories.
- GSD workflow: file-changing work goes through a GSD command (`/gsd-execute-phase` for planned phase work).
- Global: line endings are LF on disk for committed shell scripts; prefer Glob over `find | sort` on this host.

## Sources

### Primary (read directly this session)
- Repository code: `src/Vdiff.Core/Git/GitObjectReader.cs`, `Process/BoundedProcess.cs`, `Pipeline/TracerPipeline.cs`, `Cli/CliOptions.cs`, `Cli/CliApp.cs`, `Cli/FinalPath.cs`, `Output/HeldDirectory.cs`, `Output/OutputWriter.cs`, `Model/RunMeta.cs`, `Model/ModelBuilder.cs`, `Model/ModelTypes.cs` (lines 89-97), `Scope/ScopeFilter.cs`, `Extraction/CppSidecar.cs`; tests `SyntheticRepo.cs`, `RepoSnapshot.cs`, `SchemaFreezeTests.cs`, `HostileInputTests.cs` (lines 350-364, 431-450), `TracerEndToEndTests.cs`; `docs/schema/README.md`, `docs/schema/vdiff-run-meta-v1.schema.json`, `docs/schema/vdiff-model-v1.schema.json` (lines 306-323); `.planning` CONTEXT, DISCUSSION-LOG, REQUIREMENTS, ROADMAP, STATE, `02-VALIDATION.md` (grep).
- Git behaviour: probes against git 2.55.0.windows.3 in scratch repositories (rename scores, rename-limit warning, `-w` raw filtering, `--full-index`, attribute handling, `git archive` filters, partial and shallow clones, criss-cross merge-base, `mktree` hostile trees).
- Microsoft Learn: Predefined variables; Azure Repos Git (pipelines); Create a pull request (draft behaviour); REST Pull Requests - Get Pull Request 7.1; Merge strategies and squash merge ("Multiple merge bases"); PowerShell scripts for pipelines (Bearer token example).

### Secondary / Tertiary
- Search summaries on parent order of ADO's merge commit and on `commonRefCommit`: no authoritative statement found; left `[ASSUMED]`.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH, no new packages; git and BCL only.
- Architecture: HIGH for the git layer (probed), MEDIUM for ADO behaviour (docs only; nothing connected).
- Pitfalls: HIGH for git ones (each reproduced), MEDIUM for ADO REST edge behaviour.
- Export limits and timing: LOW, one synthetic Windows measurement; needs trial data.

**Research date:** 2026-11-26
**Valid until:** 2026-12-26 for git behaviour; re-check ADO variable and REST pages if the pipeline template (Phase 9) is written later than that.
