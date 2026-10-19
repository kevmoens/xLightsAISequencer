# PHASE 10 — Step 068 Evidence

**Step:** "Create configurable visual density and focus budget"
**Spec:** `docs/phase-specs/PHASE-10.md` §6, Step 068 (lines 235-249) — the third of six numbered steps
of Phase 10 (066-071)
**Date:** 2026-10-16

## 1. Pre-implementation baseline inspection

- Read `docs/phase-specs/PHASE-10.md` in full, confirming Step 068's own text: required acceptance
  ("Clutter fixture is reduced while preserving focal musical event"), implementation guidance ("Build
  measurable visibility budget (concurrent focal count, event density, brightness proxy, rest fraction);
  define emergency over-budget resolution"), failure/edge case ("Invalid input, cancellation or stale
  persisted state must not corrupt the project"), and Section 3's own proposed `VisualBudget` signature
  (`MaxConcurrentFocalProps`, `MaxMeanBrightness`, `MaxEventDensityPerSecond`, `DarknessFractionTarget`).
- Read `implementation-evidence/PHASE-10/STEP-066.md` and `STEP-067.md` in full as the immediately preceding,
  already-accepted contract baseline. Step 067's own §7 explicitly disclosed: "No real
  `ICompositionPlanner`/`IChoreographyCompiler`/`VisualBudget`/`SectionVisualDirection` exists yet ...
  Steps 068-071" — confirming `VisualBudget` is genuinely this step's own new contract to create, not a
  pre-existing gap being reopened.
- Read every file in `src/XlightsAI.Studio.Composition/` in full (`MusicalRole.cs`, `PrecedenceTier.cs`,
  `MusicalRoleAssignment.cs`, `MusicalRoleAssignmentSet.cs`, `IMusicalRoleAssignmentCatalog.cs`/
  `MusicalRoleAssignmentCatalog.cs`, `IMusicalRoleAssignmentStore.cs`/`JsonMusicalRoleAssignmentStore.cs`,
  `IRolePrecedenceResolver.cs`/`RolePrecedenceResolver.cs`, `CompositionInvalidException.cs`,
  `CompositionCorruptException.cs`) before writing any new type, specifically to decide whether this step's
  own candidate-event type could reuse `MusicalRoleAssignment` directly rather than duplicate its
  `SourceTrackId`/`Role`/`PrecedenceTier`/`Priority` fields (it does — see §3).
- Read `src/XlightsAI.Studio.Shell/RoleRoutingEditorViewModel.cs`/`RoleRoutingRowView.cs` in full (the direct
  structural precedent for this step's own Shell-layer screen view model: explicit `ScreenLoadState`
  loading/empty/error/loaded machine, "catch the domain's typed invalid-input exception, set
  `ActionErrorMessage`, leave bindable state completely unchanged on failure" convention for every mutating
  action, `RaiseStateDerivedChanged` fan-out, UI-only row-projection record convention).
- Read `tests/XlightsAI.Studio.Shell.Tests/Step067RoleRoutingEditorViewModelTests.cs` and
  `tests/XlightsAI.Studio.Composition.Tests/Step066AcceptanceTests.cs` in full as the direct structural
  precedent for this step's own test style (real catalog/store/resolver instances, not mocks; before/after
  mutation-equality negative-path convention; restart-across-a-brand-new-instance persistence proof;
  shuffled-input-order determinism proof).
- Read `src/XlightsAI.Studio.Contracts/Identifiers.cs` to confirm `TimeWindow(StartTicks, EndTicks)` and that
  `TimelineTick` is 100 ns per tick (10,000,000 ticks/second) — the basis for this step's own
  seconds-based density/duration arithmetic.
- Searched the full repository for any prior "Step 068"/"STEP-068"/`VisualBudget`/`VisualBudgetResolver`
  reference: found only `PHASE-10.md` itself, confirming this is genuinely new work.
- Confirmed via `Glob` for `*.axaml`: only `App.axaml`/`MainWindow.axaml` exist anywhere in the repository —
  no SCREEN 12 CHOREOGRAPHY Avalonia view exists yet (unchanged, carried-forward condition from every prior
  Phase 09/10 UI-adjacent step; see §3/§7).
- Baseline taken by running the real, full solution build *before* any Step 068 code was written:
  `dotnet build XlightsAI.Studio.slnx --nologo` (clean, 0 warnings/errors), reconfirming Step 067's own
  closing state immediately before starting.

## 2. Scope actually implemented

