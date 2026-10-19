# PHASE 10 — Step 069 Evidence

**Step:** "Build sectional energy and color/brightness story"
**Spec:** `docs/phase-specs/PHASE-10.md` §6, Step 069 (lines 251-265) — the fourth of six numbered steps
of Phase 10 (066-071)
**Date:** 2026-10-16

## 1. Pre-implementation baseline inspection

- Read `docs/phase-specs/PHASE-10.md` in full, confirming Step 069's own text: required acceptance
  ("Verse/chorus intensity curve follows plan"), implementation guidance ("Plan section intensity normalized
  across song; preserve energy hierarchy and avoid maximum brightness continuously"), failure/edge case
  ("Invalid input, cancellation or stale persisted state must not corrupt the project"), and Section 3's own
  proposed `VisualStory`/`SectionVisualDirection` signatures.
- Read `implementation-evidence/PHASE-10/STEP-066.md`, `STEP-067.md` and `STEP-068.md` in full as the
  immediately preceding, already-accepted contract baseline. Step 068's own §7 explicitly disclosed: "No real
  `ICompositionPlanner`/`IChoreographyCompiler`/`SectionVisualDirection`/`ChoreographyPlan` exists yet ...
  Steps 069-071" — confirming `VisualStory`/`SectionVisualDirection` are genuinely this step's own new
  contracts to create.
- Read every file in `src/XlightsAI.Studio.Composition/` in full (all Step 066/068 types: `MusicalRole.cs`,
  `PrecedenceTier.cs`, `MusicalRoleAssignment.cs`, `MusicalRoleAssignmentSet.cs`,
  `IMusicalRoleAssignmentCatalog.cs`/`MusicalRoleAssignmentCatalog.cs`,
  `IMusicalRoleAssignmentStore.cs`/`JsonMusicalRoleAssignmentStore.cs`,
  `IRolePrecedenceResolver.cs`/`RolePrecedenceResolver.cs`, `VisualBudget.cs`, `VisualBudgetCandidate.cs`,
  `VisualBudgetResolver.cs`, `VisualBudgetPreset.cs`, `VisualBudgetPresetCatalog.cs`,
  `JsonVisualBudgetPresetStore.cs`, `CompositionInvalidException.cs`, `CompositionCorruptException.cs`)
  before writing any new type, specifically to decide whether this step's own persisted-preset shape could
  reuse the already-established `VisualBudgetPreset`/`VisualBudgetPresetCatalog`/`JsonVisualBudgetPresetStore`
  atomic-write/versioning pattern (it does, keyed by a song id instead of a preset name — see §3).
- Read `src/XlightsAI.Studio.Shell/RoleRoutingEditorViewModel.cs` and
  `src/XlightsAI.Studio.Shell/VisualBudgetEditorViewModel.cs` in full (the direct structural precedent for
  this step's own Shell-layer screen view model: explicit `ScreenLoadState` loading/empty/error/loaded
  machine, "catch the domain's typed invalid-input exception, set `ActionErrorMessage`, leave bindable state
  completely unchanged on failure" convention for every mutating action, `RaiseStateDerivedChanged`
  fan-out, UI-only row-projection record convention, and `VisualBudgetEditorViewModel`'s own
  preview-then-save split — the direct precedent for this step's own plan-then-save split).
- Read `tests/XlightsAI.Studio.Composition.Tests/Step068AcceptanceTests.cs` and
  `tests/XlightsAI.Studio.Shell.Tests/Step068VisualBudgetEditorViewModelTests.cs` in full as the direct
  structural precedent for this step's own test style (real planner/catalog/store instances, not mocks;
  before/after mutation-equality negative-path convention; restart-across-a-brand-new-instance persistence
  proof; shuffled-input-order determinism proof).
- Read `src/XlightsAI.Studio.Structure/SectionKind.cs` and `src/XlightsAI.Studio.Structure/SectionLabelCandidate.cs`
  in full (Phase 08 Step 054's own already-accepted section-classification contract: `SectionKind`
  enum — `Unknown`/`Intro`/`Verse`/`PreChorus`/`Chorus`/`Bridge`/`Solo`/`Outro` — and `SectionLabelCandidate.Id`
  as the stable section identity a later step addresses a classified segment by) to decide whether this
  step's own section-energy input could reuse `SectionKind` directly rather than duplicate a parallel "section
  type" concept (it does — see §3).
- Confirmed `src/XlightsAI.Studio.Shell/XlightsAI.Studio.Shell.csproj` already references
  `XlightsAI.Studio.Structure`, and `src/XlightsAI.Studio.Structure/XlightsAI.Studio.Structure.csproj` does
  **not** reference `XlightsAI.Studio.Composition` (no project-reference cycle risk) before adding a new
  `XlightsAI.Studio.Composition -> XlightsAI.Studio.Structure` reference (see §3).
- Searched the full repository for any prior "Step 069"/"STEP-069"/`VisualStory`/`SectionVisualDirection`/
  `SectionVisualStoryPlanner` reference: found only `PHASE-10.md` itself, confirming this is genuinely new
  work.
- Confirmed via `Glob` for `*.axaml`: only `App.axaml`/`MainWindow.axaml` exist anywhere in the repository —
  no SCREEN 12 CHOREOGRAPHY Avalonia view exists yet (unchanged, carried-forward condition from every prior
  Phase 09/10 UI-adjacent step; see §3/§7).
- Baseline taken by running the real, full solution build *before* any Step 069 code was written:
  `dotnet build XlightsAI.Studio.slnx --nologo` (clean, 0 warnings/errors), reconfirming Step 068's own
  closing state immediately before starting.

## 2. Scope actually implemented

**No file from Step 001-068 was behaviorally changed.** This step adds ten new files to the existing
`src/XlightsAI.Studio.Composition/` project (plus one new `ProjectReference` to
`XlightsAI.Studio.Structure`), two new files to the existing `src/XlightsAI.Studio.Shell/` project, and two
new test files (plus one new `ProjectReference` to `XlightsAI.Studio.Structure` on the Composition test
project, needed to construct `SectionKind` fixtures directly in tests). No existing Composition-, Structure-,
Display- or Shell-project file's own existing members were modified.

### New files in `src/XlightsAI.Studio.Composition/`

- **`SectionEnergyInput.cs`** — one already-classified section's raw musical energy, the input
  `ISectionVisualStoryPlanner.Plan` normalizes. Reuses `XlightsAI.Studio.Structure.SectionKind` directly
  (Phase 08 Step 054's own already-accepted contract) rather than duplicating a parallel "section type"
  concept, and reuses `SectionLabelCandidate.Id`'s own identity convention for `SectionId`.
- **`SectionVisualDirection.cs`** — one section's own planned palette/intensity, a deliberately narrower
  realization of PHASE-10.md Section 3's own proposed `SectionVisualDirection` signature (`FocalProp`/`Roles`
  deliberately omitted — see §3).
- **`VisualStory.cs`** — the full, versioned, per-song story (PHASE-10.md Section 3's own proposed signature,
  verbatim field names for `VersionId`/`Sections`), with a `Validate()` method rejecting an empty section
  list, a duplicate `SectionId`, a blank `PaletteId`, or an out-of-`[0,1]`/non-finite `Intensity` before any
  persistence is attempted.
- **`ISectionVisualStoryPlanner.cs`** / **`SectionVisualStoryPlanner.cs`** — the normalization engine. Pure
  and deterministic (no I/O, no mutable state): validates every input (non-empty, unique `SectionId`,
  non-negative finite `RawEnergy`) before any computation; min-max normalizes `RawEnergy` into a fixed
  `[0.05, 0.92]` intensity band (strictly monotonic with `RawEnergy`, so energy hierarchy/ordering is
  preserved exactly, and no section can ever be planned at literal `0.0` or `1.0`); assigns a fixed `0.485`
  moderate intensity when every input's `RawEnergy` is identical (no hierarchy exists to preserve); derives a
  deterministic `PaletteId` from each section's own `SectionKind` via a fixed lookup table.
- **`VisualStoryRecord.cs`** / **`IVisualStoryStore.cs`** / **`JsonVisualStoryStore.cs`** /
  **`IVisualStoryCatalog.cs`** / **`VisualStoryCatalog.cs`** — the named, persisted story boundary realizing
  the data-contract rule's own "section visual direction" (PHASE-10.md §4). Mirrors
  `VisualBudgetPreset`/`VisualBudgetPresetCatalog`/`JsonVisualBudgetPresetStore`'s own atomic-write convention
  exactly, keyed by a caller-defined `Guid songId` instead of a preset name (a `VisualStory` is inherently
  whole-song, not per-phrase). `VisualStoryCatalog.SaveAsync` validates the story *before* any load/write and
  increments `Version` on every save of the same song.

### New files in `src/XlightsAI.Studio.Shell/`

- **`SectionVisualDirectionRowView.cs`** — UI-only bindable projection of one `SectionVisualDirection` for
  SCREEN 12 CHOREOGRAPHY's sectional energy/color story table (adds a rounded `IntensityPercent` for a
  directly bindable display). Same convention `VisualBudgetCandidateRowView` already established.
