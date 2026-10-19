# PHASE 01 — Step 002 Evidence

**Step:** Audit upstream analysis, layout, theme, effects, XSQ writer, preview and dependency boundaries.
**Spec:** [`docs/phase-specs/PHASE-01.md`](../../docs/phase-specs/PHASE-01.md), Section 6, Step 002.
**Date:** 2026-10-10
**Status: PASS**

> Filename note: this file is named `STEP-02.md` to match the task briefing. The phase spec's own
> template and its Step 001/.../008 headings use the 3-digit form (`STEP-002.md`); Step 001's
> evidence is filed as `STEP-001.md` for that reason. Both names are recorded here for continuity;
> future steps should pick one convention and stick to it — recommend `STEP-0NN.md` throughout to
> match the spec exactly, carried as an open item rather than silently diverging again.

## 1. Baseline check (prior step contract)

Per instructions, re-ran Step 001's suite before starting — unmodified, all green:

```text
$ dotnet build XlightsAI.Studio.slnx
Build succeeded. 0 Warning(s) 0 Error(s)

$ dotnet test XlightsAI.Studio.slnx
Passed!  - Failed: 0, Passed: 9, Skipped: 0, Total: 9
```

No files under `src/XlightsAI.Studio.Reference/*.cs` or `reference/UPSTREAM-REFERENCE.json` from
Step 001 were modified by this step — Step 002 only adds new files alongside them.

## 2. What was built

Same layering discipline as Step 001 (no UI yet — Step 004 owns the Avalonia shell):

- **Data:** `reference/MODULE-AUDIT-MATRIX.json` — the actual required deliverable, "a
  module-by-module reuse/port/replace/omit matrix with implementation risks," covering 13 upstream
  source boundaries, each tied to the exact Step 001 pinned commit SHA.
