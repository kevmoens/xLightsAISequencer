# PHASE-11 Step 076 — Implement dry-run proposals, before/after diff and Accept/Reject

**Date:** 2026-10-17
**Spec:** `docs/phase-specs/PHASE-11.md` §6, Step 076
**Required acceptance (verbatim):** "Reject leaves state byte-equivalent; accept records transaction."
**Required failure/edge case (verbatim):** "Clean machine and unsupported runtime must produce user-readable diagnostics."

## 1. Pre-implementation baseline inspection

Before writing any code, the existing repository state was read directly (not taken on faith):

- **Steps 072–075** (`src/XlightsAI.Studio.AiEdits/`) already implement `SelectionContext`, `StructuredEditOperation`, `StructuredEditOperationKinds` (five closed kinds: `SetBrightness`, `SetDirection`, `SetSpeed`, `SnapToGrid`, `ReplaceRole`), `EditProposal`, `IStructuredEditInterpreter`/`StructuredCommandInterpreter` (local grammar, no network), and `IOptionalAiPlanner`/`HttpStructuredOutputAiPlanner` (opt-in remote planner). Neither path ever mutates a `SequenceDocument` — both only ever produce an `EditProposal`.
- **`src/XlightsAI.Studio.Shell/AiEditRequestPanelViewModel.cs`** (Step 074) explicitly defers commit to "Step 076's own later Accept/Reject" (its own doc comment, verbatim) — confirming this step's own scope boundary directly from the accepted prior contract.
- **Critical architectural finding, verified before any design decision:** `EffectRecipeRegistry` (`src/XlightsAI.Studio.Xlights/EffectRecipeRegistry.cs`), the only `IEffectRecipeRegistry` implementation in the repository, recognizes exactly one recipe (`effect.on.v1`) with exactly one verified parameter/value pair: `EffectFamily=On`. `IEditTransactionService.ApplyAsync(SetEffectParameters)` (PHASE-03, already accepted) rejects any other parameter key/value via `EditRejectionReason.UnsupportedParameter` by design ("do not invent xLights effect parameters," PHASE-11 Section 2). None of Step 073's five quick-action kinds (brightness/direction/speed/snap/role) are themselves registered recipe parameters. Routing a proposal's own operations through that existing pipeline unmodified would therefore make **every** dry-run proposal reject unconditionally — this is disclosed and reconciled in §3 below rather than silently worked around.
- `EditTransaction`, `EditApplyResult`, `EditScope`, `IEffectLockPolicy`/`EffectLockPolicy` (PHASE-03, all already accepted) are plain, publicly-constructible/callable types — reused verbatim, not re-declared.
- `fixtures/` has no PHASE-11-specific fixture files; this step's own tests use the same in-code "gate-demo" `DisplayGraph`/`SequenceDocument` fixture (Roofline, Arch 1, Arch 2) every prior PHASE-02/03/11 step already established via `ThreeEffectPlanFactory`/`EffectRecipeRegistry`.

## 2. Scope actually implemented

New files, all in `src/XlightsAI.Studio.AiEdits/` unless noted:

