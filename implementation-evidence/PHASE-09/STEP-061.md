# PHASE 09 — Step 061 Evidence

**Step:** "Create graphical layout/path editor with position correction"
**Spec:** `docs/phase-specs/PHASE-09.md` §6, Step 061 (lines 232-247) — the third of seven numbered steps
of Phase 09 (059-065)
**Date:** 2026-10-15

## 1. Pre-implementation baseline inspection

- Read `docs/phase-specs/PHASE-09.md` in full, confirming Step 061's own text: required acceptance ("Drag
  and reorder saves across restart"), implementation guidance ("Use zoomable Avalonia canvas and list
  editor with accessible selection; allow manual position correction without rewriting xLights layout"),
  failure/edge case ("Invalid model/format/version must fail safely without rewriting source files"), and
  Section 4's own "Store user-authored paths, display import hash and normalized coordinate overrides
  separately."
- Read `implementation-evidence/PHASE-09/STEP-059.md` and `STEP-060.md` in full as the accepted contract
  baseline. Both explicitly disclosed "No real SCREEN 05 DISPLAY PATHS Avalonia/AXAML view or view model
  yet" and "User-defined/named paths (`SpatialPath`) are still never constructed before Step 062" as
  carried-forward gaps this step either closes (the view model) or must continue to respect (`SpatialPath`
  stays Step 062's scope, untouched here).
- Read every file in `src/XlightsAI.Studio.Display/` in full before writing any new type: `DisplayNode.cs`,
  `SpatialEdge.cs`, `SpatialDisplayGraph.cs`, `SpatialPath.cs`, `GeometryPolicy.cs`, `NodeAlias.cs`,
  `IDisplayGraphBuilder.cs`, `DisplayGraphBuilder.cs`, `ISpatialDisplayGraphStore.cs`,
  `JsonSpatialDisplayGraphStore.cs`, `ISpatialRelationInferrer.cs`, `SpatialRelationInferrer.cs`,
  `SpatialRelationInference.cs`, `SpatialDisplayGraphInvalidException.cs`,
  `SpatialDisplayGraphCorruptException.cs`, `ReadOnlyStringSetJsonConverter.cs` — confirming the exact
  current shape of every Step 059/060 contract before touching anything.
- Read `tests/XlightsAI.Studio.Display.Tests/Step059AcceptanceTests.cs` and
  `Step060AcceptanceTests.cs`, and the real golden fixture
  `tests/XlightsAI.Studio.Display.Tests/Fixtures/sample-show-spatial-display/xlights_rgbeffects.xml`, as
  the direct structural precedent for this step's own test style (real importer/builder, not mocks;
  before/after-bytes negative-path convention).
- Read every Shell-layer screen view model to confirm the project's own established UI convention before
  building a new one: `PianoRollViewModel.cs` (closest precedent — an editable, correctable, selectable
  row-based screen), `PhraseRelationshipInspectorViewModel.cs`, `TimelineViewModel.cs`,
  `ScreenLoadState.cs`, `ObservableObject.cs`, `DelegateCommand.cs`, `ShellScreen.cs`, `ShellViewModel.cs`
  (in full) and `tests/XlightsAI.Studio.Shell.Tests/Step049PianoRollViewModelTests.cs`. Confirmed via
  `Glob` that **no `.axaml` file exists anywhere in the repository** — every prior screen (Piano Roll,
  Timeline, Structure Inspector, Audio, Show Import) is a pure, Avalonia-free `ObservableObject` view
  model only, unit-tested directly; there is no real Avalonia `Window`/`UserControl` shell at all yet.
  This step follows the identical, already-established convention rather than introducing AXAML
  inconsistently for only one screen.
- Searched the full repository for any prior "Step 061"/"STEP-061" reference, any existing
  `IDisplayLayoutEditor`/`DisplayLayoutEditor`/`DisplayLayoutCorrections`/`NodePositionOverride`/
  `DisplayPathEditorViewModel` type, or any existing `ShellScreen.DisplayPathEditor` value — found only
  `PHASE-09.md` itself, confirming this is genuinely new work.
