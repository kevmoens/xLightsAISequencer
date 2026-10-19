# PHASE 09 — Step 065 Evidence

**Step:** "Add route validation, conflicting-write checks and incompatible prop fallback"
**Spec:** `docs/phase-specs/PHASE-09.md` §6, Step 065 (lines 296-310) — the seventh and final of seven numbered
steps of Phase 09 (059-065)
**Date:** 2026-10-15

## 1. Pre-implementation baseline inspection

- Read `docs/phase-specs/PHASE-09.md` in full, confirming Step 065's own text: required acceptance ("Invalid
  path cannot silently produce mis-targeted XSQ effects"), implementation guidance ("Validate missing/locked
  prop, effect support, path intersections and overlaps; report incompatibilities before generating XSQ"),
  failure/edge case ("Invalid input, cancellation or stale persisted state must not corrupt the project"),
  the User-visible outcome ("Versioned, inspectable project output plus a reproducible test report"), and
  Section 2's own broader instruction this step closes out: "Route resolution should honor missing props,
  locked/excluded props and incompatible effect recipes; produce a warning and fallback rather than silent
  mutation."
- Read `implementation-evidence/PHASE-09/STEP-059.md` through `STEP-064.md` in full as the accepted contract
  baseline — in particular Step 064's own disclosed scope boundary (§3 there): "`ResolutionSettings.SkipUnavailable`
  does not model 'locked' or 'incompatible effect recipe' props... Step 065's own numbered-step text... is the
  phase's own designated owner of the remaining two failure modes" — the explicit hand-off this step accepts
  and closes.
- Read every file in `src/XlightsAI.Studio.Display/` in full before writing any new type — all 49
  pre-existing files (34 through Step 063, 15 added by Step 064), in particular `SpatialPath.cs`,
  `SpatialDisplayGraph.cs`, `DisplayNode.cs`, `RoleAssignmentSet.cs`/`IRoleAssignmentStore.cs`/
  `JsonRoleAssignmentStore.cs`/`IRoleAssignmentCatalog.cs`/`RoleAssignmentCatalog.cs` (the closest existing
  precedent for a layout-scoped, versioned, persisted set with a catalog boundary), `ISpatialPathCatalog.cs`/
  `SpatialPathCatalog.cs` (the "a caller attaches `graph with { Paths = pathSet.Paths }`" convention),
  `ResolvedGesturePreview.cs`/`IResolvedGesturePreviewStore.cs`/`JsonResolvedGesturePreviewStore.cs`/
  `IGesturePreviewCatalog.cs`/`GesturePreviewCatalog.cs` (the closest existing precedent for a pure,
  rebuildable analysis-cache report with a composite-hash store key), `DisplayGraphBuilder.cs` (confirming
  exactly how `DisplayNode.X`/`.Y` are normalized, including the `minX == maxX` degenerate case used by this
  step's own hand-built crossing-geometry fixture) — confirming the exact current shape of every Step
  059-064 contract and the "string constants, not an enum," "atomic same-directory-temp-file write," and
  "validate before any load/write is attempted" conventions before choosing the same shapes for this step's
  own new types.
- Searched the full repository for any prior "Step 065"/"STEP-065" reference, or any existing
  `IRouteValidator`/`RouteValidationReport`/`LockedNodeSet`/`RouteIssue` type — found only `PHASE-09.md`
  itself (the numbered-step text; Section 3 does not propose concrete signatures for this step, unlike Steps
  059/064), confirming this is genuinely new work with no prior proposed API to reconcile against.
- Searched the repository for any existing "locked prop"/"effect recipe compatibility" concept outside this
  phase: found `XlightsAI.Studio.Xlights.EffectInstance.Locked` (Phase 03's own per-*effect* lock bit, not a
  per-*prop* one) and `XlightsAI.Studio.Editing.EffectLockPolicy`/`SetEffectLocked` (the Phase 03 Editing
  project's own effect-lock UI/persistence, a sibling project `XlightsAI.Studio.Display` does not and should
  not depend on — Phase 09 Section 1's own cross-phase ownership table assigns "effect locks" to Phase 03,
  not Phase 09). Confirmed this step's own "locked/excluded prop" concept (Section 2's own text, a
  *geometry-layer* concern: "a user has explicitly excluded a prop from route targeting") is a distinct,
  phase-09-owned concept from Phase 03's per-effect lock bit, and deliberately does not reuse or reference
  `EffectInstance.Locked`/`IEffectLockPolicy` — recorded as a disclosed decision in §3 below.
- Checked `src/XlightsAI.Studio.Display/XlightsAI.Studio.Display.csproj`: unchanged from Step 064 (references
  `Contracts`, `Xlights`, `Melody`); this step needed no new project reference — everything it touches
  (`DisplayNode`, `SpatialPath`, `SpatialDisplayGraph`) already lives in this same project.
- Baseline taken by running the real, full solution test suite *before* any Step 065 code was written:
  `dotnet build XlightsAI.Studio.slnx --nologo` (clean, 0 warnings/errors) immediately before starting, and
  Step 064's own already-recorded closing count (784/784 passing, real-audio-device Shell tests excluded, see
  §6) reconfirmed as the pre-existing baseline.

## 2. Scope actually implemented

**No file from Step 059-064 was behaviorally changed.** This step adds new types to the existing
`src/XlightsAI.Studio.Display/` project, extends the existing `DisplayPathEditorViewModel` in
`src/XlightsAI.Studio.Shell/` with additive members only (no existing member's signature or behavior
changed), and adds two new test files. No new project reference was needed.