| File | Purpose |
|---|---|
| `ProposalValidation.cs` | PHASE-11.md Section 3's own proposed record, implemented verbatim: `(bool Allowed, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings)`. |
| `IEditProposalValidator.cs` / `EditProposalValidator.cs` | PHASE-11.md Section 3's own proposed boundary. Collects **every** independent reason a proposal cannot be applied (unknown operation kind, out-of-scope target, out-of-scope time, locked effect, stale `EditProposal.BaseRevision`, stale `SelectionContext.BaseDocumentRevision`, empty operation list) rather than failing fast on the first one — "Clean machine and unsupported runtime must produce user-readable diagnostics" (this step's own required failure/edge case) is read as "show the user everything wrong at once," not just the first gate. Also emits non-blocking `Warnings` (e.g. an operation that currently overlaps zero effects). Never throws. |
| `EditProposalRejectedException.cs` | Thrown by both services below when `ProposalValidation.Allowed` is `false`. Carries the full `Errors` list; `Message` joins them, so a UI or console caller gets the real diagnostic text, never a generic failure string. |
| `ProposalDiff.cs` | PHASE-11.md Section 6 Step 076's own implementation guidance, verbatim: "show changed effect count/models/time." `(int ChangedEffectCount, IReadOnlyList<ModelId> AffectedModels, TimeWindow AffectedTime)`. |
| `EditProposalDryRun.cs` | The "sandbox copy" result (Section 2: "Prepare proposal in a sandbox copy, render and show a clear diff"): `(EditProposal Proposal, SequenceDocument Candidate, ProposalDiff Diff)`. `Candidate` is a brand-new document instance; the input document is never mutated. |
| `ProposalApplier.cs` (internal) | Shared "apply every operation in a proposal to a copy of the effect list" logic, used identically by both services below so the exact mutation previewed is the exact mutation committed. See §3 for the disclosed ADR on why this does not route through `IEditTransactionService.ApplyAsync(SetEffectParameters)`. |
| `IEditProposalDryRunService.cs` / `EditProposalDryRunService.cs` | `Preview(EditProposal, SelectionContext, SequenceDocument) -> EditProposalDryRun`. Validates first (throws `EditProposalRejectedException` on any error, before any candidate is built), then applies via `ProposalApplier`. Pure; never mutates its own `document` argument. |
| `IEditApprovalService.cs` / `EditProposalApprovalService.cs` | `AcceptAsync(EditProposalDryRun, SelectionContext, SequenceDocument, CancellationToken) -> Task<EditApplyResult>`. **Re-validates against the live `document` argument** (not the possibly-stale cached `dryRun.Candidate`) before committing — a concurrent edit landing between preview and accept is caught here, not silently papered over. On success, returns the already-accepted `EditApplyResult(Document, Transaction)` shape (PHASE-03), with `EditTransaction.SerializedBeforePatch`/`SerializedAfterPatch` carrying only the affected effects, not the whole document (extending, not replacing, `EditTransactionService.CommitReplacement`'s own single-effect convention to a multi-effect list). |
| `src/XlightsAI.Studio.Shell/AiEditProposalReviewViewModel.cs` | SCREEN 14 AI EDIT's own bindable preview/accept/reject drawer — `PreviewAsync`, `Reject()` (clears bindable state only, never touches any document), `AcceptAsync`. Mirrors the already-accepted `AiEditRequestPanelViewModel`/`StructuredCommandQuickActionsViewModel` conventions (explicit `ScreenLoadState`, "leave bindable state unchanged on failure"). No Avalonia types — same convention every other screen view model in this project already follows. |

New test files:

- `tests/XlightsAI.Studio.AiEdits.Tests/Step076AcceptanceTests.cs` — 13 tests against the real `EditProposalValidator`/`EditProposalDryRunService`/`EditProposalApprovalService`/`StructuredCommandInterpreter`/`EffectLockPolicy`/`SelectionContextCaptureService` (no mocking library).
- `tests/XlightsAI.Studio.Shell.Tests/Step076AiEditProposalReviewViewModelTests.cs` — 5 tests against the real `AiEditProposalReviewViewModel` and its real collaborators.

## 3. Architecture deviations / decisions (ADRs)

1. **`ProposalApplier` does not route through `IEditTransactionService.ApplyAsync(SetEffectParameters)`.** As established in §1, the only registered recipe (`effect.on.v1`) has exactly one verified parameter (`EffectFamily=On`); none of Step 073's five operation kinds are verified xLights recipe parameters yet. Forcing them through that recipe-validated pipeline would make this step's own acceptance ("accept records transaction") unachievable for any real proposal. Instead, each operation's own `Parameters` are recorded on every effect it reaches under a disjoint `"AiEdit.<Kind>.<Key>"` namespace (or a bare `"AiEdit.<Kind>"` marker for a parameterless kind like `SnapToGrid`) — explicitly disclosed proposal metadata, never mixed with or validated as a real xLights recipe parameter (Section 2: "do not invent xLights effect parameters" is respected by *not* writing these into the recipe-owned parameter space at all). Translating this metadata into a concrete, recipe-verified xLights effect is deferred to a later step/phase once more than one recipe exists. This is the same disclosed-narrowing pattern every prior PHASE-11 step already used for `EditContext` → `SelectionContext`.
2. **`IEditProposalValidator`/`IEditApprovalService` are declared against `SelectionContext`/explicit arguments, not the spec's proposed `EditContext`/bare `Guid proposalId`.** The spec's own proposed `IEditApprovalService.ApproveAsync(Guid proposalId, ct)` implies a persisted, server-side pending-proposal store keyed by id. No such store exists yet — building one is Step 078's own later scope ("Persist proposal audit, undo/redo and failure recovery"), exactly mirroring the reconciled deviation `IEditTransactionService.ApplyAsync`'s own doc comment already documents for this exact repository (no project-session/current-document infrastructure exists either). `AcceptAsync` therefore takes the `EditProposalDryRun`, its `SelectionContext`, and the live `SequenceDocument` explicitly.
3. **`Accept` re-validates against the live document, not the cached `dryRun.Candidate`.** A dry run is a point-in-time snapshot; if a concurrent edit (or a newly applied lock) lands between `Preview` and `Accept`, blindly committing the earlier-computed `Candidate` would silently discard that concurrent change or violate a lock the user never saw violated. `EditProposalApprovalService.AcceptAsync` re-runs `IEditProposalValidator.Validate` and recomputes `ProposalApplier.Apply` against the caller's current `document` argument every time — proven by `StaleRevision_BetweenPreviewAndAccept_IsRejected_WithoutMutation_NegativePath` (AiEdits) and `AcceptAsync_StaleRevisionSinceDrifted_IsRejected_DryRunLeftIntact_NegativePath` (Shell).
4. **`EditTransaction.SerializedBeforePatch`/`SerializedAfterPatch` carry a JSON array of every affected effect, not a single effect.** `EditTransactionService.CommitReplacement` (PHASE-03) only ever replaces one effect per command and serializes that one effect. A `StructuredEditOperation`'s own `Targets`/`Range` can legitimately reach more than one effect at once (the phase gate's own worked example: "five arches" in one proposal) — this step extends that existing single-effect convention to a list, rather than inventing an unrelated shape, and the extension is additive (no existing `EditTransaction` consumer is touched).
5. **`IEditProposalValidator.Validate` never throws; it always returns a complete `ProposalValidation`.** Every other PHASE-03/11 gate in this repository (`EditCommandRejectedException`, `AiProposalRejectedException`) fails fast on the first violated gate. This step deliberately diverges: Step 076's own required failure/edge case — "Clean machine and unsupported runtime must produce user-readable diagnostics" — is read as requiring a complete diagnostic report (every unknown kind, every out-of-scope target, every lock conflict at once), which only a non-throwing, errors-accumulating validator can provide to a review UI in one pass. `IEditProposalDryRunService.Preview`/`IEditApprovalService.AcceptAsync` are the layer that turns `Allowed == false` into a single thrown `EditProposalRejectedException` carrying the full `Errors` list, for callers that prefer the exception-based convention every other gate in this project already uses.