**No file from Step 001-067 was behaviorally changed.** This step adds twelve new files to the existing
`src/XlightsAI.Studio.Composition/` project, two new files to the existing `src/XlightsAI.Studio.Shell/`
project, and two new test files. No existing Composition-, Display- or Shell-project file's own existing
members were modified.

### New files in `src/XlightsAI.Studio.Composition/`

- **`VisualBudget.cs`** — the proposed PHASE-10.md Section 3 signature, verbatim, field for field
  (`MaxConcurrentFocalProps`/`MaxMeanBrightness`/`MaxEventDensityPerSecond`/`DarknessFractionTarget`), plus a
  `Validate()` method rejecting negative/non-finite/out-of-`[0,1]` fields before any resolution or
  persistence is attempted.
- **`VisualBudgetCandidate.cs`** — one candidate visual gesture competing for budget admission: wraps an
  already-accepted `MusicalRoleAssignment` (Step 066's own contract, reused directly rather than
  duplicated) plus a `TimeWindow`, a `Brightness` proxy in `[0,1]`, and an `IsFocal` flag (whether this
  gesture competes for `MaxConcurrentFocalProps` at all).
- **`VisualBudgetDecision.cs`** — `Accepted`/`Trimmed`/`Dropped`, the direct realization of the core process
  flowchart's own named emergency step: "Drop/shorten lower priority support."
- **`VisualBudgetCandidateResult.cs`** / **`VisualBudgetResolution.cs`** — one candidate's own resolved
  outcome, and the full resolution report (every result, plus observed mean brightness/event
  density/peak concurrent focal count/darkness fraction and non-fatal warnings).
- **`IVisualBudgetResolver.cs`** / **`VisualBudgetResolver.cs`** — the measurable-budget enforcer. Pure and
  deterministic (constructor-injected `IRolePrecedenceResolver`, no I/O, no mutable state): orders candidates
  by the already-accepted `IRolePrecedenceResolver` rule, then for each candidate (highest precedence first)
  checks the focal-concurrency cap (trimming the candidate's own window to the largest non-saturated
  sub-interval when possible, dropping it entirely when no such sub-interval exists), the event-density cap
  (admitted-count ÷ window-duration-in-seconds), and the mean-brightness cap (running admitted mean) — a
  lower-precedence candidate is always the one trimmed/dropped when two compete, never a higher-precedence
  one already admitted. Reports observed darkness-fraction-vs-target as a non-fatal warning (resolving
  darkness further is Step 070's own later "build, silence, transitions" scope, not this step's).
- **`VisualBudgetPreset.cs`** / **`IVisualBudgetPresetStore.cs`** / **`JsonVisualBudgetPresetStore.cs`** /
  **`IVisualBudgetPresetCatalog.cs`** / **`VisualBudgetPresetCatalog.cs`** — the named, persisted
  budget-preset boundary realizing the data-contract rule's own "visual budget presets" (PHASE-10.md §4).
  Mirrors `IMusicalRoleAssignmentStore`/`JsonMusicalRoleAssignmentStore`'s own atomic-write convention
  exactly, keyed by a validated preset name (letters/digits/`-`/`_`/space only) instead of a `PhraseId`.
  `VisualBudgetPresetCatalog.SaveAsync` validates the name and the budget *before* any load/write, and
  increments `Version` on every save of the same name.

### New files in `src/XlightsAI.Studio.Shell/`

- **`VisualBudgetCandidateRowView.cs`** — UI-only bindable projection of one `VisualBudgetCandidateResult`
  for SCREEN 12 CHOREOGRAPHY's budget-preview table. Same "Avalonia-free Shell layer projects pure domain
  data into something directly bindable" convention `RoleRoutingRowView` already established.
- **`VisualBudgetEditorViewModel.cs`** — SCREEN 12 CHOREOGRAPHY's own bindable visual-budget editor/preview
  view model, the direct UI layer over this step's own `IVisualBudgetPresetCatalog`/`IVisualBudgetResolver`:
  - `LoadState`/`IsEmpty`/`IsError`/`IsLoaded` — the same explicit loading/empty/error/loaded state machine
    `RoleRoutingEditorViewModel` already established.
  - `PresetName`/`Version`/`CurrentBudget` — identity/version/value of the preset currently open
    (`CurrentBudget` is `DefaultBudget`, a permissive built-in starting value, when nothing has ever been
    saved under the opened name).
  - `OpenPresetAsync(catalog, presetName, ct)` — loads (or switches to) a named preset; calling it again
    with a different name loads that name's own independently saved budget.
  - `SaveAsync(catalog, budget, ct)` — delegates to `IVisualBudgetPresetCatalog.SaveAsync`; rejects with
    `ActionErrorMessage` (never a partial mutation of `CurrentBudget`) when no preset is open or the catalog
    rejects the budget.
  - `Preview(resolver, candidates, evaluationWindow)` — delegates to `IVisualBudgetResolver.Resolve` against
    `CurrentBudget`, publishing `Rows`/`Warnings`/`ObservedMeanBrightness`/`ObservedEventDensityPerSecond`/
    `ObservedPeakConcurrentFocalProps`/`ObservedDarknessFraction`; rejects with `ActionErrorMessage` (never a
    partial `Rows`) when no preset is open or the resolver rejects the inputs.