- **Domain:** added to the existing `XlightsAI.Studio.Reference` library —
  `ModuleAuditEntry`/`ModuleAuditMatrix` (pure data contracts, `ModuleDecision` enum using the
  spec's own vocabulary: Reuse/Port/Replace/Omit) and `ModuleAuditValidator` (pure rules, see §4).
- **Adapter:** `JsonModuleAuditMatrixReader` (read-only, same fail-safe pattern as Step 001's
  manifest reader).
- **Application:** `ModuleAuditService.AuditAsync` — reads + validates + produces
  `ModuleAuditReport.ToDisplayText()` as the observable public output.
- **Tests:** `Step002AcceptanceTests` (xUnit) — `Step002_Acceptance_Passes` plus four distinct
  negative-path tests and one test that directly proves the "never rewrites source files on
  failure" edge case by hashing every fixture before/after every failure mode.

## 3. The actual audit (grounded in the pinned commit, not assumptions)

Performed by reading real files inside `reference/xlights-autosequencer` (pinned commit
`e3d5b062b0e338f2fbeec47c795386dae33e110e`, same commit Step 001 pinned — unchanged). Full
per-module write-up with cited file paths is in `reference/MODULE-AUDIT-MATRIX.json`; summary:

| Module | Decision | Risk | One-line rationale |
|---|---|---|---|
| `src/analyzer` | Port | High | Hierarchy data shape/orchestration is portable; vamp/madmom/demucs native runtimes are not. |
| `src/grouper` | Port | High | Tier-generation concept is portable; **write path overwrites the real show file in place (see §4).** |
| `src/themes` | Reuse | Low | JSON theme schema is implementation-agnostic domain data. |
| `src/effects` | Reuse | Medium | Pure xLights domain knowledge (storage keys); byte-exact contract risk. |
| `src/variants` | Reuse | Medium | Hardware-verified presets; non-obvious flags must be preserved exactly. |
| `src/generator` (xsq_writer, plan) | Port | High | Highest-value module — live-hardware-verified rendering knowledge; confirmed it never overwrites source files (always writes to an explicit output path). |
| `src/review` | Replace | Medium | Flask/React has no place in Avalonia; useful only as a design reference. |
| `src/story` | Port | Medium | Not in the original audit list, but found to be a hard dependency of `src/generator` — recorded so no later phase is surprised. |
| `src/evaluation` | Omit | Low | Upstream's own dev/QA tooling, not runtime logic. |
| `src/video` | Omit | Low | Explicitly out of this phase's scope; optional upstream extra. |
| `src/validation` | Omit | Low | Not independently read beyond confirming it exists — recorded as an honest scope placeholder, not a completed audit (see §6). |
| `src/packaging` | Omit | Low | Upstream's own Python/Rust desktop-bundling helpers; informational precedent only. |
| `pyproject.toml` (dependency footprint) | Replace | High | numpy/vamp/madmom/demucs/torch/ffmpeg native stack has no .NET/NuGet equivalent. |

## 4. Key finding — direct source verification

**Claim:** `src/grouper/writer.py` can overwrite the user's real `xlights_rgbeffects.xml` in place,
with only a one-time backup.

**Verified directly** (not taken on faith from the research pass) by reading
`reference/xlights-autosequencer/src/grouper/writer.py:77-89`:

```python
def write_layout(layout: Layout, output_path: str | Path) -> None:
    """Write the raw_tree to output_path, creating a .xml.bak backup for in-place writes."""
    output_path = Path(output_path)

    # Create backup before in-place overwrite (preserves pristine original)
    bak_path = output_path.with_name(output_path.name + ".bak")
    if output_path == layout.source_path and not bak_path.exists():
        shutil.copy2(output_path, bak_path)
    ...
```

The backup at line 83 only fires `if ... not bak_path.exists()` — i.e. **only on the very first
in-place write**. Every subsequent run overwrites the live show file with zero fresh backup. This
is the single highest-risk pattern found in this audit relative to this Studio's own hard
constraint ("Never overwrite source show files" — `docs/AGENTS.md`, `docs/phase-specs/PHASE-01.md`
§2/§4).

By contrast, `src/generator`'s `.xsq` writer was also checked and does **not** share this risk: it
always writes to its caller-supplied `output_path`, and only *copies* (never moves/overwrites)
referenced audio/video media next to that output, skipping the copy if a file already exists there.

This finding drives an explicit domain rule (§5) rather than just a documentation note: the audit
matrix format itself enforces that any module flagged `sourceOverwriteRisk: true` must carry a
non-empty `overwriteMitigation`, so this class of risk cannot be silently waved through in a future
module's audit entry either.

## 5. Domain rules enforced by `ModuleAuditValidator`

1. `SchemaVersion` must equal the one version this Studio currently supports (1); anything else is
   rejected, never silently coerced.
2. `SourceCommitSha` must equal the commit pinned by **Step 001** (passed in by the caller from
   Step 001's own manifest, not hardcoded twice) — a module audit for a different commit is
   rejected as stale, directly enforcing "treat prior step contracts as immutable."
3. At least one module must be present; every module needs a non-empty `ModulePath` and `Rationale`.
4. A module marked `RiskLevel: High` must list at least one concrete risk — no unexplained "High."
5. **A module flagged `SourceOverwriteRisk: true` must carry a non-empty `OverwriteMitigation`** —
   the rule directly motivated by §4's finding.

All five rules return a structured `ModuleAuditValidationResult` (never throw) so bad data is a
normal, testable outcome, not an exception path.

## 6. Honesty note on `src/validation`

The research pass found `src/validation` exists but did not deep-read it (budget was spent on the
eight explicitly-requested boundaries plus the one load-bearing dependency it surfaced,
`src/story`). Rather than fabricate a decision, the matrix entry for `src/validation` says exactly
that — "not independently read... recorded as an honest scope placeholder, not a completed audit"
— with its own `risks` entry warning not to treat its `omit` as confirmed. This is a deliberate,
recorded gap, not an oversight.

## 7. Commands actually executed

```text
$ dotnet build XlightsAI.Studio.slnx
Build succeeded. 0 Warning(s) 0 Error(s)

$ dotnet test XlightsAI.Studio.slnx --logger "console;verbosity=detailed"
Passed XlightsAI.Studio.Reference.Tests.Step001AcceptanceTests.* (9 tests, all passed, unmodified)
Passed XlightsAI.Studio.Reference.Tests.Step002AcceptanceTests.Step002_Acceptance_Passes
Passed XlightsAI.Studio.Reference.Tests.Step002AcceptanceTests.Step002_Acceptance_Passes_RejectsUnmitigatedOverwriteRisk_NegativePath
Passed XlightsAI.Studio.Reference.Tests.Step002AcceptanceTests.RejectsMatrixAuditedAgainstADifferentCommit_NegativePath
Passed XlightsAI.Studio.Reference.Tests.Step002AcceptanceTests.RejectsUnsupportedSchemaVersion_NegativePath
Passed XlightsAI.Studio.Reference.Tests.Step002AcceptanceTests.AuditAsync_MissingMatrixFile_FailsSafelyWithoutThrowing
Passed XlightsAI.Studio.Reference.Tests.Step002AcceptanceTests.AuditAsync_MalformedJson_FailsSafelyWithoutThrowing
Passed XlightsAI.Studio.Reference.Tests.Step002AcceptanceTests.InvalidModelFormatOrVersion_NeverRewritesTheSourceFixtureOnDisk

Test Run Successful.
Total tests: 16
     Passed: 16
 Total time: 1.53 Seconds
```

PowerShell was used for all commands (Git Bash remains non-functional in this environment per the
Step 001 note).

## 8. Automated test results — detail

- **`Step002_Acceptance_Passes`** — loads the real, deterministic matrix fixture (byte-identical
  copy of `reference/MODULE-AUDIT-MATRIX.json`) and the real Step 001 manifest fixture together,
  asserting: the matrix validates against the live Step 001 pinned SHA (cross-step contract, not a
  hardcoded duplicate constant); at least 10 real module entries; all four decision kinds
  (Reuse/Port/Replace/Omit) are represented; every module has a rationale; every High-risk module
  has concrete risks; the `src/grouper` entry specifically carries `SourceOverwriteRisk: true` with
  a real mitigation; and the public display text contains the decision summary and the grouper
  finding.
- **Four distinct negative-path tests**, each independent and specific (not one generic "throws"
  test): an overwrite risk with no mitigation, a matrix audited against a commit other than Step
  001's pin, an unsupported schema version, and (reused from Step 001's pattern) a missing file and
  malformed JSON — all return a structured invalid report and never throw.
- **`InvalidModelFormatOrVersion_NeverRewritesTheSourceFixtureOnDisk`** — directly operationalizes
  the step's stated edge case ("invalid model/format/version must fail safely without rewriting
  source files") as an executable assertion: SHA-256 hashes every fixture before and after running
  it through every known failure mode (unmitigated risk, stale commit, bad schema version,
  malformed JSON, missing file) and asserts byte-for-byte equality. This is read-only-by-design
  code (no write method exists on the reader/service at all), and the test proves it rather than
  merely asserting it by inspection.

## 9. Manual / environment verification

- No xLights installation, UI shell, or `.xsq` artifact exists yet at this step — Step 002 is a
  documentation/audit deliverable per the spec's own step description, and there is nothing
  xLights-observable to demonstrate yet (Step 004 adds the Avalonia shell; a generated `.xsq` first
  appears in Phase 02). Nothing BLOCKED here because nothing was claimed that wasn't run.
- The one piece of "real system" verification this step could do — directly reading the pinned
  upstream source to confirm the grouper overwrite behavior — was done (§4), not taken on faith.

## 10. Changed/added paths

```
reference/MODULE-AUDIT-MATRIX.json
src/XlightsAI.Studio.Reference/ModuleAuditMatrix.cs
src/XlightsAI.Studio.Reference/ModuleAuditValidator.cs
src/XlightsAI.Studio.Reference/IModuleAuditMatrixReader.cs
src/XlightsAI.Studio.Reference/JsonModuleAuditMatrixReader.cs
src/XlightsAI.Studio.Reference/ModuleAuditService.cs
tests/XlightsAI.Studio.Reference.Tests/Step002AcceptanceTests.cs
tests/XlightsAI.Studio.Reference.Tests/Fixtures/valid-module-audit-matrix.json
tests/XlightsAI.Studio.Reference.Tests/Fixtures/unmitigated-overwrite-risk-matrix.json
tests/XlightsAI.Studio.Reference.Tests/Fixtures/stale-commit-matrix.json
tests/XlightsAI.Studio.Reference.Tests/Fixtures/unsupported-schema-version-matrix.json
implementation-evidence/PHASE-01/STEP-02.md
```

Nothing under Step 001's paths was modified. No original xLights show files exist in this
repository to overwrite; the pinned reference submodule itself was not written to (read-only
source inspection only — `grep`/`Read`, no edits inside `reference/xlights-autosequencer/`).

## 11. Fixture hashes (SHA-256)

| File | SHA-256 |
|---|---|
| `reference/MODULE-AUDIT-MATRIX.json` | `00F02069DF2CD57D512A18E42481D1D6416FAE26A7913C78194B39B3ED0892B4` |
| `tests/.../Fixtures/valid-module-audit-matrix.json` (byte-identical copy) | `00F02069DF2CD57D512A18E42481D1D6416FAE26A7913C78194B39B3ED0892B4` |
| `tests/.../Fixtures/unmitigated-overwrite-risk-matrix.json` | `B685CD46276F4C6A3846080DA18C2D4F61ACE9B5C5F37EE1022B2504C8B387D9` |
| `tests/.../Fixtures/stale-commit-matrix.json` | `75FC769CB1C8C95856D470AC004E7F2C0D070C0C2B59275F04745C4157222E12` |
| `tests/.../Fixtures/unsupported-schema-version-matrix.json` | `E578446542D68561EBE9EA7126E963BF036BFC0544DF9C0511051C59CF09E8C2` |

Upstream pinned commit is unchanged from Step 001: `e3d5b062b0e338f2fbeec47c795386dae33e110e`.

## 12. Architecture deviations

- **None from the phase spec's architecture.** This step continues in the same
  `XlightsAI.Studio.Reference` project/namespace Step 001 created, rather than starting a new
  project — Step 001 and Step 002 are both "audit the pinned reference" concerns in the same
  bounded context, and the spec's own guidance ("do not fabricate generic interfaces without a
  concrete boundary") argues against splitting them prematurely.
- **One minor, disclosed deviation from the task briefing**: evidence filename `STEP-02.md` vs. the
  phase spec's own `STEP-002.md` convention — see the note at the top of this file.

## 13. Unresolved issues / risks carried forward

1. `src/grouper`'s in-place overwrite behavior (§4) is now a **documented and gated** risk, but no
   C# code has been written against it yet — the actual replacement write-path design is deferred
   to whichever future phase ports the grouper (not specified in PHASE-01's eight steps; layout
   import is Phase 02's "Depends on" boundary per the phase spec's Section 1).
2. `src/validation` remains an honest open item (§6), not a completed audit — flagged in the
   matrix's own `risks` field so it surfaces again if a later phase depends on it.
3. The native-dependency "Replace" decisions for `src/analyzer`'s audio runtimes and the root
   `pyproject.toml` footprint are directional, not designed — no architecture decision has been
   made yet about a Python sidecar vs. .NET-native audio analysis; that is explicitly a decision
   for whichever phase first requires real beat/onset detection (Phase 05 per the phase index), not
   this step.
4. Evidence-filename convention inconsistency (§12) — recommend settling on one form before Step
   003.

## 14. Gate checklist (this step only)

- [x] Module-by-module reuse/port/replace/omit matrix created, with implementation risks recorded
      per module (`reference/MODULE-AUDIT-MATRIX.json`).
- [x] Deterministic fixture + automated test asserting persisted domain state and public output
      (`Step002_Acceptance_Passes`).
- [x] At least one negative-path assertion included — four were added, each distinct.
- [x] "Invalid model/format/version must fail safely without rewriting source files" proven by a
      dedicated hash-before/hash-after test, not just asserted in prose.
- [x] `dotnet build` and `dotnet test` executed and recorded, including Step 001's unmodified
      regression suite (baseline-first, per instructions).
- [x] No original/show files touched; the one real safety-critical finding (grouper overwrite) is
      called out explicitly rather than glossed over.
- [ ] Not yet done, out of this step's scope: actually porting any audited module's code (Step 004+
      and later phases); resolving the `src/validation` gap; designing the native-audio-runtime
      replacement strategy.

**Step 002 status: PASS.** Proceed to Step 003 only after this is reviewed.
