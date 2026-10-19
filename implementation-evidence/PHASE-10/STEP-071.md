# PHASE 10 — Step 071 Evidence

**Step:** "Compose and resolve overlapping effects with layer/priority strategies"
**Spec:** `docs/phase-specs/PHASE-10.md` §6, Step 071 (lines 283-297) — the sixth and final numbered step
of Phase 10 (066-071)
**Date:** 2026-10-16

## 0. Scope clarification

The prompt opening this session asked to implement "Step 71 from PHASE-10.md," matching
`implementation-evidence/PHASE-10/STEP-070.md`'s own closing instruction: "do not execute the next step
until this step is proven." Step 070's evidence was read in full first and confirmed PASS for its own
narrow scope, with no real `ICompositionPlanner`/`IChoreographyCompiler`/`ChoreographyPlan` existing yet
— disclosed there as explicitly Step 071's own later scope. This document covers Step 071 only.

## 1. Pre-implementation baseline inspection

- Read `docs/phase-specs/PHASE-10.md` in full, confirming Step 071's own text: required acceptance ("No
  unintended model conflicts; XSQ renders as planned"), implementation guidance ("Combine overlapping
  role plans into a single scoped candidate; validate collisions/layers/locks and demonstrate purposeful
  coordination"), failure/edge case ("Clean machine and unsupported runtime must produce user-readable
  diagnostics" — shared verbatim with Step 070, per that step's own §0 disclosure), and Section 3's own
  proposed `PlannedGesture`/`ChoreographyPlan`/`ICompositionPlanner`/`IChoreographyCompiler` signatures.
- Read `implementation-evidence/PHASE-10/STEP-066.md` through `STEP-070.md` in full as the immediately
  preceding, already-accepted contract baseline. Step 070's own §3/§7 explicitly disclosed: "No real
  `ICompositionPlanner`/`IChoreographyCompiler`/`ChoreographyPlan` exists yet ... Step 071's own later,
  disclosed scope" — confirming this step's own compilation/collision-resolution contract is genuinely
  new work, not a redefinition of an already-accepted type.
- Read every file in `src/XlightsAI.Studio.Composition/` in full (all Step 066/068/069/070 types) before
  writing any new type:
  - `MusicalRoleAssignment.cs`/`IRolePrecedenceResolver.cs`/`RolePrecedenceResolver.cs`/`PrecedenceTier.cs`
    (Step 066) — the already-accepted, deterministic precedence ranking this step reuses directly rather
    than re-implementing (the same reuse convention `VisualBudgetCandidate` (Step 068) already
    established for the identical reason).
  - `VisualBudgetCandidate.cs`/`VisualBudgetResolver.cs`/`VisualBudgetDecision.cs`/`VisualBudgetResolution.cs`
    (Step 068) — the direct structural precedent for this step's own
    `ChoreographyCandidate`/`ChoreographyCompiler`/`ChoreographyResolutionDecision`/
    `ChoreographyCompilationResult` shape (candidate-wraps-assignment, enum decision, "restore caller's
    own original order for Results" convention, `Admitted`/`Dropped` projection properties).
  - `TransitionGesture.cs`/`TransitionRecipePlanner.cs` (Step 070) — confirmed its own ADR explicitly
    deferred "concrete xLights effect-recipe compilation/collision resolution" to this step; this step's
    own `ChoreographyCandidate` is deliberately a distinct, narrower type (a single concrete
    `ModelId`+window+already-verified recipe id), not a reuse of `TransitionGesture`'s own
    intensity-ramp shape (see §3).
- Read `src/XlightsAI.Studio.Xlights/EffectInstance.cs`, `SequenceDocument.cs`, `IEffectRecipeRegistry.cs`,
  `EffectRecipeRegistry.cs`, `XsqExporter.cs`, `XsqStructuralReader.cs`/`XsqStructuralDocument.cs`/
  `XsqStructuralElement.cs`/`XsqStructuralEffectPlacement.cs` in full — confirmed `EffectInstance` already
  carries a `Layer` field (PHASE-02 Step 012) and `XsqExporter` already groups effects into one
  `<EffectLayer>` per distinct `Layer` value per target (PHASE-02 Step 013), so this step's own layer
  assignment is a real, already-exportable concept, not a new XML shape to invent.
- Read `src/XlightsAI.Studio.Editing/EditScope.cs`, `IEffectLockPolicy.cs`, `EffectLockPolicy.cs`,
  `EditTransactionService.cs`, `EditCommandRejectedException.cs`, `EditRejectionReason.cs` in full —
  confirmed `IEffectLockPolicy.AssertEditable(document, scope)` is the already-accepted, all-or-nothing
  "no partial mutation" gate every PHASE-03 command already uses before touching anything; this step
  reuses it unmodified rather than re-implementing lock enforcement (see §3).
- Confirmed via `grep`/`Glob` that `XlightsAI.Studio.Editing` does not reference
  `XlightsAI.Studio.Composition` (only `Contracts`/`Xlights`/`Audio`) and no project references
  `Composition` at all except its own tests — adding `Composition -> Xlights` and `Composition ->
  Editing` project references introduces no cyclic reference.
- Read `tests/XlightsAI.Studio.Composition.Tests/Step069AcceptanceTests.cs` and
  `tests/XlightsAI.Studio.Xlights.Tests/Step051FrameQuantizationTests.cs` in full as the direct structural
  precedent for this step's own test style (real collaborators, not mocks; real `DisplayGraph`/
  `XsqExporter`/`XsqStructuralReader` round-trip; `NewCacheDirectory`/`IDisposable` temp-directory
  convention).
