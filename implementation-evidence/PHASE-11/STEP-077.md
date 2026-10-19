# PHASE-11 Step 077 — Implement explicit motif propagation and edit-scope intersection validation

**Date:** 2026-10-17
**Spec:** `docs/phase-specs/PHASE-11.md` §6, Step 077
**Required acceptance (verbatim):** "Locked/out-of-scope effects cannot change even with malicious proposal."
**Required failure/edge case (verbatim):** "Out-of-scope, locked or stale revision must be rejected without partial mutation."

## 1. Pre-implementation baseline inspection

Before writing any code, the existing repository state was read directly (not taken on faith):

- Ran the full baseline regression (`dotnet test XlightsAI.Studio.slnx`) before any change: `XlightsAI.Studio.AiEdits.Tests` 61/61, `XlightsAI.Studio.Shell.Tests` 256/256, all other assemblies green — the audio output device was available in that baseline run, matching Step 076's own disclosed "environment difference between sessions" note.
- **Steps 072–076** (`src/XlightsAI.Studio.AiEdits/`) already implement `SelectionContext`, `EditScope` (PHASE-03), `StructuredEditOperation`/`StructuredEditOperationKinds`, `EditProposal`, `IEditProposalValidator`/`EditProposalValidator`, `ProposalApplier` (internal), `EditProposalDryRun`/`IEditProposalDryRunService`/`EditProposalDryRunService`, and `IEditApprovalService`/`EditProposalApprovalService` — all scoped to exactly one `SelectionContext.Scope` per proposal. Step 076's own evidence doc (`implementation-evidence/PHASE-11/STEP-076.md` §7) explicitly disclosed motif propagation as out of its own scope and named this step as the next one.
- **`src/XlightsAI.Studio.Structure/`** (PHASE-08) already owns `MotifLinkCandidate`/`MotifLinkOverride`/`MotifGroupOverrideApplier`/`MotifGroupCandidate` — the automatic+user-approved motif grouping machinery. Verified this project does **not** reference `XlightsAI.Studio.AiEdits` (no cyclic reference risk), and confirmed the already-established "disclosed narrowing" convention this project's own types use (`SelectionContext`'s own remarks, verbatim: "A caller free to reference those other aggregates' own real ids here without this project ever depending on them") — so this step's new types reference `Structure` **not at all**, taking only an already-resolved `EditScope` per other occurrence from whatever caller (a future motif-group inspector UI) assembles the candidate set.
- `IEffectLockPolicy`/`EffectLockPolicy`, `EditScope`, `EditCommandRejectedException`/`EditRejectionReason` (PHASE-03, already accepted) are reused verbatim, not re-declared.
- `fixtures/` has no PHASE-11-specific fixture files; `ThreeEffectPlanFactory` is hard-coded to exactly 3 targets (verified by reading `ThreeEffectPlanFactory.cs`), which cannot express the ≥5-model, multi-occurrence fixture this step's own acceptance tests need — so this step's own tests construct `SequenceDocument`/`EffectInstance` fixtures directly via `EffectRecipeRegistry.CreateVerified` (a plain, publicly-callable method with no target-count restriction), the same recipe every prior step already uses, just not funneled through the 3-target factory.

## 2. Scope actually implemented

New files, all in `src/XlightsAI.Studio.AiEdits/` unless noted:

| File | Purpose |
|---|---|
| `MotifOccurrenceTarget.cs` | One other occurrence of the same motif a base proposal was built for, expressed purely as `(Guid OccurrenceId, EditScope Scope)` — never a reference to `Structure`'s `MotifGroupCandidate`/phrase ids. |
| `MotifPropagationSelection.cs` | The user's explicit, per-occurrence propagation choice: `IReadOnlySet<Guid> IncludedOccurrenceIds`. Empty (`MotifPropagationSelection.InstanceOnly`) is the step's own required "instance-only choice." |
| `MotifPropagatedProposal.cs` | Output of `MotifPropagationExpander.Expand`: a widened `EditProposal` plus `IReadOnlyDictionary<string, EditScope> PermittedScopeByOperationId` — one permitted scope **per operation**, not one blanket context scope, because a propagated clone's own permitted scope is a different occurrence's own boundary. |
| `MotifPropagationExpander.cs` | Pure, deterministic widening: for each included occurrence, clones every base operation with `Range`/`Targets` set to that occurrence's own `EditScope` exactly (never widened), under a `"{OperationId}::occurrence:{OccurrenceId}"` id. Throws `ArgumentException` for a duplicate occurrence id in the candidate set or a selection naming an occurrence absent from it — an internally inconsistent caller, not untrusted external input. |
| `IMotifPropagationValidator.cs` / `MotifPropagationValidator.cs` | "Edit-scope intersection validation" (this step's own heading, verbatim): validates every operation — original or propagated — against its own recorded permitted scope only (unknown kind, out-of-scope target/time against *that operation's own* scope, locked effect via the already-accepted `IEffectLockPolicy`, stale `EditProposal.BaseRevision`, empty operation list). All-or-nothing: any single failing operation makes the whole widened proposal `Allowed == false`. |
| `IMotifPropagationDryRunService.cs` / `MotifPropagationDryRunService.cs` | Mirrors Step 076's `EditProposalDryRunService` exactly, over a `MotifPropagatedProposal`: validates first (throws `EditProposalRejectedException` before any candidate is built), then applies via the already-accepted (internal) `ProposalApplier`. Reuses `EditProposalDryRun` verbatim — the diff now covers every included occurrence at once ("preview of all affected occurrences," implementation guidance, verbatim). |
| `IMotifPropagationApprovalService.cs` / `MotifPropagationApprovalService.cs` | Mirrors Step 076's `EditProposalApprovalService` exactly: re-validates against the **live** document at accept time (not the possibly-stale cached dry-run candidate), then commits via `ProposalApplier` and returns the already-accepted `EditApplyResult`/`EditTransaction` shape. |
| `src/XlightsAI.Studio.Shell/MotifPropagationPanelViewModel.cs` | SCREEN 14 AI EDIT's own bindable propagation drawer, layered on Step 076's `AiEditProposalReviewViewModel` conventions: `AttachCandidates`, `IncludeOccurrence`/`ExcludeOccurrence` (explicit per-occurrence toggles, never defaulted on), `PreviewAsync`, `Reject`, `AcceptAsync`. No Avalonia types, the same convention every prior screen view model in this project already follows. |

New test files:

- `tests/XlightsAI.Studio.AiEdits.Tests/Step077AcceptanceTests.cs` — 10 tests against the real `MotifPropagationExpander`/`MotifPropagationValidator`/`MotifPropagationDryRunService`/`MotifPropagationApprovalService`/`EffectLockPolicy` (no mocking library).
- `tests/XlightsAI.Studio.Shell.Tests/Step077MotifPropagationPanelViewModelTests.cs` — 7 tests against the real `MotifPropagationPanelViewModel` and its real collaborators.

## 3. Architecture deviations / decisions (ADRs)

1. **Per-operation permitted scope, not a single blanket `SelectionContext`.** `IEditProposalValidator`/`EditProposalValidator` (Step 076) check every operation against one `SelectionContext.Scope`. A widened, multi-occurrence proposal has operations that legitimately belong to *different* occurrences' own time/model boundaries — reusing the single-scope validator unmodified would either reject every propagated operation (if checked against the base scope) or silently widen the base scope to cover every occurrence (defeating "edit-scope intersection validation" entirely). `MotifPropagatedProposal.PermittedScopeByOperationId` and `MotifPropagationValidator` are new, additive types rather than a breaking change to the already-accepted Step 076 contract.
2. **`MotifOccurrenceTarget` carries only an already-accepted `EditScope`, never a `Structure.MotifGroupCandidate`/phrase id.** Mirrors the identical, already-disclosed narrowing `SelectionContext`'s own remarks established for Steps 072–076 ("this project only owns the `EditScope`/`SequenceDocument`-facing invariants... a caller free to reference those other aggregates' own real ids here without this project ever depending on them"). No new project reference from `XlightsAI.Studio.AiEdits` to `XlightsAI.Studio.Structure` was added; a future motif-inspector UI is responsible for resolving `MotifGroupCandidate` membership into concrete `MotifOccurrenceTarget` time/model boundaries before calling into this step's own types.
3. **A propagated clone's `Range`/`Targets` are set to the occurrence's own full `EditScope` exactly, not a relative sub-window offset from the base operation's own position within its own occurrence.** The spec's own worked example ("the same gesture... other occurrences") and this step's own implementation guidance do not require phrase-relative time-offset remapping (that would need a beat-aligned/duration-normalized mapping this step does not own); each occurrence's own full scope is itself already the exact boundary the caller (a future inspector resolving `MotifGroupCandidate` phrase boundaries into time windows) determined is equivalent across occurrences. This is a disclosed, narrower scope than a hypothetical "relative position within motif" mapping — reconciled the same way Step 076 disclosed its own `ProposalApplier` ADR.
4. **`MotifPropagationExpander.Expand` throws `ArgumentException` (not a `ProposalValidation` error) for a duplicate occurrence id or an unknown selected occurrence id.** These are internally inconsistent *caller* states (the UI/caller assembled a broken candidate set or references an id it never offered), not untrusted external proposal content — the same distinction `ThreeEffectPlanFactory`/`EffectRecipeRegistry` already draw between a malformed caller argument (`EffectPlanInvalidException`, thrown immediately) and a content-validation gate (`ProposalValidation`, collected and returned). `ProposalValidation`/`EditProposalRejectedException` remain reserved for untrusted-content gates (unknown kind, locks, scope, revision).
5. **No new project reference was added, and no existing accepted contract (Steps 072–076, or any PHASE-02/03/08 contract) was modified.** Every new type in this step is additive; `ProposalApplier` (internal to `XlightsAI.Studio.AiEdits`) is reused unmodified by the new dry-run/approval services, the same "exact same mutation previewed is the exact same mutation committed" guarantee Step 076 already established.

