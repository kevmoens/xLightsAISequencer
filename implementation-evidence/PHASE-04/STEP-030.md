# PHASE 04 — Step 030 Evidence

**Step:** "Add frame/beat/off-grid snapping and timing-diagnostics panel"
**Spec:** `docs/phase-specs/PHASE-04.md` §6, Step 030 (lines 298–312)
**Date:** 2026-10-11

## 1. Pre-implementation baseline inspection

- Re-read `docs/phase-specs/PHASE-04.md` in full (not only Step 030's own numbered block): Section 2's
  "Convert to xLights frames only when explicitly snapping or exporting"; Section 3's architectural
  design notes; Step 030's own implementation guidance ("Implement snap policy (none, frame, later
  beat/note) as dependency not baked into drag math; gather sync/drift telemetry"), required acceptance
  ("Time-to-frame conversion and boundary rounding golden tests"), and stated failure/edge case
  ("Silence, invalid timing boundaries and missing media must be handled explicitly").
- Re-read `implementation-evidence/PHASE-04/STEP-24.md` through `STEP-029.md` in full, treating every
  existing contract (`SongAsset`, `IAudioDecoder`/`WavAudioDecoder`, `IAudioTransport`/
  `WaveOutAudioTransport`/`UnsupportedPlatformAudioTransport`, `WaveformEnvelope`/
  `WaveformPyramidCache`, `TimelineViewport`/`ITimelineViewport`, `TimelineRangeSelectionBuilder`/
  `EditScope`, `MasterPlaybackClock`, `TimelineViewModel`, `ShellViewModel`) as the immutable
  prior-step contract this step only extends, never modifies.
