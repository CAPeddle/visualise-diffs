# Phase 1: Offline Tracer - Research

**Researched:** 2026-11-16
**Domain:** read-only git object access, tree-sitter C++ extraction in a Rust sidecar, .NET 10 subprocess and test infrastructure, Mermaid classDiagram output and local syntax checking
**Confidence:** HIGH for everything tagged `[VERIFIED: probe]` (run this session on Git for Windows 2.55, .NET SDK 10.0.401, cargo 1.97.1, mermaid-cli 12.2.0). MEDIUM for items tagged `[CITED]`. LOW for `[ASSUMED]`.

All probes ran in a scratch directory outside the repository against synthetic data only. No target name, path or SHA appears here (D-09).

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

Source of each decision: **PO** = said by the PO; **Orch** = orchestrator's assumption, stated to
the PO and not contradicted; **ADR** = docs/decisions/ADR-001.

- **D-01:** (PO) First test is offline against a local clone of a real repository.
- **D-02:** (Orch) Read-only access to the repository it analyses. Read commits via git objects
  (`git ls-tree`, `git cat-file --batch`, `git diff --name-status` between the two SHAs). Never
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

### Claude's Discretion

CONTEXT.md has no `## Claude's Discretion` section. Where it says "Planner chooses" (D-04, sidecar input: temp-dir materialisation versus stdin manifest) the plans chose a stdin manifest; this research confirms that choice (see Architecture Patterns).

### Deferred Ideas (OUT OF SCOPE)

- Whole-repo extraction for 1-hop neighbours (Phase 6), config file (Phase 4), renames (Phase 5),
  full HTML (Phase 7).
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| SPK-02 | Spike S2 recorded as ADR-001 — C++ tree-sitter packaging chosen (.NET binding vs Rust sidecar with `tree-sitter` 0.27 + `tree-sitter-cpp`) on package size, Linux install, speed, macro blanking (gates U3) | ADR-001 line 3 reads `- **Status:** Accepted (PO, 2026-11-16)` [VERIFIED: docs/decisions/ADR-001-cpp-tree-sitter-packaging.md:3]. This research confirms the pinned graph builds with `--locked` and that the sidecar can be driven through the pinned API (Code Examples 1). |
| TRC-01 | User can run `vdiff` offline against a local git repository with explicit base and head commits; the tool reads commit contents from git objects and leaves the repository's working tree, index, branch and refs unchanged. | Read-only findings (Git section): `diff-tree` invokes `core.fsmonitor` and can start the fsmonitor daemon; `rev-parse` and `cat-file` did not; lazy fetch in partial clones writes a pack unless `GIT_NO_LAZY_FETCH=1`. Overrides and tests specified below. |
| TRC-02 | User gets, for C++ code, a type- and member-level diff between the two commits written as model JSON, diff JSON, a Mermaid classDiagram summary (classDef per state, trailing member tags) and a self-contained HTML page in the output directory. | Measured tree-sitter-cpp 0.23.4 node shapes, Mermaid escaping results, determinism hazards. |
| TRC-03 | User can run the tracer against a real target PR on a private local clone and keep every output outside this public repository; any result recorded in this repo carries no target identifiers. | Script and record constraints; fsmonitor risk for the real clone; Mermaid text-size trap that would make the script report a false `mermaid_syntax: ok`. |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

Extracted from `.claude/CLAUDE.md` (project) and the user's global CLAUDE.md:

- C#/.NET 10 CLI `vdiff`, Roslyn in-process later; every extractor emits `vdiff-model/v1` JSON (D14). C++ via tree-sitter syntax-only, no home-grown parser (D11). In-scope code builds only on Windows (.vcxproj) and macOS (Xcode) (D9). Separate job, Ubuntu default with Windows fallback (D18, open question O4). 20-minute hard timeout, P90 under 5 minutes (R26). Never block a PR: exit 0 on publish or skip, 1 on tool failure (R27). No posting to ADO or external renderers with target content without explicit PO approval.
- Work must start through a GSD command (this research is one). Do not edit outside a GSD workflow.
- Global: use Glob rather than `find | sort` (Defender ASR blocks Git for Windows `sort.exe`); committed content is LF; shell scripts and hooks must be LF on disk; do not add `* text=auto` to an existing repo casually; write files with backslashes through the Write tool (see Pitfall 12).

## Summary

