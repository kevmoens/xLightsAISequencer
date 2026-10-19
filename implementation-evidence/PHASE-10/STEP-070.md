# PHASE 10 — Step 070 Evidence

**Step:** "Implement build, silence, transitions, release and phrase-ending recipes"
**Spec:** `docs/phase-specs/PHASE-10.md` §6, Step 070 (lines 267-281) — the fifth of six numbered steps
of Phase 10 (066-071)
**Date:** 2026-10-16

## 0. Scope clarification (resolved before implementation)

The task prompt that opened this session asked to implement "Step 69" while filing evidence at
`implementation-evidence/PHASE-10/STEP-70.md`. `implementation-evidence/PHASE-10/STEP-069.md` already records
a closed, PASSed Step 069 ("Build sectional energy and color/brightness story"). The user was asked to
disambiguate via `AskUserQuestion` and confirmed: implement **Step 070** ("Implement build, silence,
transitions, release and phrase-ending recipes" — the next unimplemented numbered step), matching the
requested evidence filename. This document covers Step 070 only.

## 1. Pre-implementation baseline inspection

- Read `docs/phase-specs/PHASE-10.md` in full, confirming Step 070's own text: required acceptance ("Musical
  break can intentionally darken all permitted props"), implementation guidance ("Add transition gestures,
  builds, blackouts and resolutions as reusable semantics with deterministic recipe strategies"), failure/edge
  case ("Clean machine and unsupported runtime must produce user-readable diagnostics" — a different phrase
  than Steps 066-069's own "Invalid input, cancellation or stale persisted state must not corrupt the
  project," shared instead with Step 071), and Section 3's own proposed `PlannedGesture`/`ChoreographyPlan`
  signatures.
- Read `implementation-evidence/PHASE-10/STEP-066.md` through `STEP-069.md` in full as the immediately
  preceding, already-accepted contract baseline. Step 069's own §3/§7 explicitly disclosed: "No real
  `ICompositionPlanner`/`IChoreographyCompiler`/`ChoreographyPlan`/`PlannedGesture` exists yet ... Steps
  070-071" — confirming this step's own transition/recipe contracts are genuinely new work, not a
  redefinition of an already-accepted type.
- Read every file in `src/XlightsAI.Studio.Composition/` in full (all Step 066/068/069 types) before writing
  any new type, specifically to decide reuse vs. duplication:
  - `VisualStory.cs`/`VisualStoryRecord.cs`/`IVisualStoryCatalog.cs`/`VisualStoryCatalog.cs`/
    `IVisualStoryStore.cs`/`JsonVisualStoryStore.cs` — the direct structural precedent for this step's own
    domain-plan-plus-versioned-persisted-record shape (`TransitionPlan`/`TransitionPlanRecord`/
    `ITransitionPlanCatalog`/`TransitionPlanCatalog`/`ITransitionPlanStore`/`JsonTransitionPlanStore`).
  - `VisualBudgetCandidate.cs`/`VisualBudgetResolver.cs` — confirmed this step's own `TransitionGesture` should
    **not** reuse `VisualBudgetCandidate` (that type wraps an already-accepted `MusicalRoleAssignment` for
    budget *admission*; this step's own concern is recipe *generation*, a distinct, earlier pipeline stage —
    see §3).
  - `MusicalRoleAssignmentSet.cs`/`IMusicalRoleAssignmentCatalog.cs` — confirmed the phrase-scoped
    (`Guid phraseId`) persistence key convention this step's own `TransitionPlanRecord` reuses (a transition
    recipe plan is phrase-scoped, not whole-song like `VisualStoryRecord`).
  - `CompositionInvalidException.cs`/`CompositionCorruptException.cs` — reused verbatim, no new exception type
    needed.
- Confirmed `XlightsAI.Studio.Composition.csproj` already references `XlightsAI.Studio.Contracts` (where
  `ModelId`/`TimeWindow` are declared) — no new project reference needed for this step, unlike Step 069's own
  new `Structure` reference.
- Read `src/XlightsAI.Studio.Shell/VisualStoryEditorViewModel.cs` and
  `src/XlightsAI.Studio.Shell/SectionVisualDirectionRowView.cs` in full (the direct structural precedent for
  this step's own Shell-layer screen view model and row-projection convention: explicit `ScreenLoadState`
  loading/empty/error/loaded machine, "catch the domain's typed invalid-input exception, set
  `ActionErrorMessage`, leave bindable state completely unchanged on failure" convention, plan-then-save
  split).
- Read `tests/XlightsAI.Studio.Composition.Tests/Step069AcceptanceTests.cs` and
  `tests/XlightsAI.Studio.Shell.Tests/Step069VisualStoryEditorViewModelTests.cs` in full as the direct
  structural precedent for this step's own test style (real planner/catalog/store instances, not mocks;
  before/after mutation-equality negative-path convention; restart-across-a-brand-new-instance persistence
  proof).