### New test files

- **`tests/XlightsAI.Studio.Composition.Tests/Step068AcceptanceTests.cs`** (14 tests) — real
  `VisualBudgetResolver`/`RolePrecedenceResolver`/`VisualBudgetPresetCatalog`/`JsonVisualBudgetPresetStore`
  (not mocks):
  - **`Step068_Acceptance_ClutterFixtureIsReducedWhilePreservingFocalEvent`** — the required acceptance
    test, verbatim. A lead piano (focal, full 10-second window) plus a fully-overlapping lower-precedence
    focal competitor plus ten low-priority "clutter" candidates, resolved under a tight budget. Proves: the
    lead is `Accepted` unmodified; the fully-overlapping competitor is `Dropped`; exactly 4 of the 10 clutter
    candidates survive the density cap (clutter genuinely reduced, not eliminated or fully admitted); the
    result is identical (by decision, per track id) regardless of input order.
  - **`Resolve_PartiallyOverlappingLowerPrecedenceFocal_IsTrimmedNotDropped`** — proves the "shorten" half of
    "Drop/shorten lower priority support" specifically: a partially-overlapping lower-precedence focal
    candidate is trimmed to its own exact non-saturated remainder (hand-verified sub-interval), not dropped.
  - **`Resolve_InvalidBudget_ThrowsBeforeAnyComputation_NegativePath`**,
    **`Resolve_InvalidEvaluationWindow_Throws_NegativePath`**,
    **`Resolve_CandidateWithOutOfRangeBrightness_Throws_NegativePath`**,
    **`Resolve_CandidateWithUnrecognizedPrecedenceTier_Throws_NegativePath`**,
    **`VisualBudget_Validate_RejectsOutOfRangeFields_NegativePath`** (4 inline cases).
  - **`VisualBudgetPresetCatalog_SaveAndReload_PersistsAcrossRestart`** — saved-state verification through a
    brand-new catalog/store instance.
  - **`VisualBudgetPresetCatalog_InvalidInput_RejectsBeforeAnyWrite_NegativePath`**,
    **`VisualBudgetPresetCatalog_Cancelled_DoesNotPersist_NegativePath`**,
    **`VisualBudgetPresetStore_LoadAsync_CorruptFile_ThrowsCorruptException_NegativePath`**.
- **`tests/XlightsAI.Studio.Shell.Tests/Step068VisualBudgetEditorViewModelTests.cs`** (7 tests) — real
  `VisualBudgetEditorViewModel`/`VisualBudgetPresetCatalog`/`JsonVisualBudgetPresetStore`/`VisualBudgetResolver`
  (not mocks) under a temporary directory:
  - **`Step068_Acceptance_PreviewReducesClutterWhilePreservingFocalEvent`** — the required acceptance text
    proven again at the bindable view-model layer (`Preview`/`Rows`).
  - **`VisualBudgetPreset_PersistsAndCanChangePerPresetName_SavedStateAcrossRestart`** — two independently
    named presets (Verse/Chorus), each independently saved, reloaded through a brand-new
    view-model/catalog instance.
  - **`SaveAsync_NothingOpen_SetsActionErrorAndLeavesBudgetUnchanged_NegativePath`**,
    **`SaveAsync_InvalidBudget_RejectsWithoutPartialMutation_NegativePath`**,
    **`Preview_NothingOpen_SetsActionErrorAndLeavesRowsEmpty_NegativePath`**,
    **`Preview_InvalidCandidate_RejectsWithoutPartialRows_NegativePath`**,
    **`OpenPresetAsync_CatalogRejectsUnsafeName_SetsErrorAndIsError_NegativePath`**.

## 3. Architecture deviations / decisions

