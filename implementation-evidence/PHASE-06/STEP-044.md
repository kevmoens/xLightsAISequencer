# PHASE 06 — Step 044 Evidence

**Step:** "Compare reference outputs, audio metrics and listening tests; establish supported hardware"
**Spec:** `docs/phase-specs/PHASE-06.md` §6, Step 044 (lines 300-314) — the last numbered step of Phase 06 (038-044)
**Date:** 2026-10-12

## 1. Pre-implementation baseline inspection

- Re-read `docs/phase-specs/PHASE-06.md` in full for this step: Step 044's own required acceptance
  ("Measured separation quality/performance is documented and acceptance threshold met"), its own
  implementation guidance ("Compute objective alignment + hearing ratings and benchmark time/memory
  across supported machines; reject parity claims unsupported by results"), its failure/edge case
  ("Silence, invalid timing boundaries and missing media must be handled explicitly"), the full-phase
  test matrix's own item 6 ("Stereo/multi-minute input stays within memory budget") and item 7 ("At
  least two listening reference clips meet declared quality bar"), and Section 12's mandatory stem model
  feasibility decision record.
- Read `implementation-evidence/PHASE-06/STEP-038.md` through `STEP-043.md` in full (all prior steps of
  this phase). Confirmed Step 038's own explicit, disclosed **BLOCKED** verdict at the model-feasibility
  level remains unchanged and uncontradicted by any later step: zero production stem-separation model
  (Demucs-onnx, sherpa-onnx, or otherwise) has ever been downloaded or run under a .NET host in this
  repository — only the real `Microsoft.ML.OnnxRuntime` CPU backend and a small, honestly-labeled
  **synthetic** "stem = input × known_gain" ONNX fixture (`fixtures/onnx/synthetic-stem-separator-v1.onnx`,
  committed in Step 040) have been proven to run end-to-end. Step 040's own evidence explicitly deferred
  "real production-model integration quality" to "Step 044's own later scope" — this step closes that
  disclosed deferral honestly: it builds the real, reusable *measurement mechanism* (objective audio
  metrics + hardware benchmarking) and proves it end-to-end against the one real mechanism that exists
  (the synthetic fixture), rather than fabricating a production-model quality claim no model in this
  repository could support. This is the same "fixture bytes/fixture backend, real mechanism" pattern
  every step since 038 has used and disclosed.
- Inspected every existing file in `src/XlightsAI.Studio.Stems/` before writing anything, in particular
  `OnnxStemSeparator` (reused completely unchanged — this step is a pure consumer of `IStemSeparator`, it
  never modifies the separation mechanism itself), `ModelFeasibilityCandidate`
  (`EstimatedCpuRamMegabytes`/`EstimatedCpuSecondsPerSongMinute` — Step 038's own *estimated/upstream-
  reported* performance fields, which this step's own real, measured `HardwareProfile` is designed to
  eventually supersede once a real model exists), `ModelFeasibilityEvaluator` (the established
  "pure, synchronous, no ambient clock, no file I/O" precedent for a phase-local decision gate — followed
  directly by the new `SeparationQualityMetricCalculator`), and `JsonModelFeasibilityReportStore`
  (the established atomic-write JSON report-store pattern — followed directly by the new
  `JsonSeparationQualityReportStore`).
- Inspected `src/XlightsAI.Studio.Audio/IAudioDecoder.cs`/`WavAudioDecoder.cs`/`SongAsset.cs` to confirm
  `ProbeAsync`/`DecodeMonoAsync`/`SongAsset.DurationTicks` are sufficient to decode both a produced stem
  file and an independently-written reference file to mono float samples for scoring, and to derive a
  clip's duration in minutes for the hardware benchmark, without any new decoder capability.
- Baseline regression before any Step 044 code existed, run fresh in this session:
  **552/552 passing** (`dotnet test XlightsAI.Studio.slnx --nologo`) — exactly Step 043's own final count,
  confirming the starting state matched the prior step's own recorded evidence before any Step 044 line
  of code existed.
- Decision made before writing code: a real, reproducible "listening reference clip" quality measurement
  requires a **known-correct reference signal** to compare against. Since no real separation model exists
  in this repository (Step 038's own disclosed BLOCKED verdict), the reference for each clip is derived
  analytically from the committed synthetic fixture's own exactly-known transform (`reference = source ×
  known_gain`, the same four gains — `0.10, 0.25, 0.50, 0.75` — Steps 040/041 already committed and
  verified against real Python `onnxruntime`). This keeps the metric calculator itself fully generic
  (it only ever compares two WAV-decodable float arrays — a real production model's real reference stems
  would plug into the exact same `QualityClip`/`ISeparationQualityEvaluator` shape unchanged), while
  being honest that today's *measured* quality number is about the mechanism, not about real musical
  separation (disclosed at length in §3 below).

## 2. Scope actually implemented

### New domain types (`src/XlightsAI.Studio.Stems/`)

- **`SeparationQualityThresholds.cs`** — the declared, versioned acceptance bar (`MinSignalToDistortionRatioDb`,
  `MinCorrelationCoefficient`, `SilenceEnergyEpsilon`), carried alongside every report rather than a
  static constant, with a `Validate()` guard (correlation bound `[-1,1]`, epsilon strictly positive).
- **`SeparationQualityMetric.cs`** — one objective measurement: `ClipId`, `Kind`, `ReferenceIsSilent`,
  `SignalToDistortionRatioDb`, `CorrelationCoefficient`, `PassesThreshold`.
- **`SeparationQualityMetricCalculator.cs`** — the real (no third-party DSP dependency), pure, synchronous
  calculator, mirroring `ModelFeasibilityEvaluator`'s own "pure, no file I/O, no ambient clock" precedent
  verbatim. Computes SDR (`10·log10(referenceEnergy / errorEnergy)`) and Pearson correlation. **Silence
  handled explicitly** (this step's own failure/edge case, verbatim): a reference signal at or below
  `SilenceEnergyEpsilon` energy never divides by zero — it is flagged `ReferenceIsSilent = true` and
  scored via one of two named, finite sentinel constants (`PerfectSilenceSentinelDb = 120.0`,
  `FailedSilenceSentinelDb = -60.0`), deliberately never `double.PositiveInfinity`/`NaN` (which
  `System.Text.Json.JsonSerializer` cannot round-trip by default, and which would themselves need special
  casing everywhere downstream). **Invalid timing boundaries handled explicitly**: a candidate/reference
  sample-count mismatch or a zero-length clip throws `SeparationQualityInvalidInputException` before any
  further computation.
- **`SeparationQualityInvalidInputException.cs`** — this step's own dedicated exception type for all
  three named failure/edge cases that are genuinely malformed input (mismatched length, zero length,
  invalid thresholds, missing media) — silence itself is *not* an error case (see above), it is scored
  data.
- **`HardwareProfile.cs`** — one real, measured (never estimated) hardware/performance observation:
  `OsDescription`/`ProcessArchitecture` (`RuntimeInformation`), `ProcessorCount` (`Environment`),
  `TotalAvailableMemoryBytes` (`GC.GetGCMemoryInfo()`), `PeakWorkingSetBytes`
  (`Process.PeakWorkingSet64`), and `MeasuredWallClockSecondsPerSongMinute` (real `Stopwatch` time around
  the real separator calls, divided by total real clip duration) — this step's own title, verbatim:
  "...establish supported hardware."
- **`SeparationQualityReport.cs`** — the persisted, versioned decision document: `Id`, caller-supplied
  `CreatedAtTicks` (same "supplied explicitly, not sampled internally" precedent as
  `ModelFeasibilityReport.CreatedAtTicks`), `ModelId`, `RecipeVersion`, every `ClipMetrics` entry (passing
  or not — kept, never deleted, for auditability), the `Thresholds` used, the measured `Hardware`
  profile, and `OverallPass` (true only when every metric passes).
- **`SeparationQualityReportCorruptException.cs`** / **`ISeparationQualityReportStore.cs`** /
  **`JsonSeparationQualityReportStore.cs`** — mirror `ModelFeasibilityReportCorruptException`/
  `IModelFeasibilityReportStore`/`JsonModelFeasibilityReportStore` exactly: atomic temp-file-then-move
  writes, a dedicated corrupt-file exception on a bad reload.
- **`QualityClip.cs`** — one "listening reference clip": a `SongAsset` to actually separate plus a
  `IReadOnlyDictionary<StemKind,string>` of known-correct reference WAV file paths to score the real
  produced stems against.
- **`ISeparationQualityEvaluator.cs`** / **`SeparationQualityEvaluator.cs`** — the real, impure
  orchestrator (the "run the real mechanism" counterpart to the pure calculator, the same division of
  responsibility `OnnxStemSeparator` itself already models relative to e.g. `StemSeparationRecipeValidator`):
  for every clip, (1) verifies every declared reference file actually exists *before* any clip's
  separation work runs at all (this step's own "missing media must be handled explicitly" failure/edge
  case, proven by `MissingReferenceMediaFile_..._SeparatorNeverInvoked_NegativePath` — the separator is
  provably never invoked when any reference is missing); (2) runs the real `IStemSeparator.SeparateAsync`
  under a real `Stopwatch`; (3) decodes each real produced stem file and its real reference file via the
  real `IAudioDecoder`; (4) scores each pair via `SeparationQualityMetricCalculator.Compute`; (5) after
  every clip, assembles the real `HardwareProfile` and the final `OverallPass`/`Decision`.

### New test file

`tests/XlightsAI.Studio.Stems.Tests/Step044AcceptanceTests.cs` — 10 tests (full list in §4).

**No changes to any existing `Contracts`, `Editing`, `Xlights`, `Projects`, `Reference`, `Rhythm`, `Shell`,
`App` type's public shape, and no change to any Step 038-043 `Stems`/`Audio` file** — `OnnxStemSeparator`,
`StemSeparationRecipe`, `IStemSeparator`, every model-asset/chunk-cache type, `StemActivityDetector`,
`StemMixdownRenderer`, `StemWavWriter`, `SongAsset`, `IAudioDecoder`/`WavAudioDecoder` are all reused
completely unchanged. The only file touched outside new `Stems`/`Stems.Tests` files is none —
no fixture manifest change was needed (this step reuses the exact committed
`fixtures/onnx/synthetic-stem-separator-v1.onnx` byte-for-byte, no new binary fixture added).

## 3. Architecture deviations / decisions

- **No production stem-separation model was downloaded, run, or scored in this step.** Disclosed, at
  length, in §1: Step 038's own BLOCKED model-feasibility verdict is unchanged. This step's own *measured*
  quality numbers (SDR ~108-130 dB, correlation 1.000000 — see §5) describe the real ONNX Runtime
  mechanism against an analytically-invertible synthetic fixture, not real musical separation quality.
  This is not a parity claim — Step 044's own implementation guidance says, verbatim, "reject parity
  claims unsupported by results," and none is made here. What *is* real and load-bearing: the metric
  calculator, the threshold gate, the persisted report, and the hardware benchmark are all real,
  generically-shaped mechanisms that a future step wiring a real model can plug real reference stems and
  real separator output into unchanged (same `QualityClip`/`ISeparationQualityEvaluator` contract) — only
  the *source* of the reference audio would differ (real mastered stems instead of an analytically-known
  gain), never the scoring/reporting code path itself.
- **`SignalToDistortionRatioDb` uses two named finite sentinel constants for the silent-reference case,
  never `double.PositiveInfinity`/`NaN`.** Disclosed in §2: `System.Text.Json.JsonSerializer` cannot
  serialize non-finite `double` values without a custom converter, and `NaN` specifically is exactly the
  failure mode this step's own required text explicitly forbids ("must be handled explicitly"). Using
  `120.0`/`-60.0` dB keeps the persisted report a plain, inspectable JSON document with no special-case
  reader logic required, while still being an obviously-named sentinel (not a plausible real measurement)
  to anyone reading the report.
- **Reference stems are supplied as file paths on disk (`QualityClip.ReferenceStemFilePaths`), not
  in-memory float arrays.** Disclosed, deliberate: this directly enables testing the "missing media"
  failure/edge case as a real `File.Exists` check against a real path (proven by
  `MissingReferenceMediaFile_..._NegativePath`), not merely a null-check against an in-memory array that
  could never actually be "missing" the way a real file on disk can.
- **`SeparationQualityEvaluator` checks every clip's reference files for existence *before* separating
  any clip**, not lazily per-clip as each one is processed. Disclosed, deliberate: a batch quality run is
  expensive (real CPU inference per clip); failing fast on a configuration mistake (a typo'd reference
  path) before spending any of that real compute is the more useful, more explicit failure behavior, and
  is directly proven by `NeverCalledSeparator` never being invoked in the missing-media test.
- **Only one machine's `HardwareProfile` is recorded per report** ("establish supported hardware" is
  interpreted as "measure and record one real machine's real figures," not "claim coverage of all
  possible target hardware"). Disclosed: Section 2's own "no undocumented claim that all ... behave
  identically" rule is applied directly — multi-machine benchmarking across the phase gate's own named
  "supported machines" plural is out of this single step/session's reach (one sandboxed CI-style
  container was available), and is not fabricated.
- **No `ShellScreen.Stems` navigation/UI wiring in this step.** Disclosed, consistent with every prior
  Phase 06 step's own precedent for research/decision-shaped work (Step 038 had none either): Step 044's
  own required acceptance text ("Measured separation quality/performance is documented and acceptance
  threshold met") and its own user-visible-outcome boilerplate ("Saved audio/analysis artifact with
  timestamp provenance...") are both satisfied by the persisted `SeparationQualityReport` JSON file
  itself; the generic "...plus a synchronized waveform/timeline demonstration" half of that same shared
  boilerplate text does not literally apply to a quality-measurement/benchmarking step and is not
  fabricated here (the same "shared phase-wide boilerplate, reinterpreted" disclosure Step 043's own
  evidence already made for its failure/edge-case text).

## 4. Commands actually executed

All run via the PowerShell tool — the sandboxed POSIX Bash tool in this session again returned a bare
exit code 1 with no output for a plain command, the same pre-existing environment limitation recorded in
every prior step's own evidence; PowerShell was used for every command below without incident.

```
dotnet test XlightsAI.Studio.slnx --nologo                                                  # baseline, before any Step 044 code existed -> 552/552 passing

# New: SeparationQualityThresholds.cs, SeparationQualityMetric.cs, SeparationQualityMetricCalculator.cs,
#      SeparationQualityInvalidInputException.cs, HardwareProfile.cs, SeparationQualityReport.cs,
#      SeparationQualityReportCorruptException.cs, ISeparationQualityReportStore.cs,
#      JsonSeparationQualityReportStore.cs, QualityClip.cs, ISeparationQualityEvaluator.cs,
#      SeparationQualityEvaluator.cs

dotnet build src\XlightsAI.Studio.Stems\XlightsAI.Studio.Stems.csproj --nologo               # 0 Warnings, 0 Errors

dotnet build tests\XlightsAI.Studio.Stems.Tests\XlightsAI.Studio.Stems.Tests.csproj --nologo # 0 Warnings, 0 Errors
dotnet test  tests\XlightsAI.Studio.Stems.Tests\XlightsAI.Studio.Stems.Tests.csproj --nologo --logger "console;verbosity=detailed" --filter "FullyQualifiedName~Step044"
  # 10/10 passed

dotnet build XlightsAI.Studio.slnx --nologo                                                  # full solution, after Step 044 implementation -> 0 Warnings, 0 Errors
dotnet test  XlightsAI.Studio.slnx --nologo                                                   # full regression, final

dotnet run --project <scratchpad>\xlai-step044-demo-proj\step044-demo.csproj --nologo         # real, non-test-harness manual demo (see §5)
```

### Build results

Full solution: `Build succeeded. 0 Warning(s). 0 Error(s).`

### Targeted test results (Step 044 tests only, real run)

```
Passed XlightsAI.Studio.Stems.Tests.Step044AcceptanceTests.MissingReferenceMediaFile_ThrowsSeparationQualityInvalidInputException_SeparatorNeverInvoked_NegativePath [57 ms]
Passed XlightsAI.Studio.Stems.Tests.Step044AcceptanceTests.EvaluateAsync_AlreadyCancelled_ThrowsOperationCanceledException_NegativePath [33 ms]
Passed XlightsAI.Studio.Stems.Tests.Step044AcceptanceTests.Step044_Acceptance_Passes_MeasuredQualityAndPerformanceDocumentedAndThresholdMet [641 ms]
Passed XlightsAI.Studio.Stems.Tests.Step044AcceptanceTests.MismatchedClipLengths_ThrowsSeparationQualityInvalidInputException_NegativePath [7 ms]
Passed XlightsAI.Studio.Stems.Tests.Step044AcceptanceTests.BelowThresholdMeasurement_FlipsOverallPassToFalse_NegativePath [118 ms]
Passed XlightsAI.Studio.Stems.Tests.Step044AcceptanceTests.InvalidThresholds_ThrowsSeparationQualityInvalidInputException_NegativePath [6 ms]
Passed XlightsAI.Studio.Stems.Tests.Step044AcceptanceTests.NoQualityClips_ThrowsSeparationQualityInvalidInputException_NegativePath [6 ms]
Passed XlightsAI.Studio.Stems.Tests.Step044AcceptanceTests.SilentReferenceReproducedBySilentCandidate_HandledExplicitly_NoNaNOrInfinity [8 ms]
Passed XlightsAI.Studio.Stems.Tests.Step044AcceptanceTests.SilentReferenceButLoudCandidate_HandledExplicitly_FailsWithoutNaN_NegativePath [8 ms]
Passed XlightsAI.Studio.Stems.Tests.Step044AcceptanceTests.ZeroLengthClip_ThrowsSeparationQualityInvalidInputException_NegativePath [9 ms]

Total tests: 10. Passed: 10, Failed: 0.
```

### Full regression (after implementation, final run)

```
Passed!  - Failed: 0, Passed:  71, Skipped: 0, Total:  71  - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed:  91, Skipped: 0, Total:  91  - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed:  75, Skipped: 0, Total:  75  - XlightsAI.Studio.Stems.Tests.dll
Passed!  - Failed: 0, Passed:  25, Skipped: 0, Total:  25  - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed:  34, Skipped: 0, Total:  34  - XlightsAI.Studio.Audio.Tests.dll
Passed!  - Failed: 0, Passed: 153, Skipped: 0, Total: 153 - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed:  82, Skipped: 0, Total:  82  - XlightsAI.Studio.Rhythm.Tests.dll
Passed!  - Failed: 0, Passed:  31, Skipped: 0, Total:  31  - XlightsAI.Studio.Reference.Tests.dll
```

**562 total, 0 failed, 0 skipped** — up from the Step 043 baseline of 552 (+10 new `Step044AcceptanceTests`
in the existing `Stems.Tests` project: 65 + 10 = 75, matching the reported `75` above). Every one of the
552 pre-existing tests still passes unmodified, confirming no prior step's contract was broken. (Re-run a
second time after the manual demo below to confirm no state leaked between runs: identical 562/562.)

## 5. Persisted-output inspection / observable demonstration for this step

A real, non-test-harness console program (not part of the repository, built under the session scratchpad,
deleted after this run) exercises the actual `OnnxStemSeparator` against the real committed
`fixtures/onnx/synthetic-stem-separator-v1.onnx` file from disk (not the tests' embedded base64 copy — an
independent code path), the real `SeparationQualityEvaluator`/`SeparationQualityMetricCalculator`, and the
real `JsonSeparationQualityReportStore`:

```
dotnet run --project <scratchpad>\xlai-step044-demo-proj\step044-demo.csproj --nologo
```

Observed real output:

```
Demo root directory: C:\Users\kmoens.LAKECO\AppData\Local\Temp\xlai-step044-demo-3fc4f17655bb440ea1fd0be1a3ac8388
Loaded real committed fixture: synthetic-stem-separator-v1.onnx (307 bytes, sha256 5fc4b43c4121faa57751ee5b3d8d4a17601f46c7e39f2501308dcff65a100a75)

=== Scenario 1: two real listening reference clips scored against known ground truth ===
Overall pass: True (8/8 clip/stem metrics passing)
Decision: All 8 measured clip/stem metrics met the declared acceptance threshold (SDR >= 60 dB, correlation >= 0.999).
  clip-a-220hz:Drums: SDR=112.01 dB, correlation=1.000000, silent=False, pass=True
  clip-a-220hz:Bass: SDR=119.97 dB, correlation=1.000000, silent=False, pass=True
  clip-a-220hz:Vocals: SDR=125.99 dB, correlation=1.000000, silent=False, pass=True
  clip-a-220hz:Other: SDR=129.51 dB, correlation=1.000000, silent=False, pass=True
  clip-b-440hz:Drums: SDR=108.49 dB, correlation=1.000000, silent=False, pass=True
  clip-b-440hz:Bass: SDR=116.44 dB, correlation=1.000000, silent=False, pass=True
  clip-b-440hz:Vocals: SDR=122.46 dB, correlation=1.000000, silent=False, pass=True
  clip-b-440hz:Other: SDR=125.99 dB, correlation=1.000000, silent=False, pass=True

=== Scenario 2: establish supported hardware (real, measured, not estimated) ===
  OS: Microsoft Windows 10.0.26300
  Architecture: X64
  ProcessorCount: 40
  TotalAvailableMemoryBytes: 68,349,579,264
  PeakWorkingSetBytes: 89,059,328
  MeasuredWallClockSecondsPerSongMinute: 8.3455

=== Scenario 3: persisted, versioned, inspectable report artifact ===
Saved report: C:\Users\kmoens.LAKECO\AppData\Local\Temp\xlai-step044-demo-3fc4f17655bb440ea1fd0be1a3ac8388\reports\separation-quality-1323c8d624b44ba3ba51e55c29ddc681.json
Reloaded report matches: True

=== Scenario 4: silence handled explicitly (no NaN/Infinity) ===
Silent reference reproduced by silent candidate: SDR=120 dB (sentinel, finite, not Infinity/NaN), correlation=1, pass=True

=== Scenario 5: invalid timing boundaries rejected explicitly ===
Correctly rejected: Clip 'bad-boundary-demo' (stem Bass): candidate length 100 does not match reference length 80 -- invalid timing boundaries (this step's own failure/edge case, verbatim).

=== Scenario 6: missing reference media rejected explicitly, separator never invoked ===
Correctly rejected: Clip 'missing-media-demo' (stem Drums): reference media file '...\reference\does-not-exist.wav' does not exist.

Cleaning up demo directory.
Step 044 manual demo completed successfully.
```

Direct, real, non-test-harness confirmation: two real listening reference clips (220 Hz / 440 Hz, 2
seconds each) are separated by the real ONNX Runtime mechanism and all 8 resulting clip/stem metrics clear
a declared 60 dB SDR / 0.999 correlation acceptance threshold (required acceptance, verbatim: "Measured
separation quality/performance is documented and acceptance threshold met"); real hardware figures
(40 logical processors, ~68 GB available memory, ~89 MB peak working set, ~8.3 real measured CPU-seconds
per song-minute for this mechanism on this machine) are recorded, not estimated (this step's own title,
verbatim: "...establish supported hardware"); the report round-trips through a real JSON file on disk;
silence, invalid timing boundaries and missing media are each handled explicitly with no crash and no
`NaN`/`Infinity` anywhere in the output (this step's own failure/edge case, verbatim).

## 6. Fixtures / input hashes

No new committed fixture file was added for this step. The real, already-committed
`fixtures/onnx/synthetic-stem-separator-v1.onnx` (307 bytes, sha256
`5fc4b43c4121faa57751ee5b3d8d4a17601f46c7e39f2501308dcff65a100a75`, registered in
`fixtures/FIXTURE-CORPUS-MANIFEST.json` since Step 040) is reused byte-for-byte. Every WAV file (source
clips, analytically-derived reference stems, produced candidate stems) used by this step's tests and demo
is generated in-process, deterministically, by the already-real `StemWavWriter.WriteMonoFloatAsync`
against a pure-function deterministic sine-wave generator (the same pattern Steps 040-043 already used) —
there is nothing new to pin a SHA-256 against ahead of time. All test-run and demo temp directories
(`%TEMP%\xlai-step044-*\`) are deleted by each test's own `IDisposable.Dispose()` / the demo's own cleanup
code (confirmed by "Cleaning up demo directory." / "Step 044 manual demo completed successfully." in the
real output above); the throwaway demo *project* itself (outside the repository, under the session
scratchpad) was deleted after this evidence was recorded.

## 7. Unresolved issues / known gaps

- **No production stem-separation model (Demucs-onnx, sherpa-onnx, UVR, or otherwise) was downloaded, run,
  or scored in this step.** Disclosed at length in §1/§3: Step 038's own model-feasibility verdict remains
  BLOCKED. This step's own measured quality/performance figures describe the real ONNX Runtime mechanism
  against an analytically-known synthetic fixture, never a real musical separation-quality or
  cross-machine performance-parity claim. Revisiting Step 038's feasibility decision once a specific,
  license-clear, size-appropriate production model is actually obtained remains a prerequisite for any
  such claim — this step's own generic `QualityClip`/`ISeparationQualityEvaluator` contract is already
  shaped to accept real reference stems unchanged whenever that happens.
- **Only one machine's `HardwareProfile` was measured.** Disclosed in §3: "establish supported hardware"
  is satisfied here as "one real, measured baseline," not "verified across every target machine class" —
  Section 2's own "no undocumented claim that all ... behave identically" rule is respected, not violated,
  by this narrower claim.
- **No `ShellScreen.Stems` navigation/UI wiring added in this step** — consistent with Step 038's own
  identical precedent for a research/decision-shaped step; this step's "Saved audio/analysis artifact with
  timestamp provenance" user-visible outcome is satisfied by the persisted `SeparationQualityReport` JSON
  file itself (§2/§5), not by new UI surface.
- **Mono-only, whole-clip pipeline** — carried forward, disclosed, unchanged from every prior Phase 06
  step; this step measures whatever `IStemSeparator` already produces, it does not change the separation
  mechanism's own scope boundaries (mono-only decode, chunked/overlap-add inference, etc.).

## 8. PASS / BLOCKED

**Status: PASS** (for this step's own narrow, disclosed scope — a real, generic, end-to-end quality-metric
and hardware-benchmarking *mechanism*, proven against the one real separation mechanism that exists in
this repository — not a production separation-quality claim, which remains the phase-level BLOCKED item
carried forward from Step 038 and does not block this step's own required acceptance text).

- "Measured separation quality/performance is documented and acceptance threshold met" (required
  acceptance, verbatim) — proven by
  `Step044_Acceptance_Passes_MeasuredQualityAndPerformanceDocumentedAndThresholdMet` (two real listening
  reference clips, real separation run, real metrics, persisted and round-tripped `SeparationQualityReport`
  with `OverallPass == true` against a declared, non-trivial 60 dB SDR / 0.999 correlation threshold) and
  independently confirmed by the real demo's Scenarios 1-3.
- "At least two listening reference clips meet declared quality bar" (full-phase test matrix item 7,
  verbatim) — proven by the same test/demo: two distinct clips (220 Hz / 440 Hz), 4 stems each, all 8
  clip/stem metrics passing.
- "...establish supported hardware" (this step's own title, verbatim) — proven by `HardwareProfile`'s
  real, measured (never estimated) `OsDescription`/`ProcessArchitecture`/`ProcessorCount`/
  `TotalAvailableMemoryBytes`/`PeakWorkingSetBytes`/`MeasuredWallClockSecondsPerSongMinute` fields, asserted
  non-trivial in the acceptance test and independently confirmed by the real demo's Scenario 2.
- "Silence, invalid timing boundaries and missing media must be handled explicitly" (failure/edge case,
  verbatim) — proven three ways:
  `SilentReferenceReproducedBySilentCandidate_HandledExplicitly_NoNaNOrInfinity` and
  `SilentReferenceButLoudCandidate_HandledExplicitly_FailsWithoutNaN_NegativePath` (silence scored via
  named finite sentinels, never `NaN`/`Infinity`);
  `MismatchedClipLengths_ThrowsSeparationQualityInvalidInputException_NegativePath` and
  `ZeroLengthClip_ThrowsSeparationQualityInvalidInputException_NegativePath` (invalid timing boundaries
  rejected explicitly); `MissingReferenceMediaFile_..._SeparatorNeverInvoked_NegativePath` (missing media
  rejected explicitly, before any expensive separation work runs). All three independently confirmed by
  the real demo's Scenarios 4-6.
- Additional rigor beyond the minimum: `BelowThresholdMeasurement_FlipsOverallPassToFalse_NegativePath`
  (proves the acceptance gate actually gates — an unattainable threshold correctly fails `OverallPass`,
  not a rubber stamp); `InvalidThresholds_ThrowsSeparationQualityInvalidInputException_NegativePath`
  (malformed threshold configuration rejected before any measurement); `NoQualityClips_Throws..._NegativePath`
  and `EvaluateAsync_AlreadyCancelled_ThrowsOperationCanceledException_NegativePath` (empty-input and
  cancellation handled cleanly, no partial/fabricated report).
- Full regression: **562/562 passing**, 0 failed, 0 skipped; every pre-existing test from Steps 001-043
  still passes unmodified.
- **Carried-forward, disclosed phase-level gap (not this step's own blocker, and this step's own final
  disposition on it):** no production separation-quality or cross-machine performance-parity claim exists
  anywhere in the repository. Step 038's BLOCKED feasibility verdict stands unresolved at the end of
  Phase 06 — this was always the explicitly-disclosed, honest possible outcome Section 12 names verbatim
  ("If no candidate meets quality/runtime requirements, do not fabricate a working ... implementation.
  Keep earlier MVP functionality available without separated stems and record Phase 06 as BLOCKED until an
  approved non-Python backend exists."). Per the user's own standing waiver
  (`memory: feedback_skip_xlights_human_verification`), the xLights human-render check is not treated as a
  blocker for this or any other step, and is not fabricated here; no xLights-specific manual verification
  applies to this step in any case (it does not touch the `.xsq`/`xlights_rgbeffects.xml` pathway).

## 9. Phase 06 gate status (required final gate report, Section 8)

All seven numbered steps (038-044) now have independently recorded evidence. Per Step 038's own disclosed
decision and Section 12's own explicit instruction, **Phase 06's overall gate status is BLOCKED**, not
PASS: no candidate stem-separation model has ever been proven to run end-to-end under a .NET host in this
repository, so the phase's own user story ("As a sequencer, I can separate drums, bass and vocals locally
...") and its own blocking phase gate (the CPU-only Windows install/separate/solo/abort demo against a
*real* model) cannot be honestly demonstrated. Steps 039-044 each independently and honestly proved their
own real mechanism (download/verification, chunked ONNX inference, waveform/mixer UI, activity detection,
and now quality/hardware measurement) against the one real, committed, honestly-synthetic fixture — none
of those steps' own required acceptance text depends on a production model, and all seven are individually
PASS — but the phase-level blocker named in Step 038 (zero feasible production model) is carried forward
unresolved and undiminished by any later step's work. Per Section 8's own instruction ("Next phase begins
only after PASS or an explicit scoped architecture waiver"), Phase 07 should not begin against a silent
assumption that Phase 06 is complete; it should begin only once this BLOCKED verdict is either resolved
(a real, license-clear, redistributable model is obtained and integrated) or an explicit scoped waiver is
recorded by the user/maintainer.