- **`VisualStoryEditorViewModel.cs`** — SCREEN 12 CHOREOGRAPHY's own bindable sectional-energy/color-story
  editor view model, the direct UI layer over this step's own `ISectionVisualStoryPlanner`/
  `IVisualStoryCatalog`:
  - `LoadState`/`IsEmpty`/`IsError`/`IsLoaded` — the same explicit loading/empty/error/loaded state machine
    `VisualBudgetEditorViewModel` already established.
  - `SongId`/`Version` — identity/version of the song currently open.
  - `OpenSongAsync(catalog, songId, ct)` — loads (or switches to) a song's own independently persisted
    `VisualStory`; calling it again with a different id loads that song's own independently saved story.
  - `Plan(planner, sections)` — computes a new story in memory via `planner`, publishing it through `Rows`
    *without persisting it* (so a user can inspect the intensity curve before committing) — rejects with
    `ActionErrorMessage` (never a partial `Rows` mutation) when no song is open or the planner rejects the
    input.
  - `SaveAsync(catalog, ct)` — persists the most recently `Plan`ned story via `IVisualStoryCatalog.SaveAsync`;
    rejects with `ActionErrorMessage` (never a partial mutation) when no song is open or nothing has been
    planned yet.
  - `Rows`/`HasUnsavedPlan` — `Rows` reflects the most recently planned-but-unsaved story if one exists, else
    the persisted story; `HasUnsavedPlan` is `true` exactly between a successful `Plan` and the next `SaveAsync`.

