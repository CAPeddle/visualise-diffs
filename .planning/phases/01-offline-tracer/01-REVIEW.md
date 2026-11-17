---
phase: 01-offline-tracer
reviewed: 2026-11-17T00:00:00Z
depth: standard
files_reviewed: 57
files_reviewed_list:
  - extractors/cpp-ts/src/main.rs
  - extractors/cpp-ts/src/protocol.rs
  - extractors/cpp-ts/src/blank.rs
  - extractors/cpp-ts/src/extract.rs
  - src/Vdiff.Core/VdiffException.cs
  - src/Vdiff.Core/Process/BoundedProcess.cs
  - src/Vdiff.Core/Git/GitObjectReader.cs
  - src/Vdiff.Core/Scope/ScopeFilter.cs
  - src/Vdiff.Core/Extraction/CppSidecar.cs
  - src/Vdiff.Core/Model/ModelTypes.cs
  - src/Vdiff.Core/Model/ModelBuilder.cs
  - src/Vdiff.Core/Diff/ModelDiffer.cs
  - src/Vdiff.Core/Pipeline/TracerPipeline.cs
  - src/Vdiff.Core/Output/OutputWriter.cs
  - src/Vdiff.Core/Output/MermaidWriter.cs
  - src/Vdiff.Core/Output/HtmlWriter.cs
  - src/Vdiff.Core/Cli/CliOptions.cs
  - src/Vdiff.Core/Cli/CliApp.cs
  - src/Vdiff.Cli/Program.cs
  - tools/mermaid-check/check.mjs
  - scripts/trace-run.ps1
  - tests/Vdiff.FakeSidecar/Program.cs
  - .gitignore
  - .gitattributes
  - extractors/cpp-ts/Cargo.toml
  - global.json
  - vdiff.sln
  - src/Vdiff.Core/Vdiff.Core.csproj
  - src/Vdiff.Cli/Vdiff.Cli.csproj
  - tests/Vdiff.Tests/Vdiff.Tests.csproj
  - tests/Vdiff.FakeSidecar/Vdiff.FakeSidecar.csproj
  - tools/mermaid-check/package.json
  - tests/Vdiff.Tests (skimmed for test quality: MermaidCliSyntaxTests, MermaidLint, SidecarFixture, RepoSnapshot, temp-dir handling)
findings:
  critical: 1
  warning: 4
  info: 6
  total: 11
status: issues_found
---

# Phase 01: Code Review Report

**Reviewed:** 2026-11-17
**Depth:** standard
**Files Reviewed:** 57 (production files read in full; tests skimmed for test-quality problems only)
**Status:** issues_found

## Summary

The implementation is careful on the project's headline concerns. Git is invoked read-only
(fsmonitor off, no optional locks, no lazy fetch, a cleaned environment). Every child process
goes through one bounded runner with a timeout, output caps and a tree kill. Commit ids,
macro names and globs are validated before use. Hostile text is HTML-encoded in the report. The
sidecar protocol is length-checked with fixed error messages. I found no injection, path
traversal into the repository by file name, or hardcoded secret.

The one real defect is in the Mermaid writer. A changed global-scope type named `link`, `note`,
`click`, `callback`, `href` or `cssClass` produces a diagram Mermaid cannot parse. I confirmed
this by running the repository's own local Mermaid check on the writer's exact line shape. The
tests do not cover it, and the structural lint accepts it.

The warnings concern guards that are weaker than they read: the `--out` containment check is
lexical only, the sidecar answer is not cross-checked against the request, the trial wrapper's
"repository unchanged" evidence omits the object database and config, and `check.mjs` reports a
browser launch failure as a Mermaid syntax failure.

No necessity-category findings were classified (no impossible-state guards stood out), so there
is no `necessity:` count.

I did not run the full test suites (already green per the brief). The only commands run were
targeted: `node tools/mermaid-check/check.mjs` on small synthetic `.mmd` files in the scratchpad
and one run with a bogus browser path.

## Critical Issues

### CR-01: Mermaid class ids that are Mermaid keywords make the whole diagram unparseable

**File:** `src/Vdiff.Core/Output/MermaidWriter.cs:52` and `:91-116`
**Issue:** `ClassId` turns the fqn into an identifier and uses it bare: `class <id>["<fqn>"]:::state`.
A global-scope type whose name is a Mermaid class-diagram keyword becomes a parse error. I ran the
local check (`tools/mermaid-check/check.mjs`) on `classDiagram` / `class <name>["<name>"]:::added { ... }` / `classDef ...`:

| id | result |
|----|--------|
| `link`, `note`, `click`, `callback`, `href`, `cssClass` | `FAILED: Parse error on line 2` |
| `style`, `end`, `direction`, `call`, `interface`, `class`, `title`, `T1`, `_` | OK |

