# PHASE 05 — Step 034 Evidence

**Step:** "Generate beats, likely downbeats, bars and confidence intervals"
**Spec:** `docs/phase-specs/PHASE-05.md` §6, Step 034 (lines 250-264)
**Date:** 2026-10-11

## 1. Pre-implementation baseline inspection

- Re-read `docs/phase-specs/PHASE-05.md` in full: Section 2's architectural assumptions (canonical
  monotonic clock, half-open `[start,end)` time scopes, "Separate raw audio features from
  human-approved beat grid"), Section 3's design notes and proposed `BeatMarker`/`BeatGrid`
  signatures, Section 4's data-contract rule ("`BeatGrid` references its audio hash, analyzer
  versions and user correction version"), and Step 034's own required acceptance ("Tempo fixture
  aligns events within configured threshold"), implementation guidance ("Represent beats with
  absolute ticks and bar indices; meter must be user correctable; show confidence and avoid
  asserting uncertainty away"), and failure/edge case ("Silence, invalid timing boundaries and
  missing media must be handled explicitly").
- Read `implementation-evidence/PHASE-05/STEP-31.md`, `STEP-032.md`, and `STEP-033.md` in full and
  treated every type/behavior they describe as an immutable prior contract: `RhythmSettings`,
  `EnergyFrame`/`EnergyEnvelope`/`IEnergyEnvelopeAnalyzer`/`EnergyEnvelopeAnalyzer`,
  `IEnergyEnvelopeStore`/`JsonEnergyEnvelopeStore`, `OnsetEvent`, `OnsetDetectionSettings`,
  `OnsetAnalysis`, `IOnsetDetector`/`OnsetDetector`, `IOnsetAnalysisStore`/`JsonOnsetAnalysisStore`,
  `TempoHypothesis`, `TempoDetectionSettings`, `TempoAnalysis`, `ITempoDetector`/`TempoDetector`,
  `ITempoAnalysisStore`/`JsonTempoAnalysisStore`, and every exception type those steps defined. None
  of these files were opened in write mode this step; verified unmodified by re-running Steps
  031-033's own 26 acceptance tests unchanged after this step's implementation (§4).
- Inspected the real source of every file this step depends on directly: `TempoAnalysis`/
  `TempoHypothesis`/`TempoDetector` (Step 033, immutable — `TempoDetector.DetectAsync` always
  returns `Candidates` ranked confidence-descending, and an empty list exactly when the source
  `OnsetAnalysis.Onsets` is empty, a real invariant this step relies on rather than re-validates),
  `OnsetAnalysis`/`OnsetEvent` (Step 032, immutable), `WavAudioDecoder`/`AudioProbeInvalidException`
  (PHASE-04, unmodified — confirms this step's own "missing media" edge case is already rejected
  one layer below, as Step 033 already established), `SongId`/`TimelineTick`/`TimeWindow`
  (`Studio.Contracts`, PHASE-01).
- Confirmed this step's own real new scope, per Section 3's design notes and Step 033's own
  disclosed "no `BeatGrid`" gap: generate a beat/bar grid from an *already ranked* `TempoAnalysis`
  against its source `OnsetAnalysis` — this step never re-decodes audio, never re-derives onsets,
  and never re-ranks tempo candidates.
- Baseline regression before any Step 034 code existed, run fresh in this session:
  **420/420 passing** (`dotnet test XlightsAI.Studio.slnx --nologo`), matching Step 033's own final
  recorded count exactly (71 Xlights + 91 Editing + 25 Projects + 26 Rhythm + 34 Audio + 142 Shell +
  31 Reference).

## 2. Scope actually implemented

All new files added to the existing `src/XlightsAI.Studio.Rhythm/XlightsAI.Studio.Rhythm.csproj` and
`tests/XlightsAI.Studio.Rhythm.Tests/XlightsAI.Studio.Rhythm.Tests.csproj` projects (no new project,
no `.csproj`/solution changes — both projects already reference everything this step needs; the
SDK-style `csproj` uses default globbing, so new files are picked up automatically).

- **`BeatMarker.cs`** — Section 3's proposed signature (`Id`, `TimeTicks`, `BeatWithinBar`,
  `Confidence`, `UserCorrected`) with one disclosed addition, **`BarIndex`** — Step 034's own
  implementation guidance, verbatim: "Represent beats with absolute ticks and bar indices." Section
  1 explicitly states proposed signatures are "proposed API intent," not a verified, unmodifiable
  shape, and Section 8's own ADR process calls for recording exactly this kind of compatibility
  decision (§3 below). `BeatWithinBar == 0` is the documented "likely downbeat" convention.