- **`VisualBudgetCandidate` wraps `MusicalRoleAssignment` rather than defining its own
  `SourceTrackId`/`Role`/`PrecedenceTier`/`Priority` fields, and `VisualBudgetResolver` consumes the
  already-accepted `IRolePrecedenceResolver` directly (constructor-injected) for its own admission order.**
  This is a genuine, load-bearing architectural choice: PHASE-10.md Section 3's own proposed
  `PlannedGesture(Guid Id, MusicalRole Role, string GestureType, TimeWindow Window, IReadOnlyList<ModelId>
  Targets, string RecipeId)` was considered and deliberately **not** used as this step's own candidate type,
  because `Targets`/`RecipeId` tie a gesture to concrete xLights models and a validated effect recipe —
  Step 071's own later, disclosed scope ("Compose and resolve overlapping effects with layer/priority
  strategies... Choreography compilation uses only verified recipes and targetable props"). Introducing
  `PlannedGesture` now would have required either fabricating an unvalidated `RecipeId`/`ModelId` concept
  this step has no business defining, or leaving those fields meaninglessly unused. `VisualBudgetCandidate`
  is scoped exactly to what a measurable visibility budget needs to decide admission (identity/precedence,
  timing, brightness, focal-ness) and nothing a later step owns.
- **Brightness-cap and density-cap over-budget resolution always drops, never trims.** The flowchart's own
  named remedy is "Drop/shorten," and this step's own resolver uses *both* verbs, but only for the
  focal-concurrency cap (where a window is a continuous resource that can genuinely be sub-divided and still
  look right) — not for density (an event-rate count a shorter window cannot reduce: shortening a gesture's
  own window does not reduce the *count* of admitted events sharing a time span) or mean brightness (an
  intrinsic property of the effect itself; reducing a candidate's own `Brightness` without changing what
  effect it represents is not a resolution this step owns — that is implicitly Step 071's own later effect
  *compilation* scope, not this step's measurable-budget scope). This is disclosed here as the chosen,
  narrower reading of "shorten" rather than an omission.
- **`DarknessFractionTarget` is a reported goal, not an admission cap.** `VisualBudgetResolver` never drops
  or trims a candidate because darkness is insufficient — it only reports `ObservedDarknessFraction` and
  emits a non-fatal warning when it falls short of the target. PHASE-10.md's own implementation guidance
  lists "rest fraction" as one of four things to *measure*, and Step 070 ("Implement build, silence,
  transitions, release and phrase-ending recipes... Musical break can intentionally darken all permitted
  props") is the step that actually owns *creating* intentional darkness; this step only measures and
  surfaces the gap.
- **Preset persistence is keyed by a caller-chosen, validated name string, not a `PhraseId`.** Unlike Step
  066/067's `MusicalRoleAssignmentSet` (keyed per phrase/section, because role routing is inherently
  per-section), a visual budget preset is a reusable style choice (e.g. "Verse"/"Chorus"/"Finale") that is
  not itself scoped to one specific phrase instance — the same "Verse" preset is meant to apply to every
  verse section in a song. `VisualBudgetPresetCatalog.SaveAsync`'s own name validation (letters/digits/`-`/
  `_`/space) keeps the on-disk filename deterministic and safe without introducing a second id type.