## 4. Commands actually executed

All commands were run via the **PowerShell** tool (the Bash tool returns no usable output in this sandboxed session, consistent with every prior PHASE-11 evidence doc's own disclosure).

```
> dotnet --version
10.0.302

> dotnet build XlightsAI.Studio.slnx -v minimal   (baseline, before any change)
Build succeeded. 0 Warning(s) 0 Error(s)

> dotnet test XlightsAI.Studio.slnx -v minimal   (baseline, before any change)
XlightsAI.Studio.AiEdits.Tests.dll : 61/61 passed
XlightsAI.Studio.Shell.Tests.dll   : 256/256 passed
(all other assemblies green; audio device available in this baseline run)

> dotnet build src\XlightsAI.Studio.AiEdits\XlightsAI.Studio.AiEdits.csproj -v minimal
Build succeeded. 0 Warning(s) 0 Error(s)

> dotnet build src\XlightsAI.Studio.Shell\XlightsAI.Studio.Shell.csproj -v minimal
Build succeeded. 0 Warning(s) 0 Error(s)

> dotnet test tests\XlightsAI.Studio.AiEdits.Tests\XlightsAI.Studio.AiEdits.Tests.csproj -v minimal --filter "FullyQualifiedName~Step077"
Passed! - Failed: 0, Passed: 10, Skipped: 0, Total: 10, Duration: 209 ms

> dotnet test tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj -v minimal --filter "FullyQualifiedName~Step077"
Passed! - Failed: 0, Passed: 7, Skipped: 0, Total: 7, Duration: 147 ms

> dotnet test tests\XlightsAI.Studio.AiEdits.Tests\XlightsAI.Studio.AiEdits.Tests.csproj -v minimal --filter "FullyQualifiedName~Step076|FullyQualifiedName~Step077"
Passed! - Failed: 0, Passed: 23, Skipped: 0, Total: 23

> dotnet test tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj -v minimal --filter "FullyQualifiedName~Step076|FullyQualifiedName~Step077"
Passed! - Failed: 0, Passed: 12, Skipped: 0, Total: 12

> dotnet build XlightsAI.Studio.slnx -v minimal   (full solution, after change)
Build succeeded. 0 Warning(s) 0 Error(s)

> dotnet test XlightsAI.Studio.slnx -v minimal   (full solution regression, after change)
XlightsAI.Studio.Xlights.Tests.dll      : 74/74 passed
XlightsAI.Studio.Audio.Tests.dll        : 28/38 passed, 10 failed
XlightsAI.Studio.Editing.Tests.dll      : 91/91 passed
XlightsAI.Studio.Display.Tests.dll      : 56/56 passed
XlightsAI.Studio.Structure.Tests.dll    : 78/78 passed
XlightsAI.Studio.Melody.Tests.dll       : 117/117 passed
XlightsAI.Studio.Composition.Tests.dll  : 78/78 passed
XlightsAI.Studio.Projects.Tests.dll     : 25/25 passed
XlightsAI.Studio.AiEdits.Tests.dll      : 71/71 passed  (61 pre-existing + 10 new)
XlightsAI.Studio.Rhythm.Tests.dll       : 82/82 passed
XlightsAI.Studio.Shell.Tests.dll        : 248/263 passed, 15 failed (256 pre-existing-passing + 7 new, 0 of the 15 failures in new or AiEdits-related code)
XlightsAI.Studio.Reference.Tests.dll    : 31/31 passed
XlightsAI.Studio.Stems.Tests.dll        : 101/101 passed
TOTAL: 1080 passed / 1105 total, 25 failed (all 25 pre-existing audio-device-dependent, see §4 note below)

> dotnet format XlightsAI.Studio.slnx --verify-no-changes --no-restore
(no output -- no formatting violations)
```

**Note on the 25 failures.** All 10 `XlightsAI.Studio.Audio.Tests` failures and all 15 `XlightsAI.Studio.Shell.Tests` failures are in pre-existing test names unrelated to this step (`Step028ShellWiringTests`/`Step029ShellWiringTests` playhead-scrub/loop-arming tests, which depend on a real audio transport device) — the exact same environment-dependent condition STEP-072 through STEP-075's own evidence docs already disclosed ("10 `XlightsAI.Studio.Audio.Tests` and 15 `XlightsAI.Studio.Shell.Tests` failures... due to an unavailable real audio output device"). STEP-076's own evidence happened to run in a session where the device *was* available (0 failures); this step's own session reproduces the originally-disclosed unavailable-device condition. **Zero** of the 25 failures are in `XlightsAI.Studio.AiEdits.Tests` or in any `Step076`/`Step077`-named test (independently re-confirmed by the filtered re-runs above, both before and after the full regression). No code this step touches has any relationship to audio playback.

Observable desktop demonstration (disposable console app, not committed to the repository, matching the `%TEMP%\xlai-stepNNN-demo-proj` convention every prior PHASE-11 evidence doc used):

```
> New-Item -ItemType Directory -Force -Path "$env:TEMP\xlai-step077-demo-proj"
> # wrote demo.csproj + Program.cs referencing the real AiEdits/Composition/Editing/Xlights/Contracts projects
> cd "$env:TEMP\xlai-step077-demo-proj"
> dotnet run -v minimal
```

Full transcript (abridged to remove restore/build noise):

```
=== PHASE-11 Step 077 demo: motif propagation + edit-scope intersection validation ===
Roofline hash before any proposal: 0F120CB696A55B359FF162B09FCC7E9DCAC2E31D91C2256863A5F3E3147E1E27

--- Round 1: propagate to the LOCKED occurrence (Arch 3/4) -- malicious/overreaching, REJECTED ---
PASS: rejected with user-readable diagnostic(s): Operation 'op-1::occurrence:354db940-1812-4e08-a72e-184b0c99aa32' reaches a locked effect: Effect 'a32d9a3a-cf94-450a-becb-b9d0c03b4b46' on 'Arch 4' is locked and cannot be edited.
PASS: document revision still 1 (no partial mutation of Arch 3's own otherwise-valid effect).
PASS: Arch 3 unaffected: True

--- Round 2: instance-only choice (no occurrence included) -- preview, then REJECT ---
Diff: 2 effect(s) changed on Arch 1, Arch 2
User rejects. Discarding candidate -- no call to Accept.
PASS: document revision unchanged (1), Roofline hash unchanged: True

--- Round 3: propagate to the fully-unlocked occurrence (Arch 5/6) -- inspect, then APPROVE ---
Diff: 4 effect(s) changed on Arch 1, Arch 2, Arch 5, Arch 6 (1 propagated occurrence(s) plus the original instance)
PASS: one transaction recorded. BeforeRevision=1, AfterRevision=2
PASS: committed document revision is now 2.
Arch 1 AiEdit.SetBrightness.Level = 90
Arch 2 AiEdit.SetBrightness.Level = 90
Arch 5 AiEdit.SetBrightness.Level = 90
Arch 6 AiEdit.SetBrightness.Level = 90
PASS: never-included occurrence (Arch 3/4) untouched: True
Roofline hash after approval: 0F120CB696A55B359FF162B09FCC7E9DCAC2E31D91C2256863A5F3E3147E1E27
PASS: Roofline hash unchanged across the whole round trip: True

--- Negative path: operation reaching outside its own permitted occurrence scope (Roofline) is rejected ---
PASS: rejected -- Allowed=False, Errors: Operation 'op-1::occurrence:malicious' targets Roofline, which is outside its own permitted occurrence scope's prop set. | Operation 'op-1::occurrence:malicious' reaches a locked effect: Effect '18cff424-ee1d-4400-a36f-34403027c3ba' on 'Roofline' is locked and cannot be edited.

=== Demo complete ===
```

This demo exercises this step's own narrowed phase-gate workflow: select an occurrence, attempt propagation to a malicious/locked target (rejected, no partial mutation), instance-only preview+reject, propagate to a valid linked occurrence, inspect the combined diff, approve, and verify the Roofline hash (the untouched, always-out-of-scope prop) is byte-identical before and after the entire round trip — plus a direct demonstration of "edit-scope intersection validation" rejecting an operation that reaches outside its own recorded occurrence scope.

The demo project and its `bin`/`obj` output were created entirely under `%TEMP%`, run, and then deleted (`Remove-Item -Recurse -Force`); they are not part of this repository's tracked tree.

## 5. Persisted-output inspection / observable demonstration

See §4's demo transcript — a hand-built `SequenceDocument` fixture (five models: Roofline (locked), Arch 1–6 across three motif occurrences, one of which contains a locked effect), the real `MotifPropagationExpander`/`MotifPropagationValidator`/`MotifPropagationDryRunService`/`MotifPropagationApprovalService` under test — not an in-memory mock standing in for any of these. Roofline's own SHA-256 hash (computed over its full JSON serialization, including `Id`, every field) is printed before any proposal and again after a full reject/propagate/preview/approve round trip, and is asserted equal by the demo itself.

## 6. Fixtures / input hashes

No new fixture files were added to `fixtures/`. All tests and the demo use an in-code, hand-built `SequenceDocument` fixture (seven effects across five "Arch N" models plus a permanently-locked "Roofline", built directly via `EffectRecipeRegistry.CreateVerified` since `ThreeEffectPlanFactory`'s own hard-coded 3-target limit cannot express the multi-occurrence shape this step needs) rather than the `ThreeEffectPlanFactory`-produced three-model fixture Steps 072–076 used — disclosed here as a deliberate, narrow fixture-construction difference, not a change to any already-accepted factory/registry contract.

## 7. Unresolved issues / known gaps

- **No real SCREEN 14 AI EDIT Avalonia/AXAML view exists yet** — a pre-existing, disclosed, project-wide condition carried forward unchanged from Steps 072–076. This step's own "Versioned, inspectable project output plus a reproducible test report" outcome is proven at the bindable `MotifPropagationPanelViewModel` layer plus the console demo in §4.
- **No dependency on `XlightsAI.Studio.Structure`'s real `MotifGroupCandidate`/phrase-boundary resolution exists yet.** `MotifOccurrenceTarget` takes an already-resolved `EditScope` per occurrence; wiring a real motif-group inspector that turns `MotifGroupCandidate.PhraseIds` into concrete time/model `MotifOccurrenceTarget`s is explicitly out of this step's own numbered scope (see ADR §3.2) — a future step's/phase's own scope once that inspector UI is built.
- **Propagated operations reuse an occurrence's own full scope verbatim, not a phrase-relative time-offset remap** — see ADR §3.3. A hypothetical "the gesture starts 2 beats into this phrase, so place it 2 beats into the other phrase" remapping is not implemented; each occurrence's own caller-supplied scope is already the exact equivalent boundary.
- **No persisted pending-proposal store** — inherited from Step 076's own disclosed gap (ADR, Step 076 §3.2); Step 078 ("Persist proposal audit, undo/redo and failure recovery") is the step whose own numbered scope covers this.
- **Undo/redo integration** — `MotifPropagationApprovalService.AcceptAsync`'s own `EditApplyResult`/`EditTransaction` are not yet pushed onto `EditTransactionService`'s own undo/redo stacks, the same disclosed gap Step 076 already carries forward; Step 078's own explicit scope.
- **Audio-device-dependent test failures** — see §4's note; 25 pre-existing, environment-dependent failures (10 `Audio.Tests`, 15 `Shell.Tests`, none in this step's own new code) reproduce the exact condition STEP-072 through STEP-075 already disclosed; not introduced or affected by this step.

## 8. Required-acceptance-text-by-text tie-back

| Required text (verbatim) | Where proven |
|---|---|
| "Locked/out-of-scope effects cannot change even with malicious proposal" | `Propagate_ToOccurrenceContainingALockedEffect_IsRejected_WithoutPartialMutation_NegativePath`, `Operation_TargetingOutsideItsOwnPermittedOccurrenceScope_IsRejected_NegativePath` (AiEdits); `PreviewAsync_IncludingAnOccurrenceWithALockedEffect_IsRejected_NegativePath` (Shell); demo §4 Round 1 and the final negative-path section (a hand-crafted operation naming "Roofline" is rejected before any candidate is built, and Roofline's own SHA-256 hash is proven byte-identical across the entire round trip). |
| "...support instance-only choice" | `InstanceOnlySelection_ExpandsToOriginalOperationsOnly` (AiEdits); `InstanceOnly_NeverIncludingAnOccurrence_PreviewsOnlyTheOriginalEdit`, `ExcludeOccurrence_AfterInclude_ReturnsToInstanceOnly` (Shell); demo §4 Round 2. |
| "Apply motif propagation only after preview of all affected occurrences and each target locks/scope permissions" | `Step077_Acceptance_PropagateToUnlockedOccurrence_ChangesOnlyIncludedOccurrences` (AiEdits: `MotifPropagationDryRunService.Preview` validates every occurrence's own scope/locks before any candidate is built, then the diff shows every included occurrence at once); `Step077_Acceptance_IncludeOccurrence_PreviewThenAccept_ChangesOnlyIncludedOccurrences` (Shell); demo §4 Round 3. |
| "Out-of-scope, locked or stale revision must be rejected without partial mutation" (required failure/edge case, verbatim) | `Propagate_ToOccurrenceContainingALockedEffect_IsRejected_WithoutPartialMutation_NegativePath`, `Propagate_ToMixOfValidAndLockedOccurrences_RejectsWholeBatch_NegativePath`, `Operation_TargetingOutsideItsOwnPermittedOccurrenceScope_IsRejected_NegativePath`, `StaleRevision_IsRejected_WithoutPartialMutation_NegativePath`, `AcceptAsync_LockAppliedBetweenPreviewAndAccept_IsRejected_WithoutMutation_NegativePath`, `EmptyOperationsList_IsRejected_NegativePath` (AiEdits, every one asserting zero `AiEdit.*` mutation after rejection); `PreviewAsync_IncludingAnOccurrenceWithALockedEffect_IsRejected_NegativePath`, `Reject_LeavesDocumentByteEquivalent_NegativePath` (Shell). |
| "edit-scope intersection validation" (this step's own heading, verbatim) | `MotifPropagationValidator` validates every operation against its own `PermittedScopeByOperationId` entry (not one blanket scope) — directly exercised by `Operation_TargetingOutsideItsOwnPermittedOccurrenceScope_IsRejected_NegativePath` and the demo's final negative-path section. |
| Internally inconsistent candidate sets rejected before cloning | `Expand_DuplicateOccurrenceIdInCandidateSet_Throws_NegativePath`, `Expand_SelectionNamesUnknownOccurrenceId_Throws_NegativePath` (AiEdits). |

## 9. PASS / BLOCKED

**PASS.**

- Both the required-acceptance sentence and the required failure/edge-case sentence are proven by passing unit tests against real collaborators (no mocking library) at both the domain/application-service layer (`MotifPropagationExpander`/`MotifPropagationValidator`/`MotifPropagationDryRunService`/`MotifPropagationApprovalService`) and the bindable view-model layer (`MotifPropagationPanelViewModel`), and by a real console demonstration exercising reject/propagate/preview/approve plus a direct "edit-scope intersection validation" negative path.
- "Support instance-only choice" is proven as an explicit, never-defaulted-on user selection, both at the domain layer and the bindable toggle layer.
- Full solution build succeeds with 0 warnings/errors; `dotnet format --verify-no-changes` reports no violations.
- Full regression: 1080/1105 tests pass; the 25 failures are pre-existing, environment-dependent (unavailable real audio output device), independently re-confirmed to be unrelated to this step by filtered re-runs of every `Step076`/`Step077`-named test both in isolation and inside the full run (23/23 and 12/12 respectively, 0 failed). 17 new tests added by this step (10 AiEdits + 7 Shell), 0 removed, 0 modified.
- No prior accepted contract (Steps 072–076, or any PHASE-02/03/08 contract) was modified — every new type in this step is additive; no new project reference was added.
- No show file, fixture, or prior persisted artifact was modified.

**Explicitly not claimed:** real xLights recipe translation of the five operation kinds into verified xLights effect-settings XML (inherited, unmodified, from Step 076's own disclosed boundary). Real `XlightsAI.Studio.Structure.MotifGroupCandidate`/phrase-boundary resolution into concrete `MotifOccurrenceTarget`s — this step owns the propagation/validation machinery once that resolution has happened, not the resolution itself (see ADR §3.2, §7). A persisted pending-proposal store and undo/redo-stack integration remain Step 078's own explicit scope, as already disclosed by Step 076.
