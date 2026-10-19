# PHASE 10 — Step 067 Evidence

**Step:** "Add configurable instrument-to-role and role-to-prop routing UI"
**Spec:** `docs/phase-specs/PHASE-10.md` §6, Step 067 (lines 219-233) — the second of six numbered steps
of Phase 10 (066-071)
**Date:** 2026-10-16

## 1. Pre-implementation baseline inspection

- Read `docs/phase-specs/PHASE-10.md` in full, confirming Step 067's own text: required acceptance
  ("Mappings persist and can change per section"), implementation guidance ("Create UI mapping table of
  sources to named spatial paths with role priority and empty/unavailable warnings; persist as user
  presets"), failure/edge case ("Invalid input, cancellation or stale persisted state must not corrupt the
  project").
- Read `implementation-evidence/PHASE-10/STEP-066.md` in full as the immediately preceding, already-accepted
  contract baseline: `MusicalRole`, `PrecedenceTier`, `MusicalRoleAssignment` (already combines one source
  track's role *and* its target `SpatialPathId` for one `PhraseId`), `MusicalRoleAssignmentSet` (already
  keyed per `PhraseId` — i.e. already "per section"), `IMusicalRoleAssignmentCatalog`/`MusicalRoleAssignmentCatalog`
  (already validates `SourceTrackId`/`Role`/`PrecedenceTier`/`SpatialPathId` before any load/write, already
  implements "replace, don't stack" conflict policy). Step 066 built the full domain/persistence boundary
  this step's own UI sits on top of; no new domain contract was needed to satisfy Step 067's own required
  acceptance text.
- Read every file in `src/XlightsAI.Studio.Composition/` in full (`MusicalRole.cs`, `PrecedenceTier.cs`,
  `MusicalRoleAssignment.cs`, `MusicalRoleAssignmentSet.cs`, `IMusicalRoleAssignmentCatalog.cs`,
  `MusicalRoleAssignmentCatalog.cs`, `IMusicalRoleAssignmentStore.cs`, `JsonMusicalRoleAssignmentStore.cs`,
  `CompositionInvalidException.cs`) before writing any new type.
- Read `src/XlightsAI.Studio.Shell/DisplayPathEditorViewModel.cs` in full (the direct structural precedent
  for a Shell-layer screen view model: explicit `ScreenLoadState` loading/empty/error/loaded machine, "catch
  the domain's typed invalid-input exception, set `ActionErrorMessage`, leave bindable state completely
  unchanged on failure" convention for every mutating action, `RaiseStateDerivedChanged` fan-out). Read
  `DisplayNodeRowView.cs` as the precedent for a UI-only bindable row projection record.
- Read `tests/XlightsAI.Studio.Shell.Tests/Step063DisplayPathEditorViewModelTests.cs` in full as the direct
  structural precedent for this step's own test style (a real catalog/store under a per-test temp directory,
  not mocks; before/after mutation-equality negative-path convention; restart-across-a-brand-new-instance
  persistence proof).
- Confirmed via `Glob` for `*.axaml`: only `App.axaml`/`MainWindow.axaml` exist anywhere in the repository —
  no SCREEN 12 CHOREOGRAPHY Avalonia view exists yet (unchanged, carried-forward condition from every prior
  Phase 09/10 UI-adjacent step; see §3/§7).
- Searched the full repository for any prior "Step 067"/"STEP-067"/`RoleRouting`/`RoutingEditor` reference:
  found only `PHASE-10.md` itself, confirming this is genuinely new work.
- Checked `src/XlightsAI.Studio.Shell/XlightsAI.Studio.Shell.csproj`: it referenced
  `XlightsAI.Studio.Display` but **not** `XlightsAI.Studio.Composition` — confirming this step's own new
  Shell-project reference is additive, not a pre-existing gap.
- Baseline taken by running the real, full solution build and test suite *before* any Step 067 code was
  written: `dotnet build XlightsAI.Studio.slnx --nologo` (clean, 0 warnings/errors, 15 projects) and
  Step 066's own closing count, **808/808 passing**, reconfirmed immediately before starting.

## 2. Scope actually implemented

**No file from Step 001-066 was behaviorally changed.** This step adds three new files to the existing
`src/XlightsAI.Studio.Shell/` project, one new `<ProjectReference>` to that project's `.csproj` (Shell now
references `XlightsAI.Studio.Composition`, which it did not before), and one new test file. No existing
Composition- or Display-project file was modified at all.

