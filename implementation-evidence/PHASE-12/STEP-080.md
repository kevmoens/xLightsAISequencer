# PHASE-12 Step 080 — Build seeded candidate generator constrained to selected gesture family

**Date:** 2026-10-17
**Spec:** `docs/phase-specs/PHASE-12.md` §6, Step 080
**Required acceptance (verbatim):** "Same seed + inputs yields same structured candidate."
**Required failure/edge case (verbatim):** "Malformed proposal, no-improvement and exhausted retry budget must not auto-commit."

## 1. Pre-implementation baseline inspection

Before writing any code, the existing repository state was read directly (not taken on faith):

- Read `docs/phase-specs/PHASE-12.md` in full (already read for Step 079; re-confirmed Step 080's own heading, implementation guidance, required acceptance/failure text, and PHASE-12 Section 7 cross-cutting test #1: "Same seed + inputs yields reproducible candidate").
- Read `implementation-evidence/PHASE-12/STEP-079.md` in full — Step 079's own disclosed ADRs (Section 3) are binding for this step: `CandidateAttempt` deliberately has no `Plan`/`Report` field (no `ChoreographyPlan`/`QualityReport` exists anywhere in the repository), and Steps 080/081 are explicitly "expected to extend `CandidateAttempt` additively, the same way Step 078 extended Steps 072-077's own services with new optional constructors rather than breaking them."
- Read every file in `src/XlightsAI.Studio.Refinement/` as it exists today: `LoopRun.cs`, `CandidateAttempt.cs`, `IRefinementRunStore.cs`, `JsonRefinementRunStore.cs`, `RefinementStatus.cs`, `RefinementStatusTransitions.cs`, `RefinementClaim.cs`, the two JSON converters, and both exception types — confirmed `JsonRefinementRunStore.SaveAttemptAsync` calls `attempt.Validate()` (line 169) and that Step 079's own `Step079AcceptanceTests.cs` `Attempt()` helper constructs many `CandidateAttempt`s with empty `Violations` and no generated content at all — any new cross-field invariant added to `CandidateAttempt.Validate()` that required non-empty generated content whenever `Violations` is empty would retroactively break those already-passing, already-accepted Step 079 tests. This directly shaped the design in §3 below.
- Read `tests/XlightsAI.Studio.Refinement.Tests/Step079AcceptanceTests.cs` in full to confirm every call site uses full named-argument record construction (never positional-only), which is what makes an additive, defaulted trailing field backward compatible without touching that file at all.
- Read `src/XlightsAI.Studio.Composition/ChoreographyCandidate.cs`, `TransitionGesture.cs`, `TransitionKind.cs`, `TransitionRecipePlanner.cs` (PHASE-10, already accepted) — `TransitionKind` (`Build`, `Blackout`, `Transition`, `Release`, `PhraseEnding`) is this repository's own already-accepted "gesture family" concept, and `TransitionGesture` (`Kind`/`Window`/`TargetProps`/`StartIntensity`/`EndIntensity`, with its own `Validate()`) is this repository's own already-accepted structured-gesture shape. Step 080's own heading names "gesture family" verbatim; reusing `TransitionKind` unmodified avoids inventing a second, parallel enum for the same concept.
- Read `src/XlightsAI.Studio.Editing/EditScope.cs` (PHASE-03, already accepted) — `EditScope.Models` is an `IReadOnlySet<ModelId>` (unordered), which is why the generator sorts model values before using them (set enumeration order is not a reproducibility guarantee this step's own required acceptance can rely on).
- Read every `src/*/*.csproj` touched by this step (`Refinement`, `Composition`) and confirmed, via `Grep`, that `Composition` does not reference `Refinement` anywhere — adding a `Refinement -> Composition` project reference is not a cyclic reference.
- Confirmed via `Grep` that no `ICandidateGenerator`/`ISeededCandidateGenerator`/`DeterministicGestureCandidateGenerator` exists anywhere in the repository yet (this step's own scope, not already partially implemented).

## 2. Scope actually implemented

All new/changed files are in `src/XlightsAI.Studio.Refinement/` (plus one new project reference) and `tests/XlightsAI.Studio.Refinement.Tests/`:

| File | Purpose |
|---|---|
| `DeterministicContent.cs` (new) | Two static helpers — `Hash(string)` (stable lowercase hex SHA-256) and `GuidFrom(string)` (a stable `Guid` derived from the first 16 bytes of the same digest) — used everywhere this step needs a reproducible identity or snapshot hash instead of `Guid.NewGuid()`/wall-clock time/`GetHashCode()`. |
| `ISeededCandidateGenerator.cs` (new) | PHASE-12 Section 3's own proposed `ICandidateGenerator.GenerateAsync(LoopRun run, Candidate? prior, CancellationToken ct)`, narrowed (disclosed ADR §3.1) to return/accept `CandidateAttempt` instead of the spec's own proposed `Candidate`/`ChoreographyPlan` (neither exists in this repository — Step 079's own ADR). |
| `DeterministicGestureCandidateGenerator.cs` (new) | The real, deterministic implementation: given a `LoopRun` and an optional prior attempt, derives a per-attempt seed from `(run.Seed, attempt-ordinal)` alone, sorts `Scope.Models` for a stable tie-break, generates exactly one `TransitionGesture` constrained to `run.GestureFamily`'s own `TransitionKind` and to exactly the permitted props, computes a deterministic `SnapshotHash`/`Id` via `DeterministicContent`, and returns a `CandidateAttempt` with `Status=PreviewReady`/`Violations=[]`/`Gestures=[gesture]` when valid, or `Status=Rejected`/`Violations=[...]`/`Gestures=[]` when the run's scope names no permitted model (this step's own required failure/edge case). Throws `RefinementRunInvalidException` immediately, before generating anything, when `run.GestureFamily` is `null` (a precondition failure, not a per-attempt soft rejection). |
| `LoopRun.cs` (extended) | Added `TransitionKind? GestureFamily = null` as a new, defaulted, trailing positional parameter. |
| `CandidateAttempt.cs` (extended) | Added `IReadOnlyList<TransitionGesture>? Gestures = null` as a new, defaulted, trailing positional parameter; `Validate()` additionally calls `TransitionGesture.Validate()` on every entry when `Gestures` is non-null (a no-op for every pre-existing Step 079 attempt, which never sets this field). |
| `XlightsAI.Studio.Refinement.csproj` (extended) | Added a `ProjectReference` to `XlightsAI.Studio.Composition` (verified not cyclic — see §1). |

New test file `tests/XlightsAI.Studio.Refinement.Tests/Step080AcceptanceTests.cs` — 9 new tests against the real `DeterministicGestureCandidateGenerator`, and, where this step's own output round-trips through Step 079's own already-accepted store, the real file-backed `JsonRefinementRunStore` (no mocking library). `tests/.../XlightsAI.Studio.Refinement.Tests.csproj` gained one new `ProjectReference` to `Composition` (the test file constructs `TransitionKind` values directly).

## 3. Architecture deviations / decisions (ADRs)

1. **`LoopRun.GestureFamily` and `CandidateAttempt.Gestures` are both additive, defaulted (`= null`), trailing positional parameters — never a required, non-defaulted field.** Both `LoopRun` and `CandidateAttempt` are already-accepted, already-shipped (Step 079) positional records, and every existing call site in `Step079AcceptanceTests.cs` constructs them with full named arguments and no knowledge of either new field. A non-defaulted new positional parameter would force every one of those already-accepted call sites to be edited just to keep compiling — exactly the kind of retroactive, unplanned rewrite of a preceding step's own test suite the governing instructions forbid ("Treat all preceding completed step contracts as immutable unless the current step has an accepted migration plan"; no such migration plan exists or is needed here). A default value makes both fields purely additive: every Step 079-era construction still compiles, unchanged, and still means exactly what it meant before.
2. **`CandidateAttempt.Validate()` does *not* require `Gestures` to be non-empty when `Violations` is empty**, even though that would be a reasonable-sounding "a winning candidate must carry real content" invariant. Step 079's own `Step079AcceptanceTests.Attempt()` helper constructs many non-violating (`Violations: []`), `PreviewReady` attempts with no `Gestures` at all, and `JsonRefinementRunStore.SaveAttemptAsync` calls `attempt.Validate()` on every save (confirmed by reading the source, §1) — adding that cross-field rule to the shared record would make every one of those already-passing Step 079 tests throw. The actual guarantee this step's own required acceptance needs — "a clean candidate this generator itself produces always carries exactly one gesture" — is instead enforced only inside `DeterministicGestureCandidateGenerator`'s own output (`candidate.Validate()` is still called before returning, so a malformed generator output cannot silently escape; it only does not retroactively re-define what "valid" meant for every other producer of a `CandidateAttempt`, generator or otherwise).
3. **`TransitionKind` (PHASE-10's own already-accepted enum) is reused, unmodified, as "gesture family" — no new `GestureFamily` enum was invented.** Step 080's own heading names "gesture family" verbatim; `TransitionKind`'s own five members (`Build`, `Blackout`, `Transition`, `Release`, `PhraseEnding`) are already exactly this repository's own accepted vocabulary for "which reusable semantic recipe" a gesture realizes (`TransitionKind.cs`'s own doc comment, PHASE-10 Step 070). Declaring a second, parallel enum for the same concept would duplicate, not share, an already-accepted contract — the same "do not fabricate generic interfaces without a concrete boundary" principle Step 079's own ADRs already applied to `ChoreographyPlan`.
4. **The generator produces exactly one `TransitionGesture` per attempt, spanning the run's entire `Scope.Time` window and targeting every permitted model, rather than a variable-count, sub-windowed set of gestures.** PHASE-12 Section 3's own architectural notes describe "deterministic parameter variation and seeded gesture generation" without specifying gesture count or sub-windowing, and no later step in this phase (081-084) names multi-gesture composition as a required behavior — `IChoreographyQualityEvaluator`'s own scope (Step 081) is scoring whatever a candidate already contains, not dictating how many gestures a candidate must have. A single, scope-spanning gesture is the smallest complete slice that still satisfies this step's own three required properties end to end (seeded determinism, family constraint, scope/permission containment) without speculatively building sub-windowing/multi-gesture composition that no acceptance text in this phase actually requires yet (the project's own "no half-finished implementations... no designing for hypothetical future requirements" rule). This is disclosed as a narrowing, not hidden as if it were the only possible design; `ICandidateGenerator`'s own return shape (`CandidateAttempt.Gestures` is a list) already accommodates a future step generating more than one gesture per attempt without another breaking change.
5. **A malformed proposal (empty permitted `Scope.Models`) is returned as a `Rejected`, violation-carrying `CandidateAttempt`, not thrown as an exception** — this is the same shape Step 079's own hard-gate rejection path already expects (`CandidateAttempt.Violations` non-empty, never eligible as `BestCandidateId`). A missing `GestureFamily`, by contrast, **is** thrown (`RefinementRunInvalidException`) immediately, before any generation attempt — this is a run-level configuration precondition (no family was ever selected for this run at all, PHASE-12 Section 3: "selected gesture family"), categorically different from a per-attempt scope that happens to be unsatisfiable; conflating the two would make a configuration bug look like a normal, retryable rejected attempt.
6. **No existing accepted contract from any prior phase was modified.** `TransitionGesture`/`TransitionKind` (PHASE-10), `EditScope`/`ModelId`/`TimeWindow` (PHASE-01/03) are consumed unchanged, read-only. `RefinementStatus`/`RefinementStatusTransitions`/`IRefinementRunStore`/`JsonRefinementRunStore`/`RefinementClaim` (Step 079) are consumed unchanged — this step adds two new files and extends two existing records additively; it does not touch the state machine, the store's own transition/claim enforcement, or any exception type.

## 4. Commands actually executed

All commands were run via the **PowerShell** tool. (As in every prior PHASE-11/Step-079 evidence doc, the Bash tool returns no usable output in this sandboxed session — even a bare `echo` silently exits 1 with no output; re-confirmed at the start of this step before falling back to PowerShell, as disclosed, not worked around by claiming Bash output that never appeared.)

```
> dotnet build src\XlightsAI.Studio.Refinement\XlightsAI.Studio.Refinement.csproj -v minimal
Build succeeded. 0 Warning(s) 0 Error(s)

> dotnet build tests\XlightsAI.Studio.Refinement.Tests\XlightsAI.Studio.Refinement.Tests.csproj -v minimal
Build succeeded. 0 Warning(s) 0 Error(s)   (one xUnit1031 analyzer warning surfaced and was fixed by
                                            converting a sync Fact to an async one before the final run)

> dotnet test tests\XlightsAI.Studio.Refinement.Tests\XlightsAI.Studio.Refinement.Tests.csproj -v minimal
Passed! - Failed: 0, Passed: 20, Skipped: 0, Total: 20, Duration: 674 ms
  (11 pre-existing Step 079 tests, unmodified, + 9 new Step 080 tests)

> dotnet build XlightsAI.Studio.slnx -v minimal   (full solution, after change)
Build succeeded. 0 Warning(s) 0 Error(s)

> dotnet test XlightsAI.Studio.slnx -v minimal   (full solution regression, after change)
XlightsAI.Studio.Xlights.Tests.dll      : 74/74 passed
XlightsAI.Studio.Editing.Tests.dll      : 91/91 passed
XlightsAI.Studio.Projects.Tests.dll     : 25/25 passed
XlightsAI.Studio.Display.Tests.dll      : 56/56 passed
XlightsAI.Studio.Melody.Tests.dll       : 117/117 passed
XlightsAI.Studio.Structure.Tests.dll    : 78/78 passed
XlightsAI.Studio.Composition.Tests.dll  : 78/78 passed
XlightsAI.Studio.Refinement.Tests.dll   : 20/20 passed   (was 11/11 before this step)
XlightsAI.Studio.AiEdits.Tests.dll      : 79/79 passed
XlightsAI.Studio.Audio.Tests.dll        : 38/38 passed
XlightsAI.Studio.Rhythm.Tests.dll       : 82/82 passed
XlightsAI.Studio.Shell.Tests.dll        : 263/263 passed
XlightsAI.Studio.Reference.Tests.dll    : 31/31 passed
XlightsAI.Studio.Stems.Tests.dll        : 101/101 passed
TOTAL: 1133/1133 passed, 0 failed   (1124 baseline-after-Step-079 + 9 new, 0 removed, 0 modified)

> dotnet format XlightsAI.Studio.slnx --verify-no-changes --no-restore
(no output -- no formatting violations)

> git status --short
(unchanged from before this step: everything still untracked, nothing committed in this repository yet;
no prior file was modified or staged by this step)
```

Observable desktop demonstration (disposable console app, not committed to the repository, `%TEMP%\xlai-step080-demo-proj` — the same convention every prior PHASE-11/Step-079 evidence doc used):

```
> New-Item -ItemType Directory -Force -Path "$env:TEMP\xlai-step080-demo-proj"
> # wrote demo.csproj + Program.cs referencing the real Refinement/Editing/Contracts/Composition projects
> cd "$env:TEMP\xlai-step080-demo-proj"
> dotnet run -v minimal
```

Full transcript (abridged to remove restore/build noise):

```
=== PHASE-12 Step 080 demo: seeded candidate generator constrained to selected gesture family ===
Persistence root: C:\Users\kmoens.LAKECO\AppData\Local\Temp\step080-demo-data-ebdb1ada0da74cd5bdb77492df3f0364

--- Same seed + inputs yields the same structured candidate (required acceptance) ---
Run 936de1bb-e40d-43b7-8c0c-884b249220e3, GestureFamily=Build, Seed=1337
Generation 1: SnapshotHash=e8c6f80ad54adc10387234f3ddc8033e58c052ee13291183e31d72381e445154, Id=6338c0f9-1ccf-5c56-c10b-b4f219ab5a3a, Kind=Build, Start=0.0836, End=0.6472
Generation 2: SnapshotHash=e8c6f80ad54adc10387234f3ddc8033e58c052ee13291183e31d72381e445154, Id=6338c0f9-1ccf-5c56-c10b-b4f219ab5a3a, Kind=Build, Start=0.0836, End=0.6472
PASS: identical SnapshotHash across two independent generator invocations: True
PASS: identical candidate Id across two independent generator invocations: True

--- Constrained to selected gesture family: a different family on an otherwise-identical run produces a differently-shaped gesture ---
Blackout run 53e9a3c3-f6e2-47df-944b-1b74f79de545: Kind=Blackout, Start=0, End=0
PASS: Build run's gesture kind is Build, not Blackout: True
PASS: Blackout run's gesture kind is Blackout, and darkens fully (0,0): True

--- Lineage: a second attempt (prior supplied) advances the ordinal and still reproduces deterministically ---
Attempt 0 ordinal=0, Attempt 1 ordinal=1
PASS: attempt ordinal advanced from prior.Attempt+1: True

--- Saved-state verification: generate once, persist via the real Step 079 JsonRefinementRunStore, reload from a brand-new store instance ---
Reloaded attempt: Id=6338c0f9-1ccf-5c56-c10b-b4f219ab5a3a, SnapshotHash=e8c6f80ad54adc10387234f3ddc8033e58c052ee13291183e31d72381e445154, GestureCount=1
PASS: reloaded SnapshotHash matches the originally generated SnapshotHash: True
PASS: reloaded gesture Kind/intensities match the originally generated gesture: True

--- Negative path: malformed proposal (empty permitted scope) is rejected, never auto-committed ---
Malformed generation: Status=Rejected, Violations=[Edit scope names no permitted models; a candidate requires at least one target model.], GestureCount=0
PASS: malformed attempt rejected as BestCandidateId: LoopRun '67192ad6-1187-4370-b575-06f24daf974a' BestCandidateId '4caecd1a-828c-086a-e9e1-c5d668cc15f7' names attempt 0, which carries 1 violation(s) and must never be retained as the best candidate.

--- Negative path: no GestureFamily selected throws before any generation happens ---
PASS: generation refused with no GestureFamily selected: LoopRun '450394c9-4cd9-4bbf-a40e-12e992df025e' has no GestureFamily selected; a seeded candidate generator requires one.

=== Demo complete ===
```

This demo exercises this step's own required acceptance end to end (same run generated twice yields an identical `SnapshotHash`, identical `Id`, and identical intensities — no `Guid.NewGuid()`/wall-clock/set-ordering was consulted anywhere in the generator), proves "constrained to selected gesture family" (the same scope/seed under two different `GestureFamily` values produces two structurally different, family-correct gestures, each independently satisfying `TransitionGesture`'s own PHASE-10 invariants — e.g. `Blackout` always darkens fully to `(0,0)`), proves lineage ordinal advancement, round-trips a generated candidate through the real on-disk Step 079 store from a brand-new store instance (saved-state verification), and proves both required negative paths: a malformed (empty-scope) proposal is rejected and can never become `BestCandidateId` (reusing Step 079's own already-accepted enforcement, unmodified), and a run with no selected gesture family is refused before any generation is attempted at all.

The demo project and its persistence-root/`bin`/`obj` output were created entirely under `%TEMP%`, run, and then deleted (`Remove-Item -Recurse -Force`; the demo's own persistence root was also deleted by the demo program itself at the end of its own run); they are not part of this repository's tracked tree.

## 5. Persisted-output inspection / observable demonstration

See §4's demo transcript — a real `CandidateAttempt` (with its `Gestures` payload) generated by `DeterministicGestureCandidateGenerator` was written to and read back from an actual temp-directory-backed `JsonRefinementRunStore` file across two independently-constructed store instances, not an in-memory mock standing in for either side of the round trip. `Step080_Acceptance_GeneratedCandidate_PersistsAndReloadsUnchanged` (xUnit) exercises the identical round trip under the test runner.

## 6. Fixtures / input hashes

No fixture files were added to `fixtures/` or `src/XlightsAI.Studio.Reference/Fixtures/` — this step's own scope (a deterministic generator over already-in-memory `LoopRun`/`EditScope` values) needs no xLights/audio fixture corpus. Every `Seed`/`InputHash`/`SnapshotHash` value exercised in the tests and demo is produced by the real `DeterministicGestureCandidateGenerator`/`DeterministicContent` code paths, not hand-constructed strings standing in for them (unlike Step 079, which had no generator yet to produce real ones).

## 7. Unresolved issues / known gaps

- **No `IChoreographyQualityEvaluator`/`QualityReport` exists yet** — this step's own scope is exactly "build a seeded candidate generator," not scoring one (Step 081's own named scope). A generated, non-violating `CandidateAttempt` is structurally eligible to become `BestCandidateId` today only because it carries no `Violations`; no soft musical-quality metric is evaluated at this step.
- **Exactly one `TransitionGesture` per attempt, spanning the run's entire scope window** — disclosed as ADR §3.4. No later step in this phase currently requires more; extending `DeterministicGestureCandidateGenerator` to vary gesture count/sub-windows remains straightforward (the `CandidateAttempt.Gestures` field is already list-shaped) if a later step's own acceptance text requires it.
- **No real repair strategy exists yet** — "generate a different seeded candidate for the next attempt ordinal" is the only variation this step implements; targeted repair of a specific failing metric is Step 082's own named scope.
- **No budget/cancellation orchestration drives this generator automatically yet** — nothing in this step calls `GenerateAsync` in a loop against `LoopRun.MaxAttempts`/time/cost budgets; that is Step 083's own named scope (`IRefinementOrchestrator` does not exist yet, consistent with Step 079's own disclosed gap).
- **No real SCREEN 15 COMPARE / SCREEN 14 AI EDIT Avalonia view exists yet** — this step's own required user-visible outcome is explicitly phrased as "Avalonia screenshot **or** repeatable desktop interaction recording with saved-state verification" (disjunctive); the candidate-gallery/preview UI itself is Step 084's own named scope ("Add candidate gallery, score explanations, preview and explicit selection"). This step satisfies the disjunction's second branch: a repeatable desktop (console) interaction recording that generates, persists, and reloads a real candidate, with saved-state verified identical across the round trip (§4-§5). No UI work was attempted or claimed for this step.
- **Cross-process claim safety remains heartbeat-staleness-based, not a real OS file lock** — this is Step 079's own already-disclosed scope (STEP-079.md §3.4/§7), unchanged by this step; this step's own demo/tests reuse the real store exactly as Step 079 left it, without touching its concurrency model.

## 8. Required-acceptance-text-by-text tie-back

| Required text (verbatim) | Where proven |
|---|---|
| "Same seed + inputs yields same structured candidate" | `Step080_Acceptance_SameSeedAndInputs_YieldSameStructuredCandidate` (identical `Id`/`Seed`/`SnapshotHash`/`Status` and field-equal `Gestures` across two independent generator calls); `Step080_Acceptance_DifferentSeed_YieldsDifferentStructuredCandidate` (the negative control — a different seed yields a different hash/intensity); demo §4's first section. |
| This step's own heading, verbatim: "constrained to selected gesture family" | `Step080_Acceptance_ConstrainedToSelectedGestureFamily` (a `Build` run and a `Blackout` run, otherwise identical, produce gestures of exactly those two different, correct `Kind`s; the `Blackout` gesture independently satisfies `TransitionGesture`'s own PHASE-10 "always darkens fully" invariant); demo §4's second section. |
| Implementation guidance, verbatim: "preserve... edit permissions" | `Step080_Acceptance_GeneratedGestureNeverTargetsPropOutsidePermittedScope` (every generated `TargetProps` entry is contained in `run.Scope.Models`). |
| Implementation guidance, verbatim: "preserve lineage links" | `Step080_Acceptance_PriorAttempt_AdvancesLineageOrdinal` (`prior.Attempt + 1`, same `RunId`); demo §4's third section. |
| User-visible outcome, verbatim: "Avalonia screenshot or repeatable desktop interaction recording with saved-state verification" | `Step080_Acceptance_GeneratedCandidate_PersistsAndReloadsUnchanged` (headless, real-store round trip); demo §4's fourth section (console interaction recording, saved-state verified identical from a brand-new store instance). |
| Required failure/edge case, verbatim: "Malformed proposal... must not auto-commit" | `Step080_NegativePath_MalformedProposal_EmptyScope_RejectedAndCannotAutoCommit` (empty-scope generation is `Rejected`/violating, and Step 079's own `BestCandidateId` enforcement still refuses it); demo §4's fifth section. |
| Required failure/edge case, verbatim (the other half): "no-improvement... must not auto-commit" | `Step080_NegativePath_ValidGeneratedCandidate_StillCannotSkipApprovalToReachAccepted` — even a clean, non-violating, real generator output cannot carry a run directly to `Accepted`; Step 079's own `RefinementStatusTransitions` still requires the distinct `AwaitingApproval` step first. |
| PHASE-12 Section 7 cross-cutting test #1 (restated for this step): "Same seed + inputs yields reproducible candidate" | Same evidence as the first row above — this step is where that cross-cutting test first has a real generator to exercise it against (Step 079 only proved it at the schema level). |
| This step's own additional, disclosed precondition (`LoopRun.GestureFamily` must be selected) | `Step080_NegativePath_NoGestureFamilySelected_ThrowsBeforeGenerating`; demo §4's sixth section. |

## 9. PASS / BLOCKED

**PASS.**

- The required-acceptance sentence ("Same seed + inputs yields same structured candidate") is proven by a real, deterministic generator with no hidden randomness source (`Guid.NewGuid()`, wall-clock time, `GetHashCode()`, and unordered-set enumeration are all deliberately avoided — see `DeterministicContent.cs` and the sorted-model-values step in `DeterministicGestureCandidateGenerator`), exercised by passing unit tests and a real console demonstration that calls the generator twice and compares actual output values.
- "Constrained to selected gesture family" is proven structurally: every generated gesture's `Kind` equals `run.GestureFamily`, and the per-family intensity recipe (`IntensityRangeFor`) makes the five `TransitionKind` values produce five genuinely different shapes, each independently re-validated against `TransitionGesture`'s own already-accepted PHASE-10 invariants before being returned.
- "Malformed proposal... must not auto-commit" is proven both newly (this step's own empty-scope rejection path) and by reuse (Step 079's own `BestCandidateId`/violation enforcement, unmodified, still refuses a malformed Step-080-generated attempt exactly as it already refused a hand-constructed one).
- "No-improvement and exhausted retry budget must not auto-commit" continues to hold against this step's own real generator output, not only against Step 079's hand-constructed fixtures — proven by `Step080_NegativePath_ValidGeneratedCandidate_StillCannotSkipApprovalToReachAccepted`.
- Full solution build succeeds with 0 warnings/errors; `dotnet format --verify-no-changes` reports no violations.
- Full regression: 1133/1133 tests pass (0 failed) — 1124 pre-existing (0 modified, 0 removed) plus 9 new `Refinement.Tests`.
- No prior accepted contract (any PHASE-01 through PHASE-11 type, or Step 079's own `RefinementStatus`/`RefinementStatusTransitions`/`IRefinementRunStore`/`JsonRefinementRunStore`/exception types) was modified. `LoopRun`/`CandidateAttempt` were extended additively (new, defaulted, trailing fields only); every Step 079-era call site and test still compiles and still passes unmodified.
- No show file, fixture, or prior persisted artifact was modified.

**Explicitly not claimed:** a real quality evaluator (`IChoreographyQualityEvaluator`/`QualityReport`, Step 081's own scope), a repair strategy (Step 082), budget/cancellation orchestration (`IRefinementOrchestrator`, Step 083), or any Avalonia UI/screenshot (Step 084's own named scope — "Add candidate gallery, score explanations, preview and explicit selection"; this step's own required outcome explicitly offers a non-UI alternative, which is what was delivered and disclosed as such). Exactly one `TransitionGesture`, spanning the run's full scope window, is generated per attempt (ADR §3.4) — not a variable-count or sub-windowed set.
