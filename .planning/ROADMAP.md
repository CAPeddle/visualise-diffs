# Roadmap: visualise-diffs

## Overview

Nine phases build the `vdiff` CLI in the order the plan settled. First the three gating
decisions are recorded (spikes S1-S3). Then the model contract and the golden-fixture
harness are fixed, so every later stage has something to turn from red to green. After
that the pipeline is built left to right: git layer, extractors, differ, selector,
renderers, publisher. The last phase is the pipeline template, the README, and the
production trial. Each stage is verified against the five synthetic fixtures before the
next one starts.

**Gates and parallelism** (no spike gates Phases 2 or 3):
- SPK-01 (S1) gates Phase 7 (renderers) and Phase 8 (publisher).
- SPK-02 (S2) gates the C++ extractor plan in Phase 4.
- SPK-03 (S3) gates the C# extractor plan in Phase 4 and the runner choice in Phase 9.
  **S3 is on hold pending PO decision O4** (see Phase 1).
- The three spikes run in parallel with each other, and alongside Phases 2 and 3.
- Phase 4 holds three independent plans (U3, U4, U5) that run in parallel.

**Coverage note:** ACC-01 (five fixtures green) is owned by Phase 2, which delivers the
fixtures and harness red. The fixtures turn green progressively: `diff.json` in Phase 5,
node sets in Phase 6, summaries in Phase 7. ACC-01 cannot be checked off until Phase 7.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [ ] **Phase 1: Decision Spikes** - Record the Mermaid-in-ADO, C++ packaging and C# runner decisions that gate later work
- [ ] **Phase 2: Model Contract and Fixture Harness** - Fix `vdiff-model/v1` and a golden-fixture harness with five synthetic fixtures
- [ ] **Phase 3: Git Layer** - Resolve head and merge-base from PR variables, list changes, skip draft and fork PRs
- [ ] **Phase 4: Extractors** - Extract C++ (tree-sitter), C# (Roslyn) and C++/CLI (scanner) into the model with honest mode labels
- [ ] **Phase 5: Differ** - Turn two models into tagged type, member and edge states with scored rename matching
- [ ] **Phase 6: Node Selector** - Pick changed types plus greyed 1-hop neighbours under the 25-node cap, or degrade to text
- [ ] **Phase 7: Renderers** - Render Mermaid, PNG and the offline HTML report
- [ ] **Phase 8: Publisher** - Post or update one sticky PR thread, handle no-change, and offer dry-run
- [ ] **Phase 9: Pipeline Template and Trial** - Ship the advisory pipeline job, config reference and README, and prove time limits

## Phase Details

### Phase 1: Decision Spikes
**Goal**: The three design questions that gate later phases are answered and recorded, so renderer, C++ packaging and C# runner choices rest on evidence rather than guesses.
**Depends on**: Nothing (first phase)
**Requirements**: SPK-01, SPK-02, SPK-03
**Plan units**: S1, S2, S3 (independent, each time-boxed to about one day)
**Success Criteria** (what must be TRUE):
  1. The S1 record in `docs/spikes/S1-mermaid-in-ado-pr.md` is filled in from a manual run of the synthetic sample on a scratch PR (run by the PO or an agent in the target organisation): it says whether `classDiagram` + `classDef` renders in a PR comment and a PR description, O3 is closed, the renderer default is set, and the trailing member-tag format is recorded. The record carries no target identifiers.
  2. ADR-001 in `docs/decisions/` records the C++ tree-sitter packaging choice (.NET binding or Rust sidecar with `tree-sitter` 0.27 + `tree-sitter-cpp`), with the comparison on package size, Linux agent install, speed and macro-blanking ease.
  3. The S3 record states which runner (Ubuntu or Windows, per D18) extracts C# with Roslyn against reference assemblies. **ON HOLD pending PO decision O4**: the PO said (2026-11-16) that S3's premise came from a mis-report and that the target C# is built on Windows. That reading is not yet confirmed. Do not assume the plan's original Ubuntu type-count test still applies; the question is reframed once O4 is decided. S1 and S2 can close without it, and Phases 2 and 3 are not blocked by it.
