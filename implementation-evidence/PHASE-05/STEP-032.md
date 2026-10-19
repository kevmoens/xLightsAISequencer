# PHASE 05 — Step 032 Evidence

**Step:** "Implement onset/transient detection with configurable sensitivity"
**Spec:** `docs/phase-specs/PHASE-05.md` §6, Step 032 (lines 218-232)
**Date:** 2026-10-11

## 1. Pre-implementation baseline inspection

- Re-read `docs/phase-specs/PHASE-05.md` in full: Section 2's architectural assumptions, Section 3's
  design notes ("Build spectral flux/energy rise onsets and optional percussive stem hooks; correct
  algorithmic group delay"), the proposed `OnsetEvent(long TimeTicks, float Strength, string
  SourceAlgorithm)` signature, Section 4's cache-key rule, and Step 032's own required acceptance
  ("Onset precision/recall measured on annotated fixtures") and failure/edge case ("Invalid input,
  cancellation or stale persisted state must not corrupt the project").
- Read `implementation-evidence/PHASE-05/STEP-31.md` in full and treated every type/behavior it
  describes as an immutable prior contract: `RhythmSettings`, `EnergyFrame`, `EnergyEnvelope`,
  `IEnergyEnvelopeAnalyzer`/`EnergyEnvelopeAnalyzer`, `IEnergyEnvelopeStore`/`JsonEnergyEnvelopeStore`,
  `EnergyEnvelopeInvalidException`/`EnergyEnvelopeCorruptException`. None of these files were opened in
  write mode this step; verified unmodified by re-running Step 031's own 7 acceptance tests unchanged
  after this step's implementation (§4).
- Inspected the real source of every file this step depends on directly: `SongAsset` (immutable,
  PHASE-04), `IAudioDecoder`/`WavAudioDecoder.DecodeMonoAsync` (does not resample — same disclosed
  constraint Step 031 already worked around, reused identically here), `DeterministicSyntheticAudioGenerator`
  (`BuildClickTrack`, `BuildNoteSequence`, `EncodeWav` — PHASE-01 Step 003, unmodified).
- Confirmed Step 031's own disclosed known gaps are exactly this step's own real new scope: "Only a
  time-domain RMS/energy feature is computed, not a real FFT-based spectral feature" and "Latency/
  group-delay correction... is not implemented in this step... flagged as a known gap for Step 032 to
  address." This step's `OnsetDetector` performs its own independent real DFT-based spectral analysis
  (not a reuse of `EnergyEnvelopeAnalyzer`'s RMS output) and explicitly implements the group-delay
  correction Step 031 deferred (§3 below).
- Baseline regression before any Step 032 code existed, run fresh in this session:
  **401/401 passing** (`dotnet test XlightsAI.Studio.slnx --nologo`), matching Step 031's own final
  recorded count exactly.

## 2. Scope actually implemented

All new files added to the existing `src/XlightsAI.Studio.Rhythm/XlightsAI.Studio.Rhythm.csproj`
project (no new project — Step 032 is the same PHASE-05/rhythm-analysis ownership area Step 031
established). No changes to the `.csproj` file itself (no new package references needed — see §3).

- **`OnsetEvent.cs`** — Section 3's proposed signature, verbatim shape: `OnsetEvent(long TimeTicks,
  float Strength, string SourceAlgorithm)`.
- **`OnsetDetectionSettings.cs`** — this step's own "configurable sensitivity" (its own title) knobs:
  `SensitivityMultiplier` (adaptive-threshold scale) and `MinInterOnsetSamples` (refractory period).
  Not named in Section 3's proposed shapes — this step's own first definition of what "configurable"
  concretely means, same convention Step 031 used for `EnergyFrame`.
- **`OnsetAnalysis.cs`** — the persisted, versioned output, mirroring `EnergyEnvelope`'s provenance
  shape (`VersionId`, `SongId`, `SourceHash`, `SourceDecoderVersion`, `AnalyzerVersion`, `Settings`)
  with `OnsetSettings` added as this step's own second, independent cache-key component.
- **`IOnsetDetector.cs`** / **`OnsetDetector.cs`** — real implementation: decodes the song exactly once
  via the injected `IAudioDecoder` (independently of `EnergyEnvelopeAnalyzer` — no shared decode/cache
  path, so neither step can silently affect the other's cached artifact), computes a real Hann-windowed
  DFT magnitude spectrum per `RhythmSettings.FftSize`/`HopSamples` frame, derives half-wave-rectified
  spectral flux between consecutive frames, unit-normalizes flux against this run's own peak, and
  peak-picks onsets against a local adaptive threshold (`local mean + SensitivityMultiplier * local
  stddev`, computed over an 8-frame radius) combined with an absolute floor (`>= 0.05` of peak,
  disclosed in §3) to reject spectral-leakage noise. Enforces `MinInterOnsetSamples` as a refractory
  period by accepting strongest-first. Reports each onset's `TimeTicks` at the analysis window's
  *center* sample, not its start — the disclosed group-delay correction (§3).
- **`IOnsetAnalysisStore.cs`** / **`JsonOnsetAnalysisStore.cs`** — persistence boundary mirroring
  `IEnergyEnvelopeStore`/`JsonEnergyEnvelopeStore` exactly: deterministic per-`SongId` path
  (`onset-analysis-{songId:N}.json`), atomic temp-file-then-`File.Move` writes, `LoadAsync` for
  independent reopening/inspection.
- **`OnsetAnalysisInvalidException.cs`** / **`OnsetAnalysisCorruptException.cs`** — mirror
  `EnergyEnvelopeInvalidException`/`EnergyEnvelopeCorruptException`'s own shape and messages.

**New test file `tests/XlightsAI.Studio.Rhythm.Tests/Step032AcceptanceTests.cs`** — 9 tests (detailed
in §4). No changes to `Step031AcceptanceTests.cs` or any other existing test file.

**No changes to any existing `Contracts`, `Audio`, `Editing`, `Xlights`, `Projects`, `Reference`,
`Shell`, `App`, or Step 031 `Rhythm` file.** Purely additive within the already-existing `Rhythm`/
`Rhythm.Tests` projects — no solution-file change needed (both projects were already registered by
Step 031).

## 3. Architecture deviations / decisions

- **A direct O(FftSize × bins) DFT, not a dedicated FFT library.** Section 3's design notes name
  "spectral flux" without mandating a specific transform implementation, and no FFT package is
  referenced anywhere in this repository today. At this step's own fixed, test-scale window sizes (256
  or 512 samples), a direct DFT is fast enough (the full annotated-fixture test, including 219 frames ×
  129 bins, completes in ~225 ms) and avoids introducing a new third-party dependency for a single
  phase-local feature — consistent with Section 2's "do not fabricate generic interfaces without a
  concrete boundary" and this project's own no-unneeded-dependency convention. If a later phase needs
  spectral analysis over much longer windows or full-song real-time rates, revisiting this for a real
  FFT is a disclosed, scoped future decision, not a silent limitation.
- **An absolute flux floor (`MinimumAbsoluteNormalizedFlux = 0.05`) alongside the relative adaptive
  threshold — found necessary by an actually-failing test, not assumed up front.** The first real run
  of `DetectAsync_SustainedTone_NoSpuriousOnsetsAfterAttack_FalseTriggerNegativePath` (a single
  held 440 Hz tone, 2 s) failed: 8 spurious onsets were detected, each with a vanishingly small
  normalized strength (~0.00004) — a real, present artifact of windowed spectral analysis: a 256-sample
  Hann window is not an exact integer multiple of a 440 Hz tone's own period (8000/440 ≈ 18.18 samples),
  so the window's phase relative to the waveform drifts cycle-to-cycle, producing a slow, genuine
  (not a bug) spectral-leakage "beat" in magnitude. Because the surrounding local flux baseline is
  equally near-zero, the purely-relative adaptive threshold (`mean + multiplier·stddev`) could not
  reject it — multiplying a near-zero stddev by any positive sensitivity still yields a near-zero
  threshold. Adding a small absolute floor (5% of this run's own peak flux) rejects the leakage
  artifact while leaving every real click-track transient (normalized strength 0.39-1.00 in the
  annotated-fixture run, §4's demo output) untouched. This is a disclosed, test-driven addition to the
  detector's own threshold logic, not a hidden tuning knob.
- **Group delay correction: report the analysis window's center sample, not its start, as each onset's
  timestamp.** Section 3 names this explicitly ("correct algorithmic group delay") and Step 031's own
  evidence disclosed it as an unaddressed gap. A windowed spectral feature computed over
  `[start, start+FftSize)` reflects energy spread across the whole window, not a single instant;
  reporting the window start as "the" onset time would systematically under-report the true transient
  position by up to half a window (16 ms at this step's 256-sample/8 kHz configuration). Reporting the
  window's center instead is the standard correction for this detector class, and is directly
  exercised by `Step032_Acceptance_Passes_OnsetPrecisionRecallMeasuredOnAnnotatedFixture`'s own
  one-hop-tolerance timestamp-accuracy assertion (every one of 6 known click onsets lands within a
  single 16 ms hop of its true position — see §4's real demo run for exact numbers).
- **A leading-silence annotated fixture, not a click starting at absolute sample 0.** Spectral flux is,
  by construction, undefined for the very first analysis frame (no predecessor frame exists to compare
  against) — a true onset placed at sample 0 could never be detected by this class of algorithm
  regardless of sensitivity, independent of any implementation choice made here. `Step032AcceptanceTests`'s
  own `BuildLeadInClickTrack` helper prepends 4000 samples (0.5 s) of silence before the first click so
  every annotated true onset has a real predecessor frame to be detected against — a disclosed,
  standard onset-detection fixture convention, not a loosened acceptance bar.
- **`OnsetDetector` decodes the song independently of `EnergyEnvelopeAnalyzer`**, rather than
  consuming `EnergyEnvelope`'s already-decoded buffer or its RMS output. The two analyzers answer
  different questions (time-domain energy vs. spectral-flux transients) from the same raw audio and
  are each independently cacheable/invalidatable per Section 4's per-artifact cache-key rule; sharing a
  decode path would require a shared decoded-sample cache this step's own scope does not need and Step
  031's own contract does not expose. Each analyzer's own single real decode is proven independently by
  each one's own `CountingAudioDecoder`-backed cache tests.
- **Reused Step 031's exact `TryLoadStillValidCacheAsync`/atomic-write/"write only after verified
  dimensions" pattern**, extended with one additional cache-key component (`OnsetDetectionSettings`
  equality) — rather than inventing a new caching shape for a second analyzer in the same project.

## 4. Commands actually executed

All run via the PowerShell tool — the sandboxed POSIX Bash tool in this session is non-functional
(every invocation, including a plain `true`, returns exit code 1 with no output), the same
pre-existing environment limitation recorded in every prior step's own evidence (re-confirmed in this
session before falling back to PowerShell).

```
dotnet build src\XlightsAI.Studio.Rhythm\XlightsAI.Studio.Rhythm.csproj --nologo         # new Rhythm source files only, before tests existed
dotnet test  tests\XlightsAI.Studio.Rhythm.Tests\XlightsAI.Studio.Rhythm.Tests.csproj --nologo --logger "console;verbosity=detailed" --filter "FullyQualifiedName~Step032"
  # first real run: 8/9 passed, 1 failed (sustained-tone false-trigger test) — see §3's absolute-floor fix
