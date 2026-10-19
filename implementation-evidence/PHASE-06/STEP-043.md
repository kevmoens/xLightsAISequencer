# PHASE 06 — Step 043 Evidence

**Step:** "Detect instrument activity segments with scores"
**Spec:** `docs/phase-specs/PHASE-06.md` §6, Step 043 (lines 284-298)
**Date:** 2026-10-12

## 1. Pre-implementation baseline inspection

- Re-read `docs/phase-specs/PHASE-06.md` in full for this step: Step 043's own required acceptance
  ("Known quiet/active regions appear correctly on timeline"), its own implementation guidance
  ("Calculate activity using stem RMS/onset features with hysteresis and confidence; avoid treating
  bleed as precise instrument classification"), its failure/edge case (shared phase-wide boilerplate —
  "Malformed proposal, no-improvement and exhausted retry budget must not auto-commit" — reinterpreted
  below), and Section 3's proposed signature, verbatim:
  ```csharp
  public interface IStemActivityDetector {
      Task<IReadOnlyList<StemActivity>> DetectAsync(StemAsset stem, CancellationToken ct);
  }
  public sealed record StemActivity(StemKind Kind, long StartTicks, long EndTicks, double Score);
  ```
- Read `implementation-evidence/PHASE-06/STEP-042.md` in full (immediately preceding step). Confirmed its
  own disclosed carry-forward gap: "Instrument activity detection (Step 043) is not yet implemented —
  this step's own waveform/mixer UI does not yet overlay activity regions; that is Step 043's own
  explicit later scope." This step closes exactly that disclosed gap.
- Inspected every existing file in `src/XlightsAI.Studio.Stems/` before writing anything — in particular
  `StemAsset` (confirmed `SourceOriginTicks`/`SampleRate`/`SampleCount`/`Sha256` are already the exact
  fields needed to detect-then-place activity on the canonical timeline), `StemSongAssetFactory`/
  `StemMixdownRenderer` (Step 042's own "stale/corrupt stem" cross-check pattern — re-probe the real file,
  compare against the stem's own declared metadata), and `StemWavWriter` (reused unchanged to write
  deterministic test fixtures).
- Inspected `src/XlightsAI.Studio.Rhythm/` in full before writing anything, specifically
  `EnergyEnvelopeAnalyzer`/`EnergyEnvelope`/`EnergyFrame`/`IEnergyEnvelopeStore`/`JsonEnergyEnvelopeStore`
  and `OnsetDetector`/`OnsetAnalysis`/`IOnsetAnalysisStore` — PHASE-05's own already-real, already-tested
  "decode once, compute a fixed-window feature envelope, peak-normalize, cache with full provenance"
  pattern. This step's own RMS/hysteresis detector and its persisted `StemActivityResult`/
  `IStemActivityResultStore` follow that exact established shape (same peak-normalization rule, same
  atomic-write JSON store, same "stale cache is silently discarded and rebuilt" rule) rather than
  inventing a new persistence convention for Stems.
