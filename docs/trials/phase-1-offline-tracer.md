# Phase 1 offline tracer: trial record

This file contains no target identifiers, paths, commit ids, counts or timings beyond the buckets below.

## TRC-03: run on a private local clone

Run date: 2026-11-17

```
result: pass
reason: none
wall_clock: under-1-min
repo_unchanged: yes
outputs_present: yes
mermaid_syntax: ok
```

This is a single run judged against the R26 budget (P90 under 5 minutes, 20-minute hard limit), not a P90 measurement.

## SPK-02 evidence

SPK-02 is satisfied. ADR-001 records the C++ tree-sitter packaging choice together with its comparison. The tracer's C++ extraction runs through the sidecar that ADR-001 chose: the two pinned versions, tree-sitter 0.27.0 and tree-sitter-cpp 0.23.4, are recorded in every model's extraction metadata and are asserted by the end-to-end test. ADR-001 was accepted by the product owner on 2026-11-16, and its status line reads Accepted.

## Pending

- The trial ran locally on Windows against a private clone (the target is not named). The Ubuntu-runner question is deferred until after this first pass.
- Spike S1 (Phase 8) and spike S3 (Phase 4) are not part of this phase.

## Known limitations of the C++ extraction

- A typedef of an anonymous struct is not extracted.
- A macro call with arguments inside a class body can yield a bogus member that `--blank` cannot fix.

Both are labelled gaps, recorded here and not fixed in Phase 1 (D-11).
