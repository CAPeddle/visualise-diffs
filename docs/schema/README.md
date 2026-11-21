# vdiff schemas

Status: draft, pending the freeze review in plan 02-06.

Three JSON Schema files define what a `vdiff` run writes. Every extractor, the differ, the
hand-written goldens and the private fixtures are written against them.

| File | Covers | Written as |
|------|--------|-----------|
| `vdiff-model-v1.schema.json` | one side's class model: files read, types, members, edges | `base.model.json`, `head.model.json` |
| `vdiff-diff-v1.schema.json` | the structural difference: summary counts, changed types, members, edges, package pins | `diff.json` |
| `vdiff-run-meta-v1.schema.json` | commit ids, tool versions, vdiff version | `run.meta.json` |

`summary.mmd` and `report.html` have no schema. Each schema file is self-contained: `TypeRef` is
defined in both the model and the diff file.

The records in `src/Vdiff.Core/Model/` are the same contract in C#. A parity test
(`SchemaParityTests`) keeps the two in step in both directions: every record has a definition,
every definition has a record, property names and their order are equal, and `required` equals
the record's non-nullable properties. A property's position in a record is its position in the
JSON.

## Keyword subset

The validator is NJsonSchema 11.6.2. It enforces most of draft-07 and silently ignores the rest,
so a schema that uses an ignored keyword accepts invalid documents. The files therefore use only:

`$schema`, `title`, `description`, `type`, `properties`, `required`, `additionalProperties`,
`items`, `enum`, `pattern`, `minimum`, `maximum`, `minLength`, `uniqueItems`, `definitions`, `$ref`.

Consequences:

- A fixed value is a single-value `enum`, never the fixed-value keyword (`"enum": ["vdiff-model/v1"]`).
- No conditional keywords and no dependency keywords. A rule that depends on another property
  (for example "present exactly when") is written in a description and enforced by a test
  checker, see Presence rules below.
- Every `$ref` is local (`#/definitions/...`). There is no `$id` and nothing is fetched.
- Objects are closed (`additionalProperties: false`), so a field that is missing from a schema
  fails validation.

`SchemaValidationTests` holds the keyword allow-list and at least one negative document for each
validating keyword; a guard fails when a used keyword has no negative document.

## Evolution policy

Inside v1, adding an enum value or an optional property is allowed, because documents written
before the change still validate. Renaming or removing a property, making an optional property
required, adding a required property, and tightening a pattern or an enum need a v2 schema and a
migration of every extractor, every golden and every private fixture.

## Presence rules

The subset cannot express these, so the descriptions state them and tests enforce them
(`DiffPresence.Problems` for the diff, `ModelPresence.Problems` for the model):

- A member's `*_before` field (access, signature, modifiers) is present exactly when the member
  exists on the base side, and its `*_after` field exactly when it exists on the head side.
- `base_file` is present when a type exists on the base side (states removed, modified, renamed)
  and `head_file` when it exists on the head side (added, modified, renamed).
- `renamed_from` and `rename` are present exactly for state `renamed` (present when the type is
  renamed, absent in every other state); a member's `renamed_from_key` is present exactly for
  member state `renamed`, and absent otherwise.
- A package's `old_version` is present exactly for states `updated` and `removed`, and its
  `new_version` exactly for `updated` and `added`.
- `via` is present exactly on association edges, in the model and in the diff.
- The summary counts equal the number of listed items in each state.
- Unchanged types and unchanged edges are counted, never listed. Unchanged members are never listed.
  Members have no unchanged count. The member and type state enums keep `unchanged` for symmetry.
- A modified edge's `to_before` and `to_after` rules are under Edge identity below.
- `score_milli` is `500 * shared / total` (integer division, rounded down, 0 when total is 0),
  plus 200 when the two types share a file or form a git rename pair, plus 200 for the same
  namespace, plus 100 for the same base set. The rename threshold is 600.

## Edge resolution

The schema fixes the edge shape: `from`, an optional `to` and `to_name`. It does not fix how `to`
is resolved; that belongs to each extractor, and a change to a resolution rule is not a schema
change.

- The syntax-only C++ extractors (the tracer and the tree-sitter sidecar) set `to` only for a
  unique exact-fqn match or, when there is no exact match, a unique `::`-suffix match among types
  of the same language.
- The Roslyn C# extractor (Phase 4) resolves bases semantically, so its `to` is the resolved
  symbol's full id. A written base `Document` in namespace `Synthetic.Orders` gives
  `class Synthetic.Orders.Document`.

## Type access and partial types

A type carries three optional or defaulted fields besides its names.

