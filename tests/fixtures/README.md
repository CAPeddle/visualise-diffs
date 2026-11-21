# Fixtures

Synthetic fixtures and their hand-written goldens. The goldens are the specification that Phases 5 to 7
implement to (D-06): they state what the finished tool must write, not what today's tracer writes. This
repository is public, so every name in a fixture is synthetic and generic (namespace `synthetic`, names such
as Widget, Gadget, Shape and Report). No fixture, golden or line of this file may name a target
organisation, product, path, commit id or code.

## Layout

A fixture lives in `tests/fixtures/<shape>/`, where `<shape>` is one of the five shape names:
`small-cpp`, `csharp`, `cross-stack`, `extract-class` and `rename`. A directory with any other name fails
`CommittedFixtureTests.Every_fixture_directory_is_a_shape`.

```text
tests/fixtures/<shape>/
  fixture.yml            what the fixture is, the tool configuration it runs with and the ratchet state
  base/                  the source tree of the base commit
  head/                  the source tree of the head commit
  expected/diff.json     the golden diff, a vdiff-diff/v1 document
  expected/summary.mmd   the golden Mermaid summary
```

The harness copies `base/` into a repository and commits it, then makes `head/` the working tree (deleting
the base files head lacks) and commits again, with a fixed identity and dates, so commit ids are
deterministic. Then it runs the real CLI on the two commits and compares the two output files with the
goldens byte for byte.

What a fixture tree must not hold:

- a directory named `bin`, `obj`, `target`, `out` or `node_modules`, a link of any kind, a file over
  256 KiB or more than 200 files in a tree;
- a carriage return or a byte-order mark. `.gitattributes` pins `tests/fixtures/**` to `eol=lf`, and
  `RepoHygieneTests` fails on a CR or BOM in a working-tree file;
- a tracked generated file. The only generated names allowed are `diff.json` and `summary.mmd` at exactly
  `tests/fixtures/<shape>/expected/` (`RepoHygieneTests.TrackedPathProblems`); `base.model.json`,
  `head.model.json`, `run.meta.json`, `report.html` and anything nested stay rejected.

## fixture.yml

The keys are strict: an unknown key fails the load, so a typo such as `blnak` cannot run a fixture without
its blanking.

```yaml
shape: small-cpp            # must equal the directory name
description: "..."          # 1 to 300 characters
config:                     # optional: only the keys the CLI has today
  include: ["include/**"]   # --include globs
  blank: [SYNTH_API]        # --blank macro names
expected:
  diff:
    state: pending          # pending or green
    due: 5                  # documentation only: the phase that turns it green
  summary:
    state: pending
    due: 7
```

The private-only keys `repo`, `base` and `head` belong to private fixtures and are described in
`docs/how-to/private-fixtures.md`; a synthetic fixture must not have them. Phase 4 adds config keys (for
example package-pin rules and the C++/CLI lane) when the `.visualise-diffs.yml` reader exists.

## The ratchet

`state` is `pending` or `green`; `due` is documentation only (D-18). A comparison that did not run properly
(the tool failed or wrote to stderr, a golden or an output is missing, an output fails its schema) is FAIL in
every state. A run that did work is judged on its bytes:

| state | bytes equal the golden | row | the test |
|-------|------------------------|-----|----------|
| pending | no | RED | passes: red by design, listed in the report |
| pending | yes | FAIL (unexpectedly matches) | fails: set the state to green |
| green | yes | GREEN | passes |
| green | no | FAIL | fails: a regression |

To flip an output, the phase that makes it match sets that output's `state` to `green` in the same commit as
the code change. Nothing is green in Phase 2: each fixture carries at least one feature today's tracer cannot
produce, so a pending output cannot match by accident. `VDIFF_FIXTURE_REPORT=<path>` makes a run write the
report table to that path; the pipeline publishes it.

## Accept mode

When a golden has to change on purpose (a reviewed format change), regenerate it instead of editing by hand:

1. Locally, run `VDIFF_FIXTURE_ACCEPT=<shape>` with the committed-fixture test, for example
   `VDIFF_FIXTURE_ACCEPT=rename dotnet test tests/Vdiff.Tests/Vdiff.Tests.csproj --filter "FullyQualifiedName~CommittedFixtureTests"`.
2. The harness rewrites `expected/diff.json` and `expected/summary.mmd` of that one shape from a successful,
   schema-valid run. The run fails by design, so an accepted golden can never turn a build green.
3. Review the change with `git diff tests/fixtures/<shape>/expected` and commit it with its reason.

Accept mode is refused in CI (`TF_BUILD` or `CI` set), for any name that is not exactly one shape, for a
shape with no `expected/` directory and over a link (D-06). Never use it to create the first golden of a
fixture: the first goldens are written by hand from the fixture's intent.

## Golden conventions

Goldens describe the finished tool, not today's tracer:

- Whole-repository extraction (R1, Phase 3). Unchanged types and unchanged edges in files the change did not
  touch are counted in `summary`.
- ModelDiffer's rules (Phase 1 D-07) apply, plus v1:
  - Types are matched by language plus id, members by key.
  - An added type lists all its members as `added` and all its bases in `bases.added`; a removed type is the
    mirror image.
  - A member is `modified` when its access, signature or modifiers differ, and `body-changed` when a diff hunk
    overlaps its span and nothing else changed (R-13).
  - A type matched by language and id is `modified` only when its bases or members differ (D-20). The v1 diff
    reports no type-level attribute change: a change to a type's access, modifiers, `partial` or `files` alone
    leaves it unchanged, and `TypeDiff` has no field for them. A type matched by rename is `renamed`, and its
    member and base changes are listed under it.
  - Unchanged members are never listed: a type's `members` holds only members in state `added`, `removed`,
    `modified`, `renamed` or `body-changed`, and members have no unchanged count. This is the frozen schema's
    presence rule, and `DiffPresence` rejects a listed member in state `unchanged`.
  - `warnings` is empty and `unmodelled` is 0 and 0 unless a fixture intends otherwise.
- Renames (R-14). A renamed type is listed once, under its head id, with `renamed_from` and `rename`:
  - `score_milli = 500 * shared / total` (integer division) + 200 (same file or git rename pair) + 200 (same
    namespace) + 100 (same base set).
  - `member_jaccard` counts member names: `shared` is the intersection, `total` the union.
  - A member of a matched type is `renamed` (with `renamed_from_key`) when its parameter list and return type
    are identical and the pair is unique.
- Edges:
  - Identity is kind, `from`, `to_name` and `via`; the resolved `to` is not part of it (D-23). Present only in
    head means `added`, only in base `removed`. The same identity on both sides with a different resolved `to`
    is `modified`, listed with `to_before` and `to_after` and no `to`; no fixture has such an edge.
  - An edge whose only change is a renamed endpoint type is listed once with state `renamed` and its head-side
    endpoints.
  - An association edge (proposed rule; Phases 4 and 5 implement it) runs from type T to type U when a field of
    T is declared with a type that resolves to a modelled type U of the same language. For the syntax-only C++
    extractors, resolution works like a base's (exact fqn, else a unique `::` suffix) after stripping template
    arguments, pointer, reference and const; per the D-16 clarification that rule is not language-universal,
    and the Roslyn C# extractor (Phase 4) resolves base and field types semantically, so a C# edge's `to` is
    the resolved symbol's full id. `via` is the field's key, and `to_name` is the type name as written. A field
    whose type resolves to no modelled type of the same language creates no edge; cross-language links are R17
    (P2).
- Fixtures avoid the keywords `static`, `virtual`, `override`, `inline`, `explicit` and `constexpr`, so every
  `modifiers` list is empty whether or not an extractor fills modifiers.