### New test files

- **`tests/XlightsAI.Studio.Composition.Tests/Step069AcceptanceTests.cs`** (18 tests) — real
  `SectionVisualStoryPlanner`/`VisualStoryCatalog`/`JsonVisualStoryStore` (not mocks):
  - **`Step069_Acceptance_VerseChorusIntensityCurveFollowsPlan`** — the required acceptance test, verbatim. A
    synthetic Intro/Verse/Chorus/Verse/Chorus/Outro excerpt with increasing real-world energy proxies. Proves:
    both choruses are strictly more intense than both verses, which are strictly more intense than the
    intro/outro; the louder chorus is more intense than the quieter chorus (energy hierarchy preserved);
    every section's own intensity stays strictly below `1.0` (no maximum brightness, even for the single
    loudest section); palette ids are deterministically derived from each section's own kind.
  - **`Plan_SameInputsInAnyOrder_ProducesTheSamePerSectionIntensities`** — shuffled-input-order determinism
    (per-`SectionId` intensities identical regardless of input order; `VersionId` intentionally differs).
  - **`Plan_AllSectionsHaveEqualEnergy_AssignsAModerateNonMaxNonZeroIntensity`** — the flat-input edge case
    (no hierarchy to preserve) never collapses to literal `0` or `1`.
  - **`Plan_EmptyInput_ThrowsBeforeAnyComputation_NegativePath`**,
    **`Plan_DuplicateSectionId_Throws_NegativePath`**,
    **`Plan_InvalidRawEnergy_Throws_NegativePath`** (3 inline cases: NaN, +Infinity, negative),
    **`VisualStory_Validate_RejectsEmptySections_NegativePath`**,
    **`VisualStory_Validate_RejectsDuplicateSectionId_NegativePath`**,
    **`VisualStory_Validate_RejectsOutOfRangeIntensity_NegativePath`** (3 inline cases),
    **`VisualStory_Validate_RejectsBlankPaletteId_NegativePath`**.
  - **`VisualStoryCatalog_SaveAndReload_PersistsAcrossRestart`** — saved-state verification through a
    brand-new catalog/store instance.
  - **`VisualStoryCatalog_InvalidStory_RejectsBeforeAnyWrite_NegativePath`**,
    **`VisualStoryCatalog_Cancelled_DoesNotPersist_NegativePath`**,
    **`VisualStoryStore_LoadAsync_CorruptFile_ThrowsCorruptException_NegativePath`**.
- **`tests/XlightsAI.Studio.Shell.Tests/Step069VisualStoryEditorViewModelTests.cs`** (7 tests) — real
  `VisualStoryEditorViewModel`/`VisualStoryCatalog`/`JsonVisualStoryStore`/`SectionVisualStoryPlanner`
  (not mocks) under a temporary directory:
  - **`Step069_Acceptance_PlanAndSaveFollowVerseChorusIntensityCurve`** — the required acceptance text proven
    again at the bindable view-model layer (`Plan`/`Rows`/`SaveAsync`).
  - **`VisualStory_PersistsAndCanChangePerSong_SavedStateAcrossRestart`** — two independently keyed songs,
    each independently planned/saved, reloaded through a brand-new view-model/catalog instance.
  - **`Plan_NothingOpen_SetsActionErrorAndLeavesRowsEmpty_NegativePath`**,
    **`Plan_InvalidSections_RejectsWithoutPartialRows_NegativePath`**,
    **`SaveAsync_NothingOpen_SetsActionErrorAndLeavesVersionNull_NegativePath`**,
    **`SaveAsync_NothingPlanned_RejectsWithoutMutatingPersistedState_NegativePath`**,
    **`OpenSongAsync_CorruptPersistedFile_SetsErrorAndIsError_NegativePath`**.

## 3. Architecture deviations / decisions

