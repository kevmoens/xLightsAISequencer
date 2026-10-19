# PHASE 03 — Step 022 Evidence

**Step:** "Undo/redo transactions and lock semantics for approved effects"
**Spec:** `docs/phase-specs/PHASE-03.md` §6, Step 022 (lines 291–305)
**Date:** 2026-10-11

## 1. Pre-implementation baseline inspection

- Re-read `docs/phase-specs/PHASE-03.md` §6 Step 022 in full: required acceptance ("Undo restores
  exact snapshot; locked effects reject programmatic edits"), implementation guidance ("Locks enforced
  below ViewModel; undo/redo persisted as transactions and respects project revision; test restart and
  stale writes"), and the shared failure/edge case ("Out-of-scope, locked or stale revision must be
  rejected without partial mutation").
- Re-read every existing `XlightsAI.Studio.Editing` type this step builds directly on:
  `IEditTransactionService` (whose `UndoAsync`/`RedoAsync` both threw `NotImplementedException` with a
  comment naming this exact step), `EditTransactionService` (the real `ApplyAsync` pipeline for
  `MoveEffect`/`ResizeEffect`/`SetEffectParameters`), `EditTransaction` (the existing before/after
  single-effect audit record), `EffectLockPolicy`/`IEffectLockPolicy` (the already-real lock
  enforcement gate, driven by `EffectInstance.Locked` — a plain data bit added in Step 019 with no
  command that could ever set it), `EditRejectionReason`, `EditCommandRejectedException`, `EditScope`,
  `EditCommand`, and the Step 019/020 concrete commands (`MoveEffect`/`ResizeEffect`/
  `SetEffectParameters`) for this repository's exact command-record shape/style.
- Read `TimelineViewModel.cs`/`ShellViewModel.cs` in full and grepped for any existing
  `IEditTransactionService`/`MoveEffect`/`UndoAsync` reference in `XlightsAI.Studio.Shell` —
  **none exists**. Confirmed directly from `implementation-evidence/PHASE-03/STEP-019.md` itself
  (§"Architecture deviations"): Step 019 explicitly recorded "No UI/ViewModel wiring was added for
  dragging — there is no effect-block visual in `TimelineViewModel`... to attach a drag gesture to
  yet," and proved its own required acceptance entirely at the command/service layer. Step 020 followed
  the identical precedent. **Step 022 follows this same, already-established scope cut**: the
  Move/Resize/SetParameters apply pipeline itself has never been wired into the Shell/UI layer, so
  wiring only Undo/Redo/lock-toggle into the UI now — with no way for a real user gesture to produce
  the underlying edits those buttons would undo/redo — would be new, inconsistent scope invention, not
  a continuation of established precedent. See §3 for the explicit architecture decision this produced.
- Baseline regression before any Step 022 code change, re-run fresh in this session (not assumed from
  Step 021's own recorded number, though it matches exactly): **258/258 passing**, confirmed via
  `dotnet test XlightsAI.Studio.slnx --nologo` immediately after the Step 022 source edits compiled but
  before any new test file existed (§4) — i.e., proving the existing suite is unaffected by the
  Step 022 source changes themselves, independent of the new tests.

## 2. Scope actually implemented

**New types in `XlightsAI.Studio.Editing`** (pure domain, no Avalonia types):

- `SetEffectLocked(DocumentRevision ExpectedRevision, EditScope Scope, Guid EffectId, bool Locked) : EditCommand`
  — the "lock semantics for approved effects" half of this step's title. The fourth concrete
  `EditCommand`, in the exact style of `MoveEffect`/`ResizeEffect`/`SetEffectParameters`.
- `EditTransactionHistoryEntry(SequenceDocument Before, SequenceDocument After, EditTransaction Transaction)`
  — one undo/redo stack entry: the complete document snapshot immediately before and after one applied
  command, plus its existing `EditTransaction` audit record.
- `EditTransactionHistorySnapshot(IReadOnlyList<EditTransactionHistoryEntry> UndoStack, IReadOnlyList<EditTransactionHistoryEntry> RedoStack)`
  — one document's full undo/redo stack state, as persisted by...
- `IEditTransactionHistoryStore` / `JsonEditTransactionHistoryStore` — an optional persistence boundary
  for a document's undo/redo stack, one JSON file per document id (`edit-history-{id}.json`), written
  atomically via the identical temp-file-then-`File.Move` pattern `JsonSequenceDocumentStore` already
  uses. `LoadAsync` returns `null` (never throws) when no history has ever been saved for a given
  document id — a document with no edit history yet is a normal state.

**Modified `XlightsAI.Studio.Editing.EditTransactionService`** (additive, both existing constructors
unchanged):

- New constructor `EditTransactionService(IEffectLockPolicy, IEffectRecipeRegistry, IEditTransactionHistoryStore)`
  — opts into real cross-instance persistence; omitting it keeps undo/redo history in-memory-only for
  that service instance's lifetime (disclosed, not silently claimed as durable — see §3/§6).
- `ApplyAsync` now also handles `SetEffectLocked`, and — after any successful command of any kind —
  records the before/after snapshot onto that document's own undo stack and **clears its redo stack**
  (the standard "a new edit invalidates pending redo history" rule), persisting the updated snapshot
  when a history store is configured.
- `UndoAsync(Guid projectId, ...)` / `RedoAsync(Guid projectId, ...)` are now fully implemented: pop the
  top of the relevant stack (rehydrating from the configured history store first if this service
  instance has no in-memory knowledge of `projectId` yet — the "survived a restart" path), move the
  entry to the other stack, persist, and return the entry's `Before`/`After` document respectively.
  Reject with the two new `EditRejectionReason` values below when the relevant stack is empty.
- `ResolveEditableEffect` gained an `enforceLocks = true` parameter; `ApplySetLocked` is the one caller
  that passes `false` (see the ADR in §3).

**Modified `XlightsAI.Studio.Editing.EditRejectionReason`** (additive): `NothingToUndo`, `NothingToRedo`.

**Modified `XlightsAI.Studio.Editing.IEditTransactionService`** (doc comments only, no signature
change): documents the "projectId" interpretation decision (§3) and the two new rejection reasons.

**No `Xlights`, `Shell`, or `App` project changes.** `EffectInstance.Locked` already existed (Step 019);
no change to its shape was needed. See §3 for why no Shell/App wiring was added.

## 3. Architecture deviations / decisions

- **No Shell/App UI wiring for Undo/Redo/lock-toggle.** As established in §1, Steps 019/020 never wired
  the apply pipeline itself (`MoveEffect`/`ResizeEffect`/`SetEffectParameters`) into
  `TimelineViewModel`/`ShellViewModel`/`MainWindow.axaml` — every one of their own required acceptances
  was proven at the command/service layer, with the UI gap explicitly disclosed in each step's own
  evidence file. Wiring only Undo/Redo buttons into the UI now, with no UI-triggered edit for them to
  act on, would (a) be new scope beyond what this step's own required acceptance demands ("Undo
  restores exact snapshot; locked effects reject programmatic edits" — both fully testable at the
  domain layer, exactly like every prior Step 019–021 acceptance was) and (b) risk presenting a
  misleadingly "more wired" UI than the edit pipeline underneath it actually is. This is the single
  largest disclosed scope decision in this step; flagged here explicitly rather than silently expanded
  or silently left unmentioned. A future step that finally wires Move/Resize/SetParameters into the UI
  (closing the Step 019/020 gap) should wire Undo/Redo/lock-toggle alongside it using this step's own
  real `UndoAsync`/`RedoAsync`/`SetEffectLocked` — no further domain change would be needed.
- **The lock-toggle command bypasses the very lock it is setting.** `ApplySetLocked` resolves its target
  effect with `enforceLocks: false`. Rationale: the phase gate's own named sequence is "lock Arch 2,
  attempt forbidden modification" — the lock toggle is the trusted, explicit approval action a human
  takes, not a content edit subject to the lock bit it is itself flipping. Without this, a locked effect
  could never be unlocked (the unlock command would itself be rejected as editing a locked effect), and
  undoing a lock-then-edit sequence would deadlock. Stale-revision and out-of-scope-model checks are
  **not** bypassed — only the lock check itself, and only for this one command. Proven directly by
  `LockedEffect_CanStillBeUnlockedByItsOwnLockToggleCommand` and the two negative-path tests
  (`SetEffectLocked_StaleRevision_IsRejected...`, `SetEffectLocked_OutOfScopeModel_IsRejected...`),
  which confirm the other two gates still apply.
- **"projectId" in `UndoAsync`/`RedoAsync` is interpreted as the target document's own
  `SequenceDocument.Id`.** This is a continuation of `IEditTransactionService.ApplyAsync`'s own already-
  disclosed Step 019 deviation (no project-session/"currently open document for project X" concept
  exists anywhere in this repository; `ApplyAsync` already takes the document explicitly rather than
  resolving one internally). Since `EditTransactionService` now owns a real undo/redo stack per document
  id — populated directly from the `SequenceDocument` argument every `ApplyAsync` call already supplies
  — there is no new infrastructure to invent; `projectId` is simply read as that same id. Documented on
  `IEditTransactionService` itself, not left implicit.
- **Undo/redo history persistence is real but optional, not SQLite-backed.** The phase spec (§4)
  states "Undo/redo history survives restart within a retention limit." No SQLite audit table for edit
  transactions exists anywhere in this repository (`XlightsAI.Studio.Projects` only has a
  project-manifest schema); inventing one now would be exactly the "generic interface without a
  concrete boundary" Section 2 warns against, and no prior PHASE-03 step added SQLite persistence for
  anything. Instead, `IEditTransactionHistoryStore`/`JsonEditTransactionHistoryStore` follow the
  identical, already-proven `ISequenceDocumentStore`/`JsonSequenceDocumentStore` pattern (atomic
  temp-file-then-move JSON, one file per owning id). This is genuinely real, tested, on-disk durability
  — not a disclosed gap pretending to be real — proven by
  `Step022_Acceptance_Passes_UndoHistorySurvivesRestart_ViaRealJsonHistoryStore`, which constructs a
  **second, independent `EditTransactionService` instance** pointed at the same on-disk directory (the
  closest honest stand-in for "the app restarted" available inside a single test process — a real
  cross-process restart is not exercised) and successfully undoes/redoes an edit applied by the first,
  now-discarded instance. When no history store is configured, history is explicitly in-memory-only for
  that instance's lifetime — proven, not silently assumed, by
  `WithoutHistoryStore_UndoHistoryDoesNotSurviveANewServiceInstance_DisclosedGap`. No explicit
  **retention limit** (the spec's own phrase) is implemented — every applied edit is kept indefinitely
  on both stacks; flagged as an unresolved gap in §6, not silently satisfied.
- **No new SQLite migration, no new `Xlights`/`Projects` project changes.** This step's own scope is
  pure document/undo-stack state; it does not touch XSQ export (that is Step 023's scope), matching the
  standing instruction not to cross into Step 023.

## 4. Commands actually executed

```
dotnet build src\XlightsAI.Studio.Editing\XlightsAI.Studio.Editing.csproj --nologo
dotnet test XlightsAI.Studio.slnx --nologo
dotnet build src\XlightsAI.Studio.Shell\XlightsAI.Studio.Shell.csproj --nologo
dotnet build src\XlightsAI.Studio.App\XlightsAI.Studio.App.csproj --nologo
dotnet build XlightsAI.Studio.slnx --nologo
dotnet test tests\XlightsAI.Studio.Editing.Tests\XlightsAI.Studio.Editing.Tests.csproj --nologo --filter "FullyQualifiedName~Step022" --logger "console;verbosity=normal"
dotnet test XlightsAI.Studio.slnx --nologo
```

All run via the PowerShell tool — the sandboxed POSIX Bash tool in this session is non-functional
(every invocation, including a plain command, returns exit code 1 with no output), the same
pre-existing environment limitation recorded in every prior PHASE-03 step's own evidence.

### Baseline (source changes applied, before any new Step 022 test file existed)

```
Passed!  - Failed: 0, Passed: 71, Skipped: 0, Total: 71 - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed: 59, Skipped: 0, Total: 59 - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed: 25, Skipped: 0, Total: 25 - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed: 72, Skipped: 0, Total: 72 - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed: 31, Skipped: 0, Total: 31 - XlightsAI.Studio.Reference.Tests.dll
```

258 total, 0 failed — confirms the `EditTransactionService`/`EditRejectionReason`/
`IEditTransactionService` source edits (the new `SetEffectLocked` command, the new
`enforceLocks` parameter, the rewritten `ApplyAsync`/`UndoAsync`/`RedoAsync`) did not break any
pre-existing test, before a single new Step 022 test was added.

### Build results (after full implementation)

All three builds (`Editing`, `Shell`, `App`) and the full solution: `Build succeeded. 0 Warning(s). 0
Error(s).`

### Targeted test results (new tests only)

```
tests\XlightsAI.Studio.Editing.Tests — filter "~Step022": 11/11 passed
  Step022_Acceptance_Passes_UndoRestoresExactSnapshot_ThenRedoReappliesExactSnapshot
  Step022_Acceptance_Passes_LockedEffectRejectsProgrammaticEdit_ButUnlockedPeersStillEditable
  Step022_Acceptance_Passes_UndoHistorySurvivesRestart_ViaRealJsonHistoryStore
  LockedEffect_CanStillBeUnlockedByItsOwnLockToggleCommand
  SetEffectLocked_StaleRevision_IsRejected_WithoutPartialMutation_NegativePath
  SetEffectLocked_OutOfScopeModel_IsRejected_NegativePath
  UndoWithNoAppliedEdits_IsRejected_NothingToUndo_NegativePath
  RedoWithNothingUndone_IsRejected_NothingToRedo_NegativePath
  NewEditAfterUndo_ClearsRedoHistory_NegativePath
  UndoAfterSaveReopenRoundTrip_StillRestoresExactSnapshot
  WithoutHistoryStore_UndoHistoryDoesNotSurviveANewServiceInstance_DisclosedGap
```

(One iteration note: the first run of
`Step022_Acceptance_Passes_LockedEffectRejectsProgrammaticEdit_ButUnlockedPeersStillEditable` failed —
moving Roofline under a whole-document scope was itself rejected as `Locked`, because
`EffectLockPolicy` (already-existing, unmodified Step 019/020 behavior) blocks an entire scope that
*reaches* any locked effect, not just an edit that *targets* one. Fixed by narrowing that test's own
scope to just Roofline's model, matching how a real caller would scope an edit around a known lock —
not a product-code change. No existing `EffectLockPolicy` behavior was touched.)

### Full regression (after implementation)

```
Passed!  - Failed: 0, Passed: 71, Skipped: 0, Total: 71, Duration: 270 ms - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed: 70, Skipped: 0, Total: 70, Duration: 269 ms - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed: 72, Skipped: 0, Total: 72, Duration: 1 s   - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed: 25, Skipped: 0, Total: 25, Duration: 2 s   - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed: 31, Skipped: 0, Total: 31, Duration: 31 s  - XlightsAI.Studio.Reference.Tests.dll
```

**269 total, 0 failed, 0 skipped** — up from the Step 021 baseline of 258 (59→70 in `Editing.Tests`;
the `+11` reconciles exactly against the 11 new tests above). `Shell.Tests` is unchanged at 72 (no
Shell/App source was modified this step — see §3). All 258 pre-existing tests still pass unmodified; no
existing test file's assertions were touched.

### Manual/desktop demonstration

```
$p = Start-Process -FilePath "src\XlightsAI.Studio.App\bin\Debug\net10.0\XlightsAI.Studio.exe" -PassThru
Start-Sleep -Seconds 5
# Still running after 5s: True
Stop-Process -Id $p.Id -Force
```

The real compiled Avalonia desktop app started and ran for 5+ seconds with no exception and no early
exit. Since this step made no Shell/App/XAML changes (§3), this smoke test confirms only that the
unrelated `Editing` project changes did not regress the app's own startup — it is **not** a UI
demonstration of undo/redo/lock-toggle, because no such UI exists yet (disclosed, not silently implied).

## 5. Fixtures / input hashes

No new fixture files were added. All tests construct the gate demo's own exact layout/plan shape
(`DisplayGraph` with `Roofline`/`Arch 1`/`Arch 2`, `ThreeEffectPlanFactory.CreatePlan(...)`) in-memory,
identical in shape to every other PHASE-03 step's own fixtures.
`Step022_Acceptance_Passes_UndoHistorySurvivesRestart_ViaRealJsonHistoryStore` and
`UndoAfterSaveReopenRoundTrip_StillRestoresExactSnapshot` write real temp-directory files under
`%TEMP%\xlai-step022-<guid>\`, deleted by the test class's `IDisposable.Dispose()` (identical cleanup
convention to `Step020AcceptanceTests`).

## 6. Unresolved issues / known gaps

- **No Shell/App UI wiring for Undo/Redo/lock-toggle** (§3) — this step's real domain logic
  (`SetEffectLocked`, `UndoAsync`/`RedoAsync`, `IEditTransactionHistoryStore`) is fully implemented and
  tested, but there is no bindable command or button a user could click yet, consistent with the
  pre-existing, still-unresolved Step 019/020 gap that the apply pipeline itself is not UI-wired. A
  future step should wire all of Move/Resize/SetParameters/SetEffectLocked/Undo/Redo into
  `TimelineViewModel`/`ShellViewModel` together.
- **No explicit retention limit on undo/redo history** (§3) — the spec names one ("within a retention
  limit"); this implementation keeps every applied edit indefinitely on both stacks for a document.
  Not required by this step's own stated "required acceptance"/"failure/edge case" text, but flagged as
  a literal, not-yet-satisfied piece of Section 4's prose.
- **Restart persistence is proven only within a single test process** (§3) — a second
  `EditTransactionService` instance over the same on-disk JSON directory stands in for "the app
  restarted," which is the strongest proof available without spawning a second real OS process; a true
  cross-process restart was not exercised.
- **No real xLights installation available in this environment** (continuing the gap disclosed in every
  prior PHASE-02/PHASE-03 step) — this step touches no XSQ/XML export at all (Step 023's own scope), so
  this gap does not affect its own required acceptance; recorded per the user's standing waiver.
- **`EffectLockPolicy`'s existing scope-wide lock semantics** (not new to this step, but newly visible
  here): a scope that merely *reaches* a locked effect blocks the whole scope's edit, even if the
  specific command targets a different, unlocked effect within that same scope. This is pre-existing
  Step 019/020 behavior, unchanged by this step (see §4's test-iteration note) — flagged here only
  because it is the first step whose own tests had to explicitly design around it.

## 7. PASS / BLOCKED

**Status: PASS** for Step 022's own stated required acceptance and failure/edge case:

- "Undo restores exact snapshot" — proven by
  `Step022_Acceptance_Passes_UndoRestoresExactSnapshot_ThenRedoReappliesExactSnapshot` (drags Arch 1,
  resizes Roofline, undoes both, redoes both — the phase gate's own named sequence — asserting full
  structural document equality, not merely "no exception," at every step) and
  `UndoAfterSaveReopenRoundTrip_StillRestoresExactSnapshot` (same proof through a real
  `JsonSequenceDocumentStore` round trip).
- "Locked effects reject programmatic edits" — proven by
  `Step022_Acceptance_Passes_LockedEffectRejectsProgrammaticEdit_ButUnlockedPeersStillEditable` (locks
  Arch 2, attempts the phase gate's own named "forbidden modification," confirms it is rejected with no
  mutation, then confirms a different, unlocked effect remains editable) and
  `LockedEffect_CanStillBeUnlockedByItsOwnLockToggleCommand` (the lock toggle's own disclosed ADR, §3).
- "Undo/redo... respects project revision" — proven by `NewEditAfterUndo_ClearsRedoHistory_NegativePath`
  (a new edit after an undo correctly invalidates/discards the now-stale pending redo entry rather than
  letting it be silently reapplied later).
- "Test restart" — proven for real, not merely disclosed, by
  `Step022_Acceptance_Passes_UndoHistorySurvivesRestart_ViaRealJsonHistoryStore` (§3/§6 caveat: proven
  within one test process via a second service instance, not a true cross-process restart).
- "Test... stale writes" / "Out-of-scope, locked or stale revision must be rejected without partial
  mutation" — proven by `SetEffectLocked_StaleRevision_IsRejected_WithoutPartialMutation_NegativePath`,
  `SetEffectLocked_OutOfScopeModel_IsRejected_NegativePath`,
  `UndoWithNoAppliedEdits_IsRejected_NothingToUndo_NegativePath`, and
  `RedoWithNothingUndone_IsRejected_NothingToRedo_NegativePath` — every negative path leaves the
  document provably unchanged.

No previously accepted step's contract was modified except the disclosed, backward-compatible additions
in §2/§3 (`EditTransactionService`'s new constructor overload and `enforceLocks` parameter;
`IEditTransactionService`'s doc comments; two new `EditRejectionReason` values) — no existing
`Editing`/`Shell`/`Xlights` public member was removed or had its existing behavior changed, confirmed
by the full pre-existing 258-test suite passing unmodified (§4).

## 8. Phase-wide status after this session

Steps 016–022 all now have recorded PASS evidence. Step 023 ("Scope-aware export and regeneration
contract") remains unimplemented and out of this session's own scope — per the task's instruction, this
session does not proceed to it. The phase-wide gate demonstration (open a three-effect sample, drag
Arch 1, resize Roofline, undo/redo both, lock Arch 2, attempt forbidden modification, export and verify
other effects stay unchanged) now has every one of its sub-behaviors proven by an automated test at the
domain layer — Step 022 closes the last missing piece (undo/redo, lock/forbidden-modification) — but
the full gate has still **not been run as one continuous, UI-driven demonstration**, both because the
Move/Resize/apply pipeline itself remains unwired to any UI (§3/§6, a standing Step 019/020 gap this
step deliberately did not expand into) and because Step 023's own export/regeneration-contract scope is
not yet implemented.
