# Phase 1: Offline Tracer — Context

**Gathered:** 2026-11-16, from the PO's pivot ("first test offline against a repository")
and the orchestrator's stated assumptions. No discuss-phase was run.

## Phase boundary

The thinnest end-to-end `vdiff` run against a local git repository, commit to commit, for C++
only, with no Azure DevOps contact. Requirements: SPK-02, TRC-01, TRC-02, TRC-03.

Out of this phase (later phases harden them): C#, C++/CLI, `.visualise-diffs.yml`, the node cap
and neighbours, rename scoring, the full `vdiff-model/v1` contract, PNG, the full HTML report,
PR threads, pipeline YAML.

## Decisions

Source of each decision: **PO** = said by the PO; **Orch** = orchestrator's assumption, stated to
the PO and not contradicted; **ADR** = docs/decisions/ADR-001.

- **D-01:** (PO) First test is offline against a local clone of a real repository.
- **D-02:** (Orch) Read-only access to the repository it analyses. Read commits via git objects
  (`git rev-parse`, `git diff-tree -r -z --raw --no-renames` between the two SHAs, and
  `git cat-file --batch`; refined 2026-11-16 from `git diff --name-status` because plumbing never
  runs diff drivers or textconv). Every git call carries `-c core.fsmonitor=false`,
  `--no-optional-locks` and `GIT_NO_LAZY_FETCH=1` (01-RESEARCH.md Pitfalls 1 and 8). Never
  `checkout`, `switch`, `stash`, `worktree add`, `reset`, `fetch` or any command that writes to
  the repo's working tree, index, refs or `.git` admin files. The PO's clone may carry another
  session's uncommitted work.
- **D-03:** (Orch) CLI shape: `vdiff --repo <path> --base <sha> --head <sha> --out <dir>
  [--blank MACRO]... [--include <glob>]...`. Output only under `--out`. Exit 0 on success, 1 on
  tool failure.
- **D-04:** (ADR, Orch) C++ extraction via the Rust sidecar from ADR-001: `tree-sitter` 0.27.0
  and `tree-sitter-cpp` 0.23.4 pinned by `Cargo.lock`, invoked once per commit as a bounded
  subprocess (timeout, output-size bound) that prints JSON on stdout. Suggested path
  `extractors/cpp-ts/`. ADR-001 is Accepted (PO, 2026-11-16); the tracer is built on it, and the choice is
  reversible behind the JSON.
  - The sidecar must accept file contents without a checkout. Recommended: the orchestrator
    materialises each commit's in-scope files from git objects into a temp directory under
    `--out` (or the system temp dir) and passes that root; or the sidecar reads a manifest of
    (path, bytes). Planner chooses; the repo itself is never written.
- **D-05:** (Orch) Macro blanking is in scope here because without it a large share of types is
  lost (ADR-001 evidence). Names come from `--blank` flags. Length-preserving, skipping
  preprocessor lines.
- **D-06:** (Orch) The orchestrator is a C#/.NET 10 console app `vdiff` (D14). Proposed layout:
  `src/Vdiff.Cli/`, `src/Vdiff.Core/` (git reader, diff, renderers), `tests/Vdiff.Tests/` (xUnit),
  `extractors/cpp-ts/` (Rust). One solution file at the root. Nothing from Roslyn is needed yet.
- **D-07:** (Orch) Diff in the tracer: types matched by fully qualified name (namespace +
  nesting) and kind; members by name + parameter text. States `added`, `removed`, `modified`,
  `unchanged`; member `modified` on signature or access change. No renames, no `body-changed`,
  no edges beyond bases. All lists sorted canonically so output is deterministic.
- **D-08:** (Orch) Outputs in `--out`: `base.model.json`, `head.model.json`, `diff.json`,
  `summary.mmd` (Mermaid `classDiagram`, a `classDef` per state, `:::state` on classes, trailing
  member tags `[added]`/`[removed]`/`[modified]`, only changed types shown), `report.html`
  (self-contained, no network; may embed the Mermaid text plus a plain table; no JS from a CDN).
  The model JSON is a provisional shape labelled `vdiff-model/v0-tracer`; Phase 2 fixes v1.
- **D-09:** (PO rule) Repository is public (D15). Tests use synthetic repos built by a script
  in a temp dir. The real-repo run (TRC-03) happens on the PO's private clone, its outputs stay
  outside this repository, and the record in this repo is pass/fail only, without names or
  figures. The real run's commit SHAs and path are supplied at run time, never written into a
  committed file.
- **D-10:** (Orch) Scope filter for the tracer: C++ files by extension (`.h`, `.hpp`, `.hh`,
  `.hxx`, `.cpp`, `.cc`, `.cxx`) under optional `--include` globs; skip files whose head and base
  blobs are identical when deciding what changed, but extract whole-tree models only for the
  files needed: for the tracer it is acceptable to extract only changed C++ files in both commits
  (neighbours come in Phase 6).
- **D-11:** (Orch, answering 01-RESEARCH open questions 2–5) The npm package checkpoint is
  answered by the PO once, at the start of execution, and recorded as the `package-checkpoint:`
  line. The minimum git version is 2.45; below it the tool refuses to run (lazy-fetch protection).
  Sidecar protocol v0 gains a per-file `status` (`ok`, `timeout`, `error`) so a timed-out file is
  labelled, never silently empty. Anonymous-struct typedefs and macro-with-argument members are
  known gaps for the tracer: they are recorded as a labelled limitation, not fixed in Phase 1.

## Environment facts (this machine)

- .NET SDK 10 and Rust/cargo are installed on Windows; WSL Ubuntu also has dotnet and cargo.
- The S2 trial source (Rust sidecar prototype that already walks classes, bases and members) is
  in `docs/spikes/S2-trial/B/` and can seed `extractors/cpp-ts/`.

## Verification expectations

- Unit/integration tests build a synthetic two-commit repo with `git init` in a temp dir and
  assert `diff.json` and `summary.mmd` content.
- A test asserts the analysed repo's `git status --porcelain`, `HEAD`, `git rev-parse --all`
  and `git worktree list` are byte-identical before and after a run, including when neither
  commit is checked out.
- Mermaid syntax is checked (mermaid-cli via npx, or a parser test) — no posting to any service.
- TRC-03's real run is a manual checkpoint the orchestrator runs on the PO's clone at the end.

## Deferred ideas

- Whole-repo extraction for 1-hop neighbours (Phase 6), config file (Phase 4), renames (Phase 5),
  full HTML (Phase 7).
