---
phase: "02"
slug: "model-contract-and-fixture-harness"
status: verified
# threats_open = count of OPEN threats at or above workflow.security_block_on severity (the blocking gate)
threats_open: 0
asvs_level: 1
created: "2026-11-23"
---

# Phase 02 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| public repository to readers | every committed line, fixture, golden and schema is public (D15) | planning text, fixtures, schemas — must hold no target identifier |
| analysed repository to output files | type names, paths, signatures, commit ids and tool versions become JSON fields | target source structure (private in private mode) |
| repository to Azure Pipelines | the committed YAML is executed by a hosted agent once connected | pipeline definition, checkout credentials |
| NuGet registry to test project | third-party code enters the test build | NJsonSchema 11.6.2, YamlDotNet 18.1.0 |
| fixture.yml and fixture trees to the harness | fixture files are read and copied into a temporary repository | fixture input (synthetic or private) |
| harness verdict to CI status | the ratchet decides whether red fixtures keep the pipeline green | RED / FAIL / PASS verdicts |
| harness to committed goldens | accept mode is the only code that writes into tests/fixtures | golden bytes |
| private clone to private runner to console | target source is read read-only; only shape, status and date may cross | private fixture detail stays under VDIFF_PRIVATE_OUT |
| user decision to contract | the schema freeze turns a draft into the contract later phases build on | vdiff-model/v1 schemas |
| planning record to gsd-tools | tools read and rewrite REQUIREMENTS.md and SUMMARY frontmatter | requirement status |

---

## Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation | Status |
|-----------|----------|-----------|----------|-------------|------------|--------|
| T-02-01 | Information disclosure | run.meta.json | medium | mitigate | RunMeta fields limited, `+` metadata stripped; DeterminismTests leak test over OutputWriter.FileNames | closed |
| T-02-02 | Tampering | canonical output files | medium | mitigate | no commit id or tool version in canonical files (TracerEndToEndTests, DeterminismTests) | closed |
| T-02-03 | Information disclosure | azure-pipelines.yml | high | mitigate | PipelineYamlTests image allow-list, forbidden keys, AccessToken and pool checks | closed |
| T-02-04 | Elevation of privilege | pipeline checkout credentials | medium | mitigate | `persistCredentials: false`, no variables block; PipelineYamlTests | closed |
| T-02-05 | Tampering | fixture and schema bytes under autocrlf | medium | mitigate | .gitattributes eol=lf; RepoHygieneTests | closed |
| T-02-06 | Tampering | CI build supply chain | medium | mitigate | `--locked` cargo, `npm ci --ignore-scripts`; PipelineYamlTests | closed |
| T-02-07 | Information disclosure | model JSON paths | medium | mitigate | DeterminismTests local-identity test over every output file | closed |
| T-02-08 | Tampering | type identity across languages | medium | mitigate | ModelDiffer keys on language plus id; ModelDifferTests | closed |
| T-02-09 | Repudiation | order-dependent model output | medium | mitigate | Canonical.Canonicalise at build, pipeline and write; seeded shuffle tests | closed |
| T-02-10 | Repudiation | Ratchet.Evaluate | high | mitigate | tool, golden, output and schema checked before state; 16 vacuity rows in RatchetTests | closed |
| T-02-11 | Repudiation | fsmonitor tests off Windows | medium | mitigate | execute bit set off Windows plus positive-control asserts | closed |
| T-02-12 | Information disclosure | ratchet detail strings | low | mitigate | constant detail strings only; RatchetTests | closed |
| T-02-13 | Repudiation | NJsonSchema validation | high | mitigate | keyword allow-list, walker self-test, negatives, coverage guard (SchemaValidationTests) | closed |
| T-02-14 | Tampering | records drifting from schemas | medium | mitigate | SchemaParityTests definitions, property order, required set, closed objects | closed |
| T-02-15 | Information disclosure | schema error text | low | mitigate | SchemaSupport emits `<Kind> at <Path>` only | closed |
| T-02-16 | Repudiation | shuffle test passing vacuously | medium | mitigate | 25 seeds with reachability assertions | closed |
| T-02-17 | Tampering | schema edits after the freeze | medium | mitigate | SchemaFreezeTests SHA-256 pins (match verified in audit) | closed |
| T-02-18 | Elevation of privilege | freeze decided without the user | high | mitigate | blocking-human checkpoint in 02-06; approval recorded before the freeze commit | closed |
| T-02-19 | Tampering | fixture.yml | medium | mitigate | FixtureLoader size cap, strict deserialiser, shape and state checks | closed |
| T-02-20 | Tampering | fixture trees | medium | mitigate | FixtureLoader refuses links, build output, oversized files and trees | closed |
| T-02-21 | Tampering | accept mode | high | mitigate | AcceptMode refuses under CI, one shape, named files, link refusals, checks before copy | closed |
| T-02-22 | Repudiation | harness reading a crash as pending | high | mitigate | all four checks passed to the ratchet; FixtureHarnessTests failure rows | closed |
| T-02-23 | Tampering | CR or BOM in fixture bytes | medium | mitigate | RepoHygieneTests with guard self-test; .gitattributes | closed |
| T-02-24 | Information disclosure | fixture trees, goldens, README | high | mitigate | synthetic namespaces, shape allow-list, target-name guard clean over the phase range | closed |
| T-02-25 | Tampering | tracked-file guard loosened too far | medium | mitigate | RepoHygieneTests exactly five segments, flagged and accepted rows | closed |
| T-02-26 | Repudiation | goldens that can never match | medium | mitigate | FixtureGoldenTests schema, presence and round-trip checks | closed |
| T-02-27 | Repudiation | pending output matching by accident | medium | mitigate | the ratchet treats a matching pending output as FAIL; all fixtures pending | closed |
| T-02-28 | Information disclosure | console and test output in private mode | high | mitigate | PrivateFixtures writes detail to files only; fixed line grammar; PrivateFixtureTests | closed |
| T-02-29 | Information disclosure | private output written into this repository | high | mitigate | refusal before first write, VDIFF_PROTECTED_DIRS set to the repo root; PrivateFixtureTests | closed |
| T-02-30 | Information disclosure | directory names in the fixtures root | medium | mitigate | PrivateFixtures name check; PrivateFixtureTests | closed |
| T-02-31 | Tampering | private fixture.yml | medium | mitigate | FixtureLoader private path, no detail echoed; FixtureHarnessTests | closed |
| T-02-32 | Information disclosure | the public record | medium | mitigate | PrivateFixtureTests on docs/trials/private-fixtures.md (shape, status and date only) | closed |
| T-02-33 | Information disclosure | csharp and cross-stack fixtures | high | mitigate | Synthetic.* project, namespace and package names; guard clean | closed |
| T-02-34 | Tampering | fixture .csproj and .vcxproj files | low | mitigate | vdiff.sln lists only product projects; the pipeline builds vdiff.sln only | closed |
| T-02-35 | Repudiation | C++/CLI header silently breaking the harness | medium | mitigate | cross-stack outputs pending; RED not FAIL recorded | closed |
| T-02-36 | Repudiation | output-set assertions | low | mitigate | exact-equality checks against OutputWriter.FileNames across suites | closed |
| T-02-37 | Repudiation | order-dependent diff output | medium | mitigate | canonical diff ordering; FullDiff shuffle test | closed |
| T-02-38 | Tampering | NuGet NJsonSchema and YamlDotNet | high | mitigate | blocking-human package checkpoint approved; exact version pins; test project only | closed |
| T-02-39 | Tampering | frozen rules the schema cannot express | medium | mitigate | ModelPresence and DiffPresence checks with sole-problem rows | closed |
| T-02-40 | Tampering | REQUIREMENTS ACC-01 row and SUMMARY frontmatter | medium | mitigate | ACC-01 Partial and unticked; absent from every requirements-completed list | closed |
| T-02-41 | Information disclosure | commits of planning text | high | mitigate | target-name guard clean over diff content and commit messages | closed |
| T-02-SC | Tampering | package installs with no dedicated row | low | accept | see Accepted Risks Log | closed |

