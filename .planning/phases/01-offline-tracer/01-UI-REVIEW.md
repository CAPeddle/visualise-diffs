# Phase 01 — UI Review

**Audited:** 2026-11-18
**Baseline:** abstract 6-pillar standards (no UI-SPEC.md), scoped to `report.html` and `summary.mmd`
**Screenshots:** not captured. The artifacts are generated files, not a served app, and no network renderer was run (privacy rule). This is a code-only audit of `HtmlWriter.cs` and `MermaidWriter.cs`.
**Interaction captures:** off (workflow.ui_interaction_capture is false). The report has no script and no interactive elements, so there is nothing to capture beyond static layout.

---

## Pillar Scores

| Pillar | Score | Key Finding |
|--------|-------|-------------|
| 1. Copywriting | 3/4 | Plain, specific labels. "Not modelled: N on the base side" is unexplained. |
| 2. Visuals | 2/4 | Diagram is only raw Mermaid text in a `<pre>`. State is carried by background colour, with no stroke or icon in HTML. |
| 3. Color | 2/4 | Fixed pastel fills with no text-contrast pairing for the state column. Colour-only meaning in HTML. |
| 4. Typography | 2/4 | Default `sans-serif`, no scale, and `<pre>` and `<code>` use the UA monospace stack. |
| 5. Spacing | 3/4 | Consistent em-based paddings. No max-width, so wide tables and `<pre>` scroll. |
| 6. Experience Design | 2/4 | No empty-state row, no responsive meta viewport, no dark mode. Static report, so loading and error states are N/A. |

**Overall: 14/24**

Provisional scores. Phase 7 owns the full report (CONTEXT.md, Phase boundary), so this phase is a tracer. The scores grade the artifact as built.

---

## Top 3 Priority Fixes