- **No real `ICompositionPlanner`/`IChoreographyCompiler`/`SectionVisualDirection`/`ChoreographyPlan` exists
  yet.** `PHASE-10.md` Section 3's own proposed signatures for those contracts remain later steps' own scope
  (Steps 069-071, per Step 067's own already-disclosed §7); this step only adds the measurable visual
  budget + emergency resolution + preset UI layer.
- **No real SCREEN 12 CHOREOGRAPHY Avalonia/AXAML view exists.** Unchanged, carried-forward disclosure from
  every prior Phase 09/10 step (confirmed again via a full repository `Glob` for `*.axaml`: still only
  `App.axaml`/`MainWindow.axaml`). This step's own required evidence text is narrower than a literal pixel
  claim — "Avalonia screenshot **or** repeatable desktop interaction recording with saved-state
  verification" — and is satisfied here by the real, non-test-harness demo in §5 below, the same precedent
  every prior UI-adjacent Phase 09/10 step's evidence already established for this exact same project-wide
  gap.
- **Per the user's own standing waiver** (`memory: feedback_skip_xlights_human_verification`), no live
  xLights render/desktop-interaction check was attempted — this step's own required acceptance text is a
  pure budget-measurement/resolution/persistence claim (no XSQ/effect-recipe output exists yet at this point
  in Phase 10), proven directly by the automated tests and demo below.

## 4. Commands actually executed

All run via the PowerShell tool — the sandboxed Bash tool in this session again returned bare exit code 1
with no output for plain commands (confirmed again at the very start of this step with `git status`/`pwd`),
the same pre-existing environment limitation every prior step's own evidence has recorded; PowerShell was
used for every `dotnet`/file-system command below without incident.

```
# Baseline, before any Step 068 code existed:
dotnet build XlightsAI.Studio.slnx --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s). (Step 067's own closing state)

# After adding the twelve new Composition files and two new Shell files:
dotnet build src\XlightsAI.Studio.Composition\XlightsAI.Studio.Composition.csproj --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).
dotnet build src\XlightsAI.Studio.Shell\XlightsAI.Studio.Shell.csproj --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).
dotnet build XlightsAI.Studio.slnx --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).

dotnet test tests\XlightsAI.Studio.Composition.Tests\XlightsAI.Studio.Composition.Tests.csproj --nologo --filter "FullyQualifiedName~Step068"
  # Passed! - Failed: 0, Passed: 14, Skipped: 0, Total: 14, Duration: 277 ms

dotnet test tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj --nologo --filter "FullyQualifiedName~Step068"
  # Passed! - Failed: 0, Passed: 7, Skipped: 0, Total: 7, Duration: 293 ms

dotnet format XlightsAI.Studio.slnx --verify-no-changes --no-restore
  # No output (clean) -- matches the repository's own CI formatting gate, checked across the FULL solution
  # this time (not just the changed projects), reconfirmed a second time after the manual demo.

dotnet test XlightsAI.Studio.slnx --nologo --filter "FullyQualifiedName!~Step025&FullyQualifiedName!~Step028&FullyQualifiedName!~Step029&FullyQualifiedName!~Step030&FullyQualifiedName!~Step042"
dotnet test tests\XlightsAI.Studio.Stems.Tests\XlightsAI.Studio.Stems.Tests.csproj --nologo --filter "FullyQualifiedName~Step042"
  # Passed! - Failed: 0, Passed: 8, Skipped: 0, Total: 8, Duration: 384 ms
  # (the blunt numeric filter above also excludes Stems.Tests' own unrelated, legitimate
  # Step042AcceptanceTests.cs -- the same filter side effect disclosed in every prior Phase 09/10 step's own
  # evidence; reconfirmed here passing in isolation. Note: the main filtered run's own Stems.Tests.dll total
  # of 93 already includes these 8 -- only Shell's distinctly-named Step042ShellWiringTests is excluded by
  # the filter, unchanged from Step 067's own disclosure.)

# Real, non-test-harness manual demo (built under the session scratchpad at %TEMP%\xlai-step068-demo-proj,
# deleted after this run):
dotnet run --project <scratchpad>\xlai-step068-demo-proj
```

### Targeted test results (Step 068's own 21 new tests, real run)

```
Step068AcceptanceTests (Composition, 14 new):
  Step068_Acceptance_ClutterFixtureIsReducedWhilePreservingFocalEvent          Passed
  Resolve_PartiallyOverlappingLowerPrecedenceFocal_IsTrimmedNotDropped         Passed
  Resolve_InvalidBudget_ThrowsBeforeAnyComputation_NegativePath                Passed
  Resolve_InvalidEvaluationWindow_Throws_NegativePath                          Passed
  Resolve_CandidateWithOutOfRangeBrightness_Throws_NegativePath                 Passed
  Resolve_CandidateWithUnrecognizedPrecedenceTier_Throws_NegativePath           Passed
  VisualBudget_Validate_RejectsOutOfRangeFields_NegativePath (x4 inline cases)  Passed
  VisualBudgetPresetCatalog_SaveAndReload_PersistsAcrossRestart                 Passed
  VisualBudgetPresetCatalog_InvalidInput_RejectsBeforeAnyWrite_NegativePath     Passed
  VisualBudgetPresetCatalog_Cancelled_DoesNotPersist_NegativePath               Passed
  VisualBudgetPresetStore_LoadAsync_CorruptFile_ThrowsCorruptException_NegativePath Passed
Composition.Tests total (Step 068 slice): 14/14 passed, 0 failed, 0 skipped.

Step068VisualBudgetEditorViewModelTests (Shell, 7 new):
  Step068_Acceptance_PreviewReducesClutterWhilePreservingFocalEvent            Passed
  VisualBudgetPreset_PersistsAndCanChangePerPresetName_SavedStateAcrossRestart Passed
  SaveAsync_NothingOpen_SetsActionErrorAndLeavesBudgetUnchanged_NegativePath   Passed
  SaveAsync_InvalidBudget_RejectsWithoutPartialMutation_NegativePath           Passed
  Preview_NothingOpen_SetsActionErrorAndLeavesRowsEmpty_NegativePath           Passed
  Preview_InvalidCandidate_RejectsWithoutPartialRows_NegativePath              Passed
  OpenPresetAsync_CatalogRejectsUnsafeName_SetsErrorAndIsError_NegativePath    Passed
Shell.Tests total (Step 068 slice): 7/7 passed, 0 failed, 0 skipped.
```

### Full regression (final, after all Step 068 implementation and tests, real-audio-device Shell tests excluded — unchanged pre-existing condition, see Step 064/065/066/067's own §6)

```
Passed! - XlightsAI.Studio.Audio.Tests.dll           23/23
Passed! - XlightsAI.Studio.Xlights.Tests.dll         74/74
Passed! - XlightsAI.Studio.Editing.Tests.dll         83/83
Passed! - XlightsAI.Studio.Melody.Tests.dll         117/117
Passed! - XlightsAI.Studio.Display.Tests.dll         56/56
Passed! - XlightsAI.Studio.Structure.Tests.dll       78/78
Passed! - XlightsAI.Studio.Composition.Tests.dll     23/23 (9 Step 066 + 14 new Step 068)
Passed! - XlightsAI.Studio.Projects.Tests.dll        25/25
Passed! - XlightsAI.Studio.Shell.Tests.dll          146/146 (139 Step 001-067 + 7 new Step 068; real-audio-device tests excluded, unchanged)
Passed! - XlightsAI.Studio.Rhythm.Tests.dll          82/82
Passed! - XlightsAI.Studio.Reference.Tests.dll       31/31
Passed! - XlightsAI.Studio.Stems.Tests.dll           93/93  (+8 Step042AcceptanceTests reconfirmed separately, below)
```

**839 total passing** (818 carried forward from Step 067's own closing count, unmodified, + this step's own
14 new Composition tests + 7 new Shell tests = 21), 0 failed, 0 skipped. Every pre-existing test from Steps
001-067 still passes unmodified; the only additions are `Step068AcceptanceTests` (14/14) and
`Step068VisualBudgetEditorViewModelTests` (7/7).

## 5. Persisted-output inspection / observable demonstration for this step

A real, non-test-harness console program (not part of the repository, built under the session scratchpad at
`%TEMP%\xlai-step068-demo-proj`, deleted after this run) exercises the real
`VisualBudgetEditorViewModel`/`VisualBudgetPresetCatalog`/`JsonVisualBudgetPresetStore`/`VisualBudgetResolver`
directly — no test harness, no mocks. It deliberately mirrors the phase gate's own named worked example
("preview verse vs louder chorus, show fewer active props in verse") over a 20-second excerpt with a lead
piano, a bass, and sixteen kick/snare accent hits:

```
dotnet run --project <scratchpad>\xlai-step068-demo-proj
```

Observed real output:

```
=== Step 068 manual demo: real visual budget UI view model, no test harness ===

--- Scenario 1: save a tight 'Verse' budget (fewer active focal props, quieter) ---
  Verse preset saved=True, version=0

--- Scenario 2: save a looser 'Chorus' budget (more active props, louder) ---
  Chorus preset saved=True, version=0

--- Scenario 3: preview the SAME 20-second excerpt's gestures against the verse budget ---
  Verse: 2/18 gestures active (fewer active props); lead piano decision=Accepted
  Verse observed mean brightness=0.5, density/sec=0.1, peak focal=1, darkness=0
    [warning] Observed darkness fraction 0 is below the target 0.4.

--- Scenario 4: preview the SAME excerpt's gestures against the chorus budget (louder) ---
  Chorus: 18/18 gestures active (more active props); lead piano decision=Accepted
  Chorus observed mean brightness=0.5, density/sec=0.9, peak focal=2, darkness=0
    [warning] Observed darkness fraction 0 is below the target 0.05.

  Verse active (2) < Chorus active (18): True  -- "show fewer active props in verse", verbatim.
  Lead piano (the focal musical event) preserved in BOTH: verse=True, chorus=True

--- Scenario 5: restart (brand-new view-model/catalog instances over the same on-disk directory) ---
  Reloaded Verse budget: MaxConcurrentFocalProps=1 (expected 1)
  Reloaded Chorus budget: MaxConcurrentFocalProps=4 (expected 4)

--- Scenario 6: invalid input is rejected and never corrupts the project (negative path) ---
  Invalid budget save rejected: True, error="MaxConcurrentFocalProps must not be negative (was -1)."
  Budget unchanged after rejection: True
  Unsafe preset name rejected: True, error="Preset name 'bad/name' must be non-blank and contain only letters, digits, '-', '_' or ' '."

Step 068 manual demo completed successfully.

Cleaning up demo cache directory.
```

Direct, real, non-test-harness confirmation of this step's own required text: (1) "Clutter fixture is
reduced while preserving focal musical event" — Scenario 3 shows only 2 of 18 candidate gestures active
under the tight Verse budget (the lead piano plus the non-focal bass; every accent hit is dropped by the
focal-concurrency cap), with the lead piano itself always `Accepted`. (2) "preview verse vs louder chorus,
show fewer active props in verse" (the phase gate's own named worked example) — Scenario 4 shows 18/18
active under the looser Chorus budget, strictly more than Verse's 2/18, while the lead piano remains
`Accepted` in both. (3) "Build measurable visibility budget (concurrent focal count, event density,
brightness proxy, rest fraction)" — both scenarios report all four measurements
(`ObservedPeakConcurrentFocalProps`/`ObservedEventDensityPerSecond`/`ObservedMeanBrightness`/
`ObservedDarknessFraction`) plus a non-fatal darkness-shortfall warning. (4) Saved-state verification —
Scenario 5 proves both named presets' own independently saved budgets survive a real restart through
brand-new view-model/catalog instances. (5) "Invalid input ... must not corrupt the project" — Scenario 6
confirms an invalid budget and an unsafe preset name are both rejected, with no mutation of the already-saved
Verse budget.