- Both outputs are pending in every fixture: `diff` due 5 and `summary` due 7.
- Every golden is also checked by `FixtureGoldenTests`: it validates against the frozen schema in
  `docs/schema/`, keeps the presence rules and equals `OutputWriter.Serialize` of its own content, so key
  order, canonical order, indentation and the final line feed are exactly what the tool will write.

## Member keys and signatures (C++)

These are the Phase 1 sidecar rules, pinned by the sidecar's own unit tests in
`extractors/cpp-ts/src/extract.rs` (for example
`signatures_keep_specifiers_and_drop_bodies_initializers_and_semicolons`, `one_member_per_declarator` and
`bases_drop_access_and_virtual_and_collapse_whitespace`). The finished tool keeps them, so a golden spells
every key and signature by these rules and the table below (D-06):

- A method or constructor key is the name, then the parameter list as written with whitespace collapsed to
  single spaces, then any trailing qualifier such as ` const`: `width() const`, `Circle(double r)`,
  `grow(int by, long times)`.
- A field key is the field name: `height_`.
- A signature is the declaration text with whitespace collapsed, up to where a body or a constructor
  initialiser list starts, without the final `;`. Specifiers stay, and so do `= 0`, `= default`, `= delete`
  and a field's default value: `int width() const`, `void paint()`, `Circle(double r)`, `int height_ = 0`,
  `double radius`.
- A member diff carries no member kind, so how an extractor labels a constructor does not appear in any
  golden.
- Bases are the written base names with access and `virtual` dropped and whitespace collapsed:
  `class SYNTH_API Widget : public Component` has bases `["Component"]` once `SYNTH_API` is blanked.
- A type id is `<kind> <fqn>`, with `::` in a C++ fqn: `class synthetic::Widget`. Its `scope` is `synthetic`.
  The id and key grammar for every language, and the `#<path>` suffix for repeated ids (no fixture repeats
  one), are in [`docs/schema/README.md`](../../docs/schema/README.md) section Identity (D-21, D-22); this
  section is the C++ part, with worked examples for these fixtures.
- `member_jaccard` counts member names, not keys, so a renamed constructor counts as two different names.

Worked keys and signatures. Every member a golden lists is here. Access is `public` everywhere except
Widget's `height_`, which is `protected` in base and `public` in head.

| Fixture | Type | Source line | key | signature |
|---------|------|-------------|-----|-----------|
| small-cpp | Widget | `int width() const { return 1; }` (head returns 2) | `width() const` | `int width() const` |
| small-cpp | Widget | `void paint();` (base only) | `paint()` | `void paint()` |
| small-cpp | Widget | `int height_ = 0;` | `height_` | `int height_ = 0` |
| small-cpp | Widget | `void show();` (head only) | `show()` | `void show()` |
| small-cpp | Legacy | `void run();` | `run()` | `void run()` |
| small-cpp | Gadget | `void tick();` | `tick()` | `void tick()` |
| rename | Circle (base) | `Circle(double r);` | `Circle(double r)` | `Circle(double r)` |
| rename | Disc (head) | `Disc(double r);` | `Disc(double r)` | `Disc(double r)` |
| rename | Disc | `double diameter() const;` (head only) | `diameter() const` | `double diameter() const` |
| extract-class | Report | `void print() { }` (head body `{ formatter.write(); }`) | `print()` | `void print()` |
| extract-class | Report (base), Formatter (head) | `int header_width;` | `header_width` | `int header_width` |
| extract-class | Report (base), Formatter (head) | `int footer_width;` | `footer_width` | `int footer_width` |
| extract-class | Report (head) | `Formatter formatter;` | `formatter` | `Formatter formatter` |
| extract-class | Formatter | `void write() { }` | `write()` | `void write()` |

Members that never appear in a golden because they are unchanged still follow the same rules and count
towards `member_jaccard` and the unchanged-type counts: Component's `id() const` (`int id() const`) and `id_`
(`int id_ = 0`), Shape's `scale() const` (`double scale() const`), and Circle and Disc's `radius`
(`double radius`), `area() const` (`double area() const`) and `perimeter() const`
(`double perimeter() const`).

