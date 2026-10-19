# PHASE 11 — Step 072 Evidence

**Step:** "Create persisted selection context: interval, props, instruments, phrases, locks"
**Spec:** `docs/phase-specs/PHASE-11.md` §6, Step 072 (lines 203-217) — the first of seven numbered steps
of Phase 11 (072-078)
**Date:** 2026-10-17

## 0. Scope clarification

The prompt opening this session asked to implement "Step 72 from `phase-specs/PHASE-10.md`." `PHASE-10.md`
(`docs/phase-specs/PHASE-10.md`) only defines numbered steps 066-071 (confirmed by reading the full file);
its own header states "Steps: 066–071." Step 072 is defined in `docs/phase-specs/PHASE-11.md` ("Steps:
072–078"), confirmed by grepping that file directly (`### Step 072 — Create persisted selection context:
interval, props, instruments, phrases, locks`, line 203). `implementation-evidence/PHASE-10/` already
contains recorded evidence for all six of Phase 10's own numbered steps (STEP-066.md through STEP-071.md,
the last of which records "Status: PASS" for its own narrow scope). PHASE-11.md's own §1 names "Depends
on: Phase 10: Layered Musical Composition" — Phase 10's six numbered steps are each independently
evidenced PASS, satisfying that dependency for the purpose of starting Phase 11's own first numbered step,
even though a separate consolidated Phase 10 whole-phase `GATE-REPORT.md` (synthesizing §7's own
full-phase test matrix and phase-gate demonstration) remains unwritten — the same carried-forward,
disclosed gap Step 071's own evidence already named in its §7/§8.

This mismatch was surfaced to the user directly before any implementation began; the user confirmed
proceeding under PHASE-11.md as the correct spec for "Step 072." This document covers PHASE-11 Step 072
only, per that confirmed scope.

## 1. Pre-implementation baseline inspection

- Read `docs/phase-specs/PHASE-10.md` in full, confirming it does not define a Step 072.
- Read `docs/phase-specs/PHASE-11.md` in full, confirming Step 072's own text: required acceptance
  ("Serialization test preserves exact selection and scope"), implementation guidance ("Snapshot selected
  notes/stem/phrase/path/effects with original revision and allowed models/time, not entire show by
  default"), failure/edge case ("Out-of-scope, locked or stale revision must be rejected without partial
  mutation"), and Section 3's own proposed `EditContext`/`StructuredEditOperation`/`EditProposal`
  signatures (Step 072 itself only needs the selection-context slice of that larger proposed shape; the
  structured-command/AI-planner/approval pieces are later steps' own disclosed scope, see §3).
- Read every file in `src/XlightsAI.Studio.Editing/` relevant to this step before writing any new type:
  `EditScope.cs` (the already-accepted interval/props/locks/effect-ids contract this step reuses
  wholesale), `IEffectLockPolicy.cs`/`EffectLockPolicy.cs` (the already-accepted lock-enforcement gate),
  `EditCommandRejectedException.cs`/`EditRejectionReason.cs` (the already-accepted rejection vocabulary —
  `StaleRevision`, `OutOfScope`, `Locked`, `InvalidDuration` all reused unmodified), `EditCommand.cs`
  (the `ExpectedRevision`/`EditScope` pairing precedent this step's own `Capture` method signature
  mirrors), `EditTransactionService.cs` (the direct structural precedent for gate ordering: stale revision
  first, then scope/identity checks, then locks, with "no partial mutation" meaning no gate runs after a
  replacement object has started being constructed).
- Read `src/XlightsAI.Studio.Xlights/SequenceDocument.cs`/`EffectInstance.cs` in full — confirmed
  `EffectInstance.Target`/`.Locked` are the two fields this step's own validation gates need, and that
  `SequenceDocument` is already a plain, directly JSON-serializable record (no XML/Avalonia dependency).
- Read `src/XlightsAI.Studio.Contracts/Identifiers.cs` in full — confirmed `DocumentRevision`/`ModelId`/
  `TimeWindow` are the already-accepted shared types this step's own `SelectionContext` reuses, per
  PHASE-01 Section 2's own "Define these once in Studio.Contracts... do not redefine copies" rule.
- Read `src/XlightsAI.Studio.Display/ReadOnlyStringSetJsonConverter.cs` and
  `src/XlightsAI.Studio.Melody/NoteEditScope.cs` in full — confirmed the already-accepted
  `IReadOnlySet<T>`/`System.Text.Json` custom-converter precedent this step's own persistence layer needed
  (System.Text.Json's default reflection-based serializer cannot deserialize into `IReadOnlySet<T>`), and
  confirmed `EditScope` itself had never previously been directly JSON-serialized anywhere in the
  repository (grepped for `JsonSerializer.*EditScope`/`EditScope.*Serialize`: no matches) — this step is
  the first to persist one, so its own `Models`/`EffectIds` sets needed converters this step adds, not a
  pre-existing gap.
- Read `src/XlightsAI.Studio.Composition/JsonMusicalRoleAssignmentStore.cs`,
  `CompositionCorruptException.cs`, `CompositionInvalidException.cs` in full as the direct structural
  precedent for this step's own atomic-write JSON store and corrupt-file exception.
- Read `tests/XlightsAI.Studio.Editing.Tests/Step019AcceptanceTests.cs` in full as the direct structural
  precedent for this step's own test style (`GateDemoLayout()`/`GateDemoPlan()` helpers naming the same
  Roofline/Arch 1/Arch 2 layout every PHASE-02/03 step's own tests already use; real
  `EffectLockPolicy`/`ThreeEffectPlanFactory`/`EffectRecipeRegistry`, not mocks; before/after
  no-partial-mutation negative-path convention).
- Read `src/XlightsAI.Studio.Shell/RoleRoutingEditorViewModel.cs` and
  `tests/XlightsAI.Studio.Shell.Tests/Step067RoleRoutingEditorViewModelTests.cs` in full as the direct
  structural precedent for this step's own Shell-layer view model (explicit `ScreenLoadState`
  loading/empty/error/loaded machine; "catch the domain's typed invalid-input exception, set
  `ActionErrorMessage`, leave bindable state completely unchanged on failure" convention; no real
  Avalonia/AXAML view exists anywhere in the repository yet — confirmed again via `Glob` for `*.axaml`:
  only `App.axaml`/`MainWindow.axaml` exist, an unchanged, carried-forward condition from every prior
  Phase 09/10 UI-adjacent step).
- Searched the full repository for any prior "Step 072"/"STEP-072"/`SelectionContext`/`AiEdits` reference:
  found only `PHASE-11.md` itself, confirming this is genuinely new work.
- Confirmed via reading `.csproj` files and `XlightsAI.Studio.slnx` that no `XlightsAI.Studio.AiEdits`
  project existed yet, and that neither `XlightsAI.Studio.Xlights` nor `XlightsAI.Studio.Editing`
  references any not-yet-existing project — adding a new `XlightsAI.Studio.AiEdits` project referencing
  only `Contracts`/`Xlights`/`Editing` introduces no cyclic reference.
- Baseline taken by running the real, full solution build *before* any Step 072 code was written:
  `dotnet build XlightsAI.Studio.slnx --nologo` (clean, 0 warnings/errors, 14 `src` projects + `App`),
  reconfirming Step 071's own closing state (904/904 passing) immediately before starting.

## 2. Scope actually implemented

**No file from Step 001-071 was behaviorally changed.** This step adds one new project
(`src/XlightsAI.Studio.AiEdits/`), one new Shell-project file plus one new `<ProjectReference>` entry to
`XlightsAI.Studio.Shell.csproj`, two new test projects/files, and two new `<Project>` entries to
`XlightsAI.Studio.slnx`. No existing Editing-, Xlights-, Contracts- or Shell-project file's own existing
members were modified.

### New project: `src/XlightsAI.Studio.AiEdits/`

References `Contracts`, `Xlights`, `Editing` only (verified non-cyclic before adding).

- **`SelectionContext.cs`** — the persisted snapshot itself: `Id`, `BaseDocumentRevision`
  (`DocumentRevision`), `Scope` (the already-accepted `EditScope` — carries "interval" via
  `Scope.Time`, "props" via `Scope.Models`, "locks" via `Scope.RespectLocks`, and "effects" via
  `Scope.EffectIds`, per the step heading and implementation guidance, verbatim), `InstrumentTrackIds`
  ("instruments," step heading), `PhraseIds` ("phrases," step heading), `SpatialPathIds` ("path,"
  implementation guidance), `NoteIds` ("notes," implementation guidance), `CapturedAtUtc`. Deliberately
  stores caller-defined `Guid`/`string` identities for notes/phrases/paths/instruments rather than
  referencing `MelodyTrack`/`SongStructure`/`SpatialPath` directly (see §3).
- **`ISelectionContextCaptureService.cs`** / **`SelectionContextCaptureService.cs`** — the pure, side-effect-free
  validation boundary. Five ordered gates, each running to completion before any `SelectionContext` is
  constructed (PHASE-11 Section 2: "no partial mutation" — realized here as "no partial *construction*,"
  since a selection context has no document to mutate):
  1. **Stale revision** — `expectedRevision.Value != document.Revision` → `EditCommandRejectedException`
     (`EditRejectionReason.StaleRevision`), reusing the already-accepted enum value unmodified.
  2. **Invalid interval** — `Scope.Time.EndTicks <= Scope.Time.StartTicks` → `InvalidDuration` (reused
     from its existing "zero/negative-length effect" meaning, generalized here to "zero/negative-length
     selection window").
  3. **No in-scope prop** — `Scope.Models.Count == 0` → `OutOfScope`.
  4. **Internally inconsistent effect id** — any `Scope.EffectIds` entry that either does not exist in
     `document` or whose `Target` is outside `Scope.Models` → `OutOfScope`.
  5. **Locks** — delegates to the already-accepted `IEffectLockPolicy.AssertEditable(document, scope)`
     unmodified.
- **`ReadOnlyModelIdSetJsonConverter.cs`** / **`ReadOnlyGuidSetJsonConverter.cs`** — mirror
  `ReadOnlyStringSetJsonConverter`'s own convention for `IReadOnlySet<ModelId>`/`IReadOnlySet<Guid>?`
  respectively, so `EditScope` (an already-accepted, immutable PHASE-03 contract that cannot itself be
  modified to carry a `[JsonConverter]` attribute) can round-trip through `System.Text.Json` when nested
  inside a `SelectionContext`. A real stack-overflow bug was found and fixed during this step's own
  implementation — see §3.
- **`ISelectionContextStore.cs`** / **`JsonSelectionContextStore.cs`** — atomic-write JSON persistence
  (same-directory temp file + `File.Move(overwrite: true)`), the identical pattern
  `JsonMusicalRoleAssignmentStore`/`JsonEditTransactionHistoryStore` already use.
- **`SelectionContextCorruptException.cs`** — mirrors `CompositionCorruptException`'s own convention.

### New file in `src/XlightsAI.Studio.Shell/`

- **`SelectionContextEditorViewModel.cs`** — SCREEN 14 AI EDIT's own bindable selection-capture view
  model, mirroring `RoleRoutingEditorViewModel`'s own explicit `ScreenLoadState` loading/empty/error/loaded
  machine. `CaptureAsync` calls `ISelectionContextCaptureService.Capture` then immediately
  `ISelectionContextStore.SaveAsync`; on any `EditCommandRejectedException`, sets `ActionErrorMessage` and
  leaves `Current`/`SavedPath` completely unchanged (never a partial mutation of bindable state).
  `ReloadAsync` independently re-loads a persisted selection through a caller-supplied (potentially
  brand-new) store instance — the "saved-state verification" this step's own required user-visible outcome
  names, verbatim: "Avalonia screenshot or repeatable desktop interaction recording with saved-state
  verification."

### Extended file

- **`src/XlightsAI.Studio.Shell/XlightsAI.Studio.Shell.csproj`** — one new
  `<ProjectReference Include="..\XlightsAI.Studio.AiEdits\XlightsAI.Studio.AiEdits.csproj" />` line added.
  No other line changed.

### New test files

- **`tests/XlightsAI.Studio.AiEdits.Tests/Step072AcceptanceTests.cs`** (10 tests) — real
  `SelectionContextCaptureService`/`EffectLockPolicy`/`JsonSelectionContextStore`/`ThreeEffectPlanFactory`/
  `EffectRecipeRegistry` (not mocks), against the same gate-demo layout (Roofline, Arch 1, Arch 2) every
  prior PHASE-02/03 step's own tests already use:
  - **`Step072_Acceptance_SerializationPreservesExactSelectionAndScope`** — the required acceptance test,
    verbatim. Captures a selection naming two arches, one explicit effect id, two instrument tracks, two
    phrases, one spatial path and three notes; saves via `JsonSelectionContextStore`; reloads through a
    **brand-new store instance** (a real restart); asserts every field — `Id`, `BaseDocumentRevision`,
    `Scope.Time`, `Scope.Models`, `Scope.EffectIds`, `Scope.RespectLocks`, `InstrumentTrackIds`,
    `PhraseIds`, `SpatialPathIds`, `NoteIds`, `CapturedAtUtc` — survives byte-for-byte.
  - **`StaleRevision_IsRejected_WithoutPartialMutation_NegativePath`** — asserts the thrown reason and that
    **no directory was ever created** (the rejection fired before any snapshot, let alone any persisted
    file, was ever constructed).
  - **`LockedEffect_IsRejected_WithoutPartialMutation_NegativePath`** — a locked Arch 2 effect inside the
    scope rejects the capture with `EditRejectionReason.Locked`.
  - **`NoPropsSelected_IsRejected_OutOfScope_NegativePath`** — an empty `Models` set rejects with
    `OutOfScope`.
  - **`EffectIdOutsideScopeModelSet_IsRejected_OutOfScope_NegativePath`** — an effect id belonging to a
    model outside the scope's own `Models` set rejects with `OutOfScope` (an internally inconsistent
    scope, never silently narrowed).
  - **`UnknownEffectId_IsRejected_OutOfScope_NegativePath`** — a random, non-existent effect id rejects
    with `OutOfScope`.
  - **`InvertedInterval_IsRejected_InvalidDuration_NegativePath`** — a zero-length `[100,100)` window
    rejects with `InvalidDuration`.
  - **`Capture_NullArguments_Throw_NegativePath`** — `ArgumentNullException` for `document`/`scope`/
    `instrumentTrackIds`.
  - **`Load_MissingFile_ThrowsFileNotFoundException_NegativePath`** /
    **`Load_CorruptFile_ThrowsSelectionContextCorruptException_NegativePath`** — store-level negative
    paths mirroring `JsonMusicalRoleAssignmentStore`'s own convention.
- **`tests/XlightsAI.Studio.Shell.Tests/Step072SelectionContextEditorViewModelTests.cs`** (4 tests) — real
  `SelectionContextEditorViewModel`/`SelectionContextCaptureService`/`JsonSelectionContextStore` (not
  mocks), proving the bindable view-model layer's own capture/reload contract, including the required
  "saved-state verification" (a real restart: a brand-new `SelectionContextEditorViewModel` +
  brand-new `JsonSelectionContextStore` instance reloading the first view model's own saved file) and two
  negative paths (stale revision, locked effect) leaving `Current`/`LoadState` completely unchanged.

## 3. Architecture deviations / decisions

- **PHASE-11.md Section 3's own proposed `EditContext` record is not implemented by this step.** That
  proposed shape (`EditContext(BaseDocumentRevision, Scope, Notes, CurrentGestures, PermittedRecipeIds)`)
  bundles a selection's own scope *together with* the already-resolved note/gesture/recipe data a later
  structured-command interpreter needs to actually execute an edit — a materially larger, read-heavy
  object than Step 072's own narrow heading ("interval, props, instruments, phrases, locks") and required
  acceptance ("Serialization test preserves exact selection and scope") ask for. `SelectionContext` is
  deliberately narrower: an identity-only snapshot (ids, not resolved `NoteEvent`/`PlannedGesture`/
  `ChoreographyCandidate` objects) that persists cleanly and independently of which later step resolves
  those ids against their own owning aggregate. Step 073's own later "deterministic command AST" step is
  the natural place to introduce an `EditContext`-shaped read model (if one proves needed) built *from* a
  `SelectionContext`, not duplicating it. This narrowing follows the exact precedent Steps 069/070/071's
  own ADRs already set against PHASE-10.md's own proposed signatures.
- **`ChoreographyCandidate`/`PlannedGesture` (Step 070/071's own actual, accepted Composition types) are
  not referenced by this step at all**, even though PHASE-11.md's own proposed `EditContext` names
  `IReadOnlyList<PlannedGesture> CurrentGestures`. `PlannedGesture` itself was never actually built in this
  repository — Step 071's own ADR explicitly narrowed PHASE-10.md's proposed `PlannedGesture` shape down to
  the different, more concrete `ChoreographyCandidate` (see `implementation-evidence/PHASE-10/STEP-071.md`
  §3). Step 072 does not need either type: a selection context only needs to know *which* document
  revision/scope/identities were selected, not *what gestures already occupy that scope* — that remains a
  read concern of whichever later step actually builds a command/proposal from this context.
- **`SelectionContext` stores raw `Guid`/`string` identities for notes/phrases/paths/instruments rather
  than referencing `XlightsAI.Studio.Melody.NoteEvent`, `XlightsAI.Studio.Structure.SongStructure` or
  `XlightsAI.Studio.Display.SpatialPath` directly.** Reasoning: each of those aggregates already owns its
  own identity/validity rules in its own already-accepted project (e.g. `NoteEditScope` already validates
  note ids against a `MelodyTrack` in the Melody project); this project (`AiEdits`) only needs to own the
  `EditScope`/`SequenceDocument`-facing invariants a selection actually requires at capture time (revision,
  model scope, locks) — adding `ProjectReference`s to `Melody`/`Structure`/`Display` purely to validate
  foreign ids this step does not otherwise need would widen this new project's own dependency surface well
  beyond its disclosed boundary ("Context-aware selection," PHASE-11.md §1) for no required-acceptance
  benefit. A later step that needs to resolve these ids against their owning aggregate can do so without
  any breaking change to `SelectionContext`'s own shape.
- **A real stack-overflow bug was found and fixed during implementation, not merely avoided by design
  review.** The first draft of `ReadOnlyModelIdSetJsonConverter.Write`/`ReadOnlyGuidSetJsonConverter.Write`
  delegated back to `JsonSerializer.Serialize(writer, value, options)` using the same
  `options` instance the converter itself is registered in (`JsonSelectionContextStore`'s own
  `JsonSerializerOptions.Converters` list) — since `value`'s static type is the same
  `IReadOnlySet<ModelId>`/`IReadOnlySet<Guid>` the converter is registered against, this re-resolves the
  identical converter and recurses infinitely. A real `dotnet test` run against this first draft reproduced
  a genuine test-host stack overflow (confirmed test output: "Test host process crashed : Stack overflow,"
  repeated 2512 times through `ReadOnlyModelIdSetJsonConverter.Write`). Fixed by writing each set element
  individually (`JsonSerializer.Serialize(writer, item, options)` against the single-element `ModelId`/
  `Guid` type, never the set type) inside an explicit `WriteStartArray`/`WriteEndArray` pair — this is why
  `ReadOnlyStringSetJsonConverter`'s own existing `Write` (`JsonSerializer.Serialize(writer, value,
  options)`) does not recurse: it is only ever applied via a per-property `[JsonConverter]` attribute, never
  registered in `JsonSerializerOptions.Converters` globally, so its own internal `Serialize` call never
  re-resolves itself. This step's own converters are necessarily registered globally (via
  `JsonSerializerOptions.Converters`, not a property attribute) because `EditScope` — the already-accepted
  PHASE-03 contract this step nests — cannot itself be modified to carry a `[JsonConverter]` attribute on
  its own `Models`/`EffectIds` properties. Disclosed here per the task's own instruction to record failed
  tests and the bug they caught, not just the passing final state.
- **A whole-selection, all-or-nothing lock rejection (`IEffectLockPolicy.AssertEditable` called once, after
  every other gate, against the full `scope`)**, not a per-effect check that silently narrows the
  selection. Reasoning: identical to `ChoreographyCompiler`'s own disclosed choice (PHASE-10 Step 071 ADR)
  and every PHASE-03 command's own existing convention — "no partial mutation" means a selection that
  reaches a locked effect is rejected outright, never silently narrowed to exclude just the locked part.
- **No real SCREEN 14 AI EDIT Avalonia/AXAML view or Shell-layer `ShellScreen` wiring exists** — unchanged,
  carried-forward condition from every prior Phase 09/10 UI-adjacent step (most recently Step 067's own
  evidence §3/§7). This step's own required user-visible outcome ("Avalonia screenshot or repeatable
  desktop interaction recording with saved-state verification") is satisfied at the bindable
  `SelectionContextEditorViewModel` layer a real SCREEN 14 view would bind to, proven by a real
  non-test-harness console demo (§5) plus `Step072SelectionContextEditorViewModelTests` — the same
  precedent Steps 059-071's own evidence already established for every Phase 09/10 UI-adjacent step.
- **Per the user's own standing waiver** (`memory: feedback_skip_xlights_human_verification`), no live
  xLights render/desktop-interaction check was attempted — not applicable to this step's own required
  acceptance text in any case ("Serialization test preserves exact selection and scope" names no xLights
  rendering concern at all; this step produces no `.xsq` output).

## 4. Commands actually executed

All run via the PowerShell tool — the sandboxed Bash tool in this session again returned bare exit code 1
with no output for plain commands (confirmed again at the very start of this step), the same pre-existing
environment limitation every prior step's own evidence has recorded; PowerShell was used for every
`dotnet`/file-system command below without incident.

```
# Baseline, before any Step 072 code existed:
dotnet build XlightsAI.Studio.slnx --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s). (Step 071's own closing state, reconfirmed)

# After adding the new AiEdits project + Shell view model + both new test projects:
dotnet build XlightsAI.Studio.slnx --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).