### New files in `src/XlightsAI.Studio.Display/`

- **`RouteIssueSeverity.cs`** — `Error` (blocks route usage — this step's own required acceptance text:
  "cannot silently produce mis-targeted XSQ effects") and `Warning` (informational, never blocking). String
  constants, the same convention `RoleTag`/`GestureBoundaryPolicy`/`PathTraversalKind` already established.
- **`RouteIssueKind.cs`** — the named issue kinds this step's own implementation guidance names verbatim:
  `MissingNode`, `LockedNode`, `IncompatibleRecipe`, `PathIntersection`, `PathOverlap`, plus `StaleRoute`
  (named for documentation completeness; the live check itself is implemented as a thrown
  `SpatialDisplayGraphInvalidException`, not a returned issue — see §3).
- **`RouteIssue.cs`** — one concrete problem found: `NodeId` (nullable `ModelId`), `Kind`, `Severity`,
  `Message`.
- **`RouteValidationReport.cs`** — this step's own required "Versioned, inspectable project output plus a
  reproducible test report," verbatim: `SourceLayoutHash`, `PathId`, `PathVersion`, `Issues`, `VersionId`,
  plus a computed `HasBlockingIssues` (`true` iff any `Issues` entry is `RouteIssueSeverity.Error`) — the
  single flag a real export/XSQ-generation action gates on, directly realizing "Invalid path cannot silently
  produce mis-targeted XSQ effects."
- **`LockedNodeSet.cs`** — the persisted, layout-scoped set of explicitly locked/excluded node ids (Section
  2: "locked/excluded props"), mirroring `RoleAssignmentSet`'s own `SourceLayoutHash`/`Version` shape (no
  history list — unlocking removes an id outright, there is no superseded-value concept for a simple
  membership set, unlike a role's own replaced value).
- **`ILockedNodeStore.cs`** / **`JsonLockedNodeStore.cs`** — persistence boundary, byte-for-byte the same
  atomic-write pattern (same-directory temp file + `File.Move(..., overwrite: true)`) and deterministic
  single-hash path convention `JsonRoleAssignmentStore` already established.
- **`ILockedNodeCatalog.cs`** / **`LockedNodeCatalog.cs`** — application-orchestration boundary:
  `GetLockedNodesAsync`/`LockAsync`/`UnlockAsync`. Rejects an unknown node id, locking an already-locked node,
  or unlocking a node that is not locked — all *before* any load/write — this step's own required
  failure/edge case, verbatim.
