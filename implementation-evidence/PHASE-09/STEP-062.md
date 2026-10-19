# PHASE 09 — Step 062 Evidence

**Step:** "Define named custom paths and semantic spatial motions"
**Spec:** `docs/phase-specs/PHASE-09.md` §6, Step 062 (lines 248-262) — the fourth of seven numbered steps
of Phase 09 (059-065)
**Date:** 2026-10-15

## 1. Pre-implementation baseline inspection

- Read `docs/phase-specs/PHASE-09.md` in full, confirming Step 062's own text: required acceptance
  ("Left-right, outside-in and vertical previews match ordered props"), implementation guidance ("Create
  named ordered paths and direction policies; reject duplicate IDs unless repeat intentionally allowed;
  path history is versioned"), failure/edge case ("Invalid input, cancellation or stale persisted state
  must not corrupt the project"), Section 3's proposed `ISpatialPathEditor`/`SpatialPath` signatures, and
  Section 4's "never silently reorder a saved path when source coordinates change."
- Read `implementation-evidence/PHASE-09/STEP-059.md`, `STEP-060.md` and `STEP-061.md` in full as the
  accepted contract baseline. Step 061's own §3/§7 explicitly disclosed and carried forward: "No step
  before 062 ... is permitted to construct one [`SpatialPath`]" and "`SpatialDisplayGraph.Paths` remains
  always `[]` after this step" — this step is the first one permitted to populate it.
- Read every file in `src/XlightsAI.Studio.Display/` in full before writing any new type, in particular
  `SpatialPath.cs` (its own doc comment already anticipated this exact step), `SpatialDisplayGraph.cs`,
  `DisplayNode.cs`, `SpatialEdge.cs`, `GeometryPolicy.cs`, `IDisplayGraphBuilder.cs`/`DisplayGraphBuilder.cs`,
  `ISpatialDisplayGraphStore.cs`/`JsonSpatialDisplayGraphStore.cs`,
  `IDisplayLayoutEditor.cs`/`DisplayLayoutEditor.cs`, `DisplayLayoutCorrections.cs`,
  `IDisplayLayoutCorrectionsStore.cs`/`JsonDisplayLayoutCorrectionsStore.cs`, `NodePositionOverride.cs`,
  `DisplayLayoutProjection.cs`, `SpatialDisplayGraphInvalidException.cs`, `NodeAlias.cs` — confirming the
  exact current shape of every Step 059/060/061 contract and the established atomic-write/deterministic-
  path-by-hash store convention before touching anything.
- Read `tests/XlightsAI.Studio.Display.Tests/Step059AcceptanceTests.cs`, `Step060AcceptanceTests.cs` and
  `Step061AcceptanceTests.cs`, and the real golden fixture
  `tests/XlightsAI.Studio.Display.Tests/Fixtures/sample-show-spatial-display/xlights_rgbeffects.xml`
  (five arches at `WorldPosX` 0, 10, 20, 30, 40, `WorldPosY` 0; a three-segment roofline at `WorldPosX` 5,
  20, 35, `WorldPosY` 8), as the direct structural precedent for this step's own test style (real
  importer/builder, not mocks; before/after-bytes and reject-before-any-write negative-path convention).
- Read `src/XlightsAI.Studio.Shell/DisplayPathEditorViewModel.cs` and `DisplayNodeRowView.cs` in full, and
  `tests/XlightsAI.Studio.Shell.Tests/Step061DisplayPathEditorViewModelTests.cs`, to confirm Step 061's own
  SCREEN 05 DISPLAY PATHS view-model conventions (explicit loading/empty/error/loaded state machine; every
  mutating action catches `SpatialDisplayGraphInvalidException`, sets `ActionErrorMessage`, and leaves
  bindable state completely unchanged on failure) before extending that same view model with named-path
  support.
- Searched the full repository for any prior "Step 062"/"STEP-062" reference, or any existing
  `ISpatialPathEditor`/`SpatialPathEditor`/`ISpatialPathSuggester`/`SpatialPathSet`/`ISpatialPathCatalog`/
  `DirectionPolicy` type — found only `PHASE-09.md` itself and `SpatialPath.cs`'s own forward-reference
  doc comment, confirming this is genuinely new work.