## 6. Fixtures / input hashes

- No show-layout XML fixture was needed for this step's own tests or demo — `VisualBudgetCandidate` operates
  on caller-supplied synthetic `MusicalRoleAssignment`s with arbitrary `SpatialPathId` `Guid`s (this step's
  own resolver never validates path existence; that remains `IMusicalRoleAssignmentCatalog`'s own job,
  already proven in Step 066/067), consistent with this step's own narrow, disclosed scope (budget
  measurement/resolution, not path routing).
- The real demo's own persisted `VisualBudgetPreset` JSON files (one per preset name: "Verse"/"Chorus") were
  written to a temporary directory for inspection and deleted by the demo's own `finally` block (confirmed by
  "Cleaning up demo cache directory." in the real output above); the throwaway demo *project* itself was
  deleted after this evidence was recorded.
- **Real-audio-device test exclusion, disclosed (unchanged, carried forward from every prior Phase 09/10
  step's own §6):** `Step025`/`Step028`/`Step029`/`Step030`/`Step042ShellWiringTests` construct a real
  `WaveOutAudioTransport` (the real Windows winmm audio device) unavailable in this session's own sandboxed
  execution environment — a pre-existing, environment-caused condition predating this step entirely. No file
  this step touched is on the call path of any of those five classes (confirmed: none reference
  `VisualBudgetEditorViewModel`/`VisualBudgetCandidateRowView`/`VisualBudgetResolver`/`VisualBudgetPresetCatalog`
  in any way).

## 7. Unresolved issues / known gaps

- **No real SCREEN 12 CHOREOGRAPHY Avalonia/AXAML view or Shell-layer `ShellScreen` wiring exists** —
  disclosed in §3; this step's own required evidence ("Avalonia screenshot **or** repeatable desktop
  interaction recording with saved-state verification") is proven at the bindable view-model layer plus the
  real, non-test-harness demo in §5, not by a pixel-rendering screenshot. Every other screen in this
  repository has the identical gap (confirmed via a full repository `Glob` for `*.axaml` — still only
  `App.axaml`/`MainWindow.axaml`) — this is the project's own pre-existing, consistent convention, not a
  shortcut unique to this step.
- **`VisualBudgetResolver`'s own density/brightness over-budget resolution always drops, never trims** —
  disclosed in §3 as a deliberate, narrower reading of "shorten" (only the focal-concurrency cap trims);
  a later step could revisit this if a real `IChoreographyCompiler` later needs a different trim strategy
  for density/brightness specifically.
- **No real `ICompositionPlanner`/`IChoreographyCompiler`/`ChoreographyPlan`/`SectionVisualDirection`
  exists yet** — `PHASE-10.md` Section 3's own proposed signatures for those contracts are later steps' own
  scope (Steps 069-071); this step only adds the measurable-budget/emergency-resolution/preset-UI layer that
  those later steps will consume.
- **Carried-forward, disclosed phase-level/dependency-level gaps are unchanged from Step 067's own closing
  evidence** (not reopened or re-resolved here): Phase 09's own consolidated `GATE-REPORT.md` is still
  unwritten; Phase 06's own stem-separation gate remains BLOCKED; Phase 07's and Phase 08's own full-phase
  gates have not been separately run/recorded; real-audio-device Shell tests remain environment-blocked
  (§6).
- **Per the user's own standing waiver** (`memory: feedback_skip_xlights_human_verification`), the
  real-xLights render/desktop-interaction check is not treated as a blocker for this step's own scope — no
  XSQ/effect-recipe output exists yet at this point in Phase 10 for such a check to apply to.

## 8. PASS / BLOCKED

**Status: PASS** (for this step's own narrow, disclosed scope — a measurable visual density/focus budget
with emergency over-budget resolution, plus a bindable preset editor/preview UI over it — not a claim that
any carried-forward phase-level gap listed in §7 is resolved, and not a claim that a real SCREEN 12 Avalonia
view, `ICompositionPlanner`, or effect-layer collision resolution — later Phase 10 steps' own scope — is
complete).

- "Clutter fixture is reduced while preserving focal musical event" (required acceptance, verbatim) — proven
  by `Step068_Acceptance_ClutterFixtureIsReducedWhilePreservingFocalEvent` (domain) and
  `Step068_Acceptance_PreviewReducesClutterWhilePreservingFocalEvent` (view model) — independently confirmed
  end-to-end by the real demo's Scenarios 3 and 4 (2/18 active in Verse vs. 18/18 in Chorus, lead piano
  `Accepted` in both).