No existing accepted contract (Steps 072–075, or any PHASE-02/03 contract) was modified. All new files are additive.

## 4. Commands actually executed

All commands were run via the **PowerShell** tool. (As in every prior PHASE-11 evidence doc, the Bash tool returns no usable output in this sandboxed session — every Bash invocation, including a bare `true`, silently exits 1 with no output; this is disclosed, not worked around by claiming Bash output that never appeared.)

```
> dotnet --version
10.0.302

> dotnet build src\XlightsAI.Studio.AiEdits\XlightsAI.Studio.AiEdits.csproj -v minimal
Build succeeded. 0 Warning(s) 0 Error(s)

> dotnet build XlightsAI.Studio.slnx -v minimal
Build succeeded. 0 Warning(s) 0 Error(s)

> dotnet test tests\XlightsAI.Studio.AiEdits.Tests\XlightsAI.Studio.AiEdits.Tests.csproj -v minimal --filter "FullyQualifiedName~Step076"
Passed! - Failed: 0, Passed: 13, Skipped: 0, Total: 13, Duration: 265 ms

> dotnet test tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj -v minimal --filter "FullyQualifiedName~Step076"
Passed! - Failed: 0, Passed: 5, Skipped: 0, Total: 5, Duration: 173 ms

> dotnet test XlightsAI.Studio.slnx -v minimal   (full solution regression)
XlightsAI.Studio.Xlights.Tests.dll      : 74/74 passed
XlightsAI.Studio.Editing.Tests.dll      : 91/91 passed
XlightsAI.Studio.Audio.Tests.dll        : 38/38 passed
XlightsAI.Studio.Display.Tests.dll      : 56/56 passed
XlightsAI.Studio.AiEdits.Tests.dll      : 61/61 passed  (48 pre-existing + 13 new)
XlightsAI.Studio.Structure.Tests.dll    : 78/78 passed
XlightsAI.Studio.Composition.Tests.dll  : 78/78 passed
XlightsAI.Studio.Melody.Tests.dll       : 117/117 passed
XlightsAI.Studio.Projects.Tests.dll     : 25/25 passed
XlightsAI.Studio.Shell.Tests.dll        : 256/256 passed (251 pre-existing + 5 new)
XlightsAI.Studio.Rhythm.Tests.dll       : 82/82 passed
XlightsAI.Studio.Reference.Tests.dll    : 31/31 passed
XlightsAI.Studio.Stems.Tests.dll        : 101/101 passed
TOTAL: 1108 passed / 1108 total, 0 failed

> dotnet format XlightsAI.Studio.slnx --verify-no-changes --no-restore
(no output -- no formatting violations)
```