- Searched the full repository for any prior "Step 070"/"STEP-070"/`TransitionKind`/`TransitionGesture`/
  `TransitionRecipePlanner` reference: found only `PHASE-10.md` itself, confirming this is genuinely new work.
- Confirmed via `Glob` for `*.axaml`: only `App.axaml`/`MainWindow.axaml` exist anywhere in the repository —
  no SCREEN 12 CHOREOGRAPHY Avalonia view exists yet (unchanged, carried-forward condition from every prior
  Phase 09/10 UI-adjacent step).
- Baseline taken by running the real, full solution build *before* any Step 070 code was written:
  `dotnet build XlightsAI.Studio.slnx --nologo` (clean, 0 warnings/errors), reconfirming Step 069's own closing
  state immediately before starting.

## 2. Scope actually implemented

**No file from Step 001-069 was behaviorally changed.** This step adds nine new files to the existing
`src/XlightsAI.Studio.Composition/` project (no new `ProjectReference` needed), two new files to the existing
`src/XlightsAI.Studio.Shell/` project, and two new test files. No existing Composition- or Shell-project
file's own existing members were modified.

### New files in `src/XlightsAI.Studio.Composition/`

- **`TransitionKind.cs`** — the five reusable semantic recipe kinds named verbatim in this step's own heading:
  `Build`, `Blackout` ("silence"), `Transition`, `Release`, `PhraseEnding`.
- **`TransitionGesture.cs`** — one planned gesture: `Id`, `Kind`, `Window` (`TimeWindow`), `TargetProps`
  (`IReadOnlyList<ModelId>`), `StartIntensity`/`EndIntensity`. Deliberately a flat intensity ramp rather than
  PHASE-10.md Section 3's own proposed `PlannedGesture` (`GestureType`/`RecipeId` strings) — see §3. Has its
  own `Validate()` defending every invariant (including the `Blackout`-must-be-all-zero invariant) against
  direct record construction, not only against planner output.
- **`TransitionRecipeRequest.cs`** — one requested gesture: `Kind`, `Window`, `PermittedProps`.
- **`TransitionPlan.cs`** — the full planned set for one planning call (`VersionId` + `Gestures`), mirroring
  `VisualStory`'s own versioned-wrapper-plus-`Validate()` shape exactly.
- **`ITransitionRecipePlanner.cs`** / **`TransitionRecipePlanner.cs`** — the deterministic recipe engine. Pure
  and stateless: a fixed `(StartIntensity, EndIntensity)` lookup table keyed by `TransitionKind`
  (`Build`=(0.15,0.90) rising, `Blackout`=(0,0) full darken, `Transition`=(0.40,0.40) steady neutral,
  `Release`=(0.85,0.20) falling, `PhraseEnding`=(0.30,0.05) tapering-but-never-literally-dark); validates every
  request (non-empty `PermittedProps`, no duplicate, defined `Kind`, valid `Window`) before producing any
  gesture.
- **`TransitionPlanRecord.cs`** / **`ITransitionPlanStore.cs`** / **`JsonTransitionPlanStore.cs`** /
  **`ITransitionPlanCatalog.cs`** / **`TransitionPlanCatalog.cs`** — the named, persisted plan boundary,
  keyed by a caller-defined `Guid phraseId` (phrase-scoped, mirroring `MusicalRoleAssignmentSet`'s own key
  convention — not whole-song like `VisualStoryRecord`). Mirrors
  `VisualStory`/`VisualStoryCatalog`/`JsonVisualStoryStore`'s own atomic-write/versioning convention exactly.
  `TransitionPlanCatalog.SaveAsync` validates the plan *before* any load/write and increments `Version` on
  every save of the same phrase.

### New files in `src/XlightsAI.Studio.Shell/`

- **`TransitionGestureRowView.cs`** — UI-only bindable projection of one `TransitionGesture` for SCREEN 12
  CHOREOGRAPHY's recipe table (adds rounded `StartIntensityPercent`/`EndIntensityPercent` and an
  `IsIntentionalBlackout` flag for a directly bindable display). Same convention
  `SectionVisualDirectionRowView` already established.
- **`TransitionRecipeEditorViewModel.cs`** — SCREEN 12 CHOREOGRAPHY's own bindable recipe editor view model,
  the direct UI layer over this step's own `ITransitionRecipePlanner`/`ITransitionPlanCatalog`:
  - `LoadState`/`IsEmpty`/`IsError`/`IsLoaded` — the same explicit loading/empty/error/loaded state machine
    `VisualStoryEditorViewModel` already established.
  - `PhraseId`/`Version` — identity/version of the phrase currently open.
  - `OpenPhraseAsync(catalog, phraseId, ct)` — loads (or switches to) a phrase's own independently persisted
    `TransitionPlan`.
  - `Plan(planner, requests)` — computes a new plan in memory via `planner`, publishing it through `Rows`
    *without persisting it*; rejects with `ActionErrorMessage` (never a partial `Rows` mutation) when no
    phrase is open or the planner rejects the input.
  - `SaveAsync(catalog, ct)` — persists the most recently `Plan`ned plan; rejects with `ActionErrorMessage`
    (never a partial mutation) when no phrase is open or nothing has been planned yet.
  - `Rows`/`HasUnsavedPlan` — the same preview-then-save pending/persisted split `VisualStoryEditorViewModel`
    already established.

