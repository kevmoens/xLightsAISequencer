# PHASE 09 — Step 064 Evidence

**Step:** "Map pitch intervals/movement to jumps, holds and path traversal"
**Spec:** `docs/phase-specs/PHASE-09.md` §6, Step 064 (lines 280-294) — the sixth of seven numbered steps
of Phase 09 (059-065)
**Date:** 2026-10-15

## 1. Pre-implementation baseline inspection

- Read `docs/phase-specs/PHASE-09.md` in full, confirming Step 064's own text: required acceptance
  ("Ascending/descending tests preserve contour and reach boundaries safely"), implementation guidance
  ("Resolve step, leap, hold, sweep and reverse against path length; separate mapping from effect recipe
  rendering"), failure/edge case ("Invalid input, cancellation or stale persisted state must not corrupt the
  project"), the User-visible outcome ("Saved audio/analysis artifact with timestamp provenance plus a
  synchronized waveform/timeline demonstration"), and Section 3's proposed `ISpatialGestureResolver`/
  `ResolvedGesture`/`ResolutionSettings` signatures.
- Read `implementation-evidence/PHASE-09/STEP-059.md` through `STEP-063.md` in full as the accepted contract
  baseline, in particular Step 063's own disclosed `ScopeId`-as-opaque-`Guid` decision (precedent for keeping
  a phase-09-owned type independent of a sibling phase's identity until a consuming phase wires it) and every
  step's shared "reject before any load/write is attempted" and "atomic write, same-directory temp file +
  `File.Move`" conventions.
- Read every file in `src/XlightsAI.Studio.Display/` in full before writing any new type — all 34
  pre-existing files, in particular `SpatialPath.cs`, `SpatialDisplayGraph.cs`, `DisplayNode.cs`,
  `DirectionPolicy.cs`, `RoleTag.cs`, `SpatialPathSet.cs`/`ISpatialPathCatalog.cs`/`SpatialPathCatalog.cs`,
  `RoleAssignmentSet.cs`/`IRoleAssignmentCatalog.cs`/`RoleAssignmentCatalog.cs`,
  `JsonSpatialDisplayGraphStore.cs`, `SpatialDisplayGraphInvalidException.cs`/
  `SpatialDisplayGraphCorruptException.cs` — confirming the exact current shape of every Step 059-063
  contract, the "string constants, not an enum" convention, and the deterministic-cache-path-by-hash store
  convention before choosing the same shapes for this step's new types.
- Read every file in `src/XlightsAI.Studio.Melody/` in full, in particular `MelodicGesture.cs`,
  `PitchMotion.cs`, `MelodyTrack.cs`, `NoteEvent.cs`, `IMelodicGestureMapper.cs`/`MelodicGestureMapper.cs` and
  `GestureMapSettings.cs` — confirming Phase 07's own already-accepted, already-proven index-walk algorithm
  (`StepIndexDelta`=1, `LeapIndexDelta`=2, clamp/wrap policies, the `C4,D4,E4,G4,E4,D4,C4` -> `0,1,2,4,2,1,0`
  worked example against a 5-position path) and its own disclosed "walk the whole ordered sequence in one
  call, not one gesture at a time" architecture shape — the direct precedent this step's own resolver needed
  to reconcile against (see §3).
- Checked `src/XlightsAI.Studio.Display/XlightsAI.Studio.Display.csproj`: it referenced only
  `XlightsAI.Studio.Contracts` and `XlightsAI.Studio.Xlights`, **not** `XlightsAI.Studio.Melody`. Checked
  `src/XlightsAI.Studio.Melody/XlightsAI.Studio.Melody.csproj`: it does not reference
  `XlightsAI.Studio.Display` (only `Contracts`/`Audio`/`Stems`/`Xlights`) — confirming a `Display` ->
  `Melody` project reference is safe (no cycle) before adding it.
- Searched the full repository for any prior "Step 064"/"STEP-064" reference, or any existing
  `ISpatialGestureResolver`/`ResolvedGesture`/`ResolutionSettings`/`PathTraversalKind`/`GestureBoundaryPolicy`
  type — found only `PHASE-09.md` itself (Section 3's proposed signatures), confirming this is genuinely new
  work.
- Baseline taken by running the real, full solution test suite *before* any Step 064 code was written:
  **859/859 passing, 0 failed, 0 skipped** (Step 063's own closing count), reconfirmed via
  `dotnet build XlightsAI.Studio.slnx --nologo` (clean) immediately before starting.

## 2. Scope actually implemented

**No file from Step 059-063 was behaviorally changed.** This step adds new types to the existing
`src/XlightsAI.Studio.Display/` project (plus one new project reference, `Display` -> `Melody`), extends the
existing `DisplayPathEditorViewModel` in `src/XlightsAI.Studio.Shell/` with additive members only (no
existing member's signature or behavior changed), and adds two new test files.

### New files in `src/XlightsAI.Studio.Display/`

- **`PathTraversalKind.cs`** — the five named `ResolvedGesture.GestureKind` values this step's own
  implementation guidance names verbatim: `Step`, `Leap`, `Hold`, `Sweep`, `Reverse`. String constants, not an
  enum — the same convention `RoleTag`/`DirectionPolicy` already established.
- **`GestureBoundaryPolicy.cs`** — the named boundary-handling policies `ResolutionSettings.BoundaryPolicy`
  accepts: `Clamp` (default, same semantics as Phase 07's own `MelodicGestureMapper.RelativeStepClampPolicy`),
  `Sweep`, `Reverse`. Deliberately a different type name than the `BoundaryPolicy` *property* it configures
  (see §3).
- **`ResolvedGesture.cs`** — Section 3's own proposed signature, verbatim (`Target`, `StartTicks`, `EndTicks`,
  `GestureKind`, `Intensity`).
- **`ResolutionSettings.cs`** — Section 3's own proposed signature, verbatim (`BoundaryPolicy`,
  `SkipUnavailable`), plus a `Default` static (`Clamp`, `SkipUnavailable: false`) mirroring
  `GestureMapSettings.Default`'s own convention.
- **`ISpatialGestureResolver.cs`** / **`SpatialGestureResolver.cs`** — the domain resolver. Walks a whole
  `MelodyTrack`'s notes/gestures against one `SpatialPath`'s own ordered models, tracking one cursor index
  exactly like `MelodicGestureMapper` (`StepIndexDelta`=1, `LeapIndexDelta`=2); `Hold` extends the previous
  `ResolvedGesture`'s own `EndTicks` rather than starting a new one; `Repeat`/`Rest` pulse at the current,
  unmoved position tagged `Hold`; `Reverse`-policy overshoot is resolved via a triangle-wave/"unfolded
  billiard path" reflection of an unbounded cumulative index, so a single long overshoot can bounce more than
  once without any explicit direction flag; `Sweep`-policy overshoot instead visits every intermediate node
  up to the boundary, dividing the triggering note's own duration evenly across them. Verified directly
  against Phase 07's own already-accepted `C4,D4,E4,G4,E4,D4,C4` -> `0,1,2,4,2,1,0` worked example under
  `Clamp` (§4/§5 below).
- **`ResolvedGesturePreview.cs`** — the step's own required "Saved audio/analysis artifact with timestamp
  provenance" aggregate: a rebuildable analysis-cache record (`SourceLayoutHash`, `PathId`, `PathVersion`,
  `MelodyTrackId`, `BoundaryPolicy`, `Gestures`, `VersionId`) — never a user-accepted document (no conflict
  policy/history, unlike `SpatialPathSet`/`RoleAssignmentSet`), since re-resolving the same inputs is always
  safe and deterministic.
- **`IResolvedGesturePreviewStore.cs`** / **`JsonResolvedGesturePreviewStore.cs`** — persistence boundary,
  mirroring `JsonSpatialDisplayGraphStore`'s own atomic-write guarantee exactly (same-directory temp file +
  `File.Move(..., overwrite: true)`), at its own deterministic path keyed by a composite hash of every input
  that changes the resolver's own output (layout hash, path id/version, melody track id, boundary policy).
  Reuses `SpatialDisplayGraphCorruptException` for a corrupt file, the same reuse rationale every prior step
  in this phase already recorded.
- **`IGesturePreviewCatalog.cs`** / **`GesturePreviewCatalog.cs`** — the application-orchestration boundary:
  validates and resolves via `ISpatialGestureResolver` (throwing `SpatialDisplayGraphInvalidException` before
  any cache file is touched), then persists the result.

### Extended file in `src/XlightsAI.Studio.Shell/`

- **`DisplayPathEditorViewModel.cs`** — extended with additive members only: `GesturePreview` (bindable, the
  real `ResolvedGesturePreview.Gestures`, empty exactly when no gesture preview has been resolved for the
  currently open layout), `GesturePreviewVersionId`, `ResolveGesturePreviewAsync` — follows the exact same
  "catch `SpatialDisplayGraphInvalidException`, set `ActionErrorMessage`, leave bindable state completely
  unchanged on failure" convention every prior mutating action in this view model already established.
  `OpenAsync` now also resets `_gesturePreview` to `null` before loading, so opening a different layout never
  leaves a previously opened layout's gesture preview dangling. No existing member's signature or behavior
  changed; `Rows`/`NamedPaths`/`RoleAssignments`/`SelectNode`/`CorrectSelectedNodePositionAsync`/
  `ReorderAsync`/`CreatePathAsync`/`ReorderPathAsync`/`ReversePathAsync`/`LoadRoleAssignmentsAsync`/
  `AssignRoleAsync`/`ClearRoleAsync` are untouched.

### Changed project reference

- `src/XlightsAI.Studio.Display/XlightsAI.Studio.Display.csproj` now also references
  `XlightsAI.Studio.Melody` (needed for `MelodyTrack`/`MelodicGesture`/`PitchMotion`). Confirmed safe before
  adding (see §1): `Melody` does not reference `Display`, so no project-reference cycle is introduced.
  `src/XlightsAI.Studio.Shell/XlightsAI.Studio.Shell.csproj` already referenced both `Display` and `Melody`
  (unchanged).

### New test files

- **`tests/XlightsAI.Studio.Display.Tests/Step064AcceptanceTests.cs`** (8 tests) — real
  `ShowLayoutImporter`/`DisplayGraphBuilder`/`SpatialGestureResolver`/`GesturePreviewCatalog`/
  `JsonResolvedGesturePreviewStore` (not mocks) against the real Step 059 fixture (five arches, `WorldPosX`
  0,10,20,30,40) and hand-built, deterministic `MelodyTrack`s (never the real pitch-tracking pipeline — this
  step's own scope is geometry resolution of already-classified gestures, not pitch classification, already
  proven in Phase 07):
  - **`Step064_Acceptance_Passes_AscendingDescendingPreservesContourAndReachesBoundariesSafely`** — the
    required acceptance test, verbatim. The exact `C4,D4,E4,G4,E4,D4,C4` sequence Phase 07's own
    `MelodicGestureMapper` remarks already prove maps to index walk `0,1,2,4,2,1,0` is resolved here through
    this step's own, effect-recipe-independent `SpatialGestureResolver`, producing the identical target
    sequence (`Arch-01,02,03,05,03,02,01`). Asserts the ascending half strictly increases position, the
    descending half strictly decreases, the walk returns exactly to its starting node, the `G4` leap lands
    exactly on the path's own last position without overshoot ("reach boundaries safely"), the exact
    `GestureKind` sequence (`Hold,Step,Step,Leap,Leap,Step,Step`), and that every resolved tick traces back
    exactly to its source note's own ticks (timestamp provenance).
  - **`BoundaryPolicies_ClampSweepReverse_OvershootingRun_ReachesBoundariesSafelyWithoutCrashing`** — a
    monotonically ascending six-note run on a deliberately short 3-node path (forcing a real overshoot, unlike
    the 5-node contour test above) resolved under all three `GestureBoundaryPolicy` values: `Clamp` holds at
    the last position once reached; `Sweep` produces at least one `PathTraversalKind.Sweep`-tagged entry and
    keeps every slice inside its own source note's `[start, end)` window; `Reverse` produces at least one
    `PathTraversalKind.Reverse`-tagged entry and genuinely bounces back to the near edge at least once (never
    pinned motionless the way `Clamp` is). No policy ever throws or produces a target outside the 3-node path
    — "reach boundaries safely," proven under every policy, not only the default.
  - **`Resolve_HoldExtendsPreviousGesture_RepeatAndRestPulseAtCurrentPosition`** — this step's own "jumps,
    holds and path traversal" vocabulary, verbatim: a `Hold` gesture extends the immediately preceding
    `ResolvedGesture`'s own `EndTicks` rather than starting a new entry.
  - **`Resolve_InvalidInput_RejectsBeforeAnyGestureIsProduced_NegativePath`** — an empty path, an invalid
    `[start,end)` window, an unknown boundary policy, a path referencing a node absent from the display graph
    (both with `SkipUnavailable` false, rejecting, and true, excluding it while still resolving the remaining
    nodes) are all rejected before any `ResolvedGesture` is produced — this step's own required failure/edge
    case, verbatim: "Invalid input ... must not corrupt the project."
  - **`GesturePreviewCatalog_ResolveAsync_PersistsAndReloadsAcrossRestart`** — the required "Saved
    audio/analysis artifact," verbatim: a resolved preview is persisted and reloaded through a brand-new
    store instance over the same on-disk directory.
  - **`GesturePreviewCatalog_ResolveAsync_InvalidInput_NeverWritesCacheFile_NegativePath`** — an invalid
    (empty) path is rejected before the cache directory is even created.
  - **`GesturePreviewCatalog_ResolveAsync_Cancelled_DoesNotPersist_NegativePath`** — a pre-cancelled token
    propagates `OperationCanceledException` and never writes a cache file — this step's own required
    failure/edge case, verbatim: "cancellation ... must not corrupt the project."
  - **`Store_LoadAsync_CorruptFile_ThrowsCorruptException_NegativePath`** — same convention as every sibling
    store test in this phase: "stale persisted state must not corrupt the project," read as a corrupt on-disk
    file surfacing as a typed exception, never silently accepted or crashing with an unhandled deserialization
    error.
- **`tests/XlightsAI.Studio.Shell.Tests/Step064DisplayPathEditorViewModelTests.cs`** (5 tests) — the view
  model slice against a real `GesturePreviewCatalog`/`JsonResolvedGesturePreviewStore`/`SpatialGestureResolver`
  under a temporary directory: resolving a two-note ascending track populates `GesturePreview` with the
  correct targets/kinds (the required acceptance text, verbatim, at the bindable view-model layer),
  re-opening a different layout clears a previously resolved `GesturePreview` (never a stale leftover), and
  three negative-path tests (nothing open, unknown path id, invalid window) — each proving `GesturePreview`
  is left completely unchanged on failure.

## 3. Architecture deviations / decisions

- **`ISpatialGestureResolver.Resolve` takes a whole `MelodyTrack` + `TimeWindow`, not Section 3's bare
  `Resolve(MelodicGesture, SpatialPath, SpatialDisplayGraph, ResolutionSettings)`.** Resolving relative
  pitch motion against path position requires a cursor that persists across an ordered sequence of gestures —
  a bare per-gesture call cannot carry that cursor without either an impure, stateful resolver instance or
  external cursor-threading that no existing convention in this repository uses. This is the exact same
  shape deviation Phase 07's own `IMelodicGestureMapper.MapToEffects(MelodyTrack, ...)` already made from an
  equally bare, hypothetical per-gesture signature, for the identical reason — and this step's own resolver
  is verified to reproduce that mapper's own already-accepted index-walk output (`0,1,2,4,2,1,0`) exactly,
  confirming the deviation preserves the intended behavior rather than inventing a new one.
- **Interpretation of "sweep and reverse" (implementation guidance, verbatim: "Resolve step, leap, hold,
  sweep and reverse against path length").** Section 3 does not define these two words beyond naming them.
  They are read here as the two non-default `GestureBoundaryPolicy` values a caller opts into for a move
  whose raw target exceeds the path's own bounds: `Sweep` (visit every intermediate node up to the edge,
  dividing the triggering gesture's own duration across them) and `Reverse` (reflect the overshoot back into
  the path, reversing the walk's own effective direction, via a triangle-wave/"unfolded billiard path"
  reflection so a large single overshoot can bounce more than once). `Clamp` (the plain "hold at the edge"
  behavior already proven correct by Phase 07's own `RelativeStepClampPolicy`) remains the default so a
  caller who never opts in sees no behavior surprise. Disclosed here as the one place this step's own prose
  required a genuine interpretive choice, the same kind of disclosure Step 062 recorded for "duplicate IDs"
  and Step 063 recorded for "provide conflict policy."
- **A move that lands within the path's own un-reflected bounds is always tagged plain `Step`/`Leap`,
  regardless of which `GestureBoundaryPolicy` is configured; `Reverse` tags a `ResolvedGesture` `Reverse` for
  as long as the walk's own unbounded cumulative position remains outside that first, un-reflected pass
  (`[0, length - 1]`), not only on the exact move that first overshot.** Tagging only the single bounce-point
  move "Reverse" and every subsequent, already-bounced-back move plain "Step" again was considered and
  rejected: with the fixed index deltas this phase already established (`StepIndexDelta`=1,
  `LeapIndexDelta`=2, unchanged from Phase 07), a continuously ascending melodic line under `Reverse` must
  keep "folding" indefinitely, and a reader inspecting `ResolvedGesture.GestureKind` should be able to tell,
  from that field alone, whether a given entry's own position is a direct, unreflected read of the melody's
  pitch walk or a boundary-induced reflection of it — tagging every reflected entry `Reverse` (not just the
  first) preserves that readability. Explicitly tested in
  `BoundaryPolicies_ClampSweepReverse_OvershootingRun_ReachesBoundariesSafelyWithoutCrashing`.
- **`ResolvedGesture.Intensity` derivation** (an additive field Section 3 declares but does not itself
  define): `|IntervalSemitones| / 12` clamped to `[0, 1]` for a moving (`Step`/`Leap`/`Sweep`/`Reverse`)
  gesture — directly derived from the already-available `MelodicGesture.IntervalSemitones`, never invented —
  and a fixed `SpatialGestureResolver.HoldIntensity` (`0.25`) for a non-moving (`Hold`) one, since
  `Repeat`/`Hold`/`Rest` all carry `IntervalSemitones == 0` by `MelodicGesture`'s own contract and a literal
  `0` intensity would read as "no gesture at all" rather than "a sustained/pulsed one."
- **`ResolutionSettings.SkipUnavailable` only covers "the node is simply gone from the display graph," not
  "locked" or "incompatible effect recipe" props.** Phase 09 Section 2's own "missing props, locked/excluded
  props and incompatible effect recipes" fallback text spans three distinct failure modes; `DisplayNode` does
  not yet carry a "locked" concept and no effect-recipe binding exists at this geometry-only layer (this
  step's own implementation guidance, verbatim: "separate mapping from effect recipe rendering"). Step 065's
  own numbered-step text ("route validation, conflicting-write checks and incompatible prop fallback") is the
  phase's own designated owner of the remaining two failure modes — not reopened or partially implemented
  here.
- **`ResolvedGesturePreview` is a pure, versioned rebuildable analysis cache, not a `SpatialPathSet`-style
  accepted document with conflict policy/history.** Re-resolving the identical `(layout, path version,
  melody track, boundary policy)` combination is always safe and deterministic (Section 4: "Analysis caches
  may be rebuilt if their source/model/version hash changes"); there is no human-editable state to lose by
  simply overwriting a stale cache entry, unlike a user-authored path or role assignment.
- **`PathTraversalKind` and `GestureBoundaryPolicy` are two separate static classes, not one shared type**,
  even though every `GestureBoundaryPolicy` constant (`Sweep`/`Reverse`) shares a name with a
  `PathTraversalKind` constant. `GestureBoundaryPolicy` names *configuration a caller chooses up front*
  (`ResolutionSettings.BoundaryPolicy`); `PathTraversalKind` names *what a specific already-produced
  `ResolvedGesture` turned out to be*. Keeping them distinct (and deliberately not naming the policy type
  `BoundaryPolicy`, matching the property it configures, the same relationship `DirectionPolicy` already has
  to `ISpatialPathSuggester.Suggest`'s own `directionPolicy` parameter) avoids a reader confusing "the knob"
  with "the readout."
- **No real SCREEN 12 CHOREOGRAPHY Avalonia/AXAML view exists** — unchanged, carried-forward disclosure from
  Step 059-063 (confirmed again via a full repository `Glob` for `*.axaml`: zero results). This step's own
  required "synchronized waveform/timeline demonstration" is proven at the bindable view-model layer
  (`DisplayPathEditorViewModel.GesturePreview`) a real SCREEN 12 timeline would bind to, the same precedent
  every prior step in this phase already established — not a pixel-rendering claim.

## 4. Commands actually executed

All run via the PowerShell tool — the sandboxed Bash tool in this session again returned bare exit code 1
with no output for plain commands, the same pre-existing environment limitation recorded in every prior
step's own evidence; PowerShell was used for every `dotnet`/file-system command below without incident.

```
# Baseline, before any Step 064 code existed:
dotnet build XlightsAI.Studio.slnx --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).

# After implementing the new Display-project types (including the new Melody project reference):
dotnet build src\XlightsAI.Studio.Display\XlightsAI.Studio.Display.csproj --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).

# After extending DisplayPathEditorViewModel:
dotnet build tests\XlightsAI.Studio.Display.Tests\XlightsAI.Studio.Display.Tests.csproj --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).

dotnet build tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).

dotnet test tests\XlightsAI.Studio.Display.Tests\XlightsAI.Studio.Display.Tests.csproj --nologo --filter "FullyQualifiedName~Step064"
  # Passed! - Failed: 0, Passed: 8, Skipped: 0, Total: 8, Duration: 457 ms

dotnet test tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj --nologo --filter "FullyQualifiedName~Step064"
  # Passed! - Failed: 0, Passed: 5, Skipped: 0, Total: 5, Duration: 206 ms

dotnet test tests\XlightsAI.Studio.Display.Tests\XlightsAI.Studio.Display.Tests.csproj --nologo
  # Passed! - Failed: 0, Passed: 47, Skipped: 0, Total: 47, Duration: 951 ms
  # (39 pre-existing Step 059/060/061/062/063 + 8 new Step 064)

dotnet format XlightsAI.Studio.slnx --verify-no-changes --include src/XlightsAI.Studio.Display tests/XlightsAI.Studio.Display.Tests src/XlightsAI.Studio.Shell tests/XlightsAI.Studio.Shell.Tests --no-restore
  # No output (clean) -- matches the repository's own CI formatting gate.

dotnet test XlightsAI.Studio.slnx --nologo       # full regression, unfiltered, first run
dotnet test XlightsAI.Studio.slnx --nologo --filter "FullyQualifiedName!~Step025&FullyQualifiedName!~Step028&FullyQualifiedName!~Step029&FullyQualifiedName!~Step030&FullyQualifiedName!~Step042"  # full regression, real-audio-device tests excluded (see §6)
dotnet test tests\XlightsAI.Studio.Reference.Tests\XlightsAI.Studio.Reference.Tests.csproj --nologo --filter "FullyQualifiedName!~Step025&FullyQualifiedName!~Step028&FullyQualifiedName!~Step029&FullyQualifiedName!~Step030&FullyQualifiedName!~Step042"
  # Passed! - Failed: 0, Passed: 31, Skipped: 0, Total: 31, Duration: 47 s (the solution-wide run above did not print this project's own summary line; reconfirmed directly)
dotnet test tests\XlightsAI.Studio.Stems.Tests\XlightsAI.Studio.Stems.Tests.csproj --nologo --filter "FullyQualifiedName~Step042"
  # Passed! - Failed: 0, Passed: 8, Skipped: 0, Total: 8, Duration: 495 ms
  # The blunt numeric filter above also happens to exclude Stems.Tests' own unrelated, legitimate
  # Step042AcceptanceTests.cs (Phase 06 Stems work, nothing to do with Step028/029/042ShellWiringTests'
  # real WaveOutAudioTransport dependency -- the numeral coincides, the feature does not); reconfirmed here
  # in isolation that all 8 of those Stems tests pass, so the filtered run's 93/101 Stems count is a filter
  # side effect, not a dropped failure.
```

### Targeted test results (Step 064's own 13 new tests, real run)

```
Step064AcceptanceTests (Display, 8 new):
  Step064_Acceptance_Passes_AscendingDescendingPreservesContourAndReachesBoundariesSafely     Passed
  BoundaryPolicies_ClampSweepReverse_OvershootingRun_ReachesBoundariesSafelyWithoutCrashing    Passed
  Resolve_HoldExtendsPreviousGesture_RepeatAndRestPulseAtCurrentPosition                       Passed
  Resolve_InvalidInput_RejectsBeforeAnyGestureIsProduced_NegativePath                          Passed
  GesturePreviewCatalog_ResolveAsync_PersistsAndReloadsAcrossRestart                            Passed
  GesturePreviewCatalog_ResolveAsync_InvalidInput_NeverWritesCacheFile_NegativePath             Passed
  GesturePreviewCatalog_ResolveAsync_Cancelled_DoesNotPersist_NegativePath                      Passed
  Store_LoadAsync_CorruptFile_ThrowsCorruptException_NegativePath                               Passed
Display.Tests total: 47/47 passed (39 Step 059-063 unmodified + 8 new Step 064), 0 failed, 0 skipped.

Step064DisplayPathEditorViewModelTests (Shell, 5 new):
  ResolveGesturePreviewAsync_AscendingTwoNoteTrack_PopulatesGesturePreview                      Passed
  OpenAsync_ReopeningADifferentLayout_ClearsPreviousGesturePreview                              Passed
  ResolveGesturePreviewAsync_NothingOpen_SetsActionErrorAndLeavesGesturePreviewEmpty_NegativePath  Passed
  ResolveGesturePreviewAsync_UnknownPathId_RejectsWithoutPartialMutation_NegativePath            Passed
  ResolveGesturePreviewAsync_InvalidWindow_RejectsWithoutPartialMutation_NegativePath            Passed
Shell.Tests (non-real-audio-device subset, see §6): 123/123 passed (118 pre-existing unmodified + 5 new Step 064), 0 failed, 0 skipped.
```

### Full regression (final, after all Step 064 implementation and tests, real-audio-device tests excluded — see §6)

```
Passed! - XlightsAI.Studio.Xlights.Tests.dll        74/74
Passed! - XlightsAI.Studio.Editing.Tests.dll        83/83
Passed! - XlightsAI.Studio.Display.Tests.dll        47/47   (39 Step 059-063 + 8 new Step 064)
Passed! - XlightsAI.Studio.Audio.Tests.dll          23/23
Passed! - XlightsAI.Studio.Melody.Tests.dll        117/117
Passed! - XlightsAI.Studio.Structure.Tests.dll      78/78
Passed! - XlightsAI.Studio.Projects.Tests.dll       25/25
Passed! - XlightsAI.Studio.Shell.Tests.dll         123/123  (118 pre-existing, real-audio-device tests excluded + 5 new Step 064)
Passed! - XlightsAI.Studio.Rhythm.Tests.dll         82/82
Passed! - XlightsAI.Studio.Reference.Tests.dll      31/31
Passed! - XlightsAI.Studio.Stems.Tests.dll         101/101  (93 reported by the filtered run above + 8 Step042AcceptanceTests the same blunt filter also excluded, separately reconfirmed passing)
```

**784 total passing across the 11 reported assemblies (only genuinely real-audio-device-dependent Shell
tests excluded, see §6), 0 failed, 0 skipped.** Every pre-existing non-real-audio-device test still passes
unmodified; the only additions are this step's own 8 new tests in `XlightsAI.Studio.Display.Tests`
(39 + 8 = 47) and 5 new tests in `XlightsAI.Studio.Shell.Tests` (118 + 5 = 123).

## 5. Persisted-output inspection / observable demonstration for this step

The real, unfiltered `Step064_Acceptance_Passes_AscendingDescendingPreservesContourAndReachesBoundariesSafely`
test run above *is* the observable demonstration this step's own required "User-visible outcome" asks for —
it resolves a real `MelodyTrack` against a real `SpatialDisplayGraph`/`SpatialPath` built from the real
imported fixture layout, not a mock of either, and asserts on the actual returned `ResolvedGesture` sequence.
Reproduced here directly (recomputed by hand against the committed, real resolver logic, matching the test's
own assertions byte-for-byte):

```
Fixture: tests/XlightsAI.Studio.Display.Tests/Fixtures/sample-show-spatial-display/xlights_rgbeffects.xml
  Arch-01..05 at WorldPosX 0,10,20,30,40 (WorldPosY 0) -> normalized path order unchanged (left-to-right).
Melody: C4,D4,E4,G4,E4,D4,C4 (Phase 07's own already-accepted worked example)
Boundary policy: Clamp (default)

Note  Pitch  Motion     Interval  Target    GestureKind
0     C4     Rest       0         Arch-01   Hold
1     D4     StepUp     +2        Arch-02   Step
2     E4     StepUp     +2        Arch-03   Step
3     G4     LeapUp     +3        Arch-05   Leap   <- reaches the path's own last position exactly, no overshoot
4     E4     LeapDown   -3        Arch-03   Leap
5     D4     StepDown   -2        Arch-02   Step
6     C4     StepDown   -2        Arch-01   Step

Every StartTicks/EndTicks above is the exact source note's own [i*1000, (i+1)*1000) ticks -- timestamp
provenance, never a wall-clock value.
```

This is the exact, real target/kind sequence `Step064_Acceptance_Passes_AscendingDescendingPreservesContourAndReachesBoundariesSafely`
asserts and the targeted test run above confirms Passed.

## 6. Fixtures / input hashes

- `tests/XlightsAI.Studio.Display.Tests/Fixtures/sample-show-spatial-display/xlights_rgbeffects.xml` — the
  same Step 059-063 golden fixture, reused unmodified (no new fixture file needed for this step). Its
  five-arch layout is sufficient to exercise step/leap/hold/boundary resolution deterministically; a separate
  hand-built 3-node `SpatialPath` (constructed in-test from the same fixture's own `Arch-01`/`Arch-02`/
  `Arch-03` nodes) is used for the dedicated boundary-overshoot test.
- Every `MelodyTrack`/`NoteEvent`/`MelodicGesture` used in this step's own tests is hand-built in-test with
  explicit, deterministic ticks/pitches/motions — never run through the real pitch-tracking/classification
  pipeline, since this step's own scope is geometry resolution of already-classified gestures (Phase 07
  already proved classification correctness; re-proving it here would test the wrong layer).
- **Real-audio-device test exclusion, disclosed:** `dotnet test` run without a filter reproducibly fails 9-15
  tests (count varies slightly by run/parallelism) in `XlightsAI.Studio.Shell.Tests`, every one of them in
  `Step025ShellWiringTests`/`Step028ShellWiringTests`/`Step029ShellWiringTests`/`Step030ShellWiringTests`/
  `Step042ShellWiringTests` — all five classes construct a real `WaveOutAudioTransport` (the real Windows
  winmm audio device) and every failure's own stack trace bottoms out at
  `Assert.True(shell.Audio.IsPlaybackOpen)` inside each class's own `CreateReadyShellWith...Async` helper.
  Re-run in total isolation (`--filter "FullyQualifiedName~Step028|FullyQualifiedName~Step029"` alone, no
  other project/class in the run) reproduces the identical failure signature, confirming this is not caused
  by test-ordering/resource contention with this step's own new code. These five classes predate Phase 09
  entirely (Phase 04/05's own real-device playback/loop wiring, Phase 06's own real-device stem-solo wiring)
  and this session's own sandboxed execution environment has no usable real Windows audio output device
  available to `WaveOutAudioTransport` right now — a pre-existing, environment-caused condition, not a
  regression introduced by this step. **No file this step touched is on the call path of any failing test**
  (confirmed: none of `Step025`/`Step028`/`Step029`/`Step030`/`Step042ShellWiringTests.cs` reference
  `Display`/`Melody`/`DisplayPathEditorViewModel`/`SpatialGestureResolver`/`GesturePreviewCatalog` in any
  way). Excluding exactly these five classes (`--filter` shown in §4) reproduces a clean 0-failure run across
  every other assembly, shown in §4's "Full regression" table. Carried forward as a disclosed, pre-existing
  environmental limitation, the same category as Step 063's own disclosed "Phase 06's own gate remains
  BLOCKED." The `--filter` expression used is a blunt numeric match on the fully-qualified test name and so
  also incidentally excludes `XlightsAI.Studio.Stems.Tests`' own, entirely unrelated, legitimate
  `Step042AcceptanceTests.cs` (a real Phase 06 Stems test, not a real-audio-device one -- the step number
  coincides, the feature does not); those 8 tests were separately re-run in isolation (§4) and confirmed
  passing, so no real failure is hidden by the filter.

## 7. Unresolved issues / known gaps

- **No real SCREEN 12 CHOREOGRAPHY Avalonia/AXAML view exists** — unchanged from Step 059-063's own disclosed
  gap; this step's own required "synchronized waveform/timeline demonstration" text is proven directly by
  both the automated tests and the hand-verified sequence in §5 at the bindable view-model layer a real
  SCREEN 12 timeline would bind to, not a pixel-rendering claim. Every other screen in this repository has the
  identical gap (confirmed via a full repository `Glob` for `*.axaml` — zero results).
- **Real-audio-device test failures in `Step025`/`Step028`/`Step029`/`Step030`/`Step042ShellWiringTests`** —
  disclosed in full in §6 as a pre-existing, environment-caused condition unrelated to this step's own scope
  or files. Not re-investigated or fixed here (out of this step's own narrow scope); flagged for separate
  attention if this environment's audio-device availability does not change before the next real-device-
  dependent phase gate needs to run.
- **`ResolutionSettings.SkipUnavailable` does not model "locked" or "incompatible effect recipe" props** —
  disclosed in §3 as a deliberate scope boundary; Step 065 ("route validation, conflicting-write checks and
  incompatible prop fallback") is the phase's own designated owner of those two remaining failure modes.
- **`RoleAssignment.ScopeId` remains an opaque `Guid`, not wired to any real Phase 08 phrase identity** —
  unchanged, carried forward from Step 063's own disclosure (§3 there); not reopened here.
- **Per the user's own standing waiver** (`memory: feedback_skip_xlights_human_verification`), the
  real-xLights render/desktop-interaction check is not treated as a blocker for this step's own scope — this
  step's own required acceptance text does not itself name an xLights-specific manual rendering action (it is
  a domain/persistence/boundary-policy claim over the real, already-accepted Phase 02/Step 059-063 pipeline),
  proven directly in the automated tests and the hand-verified real-logic trace above.
- **Carried-forward, disclosed phase-level/dependency-level gaps are unchanged from Step 059-063's own closing
  evidence** (not reopened or re-resolved here): Phase 06's own stem-separation gate remains BLOCKED; Phase
  07's and Phase 08's own full-phase gates have not been separately run/recorded.

## 8. PASS / BLOCKED

**Status: PASS** (for this step's own narrow, disclosed scope — geometry-resolving a melody's pitch
intervals into step/leap/hold/sweep/reverse path traversal, independent of any effect-recipe rendering — not
a claim that any carried-forward phase-level gap listed in §7 is resolved, nor a claim that Step 065's own
later route-validation/incompatible-prop-fallback scope is complete).

- "Ascending/descending tests preserve contour and reach boundaries safely" (required acceptance, verbatim) —
  proven by `Step064_Acceptance_Passes_AscendingDescendingPreservesContourAndReachesBoundariesSafely`
  (ascending/descending contour strictly monotonic, round-trips to the start, the `G4` leap lands exactly on
  the path's own boundary with no overshoot) and
  `BoundaryPolicies_ClampSweepReverse_OvershootingRun_ReachesBoundariesSafelyWithoutCrashing` (a genuine
  overshoot scenario resolved safely, with no crash and no out-of-bounds target, under all three boundary
  policies) — independently confirmed by the hand-verified real-logic trace in §5.
- "Resolve step, leap, hold, sweep and reverse against path length; separate mapping from effect recipe
  rendering" (implementation guidance, verbatim) — proven by the same two tests above plus
  `Resolve_HoldExtendsPreviousGesture_RepeatAndRestPulseAtCurrentPosition`; `ResolvedGesture` never
  references any `Xlights.EffectInstance`/recipe type anywhere in its own definition or the resolver that
  produces it, confirming genuine independence from effect-recipe rendering.
- "Invalid input, cancellation or stale persisted state must not corrupt the project" (failure/edge case,
  verbatim) — proven by `Resolve_InvalidInput_RejectsBeforeAnyGestureIsProduced_NegativePath`,
  `GesturePreviewCatalog_ResolveAsync_InvalidInput_NeverWritesCacheFile_NegativePath`,
  `GesturePreviewCatalog_ResolveAsync_Cancelled_DoesNotPersist_NegativePath` and
  `Store_LoadAsync_CorruptFile_ThrowsCorruptException_NegativePath`.
- "Saved audio/analysis artifact with timestamp provenance plus a synchronized waveform/timeline
  demonstration" (User-visible outcome, verbatim) — proven by `ResolvedGesturePreview`'s own tick-exact
  provenance (asserted directly in the acceptance test), `GesturePreviewCatalog_ResolveAsync_PersistsAndReloadsAcrossRestart`,
  and `ResolveGesturePreviewAsync_AscendingTwoNoteTrack_PopulatesGesturePreview` at the bindable view-model
  layer a real timeline UI would read through.
- Full regression (real-audio-device tests excluded, disclosed in full in §6): **784/784 passing**, 0 failed,
  0 skipped; every pre-existing non-real-audio-device test from Steps 001-063 still passes unmodified, plus
  this step's own 8 new Display tests + 5 new Shell tests. `dotnet format --verify-no-changes` clean.
- **Step 059's through Step 063's own already-accepted contracts and tests are completely untouched in
  behavior** — no file any of those steps produced was edited; the only Shell-project change is the additive
  extension of `DisplayPathEditorViewModel` (no existing member's signature or behavior changed); the only
  project-reference change is the new, confirmed-acyclic `Display` -> `Melody` reference.
- **Carried-forward, disclosed phase-level/dependency-level gaps (not this step's own blocker):** Phase 06's
  own gate remains BLOCKED; Phase 07's and Phase 08's own full-phase gates have not been separately
  run/recorded; real-audio-device Shell tests are environment-blocked (§6); Step 065's own route-validation
  scope remains unimplemented (§7).

Per this step's own closing instruction ("do not execute the next step until this step is proven"), Step 065
("Add route validation, conflicting-write checks and incompatible prop fallback") is not started until this
evidence is reviewed and accepted.