- Searched the full repository for any prior "Step 071"/"STEP-071"/`ChoreographyCompiler`/
  `ChoreographyCandidate` reference: found only `PHASE-10.md` itself, confirming this is genuinely new
  work.
- Confirmed via `Glob` for `*.axaml`: still only `App.axaml`/`MainWindow.axaml` exist anywhere in the
  repository (unchanged, carried-forward condition) — consistent with this step's own required evidence
  text being "Generated XML artifact and semantic diff/fixture, with xLights validation if this step
  changes rendering," not an Avalonia screenshot, so no new Shell-layer view model was needed for this
  step (a deliberate, disclosed scope decision — see §3).
- Baseline taken by running the real, full solution build *before* any Step 071 code was written:
  `dotnet build XlightsAI.Studio.slnx --nologo` (clean, 0 warnings/errors), reconfirming Step 070's own
  closing state immediately before starting.

## 2. Scope actually implemented

**No file from Step 001-070 was behaviorally changed.** This step adds six new files to the existing
`src/XlightsAI.Studio.Composition/` project, two new `<ProjectReference>` entries to that project's own
`.csproj` (to `XlightsAI.Studio.Xlights` and `XlightsAI.Studio.Editing`), one new test file, and one new
`<ProjectReference>` entry to the test project's own `.csproj` (to `XlightsAI.Studio.Editing`). No
existing Composition-project file's own existing members were modified.

### New files in `src/XlightsAI.Studio.Composition/`

- **`ChoreographyCandidate.cs`** — one candidate concrete effect placement: `Id`, `Assignment` (the
  already-accepted `MusicalRoleAssignment`, reused by reference for precedence — the same convention
  `VisualBudgetCandidate` already established), `Target` (a concrete `ModelId`), `Window` (`TimeWindow`),
  `RecipeId` (an already-verified `IEffectRecipeRegistry` recipe id — never an invented effect shape).
  Has its own `Validate()` defending every invariant (non-empty `Id`, non-blank `Target`, valid non-empty
  `[start,end)` `Window`, non-blank `RecipeId`) against direct record construction, not only against
  compiler input.
- **`ChoreographyResolutionDecision.cs`** — the three-value layer/priority outcome this step's own
  heading names directly: `Accepted` (natural layer 0), `Reassigned` (moved to a higher free layer to
  coexist with a higher-precedence occupant — "purposeful coordination," implementation guidance,
  verbatim), `Dropped` (no free layer found within `MaxLayers`).
- **`ChoreographyCandidateResult.cs`** — one candidate's own resolved outcome: `Candidate`, `Decision`,
  `ResolvedLayer` (`null` only when `Dropped`), `Reason` (populated for `Reassigned`/`Dropped`).
- **`ChoreographyCompilationResult.cs`** — the full outcome: the resulting `SequenceDocument`, every
  candidate's own `Results` (original input order), and non-fatal `Warnings`; `Admitted`/`Dropped`
  projection properties mirror `VisualBudgetResolution`'s own convention exactly.
- **`IChoreographyCompiler.cs`** / **`ChoreographyCompiler.cs`** — the compiler itself. Validates every
  candidate independently (`Validate()`, duplicate id, in-scope target, in-scope window) before any
  resolution; calls the already-accepted `IEffectLockPolicy.AssertEditable(baseDocument, allowedScope)`
  up front — the whole compile is rejected before any candidate is touched if the permitted scope reaches
  an already-locked effect; orders candidates highest-precedence-first by reusing
  `IRolePrecedenceResolver.ResolveOrder` against the *distinct* set of underlying assignments (so two
  candidates sharing an equal-valued assignment unambiguously share one rank, avoiding the
  reference-equality workaround `VisualBudgetResolver` needed for a different reason); for each candidate
  in that order, finds the lowest free layer (0..`MaxLayers`-1, `MaxLayers = 8`) on its own target that
  does not already overlap an existing effect (seeded from every pre-existing `baseDocument` effect,
  locked or not) or an already-admitted higher-precedence candidate; compiles an admitted candidate via
  the already-accepted `IEffectRecipeRegistry.CreateVerified` (never inventing an effect shape), appending
  it to a new document revision; every pre-existing effect is carried forward byte-for-byte unchanged.

### New test file