`link`, `note`, `callback` and `click` are ordinary C++ type names (a list node `struct link`, a
UI `struct click`). A namespaced type is safe (`ns::link` becomes `ns__link`), so this hits only global-scope
types. One such type in a PR makes the entire sticky-thread diagram fail to render, which defeats
the core value. The same keyword list may grow with the pinned Mermaid version.

The tests miss it. `MermaidCliSyntaxTests` uses only `shop::Cell` and `Gen<n>`, and
`MermaidLint.ClassLine` accepts any `[A-Za-z_][A-Za-z0-9_]*` id, so neither can fail on this.
**Fix:** Never emit a bare fqn-derived id. Prefix every id so it cannot equal a keyword, keep the
readable name in the label (which is already there):
```csharp
var baseId = "C_" + builder;          // or "T" + running index
```
(`C_` also removes the current special case for a leading digit.) Add a test that diffs a header
declaring `struct link`, `struct note`, `struct click`, `struct callback`, `struct href` and
`struct cssClass`, and asserts `check.mjs` exits 0. Add the same names to a unit test of `Render`.

## Warnings

### WR-01: `--out` containment check is lexical only and can be bypassed by an alias of the repository path

**File:** `src/Vdiff.Core/Cli/CliApp.cs:77-102` (same pattern in `scripts/trace-run.ps1:142-151`)
**Issue:** `RefuseUnsafeOut` compares `Path.GetFullPath(out)` with the roots reported by
`git rev-parse`. Git reports real (symlink-resolved) paths. `GetFullPath` resolves nothing. So an
`--out` that reaches the repository through a symlink, a directory junction, a `subst` drive or
an 8.3 short name is not seen as inside it. The common real case is macOS, where the temp area
is `/var/...` and git reports `/private/var/...`. The tool would then write five files into the
analysed work tree, which is the one thing it must never do (T-01-28). `OutputWriter.WriteText`
also follows a pre-existing symlink at one of the five file names (`File.WriteAllBytes`), so a
link left in a reused `--out` directory redirects the write.
**Fix:** Resolve the deepest existing ancestor of `--out` to its final target before comparing
(`new DirectoryInfo(p).ResolveLinkTarget(returnFinalTarget: true)` for each existing segment, or
compare `Directory` identity by file id), and apply the same resolution to the three git roots.
Optionally open the five files with `FileMode.Create` after checking that none is a reparse point
or symlink. Mirror the change in `Test-SameOrUnder`.

### WR-02: The sidecar answer is not cross-checked against the request, so the client does not fail closed on a missing or invented file

**File:** `src/Vdiff.Core/Extraction/CppSidecar.cs:253-259` and `:268-286`
**Issue:** The class contract says the client "fails closed" and that a file is "never dropped".
`ParseResponse` checks only schema, a non-null list and a valid status per entry. It does not check
that the response holds exactly one entry per requested file, in request order, with the requested
paths. If the sidecar returned fewer entries (a bug, a version skew, a truncated but valid JSON
document) the missing files vanish from that side's model without a trace. The differ then reports
every type in them as added or removed, with no warning. A path the request never contained is
accepted and modelled under that name. `ModelBuilder.Build` has no check either.
**Fix:** In `Extract`, after `ParseResponse`, require `response.Files.Count == files.Count` and
`response.Files[i].Path == files[i].Path` (ordinal) for every `i`; otherwise throw a
`VdiffException("the sidecar response does not match the request")`. The Rust side already
returns files in request order (tested in `main.rs`), so this costs nothing.

### WR-03: The trial wrapper's "repo_unchanged: yes" ignores the object database, config, hooks and `.git` file set

**File:** `scripts/trace-run.ps1:167-195`
**Issue:** The snapshot hashes `status`, `HEAD`, the symbolic ref, `for-each-ref`, worktrees,
stash and the bytes of `.git/index`. The failure modes the project is most worried about are not
in it: a lazy fetch writing objects or a pack into `.git/objects`, an fsmonitor daemon or socket
file appearing under `.git`, a changed `.git/config`, `FETCH_HEAD`, `packed-refs` or a new hook.
None of these changes status or refs, so the wrapper would print `repo_unchanged: yes` and
`result: pass` while the private clone had been written to. The test-side `RepoSnapshot` hashes
the whole sandbox; the wrapper, which produces the real SPK-02 evidence, does not.
**Fix:** Add cheap, bounded probes to the hashed text: `git count-objects -v`, the hash of
`.git/config`, `.git/packed-refs` and `.git/shallow`, and a recursive listing of names and sizes
of `.git` (skip `objects/` file bodies but include `objects/pack/*` names and sizes, plus
`objects/info`, `hooks`, `info`). Keep the output free of paths and counts, as now.

### WR-04: `check.mjs` reports a browser launch failure as a Mermaid syntax failure

