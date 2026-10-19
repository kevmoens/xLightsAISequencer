# PHASE 01 — Step 003 Evidence

**Step:** Create baseline fixture corpus: synthetic notes, drum pulse, instrumental, complex mixed song and 2 layouts.
**Spec:** [`docs/phase-specs/PHASE-01.md`](../../docs/phase-specs/PHASE-01.md), Section 6, Step 003.
**Date:** 2026-10-10
**Status: PASS**

> Filename note: this file is named `STEP-03.md` to match the task briefing, continuing Step 002's
> precedent (`STEP-02.md`). Step 001 used the spec's own 3-digit form (`STEP-001.md`). This
> inconsistency was already flagged as an open item in Step 002's evidence (§12/§13); still
> unresolved, carried forward again below.

## 1. Baseline check (prior step contracts, unmodified)

Re-ran the full existing suite before starting, unmodified:

```text
$ dotnet build XlightsAI.Studio.slnx
Build succeeded. 0 Warning(s) 0 Error(s)

$ dotnet test XlightsAI.Studio.slnx --logger "console;verbosity=detailed"
Passed!  - Failed: 0, Passed: 16, Skipped: 0, Total: 16
```

No file under `src/XlightsAI.Studio.Reference/*.cs` (Step 001/002), `reference/UPSTREAM-REFERENCE.json`,
or `reference/MODULE-AUDIT-MATRIX.json` was modified by this step — only new files were added
alongside them, in a new `Fixtures/` sub-namespace of the existing project (same reasoning as
Step 002: a narrow, already-established bounded context — "Foundation and Reference Baselines" —
rather than a premature new project split).

## 2. What was built

- **Data (the actual required deliverable):** a real, on-disk baseline fixture corpus under
  `fixtures/` — four synthetic audio fixtures (`fixtures/audio/*.wav`) and two structurally distinct
  xLights-shaped layout XML fixtures (`fixtures/layouts/*.xml`) — described by
  `fixtures/FIXTURE-CORPUS-MANIFEST.json`, which records, per fixture: id, kind, relative path,
  SHA-256 hash, permitted usage / rights statement, license note, reference version, and expected,
  independently-checkable outputs (sample rate, duration, note/click counts, model counts).