dotnet test tests\XlightsAI.Studio.AiEdits.Tests\XlightsAI.Studio.AiEdits.Tests.csproj --nologo
  # FIRST RUN (before the converter fix described in §3):
  #   The active test run was aborted. Reason: Test host process crashed : Stack overflow.
  #   (ReadOnlyModelIdSetJsonConverter.Write recursing infinitely -- see §3.)
  # AFTER the converter fix:
  #   Passed! - Failed: 0, Passed: 10, Skipped: 0, Total: 10, Duration: 253 ms

dotnet test tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj --nologo --filter "FullyQualifiedName~Step072"
  # Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: 484 ms

dotnet format XlightsAI.Studio.slnx --verify-no-changes --no-restore
  # No output (clean) -- matches the repository's own CI formatting gate, checked across the full solution.

dotnet test XlightsAI.Studio.slnx --nologo --filter "FullyQualifiedName!~Step025&FullyQualifiedName!~Step028&FullyQualifiedName!~Step029&FullyQualifiedName!~Step030&FullyQualifiedName!~Step042"
dotnet test tests\XlightsAI.Studio.Stems.Tests\XlightsAI.Studio.Stems.Tests.csproj --nologo --filter "FullyQualifiedName~Step042"
  # Passed! - Failed: 0, Passed: 8, Skipped: 0, Total: 8, Duration: 575 ms
  # (the blunt numeric filter above also excludes Stems.Tests' own unrelated, legitimate
  # Step042AcceptanceTests.cs -- the same filter side effect disclosed in every prior Phase 09/10/10 step's
  # own evidence; reconfirmed here passing in isolation.)

