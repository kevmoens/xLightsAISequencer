# PHASE 05 — Step 031 Evidence

**Step:** "Implement RMS/energy envelope with baseline comparison"
**Spec:** `docs/phase-specs/PHASE-05.md` §6, Step 031 (lines 202-216)
**Date:** 2026-10-11

## 1. Pre-implementation baseline inspection

- Re-read `docs/phase-specs/PHASE-05.md` in full, not only Step 031's own numbered block: Section 2's
  architectural assumptions (timing/half-open windows, "every cache key includes algorithm/model/
  settings versions"), Section 3's architectural design notes ("Separate raw audio features from
  human-approved beat grid... Implement feature extraction windows with known FFT size, hop,
  normalization and latency correction"), Section 3's proposed `RhythmSettings(AnalysisSampleRate,
  FftSize, HopSamples)` signature, Section 4's "Store raw onset feature cache separately from
  corrected beat grids," and Step 031's own stated required acceptance ("Synthetic envelope matches
  expected energy peaks") and failure/edge case ("Invalid input, cancellation or stale persisted state
  must not corrupt the project").
- Read every PHASE-04 evidence file (`STEP-24.md` through `STEP-030.md`), treating `SongAsset`,
  `IAudioDecoder`/`WavAudioDecoder`, `WaveformEnvelope`/`WaveformPyramidCache`/`WaveformPyramidBuilder`/
  `IWaveformPyramidStore`/`JsonWaveformPyramidStore` as the immutable prior-step contracts this step
  only reads from, never modifies.
- Inspected the real source of those prior types directly (not just their evidence prose):
  - `WaveformPyramidBuilder` already computes RMS (among min/max) per fixed, non-overlapping block —
    but it is a **rendering-oriented, zoom-independent multiresolution peaks pyramid** (Section 3's
    PHASE-04 scope: "cheap to render at any zoom level"), not a rhythm-analysis feature stream keyed by
    `RhythmSettings`. Reusing it directly would conflate a display concern with a rhythm-analysis
    concern the spec explicitly separates (PHASE-05 Section 2: "Separate raw audio features from
    human-approved beat grid" — implicitly, from any other prior feature extraction too). This step's
    own real new work is therefore a parallel, PHASE-05-owned feature extractor that windows by
    `RhythmSettings.FftSize`/`HopSamples` (overlap-capable, unlike the waveform pyramid's non-overlapping
    blocks) and persists with PHASE-05's own provenance shape (`AnalyzerVersion`, `RhythmSettings`), not
    a second copy of `WaveformPyramidBuilder`'s block-RMS math.
  - `WavAudioDecoder.DecodeMonoAsync` **does not resample** — its own doc comment discloses "Resampling
    is deliberately out of scope for Step 024 itself... when the source and target rates differ this
    decoder still produces a correct mono signal at the *source* rate rather than silently mislabeling
    resampled output as the target rate." This is a real, binding constraint on `RhythmSettings.
    AnalysisSampleRate`: a value that does not match the probed `SongAsset.SampleRate` cannot actually
    be honored by the decoder today. Rather than silently decode at the wrong rate and record a
    mismatched provenance, `EnergyEnvelopeAnalyzer.AnalyzeAsync` validates this explicitly and throws
    `EnergyEnvelopeInvalidException` before any decode — a disclosed architecture decision (§3 below),
    not a silent limitation.
  - No `BeatGrid`/`OnsetEvent`/`TempoHypothesis`/`IRhythmAnalyzer` exist in the repository yet (PHASE-05
    is unstarted before this session) — confirmed via a full repository file listing before writing any
    new code.
- Baseline regression before any Step 031 code existed, run fresh in this session:
  **394/394 passing** (`dotnet test XlightsAI.Studio.slnx --nologo`), matching the PHASE-04 Step 030
  evidence's own recorded final count exactly (see `implementation-evidence/PHASE-04/STEP-030.md` §4).

## 2. Scope actually implemented

**New project `src/XlightsAI.Studio.Rhythm/XlightsAI.Studio.Rhythm.csproj`** (references `Contracts` and
`Audio` only, same `TargetFramework`/analyzer settings as every other `src/` project), added to
`XlightsAI.Studio.slnx`'s `/src/` folder:

- **`RhythmSettings.cs`** — Section 3's proposed signature, verbatim shape:
  `RhythmSettings(int AnalysisSampleRate, int FftSize, int HopSamples)`.
- **`EnergyFrame.cs`** — one windowed energy measurement: `StartSample`, `EndSample` (half-open,
  Section 2), `Rms` (raw), `NormalizedEnergy` (unit-normalized against the envelope's own peak `Rms`).
  Not a type Section 3 names explicitly — this step's own first definition of what its output actually
  contains, same convention PHASE-04 Step 026 used for `WaveformResolutionLevel`.
- **`EnergyEnvelope.cs`** — the persisted, versioned output: `VersionId`, `SongId`, `SourceHash`
  (`SongAsset.Sha256` at analysis time), `SourceDecoderVersion`, `AnalyzerVersion`, `Settings`
  (the exact `RhythmSettings` used), `Frames` (contiguous half-open coverage of `[0, SampleCount)`).
- **`IEnergyEnvelopeAnalyzer.cs`** / **`EnergyEnvelopeAnalyzer.cs`** — real implementation: decodes the
  song exactly once via the injected `IAudioDecoder`, validates `RhythmSettings` (positive `FftSize`/
  `HopSamples`, `AnalysisSampleRate == song.SampleRate`) before touching the decoder or any cache file,
  checks for a still-valid existing cache (content hash + decoder version + analyzer version + exact
  `RhythmSettings` equality) before recomputing, computes per-window RMS at the configured `FftSize`
  window / `HopSamples` hop, then unit-normalizes every frame's `Rms` against the single largest `Rms`
  in the same run (peak frame = exactly `1.0`; an all-silent run leaves every `NormalizedEnergy` at `0`,
  never an undefined `0/0`), and persists only after the full decode's sample count is verified against
  `SongAsset.SampleCount`.
- **`IEnergyEnvelopeStore.cs`** / **`JsonEnergyEnvelopeStore.cs`** — persistence boundary mirroring
  `IWaveformPyramidStore`/`JsonWaveformPyramidStore` exactly: deterministic per-`SongId` path
  (`energy-envelope-{songId:N}.json`), atomic temp-file-then-`File.Move` writes, `LoadAsync` for
  independent reopening/inspection.
- **`EnergyEnvelopeInvalidException.cs`** / **`EnergyEnvelopeCorruptException.cs`** — mirror
  `WaveformPyramidInvalidException`/`WaveformPyramidCorruptException`'s own shape and messages.

**New test project `tests/XlightsAI.Studio.Rhythm.Tests/XlightsAI.Studio.Rhythm.Tests.csproj`**
(references `Rhythm`, `Audio`, `Reference`), added to `XlightsAI.Studio.slnx`'s `/tests/` folder:

- **`Step031AcceptanceTests.cs`** — 7 tests (detailed in §4).

**No changes to any existing `Contracts`, `Audio`, `Editing`, `Xlights`, `Projects`, `Reference`,
`Shell`, or `App` file.** Purely additive: one new project, one new test project, two solution-file
entries. No prior-step contract was touched.

## 3. Architecture deviations / decisions

- **`RhythmSettings.AnalysisSampleRate` must equal the probed song's native sample rate — a hard
  validation, not a silent pass-through.** `IAudioDecoder.DecodeMonoAsync` (PHASE-04, unmodified) does
  not resample; its own doc comment discloses decoding "at the *source* rate rather than silently
  mislabeling resampled output as the target rate." Rather than build on that silent mismatch,
  `EnergyEnvelopeAnalyzer.AnalyzeAsync` throws `EnergyEnvelopeInvalidException` up front whenever
  `settings.AnalysisSampleRate != song.SampleRate` — proven by
  `AnalyzeAsync_MismatchedAnalysisSampleRate_ThrowsWithoutDecodingOrWriting_InvalidInputNegativePath`,
  which also asserts zero real decode calls occurred. A future step that adds real resampling to
  `IAudioDecoder` can relax this check without changing `EnergyEnvelopeAnalyzer`'s own contract shape.
- **This step computes only a time-domain RMS/energy feature, not yet a real FFT-based spectral
  feature**, despite the proposed field name `RhythmSettings.FftSize`. Section 3's own implementation
  guidance for *this* step is "Compute RMS and spectral features with unit normalization"; Step 032's
  own title is "Implement onset/transient detection with configurable sensitivity" and its own guidance
  names "spectral flux" specifically. Reading the two steps together, spectral-flux/onset features are
  Step 032's real new scope, and Step 031's own required acceptance text ("Synthetic envelope matches
  expected energy peaks") is satisfied by RMS/energy alone. Building a genuine FFT-based spectral
  feature now, with no onset-detection consumer yet to validate it against, would be exactly the
  "Don't add features... beyond what the task requires" / "no half-finished implementations" case this
  project's own conventions warn against. `RhythmSettings.FftSize` is kept as the proposed field name
  (reused, not renamed) so Step 032 can add a real spectral computation over the *same* window
  configuration without a breaking rename.
- **A new project (`XlightsAI.Studio.Rhythm`) rather than extending `XlightsAI.Studio.Audio`.**
  PHASE-05 Section 2's stable-domain-ownership table assigns `BeatGrid` (and, by direct extension, the
  raw feature data it is built from) to PHASE-05, distinct from PHASE-04's `SongAsset`/waveform-display
  ownership. Keeping rhythm analysis in its own project matches the existing one-phase-area-per-project
  convention (`Editing`, `Xlights`, `Projects` are each already separate from `Audio`) and keeps
  `Audio`'s own public surface (and its existing 34 passing tests) completely untouched.
- **Overlapping windows are allowed and exercised deliberately** (`FftSize=256`, `HopSamples=128` → 50%
  overlap, in every acceptance test). Section 3 only requires FFT size/hop to be "known," not
  non-overlapping; a 50% overlap is a common, defensible default for energy-envelope resolution without
  being this step's own hard-coded policy (`RhythmSettings` remains a plain injectable value, same
  "dependency not baked into the algorithm" shape as Step 030's `SnapPolicy`).
- **Reused the exact `TryLoadStillValidCacheAsync`/atomic-write/"write only after verified dimensions"
  pattern `WaveformPyramidBuilder`/`JsonWaveformPyramidStore` already established**, rather than
  inventing a new caching shape — Section 8's own "inspect the current committed code... before
  implementation" guidance, applied to find and reuse an already-proven pattern instead of a novel one.

## 4. Commands actually executed

All run via the PowerShell tool — the sandboxed POSIX Bash tool in this session is non-functional
(every invocation, including a plain `git status`, returns exit code 1 with no output), the same
pre-existing environment limitation recorded in every prior step's own evidence.

```
dotnet build XlightsAI.Studio.slnx --nologo                                                     # baseline (before any Step 031 code existed): confirmed via full test run below, 394/394
dotnet test  XlightsAI.Studio.slnx --nologo                                                      # baseline, before Step 031 code existed
dotnet build XlightsAI.Studio.slnx --nologo                                                      # after adding Rhythm/Rhythm.Tests projects + solution entries
dotnet test  tests\XlightsAI.Studio.Rhythm.Tests\XlightsAI.Studio.Rhythm.Tests.csproj --nologo --logger "console;verbosity=normal"
dotnet test  XlightsAI.Studio.slnx --nologo                                                      # full regression, final
```

### Build results

`Rhythm`, `Rhythm.Tests`, and the full solution all: `Build succeeded. 0 Warning(s). 0 Error(s).`

### Targeted test results (new tests only)

```
tests\XlightsAI.Studio.Rhythm.Tests — 7/7 passed:
  Step031_Acceptance_Passes_SyntheticEnvelopeMatchesExpectedEnergyPeaks              [ 29 ms]
  AnalyzeAsync_SilentAudio_ComputesZeroEnergyWithoutThrowing_HandlesSilenceExplicitly [ 19 ms]
  AnalyzeAsync_MismatchedAnalysisSampleRate_ThrowsWithoutDecodingOrWriting_InvalidInputNegativePath [  9 ms]
  AnalyzeAsync_DeclaredSampleCountMismatch_ThrowsEnergyEnvelopeInvalidException_NegativePath [ 11 ms]
  AnalyzeAsync_CancelledBeforeDecodeCompletes_ThrowsWithoutWritingAnyCacheFile_CancellationNegativePath [ 68 ms]
  AnalyzeAsync_ContentHashChanges_InvalidatesCacheAndRebuilds_StalePersistedStateNegativePath [152 ms]
  AnalyzeAsync_SettingsChange_InvalidatesCacheAndRebuilds_StalePersistedStateNegativePath [ 28 ms]