- Searched the existing repository for any prior "snap"/"frame rate" concept before inventing a new one
  (per Section 8's own "inspect the current committed code... before implementation" guidance):
  - **`EditTimeSnapping.SnapTicks`** (`XlightsAI.Studio.Editing`, PHASE-03 Step 019) already exists: a
    pure, stateless "round to nearest multiple of an interval, or pass through unchanged" helper. It was
    never wired into any real UI drag/scrub call site — Step 019's own tests exercise it directly, not
    through `TimelineViewModel`/`ShellViewModel`. This step's own real new work is building the
    **policy** half (which interval, and "none" as a first-class option) and the **wiring** half
    (actually calling it from the Timeline's drag-rectangle and the Shell's scrub action), never
    reimplementing the rounding math itself.
  - **`XsqExporter.FrameIntervalMs = 25`** (`XlightsAI.Studio.Xlights`, PHASE-02) is the existing,
    already-tested, already-shipped xLights frame grid — used today only at export time
    (`XsqExporter.RoundToFrame`, private). This step's own frame-grid constant
    (`XLightsFrameGrid.FrameIntervalMs`) is declared as `= XsqExporter.FrameIntervalMs` (a compile-time
    constant reference, not a copied literal), so the interactive snap grid and the export grid can
    never silently drift apart — directly satisfying Section 2's "Convert to xLights frames only when
    explicitly snapping or exporting" (both now read the exact same constant).
  - Confirmed no `BeatGrid`/`MelodyTrack`/`NoteEvent` exist yet (those are PHASE-05/PHASE-07's own
    aggregates per Section 2's cross-phase ownership table) — "later beat/note" (this step's own
    implementation guidance) is satisfied by leaving `SnapMode` an open enum a future phase extends,
    never by fabricating a `Beat`/`Note` member this repository has no real data for yet.
- Baseline regression before any Step 030 code existed, run fresh in this session:
  **373/373 passing** (`dotnet test XlightsAI.Studio.slnx --nologo`), matching Step 029's own recorded
  final count exactly.

## 2. Scope actually implemented

**`XlightsAI.Studio.Editing` (new files, pure domain):**

- **`SnapPolicy.cs`** — `SnapMode` enum (`None`, `Frame`) and `SnapPolicy` record
  (`Mode`, `IntervalTicks`) with `Snap(long ticks)` delegating to the existing
  `EditTimeSnapping.SnapTicks`. Two static instances: `SnapPolicy.None` (interval `null` — unchanged
  pass-through) and `SnapPolicy.Frame` (interval = `XLightsFrameGrid.FrameIntervalTicks`). This is the
  step's own "snap policy... as dependency not baked into drag math": a plain, injectable value, not a
  hardcoded rounding call.
- **`XLightsFrameGrid.cs`** — the "time-to-frame conversion" half: `FrameIntervalMs` (`= XsqExporter.
  FrameIntervalMs`, a compile-time constant reference into the existing, already-shipped export frame
  grid — never a second, independently-chosen frame length), `FrameIntervalTicks` (`250,000` ticks at
  the default 25ms), `TicksToFrameIndex`/`FrameIndexToTicks` (pure conversions, explicitly floor-based
  so a negative tick value maps to a negative frame index rather than crashing or silently clamping to
  zero), and `SnapTicksToFrameBoundary` (delegates to `EditTimeSnapping.SnapTicks`).

**`XlightsAI.Studio.Editing` (modified, additive only):**

- **`TimelineRangeSelectionBuilder.Build`** — new optional `SnapPolicy? snapPolicy = null` parameter.
  When supplied, both resolved tick endpoints are snapped *before* the half-open window is built, and
  the two snapped ticks are re-ordered with `Math.Min`/`Math.Max` **after** snapping (never before) —
  this step's own disclosed "boundary rounding" edge case: a drag whose two raw endpoints straddle a
  single snap tie-break can never produce an inverted window. Defaulting to `null` (behaviorally
  identical to omitting the parameter) means every pre-Step-030 call site — including every existing
  Step 021/029 test — keeps compiling and behaving identically without modification.

**`XlightsAI.Studio.Shell` (`TimelineViewModel`, additive only):**

- `SnapPolicy` — new bindable property, defaults to `SnapPolicy.None`. A **user preference**, not
  playback-session state: unlike `LoopWindow`/`ScrubErrorMessage`, it deliberately **survives**
  `Reset()`/`LoadTimeline()` (loading a different song does not invalidate the user's chosen snap grid).
- `ApplyRangeSelection()` — now passes `SnapPolicy` through to `TimelineRangeSelectionBuilder.Build`
  (the drag-rectangle snap wiring).
- `PlayheadFrameIndex` — the live playhead's own xLights frame index, via `XLightsFrameGrid.
  TicksToFrameIndex(PlayheadTicks)`.
- `LastScrubRequestedTicks`/`LastScrubActualTicks`/`LastScrubDriftTicks` — this step's own "gather
  sync/drift telemetry": the most recent scrub's already-snapped requested target, the real device's
  own resulting `MasterPlaybackClock.CanonicalPositionTicks` read back afterward, and their difference.
  `null` until the first successful scrub (the "silence" edge case). Written only by the new
  `RecordScrubTelemetry(long requestedTicks, long actualTicks)` — the single authoritative recorder,
  called by `ShellViewModel` only *after* a scrub actually succeeds (never on a rejected attempt).
  Cleared (not `SnapPolicy`) by `Reset()`/`LoadTimeline()`'s new `ClearScrubTelemetry()` call — the same
  "stale session state never survives a reload" convention already applied to `LoopWindow`.
- `TimingDiagnosticsSummary` — this step's own required "timing-diagnostics panel": one bindable string
  naming the current snap mode, the live playhead frame index, and the last scrub's own
  requested/actual/drift ticks (or an explicit "n/a (no scrub yet)" — never a silently blank panel).

**`XlightsAI.Studio.Shell` (`ShellViewModel`, additive only):**

- Two new commands: `SetTimelineSnapNoneCommand`/`SetTimelineSnapFrameCommand` (→ `Timeline.SnapPolicy =
  SnapPolicy.None`/`SnapPolicy.Frame`), gated by `Timeline.IsLoaded` (the "stale" case — no timeline to
  snap against) and wired into the existing `Timeline.PropertyChanged` `IsLoaded` `RaiseCanExecuteChanged`
  sweep, same convention as every other Timeline command.
- `RunScrubTimelinePlayheadAsync()` — this step's own real scrub/drag entry point: the parsed scrub
  target is now passed through `Timeline.SnapPolicy.Snap(...)` **once**, before `ValidateScrubTarget`/
  the real `MasterPlaybackClock.SeekToCanonicalAsync` call — proving the injected policy, not a second
  hardcoded rounding call, governs the real seek. On success, calls the new
  `Timeline.RecordScrubTelemetry(snappedTicks, _masterClock.CanonicalPositionTicks)`. A rejected scrub
  (unparsable, out-of-scope *after* snapping, or no playback session open) still sets
  `Timeline.ScrubErrorMessage` exactly as before and **never** records telemetry for the failed attempt
  — never a partial mutation.

**`MainWindow.axaml`:**

- New "Snap & timing diagnostics" sub-section on SCREEN 13 TIMELINE: "Snap: None"/"Snap: Frame" buttons
  (calling the two new commands) and a `Timeline.TimingDiagnosticsSummary` text block.

**No changes to any existing `Contracts`, `Audio`, `Xlights`, `Projects`, `Reference`, or PHASE-01–03
`Shell`/`App` type's existing members, and no changes to any pre-Step-030 member's existing behavior**
— purely additive to `Editing` (two new files, one new optional parameter) and `Shell`
(`TimelineViewModel`/`ShellViewModel`/`MainWindow.axaml`).

## 3. Architecture deviations / decisions

- **"Later beat/note" is deliberately not pre-built.** `SnapMode` has exactly two members (`None`,
  `Frame`) rather than speculative `Beat`/`Note` members with no real data behind them yet —
  `BeatGrid`/`MelodyTrack`/`NoteEvent` are PHASE-05/PHASE-07's own aggregates (Section 2's cross-phase
  ownership table) and do not exist in this repository today. `SnapPolicy` itself (an open enum plus a
  plain `IntervalTicks`) is the real extension point a future phase adds a `Beat`/`Note` case to — not a
  type this step needs to pre-guess the shape of. This mirrors the project's own stated principle
  (Section "Don't add features... for hypothetical future requirements").
- **The frame grid is a single compile-time-constant reference, never a duplicated literal.**
  `XLightsFrameGrid.FrameIntervalMs = XsqExporter.FrameIntervalMs` — if a future step ever changes the
  export frame rate, this step's own interactive snap grid changes with it automatically, satisfying
  Section 2's "Convert to xLights frames only when explicitly snapping or exporting" literally: both the
  "snapping" path (this step) and the "exporting" path (`XsqExporter`, PHASE-02) now read one value.
- **"Locked" in this step's own stated failure/edge case text has no meaningful analog in this step's
  actual scope, same disclosed reasoning as Steps 028/029 §3.** This step introduces no document
  mutation and no interaction with effect locks — snapping operates purely on drag-rectangle/scrub tick
  values and the playback transport. As in Steps 028/029, **"invalid timing boundaries"** =
  out-of-scope/negative/inverted tick values (covered by `TicksToFrameIndex`'s explicit negative-tick
  floor behavior, and by the scrub command's existing out-of-scope rejection applied *after* snapping);
  **"missing media"** = no timeline loaded (the new snap commands' own `CanExecute` gate, and
  `ApplyRangeSelection`'s existing no-op guard, both already cover a snap policy applied with nothing
  loaded). **"Locked"** is explicitly disclosed as not applicable, not silently ignored.
- **Snap is applied once, at the single real drag/scrub entry point, never duplicated.** The
  drag-rectangle path snaps inside `TimelineRangeSelectionBuilder.Build` (the one real place a drag
  rectangle becomes an `EditScope`); the scrub path snaps inside `ShellViewModel.
  RunScrubTimelinePlayheadAsync` (the one real place a scrub target becomes a transport seek). Neither
  `TimelineViewModel` nor any other call site re-implements or duplicates the rounding math — both
  funnel through the same `SnapPolicy.Snap`/`EditTimeSnapping.SnapTicks`.
- **`SnapPolicy` is a user preference, not playback-session state — a deliberate, disclosed split from
  every other piece of Timeline UI state introduced since Step 024.** `LoopWindow`/`ScrubErrorMessage`/
  `SelectedRangeScope` are all cleared by `Reset()`/`LoadTimeline()` because they describe *this
  session's* playback/selection; `SnapPolicy` describes *how the user wants to interact*, independent of
  which song is loaded, so it deliberately survives both — proven by
  `Reset_ClearsScrubTelemetry_ButPreservesSnapPolicy_UserPreferenceNotSessionState`/
  `LoadTimeline_ClearsScrubTelemetry_ButPreservesSnapPolicy_UserPreferenceNotSessionState` (§4).
- **Scrub drift telemetry is recorded only on a successful scrub, never on a rejected one.** A rejected
  scrub (out-of-scope after snapping, unparsable text, or no playback session open) leaves
  `LastScrubRequestedTicks`/`LastScrubActualTicks` completely untouched — proven by
  `ScrubCommand_FrameSnapMovesTargetOutOfScope_RejectsWithoutPartialMutation_NegativePath` (§4) — so the
  timing-diagnostics panel never silently reports telemetry for a seek that was never actually sent to
  the real transport.

## 4. Commands actually executed

All run via the PowerShell tool (the sandboxed POSIX Bash tool in this session is non-functional — every
invocation returns exit code 1 with no output — the same pre-existing environment limitation recorded in
every prior step's own evidence; re-confirmed again in this session with a plain `echo` probe before
falling back to PowerShell for every command below).

```
dotnet build XlightsAI.Studio.slnx --nologo                                                           # baseline (before any Step 030 code existed): 373/373 passing
dotnet build XlightsAI.Studio.slnx --nologo                                                           # after Editing/Shell/MainWindow.axaml changes
dotnet build tests\XlightsAI.Studio.Editing.Tests\XlightsAI.Studio.Editing.Tests.csproj --nologo
dotnet test  tests\XlightsAI.Studio.Editing.Tests\XlightsAI.Studio.Editing.Tests.csproj --nologo --filter "FullyQualifiedName~Step030" --logger "console;verbosity=normal"
dotnet build tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj --nologo
dotnet test  tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj --nologo --filter "FullyQualifiedName~Step030TimelineViewModelTests" --logger "console;verbosity=normal"
dotnet test  tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj --nologo --filter "FullyQualifiedName~Step030" --logger "console;verbosity=normal"   # includes real-device Step030ShellWiringTests
dotnet build src\XlightsAI.Studio.App\XlightsAI.Studio.App.csproj --nologo                             # confirms the new MainWindow.axaml XAML compiles
dotnet test  XlightsAI.Studio.slnx --nologo                                                            # full regression, final
dotnet build src\XlightsAI.Studio.App\XlightsAI.Studio.App.csproj --nologo                             # app build for manual smoke test
```

### Build results

`Editing`, `Shell`, `App`, and the full solution all: `Build succeeded. 0 Warning(s). 0 Error(s).`

### Targeted test results (new tests only)

```
tests\XlightsAI.Studio.Editing.Tests — filter "~Step030": 8/8 passed (Step030AcceptanceTests, pure domain, hardware-free):
  Step030_Acceptance_Passes_TimeToFrameConversionGoldenValues
  TicksToFrameIndex_NegativeTicks_FloorsExplicitly_InvalidTimingBoundaryNegativePath
  Step030_Acceptance_Passes_BoundaryRoundingGoldenValues
  SnapPolicy_None_LeavesTicksUnchanged_SilenceEdgeCase
  SnapPolicy_Frame_SnapsToTheExactXsqFrameGrid
  Build_WithFrameSnapPolicy_SnapsResultingWindowToFrameBoundaries
  Build_WithNullSnapPolicy_BehavesIdenticallyToOmittingIt_RegressionNegativePath
  Build_WithFrameSnapPolicy_EndpointsStraddlingATieBreak_NeverProducesInvertedWindow

tests\XlightsAI.Studio.Shell.Tests — filter "~Step030": 13/13 passed:
  Step030TimelineViewModelTests (9, hardware-free, deterministic):
    SnapPolicy_DefaultsToNone_PreStep030BehaviorUnchanged
    ApplyRangeSelection_DefaultSnapPolicy_RawOffFrameTicks_RegressionNegativePath
    ApplyRangeSelection_FrameSnapPolicy_SnapsSelectedRangeScopeToFrameBoundaries
    PlayheadFrameIndex_ReflectsPlayheadTicksViaXLightsFrameGrid
    RecordScrubTelemetry_SetsRequestedActualAndDriftTicks
    TimingDiagnosticsSummary_NoScrubYet_ReportsExplicitly_SilenceEdgeCase
    TimingDiagnosticsSummary_FrameSnapPolicy_NamesTheFrameGrid
    Reset_ClearsScrubTelemetry_ButPreservesSnapPolicy_UserPreferenceNotSessionState
    LoadTimeline_ClearsScrubTelemetry_ButPreservesSnapPolicy_UserPreferenceNotSessionState
  Step030ShellWiringTests (4, real Windows winmm device):
    Step030_Acceptance_Passes_FrameSnapAppliesToScrubAndRecordsRealDriftTelemetry [1 s]
    ScrubCommand_FrameSnapMovesTargetOutOfScope_RejectsWithoutPartialMutation_NegativePath [276 ms]
    SetTimelineSnapFrameCommand_NoTimelineLoaded_IsDisabled_StaleNegativePath [1 ms]
    SetTimelineSnapNoneCommand_TimelineLoaded_SetsPolicyBackToNone [1 ms]
```

### Full regression (after implementation, final run)

```
Passed!  - Failed: 0, Passed: 71,  Skipped: 0, Total: 71,  Duration: 328 ms - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed: 91,  Skipped: 0, Total: 91,  Duration: 271 ms - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed: 25,  Skipped: 0, Total: 25,  Duration: 2 s   - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed: 34,  Skipped: 0, Total: 34,  Duration: 3 s   - XlightsAI.Studio.Audio.Tests.dll
Passed!  - Failed: 0, Passed: 142, Skipped: 0, Total: 142, Duration: 2 s   - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed: 31,  Skipped: 0, Total: 31,  Duration: 34 s  - XlightsAI.Studio.Reference.Tests.dll
```

**394 total, 0 failed, 0 skipped** — up from the Step 029 baseline of 373 (+8 new `Editing.Tests`,
83→91; +13 new `Shell.Tests`, 129→142). `Xlights.Tests`, `Projects.Tests`, `Audio.Tests`,
`Reference.Tests` are unchanged in count (no source in those projects was modified this step). All 373
pre-existing tests still pass unmodified.

### Manual/desktop demonstration

```
dotnet build src\XlightsAI.Studio.App\XlightsAI.Studio.App.csproj --nologo
$p = Start-Process -FilePath "src\XlightsAI.Studio.App\bin\Debug\net10.0\XlightsAI.Studio.exe" -PassThru
Start-Sleep -Seconds 5
# Still running after 5s: True
Stop-Process -Id $p.Id -Force
```

The real compiled Avalonia desktop app, now including the new "Snap & timing diagnostics" sub-section
(two buttons plus the `TimingDiagnosticsSummary` text block) on SCREEN 13 TIMELINE, started and ran for
5+ seconds with no exception and no early exit — proving the new XAML/wiring did not regress app
startup. This is a process-alive smoke test, **not** a human-driven click-through of Snap: Frame → drag
a range selection → observe the snapped selection summary → Scrub Playhead → observe the real device
seek and the timing-diagnostics line update (disclosed, not silently implied, below) — no UI-automation
driver is available in this environment for the native Avalonia window, the same standing limitation
every prior PHASE-02/03/04 step's own evidence already records for its own screen additions. The full
vertical slice (snap policy → `TimelineRangeSelectionBuilder.Build`/`ShellViewModel.
RunScrubTimelinePlayheadAsync` → the real transport's own seek → `TimelineViewModel.
RecordScrubTelemetry`) is instead proven, end to end, against the real device by
`Step030ShellWiringTests` (§4 above), which exercises the exact same `ShellViewModel`/
`TimelineViewModel`/`WaveOutAudioTransport` code path the UI binds to, including the real device's own
`PositionTicks`-derived `MasterPlaybackClock.CanonicalPositionTicks` value the drift-tolerance assertion
reads.

## 5. Fixtures / input hashes

No new fixture files were added to `fixtures/`. The real-device test
(`Step030ShellWiringTests`) constructs real WAV bytes via this repository's own already-licensed,
already-verified `DeterministicSyntheticAudioGenerator` (`XlightsAI.Studio.Reference.Fixtures`,
PHASE-01 Step 003) — the same synthesizer every prior PHASE-04 step's own tests use:

- `BuildClickTrack(bpm: 240, clickCount: 8, clickDurationSeconds: 0.02, toneHz: 1000.0, sampleRate: 8000)`
  — the same 2-second real-time click track Step 029's own wiring tests use, long enough that a
  1,260,000-tick (1.26s) scrub target sits well inside its own duration both before and after
  frame-snapping.
- `Step030AcceptanceTests`/`Step030TimelineViewModelTests` construct pure, synthetic inputs directly
  (no audio file at all) — the correct, minimal input shape for testing `XLightsFrameGrid`/`SnapPolicy`/
  `TimelineRangeSelectionBuilder`/`TimelineViewModel`'s own contracts, independent of decode/transport
  correctness already proven elsewhere.

All test files are written under `%TEMP%\xlai-step030-shell-*\`, deleted by each test class's
`Dispose()` — identical cleanup convention to every prior step's acceptance test class. Each real
`WaveOutAudioTransport` is itself disposed (`await using`) so the real device handle is released before
the next test opens it.

## 6. Unresolved issues / known gaps

- **This step's own stated failure/edge case text's "locked" clause has no meaningful analog in this
  step's own surface** (§3) — disclosed with its own reasoning, same as Steps 028/029; "invalid timing
  boundaries" and "missing media" are both fully covered by tests, "locked" is not exercised because no
  document mutation occurs in this step's scope.
- **"Beat"/"note" snap modes do not exist yet** (§3) — a disclosed, deliberate consequence of
  `BeatGrid`/`MelodyTrack`/`NoteEvent` not existing in this repository until PHASE-05/PHASE-07; `SnapMode`
  is left open for a future phase to extend, not pre-built with placeholder cases.
- **No UI-automation-driven click-through of the new Snap: None/Snap: Frame buttons or the
  timing-diagnostics text** — same disclosed, standing limitation as every prior step (no
  accessibility/UI-driver tooling available in this environment for the native Avalonia window). The
  full vertical slice is proven instead at the `ShellViewModel`/`TimelineViewModel`/
  `WaveOutAudioTransport` layer through real, un-mocked collaborators and the real device
  (`Step030ShellWiringTests`).
- **No real xLights installation available in this environment** — per the user's own standing waiver
  on requiring a real-xLights render check as a phase-gate blocker (recorded in this assistant's
  memory). Not directly applicable to this step's own required acceptance in any case: Step 030's
  acceptance is about time-to-frame conversion/snap behavior, not xLights rendering — though this step
  does, for the first time, make the interactive snap grid and `XsqExporter`'s own export grid read the
  same constant, which is the closest this step comes to an xLights-fidelity claim.
- **The phase-wide gate demonstration itself ("Import a click track, display waveforms, drag an effect
  to a click, loop 1 second, seek to song midpoint and back, close/reopen and confirm saved media
  identity and correct timing") is not re-run end-to-end as a single scripted demo in this step** — each
  of its named behaviors is independently proven by the step that introduced it (024: import/hash;
  026/027: waveform; 029: loop 1 second; 028/030: seek/scrub with snap). Per the task's own instruction,
  this session implements only Step 030; the full-phase gate report (Section 7/8 of the spec) is a
  separate, later deliverable this session does not produce.

## 7. PASS / BLOCKED

**Status: PASS** for Step 030's own stated required acceptance and failure/edge case:

- "Time-to-frame conversion and boundary rounding golden tests" — proven by
  `Step030_Acceptance_Passes_TimeToFrameConversionGoldenValues` (exact frame-index/tick-boundary table,
  including the last tick still inside the previous frame) and
  `Step030_Acceptance_Passes_BoundaryRoundingGoldenValues` (exact midpoint-tie rounding, proven to round
  away from zero — the same convention `EditTimeSnapping.SnapTicks` already established in Step 019),
  plus `Build_WithFrameSnapPolicy_EndpointsStraddlingATieBreak_NeverProducesInvertedWindow`'s own direct
  proof that the builder's post-snap re-ordering prevents an inverted window at a tie-break.
- "Implement snap policy (none, frame, later beat/note) as dependency not baked into drag math" — proven
  by `SnapPolicy`/`SnapMode` existing as a plain, injectable value (never a hardcoded call), by
  `TimelineRangeSelectionBuilder.Build`'s new optional parameter (the drag-rectangle call site), by
  `ShellViewModel.RunScrubTimelinePlayheadAsync`'s new `Timeline.SnapPolicy.Snap(...)` call (the scrub
  call site), and by the regression tests proving every pre-Step-030 call site (omitted/`null`
  parameter, default `SnapPolicy.None`) behaves completely unchanged.
- "gather sync/drift telemetry" — proven end-to-end against the real device by
  `Step030_Acceptance_Passes_FrameSnapAppliesToScrubAndRecordsRealDriftTelemetry`: a real off-frame
  scrub target is snapped, seeked on the real transport, and the real resulting position is recorded as
  `LastScrubRequestedTicks`/`LastScrubActualTicks`/`LastScrubDriftTicks`, surfaced in
  `TimingDiagnosticsSummary`.
- "Silence, invalid timing boundaries and missing media must be handled explicitly" (this step's own
  interpretation, §3) — proven by `TicksToFrameIndex_NegativeTicks_FloorsExplicitly_
  InvalidTimingBoundaryNegativePath` (explicit, deterministic negative-tick handling, never a crash),
  `TimingDiagnosticsSummary_NoScrubYet_ReportsExplicitly_SilenceEdgeCase` (explicit "n/a," never a
  silently blank panel), `SetTimelineSnapFrameCommand_NoTimelineLoaded_IsDisabled_StaleNegativePath`
  (missing media), and `ScrubCommand_FrameSnapMovesTargetOutOfScope_RejectsWithoutPartialMutation_
  NegativePath` (a snap-adjusted target that lands out-of-scope is still rejected, with telemetry left
  completely untouched — never a partial mutation).
- The step's own user-visible outcome ("Saved audio/analysis artifact with timestamp provenance plus a
  synchronized waveform/timeline demonstration") is proven: the snap policy and timing-diagnostics panel
  are directly inspectable on-screen (SCREEN 13 TIMELINE's new "Snap & timing diagnostics" sub-section),
  and every claim above is backed by an actually-executed, actually-passing automated test — see §4.

No previously accepted step's contract was modified — `SongAsset`, `IAudioDecoder`/`WavAudioDecoder`,
`IAudioTransport`/`WaveOutAudioTransport`/`UnsupportedPlatformAudioTransport`, `WaveformEnvelope`/
`WaveformPyramidCache`, `TimelineViewport`/`ITimelineViewport`/`EditScope`, `MasterPlaybackClock`,
`EditTimeSnapping`, `XsqExporter.FrameIntervalMs`, and every other PHASE-01–03 public member are
completely unchanged — only purely additive members were added to `Editing`/`TimelineViewModel`/
`ShellViewModel`, and `TimelineRangeSelectionBuilder.Build` gained one optional, default-`null`
parameter — confirmed by the full pre-existing 373-test suite passing unmodified before a single
Step 030 line of code existed (§1), and again at 394/394 (373 old + 21 new) after implementation (§4).

## 8. Phase-wide status after this session

Steps 024, 025, 026, 027, 028, 029, and now 030 (all seven steps with recorded PASS evidence) are
complete for PHASE 04's own numbered build slices. The phase-wide gate demonstration and full Gate
Report (spec Section 7/8: "Import a click track, display waveforms, drag an effect to a click, loop 1
second, seek to song midpoint and back, close/reopen and confirm saved media identity and correct
timing," run as a single scripted/observed demonstration, plus the explicit PASS/BLOCKED phase-wide
verdict) are **not** produced by this session — per the task's own instruction, this session implements
only Step 030 and does not proceed past it until this evidence is reviewed.
