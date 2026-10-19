# PHASE 04 — Step 028 Evidence

**Step:** "Use one master media time clock for playback and playhead"
**Spec:** `docs/phase-specs/PHASE-04.md` §6, Step 028 (lines 266–280)
**Date:** 2026-10-11

## 1. Pre-implementation baseline inspection

- Re-read `docs/phase-specs/PHASE-04.md` in full (not only Step 028's own numbered block): Section
  3's architectural design notes ("The playback component owns one authoritative monotonic transport
  clock; views subscribe to position snapshots, not their own timers... Separate the high-resolution
  media/sample clock from xLights export frame grid"); Step 028's own implementation guidance
  ("Expose one authoritative playhead and scrub preview; verify time conversion uses song-origin and
  sample-rate not UI frame interval"), required acceptance ("Repeated seeks do not accumulate
  drift"), and the stated failure/edge case ("Out-of-scope, locked or stale revision must be rejected
  without partial mutation").
- Re-read `implementation-evidence/PHASE-04/STEP-24.md`, `STEP-25.md`, `STEP-026.md`, `STEP-027.md`
  in full, treating `SongAsset`, `IAudioDecoder`/`WavAudioDecoder`, `IAudioTransport`/
  `WaveOutAudioTransport`/`UnsupportedPlatformAudioTransport`, `WaveformEnvelope`/
  `WaveformResolutionLevel`/`WaveformPyramidCache`, `TimelineViewport`/`ITimelineViewport`,
  `TimelineViewModel`, `AudioImportViewModel`, `ShellViewModel` as the immutable prior-step contract
  this step only extends, never modifies. Step 025's own evidence explicitly named Step 028's own
  scope: `AudioImportViewModel.RefreshPlaybackPosition`'s doc comment already says "since this step
  does not yet wire a continuous UI clock — Step 028's own scope," and Step 025 §3 already disclosed
  `SetLoop`'s UI exposure is deferred to Step 029, not this step.
- Inspected every file this step could interact with in full: `IAudioTransport.cs`,
  `WaveOutAudioTransport.cs` (specifically its `PositionTicks`/`TicksToSamples`/`SamplesToTicks`
  conventions — reused verbatim, not reimplemented, by this step's own `MasterPlaybackClock`),
  `SongAsset.cs` (`OriginTicks`/`DurationTicks`), `AudioImportViewModel.cs`, `TimelineViewModel.cs`,
  `ITimelineViewport.cs`/`TimelineViewport.cs` (`XFromTime`/`ContentBounds`), `ShellViewModel.cs`,
  `MainWindow.axaml`'s SCREEN 06 AUDIO and SCREEN 13 TIMELINE sections, `DoubleToLeftThicknessConverter.cs`,
  and the existing `tests/XlightsAI.Studio.Audio.Tests`/`tests/XlightsAI.Studio.Shell.Tests` projects
  for exact test convention (real-hardware `[SupportedOSPlatform("windows")]` integration tests vs.
  fast hardware-free unit tests).
- Baseline regression before any Step 028 code existed, run fresh in this session:
  **330/330 passing** (`dotnet test XlightsAI.Studio.slnx --nologo`), matching Step 027's own recorded
  final count exactly.

## 2. Scope actually implemented

**New in `XlightsAI.Studio.Audio`** (still references only `XlightsAI.Studio.Contracts` — no new
cross-project dependency):

- `MasterPlaybackClock` — the one authoritative, real wrapper around `IAudioTransport`:
  - `CanonicalPositionTicks` = `CurrentSong.OriginTicks + transport.PositionTicks` (`0` while no song
    is open) — the exact "song-origin and sample-rate, not UI frame interval" conversion this step's
    implementation guidance requires. No new timer/polling of its own: every read is a direct,
    on-demand computation from the transport's own already-real, device-reported `PositionTicks`
    (itself always derived from `waveOutGetPosition`'s sample count, per Step 025).
  - `IsOpen` — whether a song is currently open on the wrapped transport.
  - `SeekToCanonicalAsync(long canonicalTicks, CancellationToken ct)` — converts the canonical target
    back to the transport's own song-relative ticks (subtracting `OriginTicks`) and delegates to
    `IAudioTransport.SeekAsync`. Explicitly validates **before** ever calling the transport: throws
    `InvalidOperationException` if no song is open (the "stale" case this step's own failure/edge
    case names), and `ArgumentOutOfRangeException` if the target falls outside the open song's own
    canonical timing boundary (the "out-of-scope" case) — both rejected with zero transport calls, so
    `CanonicalPositionTicks` is provably left exactly as it was (see §4's negative-path tests).

**Shell/App wiring** (the required minimum complete UI → application → domain → adapter slice):

- `TimelineViewModel` — new `PlayheadTicks`, `HasPlayhead`, `PlayheadX` (the real pixel position via
  the exact same `TimelineViewport.XFromTime` transform the ruler/waveform already use — `null`
  without a loaded `Viewport`), `PlayheadMarginLeft` (a non-negative, always-well-defined binding
  convenience), `IsPlayheadVisible`, `ScrubTargetTicksText`, `ScrubErrorMessage`. New methods:
  `SetPlayhead(long ticks, bool isActive)` — the one authoritative mutator, called only by
  `ShellViewModel`, whenever the real master clock's reading changes; `ValidateScrubTarget(long ticks)`
  — pure validation against the loaded `Viewport.ContentBounds`, throwing *before* any real seek is
  attempted so an invalid target can never partially move the playhead; `SetScrubError(string?)`.
  `Reset()`/`LoadTimeline()` now also clear `ScrubErrorMessage` (same "stale error field" convention
  every other screen's reload already follows). `RaiseViewportDerivedChanged` now also raises
  `PlayheadX`/`PlayheadMarginLeft`/`IsPlayheadVisible` (so zoom/scroll keep the marker correctly
  positioned, same "re-derived live" contract `RulerTicks`/`WaveformBars` already have).
- `ShellViewModel` — new `_masterClock` field (one `MasterPlaybackClock` per `ShellViewModel`,
  wrapping whichever transport this instance resolved to — real `WaveOutAudioTransport` on Windows,
  `UnsupportedPlatformAudioTransport` elsewhere); new `ScrubTimelinePlayheadCommand` +
  `LastScrubTimelinePlayheadCommandTask` (same deterministic-await pattern as every other command);
  the existing `Audio.PropertyChanged` handler now also pushes `Timeline.SetPlayhead(_masterClock.CanonicalPositionTicks,
  Audio.IsPlaybackOpen)` whenever `AudioImportViewModel.PlaybackPositionTicks`/`IsPlaybackOpen`
  changes — the single place either screen's playhead state is ever written, so the Audio screen's
  own position display and the Timeline's playhead can never disagree. `RunScrubTimelinePlayheadAsync`
  parses `Timeline.ScrubTargetTicksText`, validates it via `Timeline.ValidateScrubTarget`, seeks the
  shared `_masterClock`, then re-reads/pushes the real resulting position — any failure at any stage
  sets `Timeline.ScrubErrorMessage` and leaves `PlayheadTicks` untouched, never a partial mutation.
- `MainWindow.axaml` — SCREEN 13 TIMELINE: the ruler `Border` is now wrapped in a `Grid` with a thin
  `OrangeRed` playhead marker overlaid at `Timeline.PlayheadMarginLeft` (same `DoubleToLeftThicknessConverter`
  convention the ruler ticks/waveform bars already use), visible only when `Timeline.IsPlayheadVisible`;
  a new "Playhead" sub-section shows `Timeline.PlayheadTicks`, an empty-state message when
  `!Timeline.HasPlayhead`, and a "Scrub to (100 ns ticks)" field + "Scrub Playhead" button bound to
  `ScrubTimelinePlayheadCommand`, plus `Timeline.ScrubErrorMessage` display.

**No changes to any existing `Contracts`, `Projects`, `Xlights`, `Reference`, or PHASE-01–03
`Shell`/`App` type's existing members, nor to Step 024–027's own `SongAsset`/`IAudioDecoder`/
`WavAudioDecoder`/`ISongAssetStore`/`JsonSongAssetStore`/`NullSongAssetStore`/`IAudioTransport`/
`WaveOutAudioTransport`/`UnsupportedPlatformAudioTransport`/`WaveformEnvelope`/
`WaveformResolutionLevel`/`WaveformPyramidCache`/`IWaveformBuilder`/`WaveformPyramidBuilder`/
`IWaveformPyramidStore`/`JsonWaveformPyramidStore`/`NullWaveformPyramidStore`, nor to Step 016–021's
own `TimelineViewport`/`ITimelineViewport`/`TimelineRulerTickGenerator`/`EffectBlockLayoutBuilder`,
nor to Step 027's own `WaveformBarLayoutBuilder`/`WaveformBarView`** — purely additive.

## 3. Architecture deviations / decisions

- **This step's own stated failure/edge case text ("Out-of-scope, locked or stale revision must be
  rejected without partial mutation") is the spec's templated boilerplate from the editing/
  `EditTransaction`/`DocumentRevision` steps (PHASE-03), not a literal fit for this step's own actual
  scope.** Step 028 introduces no document mutation, no `EditScope`, and no `DocumentRevision` — there
  is nothing in this step's own real surface that can be "locked" in the Phase-03 effect-lock sense.
  Rather than silently skip the requirement or fabricate an unrelated lock concept, this step
  interprets it at face value against the one piece of mutable state it does own (the playhead):
  **"out-of-scope"** = a scrub/seek target outside the open song's/loaded timeline's own timing
  boundary (`MasterPlaybackClock.SeekToCanonicalAsync`'s `ArgumentOutOfRangeException`,
  `TimelineViewModel.ValidateScrubTarget`'s `ArgumentOutOfRangeException`); **"stale"** = a scrub/seek
  attempted when no playback session is actually open (`MasterPlaybackClock.SeekToCanonicalAsync`'s
  `InvalidOperationException`) or no timeline is loaded at all (`ValidateScrubTarget`'s
  `InvalidOperationException`) — both genuinely analogous to Phase 03's "reject a command whose
  precondition no longer holds, without partially mutating state" principle, just applied to the
  playback clock instead of the document. **"Locked"** has no meaningful analog in this step's scope
  and is explicitly disclosed as not applicable, not silently ignored (§6).
- **`MasterPlaybackClock` lives in `XlightsAI.Studio.Audio`, not `Shell`.** It depends only on
  `IAudioTransport`/`SongAsset` (both already in `Audio`), introduces no new cross-project reference,
  and is itself a pure, stateless-at-construction wrapper — same "pure domain logic, nothing to
  configure" reasoning `ShellViewModel` already applies to `WavAudioDecoder`/`ThreeEffectPlanFactory`/
  `XsqExporter`. Keeping it in `Audio` (not `Shell`) also means a later non-UI consumer (e.g. a future
  phase's beat/snap diagnostics) can depend on it without pulling in `Shell`.
- **Exactly one `MasterPlaybackClock` instance per `ShellViewModel`**, constructed once in the
  constructor from whichever `_audioTransport` this instance already resolved to (real
  `WaveOutAudioTransport` on Windows, `UnsupportedPlatformAudioTransport` elsewhere) — not a new
  per-call wrapper. This is the literal embodiment of "one authoritative playhead": there is exactly
  one object in the running process that ever computes "what is the canonical playback position right
  now," and both `AudioImportViewModel`'s own position display and `TimelineViewModel`'s playhead are
  driven from reading that same object, never two independently-maintained estimates.
- **The Timeline's playhead is pushed by `ShellViewModel`, not pulled by `TimelineViewModel` itself on
  a timer.** `TimelineViewModel` has no knowledge of `IAudioTransport`/`MasterPlaybackClock` at all
  (no new project reference from `Editing`/`Shell`'s `TimelineViewModel` file to anything audio-specific
  beyond what Step 027 already added for waveform rendering) — it only exposes `SetPlayhead`/
  `ValidateScrubTarget` as a plain, audio-agnostic contract. `ShellViewModel` is the one place that
  knows about both screens and keeps them in sync, consistent with its existing role as the
  composition root for every other cross-screen wiring in this project (e.g. `LoadTimelineCommand`
  already reads `Audio.LoadedWaveformCache`/`Audio.ProbedAsset?.OriginTicks` the same way).
- **No new continuous UI-timer/polling loop was added anywhere in this step.** The playhead updates
  exactly when `AudioImportViewModel.PlaybackPositionTicks`/`IsPlaybackOpen` change — i.e., on every
  Open/Play/Pause/Seek/Refresh action, and now also on every Scrub action. A real continuously-ticking
  UI clock (e.g. an Avalonia `DispatcherTimer` driving `RefreshAudioPositionCommand` on an interval)
  is a legitimate future enhancement but is not required by this step's own stated acceptance text
  ("Repeated seeks do not accumulate drift" — a property of the seek/position *conversion*, not of
  continuous playback display) and was not invented speculatively; `RefreshAudioPositionCommand`
  itself (Step 025) already exists as the explicit "pull the latest position" action a UI timer could
  drive in a later step without any change to this step's own contract.
- **"Repeated seeks do not accumulate drift" is proven as a property of `MasterPlaybackClock`/the
  real transport being a pure function of the requested tick** — not by asserting a specific
  tolerance window. `Step028AcceptanceTests.Step028_Acceptance_Passes_RepeatedSeeksToSameTargetDoNotAccumulateDrift`
  seeks to the exact same canonical target 20 times in a row against the real device and asserts
  bit-identical results every time; `...AlternatingSeeksReturnExactlyToTheSameTargetEachTime` seeks
  back and forth between two different targets 10 times and asserts the first target's position is
  recovered exactly every time it is returned to. Both are stronger than a "stays within N ticks"
  tolerance check: they prove zero drift, not merely small drift.

## 4. Commands actually executed

All run via the PowerShell tool (the sandboxed POSIX Bash tool in this session is non-functional —
every invocation returns exit code 1 with no output — the same pre-existing environment limitation
recorded in every prior step's own evidence).

```
dotnet build XlightsAI.Studio.slnx --nologo                                                          # baseline (before any Step 028 code existed): 330/330 passing
dotnet build XlightsAI.Studio.slnx --nologo                                                          # after adding MasterPlaybackClock + TimelineViewModel/ShellViewModel/MainWindow.axaml changes
dotnet test tests\XlightsAI.Studio.Audio.Tests\XlightsAI.Studio.Audio.Tests.csproj --nologo --filter "FullyQualifiedName~Step028" --logger "console;verbosity=normal"
dotnet test tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj --nologo --filter "FullyQualifiedName~Step028" --logger "console;verbosity=normal"
dotnet build XlightsAI.Studio.slnx --nologo                                                           # full regression build
dotnet test XlightsAI.Studio.slnx --nologo                                                            # full regression, final
dotnet build src\XlightsAI.Studio.App\XlightsAI.Studio.App.csproj --nologo                             # app build for manual smoke test
```

### Build results

`Audio`, `Shell`, `App`, and the full solution all: `Build succeeded. 0 Warning(s). 0 Error(s).`

### Targeted test results (new tests only)

```
tests\XlightsAI.Studio.Audio.Tests — filter "~Step028": 5/5 passed (real Windows winmm device):
  Step028_Acceptance_Passes_RepeatedSeeksToSameTargetDoNotAccumulateDrift        [181 ms]
  Step028_Acceptance_Passes_AlternatingSeeksReturnExactlyToTheSameTargetEachTime [173 ms]
  CanonicalPositionTicks_IncludesSongOriginTicks_NotJustRawTransportPosition     [465 ms]
  SeekToCanonicalAsync_OutOfScopeTarget_ThrowsWithoutPartialMutation_NegativePath [188 ms]
  SeekToCanonicalAsync_NoSongOpen_ThrowsInvalidOperationException_StaleSessionNegativePath [2 ms]

tests\XlightsAI.Studio.Shell.Tests — filter "~Step028": 15/15 passed:
  Step028TimelineViewModelTests (8, hardware-free, deterministic):
    SetPlayhead_UpdatesTicksAndVisibilityDerivedProperties
    IsPlayheadVisible_FalseWithoutLoadedViewport_EvenIfHasPlayheadIsTrue
    ValidateScrubTarget_NoViewportLoaded_ThrowsInvalidOperationException_StaleNegativePath
    ValidateScrubTarget_BeyondContentBounds_ThrowsArgumentOutOfRangeException_OutOfScopeNegativePath
    ValidateScrubTarget_WithinContentBounds_DoesNotThrow
    SetPlayhead_RepeatedCallsWithSameTicks_AreIdempotent_NoAccumulation
    Reset_ClearsScrubErrorMessage
    LoadTimeline_ClearsScrubErrorMessage
  Step028ShellWiringTests (7, real Windows winmm device):
    Step028_Acceptance_Passes_AudioScreenActionsPushTheSamePlayheadIntoTimeline  [657 ms]
    Step028_Acceptance_Passes_ScrubPlayheadSeeksTheSharedTransport_RepeatedScrubsDoNotDrift [367 ms]
    ScrubPlayheadCommand_OutOfScopeTarget_SetsErrorWithoutMovingPlayhead_NegativePath [218 ms]
    ScrubPlayheadCommand_UnparsableText_SetsErrorWithoutMovingPlayhead_NegativePath [442 ms]
    ScrubPlayheadCommand_NoPlaybackSessionOpen_SetsStaleErrorWithoutMovingPlayhead_NegativePath [2 ms]
    ScrubTimelinePlayheadCommand_BeforeTimelineLoaded_IsDisabled [2 ms]
    LoadTimeline_WithoutAnyPlaybackOpened_PlayheadIsInactiveAtZero [3 ms]
```

### Full regression (after implementation, final run)

```
Passed!  - Failed: 0, Passed: 71,  Skipped: 0, Total: 71,  Duration: 327 ms - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed: 83,  Skipped: 0, Total: 83,  Duration: 296 ms - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed: 25,  Skipped: 0, Total: 25,  Duration: 2 s   - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed: 106, Skipped: 0, Total: 106, Duration: 1 s   - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed: 34,  Skipped: 0, Total: 34,  Duration: 4 s   - XlightsAI.Studio.Audio.Tests.dll
Passed!  - Failed: 0, Passed: 31,  Skipped: 0, Total: 31,  Duration: 33 s  - XlightsAI.Studio.Reference.Tests.dll
```

**350 total, 0 failed, 0 skipped** — up from the Step 027 baseline of 330 (+5 new `Audio.Tests`,
91→106 in `Shell.Tests` for the +15 new Step028 tests). `Xlights.Tests`, `Editing.Tests`,
`Projects.Tests`, `Reference.Tests` are unchanged in count (no source in those projects was modified
this step). All 330 pre-existing tests still pass unmodified.

### Manual/desktop demonstration

```
$p = Start-Process -FilePath "src\XlightsAI.Studio.App\bin\Debug\net10.0\XlightsAI.Studio.exe" -PassThru
Start-Sleep -Seconds 5
# Still running after 5s: True
Stop-Process -Id $p.Id -Force
```

The real compiled Avalonia desktop app, now including the new playhead marker overlay and "Playhead"
sub-section (ticks display, empty-state message, "Scrub to (ticks)" field + button, error display) on
SCREEN 13 TIMELINE, started and ran for 5+ seconds with no exception and no early exit — proving the
new UI wiring did not regress app startup. This is a process-alive smoke test, **not** a human-driven
click-through of Open Playback → Play/Seek on the Audio screen → navigate to Timeline → observe the
playhead marker move → Scrub Playhead → observe it jump (disclosed, not silently implied, in §6
below) — no UI-automation driver is available in this environment for the native Avalonia window, the
same standing limitation every prior PHASE-02/03/04 step's own evidence already records for its own
screen additions. The full vertical slice (real transport seek → `MasterPlaybackClock` →
`ShellViewModel`'s `Audio.PropertyChanged` push → `TimelineViewModel.PlayheadTicks`/`PlayheadX`/
`PlayheadMarginLeft`) is instead proven, end to end, against the real device by
`Step028ShellWiringTests` (§4 above), which exercises the exact same `ShellViewModel`/
`AudioImportViewModel`/`TimelineViewModel`/`MasterPlaybackClock` code path the UI binds to, including
the real `WaveOutAudioTransport.PositionTicks` value the playhead marker's pixel position is computed
from.

## 5. Fixtures / input hashes

No new fixture files were added to `fixtures/`. All tests construct real WAV bytes via this
repository's own already-licensed, already-verified `DeterministicSyntheticAudioGenerator`
(`XlightsAI.Studio.Reference.Fixtures`, PHASE-01 Step 003) — the same synthesizer every prior
PHASE-04 step's own tests use. Specific inputs exercised:

- `BuildClickTrack(bpm: 240, clickCount: 6, clickDurationSeconds: 0.02, toneHz: 1000.0, sampleRate: 8000)`
  — a 1.5-second real-time click track (`Step028AcceptanceTests`), the same fixture shape Step 025's
  own tests use, for the real-device drift/canonical-conversion/negative-path tests.
- `BuildClickTrack(bpm: 240, clickCount: 6, ...)` via `Step028ShellWiringTests`' own
  `CreateReadyShellWithOpenPlaybackAndTimelineAsync` helper — probed, Open-Playback'd, and
  Timeline-loaded through the real, un-mocked `ShellViewModel`/`AudioImportViewModel`/
  `TimelineViewModel`/`MasterPlaybackClock` chain.
- `Step028TimelineViewModelTests` constructs a plain `TimelineViewModel` directly with a synthetic
  `TotalDurationTicks` (no real audio file needed at all — this is the correct, pure domain input
  shape for testing `SetPlayhead`/`ValidateScrubTarget`'s own contract, independent of decode/
  transport correctness already proven elsewhere).

All test files are written under `%TEMP%\xlai-step028-*\` / `%TEMP%\xlai-step028-shell-*\`, deleted
by each test class's `Dispose()`/`DisposeAsync()` — identical cleanup convention to every prior step's
acceptance test class. Each real `WaveOutAudioTransport` is itself disposed (`await using` /
`IAsyncLifetime.DisposeAsync`) so the real device handle is released before the next test opens it.

## 6. Unresolved issues / known gaps

- **This step's own stated failure/edge case text is the spec's templated boilerplate from the
  editing/`EditTransaction` steps, reinterpreted for this step's actual scope** (§3) — disclosed with
  its own reasoning; "locked" specifically has no meaningful analog in this step's own surface (no
  document mutation occurs here) and is not exercised by any test, unlike "out-of-scope" and "stale,"
  which are both fully covered.
- **No continuous UI-timer-driven playhead advance while playing** (§3) — the playhead updates on
  every explicit Open/Play/Pause/Seek/Refresh/Scrub action, not on a free-running interval while
  audio is actively playing between actions. This is a disclosed, deliberate scope limit (not
  required by this step's own acceptance text, and `RefreshAudioPositionCommand` already exists as
  the hook a later step's timer could drive) rather than an oversight.
- **No UI-automation-driven click-through of the new playhead marker/scrub controls** — same
  disclosed, standing limitation as every prior step (no accessibility/UI-driver tooling available in
  this environment for the native Avalonia window). The full vertical slice is proven instead at the
  `ShellViewModel`/`AudioImportViewModel`/`TimelineViewModel`/`MasterPlaybackClock` layer through
  real, un-mocked collaborators and the real device (`Step028ShellWiringTests`).
- **No real xLights installation available in this environment** — per the user's own standing waiver
  on requiring a real-xLights render check as a phase-gate blocker (recorded in this assistant's
  memory). Not applicable to this step's own required acceptance in any case: Step 028's acceptance is
  about seek/playhead drift, not xLights rendering.
- **Drag an effect to a click / A-B loop playback / close-reopen (the phase-wide gate demonstration)
  remain explicitly out of this step's own scope** — Steps 029–030's own named scope (zoom-to-
  selection/A-B loop/keyboard navigation, frame/beat snapping). This step proves the master clock
  itself is drift-free and shared — not the full phase gate workflow.

## 7. PASS / BLOCKED

**Status: PASS** for Step 028's own stated required acceptance and failure/edge case:

- "Repeated seeks do not accumulate drift" — proven at the real-device transport/clock layer by
  `Step028_Acceptance_Passes_RepeatedSeeksToSameTargetDoNotAccumulateDrift` (20 repeated seeks to the
  same target, bit-identical result every time) and
  `Step028_Acceptance_Passes_AlternatingSeeksReturnExactlyToTheSameTargetEachTime` (10 round trips
  between two targets, exact recovery every time), and independently at the full Shell-wired
  vertical-slice layer by `Step028_Acceptance_Passes_ScrubPlayheadSeeksTheSharedTransport_RepeatedScrubsDoNotDrift`
  (5 repeated UI-level scrub commands to the same target, against the real device, exact agreement
  between `Timeline.PlayheadTicks` and `transport.PositionTicks` every time).
- "Expose one authoritative playhead and scrub preview" (implementation guidance) — proven by
  `Step028_Acceptance_Passes_AudioScreenActionsPushTheSamePlayheadIntoTimeline` (a real Audio-screen
  Seek action's resulting position is exactly mirrored into `Timeline.PlayheadTicks` with no second,
  independently-computed value) and the scrub-preview tests above (the Timeline's own "Scrub Playhead"
  action genuinely moves the one real shared transport, not a cosmetic-only preview value).
- "verify time conversion uses song-origin and sample-rate not UI frame interval" (implementation
  guidance) — proven by `CanonicalPositionTicks_IncludesSongOriginTicks_NotJustRawTransportPosition`:
  a non-zero `SongAsset.OriginTicks` is genuinely added to the transport's own sample-rate-derived
  `PositionTicks`, and `MasterPlaybackClock` contains no timer/frame-interval-based logic anywhere in
  its implementation (§2) — every value it reports or accepts round-trips through the real transport's
  own `TicksToSamples`/`SamplesToTicks` conversions established in Step 025.
- "Out-of-scope... must be rejected without partial mutation" (this step's own interpretation, §3) —
  proven by `SeekToCanonicalAsync_OutOfScopeTarget_ThrowsWithoutPartialMutation_NegativePath` (the
  real device position is asserted unchanged after a rejected out-of-range seek),
  `ValidateScrubTarget_BeyondContentBounds_ThrowsArgumentOutOfRangeException_OutOfScopeNegativePath`,
  and `ScrubPlayheadCommand_OutOfScopeTarget_SetsErrorWithoutMovingPlayhead_NegativePath` (full
  Shell-wired layer: both `Timeline.PlayheadTicks` and the real `transport.PositionTicks` are asserted
  unchanged after the rejected scrub).
- "...or stale revision must be rejected without partial mutation" (this step's own interpretation,
  §3) — proven by `SeekToCanonicalAsync_NoSongOpen_ThrowsInvalidOperationException_StaleSessionNegativePath`,
  `ValidateScrubTarget_NoViewportLoaded_ThrowsInvalidOperationException_StaleNegativePath`, and
  `ScrubPlayheadCommand_NoPlaybackSessionOpen_SetsStaleErrorWithoutMovingPlayhead_NegativePath` (full
  Shell-wired layer: a timeline loaded with no playback session ever opened rejects a scrub with the
  playhead left at its untouched neutral `0`/inactive state).
- The step's own user-visible outcome ("Versioned, inspectable project output plus a reproducible test
  report") is proven: the playhead's real tick value and visibility are directly inspectable on-screen
  (SCREEN 13 TIMELINE's new "Playhead" sub-section and marker overlay), and every claim above is
  backed by an actually-executed, actually-passing automated test (the "reproducible test report"
  half) — see §4.

No previously accepted step's contract was modified — `SongAsset`, `IAudioDecoder`/`WavAudioDecoder`,
`IAudioTransport`/`WaveOutAudioTransport`/`UnsupportedPlatformAudioTransport`,
`WaveformEnvelope`/`WaveformResolutionLevel`/`WaveformPyramidCache`/`WaveformBarLayoutBuilder`,
`TimelineViewport`/`ITimelineViewport`/`TimelineRulerTickGenerator`/`EffectBlockLayoutBuilder`
(PHASE-03), and every other PHASE-01–03 public member are completely unchanged — only purely additive
members were added to `TimelineViewModel`/`ShellViewModel` — confirmed by the full pre-existing
330-test suite passing unmodified before a single Step 028 line of code existed (§1), and again at
350/350 (330 old + 20 new) after implementation (§4).

## 8. Phase-wide status after this session

Steps 024, 025, 026, 027, and 028 (the five steps with recorded PASS evidence) are complete for
PHASE 04. Steps 029–030 (zoom-to-selection/A-B loop playback/keyboard navigation, frame/beat/off-grid
snapping and timing diagnostics) and the phase-wide gate demonstration ("Import a click track, display
waveforms, drag an effect to a click, loop 1 second, seek to song midpoint and back, close/reopen and
confirm saved media identity and correct timing") remain unimplemented — per the task's own
instruction, this session implements only Step 028 and does not proceed to Step 029 until this
evidence is reviewed.