- **`BeatGridSettings.cs`** — this step's own generation configuration: `BeatsPerBar` (the meter
  numerator — Step 034's own "meter must be user correctable" is satisfied by this being a plain,
  replaceable setting, not a hardcoded constant) and `AlignmentToleranceTicks` (the "configured
  threshold" this step's own required acceptance names verbatim).
- **`BeatGrid.cs`** — Section 3's proposed signature (`VersionId`, `Beats`, `Candidates`,
  `SourceHash`), extended with the same provenance convention Steps 032/033 already established
  (`SongId`, `SourceOnsetAnalysisVersionId`, `SourceTempoAnalysisVersionId`, `AnalyzerVersion`,
  `Settings`), plus `SelectedTempo` (the single candidate this grid's spacing was generated from —
  nullable, for the no-viable-tempo case) and `CorrectionVersion` (reserved, always `0` this step —
  see §3). `Beats` is `BeatMarker[]`, `Candidates` is `TempoHypothesis[]` carried through from the
  source `TempoAnalysis` unchanged.
- **`IBeatGridBuilder.cs`** / **`BeatGridBuilder.cs`** — real implementation: validates
  `BeatGridSettings`/the `OnsetAnalysis`↔`TempoAnalysis` pairing, selects the top-ranked
  `TempoHypothesis` from `TempoAnalysis.Candidates`, and generates an evenly spaced beat grid
  anchored to the earliest onset, with a phase-lock resync against real onsets and a two-band
  confidence model (both detailed in §3). Mirrors Step 033's exact
  `TryLoadStillValidCacheAsync`/atomic-write caching pattern, extended with a second source-version
  cache-key component (`SourceTempoAnalysisVersionId`, alongside `SourceOnsetAnalysisVersionId`).
- **`IBeatGridStore.cs`** / **`JsonBeatGridStore.cs`** — persistence boundary mirroring
  `ITempoAnalysisStore`/`JsonTempoAnalysisStore` exactly: deterministic per-`SongId` path
  (`beat-grid-{songId:N}.json`), atomic temp-file-then-`File.Move` writes, `LoadAsync` for
  independent reopening/inspection.
- **`BeatGridInvalidException.cs`** / **`BeatGridCorruptException.cs`** — mirror
  `TempoAnalysisInvalidException`/`TempoAnalysisCorruptException`'s own shape and messages.

**New test file `tests/XlightsAI.Studio.Rhythm.Tests/Step034AcceptanceTests.cs`** — 11 tests
(detailed in §4). No changes to `Step031AcceptanceTests.cs`, `Step032AcceptanceTests.cs`,
`Step033AcceptanceTests.cs`, or any other existing test file.

**No changes to any existing `Contracts`, `Audio`, `Editing`, `Xlights`, `Projects`, `Reference`,
`Shell`, `App`, or Step 031/032/033 `Rhythm` file.** Purely additive within the already-existing
`Rhythm`/`Rhythm.Tests` projects.

## 3. Architecture deviations / decisions

- **`IBeatGridBuilder.BuildAsync(OnsetAnalysis, TempoAnalysis, ...)`, not Section 3's proposed
  `IRhythmAnalyzer.AnalyzeAsync(SongAsset, ...)`.** Same deliberate narrowing precedent Step 033's
  own `ITempoDetector` already set relative to the same proposed `IRhythmAnalyzer` (see that step's
  own evidence, §3): this step's real scope is strictly "generate a beat grid from already-ranked
  tempo candidates," not "analyze a song end to end in one call." A future `IRhythmAnalyzer` facade
  over `OnsetDetector → TempoDetector → BeatGridBuilder` is the natural integration point if ever
  needed, not fabricated here ahead of that need (Section 2: "do not fabricate generic interfaces
  without a concrete boundary").
- **`BeatMarker` gains a `BarIndex` field beyond Section 3's proposed shape.** Step 034's own
  implementation guidance explicitly requires "bar indices," which the proposed `BeatMarker` shape
  (`Id, TimeTicks, BeatWithinBar, Confidence, UserCorrected`) does not carry on its own —
  `BeatWithinBar` alone cannot reconstruct an absolute bar number without re-deriving it from a
  beat's own array position, which is fragile once Step 035 allows individual beats to be inserted/
  removed. Declared explicitly now rather than silently assumed.
- **`BeatGrid.CorrectionVersion` is declared now, always `0`, not retrofitted in Step 035.** Section
  4's own data-contract text ("`BeatGrid` references its audio hash, analyzer versions and user
  correction version") describes this as part of `BeatGrid`'s whole-of-phase contract, and Section 2
  requires treating a completed step's contract as immutable unless the next step has "an accepted
  migration plan." Adding a required field to a `sealed record` after Step 035 already depends on
  today's shape would itself be exactly this kind of migration; declaring the field now (unused,
  always `0`, clearly documented as "reserved for Step 035") avoids that entirely. This is a
  deliberate, narrow exception to this project's own "don't add features for hypothetical future
  requirements" principle, justified because the spec itself (not a guess) names this field as part
  of this type's contract.