- "Build measurable visibility budget (concurrent focal count, event density, brightness proxy, rest
  fraction); define emergency over-budget resolution" (implementation guidance, verbatim) — proven by
  `VisualBudgetResolution`'s own four observed metrics plus `Resolve_PartiallyOverlappingLowerPrecedenceFocal_IsTrimmedNotDropped`
  (the "shorten" half) and the clutter-fixture test's own `Dropped` fully-saturating competitor (the "drop"
  half) — independently confirmed by the real demo's own printed metrics and warnings in Scenarios 3-4.
- "Invalid input, cancellation or stale persisted state must not corrupt the project" (failure/edge case,
  verbatim) — proven by `Resolve_InvalidBudget_ThrowsBeforeAnyComputation_NegativePath`,
  `Resolve_InvalidEvaluationWindow_Throws_NegativePath`,
  `Resolve_CandidateWithOutOfRangeBrightness_Throws_NegativePath`,
  `Resolve_CandidateWithUnrecognizedPrecedenceTier_Throws_NegativePath`,
  `VisualBudget_Validate_RejectsOutOfRangeFields_NegativePath`,
  `VisualBudgetPresetCatalog_InvalidInput_RejectsBeforeAnyWrite_NegativePath`,
  `VisualBudgetPresetCatalog_Cancelled_DoesNotPersist_NegativePath`,
  `SaveAsync_NothingOpen_SetsActionErrorAndLeavesBudgetUnchanged_NegativePath`,
  `SaveAsync_InvalidBudget_RejectsWithoutPartialMutation_NegativePath`,
  `Preview_NothingOpen_SetsActionErrorAndLeavesRowsEmpty_NegativePath`,
  `Preview_InvalidCandidate_RejectsWithoutPartialRows_NegativePath`, and
  `OpenPresetAsync_CatalogRejectsUnsafeName_SetsErrorAndIsError_NegativePath` — independently confirmed by
  the real demo's Scenario 6 (invalid budget and unsafe preset name both rejected, prior saved budget
  unchanged).