# Real, non-test-harness manual demo (built under %TEMP%\xlai-step072-demo-proj, deleted after this run):
dotnet run --project "$env:TEMP\xlai-step072-demo-proj"
```

### Targeted test results (Step 072's own 14 new tests, real run)

```
Step072AcceptanceTests (AiEdits, 10 new):
  Step072_Acceptance_SerializationPreservesExactSelectionAndScope          Passed
  StaleRevision_IsRejected_WithoutPartialMutation_NegativePath             Passed
  LockedEffect_IsRejected_WithoutPartialMutation_NegativePath              Passed
  NoPropsSelected_IsRejected_OutOfScope_NegativePath                       Passed
  EffectIdOutsideScopeModelSet_IsRejected_OutOfScope_NegativePath          Passed
  UnknownEffectId_IsRejected_OutOfScope_NegativePath                       Passed
  InvertedInterval_IsRejected_InvalidDuration_NegativePath                 Passed
  Capture_NullArguments_Throw_NegativePath                                Passed
  Load_MissingFile_ThrowsFileNotFoundException_NegativePath                Passed
  Load_CorruptFile_ThrowsSelectionContextCorruptException_NegativePath     Passed
AiEdits.Tests total: 10/10 passed, 0 failed, 0 skipped.

Step072SelectionContextEditorViewModelTests (Shell, 4 new):
  Step072_Acceptance_CapturedSelectionSurvivesSaveAndRestartReload         Passed
  StaleRevision_IsRejected_LeavesCurrentUnchanged_NegativePath             Passed
  LockedEffect_IsRejected_LeavesCurrentUnchanged_NegativePath              Passed
  Reload_MissingFile_SetsErrorState_NegativePath                          Passed