- **Selects only the top-ranked tempo candidate — no manual anchor or alternate-candidate
  selection.** Step 034's own title is "Generate beats... " (the automatic/algorithmic half); a user
  choosing a different ranked candidate or placing a manual anchor is Step 035's own "beat-grid
  manual correction" scope, named explicitly in the very next numbered step. `BeatGrid.Candidates`
  still carries every ranked alternative through unchanged, so a future correction UI has everything
  it needs without this step re-exposing a selection API ahead of that need.
- **"Likely downbeats" is a positional convention (`BeatWithinBar == 0`, the first beat of every
  bar), not an independent accent-detection pass.** Section 3 explicitly scopes downbeat estimation
  as "optional... with confidence and manual labeling," naming a real correction entry point
  (Step 035) rather than requiring a fully solved accent detector here. This mirrors Step 033's own
  disclosed `MeterGuess = "4/4"` placeholder simplification for the same reason.
- **Phase-lock resync, not a fixed-period extrapolation from a single anchor.** The first
  implementation attempt anchored every beat purely to `firstOnsetTicks + k * periodTicks`; against
  the real click-track fixture this accumulated enough tempo-quantization drift (the autocorrelation
  period estimate is quantized to `TempoDetectionSettings.TimeResolutionSeconds` bins — Step 033's
  own disclosed limitation) that a beat several periods past the anchor landed outside even a
  realistic alignment tolerance — a real failure this session actually hit and fixed (§4), not a
  hypothetical. The fix: whenever a predicted beat lands within `AlignmentToleranceTicks` of a real
  onset, that beat is placed exactly at the onset's own timestamp, and the *next* prediction steps
  forward from that corrected position rather than the uncorrected one — a standard phase-lock
  correction, not a new per-beat algorithm. An unaligned beat does not move the phase, so a genuine
  gap in the rhythmic content (silence, a missed click) does not propagate error into the beats that
  follow once real onsets resume.