- Baseline taken by running the real, full solution test suite *before* any Step 062 code was written:
  **833/833 passing, 0 failed, 0 skipped** (Step 061's own closing count), reconfirmed via
  `dotnet build XlightsAI.Studio.slnx --nologo` (clean) immediately before starting.

## 2. Scope actually implemented

**No file from Step 059, 060 or 061 was behaviorally changed.** This step adds new types to the existing
`src/XlightsAI.Studio.Display/` project, extends the existing `DisplayPathEditorViewModel` in
`src/XlightsAI.Studio.Shell/` with additive members only (no existing member's signature or behavior
changed), and adds two new test files.

### New files in `src/XlightsAI.Studio.Display/`

- **`DirectionPolicy.cs`** — the named direction policies `ISpatialPathSuggester` accepts:
  `LeftToRight`, `RightToLeft`, `TopToBottom`, `BottomToTop`, `OutsideIn`. String constants, not an enum —
  the same convention `SpatialEdge.Relation` already established ("not an enum so later steps can add new
  kinds without a breaking contract change").
- **`ISpatialPathEditor.cs`** / **`SpatialPathEditor.cs`** — Section 3's own proposed interface,
  implemented for the first time here (no step before 062 is permitted to construct a `SpatialPath` at
  all). `Reorder` validates the new order is an exact permutation of the path's own existing nodes before
  returning a new `SpatialPath` with `Version + 1` (throws `SpatialDisplayGraphInvalidException` on a
  missing, duplicate or foreign id — the same validation shape `DisplayLayoutEditor.ReorderAsync` already
  uses for the list editor's own reorder). `Reverse` returns a new `SpatialPath` with the node order
  reversed and `Version + 1`. Both are pure and side-effect-free, the same persistence/pure split Step 061
  established between `DisplayLayoutProjection` (pure) and `IDisplayLayoutEditor` (persisted).
- **`ISpatialPathSuggester.cs`** / **`SpatialPathSuggester.cs`** — builds a brand-new, unsaved
  `SpatialPath` (`Version` always `0`, a fresh `Id`) by ordering a caller-chosen subset of a
  `SpatialDisplayGraph`'s own nodes per a named `DirectionPolicy` — the Core process flowchart's own
  "Suggest candidate spatial paths" step, made concrete. Validates before computing any order (empty
  candidate set, unknown node id, duplicate candidate id, a node whose geometry could not be resolved
  — the `"NoGeometry"` tag — or an unrecognized policy name) and throws `SpatialDisplayGraphInvalidException`
  for each. `LeftToRight`/`RightToLeft`/`TopToBottom`/`BottomToTop` are a straightforward ascending/
  descending sort on normalized `X`/`Y` (ties broken by node id for determinism); `OutsideIn` sorts
  ascending by `X` first, then interleaves leftmost/rightmost/second-leftmost/second-rightmost/... working
  inward, with any single middle node (odd count) appended once, never doubled — this step's own required
  acceptance text, verbatim.
- **`SpatialPathSet.cs`** — the full persisted set of named paths for one imported layout (keyed by
  `SourceLayoutHash`, the same layout-scoped identity `ISpatialDisplayGraphStore`/
  `IDisplayLayoutCorrectionsStore` already use): `Paths` (current, live version of each named path) plus
  `History` (every superseded version, append-only — this step's own implementation guidance: "path
  history is versioned") plus an overall `Version` counter.
- **`ISpatialPathStore.cs`** / **`JsonSpatialPathStore.cs`** — persistence boundary for `SpatialPathSet`,
  mirroring `IDisplayLayoutCorrectionsStore`/`JsonDisplayLayoutCorrectionsStore`'s own save/load contract
  and atomic-write guarantee exactly (same-directory temp file + `File.Move(..., overwrite: true)`), at
  its own deterministic, separate on-disk path (`spatial-paths-<hash>.json`) so saving/rebuilding any one
  of the graph cache, the layout corrections or the path set never touches either of the other two.
  Reuses `SpatialDisplayGraphCorruptException` for a corrupt file, the same reuse rationale Step 061
  already recorded.
- **`ISpatialPathCatalog.cs`** / **`SpatialPathCatalog.cs`** — the application-orchestration boundary:
  `GetPathsAsync` (loads existing paths or `SpatialPathSet.Empty`); `CreateAsync` (builds a new path via
  `ISpatialPathSuggester` — which validates and fails *before* any load/write is attempted — then rejects
  a duplicate path `Name` unless the caller passes `allowReplaceExisting: true`, this step's own required
  "reject duplicate IDs unless repeat intentionally allowed," read here as: a named path's own `Name` is
  its user-facing identifier, and a replace keeps the same underlying `SpatialPath.Id`, moves the
  superseded version into `History`, and saves at `Version + 1` — a new version of the same path, not a
  fresh, disconnected one); `ReorderAsync`/`ReverseAsync` (look up the live path by `Id`, throw
  `SpatialDisplayGraphInvalidException` if it does not exist, apply `ISpatialPathEditor.Reorder`/`.Reverse`
  — which may itself throw before any write — move the prior version into `History`, and persist at an
  incremented overall `SpatialPathSet.Version`).

### Extended file in `src/XlightsAI.Studio.Shell/`

- **`DisplayPathEditorViewModel.cs`** — extended with additive members only (SCREEN 05 DISPLAY PATHS
  remains the one screen that owns both the graph/correction view and the named-path view, consistent
  with the phase gate's own single-screen demo: "inspect suggested arch and roofline paths, reverse the
  arch route manually"): `NamedPaths` (bindable, real `SpatialPathSet.Paths`, empty exactly when no path
  set has been loaded for the currently open layout), `PathSetVersion`, `LoadPathsAsync`, `CreatePathAsync`,
  `ReorderPathAsync`, `ReversePathAsync` — every mutating action follows the exact same "catch
  `SpatialDisplayGraphInvalidException`, set `ActionErrorMessage`, leave bindable state completely
  unchanged on failure" convention `CorrectSelectedNodePositionAsync`/`ReorderAsync` already established in
  Step 061. `OpenAsync` now also resets `_pathSet` to `null` before loading, so opening a different layout
  never leaves a previously opened layout's named paths dangling. No existing member's signature or
  behavior changed; `Rows`/`SelectNode`/`CorrectSelectedNodePositionAsync`/`ReorderAsync` are untouched.

### New test files

- **`tests/XlightsAI.Studio.Display.Tests/Step062AcceptanceTests.cs`** (8 tests) — real
  `ShowLayoutImporter`/`DisplayGraphBuilder`/`SpatialPathSuggester`/`SpatialPathEditor`/
  `SpatialPathCatalog`/`JsonSpatialPathStore` (not mocks):
  - **`Step062_Acceptance_Passes_LeftRightOutsideInAndVerticalPreviewsMatchOrderedProps`** — the required
    acceptance test, verbatim. Imports the real Step 059 fixture, builds the graph, and asserts: left-right
    over the five arches reproduces their natural `WorldPosX` order (`Arch-01..05`); outside-in produces
    `Arch-01, Arch-05, Arch-02, Arch-04, Arch-03` (leftmost, rightmost, second-leftmost, second-rightmost,
    middle); vertical (top-to-bottom) over `{Roof-02, Arch-03}` (same `WorldPosX` 20, different height)
    produces `Arch-03` (normalized Y 0) before `Roof-02` (normalized Y 1) — each suggestion starts at
    `Version` 0 with a fresh, distinct `Id`.
  - **`CreateAsync_ReorderAsync_ReverseAsync_PersistVersionedHistoryAcrossRestart`** — creates a named
    "Arch Route" path, reverses it, then manually reorders it, through one catalog/store instance
    ("session 1"), asserting the path's own `Version` increments (0 → 1 → 2), the overall
    `SpatialPathSet.Version` increments on every accepted edit (1 → 2 → 3), and each superseded version
    accumulates in `History` (never trimmed) — then constructs a **brand-new** catalog/store instance over
    the same on-disk directory ("restart") and confirms the reloaded set matches session 1 exactly
    (`Version` 3, the manually-reordered node order, 2 history entries).
  - **`CreateAsync_DuplicateName_RejectedUnlessAllowReplaceExisting_NegativePath`** — the required
    implementation-guidance behavior, verbatim ("reject duplicate IDs unless repeat intentionally
    allowed"). A second `CreateAsync` with the same name and `allowReplaceExisting: false` is rejected and
    leaves the persisted set completely unchanged (re-read and compared); the same call with
    `allowReplaceExisting: true` keeps the original path's own `Id`, bumps its `Version`, and moves the
    prior version into `History`.
  - **`Suggest_InvalidInput_RejectsBeforeAnyPathIsBuilt_NegativePath`** — empty candidate set, unknown
    node id, duplicate candidate id, unrecognized direction policy, and a node with no resolvable geometry
    (`"NoGeometry"` tag) are each rejected before any ordering is computed.
  - **`SpatialPathEditor_Reorder_NonPermutation_Throws_Invalid_NegativePath`** — an incomplete order, a
    duplicated id, and a foreign (unknown-to-the-path) id are each rejected.
  - **`ReorderAsync_ReverseAsync_UnknownPathId_Throws_Invalid_NegativePath`** — both catalog mutators
    reject an unknown path id and never write a path-set file at all (`File.Exists` asserted `false`
    afterward).
  - **`CreateAsync_Cancelled_DoesNotPersist_NegativePath`** — a pre-cancelled token propagates
    `OperationCanceledException` and never writes a path-set file — this step's own required failure/edge
    case, verbatim: "cancellation ... must not corrupt the project."
  - **`Store_LoadAsync_CorruptFile_ThrowsCorruptException_NegativePath`** — same convention as the sibling
    `JsonDisplayLayoutCorrectionsStore`/`JsonSpatialDisplayGraphStore` tests — "stale persisted state must
    not corrupt the project," read as: a corrupt on-disk file is surfaced as a typed exception, never
    silently accepted or allowed to crash with an unhandled deserialization error.
- **`tests/XlightsAI.Studio.Shell.Tests/Step062DisplayPathEditorViewModelTests.cs`** (5 tests) — the view
  model slice against a real `SpatialPathCatalog`/`JsonSpatialPathStore` under a temporary directory:
  loading an empty path set, create-then-reverse-then-reorder updating `NamedPaths`/`PathSetVersion`
  live, and three negative-path tests (nothing open, duplicate name without replace, unknown path id —
  each proving `NamedPaths` is left completely unchanged on failure).

## 3. Architecture deviations / decisions

- **`ISpatialPathEditor` is implemented for the first time in this step, exactly matching Section 3's
  proposed two-method signature (`Reorder`, `Reverse`) with no additive members on the interface itself.**
  No deviation to disclose here — unlike Step 061 (which deliberately built its own narrower
  `DisplayLayoutCorrections.DisplayOrder` concept instead of this interface, because `SpatialPath`
  construction was explicitly out of scope before this step), this is the step Section 3's own doc comment
  on `SpatialPath.cs` already named as the first permitted to build one.
- **A fresh, additive `ISpatialPathCatalog` (persistence/orchestration) sits alongside `ISpatialPathEditor`
  (pure in-memory edit) and `ISpatialPathSuggester` (pure candidate-generation) rather than folding
  create/persist/history responsibility into `ISpatialPathEditor` itself.** Section 3 only proposes
  `ISpatialPathEditor` as a pure, two-method contract (`Reorder`/`Reverse` on an already-existing
  `SpatialPath`); it does not propose a construction or persistence contract at all. This mirrors the
  exact three-way split Step 061 already established for its own concerns (pure `DisplayLayoutProjection`,
  persisted `IDisplayLayoutEditor`/`IDisplayLayoutCorrectionsStore`) — kept consistent rather than
  overloading one interface with two different kinds of responsibility (pure edit vs. validated,
  versioned, persisted catalog state).
- **"Reject duplicate IDs unless repeat intentionally allowed" is read as rejecting a duplicate path
  *Name*, not a duplicate `SpatialPath.Id`.** A `SpatialPath.Id` is a server-assigned `Guid` the caller
  never supplies directly (`ISpatialPathSuggester.Suggest` always mints a fresh one for a brand-new path);
  the human-chosen, user-facing identifier a sequencer actually types and could collide on is the path's
  own `Name` ("Arch Route"). `SpatialPathCatalog.CreateAsync`'s own `allowReplaceExisting` parameter is the
  "repeat intentionally allowed" escape hatch, verbatim — and when exercised, it deliberately *keeps* the
  existing path's own `Id` rather than minting a new one, so a replace reads as "a new version of this
  same named path," consistent with "path history is versioned" applying per-identity, not per-name.
  Disclosed here as the one place this step's own prose required an explicit interpretive choice.
- **`OutsideIn` is defined as a fixed geometric algorithm (sort by `X`, then interleave from both ends
  inward), not a caller-suppliable centroid or axis.** This step's own required acceptance text names
  three concrete preview kinds ("Left-right, outside-in and vertical") without specifying outside-in's own
  exact algorithm; the chosen definition is the common, unambiguous reading used throughout the sequencing
  domain (meet-in-the-middle from the physical edges of a row) and is exercised directly by
  `Step062_Acceptance_Passes_LeftRightOutsideInAndVerticalPreviewsMatchOrderedProps` against the real
  five-arch fixture.
- **No real SCREEN 05 DISPLAY PATHS Avalonia/AXAML view exists** — unchanged, carried-forward disclosure
  from Step 059/060/061 (confirmed again via a full repository `Glob` for `*.axaml`: zero results); this
  step's required acceptance text is itself a domain/suggestion-algorithm claim ("previews match ordered
  props"), proven directly by both the automated tests and the real, non-test-harness demo below, not a
  pixel-rendering claim.

## 4. Commands actually executed

All run via the PowerShell tool — the sandboxed Bash tool in this session again returned bare exit code 1
with no output for plain commands, the same pre-existing environment limitation recorded in every prior
step's own evidence; PowerShell was used for every `dotnet`/file-system command below without incident.

```
# Baseline, before any Step 062 code existed:
dotnet build XlightsAI.Studio.slnx --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).

# After implementing the new Display-project types:
dotnet build src\XlightsAI.Studio.Display\XlightsAI.Studio.Display.csproj --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).
dotnet test tests\XlightsAI.Studio.Display.Tests\XlightsAI.Studio.Display.Tests.csproj --nologo
  # Passed! - Failed: 0, Passed: 32, Skipped: 0, Total: 32, Duration: 209 ms
  # (24 pre-existing Step 059/060/061 + 8 new Step 062)

# After extending DisplayPathEditorViewModel and adding the new Shell-project test file:
dotnet build src\XlightsAI.Studio.Shell\XlightsAI.Studio.Shell.csproj --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).
dotnet test tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj --nologo
  # Passed! - Failed: 0, Passed: 177, Skipped: 0, Total: 177, Duration: 4 s
  # (172 pre-existing + 5 new Step 062)

dotnet format XlightsAI.Studio.slnx --verify-no-changes --include src/XlightsAI.Studio.Display tests/XlightsAI.Studio.Display.Tests src/XlightsAI.Studio.Shell tests/XlightsAI.Studio.Shell.Tests --no-restore
  # No output (clean) -- matches the repository's own CI formatting gate.

dotnet build XlightsAI.Studio.slnx --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s). Confirms the new types compile across the full solution
  # graph, not only in isolation.

dotnet test XlightsAI.Studio.slnx --nologo       # full regression, final
```

### Targeted test results (Step 062's own 13 new tests, real run)

```
Step062AcceptanceTests (Display, 8 new):
  Step062_Acceptance_Passes_LeftRightOutsideInAndVerticalPreviewsMatchOrderedProps   Passed
  CreateAsync_ReorderAsync_ReverseAsync_PersistVersionedHistoryAcrossRestart          Passed
  CreateAsync_DuplicateName_RejectedUnlessAllowReplaceExisting_NegativePath           Passed
  Suggest_InvalidInput_RejectsBeforeAnyPathIsBuilt_NegativePath                       Passed
  SpatialPathEditor_Reorder_NonPermutation_Throws_Invalid_NegativePath                Passed
  ReorderAsync_ReverseAsync_UnknownPathId_Throws_Invalid_NegativePath                 Passed
  CreateAsync_Cancelled_DoesNotPersist_NegativePath                                   Passed
  Store_LoadAsync_CorruptFile_ThrowsCorruptException_NegativePath                    Passed
Display.Tests total: 32/32 passed (24 Step 059/060/061 unmodified + 8 new Step 062), 0 failed, 0 skipped.

Step062DisplayPathEditorViewModelTests (Shell, 5 new):
  LoadPathsAsync_NothingSavedYet_ReturnsEmptyNamedPaths                               Passed
  CreatePathAsync_ReorderPathAsync_ReversePathAsync_UpdateNamedPaths                  Passed
  CreatePathAsync_NothingOpen_SetsActionErrorAndLeavesNamedPathsEmpty_NegativePath    Passed
  CreatePathAsync_DuplicateNameWithoutReplace_RejectsWithoutPartialMutation_NegativePath  Passed
  ReorderPathAsync_UnknownPathId_RejectsWithoutPartialMutation_NegativePath           Passed
Shell.Tests total: 177/177 passed (172 pre-existing unmodified + 5 new Step 062), 0 failed, 0 skipped.
```

### Full regression (final, after all Step 062 implementation and tests)

```
Passed! - XlightsAI.Studio.Xlights.Tests.dll        74/74
Passed! - XlightsAI.Studio.Editing.Tests.dll        91/91
Passed! - XlightsAI.Studio.Display.Tests.dll        32/32   (24 Step 059/060/061 + 8 new Step 062)
Passed! - XlightsAI.Studio.Projects.Tests.dll       25/25
Passed! - XlightsAI.Studio.Structure.Tests.dll      78/78
Passed! - XlightsAI.Studio.Melody.Tests.dll        117/117
Passed! - XlightsAI.Studio.Audio.Tests.dll          38/38
Passed! - XlightsAI.Studio.Shell.Tests.dll         177/177  (172 pre-existing + 5 new Step 062)
Passed! - XlightsAI.Studio.Rhythm.Tests.dll         82/82
Passed! - XlightsAI.Studio.Reference.Tests.dll      31/31
Passed! - XlightsAI.Studio.Stems.Tests.dll         101/101
```

**846 total (across all 11 reported assemblies), 0 failed, 0 skipped.** Every pre-existing test from Steps
001-061 still passes unmodified (833 baseline, unchanged); the only additions are this step's own 8 new
tests in `XlightsAI.Studio.Display.Tests` (24 + 8 = 32) and 5 new tests in `XlightsAI.Studio.Shell.Tests`
(172 + 5 = 177).

## 5. Persisted-output inspection / observable demonstration for this step

A real, non-test-harness console program (not part of the repository, built under the session scratchpad,
deleted after this run) exercises the real `ShowLayoutImporter`/`DisplayGraphBuilder`/`SpatialPathSuggester`/
`SpatialPathEditor`/`SpatialPathCatalog`/`JsonSpatialPathStore` directly against a real temp copy of this
step's own fixture show directory (never the repository's own copy) — no test harness, no mocks:

```
dotnet run --project <scratchpad>\xlai-step062-demo-proj
```

Observed real output:

```
=== Step 062 manual demo: real named custom paths and semantic spatial motions, no test harness ===

--- Scenario 1: import the real fixture layout, build the spatial graph ---
  Graph: 11 nodes, 10 edges, VersionId=fb9a9313-89c0-4650-bd8f-b3aea5b5fe04

--- Scenario 2: suggest left-right, outside-in and vertical previews (pure, unsaved) ---
  Arch Left-Right:  Arch-01, Arch-02, Arch-03, Arch-04, Arch-05
  Arch Outside-In:  Arch-01, Arch-05, Arch-02, Arch-04, Arch-03
  Roofline Left-Right: Roof-01, Roof-02, Roof-03
  Column Top-Bottom (Arch-03 over Roof-02): Arch-03, Roof-02

--- Scenario 3: create and persist the arch + roofline named paths, reverse the arch route ---
  Created Arch Route (v0): Arch-01, Arch-02, Arch-03, Arch-04, Arch-05
  Path set version after both creates: 2
  Reversed Arch Route (v1): Arch-05, Arch-04, Arch-03, Arch-02, Arch-01
  Path set version after reverse: 3, history entries: 1

--- Scenario 4: restart (brand-new catalog/store instance over the same on-disk directory) ---
  Reloaded path set version: 3, paths: 2, history: 1
  Reloaded Arch Route order survived restart: True

--- Scenario 5: invalid input is rejected and never corrupts the project (negative path) ---
  Duplicate-name create without allowReplaceExisting rejected: True, error="A path named 'Arch Route' already exists for this layout; pass allowReplaceExisting to update it."
  Source xlights_rgbeffects.xml bytes unchanged: True

Step 062 manual demo completed successfully.

Cleaning up demo show/cache directories.
```

Direct, real, non-test-harness confirmation of this step's own required text: (1) "Left-right, outside-in
and vertical previews match ordered props" — Scenario 2 produces the real left-right, outside-in and
vertical orderings directly against the real, imported fixture layout. (2) "reject duplicate IDs unless
repeat intentionally allowed" / "path history is versioned" — Scenario 3's reverse and Scenario 5's
rejected duplicate-name create both show path history accumulating and a duplicate name rejected outright.
(3) "Invalid input, cancellation or stale persisted state must not corrupt the project" — Scenario 5
confirms the real on-disk `xlights_rgbeffects.xml` bytes are byte-identical before and after the rejected
attempt. (4) The phase gate's own named workflow element — "inspect suggested arch and roofline paths,
reverse the arch route manually" — is exercised directly (Scenarios 2 and 3), though the gate's full
"preview C-D-E-G-E-D-C across both and prove geometry-aware effects target correct xLights props" clause
is Step 064's (`ISpatialGestureResolver`) own later scope, not claimed here.

## 6. Fixtures / input hashes

- `tests/XlightsAI.Studio.Display.Tests/Fixtures/sample-show-spatial-display/xlights_rgbeffects.xml` — the
  same Step 059/060/061 golden fixture, reused unmodified (no new fixture file needed for this step). Its
  five-arch/three-roofline layout with distinct `WorldPosX`/`WorldPosY` values is sufficient to exercise
  all three required preview kinds (left-right, outside-in, vertical) deterministically.
- Negative-path tests that isolate validation logic alone (unknown node, unsupported policy, no-geometry
  node, non-permutation reorder, unknown path id, cancellation, corrupt JSON) use small, hand-built
  in-memory `DisplayGraph`s, consistent with Step 059/060/061's own disclosed convention for pure-domain-
  logic tests that do not need the real importer.
- The real demo's own persisted `SpatialPathSet` JSON file was written to a temporary directory for
  inspection and deleted, along with the temp copy of the show directory (outside the repository, under
  `%TEMP%`), by the demo's own `finally` block (confirmed by "Cleaning up demo show/cache directories." in
  the real output above); the throwaway demo *project* was deleted after this evidence was recorded.

## 7. Unresolved issues / known gaps

- **No real SCREEN 05 DISPLAY PATHS Avalonia/AXAML view exists** — unchanged from Step 059/060/061's own
  disclosed gap; this step's required acceptance text is a domain/suggestion-algorithm claim ("previews
  match ordered props"), proven directly by both the automated tests and the real, non-test-harness demo
  above, not a pixel-rendering claim. Every other screen in this repository has the identical gap
  (confirmed via a full repository `Glob` for `*.axaml` — zero results) — this is the project's own
  pre-existing, consistent convention, not a shortcut unique to this step.
- **Geometry-aware gesture resolution (`ISpatialGestureResolver`, "C-D-E-G-E-D-C across both" and "prove
  geometry-aware effects target correct xLights props") is not implemented here** — Section 3 names it as
  its own separate interface, and the phase's own numbered-step table assigns "Map pitch intervals/
  movement to jumps, holds and path traversal" to Step 064 specifically; this step's own required
  acceptance text only names the three preview kinds, not gesture resolution. Disclosed here, not silently
  worked around — the phase gate's own full demonstration text is explicitly deferred to Step 064/065, the
  same way Step 061 deferred `SpatialPath` construction to this step.
- **`DirectionPolicy.OutsideIn`'s specific meet-in-the-middle algorithm is this step's own interpretive
  choice** (§3) — disclosed as a deliberate, tested definition, not an ambiguity left unresolved.
- **Per the user's own standing waiver** (`memory: feedback_skip_xlights_human_verification`), the
  real-xLights render/desktop-interaction check is not treated as a blocker for this step's own scope —
  this step's own required acceptance text does not itself name an xLights-specific manual rendering
  action (it is a domain/suggestion-algorithm and persisted-versioning claim over the real, already-
  accepted Phase 02/Step 059/060/061 pipeline), proven directly in both the automated tests and the real,
  non-test-harness demo above.
- **Carried-forward, disclosed phase-level/dependency-level gaps are unchanged from Step 059/060/061's own
  closing evidence** (not reopened or re-resolved here): Phase 06's own stem-separation gate remains
  BLOCKED; Phase 07's and Phase 08's own full-phase gates have not been separately run/recorded.

## 8. PASS / BLOCKED

**Status: PASS** (for this step's own narrow, disclosed scope — named, versioned spatial paths with
direction-policy-based suggestion, reorder/reverse editing, and duplicate-name-safe, history-preserving
persistence — not a claim that any carried-forward phase-level gap listed in §7 is resolved, nor a claim
that geometry-aware gesture resolution, Step 064's own later scope, is complete).

- "Left-right, outside-in and vertical previews match ordered props" (required acceptance, verbatim) —
  proven by `Step062_Acceptance_Passes_LeftRightOutsideInAndVerticalPreviewsMatchOrderedProps` (Display,
  real importer/builder/suggester against the real fixture) — independently confirmed end-to-end by the
  real demo's Scenario 2.
- "Create named ordered paths and direction policies; reject duplicate IDs unless repeat intentionally
  allowed; path history is versioned" (implementation guidance, verbatim) — proven by
  `CreateAsync_ReorderAsync_ReverseAsync_PersistVersionedHistoryAcrossRestart` and
  `CreateAsync_DuplicateName_RejectedUnlessAllowReplaceExisting_NegativePath` (Display, real catalog/store)
  plus `CreatePathAsync_ReorderPathAsync_ReversePathAsync_UpdateNamedPaths` and
  `CreatePathAsync_DuplicateNameWithoutReplace_RejectsWithoutPartialMutation_NegativePath` (Shell, real
  view-model/catalog/store) — independently confirmed by the real demo's Scenarios 3, 4 and 5.
- "Invalid input, cancellation or stale persisted state must not corrupt the project" (failure/edge case,
  verbatim) — proven by `Suggest_InvalidInput_RejectsBeforeAnyPathIsBuilt_NegativePath`,
  `SpatialPathEditor_Reorder_NonPermutation_Throws_Invalid_NegativePath`,
  `ReorderAsync_ReverseAsync_UnknownPathId_Throws_Invalid_NegativePath`,
  `CreateAsync_Cancelled_DoesNotPersist_NegativePath`,
  `Store_LoadAsync_CorruptFile_ThrowsCorruptException_NegativePath` and the two Shell-layer "rejects
  without partial mutation" tests — independently confirmed by the real demo's Scenario 5, including a
  direct real-bytes-unchanged check for the source XML.
- Full regression: **846/846 passing** (across the 11 reported assemblies), 0 failed, 0 skipped; every
  pre-existing test from Steps 001-061 still passes unmodified (833 baseline, unchanged), plus this step's
  own 8 new Display tests + 5 new Shell tests.
- **Step 059's, Step 060's and Step 061's own already-accepted contracts and tests are completely
  untouched in behavior** — no file any of those steps produced was edited; the only Shell-project change
  is the additive extension of `DisplayPathEditorViewModel` (no existing member's signature or behavior
  changed).
- **Carried-forward, disclosed phase-level/dependency-level gaps (not this step's own blocker):** Phase
  06's own gate remains BLOCKED; Phase 07's and Phase 08's own full-phase gates have not been separately
  run/recorded (§7); geometry-aware gesture resolution remains Step 064's own later scope (§7).

Per this step's own closing instruction ("do not execute the next step until this step is proven"), Step
063 ("Persist role tags: lead, rhythm, ambient, hero and accent") is not started until this evidence is
reviewed and accepted.