## Provisional summary format

D-07. S1 and Phase 7 may revise it through accept mode and a reviewed diff. It is built from the S1 sample
(`docs/spikes/S1-mermaid-in-ado-pr.md`) with the Phase 1 conventions (`C_` class ids from 01-11, quoted labels
and trailing member tags from 01-08):

1. The first line is `classDiagram`.
2. Class lines in three groups: changed types ordered by language then id; then context neighbours (R18: 1-hop
   bases, derived types and association targets of changed types, in either model, that are not themselves
   changed), ordered by language then id; then package nodes ordered by name.
3. The class id is `C_` plus the fqn with every character outside letters, digits and underscore replaced by
   `_`, made unique with `_2`, `_3` in line order. This is `MermaidWriter.ClassId`; `synthetic::Widget`
   becomes `C_synthetic__Widget`.
4. A changed type is written as two spaces, `class`, the id, `["<fqn>"]`, `:::<state>`, then ` {` when it has a
   block. A block holds stereotype lines, then changed member lines ordered by key, then a line of two spaces
   and `}`.
5. Stereotype lines are four spaces plus `<<renamed from <base name>>>` for a renamed type (first), where the
   base name is the unqualified name after the last `::` or `.` (as in the S1 sample, so no `::` sits inside a
   stereotype), and four spaces plus `<<managed wrapper>>` for a `cpp-cli` type (D-17: the lane is rendered
   from the language).
6. A member line is four spaces, a visibility character (`+` public, `#` protected, `-` private) taken from
   `access_after`, or from `access_before` for a removed member, the signature (`signature_after`, or
   `signature_before` for a removed member), a space and `[<state>]`. The state is one of added, removed,
   modified, renamed or body-changed. For example, small-cpp's removed public `paint()` renders as
   `    +void paint() [removed]`.
7. A context node is two spaces, `class`, the id, `["<fqn>"]` and `:::context`, with no block. A `cpp-cli`
   context node is the exception: its block holds only the managed-wrapper stereotype.
8. A package node is `C_package_` plus the sanitised name, labelled `package updated: <name> <old> → <new>`
   (U+2192) with `:::modified`. For an added package the label is
   `package added: <name> <new>` with `:::added`; for a removed one, `package removed: <name> <old>` with
   `:::removed`.
9. Edge lines are sorted ordinally as whole lines and drawn only between shown classes:
   - inheritance: two spaces, base id, ` <|-- `, derived id;
   - association: two spaces, from id, ` --> `, to id, ` : uses`, plus ` [added]` for an added edge;
   - a removed association uses ` ..> ` and ends ` : uses [removed]`;
   - a renamed edge is drawn like an unchanged one.
10. The classDef lines come last, all five, in this order:
    - the three the writer emits today (added, removed, modified);
    - `  classDef renamed fill:#e0ecff,stroke:#1565c0,color:#111`;
    - `  classDef context fill:#eeeeee,stroke:#9e9e9e,color:#666`.
11. LF line ends and one trailing LF.

`MermaidLint` (test code) checks every line kind above, and `MermaidCliSyntaxTests.Golden_summary_passes_the_real_syntax_check`
(`Category=MermaidCli`) runs each golden through the pinned mermaid-cli parser. Run it locally after changing a
golden summary; the pipeline step that runs the category is advisory in Phase 2.

## Per-fixture intent

Each fixture carries at least one feature today's tracer cannot produce, so a pending output never matches by
accident.

- `small-cpp`: a `body-changed` member and a greyed context neighbour (the unchanged base `Component`), beside
  an added, a removed and a modified type.
- `rename`: a type renamed in a moved header (`Circle` to `Disc`, a git rename pair) with an integer rename
  score, a renamed constructor and a renamed edge.
- `extract-class`: an added association edge (`Report` to `Formatter` through the field `formatter`) and a
  `body-changed` member, where members move into a new class that the old one now holds.