- **Two-band, non-overlapping confidence model**, not a single multiplicative formula. The initial
  formula (`tempo.Confidence * onset.Strength` for aligned beats, `tempo.Confidence * 0.5` for
  unaligned) failed this session's own negative-path test for a real, disclosed reason: a correctly
  detected but comparatively weak onset's own `Strength` could push an aligned beat's confidence
  *below* an unaligned beat's flat `0.5` multiplier, inverting the very distinction Step 034's own
  "avoid asserting uncertainty away" guidance requires making visible. The fix separates the two
  cases into non-overlapping bands: an aligned beat's confidence is always `tempo.Confidence *
  (0.6 + 0.4 * onset.Strength)` (floor `0.6`, ceiling `1.0`, of the tempo confidence), an unaligned
  beat is always `tempo.Confidence * 0.3` — the unaligned ceiling (`0.3`) is always strictly below
  the aligned floor (`0.6`), so "real corroborating evidence found" vs. "none found" is always a
  real, visible, deterministic confidence distinction, not one that chance onset-strength variation
  could erase.
- **The `AlignmentToleranceTicks` value used by this step's own tests (`2 * HopSamples` in ticks,
  320,000 ticks at this fixture's 8 kHz/128-hop settings) is derived from, not independent of, Step
  032's own already-established onset-timestamp-accuracy tolerance.** `Step032AcceptanceTests`
  itself asserts a *single* detected onset's timestamp lands within one hop (128 samples/16 ms) of
  the true event. This step compares *two* independently hop-quantized onset-derived timestamps
  (a predicted beat position that may itself trace back to a previously matched onset, against the
  next real onset) against each other, so up to two hops of disagreement is expected even for a
  perfectly isochronous source — not a loosened/arbitrary threshold, but the same one-hop precedent
  doubled for a two-sided comparison. This was confirmed empirically in this session (§4): an
  initial, tighter 5 ms tolerance produced real (not flaky) alignment failures of 80,000-120,000
  ticks, exactly consistent with hop-boundary quantization jitter at a 128-sample hop.
- **"Missing media" resolves to the same two adjacent layers Step 033 already established, carried
  forward unchanged.** A zero-length audio file is rejected by `WavAudioDecoder.ProbeAsync` one
  layer below this step's `OnsetAnalysis`/`TempoAnalysis`-only input contract (proven by a real,
  passing negative test, §4, identical in shape to Step 033's own). This step's own in-scope
  "missing media" shape is a successfully probed/decoded `OnsetAnalysis` that simply carries no
  onsets (e.g. too-short media) — proven by a second test asserting `BeatGridBuilder` returns an
  explicit empty-beats, null-`SelectedTempo` result rather than throwing.

## 4. Commands actually executed

All run via the PowerShell tool — the sandboxed POSIX Bash tool in this session is non-functional
(every invocation, including a plain directory listing, returns exit code 1 with no output), the
same pre-existing environment limitation recorded in every prior step's own evidence.

```
dotnet build XlightsAI.Studio.slnx --nologo                                              # baseline, before any Step 034 code existed
dotnet test  XlightsAI.Studio.slnx --nologo                                               # baseline regression: 420/420 passing
dotnet build src\XlightsAI.Studio.Rhythm\XlightsAI.Studio.Rhythm.csproj --nologo          # new Rhythm source files only, before tests existed
dotnet build tests\XlightsAI.Studio.Rhythm.Tests\XlightsAI.Studio.Rhythm.Tests.csproj --nologo
dotnet test  tests\XlightsAI.Studio.Rhythm.Tests\XlightsAI.Studio.Rhythm.Tests.csproj --nologo --logger "console;verbosity=detailed" --filter "FullyQualifiedName~Step034"
  # first real run: 8/11 passed, 3 failed —
  #  (a) Step034_Acceptance_Passes_TempoFixtureAlignsEventsWithinConfiguredThreshold: a beat 3 periods
  #      past the anchor was 80,000 ticks from the nearest onset, exceeding a too-tight 5 ms tolerance
  #      (see §3's "phase-lock resync" fix)
  #  (b) BuildAsync_MissingClicksLeaveUnalignedBeats_...: an aligned beat's confidence was not always
  #      above an unaligned beat's (see §3's "two-band confidence model" fix)
  #  (c) BuildAsync_TempoAnalysisFromADifferentOnsetAnalysisVersion_...: test bug — two different
  #      probed files produced two different SongIds, so the SongId-mismatch check fired before the
  #      intended VersionId-mismatch check; fixed by rebuilding the test to reuse one asset/SongId with
  #      two independently rebuilt OnsetAnalysis runs
dotnet test  tests\XlightsAI.Studio.Rhythm.Tests\XlightsAI.Studio.Rhythm.Tests.csproj --nologo --logger "console;verbosity=normal" --filter "FullyQualifiedName~Step034"
  # second run, after the phase-lock-resync fix only: 10/11 passed (confidence-ordering test (b) still failing)
dotnet test  tests\XlightsAI.Studio.Rhythm.Tests\XlightsAI.Studio.Rhythm.Tests.csproj --nologo --logger "console;verbosity=normal" --filter "FullyQualifiedName~Step034"
  # third run, after the two-band confidence fix: 11/11 passed