Test Run Successful. Total tests: 7, Passed: 7, Failed: 0.
```

### Full regression (after implementation, final run)

```
Passed!  - Failed: 0, Passed:  71, Skipped: 0, Total:  71, Duration: 324 ms - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed:  91, Skipped: 0, Total:  91, Duration: 328 ms - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed:   7, Skipped: 0, Total:   7, Duration: 429 ms - XlightsAI.Studio.Rhythm.Tests.dll
Passed!  - Failed: 0, Passed:  25, Skipped: 0, Total:  25, Duration: 2 s   - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed:  34, Skipped: 0, Total:  34, Duration: 4 s   - XlightsAI.Studio.Audio.Tests.dll
Passed!  - Failed: 0, Passed: 142, Skipped: 0, Total: 142, Duration: 4 s   - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed:  31, Skipped: 0, Total:  31, Duration: 38 s  - XlightsAI.Studio.Reference.Tests.dll
```

**401 total, 0 failed, 0 skipped** — up from the Step 030/PHASE-04-final baseline of 394 (+7 new
`Rhythm.Tests`). Every one of the 394 pre-existing tests still passes unmodified; no existing test
project's count changed (no source in `Audio`/`Editing`/`Xlights`/`Projects`/`Reference`/`Shell` was
modified this step).

### Persisted-output inspection (observable demo for this step)

Step 031's own user-visible outcome is "Versioned, inspectable project output plus a reproducible test
report" — there is no new UI screen in this step's own scope (Section 5's SCREEN 09/07 wireframes
belong to later PHASE-05 steps that display a beat grid / analysis view this step does not build). The
observable demonstration is therefore a real, running end-to-end exercise of the actual persisted
artifact, outside of the test harness's own temp-directory cleanup, run from a throwaway console program
(not part of the repository) that references the real `Rhythm`/`Audio`/`Reference` projects:

```
dotnet run --nologo   # from a throwaway console app referencing the real Rhythm/Audio/Reference projects
```

Observed real output (BPM 120 click track, 6 clicks, 8 kHz, `FftSize=256`/`HopSamples=128`):

```
Probed SongAsset: Id=c54823b3-6790-4579-8ced-4baeda40e366, Sha256=c8412ee681d1..., SampleRate=8000, SampleCount=24000
Persisted envelope at: ...\energy-cache\energy-envelope-c54823b3679045798ced4baeda40e366.json
File exists on disk: True
Frame count: 188
AnalyzerVersion: XlightsAI.Studio.Rhythm.EnergyEnvelopeAnalyzer/1.0.0
Settings: FftSize=256, HopSamples=128, AnalysisSampleRate=8000

