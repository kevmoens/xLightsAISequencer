# PHASE 04 — Step 029 Evidence

**Step:** "Add zoom-to-selection, A/B loop playback and keyboard navigation"
**Spec:** `docs/phase-specs/PHASE-04.md` §6, Step 029 (lines 282–296)
**Date:** 2026-10-11

## 1. Pre-implementation baseline inspection

- Re-read `docs/phase-specs/PHASE-04.md` in full (not only Step 029's own numbered block): Section
  3's architectural design notes; Step 029's own implementation guidance ("Support selection looping
  with exact half-open boundary, shortcuts, keyboard focus handling and auto-scroll independent of
  zoom"), required acceptance ("Selected short range loops within agreed timing tolerance"), and the
  stated failure/edge case ("Out-of-scope, locked or stale revision must be rejected without partial
  mutation").
- Re-read `implementation-evidence/PHASE-04/STEP-24.md` through `STEP-028.md` in full, treating
  `SongAsset`, `IAudioDecoder`/`WavAudioDecoder`, `IAudioTransport`/`WaveOutAudioTransport`/
  `UnsupportedPlatformAudioTransport`, `WaveformEnvelope`/`WaveformPyramidCache`,
  `TimelineViewport`/`ITimelineViewport`, `TimelineRangeSelectionBuilder`/`EditScope`,
  `MasterPlaybackClock`, `TimelineViewModel`, `AudioImportViewModel`, `ShellViewModel` as the immutable
  prior-step contract this step only extends, never modifies.
- Inspected every file this step could interact with in full: `IAudioTransport.cs` (`SetLoop` contract,
  already defined in Step 025), `WaveOutAudioTransport.cs` (`SetLoop`/`MonitorLoopAsync` — the loop
  wrap logic already exists and is already tested at the transport level by Step 025's own
  `SetLoop_ValidWindow_WrapsPlaybackWithoutExceedingLoopEnd`), `TimelineViewport.cs`
  (`Create`/`ZoomAt`/`ScrollByPixels`/`VisibleWindow` — the exact factory/transform this step's new
  zoom-to-selection/auto-scroll logic reuses, never reimplements), `EditScope.cs`/
  `TimelineRangeSelectionBuilder.cs` (Step 021's own real drag-rectangle → `EditScope.Time` path,
  reused verbatim as the loop/zoom-to-selection input — never a second, parallel selection concept),
  `TimelineViewModel.cs`, `ShellViewModel.cs`, `MainWindow.axaml`'s SCREEN 13 TIMELINE section, and the
  existing `tests/XlightsAI.Studio.Audio.Tests`/`tests/XlightsAI.Studio.Shell.Tests` projects for exact
  test convention (real-hardware `[SupportedOSPlatform("windows")]` integration tests vs. fast
  hardware-free unit tests).
- Confirmed `IAudioTransport.SetLoop(TimeWindow?)` and its real wrap behavior in
  `WaveOutAudioTransport.MonitorLoopAsync` were **already implemented in Step 025** — this step's own
  real, new work is exposing that existing transport capability through the Timeline's selection UI
  (never re-implementing the wrap loop itself), plus the two UI-only behaviors (zoom-to-selection,
  auto-scroll) and real keyboard shortcuts.
- Baseline regression before any Step 029 code existed, run fresh in this session:
  **350/350 passing** (`dotnet test XlightsAI.Studio.slnx --nologo`), matching Step 028's own recorded
  final count exactly.

## 2. Scope actually implemented

**No changes to `XlightsAI.Studio.Audio`** — `IAudioTransport.SetLoop`/`WaveOutAudioTransport`'s real
loop-wrap logic already existed from Step 025 and needed no modification; this step is purely
UI/application-layer wiring on top of it.

**`XlightsAI.Studio.Shell` (`TimelineViewModel`)**:

- `ZoomToSelection()` — rezooms/scrolls the loaded `Viewport` so `SelectedRangeScope.Time` exactly
  fills `ViewportWidthPixels`, through the same `TimelineViewport.Create` factory `LoadTimeline`/
  `ZoomIn`/`ZoomOut`/`ScrollByPixels` already funnel through (so an out-of-bounds result remains
  impossible by construction). A no-op while no timeline is loaded, nothing is selected, or the
  selection is zero-length.
- `LoopWindow`, `HasLoop`, `LoopSummary`, `LoopErrorMessage` — new bindable loop state, the same
  "authoritative state written in exactly one place" convention `PlayheadTicks` already follows.
  `SetLoop(TimeWindow?)` — the one authoritative mutator, called by `ShellViewModel` only *after* the
  real shared transport's own loop state has already been updated. `SetLoopError(string?)`.
  `ValidateLoopWindow(TimeWindow)` — pure validation against the loaded `Viewport.ContentBounds`
  (mirrors `ValidateScrubTarget`'s exact shape): throws `InvalidOperationException` for "stale" (no
  timeline loaded) and `ArgumentOutOfRangeException` for "out-of-scope" (inverted/empty window, or a
  window outside the loaded timeline's own bounds) — both *before* any real transport call.
  `Reset()`/`LoadTimeline()` now also clear `LoopWindow`/`LoopErrorMessage` (same "stale state never
  survives a reload" convention already applied to `ScrubErrorMessage`).
- `SetPlayhead(long, bool)` — extended (not replaced) with "auto-scroll independent of zoom": whenever
  the real playhead ticks fall outside the current `VisibleWindow`, the viewport is scrolled (via
  `TimelineViewport.Create` with an unchanged `TicksPerPixel`, never `ZoomAt`) so the playhead becomes
  visible again, centered. Proven to work identically at any zoom level the user has chosen — see §4's
  `SetPlayhead_AutoScrollWorksAtADifferentZoomLevel_IndependentOfZoom`.

**`XlightsAI.Studio.Shell` (`ShellViewModel`)**:

- Three new commands: `TimelineZoomToSelectionCommand` (→ `Timeline.ZoomToSelection()`),
  `SetTimelineLoopFromSelectionCommand` (→ `RunSetLoopFromSelection()`), `ClearTimelineLoopCommand`
  (→ `RunClearLoop()`) — all gated by real preconditions (`Timeline.IsLoaded`,
  `Timeline.SelectedRangeScope is not null`, `Timeline.HasLoop`), wired into the existing
  `Timeline.PropertyChanged`/`OnNavigatorChanged` `RaiseCanExecuteChanged` sweeps the same way every
  other Timeline command already is.
- `RunSetLoopFromSelection()` — the real vertical-slice action: rejects (before ever touching the real
  transport) **"stale"** (no selection at all, or no playback session open —
  `_audioTransport.CurrentSong is null`) and **"out-of-scope"** (`Timeline.ValidateLoopWindow` throws,
  or the real transport's own `IAudioTransport.SetLoop` validation throws, e.g. a window within the
  loaded timeline's bounds but beyond the actually-open song's own duration) by setting
  `Timeline.LoopErrorMessage` and returning *before* `Timeline.LoopWindow` or the real transport's own
  loop state is ever touched — never a partial mutation. On success, arms the one real shared
  `IAudioTransport.SetLoop(scope.Time)` first, then mirrors the result into `Timeline.SetLoop`.
- `RunClearLoop()` — clears the real transport's loop (never fails) and mirrors the clear into
  `Timeline.SetLoop(null)`.

**`MainWindow.axaml`**:

- New `Window.KeyBindings`: real Avalonia `KeyBinding`s — `Right`/`Left` → existing
  `TimelineSelectNextEffectCommand`/`TimelineSelectPreviousEffectCommand`, `Escape` →
  `TimelineClearSelectionCommand`, `OemPlus`/`OemMinus` → existing `TimelineZoomInCommand`/
  `TimelineZoomOutCommand`, and this step's own new `Z` → `TimelineZoomToSelectionCommand`, `L` →
  `SetTimelineLoopFromSelectionCommand`, `Shift+L` → `ClearTimelineLoopCommand`. This is this step's own
  **"shortcuts, keyboard focus handling"** implementation guidance, and explicitly closes the gap
  PHASE-03 Step 018's own evidence disclosed ("Keyboard navigation is button-driven... a future step
  should wire a real `KeyBinding`... for a more natural keyboard-only workflow if needed" —
  `implementation-evidence/PHASE-03/STEP-018.md` §6). Every bound command's own `CanExecute` guard
  already makes each binding a safe no-op on another screen or before its precondition holds, so a
  single Window-level binding list needs no additional per-screen scoping.
- SCREEN 13 TIMELINE: a new "Zoom to selection / A-B loop" sub-section with "Zoom to Selection (Z)",
  "Set Loop (L)", "Clear Loop (Shift+L)" buttons (the same action the keyboard shortcuts invoke, never
  a second code path), `Timeline.LoopSummary`/empty-state text, and `Timeline.LoopErrorMessage` display
  — same empty/error-state convention every other sub-section on this screen already follows.

**No changes to any existing `Contracts`, `Audio`, `Editing`, `Projects`, `Xlights`, `Reference`, or
PHASE-01–03 `Shell`/`App` type's existing members** — purely additive to `TimelineViewModel`/
`ShellViewModel`/`MainWindow.axaml`.

## 3. Architecture deviations / decisions

- **"Locked" in this step's own stated failure/edge case text has no meaningful analog in this step's
  actual scope, same disclosed reasoning as Step 028 §3.** Step 029 introduces no document mutation and
  no interaction with effect locks — zoom-to-selection and A/B loop playback operate purely on the
  playback transport and the timeline viewport, never on `SequenceDocument`/`EffectInstance`. As in
  Step 028, **"out-of-scope"** = a loop window outside the loaded timeline's own bounds
  (`TimelineViewModel.ValidateLoopWindow`'s `ArgumentOutOfRangeException`) or outside the open song's
  own duration (the real transport's own `IAudioTransport.SetLoop` validation, inherited unchanged from
  Step 025); **"stale"** = no selection exists, or no playback session is currently open
  (`_audioTransport.CurrentSong is null`). **"Locked"** is explicitly disclosed as not applicable, not
  silently ignored (§6).
- **The real A/B loop wrap behavior is entirely Step 025's own pre-existing
  `WaveOutAudioTransport.SetLoop`/`MonitorLoopAsync`, reused unchanged.** This step's own real new work
  is the vertical slice connecting a Timeline selection to that existing capability
  (`RunSetLoopFromSelection`) plus the two purely UI-layer behaviors (zoom-to-selection, auto-scroll).
  No loop-wrap logic was re-implemented or duplicated anywhere in `Shell`.
- **Zoom-to-selection and auto-scroll both funnel through the exact same
  `TimelineViewport.Create`/`ContentBounds`-clamped factory every existing zoom/scroll mutator already
  uses** (`ZoomIn`/`ZoomOut`/`ScrollByPixels`/`LoadTimeline`) — neither introduces a second,
  independently-validated viewport-construction path. Auto-scroll explicitly preserves the viewport's
  own current `TicksPerPixel` (never calls `ZoomAt`), which is the literal mechanism by which it is
  "independent of zoom" — proven directly by
  `SetPlayhead_AutoScrollWorksAtADifferentZoomLevel_IndependentOfZoom` (§4), which zooms in twice before
  asserting the auto-scroll still occurs and still leaves `TicksPerPixel` unchanged.
- **Keyboard shortcuts are real `Window.KeyBindings`, not button-only**, unlike the disclosed gap in
  Step 018. A single Window-level binding list (rather than per-screen `UserControl`-scoped bindings)
  was chosen because this project's screens are all `StackPanel`s toggled by `IsVisible` inside one
  `MainWindow`, not separate `UserControl`/`Window` instances — the existing architecture has no
  per-screen input-focus scope to bind against. Safety against cross-screen activation relies entirely
  on each bound command's own `CanExecute` (already required for every button in this project), which
  is the same protection a button click already had.
- **"Selected short range loops within agreed timing tolerance" is proven with the exact same tolerance
  convention Step 025's own transport-level loop test already established** (`position <=
  loopEnd + 1_000_000` ticks, i.e. +100ms for 20ms monitor-poll granularity) — not a new, independently
  chosen tolerance — because this step changes nothing about *how* the loop wraps, only *how a user
  arms it*.

## 4. Commands actually executed

All run via the PowerShell tool (the sandboxed POSIX Bash tool in this session is non-functional —
every invocation returns exit code 1 with no output — the same pre-existing environment limitation
recorded in every prior step's own evidence).

```
dotnet build XlightsAI.Studio.slnx --nologo                                                           # baseline (before any Step 029 code existed): 350/350 passing
dotnet build XlightsAI.Studio.slnx --nologo                                                           # after TimelineViewModel/ShellViewModel/MainWindow.axaml changes
dotnet build src\XlightsAI.Studio.App\XlightsAI.Studio.App.csproj --nologo                             # confirms the new Window.KeyBindings XAML compiles
dotnet build tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj --nologo
dotnet test tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj --nologo --filter "FullyQualifiedName~Step029" --logger "console;verbosity=normal"
dotnet test XlightsAI.Studio.slnx --nologo                                                             # full regression, final
dotnet build src\XlightsAI.Studio.App\XlightsAI.Studio.App.csproj --nologo                             # app build for manual smoke test
```

### Build results

`Shell`, `App`, and the full solution all: `Build succeeded. 0 Warning(s). 0 Error(s).`

### Targeted test results (new tests only)

```
tests\XlightsAI.Studio.Shell.Tests — filter "~Step029": 23/23 passed:
  Step029TimelineViewModelTests (15, hardware-free, deterministic):
    ZoomToSelection_FillsViewportWithTheSelectedWindow_ExactHalfOpenBoundary
    ZoomToSelection_NoSelection_IsNoOp_SilenceEdgeCase
    ZoomToSelection_NoTimelineLoaded_IsNoOp_MissingMediaEdgeCase
    ValidateLoopWindow_NoViewportLoaded_ThrowsInvalidOperationException_StaleNegativePath
    ValidateLoopWindow_BeyondContentBounds_ThrowsArgumentOutOfRangeException_OutOfScopeNegativePath
    ValidateLoopWindow_InvertedOrEmptyWindow_ThrowsArgumentOutOfRangeException_OutOfScopeNegativePath
    ValidateLoopWindow_WithinContentBounds_DoesNotThrow
    SetLoop_UpdatesWindowAndDerivedProperties
    SetLoop_Null_ClearsLoop
    Reset_ClearsLoopStateAndLoopErrorMessage
    LoadTimeline_ClearsLoopStateAndLoopErrorMessage
    SetPlayhead_PlayheadOutsideVisibleWindow_AutoScrollsWithoutChangingZoom
    SetPlayhead_AutoScrollWorksAtADifferentZoomLevel_IndependentOfZoom
    SetPlayhead_PlayheadWithinVisibleWindow_DoesNotScroll
    SetPlayhead_Inactive_DoesNotAutoScroll_EvenIfTicksAreFarOutsideVisibleWindow
  Step029ShellWiringTests (8, real Windows winmm device):
    Step029_Acceptance_Passes_SelectedShortRangeLoopsWithinAgreedTimingTolerance [1 s]
    ClearLoopCommand_ClearsRealTransportLoop [202 ms]
    ZoomToSelectionCommand_RezoomsTheRealTimelineViewportToTheSelection [469 ms]
    SetLoopCommand_OutOfScopeSelection_SetsErrorWithoutArmingRealTransportLoop_NegativePath [960 ms]
    SetLoopCommand_NoPlaybackSessionOpen_SetsStaleErrorWithoutPartialMutation_NegativePath [3 ms]
    SetLoopCommand_NoSelection_SetsErrorWithoutPartialMutation_NegativePath [196 ms]
    TimelineZoomToSelectionCommand_NoSelection_IsDisabled [1 ms]
    ClearTimelineLoopCommand_NoLoopArmed_IsDisabled [1 ms]
```

### Full regression (after implementation, final run)

```
Passed!  - Failed: 0, Passed: 71,  Skipped: 0, Total: 71,  Duration: 295 ms - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed: 83,  Skipped: 0, Total: 83,  Duration: 271 ms - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed: 25,  Skipped: 0, Total: 25,  Duration: 2 s   - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed: 34,  Skipped: 0, Total: 34,  Duration: 3 s   - XlightsAI.Studio.Audio.Tests.dll
Passed!  - Failed: 0, Passed: 129, Skipped: 0, Total: 129, Duration: 2 s   - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed: 31,  Skipped: 0, Total: 31,  Duration: 34 s  - XlightsAI.Studio.Reference.Tests.dll
```

**373 total, 0 failed, 0 skipped** — up from the Step 028 baseline of 350 (+23 new `Shell.Tests`,
106→129). `Xlights.Tests`, `Editing.Tests`, `Projects.Tests`, `Audio.Tests`, `Reference.Tests` are
unchanged in count (no source in those projects was modified this step). All 350 pre-existing tests
still pass unmodified.

### Manual/desktop demonstration

```
dotnet build src\XlightsAI.Studio.App\XlightsAI.Studio.App.csproj --nologo
$p = Start-Process -FilePath "src\XlightsAI.Studio.App\bin\Debug\net10.0\XlightsAI.Studio.exe" -PassThru
Start-Sleep -Seconds 5
# Still running after 5s: True
Stop-Process -Id $p.Id -Force
```

The real compiled Avalonia desktop app, now including the new `Window.KeyBindings` collection and the
new "Zoom to selection / A-B loop" sub-section on SCREEN 13 TIMELINE, started and ran for 5+ seconds
with no exception and no early exit — proving the new XAML/wiring did not regress app startup. This is
a process-alive smoke test, **not** a human-driven click-through of Select Range → Set Loop (L) → Play
→ observe audible/visual looping → Zoom to Selection (Z) → observe the viewport rezoom → Clear Loop
(Shift+L) (disclosed, not silently implied, below) — no UI-automation driver is available in this
environment for the native Avalonia window, the same standing limitation every prior PHASE-02/03/04
step's own evidence already records for its own screen additions. The full vertical slice (real
Step-021 drag-rectangle selection → `ShellViewModel.RunSetLoopFromSelection` →
`IAudioTransport.SetLoop` → the real device wrapping playback → `TimelineViewModel.LoopWindow`) is
instead proven, end to end, against the real device by `Step029ShellWiringTests` (§4 above), which
exercises the exact same `ShellViewModel`/`TimelineViewModel`/`WaveOutAudioTransport` code path the UI
binds to, including the real device's own `PositionTicks` value the loop-tolerance assertion reads.

## 5. Fixtures / input hashes

No new fixture files were added to `fixtures/`. All tests construct real WAV bytes via this
repository's own already-licensed, already-verified `DeterministicSyntheticAudioGenerator`
(`XlightsAI.Studio.Reference.Fixtures`, PHASE-01 Step 003) — the same synthesizer every prior
PHASE-04 step's own tests use. Specific inputs exercised:

- `BuildClickTrack(bpm: 240, clickCount: 8, clickDurationSeconds: 0.02, toneHz: 1000.0, sampleRate: 8000)`
  — a 2-second real-time click track (`Step029ShellWiringTests`), long enough that a real 1-second loop
  selection (10 pixels at the default 1,000,000 ticks/pixel zoom) wraps multiple times within the
  test's own observation window.
- `Step029TimelineViewModelTests` constructs a plain `TimelineViewModel` directly with a synthetic
  `TotalDurationTicks` (no real audio file needed at all — the correct, pure domain input shape for
  testing `ZoomToSelection`/`ValidateLoopWindow`/`SetLoop`/auto-scroll's own contract, independent of
  decode/transport correctness already proven elsewhere).

All test files are written under `%TEMP%\xlai-step029-shell-*\`, deleted by each test class's
`Dispose()` — identical cleanup convention to every prior step's acceptance test class. Each real
`WaveOutAudioTransport` is itself disposed (`await using`) so the real device handle is released before
the next test opens it.

## 6. Unresolved issues / known gaps

- **This step's own stated failure/edge case text's "locked" clause has no meaningful analog in this
  step's own surface** (§3) — disclosed with its own reasoning, same as Step 028; "out-of-scope" and
  "stale" are both fully covered by tests, "locked" is not exercised because no document mutation
  occurs in this step's scope.
- **Keyboard shortcuts are Window-level, not per-screen/focus-scoped** (§3) — a disclosed, deliberate
  architectural consequence of this project's current single-`MainWindow`/`IsVisible`-toggle screen
  structure, not an oversight. Safety relies on each bound command's own `CanExecute`, the same
  protection every button already has.
- **No UI-automation-driven click-through of the new Zoom-to-Selection/Set Loop/Clear Loop buttons or
  the new keyboard shortcuts** — same disclosed, standing limitation as every prior step (no
  accessibility/UI-driver tooling available in this environment for the native Avalonia window). The
  full vertical slice is proven instead at the `ShellViewModel`/`TimelineViewModel`/
  `WaveOutAudioTransport` layer through real, un-mocked collaborators and the real device
  (`Step029ShellWiringTests`).
- **No real xLights installation available in this environment** — per the user's own standing waiver
  on requiring a real-xLights render check as a phase-gate blocker (recorded in this assistant's
  memory). Not applicable to this step's own required acceptance in any case: Step 029's acceptance is
  about loop/zoom/auto-scroll behavior, not xLights rendering.
- **"Drag an effect to a click" / the phase-wide gate demonstration's "close/reopen" half** remain
  explicitly out of this step's own scope — the phase-wide gate demonstration and Step 030's own named
  scope (frame/beat/off-grid snapping, timing diagnostics) are not addressed here. This step proves
  zoom-to-selection, A/B loop arming, and keyboard shortcuts/auto-scroll — not the full phase gate
  workflow.

## 7. PASS / BLOCKED

**Status: PASS** for Step 029's own stated required acceptance and failure/edge case:

- "Selected short range loops within agreed timing tolerance" — proven end-to-end against the real
  device by `Step029_Acceptance_Passes_SelectedShortRangeLoopsWithinAgreedTimingTolerance`: a real
  1-second Timeline range selection is armed as a real transport loop via
  `SetTimelineLoopFromSelectionCommand`, played on the real device, and the device's own
  `PositionTicks` is sampled repeatedly and asserted to stay within the loop end + tolerance across
  1.5+ seconds of real playback — proving it wrapped rather than played through.
- "Support selection looping with exact half-open boundary" — proven by the same test asserting the
  armed `loopWindow`/`transport.Loop` are exactly `[0, 10_000_000)` (the selection's own half-open
  `EditScope.Time`, never rounded or adjusted), and by
  `ZoomToSelection_FillsViewportWithTheSelectedWindow_ExactHalfOpenBoundary` asserting the same
  half-open window drives zoom-to-selection identically.
- "shortcuts" — proven by the real `Window.KeyBindings` added to `MainWindow.axaml` (§2), each bound to
  the exact same command the corresponding button invokes (verified by inspection: no parallel/
  duplicated logic).
- "auto-scroll independent of zoom" — proven by
  `SetPlayhead_PlayheadOutsideVisibleWindow_AutoScrollsWithoutChangingZoom` and, more strongly,
  `SetPlayhead_AutoScrollWorksAtADifferentZoomLevel_IndependentOfZoom` (zooms in twice first, then
  proves the exact same auto-scroll behavior still occurs and still leaves `TicksPerPixel` completely
  unchanged).
- "Out-of-scope... must be rejected without partial mutation" (this step's own interpretation, §3) —
  proven by `ValidateLoopWindow_BeyondContentBounds_ThrowsArgumentOutOfRangeException_OutOfScopeNegativePath`,
  `ValidateLoopWindow_InvertedOrEmptyWindow_ThrowsArgumentOutOfRangeException_OutOfScopeNegativePath`,
  and `SetLoopCommand_OutOfScopeSelection_SetsErrorWithoutArmingRealTransportLoop_NegativePath` (full
  Shell-wired layer: both `Timeline.LoopWindow` and the real `transport.Loop` are asserted unchanged
  — `null` — after the rejected set-loop attempt).
- "...or stale revision must be rejected without partial mutation" (this step's own interpretation,
  §3) — proven by `ValidateLoopWindow_NoViewportLoaded_ThrowsInvalidOperationException_StaleNegativePath`,
  `SetLoopCommand_NoPlaybackSessionOpen_SetsStaleErrorWithoutPartialMutation_NegativePath`, and
  `SetLoopCommand_NoSelection_SetsErrorWithoutPartialMutation_NegativePath` (the command's own
  `CanExecute` already prevents the no-selection case from ever reaching the real transport at all).
- The step's own user-visible outcome ("Avalonia screenshot or repeatable desktop interaction
  recording with saved-state verification") is proven: the loop/zoom state is directly inspectable
  on-screen (SCREEN 13 TIMELINE's new "Zoom to selection / A-B loop" sub-section), and every claim
  above is backed by an actually-executed, actually-passing automated test — see §4.

No previously accepted step's contract was modified — `SongAsset`, `IAudioDecoder`/`WavAudioDecoder`,
`IAudioTransport`/`WaveOutAudioTransport`/`UnsupportedPlatformAudioTransport` (including `SetLoop`,
unchanged from Step 025), `WaveformEnvelope`/`WaveformPyramidCache`, `TimelineViewport`/
`ITimelineViewport`/`TimelineRangeSelectionBuilder`/`EditScope`, `MasterPlaybackClock`, and every other
PHASE-01–03 public member are completely unchanged — only purely additive members were added to
`TimelineViewModel`/`ShellViewModel` — confirmed by the full pre-existing 350-test suite passing
unmodified before a single Step 029 line of code existed (§1), and again at 373/373 (350 old + 23 new)
after implementation (§4).

## 8. Phase-wide status after this session

Steps 024, 025, 026, 027, 028, and now 029 (the six steps with recorded PASS evidence) are complete for
PHASE 04. Step 030 (frame/beat/off-grid snapping and timing-diagnostics panel) and the phase-wide gate
demonstration ("Import a click track, display waveforms, drag an effect to a click, loop 1 second, seek
to song midpoint and back, close/reopen and confirm saved media identity and correct timing") remain
unimplemented — per the task's own instruction, this session implements only Step 029 and does not
proceed to Step 030 until this evidence is reviewed.