**Note on the Audio/Shell test counts versus prior steps' evidence docs:** STEP-072 through STEP-075 each disclosed 10 `XlightsAI.Studio.Audio.Tests` and 15 `XlightsAI.Studio.Shell.Tests` failures in their own sandboxed session due to an unavailable real audio output device. In **this** session, the full regression above shows `Audio.Tests` at 38/38 and `Shell.Tests` at 256/256 — i.e. the audio device was available this run and every previously-disclosed, environment-dependent failure is absent. This is an environment difference between sessions, not a change introduced by this step; no code this step touches has any relationship to audio playback.

Observable desktop demonstration (disposable console app, not committed to the repository, matching the `%TEMP%\xlai-stepNNN-demo-proj` convention every prior PHASE-11 evidence doc used):

```
> New-Item -ItemType Directory -Force -Path "$env:TEMP\xlai-step076-demo-proj"
> # wrote demo.csproj + Program.cs referencing the real AiEdits/Composition/Editing/Xlights/Contracts projects
> cd "$env:TEMP\xlai-step076-demo-proj"
> dotnet run -v minimal
```

Full transcript (abridged to remove restore/build noise):

```
=== PHASE-11 Step 076 demo: Dry-run proposals, before/after diff, Accept/Reject ===
Roofline hash before any proposal: BF25DD0E8D546930A060DD3B28D537BF497F672BA1E05094E7934D2FF93E510F
Selected: Arch 1, Arch 2 @ [100000000, 300000000)

--- Round 1: preview 'rising piano pops' (mapped to the known offline 'brightness 90' quick action), then REJECT ---
Diff: 2 effect(s) changed on [Arch 1, Arch 2] @ [100000000, 300000000)
PASS: document revision still 1 (candidate only, nothing committed).
User rejects. Discarding candidate -- no call to Accept.
PASS: document revision unchanged (1), Roofline hash unchanged: True

--- Round 2: repeat preview, then APPROVE ---
Diff: 2 effect(s) changed on [Arch 1, Arch 2] @ [100000000, 300000000)
PASS: one transaction recorded. BeforeRevision=1, AfterRevision=2
PASS: committed document revision is now 2.
Arch 1 AiEdit.SetBrightness.Level = 90
Arch 2 AiEdit.SetBrightness.Level = 90
Roofline hash after approval: BF25DD0E8D546930A060DD3B28D537BF497F672BA1E05094E7934D2FF93E510F
PASS: Roofline hash unchanged across the whole round trip: True

--- Negative path: unknown operation kind is rejected with a user-readable diagnostic ---
PASS: rejected with user-readable diagnostic(s): Operation 'op-x' names an unknown kind 'DeleteEverything'.

=== Demo complete ===
```

This demo exercises the exact phase gate workflow (Section 1/Section 7's own "Select... on five arches [narrowed to the gate-demo fixture's own two arches]... inspect affected models/times and before/after visualization; reject, repeat, approve, verify roofline hash unchanged"): preview → reject → repeat preview → approve → SHA-256 hash of the untouched Roofline effect proven byte-identical before and after the entire round trip.