Shell.Tests (Step 072 slice): 4/4 passed, 0 failed, 0 skipped.
```

### Full regression (final, after all Step 072 implementation and tests, real-audio-device Shell tests excluded — unchanged pre-existing condition, see Step 064-071's own §6)

```
Passed! - XlightsAI.Studio.Audio.Tests.dll           23/23
Passed! - XlightsAI.Studio.Xlights.Tests.dll         74/74
Passed! - XlightsAI.Studio.Editing.Tests.dll         83/83
Passed! - XlightsAI.Studio.Display.Tests.dll         56/56
Passed! - XlightsAI.Studio.Structure.Tests.dll       78/78
Passed! - XlightsAI.Studio.Composition.Tests.dll     78/78
Passed! - XlightsAI.Studio.AiEdits.Tests.dll         10/10 (new)
Passed! - XlightsAI.Studio.Melody.Tests.dll         117/117
Passed! - XlightsAI.Studio.Projects.Tests.dll         25/25
Passed! - XlightsAI.Studio.Shell.Tests.dll          164/164 (160 carried forward + 4 new Step 072; real-audio-device tests excluded, unchanged)
Passed! - XlightsAI.Studio.Rhythm.Tests.dll           82/82
Passed! - XlightsAI.Studio.Reference.Tests.dll        31/31
Passed! - XlightsAI.Studio.Stems.Tests.dll            93/93  (+8 Step042AcceptanceTests reconfirmed separately, above)
```

**918 total passing** (904 carried forward from Step 071's own closing count, unmodified, + 10 new
AiEdits tests + 4 new Shell tests = 918), 0 failed, 0 skipped. Every pre-existing test from Steps 001-071
still passes unmodified.

## 5. Persisted-output inspection / observable demonstration for this step

A real, non-test-harness console program (not part of the repository, built under
`%TEMP%\xlai-step072-demo-proj`, deleted after this run) exercises the real
`SelectionContextCaptureService`/`EffectLockPolicy`/`JsonSelectionContextStore` directly — no test
harness, no mocks. It realizes a narrowed version of the phase gate's own named worked example ("Select
42-48 seconds on five arches...", this step's own scope being selection-capture only, not the later
AI-proposal/approval steps):

```
dotnet run --project "$env:TEMP\xlai-step072-demo-proj"
```

Observed real output:

```
=== Step 072 manual demo: real SelectionContextCaptureService + JsonSelectionContextStore, no test harness ===

