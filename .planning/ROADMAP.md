# Roadmap: visualise-diffs

## Overview

Nine phases build the `vdiff` CLI, tracer-first. Phase 1 is an offline tracer: the thinnest
end-to-end run of `vdiff` against a local git checkout, with no Azure DevOps contact. It
reads both commits from git objects, extracts C++ types with the Rust tree-sitter sidecar
chosen in ADR-001, diffs them at type and member level, and writes model JSON, diff JSON, a
Mermaid summary and a minimal HTML page. It is also the first test against a real repository.

Phases 2 to 9 then harden what the tracer started, in the order the plan settled: the model
contract and the golden-fixture harness, the git layer, the extractors, the differ, the node
selector, the renderers, the publisher, and finally the pipeline template and the production
trial. Each stage is verified against the five synthetic fixtures before the next one starts.

**What the tracer starts and which phase hardens it:**
- Model JSON: a thin precursor of `vdiff-model/v1`; Phase 2 fixes the contract and determinism.
- Reading git: explicit base and head commits; Phase 3 adds PR variables, merge-base and the draft and fork skips.
- C++ extraction: Phase 4 adds config-driven macro blanking, C#, C++/CLI and honest mode labels.
- Diff: types and members; Phase 5 adds renames, `body-changed`, edges and package pins.
- Diagram: Phase 6 adds the 25-node cap and the text degrade; Phase 7 delivers the final Mermaid, PNG and full offline HTML report.

**Gates and parallelism:**
- SPK-02 (S2) is recorded in Phase 1 as ADR-001 (Accepted: Rust sidecar). The tracer is built on it, and it also gates the C++ extractor plan in Phase 4.
- SPK-03 (S3) is owned by Phase 4 and attached to the C# plan. It gates that plan and the runner choice in Phase 9. **S3 is on hold pending PO decision O4** (see Phase 4).
- SPK-01 (S1) is owned by Phase 8. It is a manual run of the synthetic sample on a scratch PR with no code dependency, so it can run at any time. It must be recorded before Phase 7 closes, because it confirms the renderer default and the trailing member-tag format, and it gates the PR-thread renderer default in Phase 8. Local Mermaid rendering is not gated by it.
- Phase 4 holds three independent plans (U3, U4, U5) that run in parallel. O4 blocks only the C# plan.

**Coverage note:** ACC-01 (five fixtures green) is owned by Phase 2, which delivers the
fixtures and harness red. The fixtures turn green progressively: `diff.json` in Phase 5,
node sets in Phase 6, summaries in Phase 7. ACC-01 cannot be checked off until Phase 7.
The tracer's first cuts of R-05, R-10, R-13, R-20 and R-22 do not give Phase 1 ownership of
them: each requirement stays with the phase that hardens it.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [ ] **Phase 1: Offline Tracer** - Run `vdiff` offline against a local git repository, commit to commit, and write C++ model, diff, Mermaid and HTML outputs
- [ ] **Phase 2: Model Contract and Fixture Harness** - Fix `vdiff-model/v1` and a golden-fixture harness with five synthetic fixtures
- [ ] **Phase 3: Git Layer** - Resolve head and merge-base from PR variables, list changes, skip draft and fork PRs
- [ ] **Phase 4: Extractors** - Extract C++ (tree-sitter), C# (Roslyn) and C++/CLI (scanner) into the model with honest mode labels, and record the C# runner decision (S3, on hold)
- [ ] **Phase 5: Differ** - Turn two models into tagged type, member and edge states with scored rename matching
- [ ] **Phase 6: Node Selector** - Pick changed types plus greyed 1-hop neighbours under the 25-node cap, or degrade to text
- [ ] **Phase 7: Renderers** - Render Mermaid, PNG and the offline HTML report
- [ ] **Phase 8: Publisher** - Post or update one sticky PR thread, handle no-change, offer dry-run, and record how Azure DevOps renders Mermaid (S1)
- [ ] **Phase 9: Pipeline Template and Trial** - Ship the advisory pipeline job, config reference and README, and prove time limits

## Phase Details