- Full regression (real-audio-device tests excluded, disclosed in full in §6): **839/839 passing**, 0
  failed, 0 skipped; every pre-existing test from Phase 01-10/Step 067 still passes unmodified, plus this
  step's own 14 new Composition tests + 7 new Shell tests. `dotnet format --verify-no-changes` clean across
  the full solution.
- **No file from any prior step/phase was behaviorally changed** — this step only adds twelve new files to
  `XlightsAI.Studio.Composition` and two new files to `XlightsAI.Studio.Shell`, plus two new test files. No
  existing Composition-, Display- or Shell-project file's own existing members were modified.
- **Carried-forward, disclosed phase-level/dependency-level gaps (not this step's own blocker):** no real
  SCREEN 12 Avalonia view exists yet (§7, project-wide pre-existing condition); Phase 09's own consolidated
  gate report is still unwritten; Phase 06's own gate remains BLOCKED; Phase 07's and Phase 08's own
  full-phase gates have not been separately run/recorded; real-audio-device Shell tests are
  environment-blocked (§6); no real `ICompositionPlanner`/`IChoreographyCompiler`/`ChoreographyPlan` exists
  yet (§7 — later Phase 10 steps' own scope).

Per this step's own closing instruction ("do not execute the next step until this step is proven"), Step 069
("Build sectional energy and color/brightness story") should not begin until this evidence is reviewed and
accepted.