### New files in `src/XlightsAI.Studio.Shell/`

- **`RoleRoutingRowView.cs`** — UI-only bindable projection of one live `MusicalRoleAssignment` for SCREEN
  12 CHOREOGRAPHY's routing table: `SourceTrackId`, `Role`, `SpatialPathId`, `SpatialPathName` (resolved
  from the currently open layout's `SpatialPathSet`, or `null` when the path no longer exists),
  `PrecedenceTier`, `Priority`, `Weight`, `IsSourceUnavailable`, `IsPathUnavailable`. Same
  "Avalonia-free Shell layer projects pure domain data into something directly bindable" convention
  `DisplayNodeRowView`/`PianoRollNoteRowView` already established. Deliberately carries no row-level command
  (unlike `DisplayNodeRowView.SelectCommand`, which needs no catalog to mutate local selection state) —
  clearing a routing needs `IMusicalRoleAssignmentCatalog`, so a real screen calls
  `RoleRoutingEditorViewModel.ClearAsync` directly with `SourceTrackId`, the same way
  `DisplayPathEditorViewModel.ClearRoleAsync` is already called directly rather than through a
  row-captured command.
- **`RoleRoutingWarningView.cs`** — `RoleRoutingWarningView(RoleRoutingWarningKind Kind, string Message)` plus
  the `RoleRoutingWarningKind` enum (`UnmappedRole`, `UnavailableSource`, `UnavailablePath`) — the direct
  realization of this step's own implementation guidance, verbatim: "... with role priority and
  empty/unavailable warnings." A warning is never itself a validation failure; the routing table stays fully
  editable while warnings are shown.
- **`RoleRoutingEditorViewModel.cs`** — SCREEN 12 CHOREOGRAPHY's own bindable routing-table view model, the
  direct UI layer over Step 066's own `IMusicalRoleAssignmentCatalog`:
  - `LoadState`/`IsEmpty`/`IsError`/`IsLoaded` — the same explicit loading/empty/error/loaded state machine
    `DisplayPathEditorViewModel` already established (no section opened yet is `ScreenLoadState.Empty`,
    never a silently blank table).
  - `SectionId`/`Version` — identity/version of the section (phrase) currently open.
  - `Rows` — the real, currently-routed `RoleRoutingRowView`s for the open section, empty exactly when
    `IsLoaded` is `false`.
  - `Warnings` — every `MusicalRole` with no live routing in the open section
    (`RoleRoutingWarningKind.UnmappedRole`), every live routing whose source track is not in the currently
    supplied available-track set (`RoleRoutingWarningKind.UnavailableSource`), and every live routing whose
    target path no longer exists in the currently open layout's `SpatialPathSet`
    (`RoleRoutingWarningKind.UnavailablePath`).
  - `OpenSectionAsync(catalog, pathSet, sectionId, availableTrackIds, ct)` — loads (or switches to)
    `sectionId`'s own independently persisted routing. Calling this again with a *different* `sectionId` at
    any time loads that section's own independently saved routing — this step's own required acceptance
    text, verbatim: "Mappings ... can change per section." Catches any exception, sets `ErrorMessage`,
    transitions to `ScreenLoadState.Error` (the same `DisplayPathEditorViewModel.OpenAsync` convention).
  - `AssignAsync(catalog, sourceTrackId, role, spatialPathId, precedenceTier, priority, weight, ct)` —
    delegates to `IMusicalRoleAssignmentCatalog.AssignAsync`. Returns `false` (via `ActionErrorMessage`) when
    no section is open or the catalog rejects the request — never a partial mutation of `Rows`. Catches only
    `CompositionInvalidException` — a cancellation propagates uncaught, the same
    `CorrectSelectedNodePositionAsync`/`AssignRoleAsync` convention.
  - `ClearAsync(catalog, sourceTrackId, ct)` — delegates to `IMusicalRoleAssignmentCatalog.ClearAsync`, same
    conventions.