### Phase 1: Offline Tracer
**Goal**: A user can run `vdiff --repo <path> --base <sha> --head <sha> --out <dir>` offline against a local git repository and get a C++ type- and member-level diff in the output directory, with no Azure DevOps contact and no change to the repository that was read.
**Depends on**: Nothing (first phase)
**Requirements**: SPK-02, TRC-01, TRC-02, TRC-03
**Plan units**: S2 (ADR-001, already written) and the tracer itself, a thin slice through U1, U2, U3, U6 and U8
  - Reading git: both commits are read straight from git objects. The tool never checks out, and never changes the working tree, branch, index or refs of the repository it reads.
  - Extraction: C++ types, syntax-only, through the Rust tree-sitter sidecar from ADR-001 (sidecar crate, blanking pass and process wrapper, as listed in its Consequences). The macro names to blank are given on the command line; reading them from `.visualise-diffs.yml` (R-02) and the verified, config-driven blanking (R-05) stay in Phase 4.
  - Diff and outputs: types and members; model JSON, diff JSON, a Mermaid `classDiagram` summary and a minimal self-contained HTML page. The model JSON is a thin precursor of `vdiff-model/v1`; Phase 2 fixes the contract, so its shape may change then.
  - Out of scope here, hardened later: C#, C++/CLI, the 25-node cap, rename matching and the full model. Which files are parsed (changed files only or the whole tree) is a planning decision; whole-repository extraction (R-01) stays in Phase 3.