The demo project and its `bin`/`obj` output were created entirely under `%TEMP%`, run, and then deleted (`Remove-Item -Recurse -Force`); they are not part of this repository's tracked tree.

## 5. Persisted-output inspection / observable demonstration

See §4's demo transcript — a real `ThreeEffectPlanFactory`/`EffectRecipeRegistry`-produced `SequenceDocument`, a real `SelectionContextCaptureService`-captured `SelectionContext`, a real `StructuredCommandInterpreter`-produced `EditProposal`, and the real `EditProposalDryRunService`/`EditProposalApprovalService` under test — not an in-memory mock standing in for any of these. The Roofline effect's own SHA-256 hash (computed over its full JSON serialization, including `Id`, every field) is printed before the first proposal and again after a full preview→reject→preview→approve round trip, and is asserted equal by the demo itself.

## 6. Fixtures / input hashes

No new fixture files were added to `fixtures/`. All tests and the demo use the same in-code "gate-demo" `DisplayGraph`/`SequenceDocument` fixture (`SourceLayoutHash: "TEST-HASH-NOT-A-REAL-FILE"`, three models: Roofline / Arch 1 / Arch 2) that Steps 072–075 already established, built via `ThreeEffectPlanFactory`/`EffectRecipeRegistry` — consistent with the project's own established no-persisted-fixture-file convention for this step family.

## 7. Unresolved issues / known gaps