- Baseline taken by running the real, full solution test suite *before* any Step 061 code was written:
  **821/821 passing, 0 failed, 0 skipped** (Step 060's own closing count), reconfirmed via
  `dotnet build XlightsAI.Studio.slnx --nologo` (clean) immediately before starting.

## 2. Scope actually implemented

**No file from Step 059 or Step 060 was behaviorally changed.** This step adds new types to the existing
`src/XlightsAI.Studio.Display/` project, two new types to the existing `src/XlightsAI.Studio.Shell/`
project, a new `ShellScreen` enum value plus the standard navigation wiring in `ShellViewModel`, two new
test files, and one new project reference (`XlightsAI.Studio.Shell` → `XlightsAI.Studio.Display`).

### New files in `src/XlightsAI.Studio.Display/`

- **`NodePositionOverride.cs`** — one user-authored `(ModelId NodeId, double X, double Y)` correction to a
  single node's normalized preview position.
- **`DisplayLayoutCorrections.cs`** — the full persisted correction set for one imported layout (keyed by
  `SourceLayoutHash`, the same layout-scoped identity `ISpatialDisplayGraphStore` already uses):
  `PositionOverrides` (manual "drag" corrections) plus `DisplayOrder` (the list editor's own "reorder"
  row order). Deliberately its own aggregate, not a field on `SpatialDisplayGraph` — a
  `SpatialDisplayGraph` is a rebuildable projection of the imported layout (Step 059), while this is
  user-authored state that must survive a rebuild unchanged. `DisplayOrder` is explicitly **not** a
  `SpatialPath` — `SpatialDisplayGraph.Paths` stays untouched (still always `[]`; Step 062's own later,
  artistic-route scope).
- **`IDisplayLayoutCorrectionsStore.cs`** / **`JsonDisplayLayoutCorrectionsStore.cs`** — persistence
  boundary for `DisplayLayoutCorrections`, mirroring `ISpatialDisplayGraphStore`/
  `JsonSpatialDisplayGraphStore`'s own save/load contract and atomic-write guarantee exactly (same-directory
  temp file + `File.Move(..., overwrite: true)`), at its own deterministic, separate on-disk path
  (`display-layout-corrections-<hash>.json`) so rebuilding/overwriting the graph cache never touches the
  corrections cache or vice versa. Reuses `SpatialDisplayGraphCorruptException` for a corrupt file (the
  same exception already used for the sibling graph store — a general Display-module persistence failure,
  not graph-specific by name only).
- **`IDisplayLayoutEditor.cs`** / **`DisplayLayoutEditor.cs`** — the application-orchestration boundary:
  `GetCorrectionsAsync` (loads existing corrections or `DisplayLayoutCorrections.Empty`),
  `CorrectPositionAsync` ("drag" — validates the node id exists in the supplied graph and the new
  position is a finite value in `[0, 1]` *before* loading/writing anything, then upserts the override and
  persists with an incremented `Version`), and `ReorderAsync` ("reorder" — validates the new order is an
  exact permutation of the graph's own node ids — no missing, duplicate or foreign id — *before*
  loading/writing anything, then persists with an incremented `Version`). Both validations throw
  `SpatialDisplayGraphInvalidException` (the same exception Step 059's own `DisplayGraphBuilder` already
  uses) — this step's own required failure/edge case, verbatim: "Invalid model/format/version must fail
  safely without rewriting source files."
- **`DisplayLayoutProjection.cs`** — a pure, static `Apply(SpatialDisplayGraph, DisplayLayoutCorrections)`
  function: re-applies every position override on top of a freshly rebuilt graph's nodes and reorders them
  per `DisplayOrder` (any node id present in the graph but not yet in `DisplayOrder` is appended afterward,
  preserving its original relative order — never dropped). Throws `SpatialDisplayGraphInvalidException` if
  the corrections' own `SourceLayoutHash` does not match the graph's — Section 4's own "never silently
  reorder a saved path when source coordinates change," read here as: stale corrections are rejected
  explicitly rather than silently misapplied to a different layout snapshot.

### New files in `src/XlightsAI.Studio.Shell/`

- **`DisplayNodeRowView.cs`** — UI-only, Avalonia-free projection of one `DisplayNode` for SCREEN 05
  DISPLAY PATHS, following the exact same convention as `PianoRollNoteRowView`.