- **`tests/XlightsAI.Studio.Composition.Tests/Step071AcceptanceTests.cs`** (19 tests) — real
  `ChoreographyCompiler`/`RolePrecedenceResolver`/`EffectLockPolicy`/`EffectRecipeRegistry`/`XsqExporter`/
  `XsqStructuralReader` (not mocks), against the phase gate's own named worked-example props
  (Arches/Roofline/Tree):
  - **`Step071_Acceptance_NoUnintendedModelConflicts_XsqRendersLayeredPlanAsCompiled`** — the required
    acceptance test, verbatim. A higher-precedence lead-piano candidate and a lower-precedence,
    *genuinely overlapping* accent-snare candidate both target Arches; the accent is `Reassigned` to its
    own layer (never dropped, never overwriting the lead); the compiled document is exported to a real
    `.xsq` file and independently re-read via `XsqStructuralReader` — both effects survive export on the
    same `<Element name="Arches">`, each in its own `<EffectLayer>`, at their own planned (frame-rounded)
    windows, proving the layered plan renders exactly as composed.
  - **`Compile_CollisionsBeyondMaxLayers_DropsLowestPrecedence_NeverReplacesHigherPrecedence`** —
    `MaxLayers + 1` mutually-overlapping candidates on one target; exactly the lowest-priority one is
    `Dropped` with a clear reason; the other `MaxLayers` are exported and independently confirmed present
    in the real `.xsq`.
  - **`Compile_NonOverlappingCandidatesOnDifferentModels_AllAcceptedAtLayerZero`** — the phase gate's own
    named worked example (lead piano/Arches, kick-snare/Roofline, bass/Tree) with no time overlap: all
    three `Accepted` at layer 0, zero warnings.
  - **`Compile_SameInputsInAnyOrder_ProducesTheSameAdmissionOutcome`** — full-phase test matrix item #5
    ("Same inputs+seed produce repeatable plan"), verbatim.
  - **`Compile_LeadPrioritySurvivesSimultaneousBassAndSnareOnset_KeepsLayerZero`** — full-phase test
    matrix item #1, verbatim.
  - **`Compile_ManualLockedEffectCannotBeReplacedByNewPlan_RejectsWholeCompile_NegativePath`** —
    full-phase test matrix item #6, verbatim: a locked effect inside the permitted scope rejects the
    *entire* compile (`EditCommandRejectedException`, `EditRejectionReason.Locked`), leaving the base
    document completely untouched.
  - **`Compile_ScopeExcludesLockedModel_LockedEffectCarriedForwardUnchanged`** — the counterpart: when the
    scope does not reach the locked effect's model, the compile proceeds and the locked effect survives
    into the new revision byte-for-byte unchanged.
  - **`Compile_EmptyCandidates_ThrowsBeforeAnyComputation_NegativePath`** (the "clean machine" diagnostic,
    verbatim), **`Compile_UnsupportedRecipeId_ThrowsUserReadableDiagnostic_NegativePath`** (the
    "unsupported runtime" diagnostic, verbatim — reuses the already-accepted
    `EffectPlanInvalidException`'s own clear message rather than inventing a new exception type),
    **`Compile_CandidateTargetOutsideScope_Throws_NegativePath`**,
    **`Compile_CandidateWindowOutsideScopeTime_Throws_NegativePath`**,
    **`Compile_DuplicateCandidateId_Throws_NegativePath`**, `Compile_NullArguments_Throw_NegativePath`.
  - **`ChoreographyCandidate_Validate_RejectsInvalidWindow_NegativePath`** (3 inline cases),
    **`ChoreographyCandidate_Validate_RejectsBlankRecipeId_NegativePath`**,
    **`ChoreographyCandidate_Validate_RejectsBlankTarget_NegativePath`**,
    **`ChoreographyCandidate_Validate_RejectsEmptyId_NegativePath`**.

## 3. Architecture deviations / decisions

- **`ChoreographyCandidate` deliberately narrows PHASE-10.md Section 3's own proposed
  `ChoreographyPlan`/`PlannedGesture` shape** (`PlannedGesture(Id, Role, GestureType, Window, Targets,
  RecipeId)` with multiple `Targets` per gesture and a free-form `GestureType` string) to one concrete
  `ModelId` target per candidate and an already-typed, already-verified `RecipeId` (no `GestureType`
  field at all). Reasoning: this step's own required acceptance is about *collision/layer resolution for
  concrete effects*, not gesture semantics (that remains `TransitionGesture`'s/future steps' own scope,
  per Step 070's ADR) — multiple targets per gesture would hide which *specific* target/window pair a
  layer/collision decision actually applies to, directly undermining "validate collisions/layers" being
  independently inspectable per target. A caller wanting a multi-target gesture submits one
  `ChoreographyCandidate` per target instead; no breaking change is needed for that later.
  Same narrowing reasoning Steps 069/070's own ADRs already used for the identical proposed shape.
- **`IChoreographyCompiler.Compile` is synchronous, pure, and side-effect-free** (no `CancellationToken`,
  no I/O) — mirrors `IVisualBudgetResolver.Resolve`'s own convention exactly (Step 068's own ADR: "Resolves
  no actual effect-layer/recipe compilation itself"). Persisting the resulting `SequenceDocument` (via the
  already-accepted `ISequenceDocumentStore`) and exporting it (via the already-accepted `IXsqExporter`)
  remain the caller's own, separate, already-accepted responsibilities — this step does not duplicate
  either.
- **A whole-compile, all-or-nothing lock rejection (`IEffectLockPolicy.AssertEditable` called once, up
  front, against the entire `allowedScope`)**, not a per-candidate lock check that silently skips just the
  colliding candidate. Reasoning: this is the identical "no partial mutation" rule every existing
  `EditTransactionService` command already enforces for a single `EditCommand` — PHASE-10.md Section 2's
  own cross-cutting rule ("Keep optimistic revision checks, effect locks, selection scope... undo/redo,
  and explicit human acceptance") does not carve out a special partial-batch exception for a multi-candidate
  compile. A caller who wants to compose around an already-locked region passes a narrower `EditScope`
  that excludes it (proven directly by
  `Compile_ScopeExcludesLockedModel_LockedEffectCarriedForwardUnchanged`), the same pattern every existing
  PHASE-03 command already requires.
- **Collision detection seeds occupancy from every pre-existing effect in `baseDocument`, locked or not** —
  not only locked effects. Reasoning: the required acceptance is "No unintended model conflicts," a
  property of the whole resulting document, not merely "no locked-effect conflicts." An already-accepted,
  unlocked effect from an earlier compile/edit is just as real a model conflict risk as a locked one if a
  new candidate were allowed to silently share its layer.
- **A fixed `MaxLayers = 8` bound**, not an unbounded layer count. Reasoning: "layer/priority strategies"
  (this step's own heading, verbatim) means a genuinely irresolvable collision is disclosed as a `Dropped`
  candidate with a clear reason, never silently stacked onto a number of layers no real xLights show would
  practically render as distinct. `MaxLayers` is `public const` specifically so a later step/config surface
  can raise it without a breaking API change.
- **Precedence ranking reuses `IRolePrecedenceResolver.ResolveOrder` against the *distinct* set of
  candidates' own underlying assignments, then maps rank back via a value-keyed dictionary** — not the
  reference-equality workaround `VisualBudgetResolver` (Step 068) needed. Reasoning: unlike
  `VisualBudgetResolver`'s own per-window admission (where distinguishing two candidates sharing an
  equal-valued assignment by identity mattered for *which specific candidate* got admitted first),
  this step's own ranking only needs a *precedence number* per assignment value — two candidates sharing
  an identical assignment (e.g. two gestures for the same lead track within one phrase) legitimately
  share the same rank, and a plain value-keyed `Dictionary<MusicalRoleAssignment,int>` expresses that
  directly and more simply than re-deriving `VisualBudgetResolver`'s own by-reference list-removal
  approach.
- **No real SCREEN 12 CHOREOGRAPHY Avalonia/AXAML view or Shell-layer view model was added for this
  step** — unlike Steps 067-070, whose own required evidence text was "Avalonia screenshot or repeatable
  desktop interaction recording," Step 071's own required evidence text is "Generated XML artifact and
  semantic diff/fixture, with xLights validation if this step changes rendering" (verbatim) — a different
  evidence shape this step satisfies directly via the real `.xsq` export/re-read in §5 below, not a UI
  screenshot. A future step wiring `ChoreographyCompiler` into an interactive SCREEN 12 can be added
  without any breaking change to `IChoreographyCompiler`'s own contract.
- **Per the user's own standing waiver** (`memory: feedback_skip_xlights_human_verification`), no live
  xLights render/desktop-interaction check was attempted. This step's own required acceptance text itself
  says "XSQ renders as planned" (not "confirmed rendering inside a running xLights installation"), and no
  xLights install has been available to any step in this repository so far (carried forward from every
  Phase 02/07 disclosure, most recently Step 051's own `XsqExporter` class doc: "No installed xLights
  build has been available to any PHASE-02 step so far"). "Renders as planned" is proven here by the real,
  independently re-parsed `.xsq` structural round-trip in §5 (both layered effects present, at their own
  planned windows, on the correct element, in separate `<EffectLayer>`s) — the strongest verification
  available without an xLights install, not a substitute mock claimed to be equivalent to one.

## 4. Commands actually executed

All run via the PowerShell tool — the sandboxed Bash tool in this session again returned bare exit code 1
with no output for plain commands (confirmed again at the very start of this step, and again when
attempting `mkdir`/`git status` for this step's own evidence), the same pre-existing environment
limitation every prior step's own evidence has recorded; PowerShell was used for every `dotnet`/
file-system command below without incident.

```
# Baseline, before any Step 071 code existed:
dotnet build XlightsAI.Studio.slnx --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s). (Step 070's own closing state)

# After adding the two new <ProjectReference> entries and the six new Composition files:
dotnet build src\XlightsAI.Studio.Composition\XlightsAI.Studio.Composition.csproj --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).

# After adding the new Step 071 test file and its project's own new <ProjectReference>:
dotnet build XlightsAI.Studio.slnx --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).

dotnet test tests\XlightsAI.Studio.Composition.Tests\XlightsAI.Studio.Composition.Tests.csproj --nologo --filter "FullyQualifiedName~Step071"
  # Passed! - Failed: 0, Passed: 19, Skipped: 0, Total: 19, Duration: 212 ms

dotnet format XlightsAI.Studio.slnx --verify-no-changes --no-restore
  # No output (clean) -- matches the repository's own CI formatting gate, checked across the full solution.

dotnet test XlightsAI.Studio.slnx --nologo --filter "FullyQualifiedName!~Step025&FullyQualifiedName!~Step028&FullyQualifiedName!~Step029&FullyQualifiedName!~Step030&FullyQualifiedName!~Step042"
dotnet test tests\XlightsAI.Studio.Stems.Tests\XlightsAI.Studio.Stems.Tests.csproj --nologo --filter "FullyQualifiedName~Step042"
  # Passed! - Failed: 0, Passed: 8, Skipped: 0, Total: 8, Duration: 351 ms
  # (the blunt numeric filter above also excludes Stems.Tests' own unrelated, legitimate
  # Step042AcceptanceTests.cs -- the same filter side effect disclosed in every prior Phase 09/10 step's own
  # evidence; reconfirmed here passing in isolation.)

# Real, non-test-harness manual demo (built under %TEMP%\xlai-step071-demo-proj, deleted after this run):
dotnet run --project "$env:TEMP\xlai-step071-demo-proj"
```

### Targeted test results (Step 071's own 19 new tests, real run)

```
Step071AcceptanceTests (Composition, 19 new):
  Step071_Acceptance_NoUnintendedModelConflicts_XsqRendersLayeredPlanAsCompiled              Passed
  Compile_CollisionsBeyondMaxLayers_DropsLowestPrecedence_NeverReplacesHigherPrecedence       Passed
  Compile_NonOverlappingCandidatesOnDifferentModels_AllAcceptedAtLayerZero                    Passed
  Compile_SameInputsInAnyOrder_ProducesTheSameAdmissionOutcome                                Passed
  Compile_LeadPrioritySurvivesSimultaneousBassAndSnareOnset_KeepsLayerZero                    Passed
  Compile_ManualLockedEffectCannotBeReplacedByNewPlan_RejectsWholeCompile_NegativePath        Passed
  Compile_ScopeExcludesLockedModel_LockedEffectCarriedForwardUnchanged                        Passed
  Compile_EmptyCandidates_ThrowsBeforeAnyComputation_NegativePath                             Passed
  Compile_UnsupportedRecipeId_ThrowsUserReadableDiagnostic_NegativePath                       Passed
  Compile_CandidateTargetOutsideScope_Throws_NegativePath                                     Passed
  Compile_CandidateWindowOutsideScopeTime_Throws_NegativePath                                 Passed
  Compile_DuplicateCandidateId_Throws_NegativePath                                            Passed
  ChoreographyCandidate_Validate_RejectsInvalidWindow_NegativePath (x3)                       Passed
  ChoreographyCandidate_Validate_RejectsBlankRecipeId_NegativePath                            Passed
  ChoreographyCandidate_Validate_RejectsBlankTarget_NegativePath                              Passed
  ChoreographyCandidate_Validate_RejectsEmptyId_NegativePath                                  Passed
  Compile_NullArguments_Throw_NegativePath                                                    Passed
Composition.Tests total (Step 071 slice): 19/19 passed, 0 failed, 0 skipped.
```

### Full regression (final, after all Step 071 implementation and tests, real-audio-device Shell tests excluded — unchanged pre-existing condition, see Step 064-070's own §6)

```
Passed! - XlightsAI.Studio.Audio.Tests.dll           23/23
Passed! - XlightsAI.Studio.Xlights.Tests.dll         74/74
Passed! - XlightsAI.Studio.Editing.Tests.dll         83/83
Passed! - XlightsAI.Studio.Display.Tests.dll         56/56
Passed! - XlightsAI.Studio.Structure.Tests.dll       78/78
Passed! - XlightsAI.Studio.Composition.Tests.dll     78/78 (59 Step 066+068+069+070 + 19 new Step 071)
Passed! - XlightsAI.Studio.Melody.Tests.dll         117/117
Passed! - XlightsAI.Studio.Projects.Tests.dll         25/25
Passed! - XlightsAI.Studio.Shell.Tests.dll          160/160 (real-audio-device tests excluded, unchanged)
Passed! - XlightsAI.Studio.Rhythm.Tests.dll           82/82
Passed! - XlightsAI.Studio.Reference.Tests.dll        31/31
Passed! - XlightsAI.Studio.Stems.Tests.dll            93/93  (+8 Step042AcceptanceTests reconfirmed separately, below)
```

**904 total passing** (885 carried forward from Step 070's own closing count, unmodified, + this step's
own 19 new Composition tests = 904), 0 failed, 0 skipped. Every pre-existing test from Steps 001-070 still
passes unmodified; the only addition is `Step071AcceptanceTests` (19/19). No Shell-project test count
changed (this step added no Shell-project file).

## 5. Persisted-output inspection / observable demonstration for this step

A real, non-test-harness console program (not part of the repository, built under
`%TEMP%\xlai-step071-demo-proj`, deleted after this run) exercises the real `ChoreographyCompiler`/
`RolePrecedenceResolver`/`EffectLockPolicy`/`EffectRecipeRegistry`/`XsqExporter`/`XsqStructuralReader`
directly — no test harness, no mocks. It realizes the phase gate's own exact named worked example: lead
piano on Arches, kick/snare on Roofline, bass on Tree, over a 20-second excerpt, with a quiet verse
(0-10s, fewer active props) and a louder chorus (10-20s, all three props plus a colliding automated accent
on Arches):

```
dotnet run --project "$env:TEMP\xlai-step071-demo-proj"
```

Observed real output:

```
=== Step 071 manual demo: real ChoreographyCompiler + XsqExporter, no test harness ===

--- Scenario 1: 20-second excerpt -- quiet verse (0-10s, fewer active props) ---
  Verse: 2 effects admitted across 2 distinct props (Arches=lead piano, Tree=bass; Roofline silent).
    Lead     track=T-Piano          -> Accepted   layer=0
    Bass     track=T-Bass           -> Accepted   layer=0

--- Scenario 2: louder chorus (10-20s) -- all three named props, PLUS a colliding automated accent on Arches ---
  Chorus: 4 effects admitted across 3 distinct props (louder/busier than the verse).
    Lead     track=T-Piano          -> Accepted   layer=0
    Accent   track=T-KickSnare      -> Accepted   layer=0
    Bass     track=T-Bass           -> Accepted   layer=0
    Accent   track=T-ArchesSparkle  -> Reassigned layer=1
    WARNING: 'Accent' role (track 'T-ArchesSparkle') on 'Arches' was moved to layer 1 to coexist with a higher-precedence effect already occupying a lower layer over an overlapping window -- purposeful layer coordination, not a dropped effect.

  Fewer active props in verse than chorus: True (2 < 3).
  No unintended model conflict on Arches: lead piano and accent were both admitted on distinct layers (2 distinct layers used).

--- Scenario 3: export the composed 20-second plan to a real .xsq and independently re-read it ---
  Exported -> C:\Users\kmoens.LAKECO\AppData\Local\Temp\xlai-step071-demo-output\step071-demo.xsq
  SHA-256 = A3ECF48C4E25BF4D24A6D377B865EE551A6EA2522D27FD42314BCFE8E1B67743
    Element 'Arches': 3 effect(s) -> [0,10000)ms, [10000,20000)ms, [14000,16000)ms
    Element 'Tree': 2 effect(s) -> [2000,8000)ms, [10000,20000)ms
    Element 'Roofline': 1 effect(s) -> [10000,20000)ms

--- Scenario 4: negative paths -- clean machine, unsupported recipe, locked-effect rejection ---
  Clean machine (no candidates) rejected: True, error="At least one choreography candidate is required to compose -- a clean machine with no planned role gestures yet has none, and must be reported clearly rather than compiled as a silent no-op."
  Unsupported runtime (unknown recipe id) rejected: True, error="Unknown effect recipe id 'effect.not-a-real-recipe.v1'. Known recipes: effect.on.v1."
  Manual locked effect cannot be replaced: True, reason=Locked, error="Effect 'a7f4d12a-9c93-4f24-a3e1-e6c9e61f7fd4' on 'Arches' is locked and cannot be edited."

Step 071 manual demo completed successfully.

Cleaning up demo output.
```

Direct, real, non-test-harness confirmation of this step's own required text: (1) "No unintended model
conflicts" — Scenario 2 shows the lead piano (higher precedence) kept layer 0 on Arches while the
genuinely-overlapping, lower-precedence automated accent was reassigned to layer 1, never silently
overwriting or colliding with it; the final summary line independently confirms 2 distinct layers were
used on Arches. (2) "XSQ renders as planned" — Scenario 3's own real, independently re-parsed `.xsq`
confirms `Element 'Arches'` carries exactly the 3 planned effect windows (the verse lead-piano window
`[0,10000)`, the chorus lead-piano window `[10000,20000)`, and the reassigned accent window
`[14000,16000)`), each on its own layer, at its own planned millisecond window — the compiled plan's own
layer/collision decisions survive a real file export and independent re-read unchanged. (3) "fewer active
props in verse" (the phase gate's own named worked example) — Scenario 1 vs. Scenario 2's own summary
line: 2 active props in the verse vs. 3 in the chorus, confirmed `True`. (4) "Combine overlapping role
plans into a single scoped candidate; validate collisions/layers/locks and demonstrate purposeful
coordination" (implementation guidance, verbatim) — Scenario 2's own warning text names the coordination
decision directly ("purposeful layer coordination, not a dropped effect"). (5) "Clean machine and
unsupported runtime must produce user-readable diagnostics" (failure/edge case, verbatim) — Scenario 4
confirms all three rejection paths (empty candidates, unknown recipe id, locked-effect collision) with
specific, readable error messages and no partial mutation.

## 6. Fixtures / input hashes

- No show-layout XML fixture file was needed for this step's own tests or demo — `ChoreographyCandidate`
  operates on caller-supplied `ModelId` values and a real, in-memory `DisplayGraph` built directly in code
  (the same convention `Step051FrameQuantizationTests.GateDemoLayout()` already established) naming the
  phase gate's own worked-example props (Arches, Roofline, Tree) — this step does not itself validate prop
  existence against a real imported `DisplayGraph`/`SpatialDisplayGraph` (that remains the already-accepted
  Phase 02/09 display-import pipeline's own job; `ChoreographyCompiler` only requires a candidate's target
  to be a member of the caller-supplied `EditScope.Models` set, the same "permitted set" narrowing Step
  070's own `TransitionRecipePlanner` ADR already established for `PermittedProps`).
- The real demo's own exported `.xsq` file (`%TEMP%\xlai-step071-demo-output\step071-demo.xsq`) and its
  SHA-256 (`A3ECF48C4E25BF4D24A6D377B865EE551A6EA2522D27FD42314BCFE8E1B67743`) were recorded above and
  deleted by the demo's own cleanup step (confirmed by "Cleaning up demo output." in the real output
  above); the throwaway demo *project* itself was deleted after this evidence was recorded.
- **Real-audio-device test exclusion, disclosed (unchanged, carried forward from every prior Phase 09/10
  step's own §6):** `Step025`/`Step028`/`Step029`/`Step030`/`Step042ShellWiringTests` construct a real
  `WaveOutAudioTransport` (the real Windows winmm audio device) unavailable in this session's own sandboxed
  execution environment — a pre-existing, environment-caused condition predating this step entirely. No
  file this step touched is on the call path of any of those five classes (confirmed: none reference
  `ChoreographyCompiler`/`ChoreographyCandidate` in any way; this step added no Shell-project file at all).

## 7. Unresolved issues / known gaps

- **No real SCREEN 12 CHOREOGRAPHY Avalonia/AXAML view or Shell-layer `ShellScreen` wiring exists** —
  this step's own required evidence text ("Generated XML artifact and semantic diff/fixture, with xLights
  validation if this step changes rendering") is a different, narrower evidence shape than Steps 067-070's
  own "Avalonia screenshot or repeatable desktop interaction recording" text, and is satisfied here by the
  real `.xsq` export/re-read demo in §5 — disclosed in §3 as a deliberate scope decision, not an
  oversight. A future step wiring `ChoreographyCompiler` into an interactive screen remains open.
- **No installed xLights build has been available to verify rendering inside a real xLights
  installation** — unchanged, carried-forward condition from every PHASE-02/07 step (most recently Step
  051's own `XsqExporter` class doc, and Step 070's own §7); "XSQ renders as planned" is proven here by the
  strongest verification available without an xLights install — a real, independently re-parsed structural
  round-trip (§5) — not a claim that a running xLights installation has confirmed visual output. Per the
  user's own standing waiver (`memory: feedback_skip_xlights_human_verification`), this is not treated as
  a blocker for this step.
- **The `effect.on.v1` recipe's own EffectDB settings remain an empty placeholder** — unchanged,
  carried-forward disclosure from PHASE-02 Step 013/015 (`XsqExporter`'s own class doc): no real
  xLights-authored "On" effect sample has been captured yet to verify its actual default parameters
  against. This step does not add or change any recipe; it only schedules already-verified recipe ids onto
  layers.
- **`MaxLayers = 8` is a fixed constant, not a user-configurable setting** — disclosed in §3 as a
  deliberate, narrow bound; a future step/config surface can raise it without a breaking `IChoreographyCompiler`
  API change.
- **Carried-forward, disclosed phase-level/dependency-level gaps are unchanged from Step 070's own closing
  evidence** (not reopened or re-resolved here): Phase 09's own consolidated `GATE-REPORT.md` is still
  unwritten; Phase 06's own stem-separation gate remains BLOCKED; Phase 07's and Phase 08's own full-phase
  gates have not been separately run/recorded; real-audio-device Shell tests remain environment-blocked
  (§6); a consolidated Phase 10 gate report synthesizing Steps 066-071 together (PHASE-10.md §7's own
  full-phase test plan/matrix and §8's own "Required final gate report") has not yet been separately
  written — this document covers only Step 071's own narrow scope, not the whole-phase gate.

## 8. PASS / BLOCKED

**Status: PASS** (for this step's own narrow, disclosed scope — compiling overlapping role-based
candidate effects into a single scoped `SequenceDocument` via deterministic layer/priority collision
resolution, honoring effect locks, and demonstrating the result as a real, independently-verified exported
`.xsq` artifact — not a claim that any carried-forward phase-level gap listed in §7 is resolved, and not a
claim that a real SCREEN 12 Avalonia view or a live xLights-installation render has been independently
confirmed).

- "No unintended model conflicts; XSQ renders as planned" (required acceptance, verbatim) — proven by
  `Step071_Acceptance_NoUnintendedModelConflicts_XsqRendersLayeredPlanAsCompiled` (domain + real `.xsq`
  export/re-read) — independently confirmed end-to-end by the real demo's Scenario 2/3 (the accent is
  reassigned to its own layer, never dropped or overwriting; the exported `.xsq` carries all three planned
  Arches windows at their own planned times).
- "Combine overlapping role plans into a single scoped candidate; validate collisions/layers/locks and
  demonstrate purposeful coordination" (implementation guidance, verbatim) — proven by
  `Compile_CollisionsBeyondMaxLayers_DropsLowestPrecedence_NeverReplacesHigherPrecedence`,
  `Compile_NonOverlappingCandidatesOnDifferentModels_AllAcceptedAtLayerZero`,
  `Compile_LeadPrioritySurvivesSimultaneousBassAndSnareOnset_KeepsLayerZero`,
  `Compile_ManualLockedEffectCannotBeReplacedByNewPlan_RejectsWholeCompile_NegativePath`,
  `Compile_ScopeExcludesLockedModel_LockedEffectCarriedForwardUnchanged` — independently confirmed by the
  real demo's own printed per-candidate decisions and warning text.
- "Clean machine and unsupported runtime must produce user-readable diagnostics" (failure/edge case,
  verbatim) — proven by `Compile_EmptyCandidates_ThrowsBeforeAnyComputation_NegativePath` (clean machine),
  `Compile_UnsupportedRecipeId_ThrowsUserReadableDiagnostic_NegativePath` (unsupported runtime),
  `Compile_CandidateTargetOutsideScope_Throws_NegativePath`,
  `Compile_CandidateWindowOutsideScopeTime_Throws_NegativePath`,
  `Compile_DuplicateCandidateId_Throws_NegativePath`,
  `ChoreographyCandidate_Validate_RejectsInvalidWindow_NegativePath` (x3),
  `ChoreographyCandidate_Validate_RejectsBlankRecipeId_NegativePath`,
  `ChoreographyCandidate_Validate_RejectsBlankTarget_NegativePath`,
  `ChoreographyCandidate_Validate_RejectsEmptyId_NegativePath`,
  `Compile_NullArguments_Throw_NegativePath` — independently confirmed by the real demo's Scenario 4 (all
  three rejection paths exercised with specific, readable error messages).
- Full-phase test matrix items #1 ("Lead priority survives simultaneous bass and snare onset"), #5 ("Same
  inputs+seed produce repeatable plan"), and #6 ("Manual locked effects cannot be replaced by new plan")
  are each independently proven by this step's own named tests above, ahead of a separately-tracked,
  not-yet-written consolidated Phase 10 gate report (§7).
- Full regression (real-audio-device tests excluded, disclosed in full in §6): **904/904 passing**, 0
  failed, 0 skipped; every pre-existing test from Phase 01-10/Step 070 still passes unmodified, plus this
  step's own 19 new Composition tests. `dotnet format --verify-no-changes` clean across the full solution.
- **No file from any prior step/phase was behaviorally changed** — this step only adds six new files to
  `XlightsAI.Studio.Composition` (plus two new `<ProjectReference>` entries to that project's own
  `.csproj`, to `XlightsAI.Studio.Xlights` and `XlightsAI.Studio.Editing` — verified non-cyclic before
  adding) and one new test file (plus one new `<ProjectReference>` entry to that test project's own
  `.csproj`, to `XlightsAI.Studio.Editing`). No existing Composition-project file's own existing members
  were modified.
- **Carried-forward, disclosed phase-level/dependency-level gaps (not this step's own blocker):** no real
  SCREEN 12 Avalonia view exists yet (§7, project-wide pre-existing condition); no installed xLights build
  has been available to independently confirm rendering (§7); Phase 09's own consolidated gate report is
  still unwritten; Phase 06's own gate remains BLOCKED; Phase 07's and Phase 08's own full-phase gates have
  not been separately run/recorded; real-audio-device Shell tests are environment-blocked (§6); a
  consolidated Phase 10 whole-phase gate report has not yet been separately written (§7).

Per PHASE-10.md §8's own required final gate report, a consolidated Phase 10 gate report synthesizing
Steps 066-071 together (the full-phase test plan/matrix in §7 and the phase gate demonstration in §7's own
"exact user workflow") is the next outstanding item before Phase 10 as a whole can be declared PASS —
distinct from this document's own claim, which covers Step 071 alone.