- `access` is the type's declared accessibility, or the language default where the language has
  one (C#: `internal` at namespace scope, `private` when nested). It is absent when the extractor
  has no type-level access: C++ types at namespace scope have none. The value `none` is never a
  type-level value. The enum is shared with members, where `none` is legal, so that the three
  access enums in the schemas stay identical; absence is the only encoding of "no type-level
  access". `ModelPresence` enforces the restriction.
- `partial` is a required boolean: true only for a type declared in parts, false otherwise.
- `files` is present exactly when `partial` is true. It lists every repository-relative file that
  declares a part, sorted ordinally and without repeats (a one-file partial type lists its one
  file). `file` is the first entry of `files`, and `span` is the span of the earliest part in that
  file, so neither depends on the order in which an extractor visits the parts.

The tracer writes `partial` false and omits `access` and `files` on every type.

The diff reports no type-level attribute change. `TypeDiff` has no access, modifiers, partial or
files property. A type is `modified` only when its bases or members differ, and a change to its
access, modifiers, partial or files alone leaves it unchanged. Phase 5 may add optional type-level
change fields inside v1.

Test-side checkers enforce what the subset cannot express. `ModelPresence` checks the type rules
above (files present exactly when partial, ordinal order, no repeats, `file` equal to `files[0]`,
no type-level `none`) and that `via` is present exactly on association edges. `DiffPresence`
checks the diff presence rules (member before and after fields, files per state, rename fields,
counts, package versions, `via`, and the edge rules under Edge identity).

Open item for the freeze review: `MemberModel` has no `file`, so a member declared in another part
of a partial type cannot be placed in its file from the model alone. An optional member `file`
would be an additive change inside v1.

## Identity

What identifies things: a type is (`language`, `id`), and a member is its `key` within its type.
An `id` is the type-kind value, one space and the `fqn`. The `fqn` is the `scope`, the language's
separator and the `name`; it is just the `name` when `scope` is empty.

**Repeated ids and keys.** When one id occurs more than once in one language, every definition of
it carries `#` and the repository-relative path of its file, with forward slashes as in `file`
(`class shop::X#src/b.h`). A definition that is the second or later in its file, in source order,
also carries `@2`, `@3` (`class shop::X#src/c.h@2`). An id that occurs once stays plain, so the
first repeat changes the existing definition's id as well: the diff then shows the plain id
removed and the suffixed ids added, beside the duplicate warning. A member key repeated in one
type follows the same rule, with the path of the file that declares the member. The `fqn` is never
suffixed, and adding a file never renumbers another definition.

**`cpp` and `cpp-cli`** (separator `::`).

- The kind is `class`, `struct`, `union` or `enum`. In a C++/CLI file a `ref class` or a native
  class is `class`, a `value class` is `struct`, an `interface class` is `interface` and an
  `enum class` is `enum`.
- The `scope` is the enclosing namespaces joined by `::` (`shop::billing`). An anonymous namespace
  is named `(anonymous)` and an inline namespace by its own name.
- A nested type's scope is its enclosing type's fqn (`struct n::Outer::Inner`).
- Template arguments: a class template has its plain name (`struct Box`) and a specialisation has
  its name with the arguments as written (`struct Spec<T*>`). There is no arity marker.
- A member key is the name, then the parameter list as written with whitespace collapsed, then any
  trailing qualifier (`width() const`, `Circle(double r)`, `operator==(const Gadget& o) const`), so
  overloads are told apart by their parameter lists.
- A field key is its name, with one member per declarator (`height_`).
- A member template's key leaves out its template header, which its signature carries (`put(T t)`
  and `template <class T> void put(T t)`).
- Worked examples: `class synthetic::Widget`, `struct n::Outer::Inner`, and
  `class Synthetic::Interop::EngineWrapper` (language `cpp-cli`).

**`csharp`** (separator `.`, for namespaces and for nesting).

- The kind is `class`, `struct`, `enum`, `interface`, `record` or `delegate`.
- The `scope` is the namespace, or the enclosing type's fqn for a nested type, so a `Receipt`
  nested in `OrderService` is `class Synthetic.Orders.OrderService.Receipt`.
- Generic arity: a generic type's `name` is its plain name followed by `<`, one comma fewer than
  its number of type parameters, and `>`, the C# unbound form (`Foo<>`, `Pair<,>`). The declared
  parameter names are not part of an id, so renaming a type parameter changes nothing. `Foo` and
  `Foo<>` in one namespace are two types with the ids `class Synthetic.Orders.Foo` and
  `class Synthetic.Orders.Foo<>`, and neither carries a repeat suffix.
- A type nested in a generic type takes the generic's fqn as its scope
  (`class Synthetic.Orders.Outer<>.Inner`).
- A member key is the name and then the parameter list as written with whitespace collapsed
  (`Submit(Order order)`), so overloads are told apart by their parameter lists (`Reset()` and
  `Reset(int to)`). A generic method's name carries its arity the same way (`Map<>(int x)`). A
  field, property or event key is its name. A constructor key uses the type's plain name without
  the arity marker (`Foo(int x)` in `Foo<>`).

A shape this section does not list is added to it before an extractor emits that shape, in the
same commit as the extractor change.

## Edge identity

An edge's identity is `kind`, `from`, `to_name` and `via`; the resolved `to` is not part of it.
Before matching, the base-side ids of renamed types are replaced by their head ids. Two edges with
one identity and one `to` are `unchanged` (counted, never listed), or `renamed` when an endpoint
type was renamed (listed with head-side ids). Two edges with one identity and different `to`
values are one `modified` edge, listed with `to_before` and `to_after` and no `to`. An edge only in
head is `added`, and one only in base is `removed`.

The rules the schema cannot express, enforced by `DiffPresence`:

- `to_before` and `to_after` appear only on an edge in state `modified`.
- A modified edge carries at least one of them; each is absent when that side left `to_name`
  unresolved.
- The two are never equal.
- A modified edge carries no `to`.
- No two listed edges share an identity (kind, from, to_name, via).

Worked example: a syntax-only C++ base written `Base` resolves to `ns::Base` while that is the
only type with the suffix `::Base`. A change that adds an unrelated `other::Base` makes the written
base ambiguous, so the edge's `to` goes from present to absent. That is one `modified` edge with
`to_before` `ns::Base` and no `to_after`, not a removal and a re-addition.
