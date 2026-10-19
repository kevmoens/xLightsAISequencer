# PHASE-12 Step 079 — Create persisted run/candidate/result schema with hashes, seeds and state transitions

**Date:** 2026-10-17
**Spec:** `docs/phase-specs/PHASE-12.md` §6, Step 079
**Required acceptance (verbatim):** "Interrupted run resumes idempotently."
**Required failure/edge case (verbatim):** "Malformed proposal, no-improvement and exhausted retry budget must not auto-commit."

## 1. Pre-implementation baseline inspection

Before writing any code, the existing repository state was read directly (not taken on faith):

- **No `src/XlightsAI.Studio.Refinement/` or any PHASE-12 code exists yet.** `implementation-evidence/` has folders through `PHASE-11` only; `PHASE-11/STEP-078.md` is the last completed step (full solution: 1113/1113 tests passing at baseline, reconfirmed below).
- Read `docs/phase-specs/PHASE-12.md` in full. Section 3's own proposed `LoopRun`/`Candidate`/`QualityReport`/`MetricValue` signatures, the `RefinementStatus` state machine, and the bounded-refinement pseudocode (§12) are **proposed**, not existing code — Section 0/"standalone specification" note and the copy/paste prompt both say to reconcile proposed signatures with the real repository, not copy them blindly.
- Read every existing `src/*/*.csproj` to map the real dependency graph before adding a new project: `XlightsAI.Studio.Projects` (the only project using `Microsoft.Data.Sqlite`/`SqliteMigrationRunner`) is a leaf consumed only by `Shell`/`App`; no feature project (`Composition`, `Editing`, `AiEdits`) references it or SQLite. Every feature-level persisted aggregate in this repository (`JsonSequenceDocumentStore`, `JsonEditTransactionHistoryStore`, `JsonEditProposalAuditStore`, `JsonSelectionContextStore`, `JsonVisualStoryStore`, etc.) is instead a one-file-per-aggregate, atomic-temp-file-plus-move JSON store. This step follows that established convention rather than inventing a new SQLite table that would duplicate, not share, state.
- Read `src/XlightsAI.Studio.Editing/EditScope.cs`, `EditTransactionService.cs`, `JsonEditTransactionHistoryStore.cs`/`IEditTransactionHistoryStore.cs`/`EditTransactionHistorySnapshot.cs` (PHASE-03, already accepted) and `src/XlightsAI.Studio.Xlights/JsonSequenceDocumentStore.cs` (PHASE-02) for the exact atomic-write and optional-collaborator conventions this step reuses.
- Read `src/XlightsAI.Studio.AiEdits/ReadOnlyModelIdSetJsonConverter.cs`/`ReadOnlyGuidSetJsonConverter.cs`/`JsonSelectionContextStore.cs` (PHASE-11 Step 072) — the first place in the repository to persist an `EditScope` through `System.Text.Json`, and the reason a `LoopRun` carrying an `EditScope` needs the same two converters.
- Read `src/XlightsAI.Studio.Composition/ChoreographyCandidate.cs`/`IChoreographyCompiler.cs` and `implementation-evidence/PHASE-10/STEP-071.md` — confirms that `ChoreographyPlan`, named in PHASE-12 Section 3's own proposed `Candidate(..., ChoreographyPlan Plan, ...)` signature, **does not exist anywhere in this repository**. PHASE-10 Steps 069–071 already disclosed narrowing that exact same proposed `ChoreographyPlan` concept to the concrete `ChoreographyCandidate` shape those steps actually needed. This step follows the same precedent (see §3.1 below) rather than inventing a placeholder `ChoreographyPlan` type with no generator (Step 080) to produce it.
- Confirmed via `Grep` that `QualityReport`/`IChoreographyQualityEvaluator` (PHASE-12 Section 3's own proposed evaluator contract, Step 081's own named scope) also do not exist anywhere yet.

## 2. Scope actually implemented

New project `src/XlightsAI.Studio.Refinement/` (added to `XlightsAI.Studio.slnx`), referencing `Contracts` and `Editing` only (verified neither references `Refinement`, so this is not a cyclic reference):