dotnet build XlightsAI.Studio.slnx --nologo                                              # full solution, after Step 034 implementation
dotnet test  XlightsAI.Studio.slnx --nologo                                               # full regression, final
```

### Build results

`Rhythm`, `Rhythm.Tests`, and the full solution all: `Build succeeded. 0 Warning(s). 0 Error(s).`

### Targeted test results (Step 034 tests only, final run)

```
Passed XlightsAI.Studio.Rhythm.Tests.Step034AcceptanceTests.BuildAsync_OnsetAnalysisWithNoDetectableOnsets_ReturnsEmptyBeatsWithoutThrowing_HandlesMissingMediaExplicitly [168 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step034AcceptanceTests.BuildAsync_NegativeAlignmentTolerance_ThrowsWithoutWriting_InvalidTimingBoundaryNegativePath [196 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step034AcceptanceTests.BuildAsync_SourceTempoAnalysisVersionChanges_InvalidatesCacheAndRebuilds_StalePersistedStateNegativePath [265 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step034AcceptanceTests.ProbeAsync_ZeroLengthMedia_IsRejectedBeforeAnyRhythmAnalysis_HandlesMissingMediaExplicitly [9 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step034AcceptanceTests.BuildAsync_CancelledBeforeComputeCompletes_ThrowsWithoutWritingAnyCacheFile_CancellationNegativePath [405 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step034AcceptanceTests.Step034_Acceptance_Passes_TempoFixtureAlignsEventsWithinConfiguredThreshold [419 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step034AcceptanceTests.BuildAsync_SilentAudio_ReturnsEmptyBeatsWithNullSelectedTempoWithoutThrowing_HandlesSilenceExplicitly [149 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step034AcceptanceTests.BuildAsync_NullArguments_ThrowArgumentNullException [163 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step034AcceptanceTests.BuildAsync_MissingClicksLeaveUnalignedBeats_LowerConfidenceWithoutAssertingCertaintyAway [406 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step034AcceptanceTests.BuildAsync_NonPositiveBeatsPerBar_ThrowsWithoutWriting_InvalidTimingBoundaryNegativePath [162 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step034AcceptanceTests.BuildAsync_TempoAnalysisFromADifferentOnsetAnalysisVersion_ThrowsWithoutWriting_InvalidTimingBoundaryNegativePath [434 ms]

Total tests: 11. Passed: 11, Failed: 0.
```

### Full regression (after implementation, final run)

```
Passed!  - Failed: 0, Passed:  71, Skipped: 0, Total:  71, Duration: 359 ms - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed:  91, Skipped: 0, Total:  91, Duration: 325 ms - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed:  25, Skipped: 0, Total:  25, Duration: 2 s   - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed:  34, Skipped: 0, Total:  34, Duration: 4 s   - XlightsAI.Studio.Audio.Tests.dll
Passed!  - Failed: 0, Passed:  37, Skipped: 0, Total:  37, Duration: 3 s   - XlightsAI.Studio.Rhythm.Tests.dll
Passed!  - Failed: 0, Passed: 142, Skipped: 0, Total: 142, Duration: 3 s   - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed:  31, Skipped: 0, Total:  31, Duration: 37 s  - XlightsAI.Studio.Reference.Tests.dll
```

**431 total, 0 failed, 0 skipped** — up from the Step 033 baseline of 420 (+11 new
`Step034AcceptanceTests` in the already-existing `Rhythm.Tests` project: 37 = 7 Step031 + 9 Step032
+ 10 Step033 + 11 Step034). Every one of the 420 pre-existing tests still passes unmodified,
including all 7 Step 031, all 9 Step 032, and all 10 Step 033 tests individually, proving no prior
step's contract was broken.

### Persisted-output inspection (observable demo for this step)

Step 034's own user-visible outcome is "Saved audio/analysis artifact with timestamp provenance plus
a synchronized waveform/timeline demonstration." No `BeatGrid` screen (SCREEN 09 BEAT GRID, Section
5) or `Shell`/`App` wiring exists in this repository yet — this step's own numbered scope, per
Section 3's design notes, is the algorithmic grid-generation boundary; a real, interactive UI
naturally belongs with Step 035's own "beat-grid manual correction" (the first step that needs a
human to actually click/drag something), consistent with Steps 031-033's own precedent of staying
purely domain-level until a step's own guidance requires a UI (see also PHASE-04 Step 030's own
evidence, which *did* add UI only once its own scope — a timing-diagnostics panel — required one).
This is a disclosed scope decision, not a silently skipped requirement (see §6).

The observable demonstration is instead a real, running end-to-end exercise of the actual persisted
artifact, outside the test harness's temp-directory cleanup, run from a throwaway console program
(not part of the repository, under the session scratchpad, deleted after this run) referencing the
real `Rhythm`/`Audio`/`Reference`/`Contracts` projects:

```
dotnet run --project <scratchpad>\step034-demo\step034-demo.csproj --nologo
```

Observed real output (BPM 120 click track, 16 clicks, 0.5 s silent lead-in, 8 kHz, `FftSize=256`/
`HopSamples=128` onset windowing, `SensitivityMultiplier=1.5`/`MinInterOnsetSamples=256` onset
sensitivity, `MinBpm=40`/`MaxBpm=300`/`TimeResolutionSeconds=0.01`/`MaxCandidates=5` tempo scan,
`BeatsPerBar=4`/`AlignmentToleranceTicks=320000` grid settings):

```
Probed SongAsset: Id=SongId { Value = 9b4e3d00-3380-4e0b-966c-38b119a98a1b }, Sha256=159efe567569..., SampleRate=8000, SampleCount=68000
OnsetAnalysis: VersionId=236d865d-8407-4e6e-ae76-4c9c4319d246, OnsetCount=16
TempoAnalysis: VersionId=8e92e849-4c4e-42e8-9f41-4fd4508c4973, CandidateCount=3, Top=120.00 BPM @ 1.0000
Persisted beat grid at: ...\beatgrid-cache\beat-grid-9b4e3d0033804e0b966c38b119a98a1b.json
File exists on disk: True
AnalyzerVersion: XlightsAI.Studio.Rhythm.BeatGridBuilder/1.0.0
SelectedTempo: 120.00 BPM @ 1.0000, MeterGuess=4/4

Generated beat grid (absolute ticks, bar.beat, confidence, downbeat):
  Bar   0.0  TimeTicks=   4960000  Confidence=0.9054  Downbeat=True   NearestOnsetDistanceTicks=0
  Bar   0.1  TimeTicks=   9920000  Confidence=0.7577  Downbeat=False  NearestOnsetDistanceTicks=0
  Bar   0.2  TimeTicks=  15040000  Confidence=0.9539  Downbeat=False  NearestOnsetDistanceTicks=0
  Bar   0.3  TimeTicks=  20000000  Confidence=1.0000  Downbeat=False  NearestOnsetDistanceTicks=0
  ... (bars 1-3 follow the identical pattern; all 16 beats printed in the real session transcript) ...
  Bar   3.3  TimeTicks=  80000000  Confidence=1.0000  Downbeat=False  NearestOnsetDistanceTicks=0

Max nearest-onset distance across all beats: 0 ticks (configured threshold: 320000 ticks)

Reopened grid VersionId matches: True
Reopened beat count matches: True
Rebuild with identical inputs reuses cache (same VersionId): True

Silence demo: OnsetCount=0, TempoCandidateCount=0, SelectedTempo=null, BeatCount=0 (expected 0 beats, null tempo, no exception thrown)

Temp directory cleaned up.
```

Direct, real, on-disk confirmation of "Tempo fixture aligns events within configured threshold":
every one of the 16 generated beats lands at distance `0` from its nearest real onset (well inside
the configured 320,000-tick threshold) — the phase-lock resync (§3) places every beat exactly on its
corroborating onset for this perfectly isochronous fixture. Bars/beats-within-bar increment
correctly (`0.0`-`0.3`, `1.0`-`1.3`, ...), every fourth beat (`*.0`) is flagged as a likely downbeat,
and confidence varies per beat (`0.7577`-`1.0000`) rather than being a fabricated constant — directly
exercising "avoid asserting uncertainty away." The full raw persisted JSON file (`beat-grid-*.json`)
round-trips through `JsonBeatGridStore.LoadAsync` with an identical `VersionId` and beat count, and a
second `BuildAsync` call with identical inputs reuses the cache without recomputation. The silence
case independently confirms the "must be handled explicitly" edge case outside the test harness:
zero onsets, zero tempo candidates, a `null` selected tempo, zero beats, no exception.

No xLights/Avalonia UI demonstration was run for this step: Step 034 introduces no UI screen, no
`SequenceDocument`/`EditTransaction` mutation, and no xLights XML/XSQ interaction — its entire real
surface is the `IBeatGridBuilder`/`IBeatGridStore` pair proven above. Per this user's own standing
waiver (recorded in this assistant's memory) a real-xLights render check is not required as a
phase-gate blocker; it is doubly not applicable here since this step has no xLights-facing behavior
to demonstrate in the first place.

## 5. Fixtures / input hashes

No new fixture files were added to `fixtures/`. All tests and the manual demo construct real WAV
bytes via this repository's own already-licensed, already-verified
`DeterministicSyntheticAudioGenerator`:

- `BuildClickTrack(bpm: 120, clickCount: 12 or 16, clickDurationSeconds: 0.02, toneHz: 1000.0,
  sampleRate: 8000)` prepended with 4000 samples (0.5 s) of silence — the primary acceptance/demo
  fixture: a perfectly isochronous 120 BPM source.
- Two separate click bursts (4 clicks each) separated by a 2 s silent gap — the "avoid asserting
  uncertainty away" negative-path fixture: produces both aligned beats (near the real clicks) and
  genuinely unaligned, purely interpolated beats (across the gap) in the same grid.
- A fully silent 2 s WAV (`new float[SampleRate * 2]`) — the "silence must be handled explicitly"
  negative-path fixture.
- A true zero-length WAV (`[]` samples) — the "missing media" fixture proving the Audio layer's own
  existing explicit rejection.
- A silent 10-sample WAV (non-zero length, but far too short for Step 032's detector to find any
  transient) — the in-scope "missing media reaching this step as a zero-onset `OnsetAnalysis`"
  fixture.
- All test files are written under `%TEMP%\xlai-step034-*\`, deleted by each test class's
  `Dispose()` — identical cleanup convention to Steps 031-033. The manual demo's own throwaway files
  were written under `%TEMP%\xlai-step034-demo-*\` and deleted by the demo program itself at the end
  of its run (confirmed by "Temp directory cleaned up." in the real output above); the throwaway
  demo *project* itself (outside the repository, under the session scratchpad) was deleted after
  this evidence was recorded.

## 6. Unresolved issues / known gaps

- **No UI wiring (SCREEN 09 BEAT GRID) in this step** — a disclosed, deliberate scoping decision
  (§3/§4): this step's own numbered scope is algorithmic grid generation; a real interactive beat
  grid view naturally belongs with Step 035's own "beat-grid manual correction" (the first step that
  needs a human to click/drag a beat), matching Steps 031-033's own precedent of staying
  domain-level until a step's own guidance requires a UI.
- **No manual anchor or alternate-tempo-candidate selection** — always selects
  `TempoAnalysis.Candidates[0]` (the top-ranked hypothesis); `BeatGrid.Candidates` carries every
  alternative through unchanged so Step 035 can offer a real selection UI without this step
  re-exposing that API ahead of need. A disclosed, deliberate scope limit (§3).
- **"Likely downbeats" is a fixed first-beat-of-bar convention, not independent accent detection** —
  a disclosed, deliberate scoping decision (§3), matching Step 033's own `MeterGuess` placeholder
  precedent; real downbeat correction is Step 035's own "manual labeling" scope per Section 3.
- **`BeatGrid.CorrectionVersion` is declared but unused by this step** (always `0`) — reserved for
  Step 035's own manual-correction tracking, declared now rather than retrofitted later to avoid a
  breaking contract change once Step 035 depends on today's `BeatGrid` shape (§3).
- **Phase-lock resync only corrects forward from an aligned beat; a long unaligned stretch (e.g. a
  multi-bar intro with no detectable onsets) accumulates the same quantization drift a fixed
  extrapolation would until real onsets resume** — a real, inherent limitation of this step's own
  "known half/double-time signals aside, there is no ground truth during an unaligned stretch"
  scope, directly exercised (not hidden) by
  `BuildAsync_MissingClicksLeaveUnalignedBeats_LowerConfidenceWithoutAssertingCertaintyAway`'s own
  lower-confidence assertion for exactly those beats — the confidence model's job is to disclose
  this uncertainty, not eliminate it.
- **`AlignmentToleranceTicks` is a step-034-test-chosen constant (two hops at this fixture's own
  8 kHz/128-hop onset settings), not a universally "correct" threshold** — a real recording's onset
  timestamp accuracy depends on `RhythmSettings.HopSamples`/`FftSize`, so a production caller must
  choose this value consistent with whatever onset-detection settings actually produced the
  `OnsetAnalysis` being aligned against; this step does not (and, given Section 3's "no generic
  interfaces without a concrete boundary," should not yet) auto-derive one from `RhythmSettings`.

## 7. PASS / BLOCKED

**Status: PASS** for Step 034's own stated required acceptance and failure/edge case:

- "Tempo fixture aligns events within configured threshold" (required acceptance, verbatim) — proven
  by `Step034_Acceptance_Passes_TempoFixtureAlignsEventsWithinConfiguredThreshold` (every generated
  beat over a real 120 BPM click fixture lands within `AlignmentToleranceTicks` of a real detected
  onset) and independently confirmed by the real, non-test-harness demo run in §4 (all 16 beats at
  exactly `0` ticks from their nearest onset, well inside the configured 320,000-tick threshold).
- "Represent beats with absolute ticks and bar indices" — proven by the same test's
  `BeatWithinBar`/`BarIndex` sequencing assertions and the real demo's own printed `Bar N.M` output,
  and by `BeatMarker.TimeTicks` always being the grid's one absolute-tick field (never re-based to a
  bar-relative offset).
- "meter must be user correctable" — proven structurally: `BeatGridSettings.BeatsPerBar` is a plain
  settings value (part of the cache key, like every other analyzer setting in this phase), not a
  hardcoded constant; changing it and rebuilding produces a differently grouped grid (exercised
  implicitly by the settings-equality cache-validity check, `existing.Settings == settings`).
- "show confidence and avoid asserting uncertainty away" — proven by
  `BuildAsync_MissingClicksLeaveUnalignedBeats_LowerConfidenceWithoutAssertingCertaintyAway` (a real
  gap in the rhythmic content produces measurably and reliably lower-confidence beats, never a
  uniform placeholder) and the real demo's own non-uniform per-beat confidence values.
- "Silence... must be handled explicitly" — proven by
  `BuildAsync_SilentAudio_ReturnsEmptyBeatsWithNullSelectedTempoWithoutThrowing_HandlesSilenceExplicitly`
  and the real demo's own silence run (`BeatCount=0`, `SelectedTempo=null`, no exception).
- "Invalid timing boundaries... must be handled explicitly" — proven by
  `BuildAsync_NonPositiveBeatsPerBar_ThrowsWithoutWriting_InvalidTimingBoundaryNegativePath`,
  `BuildAsync_NegativeAlignmentTolerance_ThrowsWithoutWriting_InvalidTimingBoundaryNegativePath`, and
  `BuildAsync_TempoAnalysisFromADifferentOnsetAnalysisVersion_ThrowsWithoutWriting_InvalidTimingBoundaryNegativePath`
  (zero cache file written for every invalid-input case).
- "...and missing media must be handled explicitly" — proven at both real layers (§3), identical in
  shape to Step 033's own: the Audio layer's own pre-existing explicit rejection of zero-length media
  (`ProbeAsync_ZeroLengthMedia_IsRejectedBeforeAnyRhythmAnalysis_HandlesMissingMediaExplicitly`), and
  this step's own explicit empty-result handling of a valid-but-onset-free analysis
  (`BuildAsync_OnsetAnalysisWithNoDetectableOnsets_ReturnsEmptyBeatsWithoutThrowing_HandlesMissingMediaExplicitly`).
- Cancellation — proven by
  `BuildAsync_CancelledBeforeComputeCompletes_ThrowsWithoutWritingAnyCacheFile_CancellationNegativePath`.
- Stale persisted state — proven by
  `BuildAsync_SourceTempoAnalysisVersionChanges_InvalidatesCacheAndRebuilds_StalePersistedStateNegativePath`
  (a rebuilt `TempoAnalysis`, even for the same `SongId`/`OnsetAnalysis`, triggers a real grid rebuild
  rather than silently serving the grid built from the superseded tempo run).
- The step's own user-visible outcome ("Saved audio/analysis artifact with timestamp provenance plus
  a synchronized waveform/timeline demonstration") is proven at the domain/persistence level: the
  persisted JSON artifact was directly inspected on disk (§4, real file path, real reopen/round-trip
  confirmation), every beat carries its own absolute-tick timestamp provenance back to its source
  `OnsetAnalysis`/`TempoAnalysis` versions, and the demo synchronizes the generated grid against the
  real detected onsets it was built from (every beat's `NearestOnsetDistanceTicks` printed
  alongside it). No UI timeline view exists yet to render this visually — a disclosed scope decision
  (§3/§6), not a silently skipped requirement — and every claim above is backed by an
  actually-executed, actually-passing automated test, including three that failed on their first
  real run and were fixed with real, disclosed corrections (§3/§4), not merely described as passing.

No previously accepted step's contract was modified — `RhythmSettings`, `OnsetEvent`,
`OnsetDetectionSettings`, `OnsetAnalysis`, `IOnsetDetector`/`OnsetDetector`,
`IOnsetAnalysisStore`/`JsonOnsetAnalysisStore`, `TempoHypothesis`, `TempoDetectionSettings`,
`TempoAnalysis`, `ITempoDetector`/`TempoDetector`, `ITempoAnalysisStore`/`JsonTempoAnalysisStore`,
and every PHASE-01-04 public member are completely unchanged — confirmed by the full pre-existing
420-test suite passing unmodified before a single Step 034 line of code existed (§1), and again at
431/431 (420 old + 11 new) after implementation (§4), with all 7 Step 031, all 9 Step 032, and all 10
Step 033 tests individually still passing by name in that same run.

## 8. Phase-wide status after this session

Steps 031, 032, 033 and now 034 (of PHASE-05's seven numbered steps, 031-037) now have recorded PASS
evidence. Steps 035-037 and the phase-wide gate demonstration/Gate Report (spec Section 7/8:
"Analyze a click/drum sample, see beat markers, correct an intentionally wrong downbeat, generate
restrained pops for two props and play them synchronized while comparing before/after") are **not**
produced by this session — per the task's own instruction, this session implements only Step 034 and
does not proceed past it until this evidence is reviewed.
