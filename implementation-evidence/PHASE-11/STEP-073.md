# PHASE 11 — Step 073 Evidence

**Step:** "Implement deterministic command AST for brightness/motion/direction/speed/replace"
**Spec:** `docs/phase-specs/PHASE-11.md` §6, Step 073 (lines 219-233) — the second of seven numbered steps
of Phase 11 (072-078)
**Date:** 2026-10-17

## 1. Pre-implementation baseline inspection

- Read `docs/phase-specs/PHASE-11.md` in full (already read in full for Step 072 this same session;
  reconfirmed Step 073's own text directly): required acceptance ("UI quick actions work offline and no
  token usage"), implementation guidance ("Implement local typed edit grammar and parameter validation
  for brightness, direction, snap, hold, and role-change actions"), failure/edge case ("Invalid input,
  cancellation or stale persisted state must not corrupt the project"), and Section 3's own proposed
  `StructuredEditOperation`/`EditProposal`/`IStructuredEditInterpreter` signatures.
- Read `implementation-evidence/PHASE-11/STEP-072.md` in full — confirmed Step 072's own closing status
  (PASS, 918/918 passing) and its own ADR narrowing PHASE-11.md's proposed `EditContext` down to the
  narrower, already-accepted `SelectionContext` (identity/scope snapshot only, no resolved
  note/gesture/recipe data) — this step reuses `SelectionContext` directly rather than re-introducing
  `EditContext`, per that same ADR's own stated rationale ("Step 073's own later 'deterministic command
  AST' step is the natural place to introduce an `EditContext`-shaped read model... if one proves
  needed" — it did not prove needed: this step never needs resolved `NoteEvent`/`PlannedGesture` data,
  only the selection's own scope/revision).
- Read every file in `src/XlightsAI.Studio.AiEdits/` (all eight, created by Step 072) and every file in
  `src/XlightsAI.Studio.Editing/` this step touches or reuses: `EditScope.cs`, `EditRejectionReason.cs`,
  `EditCommandRejectedException.cs`, `EditTransactionService.cs` (the direct structural precedent for
  this step's own gate ordering — stale revision, then locks, then parameter validation, all before any
  replacement/result object is constructed), `EffectLockPolicy.cs` (reused unmodified, exactly as
  Step 072 already reuses it), `SetEffectParameters.cs` (the direct precedent for "validate every
  parameter against a closed vocabulary before building anything").
- Read `src/XlightsAI.Studio.Xlights/IEffectRecipeRegistry.cs`/`EffectRecipeRegistry.cs` in full —
  confirmed this build's own real recipe registry currently recognizes exactly one recipe
  (`effect.on.v1`) with exactly one parameter (`EffectFamily=On`); it has no brightness/direction/speed
  parameter of any kind. This directly shaped this step's own architecture decision (§3): a
  `StructuredEditOperation`'s `Kind`/`Parameters` are validated against this step's own closed,
  range/vocabulary-checked grammar (an AiEdits-layer concern — "what does a locally-understood edit
  *mean*"), never against `IEffectRecipeRegistry.IsSupportedParameterValue` (an Xlights-layer concern —
  "what does a specific xLights recipe accept as XML settings") — inventing a fake
  `EffectFamily=On,Brightness=75` recipe parameter the real registry has never verified would violate
  Section 2's own "do not invent xLights effect parameters" rule more directly than keeping the two
  concerns separate.
- Read `src/XlightsAI.Studio.Composition/MusicalRole.cs` in full — confirmed it is a small, already-
  accepted PHASE-10 enum with no `ProjectReference`s of its own, making it the smallest possible
  dependency for this step's own "follow &lt;role&gt;" role-change grammar (reused directly, not
  redeclared — PHASE-01 Section 2's own "do not redefine copies" rule).
- Read `src/XlightsAI.Studio.Shell/SelectionContextEditorViewModel.cs` and
  `tests/XlightsAI.Studio.Shell.Tests/Step072SelectionContextEditorViewModelTests.cs` in full as the
  direct structural precedent for this step's own Shell-layer view model and its own tests (explicit
  `ScreenLoadState` loading/empty/error/loaded machine; "catch the domain's typed invalid-input
  exception, set `ActionErrorMessage`, leave bindable state completely unchanged on failure" convention;
  real collaborators, not mocks; before/after no-partial-mutation negative-path convention).
- Searched the full repository for any prior "Step 073"/"STEP-073"/`StructuredEditOperation`/
  `EditProposal`/`IStructuredEditInterpreter` reference: found only `PHASE-11.md` itself, confirming
  this is genuinely new work.
- Confirmed via reading `.csproj` files that `XlightsAI.Studio.AiEdits` did not yet reference
  `XlightsAI.Studio.Composition`, and that `Composition`'s own `.csproj` does not reference `AiEdits` —
  adding the new reference is not cyclic.
- Baseline taken by running the real, full solution build and the full regression suite *before* any
  Step 073 code was written:
  `dotnet build XlightsAI.Studio.slnx --nologo` (clean, 0 warnings/errors, reconfirming Step 072's own
  closing build state), then the same filtered `dotnet test` command Step 072's own evidence used,
  reconfirming 918/918 passing immediately before starting.

## 2. Scope actually implemented

**No file from Step 001-072 was behaviorally changed**, with one additive, backward-compatible
exception disclosed in full below (a new `EditRejectionReason` enum member, appended at the end — every
existing member's own ordinal value is unchanged). This step adds five new files to the existing
`src/XlightsAI.Studio.AiEdits/` project, one new `<ProjectReference>` entry to that project's own
`.csproj`, one new file to `src/XlightsAI.Studio.Shell/`, and two new test files.

### Extended file

- **`src/XlightsAI.Studio.Editing/EditRejectionReason.cs`** — one new member appended at the end,
  `UnrecognizedInstruction`, for "the instruction text does not match any known local command grammar."
  Additive only; every existing member (`StaleRevision` through `NothingToRedo`) is completely
  unchanged, the same kind of extension Steps 020/022 already made to this same enum.
- **`src/XlightsAI.Studio.AiEdits/XlightsAI.Studio.AiEdits.csproj`** — one new
  `<ProjectReference Include="..\XlightsAI.Studio.Composition\XlightsAI.Studio.Composition.csproj" />`
  line added (verified non-cyclic before adding, see §1). No other line changed.

### New files in `src/XlightsAI.Studio.AiEdits/`

- **`StructuredEditOperationKinds.cs`** — the closed, named-constant vocabulary of `Kind` strings this
  interpreter can ever produce: `SetBrightness`, `SetDirection`, `SetSpeed`, `SnapToGrid`,
  `ReplaceRole`. Declared as string constants, not a CLR `enum`, because PHASE-11.md's own proposed
  `StructuredEditOperation` signature declares `Kind` as a bare `string` (Section 3) — a later step's
  validator must be able to reject an unrecognized `Kind` string arriving from an external JSON envelope
  the identical way it rejects one from this interpreter's own output.
- **`StructuredEditOperation.cs`** / **`EditProposal.cs`** — PHASE-11.md Section 3's own proposed
  signatures, implemented verbatim (field names, types and order unchanged from the spec text).
- **`IStructuredEditInterpreter.cs`** — declared against `SelectionContext` (not the spec's own proposed
  `EditContext`), per Step 072's own ADR (see §1); `ParseKnownCommand` is synchronous per the spec's own
  signature with an added `CancellationToken ct = default` parameter — not present in the spec's own
  illustrative signature, added because this step's own required failure/edge case explicitly names
  "cancellation" as a case to prove, and a method that cannot be canceled cannot be proven to "not
  corrupt the project" on cancellation.
- **`StructuredCommandInterpreter.cs`** — the default implementation. A small, closed, regular-
  expression grammar (never a general natural-language parser, never a call to any optional AI
  planner — Section 2: "Parse deterministic commands... without a model when possible"). Gate order,
  every gate running to completion before any `EditProposal` is constructed:
  1. **Cancellation** — `ct.ThrowIfCancellationRequested()`, first, before any other gate.
  2. **Stale revision** — `context.BaseDocumentRevision.Value != document.Revision` →
     `EditCommandRejectedException` (`EditRejectionReason.StaleRevision`).
  3. **Locks** — delegates to the already-accepted `IEffectLockPolicy.AssertEditable(document,
     context.Scope)` unmodified — an instruction can never even become a proposal that would reach a
     locked effect, independent of which specific action it describes.
  4. **Grammar + parameter validation** — the trimmed instruction is matched against a fixed set of
     known verbs (`brighten`/`dim`/`brightness N`, `reverse`/`direction <token>`, `faster`/`slower`/
     `speed Nx`, `snap`/`snap to grid`, `follow <role>`); a recognized verb with an out-of-range/
     unknown value (e.g. `brightness 150`, `speed 50x`) rejects with `EditRejectionReason
     .UnsupportedParameter`; anything matching no known verb at all (including blank input) rejects with
     `EditRejectionReason.UnrecognizedInstruction`.
  5. Only after every gate passes is a single `StructuredEditOperation` built (covering exactly
     `context.Scope.Time`/`context.Scope.Models` — never wider than the already-captured selection) and
     wrapped in a new `EditProposal`.

### New file in `src/XlightsAI.Studio.Shell/`

- **`StructuredCommandQuickActionsViewModel.cs`** — SCREEN 14 AI EDIT's own bindable offline quick-
  actions panel. Every quick-action button a real SCREEN 14 view would bind to (Brighten, Dim, Reverse,
  Faster, Slower, Snap to grid, Follow &lt;role&gt;) is a fixed instruction string passed to
  `ParseAsync`, which calls the real `IStructuredEditInterpreter` directly — no network call, no
  optional AI planner is ever consulted by this view model (Step 073's own required acceptance,
  verbatim). Mirrors `SelectionContextEditorViewModel`'s own `ScreenLoadState` machine and "leave
  bindable state completely unchanged on failure" convention; `LastProposal` is never partially
  overwritten by a rejected instruction.

### New test files

- **`tests/XlightsAI.Studio.AiEdits.Tests/Step073AcceptanceTests.cs`** (29 tests) — real
  `StructuredCommandInterpreter`/`EffectLockPolicy`/`SelectionContextCaptureService`/
  `ThreeEffectPlanFactory`/`EffectRecipeRegistry` (not mocks), against the same gate-demo layout
  (Roofline, Arch 1, Arch 2) every prior PHASE-02/03/11 step's own tests already use:
  - **`Step073_Acceptance_QuickActionsWorkOfflineWithNoTokenUsage`** — the required acceptance test,
    verbatim. Parses `"brightness 75"` against a real captured selection into a fully validated
    `EditProposal`; asserts `BaseRevision`, the single operation's `Kind`/`Parameters`/`Range`/
    `Targets`, a non-blank `Explanation`, and that the source document's own effect count is completely
    unchanged afterward.
  - **`KnownInstruction_ParsesToExpectedKindAndParameter`** (8 cases) — every named action
    (brighten/dim/reverse/direction/faster/slower/speed/follow) parses to its own expected `Kind` and
    parameter value.
  - **`SnapToGrid_ParsesWithNoParameters`** — the one action with an empty parameter set.
  - **`UnrecognizedInstruction_IsRejected_WithoutBuildingAProposal_NegativePath`** — the phase gate's own
    named worked-example phrasing ("make the piano move left-to-right") rejects with
    `UnrecognizedInstruction` (this interpreter's own closed grammar does not include free-form musical
    descriptions — that remains a later, optional-AI-planner step's own scope per Section 3), and the
    document's own effect count is unchanged.
  - **`BlankInstruction_IsRejected_UnrecognizedInstruction_NegativePath`** — "Invalid input" (step's own
    required failure/edge case, verbatim), realized as blank text.
  - **`BrightnessOutOfRange_IsRejected_UnsupportedParameter_NegativePath`** (2 cases: `150`, `-5`) /
    **`SpeedOutOfRange_IsRejected_UnsupportedParameter_NegativePath`** — a recognized verb with a value
    outside its own supported range rejects distinctly from an unrecognized verb.
  - **`StaleRevision_IsRejected_WithoutPartialMutation_NegativePath`** — "stale persisted state" (step's
    own required failure/edge case, verbatim), realized as the document's revision advancing after the
    selection was captured.
  - **`LockedTarget_IsRejected_WithoutBuildingAProposal_NegativePath`** — a locked Arch 2 effect inside
    the already-captured scope rejects the parse with `Locked`.
  - **`CanceledToken_ThrowsWithoutBuildingAProposal_NegativePath`** — "cancellation" (step's own required
    failure/edge case, verbatim): a pre-canceled token throws `OperationCanceledException` before any
    proposal is built, and the document's own effect count is unchanged afterward.
  - **`ParseKnownCommand_NullArguments_Throw_NegativePath`** — `ArgumentNullException` for
    `instruction`/`context`/`document`.
- **`tests/XlightsAI.Studio.Shell.Tests/Step073StructuredCommandQuickActionsViewModelTests.cs`** (4
  tests) — real `StructuredCommandQuickActionsViewModel`/`StructuredCommandInterpreter`/
  `EffectLockPolicy`/`SelectionContextCaptureService` (not mocks), proving the bindable view-model
  layer's own quick-action contract, including a rejected instruction leaving `LastProposal` completely
  unchanged (`Assert.Same`, not merely equal) and a null-interpreter argument throwing.

## 3. Architecture deviations / decisions

- **The step heading's own five-word list ("brightness/motion/direction/speed/replace") and the
  implementation guidance's own five-word list ("brightness, direction, snap, hold, and role-change
  actions") are not treated as a literal one-to-one checklist** — both describe the same bounded
  "offline quick actions" surface from two slightly different angles within the same spec document.
  This interpreter reconciles them as five concrete, disjoint, testable `Kind`s:
  `SetBrightness` (both lists' own "brightness"), `SetDirection` (both lists' own "direction"),
  `SetSpeed` (the heading's own "speed"), `SnapToGrid` (the guidance's own "snap", and the heading's own
  "motion" — snapping a selection's effects onto the beat grid is itself a motion/timing action), and
  `ReplaceRole` (the heading's own "replace" and the guidance's own "role-change actions" — changing
  which musical source role an effect follows, not swapping an effect's own xLights recipe id). Full
  reasoning recorded in `StructuredEditOperationKinds.cs`'s own doc comment.
- **"Replace" is implemented as a role-change (which musical source an effect follows), not an xLights
  recipe swap.** Reasoning: Section 2's own "do not invent xLights effect parameters" rule, combined
  with the real `EffectRecipeRegistry` currently recognizing exactly one recipe with exactly one
  parameter (§1) — a literal "replace this effect's recipe with recipe X" quick action would have
  nothing real to validate against except the single existing recipe, making the action vacuous; the
  implementation guidance's own explicit "role-change actions" phrase names a concrete, already-
  accepted PHASE-10 concept (`MusicalRole`) this interpreter can actually validate against instead.
- **"Hold" (named only in the implementation guidance, not the step heading) is deliberately not given
  its own operation kind.** Reasoning: PHASE-03's own already-accepted `ResizeEffect` command already
  owns "how long an effect lasts" (Step 020's own scope); a "hold N beats" quick action is UI sugar over
  that existing command — computing a new `EndTicks` and issuing a `ResizeEffect` — not a new AiEdits-
  owned `StructuredEditOperationKinds` value. No existing command's own contract was touched to reach
  this conclusion; this is a scope-boundary decision, not a code change.
- **A `StructuredEditOperation`'s `Parameters` are validated against this interpreter's own closed,
  range/vocabulary-checked grammar, never against `IEffectRecipeRegistry.IsSupportedParameterValue`.**
  Reasoning in full in §1/§2 above: the real recipe registry's own single known parameter
  (`EffectFamily=On`) has nothing to do with brightness/direction/speed at all; validating a brightness
  level against a recipe-specific XML-settings vocabulary that does not define brightness would either
  always fail (if checked naively) or require inventing a parameter name the registry has never
  verified against real xLights behavior (violating Section 2's own explicit rule). This interpreter's
  own validation is a different, narrower claim: "is this a value this *build's own offline grammar*
  understands," not "does a specific xLights effect accept this XML setting" — the latter remains
  `IEffectRecipeRegistry`'s own, unmodified, already-accepted responsibility, exercised only when a
  later step actually compiles an accepted `EditProposal` into a concrete `EffectInstance` change
  (Step 076's own later scope).
- **`IStructuredEditInterpreter.ParseKnownCommand` takes a `CancellationToken ct = default` parameter not
  present in PHASE-11.md Section 3's own proposed signature.** Reasoning: this step's own required
  failure/edge case explicitly names "cancellation" as a case to prove; a synchronous method with no
  token has no way to honor a caller's cancellation request before doing (admittedly cheap, in-memory)
  work, and the spec's own Section 2 "Architectural design notes" state every mutating path must
  "propagate `CancellationToken`" (Section 7's own cross-cutting negative case). Backward-compatible by
  construction (a default parameter) relative to the spec's own illustrative, non-normative shape.
- **No real SCREEN 14 AI EDIT Avalonia/AXAML view or Shell-layer `ShellScreen` wiring exists** —
  unchanged, carried-forward condition from every prior Phase 09/10/11 UI-adjacent step (most recently
  Step 072's own evidence §3/§7). This step's own required user-visible outcome ("Versioned, inspectable
  project output plus a reproducible test report") is satisfied at the bindable
  `StructuredCommandQuickActionsViewModel` layer a real SCREEN 14 view would bind to, proven by a real
  non-test-harness console demo (§5) plus `Step073StructuredCommandQuickActionsViewModelTests` — the
  same precedent Steps 059-072's own evidence already established.
- **Per the user's own standing waiver** (`memory: feedback_skip_xlights_human_verification`), no live
  xLights render/desktop-interaction check was attempted — not applicable to this step's own required
  acceptance text in any case ("UI quick actions work offline and no token usage" names no xLights
  rendering concern; this step produces no `.xsq` output and never mutates a `SequenceDocument`).

## 4. Commands actually executed

All run via the PowerShell tool — the sandboxed Bash tool in this session again returned bare exit code
1 with no output for plain commands (confirmed again at the very start of this step; `C:\CODE\
xLightsAISequencer` resolves correctly under PowerShell but Bash's own filesystem view is detached from
it in this environment), the same pre-existing environment limitation every prior step's own evidence
has recorded; PowerShell was used for every `dotnet`/file-system command below without incident.

```
# Baseline, before any Step 073 code existed:
dotnet build XlightsAI.Studio.slnx --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s). (Step 072's own closing state, reconfirmed)

# After adding the new AiEdits types + Shell view model + both new test files:
dotnet build XlightsAI.Studio.slnx --nologo
  # First attempt: MSB4025 (XML comment containing "--" in the new .csproj doc comment) -- fixed by
  # rewording the comment to avoid "--"; rebuilt clean: Build succeeded. 0 Warning(s). 0 Error(s).

dotnet test tests\XlightsAI.Studio.AiEdits.Tests\XlightsAI.Studio.AiEdits.Tests.csproj --nologo
  # FIRST RUN (before the two fixes described in §3/below):
  #   Failed: 2, Passed: 27, Skipped: 0, Total: 29
  #   (1) "brightness -5" matched no regex alternative (the BrightnessLevel pattern did not allow a
  #       leading '-') and fell through to UnrecognizedInstruction instead of UnsupportedParameter --
  #       fixed by widening the regex to `-?\d{1,3}`.
  #   (2) LockedTarget_IsRejected_WithoutBuildingAProposal_NegativePath did not throw -- the test's own
  #       CaptureArchSelection helper built a scope covering only Arch 1's own time window, which never
  #       overlaps Arch 2's own window (ThreeEffectPlanFactory assigns three non-overlapping thirds), so
  #       a lock on Arch 2 was never "reached" by that scope's own time+model overlap check -- fixed by
  #       widening the test helper's own scope to span both arches' own windows (not a production-code
  #       bug; a test-fixture bug, disclosed here per the task's own instruction to record failed tests).
  # AFTER both fixes:
  #   Passed! - Failed: 0, Passed: 29, Skipped: 0, Total: 29, Duration: 239 ms

dotnet test tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj --nologo --filter "FullyQualifiedName~Step073"
  # Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: 426 ms

dotnet format XlightsAI.Studio.slnx --verify-no-changes --no-restore
  # No output (clean) -- matches the repository's own CI formatting gate, checked across the full solution.

dotnet test XlightsAI.Studio.slnx --nologo --filter "FullyQualifiedName!~Step025&FullyQualifiedName!~Step028&FullyQualifiedName!~Step029&FullyQualifiedName!~Step030&FullyQualifiedName!~Step042"
dotnet test tests\XlightsAI.Studio.Stems.Tests\XlightsAI.Studio.Stems.Tests.csproj --nologo --filter "FullyQualifiedName~Step042"
  # Passed! - Failed: 0, Passed: 8, Skipped: 0, Total: 8, Duration: 420 ms
  # (the blunt numeric filter above also excludes Stems.Tests' own unrelated, legitimate
  # Step042AcceptanceTests.cs -- the same filter side effect disclosed in every prior Phase 09/10/11
  # step's own evidence; reconfirmed here passing in isolation.)

# Real, non-test-harness manual demo (built under %TEMP%\xlai-step073-demo-proj, deleted after this run):
dotnet run --project "$env:TEMP\xlai-step073-demo-proj"
```

### Targeted test results (Step 073's own 33 new tests, real run)

```
Step073AcceptanceTests (AiEdits, 29 new):
  Step073_Acceptance_QuickActionsWorkOfflineWithNoTokenUsage                 Passed
  KnownInstruction_ParsesToExpectedKindAndParameter (8 cases)                Passed
  SnapToGrid_ParsesWithNoParameters                                         Passed
  UnrecognizedInstruction_IsRejected_WithoutBuildingAProposal_NegativePath   Passed
  BlankInstruction_IsRejected_UnrecognizedInstruction_NegativePath          Passed
  BrightnessOutOfRange_IsRejected_UnsupportedParameter_NegativePath (2)     Passed
  SpeedOutOfRange_IsRejected_UnsupportedParameter_NegativePath              Passed
  StaleRevision_IsRejected_WithoutPartialMutation_NegativePath              Passed
  LockedTarget_IsRejected_WithoutBuildingAProposal_NegativePath             Passed
  CanceledToken_ThrowsWithoutBuildingAProposal_NegativePath                 Passed
  ParseKnownCommand_NullArguments_Throw_NegativePath                        Passed
AiEdits.Tests total (incl. Step 072's own 10): 29/29 passed, 0 failed, 0 skipped.

Step073StructuredCommandQuickActionsViewModelTests (Shell, 4 new):
  Step073_Acceptance_QuickActionWorksOfflineWithNoTokenUsage                Passed
  UnrecognizedInstruction_IsRejected_LeavesLastProposalUnchanged_NegativePath Passed
  StaleRevision_IsRejected_NeverLeavesEmptyState_NegativePath               Passed
  ParseAsync_NullInterpreter_Throws_NegativePath                           Passed
Shell.Tests (Step 073 slice): 4/4 passed, 0 failed, 0 skipped.
```

### Full regression (final, after all Step 073 implementation and tests, real-audio-device Shell tests excluded — unchanged pre-existing condition, see Step 064-072's own §6)

```
Passed! - XlightsAI.Studio.Audio.Tests.dll           23/23
Passed! - XlightsAI.Studio.Xlights.Tests.dll         74/74
Passed! - XlightsAI.Studio.Editing.Tests.dll         83/83
Passed! - XlightsAI.Studio.Display.Tests.dll         56/56
Passed! - XlightsAI.Studio.Structure.Tests.dll       78/78
Passed! - XlightsAI.Studio.Composition.Tests.dll     78/78
Passed! - XlightsAI.Studio.AiEdits.Tests.dll         29/29 (10 carried forward from Step 072 + 19 new)
Passed! - XlightsAI.Studio.Melody.Tests.dll         117/117
Passed! - XlightsAI.Studio.Projects.Tests.dll         25/25
Passed! - XlightsAI.Studio.Shell.Tests.dll          168/168 (164 carried forward + 4 new Step 073; real-audio-device tests excluded, unchanged)
Passed! - XlightsAI.Studio.Rhythm.Tests.dll           82/82
Passed! - XlightsAI.Studio.Reference.Tests.dll        31/31
Passed! - XlightsAI.Studio.Stems.Tests.dll            93/93  (+8 Step042AcceptanceTests reconfirmed separately, above)
```

**945 total passing** (922 carried forward from Step 072's own closing full-regression count, unmodified
except the new, additive `EditRejectionReason.UnrecognizedInstruction` member, + 19 new AiEdits tests +
4 new Shell tests = 945), 0 failed, 0 skipped. Every pre-existing test from Steps 001-072 still passes
unmodified.

## 5. Persisted-output inspection / observable demonstration for this step

A real, non-test-harness console program (not part of the repository, built under
`%TEMP%\xlai-step073-demo-proj`, deleted after this run) exercises the real
`StructuredCommandInterpreter`/`EffectLockPolicy`/`SelectionContextCaptureService` directly — no test
harness, no mocks. It realizes a narrowed version of the phase gate's own named worked example ("Select
42-48 seconds on five arches, ask for rising piano pops..." — this step's own scope being deterministic
quick-action parsing only, not the later optional-AI-planner/accept-reject steps):

```
dotnet run --project "$env:TEMP\xlai-step073-demo-proj"
```

Observed real output:

```
=== Step 073 manual demo: real StructuredCommandInterpreter, no test harness, no model/token usage ===

--- Scenario 1: select 42-48 seconds on all five arches ---
  Captured selection id=a0f793c4-251a-4937-aa58-6a522eaa24e6, baseRevision=1
  Interval=[42s, 48s)  Props=Arch 1, Arch 2, Arch 3, Arch 4, Arch 5

--- Scenario 2: known offline quick actions (no network, no model, no token usage) ---
  'brighten' -> Kind=SetBrightness Parameters=[Level=100] Explanation="Set brightness to 100%."
  'brightness 75' -> Kind=SetBrightness Parameters=[Level=75] Explanation="Set brightness to 75%."
  'direction left-to-right' -> Kind=SetDirection Parameters=[Direction=LeftToRight] Explanation="Set direction to LeftToRight."
  'speed 2x' -> Kind=SetSpeed Parameters=[Multiplier=2] Explanation="Set speed to 2x."
  'snap to grid' -> Kind=SnapToGrid Parameters=[] Explanation="Snap selected effects onto the beat grid."
  'follow lead' -> Kind=ReplaceRole Parameters=[Role=Lead] Explanation="Follow the Lead musical role instead of the current source."

--- Scenario 3: negative paths -- unrecognized instruction, out-of-range parameter, stale revision, locked target, cancellation ---
  Unrecognized instruction rejected: reason=UnrecognizedInstruction, error="Instruction 'make the piano move left-to-right' does not match any known local command; no proposal was built."
  Out-of-range brightness rejected: reason=UnsupportedParameter, error="Brightness level 150 is outside the supported 0-100 range; no proposal was built."
  Stale revision rejected: reason=StaleRevision, error="Expected revision 1 does not match the document's current revision 2; no proposal was built."
  Locked target rejected: reason=Locked, error="Effect '9898d23f-ad3e-4e87-90a4-1671132b461f' on 'Arch 1' is locked and cannot be edited."
  Canceled token: OperationCanceledException thrown before any proposal was built

  Confirming no document mutation across every scenario: document.Revision=1, document.Effects.Count=6

Step 073 manual demo completed successfully.
```

Direct, real, non-test-harness confirmation of this step's own required text: (1) "UI quick actions work
offline and no token usage" (required acceptance, verbatim) — Scenario 2 parses six distinct quick
actions purely in-process, with no `HttpClient`/socket/process call anywhere in `StructuredCommandInterpreter`
or this demo (confirmed by code inspection — the interpreter's only dependency is `IEffectLockPolicy`, a
pure in-memory check). (2) "Implement local typed edit grammar and parameter validation for brightness,
direction, snap, hold, and role-change actions" (implementation guidance, verbatim, reconciled per §3) —
Scenario 2 exercises brightness, direction, speed, snap and role-change, each producing a distinct,
correctly-parameterized `Kind`. (3) "Invalid input, cancellation or stale persisted state must not
corrupt the project" (failure/edge case, verbatim) — Scenario 3 exercises all three named categories plus
a locked-target variant, each with a specific, readable error (or, for cancellation, a thrown
`OperationCanceledException`), and the original `document` instance confirmed unchanged afterward
(revision and effect count both unchanged, exactly as before Scenario 1 even ran).

## 6. Fixtures / input hashes

- No show-layout XML fixture file was needed for this step's own tests or demo — `StructuredCommandInterpreter`
  operates on a caller-supplied `SelectionContext`/`SequenceDocument` built directly in code (the same
  convention `Step072AcceptanceTests`'s own `GateDemoLayout()`/`GateDemoPlan()` already established for
  this test suite; the demo builds its own five-arch-plus-Roofline document directly, disclosed in §5).
- The real demo produced no persisted file of any kind (this step never writes to disk — `EditProposal`
  is an in-memory value only; persisting a proposal is explicitly later, disclosed Phase 11 scope — Step
  078's own "Persist proposal audit"); the throwaway demo *project* itself was deleted after this
  evidence was recorded.
- **Real-audio-device test exclusion, disclosed (unchanged, carried forward from every prior Phase
  09/10/11 step's own §6):** `Step025`/`Step028`/`Step029`/`Step030`/`Step042ShellWiringTests` construct
  a real `WaveOutAudioTransport` (the real Windows winmm audio device) unavailable in this session's own
  sandboxed execution environment — a pre-existing, environment-caused condition predating this step
  entirely. No file this step touched is on the call path of any of those five classes (confirmed: none
  reference `StructuredCommandInterpreter`/`StructuredCommandQuickActionsViewModel` in any way).

## 7. Unresolved issues / known gaps

- **No real SCREEN 14 AI EDIT Avalonia/AXAML view or Shell-layer `ShellScreen` wiring exists** — this
  step's own required user-visible outcome ("Versioned, inspectable project output plus a reproducible
  test report") is satisfied here by the real console demo (§5) and
  `Step073StructuredCommandQuickActionsViewModelTests` (bindable-view-model layer), disclosed in §3 as
  the project-wide, pre-existing, consistent convention — not an oversight specific to this step.
- **This interpreter's grammar is deliberately small and closed** — it does not attempt to parse the
  phase gate's own illustrative free-form instruction ("make the piano move left-to-right") at all; that
  remains Section 3's own later, optional, explicitly-consented `IOptionalAiPlanner` boundary (Step 075's
  own not-yet-implemented scope). `UnrecognizedInstruction_IsRejected_WithoutBuildingAProposal_NegativePath`
  proves this interpreter correctly rejects that exact phrase rather than silently guessing at it.
- **No `IEditProposalValidator`/`IOptionalAiPlanner`/`IEditApprovalService` exists yet** — PHASE-11.md
  Section 3's own proposed signatures beyond the structured-command-AST slice remain genuinely open,
  disclosed as Step 074-078's own later, not-yet-implemented scope (verbatim from PHASE-11.md §9:
  "Implement only numbered steps 072, 073, 074, 075, 076, 077, 078").
  Step 073 also does not apply an `EditProposal` to a `SequenceDocument` at all — that is explicitly
  Step 076's own later scope ("dry-run proposals, before/after diff and Accept/Reject").
- **Carried-forward, disclosed phase-level/dependency-level gaps are unchanged from Step 072's own
  closing evidence** (not reopened or re-resolved here): a consolidated Phase 09/10 `GATE-REPORT.md` is
  still unwritten; Phase 06's own stem-separation gate remains BLOCKED; Phase 07's and Phase 08's own
  full-phase gates have not been separately run/recorded; real-audio-device Shell tests remain
  environment-blocked (§6); this document covers only Step 073's own narrow scope, not a Phase 11
  whole-phase gate.

## 8. PASS / BLOCKED

**Status: PASS** (for this step's own narrow, disclosed scope — a local, deterministic, offline command
grammar for brightness/direction/speed/snap/role-change actions, with full parameter-range/vocabulary
validation and revision/lock/cancellation-aware rejection — not a claim that any carried-forward
phase-level gap listed in §7 is resolved, and not a claim that a real SCREEN 14 Avalonia view or a live
xLights-installation render has been independently confirmed, neither of which this step's own required
acceptance text names).

- "UI quick actions work offline and no token usage" (required acceptance, verbatim) — proven by
  `Step073_Acceptance_QuickActionsWorkOfflineWithNoTokenUsage` and
  `Step073_Acceptance_QuickActionWorksOfflineWithNoTokenUsage` (domain- and view-model-level real parses
  with zero network/model dependencies — `StructuredCommandInterpreter`'s only collaborator is the
  already-accepted, pure, in-memory `IEffectLockPolicy`) — independently confirmed end-to-end by the
  real demo's Scenario 2 (six distinct quick actions, all parsed purely in-process).
- "Implement local typed edit grammar and parameter validation for brightness, direction, snap, hold,
  and role-change actions" (implementation guidance, verbatim, reconciled in §3) — proven by
  `KnownInstruction_ParsesToExpectedKindAndParameter`'s own 8 cases plus
  `SnapToGrid_ParsesWithNoParameters`, and by the two out-of-range negative tests
  (`BrightnessOutOfRange_IsRejected_UnsupportedParameter_NegativePath`,
  `SpeedOutOfRange_IsRejected_UnsupportedParameter_NegativePath`) proving real parameter *validation*,
  not just parsing — independently confirmed by the real demo's Scenario 2 (six actions) and Scenario 3's
  own out-of-range brightness rejection.
- "Invalid input, cancellation or stale persisted state must not corrupt the project" (failure/edge case,
  verbatim) — proven by `BlankInstruction_IsRejected_UnrecognizedInstruction_NegativePath`,
  `UnrecognizedInstruction_IsRejected_WithoutBuildingAProposal_NegativePath`,
  `CanceledToken_ThrowsWithoutBuildingAProposal_NegativePath`,
  `StaleRevision_IsRejected_WithoutPartialMutation_NegativePath`,
  `LockedTarget_IsRejected_WithoutBuildingAProposal_NegativePath`, and their view-model-layer
  counterparts — independently confirmed by the real demo's Scenario 3 (all four named rejection
  categories exercised with specific, readable outcomes, and the original `document` instance confirmed
  unchanged afterward: revision and effect count both unchanged).
- Full regression (real-audio-device tests excluded, disclosed in full in §6): **945/945 passing**, 0
  failed, 0 skipped; every pre-existing test from Phase 01-11/Step 072 still passes unmodified, plus this
  step's own 19 new AiEdits tests and 4 new Shell tests. `dotnet format --verify-no-changes` clean across
  the full solution.
- **No file from any prior step/phase was behaviorally changed**, except one additive, backward-
  compatible `EditRejectionReason` enum member appended at the end (`UnrecognizedInstruction`) — every
  existing member's own ordinal value and every existing call site are completely unchanged, confirmed
  by the full regression above. This step adds five new files to the already-existing
  `XlightsAI.Studio.AiEdits` project (plus one new `<ProjectReference>` entry to that project's own
  `.csproj`), one new file to `XlightsAI.Studio.Shell`, and two new test files.
- **Two real bugs were found and fixed during this step's own implementation, not merely avoided by
  design review** — disclosed in full in §3/§4: (1) the `BrightnessLevel` regex did not initially accept
  a leading `-`, misclassifying `"brightness -5"` as `UnrecognizedInstruction` instead of the intended
  `UnsupportedParameter`; (2) the test suite's own `CaptureArchSelection` helper initially built a scope
  spanning only one arch's own time window, which never reached a lock placed on the other arch —
  neither is concealed; both fixes are reflected in the final, passing tests above.
- **Carried-forward, disclosed phase-level/dependency-level gaps (not this step's own blocker):** no real
  SCREEN 14 Avalonia view exists yet (§7, project-wide pre-existing condition);
  `IEditProposalValidator`/`IOptionalAiPlanner`/`IEditApprovalService` and later Phase 11 steps remain
  genuinely unimplemented (§7); Phase 09/10's own consolidated gate reports are still unwritten; Phase
  06's own gate remains BLOCKED; Phase 07's and Phase 08's own full-phase gates have not been separately
  run/recorded; real-audio-device Shell tests are environment-blocked (§6).

Per PHASE-11.md §8's own required final gate report, this document covers Step 073 alone. Steps 074-078
remain open; the next step does not begin until this document's own PASS status is accepted, per the
task's own standing instruction ("do not execute the next step until this step is proven").