### Extended file

- **`src/XlightsAI.Studio.Shell/XlightsAI.Studio.Shell.csproj`** — one new
  `<ProjectReference Include="..\XlightsAI.Studio.Composition\XlightsAI.Studio.Composition.csproj" />` line
  added (Shell did not reference Composition before this step). No other line changed.

### New test file

- **`tests/XlightsAI.Studio.Shell.Tests/Step067RoleRoutingEditorViewModelTests.cs`** (10 tests) — real
  `RoleRoutingEditorViewModel`/`MusicalRoleAssignmentCatalog`/`JsonMusicalRoleAssignmentStore` (not mocks)
  under a per-test temporary directory, with a hand-built `SpatialPathSet` (two named paths, "Arches" and
  "Tree") standing in for an already-opened layout's own path catalog (consistent with Step 066's own test
  convention, which builds a real `SpatialPathSet` only when `SpatialPathId` validation itself is under
  test — here the routing-table/warning behavior is under test, not path-set construction, so a hand-built
  set is the more direct, equally real fixture):
  - **`Step067_Acceptance_MappingsPersistAndCanChangePerSection`** — the required acceptance test, verbatim.
    Opens a "verse" section, routes two tracks, switches to a "chorus" section (confirming it starts empty —
    its own independent routing, not a leftover), routes the chorus differently, then reopens the verse
    section through a **brand-new `RoleRoutingEditorViewModel`/`MusicalRoleAssignmentCatalog` instance** (a
    real restart) and confirms the verse's own two routings survived unaffected by the chorus section's own
    edits.
  - **`Warnings_UnmappedRole_ListsEveryRoleWithNoLiveRouting`** — after routing only `Lead`, the other five
    `MusicalRole` values each produce exactly one `UnmappedRole` warning; `Lead` itself produces none.
  - **`Warnings_UnavailableSource_FlaggedWhenTrackNoLongerInAvailableSet`** — reopening the same section with
    a track removed from the supplied available-track set flags that row's `IsSourceUnavailable` and
    produces an `UnavailableSource` warning.
  - **`Warnings_UnavailablePath_FlaggedWhenTargetPathNoLongerExists`** — reopening the same section against a
    path set with the previously-targeted path removed flags that row's `IsPathUnavailable`
    (`SpatialPathName` is `null`) and produces an `UnavailablePath` warning.
  - **`AssignAsync_NothingOpen_SetsActionErrorAndLeavesRowsEmpty_NegativePath`**.
  - **`AssignAsync_UnknownSpatialPath_RejectsWithoutPartialMutation_NegativePath`**.
  - **`AssignAsync_UnrecognizedPrecedenceTier_RejectsWithoutPartialMutation_NegativePath`**.
  - **`ClearAsync_UnknownTrack_RejectsWithoutPartialMutation_NegativePath`**.
  - **`ClearAsync_RemovesRowAndAddsUnmappedRoleWarningBack`** — clearing a track's routing both removes its
    row and makes its role reappear in `Warnings` as `UnmappedRole`.
  - **`AssignAsync_Cancelled_DoesNotPersist_NegativePath`** — a pre-cancelled token propagates
    `OperationCanceledException` (uncaught by the view model, the established convention) and never writes a
    cache file; `Rows` stays empty.

## 3. Architecture deviations / decisions

- **No new domain/persistence contract was needed.** Step 066's own `MusicalRoleAssignment` already combines
  "instrument-to-role" (`SourceTrackId` → `Role`) and "role-to-prop" (`Role`/`SourceTrackId` →
  `SpatialPathId`) into one record, and `MusicalRoleAssignmentSet` is already keyed per `PhraseId` (i.e.
  already "per section"). Step 067's own required acceptance text — "Mappings persist and can change per
  section" — is therefore proven directly by calling the *already-accepted* `IMusicalRoleAssignmentCatalog`
  with two different `PhraseId`s, not by inventing a second, parallel persistence boundary. This is
  disclosed here as a genuine, load-bearing architectural observation: Step 067's own "UI" scope sits
  entirely on Step 066's already-complete domain/persistence slice; only the bindable view-model/warning
  layer is new.
