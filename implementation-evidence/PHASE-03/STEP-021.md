# PHASE 03 — Step 021 Evidence

**Step:** "Multi-row time-range selection with inclusive/exclusive boundary semantics"
**Spec:** `docs/phase-specs/PHASE-03.md` §6, Step 021 (lines 275–289)
**Date:** 2026-10-11

This is the step the user's original instruction for this session named directly. Per the user's
explicit choice recorded in `STEP-018.md` §0, Step 018 ("Render effect blocks and inspector
selection") was implemented first in this same session, closing the standing gap Step 020's own
evidence had flagged — this step is implemented on top of Step 018's real effect-block/row
infrastructure, not the pre-018 state.

## 1. Pre-implementation baseline inspection

- Re-read `docs/phase-specs/PHASE-03.md` §6 Step 021 in full: required acceptance ("Selected model IDs
  and time range match UI; edge-case tests"), implementation guidance ("Drag rectangle returns
  half-open time window and model set; selections crossing nonadjacent rows must be explicit; display
  exact scope"), and the shared failure/edge case ("Out-of-scope, locked or stale revision must be
  rejected without partial mutation").
- Read every existing `XlightsAI.Studio.Editing` type this step builds on: `EditScope` (the exact
  output shape this step must produce), `TimelineRowSlot`/`TimelineRowFlattener`/`TimelineRowId`
  (Step 017's real, collapse-aware visible row list — the thing a drag rectangle actually indexes
  into), `ITimelineViewport`/`TimelineViewport` (Step 016's shared pixel/tick transform), and
  `EditTransactionService`/`EffectLockPolicy`/`EditRejectionReason` (Steps 019/020, the real pipeline
  this step's own negative-path tests tie into — the same technique `STEP-018.md` used).
- Confirmed Step 018 is now real (this session's own prior work, see `STEP-018.md`): `TimelineViewModel.Rows`
  renders real effect blocks with real selection, so this step's own row indices are indexing into a
  row list that now has genuine on-screen content to select across.
- Baseline regression before any Step 021 code change: **243/243 passing** (Step 018's own recorded
  final total from earlier in this same session), confirmed below in §4.

## 2. Scope actually implemented

**New type in `XlightsAI.Studio.Editing`** (pure domain, no Avalonia types):

- `TimelineRangeSelectionBuilder.Build(visibleRows, rowIndexA, rowIndexB, pixelA, pixelB, viewport,
  respectLocks)` — turns a rectangular drag selection (a row-index range into the same `TimelineRowSlot`
  list `TimelineRowFlattener` already produces, plus a horizontal pixel range) into the exact
  `EditScope` it covers:
  - **Time**: half-open `[Start, End)` (PHASE-01's own canonical rule — this step's own title names
    "inclusive/exclusive boundary semantics" explicitly), built through the existing
    `ITimelineViewport.TimeFromX` transform — never a second, parallel pixel/tick conversion.
    Order-independent: `Min(pixelA, pixelB)`/`Max(pixelA, pixelB)`, so a right-to-left drag produces
    the identical window a left-to-right drag over the same region would.
  - **Models**: the exact `ModelId` of every row actually in the `[Min(rowIndexA, rowIndexB),
    Max(rowIndexA, rowIndexB)]` index range — resolved individually per row from the real list (never
    interpolated or inferred between the two endpoints), so a group row contributes nothing, and a
    collapsed group's hidden children (not present in the visible list at all, per Step 017's own
    `TimelineRowFlattener`) are never silently swept in just because the drag rectangle visually
    crossed their collapsed ancestor — "selections crossing nonadjacent rows must be explicit," proven
    directly in §4.
  - Out-of-bounds row indices clamp safely (never throw); an empty row list produces an empty-model,
    zero-length scope (the "silence" edge case this step shares with Step 016's own viewport).

**Modified `XlightsAI.Studio.Shell.TimelineViewModel`** (additive):

- `RangeSelectionStartRowIndex`/`RangeSelectionStartPixel`/`RangeSelectionEndRowIndex`/
  `RangeSelectionEndPixel` — bindable drag-rectangle inputs.
- `SelectedRangeScope` (`EditScope?`) — the real built scope, `null` when nothing is selected.
- `SelectedRangeScopeSummary` (`string?`) — "display exact scope" (this step's own implementation
  guidance): every selected model's own name and the exact half-open time window, in one user-facing
  string; `null` exactly when nothing is selected.
- `ApplyRangeSelection()` — builds `SelectedRangeScope` from the current inputs against the same real,
  currently-visible, collapse-aware row list `Rows` itself renders at; a no-op (not a crash, not a
  stale leftover scope) while no media/layout is loaded.
- `ClearRangeSelection()` — clears the selection.
- Any prior range selection is cleared on every `LoadTimeline`/`Reset()`, same "stale state must be
  explicit" discipline Step 018 already applied to `_selectedEffectId`.

**Modified `XlightsAI.Studio.Shell.ShellViewModel`** (additive): two new commands,
`TimelineApplyRangeSelectionCommand`/`TimelineClearRangeSelectionCommand`, wired with the same
`CanExecute`/`RaiseCanExecuteChanged` convention every other Timeline command already uses.

**Modified `MainWindow.axaml`** (SCREEN 13 TIMELINE, additive): a new "Range selection" panel — four
numeric inputs (start/end row index, start/end pixel), "Select Range"/"Clear Range" buttons, the real
bound `SelectedRangeScopeSummary` text, and an explicit "No range selected" empty state.

## 3. Architecture deviations / decisions

- **The drag rectangle is driven by four numeric inputs + a button, not a real mouse-drag gesture
  over the row/ruler area.** The step's own user-visible outcome is "Avalonia screenshot or repeatable
  desktop interaction recording with saved-state verification" — the same category Steps 016/017 used
  (button-driven Zoom/Scroll, not real mouse-wheel/drag) and that was accepted as PASS in both cases.
  A genuine `PointerPressed`/`PointerMoved`/`PointerReleased` capture spanning the now-nested
  per-row/per-layer `ItemsControl` structure Step 018 introduced would be a materially larger UI
  behavior (hit-testing a specific row *and* pixel from a single pointer event across a virtualized,
  multi-level visual tree) that this step's own narrow scope — "drag rectangle returns half-open time
  window and model set" — does not itself require solving; the real domain/ViewModel logic it must
  exercise (`TimelineRangeSelectionBuilder`, `ApplyRangeSelection`, `SelectedRangeScope`) is fully real
  and real-UI-bound, not mocked. Flagged here as a disclosed, narrower-than-"real drag" interaction
  surface, not silently presented as a full pointer-drag implementation. A follow-up could attach a
  real `PointerPressed`/`PointerMoved`/`PointerReleased` handler on the row `ItemsControl` that
  resolves (container index, relative pixel) and calls the same `RangeSelectionStart*`/`End*`/
  `ApplyRangeSelection()` API this step already built — no further domain change needed.
- **Row indices reuse the exact same pre-order, collapse-aware `TimelineRowSlot` list `Rows` renders
  at — no new row-numbering scheme.** `ApplyRangeSelection` calls
  `TimelineRowFlattener.Flatten(_rowTree.Roots, _collapsedGroupIds)` directly (the same call `Rows`
  itself makes), so "row 3" in a range selection always means the exact same row "row 3" in `Rows`
  means at that instant — never a parallel or stale index space.
- **`TimelineRangeSelectionBuilder` lives in `Editing`, not `Shell`.** It is pure, stateless
  row-index/pixel → `EditScope` math with no UI/session state — the same category
  `EffectBlockLayoutBuilder` (Step 018) already occupies, not a ViewModel concern.
- **No new edit/mutation capability was added.** Exactly like Step 018, this step only *builds* a
  scope; it reuses Steps 019/020's already-PASS `EditTransactionService`/`EffectLockPolicy` as-is to
  prove the out-of-scope/locked/stale-revision negative path (§5) — no existing `Editing` contract was
  changed to support this step.

## 4. Commands actually executed

```
dotnet build src\XlightsAI.Studio.Editing\XlightsAI.Studio.Editing.csproj --nologo
dotnet build src\XlightsAI.Studio.Shell\XlightsAI.Studio.Shell.csproj --nologo
dotnet build src\XlightsAI.Studio.App\XlightsAI.Studio.App.csproj --nologo
dotnet build XlightsAI.Studio.slnx --nologo
dotnet test tests\XlightsAI.Studio.Editing.Tests\XlightsAI.Studio.Editing.Tests.csproj --nologo --filter "FullyQualifiedName~Step021" --logger "console;verbosity=normal"
dotnet test tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj --nologo --filter "FullyQualifiedName~Step021" --logger "console;verbosity=normal"
dotnet test XlightsAI.Studio.slnx --nologo
```

All run via the PowerShell tool — the sandboxed POSIX Bash tool in this session is non-functional
(every invocation, including a plain `true`, returns exit code 1 with no output), the same
pre-existing environment limitation recorded in every prior PHASE-03 step's own evidence.

### Baseline (before this step's code changes)

```
Passed!  - Failed: 0, Passed: 71, Skipped: 0, Total: 71  - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed: 50, Skipped: 0, Total: 50  - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed: 66, Skipped: 0, Total: 66  - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed: 25, Skipped: 0, Total: 25  - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed: 31, Skipped: 0, Total: 31  - XlightsAI.Studio.Reference.Tests.dll
```

243 total, 0 failed — exactly matching Step 018's own recorded final total from earlier in this
session.

### Build results (after implementation)

All four builds (`Editing`, `Shell`, `App`, full solution): `Build succeeded. 0 Warning(s). 0
Error(s).`

### Targeted test results (new tests only)

```
tests\XlightsAI.Studio.Editing.Tests — filter "~Step021": 9/9 passed
  Step021_Acceptance_Passes_SelectedModelIdsAndTimeRangeMatchExactDragRectangle
  ReversedDrag_TopToBottomAndBottomToTop_ProduceIdenticalScope
  HalfOpenBoundary_EndExclusive_EffectExactlyAtEndTicksIsNotReachedByScope
  SelectionAcrossNonadjacentRows_IncludesOnlyRowsActuallyInRange_NeverInterpolated
  RowIndicesOutOfBounds_ClampSafely_NegativePath
  EmptyRowList_ProducesEmptyScope_SilenceEdgeCase
  DragExcludingARow_EffectOnThatRow_IsRejectedOutOfScope_NegativePath
  ScopeCoversRow_ButRevisionIsStale_IsRejected_NegativePath
  ScopeCoversLockedEffectRow_IsRejected_NegativePath

tests\XlightsAI.Studio.Shell.Tests — filter "~Step021": 6/6 passed
  Step021_Acceptance_Passes_SelectedModelIdsAndTimeRangeMatchUI_ThroughRealShell
  ReversedRowAndPixelOrder_ProducesTheSameScope_ThroughRealShell
  ClearRangeSelection_ClearsScopeAndSummary
  NoMediaLoaded_ApplyRangeSelection_IsNoOp_SilenceEdgeCase
  ReloadingTimeline_ClearsAnyPriorRangeSelection
  ShellBuiltScope_ExcludingARow_EffectOnThatRow_IsRejectedOutOfScope_NegativePath
```

### Full regression (after implementation)

```
Passed!  - Failed: 0, Passed: 71, Skipped: 0, Total: 71, Duration: 349 ms  - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed: 59, Skipped: 0, Total: 59, Duration: 361 ms  - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed: 25, Skipped: 0, Total: 25, Duration: 2 s     - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed: 72, Skipped: 0, Total: 72, Duration: 1 s     - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed: 31, Skipped: 0, Total: 31, Duration: 30 s    - XlightsAI.Studio.Reference.Tests.dll
```

**258 total, 0 failed, 0 skipped** — up from the Step 018 baseline of 243 (50→59 in `Editing.Tests`,
66→72 in `Shell.Tests`; the `+15` reconciles exactly against the 9+6 new tests above). All 243
pre-existing tests still pass unmodified; no existing test file's assertions were touched.

### Manual/desktop demonstration

```
Start-Process -FilePath "src\XlightsAI.Studio.App\bin\Debug\net10.0\XlightsAI.Studio.exe" -PassThru
Start-Sleep -Seconds 5
# Still running after 5s: True
Stop-Process -Force
```

The real compiled Avalonia desktop app, including the new "Range selection" panel XAML/bindings,
started and ran for 5+ seconds with no exception and no early exit — confirming the new bindings do
not crash app startup or binding resolution (same technique Steps 017/018 used).

**No interactive screenshot of a real drag-rectangle-to-scope interaction was captured**, for the same
reason disclosed in `STEP-018.md` §4: no real Discover-compatible show-directory fixture exists in
this environment to drive Discover → Generate → Load Timeline with real rows to select across in the
live window. This is reported explicitly as **NOT VERIFIED** for the interactive screenshot artifact
specifically (the same disclosed, user-waived category as Steps 017/018), not silently claimed. The
automated test suite above (15 new tests across both projects, all exercising the real
`TimelineRangeSelectionBuilder`/`TimelineViewModel`/`ShellViewModel`/`EditTransactionService`, no
mocks) is the real behavioral evidence for this step's required acceptance and edge cases.

## 5. Fixtures / input hashes

No new fixture files were added. All tests construct the gate demo's own exact layout/plan shape
(`DisplayGraph` with `Roofline`/`Arch 1`/`Arch 2`, `ThreeEffectPlanFactory.CreatePlan(...)`) in-memory,
identical in shape to every other PHASE-03 step's own fixtures; the nonadjacent-row test
(`SelectionAcrossNonadjacentRows_IncludesOnlyRowsActuallyInRange_NeverInterpolated`) additionally
builds a small nested-group `DisplayGraph` (Front-Yard / Arch-01 / Spot-Group (collapsed) / Arch-02 /
MegaTree) matching `Step017AcceptanceTests`' own hand-built group-nesting style.

## 6. Unresolved issues / known gaps

- **No real pointer-drag gesture over the row/ruler area** (§3) — the real domain/ViewModel scope
  -building logic is fully implemented and tested, but it is driven by numeric inputs + a button, not
  a mouse-drag rectangle rendered on screen. Flagged as a disclosed, narrower interaction surface for a
  follow-up to close, not silently presented as the full gesture.
- **No real Discover-compatible show-directory fixture exists** (§4, carried forward from
  `STEP-018.md` §6) — the interactive screenshot demonstration remains NOT VERIFIED for that reason.
- **No real xLights installation available in this environment** (continuing the gap disclosed in
  every prior PHASE-02/PHASE-03 step) — this step touches no XSQ/XML export at all, so this gap does
  not affect its own required acceptance; recorded per the user's standing waiver.
- **`SelectedRangeScope` and `SelectedEffectId`/`SelectedEffectInspector` (Step 018) are independent
  selection states** — selecting a range does not clear a single-effect selection and vice versa. Not
  specified as a required interaction by either step's own text; a future step integrating multi-effect
  bulk editing against a range scope should decide explicitly whether these two selections should
  interact.

## 7. PASS / BLOCKED

**Status: PASS** for Step 021's own stated required acceptance and failure/edge case, with the
interactive-screenshot artifact explicitly NOT VERIFIED (§4, non-blocking per the user's standing
waiver) rather than silently claimed:

- "Drag rectangle returns half-open time window and model set" — proven by
  `Step021_Acceptance_Passes_SelectedModelIdsAndTimeRangeMatchExactDragRectangle` (domain) and
  `Step021_Acceptance_Passes_SelectedModelIdsAndTimeRangeMatchUI_ThroughRealShell` (real shell): the
  returned `EditScope.Time`/`.Models` match the drag rectangle's own inputs exactly, through the real
  `ITimelineViewport` transform.
- "Selections crossing nonadjacent rows must be explicit" — proven by
  `SelectionAcrossNonadjacentRows_IncludesOnlyRowsActuallyInRange_NeverInterpolated`: a drag spanning a
  collapsed group never silently includes that group's hidden children.
- "Display exact scope" — proven by the real, bound `SelectedRangeScopeSummary` string
  (`Step021_Acceptance_Passes_SelectedModelIdsAndTimeRangeMatchUI_ThroughRealShell` asserts its exact
  contents) and the corresponding XAML panel.
- "Selected model IDs and time range match UI; edge-case tests" — proven by the reversed-drag
  (order-independence), out-of-bounds-clamping, and empty-row-list tests, both at the domain and
  real-shell layers.
- "Out-of-scope, locked or stale revision must be rejected without partial mutation" — proven by
  `DragExcludingARow_EffectOnThatRow_IsRejectedOutOfScope_NegativePath`,
  `ScopeCoversRow_ButRevisionIsStale_IsRejected_NegativePath`,
  `ScopeCoversLockedEffectRow_IsRejected_NegativePath` (domain), and
  `ShellBuiltScope_ExcludingARow_EffectOnThatRow_IsRejectedOutOfScope_NegativePath` (real shell) — a
  real scope built by this step's own new code is fed into the existing, already-PASS
  `EditTransactionService`/`EffectLockPolicy` pipeline, and the original document is provably
  untouched in every case.
- "Inclusive/exclusive boundary semantics" (this step's own title) — proven directly by
  `HalfOpenBoundary_EndExclusive_EffectExactlyAtEndTicksIsNotReachedByScope`: an effect starting
  exactly at the scope's own `EndTicks` is not reached by it, while one starting at `StartTicks` is.

No previously accepted step's contract was modified except the disclosed, backward-compatible
additions in §2/§3 (`TimelineViewModel`'s new range-selection properties/methods; two new
`ShellViewModel` commands) — no existing `Editing`/`Shell`/`Xlights` public member was removed or had
its existing behavior changed, confirmed by the full pre-existing test suite (243 tests) passing
unmodified.

## 8. Phase-wide status after this session

Steps 016, 017, 018, 019, 020, and 021 all now have recorded PASS evidence. Steps 022 ("Undo/redo
transactions and lock semantics for approved effects") and 023 ("Scope-aware export and regeneration
contract") remain unimplemented and out of this session's own scope — per the task's instruction,
this session does not proceed to either. The phase-wide gate demonstration (open a three-effect
sample, drag Arch 1, resize Roofline, undo/redo both, lock Arch 2, attempt forbidden modification,
export and verify other effects stay unchanged) remains **not yet run as one continuous demonstration**
— each of its sub-behaviors has been proven independently by its own numbered step's tests
(016–021), but undo/redo (022) does not exist yet, so the full gate cannot be run end to end until
Step 022 is implemented.
