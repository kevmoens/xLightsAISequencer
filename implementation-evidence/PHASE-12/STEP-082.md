# PHASE-12 Step 082 — Implement targeted repair strategy and preserve best-so-far candidate

**Date:** 2026-10-18
**Spec:** `docs/phase-specs/PHASE-12.md` §6, Step 082
**Required acceptance (verbatim):** "Repair only addresses reported dimension and respects locks."
**Required failure/edge case (verbatim):** "Malformed proposal, no-improvement and exhausted retry budget must not auto-commit."

## 1. Pre-implementation baseline inspection

Before writing any code, the existing repository state was read directly (not taken on faith):

- Read `docs/phase-specs/PHASE-12.md` in full (already read for Steps 079–081; re-confirmed Step 082's own heading, implementation guidance, required acceptance/failure text, Section 3's architectural design notes ("Use targeted repair strategy for weakest failing metric (timing, density, pitch motion, rest, repeated motif)"), Section 12's bounded-refinement pseudocode (`if (report.Violations.Count == 0 && BetterThan(candidate, best)) best = candidate`), and Section 7 cross-cutting test #3 ("Best candidate retained when later attempt is worse").
- Read `implementation-evidence/PHASE-12/STEP-079.md`, `STEP-080.md`, `STEP-081.md` in full. Step 081's own disclosed ADR §3.5 (motif reuse scored against same-run sibling attempts, not a property of one gesture alone) was re-confirmed as still correct and directly informs this step's own disclosed decision to exclude `MotifReuse` from the repairable-metric set (§3 below).
- Read every file in `src/XlightsAI.Studio.Refinement/` as it exists today (`LoopRun.cs`, `CandidateAttempt.cs`, `RefinementStatus.cs`, `RefinementStatusTransitions.cs`, `IRefinementRunStore.cs`, `JsonRefinementRunStore.cs`, `RefinementClaim.cs`, `ISeededCandidateGenerator.cs`, `DeterministicGestureCandidateGenerator.cs`, `DeterministicContent.cs`, `MetricValue.cs`, `QualityReport.cs`, `ChoreographyQualityContext.cs`, `IChoreographyQualityEvaluator.cs`, `GestureQualityEvaluator.cs`, both exception types) and confirmed via `Grep` that no `ITargetedRepairStrategy`/repair/`BestCandidateSelector` type exists anywhere in the repository yet — this step's own scope, not already partially implemented.
- Re-read `MetricValue.cs`'s own already-accepted docstring, verbatim: "For a soft metric, a finite value in `[0, 1]` where higher is always better." This already-accepted contract is in direct tension with this step's own implementation guidance naming "reduce density" as a repair mutation (a maximal `Density` score, under that contract, reads as the *best* outcome, not a failure to fix) — resolved as a disclosed ADR (§3.1 below), not silently worked around.
- Read `src/XlightsAI.Studio.Composition/TransitionGesture.cs`/`TransitionKind.cs` (PHASE-10, already accepted) — `TransitionGesture`'s own `with`-expression record shape (every field independently replaceable) is reused unmodified as the exact mechanism a narrow, single-dimension mutation needs: `gesture with { Window = ... }` changes only the named field, leaving every other field byte-identical to the source.
- Read `src/XlightsAI.Studio.Editing/EditScope.cs` — confirmed `Models`/`Time`/`RespectLocks` are the exact fields this step's own "LegalEffects" repair clips against, unchanged from Step 081's own already-accepted reuse.
- Read `src/XlightsAI.Studio.Xlights/EffectInstance.cs` — confirmed `Locked`/`Target`/`StartTicks`/`EndTicks` are exactly what this step's own "respects locks" required acceptance needs to check against; no new lock concept was invented.
- Read `src/XlightsAI.Studio.Rhythm/BeatGrid.cs`/`BeatMarker.cs` (PHASE-05) — reused unmodified for the "Timing" repair's own nearest-beat-snap mutation.
- Read `tests/XlightsAI.Studio.Refinement.Tests/Step081AcceptanceTests.cs` in full and reused its exact fixture-construction conventions (`Scope`/`NewQueuedRun`/`Attempt`/`Gesture`/`BeatGridWith` helpers) verbatim in this step's own new test file, for consistency.
- Ran the full existing regression suite (§4) *before* writing any new code, to establish the true baseline: 1145/1145 passing (unchanged from Step 081's own recorded baseline).

## 2. Scope actually implemented

All new files are in `src/XlightsAI.Studio.Refinement/` and `tests/XlightsAI.Studio.Refinement.Tests/`. No existing file from any prior step was modified. No `.csproj` changed — every project reference this step needed (`Rhythm`, `Melody`, `Xlights`, `Composition`, `Editing`, `Contracts`) was already added by Step 081.

| File | Purpose |
|---|---|
| `RepairOutcome.cs` (new) | `RepairOutcome(CandidateAttempt Repaired, string? TargetedMetricName, string Explanation)` — the result of one repair call; `TargetedMetricName` lets a caller (or a test) directly verify this step's own required acceptance, verbatim: "Repair only addresses reported dimension." |
| `ITargetedRepairStrategy.cs` (new) | `RepairAsync(LoopRun run, CandidateAttempt candidate, QualityReport report, ChoreographyQualityContext context, CancellationToken ct)` — this step's own disclosed, additive interface (not named by PHASE-12.md Section 3's own proposed signatures; narrows Section 3's architectural design notes, verbatim: "Use targeted repair strategy for weakest failing metric"). |
| `GestureTargetedRepairStrategy.cs` (new) | The real implementation — see §2.1 below for each of the four repair mutations. |
| `BestCandidateSelector.cs` (new) | Static `SelectBest`/`AggregateScore` — this step's own heading, verbatim: "...preserve best-so-far candidate." Realizes PHASE-12 Section 12's own pseudocode `BetterThan` plus the violating/incomparable short-circuits the pseudocode's prose implies. |

New test file `tests/XlightsAI.Studio.Refinement.Tests/Step082AcceptanceTests.cs` — 13 new tests against the real `GestureTargetedRepairStrategy` and `BestCandidateSelector`, with directly-constructed, deterministic fixtures (no mocking), the same convention Steps 079–081 already established. This is this step's own required-acceptance-text "known good/bad fixtures" equivalent (Step 082's own required acceptance text does not use that phrase, but the same fixture discipline is applied).

### 2.1 The four repair mutations, as actually implemented

1. **LegalEffects (always repaired first, ahead of any soft metric, whenever it is failing).** Clips every gesture's own window to `LoopRun.Scope.Time`, drops any target prop outside `EditScope.Models`, and — this step's own required acceptance, verbatim, "respects locks" — drops any target prop that would still overlap an already-locked `EffectInstance` when `EditScope.RespectLocks` is `true`. Never widens a window or adds a prop; only ever removes/clips. If clipping collapses every gesture to nothing (no window, no legal prop, or a remaining prop still fails `TransitionGesture.Validate()`), the repaired candidate is returned with an empty gesture list and a `Rejected` status, carrying an explicit violation — never fabricated into a fake "legal" candidate.
2. **Density ("reduce density").** Drops exactly one target prop — the lexicographically last, for a deterministic tie-break — from every gesture that still targets more than one prop. Never touches a gesture's own window or intensities. **Disclosed deviation from `MetricValue`'s own already-accepted "higher is always better" docstring**, see §3.1 below.
3. **Timing ("shorten holds").** Moves each gesture's own window boundary to the nearest real `BeatGrid` beat marker, clipped back into the run's own permitted time scope. Never touches a gesture's own target props or intensities.
4. **MelodicAlignment ("repair direction").** Swaps each gesture's own `StartIntensity`/`EndIntensity`, reversing its ramp direction. Never touches a gesture's own window or target props. A `Blackout` gesture's intensities are always `0.0`/`0.0`, so the swap is a no-op and never violates its own invariant.

Selection of which single dimension to repair (§3.2 below, disclosed) is two-tiered: a genuinely failing `Timing`/`MelodicAlignment` metric (`Value < 1.0`, `Weight > 0`) is addressed first; only if neither is failing is `Density` considered, and only when some gesture still has more than one target prop to drop. `MotifReuse` is never selected (§3.3, disclosed). When `LegalEffects` is failing, it always wins regardless of any soft metric's own state — a candidate that is illegal cannot be meaningfully compared on density/timing/melody at all.

## 3. Architecture deviations / decisions (ADRs)

1. **Density is treated as "failing" (and therefore repairable) whenever any gesture still has more than one target prop — regardless of its own numeric `Value` — rather than only when `Value < 1.0`, as `Timing`/`MelodicAlignment` are.** `MetricValue.cs`'s own already-accepted docstring (Step 081) states, verbatim, for every soft metric: "a finite value in `[0, 1]` where higher is always better." Taken literally for `Density` (fraction of permitted props targeted), a maximal score of `1.0` would read as the *best* outcome, never a failure needing repair. But PHASE-12 Section 1's own user story names, verbatim, a "knowingly over-busy 8-bar phrase" as the scenario this entire phase exists to address, and this step's own implementation guidance names the mutation "reduce density" — literally, never "increase density" or "adjust density toward a target." Reconciling these without silently contradicting either already-accepted fact: `Density`'s own numeric value is left completely unchanged (Step 081's own evaluator is not touched, and this ADR does not claim a maximal `Density` is secretly "bad" in the evaluator's own scoring), but this step's own *repair strategy* — a separate, additive concern — treats "has room to reduce" (more than one target prop remains) as its own eligibility test for attempting the named "reduce density" mutation, deliberately distinct from the generic "is this metric's `Value` below `1.0`" test used for `Timing`/`MelodicAlignment`. `Density` is given lower priority than a genuinely failing `Timing`/`MelodicAlignment` (§2.1's selection order) precisely because it is not "failing" in the same documented sense — this is the disclosed reconciliation, not a silent one.
2. **Repair-target selection is a two-tier, metric-name-aware function (`SelectWeakestRepairableSoftMetric`), not a single generic "lowest `Value` among `Weight > 0` metrics" rule.** A fully generic rule would, if applied uniformly, sometimes select `Density` purely because it happens to be numerically lower than a passing `Timing`/`MelodicAlignment` score even when `Density` is nowhere near "over-busy" — the opposite of what this step's own named example mutations intend. The two-tier design (failing `Timing`/`MelodicAlignment` first, `Density` only as a fallback, `MotifReuse` never) is the smallest rule that satisfies every one of this step's own named example mutations ("shorten holds, reduce density, repair direction") without inventing an unrequested general-purpose scoring policy.
3. **`MotifReuse` is deliberately excluded from this strategy's own repairable-metric set.** Step 081's own disclosed ADR (STEP-081.md §3.5) already narrowed "motif reuse" to mean "similarity to this run's own prior sibling attempts" — a property of *which* prior shape exists, not a property any single gesture's own fields can be locally mutated to fix without first knowing which prior shape to converge toward. None of this step's own three named example mutations ("shorten holds, reduce density, repair direction") describes a motif-reuse mutation; inventing one here would be exactly the kind of unrequested, speculative generalization `CLAUDE.md`'s own "no designing for hypothetical future requirements" rule warns against.
4. **A repaired `CandidateAttempt`'s own `Attempt` ordinal is `source.Attempt + 1`, and its `Id`/`SnapshotHash` are deterministic functions of `(run.Id, source.Id, targetMetricName, nextOrdinal, repaired gesture content)` — never `Guid.NewGuid()`.** Mirrors `DeterministicGestureCandidateGenerator`'s own already-accepted reproducibility convention (Step 080) so that repair output satisfies the same cross-cutting reproducibility expectation (PHASE-12 Section 7 test #1) a freshly generated attempt already does — proven directly by `Step082_Acceptance_Reproducibility_SameInputsYieldIdenticalRepairedAttempt`.
5. **`BestCandidateSelector.SelectBest` never mutates or replaces either input `CandidateAttempt`/`QualityReport` instance — it only ever returns one of its own two inputs, unchanged.** This step's own implementation guidance, verbatim: "compare without corrupting prior candidate." A violating candidate (either `CandidateAttempt.Violations` or `QualityReport.Violations` non-empty) can never become best — this step's own required failure/edge case, verbatim: "Malformed proposal... must not auto-commit," reapplied to best-candidate preservation, not only to a single evaluation. Two reports from different `EvaluatorVersion`s (`QualityReport.AreComparable` false) cannot be safely compared, so the existing best is preserved rather than guessed at — directly reusing Step 081's own already-accepted `AreComparable` (PHASE-12 Section 7 cross-cutting test #7), not inventing a second comparability concept.
6. **No existing accepted contract from any prior phase or step was modified.** `TransitionGesture`/`TransitionKind` (PHASE-10), `EditScope`/`ModelId`/`TimeWindow` (PHASE-01/03), `EffectInstance` (PHASE-02/03), `BeatGrid`/`BeatMarker` (PHASE-05), `MetricValue`/`QualityReport`/`ChoreographyQualityContext`/`IChoreographyQualityEvaluator`/`GestureQualityEvaluator` (Step 081), `LoopRun`/`CandidateAttempt`/`RefinementStatus`/`DeterministicGestureCandidateGenerator`/`DeterministicContent` (Steps 079/080) are all consumed unchanged, read-only. This step adds four new files and zero project references (every reference it needed already existed after Step 081).
7. **No `IRefinementOrchestrator` wires this repair strategy into an automatic attempt loop yet.** Deciding *when* to call `ITargetedRepairStrategy.RepairAsync` versus generating a wholly new candidate via `ISeededCandidateGenerator`, and enforcing `MaxAttempts`/time/cost budgets around that choice, is Step 083's own named scope ("Add retry, time, compute and quality thresholds; cancellation") — consistent with Steps 079–081's own disclosed gaps naming later steps as the owner of orchestration.

## 4. Commands actually executed

All commands were run via the **PowerShell** tool (the Bash tool returns no usable output in this sandboxed session — re-confirmed at the start of this step, consistent with every prior PHASE-11/Step-079/080/081 evidence doc).

```
> dotnet build src\XlightsAI.Studio.Refinement\XlightsAI.Studio.Refinement.csproj -v minimal
Build succeeded. 0 Warning(s) 0 Error(s)

> dotnet build tests\XlightsAI.Studio.Refinement.Tests\XlightsAI.Studio.Refinement.Tests.csproj -v minimal
Build succeeded. 0 Warning(s) 0 Error(s)

> dotnet test tests\XlightsAI.Studio.Refinement.Tests\XlightsAI.Studio.Refinement.Tests.csproj -v minimal --filter "FullyQualifiedName~Step082"
Passed! - Failed: 0, Passed: 13, Skipped: 0, Total: 13, Duration: 181 ms

> dotnet build XlightsAI.Studio.slnx -v minimal   (full solution, after change)
Build succeeded. 0 Warning(s) 0 Error(s)

> dotnet test XlightsAI.Studio.slnx -v minimal   (full solution regression, after change)
XlightsAI.Studio.Xlights.Tests.dll      : 74/74 passed
XlightsAI.Studio.Editing.Tests.dll      : 91/91 passed
XlightsAI.Studio.Display.Tests.dll      : 56/56 passed
XlightsAI.Studio.AiEdits.Tests.dll      : 79/79 passed
XlightsAI.Studio.Melody.Tests.dll       : 117/117 passed
XlightsAI.Studio.Composition.Tests.dll  : 78/78 passed
XlightsAI.Studio.Refinement.Tests.dll   : 45/45 passed   (was 32/32 before this step)
XlightsAI.Studio.Audio.Tests.dll        : 38/38 passed
XlightsAI.Studio.Structure.Tests.dll    : 78/78 passed
XlightsAI.Studio.Projects.Tests.dll     : 25/25 passed
XlightsAI.Studio.Shell.Tests.dll        : 263/263 passed
XlightsAI.Studio.Rhythm.Tests.dll       : 82/82 passed
XlightsAI.Studio.Reference.Tests.dll    : 31/31 passed
XlightsAI.Studio.Stems.Tests.dll        : 101/101 passed
TOTAL: 1158/1158 passed, 0 failed   (1145 baseline-after-Step-081 + 13 new, 0 removed, 0 modified)

> dotnet format XlightsAI.Studio.slnx --verify-no-changes --no-restore
(no output -- no formatting violations)

> git status --short
(unchanged from before this step: everything still untracked, nothing committed in this repository yet;
no prior file was modified or staged by this step)
```

Observable desktop demonstration (disposable console app, not committed to the repository, `%TEMP%\xlai-step082-demo-proj` — the same convention every prior PHASE-11/Step-079/080/081 evidence doc used):

```
> New-Item -ItemType Directory -Force -Path "$env:TEMP\xlai-step082-demo-proj"
> # wrote demo.csproj + Program.cs referencing the real Refinement/Editing/Contracts/Composition/Rhythm/Melody/Xlights projects
> cd "$env:TEMP\xlai-step082-demo-proj"
> dotnet run -v minimal
```

Full transcript (abridged to remove restore/build noise):

```
=== PHASE-12 Step 082 demo: targeted repair strategy + preserve best-so-far candidate ===

--- Phase gate scenario: over-busy candidate, targeted repair of its weakest reported dimension ---
-- Generated over-busy candidate (targets all 4 permitted props) (EvaluatorVersion=gesture-quality.v1) --
   LegalEffects     Value=1.00 Weight=1.0 HardGate=True  :: 1 gesture(s) validated, scoped to permitted props/time, with no upstream violations.
   Density          Value=1.00 Weight=1.0 HardGate=False :: 4/4 permitted prop(s) targeted (100%).
   Timing/MelodicAlignment/MotifReuse: Weight=0 (no data supplied)
Repair targeted dimension: Density :: dropped one target prop from 1 gesture(s) to reduce density; window and intensities are unchanged.
-- Repaired candidate --
   Density          Value=0.75 Weight=1.0 HardGate=False :: 3/4 permitted prop(s) targeted (75%).
PASS: repair addressed only Density (window/intensities unchanged): True

--- Timing repair: snaps window to nearest beat, leaves props/intensities untouched ---
-- Offset-from-beat candidate --
   Timing           Value=0.60 :: Average gesture boundary offset from the nearest beat is 40% of one beat period (500000 ticks).
Repair targeted dimension: Timing :: snapped 1 gesture(s) own window boundaries to the nearest beat marker; target props and intensities are unchanged.
-- Repaired candidate --
   Timing           Value=1.00 :: Average gesture boundary offset from the nearest beat is 0% of one beat period (500000 ticks).

--- Legal effects repair: drops a locked-overlapping prop, keeps the legal one (respects locks) ---
-- Candidate overlapping a locked effect on 'Arches' --
   LegalEffects     Value=0.00 HardGate=True :: gesture '...' overlaps locked effect '...' on prop 'Arches'.
Repair targeted dimension: LegalEffects :: clipped 1 gesture(s) to the run's permitted scope/time and removed any locked-effect overlap; 1 gesture(s) remain legal.
-- Repaired candidate --
   LegalEffects     Value=1.00 HardGate=True :: 1 gesture(s) validated...
   Density          Value=0.50 :: 1/2 permitted prop(s) targeted (50%).
PASS: repaired gesture no longer targets the locked prop 'Arches': True
PASS: repaired gesture still targets the un-locked prop 'MegaTree': True

--- Negative path: malformed proposal (empty-scope generation) is never repaired into fake content ---
Targeted dimension: (none) :: candidate carries no gestures to repair (malformed/empty generation); returned unchanged.
PASS: malformed proposal returned unchanged, still violating, never auto-committable: True

--- Negative path: best-so-far candidate is preserved across a worse later attempt ---
Best-so-far after attempt 0: Id=b0612629-1e2a-45db-9fa0-dc31cde3dc36, AggregateScore=1.00
Best-so-far after strictly worse attempt 1 (AggregateScore=0.75): Id=b0612629-1e2a-45db-9fa0-dc31cde3dc36
PASS: best-so-far unchanged (still attempt 0, never corrupted by a worse later attempt): True
PASS: an out-of-scope (violating) candidate, even if its own score would look high, never becomes best (must not auto-commit): True

=== Demo complete ===
```

This demo exercises this step's own required acceptance end to end: the phase gate's own "over-busy" scenario (a real Step 080 `DeterministicGestureCandidateGenerator` output is fed directly into this step's own real repair strategy, reducing density by exactly one prop and leaving window/intensities byte-identical); the "Timing" repair (snaps a beat-offset window, leaving props/intensities untouched); the "legal effects respects locks" repair (drops only the locked-overlapping prop, keeps the legal one); the required failure/edge case "malformed proposal... must not auto-commit" (an empty-scope generation is returned completely unchanged, never fabricated into fake legal content); and "no-improvement... must not auto-commit" applied to best-candidate preservation (a strictly worse later attempt, and separately a violating/out-of-scope later attempt, both leave the retained best-so-far candidate unchanged). The demo project and its `bin`/`obj` output were created entirely under `%TEMP%`, run, and then deleted (`Remove-Item -Recurse -Force`); it is not part of this repository's tracked tree.

## 5. Persisted-output inspection / observable demonstration

This step's own scope is repair-strategy logic and best-candidate selection, both pure functions — neither is wired into `IRefinementRunStore` yet (no `LoopRun.BestCandidateId` is set by this step's own code; that remains the orchestrator's own responsibility per Step 079's own already-accepted `SaveRunStateAsync` contract, which independently re-validates that any `BestCandidateId` it is given names a non-violating attempt). The "persisted-output inspection" instead takes the form of the real, already-accepted Step 080 `DeterministicGestureCandidateGenerator`'s own output being fed directly into this step's own real `GestureTargetedRepairStrategy` (demo §4, first scenario: the over-busy candidate is generated, not hand-constructed), and the real Step 081 `GestureQualityEvaluator` being used both before and after every repair to prove the repaired candidate's own targeted metric actually improved while every other metric's own value is unchanged. `Step082AcceptanceTests` and the demo both construct and inspect real `RepairOutcome`/`CandidateAttempt`/`QualityReport` object graphs (never a mock) and assert on their actual field values.

## 6. Fixtures / input hashes

No fixture files were added to `fixtures/` or `src/XlightsAI.Studio.Reference/Fixtures/` — the same convention Steps 079–081 already established for this phase (no separate fixture JSON corpus exists for `Refinement` yet). Every `BeatGrid`/`EffectInstance`/`TransitionGesture` fixture value used by `Step082AcceptanceTests` is a real, fully-validated instance of its own already-accepted type, constructed with the identical helper methods (`Scope`/`NewQueuedRun`/`Attempt`/`Gesture`/`BeatGridWith`) Step 081's own test file already established, for direct cross-step consistency.

## 7. Unresolved issues / known gaps

- **No `IRefinementOrchestrator` drives repair selection (repair vs. fresh generation) or enforces `MaxAttempts`/time/cost budgets automatically yet.** That orchestration, and the "exhausted retry budget" half of this step's own required failure/edge case text, are Step 083's own named scope ("Add retry, time, compute and quality thresholds; cancellation"). This step proves the two narrower claims its own required text actually asks of *this* step's own objects: a malformed proposal is never repaired into fake content, and a non-improving (or violating) later attempt never displaces a retained best-so-far candidate. The budget-exhaustion claim itself is deliberately not claimed here — disclosed, not silently skipped.
- **`LoopRun`/`CandidateAttempt` were not extended with a `Report`/`BestCandidateId`-setting call site by this step.** Same disclosed gap Step 081 already recorded (STEP-081.md §7): wiring a `QualityReport` (or a `BestCandidateSelector` result) back onto a persisted `LoopRun`/`CandidateAttempt` via `IRefinementRunStore` remains whichever of Steps 083/084 first needs that persisted shape pinned down by its own acceptance text.
- **`Density`'s own repair-eligibility rule (§3.1) is a repair-strategy-local decision, not a change to `MetricValue`/`GestureQualityEvaluator`'s own already-accepted "higher is always better" scoring contract.** A future step that needs `BestCandidateSelector.AggregateScore` itself to treat `Density` non-monotonically (e.g. penalize over-busy candidates in the aggregate score, not only in repair-target selection) would need its own disclosed ADR; this step does not anticipate or half-implement that possibility, per `CLAUDE.md`'s own "no designing for hypothetical future requirements" rule.
- **No real SCREEN 15 COMPARE / SCREEN 14 AI EDIT Avalonia view exists yet** — this step's own required user-visible outcome ("Versioned, inspectable project output plus a reproducible test report") is satisfied by the real console demonstration (§4) and the real test suite (§2), not a UI; the candidate-gallery/preview UI itself remains Step 084's own named scope.

## 8. Required-acceptance-text-by-text tie-back

| Required text (verbatim) | Where proven |
|---|---|
| "Repair only addresses reported dimension" | `Step082_Acceptance_TimingRepair_OnlyChangesWindow_NotPropsOrIntensities`, `Step082_Acceptance_DensityRepair_OnlyChangesTargetProps_NotWindowOrIntensities`, `Step082_Acceptance_MelodicAlignmentRepair_OnlySwapsIntensities_NotWindowOrProps`, `Step082_Acceptance_RepairTargetsTheSingleWeakestWeightedMetric`; demo §4's first three scenarios (each asserts every other field byte-identical). |
| "...and respects locks" | `Step082_Acceptance_LegalEffectsRepair_DropsLockedOverlappingProp_RespectsLocks`, `Step082_Acceptance_LegalEffectsRepair_RespectLocksFalse_NeverDropsTheProp`; demo §4's third scenario. |
| Implementation guidance, verbatim: "Select a targeted mutation for failed metric (shorten holds, reduce density, repair direction)" | §2.1's four named mutations, each with its own passing/failing test. |
| Implementation guidance, verbatim: "...and compare without corrupting prior candidate" | `BestCandidateSelector.SelectBest` never mutates either input (ADR §3.5); `Step082_NegativePath_NoImprovement_BestSoFarIsPreserved_NeverCorruptedByWorseLaterAttempt` asserts `Assert.Same` on the preserved instance. |
| Required failure/edge case, verbatim: "Malformed proposal... must not auto-commit" | `Step082_NegativePath_MalformedProposal_EmptyGestures_ReturnedUnchanged_NeverFabricatesContent`, `Step082_NegativePath_LegalEffectsRepair_EverythingLockedOut_StaysRejected_NeverAutoCommits`; demo §4's malformed-proposal scenario. |
| Required failure/edge case, verbatim: "no-improvement... must not auto-commit" | `Step082_NegativePath_NoImprovement_BestSoFarIsPreserved_NeverCorruptedByWorseLaterAttempt`, `Step082_NegativePath_BestSoFar_NeverPromotesAViolatingCandidate_EvenIfScoreWouldBeHigher`, `Step082_NegativePath_BestSoFar_EvaluatorVersionMismatch_PreservesExistingBest`; demo §4's best-so-far scenario. |
| Required failure/edge case, verbatim: "exhausted retry budget... must not auto-commit" | **Not claimed by this step** — disclosed as Step 083's own named scope (§7). |
| PHASE-12 Section 7 cross-cutting test #3, verbatim: "Best candidate retained when later attempt is worse" | `Step082_NegativePath_NoImprovement_BestSoFarIsPreserved_NeverCorruptedByWorseLaterAttempt`; demo §4. |
| PHASE-12 Section 7 cross-cutting test #1 (reproducibility), applied to repair output | `Step082_Acceptance_Reproducibility_SameInputsYieldIdenticalRepairedAttempt`. |

## 9. PASS / BLOCKED

**PASS.**

- The required-acceptance sentence ("Repair only addresses reported dimension and respects locks") is proven by passing tests for all four named repair mutations (each asserting every non-targeted field is byte-identical before/after) plus two dedicated lock-respecting tests, and by a real console demonstration exercising the phase gate's own over-busy scenario end to end against the real Step 080 generator's output.
- "Malformed proposal... must not auto-commit" is proven: an empty/malformed candidate is returned as the exact same instance, untouched, never fabricated into fake legal content, both in tests and in the demo.
- "No-improvement... must not auto-commit" is proven for best-candidate preservation: a strictly worse later attempt, a violating later attempt, and an evaluator-version-mismatched later attempt all leave the retained best-so-far candidate unchanged (the same object instance), both in tests and in the demo.
- "Exhausted retry budget... must not auto-commit" is explicitly **not claimed** by this step — disclosed as Step 083's own named scope (budget/threshold enforcement), consistent with how Steps 079–081 each disclosed which later step owns which remaining piece of the phase's cross-cutting failure text.
- Full solution build succeeds with 0 warnings/errors; `dotnet format --verify-no-changes` reports no violations.
- Full regression: 1158/1158 tests pass (0 failed) — 1145 pre-existing (0 modified, 0 removed) plus 13 new `Refinement.Tests`.
- No prior accepted contract (any PHASE-01 through PHASE-11 type, or any Step 079/080/081 type) was modified. This step adds four new files and zero new project references.
- No show file, fixture, or prior persisted artifact was modified.

**Explicitly not claimed:** budget/time/cost/cancellation enforcement around repair-vs-generate decisions (Step 083's own named scope — "exhausted retry budget" is disclosed, not silently skipped, §7/§8); persistence of a repaired attempt or a selected best candidate back into `IRefinementRunStore` (no later step's own acceptance text yet requires this exact shape — disclosed gap, §7); any xLights-XML-level repair (this step mutates `TransitionGesture` plan content, the same pre-compilation level Step 081's evaluator already operates at — not compiled xLights effect XML); and any Avalonia UI/screenshot (Step 084's own named scope).