- **`DisplayPathEditorViewModel.cs`** — SCREEN 05's own bindable view model. Explicit
  loading/empty/error/loaded state machine identical to `PianoRollViewModel`'s own convention: `OpenAsync`
  loads a supplied, already-built `SpatialDisplayGraph` plus whatever corrections `IDisplayLayoutEditor`
  has previously persisted; `Rows` is `DisplayLayoutProjection.Apply` applied to the currently open
  graph/corrections, in the list editor's own current display order (accessible selection via
  `SelectNode`/`SelectedNodeId`, same as `PianoRollViewModel.SelectNote`); `CorrectSelectedNodePositionAsync`
  (the canvas "drag") and `ReorderAsync` (the list editor "reorder") both catch
  `SpatialDisplayGraphInvalidException`, set `ActionErrorMessage`, and leave `Rows` completely unchanged on
  failure — the same "catch, set an error property, never let an exception reach the UI, never a partial
  mutation" convention every other screen action in this project already follows.

### Wiring changes (no behavior change to any existing screen)

- `src/XlightsAI.Studio.Shell/XlightsAI.Studio.Shell.csproj` — added a `ProjectReference` to
  `XlightsAI.Studio.Display` (needed for the new view model's own domain types).
- `ShellScreen.cs` — added `DisplayPathEditor` (new enum value; does not renumber/remove any existing
  value).
- `ShellViewModel.cs` — added `DisplayPathEditor` (constructed the same way `PianoRoll`/
  `StructureInspector` already are: no constructor-injected collaborator, same "nothing happens until an
  explicit user action" convention — navigating here never auto-opens a stale previous layout),
  `NavigateToDisplayPathEditorCommand`, `IsShowingDisplayPathEditor`, the corresponding
  `OnNavigatorChanged` switch case (a no-op, matching PianoRoll/StructureInspector), and the standard
  `OnPropertyChangedAll`/`RaiseCanExecuteChanged` additions.

### New test files

- **`tests/XlightsAI.Studio.Display.Tests/Step061AcceptanceTests.cs`** (6 tests) — real
  `ShowLayoutImporter`/`DisplayGraphBuilder`/`DisplayLayoutEditor`/`JsonDisplayLayoutCorrectionsStore`
  (not mocks):
  - **`Step061_Acceptance_Passes_DragAndReorderSavesAcrossRestart`** — the required acceptance test,
    verbatim. Imports the real Step 059 fixture, builds the graph, drags `Arch-01` to a new position and
    reverses the full node order through one editor/store instance ("session 1"), then constructs
    **brand-new** store/editor instances over the same on-disk directory and rebuilds the graph fresh
    from the same source import (asserting a different `VersionId`, proving nothing in-memory survives)
    to model a real app restart. Asserts the reloaded corrections (`Version == 2`, exact reversed order,
    exact dragged position) and the real `DisplayLayoutProjection.Apply` output against the freshly
    rebuilt graph both match session 1 exactly.
  - **`CorrectPositionAsync_UnknownNodeId_FailsSafely_WithoutRewritingSourceFile_NegativePath`** — the
    required failure/edge case, verbatim (first half: "Invalid model... must fail safely without
    rewriting source files"). Asserts the real on-disk `xlights_rgbeffects.xml` bytes are byte-identical
    before and after, and that no corrections file was ever written.
  - **`CorrectPositionAsync_OutOfRangeCoordinate_Throws_Invalid_NegativePath`** — out-of-range (`1.5`) and
    non-finite (`NaN`) coordinates are both rejected.
  - **`ReorderAsync_IncompleteOrDuplicateOrForeignOrder_Throws_Invalid_NegativePath`** — an incomplete
    order, a duplicated id, and a foreign (unknown) id are each rejected.
  - **`DisplayLayoutProjection_Apply_MismatchedSourceLayoutHash_Throws_Invalid_NegativePath`** — stale
    corrections saved against a different layout hash are rejected rather than silently misapplied.
  - **`Store_LoadAsync_CorruptFile_ThrowsCorruptException_NegativePath`** — same convention as the
    sibling `JsonSpatialDisplayGraphStore` test.
- **`tests/XlightsAI.Studio.Shell.Tests/Step061DisplayPathEditorViewModelTests.cs`** (6 tests) — the view
  model slice against a real `DisplayLayoutEditor`/`JsonDisplayLayoutCorrectionsStore` under a temporary
  directory: open-empty rendering, drag-then-reorder persisting across a simulated restart (the same
  required acceptance proven again at the UI layer), three negative-path tests (nothing selected,
  out-of-range correction, invalid reorder — each proving `Rows`/`Version` are left completely
  unchanged), and a `ShellViewModel` navigation wiring test (`ShellViewModel_DisplayPathEditorScreen_
  NavigatesAndStartsEmpty`), the same pattern `Step049PianoRollViewModelTests` already established.

## 3. Architecture deviations / decisions

- **The list editor's "reorder" is a new, separate concept (`DisplayLayoutCorrections.DisplayOrder`), not
  `SpatialPath`/`ISpatialPathEditor.Reorder` from Section 3's proposed signatures.** Section 3 proposes
  `ISpatialPathEditor.Reorder(SpatialPath, IReadOnlyList<ModelId>)`, but `SpatialPath` is explicitly
  Step 062's own later scope ("Define named custom paths and semantic spatial motions") — no step before
  062 is permitted to construct one (`SpatialDisplayGraph.Paths` stays `[]`, verified by this step's own
  tests continuing to build graphs with empty `Paths`, same as Step 059/060). Step 061's own required
  acceptance text ("Drag **and reorder** saves across restart") is a general list-editor row-order
  correction, not yet an artistic, named route with closed/open semantics — so this step introduces its
  own narrower `DisplayOrder` concept now, and leaves `ISpatialPathEditor`/`SpatialPath` construction for
  Step 062 to build on top of, exactly as the existing `SpatialPath.cs` doc comment already anticipated.
  Disclosed here as a deliberate scope boundary, not a contradiction of Section 3 — Step 062 can still
  introduce `ISpatialPathEditor` against `SpatialPath` without needing to touch this step's own
  `DisplayOrder` field at all.
- **`DisplayLayoutCorrections` is its own persisted aggregate, not an additive field on
  `SpatialDisplayGraph`.** Chosen because `SpatialDisplayGraph` is pure, rebuildable, stateless output of
  `IDisplayGraphBuilder.Build` (Step 059's own already-accepted contract: "Pure and side-effect-free;
  never touches any file on disk"), while user-authored corrections are exactly the kind of state Section
  4 says must survive a layout reimport/rebuild unchanged. Folding corrections into `SpatialDisplayGraph`
  itself would mean every `Build()` call would need to thread a mutable, caller-supplied correction set
  through a function whose entire contract is "pure projection of the imported layout" — a second,
  unrelated concern. Keeping them separate (mirroring how `ISpatialRelationInferrer`'s inferred edges in
  Step 060 are also never auto-merged into `Build()`'s own output) keeps every earlier step's already-
  accepted contract and tests completely untouched.
- **`SpatialDisplayGraphInvalidException`/`SpatialDisplayGraphCorruptException` are reused rather than new,
  step-specific exception types.** Both are already general Display-module persistence/validation failures
  (not literally graph-specific despite the name — `SpatialDisplayGraphCorruptException` is already reused
  by this step's own `JsonDisplayLayoutCorrectionsStore`), and introducing two more near-identical
  exception types for the same two failure shapes (an invalid input rejected before any write; a corrupt
  persisted file) would be pure duplication for no behavioral benefit.
- **No real SCREEN 05 DISPLAY PATHS Avalonia/AXAML view exists** — the view model (`DisplayPathEditorViewModel`)
  is the complete UI-layer slice, following the exact same established convention as every other screen in
  this repository (Piano Roll, Timeline, Audio, Structure Inspector, Show Import) — confirmed via a full
  repository `Glob` that zero `.axaml` files exist anywhere. This is not a shortcut unique to this step; it
  is the pre-existing, consistent state of the whole Shell project. Disclosed as a known gap in §7, not
  silently worked around.
- **`DisplayPathEditorViewModel` takes its `SpatialDisplayGraph` as a method parameter to `OpenAsync`
  rather than building it itself.** Same reasoning as `PianoRollViewModel.BeginEditingAsync(editor,
  analyzedTrack, ...)` taking an already-analyzed `MelodyTrack` rather than running analysis itself — the
  view model's own job is rendering/correcting an already-produced domain object, not orchestrating the
  Step 059/060 import-and-build pipeline that produced it.

## 4. Commands actually executed

All run via the PowerShell tool — the sandboxed Bash tool in this session again returned bare exit code 1
with no output for plain commands, the same pre-existing environment limitation recorded in every prior
step's own evidence; PowerShell was used for every `dotnet`/file-system command below without incident.

```
# Baseline, before any Step 061 code existed:
dotnet build XlightsAI.Studio.slnx --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).

# After implementing the new Display-project types:
dotnet build src\XlightsAI.Studio.Display\XlightsAI.Studio.Display.csproj --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).
dotnet test tests\XlightsAI.Studio.Display.Tests\XlightsAI.Studio.Display.Tests.csproj --nologo
  # Passed! - Failed: 0, Passed: 24, Skipped: 0, Total: 24, Duration: 194 ms
  # (18 pre-existing Step 059/060 + 6 new Step 061)

# After adding the Shell -> Display project reference and the new Shell-project types:
dotnet build src\XlightsAI.Studio.Shell\XlightsAI.Studio.Shell.csproj --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).
dotnet test tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj --nologo
  # Passed! - Failed: 0, Passed: 172, Skipped: 0, Total: 172, Duration: 3 s
  # (166 pre-existing + 6 new Step 061)

dotnet format XlightsAI.Studio.slnx --verify-no-changes --include src/XlightsAI.Studio.Display tests/XlightsAI.Studio.Display.Tests src/XlightsAI.Studio.Shell tests/XlightsAI.Studio.Shell.Tests --no-restore
  # No output (clean) -- matches the repository's own CI formatting gate.

dotnet build XlightsAI.Studio.slnx --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s). Confirms the new types and the new Shell->Display
  # project reference compile across the full solution graph, not only in isolation.

dotnet test XlightsAI.Studio.slnx --nologo       # full regression, final
```

### Targeted test results (Step 061's own 13 new tests, real run)

```
Step061AcceptanceTests (Display, 6 new):
  Step061_Acceptance_Passes_DragAndReorderSavesAcrossRestart                                 Passed
  CorrectPositionAsync_UnknownNodeId_FailsSafely_WithoutRewritingSourceFile_NegativePath      Passed
  CorrectPositionAsync_OutOfRangeCoordinate_Throws_Invalid_NegativePath                       Passed
  ReorderAsync_IncompleteOrDuplicateOrForeignOrder_Throws_Invalid_NegativePath                 Passed
  DisplayLayoutProjection_Apply_MismatchedSourceLayoutHash_Throws_Invalid_NegativePath         Passed
  Store_LoadAsync_CorruptFile_ThrowsCorruptException_NegativePath                              Passed
Display.Tests total: 24/24 passed (18 Step 059/060 unmodified + 6 new Step 061), 0 failed, 0 skipped.

Step061DisplayPathEditorViewModelTests (Shell, 6 new):
  OpenAsync_NoCorrectionsYet_RendersRowsInGraphOrder                                           Passed
  CorrectSelectedNodePositionAsync_ThenReorderAsync_PersistsAcrossRestart                      Passed
  CorrectSelectedNodePositionAsync_NothingSelected_SetsActionErrorAndLeavesRowsUnchanged_NegativePath  Passed
  CorrectSelectedNodePositionAsync_OutOfRange_RejectsWithoutPartialMutation_NegativePath        Passed
  ReorderAsync_InvalidOrder_RejectsWithoutPartialMutation_NegativePath                          Passed
  ShellViewModel_DisplayPathEditorScreen_NavigatesAndStartsEmpty                                Passed
Shell.Tests total: 172/172 passed (166 pre-existing unmodified + 6 new Step 061), 0 failed, 0 skipped.
```

### Full regression (final, after all Step 061 implementation and tests)

```
Passed! - XlightsAI.Studio.Xlights.Tests.dll        74/74
Passed! - XlightsAI.Studio.Editing.Tests.dll        91/91
Passed! - XlightsAI.Studio.Display.Tests.dll        24/24   (18 Step 059/060 + 6 new Step 061)
Passed! - XlightsAI.Studio.Projects.Tests.dll       25/25
Passed! - XlightsAI.Studio.Structure.Tests.dll      78/78
Passed! - XlightsAI.Studio.Melody.Tests.dll        117/117
Passed! - XlightsAI.Studio.Audio.Tests.dll          38/38
Passed! - XlightsAI.Studio.Shell.Tests.dll         172/172  (166 pre-existing + 6 new Step 061)
Passed! - XlightsAI.Studio.Rhythm.Tests.dll         82/82
Passed! - XlightsAI.Studio.Reference.Tests.dll      31/31
Passed! - XlightsAI.Studio.Stems.Tests.dll         101/101
```

**833 total (across all 11 reported assemblies), 0 failed, 0 skipped.** Every pre-existing test from Steps
001-060 still passes unmodified (821 baseline, unchanged); the only additions are this step's own 6 new
tests in `XlightsAI.Studio.Display.Tests` (18 + 6 = 24) and 6 new tests in `XlightsAI.Studio.Shell.Tests`
(166 + 6 = 172).

## 5. Persisted-output inspection / observable demonstration for this step

A real, non-test-harness console program (not part of the repository, built under the session scratchpad,
deleted after this run) exercises the real `ShowLayoutImporter`/`DisplayGraphBuilder`/
`DisplayLayoutEditor`/`JsonDisplayLayoutCorrectionsStore`/`DisplayPathEditorViewModel` directly against a
real temp copy of this step's own fixture show directory (never the repository's own copy) — no test
harness, no mocks:

```
dotnet run --project <scratchpad>\xlai-step061-demo-proj -- "<temp show directory>"
```

Observed real output:

```
=== Step 061 manual demo: real graphical layout/path editor with position correction, no test harness ===

--- Scenario 1: import the real fixture layout, build the spatial graph ---
  Graph: 11 nodes, 10 edges, VersionId=f0aee511-90a4-47b7-bc8e-58bc3fda6f0a
  Original order: Arch-01, Arch-02, Arch-03, Arch-04, Arch-05, Roof-01, Roof-02, Roof-03, Arches, Roofline, All-Props

--- Scenario 2: open SCREEN 05 via the real view model, drag Arch-01, reorder the list ---
  Opened: True, IsLoaded=True, Version=0
  Drag Arch-01 to (0.95, 0.05): succeeded=True, new position=(0.95, 0.05), Version=1
  Reorder (reverse all): succeeded=True, Version=2
  New row order: All-Props, Roofline, Arches, Roof-03, Roof-02, Roof-01, Arch-05, Arch-04, Arch-03, Arch-02, Arch-01

--- Scenario 3: restart (brand-new store/editor/view-model + freshly rebuilt graph) ---
  Rebuilt graph VersionId differs from original: True
  Reopened after restart: True, Version=2
  Row order survived restart: True
  Arch-01 position survived restart: (0.95, 0.05)

--- Scenario 4: invalid input is rejected and never corrupts the project (negative path) ---
  Reorder with incomplete node set rejected: True, error="Reorder must list every node exactly once (11 expected, 1 given)."
  Source xlights_rgbeffects.xml bytes unchanged: True

Step 061 manual demo completed successfully.

Cleaning up demo show/cache directories.
```

Direct, real, non-test-harness confirmation of this step's own required text: (1) "Drag and reorder saves
across restart" — Scenario 2 performs a real drag and a real full-list reorder through one view-model/
editor/store instance; Scenario 3 constructs brand-new store/editor/view-model instances pointed at the
same on-disk directory and a freshly rebuilt graph (different `VersionId`, proving nothing in-memory from
Scenario 2 survived) and shows both the row order and the dragged node's position identical to before the
simulated restart. (2) "Invalid model/format/version must fail safely without rewriting source files" —
Scenario 4 rejects an incomplete reorder and independently confirms the real on-disk
`xlights_rgbeffects.xml` bytes are byte-identical before and after.

## 6. Fixtures / input hashes

- `tests/XlightsAI.Studio.Display.Tests/Fixtures/sample-show-spatial-display/xlights_rgbeffects.xml` — the
  same Step 059/060 golden fixture, reused unmodified (no new fixture file needed for this step). Its
  five-arch/three-roofline layout is sufficient to exercise a real drag and a real full-list reorder.
  `SourceLayoutHash` is computed by the real `ShowLayoutImporter` from this file's own real, unmodified
  bytes.
- Negative-path tests that isolate validation logic alone (out-of-range coordinate, invalid reorder set,
  mismatched source hash, corrupt JSON) use small, hand-built in-memory `DisplayGraph`s/
  `DisplayLayoutCorrections`, consistent with Step 059/060's own disclosed convention for pure-domain-logic
  tests that do not need the real importer.
- The real demo's own persisted `DisplayLayoutCorrections` JSON files were written to a temporary
  directory for inspection and deleted, along with the temp copy of the show directory (outside the
  repository, under `%TEMP%`), by the demo's own `finally` block (confirmed by "Cleaning up demo show/
  cache directories." in the real output above); the throwaway demo *project* was deleted after this
  evidence was recorded.

## 7. Unresolved issues / known gaps

- **No real SCREEN 05 DISPLAY PATHS Avalonia/AXAML view exists** — unchanged from Step 059/060's own
  disclosed gap; this step's required acceptance text is a domain/view-model-layer correction-and-
  persistence claim ("drag and reorder saves"), proven directly by both the automated tests and the real,
  non-test-harness demo above, not a pixel-rendering claim. Every other screen in this repository has the
  identical gap (confirmed via a full repository `Glob` for `*.axaml` — zero results) — this is the
  project's own pre-existing, consistent convention, not a shortcut unique to this step.
- **`DisplayLayoutCorrections.DisplayOrder` is a plain list-editor row order, not yet a `SpatialPath`** —
  disclosed in §3 as a deliberate scope boundary; Step 062 ("Define named custom paths and semantic
  spatial motions") is still the first step permitted to construct a `SpatialPath` — `SpatialDisplayGraph.
  Paths` remains always `[]` after this step, unchanged from Step 059/060.
- **No zoomable-canvas pan/zoom state is modeled** — this step's own required acceptance text ("Drag and
  reorder saves across restart") does not itself require persisting a canvas viewport/zoom level, and no
  other step before 065 names one; `DisplayPathEditorViewModel` exposes normalized `[0, 1]` positions only,
  leaving any pixel-space pan/zoom transform as a pure, non-persisted UI-layer concern for whichever future
  step introduces the real Avalonia view.
- **Per the user's own standing waiver** (`memory: feedback_skip_xlights_human_verification`), the
  real-xLights render/desktop-interaction check is not treated as a blocker for this step's own scope —
  this step's own required acceptance text does not itself name an xLights-specific manual rendering
  action (it is a domain/view-model correction-and-persistence claim over the real, already-accepted
  Phase 02/Step 059/060 pipeline), proven directly in both the automated tests and the real,
  non-test-harness demo above.
- **Carried-forward, disclosed phase-level/dependency-level gaps are unchanged from Step 059/060's own
  closing evidence** (not reopened or re-resolved here): Phase 06's own stem-separation gate remains
  BLOCKED; Phase 07's and Phase 08's own full-phase gates have not been separately run/recorded.

## 8. PASS / BLOCKED

**Status: PASS** (for this step's own narrow, disclosed scope — a graphical layout/path editor
view-model slice with manual position correction and list-editor reorder, both validated before any write
and both persisted independently of the rebuildable `SpatialDisplayGraph` — not a claim that any
carried-forward phase-level gap listed in §7 is resolved).

- "Drag and reorder saves across restart" (required acceptance, verbatim) — proven by
  `Step061_Acceptance_Passes_DragAndReorderSavesAcrossRestart` (Display, real importer/builder/editor/
  store) and `CorrectSelectedNodePositionAsync_ThenReorderAsync_PersistsAcrossRestart` (Shell, real
  view-model/editor/store) — independently confirmed end-to-end by the real demo's Scenarios 2 and 3.
- "Invalid model/format/version must fail safely without rewriting source files" (failure/edge case,
  verbatim) — proven by `CorrectPositionAsync_UnknownNodeId_FailsSafely_WithoutRewritingSourceFile_
  NegativePath`, `CorrectPositionAsync_OutOfRangeCoordinate_Throws_Invalid_NegativePath`,
  `ReorderAsync_IncompleteOrDuplicateOrForeignOrder_Throws_Invalid_NegativePath`,
  `DisplayLayoutProjection_Apply_MismatchedSourceLayoutHash_Throws_Invalid_NegativePath` and the three
  Shell-layer "rejects without partial mutation" tests — independently confirmed by the real demo's
  Scenario 4, including a direct real-bytes-unchanged check for the source XML.
- Full regression: **833/833 passing** (across the 11 reported assemblies), 0 failed, 0 skipped; every
  pre-existing test from Steps 001-060 still passes unmodified (821 baseline, unchanged), plus this
  step's own 6 new Display tests + 6 new Shell tests.
- **Step 059's and Step 060's own already-accepted contracts and tests are completely untouched in
  behavior** — no file either step produced was edited; the only Shell-project change beyond new files is
  the additive `ShellScreen.DisplayPathEditor` enum value and the standard, no-op navigation wiring every
  prior screen already follows.
- **Carried-forward, disclosed phase-level/dependency-level gaps (not this step's own blocker):** Phase
  06's own gate remains BLOCKED; Phase 07's and Phase 08's own full-phase gates have not been separately
  run/recorded (§7).

Per this step's own closing instruction ("do not execute the next step until this step is proven"), Step
062 ("Define named custom paths and semantic spatial motions") is not started until this evidence is
reviewed and accepted.