1. **Report has no rendered diagram (WARNING)** — A reviewer opening `report.html` sees Mermaid source text, not a class diagram, which defeats the Core Value ("understands the structural change from the diagram"). The no-script and no-external-URL constraint (T-01-06) rules out Mermaid.js, so the real fix is a server-free render (PNG or SVG, planned later). Until then, label the block "Mermaid source (paste into a Mermaid renderer)".
2. **State shown by colour only (WARNING)** — `HtmlWriter.cs:60,64` put the state word in the cell (`added`, `removed`, `modified`), so meaning survives without colour. The `.added`, `.removed` and `.modified` rules (lines 27-29) are fills only and apply to the state cell, with no non-colour cue such as a `+`/`-`/`~` marker. Fix: add a marker prefix, and set `color` explicitly on the filled cells (#111 on #fff4cc and #d4f8d4 passes, but the page never states it).
3. **No empty-state row (WARNING)** — With zero changed types, `HtmlWriter.cs:57-69` emit a header-only table under "Changed types". Fix: emit "No changed types found between base and head" instead of an empty table. `MermaidWriter.Render` likewise returns a bare `classDiagram` with only the `classDef` lines when `shown` is empty.

Further minor items: add `<meta name="viewport" content="width=device-width, initial-scale=1">`, a `max-width` on body, `font-family: ui-monospace, Consolas, monospace` on `pre` and `code`, and `<th scope="col">`.

---

## Detailed Findings

### Pillar 1: Copywriting (3/4)
- Applicable, in a limited form. All strings live in `HtmlWriter.cs:32-70`. Headings are specific ("Class diagram changes", "Changed types", "Warnings", "Mermaid"). There are no generic labels such as "Submit" or "OK".
- `HtmlWriter.cs:42`: "Not modelled: X on the base side, Y on the head side" does not say what "not modelled" means (types the extractor could not parse). A reviewer cannot act on it. WARNING.
- `HtmlWriter.cs:86-91`: "bases added:" and "bases removed:" are clear.
- Every repository-derived value passes through `Enc` (`HtmlWriter.cs:128`). Tone is neutral and consistent. Warning strings are passed through verbatim from the diff, so their quality depends on the producers.
- Empty and error copy does not exist (see Pillar 6). The console vocabulary is fixed and was not found by grep in `src/Vdiff.Cli`. It lives in `CliApp` and was not audited line by line.

### Pillar 2: Visuals (2/4)
- Focal point: the `<h1>` and the summary line come first, then the table. The `Mermaid` section is last, as a monospace `<pre>` (line 70). There is no diagram. BLOCKER-adjacent for the Core Value, but intended for this tracer (D-08: "may embed the Mermaid text plus a plain table"). WARNING.
- Table hierarchy is reasonable: type rows are bold on grey (`tr.type td`, line 26), and member rows follow beneath with no indent, so nesting is shown only by row order. Add an indent or `padding-left` on member rows.
- No icon-only controls exist, so the aria-label check does not apply.
- `MermaidWriter.cs:44-46`: removed uses a dashed stroke (`stroke-dasharray:4 3`) and a distinct stroke colour per state, so the Mermaid output carries a non-colour cue for removed. Good. Added and modified differ only by colour.
- Member tags `[added]`, `[removed]`, `[modified]` (`MermaidWriter.cs:77`) give a textual cue per member. Visibility glyphs `+`, `-`, `#` are mapped from access (lines 83-88). Note that `+` doubles as the default when access is unknown, which can mislead.
- The `Escape` look-alikes (U+2774, U+2775, U+223C, U+2033) render as visually different characters from `{`, `}`, `~`, `"` in generic signatures, so templated C++ signatures will read subtly wrong. WARNING. The trade-off is documented (T-01-20) and security-motivated.

### Pillar 3: Color (2/4)
- Palette is three state fills with no other accent: `#d4f8d4` (added), `#fde0e0` (removed), `#fff4cc` (modified), plus `#eee`, `#f6f6f6`, `#999`, `#111` (`HtmlWriter.cs:23-30`). The hardcoded hex is expected here (no design system).
- The Mermaid `classDef` colours match the HTML fills. The pairing is consistent (S1 sample).
- Text `#111` on all three fills has high contrast. Table borders `#999` on white are about 2.8:1, below the 3:1 non-text guidance. WARNING (minor).
- The green/red pair is a colour-blind hazard, mitigated in HTML only by the state word. See fix 2.
- No `prefers-color-scheme` handling. Opened in a dark-mode viewer, the page stays light because `color` and `background` are explicit only on `body` (color) and not the page background. WARNING.
- 60/30/10 split does not apply (no brand accent in a diagnostic report).

### Pillar 4: Typography (2/4)
- Distinct sizes: none declared, all default (16px), with `h1` and `h2` at UA defaults. Weights: normal plus bold (`tr.type td`). Within the 4-size and 2-weight guideline, but only by omission.
- `font-family: sans-serif` is the generic fallback, which differs across OSes. `pre` and `code` have no monospace stack set (UA default, typically a small size). The signatures in the table cells (`HtmlWriter.cs:65`) are code but are set in the proportional font, which hurts the readability of `A -> B` signature diffs. WARNING.
- No `line-height` or max line length set.

### Pillar 5: Spacing (3/4)
- Em-based and consistent: `body margin 2em`, `th, td padding .25em .6em`, `table margin 1em 0`, `pre padding 1em` (lines 23-30). No arbitrary px values. No Tailwind present.
- No `max-width` on body, so on a 1440px screen the table stretches to content and `pre` scrolls horizontally only with `overflow: auto`. On mobile (375px), the 3-column table with long signatures will overflow the viewport and there is no viewport meta tag. WARNING.
- Table cells have a 1px border on all sides (`border-collapse: collapse`), which is dense for large member lists.

### Pillar 6: Experience Design (2/4)
- Loading and error states: N/A for a generated static file. The CLI returns exit 1 on tool failure (D-03), so no partial report is meant to be produced. Not scored.
- Empty state: missing (see fix 3). Warnings are listed only when present (`HtmlWriter.cs:45`), which is right.
- No script, no external URL: verified in source (the XSS and privacy posture is strong and is a plus). All dynamic text is HTML-encoded. `Enc(type.State)` is used inside a `class` attribute (line 60), which is encoded and safe.
- Accessibility: `lang="en"` is present, `<title>` is generic ("vdiff report") and carries no base/head identity, so multiple saved reports are indistinguishable in tabs. WARNING. No `<caption>` or `scope` on `th`. No skip links needed.
- No sticky header, no way to filter or collapse unchanged members for large PRs (Phase 7 scope).
- Registry audit: not applicable (no `components.json`, no shadcn).

---

## Not Applicable

- Component library, Tailwind, shadcn, icon buttons, loading skeletons, destructive-action confirmation and breakpoints: no such UI exists in this phase.
- PowerShell trial script and console output: no visual layer to score.

## Files Audited
- `src/Vdiff.Core/Output/HtmlWriter.cs`
- `src/Vdiff.Core/Output/MermaidWriter.cs`
- `.planning/phases/01-offline-tracer/01-CONTEXT.md`
