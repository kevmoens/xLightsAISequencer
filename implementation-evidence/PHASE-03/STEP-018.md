# PHASE 03 — Step 018 Evidence

**Step:** "Render effect blocks and inspector selection"
**Spec:** `docs/phase-specs/PHASE-03.md` §6, Step 018 (lines 227–241)
**Date:** 2026-10-11

## 0. Why this step is being implemented now, out of its original request order

The user's original instruction for this session named Step 021 only. Before starting, the repo's
own `implementation-evidence/PHASE-03/STEP-020.md` (§6/§7) was read and found to explicitly
recommend implementing Step 018 ("Render effect blocks and inspector selection") before Step 021
("Multi-row time-range selection"), because Step 021's own user-visible outcome is a UI
screenshot/interaction and would otherwise have nothing on-screen to select against — no
`EffectBlock`/`Inspector`/`SelectedEffect` type existed anywhere in the repository (confirmed again
here by grep, matching Step 019/020's own prior findings). The user was asked explicitly how to
proceed and chose "implement Step 018 first, then Step 021." This file is Step 018's own evidence;
`STEP-021.md` is separate and follows it.

## 1. Pre-implementation baseline inspection

- Re-confirmed the Step 018 gap: no `implementation-evidence/PHASE-03/STEP-018.md`, no
  `Step018*Tests.cs` in either test project, zero `src/` hits for `EffectBlock`/`Inspector`/
  `SelectedEffect` — all true immediately before this step's own work began.
- Read every existing `XlightsAI.Studio.Editing` type this step builds on: `ITimelineViewport`/
  `TimelineViewport` (Step 016, the shared pixel/tick transform), `TimelineRowTree`/`TimelineRowSlot`/
  `TimelineRowFlattener`/`TimelineRowId` (Step 017, the row hierarchy this step renders effect blocks
  onto), `EditScope`/`EditCommand`/`MoveEffect`/`EditTransactionService`/`EffectLockPolicy`/
  `EditRejectionReason` (Steps 019/020, the real command pipeline this step's own negative-path test
  ties selection into), and `XlightsAI.Studio.Xlights.EffectInstance`/`SequenceDocument` (PHASE-02).
- Read `src/XlightsAI.Studio.Shell/TimelineViewModel.cs`, `TimelineRowItemView.cs`, `ShellViewModel.cs`
  and `src/XlightsAI.Studio.App/Views/MainWindow.axaml` in full to reconcile this step's new rendering
  layer against the existing, already-PASS Step 016/017 wiring rather than inventing a parallel one.
- Baseline regression before any code change: **231/231 passing** (Step 020's own recorded final
  total), confirmed below in §4.

## 2. Scope actually implemented

A full UI → application → domain vertical slice, not deferred to a future step this time — Step 018's
own required acceptance ("Selection maps exactly to effect ID and time span") and user-visible outcome
("Avalonia screenshot or repeatable desktop interaction recording") are themselves about rendering and
selection, so unlike Steps 019/020 (which had nothing to render onto yet) this step *is* the UI layer.

**New types in `XlightsAI.Studio.Editing`** (pure domain, no Avalonia types):

- `EffectBlockLayout` — one effect's pixel-space block on a model's row: `EffectId`, `Target`,
  `Layer`, `StartTicks`, `EndTicks`, `RecipeId`, `Locked`, and the real `X`/`Width` computed through
  the existing `ITimelineViewport.XFromTime` transform (never a second, parallel pixel math).
- `EffectLayerTrack` — one `Layer` value's ordered (`StartTicks` ascending) list of blocks.
- `EffectBlockLayoutBuilder` — `BuildLayerTracks(effects, modelId, viewport)` groups a model's effects
  by `Layer` (ascending) into tracks ("support overlapping layers explicitly" — Step 018 implementation
  guidance: two effects whose time spans overlap on the same model never merge into one block or hide
  one another; they render on distinct tracks). `HitTest(effects, modelId, viewport, pixelX)` maps a
  pixel back to the exact effect id under it (half-open `[Start, End)`, PHASE-01's own canonical rule),
  returning the highest `Layer` value on an overlapping pixel (the topmost, explicit, documented tie
  -break) or `null` when nothing is under that pixel.

**New types in `XlightsAI.Studio.Shell`** (Avalonia-free, same convention as every existing screen
view model):

- `EffectBlockItemView` — UI-only projection of one `EffectBlockLayout`: absolute `X` becomes
  `MarginLeft` relative to the previous block on the same track (the exact same technique
  `TimelineRulerTickView` already established in Step 016 to avoid the documented Avalonia
  `Canvas.Left`/`DataTemplate` container gotcha — see `STEP-16.md` §9); carries its own bound
  `SelectCommand` (a `DelegateCommand` closing over that block's own `EffectId`), `IsLocked`,
  `IsSelected`.
- `EffectLayerRowView` — one layer's `EffectBlockItemView` list for a row.
- `EffectInspectorView` — the real "inspector" projection of the selected effect: id, target model
  name, layer, start/end ticks, recipe id/version, locked flag, and its full parameter list.

**Modified existing types** (all additive/backward-compatible):

- `TimelineRowItemView` gained `EffectLayers` (new optional constructor parameter, defaults to `[]`
  — every existing Step 017 call site keeps compiling unchanged; only `TimelineViewModel.Rows`
  itself, internal to this project, constructs this type).
- `TimelineViewModel` gained: a new `_document` field (`SequenceDocument?`); `LoadTimeline(DisplayGraph?
  layout = null, SequenceDocument? document = null)` — the new `document` parameter defaults to
  `null`, so Step 016/017's existing call sites keep compiling and behaving identically; `SelectedEffectId`,
  `SelectedEffectInspector`, `SelectEffect(Guid)`, `ClearSelection()`, `SelectNextEffect()`/
  `SelectPreviousEffect()` (keyboard navigation in real visual row/layer/start-time order, wrapping);
  `Rows` now populates each slot's `EffectLayers` from the real document + viewport.
- `ShellViewModel.LoadTimelineCommand` now also passes `ShowImport.Plan` through (same pattern as
  Step 017's own `ShowImport.Layout` pass-through); three new commands —
  `TimelineSelectNextEffectCommand`/`TimelineSelectPreviousEffectCommand`/`TimelineClearSelectionCommand`
  — wired with the same `CanExecute`/`RaiseCanExecuteChanged` convention every other Timeline command
  already uses.
- `MainWindow.axaml` (SCREEN 13 TIMELINE): each prop row now renders a nested `ItemsControl` of layer
  tracks, each a nested `ItemsControl` of clickable effect-block `Button`s (bound to `SelectCommand`,
  colored by `IsLocked`/`IsSelected` via the new `EffectBlockBrushConverter`), plus a new "Inspector"
  panel (Previous/Next/Clear buttons, bound fields, and an explicit "No effect selected" empty state).

## 3. Architecture deviations / decisions

- **Overlapping layers render as separate horizontal tracks inside a plain `StackPanel`, not absolute
  `Canvas` positioning.** Step 016's own evidence (`STEP-16.md` §9) already documented that Avalonia's
  `ItemsControl` + `Canvas` `ItemsPanel` does not propagate a `Canvas.Left` set inside a `DataTemplate`
  to the real generated container. Rather than reopen that fight, effect blocks reuse the exact same
  "absolute pixel X → margin relative to the previous sibling" technique `TimelineRulerTickView`
  already established — applied per layer track, so overlapping-in-time effects on different layers
  still render on genuinely separate rows (never visually merged), while non-overlapping effects within
  one layer still lay out left-to-right with correct real gaps.
- **Selection and the inspector live on `TimelineViewModel` (Shell), not in the `Editing` domain
  project.** `EffectBlockLayoutBuilder`/`HitTest` are pure, stateless domain functions (render/hit-test
  math only); "what is currently selected" is UI/session state, the same category as
  `_collapsedGroupIds` (Step 017) already occupies on this exact class. No new domain-level
  "selection service" was invented.
- **`TimelineViewModel.LoadTimeline` clears any existing selection on every (re)load.** Not specified
  by the step's text, but a selected `Guid` surviving a reload against a *different* document (e.g. a
  freshly regenerated plan) would silently point at an id that may not exist, or worse, may coincide
  with an unrelated effect in the new document. Clearing on load is the same "missing/stale state must
  be explicit" discipline every other screen in this project already follows.
- **`HitTest`'s overlap tie-break (topmost = highest `Layer` wins) is an explicit, new policy** — the
  phase spec names "support overlapping layers explicitly" but does not specify a tie-break rule.
  Highest-layer-wins matches xLights' own left-to-right layer compositing order (a later/higher layer
  visually sits on top); documented here and covered by
  `OverlappingLayers_RenderOnDistinctTracks_HitTestPrefersTopmostLayer`, not left implicit.
- **No new edit/mutation capability was added.** This step only renders and selects; it reuses Steps
  019/020's already-PASS `EditTransactionService`/`EffectLockPolicy` as-is to prove the "forbidden
  modification" negative path (§5) — no existing `Editing` contract was changed to support this step.

## 4. Commands actually executed

```
dotnet build src\XlightsAI.Studio.Editing\XlightsAI.Studio.Editing.csproj --nologo
dotnet build src\XlightsAI.Studio.Shell\XlightsAI.Studio.Shell.csproj --nologo
dotnet build src\XlightsAI.Studio.App\XlightsAI.Studio.App.csproj --nologo
dotnet build XlightsAI.Studio.slnx --nologo
dotnet test tests\XlightsAI.Studio.Editing.Tests\XlightsAI.Studio.Editing.Tests.csproj --nologo --filter "FullyQualifiedName~Step018" --logger "console;verbosity=normal"
dotnet test tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj --nologo --filter "FullyQualifiedName~Step018" --logger "console;verbosity=normal"
dotnet test XlightsAI.Studio.slnx --nologo
```

All run via the PowerShell tool — the sandboxed POSIX Bash tool in this session is non-functional
(every invocation, including a plain `git status`/`true`, returns exit code 1 with no output), the
same pre-existing environment limitation recorded in every prior PHASE-03 step's own evidence.

### Baseline (before this step's code changes)

```
Passed!  - Failed: 0, Passed: 71, Skipped: 0, Total: 71  - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed: 44, Skipped: 0, Total: 44  - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed: 60, Skipped: 0, Total: 60  - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed: 25, Skipped: 0, Total: 25  - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed: 31, Skipped: 0, Total: 31  - XlightsAI.Studio.Reference.Tests.dll
```

231 total, 0 failed — exactly matching Step 020's own recorded final total.

### Build results (after implementation)

- `dotnet build src\XlightsAI.Studio.Editing\XlightsAI.Studio.Editing.csproj`: `Build succeeded. 0
  Warning(s). 0 Error(s).`
- `dotnet build src\XlightsAI.Studio.Shell\XlightsAI.Studio.Shell.csproj`: `Build succeeded. 0
  Warning(s). 0 Error(s).`
- `dotnet build src\XlightsAI.Studio.App\XlightsAI.Studio.App.csproj`: one warning hit and fixed during
  implementation (`CS8848`, an ambiguous `switch` expression precedence in the new
  `EffectBlockBrushConverter`, fixed by parenthesizing the switched expression) — final result:
  `Build succeeded. 0 Warning(s). 0 Error(s).`
- `dotnet build XlightsAI.Studio.slnx` (full solution): `Build succeeded. 0 Warning(s). 0 Error(s).`

### Targeted test results (new tests only)

```
tests\XlightsAI.Studio.Editing.Tests — filter "~Step018": 6/6 passed
  Step018_Acceptance_Passes_BuildLayerTracks_MapsExactlyToEffectIdAndTimeSpan
  Step018_Acceptance_Passes_HitTest_ReturnsExactEffectId_AtAnyPixelWithinItsSpan
  HitTest_OutsideEverySpan_ReturnsNull_NegativePath
  ModelWithNoEffects_ProducesEmptyTracks_SilenceEdgeCase
  OverlappingLayers_RenderOnDistinctTracks_HitTestPrefersTopmostLayer
  LockedEffect_StillRendersAndHitTests_VisibleNotHidden

tests\XlightsAI.Studio.Shell.Tests — filter "~Step018": 6/6 passed
  Step018_Acceptance_Passes_RowsRenderRealEffectBlocks_MappedToExactEffectId
  Step018_Acceptance_Passes_ClickingABlock_SelectsExactlyThatEffectId
  Step018_Acceptance_Passes_KeyboardNavigation_CyclesThroughEveryEffect_WrapsAround
  SelectingUnknownEffectId_IsNoOp_NegativePath
  NoDocumentLoaded_RowsHaveNoEffectBlocks_KeyboardNavigationIsNoOp_SilenceEdgeCase
  SelectedLockedEffect_IsVisible_ButForbiddenEditIsStillRejected_NegativePath
```

### Full regression (after implementation)

```
Passed!  - Failed: 0, Passed: 71, Skipped: 0, Total: 71, Duration: 341 ms  - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed: 50, Skipped: 0, Total: 50, Duration: 329 ms  - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed: 25, Skipped: 0, Total: 25, Duration: 2 s     - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed: 66, Skipped: 0, Total: 66, Duration: 1 s     - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed: 31, Skipped: 0, Total: 31, Duration: 31 s    - XlightsAI.Studio.Reference.Tests.dll
```

**243 total, 0 failed, 0 skipped** — up from the Step 020 baseline of 231 (44→50 in `Editing.Tests`,
60→66 in `Shell.Tests`; the `+12` reconciles exactly against the 6+6 new tests above). All 231
pre-existing tests still pass unmodified; no existing test file's assertions were touched.

### Manual/desktop demonstration

```
Start-Process -FilePath "src\XlightsAI.Studio.App\bin\Debug\net10.0\XlightsAI.Studio.exe" -PassThru
Start-Sleep -Seconds 5
# Still running after 5s: True
Stop-Process -Force
```

The real compiled Avalonia desktop app, including the new nested effect-block `ItemsControl`s,
`EffectBlockBrushConverter`, and inspector panel XAML, started and ran for 5+ seconds with no
exception and no early exit — confirming the new bindings/converters/templates do not crash app
startup or binding resolution (same technique Step 017 used, see `STEP-017.md` §3).

**No interactive click-through screenshot was captured against a real, Discover-loaded show
directory.** Driving the real UI through an actual effect-block click requires a real
`xlights_rgbeffects.xml`/`xlights_networks.xml` show-directory fixture compatible with
`IShowDirectoryDiscoveryService`/`ShowLayoutImporter`, which does not currently exist in
`fixtures/` (only two standalone layout XML files exist, not a full discoverable show directory) —
building one is outside this step's own narrow scope. This is reported explicitly as **NOT
VERIFIED** for the interactive screenshot artifact specifically (the same category of gap Step
017 disclosed and the user separately waived as a non-blocking item for this workstream), not
silently claimed. The automated test suite above (12 new tests, both exercising the real
`EffectBlockLayoutBuilder`/`TimelineViewModel`/`ShellViewModel`/`EditTransactionService`, no mocks)
is the real behavioral evidence for this step.

## 5. Fixtures / input hashes

No new fixture files were added. All tests construct the gate demo's own exact layout/plan shape
(`DisplayGraph` with `Roofline`/`Arch 1`/`Arch 2`, `ThreeEffectPlanFactory.CreatePlan(...)`) in-memory,
identical in shape to `Step019AcceptanceTests`/`Step020AcceptanceTests`' own fixtures.

## 6. Unresolved issues / known gaps

- **No real Discover-compatible show-directory fixture exists** (§4) — the interactive
  screenshot/click-through demonstration is NOT VERIFIED for that reason, not a defect in this step's
  own implementation. A good follow-up: add a full `fixtures/show-directories/gate-demo/` with a real
  `xlights_rgbeffects.xml`/`xlights_networks.xml` pair matching the Roofline/Arch 1/Arch 2 gate-demo
  shape, so a future step's manual verification can drive Discover → Generate → Load Timeline → click
  a real block end to end.
- **No real xLights installation available in this environment** (continuing the gap disclosed in
  every prior PHASE-02/PHASE-03 step) — this step touches no XSQ/XML export at all, so this gap does
  not affect its own required acceptance; recorded per the user's standing waiver.
- **Resize/move handles are still not rendered on the effect block itself** — this step only renders
  and selects; Steps 019/020's drag/resize commands remain wired at the command/service layer only,
  with no on-screen handle to grab yet. A future step (or a follow-up to 019/020) should attach real
  pointer-drag gestures to the now-existing `EffectBlockItemView`/button visual this step adds.
- **Keyboard navigation is button-driven (Previous/Next Effect), not bound to arrow-key/Tab focus
  traversal inside the effect-block `ItemsControl` itself** — `TimelineViewModel.SelectNextEffect`/
  `SelectPreviousEffect` are real, tested, and reachable from the UI, but a future step should wire a
  real `KeyBinding`/focus-traversal behavior for a more natural keyboard-only workflow if needed.
- **`EffectBlockBrushConverter`'s "Locked" color only applies to a block's own background** — there is
  no additional lock-glyph/icon on a locked block; acceptable for this step's own scope (visually
  distinct, not merely inspector-text) but a future visual-polish pass could do more.

## 7. PASS / BLOCKED

**Status: PASS** for Step 018's own stated required acceptance and failure/edge case, with the
interactive-screenshot artifact explicitly NOT VERIFIED (§4, non-blocking per the user's standing
waiver) rather than silently claimed:

- "Effects get immutable IDs, visual selection and inspector; hit test click and keyboard navigation;
  support overlapping layers explicitly" — proven by the full `EffectBlockLayoutBuilder`/
  `TimelineViewModel` test suite (§4): real IDs through `EffectBlockLayout.EffectId`/
  `EffectBlockItemView.EffectId`, real click selection (`SelectCommand`), real keyboard navigation
  (`SelectNextEffect`/`SelectPreviousEffect`, wrap-around proven both directions), and real distinct
  overlapping-layer tracks (`OverlappingLayers_RenderOnDistinctTracks_HitTestPrefersTopmostLayer`).
- "Selection maps exactly to effect ID and time span" — proven by
  `Step018_Acceptance_Passes_ClickingABlock_SelectsExactlyThatEffectId`: the real selected id and the
  real inspector's start/end ticks match the real underlying `EffectInstance` exactly, and no other
  block's `IsSelected` flips.
- "Out-of-scope, locked or stale revision must be rejected without partial mutation" — proven at this
  step's own render/selection layer by `SelectedLockedEffect_IsVisible_ButForbiddenEditIsStillRejected_NegativePath`:
  a locked effect is fully visible/selectable (not hidden), but a real `MoveEffect` attempt against it
  through the real `EditTransactionService` is still rejected (`EditRejectionReason.Locked`) with the
  original document provably untouched — reusing, not re-implementing, Steps 019/020's already-PASS
  rejection pipeline.
- Negative/edge paths: hit-testing an empty pixel region (`HitTest_OutsideEverySpan_ReturnsNull_NegativePath`),
  a model with no effects (`ModelWithNoEffects_ProducesEmptyTracks_SilenceEdgeCase`), selecting an
  unknown id (`SelectingUnknownEffectId_IsNoOp_NegativePath`), and no document loaded at all
  (`NoDocumentLoaded_RowsHaveNoEffectBlocks_KeyboardNavigationIsNoOp_SilenceEdgeCase`) — none throw,
  none corrupt state.

No previously accepted step's contract was modified except the disclosed, backward-compatible
additions in §2/§3 (`TimelineRowItemView`'s new defaulted constructor parameter;
`TimelineViewModel.LoadTimeline`'s new defaulted parameter; three new `ShellViewModel` commands) — no
existing `Editing`/`Shell`/`Xlights` public member was removed or had its existing behavior changed,
confirmed by the full pre-existing test suite (231 tests) passing unmodified. Per the user's explicit
choice (§0), `implementation-evidence/PHASE-03/STEP-021.md` follows this file next.