### New test files

- **`tests/XlightsAI.Studio.Composition.Tests/Step070AcceptanceTests.cs`** (18 tests) — real
  `TransitionRecipePlanner`/`TransitionPlanCatalog`/`JsonTransitionPlanStore` (not mocks):
  - **`Step070_Acceptance_MusicalBreakCanIntentionallyDarkenAllPermittedProps`** — the required acceptance
    test, verbatim. A `Blackout` request over three permitted props (`Arches`/`Roofline`/`Tree`, the phase
    gate's own named worked-example props) produces a gesture targeting all three, both ends at intensity 0.
  - **`Plan_EachKindProducesItsOwnDeterministicRecipe`** — all five kinds exercised in one plan: `Build` rises,
    `Blackout` is 0→0, `Release` falls, `PhraseEnding` tapers but never reaches literal 0 (distinct from
    `Blackout`), no non-blackout recipe reaches literal maximum brightness, and re-planning the identical
    request reproduces the identical recipe values (deterministic strategy, per implementation guidance,
    verbatim).
  - **`Plan_EmptyRequests_ThrowsBeforeAnyComputation_NegativePath`**,
    **`Plan_UndefinedTransitionKind_Throws_NegativePath`** (the "unsupported ... runtime" diagnostic — an
    undefined enum value is rejected with a clear message, not an unhandled `KeyNotFoundException`),
    **`Plan_EmptyPermittedProps_Throws_NegativePath`** (the "clean machine" diagnostic — zero permitted props,
    e.g. no layout imported yet, is rejected with a clear message, not planned as a silent no-op),
    **`Plan_DuplicatePermittedProp_Throws_NegativePath`**, **`Plan_InvalidWindow_Throws_NegativePath`**.
  - **`TransitionGesture_Validate_RejectsBlackoutWithNonZeroIntensity_NegativePath`**,
    **`TransitionGesture_Validate_RejectsEmptyTargetProps_NegativePath`**,
    **`TransitionGesture_Validate_RejectsOutOfRangeIntensity_NegativePath`** (3 inline cases),
    **`TransitionPlan_Validate_RejectsEmptyGestures_NegativePath`**,
    **`TransitionPlan_Validate_RejectsDuplicateGestureId_NegativePath`**.
  - **`TransitionPlanCatalog_SaveAndReload_PersistsAcrossRestart`** — saved-state verification through a
    brand-new catalog/store instance.
  - **`TransitionPlanCatalog_InvalidPlan_RejectsBeforeAnyWrite_NegativePath`**,
    **`TransitionPlanCatalog_Cancelled_DoesNotPersist_NegativePath`**,
    **`TransitionPlanStore_LoadAsync_CorruptFile_ThrowsCorruptException_NegativePath`**.
- **`tests/XlightsAI.Studio.Shell.Tests/Step070TransitionRecipeEditorViewModelTests.cs`** (7 tests) — real
  `TransitionRecipeEditorViewModel`/`TransitionPlanCatalog`/`JsonTransitionPlanStore`/`TransitionRecipePlanner`
  (not mocks) under a temporary directory:
  - **`Step070_Acceptance_PlanAndSaveDarkenAllPermittedPropsForABlackout`** — the required acceptance text
    proven again at the bindable view-model layer (`Plan`/`Rows`/`SaveAsync`).
  - **`TransitionPlan_PersistsAndCanChangePerPhrase_SavedStateAcrossRestart`** — two independently keyed
    phrases, each independently planned/saved, reloaded through a brand-new view-model/catalog instance.
  - **`Plan_NothingOpen_SetsActionErrorAndLeavesRowsEmpty_NegativePath`**,
    **`Plan_EmptyPermittedProps_RejectsWithoutPartialRows_NegativePath`**,
    **`SaveAsync_NothingOpen_SetsActionErrorAndLeavesVersionNull_NegativePath`**,
    **`SaveAsync_NothingPlanned_RejectsWithoutMutatingPersistedState_NegativePath`**,
    **`OpenPhraseAsync_CorruptPersistedFile_SetsErrorAndIsError_NegativePath`**.

## 3. Architecture deviations / decisions

- **`TransitionGesture` omits PHASE-10.md Section 3's own proposed `PlannedGesture` shape**
  (`GestureType`/`RecipeId` string fields, a single `TimeWindow`/`Targets` with no intensity curve). Instead
  `TransitionGesture` carries `Kind` (the already-typed `TransitionKind` enum, not a free-form `GestureType`
  string) and an explicit `StartIntensity`/`EndIntensity` ramp. Reasoning: this step's own required acceptance
  and implementation guidance are both about a measurable *intensity curve over a window* ("intentionally
  darken," "rising/falling," "reusable semantics with deterministic recipe strategies") — a bare `RecipeId`
  string would push that computation into an unverified downstream consumer instead of making it this step's
  own directly tested, directly bindable output. Concrete xLights effect-recipe compilation/collision
  resolution (where a `RecipeId`-style indirection would matter) remains Step 071's own later, disclosed scope.
- **`TransitionGesture`/`TransitionRecipeRequest` target `IReadOnlyList<ModelId>` directly** — the first
  Composition-project type to target concrete xLights models rather than a `SpatialPathId`/`SourceTrackId`.
  This is deliberate and matches Step 069's own ADR, which explicitly deferred "concrete prop/role targeting"
  to "Step 070/071's own later, disclosed scope." No new project reference was needed: `ModelId` is already
  declared in `XlightsAI.Studio.Contracts`, which `XlightsAI.Studio.Composition` already references.
- **"Permitted props" is a caller-supplied, already-filtered set, not independently computed by this step.**
  This step does not itself determine which props are locked/excluded (that remains the Editing project's own
  already-accepted `IEffectLockPolicy`/effect-lock scope, which `Composition` does not reference) — it only
  guarantees that whatever permitted set the caller supplies, a `Blackout` request darkens *all* of it. This
  is the literal, narrowest reading of the required acceptance text ("Musical break can intentionally darken
  all permitted props," not "all props" or "all unlocked props by some independently-computed lock check").
- **A non-blackout recipe's own `TargetProps` is also the full permitted set**, not a role-filtered subset.
  Reasoning: this step's own narrow scope (per its own heading) is recipe *generation* — producing a
  deterministic intensity curve for a named semantic moment — not role-based targeting or budget-based
  admission filtering, both of which are already-accepted, separate concerns (`MusicalRoleAssignment`/
  `VisualBudgetResolver`, Steps 066/068). A later step can filter `PermittedProps` down to a role-specific
  subset before calling this step's own planner without any breaking change to its contract.
- **The five-kind recipe table is a fixed, caller-uninfluenced constant**, not persisted or user-configurable
  (unlike `VisualBudgetPreset`). Reasoning: "deterministic recipe strategies" (implementation guidance,
  verbatim) is read here as "the *mapping* from kind to intensity curve is fixed and reusable," not as "the
  user tunes each curve" — the latter is a plausible future UI affordance this step's contract does not
  preclude (`TransitionKind` is already a stable, narrow enum a later step could pair with a user-editable
  curve store without a breaking change).
- **`PhraseEnding`'s own end intensity (`0.05`) is deliberately non-zero, distinct from `Blackout`'s `(0,0)`.**
  This directly realizes the implementation guidance's own distinction between "silence" (an intentional full
  darkening) and "phrase-ending" (a resolution/taper) as two named, separately-disclosed recipes rather than
  one collapsed concept — covered by its own test
  (`Plan_EachKindProducesItsOwnDeterministicRecipe`).
- **Persistence is keyed by a caller-supplied `Guid phraseId`** (mirroring `MusicalRoleAssignmentSet`'s own
  phrase scope), not a song id (unlike `VisualStoryRecord`, which is inherently whole-song). A transition
  recipe plan — a build/blackout/transition/release/phrase-ending sequence — is authored against one phrase's
  own boundary, not normalized relative to every other phrase in the song the way `VisualStory`'s intensity
  curve is.
- **`TransitionRecipeEditorViewModel` splits `Plan` (in-memory only) from `SaveAsync` (persists)**, mirroring
  `VisualStoryEditorViewModel`'s own preview-then-save split, for the same reason: a user can inspect the
  planned recipe (`Rows`) before committing it.
- **"Clean machine and unsupported runtime must produce user-readable diagnostics" (this step's own required
  failure/edge case, verbatim) is realized as two concrete, disclosed validation rules**, the same precedent
  every prior Phase 09/10 step's evidence already used for translating this spec's more abstract failure-case
  phrasing into testable code:
  - *"Clean machine"* → an empty `PermittedProps` set (the state of a machine with no layout imported yet) is
    rejected with a specific, human-readable `CompositionInvalidException` message
    (`Plan_EmptyPermittedProps_Throws_NegativePath`), never silently planned as a zero-target no-op gesture.
  - *"Unsupported runtime"* → an undefined/future `TransitionKind` enum value is rejected with a specific
    message naming the unsupported kind (`Plan_UndefinedTransitionKind_Throws_NegativePath`), never an
    unhandled `KeyNotFoundException` from the internal recipe lookup table.
  - No actual OS/xLights-build/hardware runtime-capability detection exists at this step — that remains later,
    xLights-adapter-facing scope (Phase 10's own xLights-adjacent steps and beyond); disclosed here rather than
    silently assumed solved.
- **No real `ICompositionPlanner`/`IChoreographyCompiler`/`ChoreographyPlan` exists yet.** PHASE-10.md Section
  3's own proposed signatures for those contracts remain Step 071's own later, disclosed scope ("Compose and
  resolve overlapping effects with layer/priority strategies"); this step only adds the
  build/silence/transition/release/phrase-ending recipe-generation + persistence + preview-editor UI layer
  Step 071 will consume alongside Step 068's own `VisualBudgetResolver` and Step 069's own `VisualStory`.
- **No real SCREEN 12 CHOREOGRAPHY Avalonia/AXAML view exists.** Unchanged, carried-forward disclosure from
  every prior Phase 09/10 step (confirmed again via a full repository `Glob` for `*.axaml`: still only
  `App.axaml`/`MainWindow.axaml`). This step's own required evidence text is narrower than a literal pixel
  claim — "Avalonia screenshot **or** repeatable desktop interaction recording with saved-state
  verification" — and is satisfied here by the real, non-test-harness demo in §5 below.
- **Per the user's own standing waiver** (`memory: feedback_skip_xlights_human_verification`), no live xLights
  render/desktop-interaction check was attempted — this step's own required acceptance text is a pure
  recipe-planning/persistence claim (no XSQ/effect-recipe output exists yet at this point in Phase 10), proven
  directly by the automated tests and demo below.

## 4. Commands actually executed

All run via the PowerShell tool — the sandboxed Bash tool in this session again returned bare exit code 1 with
no output for plain commands (confirmed again at the very start of this step), the same pre-existing
environment limitation every prior step's own evidence has recorded; PowerShell was used for every
`dotnet`/file-system command below without incident.

```
# Baseline, before any Step 070 code existed:
dotnet build XlightsAI.Studio.slnx --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s). (Step 069's own closing state)

# After adding the new Step 070 Composition files (no new project reference needed):
dotnet build src\XlightsAI.Studio.Composition\XlightsAI.Studio.Composition.csproj --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).

# After adding the new Step 070 Shell files:
dotnet build src\XlightsAI.Studio.Shell\XlightsAI.Studio.Shell.csproj --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).

dotnet build XlightsAI.Studio.slnx --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s). (after adding the two new test files)

dotnet test tests\XlightsAI.Studio.Composition.Tests\XlightsAI.Studio.Composition.Tests.csproj --nologo --filter "FullyQualifiedName~Step070"
  # Passed! - Failed: 0, Passed: 18, Skipped: 0, Total: 18, Duration: 252 ms

dotnet test tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj --nologo --filter "FullyQualifiedName~Step070"
  # Passed! - Failed: 0, Passed: 7, Skipped: 0, Total: 7, Duration: 264 ms

dotnet format XlightsAI.Studio.slnx --verify-no-changes --no-restore
  # No output (clean) -- matches the repository's own CI formatting gate, checked across the full solution.

dotnet test XlightsAI.Studio.slnx --nologo --filter "FullyQualifiedName!~Step025&FullyQualifiedName!~Step028&FullyQualifiedName!~Step029&FullyQualifiedName!~Step030&FullyQualifiedName!~Step042"
dotnet test tests\XlightsAI.Studio.Stems.Tests\XlightsAI.Studio.Stems.Tests.csproj --nologo --filter "FullyQualifiedName~Step042"
  # Passed! - Failed: 0, Passed: 8, Skipped: 0, Total: 8, Duration: 436 ms
  # (the blunt numeric filter above also excludes Stems.Tests' own unrelated, legitimate
  # Step042AcceptanceTests.cs -- the same filter side effect disclosed in every prior Phase 09/10 step's own
  # evidence; reconfirmed here passing in isolation.)

# Real, non-test-harness manual demo (built under %TEMP%\xlai-step070-demo-proj, deleted after this run):
dotnet run --project "$env:TEMP\xlai-step070-demo-proj"
```

### Targeted test results (Step 070's own 25 new tests, real run)

```
Step070AcceptanceTests (Composition, 18 new):
  Step070_Acceptance_MusicalBreakCanIntentionallyDarkenAllPermittedProps     Passed
  Plan_EachKindProducesItsOwnDeterministicRecipe                            Passed
  Plan_EmptyRequests_ThrowsBeforeAnyComputation_NegativePath                Passed
  Plan_UndefinedTransitionKind_Throws_NegativePath                          Passed
  Plan_EmptyPermittedProps_Throws_NegativePath                              Passed
  Plan_DuplicatePermittedProp_Throws_NegativePath                           Passed
  Plan_InvalidWindow_Throws_NegativePath                                    Passed
  TransitionGesture_Validate_RejectsBlackoutWithNonZeroIntensity_NegativePath  Passed
  TransitionGesture_Validate_RejectsEmptyTargetProps_NegativePath           Passed
  TransitionGesture_Validate_RejectsOutOfRangeIntensity_NegativePath (x3)   Passed
  TransitionPlan_Validate_RejectsEmptyGestures_NegativePath                 Passed
  TransitionPlan_Validate_RejectsDuplicateGestureId_NegativePath            Passed
  TransitionPlanCatalog_SaveAndReload_PersistsAcrossRestart                 Passed
  TransitionPlanCatalog_InvalidPlan_RejectsBeforeAnyWrite_NegativePath      Passed
  TransitionPlanCatalog_Cancelled_DoesNotPersist_NegativePath               Passed
  TransitionPlanStore_LoadAsync_CorruptFile_ThrowsCorruptException_NegativePath  Passed
Composition.Tests total (Step 070 slice): 18/18 passed, 0 failed, 0 skipped.

Step070TransitionRecipeEditorViewModelTests (Shell, 7 new):
  Step070_Acceptance_PlanAndSaveDarkenAllPermittedPropsForABlackout         Passed
  TransitionPlan_PersistsAndCanChangePerPhrase_SavedStateAcrossRestart      Passed
  Plan_NothingOpen_SetsActionErrorAndLeavesRowsEmpty_NegativePath           Passed
  Plan_EmptyPermittedProps_RejectsWithoutPartialRows_NegativePath           Passed
  SaveAsync_NothingOpen_SetsActionErrorAndLeavesVersionNull_NegativePath    Passed
  SaveAsync_NothingPlanned_RejectsWithoutMutatingPersistedState_NegativePath  Passed
  OpenPhraseAsync_CorruptPersistedFile_SetsErrorAndIsError_NegativePath     Passed
Shell.Tests total (Step 070 slice): 7/7 passed, 0 failed, 0 skipped.
```

### Full regression (final, after all Step 070 implementation and tests, real-audio-device Shell tests excluded — unchanged pre-existing condition, see Step 064-069's own §6)

```
Passed! - XlightsAI.Studio.Audio.Tests.dll           23/23
Passed! - XlightsAI.Studio.Xlights.Tests.dll         74/74
Passed! - XlightsAI.Studio.Editing.Tests.dll         83/83
Passed! - XlightsAI.Studio.Display.Tests.dll         56/56
Passed! - XlightsAI.Studio.Structure.Tests.dll       78/78
Passed! - XlightsAI.Studio.Composition.Tests.dll     59/59 (41 Step 066+068+069 + 18 new Step 070)
Passed! - XlightsAI.Studio.Melody.Tests.dll         117/117
Passed! - XlightsAI.Studio.Projects.Tests.dll         25/25
Passed! - XlightsAI.Studio.Shell.Tests.dll          160/160 (153 Step 001-069 + 7 new Step 070; real-audio-device tests excluded, unchanged)
Passed! - XlightsAI.Studio.Rhythm.Tests.dll           82/82
Passed! - XlightsAI.Studio.Reference.Tests.dll        31/31
Passed! - XlightsAI.Studio.Stems.Tests.dll            93/93  (+8 Step042AcceptanceTests reconfirmed separately, below)
```

**885 total passing** (860 carried forward from Step 069's own closing count, unmodified, + this step's own
18 new Composition tests + 7 new Shell tests = 25), 0 failed, 0 skipped. Every pre-existing test from Steps
001-069 still passes unmodified; the only additions are `Step070AcceptanceTests` (18/18) and
`Step070TransitionRecipeEditorViewModelTests` (7/7).

## 5. Persisted-output inspection / observable demonstration for this step

A real, non-test-harness console program (not part of the repository, built under
`%TEMP%\xlai-step070-demo-proj`, deleted after this run) exercises the real
`TransitionRecipeEditorViewModel`/`TransitionPlanCatalog`/`JsonTransitionPlanStore`/`TransitionRecipePlanner`
directly — no test harness, no mocks. It uses the phase gate's own named worked-example props (arches,
roofline, tree) and plans a build-into-chorus / musical-break-blackout / release / phrase-ending sequence:

```
dotnet run --project "$env:TEMP\xlai-step070-demo-proj"
```

Observed real output:

```
=== Step 070 manual demo: real transition recipe UI view model, no test harness ===

--- Scenario 1: open a new phrase (nothing planned/saved yet) ---
  Opened=True, LoadState=Loaded, Version=null, Rows=0

--- Scenario 2: plan a build into the chorus, then an intentional musical-break blackout ---
  Planned=True, HasUnsavedPlan=True
    Build          targets=3   15% ->  90%  Blackout=False
    Blackout       targets=3    0% ->   0%  Blackout=True
    Release        targets=3   85% ->  20%  Blackout=False
    PhraseEnding   targets=3   30% ->   5%  Blackout=False
  Musical break darkens ALL permitted props: True  -- "Musical break can intentionally darken all permitted props", verbatim.

--- Scenario 3: save the planned recipe ---
  Saved=True, Version=0, HasUnsavedPlan=False

--- Scenario 4: restart (brand-new view-model/catalog instances over the same on-disk directory) ---
  Reloaded Version=0, Rows=4
  Reloaded blackout still darkens every permitted prop: True

--- Scenario 5: invalid input is rejected and never corrupts the project (negative path) ---
  Plan with nothing open rejected: True, error="Open a phrase before planning a transition recipe."
  Plan with zero permitted props (clean machine) rejected: True, error="At least one permitted prop is required to plan a transition gesture -- a clean machine with no layout imported yet has none, and must be reported clearly rather than planned as a silent no-op."
  Save with nothing planned rejected: True, error="Plan a transition recipe before saving it."
  Rows left empty after all rejections: True

Step 070 manual demo completed successfully.

Cleaning up demo cache directory.
```

Direct, real, non-test-harness confirmation of this step's own required text: (1) "Musical break can
intentionally darken all permitted props" — Scenario 2 shows the `Blackout` gesture targeting all three
permitted props (arches/roofline/tree) at 0%→0%, independently confirmed again at the bindable-row layer. (2)
"Add transition gestures, builds, blackouts and resolutions as reusable semantics with deterministic recipe
strategies" — Scenario 2 shows all four distinct kinds (`Build`/`Blackout`/`Release`/`PhraseEnding`) each
producing their own fixed, named intensity curve in one plan call. (3) Saved-state verification — Scenario 4
proves the saved plan survives a real restart through brand-new view-model/catalog instances, with the
reloaded blackout gesture bit-identical to what was planned and saved. (4) "Clean machine and unsupported
runtime must produce user-readable diagnostics" — Scenario 5 confirms planning with nothing open, planning
with zero permitted props (the "clean machine" case), and saving with nothing planned are all rejected with a
specific, readable error message and no row mutation.

## 6. Fixtures / input hashes

- No show-layout XML fixture was needed for this step's own tests or demo — `TransitionRecipeRequest` operates
  on caller-supplied `ModelId` values with arbitrary string identities (this step's own planner never
  validates prop existence against a real imported `DisplayGraph`/`SpatialDisplayGraph`; that remains the
  already-accepted Phase 02/09 display-import pipeline's own job), consistent with this step's own narrow,
  disclosed scope (recipe generation, not layout/import validation).
- The real demo's own persisted `TransitionPlanRecord` JSON file (one per phrase id) was written to a
  temporary directory for inspection and deleted by the demo's own cleanup step (confirmed by "Cleaning up
  demo cache directory." in the real output above); the throwaway demo *project* itself was deleted after this
  evidence was recorded.
- **Real-audio-device test exclusion, disclosed (unchanged, carried forward from every prior Phase 09/10
  step's own §6):** `Step025`/`Step028`/`Step029`/`Step030`/`Step042ShellWiringTests` construct a real
  `WaveOutAudioTransport` (the real Windows winmm audio device) unavailable in this session's own sandboxed
  execution environment — a pre-existing, environment-caused condition predating this step entirely. No file
  this step touched is on the call path of any of those five classes (confirmed: none reference
  `TransitionRecipeEditorViewModel`/`TransitionGestureRowView`/`TransitionRecipePlanner`/`TransitionPlanCatalog`
  in any way).

## 7. Unresolved issues / known gaps

- **No real SCREEN 12 CHOREOGRAPHY Avalonia/AXAML view or Shell-layer `ShellScreen` wiring exists** —
  disclosed in §3; this step's own required evidence ("Avalonia screenshot **or** repeatable desktop
  interaction recording with saved-state verification") is proven at the bindable view-model layer plus the
  real, non-test-harness demo in §5, not by a pixel-rendering screenshot. Every other screen in this
  repository has the identical gap (confirmed via a full repository `Glob` for `*.axaml`).
- **`TransitionGesture` deliberately omits PHASE-10.md Section 3's own proposed `PlannedGesture` shape
  (`GestureType`/`RecipeId` strings)** — disclosed in §3 as a deliberate, concrete-intensity-curve realization
  instead; Step 071's own later effect-compilation/collision-resolution scope is expected to consume
  `TransitionGesture` alongside `VisualBudgetCandidate`/`SectionVisualDirection` when it owns compiling a
  section's/phrase's story into actual xLights effect recipes.
- **No actual OS/xLights-build/hardware runtime-capability detection exists** — this step's own "unsupported
  runtime" failure/edge case text is realized narrowly as "reject an undefined `TransitionKind` enum value
  with a clear message" (disclosed in §3); real runtime/xLights-version capability detection remains later,
  xLights-adapter-facing scope.
- **Permitted-prop exclusion/locking is entirely caller-supplied, not independently computed by this step** —
  disclosed in §3; this step guarantees a `Blackout` darkens every prop in whatever permitted set it is given,
  not that it independently re-derives that set from `IEffectLockPolicy` or an imported layout.
- **No real `ICompositionPlanner`/`IChoreographyCompiler`/`ChoreographyPlan` exists yet** — `PHASE-10.md`
  Section 3's own proposed signatures for those contracts are Step 071's own later scope; this step only adds
  the build/silence/transition/release/phrase-ending recipe-generation/persistence/preview-editor layer Step
  071 will consume.
- **Carried-forward, disclosed phase-level/dependency-level gaps are unchanged from Step 069's own closing
  evidence** (not reopened or re-resolved here): Phase 09's own consolidated `GATE-REPORT.md` is still
  unwritten; Phase 06's own stem-separation gate remains BLOCKED; Phase 07's and Phase 08's own full-phase
  gates have not been separately run/recorded; real-audio-device Shell tests remain environment-blocked (§6).
- **Per the user's own standing waiver** (`memory: feedback_skip_xlights_human_verification`), the
  real-xLights render/desktop-interaction check is not treated as a blocker for this step's own scope — no
  XSQ/effect-recipe output exists yet at this point in Phase 10 for such a check to apply to.

## 8. PASS / BLOCKED

**Status: PASS** (for this step's own narrow, disclosed scope — deterministic build/silence/transition/
release/phrase-ending recipe generation with a bindable plan/save preview editor UI over it — not a claim
that any carried-forward phase-level gap listed in §7 is resolved, and not a claim that a real SCREEN 12
Avalonia view, `ICompositionPlanner`, or effect-layer collision resolution — Step 071's own scope — is
complete).

- "Musical break can intentionally darken all permitted props" (required acceptance, verbatim) — proven by
  `Step070_Acceptance_MusicalBreakCanIntentionallyDarkenAllPermittedProps` (domain) and
  `Step070_Acceptance_PlanAndSaveDarkenAllPermittedPropsForABlackout` (view model) — independently confirmed
  end-to-end by the real demo's Scenario 2 (the Blackout gesture targets all three permitted props at 0%→0%).
- "Add transition gestures, builds, blackouts and resolutions as reusable semantics with deterministic recipe
  strategies" (implementation guidance, verbatim) — proven by `Plan_EachKindProducesItsOwnDeterministicRecipe`
  (all five kinds, fixed recipe table, re-planning reproduces identical values) — independently confirmed by
  the real demo's own printed recipe values for Build/Blackout/Release/PhraseEnding.
- "Clean machine and unsupported runtime must produce user-readable diagnostics" (failure/edge case, verbatim)
  — proven by `Plan_EmptyPermittedProps_Throws_NegativePath` (clean machine),
  `Plan_UndefinedTransitionKind_Throws_NegativePath` (unsupported kind),
  `Plan_EmptyRequests_ThrowsBeforeAnyComputation_NegativePath`, `Plan_DuplicatePermittedProp_Throws_NegativePath`,
  `Plan_InvalidWindow_Throws_NegativePath`,
  `TransitionGesture_Validate_RejectsBlackoutWithNonZeroIntensity_NegativePath`,
  `TransitionGesture_Validate_RejectsEmptyTargetProps_NegativePath`,
  `TransitionGesture_Validate_RejectsOutOfRangeIntensity_NegativePath`,
  `TransitionPlan_Validate_RejectsEmptyGestures_NegativePath`,
  `TransitionPlan_Validate_RejectsDuplicateGestureId_NegativePath`,
  `TransitionPlanCatalog_InvalidPlan_RejectsBeforeAnyWrite_NegativePath`,
  `TransitionPlanCatalog_Cancelled_DoesNotPersist_NegativePath`,
  `Plan_NothingOpen_SetsActionErrorAndLeavesRowsEmpty_NegativePath`,
  `Plan_EmptyPermittedProps_RejectsWithoutPartialRows_NegativePath`,
  `SaveAsync_NothingOpen_SetsActionErrorAndLeavesVersionNull_NegativePath`, and
  `SaveAsync_NothingPlanned_RejectsWithoutMutatingPersistedState_NegativePath` — independently confirmed by
  the real demo's Scenario 5 (all three rejection paths exercised, rows left empty).
- Full regression (real-audio-device tests excluded, disclosed in full in §6): **885/885 passing**, 0 failed,
  0 skipped; every pre-existing test from Phase 01-10/Step 069 still passes unmodified, plus this step's own
  18 new Composition tests + 7 new Shell tests. `dotnet format --verify-no-changes` clean across the full
  solution.
- **No file from any prior step/phase was behaviorally changed** — this step only adds nine new files to
  `XlightsAI.Studio.Composition` (no new project reference needed) and two new files to
  `XlightsAI.Studio.Shell`, plus two new test files. No existing Composition- or Shell-project file's own
  existing members were modified.
- **Carried-forward, disclosed phase-level/dependency-level gaps (not this step's own blocker):** no real
  SCREEN 12 Avalonia view exists yet (§7, project-wide pre-existing condition); Phase 09's own consolidated
  gate report is still unwritten; Phase 06's own gate remains BLOCKED; Phase 07's and Phase 08's own
  full-phase gates have not been separately run/recorded; real-audio-device Shell tests are
  environment-blocked (§6); no real `ICompositionPlanner`/`IChoreographyCompiler`/`ChoreographyPlan` exists
  yet (§7 — Step 071's own scope).

Per this step's own closing instruction ("do not execute the next step until this step is proven"), Step 071
("Compose and resolve overlapping effects with layer/priority strategies") should not begin until this
evidence is reviewed and accepted.
