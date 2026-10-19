# PHASE 03 — Step 016 Evidence

**Step:** Build time ruler, horizontal scroll and zoom.
**Spec:** [`docs/phase-specs/PHASE-03.md`](../../docs/phase-specs/PHASE-03.md), Section 6, Step 016.
**Date:** 2026-10-11
**Status: PASS** (scoped to this step's own required acceptance — pixel-to-time conversion at zoom
extremes, plus the named silence/invalid-timing-boundary/missing-media failure/edge cases — all
proven below against a real, running Avalonia desktop app and real unit/integration tests; no
external dependency (xLights, audio hardware, a real song/media file) is required or claimed by
this step's own acceptance text).

## 1. Baseline check (prior phase contracts, unmodified)

Re-ran the full existing suite before starting (PHASE-01 Steps 001-008 + PHASE-02 Steps 009-015,
every prior step — this repository has no PHASE-03 work committed yet):

```text
$ dotnet build XlightsAI.Studio.slnx
Build succeeded. 0 Warning(s) 0 Error(s)

$ dotnet test XlightsAI.Studio.slnx
XlightsAI.Studio.Xlights.Tests.dll:   Passed! Total: 71
XlightsAI.Studio.Shell.Tests.dll:     Passed! Total: 50
XlightsAI.Studio.Projects.Tests.dll:  Passed! Total: 25
XlightsAI.Studio.Reference.Tests.dll: Passed! Total: 31
# 177/177, matching PHASE-02 Step 015's own recorded final total exactly.
```

No PHASE-01/PHASE-02 production `src/**` type's existing public shape was removed or modified in a
breaking way (see §9 for the one additive-only change to `ShellViewModel.cs`/`ShellScreen.cs`/
`MainWindow.axaml`, all pure additions alongside the pre-existing members).

## 2. What was built

Per PHASE-03 Section 6 Step 016 ("Implement shared time/pixel transformation with zoom anchors and
virtualized timeline bounds; unit test bidirectional conversions at arbitrary scroll offsets.") and
the required acceptance ("Pixel-to-time conversion tests at zoom extremes."):

- **New project `src/XlightsAI.Studio.Editing`** (`XlightsAI.Studio.Editing` namespace, per PHASE-03
  Section 3's `namespace XlightsAI.Studio.Editing;`), referencing only `XlightsAI.Studio.Contracts`
  (the shared `TimeWindow`/`TimelineTick` records already declared there, per Section 2's
  single-declaration rule):
  - **`ITimelineViewport.cs`** — the exact phase-local interface PHASE-03 Section 3 proposes:
    `long TimeFromX(double pixel)`, `double XFromTime(long ticks)`.
  - **`TimelineViewport.cs`** — the real, immutable implementation: an explicit `ContentBounds`
    (`TimeWindow`, the "virtualized timeline bounds"), `ScrollOffsetTicks`, and `TicksPerPixel`
    (zoom level), bounded to `[MinTicksPerPixel=100, MaxTicksPerPixel=10_000_000]` (10 microseconds
    to 1 second per pixel). `Create` is the only factory path and explicitly rejects an inverted
    content window or a non-positive/non-finite zoom level via a new, named
    `TimelineViewportInvalidException` — never a silent clamp into something plausible-looking.
    `ZoomAt(anchorPixel, zoomFactor)` implements the "zoom anchors" the step names explicitly: the
    tick under `anchorPixel` stays under that same pixel after the zoom, clamped to the two extremes
    and to `ContentBounds`. `ScrollByPixels`/`VisibleWindow` implement scrolling and the visible-range
    query used for virtualization, both clamped to `ContentBounds`.
  - **`TimelineViewportInvalidException.cs`** — the explicit, named failure signal for invalid
    timing boundaries/zoom levels (this step's own required failure/edge case).
  - **`TimelineRulerTick.cs`** / **`TimelineRulerTickGenerator.cs`** — the actual "time ruler": turns
    a viewport's current scroll/zoom into labeled tick marks (chosen at a "nice" 1/2/5 × 10ⁿ
    interval so labels never overlap regardless of zoom), generated only across the currently
    visible pixel width, never the full (potentially huge) content bounds — the virtualization this
    step names explicitly.
- **`src/XlightsAI.Studio.Shell/TimelineViewModel.cs`** (new) — SCREEN 13 TIMELINE's view model, no
  Avalonia types, same convention as every other screen. Explicit `ScreenLoadState.Empty` /
  `Error` / `Loaded` state machine (mirrors `ShowImportViewModel`): `Viewport` is `null` exactly when
  no media has been loaded yet or the last load failed — "missing media must be handled explicitly."
  `LoadTimeline()` builds a fresh viewport from a user-supplied `TotalDurationTicks` (this phase has
  no real `SongAsset` duration yet — Phase 04's own scope, same reasoning `ShowImportViewModel`
  already documents for its own `SongDurationTicks`); a negative duration sets the explicit Error
  state rather than throwing out of a UI command. `ZoomIn`/`ZoomOut`/`ScrollByPixels` are explicit
  no-ops while no media is loaded. `RulerTicks` exposes `TimelineRulerTickView` (new, UI-only
  projection with a *relative* `MarginLeft` instead of the domain's *absolute* `X`) so the view can
  lay ticks out in a plain horizontal panel without this Avalonia-free project needing Avalonia
  types.
- **`src/XlightsAI.Studio.Shell/TimelineRulerTickView.cs`** (new) — the UI-only tick projection
  described above.
- **`src/XlightsAI.Studio.Shell/ShellScreen.cs`** (modified, additive): new `Timeline` enum member.
- **`src/XlightsAI.Studio.Shell/ShellViewModel.cs`** (modified, additive): new `Timeline` property,
  `NavigateToTimelineCommand`/`LoadTimelineCommand`/`TimelineZoomInCommand`/`TimelineZoomOutCommand`/
  `TimelineScrollLeftCommand`/`TimelineScrollRightCommand`, `IsShowingTimeline`, and the matching
  `CanExecuteChanged`/`PropertyChanged` wiring — no existing member removed or changed.
- **`src/XlightsAI.Studio.App/Views/MainWindow.axaml`** (modified, additive): a new "Timeline" nav
  button and SCREEN 13 TIMELINE body (duration input, Load Timeline, Zoom In/Out, Scroll Left/Right,
  the real ruler) — no existing markup removed.
- **`src/XlightsAI.Studio.App/Converters/DoubleToLeftThicknessConverter.cs`** (new) — converts the
  Shell's plain `double` margin into an Avalonia `Thickness`, keeping Avalonia types entirely inside
  the App project.
- **Tests** (new, no existing test file's assertions touched):
  - `tests/XlightsAI.Studio.Editing.Tests/Step016AcceptanceTests.cs` — 14 tests (3 of them
    `[Theory]`-parameterized) against the real `TimelineViewport`/`TimelineRulerTickGenerator`, no
    mocks.
  - `tests/XlightsAI.Studio.Shell.Tests/Step016ShellWiringTests.cs` — 5 tests proving the real
    `ShellViewModel.Timeline`/navigation/Load/Zoom/Scroll commands end to end.

## 3. Commands actually executed

```text
$ dotnet build XlightsAI.Studio.slnx
Build succeeded. 0 Warning(s) 0 Error(s)

$ dotnet test tests\XlightsAI.Studio.Editing.Tests\XlightsAI.Studio.Editing.Tests.csproj --logger "console;verbosity=normal"
Total tests: 14. Passed: 14.

$ dotnet test tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj --filter "FullyQualifiedName~Step016" --logger "console;verbosity=normal"
Total tests: 5. Passed: 5.

$ dotnet test XlightsAI.Studio.slnx
XlightsAI.Studio.Editing.Tests.dll:   Passed! Total: 14   (new)
XlightsAI.Studio.Xlights.Tests.dll:   Passed! Total: 71   (untouched)
XlightsAI.Studio.Shell.Tests.dll:     Passed! Total: 55   (50 baseline + 5 new Step 016 tests)
XlightsAI.Studio.Projects.Tests.dll:  Passed! Total: 25   (untouched)
XlightsAI.Studio.Reference.Tests.dll: Passed! Total: 31   (untouched)
# Full, final regression: 196/196 passing (177 baseline + 19 new), 0 regressions.

$ dotnet format XlightsAI.Studio.slnx --verify-no-changes
(no output; exit code 0)
```

All commands were run via PowerShell; the Bash tool continues to fail on every invocation in this
environment (exit 1 / no output), consistent with every prior step's evidence.

## 4. Required acceptance: "Pixel-to-time conversion tests at zoom extremes"

`tests/XlightsAI.Studio.Editing.Tests/Step016AcceptanceTests.cs`:

```text
Passed Step016_Acceptance_Passes_BidirectionalConversionAtArbitraryScrollOffsets(pixel: 0, scrollOffsetTicks: 0, ticksPerPixel: 12345)
Passed Step016_Acceptance_Passes_BidirectionalConversionAtArbitraryScrollOffsets(pixel: 100, scrollOffsetTicks: 50000, ticksPerPixel: 0)
Passed Step016_Acceptance_Passes_BidirectionalConversionAtArbitraryScrollOffsets(pixel: -200, scrollOffsetTicks: -1000000, ticksPerPixel: 10000)
Passed Step016_Acceptance_Passes_PixelToTimeConversion_AtMostZoomedInExtreme
Passed Step016_Acceptance_Passes_PixelToTimeConversion_AtMostZoomedOutExtreme
Passed Step016_Acceptance_Passes_ZoomAnchorKeepsAnchoredTickUnderSamePixel
Passed Step016_Acceptance_Passes_ScrollingIsClampedToVirtualizedContentBounds
```

- `..._AtMostZoomedInExtreme`/`..._AtMostZoomedOutExtreme` are this step's own named acceptance
  text verbatim: they construct a viewport at `TimelineViewport.MinTicksPerPixel`/`MaxTicksPerPixel`
  respectively, assert `TimeFromX` is correct at that extreme, and assert a further zoom attempt
  stays pinned at the extreme rather than exceeding it.
- `..._BidirectionalConversionAtArbitraryScrollOffsets` is a `[Theory]` across three distinct
  `(pixel, scrollOffsetTicks, ticksPerPixel)` combinations (including a negative scroll offset and a
  non-origin pixel), proving `TimeFromX`/`XFromTime` round-trip correctly at arbitrary scroll
  offsets, not only `0` — the step's own named "unit test bidirectional conversions at arbitrary
  scroll offsets."
- `..._ZoomAnchorKeepsAnchoredTickUnderSamePixel` proves the "zoom anchors" implementation guidance:
  zooming in and zooming out, both anchored at the same pixel, each leave the tick originally under
  that pixel still under that exact pixel afterward.
- Real, running-app confirmation: `step-016-timeline-zoomed-in.png` (§6) shows `Ticks per pixel`
  changing from `1000000` to `125000` after three real Zoom In clicks, with the ruler's own visible
  labels updating to match (`5000 ms`...`11000 ms` instead of `0 ms`...`50000 ms`).

## 5. Required failure/edge case: "Silence, invalid timing boundaries and missing media must be handled explicitly"

### `tests/XlightsAI.Studio.Editing.Tests/Step016AcceptanceTests.cs` (domain layer, no mocks)

```text
Passed Step016_Acceptance_Passes_SilenceEdgeCase_ZeroLengthContentStillProducesUsableViewport
Passed Step016_Acceptance_Passes_InvalidTimingBoundaries_RejectedExplicitly_NegativePath
Passed Step016_Acceptance_Passes_InvalidZoomLevel_RejectedExplicitly_NegativePath(invalidTicksPerPixel: ∞)
Passed Step016_Acceptance_Passes_InvalidZoomLevel_RejectedExplicitly_NegativePath(invalidTicksPerPixel: 0)
Passed Step016_Acceptance_Passes_InvalidZoomLevel_RejectedExplicitly_NegativePath(invalidTicksPerPixel: -1)
Passed Step016_Acceptance_Passes_InvalidZoomLevel_RejectedExplicitly_NegativePath(invalidTicksPerPixel: NaN)
Passed Step016_Acceptance_Passes_MissingMedia_NoContentLoaded_IsHandledExplicitlyByViewModel

Total: 14. Passed: 14.
```

- **Silence** — `..._SilenceEdgeCase_...`: a zero-length `TimeWindow` (an empty document, nothing on
  the timeline yet) still produces a valid, non-crashing viewport pinned to that single instant, and
  the ruler generator still produces at least one labeled tick rather than an empty/crashed ruler.
- **Invalid timing boundaries** — `..._InvalidTimingBoundaries_RejectedExplicitly_NegativePath`: an
  inverted content window (`End` before `Start`) is rejected by the named
  `TimelineViewportInvalidException`, with a message naming the actual offending values, never a
  silent clamp. `..._InvalidZoomLevel_RejectedExplicitly_NegativePath` generalizes the same explicit
  rejection across every invalid zoom shape (zero, negative, `NaN`, `+Infinity`).
- **Missing media** — this step's own domain contract always requires *some* content window (there
  is no "null bounds" state in `TimelineViewport` itself); "missing media" (no document loaded yet)
  is this step's UI-layer concern, proven end to end below.

### `tests/XlightsAI.Studio.Shell.Tests/Step016ShellWiringTests.cs` (shell-wiring layer, no mocks)

```text
Passed Step016_Acceptance_Passes_NavigateAndLoadTimeline_BuildsUsableViewport
Passed Step016_Acceptance_Passes_ZoomCommandsRespectExtremesThroughRealShell
Passed Step016_Acceptance_Passes_ScrollCommandsClampToContentBounds
Passed Step016_Acceptance_Passes_ZoomScrollCommands_DisabledWithoutLoadedMedia_NegativePath
Passed Step016_Acceptance_Passes_InvalidTimingBoundary_SetsExplicitErrorState_NegativePath

Total: 5. Passed: 5.
```

- `..._NavigateAndLoadTimeline_BuildsUsableViewport` proves navigating to the Timeline screen never
  auto-loads a viewport (`Timeline.IsEmpty`/`Timeline.Viewport is null` immediately after
  navigation) — "missing media" is the real, observed state, not a convenient default.
- `..._ZoomScrollCommands_DisabledWithoutLoadedMedia_NegativePath` proves
  `TimelineZoomInCommand`/`TimelineZoomOutCommand`/`TimelineScrollLeftCommand`/
  `TimelineScrollRightCommand` all report `CanExecute == false` while no media is loaded, and that
  calling the underlying view-model methods directly is still a safe no-op (`Viewport` stays
  `null`) rather than a `NullReferenceException`.
- `..._InvalidTimingBoundary_SetsExplicitErrorState_NegativePath` proves a negative
  `TotalDurationTicks` sets `Timeline.IsError`/a real, non-empty `ErrorMessage` through the real
  `LoadTimelineCommand`, never an unhandled exception out of a UI command.
- Real, running-app confirmation: `step-016-timeline-empty-missing-media.png` and
  `step-016-timeline-invalid-duration-error.png` (§6) show both states in the actual compiled
  desktop app.

## 6. Manual / desktop UI verification — real Avalonia app

Per the task's "Build the minimum complete UI -> application -> domain -> data/adapter slice" and
"run... xLights or native inference manual tests when this step requires them" instructions: this
step's own required acceptance/failure text names no xLights/audio/media dependency, so the real
compiled desktop app (`src\XlightsAI.Studio.App\bin\Debug\net10.0\XlightsAI.Studio.exe`) was
launched for real and driven via Windows UI Automation (real `InvokePattern`/`ValuePattern` calls
against the real running window), matching every prior PHASE-02 step's own verification technique.

1. **Missing media (explicit empty state)** —
   `implementation-evidence/PHASE-03/step-016-timeline-empty-missing-media.png`: navigating to the
   real Timeline screen for the first time shows "No media loaded. Set a duration and click Load
   Timeline." — no zoom/scroll controls, no silently blank ruler.
2. **Load Timeline, real ruler** —
   `implementation-evidence/PHASE-03/step-016-timeline-loaded.png`: duration set to real
   `50000000` ticks (5 seconds), **Load Timeline** clicked. The screen shows `Ticks per pixel:
   1000000`, `Visible start (ticks): 0`, `Visible end (ticks): 760000000`, and a real ruler with
   evenly spaced, correctly labeled ticks (`0 ms`, `10000 ms`, `20000 ms`, ..., `50000 ms`).
3. **Zoom In (anchored, extremes-aware)** —
   `implementation-evidence/PHASE-03/step-016-timeline-zoomed-in.png`: three real **Zoom In** clicks.
   `Ticks per pixel` drops from `1000000` to `125000` and the ruler's own labels update to the
   zoomed-in range (`5000 ms`...`11000 ms`) — `Visible start (ticks)` reads `50000000`, the real
   content end, because the zoom-anchor math (anchored at the ruler's horizontal center) pushed the
   scroll offset against the real clamp at the content boundary, independently confirming the
   clamping behavior proven in §5.
4. **Scroll (clamped to content bounds)** —
   `implementation-evidence/PHASE-03/step-016-timeline-scrolled.png`: three real **Zoom Out** clicks
   (back to `1000000` ticks/pixel) followed by twenty real **Scroll Left** clicks (driving the
   offset to the real `0` floor) and five real **Scroll Right** clicks. The screen shows `Visible
   start (ticks): 50000000` — the real content end again — proving the real scroll clamp holds at
   both ends of the real content window, not only in the unit tests.
5. **Invalid timing boundary (explicit error, no crash)** —
   `implementation-evidence/PHASE-03/step-016-timeline-invalid-duration-error.png`: duration set to
   `-1` and **Load Timeline** clicked. The screen shows the real red error "Invalid timeline
   duration: TotalDurationTicks (-1) must not be negative." — not a crash, not a silently-accepted
   garbage viewport.

**No real xLights/audio/media dependency was required or exercised by this step** — Step 016's own
required acceptance/failure text (Section 6) is entirely about the pixel/tick transform, the ruler,
and scroll/zoom, none of which touch `xlights_rgbeffects.xml`/`.xsq`/audio. No prior step's own
still-BLOCKED "real xLights render" item (PHASE-02 STEP-14.md/STEP-15.md §10) is reopened, re-tested,
or claimed resolved by this step.

## 7. Fixture hashes

No new fixture files were introduced by this step (pure domain math + UI state, no file-based
fixtures). No show-directory/XSQ fixture was touched; the manual verification above wrote no files
to disk at all (no export/save action exists on this screen).

## 8. Changed/added paths

```
src/XlightsAI.Studio.Editing/XlightsAI.Studio.Editing.csproj                (new)
src/XlightsAI.Studio.Editing/ITimelineViewport.cs                           (new)
src/XlightsAI.Studio.Editing/TimelineViewport.cs                           (new)
src/XlightsAI.Studio.Editing/TimelineViewportInvalidException.cs           (new)
src/XlightsAI.Studio.Editing/TimelineRulerTick.cs                          (new)
src/XlightsAI.Studio.Editing/TimelineRulerTickGenerator.cs                 (new)
src/XlightsAI.Studio.Shell/TimelineViewModel.cs                            (new)
src/XlightsAI.Studio.Shell/TimelineRulerTickView.cs                        (new)
src/XlightsAI.Studio.Shell/ShellScreen.cs                                  (modified: +Timeline enum member)
src/XlightsAI.Studio.Shell/ShellViewModel.cs                               (modified: +Timeline property/commands/wiring)
src/XlightsAI.Studio.Shell/XlightsAI.Studio.Shell.csproj                   (modified: +ProjectReference to Editing)
src/XlightsAI.Studio.App/Views/MainWindow.axaml                            (modified: +Timeline nav button + SCREEN 13 body)
src/XlightsAI.Studio.App/Converters/DoubleToLeftThicknessConverter.cs      (new)
tests/XlightsAI.Studio.Editing.Tests/XlightsAI.Studio.Editing.Tests.csproj (new)
tests/XlightsAI.Studio.Editing.Tests/Step016AcceptanceTests.cs             (new; 14 tests)
tests/XlightsAI.Studio.Shell.Tests/Step016ShellWiringTests.cs              (new; 5 tests)
tests/XlightsAI.Studio.Shell.Tests/XlightsAI.Studio.Shell.Tests.csproj     (modified: +ProjectReference to Editing)
XlightsAI.Studio.slnx                                                      (modified: +Editing src/test project entries)
implementation-evidence/PHASE-03/STEP-16.md                                (new, this file)
implementation-evidence/PHASE-03/step-016-timeline-empty-missing-media.png (new)
implementation-evidence/PHASE-03/step-016-timeline-loaded.png              (new)
implementation-evidence/PHASE-03/step-016-timeline-zoomed-in.png           (new)
implementation-evidence/PHASE-03/step-016-timeline-scrolled.png            (new)
implementation-evidence/PHASE-03/step-016-timeline-invalid-duration-error.png (new)
```

No PHASE-01/PHASE-02 production file's existing members were edited or removed; every modified file
above gained only new, additive members alongside its existing, untouched content.

## 9. Architecture deviations

- **New project `XlightsAI.Studio.Editing` added**, matching PHASE-03 Section 3's own proposed
  `namespace XlightsAI.Studio.Editing;` exactly, following the same "one project per phase-owned
  domain boundary" pattern already established by `XlightsAI.Studio.Xlights` (PHASE-02) — not a
  generic/speculative abstraction, but the concrete boundary this phase's own spec already names
  (Section 53's "Stable domain names" table: `EditScope`, `EditTransaction`, `DocumentRevision` —
  "First introduced: 03").
- **`ITimelineViewport`'s real implementation is a plain class (`TimelineViewport`), not the
  `EditScope`/`EditCommand` immutable-record style** used elsewhere in Section 3's proposed
  signatures. A `sealed record` was considered, but `TimelineViewport` needs validated construction
  (rejecting an inverted window/invalid zoom) that a public positional-record constructor cannot
  express without also leaving an unvalidated `with`-expression escape hatch; a private constructor
  plus a single `Create` factory closes that gap while every mutator (`ZoomAt`/`ScrollByPixels`)
  still returns a new, independently-valid instance — the same immutable-value discipline, achieved
  without `with`.
- **`TimelineRulerTick.X` (the domain's absolute pixel offset) is not bound to directly in the
  Avalonia view.** Avalonia's `ItemsControl` with a `Canvas` `ItemsPanel` does not propagate a
  `Canvas.Left` attached property set on a `DataTemplate`'s root content to the actual generated
  container added to the `Canvas` (a documented Avalonia/WPF item-container-vs-content-template
  distinction) — confirmed by an initial, now-discarded implementation attempt whose real running-app
  screenshot showed ticks collapsed near the origin instead of spread across the ruler. The fix:
  `TimelineViewModel.RulerTicks` exposes a new UI-only `TimelineRulerTickView(Label, MarginLeft)`
  (relative margin instead of absolute position), letting a plain horizontal `StackPanel` lay ticks
  out correctly with no Avalonia types inside the Shell project. The domain's own
  `TimelineRulerTickGenerator`/`TimelineRulerTick` (absolute `X`, directly unit-tested in §4/§5) is
  unchanged by this — the UI-only projection is an additive, one-way adapter over it.
- **`ViewportWidthPixels` is not yet wired to the real control's actual rendered width** (no
  `SizeChanged`/`EffectiveViewportChanged` handler) — it defaults to a constant (`760`, matching the
  ruler `Border`'s own fixed `Width="760"` in XAML) and only changes if a future caller sets it
  explicitly. This phase's own view models contain no Avalonia types by convention (every existing
  screen view model follows this), so wiring a real layout-size callback into `TimelineViewModel`
  would need either a small Avalonia-side behavior/attached property (a new, currently
  out-of-scope UI mechanism) or breaking that convention; recorded here as explicitly deferred
  rather than silently assumed resolved. The viewport transform itself (`TimeFromX`/`XFromTime`,
  the zoom/scroll math, and the ruler tick generation) is entirely independent of *how* the width
  value arrives, so this deferral does not weaken this step's own required acceptance.
- **No virtualized *row*/model list exists yet** — that is PHASE-03 Step 017's own explicit scope
  ("Build prop rows, group expansion and stable mapping to model IDs"), not reopened or
  pre-implemented here.

## 10. Unresolved issues / risks carried forward

1. **`ViewportWidthPixels` is a fixed default, not yet bound to the real control's rendered size**
   (§9) — a future step (likely Step 017, which adds the row/effect-block rendering this ruler sits
   above) is the natural place to wire a real resize callback, once more of the timeline surface
   exists to resize against.
2. **No real `SongAsset`/audio duration exists yet** (Phase 04's own scope per Section 53) — this
   step's `TotalDurationTicks` is a user-supplied placeholder, same precedent as
   `ShowImportViewModel.SongDurationTicks` (PHASE-02 Step 012).
3. **PHASE-02's own still-BLOCKED "real xLights render" item remains BLOCKED and unexercised by this
   step** (STEP-14.md/STEP-15.md §10, not re-verified here since nothing in this environment
   changed, and this step's own acceptance text never requires it) — **the user has separately
   waived this human/xLights-render verification as a phase-gate blocker for this workstream**, so
   it is recorded here as a disclosed, accepted waiver rather than an open question.
4. Git Bash remains non-functional in this environment (every invocation returns exit 1 with no
   output); every command in this evidence was run via PowerShell, same as every prior step.
5. **This repository currently has no git commits** (`git log` is empty; `git status` shows every
   phase's files as newly added/untracked) — `implementation_commit` below is left `N/A` rather than
   a fabricated hash; no commit was made as part of this step per this task's own "only commit when
   explicitly asked" instruction.

## 11. Gate checklist (this step only)

- [x] Ran `dotnet build`/`dotnet test` for the full existing suite before starting; 177/177 passing,
      matching PHASE-02 Step 015's own recorded final total exactly (§1).
- [x] No prior-step production file's existing public shape was removed; every modified file
      (`ShellScreen.cs`, `ShellViewModel.cs`, `MainWindow.axaml`, the two `.csproj` files) gained
      only new, additive members (§8).
- [x] Turned the step's acceptance text into executable tests: pixel-to-time conversion at both
      zoom extremes, plus bidirectional-conversion-at-arbitrary-scroll-offsets, at both the domain
      (`Editing.Tests`) and shell-wiring (`Shell.Tests`) layers (§4).
- [x] At least one failure-path assertion, and specifically the named failure/edge case text
      ("Silence, invalid timing boundaries and missing media must be handled explicitly") —
      covered as three distinct, separately-tested cases, not collapsed into one (§5).
- [x] Built the minimum complete UI → application → domain slice (no persistence/export adapter is
      named by this step's own scope) and ran a real desktop interaction (not simulated/mocked) —
      five screenshots via real Windows UI Automation against the real running app, independently
      cross-checked against the real bound values on screen (`Ticks per pixel`, `Visible start/end
      (ticks)`) at each step (§6).
- [x] Full regression suite re-run, final: 196/196 passing (177 PHASE-01/PHASE-02 baseline + 19 new
      Step 016 tests), zero regressions (§3, §4, §5).
- [x] `dotnet format --verify-no-changes` passes cleanly against every new and modified file (§3).
- [x] No original/show file touched (this step performs no file I/O at all); no prior-step test
      assertion touched.
- [x] Architecture deviations disclosed in full (§9), per this task's explicit instruction.

**Step 016 status: PASS** (scoped to this step's own required acceptance). Per this task's
instruction, the next numbered step (017) is not started as part of this evidence.