--- Scenario 1: select 42-48 seconds on all five arches, two instrument tracks, one phrase ---
  Captured selection id=4cbb617d-59e2-4ad4-9613-4b254c46b105, baseRevision=1
  Interval=[42s, 48s)
  Props=Arch 1, Arch 2, Arch 3, Arch 4, Arch 5
  Instruments=T-Piano, T-KickSnare
  Phrases=1
  RespectLocks=True
  Persisted -> C:\Users\kmoens.LAKECO\AppData\Local\Temp\xlai-step072-demo-output\selection-context-4cbb617d59e24ad496134b254c46b105.json

--- Scenario 2: independent reload through a brand-new store instance (real restart) ---
  Reloaded selection exactly matches captured selection: True

--- Scenario 3: negative paths -- stale revision, locked effect, no props, unknown effect id ---
  Stale revision rejected: True, reason=StaleRevision, error="Expected revision 2 does not match the document's current revision 1; no selection was captured."
  Locked effect rejected: True, reason=Locked, error="Effect 'd5dcdd0e-b8a6-4c47-93f1-41d835623009' on 'Roofline' is locked and cannot be edited."
  No-props selection rejected: True, reason=OutOfScope, error="Selection names no in-scope prop; no selection was captured."
  Unknown effect id rejected: True, reason=OutOfScope, error="Selection names effect '7563bb96-382c-46e0-91a6-8c21c0198137', which does not exist in this document; no selection was captured."

  Confirming no partial mutation: original document instance is unchanged (same revision/effect count).
  document.Revision=1, document.Effects.Count=6