- **Domain:** `XlightsAI.Studio.Reference.Fixtures` namespace —
  `FixtureKind`/`FixtureEntry`/`FixtureCorpusManifest` (pure data contracts),
  `FixtureCorpusValidator` (pure rules: schema version, unique non-empty ids, well-formed SHA-256,
  non-empty usage/license/reference-version/expected-outputs, and the step's own minimum corpus
  shape — at least one synthetic-notes, one drum-pulse-click-track, one instrumental-phrase, one
  complex-mixed-song, and **at least two** layout-shape entries), and
  `DeterministicSyntheticAudioGenerator` — a pure, no-randomness, no-wall-clock algorithm that
  synthesizes every audio fixture's PCM samples and encodes them as 16-bit PCM WAV. This is what
  makes "repeatable invocation" (the step's required acceptance) a provable fact rather than an
  assertion: the same recipe parameters always produce byte-identical output.
- **Adapter:** `JsonFixtureCorpusManifestReader` (read-only, same fail-safe pattern as Steps 001/002).
- **Application:** `FixtureCorpusService.AuditAsync` — reads + validates the manifest, and (when a
  fixtures root directory is supplied) re-hashes every referenced file on disk and reports any drift
  from the recorded hash (missing file, or content changed). `FixtureCorpusAuditReport.ToDisplayText()`
  is the observable public output.
- **Repeatable-invocation tool:** `tools/FixtureCorpusGenerator` (a small console app referencing
  the same library) — the literal, runnable command that (re)materializes every synthetic audio
  fixture and the manifest from fixed recipe constants. The two layout XML fixtures are hand-authored
  data (not synthesizable audio), so the tool only hashes them; it never writes to them. Running the
  tool twice in a row (Section 3) produced byte-identical SHA-256 hashes for all six fixtures,
  directly demonstrating the "repeatable invocation" required-acceptance criterion end-to-end, not
  just at the unit-test level.
- **Tests:** `Step003AcceptanceTests` (xUnit) — `Step003_Acceptance_Passes` plus a dedicated
  determinism test, two drift-detection tests (missing file, tampered file), two schema/content
  negative-path tests, the standard missing-file/malformed-JSON negative paths, and a hash-before/
  hash-after test proving the edge case "must fail safely without rewriting source files."

## 3. Commands actually executed

```text
$ dotnet build XlightsAI.Studio.slnx
Build succeeded. 0 Warning(s) 0 Error(s)

$ dotnet run --project tools\FixtureCorpusGenerator\FixtureCorpusGenerator.csproj --no-build -c Debug
Wrote 6 fixtures and manifest to 'C:\Code\xLightsAISequencer\fixtures'.
  synthetic-notes                  ce1813fc944ed7771cfe6a35cbec760d029ca322724c5e3c05ac1d4801de8902
  drum-pulse-click-track           7010b148c29ab4a363d928d62c467c16b2aab41be78943b061ba7695a6cae1b7
  instrumental-piano-phrase        2718168674a45202790d0e7ccb67e9c86d91d4de54b45ff2853779d7cedbeef8
  complex-mixed-song               18158e464a553ad6389f535b96a80902153c7c96d7f2d9710069111298dbddaf
  layout-shape-a-arch-megatree     336608e660526a3ab81cc7dce0c9fdcf2c790b496fe75f1b7f3ef62b55eddd12
  layout-shape-b-matrix-stars      b227386799700a734e05ad9a398fa01e54ed21c8a61a009693aa05ce552a11f1

# re-run immediately after, no source changes — "repeatable invocation" proof:
$ dotnet run --project tools\FixtureCorpusGenerator\FixtureCorpusGenerator.csproj --no-build -c Debug
Wrote 6 fixtures and manifest to 'C:\Code\xLightsAISequencer\fixtures'.
  synthetic-notes                  ce1813fc944ed7771cfe6a35cbec760d029ca322724c5e3c05ac1d4801de8902
  drum-pulse-click-track           7010b148c29ab4a363d928d62c467c16b2aab41be78943b061ba7695a6cae1b7
  instrumental-piano-phrase        2718168674a45202790d0e7ccb67e9c86d91d4de54b45ff2853779d7cedbeef8
  complex-mixed-song               18158e464a553ad6389f535b96a80902153c7c96d7f2d9710069111298dbddaf
  layout-shape-a-arch-megatree     336608e660526a3ab81cc7dce0c9fdcf2c790b496fe75f1b7f3ef62b55eddd12
  layout-shape-b-matrix-stars      b227386799700a734e05ad9a398fa01e54ed21c8a61a009693aa05ce552a11f1
# identical hashes both runs — confirmed byte-for-byte.

$ dotnet test XlightsAI.Studio.slnx --logger "console;verbosity=detailed"
Passed!  - Failed: 0, Passed: 27, Skipped: 0, Total: 27, Duration: 200 ms
```

All commands were run via PowerShell. The Bash tool in this environment again failed on every
invocation (exit 1, no output) when first attempted this step, consistent with the note already
carried forward from Steps 001/002.

## 4. The fixture corpus (recorded in `fixtures/FIXTURE-CORPUS-MANIFEST.json`)

| Fixture id | Kind | Recipe | Expected outputs |
|---|---|---|---|
| `synthetic-notes` | SyntheticNotes | 5-note ascending diatonic scale (C4–G4), 0.3s/note, 8000 Hz mono | duration 1.5s, 5 notes |
| `drum-pulse-click-track` | DrumPulseClickTrack | 120 BPM, 8 tone-burst clicks (20ms, 1000 Hz, linear decay), 8000 Hz mono | duration 4.0s, bpm 120, 8 clicks |
| `instrumental-piano-phrase` | InstrumentalPhrase | 4-note arpeggio (C4-E4-G4-C5), additive harmonics (fundamental + 2nd/3rd, exponential decay), 0.5s/note, 8000 Hz mono | duration 2.0s, 4 notes |
| `complex-mixed-song` | ComplexMixedSong | sum of a 3-note melody (G4-A4-B4, 1.0s/note) and a 6-click 120 BPM click track, both 8000 Hz mono, clamped to [-1,1] | duration 3.0s, 2 mixed sources |
| `layout-shape-a-arch-megatree` | LayoutShape | hand-authored: 1 Arches model + 1 Tree 360 (mega tree) model, 1 model group | 2 models, 1 group |
| `layout-shape-b-matrix-stars` | LayoutShape | hand-authored: 1 Matrix model + 2 Star models, 2 model groups | 3 models, 2 groups |

**Rights / provenance:** every audio fixture is 100% synthetically generated sine/harmonic tone —
no copyrighted recording is read, sampled, or mixed in, directly satisfying the architecture note
"collect synthetic audio ... fixtures without redistributing copyrighted songs." Both layout XML
fixtures are hand-authored directly for this repository (not exported from, or copied out of, any
real user's xLights show) and are explicitly commented as such in each file, and are **not** parsed
by any code yet — consistent with Phase 01's own out-of-scope note ("do not parse a real xLights
layout"); they exist purely as a stable, known input shape for a later phase's importer.

## 5. Automated test results — detail

```text
Passed XlightsAI.Studio.Reference.Tests.Step001AcceptanceTests.* (9 tests, unmodified)
Passed XlightsAI.Studio.Reference.Tests.Step002AcceptanceTests.* (7 tests, unmodified)
Passed XlightsAI.Studio.Reference.Tests.Step003AcceptanceTests.Step003_Acceptance_Passes
Passed XlightsAI.Studio.Reference.Tests.Step003AcceptanceTests.Step003_RepeatableInvocation_GeneratorReproducesCommittedAudioBytesExactly
Passed XlightsAI.Studio.Reference.Tests.Step003AcceptanceTests.AuditAsync_MissingFixtureFilesOnDisk_ReportsHashMismatchesAndIsNotAccepted
Passed XlightsAI.Studio.Reference.Tests.Step003AcceptanceTests.AuditAsync_TamperedFixtureFile_DetectsHashDriftAndIsNotAccepted
Passed XlightsAI.Studio.Reference.Tests.Step003AcceptanceTests.RejectsManifestMissingRequiredLayoutShapeCount_NegativePath
Passed XlightsAI.Studio.Reference.Tests.Step003AcceptanceTests.RejectsInvalidFixtureEntries_BadHashDuplicateIdAndMissingUsage_NegativePath
Passed XlightsAI.Studio.Reference.Tests.Step003AcceptanceTests.AuditAsync_MissingManifestFile_FailsSafelyWithoutThrowing
Passed XlightsAI.Studio.Reference.Tests.Step003AcceptanceTests.AuditAsync_MalformedJson_FailsSafelyWithoutThrowing
Passed XlightsAI.Studio.Reference.Tests.Step003AcceptanceTests.InvalidModelFormatOrVersion_NeverRewritesAnyFixtureFileOnDisk
Passed XlightsAI.Studio.Reference.Tests.Step003AcceptanceTests.Validator_AcceptsAFullyPopulatedManifest
Passed XlightsAI.Studio.Reference.Tests.Step003AcceptanceTests.Validator_RejectsNullManifest

Total tests: 27
     Passed: 27
```

- **`Step003_Acceptance_Passes`** — audits the real, committed corpus (manifest + all six on-disk
  fixture files) through `FixtureCorpusService`, and asserts: the manifest is accepted with zero
  hash drift against the actual files; all four required non-layout kinds are present; at least two
  `LayoutShape` entries exist; every fixture has a well-formed SHA-256, non-empty permitted-usage,
  non-empty reference version, and at least one expected output; and the public display text
  contains the generator identity and "Hash drift: none" — not just that a mock was invoked.
- **`Step003_RepeatableInvocation_...`** — calls `DeterministicSyntheticAudioGenerator` twice with
  the exact recipe used to author `synthetic-notes.wav` and asserts both runs hash identically to
  each other *and* to the real committed file — the unit-test-level proof backing the tool-level
  proof in Section 3.
- **Two drift-detection tests** — `fixture-corpus-incomplete` (audio files absent entirely) proves
  missing-file detection; `fixture-corpus-tampered` (one byte flipped in `synthetic-notes.wav`)
  proves content-drift detection. Both leave `Validation.IsValid == true` (the manifest JSON itself
  is fine) while `IsAccepted == false` (hash mismatches present) — proving these are independent
  checks.
- **Two schema/content negative-path tests** — a manifest with only one `LayoutShape` entry
  (rejects "at least 2 fixture(s) of kind LayoutShape"); a manifest with an invalid-format SHA-256,
  a duplicate fixture id, and an empty `PermittedUsage`, all three rejected simultaneously with
  distinct, specific error messages (not one generic failure).
- **`InvalidModelFormatOrVersion_NeverRewritesAnyFixtureFileOnDisk`** — directly operationalizes the
  step's stated edge case: SHA-256 hashes every real file in the committed `fixture-corpus` test
  copy, runs the service through five known failure modes (missing layout kind, invalid entries,
  malformed JSON, missing manifest file, missing fixture files, tampered fixture file against a
  *different* root), then re-hashes and asserts byte-for-byte equality. `FixtureCorpusService` has
  no write method at all (read + hash-compare only), and this test proves that by observation
  rather than by code inspection alone.
- **Negative-path manifests** (`missing-layout-kind-manifest.json`, `invalid-fixture-entries-manifest.json`)
  and tampered/incomplete fixture-root copies are checked into
  `tests/XlightsAI.Studio.Reference.Tests/Fixtures/` alongside a byte-identical copy of the real
  corpus, following the same pattern Steps 001/002 established.

## 6. Manual / environment verification

- **xLights validation:** not applicable to this step. This step adds data fixtures and a
  deterministic-generation/audit pipeline only; it does not generate, parse, or render an `.xsq` or
  any xLights-observable artifact (Phase 01's own scope note: "do not parse a real xLights layout").
  Checked for completeness: no xLights installation is present on this machine
  (`Get-Command xLights` and both standard Program Files paths all resolved to nothing/`False`) —
  consistent with there being nothing to validate against yet, not a workaround for a missing tool.
- **Audio fixture playback:** not manually auditioned in a DAW/player as part of this evidence run;
  the fixtures are pure sine/harmonic synthesis and their correctness is verified structurally
  (sample rate, duration, note/click counts, WAV header fields via the encoder itself) rather than
  by ear. Flagged as an open item below.
- **Repeatable invocation** was demonstrated as an actual CLI run, twice, with identical output
  hashes (Section 3) — this is the real "observable result" this step's deliverable is about, since
  there is no new UI screen in this step's scope (Step 004 adds the Avalonia shell).

## 7. Changed/added paths

```
XlightsAI.Studio.slnx                                                        (added /tools/ folder + project)
fixtures/FIXTURE-CORPUS-MANIFEST.json
fixtures/audio/synthetic-notes.wav
fixtures/audio/drum-pulse-click-track.wav
fixtures/audio/instrumental-piano-phrase.wav
fixtures/audio/complex-mixed-song.wav
fixtures/layouts/layout-shape-a-arch-megatree.xml
fixtures/layouts/layout-shape-b-matrix-stars.xml
tools/FixtureCorpusGenerator/FixtureCorpusGenerator.csproj
tools/FixtureCorpusGenerator/Program.cs
src/XlightsAI.Studio.Reference/Fixtures/FixtureCorpusManifest.cs
src/XlightsAI.Studio.Reference/Fixtures/FixtureCorpusValidator.cs
src/XlightsAI.Studio.Reference/Fixtures/IFixtureCorpusManifestReader.cs
src/XlightsAI.Studio.Reference/Fixtures/JsonFixtureCorpusManifestReader.cs
src/XlightsAI.Studio.Reference/Fixtures/FixtureCorpusService.cs
src/XlightsAI.Studio.Reference/Fixtures/DeterministicSyntheticAudioGenerator.cs
tests/XlightsAI.Studio.Reference.Tests/Step003AcceptanceTests.cs
tests/XlightsAI.Studio.Reference.Tests/XlightsAI.Studio.Reference.Tests.csproj  (copy *.wav/*.xml fixtures to output)
tests/XlightsAI.Studio.Reference.Tests/Fixtures/missing-layout-kind-manifest.json
tests/XlightsAI.Studio.Reference.Tests/Fixtures/invalid-fixture-entries-manifest.json
tests/XlightsAI.Studio.Reference.Tests/Fixtures/fixture-corpus/**                 (byte-identical copy of fixtures/)
tests/XlightsAI.Studio.Reference.Tests/Fixtures/fixture-corpus-incomplete/**      (manifest + layouts only, no audio)
tests/XlightsAI.Studio.Reference.Tests/Fixtures/fixture-corpus-tampered/**        (copy with 1 byte flipped)
implementation-evidence/PHASE-01/STEP-03.md
```

Nothing under Step 001's or Step 002's existing paths was modified. No real xLights show files
exist in this repository to overwrite; every layout fixture this step creates is new, synthetic,
and explicitly labeled as such — nothing pre-existing was at risk.

## 8. Fixture and artifact hashes (SHA-256)

| File | SHA-256 |
|---|---|
| `fixtures/FIXTURE-CORPUS-MANIFEST.json` | `51a6104f651cfbe7846573c49c6662fd0c8a7c6efa69f4dbef51eea5da7a72fe` |
| `fixtures/audio/synthetic-notes.wav` | `ce1813fc944ed7771cfe6a35cbec760d029ca322724c5e3c05ac1d4801de8902` |
| `fixtures/audio/drum-pulse-click-track.wav` | `7010b148c29ab4a363d928d62c467c16b2aab41be78943b061ba7695a6cae1b7` |
| `fixtures/audio/instrumental-piano-phrase.wav` | `2718168674a45202790d0e7ccb67e9c86d91d4de54b45ff2853779d7cedbeef8` |
| `fixtures/audio/complex-mixed-song.wav` | `18158e464a553ad6389f535b96a80902153c7c96d7f2d9710069111298dbddaf` |
| `fixtures/layouts/layout-shape-a-arch-megatree.xml` | `336608e660526a3ab81cc7dce0c9fdcf2c790b496fe75f1b7f3ef62b55eddd12` |
| `fixtures/layouts/layout-shape-b-matrix-stars.xml` | `b227386799700a734e05ad9a398fa01e54ed21c8a61a009693aa05ce552a11f1` |

(Per-entry hashes also live inside the manifest itself, Section 4/Section 3 above — this table is
the independent, directly-computed cross-check, not a copy-paste of the manifest's own claims.)

## 9. Architecture deviations

- **None from the phase spec's architecture.** Continues the existing `XlightsAI.Studio.Reference`
  project (new `Fixtures` sub-namespace) rather than a new class library, for the same reason
  Step 002 gave: these are still "Foundation and Reference Baselines" concerns, not yet a concrete
  enough boundary to warrant a split (per the spec's own guidance, "do not fabricate generic
  interfaces without a concrete boundary").
- **One addition beyond the spec's literal text, disclosed:** the spec's required acceptance says
  "record ... a repeatable invocation" — interpreted here as *a real, runnable tool*
  (`tools/FixtureCorpusGenerator`), not merely a prose description of one, since a prose-only
  "invocation" cannot itself be proven repeatable. This is a new top-level `tools/` solution folder,
  sibling to `src/`/`tests/`, holding build/dev-time tooling rather than shipped application code.
- **One minor, disclosed deviation from the task briefing**, continuing Step 002's precedent:
  evidence filename `STEP-03.md` vs. the phase spec's own `STEP-003.md` convention (Section header
  note above).

## 10. Unresolved issues / risks carried forward

1. **Evidence-filename convention inconsistency** (still unresolved from Step 002, §13 item 4) —
   `STEP-001.md` vs. `STEP-02.md`/`STEP-03.md`. Recommend settling on `STEP-0NN.md` before Step 004
   and renaming the earlier files for consistency, as a small cleanup commit.
2. **Audio fixtures are not manually auditioned** (Section 6) — correctness is verified structurally
   (counts, durations, sample rate, WAV header) but no human listened to confirm the synthesized
   tones sound as intended. Low risk given these are placeholder/test-harness audio, not
   user-facing content, but flagged for transparency.
3. **Layout XML fixture "shape" is illustrative, not schema-verified** — the two layout files are
   written in a plausible `xlights_rgbeffects.xml`-like shape based on the manual/glossary
   references in the phase spec, but have not been round-tripped through a real xLights install
   (none is available in this environment — Section 6) or validated against the real upstream
   `layout/` fixture noted in Step 001's component inventory. Whichever future phase first builds
   the real layout importer (Phase 02 per the phase index) should verify these shapes against an
   actual xLights-exported file before relying on them for parser development, not just this
   hand-authored approximation.
4. Git Bash remains non-functional in this environment (carried forward from Steps 001/002); all
   commands in this evidence were run via PowerShell.

## 11. Gate checklist (this step only)

- [x] Baseline fixture corpus created: synthetic notes, drum pulse click track, isolated
      instrumental phrase, complex mixed-source song, and two structurally distinct layout XML
      shapes (`fixtures/`).
- [x] Input hashes, permitted usage, reference versions and expected outputs recorded for every
      fixture (`fixtures/FIXTURE-CORPUS-MANIFEST.json`).
- [x] Repeatable invocation demonstrated as an actual re-runnable command producing byte-identical
      output (`tools/FixtureCorpusGenerator`, Section 3), not just claimed in prose.
- [x] Deterministic fixture + automated test asserting persisted domain state and public output
      (`Step003_Acceptance_Passes`).
- [x] Negative-path assertions included (schema/content rejection, missing-file/malformed-JSON,
      missing/tampered fixture file drift detection) — six distinct negative tests, not one generic.
- [x] "Invalid model/format/version must fail safely without rewriting source files" proven by a
      dedicated hash-before/hash-after test across every known failure mode.
- [x] `dotnet build` and `dotnet test` executed and recorded, including Steps 001/002's unmodified
      regression suite (baseline-first, per instructions).
- [x] No original/show files touched; nothing pre-existing was at risk.
- [ ] Not yet done, out of this step's scope: real xLights round-trip validation of the layout
      shapes (no xLights install available — Section 6); actual layout import/parsing (Phase 02).

**Step 003 status: PASS.** Proceed to Step 004 only after this is reviewed.