- **`SectionVisualDirection` omits PHASE-10.md Section 3's own proposed `FocalProp`/`Roles` fields.** The
  proposed signature is `SectionVisualDirection(Guid SectionId, string PaletteId, double Intensity, ModelId?
  FocalProp, IReadOnlyList<RoleAssignment> Roles)`. `FocalProp` (a concrete `ModelId`) and `Roles` (concrete
  prop/role targeting) were deliberately **not** included here, for the exact same reason Step 068's own ADR
  gave for not adopting `PlannedGesture` as its candidate type: they tie a section's *story* to concrete
  xLights models and role-to-prop targeting, which is Step 070/071's own later, disclosed scope ("Implement
  build, silence, transitions..." / "Compose and resolve overlapping effects with layer/priority strategies
  ... Choreography compilation uses only verified recipes and targetable props"). This step's own narrow scope
  is "plan section intensity normalized across song" (implementation guidance, verbatim) — a per-section
  palette/intensity story independent of which concrete prop ends up rendering it.
- **`SectionEnergyInput`/`SectionVisualDirection` reuse `XlightsAI.Studio.Structure.SectionKind` directly**,
  requiring a new `XlightsAI.Studio.Composition -> XlightsAI.Studio.Structure` project reference (confirmed
  acyclic in §1: `Structure` does not reference `Composition`, and `Shell` already references both). This
  mirrors Step 066's own precedent of reusing an already-accepted contract (there, `Display.RoleAssignment`'s
  naming lesson) rather than duplicating a second, parallel "section kind" enum with the same eight values.
