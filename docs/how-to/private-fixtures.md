# How to check private fixtures

Every name, path and commit id on this page is a placeholder. Real values never go into this repository.

## What this is for

The five fixture shapes (`small-cpp`, `csharp`, `cross-stack`, `extract-class` and `rename`) have synthetic
fixtures in `tests/fixtures/`. Private mode runs the same five shapes on five real pull requests, inside the
target organisation, with the same harness CI runs: the same CLI arguments, the same CLI run and the same
comparison, so a private run tests exactly what CI tests. The result reaches this repository only as a shape, a
status and a date.

## Where private fixtures live

In the target organisation, never in this repository. A private fixture and its expected files hold or describe
target content, so they stay on a machine and in a directory inside the organisation. The repository tells the
tool where the fixtures are through an environment variable; it never stores them.

## A private fixture

A private fixture is a directory named after one of the five shapes. It holds `fixture.yml` and `expected/`:

```text
<fixtures directory>/
  small-cpp/
    fixture.yml
    expected/
      diff.json
      summary.mmd
```

There is no `base/` or `head/` tree. `fixture.yml` names a local clone and two commits, and the CLI reads the
clone's git objects read-only. Nothing is checked out and nothing is copied. A private fixture that carries a
`base/` or `head/` directory is refused.

```yaml
shape: small-cpp
description: a placeholder description of the pull request's shape
repo: D:\clones\product
base: 0000000000000000000000000000000000000000
head: 0000000000000000000000000000000000000000
config:
  include:
    - "src/**"
  blank:
    - EXPORT_MACRO
expected:
  diff:
    state: pending
    due: 5
  summary:
    state: pending
    due: 7
```

- `shape` equals the directory name.
- `repo` is an absolute path to an existing local clone. `base` and `head` are full commit ids (40 hex digits).
- `config.include` and `config.blank` are optional and carry the same meaning as the CLI's `--include` and
  `--blank` options.
- `expected.diff` and `expected.summary` each have a `state` (`pending` or `green`) and a `due` phase number.
  A `pending` output must differ from its expected file; a `green` output must match it byte for byte.
- Unknown keys fail the load. The load error is fixed text and never quotes the file.

The expected files follow the golden conventions in `tests/fixtures/README.md`: byte-exact, LF line endings, one
final newline, the layout of the `vdiff-diff/v1` schema.

## Running

Set both variables to directories outside this repository, then run the entry test.

PowerShell:

```powershell
$env:VDIFF_FIXTURES_DIR = "D:\private\fixtures"
$env:VDIFF_PRIVATE_OUT = "D:\private\results"
dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --filter "Category=PrivateFixtures" --logger "console;verbosity=detailed"
```

bash:

```bash
export VDIFF_FIXTURES_DIR=/private/fixtures
export VDIFF_PRIVATE_OUT=/private/results
dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --filter "Category=PrivateFixtures" --logger "console;verbosity=detailed"
```

With `VDIFF_FIXTURES_DIR` unset the entry test returns at once and passes, so CI and an ordinary test run need no
private fixture. The runner also sets `VDIFF_PROTECTED_DIRS` to this repository's root around each CLI run, so
the CLI itself refuses an output directory inside this repository.

## What you see

One line per shape, in name order:

```text
<shape>: PASS|FAIL|PENDING <yyyy-MM-dd>
```

The status of a shape is decided over its two outputs, diff and summary:

- `FAIL` when any output failed the ratchet: the tool run failed, an expected file or an output is missing, an
  output does not validate against its schema, a `pending` output matches its expected file, or a `green` output
  does not.
- `PENDING` when no output failed and at least one is red by design (`pending` and different).
- `PASS` when every output is green and matches.

The line carries no commit id, path, type name, member name, count or diff text. The test's own failure message is
fixed text as well.

## Where the details go

Everything else is written under `VDIFF_PRIVATE_OUT`, which stays in the organisation:

| File | Content |
| --- | --- |
| `private-run.txt` | the console lines |
| `fixture-report.md` | one row per shape and output, with fixed-text details |
| `<shape>/out/` | the CLI's own output for that shape |
| `<shape>/diff.diff`, `<shape>/summary.diff` | the unified diff of an output that differs from its expected file |
| `<shape>/cli-stderr.txt` | what the CLI printed on stderr, if anything |
| `<shape>/error.txt` | the full text of a load or run error for that shape |

Read these files to find out why a shape is `FAIL` or what makes it `PENDING`.

## Refusals

A refusal is the only console line, and nothing is written.

| Message | What to do |
| --- | --- |
| `accept mode is synthetic-only; unset VDIFF_FIXTURE_ACCEPT for a private run` | Unset `VDIFF_FIXTURE_ACCEPT`. Private expected files are written and reviewed by hand. |
| `VDIFF_FIXTURES_DIR must name an existing directory` | Point it at a directory that exists and can be read. |
| `VDIFF_FIXTURES_DIR must be outside this repository` | Move the fixtures into the organisation's storage. |
| `VDIFF_PRIVATE_OUT must name a directory outside this repository` | Set it to a directory outside this repository; an unset or empty value is refused too. |
| `VDIFF_FIXTURES_DIR may hold only directories named after the five shapes` | Remove or rename the other directory. The message does not say which one, on purpose. |
| `VDIFF_FIXTURES_DIR holds no shape directory` | Add at least one shape directory. |

A shape whose `fixture.yml` is invalid, or whose run throws, is a `FAIL` line for that shape; the reason is in
`<shape>/error.txt`.

## Recording a result here

Record a private result in `docs/trials/private-fixtures.md`, and only as a shape, a status and a date:

```text
| rename | PASS | 2026-11-20 |
```

Never record a commit id, a path, a type name, a member name, a count or diff text. The same rule governs the
trial record of the first tracer run (TRC-03): this repository learns that a check passed or failed, not what it
looked at. A test checks every row of that file against this grammar. A newer result for a shape replaces that
shape's row.

## Accept mode

Accept mode, which rewrites expected files from a run (`VDIFF_FIXTURE_ACCEPT`), is for the synthetic fixtures
only. A private run refuses to start while the variable is set.