Peak-energy frames (NormalizedEnergy >= 0.9), should line up near each 4000-sample beat boundary (0, 4000, 8000, 12000, 16000, 20000):
  [     0,    256)  NormalizedEnergy=1.00000
  [  3840,   4096)  NormalizedEnergy=0.96749
  [  3968,   4224)  NormalizedEnergy=1.00000
  [  7936,   8192)  NormalizedEnergy=1.00000
  [ 11904,  12160)  NormalizedEnergy=1.00000
  [ 15872,  16128)  NormalizedEnergy=0.99600
  [ 16000,  16256)  NormalizedEnergy=1.00000
  [ 19840,  20096)  NormalizedEnergy=0.96749
  [ 19968,  20224)  NormalizedEnergy=1.00000

Reopened envelope VersionId matches: True
Reopened frame count matches: True
```

Every one of the nine >=0.9-normalized-energy frames clusters tightly around exactly one of the six
known 4000-sample beat boundaries (0, 4000, 8000, 12000, 16000, 20000) with no spurious peak anywhere
else in the 188-frame envelope — direct, real, on-disk confirmation of "Synthetic envelope matches
expected energy peaks," independent of the unit test assertions. The full raw persisted JSON file
(`energy-envelope-*.json`) was also printed and visually inspected in this session; it round-trips
through `JsonEnergyEnvelopeStore.LoadAsync` with an identical `VersionId` and frame count, confirming
the on-disk artifact is the same object the analyzer returned, not a different or partial write.

No xLights/Avalonia UI demonstration was run for this step: Step 031 introduces no UI screen, no
`SequenceDocument`/`EditTransaction` mutation, and no xLights XML/XSQ interaction — its entire real
surface is the `IEnergyEnvelopeAnalyzer`/`IEnergyEnvelopeStore` pair proven above. Per this user's own
standing waiver (recorded in this assistant's memory) a real-xLights render check is not required as a
phase-gate blocker; it is doubly not applicable here since this step has no xLights-facing behavior to
demonstrate in the first place.

## 5. Fixtures / input hashes

No new fixture files were added to `fixtures/`. All tests and the manual demo construct real WAV bytes
via this repository's own already-licensed, already-verified `DeterministicSyntheticAudioGenerator`
(`XlightsAI.Studio.Reference.Fixtures`, PHASE-01 Step 003):

- `BuildClickTrack(bpm: 120, clickCount: 6, clickDurationSeconds: 0.02, toneHz: 1000.0, sampleRate: 8000)`
  — the primary acceptance/demo fixture: 24,000 samples (3 s), six 160-sample percussive bursts spaced
  exactly 4,000 samples (0.5 s) apart.
- A 2-second (16,000-sample) all-zero buffer for the explicit silence case.
- All test files are written under `%TEMP%\xlai-step031-*\`, deleted by each test class's `Dispose()` —
  identical cleanup convention to every prior step's acceptance test class.

## 6. Unresolved issues / known gaps

- **Only a time-domain RMS/energy feature is computed, not a real FFT-based spectral feature** — a
  disclosed, deliberate scoping decision (§3): Step 032's own stated scope ("spectral flux/energy rise
  onsets") is the real spectral-analysis work; building it now with no onset-detection consumer to
  validate it against would be premature.
- **`RhythmSettings.AnalysisSampleRate` must exactly equal the source song's native sample rate** — a
  disclosed, real constraint inherited from `IAudioDecoder.DecodeMonoAsync`'s own "resampling is
  deliberately out of scope" limitation (PHASE-04, unmodified by this step). A future step that adds
  real resampling to the decoder can relax `EnergyEnvelopeAnalyzer`'s validation without changing its
  public contract shape.
- **No UI/xLights demonstration** — disclosed above (§4): this step's own scope has no UI or xLights
  surface to demonstrate; the persisted JSON artifact is the step's entire observable output, and it was
  independently inspected (§4).
- **Latency/group-delay correction** (Section 3: "Implement feature extraction windows with known FFT
  size, hop, normalization **and latency correction**") is not implemented in this step. No onset
  detector exists yet to consume a latency-corrected timestamp, so there is nothing yet for a latency
  correction to be verified against; `EnergyFrame.StartSample`/`EndSample` are the literal, uncorrected
  sample-domain window boundaries. This is flagged as a known gap for Step 032 to address alongside its
  own onset-timestamp work, not silently assumed away.

## 7. PASS / BLOCKED

**Status: PASS** for Step 031's own stated required acceptance and failure/edge case:

- "Synthetic envelope matches expected energy peaks" — proven by
  `Step031_Acceptance_Passes_SyntheticEnvelopeMatchesExpectedEnergyPeaks` (a click-burst-overlapping
  frame carries strictly more normalized energy than a frame squarely inside a silent gap, the loudest
  frame is exactly `1.0`, every frame is in `[0,1]`) and independently confirmed by the real,
  non-test-harness demo run in §4 (all nine high-energy frames cluster exactly at the six known beat
  boundaries, no spurious peaks elsewhere in 188 frames).
- "Compare to reference numbers for synthetic tones and silence" — proven by
  `AnalyzeAsync_SilentAudio_ComputesZeroEnergyWithoutThrowing_HandlesSilenceExplicitly` (true digital
  silence produces `Rms == 0` and `NormalizedEnergy == 0`, never `NaN`, for every frame) alongside the
  click-track ("synthetic tone"-equivalent percussive fixture) case above.
- "Invalid input... must not corrupt the project" — proven by
  `AnalyzeAsync_MismatchedAnalysisSampleRate_ThrowsWithoutDecodingOrWriting_InvalidInputNegativePath`
  (zero real decode calls, no cache file written) and
  `AnalyzeAsync_DeclaredSampleCountMismatch_ThrowsEnergyEnvelopeInvalidException_NegativePath` (no cache
  file written when the song's declared dimensions don't match what actually decodes).
- "Cancellation... must not corrupt the project" — proven by
  `AnalyzeAsync_CancelledBeforeDecodeCompletes_ThrowsWithoutWritingAnyCacheFile_CancellationNegativePath`
  (a pre-cancelled token surfaces as `OperationCanceledException` with zero cache file written).
- "Stale persisted state must not corrupt the project" — proven by
  `AnalyzeAsync_ContentHashChanges_InvalidatesCacheAndRebuilds_StalePersistedStateNegativePath` and
  `AnalyzeAsync_SettingsChange_InvalidatesCacheAndRebuilds_StalePersistedStateNegativePath` (a changed
  content hash or changed `RhythmSettings` triggers a real second decode/rebuild rather than silently
  serving the old envelope).
- The step's own user-visible outcome ("Versioned, inspectable project output plus a reproducible test
  report") is proven: `EnergyEnvelope`/`EnergyFrame` carry explicit versioning (`VersionId`,
  `SourceHash`, `SourceDecoderVersion`, `AnalyzerVersion`, `Settings`), the persisted JSON artifact was
  directly inspected on disk (§4), and every claim above is backed by an actually-executed,
  actually-passing automated test.

No previously accepted step's contract was modified — `SongAsset`, `IAudioDecoder`/`WavAudioDecoder`,
`WaveformEnvelope`/`WaveformPyramidCache`/`WaveformPyramidBuilder`/`IWaveformPyramidStore`/
`JsonWaveformPyramidStore`, and every other PHASE-01-04 public member are completely unchanged —
confirmed by the full pre-existing 394-test suite passing unmodified before a single Step 031 line of
code existed (§1), and again at 401/401 (394 old + 7 new) after implementation (§4).

## 8. Phase-wide status after this session

Only Step 031 (of PHASE-05's seven numbered steps, 031-037) has recorded PASS evidence after this
session. Steps 032-037 and the phase-wide gate demonstration/Gate Report (spec Section 7/8: "Analyze a
click/drum sample, see beat markers, correct an intentionally wrong downbeat, generate restrained pops
for two props and play them synchronized while comparing before/after") are **not** produced by this
session — per the task's own instruction, this session implements only Step 031 and does not proceed
past it until this evidence is reviewed.
