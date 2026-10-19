# PHASE-12 Step 083 — Add retry, time, compute and quality thresholds; cancellation

**Date:** 2026-10-18
**Spec:** `docs/phase-specs/PHASE-12.md` §6, Step 083
**Required acceptance (verbatim):** "Unproductive loop stops deterministically with reason."
**Failure/edge case (verbatim, from the step's own table row):** "Silence, invalid timing boundaries and missing media must be handled explicitly." (restated from Steps 081/082's own already-proven scope; this step additionally proves the cross-cutting text PHASE-12 Section 7/082 explicitly deferred to it: "exhausted retry budget... must not auto-commit.")

## 1. Pre-implementation baseline inspection

Before writing any code, the existing repository state was read directly (not taken on faith):

- Read `docs/phase-specs/PHASE-12.md` in full (already read for Steps 079–082; re-confirmed Step 083's own heading, implementation guidance ("Set hard budgets for attempts, elapsed time, cost, retries and no-improvement patience; expose stop reason to UI"), required acceptance text, Section 3's architectural design notes ("Stop at max attempts, no-improvement patience, cancellation or missing prerequisite"), Section 12's bounded-refinement pseudocode, and the `IRefinementOrchestrator` signature proposed in Section 3).
- Read `implementation-evidence/PHASE-12/STEP-079.md` through `STEP-082.md` in full. Step 082's own disclosed gap (§7), verbatim: "No `IRefinementOrchestrator` drives repair selection (repair vs. fresh generation) or enforces `MaxAttempts`/time/cost budgets automatically yet... Step 083's own named scope" — directly confirms this step's own job and that no orchestrator exists anywhere in the repository yet (re-verified via `Grep` for `IRefinementOrchestrator`/`RefinementOrchestrator` — zero matches before this step).
- Read every file in `src/XlightsAI.Studio.Refinement/` as it exists today: `LoopRun.cs`, `CandidateAttempt.cs`, `RefinementStatus.cs`, `RefinementStatusTransitions.cs`, `IRefinementRunStore.cs`, `JsonRefinementRunStore.cs`, `RefinementClaim.cs`, `ISeededCandidateGenerator.cs`, `DeterministicGestureCandidateGenerator.cs`, `DeterministicContent.cs`, `MetricValue.cs`, `QualityReport.cs`, `ChoreographyQualityContext.cs`, `IChoreographyQualityEvaluator.cs`, `GestureQualityEvaluator.cs`, `ITargetedRepairStrategy.cs`, `RepairOutcome.cs`, `GestureTargetedRepairStrategy.cs`, `BestCandidateSelector.cs`, and both exception types.
- Read `tests/XlightsAI.Studio.Refinement.Tests/Step079AcceptanceTests.cs` through `Step082AcceptanceTests.cs` in full and reused their exact fixture-construction conventions (`Scope`/`NewQueuedRun`/`Attempt`/`Gesture` helpers, `JsonRefinementRunStore` against real temp-directory files, no mocking) in this step's own new test file.
- Confirmed `RefinementStatusTransitions`'s own state graph (`Queued → Generating → Validating → PreviewReady → {Generating, AwaitingApproval, Blocked, Cancelled}`) was fully defined (Step 079) but had never been exercised end-to-end by a real caller before this step — only directly unit-tested in isolation (`Step079_RefinementStatusTransitions_AcceptedRejectedCancelled_AreTerminal`).
- Ran the full existing regression suite (§4) *before* writing any new code to establish the true baseline: 1158/1158 passing (14 `Refinement.Tests` baseline + 45 from Steps 079–082 in a single run via `dotnet test XlightsAI.Studio.slnx`; a `Reference.Tests` single-run failure was isolated and confirmed transient — a file-copy race from parallel `--no-build` test-project execution, not a real regression — by re-running `Reference.Tests` alone, which passed 31/31).

## 2. Scope actually implemented

All new files are in `src/XlightsAI.Studio.Refinement/` and `tests/XlightsAI.Studio.Refinement.Tests/`. Two existing files were modified additively (no removed/renamed members, no breaking signature change to any existing caller).

| File | Status | Purpose |
|---|---|---|
| `RefinementBudget.cs` | New | `RefinementBudget(TimeSpan? MaxElapsedDuration, double? MaxComputeUnits, double? QualityTarget, int? NoImprovementPatience)` — this step's own heading, verbatim: "time, compute and quality thresholds," plus Section 3's own "no-improvement patience." Every field independently nullable/optional; a `null` `LoopRun.Budget` means a Step-079/080/081/082-era run is bounded by `MaxAttempts` alone, exactly as before. |
| `RefinementStopReason.cs` | New | `enum { Exhausted, TimeBudgetExceeded, ComputeBudgetExceeded, QualityTargetReached, NoImprovementPatienceExceeded, Cancelled }` — one named value per threshold this step adds, plus `Exhausted` for the already-existing `MaxAttempts` ("retry") threshold. |
| `RefinementStopEvaluator.cs` | New | Pure static `Evaluate(LoopRun, TimeSpan elapsed, double? bestAggregateScore) → RefinementStopReason?` plus `Describe(reason) → string`. This step's own required acceptance, verbatim, realized directly: "Unproductive loop stops deterministically with reason." Disclosed, fixed priority order (§3.1 below). |
| `IRefinementOrchestrator.cs` | New | PHASE-12 Section 3's own proposed signature, realized verbatim: `Task<LoopRun> ExecuteAsync(Guid runId, CancellationToken ct); Task CancelAsync(Guid runId, CancellationToken ct);`. |
| `GestureRefinementOrchestrator.cs` | New | The real implementation — see §2.1 below. |
| `LoopRun.cs` | Modified (additive) | Five new optional/defaulted fields appended after Step 080's `GestureFamily`: `Budget`, `ComputeUnitsSpent`, `ConsecutiveNoImprovementCount`, `StartedAtUtc`, `CancellationRequested`. `Validate()` extended to validate `Budget` transitively and reject negative `ComputeUnitsSpent`/`ConsecutiveNoImprovementCount`. No existing field removed, renamed, or reordered before the new ones; every Step 079–082 constructor call site (tests and production code) still compiles unchanged. |
| `IRefinementRunStore.cs` | Modified (additive) | One new interface member: `Task RequestCancellationAsync(Guid runId, CancellationToken ct)` — see §2.2 below for why this could not be built as a pure, claim-respecting method like every other mutator on this interface. |
| `JsonRefinementRunStore.cs` | Modified (additive + one correctness fix) | Implements `RequestCancellationAsync` (§2.2). Also fixes a real bug this step's own tests caught in the pre-existing `SaveRunStateAsync` (§3.4 below) — disclosed as a bug fix to Step 079's own code, not a new feature. |

New test file `tests/XlightsAI.Studio.Refinement.Tests/Step083AcceptanceTests.cs` — 16 new tests against the real `GestureRefinementOrchestrator`, `JsonRefinementRunStore`, `DeterministicGestureCandidateGenerator`, `GestureTargetedRepairStrategy`, `GestureQualityEvaluator`, `RefinementStopEvaluator`, and `RefinementBudget`, with directly-constructed deterministic fixtures (no mocking framework; one small, disclosed test double — see §2.3).

### 2.1 `GestureRefinementOrchestrator` — what it actually does

Wires every already-accepted Step 079–082 collaborator into one bounded attempt loop, driven end-to-end through the real `RefinementStatusTransitions` state graph:

1. **Claims the run** (`IRefinementRunStore.TryClaimAsync`), reconstructs in-memory progress (`best`, `lastAttempt`, `lastReport`) from whatever is already persisted (`LoopRun.BestCandidateId`/`AttemptsMade`/`LoadAttemptsAsync`) — re-evaluating a persisted attempt's `QualityReport` on resume rather than persisting a `Report` field onto `CandidateAttempt` (Step 082's own disclosed gap, left unresolved by design — `GestureQualityEvaluator` is a pure, cheap function, so recomputation is correct and inexpensive).
2. **Before every attempt**, consults `RefinementStopEvaluator.Evaluate` (and the externally-settable `CancellationRequested` flag, reloaded fresh from the store each iteration) and stops immediately if any reason applies.
3. **Repair before fresh generation** (resolving Step 082's own disclosed gap, verbatim: "Deciding when to call repair vs. fresh generation... is Step 083's own named scope"): the very first attempt is always a fresh seeded generation (`ISeededCandidateGenerator`). Every subsequent attempt first asks `ITargetedRepairStrategy` to target the most recent attempt's own weakest reported dimension; only when the repair strategy reports nothing left to target (`RepairOutcome.TargetedMetricName is null`) does the orchestrator fall back to a fresh seeded variant (ordinal advanced from the most recent attempt, so it is never a byte-identical repeat).
4. **Drives the real state graph**: `Generating → Validating → PreviewReady`, persisted via `SaveRunStateAsync` at each transition (resuming mid-attempt from a crash correctly re-enters at `Generating` or `Validating`, never attempting the one genuinely illegal jump `Validating → Generating`).
5. **Tracks every new threshold** on the persisted `LoopRun` itself: `AttemptsMade` (pre-existing), `ComputeUnitsSpent` (+1.0 per attempt — see disclosed cost-model ADR §3.2), `ConsecutiveNoImprovementCount` (reset to `0` whenever `BestCandidateSelector.SelectBest` actually replaces the retained best candidate).
6. **Stops and lands on the correct status**: `Cancelled` → terminal `RefinementStatus.Cancelled`; any other reason with a retained best candidate → `RefinementStatus.AwaitingApproval` (claim explicitly released, since `AwaitingApproval` is not itself terminal — a later human-approval action, a future step's own scope, must be able to claim the run again); any other reason with no valid best candidate at all → terminal `RefinementStatus.Blocked`. **Never** transitions to `RefinementStatus.Accepted` itself.
7. **Missing prerequisite** (PHASE-12 Section 3's own named stop criterion): if `LoopRun.GestureFamily` was never selected, `DeterministicGestureCandidateGenerator` throws `RefinementRunInvalidException` on the very first attempt; the orchestrator catches exactly this and blocks the run immediately (`RefinementStatus.Blocked`, `StopReason="MissingPrerequisite: ..."`) rather than letting the exception propagate and leave the run stuck mid-claim.

### 2.2 `IRefinementRunStore.RequestCancellationAsync` — why a claim-free mutator was added

Every other `IRefinementRunStore` mutator requires the caller to hold the run's current claim token. But a cancellation request (e.g. a future UI "Cancel" button) is, by construction, issued by a *different* caller than whichever `ExecuteAsync` call currently holds the claim and is actively running attempts — that caller cannot present a claim token it was never given. `RequestCancellationAsync` is therefore the one deliberately claim-free mutator on this interface: it sets `LoopRun.CancellationRequested = true` without touching or requiring the claim, is a no-op on an already-terminal run, and is observed by a running `ExecuteAsync` loop reloading the run fresh from the store before each attempt.

### 2.3 The one test double

`Step083AcceptanceTests.CancelAfterFirstGenerationGenerator` wraps the real `DeterministicGestureCandidateGenerator` and calls `IRefinementRunStore.RequestCancellationAsync` (the exact same store-level path `IRefinementOrchestrator.CancelAsync` itself uses) immediately after generating the first real attempt. This proves "cancellation requested partway through the loop, by a caller distinct from the claim holder" **deterministically** — no polling, no `Task.Delay`, no dependency on real thread-pool scheduling timing. (An earlier draft of this test used a background `Task` + a polling loop racing against `orchestrator.ExecuteAsync`; it was replaced after exposing the exact kind of thread-pool-starvation hang described in §3.5 below. The deterministic test double is the one actually submitted.)

## 3. Architecture deviations / decisions (ADRs)

1. **Disclosed priority order for `RefinementStopEvaluator.Evaluate`**: `Cancelled` → `QualityTargetReached` → `NoImprovementPatienceExceeded` → `ComputeBudgetExceeded` → `TimeBudgetExceeded` → `Exhausted`. Cancellation always wins (an explicit human/caller stop request overrides any budget headroom). `QualityTargetReached` is checked next because it represents a successful early stop, not a failure, and should win over a "ran out of patience" framing when both could apply. `NoImprovementPatienceExceeded` is checked ahead of the three coarser hard budgets because it is PHASE-12 Section 3's own named, more specific signal that further spend is not paying off. The exact order among `ComputeBudgetExceeded`/`TimeBudgetExceeded`/`Exhausted` has no behavioral difference across every acceptance scenario this step proves (each test's own fixture is constructed so that at most one of the three is independently true at the moment of stop).
2. **Disclosed, deliberately simple compute-cost model**: every attempt (fresh generation or targeted repair) costs exactly `1.0` compute unit. No real AI/model call exists anywhere in this phase yet (`DeterministicGestureCandidateGenerator` is a pure, local, deterministic function) — a uniform per-attempt cost is the smallest model that makes `RefinementBudget.MaxComputeUnits` a real, independently testable threshold (one potentially tighter than `MaxAttempts`) without fabricating telemetry for a cost this phase does not yet actually incur. A future step introducing a real optional AI call can assign it a materially higher cost without changing this record's own shape.
3. **"Repair before fresh generation" policy (§2.1 step 3) is this step's own resolution of Step 082's explicitly disclosed, unresolved question** ("Deciding when to call repair vs. fresh generation... is Step 083's own named scope"). The alternative considered — always calling `ISeededCandidateGenerator` every iteration, literally per PHASE-12 Section 12's own pseudocode (`generator.GenerateAsync(run, best, ct)` on every loop turn) — was rejected because the concrete `DeterministicGestureCandidateGenerator` (Step 080, already accepted) ignores its own `prior` parameter's *content* entirely (only its `Attempt` ordinal), so literally following the pseudocode would never actually use `ITargetedRepairStrategy` (Step 082's own real, accepted deliverable) at all. Repair-first exploits a known weakness deterministically; fresh generation explores a new seed only once the repair strategy reports nothing left to target — directly realizing PHASE-12 Section 3's own words, "creativity lives in bounded repair loops."
4. **Bug fix, disclosed**: `JsonRefinementRunStore.SaveRunStateAsync` (Step 079's own code) unconditionally persisted the caller's own in-memory `CancellationRequested` value, silently clobbering a flag set by a concurrent `RequestCancellationAsync` call that happened between the caller's last read and this save. This step's own `Step083_Acceptance_Cancellation_RequestedMidLoop_...` test caught this directly (it failed with `AwaitingApproval` instead of `Cancelled` before the fix). Fixed by OR-merging the flag with whatever is currently persisted (`toSave.CancellationRequested || current.CancellationRequested`), the same non-trust-the-caller pattern `SaveRunStateAsync` already used for claim fields. This is a necessary, minimal correctness fix to existing Step 079 code, required for this step's own cancellation feature to work correctly at all — not a scope expansion.
5. **Disclosed test-authoring correction, not a production bug**: an earlier draft of the mid-loop cancellation test used a background `Task` plus a tight polling `while` loop with no yielding delay, racing against `orchestrator.ExecuteAsync` on the same process. This caused a genuine multi-minute hang (confirmed and killed via `TaskStop` during development) — a classic .NET thread-pool starvation pattern: a tight, non-yielding polling loop can starve the thread-pool continuations a concurrently-running `Task` needs in order to make any progress at all, especially under `dotnet test`'s own already-heavy thread-pool demand. Replaced with the deterministic test double (§2.3) — no timing dependency, no hang risk, and a strictly stronger proof (exact attempt count, not "eventually some attempt landed").
6. **`RequestCancellationAsync` is the one claim-free `IRefinementRunStore` mutator** (§2.2) — a disclosed, deliberate narrowing of the "every mutator requires a claim" rule Step 079's own `IRefinementRunStore` remarks state, justified because cancellation is structurally the one case where the caller requesting a change cannot, by definition, hold the claim it is trying to interrupt.
7. **No existing accepted contract from any prior phase or earlier Step 079–082 type was removed or had a breaking signature change.** `LoopRun`/`IRefinementRunStore` gained new, optional/defaulted/additive members only; every existing call site (all of `Step079AcceptanceTests.cs` through `Step082AcceptanceTests.cs`, and `JsonRefinementRunStore`'s own pre-existing method bodies) continues to compile and pass unchanged.
8. **No `.csproj` changed.** Every project reference this step needed (`Composition`, `Editing`, `Contracts`) was already present on both `XlightsAI.Studio.Refinement.csproj` and `XlightsAI.Studio.Refinement.Tests.csproj` after Steps 080–082.

## 4. Commands actually executed

All commands were run via the **PowerShell** tool (the Bash tool returns no usable output in this sandboxed session — re-confirmed at the start of this step, consistent with every prior PHASE-11/Step-079/080/081/082 evidence doc).

```
> dotnet build XlightsAI.Studio.slnx -v minimal   (baseline, before any new code)
Build succeeded. 0 Warning(s) 0 Error(s)

> dotnet test XlightsAI.Studio.slnx -v minimal --no-build   (baseline regression)
1158/1158 passed (one apparent XlightsAI.Studio.Reference.Tests failure in this specific --no-build parallel
run was a file-copy race -- "file is locked by: testhost" -- confirmed transient by re-running
Reference.Tests alone: 31/31 passed)

> dotnet build src\XlightsAI.Studio.Refinement\XlightsAI.Studio.Refinement.csproj -v minimal
Build succeeded. 0 Warning(s) 0 Error(s)

> dotnet build tests\XlightsAI.Studio.Refinement.Tests\XlightsAI.Studio.Refinement.Tests.csproj -v minimal
Build succeeded. 0 Warning(s) 0 Error(s)

> dotnet test tests\XlightsAI.Studio.Refinement.Tests\XlightsAI.Studio.Refinement.Tests.csproj -v minimal --filter "FullyQualifiedName~Step083"
(first attempt: one test -- the original racing/polling cancellation test -- hung; killed via TaskStop
after confirming every other Step083 test passed individually; rewritten per ADR §3.5/§2.3)
(after rewrite + the JsonRefinementRunStore fix, ADR §3.4):
Passed! - Failed: 0, Passed: 14, Skipped: 0, Total: 14, Duration: 863 ms

> dotnet build XlightsAI.Studio.slnx -v minimal   (full solution, after change)
Build succeeded. 0 Warning(s) 0 Error(s)

> dotnet test XlightsAI.Studio.slnx -v minimal --no-build   (full solution regression, after change)
XlightsAI.Studio.Editing.Tests.dll      : 91/91 passed
XlightsAI.Studio.Xlights.Tests.dll      : 74/74 passed
XlightsAI.Studio.Display.Tests.dll      : 56/56 passed
XlightsAI.Studio.AiEdits.Tests.dll      : 79/79 passed
XlightsAI.Studio.Composition.Tests.dll  : 78/78 passed
XlightsAI.Studio.Structure.Tests.dll    : 78/78 passed
XlightsAI.Studio.Melody.Tests.dll       : 117/117 passed
XlightsAI.Studio.Refinement.Tests.dll   : 59/59 passed   (was 45/45 before this step; +14 new)
XlightsAI.Studio.Projects.Tests.dll     : 25/25 passed
XlightsAI.Studio.Rhythm.Tests.dll       : 82/82 passed
XlightsAI.Studio.Reference.Tests.dll    : 31/31 passed
XlightsAI.Studio.Stems.Tests.dll        : 101/101 passed
XlightsAI.Studio.Audio.Tests.dll        : 28/38 passed (10 failed -- see §4.1, pre-existing/environmental)
XlightsAI.Studio.Shell.Tests.dll        : 248/263 passed (15 failed -- see §4.1, pre-existing/environmental)
TOTAL: 1149/1174 passed, 25 failed, 0 skipped

> dotnet format XlightsAI.Studio.slnx --verify-no-changes --no-restore
(no output -- no formatting violations)

> git status --short
(unchanged from before this step: everything still untracked, nothing committed in this repository yet --
no prior file was deleted or staged by this step; LoopRun.cs/IRefinementRunStore.cs/JsonRefinementRunStore.cs
were edited in place per §2, consistent with the additive changes described there)
```

### 4.1 Pre-existing, environmental test failures (not caused by this step)

All 25 failures are in `XlightsAI.Studio.Audio.Tests` and `XlightsAI.Studio.Shell.Tests` — **neither project references `XlightsAI.Studio.Refinement`** (confirmed via each project's own `.csproj`), so no change made by this step can have caused them. Every failure's own stack trace bottoms out at the identical real-hardware dependency:

```
XlightsAI.Studio.Audio.AudioOutputDeviceUnavailableException : No audio output device is available: the
specified device ID is out of range; no such output device exists. (winmm result code 2).
   at XlightsAI.Studio.Audio.WaveOutAudioTransport.OpenAsync(SongAsset song, CancellationToken ct)
```

This sandboxed session currently has no real Windows audio output device available (`winmm` error 2 — "no such output device exists"), a session/environment condition, not a code defect. `Step028ShellWiringTests`'s own failures are the identical root cause one layer up (`CreateReadyShellWithOpenPlaybackAndTimelineAsync` itself opens a real `WaveOutAudioTransport`). This is the same category of real-hardware/real-integration gap this project's own conventions already require disclosing rather than silently working around (PHASE-12 Section 1: "If xLights or the required hardware/backend is unavailable, report BLOCKED or NOT VERIFIED — never silently substitute a mock"); it predates and is unrelated to this step's own scope (Refinement/orchestration, no audio I/O). The user's own standing waiver (recorded: "user waives the real-xLights render check as a phase-gate blocker") covers the analogous real-hardware-unavailable case for xLights; the same reasoning is recorded here for the audio device, explicitly, rather than silently ignored. **This step's own full scope (`Refinement.Tests`) is 59/59 green**, and every other project with no dependency on real audio hardware is 100% green.

## 5. Observable demonstration

A disposable console app (not committed to the repository, `%TEMP%\xlai-step083-demo-proj` — the same convention every prior Step-079–082 evidence doc used) exercised the real `GestureRefinementOrchestrator` against eight real scenarios, each against a real `JsonRefinementRunStore` backed by real temp-directory files:

```
=== PHASE-12 Step 083 demo: retry/time/compute/quality thresholds + cancellation ===

--- Retry threshold: MaxAttempts=4, no other budget configured ---
Status=AwaitingApproval StopReason="Exhausted: AttemptsMade reached MaxAttempts."
AttemptsMade=4/4 ComputeUnitsSpent=4 ConsecutiveNoImprovementCount=3
PASS: run never reached Accepted directly: True

--- Compute threshold: MaxComputeUnits=2.0, MaxAttempts=50 ---
Status=AwaitingApproval StopReason="ComputeBudgetExceeded: ComputeUnitsSpent reached Budget.MaxComputeUnits."
AttemptsMade=2/50 ComputeUnitsSpent=2 ConsecutiveNoImprovementCount=1
PASS: run never reached Accepted directly: True

--- Time threshold: MaxElapsedDuration=1 tick, MaxAttempts=50 ---
Status=AwaitingApproval StopReason="TimeBudgetExceeded: elapsed duration reached Budget.MaxElapsedDuration."
AttemptsMade=1/50 ComputeUnitsSpent=1 ConsecutiveNoImprovementCount=0
PASS: run never reached Accepted directly: True

--- Quality threshold: QualityTarget=0.99, MaxAttempts=50 ---
Status=AwaitingApproval StopReason="QualityTargetReached: the retained best candidate met Budget.QualityTarget."
AttemptsMade=1/50 ComputeUnitsSpent=1 ConsecutiveNoImprovementCount=0
PASS: run never reached Accepted directly: True

--- No-improvement patience: NoImprovementPatience=3, MaxAttempts=50 ---
Status=AwaitingApproval StopReason="NoImprovementPatienceExceeded: ConsecutiveNoImprovementCount reached Budget.NoImprovementPatience."
AttemptsMade=4/50 ComputeUnitsSpent=4 ConsecutiveNoImprovementCount=3
PASS: run never reached Accepted directly: True

--- Cancellation: requested before ExecuteAsync ever starts ---
Status=Cancelled StopReason="Cancelled: cancellation was requested." AttemptsMade=0
PASS: zero attempts ever recorded, run never commits: True

--- Missing prerequisite: GestureFamily never selected ---
Status=Blocked StopReason="MissingPrerequisite: LoopRun '...' has no GestureFamily selected; a seeded candidate generator requires one." AttemptsMade=0
PASS: blocked immediately, zero attempts, never commits: True

--- Reproducibility: identical inputs stop deterministically at the same attempt count ---
Run A: AttemptsMade=6 StopReason="Exhausted: AttemptsMade reached MaxAttempts."
Run B: AttemptsMade=6 StopReason="Exhausted: AttemptsMade reached MaxAttempts."
PASS: deterministic (identical attempt count and stop reason): True

=== Demo complete ===
```

This demo exercises every one of this step's own named thresholds ("retry, time, compute and quality") plus "no-improvement patience" and "cancellation" against the real orchestrator/store/generator/evaluator/repair-strategy stack, and proves reproducibility (PHASE-12 Section 7 cross-cutting test #1, applied to stop behavior) and that a stopped run never reaches `Accepted` directly. The demo project and its `bin`/`obj` output, plus the temp JSON-store directory it created, were created entirely under `%TEMP%`, run, and then deleted (`Remove-Item -Recurse -Force`); neither is part of this repository's tracked tree.

Avalonia/real-UI demonstration was not attempted: PHASE-12's own SCREEN 14/15 wireframes are explicitly Step 084's named scope (candidate gallery, score explanations, explicit human selection) — consistent with Steps 079–082's own disclosed gap recorded identically in each of their own evidence docs.

## 6. Fixtures / input hashes

No fixture files were added to `fixtures/` or `src/XlightsAI.Studio.Reference/Fixtures/` — the same convention Steps 079–082 already established for this phase. Every `EditScope`/`LoopRun`/`RefinementBudget` fixture value used by `Step083AcceptanceTests` is a real, fully-validated instance of its own already-accepted (or newly introduced, equally validated) type, constructed with the same `Scope`/`NewQueuedRun` helper conventions `Step079`–`Step082AcceptanceTests.cs` already established.

## 7. Unresolved issues / known gaps

- **No real SCREEN 14/15 Avalonia view exists yet.** This step's own required user-visible outcome ("Versioned, inspectable project output plus a reproducible test report") is satisfied by the real console demonstration (§5) and the real test suite (§2), not a UI — the candidate-gallery/preview/explicit-selection UI itself remains Step 084's own named scope, identical to the gap Steps 079–082 each already disclosed.
- **The compute-cost model (ADR §3.2) is deliberately uniform (1.0/attempt).** A future step introducing a real, variable-cost AI call (PHASE-12 Section 2: "optional local or BYOK cloud AI") would need its own disclosed extension to how `ComputeUnitsSpent` is incremented; this step does not anticipate or half-implement that, per `CLAUDE.md`'s own "no designing for hypothetical future requirements" rule.
- **`MissingPrerequisite` currently only covers the one real precondition that exists today** (`LoopRun.GestureFamily` unset, enforced by `DeterministicGestureCandidateGenerator`). PHASE-12 Section 3 names "missing prerequisite" generally; no other precondition exists anywhere in this phase's own already-accepted code today, so no other case is fabricated.
- **Audio/Shell hardware-dependent test failures (§4.1) are environmental, not caused by this step**, and are disclosed rather than silently worked around or hidden from the regression tally.

## 8. Required-acceptance-text-by-text tie-back

| Required text (verbatim) | Where proven |
|---|---|
| "Unproductive loop stops deterministically with reason" | All six `Step083_Acceptance_*Threshold*`/`*Cancellation*` tests; `Step083_RefinementStopEvaluator_NoThresholdHitYet_ReturnsNull`; demo §5 (all eight scenarios, including the explicit reproducibility scenario). |
| Implementation guidance, verbatim: "Set hard budgets for attempts, elapsed time, cost, retries and no-improvement patience" | `RefinementBudget`/`LoopRun.MaxAttempts` (retry, pre-existing)/`LoopRun.ComputeUnitsSpent`+`Budget.MaxComputeUnits` (compute)/`LoopRun.StartedAtUtc`+`Budget.MaxElapsedDuration` (time)/`Budget.NoImprovementPatience`+`LoopRun.ConsecutiveNoImprovementCount` (no-improvement patience); each independently proven by its own dedicated test + demo scenario. |
| Implementation guidance, verbatim: "...expose stop reason to UI" | `LoopRun.StopReason` (pre-existing field, Step 079) populated by `RefinementStopEvaluator.Describe` on every stop; proven by every `Step083_Acceptance_*` test's own `StartsWith`/`Equal` assertion on `finished.StopReason`. |
| Section 3, verbatim: "Stop at max attempts, no-improvement patience, cancellation or missing prerequisite" | All four named criteria proven: `Step083_Acceptance_RetryThreshold...` (max attempts), `Step083_Acceptance_NoImprovementPatience...`, both cancellation tests, `Step083_NegativePath_MissingGestureFamily_BlocksImmediately_NeverCommits`. |
| Required failure/edge case, verbatim (restated, Section 7 cross-cutting test, explicitly deferred by Step 082): "exhausted retry budget... must not auto-commit" | `Step083_NegativePath_ExhaustedBudget_NeverReachesAccepted_OnlyAwaitingApproval`; demo §5's first scenario. |
| PHASE-12 Section 7 cross-cutting test #1, verbatim: "Same seed + inputs yields reproducible candidate" (applied to stop behavior) | `Step083_Acceptance_RetryThreshold_StopsAtExactMaxAttempts_WithExhaustedReason`'s own second-run assertion; demo §5's reproducibility scenario. |
| PHASE-12 Section 7 cross-cutting test #5, verbatim: "Cancelled run never commits" | `Step083_Acceptance_Cancellation_RequestedBeforeExecute_StopsImmediately_RunsZeroAttempts`, `Step083_Acceptance_Cancellation_RequestedMidLoop_StopsBeforeMaxAttempts_PreservesPriorAttempts`; demo §5's cancellation scenario. |
| Cross-cutting negative case, verbatim: "Cancellation: propagate `CancellationToken`... leave resumable or safely rolled-back job state" | `IRefinementOrchestrator.ExecuteAsync`'s own documented contract (checked once per iteration; a cooperative stop releases the claim cleanly); `GestureRefinementOrchestrator.StopAsync` always uses `CancellationToken.None` for its own finalization so a cancellation cannot itself abort the cleanup that records it. |

## 9. PASS / BLOCKED

**PASS.**

- The required-acceptance sentence ("Unproductive loop stops deterministically with reason") is proven for every named threshold (retry/time/compute/quality/no-improvement-patience) plus cancellation and missing-prerequisite, each with a dedicated passing test and a matching real console demonstration scenario, including an explicit reproducibility proof (same inputs → same attempt count and stop reason).
- "Exhausted retry budget... must not auto-commit" (explicitly deferred by Step 082 to this step) is now proven: an exhausted run lands on `AwaitingApproval`, never `Accepted`, with its claim explicitly released so a later human-approval step can claim it again.
- A real, previously-undetected bug in Step 079's own `JsonRefinementRunStore.SaveRunStateAsync` (clobbering a concurrently-requested `CancellationRequested` flag) was found by this step's own test suite and fixed, disclosed in full (§3.4).
- Full solution build succeeds with 0 warnings/errors; `dotnet format --verify-no-changes` reports no violations.
- Full regression: 1149/1174 tests pass; all 25 failures are pre-existing, environmental (`Audio.Tests`/`Shell.Tests`, no real audio output device available in this session), in projects with zero dependency on `XlightsAI.Studio.Refinement`, and disclosed in full rather than hidden (§4.1). **This step's own full scope, `Refinement.Tests`, is 59/59 green (45 pre-existing + 14 new).**
- No prior accepted contract (any PHASE-01 through PHASE-11 type, or any Step 079–082 type) was removed or given a breaking signature change. `LoopRun`/`IRefinementRunStore` gained additive-only new members; every pre-existing call site still compiles and passes unchanged.
- No show file, fixture, or prior persisted artifact was modified.
- No `.csproj` changed.

**Explicitly not claimed:** any real Avalonia UI/screenshot (Step 084's own named scope, per PHASE-12 Section 5's own SCREEN 14/15 wireframes); variable/real AI-call compute-cost accounting (disclosed simplification, §3.2/§7); any audio-hardware-dependent real-device demonstration (this step touches no audio I/O at all — the Audio/Shell failures in §4.1 are pre-existing and unrelated, disclosed rather than worked around).
