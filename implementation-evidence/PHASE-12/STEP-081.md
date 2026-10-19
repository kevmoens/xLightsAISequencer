# PHASE-12 Step 081 — Implement quality evaluators: timing, melodic alignment, density, motif reuse, legal effects

**Date:** 2026-10-18
**Spec:** `docs/phase-specs/PHASE-12.md` §6, Step 081
**Required acceptance (verbatim):** "Known good/bad fixtures produce expected scores and findings."
**Required failure/edge case (verbatim):** "Silence, invalid timing boundaries and missing media must be handled explicitly."

## 1. Pre-implementation baseline inspection

Before writing any code, the existing repository state was read directly (not taken on faith):

- Read `docs/phase-specs/PHASE-12.md` in full (already read for Steps 079/080; re-confirmed Step 081's own heading, implementation guidance, required acceptance/failure text, PHASE-12 Section 7 cross-cutting test #7 ("Quality metric evaluator-version mismatch flagged") and Section 12's bounded-refinement pseudocode, which names `BetterThan` as needing same-evaluator-version comparability).
- Read `implementation-evidence/PHASE-12/STEP-079.md` and `STEP-080.md` in full. Step 079's own disclosed ADR (Section 3.1) establishes that `ChoreographyPlan`/`QualityReport` do not exist yet and that Steps 080/081 are expected to extend `CandidateAttempt` additively. Step 080's own disclosed ADR (Section 3.1) already narrowed the spec's proposed `ICandidateGenerator.GenerateAsync(LoopRun, Candidate?, ct)` to use this repository's own `CandidateAttempt` in place of the spec's proposed `Candidate`/`ChoreographyPlan` — this step's own `IChoreographyQualityEvaluator` is narrowed the identical way, disclosed below (§3).
- Read every file in `src/XlightsAI.Studio.Refinement/` as it exists today (`LoopRun.cs`, `CandidateAttempt.cs`, `RefinementStatus.cs`, `RefinementStatusTransitions.cs`, `IRefinementRunStore.cs`, `JsonRefinementRunStore.cs`, `RefinementClaim.cs`, `ISeededCandidateGenerator.cs`, `DeterministicGestureCandidateGenerator.cs`, `DeterministicContent.cs`, both exception types) and confirmed no `IChoreographyQualityEvaluator`/`QualityReport`/`MetricValue` exists anywhere in the repository yet (via `Grep`) — this step's own scope, not already partially implemented.
- Read `src/XlightsAI.Studio.Composition/TransitionGesture.cs` and `TransitionKind.cs` (PHASE-10, already accepted) — `TransitionGesture.Validate()`'s own invariants (non-empty, non-duplicate `TargetProps`; `[Start,End)` window; `[0,1]` intensities; `Blackout` always darkens to exactly `(0,0)`) are reused unmodified as the per-gesture half of this step's own "legal effects" hard gate.
- Read `src/XlightsAI.Studio.Editing/EditScope.cs` — confirmed `Models` (`IReadOnlySet<ModelId>`), `Time` (`TimeWindow`), and `RespectLocks` (defaults `true`) are the exact fields this step's own "time/model scopes" and lock hard gates needed; no new scope concept was invented.
- Read `src/XlightsAI.Studio.Xlights/EffectInstance.cs` — confirmed `Locked` (PHASE-03's own effect-lock bit, added Step 019) and `Target`/`StartTicks`/`EndTicks` are exactly what a "would this gesture overwrite an already-locked effect" hard gate needs; no new lock concept was invented. Confirmed via `Grep` that `XlightsAI.Studio.Xlights` does not reference `XlightsAI.Studio.Refinement` anywhere (not a cyclic reference).
- Read `src/XlightsAI.Studio.Rhythm/BeatGrid.cs`/`BeatMarker.cs` (PHASE-05, already accepted) — `Beats` (ordered `BeatMarker[]`, each carrying `TimeTicks`) is reused unmodified as the real analysis input behind the "timing" soft metric; confirmed via `Grep` that `Rhythm` does not reference `Refinement`.
- Read `src/XlightsAI.Studio.Melody/MelodyTrack.cs`/`NoteEvent.cs` (PHASE-07, already accepted) — `Notes` (ordered `NoteEvent[]`, each carrying `StartTicks`/`EndTicks`/`MidiNote`) is reused unmodified as the real analysis input behind the "melodic alignment" soft metric; confirmed via `Grep` that `Melody` does not reference `Refinement`.
- Read every `src/*/*.csproj` touched by this step (`Refinement`) and the test project's `.csproj`, and confirmed (via the `Grep` checks above) that adding `ProjectReference`s from `Refinement`/`Refinement.Tests` to `Rhythm`, `Melody`, and `Xlights` introduces no cyclic reference.
- Read `tests/XlightsAI.Studio.Refinement.Tests/Step079AcceptanceTests.cs` and `Step080AcceptanceTests.cs` in full to confirm this step's own tests follow the same conventions (full named-argument record construction, deterministic in-test fixtures rather than separate fixture files, real types rather than mocks).

## 2. Scope actually implemented

All new files are in `src/XlightsAI.Studio.Refinement/` (plus new project references) and `tests/XlightsAI.Studio.Refinement.Tests/`. No existing file from any prior step was modified.

| File | Purpose |
|---|---|
| `MetricValue.cs` (new) | PHASE-12 Section 3's own proposed signature, realized verbatim: `MetricValue(string Name, double Value, double Weight, bool HardGate, string Explanation)`, with a `Validate()` enforcing `Value`/`Weight` ranges, a non-blank `Name`/`Explanation`, and that a hard-gate metric's `Value` is always exactly `0.0` or `1.0` (never partial credit — PHASE-12 Section 3: "Hard gates cannot be traded for weighted scoring"). |
| `QualityReport.cs` (new) | PHASE-12 Section 3's own proposed signature, realized verbatim: `QualityReport(Guid CandidateId, string EvaluatorVersion, IReadOnlyList<MetricValue> Metrics, IReadOnlyList<string> Violations)`, with a `Validate()` that rejects an internally-inconsistent report (a failed hard-gate metric with no corresponding `Violations` entry), plus a static `AreComparable(QualityReport, QualityReport)` realizing PHASE-12 Section 7 cross-cutting test #7 ("Quality metric evaluator-version mismatch flagged") and Section 12's own pseudocode note ("`BetterThan` must compare quality reports produced with the same evaluator version"). |
| `ChoreographyQualityContext.cs` (new) | Optional, independently-nullable real analysis inputs (`BeatGrid?`, `MelodyTrack?`, `IReadOnlyList<EffectInstance>? LockedEffects`, `IReadOnlyList<CandidateAttempt>? PriorAttempts`) a caller supplies when available; a static `Empty` singleton for when none are. |
| `IChoreographyQualityEvaluator.cs` (new) | PHASE-12 Section 3's own proposed `IChoreographyQualityEvaluator.EvaluateAsync(Candidate, ct)`, narrowed (disclosed ADR §3 below) to `EvaluateAsync(LoopRun run, CandidateAttempt candidate, ChoreographyQualityContext context, CancellationToken ct)`. |
| `GestureQualityEvaluator.cs` (new) | The real implementation — see §2.1 below for each of the five named dimensions. `EvaluatorVersion = "gesture-quality.v1"` (a `public const string`, consulted by tests and the demo). |
| `XlightsAI.Studio.Refinement.csproj` (extended) | Added `ProjectReference`s to `XlightsAI.Studio.Rhythm`, `XlightsAI.Studio.Melody`, and `XlightsAI.Studio.Xlights` (verified non-cyclic — §1). |

New test file `tests/XlightsAI.Studio.Refinement.Tests/Step081AcceptanceTests.cs` — 15 new tests (12 `[Fact]` methods, some asserting multiple sub-cases) against the real `GestureQualityEvaluator`, with directly-constructed, deterministic `LoopRun`/`CandidateAttempt`/`TransitionGesture`/`BeatGrid`/`MelodyTrack`/`EffectInstance` fixtures (no mocking library; the same convention Steps 079/080 already established). `Step081AcceptanceTests` is the "known good/bad fixtures" this step's own required acceptance names, verbatim. `tests/.../XlightsAI.Studio.Refinement.Tests.csproj` gained matching new `ProjectReference`s.

### 2.1 The five named dimensions, as actually implemented

1. **Legal effects (hard gate).** Fails (`Value = 0.0`, entry added to `Violations`) when: the candidate already carries an upstream violation (a prior step's own rejected/malformed generation); the candidate carries no gestures at all (this step's own "Silence... must be handled explicitly" edge case); any gesture fails its own already-accepted `TransitionGesture.Validate()`; any gesture targets a prop outside `run.Scope.Models`; any gesture's window falls outside `run.Scope.Time`; or (when `run.Scope.RespectLocks` is `true`) any gesture overlaps, in both time and target prop, an already-locked `EffectInstance` supplied via `ChoreographyQualityContext.LockedEffects`. Passes (`Value = 1.0`) only when none of the above holds.
2. **Density (soft).** The fraction of `run.Scope.Models` the candidate's gestures actually target, in `[0, 1]` — the concrete, always-computable metric behind the phase gate's own "knowingly over-busy 8-bar phrase" scenario (§4 demo, first scenario).
3. **Timing (soft).** How closely each gesture's own start/end ticks snap to the nearest real `BeatGrid` beat marker, normalized by the grid's own average beat period. Reports `Weight = 0` with an explicit "no BeatGrid supplied" / "fewer than two beat markers" explanation when no usable beat grid is supplied — this step's own "invalid timing boundaries... must be handled explicitly" edge case.
4. **Melodic alignment (soft).** Whether a gesture's own intensity-ramp direction (`StartIntensity` → `EndIntensity`) agrees with the melody's own pitch-contour direction (first → last overlapping `NoteEvent.MidiNote`) across the gesture's window. Reports `Weight = 0` with an explicit explanation when no melody, an empty melody, or no notes overlapping the gesture's own window is available — this step's own "missing media must be handled explicitly" edge case.
5. **Motif reuse (soft).** The best-matching shape similarity (`TransitionKind` equality plus intensity closeness) between this candidate's own gesture and every non-violating prior sibling attempt supplied via `ChoreographyQualityContext.PriorAttempts`. Reports `Weight = 0` with an explicit explanation when no eligible priors are supplied (e.g. the run's first attempt).