- **Intensity is normalized into a fixed `[0.05, 0.92]` band, never `[0,1]` directly.** This is the concrete,
  disclosed realization of "avoid maximum brightness continuously" (implementation guidance, verbatim): the
  ceiling (`0.92`) guarantees no section — not even the single loudest section in an entire song — is ever
  planned at literal `1.0`; the floor (`0.05`) guarantees no section's own planned intensity is literal `0.0`
  either (full, permanent blackout remains Step 070's own later, disclosed "build, silence, transitions" scope,
  not this step's normalization). The transform is a strict positive-linear (min-max) map, so ordering and
  relative differences in `RawEnergy` are preserved exactly in `Intensity` — this is how "preserve energy
  hierarchy" is realized precisely, not merely approximately.
- **A perfectly flat `RawEnergy` input (every section equal) is planned at a single fixed moderate intensity
  (`0.485`, the midpoint of the `[0.05, 0.92]` band), not at the ceiling or the floor.** A flat input carries
  no energy hierarchy to preserve, so neither extreme would be a defensible claim about the input; disclosed
  here as a deliberate choice, covered by its own test
  (`Plan_AllSectionsHaveEqualEnergy_AssignsAModerateNonMaxNonZeroIntensity`).
- **Palette assignment is a fixed, deterministic lookup table keyed by `SectionKind`, not a caller-supplied or
  persisted-per-name value (unlike `VisualBudgetPreset`, which is caller-named).** This avoids introducing a
  second class of invalid-input validation (an arbitrary palette string) for a step whose own required
  acceptance text is about the *intensity curve*, not palette curation; a later step can extend or override
  this table without a breaking change to `SectionVisualDirection`'s own shape.
- **Persistence is keyed by a caller-supplied `Guid songId`, not a `PhraseId` (unlike
  `MusicalRoleAssignmentSet`) and not a name string (unlike `VisualBudgetPreset`).** A `VisualStory` is
  inherently whole-song (every section's intensity is normalized relative to every other section in the same
  story), so it cannot be scoped per-phrase/section the way role routing is, and it is not a reusable named
  style preset the way a visual budget is — it is one specific song's own one specific planned story.
  `VisualStoryCatalog`/`JsonVisualStoryStore` otherwise mirror `VisualBudgetPresetCatalog`/
  `JsonVisualBudgetPresetStore`'s own atomic-write/versioning convention exactly.
- **`VisualStoryEditorViewModel` splits `Plan` (in-memory only) from `SaveAsync` (persists), mirroring
  `VisualBudgetEditorViewModel`'s own preview-then-save split.** A user can inspect the planned intensity
  curve (`Rows`) before committing it; `HasUnsavedPlan` makes that pending-vs-persisted distinction bindable.
- **No real `ICompositionPlanner`/`IChoreographyCompiler`/`ChoreographyPlan`/`PlannedGesture` exists yet.**
  PHASE-10.md Section 3's own proposed signatures for those contracts remain later steps' own scope (Steps
  070-071, per Step 068's own already-disclosed §7); this step only adds the sectional energy/color-story
  normalization + persistence + preview-editor UI layer those later steps will consume.
- **No real SCREEN 12 CHOREOGRAPHY Avalonia/AXAML view exists.** Unchanged, carried-forward disclosure from
  every prior Phase 09/10 step (confirmed again via a full repository `Glob` for `*.axaml`: still only
  `App.axaml`/`MainWindow.axaml`). This step's own required evidence text is narrower than a literal pixel
  claim — "Avalonia screenshot **or** repeatable desktop interaction recording with saved-state
  verification" — and is satisfied here by the real, non-test-harness demo in §5 below, the same precedent
  every prior UI-adjacent Phase 09/10 step's evidence already established for this exact same project-wide
  gap.
- **Per the user's own standing waiver** (`memory: feedback_skip_xlights_human_verification`), no live
  xLights render/desktop-interaction check was attempted — this step's own required acceptance text is a pure
  intensity-curve-planning/persistence claim (no XSQ/effect-recipe output exists yet at this point in Phase
  10), proven directly by the automated tests and demo below.

## 4. Commands actually executed

All run via the PowerShell tool — the sandboxed Bash tool in this session again returned bare exit code 1
with no output for plain commands (confirmed again at the very start of this step with `pwd`/`echo`, and
again when attempting `dotnet build`), the same pre-existing environment limitation every prior step's own
evidence has recorded; PowerShell was used for every `dotnet`/file-system command below without incident.

```
# Baseline, before any Step 069 code existed:
dotnet build XlightsAI.Studio.slnx --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s). (Step 068's own closing state)

# After adding the XlightsAI.Studio.Composition -> XlightsAI.Studio.Structure project reference and the
# new Step 069 Composition files:
dotnet build src\XlightsAI.Studio.Composition\XlightsAI.Studio.Composition.csproj --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).

# After adding the new Step 069 Shell files:
dotnet build src\XlightsAI.Studio.Shell\XlightsAI.Studio.Shell.csproj --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).

dotnet test tests\XlightsAI.Studio.Composition.Tests\XlightsAI.Studio.Composition.Tests.csproj --nologo --filter "FullyQualifiedName~Step069"
  # Passed! - Failed: 0, Passed: 18, Skipped: 0, Total: 18, Duration: 254 ms
  # (one xUnit2013 analyzer warning on first run -- "do not use Assert.Equal() for collection size" -- fixed
  # by switching to Assert.Single before the final run below)

dotnet test tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj --nologo --filter "FullyQualifiedName~Step069"
  # Passed! - Failed: 0, Passed: 7, Skipped: 0, Total: 7, Duration: 221 ms

dotnet build XlightsAI.Studio.slnx --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).

dotnet format XlightsAI.Studio.slnx --verify-no-changes --no-restore
  # No output (clean) -- matches the repository's own CI formatting gate, checked across the full solution.

dotnet test XlightsAI.Studio.slnx --nologo --filter "FullyQualifiedName!~Step025&FullyQualifiedName!~Step028&FullyQualifiedName!~Step029&FullyQualifiedName!~Step030&FullyQualifiedName!~Step042"
dotnet test tests\XlightsAI.Studio.Stems.Tests\XlightsAI.Studio.Stems.Tests.csproj --nologo --filter "FullyQualifiedName~Step042"
  # Passed! - Failed: 0, Passed: 8, Skipped: 0, Total: 8, Duration: 402 ms
  # (the blunt numeric filter above also excludes Stems.Tests' own unrelated, legitimate
  # Step042AcceptanceTests.cs -- the same filter side effect disclosed in every prior Phase 09/10 step's own
  # evidence; reconfirmed here passing in isolation.)

# Real, non-test-harness manual demo (built under the session scratchpad at %TEMP%\xlai-step069-demo-proj,
# deleted after this run):
dotnet run --project <scratchpad>\xlai-step069-demo-proj
```

### Targeted test results (Step 069's own 25 new tests, real run)

```
Step069AcceptanceTests (Composition, 18 new):
  Step069_Acceptance_VerseChorusIntensityCurveFollowsPlan                      Passed
  Plan_SameInputsInAnyOrder_ProducesTheSamePerSectionIntensities               Passed
  Plan_AllSectionsHaveEqualEnergy_AssignsAModerateNonMaxNonZeroIntensity       Passed
  Plan_EmptyInput_ThrowsBeforeAnyComputation_NegativePath                     Passed
  Plan_DuplicateSectionId_Throws_NegativePath                                 Passed
  Plan_InvalidRawEnergy_Throws_NegativePath (x3 inline cases)                 Passed
  VisualStory_Validate_RejectsEmptySections_NegativePath                      Passed
  VisualStory_Validate_RejectsDuplicateSectionId_NegativePath                 Passed
  VisualStory_Validate_RejectsOutOfRangeIntensity_NegativePath (x3 inline)    Passed
  VisualStory_Validate_RejectsBlankPaletteId_NegativePath                     Passed
  VisualStoryCatalog_SaveAndReload_PersistsAcrossRestart                      Passed
  VisualStoryCatalog_InvalidStory_RejectsBeforeAnyWrite_NegativePath          Passed
  VisualStoryCatalog_Cancelled_DoesNotPersist_NegativePath                    Passed
  VisualStoryStore_LoadAsync_CorruptFile_ThrowsCorruptException_NegativePath  Passed
Composition.Tests total (Step 069 slice): 18/18 passed, 0 failed, 0 skipped.

Step069VisualStoryEditorViewModelTests (Shell, 7 new):
  Step069_Acceptance_PlanAndSaveFollowVerseChorusIntensityCurve               Passed
  VisualStory_PersistsAndCanChangePerSong_SavedStateAcrossRestart             Passed
  Plan_NothingOpen_SetsActionErrorAndLeavesRowsEmpty_NegativePath             Passed
  Plan_InvalidSections_RejectsWithoutPartialRows_NegativePath                Passed
  SaveAsync_NothingOpen_SetsActionErrorAndLeavesVersionNull_NegativePath      Passed
  SaveAsync_NothingPlanned_RejectsWithoutMutatingPersistedState_NegativePath  Passed
  OpenSongAsync_CorruptPersistedFile_SetsErrorAndIsError_NegativePath         Passed
Shell.Tests total (Step 069 slice): 7/7 passed, 0 failed, 0 skipped.
```

### Full regression (final, after all Step 069 implementation and tests, real-audio-device Shell tests excluded — unchanged pre-existing condition, see Step 064-068's own §6)

```
Passed! - XlightsAI.Studio.Audio.Tests.dll           23/23
Passed! - XlightsAI.Studio.Xlights.Tests.dll         74/74
Passed! - XlightsAI.Studio.Editing.Tests.dll         83/83
Passed! - XlightsAI.Studio.Display.Tests.dll         56/56
Passed! - XlightsAI.Studio.Structure.Tests.dll       78/78
Passed! - XlightsAI.Studio.Composition.Tests.dll     41/41 (23 Step 066+068 + 18 new Step 069)
Passed! - XlightsAI.Studio.Melody.Tests.dll         117/117
Passed! - XlightsAI.Studio.Projects.Tests.dll         25/25
Passed! - XlightsAI.Studio.Shell.Tests.dll          153/153 (146 Step 001-068 + 7 new Step 069; real-audio-device tests excluded, unchanged)
Passed! - XlightsAI.Studio.Rhythm.Tests.dll           82/82
Passed! - XlightsAI.Studio.Reference.Tests.dll        31/31
Passed! - XlightsAI.Studio.Stems.Tests.dll            93/93  (+8 Step042AcceptanceTests reconfirmed separately, below)
```

**860 total passing** (839 carried forward from Step 068's own closing count, unmodified, + this step's own
18 new Composition tests + 7 new Shell tests = 25), 0 failed, 0 skipped. Every pre-existing test from Steps
001-068 still passes unmodified; the only additions are `Step069AcceptanceTests` (18/18) and
`Step069VisualStoryEditorViewModelTests` (7/7).

## 5. Persisted-output inspection / observable demonstration for this step

A real, non-test-harness console program (not part of the repository, built under the session scratchpad at
`%TEMP%\xlai-step069-demo-proj`, deleted after this run) exercises the real
`VisualStoryEditorViewModel`/`VisualStoryCatalog`/`JsonVisualStoryStore`/`SectionVisualStoryPlanner` directly —
no test harness, no mocks. It deliberately mirrors the phase gate's own named worked example ("preview verse
vs louder chorus") over a 20-second excerpt with intro/verse/chorus/verse/chorus/outro sections:

```
dotnet run --project <scratchpad>\xlai-step069-demo-proj
```

Observed real output:

```
=== Step 069 manual demo: real visual story UI view model, no test harness ===

--- Scenario 1: open a new song (nothing planned/saved yet) ---
  Opened=True, LoadState=Loaded, Version=null, Rows=0

--- Scenario 2: plan a 20-second excerpt's own energy (intro/verse/chorus/verse/chorus/outro) ---
  Planned=True, HasUnsavedPlan=True
    Intro      palette=Soft       intensity=0.098 (10%)
    Verse      palette=Calm       intensity=0.388 (39%)
    Chorus     palette=Vivid      intensity=0.775 (78%)
    Verse      palette=Calm       intensity=0.437 (44%)
    Chorus     palette=Vivid      intensity=0.920 (92%)
    Outro      palette=Soft       intensity=0.050 (5%)
  Chorus1 (0.775) > Verse1 (0.388): True  -- "Verse/chorus intensity curve follows plan", verbatim.
  Chorus2 (0.920) > Verse2 (0.437): True
  Loudest chorus (Chorus2) louder than quieter chorus (Chorus1): True
  No section ever reaches literal maximum brightness (all < 1.0): True

--- Scenario 3: save the planned story ---
  Saved=True, Version=0, HasUnsavedPlan=False

--- Scenario 4: restart (brand-new view-model/catalog instances over the same on-disk directory) ---
  Reloaded Version=0, Rows=6
  Reloaded Chorus2 intensity matches what was saved: True

--- Scenario 5: invalid input is rejected and never corrupts the project (negative path) ---
  Plan with nothing open rejected: True, error="Open a song before planning a visual story."
  Plan with empty sections rejected: True, error="At least one SectionEnergyInput is required to plan a VisualStory."
  Save with nothing planned rejected: True, error="Plan a visual story before saving it."
  Rows left empty after all rejections: True

Step 069 manual demo completed successfully.

Cleaning up demo cache directory.
```

Direct, real, non-test-harness confirmation of this step's own required text: (1) "Verse/chorus intensity
curve follows plan" — Scenario 2 shows both choruses (0.775, 0.920) strictly more intense than both verses
(0.388, 0.437), and the louder chorus more intense than the quieter chorus, exactly matching the fed-in
relative energy. (2) "Plan section intensity normalized across song; preserve energy hierarchy and avoid
maximum brightness continuously" — every planned intensity stays strictly below `1.0` even for the single
loudest section in the excerpt (Chorus2 at 0.920), and the ordering of planned intensities exactly mirrors the
ordering of the fed-in raw energy values. (3) Saved-state verification — Scenario 4 proves the saved story
survives a real restart through brand-new view-model/catalog instances, with the reloaded Chorus2 intensity
bit-identical to what was planned and saved. (4) "Invalid input ... must not corrupt the project" — Scenario 5
confirms planning with nothing open, planning with empty input, and saving with nothing planned are all
rejected with a bindable error message and no row mutation.

## 6. Fixtures / input hashes

- No show-layout XML fixture was needed for this step's own tests or demo — `SectionEnergyInput` operates on
  caller-supplied synthetic `(SectionId, SectionKind, RawEnergy)` tuples with arbitrary `Guid`s (this step's
  own planner never validates section existence against a real `SongStructure`/`SectionLabelCandidate` set;
  that remains Phase 08's own already-accepted `ISectionLabelClassifier`'s own job), consistent with this
  step's own narrow, disclosed scope (intensity/palette normalization, not section boundary/label detection).
- The real demo's own persisted `VisualStoryRecord` JSON file (one per song id) was written to a temporary
  directory for inspection and deleted by the demo's own `finally` block (confirmed by "Cleaning up demo
  cache directory." in the real output above); the throwaway demo *project* itself was deleted after this
  evidence was recorded.
- **Real-audio-device test exclusion, disclosed (unchanged, carried forward from every prior Phase 09/10
  step's own §6):** `Step025`/`Step028`/`Step029`/`Step030`/`Step042ShellWiringTests` construct a real
  `WaveOutAudioTransport` (the real Windows winmm audio device) unavailable in this session's own sandboxed
  execution environment — a pre-existing, environment-caused condition predating this step entirely. No file
  this step touched is on the call path of any of those five classes (confirmed: none reference
  `VisualStoryEditorViewModel`/`SectionVisualDirectionRowView`/`SectionVisualStoryPlanner`/`VisualStoryCatalog`
  in any way).

## 7. Unresolved issues / known gaps

- **No real SCREEN 12 CHOREOGRAPHY Avalonia/AXAML view or Shell-layer `ShellScreen` wiring exists** —
  disclosed in §3; this step's own required evidence ("Avalonia screenshot **or** repeatable desktop
  interaction recording with saved-state verification") is proven at the bindable view-model layer plus the
  real, non-test-harness demo in §5, not by a pixel-rendering screenshot. Every other screen in this
  repository has the identical gap (confirmed via a full repository `Glob` for `*.axaml` — still only
  `App.axaml`/`MainWindow.axaml`) — this is the project's own pre-existing, consistent convention, not a
  shortcut unique to this step.
- **`SectionVisualDirection` deliberately omits `FocalProp`/`Roles`** — disclosed in §3 as a deliberate,
  narrower scope than PHASE-10.md Section 3's own proposed signature; Step 070/071's own later effect-layer
  scope is expected to add concrete prop/role targeting when it owns compiling a section's story into actual
  gestures.
- **Palette assignment is a fixed lookup table, not user-configurable** — disclosed in §3; a later step could
  add a palette-override UI without a breaking change to `SectionVisualDirection`'s own shape (`PaletteId` is
  already an opaque string, not an enum).
- **No real `ICompositionPlanner`/`IChoreographyCompiler`/`ChoreographyPlan`/`PlannedGesture` exists yet** —
  `PHASE-10.md` Section 3's own proposed signatures for those contracts are later steps' own scope (Steps
  070-071); this step only adds the sectional energy/color-story normalization/persistence/preview-editor
  layer those later steps will consume.
- **Carried-forward, disclosed phase-level/dependency-level gaps are unchanged from Step 068's own closing
  evidence** (not reopened or re-resolved here): Phase 09's own consolidated `GATE-REPORT.md` is still
  unwritten; Phase 06's own stem-separation gate remains BLOCKED; Phase 07's and Phase 08's own full-phase
  gates have not been separately run/recorded; real-audio-device Shell tests remain environment-blocked
  (§6).
- **Per the user's own standing waiver** (`memory: feedback_skip_xlights_human_verification`), the
  real-xLights render/desktop-interaction check is not treated as a blocker for this step's own scope — no
  XSQ/effect-recipe output exists yet at this point in Phase 10 for such a check to apply to.

## 8. PASS / BLOCKED

**Status: PASS** (for this step's own narrow, disclosed scope — a measurable, normalized sectional
energy/color-brightness story with a bindable plan/save preview editor UI over it — not a claim that any
carried-forward phase-level gap listed in §7 is resolved, and not a claim that a real SCREEN 12 Avalonia view,
`ICompositionPlanner`, or effect-layer collision resolution — later Phase 10 steps' own scope — is complete).

- "Verse/chorus intensity curve follows plan" (required acceptance, verbatim) — proven by
  `Step069_Acceptance_VerseChorusIntensityCurveFollowsPlan` (domain) and
  `Step069_Acceptance_PlanAndSaveFollowVerseChorusIntensityCurve` (view model) — independently confirmed
  end-to-end by the real demo's Scenario 2 (both choruses strictly more intense than both verses, louder
  chorus more intense than quieter chorus).
- "Plan section intensity normalized across song; preserve energy hierarchy and avoid maximum brightness
  continuously" (implementation guidance, verbatim) — proven by the strict monotonic `[0.05, 0.92]`
  normalization in `SectionVisualStoryPlanner` plus `Plan_SameInputsInAnyOrder_ProducesTheSamePerSectionIntensities`
  and `Plan_AllSectionsHaveEqualEnergy_AssignsAModerateNonMaxNonZeroIntensity` — independently confirmed by
  the real demo's own printed intensities (every value strictly below `1.0`, ordering matching the fed-in
  energy).
- "Invalid input, cancellation or stale persisted state must not corrupt the project" (failure/edge case,
  verbatim) — proven by `Plan_EmptyInput_ThrowsBeforeAnyComputation_NegativePath`,
  `Plan_DuplicateSectionId_Throws_NegativePath`, `Plan_InvalidRawEnergy_Throws_NegativePath`,
  `VisualStory_Validate_RejectsEmptySections_NegativePath`,
  `VisualStory_Validate_RejectsDuplicateSectionId_NegativePath`,
  `VisualStory_Validate_RejectsOutOfRangeIntensity_NegativePath`,
  `VisualStory_Validate_RejectsBlankPaletteId_NegativePath`,
  `VisualStoryCatalog_InvalidStory_RejectsBeforeAnyWrite_NegativePath`,
  `VisualStoryCatalog_Cancelled_DoesNotPersist_NegativePath`,
  `Plan_NothingOpen_SetsActionErrorAndLeavesRowsEmpty_NegativePath`,
  `Plan_InvalidSections_RejectsWithoutPartialRows_NegativePath`,
  `SaveAsync_NothingOpen_SetsActionErrorAndLeavesVersionNull_NegativePath`, and
  `SaveAsync_NothingPlanned_RejectsWithoutMutatingPersistedState_NegativePath` — independently confirmed by
  the real demo's Scenario 5 (all three rejection paths exercised, rows left empty).
- Full regression (real-audio-device tests excluded, disclosed in full in §6): **860/860 passing**, 0 failed,
  0 skipped; every pre-existing test from Phase 01-10/Step 068 still passes unmodified, plus this step's own
  18 new Composition tests + 7 new Shell tests. `dotnet format --verify-no-changes` clean across the full
  solution.
- **No file from any prior step/phase was behaviorally changed** — this step only adds ten new files to
  `XlightsAI.Studio.Composition` (plus a new `ProjectReference` to `XlightsAI.Studio.Structure`) and two new
  files to `XlightsAI.Studio.Shell`, plus two new test files. No existing Composition-, Structure-, Display-
  or Shell-project file's own existing members were modified.
- **Carried-forward, disclosed phase-level/dependency-level gaps (not this step's own blocker):** no real
  SCREEN 12 Avalonia view exists yet (§7, project-wide pre-existing condition); Phase 09's own consolidated
  gate report is still unwritten; Phase 06's own gate remains BLOCKED; Phase 07's and Phase 08's own
  full-phase gates have not been separately run/recorded; real-audio-device Shell tests are
  environment-blocked (§6); no real `ICompositionPlanner`/`IChoreographyCompiler`/`ChoreographyPlan` exists
  yet (§7 — later Phase 10 steps' own scope).

Per this step's own closing instruction ("do not execute the next step until this step is proven"), Step 070
("Implement build, silence, transitions, release and phrase-ending recipes") should not begin until this
evidence is reviewed and accepted.