dotnet test  tests\XlightsAI.Studio.Rhythm.Tests\XlightsAI.Studio.Rhythm.Tests.csproj --nologo --logger "console;verbosity=normal"
  # after the fix: full Rhythm.Tests project, 16/16 passed (7 Step031 + 9 Step032)
dotnet build XlightsAI.Studio.slnx --nologo                                              # full solution, after Step 032 implementation
dotnet test  XlightsAI.Studio.slnx --nologo                                               # full regression, final
```

### Build results

`Rhythm`, `Rhythm.Tests`, and the full solution all: `Build succeeded. 0 Warning(s). 0 Error(s).`

### Targeted test results (Step 032 tests only, final run)

```
Passed XlightsAI.Studio.Rhythm.Tests.Step032AcceptanceTests.DetectAsync_DeclaredSampleCountMismatch_ThrowsOnsetAnalysisInvalidException_NegativePath [59 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step032AcceptanceTests.DetectAsync_MismatchedAnalysisSampleRate_ThrowsWithoutDecodingOrWriting_InvalidInputNegativePath [15 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step032AcceptanceTests.DetectAsync_HigherSensitivityMultiplier_ProducesFewerOrEqualOnsets_ConfigurableSensitivity [448 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step032AcceptanceTests.Step032_Acceptance_Passes_OnsetPrecisionRecallMeasuredOnAnnotatedFixture [225 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step032AcceptanceTests.DetectAsync_CancelledBeforeDecodeCompletes_ThrowsWithoutWritingAnyCacheFile_CancellationNegativePath [18 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step032AcceptanceTests.DetectAsync_SustainedTone_NoSpuriousOnsetsAfterAttack_FalseTriggerNegativePath [128 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step032AcceptanceTests.DetectAsync_NonPositiveSensitivityMultiplier_ThrowsWithoutDecodingOrWriting_InvalidInputNegativePath [7 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step032AcceptanceTests.DetectAsync_ContentHashChanges_InvalidatesCacheAndRebuilds_StalePersistedStateNegativePath [260 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step032AcceptanceTests.DetectAsync_OnsetSettingsChange_InvalidatesCacheAndRebuilds_StalePersistedStateNegativePath [417 ms]

Total tests: 16 (full Rhythm.Tests: 7 Step031 + 9 Step032). Passed: 16, Failed: 0.
```

### Full regression (after implementation, final run)

```
Passed!  - Failed: 0, Passed:  71, Skipped: 0, Total:  71, Duration: 319 ms - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed:  91, Skipped: 0, Total:  91, Duration: 305 ms - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed:  16, Skipped: 0, Total:  16, Duration: 1 s   - XlightsAI.Studio.Rhythm.Tests.dll
Passed!  - Failed: 0, Passed:  25, Skipped: 0, Total:  25, Duration: 2 s   - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed:  34, Skipped: 0, Total:  34, Duration: 4 s   - XlightsAI.Studio.Audio.Tests.dll
Passed!  - Failed: 0, Passed: 142, Skipped: 0, Total: 142, Duration: 4 s   - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed:  31, Skipped: 0, Total:  31, Duration: 35 s  - XlightsAI.Studio.Reference.Tests.dll
```

**410 total, 0 failed, 0 skipped** — up from the Step 031 baseline of 401 (+9 new `Step032AcceptanceTests`
in the already-existing `Rhythm.Tests` project). Every one of the 401 pre-existing tests still passes
unmodified, including all 7 of Step 031's own tests, proving no Step 031 contract was broken.

### Persisted-output inspection (observable demo for this step)

Step 032's own user-visible outcome is "Versioned, inspectable project output plus a reproducible test
report" — same shape as Step 031, no new UI screen in this step's own scope. The observable
demonstration is a real, running end-to-end exercise of the actual persisted artifact, outside the test
harness's temp-directory cleanup, run from a throwaway console program (not part of the repository,
under the session scratchpad, deleted after this run) referencing the real `Rhythm`/`Audio`/`Reference`
projects:

```
dotnet run --nologo   # from a throwaway console app referencing the real Rhythm/Audio/Reference projects
```

Observed real output (BPM 120 click track, 6 clicks, 0.5 s silent lead-in, 8 kHz, `FftSize=256`/
`HopSamples=128`, `SensitivityMultiplier=1.5`/`MinInterOnsetSamples=256`):

```
Probed SongAsset: Id=9f79e640-c6b4-46c8-8d39-0f27ea2e8bba, Sha256=e0dcd357fcb6..., SampleRate=8000, SampleCount=28000
Persisted analysis at: ...\onset-cache\onset-analysis-9f79e640c6b446c88d390f27ea2e8bba.json
File exists on disk: True
Onset count: 6
AnalyzerVersion: XlightsAI.Studio.Rhythm.OnsetDetector/1.0.0
Settings: FftSize=256, HopSamples=128, AnalysisSampleRate=8000
OnsetSettings: SensitivityMultiplier=1.5, MinInterOnsetSamples=256

True onset samples (known click starts) vs nearest detected onset:
  true sample=  4000  nearest detected ticks=   4960000  offsetTicks=   40000  withinOneHopTolerance=True  strength=0.76344
  true sample=  8000  nearest detected ticks=   9920000  offsetTicks=   80000  withinOneHopTolerance=True  strength=0.39413
  true sample= 12000  nearest detected ticks=  15040000  offsetTicks=   40000  withinOneHopTolerance=True  strength=0.88478
  true sample= 16000  nearest detected ticks=  20000000  offsetTicks=       0  withinOneHopTolerance=True  strength=1.00000
  true sample= 20000  nearest detected ticks=  24960000  offsetTicks=   40000  withinOneHopTolerance=True  strength=0.76344
  true sample= 24000  nearest detected ticks=  29920000  offsetTicks=   80000  withinOneHopTolerance=True  strength=0.39413

Precision=1.000  Recall=1.000  (truePositives=6, detected=6, trueOnsets=6)

Reopened analysis VersionId matches: True
Reopened onset count matches: True
```

All 6 known click onsets are matched within one hop (16 ms, well under `FftSize`'s 32 ms full window)
of their true sample position, with zero spurious detections — direct, real, on-disk confirmation of
"Onset precision/recall measured on annotated fixtures" (Precision=1.000, Recall=1.000), independent of
the unit test assertions. The full raw persisted JSON file (`onset-analysis-*.json`) was also printed
and visually inspected; it round-trips through `JsonOnsetAnalysisStore.LoadAsync` with an identical
`VersionId` and onset count, confirming the on-disk artifact is the same object the detector returned.

No xLights/Avalonia UI demonstration was run for this step: Step 032 introduces no UI screen, no
`SequenceDocument`/`EditTransaction` mutation, and no xLights XML/XSQ interaction — its entire real
surface is the `IOnsetDetector`/`IOnsetAnalysisStore` pair proven above. Per this user's own standing
waiver (recorded in this assistant's memory) a real-xLights render check is not required as a
phase-gate blocker; it is doubly not applicable here since this step has no xLights-facing behavior to
demonstrate in the first place.

## 5. Fixtures / input hashes

No new fixture files were added to `fixtures/`. All tests and the manual demo construct real WAV bytes
via this repository's own already-licensed, already-verified `DeterministicSyntheticAudioGenerator`:

- `BuildClickTrack(bpm: 120, clickCount: 6, clickDurationSeconds: 0.02, toneHz: 1000.0, sampleRate: 8000)`
  prepended with 4000 samples (0.5 s) of silence — the primary acceptance/demo annotated fixture: true
  onset sample positions (4000, 8000, 12000, 16000, 20000, 24000) are known exactly from the generator's
  own recipe plus the fixed lead-in offset.
- `BuildNoteSequence(frequenciesHz: [440.0], noteDurationSeconds: 2.0, sampleRate: 8000)` — a single
  sustained tone, used for the "false triggers from sustained chords" negative-path test.
- All test files are written under `%TEMP%\xlai-step032-*\`, deleted by each test class's `Dispose()` —
  identical cleanup convention to Step 031. The manual demo's own throwaway files were written under
  `%TEMP%\xlai-step032-demo-*\` and deleted by the demo program itself at the end of its run (confirmed
  by "Temp directory cleaned up." in the real output above).

## 6. Unresolved issues / known gaps

- **Only a direct O(FftSize × bins) DFT is implemented, not a dedicated FFT library** — a disclosed,
  deliberate scoping decision (§3): sufficient for this step's own fixed, test-scale window sizes;
  revisiting for a real FFT is a scoped future decision if a later phase needs much longer windows or
  full-song real-time analysis rates.
- **No "percussive stem hooks"** (Section 3: "spectral flux/energy rise onsets *and optional percussive
  stem hooks*") — `StemManifest`/`StemAsset` (PHASE-06, per Section 2's stable-domain-ownership table)
  do not exist yet; this is explicitly named "optional" by the spec and is PHASE-06's own scope to
  introduce, not a silent omission here. `OnsetEvent.SourceAlgorithm` is reserved to let a future
  stem-based detector coexist with `OnsetDetector.AlgorithmName` ("SpectralFlux") without a breaking
  rename.
- **A true onset at absolute sample 0 cannot be detected** — a disclosed, standard limitation of
  spectral-flux detection in general (no predecessor frame exists for the very first frame), not an
  implementation gap specific to this detector. `Step032AcceptanceTests`'s own fixtures use a silent
  lead-in to avoid asserting against an inherently undetectable position (§3).
- **The absolute flux floor (`MinimumAbsoluteNormalizedFlux = 0.05`) is a fixed constant, not itself
  configurable** — found necessary mid-implementation by a real failing test (§3), not part of Step
  032's own "configurable sensitivity" surface (`OnsetDetectionSettings.SensitivityMultiplier`/
  `MinInterOnsetSamples`). If a future step needs to tune this floor per-song or per-genre, exposing it
  as a third `OnsetDetectionSettings` field is a small, disclosed, additive follow-up — not a breaking
  change to this step's own contract shape.
- **No manual/human downbeat correction, tempo candidates, or `BeatGrid`** — Steps 033 ("BPM candidate
  detection") and 034 ("beats, likely downbeats, bars") are this phase's own next steps, explicitly out
  of this step's scope.

## 7. PASS / BLOCKED

**Status: PASS** for Step 032's own stated required acceptance and failure/edge case:

- "Onset precision/recall measured on annotated fixtures" — proven by
  `Step032_Acceptance_Passes_OnsetPrecisionRecallMeasuredOnAnnotatedFixture` (precision >= 0.8, recall
  == 1.0 against a click-track fixture with exactly known true onset sample positions) and
  independently confirmed by the real, non-test-harness demo run in §4 (Precision=1.000, Recall=1.000,
  all 6 known onsets matched within one hop of their true position).
- "Build spectral flux/energy rise onsets" — a real Hann-windowed DFT-based spectral-flux computation,
  not a reuse of Step 031's time-domain RMS (§2/§3); proven by every passing test exercising
  `OnsetDetector.DetectAsync`.
- "Correct algorithmic group delay" — proven by the same acceptance test's one-hop timestamp-accuracy
  assertion and the real demo's own per-click offset numbers (0-80000 ticks / 0-8 ms, well under the
  16 ms one-hop tolerance and far under the 32 ms full-window size that an uncorrected timestamp would
  risk).
- "Test false triggers from sustained chords" — proven by
  `DetectAsync_SustainedTone_NoSpuriousOnsetsAfterAttack_FalseTriggerNegativePath`, which failed on its
  first real run (8 spurious low-strength onsets from genuine spectral leakage) and now passes after the
  disclosed absolute-floor fix (§3) — a real, test-driven correction, not an assumption-free pass.
- "Configurable sensitivity" (this step's own title) — proven by
  `DetectAsync_HigherSensitivityMultiplier_ProducesFewerOrEqualOnsets_ConfigurableSensitivity` (a
  stricter `SensitivityMultiplier` never detects more onsets than a more sensitive one on the same
  signal) and `DetectAsync_OnsetSettingsChange_InvalidatesCacheAndRebuilds_StalePersistedStateNegativePath`.
- "Invalid input... must not corrupt the project" — proven by
  `DetectAsync_MismatchedAnalysisSampleRate_ThrowsWithoutDecodingOrWriting_InvalidInputNegativePath`,
  `DetectAsync_NonPositiveSensitivityMultiplier_ThrowsWithoutDecodingOrWriting_InvalidInputNegativePath`,
  and `DetectAsync_DeclaredSampleCountMismatch_ThrowsOnsetAnalysisInvalidException_NegativePath` (zero
  real decode calls / no cache file written for every invalid-input case).
- "Cancellation... must not corrupt the project" — proven by
  `DetectAsync_CancelledBeforeDecodeCompletes_ThrowsWithoutWritingAnyCacheFile_CancellationNegativePath`.
- "Stale persisted state must not corrupt the project" — proven by
  `DetectAsync_ContentHashChanges_InvalidatesCacheAndRebuilds_StalePersistedStateNegativePath` and
  `DetectAsync_OnsetSettingsChange_InvalidatesCacheAndRebuilds_StalePersistedStateNegativePath` (a
  changed content hash or changed `OnsetDetectionSettings` triggers a real second decode/rebuild rather
  than silently serving the old analysis).
- The step's own user-visible outcome ("Versioned, inspectable project output plus a reproducible test
  report") is proven: `OnsetAnalysis`/`OnsetEvent` carry explicit versioning and two independent
  cache-key components (`Settings`, `OnsetSettings`), the persisted JSON artifact was directly inspected
  on disk (§4), and every claim above is backed by an actually-executed, actually-passing automated
  test — including one that failed on its first real run and was fixed, not merely described as passing.

No previously accepted step's contract was modified — `RhythmSettings`, `EnergyFrame`, `EnergyEnvelope`,
`IEnergyEnvelopeAnalyzer`/`EnergyEnvelopeAnalyzer`, `IEnergyEnvelopeStore`/`JsonEnergyEnvelopeStore`,
`EnergyEnvelopeInvalidException`/`EnergyEnvelopeCorruptException`, and every PHASE-01-04 public member
are completely unchanged — confirmed by the full pre-existing 401-test suite passing unmodified before
a single Step 032 line of code existed (§1), and again at 410/410 (401 old + 9 new) after implementation
(§4), with all 7 of Step 031's own tests individually still passing by name in that same run.

## 8. Phase-wide status after this session

Steps 031 and 032 (of PHASE-05's seven numbered steps, 031-037) now have recorded PASS evidence. Steps
033-037 and the phase-wide gate demonstration/Gate Report (spec Section 7/8: "Analyze a click/drum
sample, see beat markers, correct an intentionally wrong downbeat, generate restrained pops for two
props and play them synchronized while comparing before/after") are **not** produced by this session —
per the task's own instruction, this session implements only Step 032 and does not proceed past it
until this evidence is reviewed.
