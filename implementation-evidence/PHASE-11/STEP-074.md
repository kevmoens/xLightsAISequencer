# PHASE 11 — Step 074 Evidence

**Step:** "Add edit request panel, suggestions and change explanation"
**Spec:** `docs/phase-specs/PHASE-11.md` §6, Step 074 (lines 235-249) — the third of seven numbered steps
of Phase 11 (072-078)
**Date:** 2026-10-17

## 1. Pre-implementation baseline inspection

- Read `docs/phase-specs/PHASE-11.md` in full (previously read in full for Steps 072/073 this same
  working context; reconfirmed Step 074's own text directly): required acceptance ("Request always
  displays target context"), implementation guidance ("Create selection-summary drawer listing affected
  props and exact time range and AI text input; do not send until explicit action"), failure/edge case
  ("Out-of-scope, locked or stale revision must be rejected without partial mutation"), and Section 3's
  own proposed `EditContext`/`EditProposal`/`IOptionalAiPlanner` signatures (not yet implemented; this
  step's own scope is the deterministic/offline request surface only — see §3 below).
- Read `implementation-evidence/PHASE-11/STEP-072.md` and `STEP-073.md` in full — confirmed both steps'
  own closing status (PASS; 918/918 then 945/945 passing) and Step 073's own ADR (reuse
  `SelectionContext`, not the spec's larger proposed `EditContext`).
- Read every file in `src/XlightsAI.Studio.AiEdits/` (all thirteen, from Steps 072/073):
  `SelectionContext.cs`, `StructuredEditOperation.cs`, `EditProposal.cs`, `IStructuredEditInterpreter.cs`,
  `StructuredCommandInterpreter.cs`, `StructuredEditOperationKinds.cs` — confirmed `EditProposal` already
  carries a non-blank `Explanation` field for every successfully parsed instruction (Step 073's own
  signature, unchanged), so "change explanation" (this step's own heading, verbatim) needs no new domain
  field — only a UI-facing projection over the field that already exists.
- Read `src/XlightsAI.Studio.Shell/SelectionContextEditorViewModel.cs` and
  `StructuredCommandQuickActionsViewModel.cs`, and their own test files
  (`Step072SelectionContextEditorViewModelTests.cs`, `Step073StructuredCommandQuickActionsViewModelTests.cs`)
  in full — the direct structural precedent for this step's own Shell-layer view model: explicit
  `ScreenLoadState` loading/empty/error/loaded machine; "catch the domain's typed invalid-input
  exception, set `ActionErrorMessage`, leave bindable state completely unchanged on failure" convention;
  real collaborators (`StructuredCommandInterpreter`/`EffectLockPolicy`/`SelectionContextCaptureService`),
  never mocks.
- Read `src/XlightsAI.Studio.Editing/EditScope.cs` and `src/XlightsAI.Studio.Contracts/Identifiers.cs` in
  full — confirmed `TimeWindow`'s half-open `[StartTicks, EndTicks)` shape and the project-wide
  `10_000_000` ticks-per-second convention already used identically by `SongAsset`, `BarFingerprintBuilder`,
  `VisualBudgetResolver`, `WaveformBarLayoutBuilder` and others — reused directly for this step's own
  target-context time formatting rather than inventing a new conversion constant.
- Searched the full repository for any prior "Step 074"/"STEP-074"/`AiEditRequestPanelViewModel` reference:
  found only `PHASE-11.md` itself, confirming this is genuinely new work.
- Confirmed via reading `XlightsAI.Studio.Shell.csproj` that it already references
  `XlightsAI.Studio.AiEdits` (added for Step 073's `StructuredCommandQuickActionsViewModel`) — no new
  `<ProjectReference>` is needed for this step.
- Baseline taken by running the real, full solution build and the full regression suite *before* any
  Step 074 code was written: `dotnet build XlightsAI.Studio.slnx --nologo` (clean, 0 warnings/errors,
  reconfirming Step 073's own closing build state), then the same filtered `dotnet test` command Step
  073's own evidence used, reconfirming 945/945 passing immediately before starting.

## 2. Scope actually implemented

**No file from Steps 001-073 was changed at all** — this step is purely additive: one new file in
`src/XlightsAI.Studio.Shell/`, and one new test file in `tests/XlightsAI.Studio.Shell.Tests/`. No new
`XlightsAI.Studio.AiEdits` domain type was needed (see §3 — "change explanation" and "suggestions" are
UI-facing projections over Step 073's own already-accepted `EditProposal.Explanation` and
`StructuredCommandInterpreter`'s own already-accepted closed grammar, not new domain concepts).

### New file in `src/XlightsAI.Studio.Shell/`

- **`AiEditRequestPanelViewModel.cs`** — SCREEN 14 AI EDIT's own bindable selection-summary drawer plus
  free-text instruction request, implementation guidance realized verbatim ("Create selection-summary
  drawer listing affected props and exact time range and AI text input; do not send until explicit
  action"):
  - `TargetSummary` — populated immediately and unconditionally by `AttachContext(SelectionContext)`,
    formatted as `"<sorted prop list> @ [<start>s, <end>s)"` (half-open interval notation, matching
    PHASE-01 Section 2's own canonical `[Start, End)` convention) — this is the "Request always displays
    target context" required acceptance, realized as a property that is populated the instant a context
    is attached, independent of `InstructionText` or any submit attempt.
  - `Suggestions` — a fixed, closed `static` list of instruction strings drawn directly from
    `StructuredCommandInterpreter`'s own known grammar (Step 073) — never a free-form or AI-generated
    suggestion list, consistent with Section 2's own "deterministic commands... without a model when
    possible" rule.
  - `InstructionText` — a plain bindable `get`/`set` property with **no** side effect in its setter; only
    `SubmitAsync` ever reaches the interpreter — the literal realization of "do not send until explicit
    action."
  - `SubmitAsync(IStructuredEditInterpreter, SequenceDocument, CancellationToken)` — the one explicit
    "send" action. Delegates directly to the already-accepted
    `IStructuredEditInterpreter.ParseKnownCommand`, so every one of Step 073's own gates (cancellation,
    stale revision, locks, grammar/parameter validation) applies unmodified; catches
    `EditCommandRejectedException`, sets `ActionErrorMessage`, and leaves `LastProposal`/`Explanation`/
    `TargetSummary` completely untouched on any rejection (Step 074's own required failure/edge case,
    verbatim: "Out-of-scope, locked or stale revision must be rejected without partial mutation").
  - `LastProposal`/`Explanation` — `Explanation` is a direct projection of `LastProposal?.Explanation`
    (Step 073's own already-validated field); this step adds no new explanation-generation logic of its
    own, only the bindable surface a real SCREEN 14 view would read.
  - `AttachContext` resets `InstructionText`/`LastProposal`/`ActionErrorMessage` so a stale request typed
    against a previous selection can never be confused with a newly attached one — a new architectural
    decision this step introduces (not present in Step 073's narrower quick-actions panel, which has no
    persistent `SelectionContext` of its own to re-attach).

### New test file

- **`tests/XlightsAI.Studio.Shell.Tests/Step074AiEditRequestPanelViewModelTests.cs`** (13 tests) — real
  `AiEditRequestPanelViewModel`/`StructuredCommandInterpreter`/`EffectLockPolicy`/
  `SelectionContextCaptureService`/`ThreeEffectPlanFactory`/`EffectRecipeRegistry` (not mocks), against
  the same gate-demo layout (Roofline, Arch 1, Arch 2) every prior PHASE-02/03/11 step's own tests already
  use:
  - **`Step074_Acceptance_RequestAlwaysDisplaysTargetContext`** — the required acceptance test, verbatim:
    attaching a real captured selection immediately shows the affected props (`Arch 1`, `Arch 2`) and
    exact half-open time range (`[10s, 30s)`, derived from the real `ThreeEffectPlanFactory` thirds-split
    layout), **before** any instruction is typed or submitted — `InstructionText`/`LastProposal`/
    `Explanation` are all still at their initial/empty state at the point `TargetSummary` is already fully
    populated.
  - **`SuggestionsAreFixedAndMatchKnownOfflineGrammar`** — the fixed suggestion list contains real,
    interpreter-recognized phrases (`"brighten"`, `"snap to grid"`).
  - **`SettingInstructionText_NeverInvokesInterpreter_UntilSubmitAsync`** — "do not send until explicit
    action" (implementation guidance, verbatim): typing (including the phase gate's own named
    free-form example, "make the piano move left-to-right") leaves `LastProposal`/`ActionErrorMessage`
    untouched and the document's own effect count unchanged until `SubmitAsync` is explicitly called.
  - **`Step074_SubmitAsync_AcceptedInstructionPopulatesProposalAndExplanation`** — an explicit submit of a
    known quick action (`"brightness 75"`) populates `LastProposal`/`Explanation` with a non-blank change
    explanation ("change explanation," step heading, verbatim) and leaves `TargetSummary` unchanged.
  - **`UnrecognizedInstruction_IsRejected_LeavesTargetContextAndProposalUnchanged_NegativePath`** — the
    phase gate's own named worked-example phrasing rejects without building a proposal; `TargetSummary`
    and the document's own effect count are both unchanged.
  - **`StaleRevision_IsRejected_WithoutPartialMutation_NegativePath`** / **`LockedTarget_IsRejected_WithoutPartialMutation_NegativePath`**
    — this step's own required failure/edge case, verbatim ("Out-of-scope, locked or stale revision must
    be rejected without partial mutation") for the two cases realizable at the panel layer; `TargetSummary`
    is explicitly asserted unchanged by the stale-revision case (the panel-specific negative-path
    assertion beyond what Step 073's quick-actions panel needed to prove, since that panel has no
    persistent target-context display of its own).
  - **`SubmitAsync_WithoutAttachedContext_Throws_NegativePath`** / **`SubmitAsync_NullArguments_Throw_NegativePath`**
    / **`AttachContext_Null_Throws_NegativePath`** — argument/state-contract negative paths.
  - **`AttachContext_AfterSubmit_ResetsInstructionAndProposal`** — proves the new re-attach reset
    behavior disclosed in §2/§3.

## 3. Architecture deviations / decisions

- **No new `XlightsAI.Studio.AiEdits` domain type was introduced for this step.** PHASE-11.md Section 3's
  own proposed `EditContext`/`IOptionalAiPlanner` signatures remain Step 075's own later, not-yet-
  implemented, optional-AI-planner scope (verbatim, Section 3: "Optional local/cloud LLM receives a
  redacted, least-privilege context..."). Step 074's own heading ("Add edit request panel, suggestions and
  change explanation") and its own required acceptance text name a UI-facing concern only ("Request
  always displays target context") — the underlying validated-proposal/explanation machinery this panel
  displays was already fully built and proven in Step 073. Reconciling the step's own request/suggestion/
  explanation surface with what already exists, rather than re-introducing a parallel `EditContext`
  record never consumed anywhere, follows the same "do not redefine copies" discipline Step 073's own ADR
  already applied to `SelectionContext` vs. `EditContext`.
- **"AI text input" (implementation guidance, verbatim) is realized as a plain free-text property routed
  through the existing, already-accepted `IStructuredEditInterpreter`, not a new optional-AI-planner call.**
  Reasoning: Step 075's own later scope ("Add optional Ollama/cloud provider behind opt-in
  structured-output adapter") is the spec's own explicit boundary for any actual AI-model call; this
  step's own required acceptance text ("Request always displays target context") and failure/edge text
  ("Out-of-scope, locked or stale revision...") name only the deterministic, already-gated request path —
  identical to the interpreter Step 073 already proved offline/no-token-usage. The panel's own
  `SubmitAsync` is written against the `IStructuredEditInterpreter` interface specifically so a later
  step can route the same call through an `IOptionalAiPlanner` fallback (Section 3's own flowchart: "Known
  deterministic command? ... No -> Optional model configured + consent? ... Yes -> Request structured AI
  proposal") without this panel's own bindable contract changing.
- **`AttachContext` resets `InstructionText`/`LastProposal`/`ActionErrorMessage` on every call, including
  re-attaching the same context instance.** Reasoning: this step's own required acceptance explicitly
  names a persistent, always-current target-context display; a panel that could show a target context
  from one selection paired with a leftover accepted/rejected request from a different (or even the same,
  since-superseded) selection would violate the spirit of "Request always displays target context" by
  mixing two different requests' own state. No existing contract was changed to reach this decision — it
  is new behavior specific to this step's own view model.
- **A `string.Empty` initial `TargetSummary` (rather than `null`) is used while `IsEmpty`.** Reasoning:
  matches this step's own required acceptance text implying "always displays" a string the UI can bind to
  directly without a null-check in the view, the same convention `SelectionContextEditorViewModel`
  already applies to its own `Current`/`SavedPath` nullable-but-always-consistent properties (here
  realized as a non-null empty string specifically because `TargetSummary` is itself text meant for direct
  display, not an object reference).
- **No real SCREEN 14 AI EDIT Avalonia/AXAML view or Shell-layer `ShellScreen` wiring exists** —
  unchanged, carried-forward condition from every prior Phase 09/10/11 UI-adjacent step (most recently
  Step 073's own evidence §3/§7). This step's own required user-visible outcome ("Versioned, inspectable
  project output plus a reproducible test report") is satisfied at the bindable
  `AiEditRequestPanelViewModel` layer a real SCREEN 14 view would bind to, proven by a real
  non-test-harness console demo (§5) plus `Step074AiEditRequestPanelViewModelTests` — the same precedent
  Steps 059-073's own evidence already established.
- **Per the user's own standing waiver** (`memory: feedback_skip_xlights_human_verification`), no live
  xLights render/desktop-interaction check was attempted — not applicable to this step's own required
  acceptance text in any case ("Request always displays target context" names no xLights rendering
  concern; this step produces no `.xsq` output and never mutates a `SequenceDocument`).

## 4. Commands actually executed

All run via the PowerShell tool — the sandboxed Bash tool in this session again returned no usable output
for plain filesystem/`dotnet` commands against this working directory (the same pre-existing environment
limitation every prior step's own evidence has recorded); PowerShell was used for every `dotnet`/
file-system command below without incident.

```
# Baseline, before any Step 074 code existed:
dotnet build XlightsAI.Studio.slnx --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s). (Step 073's own closing state, reconfirmed)

# After adding AiEditRequestPanelViewModel.cs + its new test file:
dotnet build XlightsAI.Studio.slnx --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s). (clean on first attempt)

dotnet test tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj --nologo --filter "FullyQualifiedName~Step074"
  # Passed! - Failed: 0, Passed: 11, Skipped: 0, Total: 11, Duration: 138 ms

dotnet format XlightsAI.Studio.slnx --verify-no-changes --no-restore
  # No output (clean) -- matches the repository's own CI formatting gate, checked across the full solution.

dotnet test XlightsAI.Studio.slnx --nologo --filter "FullyQualifiedName!~Step025&FullyQualifiedName!~Step028&FullyQualifiedName!~Step029&FullyQualifiedName!~Step030&FullyQualifiedName!~Step042"
dotnet test tests\XlightsAI.Studio.Stems.Tests\XlightsAI.Studio.Stems.Tests.csproj --nologo --filter "FullyQualifiedName~Step042"
  # Passed! - Failed: 0, Passed: 8, Skipped: 0, Total: 8, Duration: 424 ms
  # (the blunt numeric filter above also excludes Stems.Tests' own unrelated, legitimate
  # Step042AcceptanceTests.cs -- the same filter side effect disclosed in every prior Phase 09/10/11
  # step's own evidence; reconfirmed here passing in isolation.)

# Real, non-test-harness manual demo (built under %TEMP%\xlai-step074-demo-proj, deleted after this run):
dotnet run --project "$env:TEMP\xlai-step074-demo-proj"
Remove-Item -Recurse -Force "$env:TEMP\xlai-step074-demo-proj"
```

### Targeted test results (Step 074's own 11 new tests, real run)

```
Step074AiEditRequestPanelViewModelTests (Shell, 11 new):
  Step074_Acceptance_RequestAlwaysDisplaysTargetContext                                  Passed
  SuggestionsAreFixedAndMatchKnownOfflineGrammar                                         Passed
  SettingInstructionText_NeverInvokesInterpreter_UntilSubmitAsync                        Passed
  Step074_SubmitAsync_AcceptedInstructionPopulatesProposalAndExplanation                 Passed
  UnrecognizedInstruction_IsRejected_LeavesTargetContextAndProposalUnchanged_NegativePath Passed
  StaleRevision_IsRejected_WithoutPartialMutation_NegativePath                           Passed
  LockedTarget_IsRejected_WithoutPartialMutation_NegativePath                            Passed
  SubmitAsync_WithoutAttachedContext_Throws_NegativePath                                 Passed
  SubmitAsync_NullArguments_Throw_NegativePath                                           Passed
  AttachContext_Null_Throws_NegativePath                                                 Passed
  AttachContext_AfterSubmit_ResetsInstructionAndProposal                                 Passed
Shell.Tests (Step 074 slice): 11/11 passed, 0 failed, 0 skipped.
```

### Full regression (final, after all Step 074 implementation and tests, real-audio-device Shell tests excluded — unchanged pre-existing condition, see Step 064-073's own §6)

```
Passed! - XlightsAI.Studio.Audio.Tests.dll           23/23
Passed! - XlightsAI.Studio.Xlights.Tests.dll         74/74
Passed! - XlightsAI.Studio.Editing.Tests.dll         83/83
Passed! - XlightsAI.Studio.Display.Tests.dll         56/56
Passed! - XlightsAI.Studio.Structure.Tests.dll       78/78
Passed! - XlightsAI.Studio.Composition.Tests.dll     78/78
Passed! - XlightsAI.Studio.AiEdits.Tests.dll         29/29 (unchanged -- no new AiEdits-layer tests this step)
Passed! - XlightsAI.Studio.Melody.Tests.dll         117/117
Passed! - XlightsAI.Studio.Projects.Tests.dll         25/25
Passed! - XlightsAI.Studio.Shell.Tests.dll          179/179 (168 carried forward + 11 new Step 074; real-audio-device tests excluded, unchanged)
Passed! - XlightsAI.Studio.Rhythm.Tests.dll           82/82
Passed! - XlightsAI.Studio.Reference.Tests.dll        31/31
Passed! - XlightsAI.Studio.Stems.Tests.dll            93/93  (+8 Step042AcceptanceTests reconfirmed separately, above)
```

**956 total passing** (945 carried forward from Step 073's own closing full-regression count, completely
unmodified + 11 new Shell tests = 956), 0 failed, 0 skipped. Every pre-existing test from Steps 001-073
still passes unmodified; zero files from any prior step were changed.

## 5. Persisted-output inspection / observable demonstration for this step

A real, non-test-harness console program (not part of the repository, built under
`%TEMP%\xlai-step074-demo-proj`, deleted after this run) exercises the real
`AiEditRequestPanelViewModel`/`StructuredCommandInterpreter`/`EffectLockPolicy`/
`SelectionContextCaptureService` directly — no test harness, no mocks. It realizes the phase gate's own
named worked example almost verbatim ("Select 42-48 seconds on five arches, ask for rising piano pops,
inspect affected models/times..." — this step's own scope being the request-panel display/submit surface,
not the later dry-run diff/accept-reject steps):

```
dotnet run --project "$env:TEMP\xlai-step074-demo-proj"
```

Observed real output:

```
=== Step 074 manual demo: real AiEditRequestPanelViewModel, no test harness ===

--- Scenario 1: select 42-48 seconds on all five arches, attach to the request panel ---
  Before AttachContext: IsEmpty=True, TargetSummary=''
  After AttachContext:  IsLoaded=True, TargetSummary='Arch 1, Arch 2, Arch 3, Arch 4, Arch 5 @ [42s, 48s)'
  Suggestions offered: brighten, dim, reverse, direction left-to-right, faster, slower, snap to grid, follow lead

--- Scenario 2: typing alone never sends anything (no network, no model, no token usage) ---
  Typed free-form instruction; LastProposal is still null: True
  ActionErrorMessage is still null: True
  TargetSummary unchanged by typing: 'Arch 1, Arch 2, Arch 3, Arch 4, Arch 5 @ [42s, 48s)'

--- Scenario 3: explicit submit of the free-form instruction (unrecognized -- rejected, no mutation) ---
  Submitted='make the piano move left-to-right' -> accepted=False, error='Instruction 'make the piano move left-to-right' does not match any known local command; no proposal was built.'
  TargetSummary still shown after rejection: 'Arch 1, Arch 2, Arch 3, Arch 4, Arch 5 @ [42s, 48s)'

--- Scenario 4: explicit submit of a known offline quick action (accepted, with change explanation) ---
  Submitted='brightness 75' -> accepted=True
  LastProposal.Operations[0].Kind=SetBrightness Parameters=[Level=75]
  Explanation: "Set brightness to 75%."

--- Scenario 5: negative paths -- stale revision, locked target ---
  Stale revision submit -> accepted=False, error='Expected revision 1 does not match the document's current revision 2; no proposal was built.'
  LastProposal unchanged by the rejected attempt: Kind=SetBrightness
  Locked-target submit -> accepted=False, error='Effect 'dcc651b8-5cf7-413d-a500-4d14e41182cf' on 'Arch 2' is locked and cannot be edited.'

  Confirming no document mutation across every scenario: document.Revision=1, document.Effects.Count=5

Step 074 manual demo completed successfully.
```

Direct, real, non-test-harness confirmation of this step's own required text: (1) "Request always displays
target context" (required acceptance, verbatim) — Scenario 1 shows `TargetSummary` fully populated
(`'Arch 1, Arch 2, Arch 3, Arch 4, Arch 5 @ [42s, 48s)'`) the instant the context is attached, before any
instruction is typed; Scenarios 2/3/5 each reconfirm the identical `TargetSummary` string is still shown
after typing, after a rejected submit, and after further rejected submits — it is never cleared or altered
by any request outcome. (2) "Create selection-summary drawer listing affected props and exact time range
and AI text input; do not send until explicit action" (implementation guidance, verbatim) — Scenario 1's
own summary lists the exact five affected props and the exact `[42s, 48s)` half-open time range; Scenario
2 types the phase gate's own named free-form instruction and proves no proposal/error state changes until
Scenario 3's own explicit `SubmitAsync` call. (3) "Out-of-scope, locked or stale revision must be rejected
without partial mutation" (failure/edge case, verbatim) — Scenario 3 (unrecognized instruction, this
panel's closed-grammar equivalent of "out of scope" since a free-form instruction can never reach any
operation at all) and Scenario 5 (stale revision, locked target) each reject with a specific, readable
error, and the panel's own `LastProposal` stays pinned to Scenario 4's accepted proposal throughout — the
original `document` instance is confirmed unchanged afterward (revision and effect count both unchanged,
exactly as before Scenario 1 even ran).

## 6. Fixtures / input hashes

- No show-layout XML fixture file was needed for this step's own tests or demo —
  `AiEditRequestPanelViewModel` operates on a caller-supplied `SelectionContext`/`SequenceDocument` built
  directly in code (the same convention `Step072AcceptanceTests`/`Step073AcceptanceTests`'s own
  `GateDemoLayout()`/`GateDemoPlan()` already established for this test suite; the demo builds its own
  five-arch document directly, disclosed in §5).
- The real demo produced no persisted file of any kind (this step never writes to disk — `EditProposal`
  is an in-memory value only, same as Step 073; persisting a proposal remains Step 078's own later
  "Persist proposal audit" scope); the throwaway demo *project* itself was deleted after this evidence was
  recorded.
- **Real-audio-device test exclusion, disclosed (unchanged, carried forward from every prior Phase
  09/10/11 step's own §6):** `Step025`/`Step028`/`Step029`/`Step030`/`Step042ShellWiringTests` construct a
  real `WaveOutAudioTransport` (the real Windows winmm audio device) unavailable in this session's own
  sandboxed execution environment — a pre-existing, environment-caused condition predating this step
  entirely. No file this step touched is on the call path of any of those five classes (confirmed: none
  reference `AiEditRequestPanelViewModel` in any way).

## 7. Unresolved issues / known gaps

- **No real SCREEN 14 AI EDIT Avalonia/AXAML view or Shell-layer `ShellScreen` wiring exists** — this
  step's own required user-visible outcome ("Versioned, inspectable project output plus a reproducible
  test report") is satisfied here by the real console demo (§5) and
  `Step074AiEditRequestPanelViewModelTests` (bindable-view-model layer), disclosed in §3 as the
  project-wide, pre-existing, consistent convention — not an oversight specific to this step.
- **The free-text "AI text input" still only ever reaches the local, deterministic
  `StructuredCommandInterpreter`** — no optional AI planner exists yet (`IOptionalAiPlanner`,
  Section 3's own proposed interface, remains Step 075's own not-yet-implemented scope); a free-form
  instruction the local grammar does not recognize (e.g. the phase gate's own "rising piano pops" example)
  is rejected with `UnrecognizedInstruction`, exactly as Step 073's own interpreter already does — this
  panel adds no new natural-language understanding of its own.
- **No `IEditProposalValidator`/`IOptionalAiPlanner`/`IEditApprovalService` exists yet** — PHASE-11.md
  Section 3's own proposed signatures beyond the structured-command-AST/request-panel slice remain
  genuinely open, disclosed as Step 075-078's own later, not-yet-implemented scope (verbatim from
  PHASE-11.md §9: "Implement only numbered steps 072, 073, 074, 075, 076, 077, 078"). Step 074 also does
  not apply an `EditProposal` to a `SequenceDocument` at all — that remains Step 076's own later scope
  ("dry-run proposals, before/after diff and Accept/Reject").
- **Carried-forward, disclosed phase-level/dependency-level gaps are unchanged from Step 073's own
  closing evidence** (not reopened or re-resolved here): a consolidated Phase 09/10 `GATE-REPORT.md` is
  still unwritten; Phase 06's own stem-separation gate remains BLOCKED; Phase 07's and Phase 08's own
  full-phase gates have not been separately run/recorded; real-audio-device Shell tests remain
  environment-blocked (§6); this document covers only Step 074's own narrow scope, not a Phase 11
  whole-phase gate.

## 8. PASS / BLOCKED

**Status: PASS** (for this step's own narrow, disclosed scope — a bindable selection-summary drawer with
an always-current target-context display, a fixed offline suggestion list, an explicit two-phase
type-then-submit instruction flow, and a non-blank change explanation for every accepted request — not a
claim that any carried-forward phase-level gap listed in §7 is resolved, and not a claim that a real
SCREEN 14 Avalonia view or a live xLights-installation render has been independently confirmed, neither of
which this step's own required acceptance text names).

- "Request always displays target context" (required acceptance, verbatim) — proven by
  `Step074_Acceptance_RequestAlwaysDisplaysTargetContext` (target context populated before any
  instruction is typed or submitted) and reconfirmed by every other test's own unchanged-`TargetSummary`
  assertions — independently confirmed end-to-end by the real demo's Scenario 1 (immediate display) and
  Scenarios 2/3/5 (unchanged across typing and two distinct rejection categories).
- "Create selection-summary drawer listing affected props and exact time range and AI text input; do not
  send until explicit action" (implementation guidance, verbatim) — proven by
  `SettingInstructionText_NeverInvokesInterpreter_UntilSubmitAsync` (typing alone never reaches the
  interpreter) and `Step074_SubmitAsync_AcceptedInstructionPopulatesProposalAndExplanation` (an explicit
  submit is the only path to a result) — independently confirmed by the real demo's Scenario 1 (exact
  props/time range listed) and Scenario 2 (typing produces no side effect) followed by Scenario 3's own
  explicit submit.
- "Out-of-scope, locked or stale revision must be rejected without partial mutation" (failure/edge case,
  verbatim) — proven by `UnrecognizedInstruction_IsRejected_LeavesTargetContextAndProposalUnchanged_NegativePath`,
  `StaleRevision_IsRejected_WithoutPartialMutation_NegativePath`,
  `LockedTarget_IsRejected_WithoutPartialMutation_NegativePath`, and the argument/state-contract negative
  paths (`SubmitAsync_WithoutAttachedContext_Throws_NegativePath`,
  `SubmitAsync_NullArguments_Throw_NegativePath`, `AttachContext_Null_Throws_NegativePath`) —
  independently confirmed by the real demo's Scenario 3 (unrecognized instruction) and Scenario 5 (stale
  revision, locked target), each with a specific, readable error and the panel's own `LastProposal`/
  `TargetSummary` left completely unchanged, and the original `document` instance confirmed unchanged
  afterward (revision and effect count both unchanged).
- Full regression (real-audio-device tests excluded, disclosed in full in §6): **956/956 passing**, 0
  failed, 0 skipped; every pre-existing test from Phase 01-11/Step 073 still passes unmodified, plus this
  step's own 11 new Shell tests. `dotnet format --verify-no-changes` clean across the full solution.
- **No file from any prior step/phase was changed at all this step** (fully additive — one new Shell
  view model file and one new test file; no `.csproj` change was even needed since `XlightsAI.Studio.Shell`
  already referenced `XlightsAI.Studio.AiEdits` from Step 073).
- **No bugs were found during this step's own implementation** — the first `dotnet build` and first
  `dotnet test` run for the new Step 074 code both succeeded with no failures, unlike Step 073's own two
  disclosed fixes; this is stated plainly, not omitted, since the task's own instruction is to disclose
  failed tests when they occur, not to imply every step necessarily has one.
- **Carried-forward, disclosed phase-level/dependency-level gaps (not this step's own blocker):** no real
  SCREEN 14 Avalonia view exists yet (§7, project-wide pre-existing condition);
  `IEditProposalValidator`/`IOptionalAiPlanner`/`IEditApprovalService` and later Phase 11 steps remain
  genuinely unimplemented (§7); Phase 09/10's own consolidated gate reports are still unwritten; Phase
  06's own gate remains BLOCKED; Phase 07's and Phase 08's own full-phase gates have not been separately
  run/recorded; real-audio-device Shell tests are environment-blocked (§6).

Per PHASE-11.md §8's own required final gate report, this document covers Step 074 alone. Steps 075-078
remain open; the next step does not begin until this document's own PASS status is accepted, per the
task's own standing instruction ("do not execute the next step until this step is proven").