- **`EffectRecipeSupport.cs`** — the caller-declared (never invented — Section 2: "do not invent xLights
  effect parameters") policy of which recipe ids a given `DisplayNode.ModelKind` is known to support, plus a
  `static Unrestricted` default (empty — "no declared incompatibility is not evidence of one," the same
  "absence is not a claim" rationale `ResolutionSettings.SkipUnavailable`'s own remarks already establish for
  missing nodes).
- **`IRouteValidator.cs`** / **`RouteValidator.cs`** — the domain validator. For every distinct node id in
  `SpatialPath.OrderedModels`: missing from the display graph → `MissingNode` (Error); present but in
  `LockedNodeSet` → `LockedNode` (Error); present and a `recipeId` was supplied and that model kind declares
  a non-empty supported-recipe set that does not contain it → `IncompatibleRecipe` (Error). Then, over the
  path's own resolvable normalized preview geometry: two *different* node ids at the exact same position →
  `PathOverlap` (Warning); two non-adjacent ordered segments crossing in 2D (standard orientation-based
  segment-intersection test, closed-path wrap-around segment included, adjacent/shared-endpoint segment pairs
  skipped) → `PathIntersection` (Warning). Throws `SpatialDisplayGraphInvalidException` before any issue is
  produced for a structurally invalid input: an empty path, or a `LockedNodeSet` authored against a different
  layout hash than the supplied display graph.
- **`IRouteValidationReportStore.cs`** / **`JsonRouteValidationReportStore.cs`** — persistence boundary for
  `RouteValidationReport`, mirroring `JsonResolvedGesturePreviewStore`'s own atomic-write guarantee exactly,
  at a deterministic path keyed by a composite hash of every input that changes the validator's own output
  (layout hash, path id/version, recipe id).
- **`IRouteValidationCatalog.cs`** / **`RouteValidationCatalog.cs`** — the application-orchestration boundary
  that implements this step's own required title text, verbatim: "conflicting-write checks." Before running
  any geometry/policy check, looks for a path with the same `SpatialPath.Id` already attached to the supplied
  `SpatialDisplayGraph.Paths` (the live, currently-persisted ordering a caller attaches via
  `graph with { Paths = pathSet.Paths }` — the exact convention `ISpatialPathCatalog`'s own remarks already
  document); when one exists at a *different* `SpatialPath.Version`, the call is rejected outright, before a
  stale route can ever reach geometry validation or produce a cached report. Otherwise runs
  `IRouteValidator.Validate` (throwing before any cache write for a structurally invalid input) and persists
  the result.

### Extended file in `src/XlightsAI.Studio.Shell/`

- **`DisplayPathEditorViewModel.cs`** — extended with additive members only: `LockedNodeIds`,
  `LockedNodeSetVersion`, `RouteIssues`, `HasBlockingRouteIssues`, `RouteValidationVersionId` (bindable),
  `LoadLockedNodesAsync`, `LockNodeAsync`, `UnlockNodeAsync`, `ValidateRouteAsync` — follows the exact same
  "catch `SpatialDisplayGraphInvalidException`, set `ActionErrorMessage`, leave bindable state completely
  unchanged on failure" convention every prior mutating action in this view model already established.
  `OpenAsync` now also resets `_lockedNodes`/`_routeValidation` to `null` before loading, so opening a
  different layout never leaves a previously opened layout's locks or route report dangling.
  `ValidateRouteAsync` auto-loads an empty `LockedNodeSet` via the supplied `ILockedNodeCatalog` if
  `LoadLockedNodesAsync` has not yet been called (never an error — the same "no locks yet" default
  `LockedNodeSet.Empty` already establishes), and attaches the currently-loaded `_pathSet.Paths` onto the
  graph before calling the catalog (so the view model's own in-process "conflicting write" protection is
  exercised automatically whenever `LoadPathsAsync`/`CreatePathAsync`/`ReorderPathAsync`/`ReversePathAsync`
  have already been used in the same session). No existing member's signature or behavior changed;
  `Rows`/`NamedPaths`/`RoleAssignments`/`GesturePreview`/`SelectNode`/`CorrectSelectedNodePositionAsync`/
  `ReorderAsync`/`CreatePathAsync`/`ReorderPathAsync`/`ReversePathAsync`/`LoadRoleAssignmentsAsync`/
  `AssignRoleAsync`/`ClearRoleAsync`/`ResolveGesturePreviewAsync` are untouched.

### New test files

- **`tests/XlightsAI.Studio.Display.Tests/Step065AcceptanceTests.cs`** (9 tests) — real
  `ShowLayoutImporter`/`DisplayGraphBuilder`/`RouteValidator`/`LockedNodeCatalog`/`RouteValidationCatalog`/
  `JsonLockedNodeStore`/`JsonRouteValidationReportStore` (not mocks) against the real Step 059 fixture (five
  arches, `WorldPosX` 0,10,20,30,40) and a small hand-built four-node "bowtie" graph for deliberate geometric
  crossing/overlap cases (positions chosen directly, not derivable from the straight-line arch fixture):
  - **`Step065_Acceptance_InvalidPathNeverSilentlyProducesMisTargetedXsqEffects`** — the required acceptance
    test, verbatim. Proves, against the real arch graph: a clean path produces zero issues and is never
    blocked; a path referencing a node absent from the graph is reported `MissingNode`/`Error` and
    `HasBlockingIssues` is `true`; a path referencing a node present in a `LockedNodeSet` is reported
    `LockedNode`/`Error` and blocking; a path whose nodes' own model kind declares a non-matching supported
    recipe set is reported `IncompatibleRecipe`/`Error` for every affected node and blocking, while the exact
    same recipe declared supported produces zero incompatibility issues.
  - **`PathIntersectionAndOverlap_AreReportedAsWarnings_NeverBlocking`** — this step's own "path
    intersections and overlaps" vocabulary, verbatim, against the hand-built bowtie graph: a path whose
    segments cross is reported `PathIntersection`/`Warning` and never blocking; two different node ids at the
    exact same position are reported `PathOverlap`/`Warning` and never blocking; a path that simply revisits
    the same node id (an intentional out-and-back) is never itself flagged as an overlap.
  - **`Validate_InvalidInput_RejectsBeforeAnyIssueIsProduced_NegativePath`** — an empty path and a
    `LockedNodeSet` authored against a mismatched layout hash are both rejected before any `RouteIssue` is
    produced — this step's own required failure/edge case, verbatim: "Invalid input... must not corrupt the
    project."
  - **`LockedNodeCatalog_LockAndUnlock_PersistsAndRejectsInvalidInput`** — locks/unlocks persist and reload
    across a brand-new catalog/store instance over the same directory (restart); an unknown node id, an
    already-locked lock request, and an unlock of a non-locked node are each rejected before any write.
  - **`RouteValidationCatalog_ValidateAsync_PersistsAndReloadsAcrossRestart`** — the required "Versioned,
    inspectable project output," verbatim: a validated report is persisted and reloaded through a brand-new
    store instance over the same on-disk directory.
  - **`RouteValidationCatalog_ValidateAsync_ConflictingWrite_RejectsStalePathVersion_NegativePath`** — this
    step's own required title text, verbatim: validating an in-memory path copy at version 0 while a live
    path of the same id already exists at version 1 on the supplied graph is rejected outright before any
    cache file is written; validating the exact live version succeeds normally; a path never attached to the
    graph's own live `Paths` (not yet saved) has nothing to conflict with and is never rejected on this basis
    alone.
  - **`RouteValidationCatalog_ValidateAsync_InvalidInput_NeverWritesCacheFile_NegativePath`** — an invalid
    (empty) path is rejected before the cache directory is even created.
  - **`RouteValidationCatalog_ValidateAsync_Cancelled_DoesNotPersist_NegativePath`** — a pre-cancelled token
    propagates `OperationCanceledException` and never writes a cache file — this step's own required
    failure/edge case, verbatim: "cancellation... must not corrupt the project."
  - **`Store_LoadAsync_CorruptFile_ThrowsCorruptException_NegativePath`** — same convention as every sibling
    store test in this phase: a corrupt on-disk file (for both the report store and the locked-node store)
    surfaces as a typed exception, never silently accepted or crashing with an unhandled deserialization
    error.
- **`tests/XlightsAI.Studio.Shell.Tests/Step065DisplayPathEditorViewModelTests.cs`** (6 tests) — the view
  model slice against a real `RouteValidationCatalog`/`LockedNodeCatalog`/`JsonRouteValidationReportStore`/
  `JsonLockedNodeStore`/`RouteValidator` under a temporary directory: validating a clean three-node path
  populates `RouteIssues` empty and `HasBlockingRouteIssues` false (the required acceptance text, verbatim,
  at the bindable view-model layer); locking a node then validating reports a blocking `LockedNode` issue,
  and unlocking it again updates `LockedNodeIds`; re-opening a different layout clears previously-loaded
  locked nodes and route validation state (never a stale leftover); three negative-path tests (nothing open,
  unknown path id, unknown node id to lock) — each proving the relevant bindable state is left completely
  unchanged on failure.

## 3. Architecture deviations / decisions

- **Section 3 of `PHASE-09.md` proposes no concrete C# signatures for Step 065** (unlike Steps 059/064,
  which had a proposed `ISpatialGestureResolver`/`ResolvedGesture` etc. to reconcile against). Every type
  introduced in this step (`IRouteValidator`, `RouteValidationReport`, `LockedNodeSet`, `RouteIssue`,
  `EffectRecipeSupport`, `IRouteValidationCatalog`) is this step's own original design, built directly from
  Section 2's prose ("Route resolution should honor missing props, locked/excluded props and incompatible
  effect recipes; produce a warning and fallback rather than silent mutation") and the numbered-step's own
  required/implementation-guidance text, following the nearest existing sibling conventions
  (`RoleAssignmentSet`/`IRoleAssignmentCatalog` for the persisted locked-node set;
  `ResolvedGesturePreview`/`IGesturePreviewCatalog` for the persisted, pure-cache validation report) rather
  than inventing a new shape from nothing.