**Success Criteria** (what must be TRUE):
  1. A test script builds a synthetic two-commit C++ git repository (public code, no target content). Running `vdiff --repo <path> --base <sha> --head <sha> --out <dir>` on it writes model JSON, diff JSON, a Mermaid summary and an HTML page to the output directory, and the diff JSON lists exactly the expected added, removed and modified types and members.
  2. A run never changes the repository it reads: before and after, `git status`, HEAD, the current branch, the index, every ref and `git worktree list` are identical, including when neither commit is the one checked out. This is checked on the synthetic repository in tests and on a private local clone in the trial. Everything the run writes lands in the given output directory and nowhere else.
  3. The Mermaid summary is a `classDiagram` with a `classDef` per type state and trailing member state tags (the plan's provisional format until S1 confirms it), and it passes a Mermaid syntax check in the test run. The HTML page is one self-contained file that opens in a browser with no network access and lists the changed types and members with their states.
  4. A run against a small real C++ PR (its base and head commits) on a private local clone completes well inside the R26 budget (P90 under 5 minutes, 20-minute hard limit), with its output in a directory outside this repository. The outcome is recorded in this repository, if at all, only as pass or fail against that budget, with no target identifiers and no target-measured figures.
  5. ADR-001 in `docs/decisions/` records the C++ tree-sitter packaging choice (.NET binding or Rust sidecar with `tree-sitter` 0.27 + `tree-sitter-cpp`), with the comparison on package size, Linux agent install, speed and macro-blanking ease, and the tracer's C++ extraction runs through the sidecar it chose. ADR-001 was accepted by the PO on 2026-11-16; the tracer is built on it, and the choice is reversible behind the model JSON.
**Plans**: 8/10 plans executed in 5 waves

Plans:
**Wave 1**
- [x] 01-01-PLAN.md — Wave 1: Rust sidecar crate from ADR-001 (pinned lock, stdin protocol, blanking, tracer-level extraction)
- [x] 01-02-PLAN.md — Wave 1: .NET core read path (solution scaffold, bounded process runner, read-only git object reader, scope filter, sidecar client, synthetic-repo test support)

**Wave 2** *(blocked on Wave 1 completion)*
- [x] 01-03-PLAN.md — Wave 2: end-to-end tracer (model, differ, writers, CLI, repository-unchanged assertion) plus the npm package-legitimacy checkpoint

**Wave 3** *(blocked on Wave 2 completion)*
- [x] 01-04-PLAN.md — Wave 3: repository-unchanged matrix, hostile arguments and content, public-repository guards
- [x] 01-05-PLAN.md — Wave 3: fake-sidecar bounds (timeout, caps, exit, schema) and the timeout override
- [x] 01-06-PLAN.md — Wave 3: sidecar depth (enums, nested types, special members, `--blank` proof, error-node labelling, robustness)
- [x] 01-07-PLAN.md — Wave 3: differ and scope hardening (member-level states, duplicate policy, canonical order, `--include` globs)
- [x] 01-08-PLAN.md — Wave 3: Mermaid and HTML writers hardened, determinism and no-local-path output

**Wave 4** *(blocked on Wave 3 completion)*
- [ ] 01-09-PLAN.md — Wave 4: local mermaid-cli syntax check (pinned install, check script, tests)

**Wave 5** *(blocked on Wave 4 completion)*
- [ ] 01-10-PLAN.md — Wave 5: trial script, merged-suite check, orchestrator run on the private Windows clone, public record, SPK-02 closure
**Note**: The HTML page here is deliberately minimal; the full offline report (no cap, per-language tabs, search, mode banner) is Phase 7 (R-22). Spikes S1 and S3 are no longer in this phase: S1 is in Phase 8 and S3 in Phase 4.

### Phase 2: Model Contract and Fixture Harness
**Goal**: Every later phase works against one fixed `vdiff-model/v1` contract and a golden-fixture harness that proves determinism, so each stage can be turned from red to green.
**Depends on**: Phase 1 (the tracer's thin model JSON is what this phase turns into the fixed contract)
**Requirements**: R-10, R-23, ACC-01, ACC-02
**Plan units**: U1
**Success Criteria** (what must be TRUE):
  1. `vdiff-model/v1` is defined (types, members, bases, derived edges, spans, extraction metadata) with volatile metadata kept apart from the canonical body, and a model written by any extractor validates against it.
  2. Given base, head and config, the harness compares the tool's output with the expected `diff.json` and the expected summary for a fixture, and it runs in CI.
  3. Five synthetic fixtures (small C++, C#, cross-stack with C++/CLI, extract-class refactor, rename) are in the repository, and the CI harness reports each as failing against its expected output, as intended before any extractor exists.
  4. Serialising the same model twice, with collections supplied in any order, yields byte-identical JSON.
  5. A documented private-fixture recipe lets five real PRs of the same shapes be checked inside the target organisation without their content entering this repository.
**Plans**: TBD
**Note**: ACC-01 ("green in CI") is owned here but only reaches green in later phases; see the coverage note in the Overview.

### Phase 3: Git Layer
**Goal**: Given an Azure DevOps PR context, the tool knows exactly what to compare and which PRs to leave alone.
**Depends on**: Phase 2
**Requirements**: R-01, R-04
**Plan units**: U2
**Success Criteria** (what must be TRUE):
  1. From the Azure DevOps PR variables the tool resolves head and base, where base is `merge-base(target branch tip, head)` (the same comparison Azure DevOps shows for the PR), and provides a worktree of the base commit beside head so both trees are available for whole-repository extraction.
  2. The tool lists changed files, finds rename pairs with a similarity score, and maps diff hunks to source spans; unit tests on a scripted repo pass.
  3. A draft PR and a PR from a fork are each skipped: no thread is posted and the run exits 0.
**Plans**: TBD

### Phase 4: Extractors
**Goal**: Every in-scope language is extracted into `vdiff-model/v1` for both commits, and each file and language states honestly how it was extracted.
**Depends on**: Phase 3 (and the Phase 2 model contract); Phase 1 for the tracer and the C++ packaging decision (SPK-02 / ADR-001)
**Requirements**: R-02, R-03, R-05, R-06, R-07, R-08, R-09, SPK-03
**Plan units**: U3, U4, U5 (three independent plans that run in parallel)
  - C++ plan (U3): hardens the Phase 1 tracer's sidecar and blanking pass; gated by SPK-02 / ADR-001, recorded in Phase 1.
  - C# plan (U4): opens with the S3 record (SPK-03) and is gated by it, **on hold pending PO decision O4** (see criterion 2).
  - C++/CLI scanner plan (U5): not gated.
**Success Criteria** (what must be TRUE):
  1. C++ classes, structs, enums, nested types, bases and members with access sections are extracted syntax-only (templates kept as names), after the macros configured in `.visualise-diffs.yml` are blanked in a length-preserving way so source spans stay valid. Synthetic C++ fixtures pass, and a private sample checked in the target organisation shows at least 95% of members correct.
  2. C# files are listed from `.csproj` compile items (SDK-style and legacy), and types, members, accessibility, partials and generics are extracted with Roslyn from the project file and the target framework's reference assemblies, with no MSBuild or Visual Studio. When references are insufficient the extractor falls back to syntax-only and reports the unresolved-symbol count and the reason. Synthetic C# fixtures pass and the output is deterministic. The S3 record (SPK-03) states which runner (Ubuntu or Windows, per D18) extracts C# with Roslyn against reference assemblies. **ON HOLD pending PO decision O4**: the PO said (2026-11-16) that S3's premise came from a mis-report and that the target C# is built on Windows. That reading is not yet confirmed. Do not assume the plan's original Ubuntu type-count test still applies; the question is reframed once O4 is decided. The C++ and C++/CLI plans, and Phases 1 to 3, are not blocked by it.
  3. In the cross-stack fixture the scanner places the C++/CLI wrapper type in its own "managed wrapper" lane (the managed-type declarations and `gcroot<>` members listed in R-08).
  4. Every target-specific setting (include/exclude globs, C# projects, macros to blank) is read from `.visualise-diffs.yml`, and the default exclusions (tests opt-in, `*.Designer.cs`, generated files, build output, vendored and `dist/` trees) apply unless the config overrides them.
  5. Every language and file is labelled `semantic`, `syntax-only` or `scanner`; each fallback has a stated reason; files that could not be modelled are listed; C++ is always `syntax-only`; no partial extraction is presented as complete.
**Plans**: TBD (expected: three independent plans, one per unit, run as one parallel wave)

### Phase 5: Differ
**Goal**: Two models become a trustworthy structural diff: every type, member and edge carries a state, renames are matched by a pinned score, and a package-pin bump shows as one node.
**Depends on**: Phase 4
**Requirements**: R-13, R-14, R-15
**Plan units**: U6
**Success Criteria** (what must be TRUE):
  1. Every type, member and edge is tagged `added`, `removed`, `modified`, `renamed` or `unchanged`; a member is `modified` when its signature, type, visibility or static/virtual/override changes, and `body-changed` when a diff hunk overlaps its span and nothing else changed.
  2. A removed type and an added type of the same language and kind are reported as a rename only when their score (0.5 x Jaccard(member names) + 0.2 x same file or git rename pair + 0.2 x same namespace + 0.1 x same base set) is at least 0.6 and each is the other's unique best match; ties are not renamed; members of matched types are renamed when parameter list and return type are identical and the pair is unique; the thresholds are configurable and pinned by the rename fixture.
  3. A changed dependency-package pin, detected by a configured rule, appears as a single `package updated: name old → new` node.
  4. The `diff.json` of all five synthetic fixtures matches its expected file in CI.
**Plans**: TBD

### Phase 6: Node Selector
**Goal**: A diff becomes a bounded, readable node set, or a text table when a diagram would be unreadable.
**Depends on**: Phase 5
**Requirements**: R-18, R-19
**Plan units**: U7
**Success Criteria** (what must be TRUE):
  1. The selection starts with the changed types and adds their 1-hop neighbours (bases, derived types, association targets) marked as greyed context; the total never exceeds 25 nodes, neighbours included; changed types rank first (public API before internal), neighbours by edge count.
  2. Anything dropped is summarised as "+N more changed" or "+N context types".
  3. When changed types exceed 2N, or the Mermaid output would exceed its limits (50,000 characters / 500 edges), the summary degrades to a text table grouped by namespace or project; a 600-type change degrades to text; the node cap always wins and the Mermaid limits are only a hard ceiling.
  4. The selected node sets for the synthetic fixtures match their expected sets.
**Plans**: TBD

### Phase 7: Renderers
**Goal**: The selected nodes are rendered in the format a reviewer will actually see in the PR, and a full report is available offline.
**Depends on**: Phase 6. SPK-01 (owned by Phase 8, a manual run with no code dependency) must be recorded before this phase closes, because S1 confirms the renderer default and the member-tag format; local Mermaid rendering itself is not gated by it.
**Requirements**: R-20, R-21, R-22
**Plan units**: U8
**Success Criteria** (what must be TRUE):
  1. The summary is a Mermaid `classDiagram` with a `classDef` for each type state (`added`, `removed`, `modified`, `renamed`, `context`) and trailing member state tags in the format S1 fixed; the golden summaries for the synthetic fixtures match.
  2. A PNG render (via mermaid-cli) is produced, the default between Mermaid and PNG follows the S1 result, and the config can switch it.
  3. The HTML report opens in a browser with no network access, has no node cap, offers per-language tabs and search, and shows a banner stating the extraction mode.
  4. All five synthetic fixtures are green in CI against their expected summaries (completes ACC-01).
**Plans**: TBD
**UI hint**: yes

### Phase 8: Publisher
**Goal**: The result reaches reviewers in the PR as one quiet, self-updating thread, the whole flow can be rehearsed without contacting Azure DevOps, and how Azure DevOps renders the summary is known, so the renderer default rests on evidence.
**Depends on**: Phase 7 (S1, the spike owned here, has no code dependency and can run earlier)
**Requirements**: R-24, R-25, R-28, SPK-01
**Plan units**: U9, and S1 (a manual spike, time-boxed to about one day)
**Success Criteria** (what must be TRUE):
  1. Using `System.AccessToken`, the tool posts one PR thread, finds it again by a hidden marker on a later run, updates it in place without recreating it, and links the HTML artifact. Verified manually on a scratch PR, run by the PO or an agent in the target organisation; nothing with target content is posted to Azure DevOps or an external renderer without explicit PO approval.
  2. On a PR with no structural change nothing is posted, and a thread left by an earlier run is updated to say "no structural changes".
  3. Dry-run mode writes the thread body, the PNG and the HTML locally and makes no call to Azure DevOps.
  4. The S1 record in `docs/spikes/S1-mermaid-in-ado-pr.md` is filled in from a manual run of the synthetic sample on a scratch PR (run by the PO or an agent in the target organisation): it says whether `classDiagram` + `classDef` renders in a PR comment and a PR description, O3 is closed, the renderer default is set, and the trailing member-tag format is recorded. The record carries no target identifiers. It is recorded before Phase 7 closes.
**Plans**: TBD

### Phase 9: Pipeline Template and Trial
**Goal**: A target repository can adopt the tool as a separate, advisory pipeline job that never blocks a PR or lengthens its build, and that claim is measured.
**Depends on**: Phase 8; SPK-03 (Phase 4) for the runner choice (**on hold pending PO decision O4**)
**Requirements**: R-26, R-27, ACC-03
**Plan units**: U10
**Success Criteria** (what must be TRUE):
  1. The pipeline template defines a separate job that never sits on the build's critical path, with the runner chosen from the S3 outcome (Ubuntu by default, Windows as the D18 fallback) and a 20-minute hard timeout.
  2. The job uses `continueOnError: true`; after a crash or timeout the existing thread is untouched and a pipeline warning is raised; the exit code is 0 when published or skipped and 1 on a tool failure.
  3. A `.visualise-diffs.yml` reference and the README let someone adopt the tool in a target repository without reading the source.
  4. A trial on the target PR pipeline, run by the PO or an agent in the target organisation, shows that over 10 consecutive PR runs none extends the PR build's wall-clock and the P90 job time is under 5 minutes. The result is recorded here without target identifiers.
**Plans**: TBD

## Progress

**Execution Order:**
Phases execute in numeric order: 1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 → 9. Phase 1 (the offline
tracer) comes first and Phase 2 depends on it. S1 (Phase 8) and S3 (Phase 4) are decision
records, not code. S1 has no code dependency and can run whenever the PO or an agent in the
target organisation is available; it must be recorded before Phase 7 closes. S3 is on hold
pending O4. The gates are listed in the Overview.

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Offline Tracer | 8/10 | In Progress | - |
| 2. Model Contract and Fixture Harness | 0/TBD | Not started | - |
| 3. Git Layer | 0/TBD | Not started | - |
| 4. Extractors | 0/TBD | Not started | - |
| 5. Differ | 0/TBD | Not started | - |
| 6. Node Selector | 0/TBD | Not started | - |
| 7. Renderers | 0/TBD | Not started | - |
| 8. Publisher | 0/TBD | Not started | - |
| 9. Pipeline Template and Trial | 0/TBD | Not started | - |
