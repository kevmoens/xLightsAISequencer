# PHASE 06A — Step 044E Evidence

**Step:** "Re-run the quality/hardware-benchmark mechanism against the real model's real output, as a
disclosed proxy"
**Spec:** `docs/phase-specs/PHASE-06A.md` §6, Step 044E — fifth numbered step of this addendum
(044A-044F), inserted between Phase 06's Step 044 and Phase 07's Step 045
**Date:** 2026-10-14

## 1. Pre-implementation baseline inspection

- Read `docs/phase-specs/PHASE-06A.md` in full before writing any code, in particular §3's
  `MixReconstructionSelfConsistencyReport`/`IMixReconstructionSelfConsistencyEvaluator` proposed signatures
  and §6 Step 044E's own required acceptance ("A `MixReconstructionSelfConsistencyReport` is produced and
  persisted, recording a real, measured `SeparationQualityMetric` (reconstructed-mix-vs-original) and a
  real, measured `HardwareProfile` for this specific mechanism on this specific machine"), implementation
  guidance (reuse `SeparationQualityMetricCalculator`/`SeparationQualityThresholds`/`HardwareProfile`
  unmodified; do **not** touch `SeparationQualityEvaluator`/`QualityClip`), and failure/edge case ("Reject
  fabricating any claim of 'matches reference/professional separation quality' anywhere in this report's
  own `Decision` text ... A test must assert the report's own `Decision` text does not claim quality
  parity, matching Step 044's own implementation guidance verbatim ('reject parity claims unsupported by
  results')").
- Read `implementation-evidence/PHASE-06A/STEP-044D.md` in full: confirmed Steps 044A-044D are all **PASS**,
  and in particular that Step 044D's real end-to-end run (§5/§7 of that evidence file) already proves a
  real stereo audio sample can be separated into two real stem WAV files via the real downloaded
  `sherpa-onnx-spleeter-2stems` model — this step is **not** BLOCKED on "no real model available"; it runs
  against the exact real model Step 044D proved out.
- Inspected every relevant existing file in `src/XlightsAI.Studio.Stems/` before writing anything:
  `SeparationQualityMetricCalculator.cs` (pure, generic `Compute(clipId, kind, candidate, reference,
  thresholds)` — confirmed it makes no assumption that `candidate`/`reference` are a genuine stem vs. a
  known-correct reference; any two equal-length float arrays work, exactly as this step's own guidance
  requires), `SeparationQualityMetric.cs`, `SeparationQualityThresholds.cs`, `HardwareProfile.cs` (confirmed
  all four are reused completely unmodified — no edits were made to any of them), `SeparationQualityEvaluator.cs`
  (read in full to confirm this step must **not** touch it or `QualityClip.cs` — reused only as "the same
  `HardwareProfile` measurement pattern", per the spec's own words, not as an actual dependency),
  `SeparationQualityReport.cs`/`JsonSeparationQualityReportStore.cs`/`SeparationQualityReportCorruptException.cs`
  (the existing persisted-report/store/corrupt-exception pattern this step's own new types mirror),
  `ISpectrogramStemSeparator.cs`, `SpectrogramOnnxStemSeparator.cs`, `SpectrogramModelBundle.cs`,
  `SherpaOnnxSpleeterModelAsset.cs`, `StemManifest.cs`, `StemAsset.cs`, `StemKind.cs` (no "Mix" member
  exists — a disclosed decision below), `IAudioDecoder.cs`/`WavAudioDecoder.cs` (the existing mono-downmix
  decode pathway `SeparationQualityEvaluator.DecodeFullMonoAsync` already established — reused, not
  reinvented, for decoding both the produced stems and the original mix).
- Confirmed via `Grep`/`Glob` that no file anywhere in the repository already declares
  `MixReconstructionSelfConsistencyReport`, `IMixReconstructionSelfConsistencyEvaluator`, or a
  `Step044E*Tests` file.
- Baseline regression before any Step 044E file existed (`dotnet build XlightsAI.Studio.slnx --nologo` then
  `dotnet test XlightsAI.Studio.slnx --nologo`, both run fresh in this session before any new code):
  **Build succeeded, 0 Warnings, 0 Errors.** Full solution: **680/680 passing, 0 failed** (71 Xlights + 91
  Editing + 86 Melody + 38 Audio + 25 Projects + 159 Shell + 82 Rhythm + 31 Reference + 97 Stems), matching
  Step 044D's own final recorded baseline exactly.

## 2. Scope actually implemented

### New files

- **`src/XlightsAI.Studio.Stems/MixReconstructionSelfConsistencyReport.cs`** — the exact record proposed in
  PHASE-06A.md §3, verbatim shape: `(Guid Id, long CreatedAtTicks, string ModelId, string RecipeVersion,
  SeparationQualityMetric ReconstructionMetric, HardwareProfile Hardware, string Decision)`.
- **`src/XlightsAI.Studio.Stems/IMixReconstructionSelfConsistencyEvaluator.cs`** — the exact interface
  proposed in §3, verbatim shape: `EvaluateAsync(Guid reportId, long createdAtTicks, ISpectrogramStemSeparator
  separator, SpectrogramModelBundle modelBundle, SpectrogramSeparationRecipe recipe, SongAsset originalMix,
  SeparationQualityThresholds thresholds, CancellationToken ct)`.
- **`src/XlightsAI.Studio.Stems/MixReconstructionSelfConsistencyEvaluator.cs`** — the real implementation:
  1. Runs the real `ISpectrogramStemSeparator.SeparateAsync` against `originalMix` (never mocked), timed
     with a real `Stopwatch` around the call.
  2. Decodes every real produced stem WAV file to mono via the real, unmodified `IAudioDecoder`
     (`ProbeAsync` + `DecodeMonoAsync`) — the identical decode pathway `SeparationQualityEvaluator` already
     uses for its own reference-vs-candidate comparisons.
  3. Sums the decoded mono stem sample arrays sample-by-sample into one reconstructed-mix array.
  4. Decodes the real original mix to mono the same way.
  5. Calls the real, unmodified `SeparationQualityMetricCalculator.Compute` with the reconstructed mix as
     `candidate` and the real original mix as `reference` — producing a real `SeparationQualityMetric`.
  6. Measures a real `HardwareProfile` via the same `RuntimeInformation`/`Environment`/`GC.GetGCMemoryInfo`/
     `Process.GetCurrentProcess().PeakWorkingSet64` fields `SeparationQualityEvaluator` already uses,
     completely unmodified in shape.
  7. Builds a `Decision` string that is required, by construction, to always include the literal phrase
     `"does NOT measure separation quality"` and the word `"self-consistency"` in both the pass and fail
     branches, and never any of a disclosed list of parity-claiming phrases (§4 below).
- **`src/XlightsAI.Studio.Stems/IMixReconstructionSelfConsistencyReportStore.cs`** and
  **`JsonMixReconstructionSelfConsistencyReportStore.cs`** — not named in the spec's own proposed C#
  signatures, but required by this step's own required-acceptance text ("is produced **and persisted**")
  and by Step 044's own identical "Saved audio/analysis artifact with timestamp provenance" user-visible
  outcome. Mirrors `ISeparationQualityReportStore`/`JsonSeparationQualityReportStore`'s own exact atomic
  write pattern (temp file + `File.Move(..., overwrite: true)`), unmodified in structure.
- **`src/XlightsAI.Studio.Stems/MixReconstructionSelfConsistencyReportCorruptException.cs`** — mirrors
  `SeparationQualityReportCorruptException`'s own exact pattern for this new report type.
- **`tests/XlightsAI.Studio.Stems.Tests/Step044EAcceptanceTests.cs`** — 2 tests (full list/results in §6).

### Modified files

**None.** No existing file in `src/XlightsAI.Studio.Stems/`, `src/XlightsAI.Studio.Audio/`, or anywhere
else in the repository was changed. In particular, `SeparationQualityMetricCalculator.cs`,
`SeparationQualityThresholds.cs`, `HardwareProfile.cs`, `SeparationQualityMetric.cs`,
`SeparationQualityEvaluator.cs`, `QualityClip.cs`, `ISpectrogramStemSeparator.cs`,
`SpectrogramOnnxStemSeparator.cs`, `SherpaOnnxSpleeterModelAsset.cs`, and every Step 038-044D file are
byte-for-byte unchanged — confirmed by this session making no `Edit` calls against any of them, only `Write`
calls creating the six new files listed above.

## 3. Architecture deviations / decisions

### Named decision 1 — `IMixReconstructionSelfConsistencyReportStore`/`JsonMixReconstructionSelfConsistencyReportStore` are additive types not named in the spec's own proposed signatures

The spec's §3 proposed signatures declare `MixReconstructionSelfConsistencyReport` and
`IMixReconstructionSelfConsistencyEvaluator` only — no store interface. But §6 Step 044E's own required
acceptance text says the report must be "produced **and persisted**", and §1's "User-visible outcome" for
this step is verbatim "Saved audio/analysis artifact with timestamp provenance plus a synchronized
waveform/timeline demonstration" — identical wording to Step 044's own outcome, which Step 044 satisfied via
`ISeparationQualityReportStore`/`JsonSeparationQualityReportStore`. This step adds the directly analogous
pair for the new report type, mirroring that exact existing pattern (atomic write, `GetPath`/`SaveAsync`/
`LoadAsync` shape, dedicated corrupt-exception type) rather than inventing a different persistence shape.
This is an additive extension consistent with the spec's own intent, not a deviation from it.

### Named decision 2 — `SeparationQualityMetric.Kind` is populated with `StemKind.Other` as a disclosed, third overload

`SeparationQualityMetric.Compute` requires a `StemKind` parameter, but this step's own measurement is over
the **whole reconstructed mix** (sum of every produced stem), never any one instrument/stem — and no "Mix"
or "WholeSignal" member exists on `StemKind`, and adding one is out of this step's own named scope (§3 of
the spec reuses `SeparationQualityMetricCalculator`/`HardwareProfile` "unmodified"; it does not propose
extending `StemKind`). `StemKind.Other` is reused as the least-wrong existing value. This is disclosed as a
**third**, narrower overload of `StemKind.Other` beyond the two the addendum has already named:
(1) `SpectrogramModelBundle`'s own "whole undifferentiated backing track" overload for a real produced
accompaniment stem (PHASE-06A.md §3, Step 044D's own `StemArchiveEntryNames` mapping), and (2) this step's
own "the entire reconstructed mix, not one instrument" overload for the self-consistency metric's `Kind`
field. `MixReconstructionSelfConsistencyEvaluator.ReconstructionClipId` (a public constant,
`"reconstructed-mix-self-consistency"`) is the field a caller must actually read to know what was measured
— documented explicitly in the evaluator's own XML doc comment, and asserted directly in
`Step044E_Acceptance_Passes_RealReconstructionSelfConsistencyReportIsProducedAndPersisted`.

### Named decision 3 — the required "Decision never claims quality parity" assertion is enforced by construction, not merely tested after the fact

Rather than writing a `Decision` string first and then hoping a test happens to catch an accidental parity
claim, `MixReconstructionSelfConsistencyEvaluator.EvaluateAsync`'s own `Decision`-building code was written
to always include the literal substring `"does NOT measure separation quality"` and the word
`"self-consistency"` in **both** the pass and fail branches — so the disclosure is structurally guaranteed
by the implementation, and the test (`AssertDecisionNeverClaimsQualityParity`, run against both the branch
actually produced by the real test run and after a real JSON round-trip) is a genuine regression guard
against someone later editing the string to drop that disclosure, not a test written to match whatever
string happened to come out.

### Named decision 4 — the negative-path test exercises a fake zero-stem separator, not the real model, to prove the evaluator's own explicit guard

The spec's own §6 failure/edge case for this step is specifically about the `Decision` text never claiming
quality parity (covered by the acceptance test itself, run against the real model). This step's own
implementation adds one more defensive guard not explicitly demanded by the spec's literal failure/edge-case
text, but required by basic correctness: if a (real or, for this one test, deliberately fake) separator ever
produced zero stems, summing zero arrays into a reconstructed mix would either throw an unhelpful
`IndexOutOfRangeException` or silently report a vacuous "perfect" self-consistency score against an all-zero
array. `EvaluateAsync` explicitly checks `manifest.Stems.Count == 0` and throws the existing, unmodified
`SeparationQualityInvalidInputException` before any decode/mask work begins. This is tested with a small,
local `ZeroStemSeparator` fake (not SharpCompress, not real ONNX inference) specifically because it is
testing the evaluator's own input-validation logic in isolation, independent of whether the real model
happens to ever produce zero stems in practice (it doesn't — Step 044D's own real run always produces
exactly 2) — the real acceptance test elsewhere in this same file exercises the real model end-to-end.

## 4. Real end-to-end demonstration (this step's own required acceptance)

`Step044E_Acceptance_Passes_RealReconstructionSelfConsistencyReportIsProducedAndPersisted`, run via
`dotnet test ... --filter "FullyQualifiedName~RealReconstructionSelfConsistencyReportIsProducedAndPersisted"`,
genuinely:

1. Reuses the real, already-downloaded, hash-verified `sherpa-onnx-spleeter-2stems` archive from the same
   shared cache directory Step 044D's own tests established (`%TEMP%\xlai-phase06a-step044d-model-asset-cache`)
   — no re-download occurred in this session's run (the cache from this same session's earlier Step 044D/
   044E-negative-path runs was already present and already hash-verified).
2. Builds a real 3.0-second, 44,100 Hz stereo WAV fixture (left = 220/330 Hz harmonic phrase, right =
   440/550 Hz harmonic phrase — the identical deterministic, non-copyrighted fixture shape Step 044D's own
   test uses), probed via the real, unmodified `WavAudioDecoder`.
3. Runs the **real** `SpectrogramOnnxStemSeparator.SeparateAsync` (Step 044C, unmodified) through the real
   `MixReconstructionSelfConsistencyEvaluator.EvaluateAsync` — real stereo decode, real STFT, two real
   `Microsoft.ML.OnnxRuntime.InferenceSession` CPU inferences (one per real 39 MB ONNX file extracted from
   the real archive), real soft mask, real ISTFT, real stereo WAV write — producing 2 real stem files
   (`Vocals`, `Other`).
4. Decodes both real produced stem WAV files and the real original mix to mono via the real, unmodified
   `WavAudioDecoder`, sums the two real decoded stem arrays sample-by-sample, and calls the real, unmodified
   `SeparationQualityMetricCalculator.Compute` against the real decoded original mix.
5. Asserts the resulting `SeparationQualityMetric` is finite (no `NaN`/`Infinity`) and that its correlation
   coefficient lies in the mathematically valid `[-1, 1]` range — a real, measured value, never a fabricated
   "passing" constant.
6. Asserts the resulting `HardwareProfile`'s every field is a real, non-trivial, machine-specific
   measurement (`OsDescription` non-empty, `ProcessorCount > 0`, `TotalAvailableMemoryBytes > 0`,
   `PeakWorkingSetBytes > 0`, `MeasuredWallClockSecondsPerSongMinute > 0`) — real, not estimated/
   upstream-reported figures (the exact distinction `HardwareProfile`'s own doc comment names relative to
   Step 038's superseded `EstimatedCpuRamMegabytes`/`EstimatedCpuSecondsPerSongMinute` fields).
7. Asserts the report's own `Decision` text never contains any of seven disclosed parity-claiming phrases
   (`"matches reference separation quality"`, `"matches professional separation quality"`,
   `"reference-quality separation"`, `"professional-quality separation"`, `"proven separation quality"`,
   `"separation quality is proven"`, `"quality parity"`), and that it **does** positively disclose
   `"self-consistency"` and the literal phrase `"does NOT measure separation quality"` — the step's own
   required failure/edge-case assertion, verbatim.
8. Persists the real report via the real, new `JsonMixReconstructionSelfConsistencyReportStore` (atomic
   temp-file-then-move write), reloads it from the real written JSON file, and re-asserts every field
   (`Id`, `ModelId`, the metric's own `SignalToDistortionRatioDb`, and the full `Decision` text including
   the same parity-claim guard) survives a real JSON round-trip unchanged — proving this is a genuinely
   persisted artifact, not merely an in-memory object asserted once.

This is the phase gate's own "real UI or xLights demonstration appropriate to the phase" for this specific
step — Step 044E's own user-visible outcome is explicitly "Saved audio/analysis artifact with timestamp
provenance plus a synchronized waveform/timeline demonstration," satisfied here by the real, persisted,
round-tripped `MixReconstructionSelfConsistencyReport` JSON file this test writes and reloads (no Avalonia
UI exists yet to wire a visual waveform view into — consistent with every prior Phase 06/06A step's own
identical "no UI wiring yet, real persisted output stands in for it" precedent, see Step 044D's own evidence
§5).

## 5. Commands actually executed, with real observed output

All commands run via the PowerShell tool in this session (the sandboxed POSIX Bash tool again returned a
bare `exit code 1` with no output for plain commands — the same pre-existing environment limitation
recorded in every prior addendum step's own evidence).

```powershell
# Baseline, before any Step 044E file existed:
dotnet build XlightsAI.Studio.slnx --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).
dotnet test XlightsAI.Studio.slnx --nologo
  # 680/680 passing, 0 failed (71 Xlights, 91 Editing, 86 Melody, 38 Audio, 25 Projects, 159 Shell,
  #  82 Rhythm, 31 Reference, 97 Stems)

# Implementation, incremental build verification:
dotnet build src\XlightsAI.Studio.Stems\XlightsAI.Studio.Stems.csproj --nologo
  # Build succeeded. 0 Error(s).
dotnet build tests\XlightsAI.Studio.Stems.Tests\XlightsAI.Studio.Stems.Tests.csproj --nologo
  # Build succeeded. 0 Error(s).

# Targeted Step 044E test runs:
dotnet test tests\XlightsAI.Studio.Stems.Tests\... --filter "FullyQualifiedName~Step044E&FullyQualifiedName!~RealReconstructionSelfConsistencyReportIsProducedAndPersisted"
  # Passed! - Failed: 0, Passed: 1, Skipped: 0, Total: 1, Duration: 202 ms (negative-path test, no model/network needed)

dotnet test tests\XlightsAI.Studio.Stems.Tests\... --filter "FullyQualifiedName~RealReconstructionSelfConsistencyReportIsProducedAndPersisted"
  # Passed! - Failed: 0, Passed: 1, Skipped: 0, Total: 1, Duration: 39 s (real separation + real metric +
  #  real hardware profile + real JSON persist/reload, reusing the already-downloaded/verified model cache)

dotnet test tests\XlightsAI.Studio.Stems.Tests\XlightsAI.Studio.Stems.Tests.csproj --nologo
  # Passed! - Failed: 0, Passed: 99, Skipped: 0, Total: 99, Duration: 1 m 47 s
  #  (97 pre-existing Step 038-044D + 2 new Step 044E tests)

dotnet build XlightsAI.Studio.slnx --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).

dotnet test XlightsAI.Studio.slnx --nologo
  # Passed! 71 - Xlights.Tests
  # Passed! 91 - Editing.Tests
  # Passed! 86 - Melody.Tests
  # Passed! 38 - Audio.Tests
  # Passed! 25 - Projects.Tests
  # Passed! 159 - Shell.Tests
  # Passed! 82 - Rhythm.Tests
  # Passed! 31 - Reference.Tests
  # Passed! 99 - Stems.Tests
  # TOTAL: 682/682 passing, 0 failed (680 pre-existing + 2 new Step 044E tests)
```

### Targeted Step 044E test results (final run, full list)

```
Passed Step044EAcceptanceTests.Step044E_Acceptance_Passes_RealReconstructionSelfConsistencyReportIsProducedAndPersisted [39 s]
Passed Step044EAcceptanceTests.EvaluateAsync_SeparatorProducesZeroStems_ThrowsSeparationQualityInvalidInputException_NegativePath [< 1 s]
```

## 6. Persisted-output inspection

Real, directly observed, non-fabricated results from this step's own real end-to-end run:

- A real 3.0-second stereo WAV fixture was separated via the real downloaded `sherpa-onnx-spleeter-2stems`
  model into 2 real stem WAV files (`Vocals`, `Other`), exactly as Step 044D's own run already proved.
- Both real stems were decoded to mono via the real, unmodified `WavAudioDecoder` and summed into one
  real reconstructed-mix array; the real original mix was decoded the same way.
- The real, measured `SeparationQualityMetric` (`ClipId = "reconstructed-mix-self-consistency"`,
  `Kind = StemKind.Other`, per Named decision 2) was finite and had a correlation coefficient within the
  mathematically valid `[-1, 1]` bound — a real self-consistency measurement for this specific model/recipe
  on this specific fixture, not a hand-picked or fabricated value. (This addendum's own real, permanent
  frequency-bin-truncation quality ceiling — bins 1024-2048 of 2049 are always zero in the reconstructed
  stems, per Steps 044B/044C/044D — is an inherent, disclosed property of the real model output this
  measurement is taken against; it is not separately re-asserted numerically in this step's own test, since
  Step 044C's own synthetic masking test already proves that property of the math directly, consistent with
  Step 044D's own identical disclosed scoping decision.)
- The real, measured `HardwareProfile` for this specific mechanism (reconstructed-mix evaluation, not the
  raw separation call alone) on this specific machine: a real, non-empty `OsDescription`, a real positive
  `ProcessorCount`, a real positive `TotalAvailableMemoryBytes`, a real positive `PeakWorkingSetBytes`, and a
  real positive `MeasuredWallClockSecondsPerSongMinute` — all sampled directly via
  `RuntimeInformation`/`Environment`/`GC.GetGCMemoryInfo()`/`Process.GetCurrentProcess().PeakWorkingSet64`
  around the real separation call, identical measurement technique to `SeparationQualityEvaluator`'s own
  established pattern.
- The report's own `Decision` text, inspected directly by the test, contains the literal phrase "does NOT
  measure separation quality" and the word "self-consistency", and contains none of the seven disclosed
  parity-claiming phrases checked — both before and after a real JSON file round-trip through
  `JsonMixReconstructionSelfConsistencyReportStore`.
- The persisted JSON file exists on disk at `{runDirectory}/reports/mix-reconstruction-self-consistency-{id:N}.json`
  and was successfully reloaded and compared field-by-field against the original in-memory report.

## 7. Fixtures / input hashes

| Artifact | Value |
|---|---|
| Real sherpa-onnx-spleeter-2stems archive (reused, not re-downloaded this run) | SHA-256 `e26401d9c1801f43c0229731d78d32c2e80085e3aceedeb25f29d2de5fa68ca2`, 74,682,545 bytes — identical to Step 044D's own pinned value, unchanged |
| Real stereo WAV test fixture | Synthesized in-memory per test run via `DeterministicSyntheticAudioGenerator.BuildHarmonicPhrase`/`EncodeStereoWav` (220/330 Hz left, 440/550 Hz right, 1.5 s each, 44,100 Hz) — deterministic, no external file, no copyrighted recording, identical fixture shape to Step 044D's own test |
| Real `MixReconstructionSelfConsistencyReport` JSON output | Written to a per-test-run temp directory (`%TEMP%\xlai-step044e-{guid}\reports\mix-reconstruction-self-consistency-{id}.json`), deleted on test `Dispose()` -- not a stable golden file, since the exact SDR/correlation values are mechanism-measured, not fixed constants |

## 8. Unresolved issues / known gaps

- **This is explicitly not a separation-quality measurement**, by this step's own named design (§6 of the
  spec, carried into every doc comment and the `Decision` text itself) — the measured SDR/correlation
  describe only how closely summing the real produced stems reproduces the real original input mix, never
  whether the vocals/accompaniment split itself is musically correct. No professionally-mastered reference
  multitrack exists to measure that (the same honest constraint Step 044 itself already named, and the
  reason this step exists instead of re-running `SeparationQualityEvaluator`/`QualityClip` directly).
- **`StemKind.Other` is reused as a disclosed, third overload** for the whole-mix self-consistency metric's
  `Kind` field (Named decision 2) — flagged here for whoever next touches `StemKind` or any code that
  pattern-matches on `SeparationQualityMetric.Kind`, since `ReconstructionClipId` (not `Kind`) is the field
  that actually identifies this measurement.
- **No UI wiring.** Consistent with every prior Phase 06/06A step's own identical precedent — this step's
  real persisted JSON report (§4/§6) stands in for an Avalonia/xLights visual demonstration.
- **The real acceptance test takes ~39 s** (one real CPU ONNX separation pass plus mono-decode/sum/metric
  overhead) when the model archive is already cached from a prior Step 044D/044E run on the same machine;
  the very first run of either step's own tests on a fresh machine additionally pays the real one-time
  ~74.7 MB HTTPS download cost (disclosed in Step 044D's own evidence, unchanged here).
- **`IMixReconstructionSelfConsistencyReportStore`/`JsonMixReconstructionSelfConsistencyReportStore` are
  additive types beyond the spec's own literal §3 proposed signatures** (Named decision 1) — disclosed
  explicitly rather than silently added, since required by this step's own "produced and persisted"
  acceptance text and user-visible-outcome wording, not by the signature block itself.

## 9. PASS / BLOCKED

**Status: PASS**

- "A `MixReconstructionSelfConsistencyReport` is produced and persisted, recording a real, measured
  `SeparationQualityMetric` (reconstructed-mix-vs-original) and a real, measured `HardwareProfile` for this
  specific mechanism on this specific machine" (required acceptance, verbatim) — proven by
  `Step044E_Acceptance_Passes_RealReconstructionSelfConsistencyReportIsProducedAndPersisted`: a real
  separation run against the real downloaded `sherpa-onnx-spleeter-2stems` model, a real reconstructed-mix
  self-consistency metric computed via the unmodified `SeparationQualityMetricCalculator.Compute`, a real
  measured `HardwareProfile`, and a real, round-tripped JSON persistence via the new
  `JsonMixReconstructionSelfConsistencyReportStore`.
- "Reject fabricating any claim of 'matches reference/professional separation quality' anywhere in this
  report's own `Decision` text or field names ... A test must assert the report's own `Decision` text does
  not claim quality parity" (failure/edge case, verbatim) — proven by
  `AssertDecisionNeverClaimsQualityParity`, run against both the real test run's own produced `Decision`
  text and the same text after a real JSON round-trip, checking for the absence of seven disclosed
  parity-claiming phrases and the required presence of an explicit "does NOT measure separation quality"
  disclosure.
- Full targeted suite: **2/2 passing**, 0 failed. Full `Stems.Tests` assembly: **99/99 passing** (97
  pre-existing Step 038-044D tests + 2 new Step 044E tests), 0 failed — every prior Phase 06/06A Stems test
  still passes unmodified. Full solution regression: **682/682 passing, 0 failed** (680 pre-existing + 2
  new), including every pre-existing Phase 06/07 test.
- No carried-forward blocker specific to this step's own scope. This step's own additive-persistence-type
  and `StemKind.Other`-reuse decisions are named architecture decisions (§3), not deviations requiring a
  waiver. Step 044D's real, downloaded, hash-verified model remained available throughout this step's own
  session, so this step is a genuine real-model re-measurement, never a synthetic-fixture substitute
  presented as real.
- Phase 06's own phase-level `BLOCKED` gate status (Step 038's feasibility verdict) is **not** resolved by
  this step alone — Step 044F (not this step) is explicitly where that status is formally revisited, per
  the addendum's own step-gating rule.

## 10. Architecture deviations to flag for review

- **`IMixReconstructionSelfConsistencyReportStore`/`JsonMixReconstructionSelfConsistencyReportStore`** are
  additive types not literally named in PHASE-06A.md §3's proposed signatures (Named decision 1) — disclosed
  as required by this step's own "produced and persisted" acceptance text, not a deviation from the spec's
  intent.
- **`SeparationQualityMetric.Kind` is populated with `StemKind.Other`** for this step's whole-mix
  self-consistency metric (Named decision 2) — a disclosed, third overload of that enum member; flagged for
  whoever next extends `StemKind` or reviews Step 044F's own `ModelFeasibilityCandidate` revision.
- Do not proceed to Step 044F until this evidence is reviewed, per the addendum's own step-gating rule
  (§6: "Implementation / tests / observable demo / evidence all complete before next numbered step").
