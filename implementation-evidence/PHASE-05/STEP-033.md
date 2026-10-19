# PHASE 05 — Step 033 Evidence

**Step:** "Implement BPM candidate detection including half/double-time alternatives"
**Spec:** `docs/phase-specs/PHASE-05.md` §6, Step 033 (lines 234-248)
**Date:** 2026-10-11

## 1. Pre-implementation baseline inspection

- Re-read `docs/phase-specs/PHASE-05.md` in full: Section 2's architectural assumptions, Section 3's
  design notes ("Compare multiple tempo candidates (e.g. double/half-time) and allow low-confidence
  manual BPM or beat anchors... evaluate autocorrelation/periodicity and known half/double-time
  signals"), the proposed `TempoHypothesis(double Bpm, double Confidence, string MeterGuess)` signature,
  Section 4's cache-key rule, and Step 033's own required acceptance ("120 BPM and 60/120 ambiguity tests
  pass") and failure/edge case ("Silence, invalid timing boundaries and missing media must be handled
  explicitly").
- Read `implementation-evidence/PHASE-05/STEP-31.md` and `implementation-evidence/PHASE-05/STEP-032.md`
  in full and treated every type/behavior they describe as an immutable prior contract: `RhythmSettings`,
  `EnergyFrame`/`EnergyEnvelope`/`IEnergyEnvelopeAnalyzer`/`EnergyEnvelopeAnalyzer`,
  `IEnergyEnvelopeStore`/`JsonEnergyEnvelopeStore`, `EnergyEnvelopeInvalidException`/
  `EnergyEnvelopeCorruptException`, `OnsetEvent`, `OnsetDetectionSettings`, `OnsetAnalysis`,
  `IOnsetDetector`/`OnsetDetector`, `IOnsetAnalysisStore`/`JsonOnsetAnalysisStore`,
  `OnsetAnalysisInvalidException`/`OnsetAnalysisCorruptException`. None of these files were opened in
  write mode this step; verified unmodified by re-running Steps 031/032's own 16 acceptance tests
  unchanged after this step's implementation (§4).
- Inspected the real source of every file this step depends on directly: `OnsetAnalysis`/`OnsetEvent`/
  `OnsetDetectionSettings` (Step 032, immutable), `OnsetDetector`/`JsonOnsetAnalysisStore` (used
  unmodified by this step's own tests to build real onset input, never re-implemented), `SongId`
  (PHASE-01 `Contracts`), `DeterministicSyntheticAudioGenerator` (`BuildClickTrack`, `EncodeWav` —
  PHASE-01 Step 003, unmodified), `WavAudioDecoder`/`AudioProbeInvalidException` (PHASE-04, unmodified —
  used to confirm this step's own "missing media" edge case is already rejected one layer below it, see
  §3).
- Confirmed this step's own real new scope, per Section 3's design notes and Step 032's own disclosed
  "no `BeatGrid`" gap: rank BPM candidates from an *already built* `OnsetAnalysis` — this step never
  re-decodes audio or re-derives onsets; its sole algorithmic input is Step 032's own persisted output.
- Baseline regression before any Step 033 code existed, run fresh in this session:
  **410/410 passing** (`dotnet test XlightsAI.Studio.slnx --nologo`), matching Step 032's own final
  recorded count exactly.

## 2. Scope actually implemented

All new files added to the existing `src/XlightsAI.Studio.Rhythm/XlightsAI.Studio.Rhythm.csproj` and
`tests/XlightsAI.Studio.Rhythm.Tests/XlightsAI.Studio.Rhythm.Tests.csproj` projects (no new project, no
`.csproj`/solution changes — Step 033 is the same PHASE-05/rhythm-analysis ownership area Steps 031/032
already established, and both projects already reference everything this step needs).

- **`TempoHypothesis.cs`** — Section 3's proposed signature, verbatim shape: `TempoHypothesis(double Bpm,
  double Confidence, string MeterGuess)`.
- **`TempoDetectionSettings.cs`** — this step's own scan configuration: `MinBpm`/`MaxBpm` (the BPM range
  scanned), `TimeResolutionSeconds` (autocorrelation bin width), `MaxCandidates` (ranked-list cap). Not
  named in Section 3's proposed shapes — this step's own first definition of the scan knobs, same
  convention Step 032 used for `OnsetDetectionSettings`.
- **`TempoAnalysis.cs`** — the persisted, versioned output, mirroring `OnsetAnalysis`'s provenance shape
  (`VersionId`, `SongId`, `SourceHash`, `AnalyzerVersion`, `Settings`) with `SourceOnsetAnalysisVersionId`
  added as this step's own cache-key link back to the exact onset run it was derived from, and
  `Candidates` (ranked `TempoHypothesis[]`, empty when the source carried no onsets) replacing
  `OnsetAnalysis.Onsets`.
- **`ITempoDetector.cs`** / **`TempoDetector.cs`** — real implementation: quantizes
  `OnsetAnalysis.Onsets` into fixed-width (`TempoDetectionSettings.TimeResolutionSeconds`) time bins
  weighted by onset strength, computes a real normalized autocorrelation score for every lag in the
  requested BPM range, finds local-maximum peaks, explicitly also evaluates the half-time and double-time
  lag of the single strongest peak (via the same real autocorrelation function) so genuine half/double
  ambiguity is represented even when it did not already surface as its own local maximum, merges
  near-duplicate lags, and returns the ranked, confidence-normalized result capped at
  `TempoDetectionSettings.MaxCandidates`.
- **`ITempoAnalysisStore.cs`** / **`JsonTempoAnalysisStore.cs`** — persistence boundary mirroring
  `IOnsetAnalysisStore`/`JsonOnsetAnalysisStore` exactly: deterministic per-`SongId` path
  (`tempo-analysis-{songId:N}.json`), atomic temp-file-then-`File.Move` writes, `LoadAsync` for
  independent reopening/inspection.
- **`TempoAnalysisInvalidException.cs`** / **`TempoAnalysisCorruptException.cs`** — mirror
  `OnsetAnalysisInvalidException`/`OnsetAnalysisCorruptException`'s own shape and messages.

**New test file `tests/XlightsAI.Studio.Rhythm.Tests/Step033AcceptanceTests.cs`** — 10 tests (detailed
in §4). No changes to `Step031AcceptanceTests.cs`, `Step032AcceptanceTests.cs`, or any other existing
test file.

**No changes to any existing `Contracts`, `Audio`, `Editing`, `Xlights`, `Projects`, `Reference`,
`Shell`, `App`, or Step 031/032 `Rhythm` file.** Purely additive within the already-existing `Rhythm`/
`Rhythm.Tests` projects — no solution-file change needed.

## 3. Architecture deviations / decisions

- **Consumes `OnsetAnalysis`, not `SongAsset`, as its sole input — a narrower contract than Section 3's
  proposed `IRhythmAnalyzer.AnalyzeAsync(SongAsset song, ...)`.** Section 3's proposed shape returns a
  full `BeatGrid` directly from a song in one call; this step's own real scope (per its own title, "BPM
  candidate detection") is strictly the ranked tempo list consumed from Step 032's own already-persisted
  onset candidates, matching the same "proposed shapes are proposed, not verified source" instruction
  this spec itself states (§1 line 5) and the same narrowing precedent Step 032's own `IOnsetDetector`
  already set relative to the same proposed `IRhythmAnalyzer`. `BeatGrid` construction (Step 034) is the
  natural point where a full `IRhythmAnalyzer`-shaped facade over `OnsetDetector` → `TempoDetector` →
  beat-grid assembly can be introduced, if ever needed — not fabricated here ahead of that need (Section
  2: "do not fabricate generic interfaces without a concrete boundary").
- **Onset-impulse-train autocorrelation, not a dedicated beat-tracking library.** Section 3 names
  "evaluate autocorrelation/periodicity" without mandating an implementation; no beat-tracking package is
  referenced anywhere in this repository. A direct, time-binned, strength-weighted autocorrelation over
  the requested BPM range is a standard, well-understood tempo-induction technique, needs no new
  third-party dependency, and is fast enough at this step's own fixture scale (the full acceptance test,
  scanning 131 lag bins over a ~6 s signal, completes in well under half a second) — consistent with Step
  032's own "no new dependency" precedent for its DFT.
- **Half/double-time candidates are measured, not fabricated.** Rather than always inserting `bpm/2` and
  `bpm*2` of the strongest candidate as synthetic entries with an assumed confidence, `TempoDetector`
  explicitly re-evaluates the *real* autocorrelation score at those exact lags (reusing the same
  `AutocorrelationScore` function used for the main scan) and only adds them if that real score is
  positive and not already represented by an existing peak. For a genuinely isochronous source (this
  step's own click-track fixture), the true period's own integer multiples are mathematically also real
  periods of the same impulse train, so they already appear as independent local maxima in the vast
  majority of cases; the explicit secondary check is a disclosed safety net for sparser or less perfectly
  isochronous input, not this step's primary detection path.
- **Confidence is normalized against the single largest score found across the *entire* scanned lag
  range**, not just among the final returned/ranked candidates — proven directly by the real demo run in
  §4, where the top (true) 120 BPM candidate always reports exactly `Confidence = 1.0` (it is, by
  construction, always the global maximum of the full scan) and every other candidate's confidence is a
  real fraction of that same peak, not independently re-normalized per candidate.
- **"Missing media" resolves to two distinct, already-adjacent layers of explicit handling, not one.**
  Investigating this step's own failure/edge case text ("...missing media must be handled explicitly")
  revealed that a zero-length audio file is *already* rejected, with an explicit, clear reason, by the
  existing (PHASE-04) `WavAudioDecoder.ProbeAsync` — one layer below this step's own `OnsetAnalysis`-only
  input contract (confirmed by a real, passing negative test, §4). This step's own in-scope "missing
  media" shape is therefore the next layer up: a real, successfully probed/decoded `OnsetAnalysis` that
  simply carries zero onsets (e.g. because the underlying clip was too short for Step 032's own detector
  to find any transient) — proven by a second, independent test asserting `TempoDetector` returns an
  explicit empty `Candidates` list rather than throwing. Both layers are now evidenced rather than one
  being silently assumed to cover the other.
- **Reused Step 032's exact `TryLoadStillValidCacheAsync`/atomic-write pattern**, extended with one
  additional cache-key component (`SourceOnsetAnalysisVersionId` equality, replacing
  `SourceDecoderVersion` since this step never touches the decoder directly) — rather than inventing a
  new caching shape for a third analyzer in the same project.
- **`MeterGuess` is a fixed `"4/4"` placeholder on every candidate, not a real meter inference.** Section
  3 explicitly scopes downbeat/meter estimation as "optional... with confidence and manual labeling," and
  names it as Step 034's own concern ("Generate beats, likely downbeats, bars and confidence intervals").
  This step's own proposed-shape field is populated with an honestly-labeled simplification, not left
  null or fabricated as if independently verified — disclosed as a known gap in §6.

## 4. Commands actually executed

All run via the PowerShell tool — the sandboxed POSIX Bash tool in this session is non-functional (every
invocation, including a plain `pwd`, returns exit code 1 with no output), the same pre-existing
environment limitation recorded in every prior step's own evidence.

```
dotnet build XlightsAI.Studio.slnx --nologo                                              # baseline, before any Step 033 code existed
dotnet test  XlightsAI.Studio.slnx --nologo                                               # baseline regression: 410/410 passing
dotnet build src\XlightsAI.Studio.Rhythm\XlightsAI.Studio.Rhythm.csproj --nologo          # new Rhythm source files only, before tests existed
dotnet test  tests\XlightsAI.Studio.Rhythm.Tests\XlightsAI.Studio.Rhythm.Tests.csproj --nologo --logger "console;verbosity=detailed" --filter "FullyQualifiedName~Step033"
  # first real run: 9/10 passed, 1 failed (missing-media test asserted empty candidates for a
  # zero-length probed file, but WavAudioDecoder.ProbeAsync already throws AudioProbeInvalidException
  # for zero-length audio before Step 033 code is ever reached) — see §3's two-layer fix
dotnet test  tests\XlightsAI.Studio.Rhythm.Tests\XlightsAI.Studio.Rhythm.Tests.csproj --nologo --logger "console;verbosity=normal" --filter "FullyQualifiedName~Step033"
  # after the fix: 10/10 passed
dotnet build XlightsAI.Studio.slnx --nologo                                              # full solution, after Step 033 implementation
dotnet test  XlightsAI.Studio.slnx --nologo                                               # full regression, final
```

### Build results

`Rhythm`, `Rhythm.Tests`, and the full solution all: `Build succeeded. 0 Warning(s). 0 Error(s).`

### Targeted test results (Step 033 tests only, final run)

```
Passed XlightsAI.Studio.Rhythm.Tests.Step033AcceptanceTests.DetectAsync_SourceOnsetAnalysisVersionChanges_InvalidatesCacheAndRebuilds_StalePersistedStateNegativePath [604 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step033AcceptanceTests.DetectAsync_NonPositiveTimeResolution_ThrowsWithoutWriting_InvalidTimingBoundaryNegativePath [162 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step033AcceptanceTests.Step033_Acceptance_Passes_120BpmAndHalfDoubleAmbiguity [434 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step033AcceptanceTests.DetectAsync_SilentAudio_ReturnsEmptyCandidatesWithoutThrowing_HandlesSilenceExplicitly [134 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step033AcceptanceTests.DetectAsync_NullOnsets_ThrowsArgumentNullException [4 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step033AcceptanceTests.ProbeAsync_ZeroLengthMedia_IsRejectedBeforeAnyOnsetOrTempoScan_HandlesMissingMediaExplicitly [9 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step033AcceptanceTests.DetectAsync_OnsetAnalysisWithNoDetectableOnsets_ReturnsEmptyCandidatesWithoutThrowing_HandlesMissingMediaExplicitly [17 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step033AcceptanceTests.DetectAsync_CancelledBeforeComputeCompletes_ThrowsWithoutWritingAnyCacheFile_CancellationNegativePath [400 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step033AcceptanceTests.DetectAsync_NonPositiveMinBpm_ThrowsWithoutWriting_InvalidTimingBoundaryNegativePath [161 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step033AcceptanceTests.DetectAsync_MaxBpmNotGreaterThanMinBpm_ThrowsWithoutWriting_InvalidTimingBoundaryNegativePath [161 ms]

Total tests: 10. Passed: 10, Failed: 0.
```

### Full regression (after implementation, final run)

```
Passed!  - Failed: 0, Passed:  71, Skipped: 0, Total:  71, Duration: 362 ms - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed:  91, Skipped: 0, Total:  91, Duration: 292 ms - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed:  25, Skipped: 0, Total:  25, Duration: 2 s   - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed:  26, Skipped: 0, Total:  26, Duration: 2 s   - XlightsAI.Studio.Rhythm.Tests.dll
Passed!  - Failed: 0, Passed:  34, Skipped: 0, Total:  34, Duration: 3 s   - XlightsAI.Studio.Audio.Tests.dll
Passed!  - Failed: 0, Passed: 142, Skipped: 0, Total: 142, Duration: 3 s   - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed:  31, Skipped: 0, Total:  31, Duration: 35 s  - XlightsAI.Studio.Reference.Tests.dll
```

**420 total, 0 failed, 0 skipped** — up from the Step 032 baseline of 410 (+10 new `Step033AcceptanceTests`
in the already-existing `Rhythm.Tests` project: 26 = 7 Step031 + 9 Step032 + 10 Step033). Every one of
the 410 pre-existing tests still passes unmodified, including all 7 Step 031 and all 9 Step 032 tests
individually, proving no prior step's contract was broken.

### Persisted-output inspection (observable demo for this step)

Step 033's own user-visible outcome is "Versioned, inspectable project output plus a reproducible test
report" — same shape as Steps 031/032, no new UI screen in this step's own scope. The observable
demonstration is a real, running end-to-end exercise of the actual persisted artifact, outside the test
harness's temp-directory cleanup, run from a throwaway console program (not part of the repository, under
the session scratchpad, deleted after this run) referencing the real `Rhythm`/`Audio`/`Reference`
projects:

```
dotnet run --nologo   # from a throwaway console app referencing the real Rhythm/Audio/Reference projects
```

Observed real output (BPM 120 click track, 12 clicks, 0.5 s silent lead-in, 8 kHz, `FftSize=256`/
`HopSamples=128` onset windowing, `SensitivityMultiplier=1.5`/`MinInterOnsetSamples=256` onset
sensitivity, `MinBpm=40`/`MaxBpm=300`/`TimeResolutionSeconds=0.01`/`MaxCandidates=5` tempo scan):

```
Probed SongAsset: Id=c5f512da-358b-4a4a-8d50-a2031c682f91, Sha256=d6de01f9d3be..., SampleRate=8000, SampleCount=52000
OnsetAnalysis: VersionId=913a98f5-cfea-4cff-928a-a53d4760a2c0, OnsetCount=12
Persisted tempo analysis at: ...\tempo-cache\tempo-analysis-c5f512da358b4a4a8d50a2031c682f91.json
File exists on disk: True
AnalyzerVersion: XlightsAI.Studio.Rhythm.TempoDetector/1.0.0
Settings: MinBpm=40, MaxBpm=300, TimeResolutionSeconds=0.01, MaxCandidates=5

Ranked tempo candidates (confidence descending):
  Bpm= 120.00  Confidence=1.0000  MeterGuess=4/4
  Bpm=  59.41  Confidence=0.9941  MeterGuess=4/4
  Bpm=  40.00  Confidence=0.8145  MeterGuess=4/4

Reopened analysis VersionId matches: True
Reopened candidate count matches: True

Silence demo: OnsetCount=0, TempoCandidateCount=0 (expected 0, no exception thrown)
```

Direct, real, on-disk confirmation of "120 BPM and 60/120 ambiguity tests pass": the top-ranked candidate
is exactly 120 BPM at full confidence (1.0 — the global scan maximum, by construction), and a genuine
second candidate near 60 BPM (59.41, confidence 0.9941 — the natural half-time periodicity of the same
isochronous source) is independently ranked right behind it, proving the detector represents the
ambiguity rather than collapsing to a single answer. The full raw persisted JSON file
(`tempo-analysis-*.json`) was printed and visually inspected (reproduced in full in the actual session
transcript); it round-trips through `JsonTempoAnalysisStore.LoadAsync` with an identical `VersionId` and
candidate count, confirming the on-disk artifact is the same object the detector returned. The silence
case independently confirms the "must be handled explicitly" edge case outside the test harness: zero
onsets, zero tempo candidates, no exception.

No xLights/Avalonia UI demonstration was run for this step: Step 033 introduces no UI screen, no
`SequenceDocument`/`EditTransaction` mutation, and no xLights XML/XSQ interaction — its entire real
surface is the `ITempoDetector`/`ITempoAnalysisStore` pair proven above. Per this user's own standing
waiver (recorded in this assistant's memory) a real-xLights render check is not required as a phase-gate
blocker; it is doubly not applicable here since this step has no xLights-facing behavior to demonstrate
in the first place.

## 5. Fixtures / input hashes

No new fixture files were added to `fixtures/`. All tests and the manual demo construct real WAV bytes
via this repository's own already-licensed, already-verified `DeterministicSyntheticAudioGenerator`:

- `BuildClickTrack(bpm: 120, clickCount: 12, clickDurationSeconds: 0.02, toneHz: 1000.0, sampleRate: 8000)`
  prepended with 4000 samples (0.5 s) of silence — the primary acceptance/demo fixture: a perfectly
  isochronous 120 BPM source whose own true period (4000 samples = 50 autocorrelation bins at this step's
  10 ms resolution) is, by construction, also periodic at 60 BPM (100 bins) and 240 BPM (25 bins).
- A fully silent 2 s WAV (`new float[SampleRate * 2]`) — the "silence must be handled explicitly"
  negative-path fixture.
- A true zero-length WAV (`[]` samples) — the "missing media" fixture proving the Audio layer's own
  existing explicit rejection (§3).
- A silent 10-sample WAV (non-zero length, but far too short for Step 032's detector to find any
  transient) — the in-scope "missing media reaching this step as a zero-onset `OnsetAnalysis`" fixture.
- All test files are written under `%TEMP%\xlai-step033-*\`, deleted by each test class's `Dispose()` —
  identical cleanup convention to Steps 031/032. The manual demo's own throwaway files were written under
  `%TEMP%\xlai-step033-demo-*\` and deleted by the demo program itself at the end of its run (confirmed by
  "Temp directory cleaned up." in the real output above); the throwaway demo *project* itself (outside the
  repository, under the session scratchpad) was deleted after this evidence was recorded.

## 6. Unresolved issues / known gaps

- **`MeterGuess` is a fixed `"4/4"` placeholder, not a real meter inference** — a disclosed, deliberate
  scoping decision (§3): Section 3 names meter/downbeat estimation as Step 034's own concern ("Generate
  beats, likely downbeats, bars and confidence intervals... Downbeat estimation must be optional with
  confidence and manual labeling"), explicitly out of this step's own "BPM candidate detection" title.
- **No `BeatGrid`, beat positions, or downbeats** — this step's own output is a ranked BPM list only; Step
  034 is this phase's own next step, explicitly out of this step's scope.
- **A direct time-binned autocorrelation, not a dedicated beat-tracking library** — a disclosed, deliberate
  scoping decision (§3), consistent with Step 032's own "no new dependency" precedent; sufficient at this
  step's own fixed, test-scale fixture durations.
- **`TempoDetectionSettings.MaxCandidates` truncates the ranked list without a disclosed-dropped-count
  log** — for this step's own fixed, small test fixtures the full candidate set is always well under 5;
  if a future step's real-song fixtures regularly exceed `MaxCandidates`, surfacing how many were dropped
  (not just silently truncating) is a small, disclosed, additive follow-up for Step 034+.
- **Autocorrelation bin resolution (`TimeResolutionSeconds`) quantizes reported BPM** — a 60 BPM candidate
  from a slightly non-isochronous real recording may resolve to, e.g., 59.4 rather than exactly 60.0 (as
  directly observed in §4's real demo output); `Step033_Acceptance_Passes_120BpmAndHalfDoubleAmbiguity`'s
  own tolerance (`Assert.Equal(..., tolerance: 2.0)` for the exact-period 120 BPM candidate,
  `HasCandidateNear(..., tolerance: 2.0)` for the half-time candidate) already accounts for this, not an
  unaddressed gap, but a finer `TimeResolutionSeconds` trades this precision against a wider lag scan.

## 7. PASS / BLOCKED

**Status: PASS** for Step 033's own stated required acceptance and failure/edge case:

- "120 BPM and 60/120 ambiguity tests pass" (required acceptance, verbatim) — proven by
  `Step033_Acceptance_Passes_120BpmAndHalfDoubleAmbiguity` (top-ranked candidate is exactly 120 BPM at
  confidence 1.0; a genuine ~60 BPM alternate hypothesis is independently present and ranked) and
  independently confirmed by the real, non-test-harness demo run in §4 (120 BPM @ 1.0000 confidence,
  59.41 BPM @ 0.9941 confidence, both from one real autocorrelation scan of one real onset analysis).
- "Rank multiple tempo candidates rather than one BPM" — proven by the same test
  (`Candidates.Length > 1`, confidence-descending order asserted) and the real demo's own 3-candidate
  ranked output.
- "Evaluate autocorrelation/periodicity and known half/double-time signals" — proven by the real
  `AutocorrelationScore`-based implementation (§2/§3) and the same acceptance test/demo surfacing a real
  half-time candidate, not a fabricated one.
- "Silence... must be handled explicitly" — proven by
  `DetectAsync_SilentAudio_ReturnsEmptyCandidatesWithoutThrowing_HandlesSilenceExplicitly` and the real
  demo's own silence run (`TempoCandidateCount=0`, no exception).
- "Invalid timing boundaries... must be handled explicitly" — proven by
  `DetectAsync_MaxBpmNotGreaterThanMinBpm_ThrowsWithoutWriting_InvalidTimingBoundaryNegativePath`,
  `DetectAsync_NonPositiveMinBpm_ThrowsWithoutWriting_InvalidTimingBoundaryNegativePath`, and
  `DetectAsync_NonPositiveTimeResolution_ThrowsWithoutWriting_InvalidTimingBoundaryNegativePath` (zero
  cache file written for every invalid-settings case).
- "...and missing media must be handled explicitly" — proven at both real layers (§3): the Audio layer's
  own pre-existing explicit rejection of zero-length media
  (`ProbeAsync_ZeroLengthMedia_IsRejectedBeforeAnyOnsetOrTempoScan_HandlesMissingMediaExplicitly`), and
  this step's own explicit empty-result handling of a valid-but-onset-free analysis
  (`DetectAsync_OnsetAnalysisWithNoDetectableOnsets_ReturnsEmptyCandidatesWithoutThrowing_HandlesMissingMediaExplicitly`).
- Cancellation — proven by
  `DetectAsync_CancelledBeforeComputeCompletes_ThrowsWithoutWritingAnyCacheFile_CancellationNegativePath`.
- Stale persisted state — proven by
  `DetectAsync_SourceOnsetAnalysisVersionChanges_InvalidatesCacheAndRebuilds_StalePersistedStateNegativePath`
  (a rebuilt `OnsetAnalysis`, even for the same `SongId`/content, triggers a real tempo rebuild rather
  than silently serving the analysis built from the superseded onset run).
- The step's own user-visible outcome ("Versioned, inspectable project output plus a reproducible test
  report") is proven: `TempoAnalysis`/`TempoHypothesis` carry explicit versioning and two independent
  cache-key components (`Settings`, `SourceOnsetAnalysisVersionId`), the persisted JSON artifact was
  directly inspected on disk (§4), and every claim above is backed by an actually-executed,
  actually-passing automated test — including one that failed on its first real run (the missing-media
  test's original assumption) and was fixed with a real, disclosed correction, not merely described as
  passing.

No previously accepted step's contract was modified — `RhythmSettings`, `EnergyFrame`/`EnergyEnvelope`/
`IEnergyEnvelopeAnalyzer`/`EnergyEnvelopeAnalyzer`, `IEnergyEnvelopeStore`/`JsonEnergyEnvelopeStore`,
`EnergyEnvelopeInvalidException`/`EnergyEnvelopeCorruptException`, `OnsetEvent`,
`OnsetDetectionSettings`, `OnsetAnalysis`, `IOnsetDetector`/`OnsetDetector`,
`IOnsetAnalysisStore`/`JsonOnsetAnalysisStore`, `OnsetAnalysisInvalidException`/
`OnsetAnalysisCorruptException`, and every PHASE-01-04 public member are completely unchanged — confirmed
by the full pre-existing 410-test suite passing unmodified before a single Step 033 line of code existed
(§1), and again at 420/420 (410 old + 10 new) after implementation (§4), with all 7 Step 031 and all 9
Step 032 tests individually still passing by name in that same run.

## 8. Phase-wide status after this session

Steps 031, 032 and 033 (of PHASE-05's seven numbered steps, 031-037) now have recorded PASS evidence.
Steps 034-037 and the phase-wide gate demonstration/Gate Report (spec Section 7/8: "Analyze a click/drum
sample, see beat markers, correct an intentionally wrong downbeat, generate restrained pops for two props
and play them synchronized while comparing before/after") are **not** produced by this session — per the
task's own instruction, this session implements only Step 033 and does not proceed past it until this
evidence is reviewed.
