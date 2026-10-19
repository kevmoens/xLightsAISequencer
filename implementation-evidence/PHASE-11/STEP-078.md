# PHASE-11 Step 078 — Persist proposal audit, undo/redo and failure recovery

**Date:** 2026-10-17
**Spec:** `docs/phase-specs/PHASE-11.md` §6, Step 078
**Required acceptance (verbatim):** "Crash/restart leaves committed edits intact, partial proposal uncommitted."
**Required failure/edge case (verbatim):** "Out-of-scope, locked or stale revision must be rejected without partial mutation."

## 1. Pre-implementation baseline inspection

Before writing any code, the existing repository state was read directly (not taken on faith):

- Ran the full baseline regression (`dotnet test XlightsAI.Studio.slnx`) before any change: all 13 test assemblies green, 1105 passed (matches STEP-076's own disclosed "audio device available" session; this session also had the real audio device, so `Audio.Tests`/`Shell.Tests` were both fully green at baseline — reconfirmed after the change, see §4).
- **Steps 072–077** (`src/XlightsAI.Studio.AiEdits/`) already implement the full proposal pipeline: `SelectionContext`, `EditProposal`/`StructuredEditOperation`, `IEditProposalValidator`/`EditProposalValidator`, `IEditProposalDryRunService`/`EditProposalDryRunService`, `IEditApprovalService`/`EditProposalApprovalService`, and the Step 077 motif-propagation counterparts `IMotifPropagationValidator`/`MotifPropagationValidator`, `IMotifPropagationDryRunService`/`MotifPropagationDryRunService`, `IMotifPropagationApprovalService`/`MotifPropagationApprovalService`. Both approval services already compute and return an `EditApplyResult(SequenceDocument, EditTransaction)` on success — this step owns making that outcome (and every rejected outcome) durable, not re-deriving it.
- **Step 076's own evidence doc** (`implementation-evidence/PHASE-11/STEP-076.md` §7) explicitly disclosed, verbatim: "No persisted pending-proposal store keyed by `Guid proposalId` exists," "Undo/redo integration: `EditApplyResult`/`EditTransaction` returned by `EditProposalApprovalService.AcceptAsync` are not yet pushed onto `EditTransactionService`'s own undo/redo stacks... Step 078's own explicit scope." Step 077's own evidence doc (§7) carried the identical two gaps forward unchanged. This step closes exactly those two named gaps, plus the lifecycle-audit requirement this step's own heading and implementation guidance add on top.
- **`src/XlightsAI.Studio.Editing/`** (PHASE-03) already implements the real undo/redo machinery: `IEditTransactionService`/`EditTransactionService` (Step 022) with per-document `_undoStacks`/`_redoStacks`, an optional `IEditTransactionHistoryStore` collaborator for cross-restart persistence, and a concrete `JsonEditTransactionHistoryStore` (atomic temp-file+move JSON writer, one file per document id). Read `EditTransactionService.cs` in full: `UndoAsync`/`RedoAsync` resolve "projectId" as the target `SequenceDocument.Id` (the interface's own disclosed, already-accepted deviation), and lazily rehydrate a document's stacks from `_historyStore` the first time that service instance touches that document id — exactly the "survives restart" behavior this step's own required acceptance needs, already built and already accepted. **Critical finding:** `EditTransactionService`'s own `RecordHistoryAsync`/`PersistAsync` are private — there was no existing way for `XlightsAI.Studio.AiEdits`'s own approval services (which never route through `IEditTransactionService.ApplyAsync`, a disclosed Step 076 ADR — see STEP-076.md §3.1) to push an externally-produced `EditTransaction` onto the *same* persisted undo/redo stack `EditTransactionService` itself reads from. This is reconciled in §3 below with a new, additive, public helper rather than by modifying `EditTransactionService` itself.
- **`src/XlightsAI.Studio.Xlights/JsonSequenceDocumentStore.cs`** (PHASE-02) already exists and persists a whole `SequenceDocument` atomically, one file per document id — reused verbatim in this step's own tests/demo to make the "restart" scenario fully realistic (a real persisted document on disk, not an in-memory stand-in).
- `fixtures/` has no PHASE-11-specific fixture files; this step's own tests reuse the same `ThreeEffectPlanFactory`/`EffectRecipeRegistry` gate-demo fixture (Roofline, Arch 1, Arch 2) Steps 072–076 already established, plus a small hand-built four-model fixture (mirroring Step 077's own pattern) for the motif-propagated audit test.

## 2. Scope actually implemented

New files, all in `src/XlightsAI.Studio.AiEdits/` unless noted:

| File | Purpose |
|---|---|
| `EditProposalAuditStatus.cs` | The three lifecycle states this step's own implementation guidance names: `Pending` (validated, previewed, not yet accepted), `Rejected` (validation failed, at preview or at accept time), `Committed` (accept succeeded). A record moves forward only, never backward. |
| `EditProposalAuditRecord.cs` | One persisted lifecycle entry per proposal id: `ProposalId`, `SelectionContextId` (nullable — `null` for a motif-propagated proposal, which is not captured against a single `SelectionContext`), `BaseRevision`, `Status`, `ValidationErrors`, `TransactionId`, `CommittedRevision`, `RecordedAtUtc`. |
| `IEditProposalAuditStore.cs` / `JsonEditProposalAuditStore.cs` | Persistence boundary: synchronous `Save(record)` (atomic temp-file+move, identical pattern to `JsonSelectionContextStore`/`JsonEditTransactionHistoryStore`) and `Load(proposalId)` (returns `null` for an unknown id, never throws). Deliberately synchronous — see ADR §3.1. |
| `EditProposalAuditCorruptException.cs` | Mirrors `SelectionContextCorruptException`'s own convention for a corrupt audit file. |
| `EditProposalDryRunService.cs` (modified, additively) | New optional constructor `(IEditProposalValidator, IEditProposalAuditStore?)`. `Preview` now writes `Rejected` (with the real validation errors) before throwing on failure, or `Pending` before returning a valid dry run. |
| `EditProposalApprovalService.cs` (modified, additively) | New optional constructor `(IEditProposalValidator, IEditProposalAuditStore?, IEditTransactionHistoryStore?)`. `AcceptAsync` now writes `Rejected` (updating the proposal's own `Pending` record) on a failed re-validation, or appends the committed transaction onto the shared `IEditTransactionHistoryStore` and writes `Committed` (with the real `TransactionId`/`CommittedRevision`) on success. |
| `MotifPropagationDryRunService.cs` / `MotifPropagationApprovalService.cs` (modified, additively) | Identical optional-collaborator pattern, keyed by `MotifPropagatedProposal.Proposal.Id`, with `SelectionContextId` always `null` (see `EditProposalAuditRecord`'s own remarks). |

New file in `src/XlightsAI.Studio.Editing/`:

| File | Purpose |
|---|---|
| `EditTransactionHistoryRecorder.cs` | Public static `AppendAsync(IEditTransactionHistoryStore, before, after, transaction, ct)`: loads the store's current snapshot for `before.Id` (or starts empty), appends one new undo entry, clears the redo stack (the same "a new edit invalidates pending redo history" rule `EditTransactionService`'s own private `RecordHistoryAsync` already enforces), and saves atomically. This is the bridge that lets an AI-approved edit land in the exact same persisted file a `EditTransactionService` instance reads from — see ADR §3.2. |

New test file:

- `tests/XlightsAI.Studio.AiEdits.Tests/Step078AcceptanceTests.cs` — 8 tests against the real `JsonEditProposalAuditStore`, `JsonEditTransactionHistoryStore`, `JsonSequenceDocumentStore`, `EditProposalValidator`/`EditProposalDryRunService`/`EditProposalApprovalService`, `MotifPropagationValidator`/`MotifPropagationDryRunService`/`MotifPropagationApprovalService`, and `EditTransactionService` (no mocking library) — every "restart" is modeled by constructing brand-new service instances pointed at the same real, temp-directory-backed on-disk files, never by reusing an existing in-memory object graph.

## 3. Architecture deviations / decisions (ADRs)

1. **`IEditProposalAuditStore` is synchronous (`void Save`/`EditProposalAuditRecord? Load`), not `Task`-returning.** The only two call sites that write (`EditProposalDryRunService.Preview`, `MotifPropagationDryRunService.Preview`) are themselves synchronous — an already-accepted Step 076/077 signature this step must not change ("treat all preceding completed step contracts as immutable"). Making the store asynchronous would force those methods to block on a task for no benefit; a local JSON file write is fast enough to do inline. The accept-path call sites (`EditProposalApprovalService.AcceptAsync`/`MotifPropagationApprovalService.AcceptAsync`) are already `async` and simply call the synchronous `Save` inline, which is the normal, correct way to do a fast local file write from an async method.
2. **A new public `EditTransactionHistoryRecorder.AppendAsync` helper in `XlightsAI.Studio.Editing`, rather than modifying `EditTransactionService` or exposing its own private `RecordHistoryAsync`.** As found in §1, `EditTransactionService`'s own undo/redo recording logic is private, and `XlightsAI.Studio.AiEdits`'s approval services do not — and, per the already-accepted Step 076 ADR (STEP-076.md §3.1), cannot yet — route an AI-approved edit through `IEditTransactionService.ApplyAsync`. Rather than fabricating a parallel, independently-written "append to the history store" code path that could silently diverge from `EditTransactionService`'s own semantics (most importantly, "a new edit clears pending redo history"), this step adds one small, additive, public, pure function in the `Editing` project itself — the project that already owns `IEditTransactionHistoryStore`/`EditTransactionHistorySnapshot`/`EditTransactionHistoryEntry` — that any external committer of a transaction can call to land correctly-shaped data in the exact same persisted file `EditTransactionService` itself reads from. `EditTransactionService.cs` itself is **not modified** — no already-accepted contract changed.
3. **Same-process live synchronization between an already-constructed `EditTransactionService` instance's own in-memory stacks and a concurrently-running AI approval service is not attempted.** `EditTransactionHistoryRecorder.AppendAsync` writes directly to the persisted store; an `EditTransactionService` instance that has never touched that document id in memory yet will transparently pick up the AI-approved entry the first time it hydrates (its own already-accepted `GetHydratedStackAsync` lazy-load behavior) — exactly the realistic "a new `EditTransactionService` is constructed after the AI edit was committed" scenario this step's own required acceptance names ("crash/restart"). A long-lived `EditTransactionService` instance that already cached that document's stacks in memory *before* the AI edit landed would not see the new entry until it is reconstructed — disclosed here, not silently assumed away; building live cross-instance invalidation is out of this step's own numbered scope and is not implied by anything in its own required acceptance text.
4. **Audit writes happen *after* the `IEditTransactionHistoryStore` append on the commit path, not before, and not as a single atomic cross-store transaction.** `EditProposalApprovalService.AcceptAsync`/`MotifPropagationApprovalService.AcceptAsync` append to `_historyStore` first, then write the `Committed` audit record. A process crash in the narrow window between these two writes is possible and is **disclosed as a known, accepted limitation**: the history store would then hold an undoable entry for a transaction whose own audit record still reads `Pending`. This is a best-effort ordering appropriate to a local desktop app (no two-phase commit across two independent JSON files is implemented, nor warranted at this step's own scope), not a silently-assumed atomicity guarantee. It does not affect the required acceptance text itself: "committed edits intact" is proven by the undo stack (written first, so it is present at least as early as the audit record), and "partial proposal uncommitted" concerns a proposal that was *never* accepted at all (its own record stays `Pending` because `AcceptAsync` was never called), which this ordering does not touch.
5. **`EditProposalAuditRecord.SelectionContextId` is nullable, `null` for every motif-propagated proposal.** A `MotifPropagatedProposal` is, by Step 077's own already-accepted design (`MotifPropagatedProposal.PermittedScopeByOperationId`'s own remarks), not captured against one single `SelectionContext` — inventing a synthetic non-null id for this field would misrepresent the record. Disclosed directly in the field's own doc comment rather than silently defaulted to `Guid.Empty`.
6. **No existing accepted contract (Steps 072–077, or any PHASE-02/03 contract) was modified.** Every new type is additive. `EditProposalDryRunService`, `EditProposalApprovalService`, `MotifPropagationDryRunService`, and `MotifPropagationApprovalService` each keep their original constructor unchanged (delegating to the new one with the new collaborator(s) defaulted to `null`) — every existing call site (including every Step 072–077 test) keeps compiling and passing unmodified; reconfirmed in §4.

## 4. Commands actually executed

All commands were run via the **PowerShell** tool. (As in every prior PHASE-11 evidence doc, the Bash tool returns no usable output in this sandboxed session — every Bash invocation, including a bare `echo`, silently exits 1 with no output; this is disclosed, not worked around by claiming Bash output that never appeared.)

```
> dotnet --version
10.0.302

> dotnet build src\XlightsAI.Studio.Editing\XlightsAI.Studio.Editing.csproj -v minimal
Build succeeded. 0 Warning(s) 0 Error(s)

> dotnet build src\XlightsAI.Studio.AiEdits\XlightsAI.Studio.AiEdits.csproj -v minimal
Build succeeded. 0 Warning(s) 0 Error(s)

> dotnet test tests\XlightsAI.Studio.AiEdits.Tests\XlightsAI.Studio.AiEdits.Tests.csproj -v minimal  (before writing the new test file -- regression check on wiring changes alone)
Passed! - Failed: 0, Passed: 71, Skipped: 0, Total: 71, Duration: 1 s

> dotnet test tests\XlightsAI.Studio.Editing.Tests\XlightsAI.Studio.Editing.Tests.csproj -v minimal
Passed! - Failed: 0, Passed: 91, Skipped: 0, Total: 91, Duration: 231 ms

> dotnet test tests\XlightsAI.Studio.AiEdits.Tests\XlightsAI.Studio.AiEdits.Tests.csproj -v minimal --filter "FullyQualifiedName~Step078"
Passed! - Failed: 0, Passed: 8, Skipped: 0, Total: 8, Duration: 413 ms

> dotnet build XlightsAI.Studio.slnx -v minimal   (full solution, after change)
Build succeeded. 0 Warning(s) 0 Error(s)

> dotnet test XlightsAI.Studio.slnx -v minimal   (full solution regression, after change)
XlightsAI.Studio.Xlights.Tests.dll      : 74/74 passed
XlightsAI.Studio.Editing.Tests.dll      : 91/91 passed
XlightsAI.Studio.Display.Tests.dll      : 56/56 passed
XlightsAI.Studio.Structure.Tests.dll    : 78/78 passed
XlightsAI.Studio.Melody.Tests.dll       : 117/117 passed
XlightsAI.Studio.Audio.Tests.dll        : 38/38 passed
XlightsAI.Studio.Composition.Tests.dll  : 78/78 passed
XlightsAI.Studio.AiEdits.Tests.dll      : 79/79 passed  (71 pre-existing + 8 new)
XlightsAI.Studio.Rhythm.Tests.dll       : 82/82 passed
XlightsAI.Studio.Projects.Tests.dll     : 25/25 passed
XlightsAI.Studio.Shell.Tests.dll        : 263/263 passed
XlightsAI.Studio.Reference.Tests.dll    : 31/31 passed
XlightsAI.Studio.Stems.Tests.dll        : 101/101 passed
TOTAL: 1113 passed / 1113 total, 0 failed

> dotnet format XlightsAI.Studio.slnx --verify-no-changes --no-restore
(no output -- no formatting violations)
```

**Note on the Audio/Shell test counts.** This session's real audio output device was available (the same environment-dependent condition STEP-076's own evidence disclosed in its own session) — `Audio.Tests` 38/38 and `Shell.Tests` 263/263, 0 failures, matching STEP-076's own full-green session rather than STEP-072–075/077's own disclosed 25-failure sessions. This is an environment difference between sessions, not a change introduced by this step; no code this step touches has any relationship to audio playback.

Observable desktop demonstration (disposable console app, not committed to the repository, matching the `%TEMP%\xlai-stepNNN-demo-proj` convention every prior PHASE-11 evidence doc used):

```
> New-Item -ItemType Directory -Force -Path "$env:TEMP\xlai-step078-demo-proj"
> # wrote demo.csproj + Program.cs referencing the real AiEdits/Composition/Editing/Xlights/Contracts projects
> cd "$env:TEMP\xlai-step078-demo-proj"
> dotnet run -v minimal
```

Full transcript (abridged to remove restore/build noise):

```
=== PHASE-11 Step 078 demo: Persist proposal audit, undo/redo and failure recovery ===
Persistence root: C:\Users\kmoens.LAKECO\AppData\Local\Temp\step078-demo-data-6418e1c9395b4c5083b18bebe76884dd
Roofline hash before any proposal: 966FFEF24A7553533B29938A70ABE49593B2A9FE7EE18A657764F8D212B83B39

--- Session 1: preview + accept proposal 1 (rising piano pops -> brightness 90), persist document ---
Preview 1 diff: 2 effect(s) on Arch 1, Arch 2
Proposal 1 audit status after preview: Pending
PASS: accepted. BeforeRevision=1, AfterRevision=2
Proposal 1 audit status after accept: Committed (TransactionId=23b94fb2-ada6-4d83-820a-80a9eac337dd)
Committed document persisted to: C:\Users\kmoens.LAKECO\AppData\Local\Temp\step078-demo-data-6418e1c9395b4c5083b18bebe76884dd\sequence-plan-c8e227a04bcc4d5699857f7ee58bd055.json

--- Session 1 (continued): preview proposal 2 (reverse) but NEVER accept -- simulated crash before commit ---
Preview 2 diff: 2 effect(s) -- proposal 2 is valid and previewable, but Accept is deliberately never called.
Proposal 2 audit status: Pending (this is the 'crash point' -- process ends here in this simulation)

--- Session 2 ("restart"): brand-new stores/services pointed at the same directory ---
Proposal 1 (committed) audit status after restart: Committed, TransactionId=23b94fb2-ada6-4d83-820a-80a9eac337dd, CommittedRevision=2
Proposal 2 (crashed before accept) audit status after restart: Pending -- PASS: still Pending, never silently promoted to Committed: True
Persisted document revision after restart: 2 (PASS: reflects only proposal 1's commit, not proposal 2's uncommitted reverse: True)
Arch 1 brightness after restart: 90
PASS: undo (after restart, from persisted history alone) restored revision 1, matching the original pre-accept revision 1: True
PASS: undo restored exact original Arch 1 state: True
Roofline hash after the whole crash/restart/undo round trip: 966FFEF24A7553533B29938A70ABE49593B2A9FE7EE18A657764F8D212B83B39
PASS: Roofline hash unchanged across the whole round trip: True

--- Negative path: a rejected proposal's validation failure is itself persisted ---
PASS: rejected with user-readable diagnostic(s): Operation 'op-x' names an unknown kind 'DeleteEverything'.
PASS: rejection was persisted: Status=Rejected, Errors=Operation 'op-x' names an unknown kind 'DeleteEverything'.

=== Demo complete ===
```

This demo exercises this step's own required acceptance end to end: a proposal is previewed and accepted, its committed transaction and resulting document are durably persisted to real files on disk; a second proposal is previewed (and would have been valid) but Accept is deliberately never called, simulating a crash between preview and accept; brand-new store/service instances are then constructed against the same on-disk directory ("restart") and prove that (1) the first proposal's commit is fully intact — its audit record still reads `Committed` with the real transaction id, the persisted document still carries the brightness change, and a freshly-constructed `EditTransactionService` can undo it purely by reading the persisted history file, and (2) the second, crashed proposal's own audit record is still `Pending` — never silently promoted to `Committed`, and no trace of its own "reverse" operation ever reached the persisted document. The negative path then shows a rejected proposal's own validation errors are themselves durably persisted, not merely thrown and discarded. The Roofline effect's own SHA-256 hash is proven byte-identical across the entire crash/restart/undo round trip.

The demo project and its persistence-root/`bin`/`obj` output were created entirely under `%TEMP%`, run, and then deleted (`Remove-Item -Recurse -Force`; the demo's own persistence root was also deleted by the demo program itself at the end of its own run); they are not part of this repository's tracked tree.

## 5. Persisted-output inspection / observable demonstration

See §4's demo transcript — real `JsonEditProposalAuditStore`/`JsonEditTransactionHistoryStore`/`JsonSequenceDocumentStore` files were written to and read back from an actual temp directory on disk across two independently-constructed sets of service instances (not an in-memory mock standing in for the file system in either "session"). The committed proposal's audit JSON, the persisted document JSON, and the persisted undo/redo-history JSON were all real files on disk at `step078-demo-data-6418e1c9395b4c5083b18bebe76884dd\` during the run (inspected via the demo's own `Load`/`LoadAsync` calls against those real paths, not reconstructed from memory).

## 6. Fixtures / input hashes

No new fixture files were added to `fixtures/`. Tests and the demo reuse the same in-code `ThreeEffectPlanFactory`/`EffectRecipeRegistry` gate-demo fixture (Roofline, Arch 1, Arch 2) Steps 072–076 already established, plus one small hand-built four-model fixture (Arch 1/2/5/6, mirroring Step 077's own occurrence-fixture pattern) for the motif-propagated-proposal audit test — consistent with this project's own established no-persisted-fixture-file convention for this step family.

## 7. Unresolved issues / known gaps

- **No real SCREEN 14 AI EDIT Avalonia/AXAML view exists yet** — a pre-existing, disclosed, project-wide condition carried forward unchanged from Steps 072–077. This step's own scope is the persistence/audit/undo-redo layer underneath that screen, not the screen itself; a future screen binding would wire `IEditProposalAuditStore`/`IEditTransactionHistoryStore` through the already-existing `AiEditProposalReviewViewModel`/`MotifPropagationPanelViewModel` constructors, which this step does not modify.
- **Cross-store write ordering is best-effort, not a two-phase commit** — see ADR §3.4. A crash in the narrow window between the history-store append and the audit-record write on the commit path is disclosed as a known limitation; it does not affect either half of this step's own required acceptance text (see ADR §3.4's own reasoning).
- **Same-process live synchronization** between an already-constructed, already-hydrated `EditTransactionService` instance and a concurrently-running AI approval service is not implemented — see ADR §3.3. The "survives restart" scenario this step's own required acceptance names is proven with a freshly-constructed service instance, which is the realistic restart case.
- **No actual UI-level "undo" button wired to `EditTransactionService.UndoAsync` for an AI-approved edit exists yet** — this step proves the underlying persisted mechanism is correct and exercisable (the demo and tests call `UndoAsync` directly), not that a Shell view model exposes it yet; no prior PHASE-11 step's own evidence claimed that wiring existed either.
- **Audio-device-dependent test variability across sessions** — see §4's note; this session's real audio device was available (0 failures, matching STEP-076's own session), unlike STEP-072–075/077's own sessions (25 disclosed, unrelated, environment-dependent failures). Not introduced or affected by this step either way.

## 8. Required-acceptance-text-by-text tie-back

| Required text (verbatim) | Where proven |
|---|---|
| "Crash/restart leaves committed edits intact, partial proposal uncommitted" | `Step078_Acceptance_CommittedProposal_IsAuditedAndUndoableAfterRestart`, `Step078_Acceptance_CrashBetweenPreviewAndAccept_LeavesCommittedEditIntactAndPendingProposalUncommitted` (AiEdits: fresh store/service instances after an accepted-but-never-persisted-in-memory commit prove both the committed proposal's audit record and its undo capability survive, while a second, never-accepted proposal's own record stays `Pending` and never reaches the persisted document); `MotifPropagatedProposal_CommittedAccept_IsAuditedAndUndoableAfterRestart` (same proof for Step 077's propagated path); demo §4, Sessions 1-2 end to end. |
| "Persist proposal lifecycle, validation failures and accepted transactions" (implementation guidance, verbatim) | Every `Step078AcceptanceTests` test asserts the real persisted `EditProposalAuditRecord` at each lifecycle stage (`Pending` after a valid preview, `Rejected` with the real error text after any validation failure at preview or accept time, `Committed` with the real transaction id/revision after a successful accept); `RejectedAtPreview_PersistsRejectionWithErrors_NoPartialMutation_NegativePath`, `OutOfScopeTarget_RooflineOutsideSelectedArches_PersistsRejection_NegativePath` (validation failures specifically); demo §4's negative-path section. |
| "test restart mid-proposal and provider error" (implementation guidance, verbatim) | "Restart mid-proposal" is `Step078_Acceptance_CrashBetweenPreviewAndAccept_LeavesCommittedEditIntactAndPendingProposalUncommitted` and demo §4 Session 2 (a previewed-but-never-accepted proposal is the "mid-proposal" state a crash leaves behind). "Provider error": Step 075's `IOptionalAiPlanner`/`HttpStructuredOutputAiPlanner` already only ever produces an `EditProposal` or throws before one exists (STEP-075's own already-accepted scope) — a planner failure therefore never reaches this step's own audit store at all (no `Pending` record is ever written for a proposal the planner never successfully returned), which is itself the correct "no partial mutation, nothing falsely recorded" behavior; no new test is needed to prove a no-op, and none is claimed beyond this reasoning. |
| "Out-of-scope, locked or stale revision must be rejected without partial mutation" (required failure/edge case, verbatim) | `RejectedAtPreview_PersistsRejectionWithErrors_NoPartialMutation_NegativePath` (unknown kind, preview-time), `OutOfScopeTarget_RooflineOutsideSelectedArches_PersistsRejection_NegativePath` (out-of-scope, preview-time), `LockedBetweenPreviewAndAccept_UpdatesAuditFromPendingToRejected_NoPartialMutation_NegativePath` (locked, discovered at accept time — proves the audit record itself transitions `Pending` -> `Rejected`, not left stale), `StaleRevisionBetweenPreviewAndAccept_UpdatesAuditToRejected_NoPartialMutation_NegativePath` (stale revision, accept time) — every one asserts both the real persisted audit record's status/errors and zero `AiEdit.*` mutation on the document. |
| Section 7 phase gate cross-cutting test #8: "Accept creates one undoable transaction and survives restart" | `Step078_Acceptance_CommittedProposal_IsAuditedAndUndoableAfterRestart`, `MotifPropagatedProposal_CommittedAccept_IsAuditedAndUndoableAfterRestart` — both call `EditTransactionService.UndoAsync` on a **freshly-constructed** service instance and assert the exact pre-accept document state is restored; demo §4 Session 2's own undo call. This closes the gap both STEP-076.md §7 and STEP-077.md §7 explicitly disclosed as deferred to this step. |

## 9. PASS / BLOCKED

**PASS.**

- Both the required-acceptance sentence and the required failure/edge-case sentence are proven by passing unit tests against real collaborators (no mocking library, real on-disk files) and by a real console demonstration exercising a full preview/accept/persist/"crash"/"restart"/audit-check/undo/negative-path round trip.
- "Persist proposal lifecycle, validation failures and accepted transactions" is proven as three distinct, durably-written states (`Pending`/`Rejected`/`Committed`), not only the success path.
- "Accept creates one undoable transaction and survives restart" (PHASE-11 Section 7's own cross-cutting test #8, previously unproven and explicitly disclosed as deferred by both Step 076 and Step 077) is now proven directly: a brand-new `EditTransactionService` instance, constructed only from the persisted history store, successfully undoes an AI-approved edit.
- Full solution build succeeds with 0 warnings/errors; `dotnet format --verify-no-changes` reports no violations.
- Full regression: 1113/1113 tests pass (0 failed) across every test assembly in the solution; 8 new tests added by this step, 0 removed, 0 modified. Every Step 072–077 test (`AiEdits.Tests`, `Shell.Tests`) continues to pass unmodified against its own original constructor call sites, proving the new collaborators are genuinely additive.
- No prior accepted contract (Steps 072–077, or any PHASE-02/03 contract) was modified — every new type is additive, and `EditTransactionService.cs` itself was not touched.
- No show file, fixture, or prior persisted artifact was modified.

**Explicitly not claimed:** a UI-level undo button or SCREEN 14 AI EDIT Avalonia view wired to this mechanism (no prior PHASE-11 step claimed this either). A two-phase, cross-store-atomic commit between the audit store and the transaction-history store (disclosed limitation, ADR §3.4 — does not affect either half of the required acceptance text). Live, same-process synchronization of an already-hydrated `EditTransactionService` instance's in-memory stacks with a concurrently-running AI approval service (ADR §3.3 — the realistic "restart" scenario this step's own required acceptance names is proven with a freshly-constructed instance). Real xLights recipe translation of the AI-edit metadata (inherited, unmodified, from Step 076's own disclosed boundary).