- **Interpretation of "missing/locked prop" severity, verbatim implementation guidance.** Section 2's own
  sentence groups all three fallback categories (missing, locked, incompatible recipe) under one "produce a
  warning and fallback rather than silent mutation" instruction, which could be read as all three being
  mere warnings. This step instead treats all three as `RouteIssueSeverity.Error` (blocking), while only the
  two purely geometric categories (`PathIntersection`/`PathOverlap`) are `Warning`. Rationale: this step's
  own *required acceptance* text is unconditional — "Invalid path **cannot silently** produce mis-targeted
  XSQ effects" — and a missing, locked, or declared-incompatible node is not a cosmetic concern but a literal
  wrong-or-impossible target; downgrading any of the three to non-blocking would make "cannot silently
  produce" false by construction (a caller could ignore a `Warning` and still generate a mis-targeted
  effect). A geometric self-crossing or coincident-position route, by contrast, is still a perfectly valid,
  correctly-targeted route — crossing lines are an artistic/visual property, not a wrong-target risk — so
  those two remain non-blocking, surfaced for the user's own judgment. Disclosed here as the one place this
  step's own prose required a genuine interpretive choice, the same kind of disclosure Step 062 recorded for
  "duplicate IDs," Step 063 for "provide conflict policy," and Step 064 for "sweep and reverse."
- **Interpretation of "path intersections and overlaps," verbatim implementation guidance.** Neither word is
  defined further in Section 3. "Overlap" is read as two *different* node ids occupying the exact same
  resolved normalized preview position (a layout/geometry quirk, not an intentional same-prop revisit); a
  path legitimately revisiting the same node id (e.g. an out-and-back route) is never itself flagged, since
  that is an ordinary, expected choreography shape, not an error condition. "Intersection" is read as two
  non-adjacent ordered segments of the route crossing each other in 2D normalized preview space, computed via
  the standard orientation-based segment-intersection test (collinear-overlap cases handled via the
  classical `onSegment` bounding-box check). Both checks only consider nodes this step can already resolve a
  position for — a node already reported `MissingNode` contributes no geometry, rather than crashing or
  guessing a position for it.