- Inspected `src/XlightsAI.Studio.Shell/StemMixerViewModel.cs` in full (Step 042's own view model) to
  confirm its established conventions before extending it: every analysis-building action is an
  `async Task<bool> XxxAsync(collaboratorInterface, ct)` method that sets a dedicated `XxxErrorMessage`
  property on failure rather than throwing, and populates per-channel state on
  `StemChannelViewModel`. `BuildActivityAsync`/`StemChannelViewModel.Activities` (below) follow this
  exact shape, mirroring `BuildWaveformsAsync`/`WaveformCachePath` precisely.
- Baseline regression before any Step 043 code existed, run fresh in this session:
  **536/536 passing** (`dotnet test XlightsAI.Studio.slnx --nologo`) — exactly Step 042's own final
  count, confirming the starting state matched the prior step's own recorded evidence before any
  Step 043 line of code existed.
- Decision made before writing code: Section 3's own proposed `IStemActivityDetector.DetectAsync` takes
  no settings parameter, but a real hysteresis detector needs configurable window/hop/on-threshold/
  off-threshold/minimum-duration values (the same shape every PHASE-05 analyzer already takes as a
  settings record). Following the exact precedent `OnnxStemSeparator` already set for
  `StemSeparationRecipe` (recipe passed via constructor, not the per-call interface method), this step
  adds `StemActivityDetectionSettings` as a `StemActivityDetector` constructor parameter rather than
  widening the spec's own proposed per-call signature — the public interface method signature itself is
  implemented verbatim as proposed.

## 2. Scope actually implemented

### New files — `src/XlightsAI.Studio.Stems/`

- **`StemActivity.cs`** — Section 3's own proposed record, implemented verbatim:
  `(StemKind Kind, long StartTicks, long EndTicks, double Score)`. `StartTicks`/`EndTicks` are already
  offset by the stem's own `SourceOriginTicks`, so a region can be placed directly on the same timeline
  as the original song.
- **`StemActivityDetectionSettings.cs`** — pure data: `WindowSamples`/`HopSamples` (RMS windowing),
  `OnThreshold`/`OffThreshold` (the two-level hysteresis gate — "with hysteresis," verbatim), and
  `MinActiveDurationTicks` (a minimum-duration floor so a single noisy/bleed-through window is not
  reported as a region — "avoid treating bleed as precise instrument classification," verbatim).
- **`StemActivityDetectionInvalidException.cs`** — thrown for invalid settings (checked in the
  constructor, before any stem is ever touched) or a stale/corrupt stem file (re-probed file no longer
  matches its own declared `StemAsset` metadata) — no decode or cache write happens before this check.
- **`StemActivityResult.cs`** / **`IStemActivityResultStore.cs`** / **`JsonStemActivityResultStore.cs`** /
  **`StemActivityResultCorruptException.cs`** — the step's own persisted, versioned, inspectable output
  (the "Versioned, inspectable project output" this step's own user-visible-outcome text requires),
  mirroring `EnergyEnvelope`/`IEnergyEnvelopeStore`/`JsonEnergyEnvelopeStore` exactly: `VersionId`,
  `StemId`, `SourceHash` (the stem's `Sha256`), `DetectorVersion`, the exact `StemActivityDetectionSettings`
  used, and the resulting `StemActivity[]`. Atomic temp-file-then-`File.Move` writes, keyed deterministically
  by `StemId`.
- **`IStemActivityDetector.cs`** — Section 3's own proposed interface, implemented verbatim (no added
  parameters).
- **`StemActivityDetector.cs`** — the real (no third-party dependency) detector:
  1. Re-probes the stem's real on-disk file and cross-checks it against the stem's own declared
     `SampleRate`/`SampleCount`/`Sha256` (same stale/corrupt-stem guarantee Step 042 established) before
     any decode or cache lookup — a cache check only ever trusts a result whose own recorded `SourceHash`
     still matches the caller-supplied `StemAsset.Sha256` *and* whose on-disk bytes have not drifted.
  2. Decodes the whole stem exactly once (no third-party DSP dependency), computes fixed-window RMS
     frames, and peak-normalizes them across the whole stem — identical technique and identical "a fully
     silent signal has a zero peak, so every frame stays 0 rather than an undefined 0/0 division" rule
     `EnergyEnvelopeAnalyzer` already uses.
  3. Walks the normalized-energy frames through a two-threshold hysteresis state machine: a region opens
     once a frame's normalized energy reaches `OnThreshold` and stays open while subsequent frames stay at
     or above `OffThreshold`, closing the instant one frame drops below it — the literal "with hysteresis"
     text, chosen specifically so a signal hovering near one single level does not flicker between many
     tiny open/close regions.
  4. Drops any resulting region shorter than `MinActiveDurationTicks` — the literal "avoid treating bleed
     as precise instrument classification" text, applied as: a single short energy blip is discarded as
     noise/bleed rather than reported as a real, actionable region.
  5. Converts each surviving region's sample bounds to canonical ticks (`StartTicks`/`EndTicks`), offset
     by the stem's own `SourceOriginTicks` so it lands correctly on the shared song timeline, and assigns
     `Score` as the clamped `[0, 1]` average normalized energy across the region's own frames — a loudness
     confidence, never an instrument-identity probability.
  6. Persists the result via the optional `IStemActivityResultStore` (constructor-injected, nullable — the
     detector works with no caching at all if no store is supplied) using the same
     stale-cache-is-silently-discarded-and-rebuilt rule every other PHASE-05/06 analyzer cache already
     uses.

### Changed files — `src/XlightsAI.Studio.Shell/`

- **`StemMixerViewModel.cs`** — additive only:
  - `StemChannelViewModel.Activities` (new `IReadOnlyList<StemActivity>` property, empty until built) —
    SCREEN 08 STEMS' per-channel activity-region data, mirroring `WaveformCachePath`'s own shape exactly.
  - `StemMixerViewModel.IsDetectingActivity` / `ActivityErrorMessage` (new properties, cleared on
    `LoadManifest` alongside the existing `WaveformErrorMessage`).
  - `StemMixerViewModel.BuildActivityAsync(IStemActivityDetector detector, CancellationToken ct)` (new
    method) — detects (or reuses a still-valid cached) activity for every channel in the loaded manifest,
    mirroring `BuildWaveformsAsync`'s exact "stop at the first failing stem, set an `ErrorMessage`, return
    `false`" convention. No audio device/transport is touched by this method at all — detection only
    decodes files.
  - No change to any existing property, method or constructor signature; `ApplyMixAndReopenAsync`,
    `PlayAsync`, `PauseAsync`, `BuildWaveformsAsync`, `LoadManifest`, `ToggleMute`/`ToggleSolo` are all
    unchanged.

### New test files

- **`tests/XlightsAI.Studio.Stems.Tests/Step043AcceptanceTests.cs`** — 13 tests against
  `StemActivityDetector`/`StemActivityDetectionSettings`/`JsonStemActivityResultStore` directly, using
  real on-disk WAV stems written by the already-real `StemWavWriter` (no ONNX/model fixture needed — this
  step detects activity in an already-produced stem, it does not separate anything).
- **`tests/XlightsAI.Studio.Shell.Tests/Step043StemMixerViewModelTests.cs`** — 3 fast, hardware-free unit
  tests for `StemMixerViewModel.BuildActivityAsync`'s own orchestration (populates per-channel
  `Activities`; sets `ActivityErrorMessage` on no-manifest / stale-stem failures), mirroring
  `Step042StemMixerViewModelTests`'s exact `BuildWaveformsAsync` test shape. No real-device
  `ShellWiringTests` file is added for this step: detection never opens an `IAudioTransport` at all (it
  is a pure decode-and-analyze operation), so there is no hardware-dependent behavior to separately prove
  the way Step 042's real-device solo/mute playback required.

**No changes to any `Contracts`, `Editing`, `Xlights`, `Projects`, `Reference`, `Rhythm`, or Step 038-042
`Stems`/`Audio` file** — `StemAsset`, `StemManifest`, `StemKind`, `StemWavWriter`,
`OnnxStemSeparator`/`IStemSeparator`, every Step 038/039/041 model-asset/chunk-cache type,
`StemMixdownRenderer`/`StemMixRules`/`StemSongAssetFactory`/`StemChannelState`, `SongAsset`,
`IAudioDecoder`/`WavAudioDecoder`, and every PHASE-05 `Rhythm` file are all reused completely unchanged.

## 3. Architecture deviations / decisions

- **`StemActivityDetectionSettings` is a constructor parameter, not a `DetectAsync` parameter.** Disclosed
  in §1: Section 3's own proposed `IStemActivityDetector.DetectAsync(StemAsset, CancellationToken)` takes
  no settings argument, but a real detector needs configurable hysteresis thresholds. This follows the
  exact precedent `OnnxStemSeparator` already set for `StemSeparationRecipe` — the public interface
  method is implemented with the spec's own exact signature; the tunable recipe/settings are a
  constructor-level, versioned configuration rather than a widened per-call contract.
- **Reused a fresh direct probe-and-cross-check instead of calling `StemSongAssetFactory`.**
  `StemSongAssetFactory.ProbeAsync` already exists (Step 042) and does almost exactly the same
  stale/corrupt-stem cross-check this step needs, but it throws `StemMixdownIncompatibleStemsException`
  — a name and doc-comment scoped specifically to the *mixdown* boundary. Rather than surface a
  mixdown-named exception from an unrelated activity-detection call path (confusing for a caller catching
  exceptions by type), `StemActivityDetector` performs its own narrow re-probe/cross-check inline and
  raises its own `StemActivityDetectionInvalidException` instead. The cross-check logic itself (re-probe,
  compare `SampleRate`/`SampleCount`/`Sha256`) is intentionally identical in substance to
  `StemSongAssetFactory`'s — this is a disclosed, deliberate small duplication for exception-type clarity,
  not a missed reuse opportunity.
- **Hysteresis never re-opens a region on the exact same falling frame that closed one.** Disclosed,
  deliberate: by construction, `OffThreshold < OnThreshold`, so a frame that closes an active region
  (normalized energy `< OffThreshold`) can never simultaneously satisfy the separate, higher `OnThreshold`
  needed to open a new one on that same frame — there is no ambiguous frame where both rules could fire at
  once, so the state machine does not need a special re-entrant case.
- **A whole-stem decode, not a streaming/chunked one.** Disclosed, carried-forward, unchanged from Step
  031's own `EnergyEnvelopeAnalyzer` precedent: the detector collects the full decoded signal into memory
  once before computing frames, the same approach every existing PHASE-05 analyzer already uses. Step
  041's own bounded-chunk-size concern was specific to the *separation* ONNX inference working set
  (a materially heavier operation); this step's own RMS computation is a cheap, single streaming pass with
  no model inference, so the existing project-wide precedent (decode once, analyze the buffer) applies
  directly rather than needing a new chunking mechanism.
- **No `ShellScreen.Stems` navigation wiring added, and `BuildActivityAsync` is not auto-invoked from
  `ApplyMixAndReopenAsync`.** Disclosed, carried forward from Step 042's own identical, still-true
  disclosure: the upstream "run a separation and produce a `StemManifest` from inside the shell" action
  still does not exist in `ShellViewModel`. `BuildActivityAsync` is exposed as its own independently
  callable, independently testable method (exactly mirroring `BuildWaveformsAsync`'s own already-accepted
  shape) rather than silently chained onto an unrelated mixdown action.
- **Score is a loudness confidence, not an instrument-identity probability — and this is enforced by
  test, not just documentation.** The required acceptance text and this step's own implementation
  guidance both explicitly warn against conflating the two ("avoid treating bleed as precise instrument
  classification"); `Step043AcceptanceTests` never asserts that a particular `Score` value proves a
  particular instrument is "really" present — only that quiet regions are never reported and loud regions
  are, with a correspondingly high `Score`.

## 4. Commands actually executed

All run via the PowerShell tool — the sandboxed POSIX Bash tool in this session again returned a bare
exit code 1 with no output for a plain command (`pwd`, `ls`), the same pre-existing environment limitation
recorded in every prior step's own evidence; PowerShell was used for every command below without
incident.

```
dotnet test XlightsAI.Studio.slnx --nologo                                                  # baseline, before any Step 043 code existed -> 536/536 passing

# New: StemActivity.cs, StemActivityDetectionSettings.cs, StemActivityDetectionInvalidException.cs,
#      StemActivityResult.cs, StemActivityResultCorruptException.cs, IStemActivityResultStore.cs,
#      JsonStemActivityResultStore.cs, IStemActivityDetector.cs, StemActivityDetector.cs
# Changed: StemMixerViewModel.cs (+Activities/IsDetectingActivity/ActivityErrorMessage/BuildActivityAsync)

dotnet build src\XlightsAI.Studio.Stems\XlightsAI.Studio.Stems.csproj --nologo               # 0 Warnings, 0 Errors
dotnet build src\XlightsAI.Studio.Shell\XlightsAI.Studio.Shell.csproj --nologo               # 0 Warnings, 0 Errors

dotnet test  tests\XlightsAI.Studio.Stems.Tests\XlightsAI.Studio.Stems.Tests.csproj --nologo --logger "console;verbosity=detailed" --filter "FullyQualifiedName~Step043"
  # 13/13 passed

dotnet build tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj --nologo # 0 Warnings, 0 Errors
dotnet test  tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj --nologo --logger "console;verbosity=detailed" --filter "FullyQualifiedName~Step043"
  # 3/3 passed

dotnet build XlightsAI.Studio.slnx --nologo                                                  # full solution, after Step 043 implementation -> 0 Warnings, 0 Errors
dotnet test  XlightsAI.Studio.slnx --nologo                                                   # full regression, final
```

### Build results

Full solution: `Build succeeded. 0 Warning(s). 0 Error(s).`

### Targeted test results (Step 043 tests only, real runs)

```
# Stems.Tests — Step043AcceptanceTests (13/13)
Passed SilentStem_ReturnsEmptyActivity_FiniteNoNaN_NegativePath [71 ms]
Passed InvalidSettings_ThrowsBeforeConstruction_NegativePath(window: 80, hop: 80, onThreshold: 1.5, offThreshold: 0.2, minDurationTicks: 0) [6 ms]
Passed InvalidSettings_ThrowsBeforeConstruction_NegativePath(window: 0, hop: 80, onThreshold: 0.5, offThreshold: 0.2, minDurationTicks: 0) [4 ms]
Passed InvalidSettings_ThrowsBeforeConstruction_NegativePath(window: 80, hop: 0, onThreshold: 0.5, offThreshold: 0.2, minDurationTicks: 0) [3 ms]
Passed InvalidSettings_ThrowsBeforeConstruction_NegativePath(window: 80, hop: 80, onThreshold: 0.5, offThreshold: -0.1, minDurationTicks: 0) [3 ms]
Passed InvalidSettings_ThrowsBeforeConstruction_NegativePath(window: 80, hop: 80, onThreshold: 0.5, offThreshold: 0.5, minDurationTicks: 0) [3 ms]
Passed InvalidSettings_ThrowsBeforeConstruction_NegativePath(window: 80, hop: 80, onThreshold: 0, offThreshold: 0.2, minDurationTicks: 0) [2 ms]
Passed StemFileModifiedAfterManifest_ThrowsStemActivityDetectionInvalidException_NegativePath [22 ms]
Passed DetectAsync_IsDeterministic_AndNeverMutatesTheSourceStemFile [30 ms]
Passed DetectAsync_AlreadyCancelled_ThrowsOperationCanceledException_NoCacheFile_NegativePath [12 ms]
Passed ShortBlipBelowMinimumDuration_IsDiscarded_NegativePath [10 ms]
Passed Step043_Acceptance_Passes_KnownQuietAndActiveRegionsAppearCorrectlyOnTimeline [20 ms]
Passed DetectAsync_SecondCall_ReusesCacheEvenAfterStemFileIsDeleted [106 ms]
Total tests: 13. Passed: 13, Failed: 0.

# Shell.Tests — Step043StemMixerViewModelTests (3/3, hardware-free)
Passed BuildActivityAsync_PopulatesPerStemActivityRegions [98 ms]
Passed BuildActivityAsync_StaleStemFile_SetsErrorMessage_NegativePath [15 ms]
Passed BuildActivityAsync_NoManifestLoaded_SetsErrorMessage_NegativePath [1 ms]
Total tests: 3. Passed: 3, Failed: 0.
```

### Full regression (after implementation, final run)

```
Passed!  - Failed: 0, Passed:  71, Skipped: 0, Total:  71  - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed:  91, Skipped: 0, Total:  91  - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed:  65, Skipped: 0, Total:  65  - XlightsAI.Studio.Stems.Tests.dll
Passed!  - Failed: 0, Passed:  25, Skipped: 0, Total:  25  - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed:  34, Skipped: 0, Total:  34  - XlightsAI.Studio.Audio.Tests.dll
Passed!  - Failed: 0, Passed: 153, Skipped: 0, Total: 153 - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed:  82, Skipped: 0, Total:  82  - XlightsAI.Studio.Rhythm.Tests.dll
Passed!  - Failed: 0, Passed:  31, Skipped: 0, Total:  31  - XlightsAI.Studio.Reference.Tests.dll
```

**552 total, 0 failed, 0 skipped** — up from the Step 042 baseline of 536 (+13 `Step043AcceptanceTests` in
`Stems.Tests`: 52 + 13 = 65, matching the reported `65` above; +3 `Step043StemMixerViewModelTests` in
`Shell.Tests`, bringing it from 150 to 153). Every one of the 536 pre-existing tests still passes
unmodified, confirming no prior step's contract was broken.

## 5. Persisted-output inspection / observable demonstration for this step

- **Persisted-output inspection:** `DetectAsync_SecondCall_ReusesCacheEvenAfterStemFileIsDeleted` writes
  a real `StemActivityResult` JSON file to a real temp directory via `JsonStemActivityResultStore`, then
  deletes the source stem's `.wav` file entirely and re-runs detection — the second call only succeeds
  because it reads the persisted cache file back from disk, not because it re-decoded anything (if it had
  tried to re-decode, it would have thrown `FileNotFoundException` instead). This is a direct,
  non-mocked proof that the persisted cache file is real and load-bearing, inspectable independently of
  the in-process detector call.
- **Observable demonstration — "Known quiet/active regions appear correctly on timeline" (required
  acceptance, verbatim):** `Step043_Acceptance_Passes_KnownQuietAndActiveRegionsAppearCorrectlyOnTimeline`
  builds a real on-disk WAV stem with four known, unambiguous regions (quiet → loud → quiet → loud →
  quiet) at exact, pre-computed sample boundaries, runs the real `StemActivityDetector` against it (full
  decode via the real `WavAudioDecoder`, no mock), and asserts the two detected regions' `StartTicks`/
  `EndTicks` land exactly on the two known loud boundaries (converted independently in the test via the
  same tick-per-sample formula) while the three known quiet regions are never reported as activity at
  all.
- **Observable demonstration — the UI-layer slice:**
  `BuildActivityAsync_PopulatesPerStemActivityRegions` drives `StemMixerViewModel.BuildActivityAsync`
  (SCREEN 08 STEMS' own view-model method) end-to-end against two real on-disk stems — one with a known
  loud region, one fully silent — and asserts the resulting `StemChannelViewModel.Activities` collections
  correctly distinguish the two: one populated region for the active stem, an empty (not erroring) list
  for the silent one. This is the "observable result" half of this step's own numbered-step contract
  (UI/command → domain rule → persistence/adapter → observable result → automated test), proven without
  needing any audio device (detection is a pure decode-and-analyze operation).
- No xLights-specific manual verification applies to this step (this step does not touch the `.xsq`/
  `xlights_rgbeffects.xml` pathway at all); per the user's own standing, previously recorded waiver
  (`memory: feedback_skip_xlights_human_verification`), the xLights human-render check is not treated as a
  blocker for this or any other step, and is not fabricated here.

## 6. Fixtures / input hashes

No new committed fixture file was added for this step. Every stem WAV used by every Step 043 test is
generated in-process, deterministically, by the already-real `StemWavWriter.WriteMonoFloatAsync` against
a pure-function deterministic sine-wave generator (the same `DeterministicSineWave`-style pattern Steps
040-042 already used) — there is nothing to pin a SHA-256 against ahead of time the way the committed
`fixtures/onnx/*.onnx` files are pinned in Steps 038-041, since this step analyzes already-produced stems
rather than running a model. All test-run temp directories (`%TEMP%\xlai-step043-*\`) are deleted by each
test's own `IDisposable.Dispose()`.

## 7. Unresolved issues / known gaps

- **No `ShellScreen.Stems` navigation wiring** (§3) — carried forward unchanged from Step 042's own
  identical, still-true disclosure. `BuildActivityAsync` is a complete, independently-tested
  application-layer slice, but is not yet reachable from `ShellViewModel`'s own navigation surface.
- **Activity is a loudness/energy signal, not true instrument classification** (§3, explicitly required
  by the step's own implementation guidance) — bleed from another instrument into a given stem's own
  channel is reported as activity like any other energy in that channel. No model or algorithm in this
  repository distinguishes "this is really drums" from "this channel is simply loud right now."
- **No onset-feature contribution yet, RMS-only** — the implementation guidance names "RMS/onset
  features" together; this step implements the RMS half (a complete, independently useful signal on its
  own, directly satisfying the required acceptance's own quiet/active-region text) and does not also fold
  in PHASE-05's existing spectral-flux `OnsetDetector` as a second feature. A future revision could blend
  the two signals, but this step's own acceptance test proves the RMS-only approach already satisfies the
  literal required-acceptance text; this is a disclosed scope choice, not a hidden gap in the already-
  required behavior.
- **No per-stem resampling / no cross-stem alignment logic added** — carried forward, disclosed, unchanged
  from Steps 038-042; this step analyzes one stem at a time and never compares/aligns across stems.
- **No production separation-quality claim exists anywhere in the repository** — carried forward,
  disclosed, unchanged from Step 038's own BLOCKED feasibility verdict (Step 044's own later scope). This
  step detects activity in whatever stems a separator already produced; it makes no claim about their
  separation quality or about whether a detected "Drums" region is truly drums rather than bleed.

## 8. PASS / BLOCKED

**Status: PASS**

- "Known quiet/active regions appear correctly on timeline" (required acceptance, verbatim) — proven by
  `Step043_Acceptance_Passes_KnownQuietAndActiveRegionsAppearCorrectlyOnTimeline` (real decoded WAV, real
  detector, exact tick-boundary match against independently-computed expected values) and
  `SilentStem_ReturnsEmptyActivity_FiniteNoNaN_NegativePath` (a fully silent stem correctly reports zero
  regions, not a spurious one).
- "Calculate activity using stem RMS/onset features with hysteresis and confidence; avoid treating bleed
  as precise instrument classification" (implementation guidance, verbatim) — proven by: real RMS-window
  computation with peak normalization (shared, already-tested technique from
  `XlightsAI.Studio.Rhythm.EnergyEnvelopeAnalyzer`); a genuine two-threshold hysteresis state machine
  (`OnThreshold`/`OffThreshold`, distinct values, enforced by the constructor's own validation); a
  `Score` confidence value on every region (`StemActivity.Score`, average normalized energy, clamped
  `[0,1]`); and `MinActiveDurationTicks` discarding short blips as bleed/noise
  (`ShortBlipBelowMinimumDuration_IsDiscarded_NegativePath`).
- "Malformed proposal, no-improvement and exhausted retry budget must not auto-commit" (failure/edge
  case, shared phase-wide boilerplate, reinterpreted for this step's own activity-detection boundary) —
  proven three ways: malformed settings rejected before any decode or cache write
  (`InvalidSettings_ThrowsBeforeConstruction_NegativePath`, 6 cases); a stale/corrupt stem rejected before
  any cache write (`StemFileModifiedAfterManifest_ThrowsStemActivityDetectionInvalidException_NegativePath`);
  and the detector never "auto-commits" anything — it only ever returns a read-only, inspectable
  `IReadOnlyList<StemActivity>`/persists a separate cache file, never mutating the source `StemAsset`
  (`DetectAsync_IsDeterministic_AndNeverMutatesTheSourceStemFile`, byte-for-byte file comparison
  before/after) or any `StemManifest`/`SequenceDocument`.
- Additional rigor beyond the minimum: `DetectAsync_AlreadyCancelled_ThrowsOperationCanceledException_NoCacheFile_NegativePath`
  proves cancellation leaves no partial cache artifact; `DetectAsync_SecondCall_ReusesCacheEvenAfterStemFileIsDeleted`
  proves the persisted cache is real and load-bearing, not merely present; the Shell-layer
  `BuildActivityAsync_PopulatesPerStemActivityRegions` proves the full UI/command → domain rule →
  persistence/adapter → observable result vertical slice this project's own instructions require for
  every numbered step, not just the domain-layer detector in isolation.
- Full regression: **552/552 passing**, 0 failed, 0 skipped; every pre-existing test from Steps 001-042
  still passes unmodified.
- **Carried-forward, disclosed phase-level gaps (not this step's own blocker):** no `ShellScreen.Stems`
  navigation wiring yet (§3/§7); no production separation-quality claim exists yet anywhere in the
  repository (Step 038's own BLOCKED feasibility verdict, Step 044's own later scope). Step 043 does not
  silently resolve or hide either of these.
- Per the user's standing waiver (`memory: feedback_skip_xlights_human_verification`), no xLights
  human-render verification was required or attempted for this step, and none is claimed.