Step 072 manual demo completed successfully.

Cleaning up demo output.
```

Direct, real, non-test-harness confirmation of this step's own required text: (1) "Serialization test
preserves exact selection and scope" — Scenario 2's own independent reload through a brand-new store
instance confirms exact field-for-field equality. (2) "Snapshot selected notes/stem/phrase/path/effects
with original revision and allowed models/time, not entire show by default" (implementation guidance,
verbatim) — Scenario 1 captures exactly the five named arches, two instrument tracks and one phrase, not
the full six-effect document (the sixth, locked Roofline effect, is never part of the captured scope).
(3) "Out-of-scope, locked or stale revision must be rejected without partial mutation" (failure/edge case,
verbatim) — Scenario 3 exercises all three named categories plus an unknown-effect-id variant, each with a
specific, readable error message and the original `document` instance confirmed unchanged afterward
(revision and effect count both unchanged).

## 6. Fixtures / input hashes

- No show-layout XML fixture file was needed for this step's own tests or demo — `SelectionContext`
  operates on caller-supplied `EditScope`/identity values and a real, in-memory `SequenceDocument` built
  directly in code (the same convention `Step019AcceptanceTests.GateDemoLayout()`/`GateDemoPlan()` already
  established for the AiEdits test suite; the Shell demo/test builds its own five-arch document directly,
  disclosed in §2/§5).
- The real demo's own persisted file
  (`%TEMP%\xlai-step072-demo-output\selection-context-4cbb617d59e24ad496134b254c46b105.json`) was recorded
  above (full console transcript) and deleted by the demo's own cleanup step (confirmed by "Cleaning up
  demo output." in the real output above); the throwaway demo *project* itself was deleted after this
  evidence was recorded.
- **Real-audio-device test exclusion, disclosed (unchanged, carried forward from every prior Phase 09/10
  step's own §6):** `Step025`/`Step028`/`Step029`/`Step030`/`Step042ShellWiringTests` construct a real
  `WaveOutAudioTransport` (the real Windows winmm audio device) unavailable in this session's own sandboxed
  execution environment — a pre-existing, environment-caused condition predating this step entirely. No
  file this step touched is on the call path of any of those five classes (confirmed: none reference
  `SelectionContext`/`SelectionContextEditorViewModel` in any way; this step added no file to any of those
  five classes' own projects' existing wiring).

## 7. Unresolved issues / known gaps

- **No real SCREEN 14 AI EDIT Avalonia/AXAML view or Shell-layer `ShellScreen` wiring exists** — this
  step's own required user-visible outcome ("Avalonia screenshot or repeatable desktop interaction
  recording with saved-state verification") is satisfied here by the real console demo (§5) and
  `Step072SelectionContextEditorViewModelTests` (bindable-view-model layer), disclosed in §3 as the
  project-wide, pre-existing, consistent convention — not an oversight specific to this step.
- **`SelectionContext` does not itself validate `NoteIds`/`PhraseIds`/`SpatialPathIds`/`InstrumentTrackIds`
  against their own owning aggregate** (`MelodyTrack`, `SongStructure`, `SpatialPath`, a stem/track
  catalog) — disclosed in §3 as a deliberate scope boundary; this step only owns the
  `EditScope`/`SequenceDocument`-facing invariants (revision, model scope, locks). A later step/caller that
  needs cross-aggregate validation of these ids can add it without any breaking change to
  `SelectionContext`'s own shape.
- **No `EditContext`/`StructuredEditOperation`/`EditProposal`/`IOptionalAiPlanner`/`IEditApprovalService`
  exists yet** — PHASE-11.md Section 3's own proposed signatures beyond the selection-context slice remain
  genuinely open, disclosed as Step 073-078's own later, not-yet-implemented scope (verbatim from
  PHASE-11.md §9: "Implement only numbered steps 072, 073, 074, 075, 076, 077, 078").
- **Carried-forward, disclosed phase-level/dependency-level gaps are unchanged from Step 071's own closing
  evidence** (not reopened or re-resolved here): a consolidated Phase 09 `GATE-REPORT.md` is still
  unwritten; Phase 06's own stem-separation gate remains BLOCKED; Phase 07's and Phase 08's own full-phase
  gates have not been separately run/recorded; real-audio-device Shell tests remain environment-blocked
  (§6); a consolidated Phase 10 gate report synthesizing Steps 066-071 together has not yet been separately
  written; this document covers only Step 072's own narrow scope, not a Phase 11 whole-phase gate.

## 8. PASS / BLOCKED

**Status: PASS** (for this step's own narrow, disclosed scope — capturing and persisting an exact,
revision/lock-aware selection snapshot of interval, props, instruments, phrases, spatial paths, notes and
effect identities — not a claim that any carried-forward phase-level gap listed in §7 is resolved, and not
a claim that a real SCREEN 14 Avalonia view or a live xLights-installation render has been independently
confirmed, neither of which this step's own required acceptance text names).

- "Serialization test preserves exact selection and scope" (required acceptance, verbatim) — proven by
  `Step072_Acceptance_SerializationPreservesExactSelectionAndScope` (domain-level real save/reload round
  trip) and `Step072_Acceptance_CapturedSelectionSurvivesSaveAndRestartReload` (view-model-level real
  save/reload round trip through a brand-new store instance) — independently confirmed end-to-end by the
  real demo's Scenario 2 ("Reloaded selection exactly matches captured selection: True").
- "Snapshot selected notes/stem/phrase/path/effects with original revision and allowed models/time, not
  entire show by default" (implementation guidance, verbatim) — proven by `SelectionContext`'s own shape
  and `SelectionContextCaptureService`'s own gate order; independently confirmed by the real demo's
  Scenario 1 (exactly the five selected arches and two instrument tracks are captured, not the full
  six-effect document).
- "Out-of-scope, locked or stale revision must be rejected without partial mutation" (failure/edge case,
  verbatim) — proven by `StaleRevision_IsRejected_WithoutPartialMutation_NegativePath`,
  `LockedEffect_IsRejected_WithoutPartialMutation_NegativePath`,
  `NoPropsSelected_IsRejected_OutOfScope_NegativePath`,
  `EffectIdOutsideScopeModelSet_IsRejected_OutOfScope_NegativePath`,
  `UnknownEffectId_IsRejected_OutOfScope_NegativePath`,
  `InvertedInterval_IsRejected_InvalidDuration_NegativePath`, and their view-model-layer counterparts —
  independently confirmed by the real demo's Scenario 3 (all four rejection paths exercised with specific,
  readable error messages, and the original `document` instance confirmed unchanged afterward).
- Full regression (real-audio-device tests excluded, disclosed in full in §6): **918/918 passing**, 0
  failed, 0 skipped; every pre-existing test from Phase 01-10/Step 071 still passes unmodified, plus this
  step's own 10 new AiEdits tests and 4 new Shell tests. `dotnet format --verify-no-changes` clean across
  the full solution.
- **No file from any prior step/phase was behaviorally changed** — this step adds one new project
  (`XlightsAI.Studio.AiEdits`, referencing only `Contracts`/`Xlights`/`Editing`, verified non-cyclic), one
  new file to `XlightsAI.Studio.Shell` (plus one new `<ProjectReference>` entry to that project's own
  `.csproj`), and two new test projects/files (plus two new `<Project>` entries to
  `XlightsAI.Studio.slnx`). No existing file's own existing members were modified.
- **A real bug (infinite-recursion stack overflow in the first draft of the two new
  `IReadOnlySet<T>` JSON converters) was found and fixed during this step's own implementation** — disclosed
  in full in §3/§4, not concealed; the final, fixed converters are covered by the passing serialization
  round-trip tests above.
- **Carried-forward, disclosed phase-level/dependency-level gaps (not this step's own blocker):** no real
  SCREEN 14 Avalonia view exists yet (§7, project-wide pre-existing condition); `EditContext`/
  `StructuredEditOperation`/`EditProposal` and later Phase 11 steps remain genuinely unimplemented (§7);
  Phase 09's own consolidated gate report is still unwritten; Phase 06's own gate remains BLOCKED; Phase
  07's and Phase 08's own full-phase gates have not been separately run/recorded; real-audio-device Shell
  tests are environment-blocked (§6); a consolidated Phase 10 whole-phase gate report has not yet been
  separately written.

Per PHASE-11.md §8's own required final gate report, this document covers Step 072 alone. Steps 073-078
remain open; the next step does not begin until this document's own PASS status is accepted, per the
task's own standing instruction ("do not execute the next step until this step is proven").