- **Not reusing `XlightsAI.Studio.Xlights.EffectInstance.Locked`/`XlightsAI.Studio.Editing.EffectLockPolicy`
  for "locked prop."** Those are Phase 03's own per-*effect-instance* lock bit (Section 1's cross-phase
  ownership table: "effect locks — Phase 03"), already serving a real, accepted contract for a different
  concept (a human protecting one placed effect from further automated edits). Section 2's "locked/excluded
  props" is a Phase-09-owned, geometry-layer concept: a user excluding an entire *prop* from future route
  targeting, independent of whether any effect has ever been placed on it. Reusing the Phase 03 type would
  also require `XlightsAI.Studio.Display` to depend on `XlightsAI.Studio.Editing` (which it does not and
  should not, per Section 1's cross-phase ownership boundaries), purely to read a flag that means something
  different. `LockedNodeSet` is therefore a new, independent, phase-09-owned persisted aggregate.
- **`EffectRecipeSupport` is a caller-supplied, non-persisted policy record, not a built-in recipe catalog.**
  Section 2 is explicit: "do not invent xLights effect parameters." This project owns no recipe registry of
  its own (`RecipeId`/`RecipeVersion` are Phase 02/03's own `EffectInstance` fields); a later phase's own
  recipe catalog is expected to project its own per-model-kind support data into this shape when calling
  `IRouteValidator.Validate`. The `Unrestricted` default (empty dictionary, every model kind implicitly
  compatible with every recipe) ensures a caller that has not yet wired in a real recipe registry never sees
  a false, invented incompatibility — "absence of declared support" is read as "no restriction declared,"
  never as "declared incompatible," the identical rationale `ResolutionSettings.SkipUnavailable`'s own
  remarks already apply to a node simply missing from the graph.
- **"Conflicting-write checks" (this step's own required title text, verbatim) is implemented as: a path
  attached to `SpatialDisplayGraph.Paths` at a version different from the one being validated is rejected.**
  No prior step in this phase ever modeled optimistic-concurrency/stale-version rejection (every existing
  catalog — `SpatialPathCatalog`/`RoleAssignmentCatalog`/`LockedNodeCatalog` itself — simply reloads current
  state and increments a version on each accepted edit; there is no existing "reject a write against a stale
  base version" precedent anywhere in the repository to follow instead). This step introduces the concept
  for the first time, deliberately scoped to the exact mechanism `ISpatialPathCatalog`'s own remarks already
  document (`graph with { Paths = pathSet.Paths }` as "the live, currently-persisted ordering"), so a caller
  who already has that live path set attached and tries to validate (and, downstream, generate XSQ from) a
  since-superseded in-memory copy is caught before any geometry check or cache write. A caller who never
  attaches a live path set (e.g. validating a brand-new path that has not yet been saved via
  `ISpatialPathCatalog`) has nothing to conflict with and is never rejected on this basis alone — the check
  detects a genuine supersession, not merely "this path object was not loaded from a catalog."
- **`RouteValidationReport` is a pure, versioned rebuildable analysis cache, not a `SpatialPathSet`/
  `LockedNodeSet`-style accepted document with conflict policy/history** — the same rationale Step 064
  recorded for `ResolvedGesturePreview` (Section 4: "Analysis caches may be rebuilt if their
  source/model/version hash changes"). Re-validating the identical `(layout, path id/version, recipe id)`
  combination is always safe and deterministic; there is no human-editable state to lose by overwriting a
  stale cache entry.
- **No real SCREEN 05 DISPLAY PATHS / SCREEN 12 CHOREOGRAPHY Avalonia/AXAML view exists** — unchanged,
  carried-forward disclosure from Step 059-064 (confirmed again via a full repository `Glob` for `*.axaml`:
  zero results). This step's own required "report incompatibilities before generating XSQ" text is proven at
  the bindable view-model layer (`DisplayPathEditorViewModel.RouteIssues`/`HasBlockingRouteIssues`) a real
  export/XSQ-generation button would gate on, the same precedent every prior step in this phase already
  established — not a pixel-rendering claim, and not a claim that an actual `IXsqExporter`/XSQ-writing call
  site has been wired to check `HasBlockingRouteIssues` (no such call site exists anywhere in this phase;
  Phase 09's own owned boundary per §1 of the spec is "geometry-aware gesture resolution," not XSQ writing
  itself, which remains Phase 02/03's `XsqExporter`/`ScopedXsqExporter` — out of this step's own narrow
  scope to modify).

## 4. Commands actually executed

All run via the PowerShell tool — the sandboxed Bash tool in this session again returned bare exit code 1
with no output for plain commands, the same pre-existing environment limitation recorded in every prior
step's own evidence; PowerShell was used for every `dotnet`/file-system command below without incident.

```
# Baseline, before any Step 065 code existed:
dotnet build XlightsAI.Studio.slnx --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).

# After implementing the new Display-project types:
dotnet build src\XlightsAI.Studio.Display\XlightsAI.Studio.Display.csproj --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).

# After extending DisplayPathEditorViewModel:
dotnet build src\XlightsAI.Studio.Shell\XlightsAI.Studio.Shell.csproj --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).

dotnet build tests\XlightsAI.Studio.Display.Tests\XlightsAI.Studio.Display.Tests.csproj --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).

dotnet test tests\XlightsAI.Studio.Display.Tests\XlightsAI.Studio.Display.Tests.csproj --nologo --filter "FullyQualifiedName~Step065|FullyQualifiedName~RouteValidation|FullyQualifiedName~LockedNodeCatalog|FullyQualifiedName~PathIntersectionAndOverlap|FullyQualifiedName~Validate_InvalidInput"
  # First run: 1 failure (ModelKind "Arch" assumed instead of the real fixture value "Arches") -- fixed in
  # the test file, re-run below.
  # Passed! - Failed: 0, Passed: 9, Skipped: 0, Total: 9, Duration: 308 ms

dotnet test tests\XlightsAI.Studio.Display.Tests\XlightsAI.Studio.Display.Tests.csproj --nologo
  # Passed! - Failed: 0, Passed: 56, Skipped: 0, Total: 56, Duration: 316 ms
  # (47 pre-existing Step 059-064 + 9 new Step 065)

dotnet build tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).

dotnet test tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj --nologo --filter "FullyQualifiedName~Step065"
  # Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 292 ms

dotnet format XlightsAI.Studio.slnx --verify-no-changes --include src/XlightsAI.Studio.Display tests/XlightsAI.Studio.Display.Tests src/XlightsAI.Studio.Shell tests/XlightsAI.Studio.Shell.Tests --no-restore
  # No output (clean) -- matches the repository's own CI formatting gate.

dotnet build XlightsAI.Studio.slnx --nologo       # full solution build, after all Step 065 changes
  # Build succeeded. 0 Warning(s). 0 Error(s).

dotnet test XlightsAI.Studio.slnx --nologo --filter "FullyQualifiedName!~Step025&FullyQualifiedName!~Step028&FullyQualifiedName!~Step029&FullyQualifiedName!~Step030&FullyQualifiedName!~Step042"  # full regression, real-audio-device tests excluded (see §6)
dotnet test tests\XlightsAI.Studio.Stems.Tests\XlightsAI.Studio.Stems.Tests.csproj --nologo --filter "FullyQualifiedName~Step042"
  # Passed! - Failed: 0, Passed: 8, Skipped: 0, Total: 8, Duration: 382 ms
  # (the blunt numeric filter above also excludes Stems.Tests' own unrelated, legitimate
  # Step042AcceptanceTests.cs -- the same filter side effect disclosed in every prior step's own evidence;
  # reconfirmed here passing in isolation.)
```

### Targeted test results (Step 065's own 15 new tests, real run)

```
Step065AcceptanceTests (Display, 9 new):
  Step065_Acceptance_InvalidPathNeverSilentlyProducesMisTargetedXsqEffects                   Passed
  PathIntersectionAndOverlap_AreReportedAsWarnings_NeverBlocking                              Passed
  Validate_InvalidInput_RejectsBeforeAnyIssueIsProduced_NegativePath                          Passed
  LockedNodeCatalog_LockAndUnlock_PersistsAndRejectsInvalidInput                              Passed
  RouteValidationCatalog_ValidateAsync_PersistsAndReloadsAcrossRestart                        Passed
  RouteValidationCatalog_ValidateAsync_ConflictingWrite_RejectsStalePathVersion_NegativePath  Passed
  RouteValidationCatalog_ValidateAsync_InvalidInput_NeverWritesCacheFile_NegativePath         Passed
  RouteValidationCatalog_ValidateAsync_Cancelled_DoesNotPersist_NegativePath                  Passed
  Store_LoadAsync_CorruptFile_ThrowsCorruptException_NegativePath                             Passed
Display.Tests total: 56/56 passed (47 Step 059-064 unmodified + 9 new Step 065), 0 failed, 0 skipped.

Step065DisplayPathEditorViewModelTests (Shell, 6 new):
  ValidateRouteAsync_CleanPath_PopulatesEmptyRouteIssues_NotBlocking                          Passed
  LockNodeAsync_ThenValidateRouteAsync_ReportsBlockingLockedNodeIssue                         Passed
  OpenAsync_ReopeningADifferentLayout_ClearsPreviousRouteValidationAndLockedNodes             Passed
  ValidateRouteAsync_NothingOpen_SetsActionErrorAndLeavesEmpty_NegativePath                   Passed
  ValidateRouteAsync_UnknownPathId_RejectsWithoutPartialMutation_NegativePath                 Passed
  LockNodeAsync_UnknownNode_RejectsWithoutPartialMutation_NegativePath                        Passed
Shell.Tests (non-real-audio-device subset, see §6): 129/129 passed (123 pre-existing unmodified + 6 new Step 065), 0 failed, 0 skipped.
```

### Full regression (final, after all Step 065 implementation and tests, real-audio-device tests excluded — see §6)

```
Passed! - XlightsAI.Studio.Xlights.Tests.dll        74/74
Passed! - XlightsAI.Studio.Editing.Tests.dll        83/83
Passed! - XlightsAI.Studio.Display.Tests.dll        56/56   (47 Step 059-064 + 9 new Step 065)
Passed! - XlightsAI.Studio.Audio.Tests.dll          23/23
Passed! - XlightsAI.Studio.Melody.Tests.dll        117/117
Passed! - XlightsAI.Studio.Structure.Tests.dll      78/78
Passed! - XlightsAI.Studio.Projects.Tests.dll       25/25
Passed! - XlightsAI.Studio.Shell.Tests.dll         129/129  (123 pre-existing, real-audio-device tests excluded + 6 new Step 065)
Passed! - XlightsAI.Studio.Rhythm.Tests.dll         82/82
Passed! - XlightsAI.Studio.Reference.Tests.dll      31/31
Passed! - XlightsAI.Studio.Stems.Tests.dll         101/101  (93 reported by the filtered run above + 8 Step042AcceptanceTests the same blunt filter also excluded, separately reconfirmed passing)
```

**799 total passing across the 11 reported assemblies (only genuinely real-audio-device-dependent Shell
tests excluded, see §6), 0 failed, 0 skipped.** Every pre-existing non-real-audio-device test still passes
unmodified; the only additions are this step's own 9 new tests in `XlightsAI.Studio.Display.Tests`
(47 + 9 = 56) and 6 new tests in `XlightsAI.Studio.Shell.Tests` (123 + 6 = 129).

## 5. Persisted-output inspection / observable demonstration for this step

The real, unfiltered `Step065_Acceptance_InvalidPathNeverSilentlyProducesMisTargetedXsqEffects` test run
above *is* the observable demonstration this step's own required "User-visible outcome" asks for — it
validates a real `SpatialPath` against a real `SpatialDisplayGraph` built from the real imported fixture
layout (not a mock of either) and asserts on the actual returned `RouteValidationReport`. Reproduced here
directly (recomputed by hand against the committed, real validator logic, matching the test's own assertions
byte-for-byte):

```
Fixture: tests/XlightsAI.Studio.Display.Tests/Fixtures/sample-show-spatial-display/xlights_rgbeffects.xml
  Arch-01..05 at WorldPosX 0,10,20,30,40 (WorldPosY 0), ModelKind "Arches".

Clean path [Arch-01..05]:                       0 issues,  HasBlockingIssues = false.
Missing-node path [Arch-01, Ghost]:             1 issue  (Ghost, MissingNode, Error),     HasBlockingIssues = true.
Locked-node path [Arch-01..05], Arch-03 locked:  1 issue  (Arch-03, LockedNode, Error),    HasBlockingIssues = true.
Recipe path [Arch-01..05], recipeId "Spin",
  "Arches" declares only {"Twinkle"}:            5 issues (one IncompatibleRecipe/Error per arch), HasBlockingIssues = true.
Same path, recipeId "Twinkle" (declared):        0 IncompatibleRecipe issues,              HasBlockingIssues = false.

Bowtie graph A(0,0) B(1,1) C(1,0) D(0,1), path [A,B,C,D]:
  Segment A-B and segment C-D cross            -> 1 PathIntersection/Warning, HasBlockingIssues = false.
Bowtie graph + E at A's own position (0,0), path [A,E]:
  A and E coincide                             -> 1 PathOverlap/Warning,      HasBlockingIssues = false.
Path [A,B,A] (out-and-back, same graph, no coincident duplicate):
  0 PathOverlap issues (same-node revisit is never itself flagged).
```

This is the exact, real issue/severity sequence `Step065_Acceptance_InvalidPathNeverSilentlyProducesMisTargetedXsqEffects`
and `PathIntersectionAndOverlap_AreReportedAsWarnings_NeverBlocking` assert and the targeted test run above
confirms Passed.

## 6. Fixtures / input hashes

- `tests/XlightsAI.Studio.Display.Tests/Fixtures/sample-show-spatial-display/xlights_rgbeffects.xml` — the
  same Step 059-064 golden fixture, reused unmodified (no new fixture file needed for this step). Its
  five-arch layout is sufficient to exercise missing/locked/incompatible-recipe checks deterministically
  (model kind `"Arches"`, confirmed directly from the fixture's own `DisplayAs="Arches"` attribute — the
  first test run incorrectly assumed `"Arch"` and failed with a wrong expected-issue count; corrected in the
  committed test file, see §4).
- A small, hand-built four-node graph (`A(0,0)`, `B(1,1)`, `C(1,0)`, `D(0,1)`, all `ModelKind "Arch"`) is
  constructed directly in-test (not via the importer) for the geometric intersection/overlap cases, since the
  straight-line, single-row arch fixture cannot itself produce a crossing route. A fifth node `E` at `A`'s
  own exact position is added in-test for the coincident-overlap case.
- **Real-audio-device test exclusion, disclosed (unchanged from Step 064's own §6, reconfirmed here):**
  `dotnet test` run without a filter reproducibly fails tests in
  `Step025ShellWiringTests`/`Step028ShellWiringTests`/`Step029ShellWiringTests`/`Step030ShellWiringTests`/
  `Step042ShellWiringTests`, all five constructing a real `WaveOutAudioTransport` (the real Windows winmm
  audio device) unavailable in this session's own sandboxed execution environment — a pre-existing,
  environment-caused condition predating Phase 09 entirely, not a regression introduced by this step. **No
  file this step touched is on the call path of any failing test** (confirmed: none of those five classes
  reference `Display`/`LockedNodeCatalog`/`RouteValidator`/`RouteValidationCatalog`/
  `DisplayPathEditorViewModel` in any way). Excluding exactly these five classes (`--filter` shown in §4)
  reproduces a clean 0-failure run across every other assembly, shown in §4's "Full regression" table. The
  filter expression's own incidental exclusion of `XlightsAI.Studio.Stems.Tests`' own unrelated, legitimate
  `Step042AcceptanceTests.cs` is separately reconfirmed passing in isolation (§4), the same disclosed filter
  side effect every prior step in this phase has already recorded.

## 7. Unresolved issues / known gaps

- **No real SCREEN 05 DISPLAY PATHS / SCREEN 12 CHOREOGRAPHY Avalonia/AXAML view exists** — unchanged from
  Step 059-064's own disclosed gap; this step's own required "report incompatibilities before generating
  XSQ" text is proven directly by both the automated tests and the hand-verified sequence in §5 at the
  bindable view-model layer a real SCREEN 05/12 export action would bind to/gate on, not a pixel-rendering
  claim. Every other screen in this repository has the identical gap (confirmed via a full repository `Glob`
  for `*.axaml` — zero results).
- **No real `IXsqExporter`/`ScopedXsqExporter` call site in this repository actually checks
  `RouteValidationReport.HasBlockingIssues` before writing XSQ.** This step's own owned boundary (Section 1:
  "geometry-aware gesture resolution") is producing the report and the bindable flag a caller must gate on —
  not modifying Phase 02/03's own already-accepted XSQ-writing code paths, which this phase explicitly does
  not touch ("Out of scope: No edits to xLights controller channel definitions"). Disclosed here as a true
  gap a later phase's own XSQ-generation call site is expected to close by consulting
  `IRouteValidationCatalog`/`HasBlockingIssues` before writing, not reopened or partially implemented here.
- **Real-audio-device test failures in `Step025`/`Step028`/`Step029`/`Step030`/`Step042ShellWiringTests`** —
  disclosed in full in §6 as a pre-existing, environment-caused condition unrelated to this step's own scope
  or files. Not re-investigated or fixed here; unchanged, carried forward from every prior step's own
  identical disclosure.
- **`LockedNodeSet`/`RouteValidationReport` are independent of Phase 03's `EffectInstance.Locked`/
  `IEffectLockPolicy`** — disclosed in §3 as a deliberate, scope-respecting design boundary; not reopened
  here. A future phase wiring an actual XSQ-generation path against this step's own validator is free to
  additionally consult `EffectInstance.Locked` for its own, separate per-effect concern.
- **`RoleAssignment.ScopeId` remains an opaque `Guid`, not wired to any real Phase 08 phrase identity** —
  unchanged, carried forward from Step 063's own disclosure; not reopened here.
- **Per the user's own standing waiver** (`memory: feedback_skip_xlights_human_verification`), the
  real-xLights render/desktop-interaction check is not treated as a blocker for this step's own scope — this
  step's own required acceptance text does not itself name an xLights-specific manual rendering action (it is
  a domain/persistence/validation-report claim over the real, already-accepted Phase 02/Step 059-064
  pipeline), proven directly in the automated tests and the hand-verified real-logic trace above.
- **Carried-forward, disclosed phase-level/dependency-level gaps are unchanged from Step 059-064's own
  closing evidence** (not reopened or re-resolved here): Phase 06's own stem-separation gate remains
  BLOCKED; Phase 07's and Phase 08's own full-phase gates have not been separately run/recorded.

## 8. PASS / BLOCKED

**Status: PASS** (for this step's own narrow, disclosed scope — route/recipe/lock validation and
geometry-overlap reporting, plus a conflicting-write check at the validation-catalog boundary, independent of
any actual XSQ-writing call site — not a claim that any carried-forward phase-level gap listed in §7 is
resolved).

- "Invalid path cannot silently produce mis-targeted XSQ effects" (required acceptance, verbatim) — proven by
  `Step065_Acceptance_InvalidPathNeverSilentlyProducesMisTargetedXsqEffects` (missing/locked/incompatible-
  recipe nodes are each reported as a blocking `Error` issue, never silently dropped or targeted) and
  `RouteValidationCatalog_ValidateAsync_ConflictingWrite_RejectsStalePathVersion_NegativePath` (a stale route
  copy is rejected outright before it can ever reach geometry validation or a cache write) — independently
  confirmed by the hand-verified real-logic trace in §5.
- "Validate missing/locked prop, effect support, path intersections and overlaps; report incompatibilities
  before generating XSQ" (implementation guidance, verbatim) — proven by the same acceptance test above plus
  `PathIntersectionAndOverlap_AreReportedAsWarnings_NeverBlocking` (geometric crossing/overlap detection,
  correctly non-blocking); `RouteValidationReport` is produced and persisted entirely independent of any XSQ
  writer/exporter type, confirming "report... before generating XSQ" rather than during or after it.
- "Invalid input, cancellation or stale persisted state must not corrupt the project" (failure/edge case,
  verbatim) — proven by `Validate_InvalidInput_RejectsBeforeAnyIssueIsProduced_NegativePath`,
  `LockedNodeCatalog_LockAndUnlock_PersistsAndRejectsInvalidInput`'s own three rejection assertions,
  `RouteValidationCatalog_ValidateAsync_InvalidInput_NeverWritesCacheFile_NegativePath`,
  `RouteValidationCatalog_ValidateAsync_Cancelled_DoesNotPersist_NegativePath`, and
  `Store_LoadAsync_CorruptFile_ThrowsCorruptException_NegativePath`.
- "Versioned, inspectable project output plus a reproducible test report" (User-visible outcome, verbatim) —
  proven by `RouteValidationReport`'s own `PathVersion`/`VersionId` provenance (asserted directly in the
  acceptance test), `RouteValidationCatalog_ValidateAsync_PersistsAndReloadsAcrossRestart`, and
  `ValidateRouteAsync_CleanPath_PopulatesEmptyRouteIssues_NotBlocking` at the bindable view-model layer a
  real SCREEN 05/12 UI would read through.
- Full regression (real-audio-device tests excluded, disclosed in full in §6): **799/799 passing**, 0
  failed, 0 skipped; every pre-existing non-real-audio-device test from Steps 001-064 still passes
  unmodified, plus this step's own 9 new Display tests + 6 new Shell tests. `dotnet format --verify-no-changes`
  clean.
- **Step 059's through Step 064's own already-accepted contracts and tests are completely untouched in
  behavior** — no file any of those steps produced was edited; the only Shell-project change is the additive
  extension of `DisplayPathEditorViewModel` (no existing member's signature or behavior changed); no project
  reference changed.
- **All seven numbered steps of Phase 09 (059-065) now have independently recorded PASS evidence** — this is
  the final numbered step of this phase.
- **Carried-forward, disclosed phase-level/dependency-level gaps (not this step's own blocker):** Phase 06's
  own gate remains BLOCKED; Phase 07's and Phase 08's own full-phase gates have not been separately
  run/recorded; real-audio-device Shell tests are environment-blocked (§6); no real XSQ-generation call site
  in this repository yet consults `HasBlockingIssues` (§7 — a future phase's own integration point, not
  this step's own scope to add).

Per this step's own closing instruction ("do not execute the next step until this step is proven") and this
being the last numbered step of Phase 09, the phase gate demonstration below is the remaining item before
Phase 09 can be marked PASS/BLOCKED as a whole and Phase 10 begun.

## 9. Phase 09 gate demonstration status (informational — not claimed complete by this step alone)

Section 7's own "Phase gate demonstration — exact user workflow" text: "Load a layout, inspect suggested arch
and roofline paths, reverse the arch route manually, preview C-D-E-G-E-D-C across both and prove
geometry-aware effects target correct xLights props." This workflow spans Steps 059-064 (layout/path
suggestion, reversal, melodic-contour resolution) plus this step's own validation layer, and is exercised
piecewise by each step's own acceptance tests (Step 059/060's suggestion tests, Step 062's reversal tests,
Step 064's own `C4,D4,E4,G4,E4,D4,C4` -> `Arch-01,02,03,05,03,02,01` worked example, this step's own
missing/locked/incompatible/geometry validation) rather than as one single combined end-to-end test file.
Per the user's own standing waiver (`memory: feedback_skip_xlights_human_verification`), a live desktop/real-
xLights run of this exact combined workflow is not treated as a blocker; a full Phase 09 gate report
synthesizing all seven steps' own already-recorded evidence has not been separately authored as its own
document and is called out here as the remaining phase-level (not step-level) item.