*Status: open · closed · open — below high threshold (non-blocking)*
*Severity: critical > high > medium > low — only open threats at or above workflow.security_block_on count toward threats_open*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-02-01 | T-02-SC | Each plan installs no package, or only the two NuGet pins gated by T-02-38; `npm ci --ignore-scripts` restores the lock pinned in Phase 1. The rationale is in every 02-NN-PLAN.md threat model. | plan-time threat model (02-01 to 02-13) | 2026-11-23 |

*Accepted risks do not resurface in future audit runs.*

---

## Audit Notes (non-blocking)

- **N-1.** 02-UAT.md test 1 (both pipeline legs) reads pass with no run reference, while the Deferred Follow-Ups note in the same file, 02-VERIFICATION.md and the azure-pipelines.yml header say the pipeline is not yet connected. The owner should add the run id or correct the record.
- **N-2.** PipelineYamlTests has no negative row for the `project`, `azureSubscription` and `connectedServiceName` keys, although T-02-03 promises one per key. They share the tested rejection path with `organization`. Closed at L1; a finding at L2.
- **N-3.** No test sends a schema-invalid output through FixtureHarness.Evaluate; the wiring is pinned one layer down. Linked to open review finding WR-04, which fails closed. Closed at L1; open at L2.
- **N-4.** The public-diff guard script runs by hand before each commit, has no hook, scans diff content only, and reports clean when `git diff` itself fails (no pipefail). Suggested: check git's exit status and install it as a pre-commit hook.
- **U-1.** Unregistered low flag from 02-04: FixtureReport.WriteIfRequested writes fixed-text rows to the path in VDIFF_FIXTURE_REPORT.
- Open review findings that touch registered threats (WR-04, WR-05, IN-06, IN-07) remove no declared control.
- One owner decision is outstanding outside the register; details were delivered separately.

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-11-23 | 42 | 42 | 0 | gsd-security-auditor (ASVS L1, block_on high) |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-11-23
