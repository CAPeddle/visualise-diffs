# Phase 2: Model Contract and Fixture Harness - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-11-19
**Phase:** 02-model-contract-and-fixture-harness
**Areas discussed:** Contract form & volatile split, Fixture layout & goldens, Red-by-design in CI, Private-fixture recipe

---

## Contract form & volatile split

| Question | Options | Selected |
|----------|---------|----------|
| Source of truth for vdiff-model/v1 | JSON Schema file (Recommended) / C# records are the contract / Schema generated from records | JSON Schema file |
| Where volatile metadata lives | Separate sidecar file (Recommended) / Top-level `meta` section / You decide | You decide |
| Diff contract timing | Freeze vdiff-diff/v1 now (Recommended) / Draft now, may change in Phase 5 / Model only; diff goldens in Phase 5 | Freeze vdiff-diff/v1 now |
| Tracer migration | Yes, migrate and drop v0 (Recommended) / Migrate model only / Keep tracer on v0 | Migrate and drop v0 |

## Fixture layout & goldens

| Question | Options | Selected |
|----------|---------|----------|
| Fixture storage | base/ + head/ trees (Recommended) / Committed git bundles / C# builder scripts | base/ + head/ trees |
| Golden authoring | Hand-written spec-first (Recommended) / Approval only | Hand-written spec-first |
| Expected outputs now | diff.json + summary.mmd (Recommended) / Also models + node sets / diff.json only | diff.json + summary.mmd |
| Comparison | Exact bytes, readable diff (Recommended) / Semantic JSON compare / You decide | Exact bytes, readable diff |

## Red-by-design in CI

| Question | Options | Selected |
|----------|---------|----------|
| CI host | GitHub Actions (Recommended) / Azure Pipelines / Both | Azure Pipelines |
| ADO organisation | Personal ADO org (Recommended) / Target organisation / YAML only, wire later | YAML only, wire later |
| Red policy | Expected-failure ratchet (Recommended) / Separate always-red job / Truly red build | Expected-failure ratchet |
| OS legs | Windows + Linux, fix 999.1 here (Recommended) / Windows only / Windows + Linux + macOS | Windows + Linux, fix 999.1 here |

**Notes:** The user chose Azure Pipelines over the recommended GitHub Actions, then chose to commit the YAML only and connect it to an organisation later.

## Private-fixture recipe

| Question | Options | Selected |
|----------|---------|----------|
| Recipe form | Doc + external-path entry (Recommended) / Document only | Doc + external-path entry |
| How a private fixture points at its PR | Repo path + base/head SHAs (Recommended) / Copied trees like synthetic | Repo path + base/head SHAs |
| What is recorded back here | Pass/fail per shape only (Recommended) / Nothing recorded here | Pass/fail per shape only |

## Claude's Discretion

- How volatile metadata is kept apart (separate file or one shared canonicaliser).
- Where commit SHAs sit, how the harness drives the tool, how a fixture's config maps to CLI flags, which schema validator to use, the content of each fixture, and whether CI runs the Mermaid syntax check.

## Deferred Ideas

- Connecting the pipeline to an ADO organisation.
- A macOS CI leg.
- Node-set goldens (Phase 6); revising the summary goldens after S1.