| File | Purpose |
|---|---|
| `RefinementStatus.cs` | The spec's own proposed state enum, verbatim: `Queued, Generating, Validating, PreviewReady, AwaitingApproval, Accepted, Rejected, Blocked, Cancelled`. |
| `RefinementStatusTransitions.cs` | The spec's own proposed state graph made explicit and enforced: `CanTransition(from, to)` / `IsTerminal(status)`. `Accepted`/`Rejected`/`Blocked`/`Cancelled` have no outgoing edge. `Accepted` is reachable **only** from `AwaitingApproval`. |
| `LoopRun.cs` | The spec's own proposed `LoopRun` record, realized with the resumable-job fields this step's own implementation guidance names verbatim ("resumable job states with input hashes, selected scope, attempts, last heartbeat, seeds and cancellation records"): `Id`, `DocumentId` (additive — see §3.2), `BaseRevision`, `Scope` (`EditScope`), `InputHash`, `Seed`, `MaxAttempts`, `AttemptsMade`, `Status`, `BestCandidateId`, `StopReason`, `ClaimToken`/`ClaimedAtUtc`/`HeartbeatAtUtc`, `CreatedAtUtc`. `Validate()` enforces shape invariants. |
| `CandidateAttempt.cs` | The spec's own proposed `Candidate` record, narrowed for this step's own scope (disclosed ADR §3.1): `Id`, `RunId`, `Attempt`, `Seed`, `SnapshotHash`, `Status`, `Violations`, `RecordedAtUtc`. `Validate()` enforces shape invariants. |
| `RefinementRunRecord.cs` | The full persisted shape of one run file: the `LoopRun` plus every `CandidateAttempt` recorded so far, keyed by attempt ordinal (idempotent overwrite, never append-duplicate). |
| `RefinementClaim.cs` | `TryClaimAsync`'s own success result: an opaque claim token plus the run state as of the claim. |
| `IRefinementRunStore.cs` / `JsonRefinementRunStore.cs` | The persistence boundary and its real, file-backed implementation: `CreateAsync`, `LoadRunAsync`, `LoadAttemptsAsync`, `TryClaimAsync`, `HeartbeatAsync`, `SaveRunStateAsync`, `SaveAttemptAsync`, `ReleaseClaimAsync`. One JSON file per run (`refinement-run-{id}.json`), atomic temp-file+move writes, a per-run in-process `SemaphoreSlim` guarding read-modify-write. |
| `RefinementRunInvalidException.cs` / `RefinementRunCorruptException.cs` | Mirror `CompositionInvalidException`/`CompositionCorruptException`'s own convention. |
| `ReadOnlyModelIdSetJsonConverter.cs` / `ReadOnlyGuidSetJsonConverter.cs` | This project's own copy of `AiEdits`'s identically-named converters (each feature project owns its own copy — the already-established convention, not a shared dependency), needed because `LoopRun.Scope` is an `EditScope`. |

New test project `tests/XlightsAI.Studio.Refinement.Tests/` (added to `XlightsAI.Studio.slnx`):

- `Step079AcceptanceTests.cs` — 11 tests against the real `JsonRefinementRunStore` (no mocking library, real temp-directory-backed on-disk files); every "crash"/"restart" is modeled by constructing a brand-new store instance pointed at the same directory, never by reusing an in-memory object graph.

## 3. Architecture deviations / decisions (ADRs)