The ten plans are technically sound in outline and unusually thorough. Research verified most of their load-bearing claims (pinned lock builds with `--locked`, `dotnet test` summary format, `git cat-file` reads raw bytes, `GIT_NO_LAZY_FETCH` stops lazy fetch, `--ignore-scripts` install of mermaid-cli works with the machine's own Chrome, Kill(entireProcessTree) works, stdin to an exited child throws IOException). It also found a cluster of defects that would otherwise surface late and expensively.

The most important: (1) **`git diff-tree` runs the repository's `core.fsmonitor` hook, and with `core.fsmonitor=true` on Windows it auto-starts the fsmonitor daemon and creates `.git/fsmonitor--daemon/`** — a write into `.git` that violates D-02 and that no planned test would catch. `git status` (used by `RepoSnapshot` and `trace-run.ps1`) does the same. The fix is `-c core.fsmonitor=false` on every git call. (2) **tree-sitter 0.27.0 has no timeout setter at all** (only a progress callback), and a cancelled parser *resumes the abandoned parse* on its next use unless `reset()` is called — silent wrong output under the plans' per-thread parser reuse. (3) **mermaid-cli exits 0 on any diagram over 50,000 characters without parsing it** (it renders a "maximum text size exceeded" diagram), so plan 01-09's check and plan 01-10's `mermaid_syntax` field would report success on unparsed text; raising `maxTextSize` through `-c` fixes it. (4) Several tree-shape statements in plans 01-01 and 01-06 are wrong for tree-sitter-cpp 0.23.4 (destructor name, reference declarators, conversion operators, members inside `#if` blocks).

Everything else is a smaller correction (abbreviated-hash shadowing by a branch name, `Matcher` defaults to case-insensitive, Windows MAX_PATH breaks the 200-character-name fixture, git version floor for lazy-fetch protection). Section `## Plan Corrections` lists each with plan id, task, change, reason and source, followed by what must not change.

**Primary recommendation:** Execute the plans as written after applying the HIGH items in `## Plan Corrections` (C1-C4, C32, C11-C12, C25-C27, C37, C43-C44, C47-C48); the remaining items are cheap folds into tasks already planned.

## Architectural Responsibility Map

This is a CLI plus a native child process, not a web tier stack. Tiers here are the process and library boundaries the plans already use.

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Argument validation, exit codes, `--out` containment | `Vdiff.Core/Cli` | `Vdiff.Cli` (entry point only) | One testable `CliApp.Run(args, stdout, stderr)`; keeps the executable trivial |
| Reading commits (resolve, changed files, blobs) | `Vdiff.Core/Git` via `BoundedProcess` | — | Only place that spawns git; all read-only guarantees live here |
| Syntax-only C++ extraction, macro blanking, error-node counting | Rust sidecar `extractors/cpp-ts` | `Vdiff.Core/Extraction` (client, limits, schema check) | Native parser faults stay in the child (ADR-001); the client fails closed |
| Model build, diff, canonical ordering | `Vdiff.Core/Model`, `Vdiff.Core/Diff` | — | Pure functions on in-memory models; unit-testable without a repository |
| Mermaid, HTML, JSON writers | `Vdiff.Core/Output` | — | Single escape function and single serialiser enforce determinism and injection safety |
| Mermaid syntax check | `tools/mermaid-check` (node, local headless browser) | `MermaidLint` (structural, in tests) | Real parser only runs locally; never contacts a service |
| Test infrastructure (synthetic repos, snapshots, fake sidecar) | `tests/Vdiff.Tests/Support`, `tests/Vdiff.FakeSidecar` | — | Public repo: synthetic content only (D-09) |
| Private-run wrapper | `scripts/trace-run.ps1` | orchestrator (manual) | Emits a six-line fixed vocabulary so nothing identifying can leak |

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET SDK / `net10.0` | SDK 10.0.401 installed (`global.json`: 10.0.100, `latestFeature`) | `vdiff` CLI, tests | D14. `dotnet new sln --name vdiff --format sln` created `vdiff.sln` on this SDK [VERIFIED: probe] |
| `tree-sitter` (Rust) | 0.27.0 (`=` pin) | parser runtime | ADR-001. `tree-sitter = "=0.27.0"` [VERIFIED: docs/spikes/S2-trial/B/Cargo.toml:7] |
| `tree-sitter-cpp` (Rust) | 0.23.4 (`=` pin) | C++ grammar | ADR-001. `tree-sitter-cpp = "=0.23.4"` [VERIFIED: docs/spikes/S2-trial/B/Cargo.toml:8] |
| `rayon` / `serde` / `serde_json` | `"1"` ranges; lock fixes them | parallel parse, JSON | Same as the ADR-001 trial [VERIFIED: docs/spikes/S2-trial/B/Cargo.toml:9-11] |
| xunit | 2.9.3 | test framework | Emitted by the SDK 10 template; latest v2 release [VERIFIED: probe `dotnet new xunit`, api.nuget.org] |
| xunit.runner.visualstudio | 3.1.4 | runner | Same template [VERIFIED: probe] |
| Microsoft.NET.Test.Sdk | 17.14.1 | test host | Same template [VERIFIED: probe] |
| Microsoft.Extensions.FileSystemGlobbing | 10.0.15 | `--include` globs | Latest 10.0.x on NuGet (11.0.0 also exists; stay on 10.0.x for `net10.0`) [VERIFIED: api.nuget.org] |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `@mermaid-js/mermaid-cli` [WARNING: flagged as suspicious by the legitimacy seam (too-new) — verify before using; checkpoint in plan 01-03] | 12.2.0 (exact pin) | local Mermaid syntax check | Plan 01-09 only. Version, peer dependency `puppeteer ^25.0.0` and engines node `>=22.13.0` are [VERIFIED: npm view]; the legitimacy verdict is SUS and is not upgraded by that |
| `puppeteer` [WARNING: flagged as suspicious by the legitimacy seam (too-new) — verify before using; checkpoint in plan 01-03] | 25.17.0 (exact pin) | headless Chrome driver for mermaid-cli | Install with `--ignore-scripts` (it has `postinstall: node install.mjs`); use the machine's Chrome via `executablePath`. Version and script facts are [VERIFIED: npm view, probe]; the legitimacy verdict is SUS |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| xunit 2.9.3 | xunit.v3 4.0.3 | Exists on NuGet but the SDK template and all plan text use v2; no benefit for this phase |
| mermaid-cli syntax check | `mermaid.parse` called from a small node script | Avoids launching a browser per check, but the plans chose mmdc and it works; keep it |
| stdin manifest (chosen) | temp-dir materialisation (D-04 option 1) | Manifest never writes file content to disk and needs no cleanup; keep the manifest |

**Installation:**
```bash
# Rust sidecar (cold build 76 s measured on this machine)
cargo build --release --locked --manifest-path extractors/cpp-ts/Cargo.toml

# mermaid check (30 s measured; 192 packages; scripts off, system Chrome used)
npm --prefix tools/mermaid-check ci --ignore-scripts
```

**Version verification:** `npm view` for both npm packages, `api.nuget.org` flat-container index for the two NuGet packages, template output for the xunit trio, `cargo build --locked` for the crates (done 2026-11-16, see Sources). A lock root rename (`s2b` to another package name) is accepted by `--locked` [VERIFIED: probe: renamed root built with `--locked`], so the fallback sentence in plan 01-01 task 1 ("If `--locked` rejects the rename...") is not needed. The lock holds 33 `[[package]]` entries, not five: "five crates" in T-01-SC means five direct dependencies [VERIFIED: probe `grep -c '^\[\[package\]\]'`].

## Package Legitimacy Audit

Seam run: `package-legitimacy check` on 2026-11-16.

| Package | Registry | Age | Downloads | Source Repo | Verdict | Disposition |
|---------|----------|-----|-----------|-------------|---------|-------------|
| `@mermaid-js/mermaid-cli` | npm | multi-year line; 11.x and 12.x releases since 2026-05, 12.0.0 on 2026-09-24, 12.2.0 on 2026-11-02; maintainers include the mermaid-js team | ~833k/wk | github.com/mermaid-js/mermaid-cli | SUS (`too-new`: latest version published 2026-11-02) | Flagged. Keep the blocking human checkpoint in plan 01-03 task 2. Registry facts are `[VERIFIED: npm view]` but the seam verdict is not upgraded by them |
| `puppeteer` | npm | multi-year; 25.13.0 to 25.17.0 weekly between 2026-10-08 and 2026-11-10; maintainers `mathias` and the Google release bot | ~15.3M/wk | github.com/puppeteer/puppeteer | SUS (`too-new`: latest version published 2026-11-10), `postinstall: node install.mjs` | Flagged. Install only with `--ignore-scripts`; keep the checkpoint |
| `tree-sitter`, `tree-sitter-cpp`, `rayon`, `serde`, `serde_json` | crates.io | years | high | upstream repos | OK | Approved; pinned by ADR-001 and `Cargo.lock` |
| `Microsoft.Extensions.FileSystemGlobbing`, `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk` | NuGet | years | high | Microsoft / xunit.net | not covered by the seam | Versions verified on NuGet and in the SDK template; legitimacy not machine-checked (outside the cargo/npm gate, as plan T-01-SC states) |

**Packages removed due to [SLOP] verdict:** none.
**Packages flagged as suspicious [SUS]:** `@mermaid-js/mermaid-cli`, `puppeteer`. Both `too-new` verdicts reflect the date of the latest release, not the age of the package; the planner keeps the existing checkpoint (plan 01-03 task 2) and plan 01-09 installs with scripts disabled and a committed lockfile.

Observation for the orchestrator: `workflow.human_verify_mode` is `end-of-phase` in `.planning/config.json`, yet plan 01-03 puts a blocking human gate in wave 2, which blocks waves 3 to 5. The PO can answer it now, in conversation, once.

## Architecture Patterns

### System Architecture Diagram

```
 argv --repo --base --head --out [--blank]* [--include]*
        |
        v
 CliOptions.Parse --(invalid)--> stderr "vdiff: <msg>", exit 1
        |
        v
 GitObjectReader (every call: git -c core.fsmonitor=false --no-optional-locks -C <repo> ...,
                  env: GIT_DIR family removed, GIT_OPTIONAL_LOCKS=0, GIT_NO_LAZY_FETCH=1)
   rev-parse --verify <hex>^{commit} ---> full ids (check id startsWith input)
   diff-tree -r -z --raw --no-renames --no-abbrev base head ---> ParseDiffTree
        |  keep regular files (100644/100755) per side; drop mode-only; ScopeFilter (ext + --include)
        v
   cat-file --batch-check (size/type) ---> cap 4 MiB, "missing" -> unmodelled
   cat-file --batch (blobs) ---> raw bytes (no filters)
        |
        v   (once per commit side)
 BoundedProcess --stdin: 1 JSON header line + raw bytes--> Rust sidecar vdiff-cpp-ts
        ^  timeout, stdout cap, stderr cap, tree kill        |  blank macros (length-preserving)
        |                                                    |  tree-sitter parse (deadline callback, reset on cancel)
        +------ one UTF-8 JSON object <----------------------+  extract types/members, count ERROR/MISSING
        |
        v
 ModelBuilder (ordinal sort, #n duplicate suffix) -> ModelDiffer -> DiffFile
        |
        v   (all in memory; nothing written yet)
 MermaidWriter / HtmlWriter / canonical JSON ---> OutputWriter.WriteAll(--out)  (5 fixed names, UTF-8 no BOM, LF)
```

### Recommended Project Structure

As in plans 01-01/01-02/01-03 (matches D-06): `src/Vdiff.Cli`, `src/Vdiff.Core/{Cli,Git,Process,Scope,Extraction,Model,Diff,Output}`, `tests/Vdiff.Tests/{Support,Safety,Extraction,Diff,Scope,Output,Trial}`, `tests/Vdiff.FakeSidecar`, `extractors/cpp-ts/src/{main,protocol,blank,extract}.rs`, `tools/mermaid-check`, `scripts/trace-run.ps1`.

### Pattern 1: Read-only git invocation

**What:** every git call is an argument array, `git -c core.fsmonitor=false --no-optional-locks -C <repo> <plumbing verb> ...`, environment minus the GIT_DIR family, plus `GIT_OPTIONAL_LOCKS=0`, `GIT_TERMINAL_PROMPT=0`, `GIT_NO_LAZY_FETCH=1`.
**When to use:** all production git calls, `RepoSnapshot`, and `trace-run.ps1`.
**Evidence (probe, Git for Windows 2.55.0):**

| Scenario | Result |
|----------|--------|
| `rev-parse --verify --quiet --end-of-options <abbrev>^{commit}` with a marker-writing `core.fsmonitor` hook, stale index, `gc.auto=1`, 8 loose objects | hook not run; `.git` file listing (path, size, mtime) identical before and after |
| `diff-tree -r -z --raw --no-renames --no-abbrev A B` in the same repo | **hook ran** (marker file written outside the repo; git warned `Empty last update token`); `.git` listing unchanged because the hook was a no-op |
| `cat-file --batch-check` and `cat-file --batch` | hook not run |
| `git status --porcelain` (with and without `--no-optional-locks`) | **hook ran**; without `--no-optional-locks` the index was rewritten (listing hash changed), with it unchanged |
| `core.fsmonitor=true`, `diff-tree` | **daemon started** and `.git/fsmonitor--daemon/` created; `git fsmonitor--daemon status` reported it watching the work tree |
| same, with `-c core.fsmonitor=false` | no hook, no daemon, no directory (the `-c` override beats the repository config) |
| partial clone (`--filter=blob:none`), `cat-file --batch-check` of an absent blob | with `GIT_NO_LAZY_FETCH=1`: prints `<oid> missing`, exit 0, `.git` unchanged. Without: fetched the blob, `.git` listing changed |
| `cat-file --batch` of a CRLF blob with `-c core.autocrlf=true` | raw stored bytes returned (CRLF intact): no filters without `--filters` |
| `diff-tree` against a gitlink and a symlink added via `update-index --cacheinfo` | records `:000000 160000 ... A` and `:000000 120000 ... A`; plans' regular-file filter drops both |
| branch named like the 7-hex abbreviation of the head commit; `rev-parse <abbrev>^{commit}` | resolved to the **branch's** commit, not the commit the hex names |
| `rev-parse --git-common-dir` run through `-C <subdir>` | returned a path relative to that subdirectory (`../.git`); `--show-toplevel` returned forward slashes. With `--path-format=absolute --show-toplevel --git-common-dir` (plus `--absolute-git-dir`) from the same subdirectory all three values were absolute, forward-slash paths. A linked worktree was not probed |

The weak probes (gc.auto, `core.hooksPath`) are inconclusive: eight loose objects do not reach any real `gc.auto` threshold and the hooks directory held no hook these commands run. Treat "plumbing never runs gc --auto or hooks" as `[ASSUMED]` and let the repository-unchanged tests, not this research, carry it. The decisive results are fsmonitor, index refresh via `status`, and lazy fetch.

**Do not** set `GIT_CONFIG_NOSYSTEM` or `GIT_CONFIG_GLOBAL` in production: this machine's global config carries `safe.directory` entries and the private clone may need them. Config isolation belongs to `SyntheticRepo` only (as plan 01-02 already has it). Use `-c` overrides for production.

### Pattern 2: Bounded child process (.NET 10)

**What:** `ProcessStartInfo.ArgumentList`, `UseShellExecute=false`, redirect all three streams, write stdin on its own task through `StandardInput.BaseStream`, drain stdout and stderr concurrently as bytes, `Kill(entireProcessTree: true)` on breach.
**Evidence (probe, .NET 10 on Windows 11):**
- `Process.Start("probe-tool-x")` with `UseShellExecute=false`, where that non-system executable (a renamed copy of a probe build, not on PATH) sat only in the current directory, threw `Win32Exception`: the current directory is not searched, so a planted `git.exe` there is not run [VERIFIED: probe].
- Writing 20 MB to the stdin of a child that had already exited threw `IOException`. `BoundedProcess` must catch it on the writer task and let the child's exit code and stderr decide the result (this is plan 01-05's "exits at once" case).
- `Kill(entireProcessTree: true)` removed the grandchild (`ping` count 1 to 0).
- A `ProjectReference` from the test project to an Exe project copies `fakesidecar.exe`, `.dll`, `.runtimeconfig.json` and `.deps.json` into the test output (plan 01-05's "confirm the apphost lands" is satisfied by default).

### Pattern 3: Sidecar parse with a deadline (tree-sitter 0.27.0)

See Code Examples 1. The Rust crate has **no** `set_timeout_micros`: `grep -n timeout binding_rust/lib.rs` finds nothing in 0.27.0; `ParseOptions` has exactly one field, `progress_callback` [VERIFIED: ~/.cargo registry source tree-sitter-0.27.0 `binding_rust/lib.rs:216-218`: `pub struct ParseOptions<'a> { pub progress_callback: Option<ParseProgressCallback<'a>>, }`]. The C API names the callback as the only cancellation path [VERIFIED: `include/tree_sitter/api.h` doc of `ts_parser_parse`: "Parsing was cancelled due to the progress callback returning true"] and says a cancelled parser "will resume where it left off on the next call" unless `ts_parser_reset` is called [VERIFIED: api.h doc of `ts_parser_reset`]. Behaviour confirmed by probe: a 20 ms budget cancelled a 12 MB parse in 24 ms; reusing the parser without `reset()` returned a tree whose root ended at byte 86,649 for a 19-byte input.

### Pattern 4: Mermaid classDiagram output

Measured with mermaid-cli 12.2.0 (mermaid 12.3.0 resolved by the lock), Chrome, `--ignore-scripts` install. "Parse" below means `mmdc` exit 0 with a real render; rendering fidelity was not inspected.

| Input | Result |
|-------|--------|
| plan format: `class Id["ns::T"]:::modified {` ... `+size() const [modified]` ... `classDef` x3, `A <|-- B` | exit 0 |
| member text with `operator==(const T& o) const`, `operator<<`, `std::map<int, std::vector<char*>> m`, `T&&`, function-pointer, `= 0`, default argument with comma, `template <typename U> ...`, `using X = std::pair<int,int>`, `%%`, `#`, `;`, `::` | exit 0 |
| raw `~` in a member (`+~A()`), trailing `*` or `$` | exit 0 (fidelity of `~...~` generics and classifiers not inspected; keep the plan's escapes) |
| raw `{` or `}` in member text; unbalanced brace | exit 1 `Parse error on line N` |
| raw `"` inside a `["label"]` | exit 1 |
| `[` `]` inside a quoted label; label starting with a digit; duplicate `class A` lines; CRLF line endings; 600 classes (4.8 s); 299 edges | exit 0 |
| bare `classDiagram` with nothing else | **exit 1** (`Parse error`); `classDiagram` plus only `classDef` lines: exit 0 |
| diagram text over **50,000 characters**, valid or broken | **exit 0 and byte-identical SVG** containing the "maximum text size" error diagram: not parsed |
| same big diagrams with `-c config.json` containing `{"maxTextSize": 2000000}` | valid: exit 0, 1.2 MB SVG; broken: exit 1 `Parse error on line 2503` |

mmdc needs about 1.2-1.6 s per invocation. `maxEdges` also has a default (believed 500) `[ASSUMED]`; set it in the same config.

### Anti-Patterns to Avoid

- **Taking the innermost leaf of a declarator as the member name.** Wrong for destructors and operators (Pitfall 3).
- **Reusing a `Parser` after a cancelled parse without `reset()`.**
- **Treating any `mermaid-cli` exit 0 as "syntax ok" without raising `maxTextSize`.**
- **Calling `git` without `-c core.fsmonitor=false`.**
- **`Matcher()` with the default comparer** (case-insensitive).
- **`List.Sort`/`Array.Sort` for output ordering** (unstable) and culture-sensitive string APIs (Pitfall 9).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| C++ parsing | a regex/scan parser for classes | tree-sitter via the sidecar (D11, ADR-001) | grammar edge cases measured below (templates, operators, preprocessor wrappers) |
| Process timeout and tree kill | custom polling or `taskkill` calls | `Process.Kill(entireProcessTree: true)` + `CancellationToken` | verified to remove grandchildren |
| Glob matching | custom wildcard code | `Microsoft.Extensions.FileSystemGlobbing` with `StringComparison.Ordinal` | edge cases of `**`, `./`, separators |
| JSON | string concatenation | `System.Text.Json` with `SnakeCaseLower`, `NewLine = "\n"` | measured: default indented output on Windows uses CRLF |
| HTML escaping | custom replace chains | `WebUtility.HtmlEncode` | plan 01-08 already specifies it |
| Mermaid syntax validation | a regex "validator" as the only check | mermaid-cli with raised `maxTextSize`, plus the structural `MermaidLint` for fast tests | only the real parser knows its grammar |
| Commit id resolution | trusting `rev-parse` of a short hex | `rev-parse --verify --end-of-options <hex>^{commit}` plus a prefix check on the result | branch named like an abbreviation shadows it (measured) |

**Key insight:** every "simple" step in this phase (reading a commit, naming a member, checking a diagram) has an environment-dependent trap that only an experiment shows; the plans already pick the right libraries, the corrections below are about their settings.

## Runtime State Inventory

Omitted: greenfield phase, not a rename, refactor or migration. The repository currently holds only `.planning/`, `docs/` and `.claude/` (verified by listing the root).

## Common Pitfalls

### Pitfall 1: fsmonitor turns a read into a write (and an arbitrary command)
**What goes wrong:** `git diff-tree` and `git status` read the index and call the configured `core.fsmonitor`; with `true` the Windows daemon starts and creates `.git/fsmonitor--daemon/`; with a path, an arbitrary command from repository config runs.
**Why it happens:** the probe shows the hook runs for `diff-tree` and `status` but not for `rev-parse` or `cat-file` `[VERIFIED: probe]`; that `diff-tree` loads the index (for attributes) is the suspected cause `[ASSUMED]`.
**How to avoid:** `-c core.fsmonitor=false` on every git call including `RepoSnapshot` and `trace-run.ps1`.
**Warning signs:** a `.git/fsmonitor--daemon` directory or process after a run; a stderr line `warning: Empty last update token`. This machine has no global fsmonitor setting (checked by key name only), so the planned sandbox tests would not catch it without a deliberate scenario.

### Pitfall 2: cancelled tree-sitter parser resumes the old parse
**What goes wrong:** with `map_init` per-thread parsers, a file that hit its deadline poisons the next file on that thread. **How to avoid:** `parser.reset()` whenever `parse_with_options` returns `None`; never `unwrap()` it (the S2 trial does `parser.parse(&src, None).unwrap()`). `[VERIFIED: probe]`

### Pitfall 3: tree-sitter-cpp 0.23.4 node shapes differ from the plans in five places
Measured shapes (field names as printed by a tree walker; `declarator:` is a field name):

| Source | Shape |
|--------|-------|
| `virtual ~Gadget() = default;` | `function_definition` > `declarator: function_declarator` > `declarator: destructor_name` > child `identifier "Gadget"`; `default_method_clause`. The leaf is `Gadget`, so "innermost leaf" gives the wrong name; use the text of the `destructor_name` node (`~Gadget`) |
| `bool operator==(...) const;` | `field_declaration` > `function_declarator` > `declarator: operator_name` (a leaf, text `operator==`); `type_qualifier` is a child of the `function_declarator`. `operator()` is `operator_name` too. `operator new` text is `operator new` |
| `Foo& ref();` | `field_declaration` > `declarator: reference_declarator` > unnamed child `function_declarator` (**no `declarator` field** on `reference_declarator`); `pointer_declarator` does have the field |
| `operator bool() const;` and `operator std::string() const {...}` | `declaration` or `function_definition` > `declarator: operator_cast` > `type:` (`bool`) and `declarator: abstract_function_declarator` (`parameters`, `type_qualifier`). Not "operator in the type position" as plan 01-06 says; there is no `function_declarator` on the path |
| `int (*cb)(int);` | `function_declarator` whose `declarator` is `parenthesized_declarator` > `pointer_declarator` > `field_identifier "cb"` (a field, per the plan's "plain name" rule) |
| `void (S::*pm)(int);` | contains an `ERROR` node in this grammar (pointer to member); file gets `error_nodes >= 1` |
| `int a = 1, b = 2;` | one `field_declaration` with `declarator`, `default_value`, `declarator`, `default_value` (one member per declarator, as planned) |
| `virtual void g() override = 0;` | `field_declaration`; `= 0` is `default_value`; `override` is a `virtual_specifier` under the `function_declarator`. `= default` is `default_method_clause`; plan 01-06's rule "`= 0` stays in signature" is consistent with "cut at the body": define the cut as the start of `body` (`compound_statement`, `field_initializer_list`), not the `default_value` |
| `enum class Mode : unsigned char {...}` | `enum_specifier` with `name`, `base: sized_type_specifier` (text `unsigned char`), `body: enumerator_list`; `enumerator` has `name` and optional `value`. Use the `base` field text, not its leaf `primitive_type "char"` |
| `class D final : public C {}` | `class_specifier`: `name`, `virtual_specifier`, `base_class_clause` (children: `access_specifier`, `type_identifier`/`template_type`), `body: field_declaration_list` |
| `namespace shop::billing {` | `namespace_definition` with `name: nested_namespace_specifier` (children `namespace_identifier`), `body: declaration_list`; anonymous namespace has no `name`; `inline namespace v1` has `name` |
| `template <class T> struct Spec<T*> {}` | `template_declaration` > `struct_specifier` whose `name` is a `template_type` (text `Spec<T*>`) |
| `class __declspec(dllexport) Dll : public B {}` | handled natively: `ms_declspec_modifier` child, `name` intact. No `--blank` needed for it |
| `class alignas(8) Al {}`, `class [[nodiscard]] Nd {}` | native: `alignas_qualifier`, `attribute_declaration` children |
| `struct S2 { int a;` `#if X` `int b;` `#endif };` | member `b` sits under `preproc_if` (also `preproc_ifdef`, `preproc_else` via the `alternative` field), **not** a direct child of `field_declaration_list`. Same at namespace and top level; `#if/#else` with two definitions yields two `class A` (plan 01-07's `#2` policy handles it) |
| `extern "C++" { class L {}; }` | `linkage_specification` > `body: declaration_list` > `class_specifier` |
| `friend bool operator==(...)`, `using Base::f;`, `static_assert` | `friend_declaration`, `using_declaration`, `static_assert_declaration`: yield nothing, as planned |
| `struct { int a; } anon;` | unnamed `struct_specifier` (no `name`): not a type; field `anon` extracted |
| `void ff() { struct Local {...}; }` | `class_specifier` found by any-depth recursion inside a `compound_statement`; its scope would omit the function |
| `class EXPORT_API Foo : public Base { int x; };` (unblanked) | parsed as a `function_definition` whose return type is a `class_specifier` named `EXPORT_API` with no body, plus `ERROR`: no `class Foo` (plan 01-06 task 2's "without `--blank`" test holds) |
| `class Dll { Q_OBJECT int y; DECLARE_X(Foo) public: int z; };` | bogus members (`Q_OBJECT` as a type, `DECLARE_X(Foo)` as a declaration) plus `ERROR`/`MISSING ;`. `--blank Q_OBJECT` helps; macro calls with arguments are not fixable by blanking a name |

`ERROR` and `MISSING` nodes: the missing token (`;`) is an anonymous node, so counting must walk all children (`is_error()`/`is_missing()`), not only named ones. Use `TreeCursor` iteration, not recursion.

Parser robustness (probe, release build, all completed without crash): 10,000 nested namespaces 20 ms; 200,000 nested parentheses 175 ms; 20,000 nested template arguments about 1 s; 1,000,000 `{` 380 ms; 120,000 repeated class lines (4 MB) about 1 s; 50,000-deep initialiser braces 31 ms. No pathological hang was found, so the progress-callback deadline is cheap insurance, not a response to an observed hang `[VERIFIED: probe]`. The depth cap in plan 01-06 protects the extractor's own recursion, not tree-sitter.

### Pitfall 4: lossy text and byte cuts in the sidecar
The S2 trial's helper is `std::str::from_utf8(&src[n.start_byte()..n.end_byte()]).unwrap_or("")` [VERIFIED: docs/spikes/S2-trial/B/src/main.rs:67]: invalid UTF-8 inside a node (a Latin-1 byte in a comment inside a member) silently becomes an empty string. Plan 01-01's "cut at 4096 bytes" can also split a multi-byte character. Use `String::from_utf8_lossy`, and cut on a char boundary.

### Pitfall 5: `key` and `signature` text carry whitespace
Parameter-list text spans lines in real headers; plan 01-01 collapses whitespace for `signature` but states the `key` as "name immediately followed by the parameter-list text". Collapse whitespace in `key` too, or CRLF/LF or reflow changes alter keys and produce remove-plus-add noise.

### Pitfall 6: abbreviated commit id shadowed by a ref
**What goes wrong:** `--base abc1234` resolves to a branch called `abc1234` if one exists. `[VERIFIED: probe]` **How to avoid:** after `rev-parse`, require `fullId.StartsWith(input, StringComparison.OrdinalIgnoreCase)`; add a hostile test.

### Pitfall 7: Windows MAX_PATH breaks the 200-character-name fixture
`git add` of `include/<200 chars>.h` failed with `Filename too long` in a sandbox whose root was 118 characters long; with `core.longpaths=true` it succeeded. A sandbox under a typical `%TEMP%` (about 75 characters) plus `\repo\include\` plus a 200-character name is also above 260. Set `core.longpaths=true` in `SyntheticRepo`, or build such entries without a working tree (`hash-object -w` + `update-index --add --cacheinfo` + `commit-tree`). The same plumbing builds symlink (`120000`) and gitlink (`160000`) fixtures, which cannot be made reliably on Windows. `[VERIFIED: probe]`

### Pitfall 8: git version floor for lazy-fetch protection
`git --no-lazy-fetch` appears in the Git 2.45.0 release notes and not in 2.44.0 `[CITED: kernel.org RelNotes 2.45.0 and 2.44.0]`. Those notes name only the option; that the `GIT_NO_LAZY_FETCH` variable arrived with it and is equivalent is `[ASSUMED]` (a search summary of the mailing-list patch says so; the variable itself worked on 2.55.0 in the probe). Older git ignoring the variable silently is also `[ASSUMED]`. Behaviour is verified on 2.55.0.windows.3 only. A version gate (refuse, or warn when the repo has a promisor remote) turns a silent write into a loud failure.

### Pitfall 9: determinism hazards in .NET
Measured: indented JSON on Windows uses CRLF unless `NewLine` is set to a line feed; the default encoder escapes less-than, greater-than, ampersand, apostrophe, plus and all non-ASCII characters as backslash-u sequences (so `operator<<` is written with escapes); `SnakeCaseLower`, omitted nulls and record declaration order all work. A string holding a soft hyphen (U+00AD) between `a` and `b` is reported as starting with `ab` under the default culture comparison and as not starting with it under `StringComparison.Ordinal`. Further: `List.Sort` and `Array.Sort` are unstable (use `OrderBy` or total comparers with tiebreaks); `IndexOf(string)`, `StartsWith`, `EndsWith` and `ToLower` are culture-sensitive (use ordinal and invariant forms); and `Matcher()` defaults to `OrdinalIgnoreCase` [VERIFIED: probe: `new Matcher()` matched `Include/A/x.h` against `include/a/**`; `new Matcher(StringComparison.Ordinal)` did not]. Multi-line raw or verbatim string literals in `.cs` files take the file's line endings; on this machine (`core.autocrlf=true`, checked) a CRLF working copy would put CR into a template-based HTML writer `[ASSUMED]`. Plan 01-08's "LF only" assertion would catch it on Windows; the fix is `*.cs text eol=lf` in `.gitattributes` (plan 01-01 task 1 owns that file) or normalising text before writing. Consider `InvariantGlobalization` in the project files.

### Pitfall 10: Mermaid text-size trap
Over 50,000 characters mermaid-cli exits 0 without parsing. See Pattern 4 and Plan Corrections C43, C48. `[VERIFIED: probe]`

### Pitfall 11: sidecar total-size mismatch
Plan 01-01's sidecar rejects a request over 512 MiB total; plan 01-02 caps each blob at 4 MiB but sets no total. A large PR with many near-cap blobs would make the sidecar exit 2 and the whole run fail rather than mark files unmodelled. Cap the total on the .NET side below the sidecar's cap with a deterministic overflow rule (ordinal path order, reason `total-too-large`).

### Pitfall 12: shell tooling mangles backslashes
Executing agents using bash heredocs or `node -e` to write files that contain `\\` (for example a Windows browser path or a JSON string) got single backslashes (`C:Program FilesGoogle...`). It is a harness artefact, not a mermaid-cli defect. Write such files with the Write tool; in committed scripts build the config through `JSON.stringify` (or use forward slashes, which Chrome accepts).

## Code Examples

### 1. Bounded parse with a deadline (compiled and run against the pinned crates)
```rust
// Source: tree-sitter 0.27.0 binding_rust/lib.rs (parse_with_options, ParseOptions); probe
use std::ops::ControlFlow;
use std::time::{Duration, Instant};
use tree_sitter::{ParseOptions, Parser};

fn parse_bounded(p: &mut Parser, src: &[u8], budget: Duration) -> Option<tree_sitter::Tree> {
    let deadline = Instant::now() + budget;
    let mut cb = |_: &tree_sitter::ParseState| {
        if Instant::now() >= deadline { ControlFlow::Break(()) } else { ControlFlow::Continue(()) }
    };
    let tree = p.parse_with_options(
        &mut |i, _| if i < src.len() { &src[i..] } else { &[][..] },
        None,
        Some(ParseOptions::new().progress_callback(&mut cb)),
    );
    if tree.is_none() { p.reset(); } // mandatory: otherwise the next parse resumes this one
    tree
}
```
The caller turns `None` into a per-file result (`status: "parse-timeout"`) and keeps going; with `map_init` one parser per thread stays valid after `reset()`.

### 2. Read-only git argument prefix
```csharp
// Source: probe results in Pattern 1; ArgumentList, no shell
static readonly string[] GitPrefix = { "-c", "core.fsmonitor=false", "--no-optional-locks" };
// full: git -c core.fsmonitor=false --no-optional-locks -C <repo> diff-tree -r -z --raw --no-renames --no-abbrev <base> <head>
// env: remove GIT_DIR GIT_WORK_TREE GIT_INDEX_FILE GIT_COMMON_DIR GIT_NAMESPACE GIT_EXTERNAL_DIFF;
//      set GIT_OPTIONAL_LOCKS=0 GIT_TERMINAL_PROMPT=0 GIT_NO_LAZY_FETCH=1
```
Raw record format verified: `:<mode> <mode> <oid> <oid> <status>\0<path>\0`; an added file has an all-zero base oid (40 zeros), a deleted file an all-zero head oid; `A`/`M`/`D` seen, a type change is `T`. Both sides of a record must be judged separately (a regular file turned symlink is "gone" on the head side).

### 3. Commit id resolution
```csharp
// Source: probe (branch named like an abbreviation shadows the hash)
var full = RunGit("rev-parse", "--verify", "--quiet", "--end-of-options", hex + "^{commit}").Trim();
if (!full.StartsWith(hex, StringComparison.OrdinalIgnoreCase)) throw new VdiffException("commit id is shadowed by a ref");
```

### 4. Local Mermaid check configuration (verified end to end)
```js
// Source: probe; mermaid-cli 12.2.0 flags -p (puppeteer config), -c (mermaid config), -i, -o
// puppeteer config:  { "executablePath": "<chrome or edge path>" }        (write with JSON.stringify)
// mermaid config:    { "maxTextSize": 2000000 }                            (without it >50,000 chars is not parsed)
// spawnSync(process.execPath, [cli, "-p", pcfg, "-c", mcfg, "-i", file, "-o", svgPath])  // exit 1 on "Parse error"
```
Install: `npm install --save-exact --ignore-scripts @mermaid-js/mermaid-cli@12.2.0 puppeteer@25.17.0` then commit the lock; `npm ci --ignore-scripts` reproduces it (192 packages in 29 s).

### 5. JSON settings
```csharp
// Source: probe
var o = new JsonSerializerOptions {
  PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, WriteIndented = true, NewLine = "\n",
  DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
  Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }; // optional: readable operator<<; safe because JSON is never embedded in HTML
```

### 6. Glob matcher
```csharp
// Source: probe
var m = new Matcher(StringComparison.Ordinal); m.AddInclude("include/a/**");   // "./src/**" also works; "*.h" is not recursive
// no includes => matches nothing, so the "no --include" case must bypass the Matcher
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `Parser::set_timeout_micros` | `parse_with_options` + `ParseOptions::progress_callback` returning `ControlFlow::Break` | absent in the pinned 0.27.0 (deprecation history `[ASSUMED]`) | sidecar time bound must be built by us |
| `dotnet new sln` creating `.sln` | SDK 10 default is `.slnx`; use `--format sln` | .NET 10 | plan 01-02 already handles it; verified `--format sln` works |
| mermaid-cli 11 | 12.2.0 with mermaid 12.3.0, puppeteer ^25 | 2026-09 to 2026-11 | `-p`/`-c`/`-i`/`-o` flags unchanged; works with system Chrome |
| `git cat-file --batch` lazy fetch | `GIT_NO_LAZY_FETCH` / `--no-lazy-fetch` | Git 2.45 | version gate needed |

**Deprecated/outdated:** none relevant found in the pinned stack.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `rev-parse`, `diff-tree` and `cat-file` never run `gc --auto`, maintenance or hooks (probes were inconclusive) | Pattern 1 | A write into `.git` the tests do not provoke; caught only by the matrix tests on a repo configured to trigger it |
| A2 | Git older than 2.45 silently ignores `GIT_NO_LAZY_FETCH`, and the variable shipped together with `--no-lazy-fetch` in 2.45 | Pitfall 8 | Lazy fetch writes into `.git` on an older git with a partial clone; the version gate (C13) would be set at the wrong version |
| A3 | `diff.orderFile` may reorder `diff-tree` output | Plan Corrections C14 | none if the C# side sorts anyway (recommended) |
| A4 | Mermaid renders raw `~...~`, trailing `*`/`$`, `<...>` as generics/classifiers rather than literal text | Pattern 4 | Diagram reads oddly but still parses; keep the plan's escapes |
| A5 | mermaid `maxEdges` default is 500 and behaves like `maxTextSize` | Pattern 4 | Large diagrams unparsed silently; set both in the config |
| A6 | CRLF in `.cs` multi-line literals would leak into outputs on autocrlf machines | Pitfall 9 | LF-only test fails on Windows; fixed by `.gitattributes` |
| A7 | Edge works as `executablePath` with the same config | Plan 01-09 | Browser detection falls back to Chrome |
| A8 | Ubuntu runner behaviour (git version, Chrome `--no-sandbox`) | whole phase | Out of scope: first pass is local Windows (STATE.md) |
| A9 | An unnamed `typedef struct { ... } Name;` is silently not extracted and not labelled (shape not probed) | Pitfall 3 | A silently missing type class in older C++; Phase 4 decision |
| A10 | `safe.directory` ownership refusal surfaces as a git exit 128 and therefore a normal `vdiff:` failure | Security | Unclear message on the private clone; not probed |
| A11 | `--end-of-options` for `rev-parse` needs a git well below 2.55 | Pattern 1 | Works on 2.55.0 (verified); older floor unknown |
| A12 | Deprecation history of `set_timeout_micros` (removed in 0.25/0.26) | State of the Art | None; present-state facts are verified |
| A13 | `diff-tree` reads the index for attribute lookup (the cause of the fsmonitor hook call) | Pitfall 1 | None for the fix (`-c core.fsmonitor=false` is verified); only the explanation |

## Open Questions

1. **D-02 wording versus the plans' `diff-tree`.**
   - What we know: D-02 (source: Orch, not PO) lists `git ls-tree`, `git cat-file --batch`, `git diff --name-status`; plan 01-02 uses `diff-tree -r -z --raw --no-renames --no-abbrev` and states the intent is unchanged. Raw plumbing returns modes and both blob ids NUL-delimited (verified), which `--name-status` does not.
   - What's unclear: nobody has acknowledged the deviation from the locked wording.
   - Recommendation: record it as a one-line acknowledged refinement in CONTEXT.md (orchestrator), not as a silent endorsement. Both forms need `-c core.fsmonitor=false`.
2. **Where does the npm checkpoint run?** Config says `human_verify_mode: end-of-phase`; plan 01-03 gates mid-phase. Recommendation: ask the PO once at the start of execution and record the answer; the evidence above shows the packages are the genuine current releases but the seam verdict stays SUS.
3. **Minimum git version.** Windows 2.55 here; the PO's clone machine and any Ubuntu runner (Phase 9) may be older. Recommendation: gate on `git --version >= 2.45` for the lazy-fetch guarantee, or detect a promisor remote and refuse.
4. **Whole-run failure versus per-file labelling for sidecar timeouts.** Plans treat a sidecar failure as a tool failure (ADR-001). A per-file parse deadline lets one pathological file become `unmodelled` instead. Decide whether the tracer adds `status` per file to protocol v0 (recommended, small).
5. **Anonymous-struct typedefs and macro-with-argument members** are silently missing or bogus; confirm they are acceptable for TRC-03 or add them to Phase 4's list.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| git | all git reads | yes | 2.55.0.windows.3 | — |
| .NET SDK | CLI, tests | yes | 10.0.401 (also 9.0.318) | — |
| cargo / rustc, C compiler | sidecar build | yes | 1.97.1 (cold build 76 s) | — |
| node / npm | mermaid check | yes | 26.12.1 / 12.2.1 (needs >= 22.13) | skip with `skipped` (plan 01-09 policy) |
| Chrome / Edge | mermaid-cli | yes | Chrome and Edge present | `VDIFF_MERMAID_BROWSER` |
| PowerShell | `trace-run.ps1`, tests | yes | pwsh 7.7.0 and Windows PowerShell 5.1 | — |
| WSL Ubuntu dotnet/cargo | optional Linux run | per CONTEXT.md, not probed | — | out of scope this pass |

**Missing dependencies with no fallback:** none.
**Missing dependencies with fallback:** none. Global git config on this machine carries `safe.directory` entries (keys listed by name only); it does not set `core.fsmonitor`.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit 2.9.3 on Microsoft.NET.Test.Sdk 17.14.1 (.NET side); `cargo test` (Rust side) |
| Config file | `tests/Vdiff.Tests/Vdiff.Tests.csproj` (created by plan 01-02); `extractors/cpp-ts/Cargo.toml` (plan 01-01) |
| Quick run command | `cargo test --locked --manifest-path extractors/cpp-ts/Cargo.toml` and `dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --nologo --filter "FullyQualifiedName~<Class>"` |
| Full suite command | `dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --nologo` then `cargo test --locked --manifest-path extractors/cpp-ts/Cargo.toml` |

Output formats verified: `dotnet test` prints `Passed!  - Failed:     0, Passed:     6, Skipped:     0, Total:     6, Duration: ...` (the plans' regex `Passed! +- Failed: +0, Passed: +N` matches); a filter that matches nothing prints `No test matches the given testcase filter` and **exits 0**; `Category=` trait filters and `A|B` filters work; a collection fixture was constructed once across two classes in one collection. `cargo test` prints `test result: ok. N passed; 0 failed; ...`; a filter matching nothing prints `ok. 0 passed ... 2 filtered out` and exits 0 (the plans' `[1-9][0-9]* passed` regexes correctly reject it).

### Phase Requirements to Test Map

| Req ID / criterion | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| SPK-02 / SC5 | ADR status line is `Accepted (PO, 2026-11-16)`; sidecar pinned and versions reported | unit + grep | `cargo test --locked ... ` (version-guard test) and `grep -c "^- \*\*Status:\*\* Accepted (PO, 2026-11-16)" docs/decisions/ADR-001-cpp-tree-sitter-packaging.md` | planned 01-01 T1, 01-10 T3 (new) |
| SPK-02 / SC5 | tracer extraction runs through the sidecar; metadata in every model | integration | `dotnet test ... --filter "FullyQualifiedName~TracerEndToEndTests"` | planned 01-03 |
| TRC-01 / SC2 | repo identical before/after in every state (dirty, stash, worktree, detached, bare, neither commit checked out) | integration | `... --filter "FullyQualifiedName~RepoUnchangedMatrixTests"` | planned 01-04; **add fsmonitor-hook scenario (C25)** |
| TRC-01 | hostile args never reach git as options; abbreviated-id shadowing | integration | `... --filter "FullyQualifiedName~HostileInputTests"` | planned 01-04; **add shadowing case (C26)** |
| TRC-01 | env hardening, pure environment builder | unit | `... --filter "FullyQualifiedName~GitObjectReaderTests"` | planned 01-02 |
| TRC-02 / SC1 | diff.json lists exactly expected types and members | integration | `... --filter "FullyQualifiedName~TracerEndToEndTests"` | planned 01-03 |
| TRC-02 | member-level states, duplicates, canonical order | unit | `... --filter "FullyQualifiedName~ModelDifferTests\|FullyQualifiedName~ModelBuilderTests"` | planned 01-07 |
| TRC-02 | extraction shapes (enums, operators, templates, preproc, error nodes, deadline) | cargo | `cargo test --locked --manifest-path extractors/cpp-ts/Cargo.toml` | planned 01-01/01-06; **add C1-C7 and C32 cases** |
| TRC-02 / SC3 | Mermaid structure + injection safety | unit | `... --filter "FullyQualifiedName~MermaidWriterTests"` | planned 01-08 |
| TRC-02 / SC3 | Mermaid real syntax check, including a >50,000-char case and a zero-change diagram | integration | `... --filter "Category=MermaidCli"` | planned 01-09; **add C43/C44** |
| TRC-02 | HTML self-contained and encoded; byte-identical deterministic outputs, LF, no local path | unit + integration | `... --filter "FullyQualifiedName~HtmlWriterTests\|FullyQualifiedName~DeterminismTests"` | planned 01-08 |
| TRC-01/TRC-02 | sidecar misbehaviour (hang, flood, exit, garbage) is exit 1, nothing written | integration | `... --filter "FullyQualifiedName~SidecarBoundsTests"` | planned 01-05 |
| TRC-03 / SC4 | script prints six fixed lines, no leaks, correct failure reasons | integration | `... --filter "FullyQualifiedName~TraceRunScriptTests"` | planned 01-10 |
| TRC-03 / SC4 | real run on private clone inside R26 budget | manual (human-action) | orchestrator runs `pwsh -NoProfile -File scripts/trace-run.ps1 ...` | planned 01-10 T2 |
| hygiene (D-09) | no output/build dirs tracked; ignore rules; no remote use in tests | integration | `... --filter "FullyQualifiedName~RepoHygieneTests"` | planned 01-04 |

### Sampling Rate
- **Per task commit:** the task's own `--filter` or `cargo test <module>` command (each under about 30 s after the one-time sidecar build; the cold sidecar build is 76 s and runs once through the collection fixture).
- **Per wave merge:** full `dotnet test` plus full `cargo test`.
- **Phase gate:** both full suites green, `git ls-files` shows no output/build paths, then the manual TRC-03 run.

### Wave 0 Gaps
- None before plan 01-01/01-02: the repository has no code or tests yet, and the plans create all infrastructure (solution, test project, `SyntheticRepo`, `RepoSnapshot`, `SidecarFixture`, fake sidecar).
- Additions to existing tasks, not new waves: fsmonitor-hook scenario in `RepoUnchangedMatrixTests`; a `RepoSnapshot` mode that passes `-c core.fsmonitor=false`; a sidecar-deadline cargo test using a header-supplied budget; a >50,000-character Mermaid case.

## Security Domain

`security_enforcement` is enabled in `.planning/config.json` (ASVS level 1, block on high).

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | no accounts, no network |
| V3 Session Management | no | — |
| V4 Access Control | partly | output confined to `--out`; refuse `--out` inside the analysed repo (`rev-parse --path-format=absolute` results normalised with `Path.GetFullPath`) |
| V5 Input Validation | yes | hex-only commit ids, identifier-only macro names, glob count/length caps, bounded JSON protocol with checked arithmetic; ArgumentList, no shell |
| V6 Cryptography | no | none used |
| V10/V14 Supply chain and configuration | yes | exact pins, committed lock files, `--ignore-scripts`, `--locked`; repo-controlled git config neutralised by `-c core.fsmonitor=false` |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Repo-controlled config executes a command during a "read" (`core.fsmonitor`) | Elevation of privilege / Tampering | `-c core.fsmonitor=false` (verified); matrix scenario with a marker hook |
| Argument injection through commit ids (`--output=...`) | Tampering | hex validation, `--end-of-options`, ArgumentList |
| Ref named like a hex abbreviation redirecting the analysis | Spoofing | prefix check on the resolved id |
| Lazy fetch writing into `.git` | Tampering | `GIT_NO_LAZY_FETCH=1` plus version gate |
| Hostile source crashing or hanging native parser | Denial of service | child process, caps, deadline callback with `reset()` |
| Repository text becoming Mermaid structure or HTML | Tampering | single escape function (braces and quotes verified to break parsing), `HtmlEncode`, structural lint |
| Private data in the public repo or transcript | Information disclosure | `.gitignore`, hygiene tests, six-line vocabulary script |
| Mermaid check reporting success on unparsed text | Repudiation (false assurance) | raise `maxTextSize`; test with a >50,000-char broken diagram |
| npm install scripts | Tampering | `--ignore-scripts`, exact pins, lockfile, human checkpoint |

## Sources

### Primary (HIGH confidence)
- Probes run this session, synthetic data only: git 2.55.0.windows.3 plumbing behaviour (fsmonitor, lazy fetch, shadowing, raw formats, long paths, toplevel); tree-sitter 0.27.0 + tree-sitter-cpp 0.23.4 tree dumps and parse cancellation; mermaid-cli 12.2.0 end-to-end syntax cases; .NET 10 Process behaviour; `dotnet test` and `cargo test` output formats; System.Text.Json settings; FileSystemGlobbing `Matcher`.
- tree-sitter 0.27.0 crate source (`binding_rust/lib.rs:185-250, 915-1035`, `include/tree_sitter/api.h`) from the local cargo registry.
- docs/decisions/ADR-001-cpp-tree-sitter-packaging.md; docs/spikes/S2-trial/B/{Cargo.toml,Cargo.lock,src/main.rs}; docs/spikes/S1-mermaid-in-ado-pr.md; the ten plans, CONTEXT.md, ROADMAP.md, REQUIREMENTS.md, STATE.md.
- `npm view` for `@mermaid-js/mermaid-cli` and `puppeteer`; api.nuget.org flat-container indexes; GSD `package-legitimacy check` seam output.

### Secondary (MEDIUM confidence)
- Git 2.45.0 release notes (`--no-lazy-fetch`, kernel.org) and the absence of the term in the 2.44.0 notes.

### Tertiary (LOW confidence)
- Search summaries on mermaid-cli config conventions and the git `GIT_NO_LAZY_FETCH` patch history (used only to find primary sources).

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH, versions confirmed by registry, template output and builds.
- Architecture: HIGH for read-only git, sidecar API and Mermaid behaviour (probed); MEDIUM for Linux and older-git behaviour (not probed).
- Pitfalls: HIGH where marked VERIFIED; see the Assumptions Log for the rest.

**Research date:** 2026-11-16
**Valid until:** 2026-12-16 for npm packages (weekly releases); the git, tree-sitter and .NET findings hold while the pins hold.

## Plan Corrections

Priority: **H** must fix before or while executing; **M** fold into the task; **L** note.

### Plan 01-01 (Rust sidecar)

| # | Pri | Task | Change | Why | Source |
|---|-----|------|--------|-----|--------|
| C1 | H | T1 (`main.rs`) and 01-05/01-06 tests | Parse with `parse_with_options` and a per-file wall-clock deadline (default about 10 s, overridable by a header field such as `parse_budget_ms` so a cargo test can set 1 ms); on `None` call `parser.reset()`, never `unwrap()`; report the file with a status (`parse-timeout`) and make the .NET side list it as `unmodelled`. Add a cargo test that a cancelled parse is followed by a correct parse on the same thread. | The crate has no `set_timeout_micros`; a cancelled parser resumes the old parse (root end 86,649 for a 19-byte input) | tree-sitter 0.27.0 `lib.rs:216-218`, `api.h`; probe |
| C2 | H | T2 | Member name for `destructor_name` and `operator_name` is the node's own text, not the innermost leaf (leaf is `Gadget`, node text is `~Gadget`) | Plan text "innermost leaf's text, so a destructor is `~Name`" is self-contradictory | probe tree dump |
| C3 | H | T2 | `reference_declarator` has no `declarator` field: find the wrapped `function_declarator`/name among named children. Handle `parenthesized_declarator` (function pointer), `array_declarator`, `pointer_declarator` | Plan assumes a uniform wrapper shape | probe tree dump |
| C4 | H | T2 | Descend through `preproc_if`, `preproc_ifdef`, `preproc_else` (and elif variants) both at namespace/top level and **inside class bodies**; do not limit members to direct children of `field_declaration_list` | Members and types under `#if` would be dropped with no error node | probe tree dump |
| C5 | M | T2 | Define descent explicitly (whitelist: `translation_unit`, `namespace_definition`/`declaration_list`, `template_declaration`, `linkage_specification`, `preproc_*`, `declaration`, `type_definition`, class/struct bodies); do not descend into function bodies. Also descend into `ERROR` nodes: in the broken-class sample, `class Next` was reachable only through an `ERROR` subtree, and plan 01-06 task 3 requires parsed types to come back alongside a non-zero `error_nodes`. Error-node counting walks all children (anonymous `MISSING ;` included) with a `TreeCursor`, not recursion | "found at any depth" picks up local classes with a wrong scope and recurses on hostile depth | probe tree dump |
| C6 | M | T2 | Collapse whitespace in `key` as well as `signature`; define "signature cut" as the start of `body`, keeping `= 0`, `= default`, `= delete` and field default values | multi-line parameter lists; plan 01-01 and 01-06 phrase this differently | probe; plans |
| C7 | M | T2 | Use `String::from_utf8_lossy`, never `from_utf8(...).unwrap_or("")`; cut the 4096-byte limit at a char boundary | trial helper returns empty text on invalid UTF-8; slicing mid-character panics | S2 `main.rs:67` |
| C9 | M | T1 (`.gitattributes`) | Add explicit `*.cs`, `*.rs` and `*.ps1` as `text eol=lf` next to the planned `*.sh`, `Cargo.lock`, `package-lock.json`, `*.mjs` rules (still no catch-all `text=auto`) | CRLF working copies on an autocrlf machine leak CR into multi-line literals (see C22, C23) | Pitfall 9 |
| C10 | L | T1 | Drop the "if `--locked` rejects the rename" fallback; say the lock holds 33 packages (five direct) | verified accepted | probe |

### Plan 01-02 (.NET core read path)

| # | Pri | Task | Change | Why | Source |
|---|-----|------|--------|-----|--------|
| C1b | H | T3 (`CppSidecar`) | Add the per-file `status` field to `SidecarFile` (C1) and make `Extract` return it; unknown properties are ignored by the client, so without this change a timed-out file would arrive with zero types and look clean. Alternative if protocol v0 must not change: report a timed-out file through a non-zero `error_nodes` as plan 01-06 task 3 does for depth skips, but then it cannot be told apart from a parse error | consistency with C1 | plan 01-02 task 3 text; C1 |
| C11 | H | T1 (`RepoSnapshot`), T2 (`GitObjectReader`) | Add `-c core.fsmonitor=false` before `--no-optional-locks` on every git call, including every snapshot command; unit-test the argument builder | `diff-tree` and `status` run the hook and can start the daemon | Pattern 1 table |
| C12 | H | T2 (`ResolveCommit`) | After `rev-parse`, require the full id to start with the (case-insensitive) input | branch named like an abbreviation shadows the commit | Pattern 1 table |
| C13 | M | T2 | Read `git --version` once; refuse (or warn and refuse when a promisor remote exists) below 2.45 | `--no-lazy-fetch` first released in 2.45 | Pitfall 8 |
| C14 | M | T2 | `ListChanged` judges each side by its own mode; handle `T` and all-zero oids; never send a zero oid to `cat-file`; sort in C# (do not rely on git order); build gitlink/symlink fixtures with `hash-object -w` + `update-index --cacheinfo` | Windows cannot create these through the working tree; verified raw records | Code Examples 2; probe |
| C16 | M | T2 (`ToplevelOrGitDir`) | Use `rev-parse --path-format=absolute --show-toplevel --git-common-dir` (and the absolute git dir), then `Path.GetFullPath`; test from a subdirectory and a linked worktree | `--git-common-dir` was relative without the flag and absolute with it (verified from a subdirectory); `--show-toplevel` uses forward slashes; the linked-worktree case is not probed | probe |
| C17 | M | T1 (`SyntheticRepo`) | Set `core.longpaths=true`; keep `GIT_CONFIG_GLOBAL`/`GIT_CONFIG_NOSYSTEM` out of production code | MAX_PATH; `safe.directory` lives in global config | Pitfall 7 |
| C18 | M | T1 (`BoundedProcess`) | Write stdin via `BaseStream`; catch `IOException` on the writer task | verified exception | Pattern 2 |
| C20 | M | T2/T3 | Total blob budget on the .NET side below the sidecar's 512 MiB; overflow becomes `unmodelled` `total-too-large` | otherwise one large PR fails the whole run | Pitfall 11 |

### Plan 01-03 (end to end + checkpoint)

| # | Pri | Task | Change | Why | Source |
|---|-----|------|--------|-----|--------|
| C1c | H | T1 (`ModelBuilder`) | Map a `parse-timeout` file status to an `unmodelled` entry with a fixed reason (`parse-timeout`) so the diff warnings count it | otherwise the label never reaches `diff.json` | C1 |
| C21 | M | T1 (`OutputWriter`) | Keep `NewLine="\n"`; consider `UnsafeRelaxedJsonEscaping` for readable `operator<<` | default encoder escapes `<`, `&`, `'`, `+`, non-ASCII; default NewLine is CRLF | Pitfall 9 |
| C22 | M | T1 | Add `InvariantGlobalization=true` and ordinal string APIs (the `.gitattributes` part is C9, owned by plan 01-01) | culture-sensitive compare measured | Pitfall 9 |
| C24 | M | T2 (checkpoint) | Keep it; resolve it with the PO at execution start instead of blocking wave 2 | `human_verify_mode: end-of-phase` | Package audit |

### Plan 01-04 (safety)

| # | Pri | Task | Change | Why | Source |
|---|-----|------|--------|-----|--------|
| C25 | H | T1 | Add matrix scenario: repo with `core.fsmonitor` set to a marker-writing hook outside the repo (portable) and, on Windows, `core.fsmonitor=true` asserting no `.git/fsmonitor--daemon`; the snapshot's own status call must use `-c core.fsmonitor=false` | not covered by any listed scenario | Pattern 1 |
| C26 | H | T2 | Hostile case: a branch named like the head's 7-hex abbreviation must not change which commit is analysed (error or correct commit, never the branch tip) | verified shadowing | Pitfall 6 |
| C27 | H | T2 | 200-character path fixture needs `core.longpaths=true` or plumbing construction | `git add` fails with `Filename too long` | Pitfall 7 |

### Plan 01-05 (bounds)

| # | Pri | Task | Change | Why | Source |
|---|-----|------|--------|-----|--------|
| C30 | L | T1 | Confirm apphost copy is default (no extra work); "exits at once" case expects `IOException` handling, not a hang | verified | Pattern 2 |
| C31 | M | T1/T2 | Add a sidecar-level deadline test via the fake or the real sidecar's budget field (ties to C1) | per-file bound was missing from both plans | C1 |

### Plan 01-06 (sidecar depth)

| # | Pri | Task | Change | Why | Source |
|---|-----|------|--------|-----|--------|
| C32 | H | T1 | Conversion operators are `operator_cast` (`type` + `abstract_function_declarator`); name is the text from `operator` to the parameter list (`operator bool`), kind method | plan text describes a different shape | Pitfall 3 table |
| C33 | M | T1 | Enum underlying type from the `base` field text | leaf is `char`, text is `unsigned char` | Pitfall 3 table |
| C35 | M | T3 | Keep the 128 cap and 10,000-namespace test but note tree-sitter itself parsed that in about 20 ms; assert on the extractor's recursion, and make the error-count traversal iterative | recursion hazard is ours | Pitfall 3 |
| C36 | L | T2 | Note in the summary that macro calls with arguments inside class bodies produce bogus members that `--blank` cannot fix | measured | Pitfall 3 table |

### Plan 01-07 (differ, scope)

| # | Pri | Task | Change | Why | Source |
|---|-----|------|--------|-----|--------|
| C37 | H | T2 | `new Matcher(StringComparison.Ordinal)` explicitly | default is case-insensitive; plan requires case-sensitive | probe |
| C39 | M | T1 | Use stable `OrderBy` with total comparers (tiebreak file ordinal, start_byte) | `Sort` is unstable | Pitfall 9 |

### Plan 01-08 (writers)

| # | Pri | Task | Change | Why | Source |
|---|-----|------|--------|-----|--------|
| C40 | M | T1 | Keep the always-emitted three `classDef` lines and add a test that a zero-change diagram still parses in 01-09 | a bare `classDiagram` fails | Pattern 4 |
| C41 | L | T1 | Record that braces and `"` break parsing (verified) while `~`, trailing `*`, `<>`, `%%`, `#`, `;` only parse-pass; rendering fidelity unverified | honest scope of the lint | Pattern 4 |
| C23 | M | T2 | If `HtmlWriter` uses a multi-line literal, normalise `\r\n` before writing | autocrlf machine | Pitfall 9 |

### Plan 01-09 (mermaid check)

| # | Pri | Task | Change | Why | Source |
|---|-----|------|--------|-----|--------|
| C43 | H | T1 (`check.mjs`) | Pass `-c <config>` with `{"maxTextSize": 2000000}` (and a raised `maxEdges`); write both configs with `JSON.stringify` | otherwise diagrams over 50,000 characters exit 0 unparsed | Pattern 4 |
| C44 | H | T2 | Add tests: a broken diagram over 50,000 characters must fail; a zero-change diagram must pass | covers C43 and C40 | Pattern 4 |
| C45 | L | T1 | Install and browser handling as written is verified (system Chrome, `--ignore-scripts`, `-p`, exit 1 on `Parse error`); expect 1.2-1.6 s per run | verified | Pattern 4 |

### Plan 01-10 (trial)

| # | Pri | Task | Change | Why | Source |
|---|-----|------|--------|-----|--------|
| C47 | H | T1 (`trace-run.ps1`) | Every git command in the snapshot uses `-c core.fsmonitor=false --no-optional-locks`; add a test on a repo with a marker `core.fsmonitor` hook | the script's own `status` would start the daemon in the private clone | Pattern 1 |
| C48 | H | T1 | `mermaid_syntax` must use the fixed `check.mjs` (C43); a real `summary.mmd` can exceed 50,000 characters | false `ok` | Pattern 4 |
| C49 | L | T1 | `pwsh` 7.7.0 and `powershell.exe` 5.1 are both present; keep scripts ASCII-only for 5.1 | verified availability | Environment |

### Cross-cutting

- **D-02 wording** (Open Question 1): record the `diff-tree` refinement as acknowledged.
- **Verify-command regexes** for `dotnet test` and `cargo test` match the real output formats; leave them as written.

### Keep unchanged (already correct, verified)

- Hex-only 7 to 64 ids validated before any process starts, `rev-parse --verify --quiet --end-of-options <hex>^{commit}`, ArgumentList, no shell.
- Dropping `GIT_DIR`-family variables; `GIT_OPTIONAL_LOCKS=0`, `GIT_TERMINAL_PROMPT=0`, `GIT_NO_LAZY_FETCH=1` (works on 2.55).
- `diff-tree -r -z --raw --no-renames --no-abbrev` with `ParseDiffTree` on NUL records; dropping mode-only changes; keeping only regular files (gitlinks and symlinks appear with modes 160000 and 120000).
- `cat-file --batch-check` first, then `--batch`; blobs are raw (no filters, autocrlf irrelevant); `missing` reported with exit 0.
- Stdin manifest with raw bytes, caps, checked arithmetic, exit 2; one sidecar call per commit; per-thread parsers in input order (after C1).
- `global.json` 10.0.100 with `latestFeature` (resolves to 10.0.401); `dotnet new sln --format sln`; xunit/runner/Test.Sdk versions; `FileSystemGlobbing` 10.0.15.
- `[Collection("sidecar")]` fixture that always runs `cargo build --release --locked` once (cargo test does not rebuild the release binary).
- Fake sidecar selected by environment variable through `VDIFF_CPP_TS`.
- `Kill(entireProcessTree: true)`, concurrent drains, caps (kill verified).
- `NewLine = "\n"`, `SnakeCaseLower`, nulls omitted, UTF-8 without BOM, LF-only assertion.
- Exactly three `classDef` lines always; escapes for `{`, `}`, `"`, `~`; class id sanitising; injection test.
- mermaid-cli pinned install with `--ignore-scripts` and a system browser; distinct exit code 3 for a missing prerequisite; the human checkpoint.
- Six-line fixed-vocabulary script design and the identifier-scan gates in plan 01-10 task 3.