## 3. Architecture deviations / decisions (ADRs)

1. **`IChoreographyQualityEvaluator.EvaluateAsync` takes `(LoopRun run, CandidateAttempt candidate, ChoreographyQualityContext context, CancellationToken ct)`, not the spec's own proposed `(Candidate candidate, CancellationToken ct)`.** Three disclosed narrowings, each forced by an already-accepted prior-step fact, not invented speculatively: (a) `CandidateAttempt` stands in for the spec's own proposed `Candidate`/`ChoreographyPlan`, which do not exist in this repository (Step 079's own disclosed ADR, carried forward unchanged by Step 080); (b) the owning `LoopRun` is required as a separate parameter because the "time/model scopes" hard gate this step's own implementation guidance names, verbatim, lives on `LoopRun.Scope`, not on a `CandidateAttempt` — the spec's own single-parameter signature has nowhere to carry it, and duplicating `Scope` onto every `CandidateAttempt` would make two independently-mutable copies of the same boundary; (c) `ChoreographyQualityContext` is a new, wholly additive, independently-nullable bundle of real optional analysis inputs (`BeatGrid`/`MelodyTrack`/locked effects/prior attempts) — none of PHASE-12 Section 3's own proposed signatures anticipates these (the spec's own proposed `IChoreographyQualityEvaluator` has no parameter for any analysis input at all), but this step's own required failure/edge case ("missing media must be handled explicitly") is unsatisfiable without a place to explicitly supply, or explicitly omit, that media.
2. **`QualityReport`/`MetricValue` needed no narrowing** — unlike `Candidate`/`LoopRun` in Steps 079/080, the spec's own proposed signatures for both types (PHASE-12 Section 3) map onto exactly what this step's own evaluator produces, with no dead/unfillable field. Both are declared exactly as the spec proposes them, verbatim field-for-field.
3. **A failed hard gate must always also appear in `QualityReport.Violations` — enforced by `QualityReport.Validate()` itself, not left to caller discipline.** PHASE-12 Section 3: "Reject candidate on scope/lock, invalid timing, missing recipe or render failures regardless of aesthetic score." A `QualityReport` that reports a `0.0`-valued `HardGate` metric with an empty `Violations` list would be exactly the kind of inconsistent, silently-escapable state a future consumer (Step 082's repair strategy, Step 083's orchestrator, Step 084's UI) could misread as "clean" — so this invariant is enforced structurally, the same way `CandidateAttempt`'s own `Violations` list is already the sole gate Step 079's `JsonRefinementRunStore.SaveRunStateAsync` trusts for `BestCandidateId` eligibility.
4. **A soft metric with no supplied analysis input reports a fixed `Value = 0.5` and `Weight = 0.0`, never a fabricated confident score and never a special sentinel like `NaN`.** This step's own required failure/edge case, verbatim: "missing media must be handled explicitly." `Weight = 0` is the signal a consumer must check before using `Value` in any aggregate comparison (documented on `MetricValue.Weight` itself); `Value = 0.5` (rather than, say, `0.0`) keeps `MetricValue.Validate()`'s own `[0,1]` range invariant uniform across every metric without special-casing the "no data" case, and is never read when `Weight = 0` is respected.
5. **"Motif reuse" is scored against sibling attempts within the same run (`ChoreographyQualityContext.PriorAttempts`), not against PHASE-08's own `MotifLink`/`MotifClusteringAnalysis` song-structure concept.** PHASE-12's own Section 3 names "motif reuse" as one of this phase's quality dimensions without specifying which motif concept it refers to, and this phase's own owned boundary (Section 1) is "deterministic candidate generation, technical scoring... and candidate comparison" over bounded refinement attempts — not song-structure analysis. Reusing PHASE-08's `MotifLink` would require wiring an unrelated song-structure pipeline into every refinement run before this step could be exercised at all (a far larger, undisclosed scope); scoring a generated gesture's own shape similarity against this run's own prior attempts is the smallest complete reading of "motif reuse" that is actually testable today, exactly as a bounded refinement loop (PHASE-12 Section 3: "targeted repair strategy for weakest failing metric") would want to measure "is this run converging on a shape, or thrashing."
6. **No existing accepted contract from any prior phase was modified.** `TransitionGesture`/`TransitionKind` (PHASE-10), `EditScope`/`ModelId`/`TimeWindow` (PHASE-01/03), `EffectInstance` (PHASE-02/03), `BeatGrid`/`BeatMarker` (PHASE-05), `MelodyTrack`/`NoteEvent` (PHASE-07) are consumed unchanged, read-only. `LoopRun`/`CandidateAttempt`/`RefinementStatus`/`RefinementStatusTransitions`/`IRefinementRunStore`/`JsonRefinementRunStore`/`ISeededCandidateGenerator`/`DeterministicGestureCandidateGenerator` (Steps 079/080) are consumed unchanged — this step adds five new files and two project references; it does not touch the state machine, the store, or the generator.

## 4. Commands actually executed

All commands were run via the **PowerShell** tool (the Bash tool returns no usable output in this sandboxed session — re-confirmed at the start of this step, consistent with every prior PHASE-11/Step-079/080 evidence doc).

```
> dotnet build src\XlightsAI.Studio.Refinement\XlightsAI.Studio.Refinement.csproj -v minimal
Build succeeded. 0 Warning(s) 0 Error(s)

> dotnet build tests\XlightsAI.Studio.Refinement.Tests\XlightsAI.Studio.Refinement.Tests.csproj -v minimal
Build succeeded. 0 Warning(s) 0 Error(s)

> dotnet test tests\XlightsAI.Studio.Refinement.Tests\XlightsAI.Studio.Refinement.Tests.csproj -v minimal
Passed! - Failed: 0, Passed: 32, Skipped: 0, Total: 32, Duration: 623 ms
  (20 pre-existing Step 079/080 tests, unmodified, + 12 new Step 081 tests)

> dotnet build XlightsAI.Studio.slnx -v minimal   (full solution, after change)
Build succeeded. 0 Warning(s) 0 Error(s)

> dotnet test XlightsAI.Studio.slnx -v minimal   (full solution regression, after change)
XlightsAI.Studio.Xlights.Tests.dll      : 74/74 passed
XlightsAI.Studio.Editing.Tests.dll      : 91/91 passed
XlightsAI.Studio.Display.Tests.dll      : 56/56 passed
XlightsAI.Studio.Melody.Tests.dll       : 117/117 passed
XlightsAI.Studio.Refinement.Tests.dll   : 32/32 passed   (was 20/20 before this step)
XlightsAI.Studio.Structure.Tests.dll    : 78/78 passed
XlightsAI.Studio.Composition.Tests.dll  : 78/78 passed
XlightsAI.Studio.Projects.Tests.dll     : 25/25 passed
XlightsAI.Studio.AiEdits.Tests.dll      : 79/79 passed
XlightsAI.Studio.Audio.Tests.dll        : 38/38 passed
XlightsAI.Studio.Shell.Tests.dll        : 263/263 passed
XlightsAI.Studio.Rhythm.Tests.dll       : 82/82 passed
XlightsAI.Studio.Reference.Tests.dll    : 31/31 passed
XlightsAI.Studio.Stems.Tests.dll        : 101/101 passed
TOTAL: 1145/1145 passed, 0 failed   (1133 baseline-after-Step-080 + 12 new, 0 removed, 0 modified)

> dotnet format XlightsAI.Studio.slnx --verify-no-changes --no-restore
(no output -- no formatting violations)

> git status --short
(unchanged from before this step: everything still untracked, nothing committed in this repository yet;
no prior file was modified or staged by this step)
```

Observable desktop demonstration (disposable console app, not committed to the repository, `%TEMP%\xlai-step081-demo-proj` — the same convention every prior PHASE-11/Step-079/080 evidence doc used):

```
> New-Item -ItemType Directory -Force -Path "$env:TEMP\xlai-step081-demo-proj"
> # wrote demo.csproj + Program.cs referencing the real Refinement/Editing/Contracts/Composition/Rhythm/Melody/Xlights projects
> cd "$env:TEMP\xlai-step081-demo-proj"
> dotnet run -v minimal
```

Full transcript (abridged to remove restore/build noise):

```
=== PHASE-12 Step 081 demo: quality evaluators (timing, melodic alignment, density, motif reuse, legal effects) ===

--- Phase gate scenario: knowingly over-busy 8-bar phrase vs a sparser alternative ---
-- Candidate A: generator output targeting every permitted prop (over-busy) (EvaluatorVersion=gesture-quality.v1) --
   LegalEffects       Value=1.00 Weight=1.0 HardGate=True  :: 1 gesture(s) validated, scoped to permitted props/time, with no upstream violations.
   Density            Value=1.00 Weight=1.0 HardGate=False :: 8/8 permitted prop(s) targeted (100%).
   Timing             Value=0.50 Weight=0.0 HardGate=False :: no BeatGrid supplied.
   MelodicAlignment   Value=0.50 Weight=0.0 HardGate=False :: no MelodyTrack supplied.
   MotifReuse         Value=0.50 Weight=0.0 HardGate=False :: no prior, non-violating sibling attempts supplied to compare motif reuse against.
   Violations: (none)
-- Candidate B: hand-scoped alternative targeting 2/8 props (less busy) (EvaluatorVersion=gesture-quality.v1) --
   Density            Value=0.25 Weight=1.0 HardGate=False :: 2/8 permitted prop(s) targeted (25%).
   ... (LegalEffects/Timing/MelodicAlignment/MotifReuse rows omitted here for brevity; full in original run)
PASS: over-busy candidate has strictly higher Density than the sparser alternative: True (1.00 vs 0.25)

--- Timing: perfectly beat-snapped window scores higher than an offset window ---
-- Snapped-to-beat window --
   Timing             Value=1.00 Weight=1.0 HardGate=False :: Average gesture boundary offset from the nearest beat is 0% of one beat period (500000 ticks).
-- Offset-from-beat window --
   Timing             Value=0.60 Weight=1.0 HardGate=False :: Average gesture boundary offset from the nearest beat is 40% of one beat period (500000 ticks).
PASS: snapped window Timing score (1.00) exceeds offset window (0.60): True

--- Melodic alignment: rising melody agrees with Build, disagrees with Release ---
-- Build gesture (rising intensity) against rising melody --
   MelodicAlignment   Value=1.00 Weight=1.0 HardGate=False :: 1 gesture(s) compared against overlapping melody notes; average direction-agreement score 1.00.
-- Release gesture (falling intensity) against rising melody --
   MelodicAlignment   Value=0.00 Weight=1.0 HardGate=False :: 1 gesture(s) compared against overlapping melody notes; average direction-agreement score 0.00.

--- Legal effects hard gate: out-of-scope prop and locked-effect overlap are both rejected ---
-- Out-of-scope prop candidate --
   LegalEffects       Value=0.00 Weight=1.0 HardGate=True  :: gesture '...' targets prop(s) outside the run's permitted scope: MegaTree.
-- Locked-effect-overlapping candidate --
   LegalEffects       Value=0.00 Weight=1.0 HardGate=True  :: gesture '...' overlaps locked effect '...' on prop 'Arches'.

--- Negative path: silence (empty gestures from a malformed/empty-scope generation) ---
-- Silent/empty-scope candidate --
   LegalEffects       Value=0.00 Weight=1.0 HardGate=True  :: candidate already carries 1 upstream violation(s). candidate carries no gestures to evaluate (silence/empty generation).
   Violations: Edit scope names no permitted models; a candidate requires at least one target model. | Legal effects hard gate failed: ...
PASS: silence is handled explicitly, never crashes, and fails the legal gate: True

--- Negative path: missing media reports Weight=0, never a fabricated confident score ---
PASS: every metric needing absent analysis input reports Weight=0: True

--- Evaluator-version mismatch is flagged (PHASE-12 Section 7 cross-cutting test #7) ---
PASS: two reports from different EvaluatorVersions are correctly flagged as not comparable: True

=== Demo complete ===
```

This demo exercises this step's own required acceptance end to end against the phase gate's own named scenario (an over-busy candidate generated by the real Step 080 `DeterministicGestureCandidateGenerator` scores a strictly higher `Density` than a sparser hand-scoped alternative — the exact comparison the phase gate's own blocking demonstration needs: "generate three candidates, inspect density/timing metrics... accept one"), proves the "timing" metric against a real `BeatGrid`, proves "melodic alignment" against a real `MelodyTrack` (showing both agreement and disagreement, not only the favorable case), proves the "legal effects" hard gate rejects both an out-of-scope prop and a locked-effect overlap, proves both required negative paths (silence never crashes and fails the legal gate explicitly; missing media never fabricates a confident score), and proves the evaluator-version-mismatch cross-cutting test. The demo project and its `bin`/`obj` output were created entirely under `%TEMP%`, run, and then deleted (`Remove-Item -Recurse -Force`); it is not part of this repository's tracked tree.

## 5. Persisted-output inspection / observable demonstration

This step's own scope is scoring, not persistence — `QualityReport` is not yet wired into `IRefinementRunStore` (no `CandidateAttempt.Report` field exists; `LoopRun`/`CandidateAttempt` were not extended by this step at all — see §7). The "persisted-output inspection" instead takes the form of the real, already-accepted Step 080 `DeterministicGestureCandidateGenerator`'s own output being fed directly into this step's own real `GestureQualityEvaluator` (demo §4, first scenario: `busyCandidate` is generated, not hand-constructed), proving these two steps compose correctly against real generator output, not only against hand-built fixtures. `Step081AcceptanceTests` and the demo both construct and inspect real `QualityReport`/`MetricValue` object graphs (never a mock) and assert on their actual field values.

## 6. Fixtures / input hashes

No fixture files were added to `fixtures/` or `src/XlightsAI.Studio.Reference/Fixtures/` — this step's own required acceptance ("known good/bad fixtures produce expected scores and findings") is satisfied by deterministic, directly-constructed in-test fixtures (`Step081AcceptanceTests`'s own `Scope`/`NewQueuedRun`/`Attempt`/`Gesture`/`BeatGridWith` helpers), the same convention Steps 079/080 already established for this phase (no separate fixture JSON corpus exists for `Refinement` yet). Every `BeatGrid`/`MelodyTrack`/`EffectInstance`/`TransitionGesture` fixture value is a real, fully-validated instance of its own already-accepted type (`BeatGrid`/`MelodyTrack` carry no `Validate()` of their own to call, but every `TransitionGesture`/`EffectInstance` field used is independently real and schema-correct), not a hand-waved stand-in.

## 7. Unresolved issues / known gaps

- **`CandidateAttempt`/`LoopRun` were not extended with a `Report`/`EvaluatorVersion` field by this step.** The spec's own proposed `Candidate(..., QualityReport? Report)` (PHASE-12 Section 3) carries the evaluated report alongside the candidate; this step produces `QualityReport` values but does not yet wire one back onto a saved `CandidateAttempt` or persist it via `IRefinementRunStore`. No required acceptance/failure text for *this* step names persistence of the *report itself* (Step 079's own "hashes, seeds and state transitions" persistence scope is already complete; only the attempt's own structural content, not its score, is persisted so far) — wiring `QualityReport` into the saved attempt record (an additive field, following the same pattern as `LoopRun.GestureFamily`/`CandidateAttempt.Gestures` in Step 080) is left for whichever of Steps 082–084 first needs a *persisted* quality report (Step 082's repair strategy and Step 084's candidate gallery both plausibly need one; this step deliberately does not guess which shape that extension should take before either of those steps' own acceptance text exists to pin it down — `CLAUDE.md`'s own "no half-finished implementations... no designing for hypothetical future requirements" rule).
- **No `IRefinementOrchestrator` drives this evaluator automatically yet** — nothing in this step calls `EvaluateAsync` in a loop against a `LoopRun`'s own attempts; that orchestration is Step 083's own named scope, consistent with Steps 079/080's own disclosed gaps.
- **No real repair strategy exists yet** — this step only *scores* a candidate; selecting a targeted mutation for the weakest-scoring metric is Step 082's own named scope. `MetricValue.Name`/`Value` already carry exactly what a repair strategy would need to identify "which dimension is weakest," but no consumer reads them that way yet.
- **"Motif reuse" is scored against same-run sibling attempts, not PHASE-08's own song-structure `MotifLink` concept** — disclosed as ADR §3.5. Revisiting this (e.g. additionally rewarding/penalizing reuse of an actual `MotifLinkCandidate`-identified song-structure motif) remains straightforward if a later step's own acceptance text requires it — `ChoreographyQualityContext` is additive and already has room for a new optional field.
- **No `IEffectRecipeRegistry`/xLights-XML-level "legal effects" check** — this step's "legal effects" hard gate validates `TransitionGesture`'s own structural invariants and scope/lock containment (the only concrete things a pre-XML-compilation gesture plan can be checked against); it does not yet validate against `EffectRecipeRegistry.IsSupportedParameterValue` or any compiled xLights effect-settings XML, because `TransitionGesture` → xLights effect XML compilation remains out of this step's own named scope (PHASE-10 Step 071's own already-disclosed scope covers compiling an *accepted* `ChoreographyCandidate`, not a `CandidateAttempt` mid-refinement-loop). This step's own required user-visible outcome text ("Generated XML artifact and semantic diff/fixture, with xLights validation **if this step changes rendering**," emphasis added) is explicitly conditional — this step does not change rendering (no new XML is emitted or compiled anywhere in this step's own code), so no xLights-XML artifact/validation is claimed; this is a disclosed non-claim, not a silently skipped requirement.
- **No real SCREEN 15 COMPARE / SCREEN 14 AI EDIT Avalonia view exists yet** — this step's own required user-visible outcome is satisfied by the real console demonstration (§4) and the real test suite (§2), not a UI; the candidate-gallery/preview UI itself remains Step 084's own named scope.

## 8. Required-acceptance-text-by-text tie-back

| Required text (verbatim) | Where proven |
|---|---|
| "Known good/bad fixtures produce expected scores and findings" | `Step081_Acceptance_KnownGoodFixture_PassesLegalGate_WithExpectedExplanation` / `Step081_Acceptance_KnownBadFixture_OutOfScopeProp_FailsLegalGate_WithFinding`; demo §4's "legal effects" scenario (both an out-of-scope-prop and a locked-effect-overlap bad fixture). |
| This step's own heading, verbatim: "timing" | `Step081_Acceptance_Timing_PerfectBeatSnap_ScoresHigherThanLargeOffset`; demo §4's timing scenario. |
| This step's own heading, verbatim: "melodic alignment" | `Step081_Acceptance_MelodicAlignment_RisingPitchAgreesWithBuild_ButNotRelease`; demo §4's melodic-alignment scenario (both agreement and disagreement shown). |
| This step's own heading, verbatim: "density" | `Step081_Acceptance_Density_ComputesFractionOfPermittedPropsTargeted`; demo §4's phase-gate-aligned over-busy-vs-sparse scenario. |
| This step's own heading, verbatim: "motif reuse" | `Step081_Acceptance_MotifReuse_HighestForShapeMatchingPriorAttempt`. |
| This step's own heading, verbatim: "legal effects" | `Step081_Acceptance_KnownGoodFixture_...` / `Step081_Acceptance_KnownBadFixture_...` / `Step081_Acceptance_LockedEffectOverlap_FailsLegalGate_UnlessRespectLocksIsFalse`; demo §4. |
| Implementation guidance, verbatim: "Implement invariant gates (XML/effect compatibility, time/model scopes, locks) apart from soft music/density metrics with clear units" | `LegalEffects` is the one `HardGate: true` metric (scope/time/lock/effect-structural-validity); every other metric (`Density`/`Timing`/`MelodicAlignment`/`MotifReuse`) is `HardGate: false` with a human-readable `Explanation` carrying its own concrete unit (fraction of permitted props; fraction of one beat period; direction-agreement score; shape-similarity score). |
| Required failure/edge case, verbatim: "Silence... must be handled explicitly" | `Step081_NegativePath_Silence_EmptyGestures_FailsLegalGate_WithExplicitExplanation`; demo §4's silence scenario (generated via the real Step 080 generator against an empty scope). |
| Required failure/edge case, verbatim: "invalid timing boundaries... must be handled explicitly" | `Step081_NegativePath_InvalidTimingBoundary_WindowExceedsScope_FailsLegalGate`. |
| Required failure/edge case, verbatim: "missing media must be handled explicitly" | `Step081_NegativePath_MissingMedia_ReportsWeightZero_NotFabricatedScore`; demo §4's missing-media scenario. |
| PHASE-12 Section 7 cross-cutting test #7, verbatim: "Quality metric evaluator-version mismatch flagged" | `Step081_Acceptance_EvaluatorVersionMismatch_Flagged`; demo §4's final scenario. |
| This step's own additional, disclosed invariant (a failed hard gate must always appear in `Violations`) | `Step081_NegativePath_QualityReportValidate_RejectsFailedHardGateWithNoViolation`. |

## 9. PASS / BLOCKED

**PASS.**

- The required-acceptance sentence ("Known good/bad fixtures produce expected scores and findings") is proven by 12 new passing tests against directly-constructed good and bad fixtures for every one of this step's own five named dimensions, plus a real console demonstration that exercises the phase gate's own "over-busy phrase" scenario end to end against the real Step 080 generator's output.
- Every one of this step's own five named dimensions (timing, melodic alignment, density, motif reuse, legal effects) has a real, independently-testable implementation with a clear unit, proven by both passing/failing fixtures.
- "Silence... must be handled explicitly" and "missing media must be handled explicitly" are both proven never to crash and never to fabricate a confident-looking score — a silent/empty candidate fails the legal gate with an explicit explanation; an absent analysis input reports `Weight = 0` with an explicit explanation, both exercised in tests and in the demo.
- "Invalid timing boundaries... must be handled explicitly" is proven: a gesture window exceeding the run's own permitted time scope fails the legal gate with an explicit explanation naming both windows.
- PHASE-12 Section 7 cross-cutting test #7 ("Quality metric evaluator-version mismatch flagged") is proven via `QualityReport.AreComparable`.
- Full solution build succeeds with 0 warnings/errors; `dotnet format --verify-no-changes` reports no violations.
- Full regression: 1145/1145 tests pass (0 failed) — 1133 pre-existing (0 modified, 0 removed) plus 12 new `Refinement.Tests`.
- No prior accepted contract (any PHASE-01 through PHASE-11 type, or Step 079/080's own `LoopRun`/`CandidateAttempt`/`RefinementStatus`/`RefinementStatusTransitions`/`IRefinementRunStore`/`JsonRefinementRunStore`/`ISeededCandidateGenerator`/`DeterministicGestureCandidateGenerator`/exception types) was modified. This step adds five new files and two new project references only.
- No show file, fixture, or prior persisted artifact was modified.

**Explicitly not claimed:** persistence of a `QualityReport` back onto a saved `CandidateAttempt` (no later step's own acceptance text yet requires this shape — disclosed gap, §7), any xLights-XML-level effect-legality check or compiled-XML artifact (this step does not change rendering — disclosed, §7, quoting this step's own conditional user-visible-outcome text), a repair strategy acting on these scores (Step 082's own named scope), budget/cancellation orchestration calling this evaluator automatically (Step 083's own named scope), or any Avalonia UI/screenshot (Step 084's own named scope).