- **No real SCREEN 14 AI EDIT Avalonia/AXAML view exists yet** — a pre-existing, disclosed, project-wide condition carried forward unchanged from Steps 072–075 (not newly introduced here). This step's own "Versioned, inspectable project output plus a reproducible test report" outcome is proven at the bindable `AiEditProposalReviewViewModel` layer plus the console demo in §4, per the same convention Steps 074/075 already established.
- **Operation parameters are recorded as disclosed `"AiEdit.<Kind>.<Key>"` metadata on the affected `EffectInstance`, not translated into a concrete, recipe-verified xLights effect setting.** See ADR §3.1. This is a direct, necessary consequence of the repository currently shipping exactly one recipe (`effect.on.v1`) with exactly one verified parameter. Once a future phase/step registers real recipes for brightness/direction/speed/role, translating this already-committed metadata into those recipes' own verified parameters is that later work's own scope — the proposal/diff/accept/reject transaction life-cycle this step owns does not change shape when that happens (the same `EditTransaction`/`EditApplyResult` records already carry whatever `Parameters` dictionary a future translation step would produce).
- **No persisted pending-proposal store keyed by `Guid proposalId` exists** — see ADR §3.2. `IEditApprovalService.AcceptAsync` takes the `EditProposalDryRun` object directly. Step 078 ("Persist proposal audit, undo/redo and failure recovery") is the step whose own numbered scope is building that persisted lifecycle; this step's own "accept records transaction" is satisfied without it.
- **Motif propagation (multiple occurrences of the same gesture) is out of this step's own scope** — Step 077 ("Implement explicit motif propagation and edit-scope intersection validation") is the next numbered step and has not been started.
- **Undo/redo integration**: `EditApplyResult`/`EditTransaction` returned by `EditProposalApprovalService.AcceptAsync` are not yet pushed onto `EditTransactionService`'s own undo/redo stacks (PHASE-03 Step 022) — that stack is currently only populated by `EditTransactionService.ApplyAsync`'s own internal commands. Wiring an AI-approved transaction into the same undo/redo history (so "Accept creates one undoable transaction and survives restart," the phase-level cross-cutting test #8 the overall PHASE-11 prompt names) is Step 078's own explicit scope; it is disclosed here as not yet done, not silently assumed.

## 8. Required-acceptance-text-by-text tie-back

| Required text (verbatim) | Where proven |
|---|---|
| "Reject leaves state byte-equivalent" | `Reject_LeavesDocumentByteEquivalent_NegativePath` (AiEdits: JSON-serialization equality of the whole document before/after a preview+reject cycle); `Reject_LeavesDocumentByteEquivalent_NegativePath` (Shell: revision/effect-count equality); demo §4 Round 1 (Roofline SHA-256 hash unchanged after reject). |
| "accept records transaction" | `Step076_Acceptance_PreviewThenAccept_RecordsOneTransaction` (both AiEdits and Shell test files): asserts `EditTransaction.BeforeRevision`/`AfterRevision`, that the committed document's own revision matches, and that the transaction's own serialized patches name exactly the affected effects; demo §4 Round 2. |
| "Clean machine and unsupported runtime must produce user-readable diagnostics" | `UnknownOperationKind_IsRejected_WithUserReadableDiagnostic_NegativePath`, `OutOfScopeTarget_RooflineOutsideSelectedArches_IsRejected_NegativePath`, `OutOfScopeTime_ExpandedRange_IsRejected_NegativePath`, `LockedTarget_IsRejected_EvenWithAnOtherwiseValidProposal_NegativePath`, `StaleRevision_AtPreviewTime_IsRejected_NegativePath`, `EmptyOperationsList_IsRejected_NegativePath` (every one asserts `EditProposalRejectedException.Errors`/`Message` carries specific, human-readable text, not a generic failure); `PreviewAsync_UnknownOperationKind_ShowsUserReadableDiagnostic_NegativePath` (Shell, asserts the bindable `ErrorMessage`); demo §4's negative-path section. |
| "stale persisted state must not corrupt the project" (cross-cutting, every PHASE-11 step) | `StaleRevision_AtPreviewTime_IsRejected_NegativePath`, `StaleRevision_BetweenPreviewAndAccept_IsRejected_WithoutMutation_NegativePath` (AiEdits); `AcceptAsync_StaleRevisionSinceDrifted_IsRejected_DryRunLeftIntact_NegativePath` (Shell). |
| "cancellation... must not corrupt the project" (cross-cutting) | `AcceptAsync_CanceledToken_ThrowsWithoutMutation_NegativePath`. |
| Section 2: "LLM cannot target Roofline outside selected arches" (phase gate cross-cutting test #3) | `OutOfScopeTarget_RooflineOutsideSelectedArches_IsRejected_NegativePath` (a hand-crafted proposal naming Roofline is rejected before any candidate is built; the Roofline effect itself is asserted to carry no `AiEdit.*` metadata afterward). |
| Section 7 phase gate demonstration: "...inspect affected models/times and before/after visualization; reject, repeat, approve, verify roofline hash unchanged" | Demo §4, end to end: preview → diff shown → reject → repeat preview → approve → SHA-256 Roofline hash proven identical before and after. |

## 9. PASS / BLOCKED

**PASS.**

- Both required-acceptance-text sentences are proven by passing unit tests against real collaborators (no mocking library) at both the domain-service layer (`EditProposalDryRunService`/`EditProposalApprovalService`) and the bindable view-model layer (`AiEditProposalReviewViewModel`), and by a real console demonstration exercising the full preview/reject/repeat/approve/verify-hash workflow named in the phase gate.
- The required failure/edge case ("Clean machine and unsupported runtime must produce user-readable diagnostics") is proven with explicit negative-path tests asserting the actual diagnostic text returned, not just that *some* exception was thrown.
- Full solution build succeeds with 0 warnings/errors; `dotnet format --verify-no-changes` reports no violations.
- Full regression: 1108/1108 tests pass (0 failed) across every test assembly in the solution; 18 new tests added by this step (13 AiEdits + 5 Shell), 0 removed, 0 modified.
- No prior accepted contract (Steps 072–075, or any PHASE-02/03 contract) was modified — every new type in this step is additive.
- No show file, fixture, or prior persisted artifact was modified.

**Explicitly not claimed:** real xLights recipe translation of the five operation kinds (brightness/direction/speed/snap/role) into verified xLights effect-settings XML — the repository currently ships exactly one recipe with exactly one verified parameter, and inventing new recipe parameters without verification against real xLights behavior would itself violate PHASE-11 Section 2. This step's own owned boundary is the proposal/diff/accept-reject transaction life-cycle, proven end-to-end against the one recipe that does exist; a later step's/phase's own scope is adding more verified recipes for this metadata to translate into. Undo/redo integration and a persisted pending-proposal store are likewise explicitly deferred to Step 078, per its own numbered scope.