1. **`CandidateAttempt` narrows the spec's own proposed `Candidate(Guid Id, Guid RunId, int Attempt, int Seed, string SnapshotHash, ChoreographyPlan Plan, QualityReport? Report)` — no `Plan` or `Report` field exists on this step's own record.** Neither `ChoreographyPlan` nor `QualityReport` exists anywhere in this repository (confirmed by Grep, §1). PHASE-10 Steps 069–071 already disclosed the identical narrowing of the spec's own proposed `ChoreographyPlan` concept down to the concrete `ChoreographyCandidate` shape that step actually needed (`STEP-071.md`). Declaring a `Plan`/`Report` field now, with no generator (Step 080) or evaluator (Step 081) yet built to populate it, would be exactly the "half-finished implementation" the project's own guardrails forbid. `SnapshotHash` stands in for the plan's own reproducibility proof (Section 7 cross-cutting test #1: "Same seed + inputs yields reproducible candidate"), and `Violations` stands in for a hard-gate rejection outcome (this step's own required failure/edge case) without inventing a `QualityReport` shape Step 081 has not yet designed. Steps 080/081 are expected to extend `CandidateAttempt` additively, the same way Step 078 extended Steps 072–077's services with new optional constructors rather than breaking them.
2. **`LoopRun.DocumentId` is added, additively, to the spec's own proposed `LoopRun` signature.** The spec's own proposed `LoopRun` carries `BaseRevision` and `Scope` (an `EditScope`) but no document identity; an `EditScope` names a time/model boundary, never which `SequenceDocument` it applies to. Without `DocumentId`, a persisted run could not say which document's revision `BaseRevision` refers to. This is additive (every spec-named field is still present, unchanged), not a narrowing.
3. **No SQLite table was created for refinement runs, despite PHASE-01 Section 2's "Ownership" text naming SQLite for "project metadata and committed user edits."** Every other phase's own persisted feature-level aggregate (`EditTransaction` history, `SequenceDocument`, `EditProposalAuditRecord`, `SelectionContext`, `VisualStory`, etc.) is a JSON file per aggregate, not a SQLite table — `XlightsAI.Studio.Projects`'s own `SqliteMigrationRunner`/`studio.db` is the one place SQLite is actually used in this repository, and it is a generic key/value `project_state` table consumed only by `Shell`/`App`, never by a feature project. Inventing a new SQLite table reachable only from a new `Refinement` project would duplicate, not share, the pattern every other phase already uses for exactly this kind of resumable, versioned, hash/seed-bearing state. "Transactionally claimed work" (this step's own implementation guidance, verbatim) is instead realized as a claim-token/heartbeat-staleness protocol enforced by every mutating `IRefinementRunStore` method (see §3.4) — the correct scope for a local, single-user desktop app, consistent with `EditTransactionService`'s own optimistic-revision-check concurrency model (no SQL transaction there either).
4. **"Transactionally claimed work" is realized as claim-token possession plus heartbeat staleness, not a SQL transaction or a cross-process file lock.** `TryClaimAsync` grants a fresh opaque token when a run has never been claimed, or when the existing claim's heartbeat is older than the caller's own staleness window (the prior claimant is presumed crashed); every other mutating method (`SaveRunStateAsync`, `SaveAttemptAsync`, `HeartbeatAsync`, `ReleaseClaimAsync`) requires the caller to present the exact currently-held token, rejecting (`RefinementRunInvalidException`, no file write) a foreign or stale token. A per-run in-process `SemaphoreSlim` additionally serializes this *store instance's own* concurrent calls against the same run id so a read-modify-write never races itself. Cross-process safety relies entirely on the heartbeat-staleness check, not a real OS file lock — disclosed, not silently assumed, and proven sufficient for this step's own required acceptance by `Step079_Acceptance_InterruptedRun_ResumesIdempotently_WithoutDuplicatingWork` (two independent `JsonRefinementRunStore` instances against the same directory, the second reclaiming the first's abandoned claim) and `Step079_Acceptance_LiveClaim_CannotBeStolenBeforeItGoesStale` (a live heartbeat cannot be stolen).
5. **`SaveRunStateAsync` ignores the caller-supplied claim fields on its own `run` parameter and always derives the persisted claim fields from the currently-persisted record.** A terminal status (`RefinementStatusTransitions.IsTerminal`) always clears the claim (nothing is left to resume); any other status always keeps the currently-held token/claimed-at and refreshes the heartbeat to "now." This means a caller cannot accidentally (or maliciously) persist a fabricated claim state by constructing a `LoopRun` with made-up claim fields — only `TryClaimAsync`/`ReleaseClaimAsync` ever actually set them.
6. **`SaveRunStateAsync` rejects a `BestCandidateId` that names a missing attempt, or one whose `Violations` is non-empty, before writing anything.** This is the concrete mechanism behind this step's own required failure/edge case, verbatim: "Malformed proposal... must not auto-commit" — a violating attempt can structurally never become the retained best candidate, which is the only field a later step's commit path (Step 084) could read from. Proven by `Step079_NegativePath_MalformedAttempt_CannotBecomeBestCandidate_RunNeverReachesAccepted` and the demo's own negative-path section (§4).
7. **`RefinementStatusTransitions` makes `Accepted` reachable only from `AwaitingApproval`.** This is the concrete mechanism behind the other half of the same required failure/edge case: "no-improvement and exhausted retry budget must not auto-commit." An exhausted-budget or no-improvement stop can only ever be persisted as `AwaitingApproval` (or `Blocked`) — never directly as `Accepted` — so reaching `Accepted` structurally requires a distinct, later, human-approval transition that this step does not implement (Step 084's own named scope: "record deliberate human accept/reject"). Proven by `Step079_NegativePath_NoImprovementStop_CannotSkipApprovalAndReachAcceptedDirectly` and the demo's own illegal-jump section.
8. **No existing accepted contract from any prior phase was modified.** `EditScope`/`DocumentRevision`/`ModelId`/`TimeWindow` (PHASE-01/03) are consumed unchanged. `Refinement` is a brand-new, additive project; nothing in `Editing`, `Composition`, `AiEdits`, `Xlights`, or `Projects` was touched.

## 4. Commands actually executed

All commands were run via the **PowerShell** tool. (As in every prior PHASE-11 evidence doc, the Bash tool returns no usable output in this sandboxed session — every Bash invocation, including a bare `echo`, silently exits 1 with no output; this is disclosed, not worked around by claiming Bash output that never appeared.)

```
> dotnet --version
10.0.302

> dotnet build XlightsAI.Studio.slnx -v minimal   (baseline, before any change)
Build succeeded. 0 Warning(s) 0 Error(s)

> dotnet test XlightsAI.Studio.slnx -v minimal   (baseline, before any change)
XlightsAI.Studio.Xlights.Tests.dll      : 74/74 passed
XlightsAI.Studio.Editing.Tests.dll      : 91/91 passed
XlightsAI.Studio.Display.Tests.dll      : 56/56 passed
XlightsAI.Studio.Structure.Tests.dll    : 78/78 passed
XlightsAI.Studio.Melody.Tests.dll       : 117/117 passed
XlightsAI.Studio.Projects.Tests.dll     : 25/25 passed
XlightsAI.Studio.Composition.Tests.dll  : 78/78 passed
XlightsAI.Studio.Audio.Tests.dll        : 38/38 passed
XlightsAI.Studio.AiEdits.Tests.dll      : 79/79 passed
XlightsAI.Studio.Rhythm.Tests.dll       : 82/82 passed
XlightsAI.Studio.Shell.Tests.dll        : 263/263 passed
XlightsAI.Studio.Stems.Tests.dll        : 101/101 passed
XlightsAI.Studio.Reference.Tests.dll    : 31/31 passed
BASELINE TOTAL: 1113/1113 passed, 0 failed

> dotnet build src\XlightsAI.Studio.Refinement\XlightsAI.Studio.Refinement.csproj -v minimal
Build succeeded. 0 Warning(s) 0 Error(s)

> dotnet build tests\XlightsAI.Studio.Refinement.Tests\XlightsAI.Studio.Refinement.Tests.csproj -v minimal
Build succeeded. 0 Warning(s) 0 Error(s)

> dotnet test tests\XlightsAI.Studio.Refinement.Tests\XlightsAI.Studio.Refinement.Tests.csproj -v minimal
Passed! - Failed: 0, Passed: 11, Skipped: 0, Total: 11, Duration: 523 ms

> dotnet build XlightsAI.Studio.slnx -v minimal   (full solution, after change)
Build succeeded. 0 Warning(s) 0 Error(s)

> dotnet test XlightsAI.Studio.slnx -v minimal   (full solution regression, after change)
XlightsAI.Studio.Editing.Tests.dll      : 91/91 passed
XlightsAI.Studio.Xlights.Tests.dll      : 74/74 passed
XlightsAI.Studio.Projects.Tests.dll     : 25/25 passed
XlightsAI.Studio.Structure.Tests.dll    : 78/78 passed
XlightsAI.Studio.Refinement.Tests.dll   : 11/11 passed  (new)
XlightsAI.Studio.Display.Tests.dll      : 56/56 passed
XlightsAI.Studio.Melody.Tests.dll       : 117/117 passed
XlightsAI.Studio.Composition.Tests.dll  : 78/78 passed
XlightsAI.Studio.Audio.Tests.dll        : 38/38 passed
XlightsAI.Studio.AiEdits.Tests.dll      : 79/79 passed
XlightsAI.Studio.Rhythm.Tests.dll       : 82/82 passed
XlightsAI.Studio.Shell.Tests.dll        : 263/263 passed
XlightsAI.Studio.Reference.Tests.dll    : 31/31 passed
XlightsAI.Studio.Stems.Tests.dll        : 101/101 passed
TOTAL: 1124/1124 passed, 0 failed  (1113 baseline + 11 new, 0 removed, 0 modified)

> dotnet format XlightsAI.Studio.slnx --verify-no-changes --no-restore
(no output -- no formatting violations)
```

**Audio/Shell test counts.** This session's real audio output device was available (`Audio.Tests` 38/38, `Shell.Tests` 263/263, 0 failures), matching STEP-076/078's own full-green sessions rather than the 25-disclosed-failure sessions some earlier PHASE-11 steps reported. This is an environment condition, not a change introduced by this step; nothing this step touches has any relationship to audio playback.

Observable desktop demonstration (disposable console app, not committed to the repository, matching the `%TEMP%\xlai-stepNNN-demo-proj` convention every prior PHASE-11 evidence doc used):

```
> New-Item -ItemType Directory -Force -Path "$env:TEMP\xlai-step079-demo-proj"
> # wrote demo.csproj + Program.cs referencing the real Refinement/Editing/Contracts projects
> cd "$env:TEMP\xlai-step079-demo-proj"
> dotnet run -v minimal
```

Full transcript (abridged to remove restore/build noise):

```
=== PHASE-12 Step 079 demo: persisted run/candidate schema, hashes, seeds, state transitions ===
Persistence root: C:\Users\kmoens.LAKECO\AppData\Local\Temp\step079-demo-data-70698050826644c3b24a8ff8f2b3ebb4

--- Session 1: create run, claim, run two attempts, 'crash' before the third ---
Created run 852d59a2-4254-47cd-b3db-d6402ba10a87, Status=Queued, MaxAttempts=3
Claimed run with token e5d39fd1-51fb-40f7-a85e-913851961140
Recorded attempt 0: seed=1337, snapshotHash=snapshot-hash-0-of-seed-1337
Recorded attempt 1: seed=1338, snapshotHash=snapshot-hash-1-of-seed-1338
Run state after 2 attempts: Status=Generating, AttemptsMade=2
Simulated crash here -- process ends, claim is never released, heartbeat never refreshed again.

--- Session 2 ('restart'): brand-new store instance pointed at the same directory ---
Reloaded run after restart: Status=Generating, AttemptsMade=2
PASS: exactly 2 attempts survived the crash, none duplicated: True
PASS: abandoned claim reclaimed with a fresh token (old=e5d39fd1-..., new=62664d0c-...): True
PASS: the old (crashed) claim token can no longer mutate this run: True
Run stopped after resuming: Status=AwaitingApproval, StopReason=Exhausted, AttemptsMade=3/3, BestCandidateId=48be4dac-...
PASS: run never reached Accepted on its own (exhausted budget must not auto-commit): True

--- Negative path: a malformed (violating) attempt can never become the best candidate ---
PASS: malformed attempt rejected as BestCandidateId: True (LoopRun '...' BestCandidateId '...' names attempt 0, which carries 1 violation(s) and must never be retained as the best candidate.)
PASS: run2 never carries a BestCandidateId and never reached Accepted: True

--- Negative path: illegal status jump (PreviewReady -> Accepted, skipping AwaitingApproval) is rejected ---
PASS: illegal transition PreviewReady->Accepted (skipping AwaitingApproval) rejected: True
PASS: run3's persisted status is unchanged by the rejected call: True

=== Demo complete ===
```

This demo exercises this step's own required acceptance end to end: a run is created, claimed, and two attempts are recorded against real on-disk files; the process is simulated to crash (the claim is never released); a brand-new `JsonRefinementRunStore` instance, pointed at the same directory, proves both recorded attempts survived intact (no duplication), reclaims the abandoned claim with a fresh token once the old one is stale, proves the old token can no longer mutate the run, and resumes the run to completion (a third attempt, then the normal state progression to an exhausted-budget stop). Two negative paths then prove the required failure/edge case: a malformed (violating) attempt can never be retained as the best candidate, and an illegal direct jump to `Accepted` (skipping human approval) is rejected without mutating the persisted state.

One authoring bug was caught and fixed during this step's own demo-writing process, not left silently wrong: an earlier draft of the "illegal transition" negative path reused a run that had already legitimately advanced to `AwaitingApproval` by that point in the script, so the `Accepted` transition it attempted was **correctly** accepted (not an illegal jump at all) — the demo's own narration was wrong, not the implementation. Fixed by using a dedicated, freshly-created run for that specific negative assertion; rerun and reconfirmed above.

The demo project and its persistence-root/`bin`/`obj` output were created entirely under `%TEMP%`, run, and then deleted (`Remove-Item -Recurse -Force`; the demo's own persistence root was also deleted by the demo program itself at the end of its own run); they are not part of this repository's tracked tree.

## 5. Persisted-output inspection / observable demonstration

See §4's demo transcript — real `JsonRefinementRunStore` run files (`refinement-run-{id}.json`) were written to and read back from an actual temp directory on disk across two independently-constructed store instances (not an in-memory mock standing in for the file system in either "session"). Every claim/heartbeat/state-transition/attempt-save observed in the transcript reflects an actual file read-modify-write, not an in-memory simulation.

## 6. Fixtures / input hashes

No fixture files were added to `fixtures/` or `src/XlightsAI.Studio.Reference/Fixtures/` — this step's own scope (a persisted schema and state machine) needs no xLights/audio fixture corpus; `InputHash`/`SnapshotHash` values in the tests and demo are hand-constructed deterministic strings, consistent with this step's own disclosed narrowing (no real candidate generator exists yet — Step 080's own scope).

## 7. Unresolved issues / known gaps

- **No `ICandidateGenerator`/`IChoreographyQualityEvaluator`/`IRefinementOrchestrator` exists yet** — this step's own scope is exactly "create persisted run/candidate/result schema," not the generator (Step 080), evaluator (Step 081), repair strategy (Step 082), or budget/cancellation orchestration (Step 083) that will drive this schema end to end. `CandidateAttempt` is deliberately narrower than the spec's own proposed `Candidate` record (no `Plan`/`Report` field) until those steps exist to populate them — see ADR §3.1.
- **No real SCREEN 15 COMPARE / SCREEN 14 AI EDIT Avalonia view exists yet** — this step's own required user-visible outcome is explicitly "Versioned, inspectable project output plus a reproducible test report," not a desktop screenshot (unlike Steps 080/084, which do require one); no UI work was attempted or claimed.
- **Cross-process claim safety is heartbeat-staleness-based, not a real OS file lock** — disclosed as ADR §3.4. Two genuinely concurrent processes racing to claim the same run at the same instant (both observing no live claim, both about to write) are not interlocked by an OS-level primitive; the per-run `SemaphoreSlim` only serializes calls within one store instance. This is the same scope every other JSON-store concurrency model in this repository already accepts (e.g. `EditTransactionService`'s own optimistic revision check, not a SQL transaction); a genuinely multi-process desktop scenario is out of this step's own named scope.
- **`IRefinementRunStore` does not itself enforce `MaxAttempts` as a hard cap on `SaveAttemptAsync` calls** — Step 083's own named scope is "add retry, time, compute and quality thresholds; cancellation." This step's own `LoopRun.Validate()` does reject a persisted `AttemptsMade` exceeding `MaxAttempts` as a structurally invalid record (proven by `Step079_Acceptance_MaxAttemptsEnforced_ExactAttemptCount`), but does not stop an orchestrator (not yet built) from calling `SaveAttemptAsync` an (n+1)th time before it updates `AttemptsMade` — that budget-stopping behavior belongs to the orchestrator Step 083 has not yet built, not to this step's own schema layer.

## 8. Required-acceptance-text-by-text tie-back

| Required text (verbatim) | Where proven |
|---|---|
| "Interrupted run resumes idempotently" | `Step079_Acceptance_InterruptedRun_ResumesIdempotently_WithoutDuplicatingWork` (two independent store instances against the same directory; the abandoned claim is reclaimed, the in-flight attempt is intact and not duplicated, re-saving the same state/attempt is a no-op overwrite, and the old claim token is permanently rejected); demo §4 Sessions 1–2 end to end. |
| Implementation guidance, verbatim: "resumable job states with input hashes, selected scope, attempts, last heartbeat, seeds and cancellation records" | `LoopRun` carries exactly these fields (`InputHash`, `Scope`, `AttemptsMade`/`MaxAttempts`, `HeartbeatAtUtc`, `Seed`, `StopReason`="Cancelled" for a cancellation record); `Step079_NegativePath_CancelledRun_NeverCommits` exercises the cancellation record path. |
| Implementation guidance, verbatim: "use transactionally claimed work" | `IRefinementRunStore.TryClaimAsync`/`SaveRunStateAsync`/`SaveAttemptAsync`/`HeartbeatAsync`/`ReleaseClaimAsync` all require the exact currently-held claim token; `Step079_Acceptance_LiveClaim_CannotBeStolenBeforeItGoesStale` and `Step079_NegativePath_ForeignClaimToken_RejectedWithoutMutation` prove this directly. See ADR §3.4 for the disclosed scope of "transactional" at this step's own level. |
| Required failure/edge case, verbatim: "Malformed proposal... must not auto-commit" | `Step079_NegativePath_MalformedAttempt_CannotBecomeBestCandidate_RunNeverReachesAccepted`; demo §4's first negative-path section. |
| Required failure/edge case, verbatim: "no-improvement and exhausted retry budget must not auto-commit" | `Step079_NegativePath_NoImprovementStop_CannotSkipApprovalAndReachAcceptedDirectly` (no-improvement); `Step079_Acceptance_MaxAttemptsEnforced_ExactAttemptCount` (exhausted budget, exact count); demo §4's "exhausted" and "illegal jump" sections. |
| PHASE-12 Section 7 cross-cutting test #1: "Same seed + inputs yields reproducible candidate" | `Step079_Acceptance_SameSeedAndInputs_ProduceReproducibleSnapshotHash` (schema-level: identical seed/input hash yields identical `SnapshotHash` shape; actual generation determinism is Step 080's own scope). |
| PHASE-12 Section 7 cross-cutting test #4: "MaxAttempts enforced with exact attempt count" | `Step079_Acceptance_MaxAttemptsEnforced_ExactAttemptCount`. |
| PHASE-12 Section 7 cross-cutting test #5: "Cancelled run never commits" | `Step079_NegativePath_CancelledRun_NeverCommits` (cancellation is terminal, releases the claim, and `Cancelled` has no outgoing edge to `Accepted`). |
| Section 7 cross-cutting negative case: "Invalid/stale input: reject and show the source of the problem; do not commit partial project state" | `Step079_NegativePath_StaleRevisionInput_RejectedWithoutMutation`, `Step079_NegativePath_InconsistentClaimFields_RejectedAtValidation`, `Step079_NegativePath_ForeignClaimToken_RejectedWithoutMutation` — every rejection throws before any file write. |

## 9. PASS / BLOCKED

**PASS.**

- Both the required-acceptance sentence and the required failure/edge-case sentence are proven by passing unit tests against a real, file-backed `JsonRefinementRunStore` (no mocking library) and by a real console demonstration exercising a full create/claim/attempt/"crash"/"restart"/resume/negative-path round trip.
- "Interrupted run resumes idempotently" is proven with two independently-constructed store instances against the same real on-disk directory — not an in-memory stand-in for either "session."
- "Malformed proposal, no-improvement and exhausted retry budget must not auto-commit" is proven structurally: `RefinementStatusTransitions` makes `Accepted` reachable only from `AwaitingApproval`, and `SaveRunStateAsync` rejects any `BestCandidateId` naming a violating attempt — both before any file write.
- Full solution build succeeds with 0 warnings/errors; `dotnet format --verify-no-changes` reports no violations.
- Full regression: 1124/1124 tests pass (0 failed) — 1113 pre-existing (0 modified, 0 removed) plus 11 new `Refinement.Tests`.
- No prior accepted contract (any PHASE-01 through PHASE-11 type) was modified. `XlightsAI.Studio.Refinement` is a brand-new, additive project.
- No show file, fixture, or prior persisted artifact was modified.

**Explicitly not claimed:** a real candidate generator (`ICandidateGenerator`, Step 080's own scope), a real quality evaluator (`IChoreographyQualityEvaluator`/`QualityReport`, Step 081's own scope), a repair strategy (Step 082), budget/cancellation orchestration beyond the schema-level invariants this step enforces (Step 083), or any Avalonia UI (Steps 080/084's own scope, not this one). `CandidateAttempt` is a disclosed, additive narrowing of the spec's own proposed `Candidate` record (ADR §3.1) — `Plan`/`Report` fields do not exist on it yet. Cross-process claim safety is heartbeat-staleness-based, not an OS-level file lock (ADR §3.4).