**Plans**: TBD

### Phase 2: Model Contract and Fixture Harness
**Goal**: Every later phase works against one fixed `vdiff-model/v1` contract and a golden-fixture harness that proves determinism, so each stage can be turned from red to green.
**Depends on**: Nothing (no spike is a hard input to this phase)
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
**Depends on**: Phase 3 (and the Phase 2 model contract); Phase 1 for the gating decisions: SPK-02 for the C++ plan, SPK-03 for the C# plan
**Requirements**: R-02, R-03, R-05, R-06, R-07, R-08, R-09
**Plan units**: U3, U4, U5 (three independent plans that run in parallel)
  - C++ plan (U3): gated by SPK-02 / ADR-001.
  - C# plan (U4): gated by SPK-03, **on hold pending PO decision O4**.
  - C++/CLI scanner plan (U5): not gated.
**Success Criteria** (what must be TRUE):
  1. C++ classes, structs, enums, nested types, bases and members with access sections are extracted syntax-only (templates kept as names), after the macros configured in `.visualise-diffs.yml` are blanked in a length-preserving way so source spans stay valid. Synthetic C++ fixtures pass, and a private sample checked in the target organisation shows at least 95% of members correct.
  2. C# files are listed from `.csproj` compile items (SDK-style and legacy), and types, members, accessibility, partials and generics are extracted with Roslyn from the project file and the target framework's reference assemblies, with no MSBuild or Visual Studio. When references are insufficient the extractor falls back to syntax-only and reports the unresolved-symbol count and the reason. Synthetic C# fixtures pass and the output is deterministic.
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
**Depends on**: Phase 6; SPK-01 (Phase 1) must be closed, because S1 fixes the renderer default and the member-tag format
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
**Goal**: The result reaches reviewers in the PR as one quiet, self-updating thread, and the whole flow can be rehearsed without contacting Azure DevOps.
**Depends on**: Phase 7; SPK-01 (Phase 1)
**Requirements**: R-24, R-25, R-28
**Plan units**: U9
**Success Criteria** (what must be TRUE):
  1. Using `System.AccessToken`, the tool posts one PR thread, finds it again by a hidden marker on a later run, updates it in place without recreating it, and links the HTML artifact. Verified manually on a scratch PR, run by the PO or an agent in the target organisation; nothing with target content is posted to Azure DevOps or an external renderer without explicit PO approval.
  2. On a PR with no structural change nothing is posted, and a thread left by an earlier run is updated to say "no structural changes".
  3. Dry-run mode writes the thread body, the PNG and the HTML locally and makes no call to Azure DevOps.
**Plans**: TBD

### Phase 9: Pipeline Template and Trial
**Goal**: A target repository can adopt the tool as a separate, advisory pipeline job that never blocks a PR or lengthens its build, and that claim is measured.
**Depends on**: Phase 8; SPK-03 (Phase 1) for the runner choice (**on hold pending PO decision O4**)
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
Phases execute in numeric order: 1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 → 9. Phase 1 does not
have to finish before Phases 2 and 3 start; the gates are listed in the Overview.

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Decision Spikes | 0/TBD | Not started | - |
| 2. Model Contract and Fixture Harness | 0/TBD | Not started | - |
| 3. Git Layer | 0/TBD | Not started | - |
| 4. Extractors | 0/TBD | Not started | - |
| 5. Differ | 0/TBD | Not started | - |
| 6. Node Selector | 0/TBD | Not started | - |
| 7. Renderers | 0/TBD | Not started | - |
| 8. Publisher | 0/TBD | Not started | - |
| 9. Pipeline Template and Trial | 0/TBD | Not started | - |