**File:** `tools/mermaid-check/check.mjs:142-146`
**Issue:** The script maps mermaid-cli exit 1 to `EXIT_SYNTAX`. mermaid-cli also exits 1 when
Chrome cannot start. I confirmed it with `VDIFF_MERMAID_BROWSER` pointing at an existing
non-browser executable: output `mermaid-check: FAILED / Error: Failed to launch the browser process`, exit 1. A
missing sandbox permission in a container, a Chrome crash or a bad `--disable-gpu` interaction
therefore looks like a bad diagram. `trace-run.ps1` then prints `mermaid_syntax: fail`, which
would be recorded as evidence about the writer. The header comment promises that exit 4 means
"the check itself broke".
**Fix:** Classify by stderr. Exit 1 only when stderr contains `Parse error` or `Syntax error`
(or the diagram text that mermaid-cli prints for parse failures); treat `Failed to launch the browser` and any
other failure as `EXIT_BROKEN` (4), or as `EXIT_PREREQUISITE` (3) for the launch case so the
wrapper reports `skipped`.

## Info

### IN-01: A panic in one file's extraction fails the whole sidecar run instead of labelling that file `error`

**File:** `extractors/cpp-ts/src/main.rs:26-54` and `:72-86`
**Issue:** The design (D-11) isolates per-file failures as `status: "error"`. `process_file` has
no `catch_unwind`, so any panic in a rayon worker unwinds through `pool.install`, the process
exits 101, and the C# client fails the whole run. I found no reachable panic (the slicing is
guarded and there is no `unwrap` in non-test code), so this is defence in depth for hostile input.
**Fix:** Wrap the body of `process_file` in `std::panic::catch_unwind(AssertUnwindSafe(...))`
and return a `FileResult` with status `error` on `Err`. The existing panic hook already prints a
fixed line.

### IN-02: Inheritance edges do not show added versus removed, and base resolution by suffix can pick a wrong type

**File:** `src/Vdiff.Core/Output/MermaidWriter.cs:123-165` (line 134)
**Issue:** Edges are built from `Bases.Added` concatenated with `Bases.Removed` and drawn with the
same `<|--` arrow, so a removed base looks like a current one. The product value is "which edges
changed shape". A base written `Base` inside namespace `a` also resolves to the single displayed
type ending in `::Base` even if that is `b::Base`. Accepted for a tracer, but it should be a
known limitation.
**Fix:** Record the edge direction of the change (for example a dashed link or a trailing label
`removed`) and, before the unique-suffix fallback, try the derived type's own scope chain.

### IN-03: Macro blanking cost is unbounded and outside the per-file parse budget

**File:** `extractors/cpp-ts/src/blank.rs:21-62`
**Issue:** `blank` scans every line once per macro name: cost is about `bytes x macros`. With the
CLI limits (256 names, 4 MiB per blob, 256 MiB total) a hostile change can spend tens of
seconds of CPU here before any parse deadline starts. The 120 s process timeout still bounds it
and fails the run, so this is a robustness gap, not a hang.
**Fix:** Do one pass per line that finds identifier tokens and looks each up in a `HashSet`
of names, which makes the cost linear in the input.

### IN-04: Timeouts in the wrapper and the Mermaid check do not kill the process tree

**File:** `scripts/trace-run.ps1:103-106` and `tools/mermaid-check/check.mjs:122-126`
**Issue:** `$proc.Kill()` (Windows PowerShell 5.1 has no tree overload) and `spawnSync(..., { timeout })` end
only the direct child. A timed-out `dotnet vdiff.dll` leaves its sidecar and git children, and a
timed-out mermaid-cli leaves its headless Chrome. `BoundedProcess` itself does this correctly.
**Fix:** On PowerShell 7 use `$proc.Kill($true)`; on 5.1 run `taskkill /T /F /PID`. In
`check.mjs` spawn with `detached` and kill the group on Unix, or `taskkill /T` on Windows.

### IN-05: The report page has no Content-Security-Policy

**File:** `src/Vdiff.Core/Output/HtmlWriter.cs:20`
**Issue:** The page is safe today because every repository-derived value passes `HtmlEncode`
and there is no script. That safety is one forgotten `Enc(...)` away from script execution in a
page opened from a pipeline artifact.
**Fix:** Add `<meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'">`
to the head. One line, and it also pins the "no external URL" promise.

### IN-06: Output writing is not atomic, and every `IOException` is reported as an output failure

**File:** `src/Vdiff.Core/Output/OutputWriter.cs:43-52` and `src/Vdiff.Core/Cli/CliApp.cs:60-62`
**Issue:** The five files are written one after another, so a disk-full or permission error part-way
leaves a mix of new and old files in `--out`, while the class comment says a failure leaves it
"absent or untouched" (that holds only for failures before the write). The catch clause also labels
any `IOException` or `UnauthorizedAccessException` "could not write the output", including one
thrown by an earlier read-side step, and prints the .NET message, which can contain a full path.
**Fix:** Write to a temporary sibling directory and rename it into place, or write each file
to `name.tmp` and move all five at the end. Narrow the catch to the `WriteAll` call, and report a fixed message.

---

_Reviewed: 2026-11-17_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