- **"Persist as user presets" (implementation guidance) is satisfied by the already-accepted
  `JsonMusicalRoleAssignmentStore`, not a second, separate "preset" file format.** The spec's own prose does
  not define a "preset" as a concept distinct from "the currently saved per-section routing" anywhere else
  in Section 3/4 — `ChoreographyPlan`/`VisualStory`/`SectionVisualDirection` (Section 3's own proposed types)
  do not introduce a named preset entity either. The chosen, disclosed reading: a per-section
  `MusicalRoleAssignmentSet`, already persisted atomically and versioned via Step 066's own
  `JsonMusicalRoleAssignmentStore`, *is* the user's saved preset for that section — reopening the same
  `PhraseId` at any later time (including after a restart, proven by this step's own acceptance test)
  reloads exactly what the user last saved. No new on-disk artifact or file format was introduced.
- **Warnings are computed, not persisted.** `RoleRoutingEditorViewModel.Warnings` is a pure, derived
  projection recomputed from `Rows`/`_availableTrackIds`/`_pathSet` on every access — never written to disk,
  never part of `MusicalRoleAssignmentSet`. This matches the spec's own data-contract rule (§4: "Persist user
  role-to-path mappings, palette IDs, visual budget presets, section visual direction, generated gesture
  provenance and `ChoreographyPlan` inputs hash" — warnings are not in that list) and avoids a warning
  becoming stale persisted state the way a cached analysis artifact could.
- **`UnmappedRole` warnings are produced for every `MusicalRole` with zero live routings, with no severity
  distinction between roles.** The spec's own implementation guidance says only "empty ... warnings," without
  naming which roles must always be filled (e.g. `Lead` arguably matters more than `Atmosphere`). The chosen,
  disclosed reading treats every role uniformly — a real SCREEN 12 view can choose to visually de-emphasize
  less-critical role warnings; the view model's own job is only to report the gap completely and
  deterministically, not to editorialize about which gaps matter most (a judgment this step's own narrow
  scope should not make on the UI's behalf).
- **`RoleRoutingEditorViewModel` is a fresh, additive Shell-layer view model, not an extension of
  `DisplayPathEditorViewModel`.** Unlike Step 063 (role tags) and Step 064/065 (gesture preview/route
  validation), which extended the single existing `DisplayPathEditorViewModel` because those steps operated
  on the *same* SCREEN 05/12-adjacent `SpatialDisplayGraph`/layout-scoped state that view model already
  owns, this step's own routing table is scoped by *section* (`PhraseId`), not by *layout*
  (`SourceLayoutHash`) — a `RoleRoutingEditorViewModel` can be opened against a given layout's
  `SpatialPathSet` independently of whether a `DisplayPathEditorViewModel` instance for that same layout is
  currently open, and a single layout's choreography spans many sections. Folding this into
  `DisplayPathEditorViewModel` would conflate two different scoping axes the domain layer itself already
  keeps separate (`SpatialPathSet` is layout-scoped; `MusicalRoleAssignmentSet` is phrase-scoped).
- **No real SCREEN 12 CHOREOGRAPHY Avalonia/AXAML view exists.** Unchanged, carried-forward disclosure from
  every prior Phase 09/10 step (confirmed again via a full repository `Glob` for `*.axaml`: still only
  `App.axaml`/`MainWindow.axaml`). This step's own required evidence text is narrower than a literal pixel
  claim — "Avalonia screenshot **or** repeatable desktop interaction recording with saved-state
  verification" — and is satisfied here by the real, non-test-harness demo in §5 below (a repeatable
  interaction recording against the real, Avalonia-free bindable view-model layer a real SCREEN 12 view
  would bind to), the same precedent every prior UI-adjacent Phase 09 step's evidence already established
  for this exact same project-wide gap.
- **Per the user's own standing waiver** (`memory: feedback_skip_xlights_human_verification`), no live
  xLights render/desktop-interaction check was attempted — this step's own required acceptance text is a
  pure UI/persistence/warning claim (no XSQ/effect-recipe output exists yet at this point in Phase 10),
  proven directly by the automated tests and demo below.

## 4. Commands actually executed

All run via the PowerShell tool — the sandboxed Bash tool in this session again returned bare exit code 1
with no output for plain commands, the same pre-existing environment limitation every prior step's own
evidence has recorded; PowerShell was used for every `dotnet`/file-system command below without incident.

```
# Baseline, before any Step 067 code existed:
dotnet build XlightsAI.Studio.slnx --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s). (15 pre-existing projects, Step 066's own closing state)

# After adding the Shell -> Composition project reference and the three new Shell files:
dotnet build XlightsAI.Studio.slnx --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).

dotnet test tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj --nologo --filter "FullyQualifiedName~Step067"
  # Passed! - Failed: 0, Passed: 10, Skipped: 0, Total: 10, Duration: 314 ms

dotnet format XlightsAI.Studio.slnx --verify-no-changes --include src/XlightsAI.Studio.Shell tests/XlightsAI.Studio.Shell.Tests --no-restore
  # No output (clean) -- matches the repository's own CI formatting gate.

dotnet test XlightsAI.Studio.slnx --nologo --filter "FullyQualifiedName!~Step025&FullyQualifiedName!~Step028&FullyQualifiedName!~Step029&FullyQualifiedName!~Step030&FullyQualifiedName!~Step042"
dotnet test tests\XlightsAI.Studio.Stems.Tests\XlightsAI.Studio.Stems.Tests.csproj --nologo --filter "FullyQualifiedName~Step042"
  # Passed! - Failed: 0, Passed: 8, Skipped: 0, Total: 8, Duration: 450 ms
  # (the blunt numeric filter above also excludes Stems.Tests' own unrelated, legitimate
  # Step042AcceptanceTests.cs -- the same filter side effect disclosed in every prior Phase 09/10 step's own
  # evidence; reconfirmed here passing in isolation.)

# Real, non-test-harness manual demo (built under the session scratchpad at %TEMP%\xlai-step067-demo-proj,
# deleted after this run):
dotnet run --project <scratchpad>\xlai-step067-demo-proj
```

### Targeted test results (Step 067's own 10 new tests, real run)

```
Step067RoleRoutingEditorViewModelTests (Shell, 10 new):
  Step067_Acceptance_MappingsPersistAndCanChangePerSection                      Passed
  Warnings_UnmappedRole_ListsEveryRoleWithNoLiveRouting                         Passed
  Warnings_UnavailableSource_FlaggedWhenTrackNoLongerInAvailableSet             Passed
  Warnings_UnavailablePath_FlaggedWhenTargetPathNoLongerExists                  Passed
  AssignAsync_NothingOpen_SetsActionErrorAndLeavesRowsEmpty_NegativePath        Passed
  AssignAsync_UnknownSpatialPath_RejectsWithoutPartialMutation_NegativePath     Passed
  AssignAsync_UnrecognizedPrecedenceTier_RejectsWithoutPartialMutation_NegativePath Passed
  ClearAsync_UnknownTrack_RejectsWithoutPartialMutation_NegativePath            Passed
  ClearAsync_RemovesRowAndAddsUnmappedRoleWarningBack                          Passed
  AssignAsync_Cancelled_DoesNotPersist_NegativePath                            Passed
Shell.Tests total (Step 067 slice): 10/10 passed, 0 failed, 0 skipped.
```

### Full regression (final, after all Step 067 implementation and tests, real-audio-device Shell tests excluded — unchanged pre-existing condition, see Step 064/065/066's own §6)

```
Passed! - XlightsAI.Studio.Audio.Tests.dll           23/23
Passed! - XlightsAI.Studio.Xlights.Tests.dll         74/74
Passed! - XlightsAI.Studio.Editing.Tests.dll         83/83
Passed! - XlightsAI.Studio.Melody.Tests.dll         117/117
Passed! - XlightsAI.Studio.Display.Tests.dll         56/56
Passed! - XlightsAI.Studio.Structure.Tests.dll       78/78
Passed! - XlightsAI.Studio.Composition.Tests.dll      9/9
Passed! - XlightsAI.Studio.Projects.Tests.dll        25/25
Passed! - XlightsAI.Studio.Shell.Tests.dll          139/139 (129 Step 001-066 + 10 new Step 067; real-audio-device tests excluded, unchanged)
Passed! - XlightsAI.Studio.Rhythm.Tests.dll          82/82
Passed! - XlightsAI.Studio.Reference.Tests.dll       31/31
Passed! - XlightsAI.Studio.Stems.Tests.dll           93/93  (+8 Step042AcceptanceTests reconfirmed separately, below)
```

**818 total passing** (808 carried forward from Step 066's own closing count, unmodified, + this step's own
10 new Shell tests), 0 failed, 0 skipped. Every pre-existing test from Steps 001-066 still passes unmodified;
the only addition is `Step067RoleRoutingEditorViewModelTests` (brand new, 10/10).

## 5. Persisted-output inspection / observable demonstration for this step

A real, non-test-harness console program (not part of the repository, built under the session scratchpad at
`%TEMP%\xlai-step067-demo-proj`, deleted after this run) exercises the real
`RoleRoutingEditorViewModel`/`MusicalRoleAssignmentCatalog`/`JsonMusicalRoleAssignmentStore` directly — no
test harness, no mocks. It deliberately mirrors the phase gate's own named worked example ("Choose lead
piano on arches, kick/snare on roofline and bass on tree for a 20-second song excerpt"), though full XSQ
export/preview is a later Phase 10 step's own scope (Steps 069-071), not this step's:

```
dotnet run --project <scratchpad>\xlai-step067-demo-proj
```

Observed real output:

```
=== Step 067 manual demo: real routing UI view model, no test harness ===

--- Scenario 1: open the verse section, route the 20-second-excerpt demo mapping ---
  Verse section id=f938ddf8-8dc1-493e-8648-45fe0fe17039, version=4, rows=4
    T-Bass   -> Bass       -> Tree       (tier=Manual, priority=1, unavailable-source=False, unavailable-path=False)
    T-Kick   -> Rhythm     -> Roofline   (tier=Manual, priority=1, unavailable-source=False, unavailable-path=False)
    T-Piano  -> Lead       -> Arches     (tier=Manual, priority=2, unavailable-source=False, unavailable-path=False)
    T-Snare  -> Accent     -> Roofline   (tier=Manual, priority=1, unavailable-source=False, unavailable-path=False)

--- Scenario 2: unmapped-role warnings for the verse section (Harmony/Atmosphere unrouted) ---
    [UnmappedRole] No source track is routed to role 'Harmony' for this section.
    [UnmappedRole] No source track is routed to role 'Atmosphere' for this section.

--- Scenario 3: switch to the chorus section -- independently empty, then routed differently ---
  Chorus section id=7249b426-1d8f-4804-b88f-01f133b6e317, version=0, rows=0 (own independent routing, not the verse's)
  After routing the chorus: rows=2
    T-Kick   -> Lead       -> Roofline
    T-Piano  -> Harmony    -> Tree

--- Scenario 4: restart (brand-new view model/catalog instance over the same on-disk directory) ---
  Reloaded verse section rows=4 (expected 4, unaffected by chorus edits)
  Reloaded chorus section rows=2 (expected 2)

--- Scenario 5: unavailable-source and unavailable-path warnings (stem removed, path deleted) ---
  T-Piano row: IsSourceUnavailable=True (track no longer supplied)
  T-Bass row: IsPathUnavailable=True, SpatialPathName=(null) (Tree path deleted)
    [UnavailableSource] Source track 'T-Piano' (role 'Lead') is no longer available.
    [UnavailablePath] Target path for source track 'T-Bass' (role 'Bass') is no longer available.

--- Scenario 6: invalid input is rejected and never corrupts the project (negative path) ---
  Unknown spatial path rejected: True, error="Spatial path '79920ac6-05f4-472e-896c-83f4d505b8ec' does not exist in the supplied path set (source layout 'hash-demo-067')."
  Unrecognized tier rejected: True, error="Unknown precedence tier 'SuperLocked'; expected one of: UserLocked, Manual, AcceptedMotif, Automated."
  Rows unchanged by both rejections: True

Step 067 manual demo completed successfully.

Cleaning up demo cache directory.
```

Direct, real, non-test-harness confirmation of this step's own required text: (1) "Mappings persist and can
change per section" — Scenario 3 switches from the verse to the chorus section (its own independent, empty
routing), and Scenario 4 proves both sections' own routings independently survive a real restart through a
brand-new view-model/catalog instance. (2) "Create UI mapping table of sources to named spatial paths with
role priority" — Scenario 1's table shows every source track's role, target path name and priority
together. (3) "... and empty/unavailable warnings" — Scenario 2 shows unmapped-role warnings; Scenario 5
shows both an unavailable-source and an unavailable-path warning, each tied to the real affected row. (4)
"Invalid input, cancellation or stale persisted state must not corrupt the project" — Scenario 6 confirms an
unknown target path and an unrecognized precedence tier are both rejected with the row count unchanged
before and after.

## 6. Fixtures / input hashes

- No show-layout XML fixture was needed for this step's own tests or demo — `SpatialPathSet` is hand-built
  directly (two/three named paths), consistent with Step 066's own convention of using a hand-built
  `SpatialPathSet` for cases where `SpatialPathId` existence-validation itself is not the behavior under
  test (here, routing-table/warning behavior is under test). `MusicalRoleAssignment`/`MusicalRoleAssignmentSet`
  operate on caller-supplied synthetic track ids (`SourceTrackId` is an opaque string); this step owns no
  stem/melody-track import of its own.
- The real demo's own persisted `MusicalRoleAssignmentSet` JSON files (one per section) were written to a
  temporary directory for inspection and deleted by the demo's own `finally` block (confirmed by "Cleaning
  up demo cache directory." in the real output above); the throwaway demo *project* itself was deleted after
  this evidence was recorded.
- **Real-audio-device test exclusion, disclosed (unchanged, carried forward from every prior Phase 09/10
  step's own §6):** `Step025`/`Step028`/`Step029`/`Step030`/`Step042ShellWiringTests` construct a real
  `WaveOutAudioTransport` (the real Windows winmm audio device) unavailable in this session's own sandboxed
  execution environment — a pre-existing, environment-caused condition predating this step entirely. No file
  this step touched is on the call path of any of those five classes (confirmed: none reference
  `RoleRoutingEditorViewModel`/`RoleRoutingRowView`/`RoleRoutingWarningView` in any way).

## 7. Unresolved issues / known gaps

- **No real SCREEN 12 CHOREOGRAPHY Avalonia/AXAML view or Shell-layer `ShellScreen` wiring exists** —
  disclosed in §3; this step's own required evidence ("Avalonia screenshot **or** repeatable desktop
  interaction recording with saved-state verification") is proven at the bindable view-model layer plus the
  real, non-test-harness demo in §5, not by a pixel-rendering screenshot. Every other screen in this
  repository has the identical gap (confirmed via a full repository `Glob` for `*.axaml` — still only
  `App.axaml`/`MainWindow.axaml`) — this is the project's own pre-existing, consistent convention, not a
  shortcut unique to this step.
- **`RoleRoutingEditorViewModel` does not itself enforce that `Priority` is used for anything beyond display**
  — `IRolePrecedenceResolver` (Step 066's own contract) is the actual consumer of `Priority`/`PrecedenceTier`
  for event-precedence ranking; this step's own view model only surfaces those fields in the routing table,
  it does not call `IRolePrecedenceResolver` itself (no competing-event resolution happens at this point in
  Phase 10 — that remains Step 071's own later, disclosed scope).
- **No real `ICompositionPlanner`/`IChoreographyCompiler`/`VisualBudget`/`SectionVisualDirection` exists
  yet** — `PHASE-10.md` Section 3's own proposed signatures for those contracts are later steps' own scope
  (Steps 068-071); this step only adds the routing-table UI layer over Step 066's own role/precedence
  foundation.
- **Carried-forward, disclosed phase-level/dependency-level gaps are unchanged from Step 066's own closing
  evidence** (not reopened or re-resolved here): Phase 09's own consolidated `GATE-REPORT.md` is still
  unwritten; Phase 06's own stem-separation gate remains BLOCKED; Phase 07's and Phase 08's own full-phase
  gates have not been separately run/recorded; real-audio-device Shell tests remain environment-blocked
  (§6).
- **Per the user's own standing waiver** (`memory: feedback_skip_xlights_human_verification`), the
  real-xLights render/desktop-interaction check is not treated as a blocker for this step's own scope — no
  XSQ/effect-recipe output exists yet at this point in Phase 10 for such a check to apply to.

## 8. PASS / BLOCKED

**Status: PASS** (for this step's own narrow, disclosed scope — a bindable instrument-to-role/role-to-prop
routing table UI, with role-priority display and empty/unavailable warnings, over Step 066's own
already-accepted per-section persistence — not a claim that any carried-forward phase-level gap listed in
§7 is resolved, and not a claim that a real SCREEN 12 Avalonia view, `ICompositionPlanner`, or event
collision resolution — later Phase 10 steps' own scope — is complete).

- "Mappings persist and can change per section" (required acceptance, verbatim) — proven by
  `Step067_Acceptance_MappingsPersistAndCanChangePerSection` (two different sections routed independently,
  reloaded through a brand-new view-model/catalog instance) — independently confirmed end-to-end by the real
  demo's Scenarios 1, 3 and 4.
- "Create UI mapping table of sources to named spatial paths with role priority and empty/unavailable
  warnings; persist as user presets" (implementation guidance, verbatim) — proven by `Rows`/`Warnings`
  together across every test in §2 and the real demo's Scenarios 1, 2 and 5 (unmapped-role, unavailable-source
  and unavailable-path warnings each independently demonstrated against real, affected rows).
- "Invalid input, cancellation or stale persisted state must not corrupt the project" (failure/edge case,
  verbatim) — proven by `AssignAsync_NothingOpen_SetsActionErrorAndLeavesRowsEmpty_NegativePath`,
  `AssignAsync_UnknownSpatialPath_RejectsWithoutPartialMutation_NegativePath`,
  `AssignAsync_UnrecognizedPrecedenceTier_RejectsWithoutPartialMutation_NegativePath`,
  `ClearAsync_UnknownTrack_RejectsWithoutPartialMutation_NegativePath`, and
  `AssignAsync_Cancelled_DoesNotPersist_NegativePath` — independently confirmed by the real demo's Scenario 6
  (rejected bad path/tier, row count unchanged before and after).
- Full regression (real-audio-device tests excluded, disclosed in full in §6): **818/818 passing**, 0
  failed, 0 skipped; every pre-existing test from Phase 01-10/Step 066 still passes unmodified, plus this
  step's own 10 new Shell tests. `dotnet format --verify-no-changes` clean.
- **No file from any prior step/phase was behaviorally changed** — this step only adds
  `RoleRoutingRowView.cs`/`RoleRoutingWarningView.cs`/`RoleRoutingEditorViewModel.cs` to the existing
  `XlightsAI.Studio.Shell` project, adds one new `<ProjectReference>` line to that project's `.csproj`
  (Shell → Composition), and adds one new test file. No existing Composition-, Display- or Shell-project
  file's own existing members were modified.
- **Carried-forward, disclosed phase-level/dependency-level gaps (not this step's own blocker):** no real
  SCREEN 12 Avalonia view exists yet (§7, project-wide pre-existing condition); Phase 09's own consolidated
  gate report is still unwritten; Phase 06's own gate remains BLOCKED; Phase 07's and Phase 08's own
  full-phase gates have not been separately run/recorded; real-audio-device Shell tests are
  environment-blocked (§6); no real `ICompositionPlanner`/`IChoreographyCompiler`/event-collision resolution
  exists yet (§7 — later Phase 10 steps' own scope).

Per this step's own closing instruction ("do not execute the next step until this step is proven"), Step 068
("Create configurable visual density and focus budget") should not begin until this evidence is reviewed and
accepted.
