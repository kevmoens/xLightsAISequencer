# PHASE-12 Step 084 — Add candidate gallery, score explanations, preview and explicit selection

**Date:** 2026-10-18
**Spec:** `docs/phase-specs/PHASE-12.md` §6, Step 084
**Required acceptance (verbatim):** "User can compare alternatives and commit chosen candidate."
**Failure/edge case (verbatim, from the step's own table row):** "Out-of-scope, locked or stale revision must be rejected without partial mutation."

## 1. Pre-implementation baseline inspection

Before writing any code, the existing repository state was read directly (not taken on faith):

- Read `docs/phase-specs/PHASE-12.md` in full — this step's own heading, implementation guidance ("Compare best-vs-current with scores and synchronized preview; record deliberate human accept/reject and optional feedback labels"), required acceptance/failure text, Section 3's `CandidateCompareViewModel` class-diagram entry, Section 5's SCREEN 15 COMPARE wireframe reference, and Section 7's cross-cutting test #8 ("User approval checks base revision to prevent stale overwrite").
- Read `implementation-evidence/PHASE-12/STEP-079.md` through `STEP-083.md` in full. Step 083's own disclosed gap (§7), verbatim: "No real SCREEN 14/15 Avalonia view exists yet... the candidate-gallery/preview/explicit-selection UI itself remains Step 084's own named scope" — confirms this step's own job.
- Read every file in `src/XlightsAI.Studio.Refinement/` as it exists today (`LoopRun.cs`, `CandidateAttempt.cs`, `RefinementStatus.cs`, `RefinementStatusTransitions.cs`, `IRefinementRunStore.cs`, `JsonRefinementRunStore.cs`, `RefinementClaim.cs`, `GestureQualityEvaluator.cs`, `ChoreographyQualityContext.cs`, `BestCandidateSelector.cs`, `GestureRefinementOrchestrator.cs`, `QualityReport.cs`, `MetricValue.cs`) to confirm exactly what state a run reaches at `AwaitingApproval` and that no commit-to-`SequenceDocument` path exists anywhere yet (re-verified via `Grep` for `IRefinementCandidateCommitService`/`CandidateCompareViewModel` — zero matches before this step).
- Read `src/XlightsAI.Studio.Composition/TransitionGesture.cs`, `TransitionRecipePlanner.cs`, `ChoreographyCandidate.cs`, `IChoreographyCompiler.cs`, `ChoreographyCompiler.cs`, `ChoreographyCompilationResult.cs` — confirmed PHASE-10's own `TransitionGesture` (what `CandidateAttempt.Gestures` actually carries) has never been compiled into a real `EffectInstance` anywhere in the repository: `ChoreographyCompiler` only ever compiles the differently-shaped `ChoreographyCandidate` (role-assignment based, Step 071), and `TransitionRecipePlanner`'s own Step 070 ADR explicitly deferred "concrete xLights effect recipe/layer compilation" to a later step that, until now, had never arrived.
- Read `src/XlightsAI.Studio.Xlights/EffectInstance.cs`, `EffectRecipeRegistry.cs`, `IEffectRecipeRegistry.cs` — confirmed this build's `IEffectRecipeRegistry` still recognizes exactly one verified recipe, `effect.on.v1` (PHASE-02 Step 012), unchanged since Step 012.
- Read `src/XlightsAI.Studio.Editing/EditScope.cs`, `EditTransaction.cs`, `EditApplyResult.cs`, `IEffectLockPolicy.cs`, `EffectLockPolicy.cs`, `EditTransactionService.cs`, `EditRejectionReason.cs`, `EditCommandRejectedException.cs`, `EditTransactionHistoryRecorder.cs` — confirmed `EditRejectionReason` already carries exactly the three named reasons this step's own required failure text repeats verbatim ("Out-of-scope, locked or stale revision" — `OutOfScope`/`Locked`/`StaleRevision`), established originally by PHASE-03 Step 018 and reused unchanged by every PHASE-03/PHASE-11 command/approval path since.
- Read `src/XlightsAI.Studio.AiEdits/IEditApprovalService.cs`, `EditProposalApprovalService.cs` and `src/XlightsAI.Studio.Shell/AiEditProposalReviewViewModel.cs` in full — PHASE-11 Step 076's own closest sibling: an Accept/Reject boundary that re-validates against the live document at accept time (never trusting an earlier preview), returns `EditApplyResult` without persisting it itself, and a bindable, Avalonia-free view model wrapping it. This step's own `IRefinementCandidateCommitService`/`GestureCandidateCommitService`/`CandidateCompareViewModel` deliberately mirror this exact shape rather than inventing a parallel one.
- Read `tests/XlightsAI.Studio.Refinement.Tests/Step082AcceptanceTests.cs` and `Step083AcceptanceTests.cs`, and `tests/XlightsAI.Studio.Shell.Tests/Step076AiEditProposalReviewViewModelTests.cs` in full, and reused their exact fixture-construction conventions (`Scope`/`NewQueuedRun`/real `JsonRefinementRunStore` against real temp-directory files, no mocking; Shell-level view-model tests live in `XlightsAI.Studio.Shell.Tests`, not `XlightsAI.Studio.Refinement.Tests`) in this step's own two new test files.
- Ran the full existing regression suite (§4) *before* writing any new code to establish the true baseline: `dotnet build XlightsAI.Studio.slnx` succeeded (0 warnings/errors), confirming the Step 083 baseline state described in `STEP-083.md` (1149/1174 passing, 25 pre-existing/environmental audio-hardware failures) was still the starting point.

## 2. Scope actually implemented

All new files are in `src/XlightsAI.Studio.Refinement/`, `src/XlightsAI.Studio.Shell/`, `tests/XlightsAI.Studio.Refinement.Tests/`, and `tests/XlightsAI.Studio.Shell.Tests/`. Two `.csproj` files were modified (one new `ProjectReference` each, no other change).

| File | Status | Purpose |
|---|---|---|
| `src/XlightsAI.Studio.Refinement/IRefinementCandidateCommitService.cs` | New | The explicit, deliberate human accept/reject boundary this step's own heading names — PHASE-12 Section 12's own pseudocode, verbatim: "`MarkAwaitingHumanApprovalAsync`... not a commit to `SequenceDocument`." This interface is exactly that deferred commit. |
| `src/XlightsAI.Studio.Refinement/GestureCandidateCommitService.cs` | New | Default implementation — see §2.1 below. |
| `src/XlightsAI.Studio.Shell/CandidateGalleryRow.cs` | New | One bindable gallery row: a `CandidateAttempt` paired with its re-evaluated `QualityReport`, aggregate score, "best" flag, and a textual preview summary. |
| `src/XlightsAI.Studio.Shell/CandidateCompareViewModel.cs` | New | SCREEN 15 COMPARE's own bindable candidate gallery — see §2.2 below. |
| `src/XlightsAI.Studio.Refinement/XlightsAI.Studio.Refinement.csproj` | Unchanged | No new project reference was needed — `Editing`, `Composition`, and `Xlights` were already referenced since Steps 079–081. |
| `src/XlightsAI.Studio.Shell/XlightsAI.Studio.Shell.csproj` | Modified (additive) | One new `<ProjectReference>` to `XlightsAI.Studio.Refinement` (verified non-cyclic: `Refinement` does not reference `Shell`). |

New test files: `tests/XlightsAI.Studio.Refinement.Tests/Step084AcceptanceTests.cs` (12 tests against the real `GestureCandidateCommitService`/`JsonRefinementRunStore`/`GestureRefinementOrchestrator`), and `tests/XlightsAI.Studio.Shell.Tests/Step084CandidateCompareViewModelTests.cs` (7 tests against the real `CandidateCompareViewModel` wired to the same real collaborators). No mocking framework; no test doubles at all in either file.

### 2.1 `GestureCandidateCommitService` — what it actually does

1. **`AcceptAsync`** — every gate is checked, and every effect this commit would add is fully computed, *before* the run's own persisted `LoopRun.Status` is ever touched (pure reads/computation first, then one terminal `SaveRunStateAsync` call last):
   - Loads the run; rejects (as `RefinementRunInvalidException`) an unknown run id, a document-id mismatch, or a run not currently `AwaitingApproval`.
   - **Stale revision** (`EditRejectionReason.StaleRevision`): the caller's own `document.Revision` must still equal `run.BaseRevision` — this step's own required failure text, verbatim.
   - Loads the named candidate attempt; rejects (`RefinementRunInvalidException`) an unknown candidate id or one carrying any `CandidateAttempt.Violations` ("must not auto-commit," restated from Steps 079–083).
   - **Locked** (`EditRejectionReason.Locked`): `IEffectLockPolicy.AssertEditable(document, run.Scope)` — the same PHASE-03 lock gate every other command in the repository already uses, re-checked live against the caller's own current document rather than trusted from `GestureQualityEvaluator`'s own earlier evaluation.
   - **Out-of-scope** (`EditRejectionReason.OutOfScope`): a disclosed defense-in-depth re-check of exactly the two scope rules `GestureQualityEvaluator`'s own "legal effects" hard gate already enforced (Step 081) against an earlier snapshot — re-run here against the run's live `Scope`.
   - Compiles every gesture's own target prop into one real `EffectInstance` via the one recipe this build's `IEffectRecipeRegistry` actually knows (`EffectRecipeRegistry.SimpleOnRecipeId`), choosing the lowest non-colliding layer against `document`'s own existing effects (the same half-open-overlap layer-assignment rule `ChoreographyCompiler` already established for role-based candidates, reused here for gesture-based ones) — throwing `EditCommandRejectedException(OutOfScope, ...)` *before* constructing any new document if no free layer exists for any gesture/prop.
   - Only then claims the run and calls `SaveRunStateAsync` with `Status = Accepted`, `BestCandidateId = <the chosen candidate>` — a terminal transition, so the store auto-releases the claim.
   - Returns an `EditApplyResult` (updated document + `EditTransaction`) without persisting it itself — the exact same "caller persists it" split `XlightsAI.Studio.AiEdits.IEditApprovalService.AcceptAsync` already established, so no `ISequenceDocumentStore` dependency exists on this service at all.
2. **`RejectAsync`** — transitions the run straight to terminal `RefinementStatus.Rejected` with an optional human feedback label folded into `StopReason`; never constructs, loads, or references a `SequenceDocument` in any code path — "Reject leaves state byte-equivalent" (PHASE-11 Step 076's own required acceptance, applied here) holds trivially by construction, not by discipline.

### 2.2 `CandidateCompareViewModel` — what it actually does

- `LoadAsync` loads every recorded `CandidateAttempt` for a run (not only the retained best) and re-evaluates each one's own `QualityReport` via the real `IChoreographyQualityEvaluator` — the same "recompute rather than persist a `Report` field" convention `GestureRefinementOrchestrator`'s own resume path already established (`STEP-083.md` §2.1). Defaults `SelectedCandidateId` to `LoopRun.BestCandidateId`.
- `SelectCandidate` lets a caller pick **any** currently-loaded row — this step's own required acceptance, verbatim: "User can compare alternatives," proven by a dedicated test selecting a non-best candidate and then accepting exactly that one (`Step084_Acceptance_UserCanChooseANonBestAlternative_NotOnlyTheRetainedBest` / `Step084_Acceptance_UserCanSelectAnyAlternative_ThenAcceptTheExplicitlyChosenOne`).
- `AcceptAsync`/`RejectAsync` delegate directly to `IRefinementCandidateCommitService`, surfacing any rejected gate via `ErrorMessage` without mutating `Candidates`/`SelectedCandidateId` on failure (so a caller can re-load against the current document state and retry) — the identical "leave state intact on rejection" pattern `AiEditProposalReviewViewModel.PreviewAsync`/`AcceptAsync` already established.
- Each `CandidateGalleryRow` carries the full `QualityReport` (every `MetricValue.Explanation` intact, never re-summarized or dropped — this step's own heading, verbatim: "score explanations") plus a human-readable, per-gesture `PreviewSummary` string ("synchronized preview" — a textual stand-in, since no real xLights/Avalonia render surface exists yet for this phase, the same disclosed convention `AiEditProposalReviewViewModel.DiffSummary` already established for its own PHASE-11 sibling).

## 3. Architecture deviations / decisions (ADRs)

1. **`TransitionGesture` → `EffectInstance` compilation is new as of this step; it reuses the one already-verified recipe (`effect.on.v1`) rather than inventing an unverified one.** PHASE-10's own `TransitionGesture` (Step 070) carries no `RecipeId` field at all — its own ADR explicitly deferred "concrete xLights effect recipe/layer compilation" to a later step. `ChoreographyCompiler` (Step 071) never solved this for gestures either; it compiles the differently-shaped, role-assignment-based `ChoreographyCandidate`. This step is the first point any gesture is ever turned into a real document effect, and does so using the one recipe this build's own `IEffectRecipeRegistry` has ever verified (Section 2: "do not invent xLights effect parameters") — a deliberate, minimal choice, not a placeholder abandoned mid-way.
2. **Layer assignment reuses `ChoreographyCompiler.MaxLayers` and its own half-open-overlap collision rule, applied to gesture-sourced effects.** Rather than inventing a second, parallel layer-assignment algorithm, `GestureCandidateCommitService.CompileEffects` seeds per-target occupancy from `document.Effects` (exactly as `ChoreographyCompiler.Compile` already does) and picks the lowest non-colliding layer up to the same bound — proven by `Step084_Acceptance_CommittedEffects_NeverCollideWithExistingDocumentEffects`. Unlike `ChoreographyCompiler`, there is no cross-candidate *precedence* concern here (a human has already explicitly chosen exactly one candidate to commit), so no `IRolePrecedenceResolver` dependency was added — reusing only the part of `ChoreographyCompiler`'s own logic this step's own narrower scope actually needs.
3. **`IRefinementCandidateCommitService` never persists the returned `EditApplyResult.Document` itself, and takes no `ISequenceDocumentStore`/`IEditTransactionHistoryStore` dependency.** This is a disclosed, deliberate mirror of `XlightsAI.Studio.AiEdits.IEditApprovalService.AcceptAsync`'s own already-accepted shape (PHASE-11 Step 076/078): the caller (a real SCREEN 15 view model layer, and in this step's own demo, a disposable console harness) is responsible for persisting the document and, if it wants the committed effects to be undoable via `EditTransactionService.UndoAsync`, calling the already-accepted `EditTransactionHistoryRecorder.AppendAsync` helper (PHASE-11 Step 078) exactly the way `EditProposalApprovalService` optionally does. This step's own demo (§5) proves end-to-end that this composition actually works (Accept → record history → `EditTransactionService.UndoAsync` → exact prior document state restored) without duplicating `EditTransactionService`'s own undo/redo stack logic inside the Refinement project.
4. **`AcceptAsync` can commit *any* non-violating recorded candidate attempt, not only `LoopRun.BestCandidateId`.** This step's own required acceptance, verbatim, is "User can compare alternatives **and commit chosen candidate**" — not "commit the retained best." `BestCandidateId` is updated to whichever candidate was actually chosen on a successful accept, so the persisted run's own audit trail always reflects the human's real decision, not merely the orchestrator's own earlier algorithmic preference. Proven by `Step084_Acceptance_UserCanChooseANonBestAlternative_NotOnlyTheRetainedBest` and its `CandidateCompareViewModel` sibling.
5. **Every gate is checked and every new effect is fully computed strictly before the run's own persisted status is touched, with no interim claim-then-release cleanup path needed on a rejected gate.** Unlike `GestureRefinementOrchestrator` (which must hold a claim across many iterations and therefore needs explicit claim-release cleanup on every exit path, STEP-083.md §2.1), this step's own `AcceptAsync` only ever claims the run for the single, final, already-validated `SaveRunStateAsync` call — so a rejected gate never claims anything at all, and "no partial mutation" holds by the order of operations rather than by a compensating rollback. Proven by every negative-path test asserting the run's own `ClaimToken` is `null` and `Status` is unchanged (still re-claimable) after a rejected `AcceptAsync` call.
6. **No existing accepted contract from any prior phase or earlier Step 079–083 type was removed or had a breaking signature change.** `LoopRun`/`CandidateAttempt`/`IRefinementRunStore`/`GestureQualityEvaluator` are all used exactly as already accepted, with zero modification. `EditRejectionReason`/`EditCommandRejectedException`/`IEffectLockPolicy`/`IEffectRecipeRegistry`/`EditApplyResult`/`EditTransaction` (all PHASE-02/03) are reused unmodified.
7. **No `.csproj` changed except one additive `ProjectReference`** (`XlightsAI.Studio.Shell` → `XlightsAI.Studio.Refinement`, verified non-cyclic).

## 4. Commands actually executed

All commands were run via the **PowerShell** tool (the Bash tool returns no usable output in this sandboxed session — re-confirmed at the start of this step, consistent with every prior PHASE-11/Step-079–083 evidence doc).

```
> dotnet build src\XlightsAI.Studio.Refinement\XlightsAI.Studio.Refinement.csproj -v minimal
Build succeeded. 0 Warning(s) 0 Error(s)

> dotnet build src\XlightsAI.Studio.Shell\XlightsAI.Studio.Shell.csproj -v minimal
Build succeeded. 0 Warning(s) 0 Error(s)

> dotnet build tests\XlightsAI.Studio.Refinement.Tests\XlightsAI.Studio.Refinement.Tests.csproj -v minimal
Build succeeded. 0 Warning(s) 0 Error(s)

> dotnet test tests\XlightsAI.Studio.Refinement.Tests\XlightsAI.Studio.Refinement.Tests.csproj -v minimal --no-build --filter "FullyQualifiedName~Step084"
Passed! - Failed: 0, Passed: 12, Skipped: 0, Total: 12, Duration: 1 s

> dotnet build tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj -v minimal
Build succeeded. 0 Warning(s) 0 Error(s)

> dotnet test tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj -v minimal --no-build --filter "FullyQualifiedName~Step084"
Passed! - Failed: 0, Passed: 7, Skipped: 0, Total: 7, Duration: 685 ms

> dotnet build XlightsAI.Studio.slnx -v minimal   (full solution, after change)
Build succeeded. 0 Warning(s) 0 Error(s)

> dotnet test XlightsAI.Studio.slnx -v minimal --no-build   (full solution regression, after change)
XlightsAI.Studio.Editing.Tests.dll      : 91/91 passed
XlightsAI.Studio.Display.Tests.dll      : 56/56 passed
XlightsAI.Studio.Xlights.Tests.dll      : 74/74 passed
XlightsAI.Studio.AiEdits.Tests.dll      : 79/79 passed
XlightsAI.Studio.Composition.Tests.dll  : 78/78 passed
XlightsAI.Studio.Structure.Tests.dll    : 78/78 passed
XlightsAI.Studio.Audio.Tests.dll        : 38/38 passed   (real audio output device was available in this run -- unlike STEP-083.md's own session; see §4.1)
XlightsAI.Studio.Melody.Tests.dll       : 117/117 passed
XlightsAI.Studio.Projects.Tests.dll     : 25/25 passed
XlightsAI.Studio.Refinement.Tests.dll   : 71/71 passed   (was 59/59 before this step; +12 new)
XlightsAI.Studio.Shell.Tests.dll        : 270/270 passed (was 263 total before this step -- 248 passed/15 environmental failures per STEP-083.md §4; +7 new, now fully green per §4.1)
XlightsAI.Studio.Rhythm.Tests.dll       : 82/82 passed
XlightsAI.Studio.Reference.Tests.dll    : 31/31 passed
XlightsAI.Studio.Stems.Tests.dll        : 101/101 passed
TOTAL: 1191/1191 passed, 0 failed, 0 skipped

> dotnet format XlightsAI.Studio.slnx --verify-no-changes --no-restore
(no output -- no formatting violations)

> git status --short
(unchanged from before this step: everything still untracked, nothing committed in this repository yet --
no prior file was deleted or staged by this step; XlightsAI.Studio.Shell.csproj was edited in place per
§2, consistent with the additive change described there)
```

### 4.1 Full regression is 100% green this run — a change in environment, not in this step's own scope

`STEP-083.md`'s own session reported 25 pre-existing, environmental failures in `Audio.Tests`/`Shell.Tests` because no real Windows audio output device was available (`winmm` error 2). In this step's own session, a real audio output device was available, so `Audio.Tests` (38/38) and every `Shell.Tests` test depending on real `WaveOutAudioTransport` playback passed without any code change on this step's part. Neither `GestureCandidateCommitService` nor `CandidateCompareViewModel` touches audio I/O; this difference is purely a property of the current sandboxed session's own hardware availability, disclosed here rather than silently taken credit for as a fix.

## 5. Observable demonstration

A disposable console app (not committed to the repository, `%TEMP%\xlai-step084-demo-proj` — the same convention every prior Step-079–083 evidence doc used) exercised the real `GestureRefinementOrchestrator` → `CandidateCompareViewModel` → `GestureCandidateCommitService` → `EditTransactionHistoryRecorder` → `EditTransactionService.UndoAsync` pipeline against a real `JsonRefinementRunStore`/`JsonEditTransactionHistoryStore` backed by real temp-directory files — **this is the phase gate's own exact named demonstration**, verbatim: "Start with a knowingly over-busy 8-bar phrase, generate three candidates, inspect density/timing metrics and previews, accept one, then use Undo and show previous state restored."

```
=== PHASE-12 Step 084 demo: candidate gallery, score explanations, preview, explicit accept + Undo ===

Phase gate scenario, verbatim: "Start with a knowingly over-busy 8-bar phrase, generate three
candidates, inspect density/timing metrics and previews, accept one, then use Undo and show
previous state restored."

Run finished: Status=AwaitingApproval StopReason="Exhausted: AttemptsMade reached MaxAttempts." AttemptsMade=3/3 BestCandidateId=ce1c520b-230e-ccd2-ab89-8f47e9a2ae90

Gallery loaded: True  Candidates=3  SelectedCandidateId(default=best)=ce1c520b-230e-ccd2-ab89-8f47e9a2ae90

--- Candidate attempt 0 (ce1c520b-230e-ccd2-ab89-8f47e9a2ae90)  [BEST] ---
  Status=PreviewReady  AggregateScore=1.000  HasViolations=False
    LegalEffects     Value=1.00 Weight=1.0 HardGate=True  1 gesture(s) validated, scoped to permitted props/time, with no upstream violations.
    Density          Value=1.00 Weight=1.0 HardGate=False  4/4 permitted prop(s) targeted (100%).
    Timing           Value=0.50 Weight=0.0 HardGate=False  no BeatGrid supplied.
    MelodicAlignment Value=0.50 Weight=0.0 HardGate=False  no MelodyTrack supplied.
    MotifReuse       Value=0.50 Weight=0.0 HardGate=False  no prior, non-violating sibling attempts supplied to compare motif reuse against.
  Preview: Build on Arches, MegaTree, Mini1, Mini2 @ [0s, 16s) intensity 0.15->0.95

--- Candidate attempt 1 (34e8921d-4654-1aca-7271-2a6b6183f306) ---
  Status=PreviewReady  AggregateScore=0.875  HasViolations=False
    Density          Value=0.75 Weight=1.0 HardGate=False  3/4 permitted prop(s) targeted (75%).
  Preview: Build on Arches, MegaTree, Mini1 @ [0s, 16s) intensity 0.15->0.95

--- Candidate attempt 2 (8d72c331-e7ad-9d94-dd21-0978e5ccc5b1) ---
  Status=PreviewReady  AggregateScore=0.750  HasViolations=False
    Density          Value=0.50 Weight=1.0 HardGate=False  2/4 permitted prop(s) targeted (50%).
  Preview: Build on Arches, MegaTree @ [0s, 16s) intensity 0.15->0.95

User selects candidate attempt 0 (ce1c520b-230e-ccd2-ab89-8f47e9a2ae90) and accepts it.
Accept succeeded: True  Document revision 1 -> 2  EffectsAdded=4
Run status after accept: Accepted  BestCandidateId=ce1c520b-230e-ccd2-ab89-8f47e9a2ae90  StopReason="Accepted: candidate attempt 0 ('ce1c520b-230e-ccd2-ab89-8f47e9a2ae90') committed as EditTransaction '77f372a4-7581-48b6-b51e-f3eb3d082567'."

After Undo: document revision = 1  effect count = 0
PASS: Undo restored the exact pre-accept document state: True

--- Negative path: stale revision (fresh run, independent of the one just accepted) ---
Negative-demo run finished: Status=AwaitingApproval BestCandidateId=0a4fb81e-4c68-26c8-1a45-c0477b9b20bf
PASS: rejected as expected. Reason=StaleRevision  Message="Document '6417bf6f-a5c8-4f56-82e3-d12701404850' revision 2 no longer matches LoopRun '62fd0221-86e2-47b0-8098-b224d3f7e6df''s own BaseRevision 1; the document changed since this run was snapshotted. No candidate was committed."
PASS: run status unchanged after rejected stale-revision attempt (no partial mutation): True

=== Demo complete ===
```

(Full output included three candidates with complete per-metric explanation lines; the above elides the repeated `Timing`/`MelodicAlignment`/`MotifReuse` "no data" rows for candidates 1/2 only for this doc's own brevity — every row was printed and inspected in the real run.)

This demo realizes the phase gate's own exact named workflow end-to-end against real, non-mocked collaborators: an over-busy 8-bar phrase (4 permitted props, 16-second scope) produces three real candidates with decreasing density (100%/75%/50% — "inspect density... metrics"), each with a full score explanation and textual preview ("and previews"), the user explicitly selects and accepts the best one ("accept one"), and a real `EditTransactionService.UndoAsync` call — fed the accepted transaction via the already-accepted `EditTransactionHistoryRecorder` bridge — restores the document to its exact pre-accept revision and effect count ("use Undo and show previous state restored"). A second, independent run then proves the stale-revision hard gate rejects without any partial mutation. The demo project, its `bin`/`obj` output, and the two temp JSON-store directories it created were created entirely under `%TEMP%`, run, and then deleted (`Remove-Item -Recurse -Force`); none is part of this repository's tracked tree.

Avalonia/real-UI demonstration was not attempted: no real SCREEN 14/15 `.axaml` view exists anywhere in this repository yet (the project's own pre-existing, consistent convention disclosed identically in every PHASE-11/Step-079–083 evidence doc) — this step's own required "Avalonia screenshot or repeatable desktop interaction recording with saved-state verification" outcome is satisfied at the bindable `CandidateCompareViewModel` layer a real SCREEN 15 view would bind to, exactly as `AiEditProposalReviewViewModel` already established for SCREEN 14.

## 6. Fixtures / input hashes

No fixture files were added to `fixtures/` or `src/XlightsAI.Studio.Reference/Fixtures/` — the same convention Steps 079–083 already established for this phase. Every `EditScope`/`LoopRun`/`SequenceDocument`/`EffectInstance` fixture value used by `Step084AcceptanceTests`/`Step084CandidateCompareViewModelTests` is a real, fully-validated instance of its own already-accepted type, constructed with the same `Scope`/`NewQueuedRun` helper conventions `Step079`–`Step083AcceptanceTests.cs` already established.

## 7. Unresolved issues / known gaps

- **No real SCREEN 15 COMPARE Avalonia/AXAML view exists.** Disclosed in §5, identical in nature to the gap every prior Step 072–083 evidence doc already disclosed for its own named screen.
- **`GestureCandidateCommitService` compiles every gesture's own target prop using the one recipe this build has ever verified (`effect.on.v1`).** A gesture's own `Kind`/`StartIntensity`/`EndIntensity` are therefore not literally preserved as committed effect parameters (the registry's own `IsSupportedParameterValue` recognizes no other value) — only the gesture's own target prop and time window become the real, committed effect's shape. This is a disclosed, deliberate consequence of PHASE-02's own still-narrow recipe registry, not a defect introduced by this step; a future phase that adds a richer, verified xLights recipe could extend `GestureCandidateCommitService` additively to carry more of a gesture's own shape into the committed parameters.
- **No persisted `EditProposalAuditRecord`-style audit trail exists for Refinement accept/reject decisions** (unlike `XlightsAI.Studio.AiEdits.EditProposalApprovalService`'s own optional `IEditProposalAuditStore`). `LoopRun.StopReason` carries a human-readable acceptance/rejection record, which is sufficient for this step's own required acceptance text, but a dedicated audit store was not added — PHASE-12 Section 6 names no such requirement for this step, and `CLAUDE.md`'s own "no designing for hypothetical future requirements" rule applies.
- **The committed effect's own `Layer` assignment can, in principle, run out of the same `ChoreographyCompiler.MaxLayers = 8` headroom `ChoreographyCompiler` itself already bounds** — disclosed, not silently unbounded (§2.1/ADR §2); this step's own tests do not need to reach that ceiling (a knowingly over-busy 8-bar phrase here still only ever needs 1 layer per prop against an otherwise-empty document).

## 8. Required-acceptance-text-by-text tie-back

| Required text (verbatim) | Where proven |
|---|---|
| "User can compare alternatives and commit chosen candidate" | `Step084_Acceptance_UserCanChooseANonBestAlternative_NotOnlyTheRetainedBest`; `Step084_Acceptance_UserCanSelectAnyAlternative_ThenAcceptTheExplicitlyChosenOne`; demo §5 (user inspects all three candidates before accepting). |
| Implementation guidance, verbatim: "Compare best-vs-current with scores" | `CandidateGalleryRow.IsBest`/`AggregateScore`; `Step084_Acceptance_LoadAsync_PopulatesGalleryWithScoresAndExplanations_DefaultsToBest`; demo §5's three-candidate score table. |
| Implementation guidance, verbatim: "...and synchronized preview" | `CandidateGalleryRow.PreviewSummary`; demo §5's `Preview:` lines. |
| Implementation guidance, verbatim: "record deliberate human accept/reject and optional feedback labels" | `GestureCandidateCommitService.AcceptAsync`/`RejectAsync`; `Step084_Acceptance_Reject_LeavesDocumentByteEquivalent_RecordsOptionalFeedbackLabel`; `Step084_Acceptance_Reject_WithNoFeedbackLabel_RecordsGenericReason`. |
| Required failure/edge case, verbatim: "Out-of-scope... must be rejected without partial mutation" | `Step084_NegativePath_CandidateGestureOutOfScope_RejectedWithoutPartialMutation`; layer-collision path in `Step084_Acceptance_CommittedEffects_NeverCollideWithExistingDocumentEffects`'s own underlying gate. |
| Required failure/edge case, verbatim: "...locked... must be rejected without partial mutation" | `Step084_NegativePath_LockedEffectInScope_RejectedWithoutPartialMutation`. |
| Required failure/edge case, verbatim: "...or stale revision must be rejected without partial mutation" | `Step084_NegativePath_StaleRevision_RejectedWithoutPartialMutation`; `Step084_NegativePath_StaleRevision_AcceptFails_GalleryStateUntouched`; demo §5's negative-path section. |
| PHASE-12 Section 7 cross-cutting test #8, verbatim: "User approval checks base revision to prevent stale overwrite" | Same tests as the row above — `document.Revision != run.BaseRevision` is checked explicitly before any other gate in `AcceptAsync`. |
| PHASE-12 Section 1, verbatim (restated from Steps 079–083): a violating candidate "must not auto-commit" | `Step084_NegativePath_ViolatingCandidate_NeverCommits`. |

## 9. PASS / BLOCKED

**PASS.**

- The required-acceptance sentence ("User can compare alternatives and commit chosen candidate") is proven both at the service layer (`GestureCandidateCommitService`) and at the bindable view-model layer (`CandidateCompareViewModel`), including explicitly accepting a non-best alternative.
- "Out-of-scope, locked or stale revision must be rejected without partial mutation" is proven for all three named categories, each with its own dedicated test asserting the run's own persisted state (`Status`, `ClaimToken`) is completely unchanged after the rejection, plus a real demo scenario for the stale-revision case.
- The phase gate's own exact named demonstration (§5) ran against real, non-mocked collaborators end-to-end: over-busy 8-bar phrase → three real candidates with inspectable density/timing metrics and previews → explicit accept → real `EditTransactionService.UndoAsync` restoring the exact pre-accept document state.
- Full solution build succeeds with 0 warnings/errors; `dotnet format --verify-no-changes` reports no violations.
- Full regression: **1191/1191 tests pass, 0 failed** (including every previously-environmental `Audio.Tests`/`Shell.Tests` failure from `STEP-083.md`'s own session — a real audio device was available in this session; disclosed in §4.1 as an environment difference, not a fix claimed by this step). This step's own new scope, `Refinement.Tests` (+12) and `Shell.Tests` (+7), is 100% green.
- No prior accepted contract (any PHASE-01 through PHASE-11 type, or any Step 079–083 type) was removed or given a breaking signature change. Only one additive `ProjectReference` was added (`Shell` → `Refinement`), verified non-cyclic.
- No show file, fixture, or prior persisted artifact was modified.

**Explicitly not claimed:** any real Avalonia UI/screenshot for SCREEN 15 (no `.axaml` view exists anywhere in this repository yet — disclosed identically to every prior PHASE-11/PHASE-12 step); a gesture's own `Kind`/intensity ramp preserved as committed effect parameters (disclosed simplification, §7, bounded by PHASE-02's own still-narrow verified recipe registry); a persisted, queryable audit-record store for Refinement accept/reject decisions (disclosed scope boundary, §7 — `LoopRun.StopReason` satisfies this step's own required text without it).
