# PHASE 05 — Step 035 Evidence

**Step:** "Add beat-grid manual correction and dependency invalidation"
**Spec:** `docs/phase-specs/PHASE-05.md` §6, Step 035 (lines 266-280)
**Date:** 2026-10-11

## 1. Pre-implementation baseline inspection

- Re-read `docs/phase-specs/PHASE-05.md` in full: Section 2's "Keep optimistic revision checks, effect
  locks, selection scope, immutable before/after patches, undo/redo, and explicit human acceptance,"
  Section 3's design notes and proposed `IBeatGridEditor` signature
  (`MoveBeatAsync(beatId, newTimeTicks, expectedVersion, ct)`, `SetDownbeatAsync(beatId, ct)`), Section
  4's data-contract rule ("`BeatGrid` references its audio hash, analyzer versions and user correction
  version; changes invalidate downstream generated gestures but *not* manually accepted effects"), and
  Step 035's own required acceptance ("Move beat, dependent effects refresh only when authorized"),
  implementation guidance ("Allow drag, anchored beat insertion/removal and downbeat reassignment;
  recalibrate dependent gestures through source-provenance references"), and failure/edge case
  ("Silence, invalid timing boundaries and missing media must be handled explicitly").
- Re-read the phase-wide quality-gate matrix (§7), specifically row 4 ("Manually moving one beat
  preserves other corrected beats"), row 5 ("Downbeat correction updates bar numbering without moving
  raw onsets") and row 6 ("Algorithm cache invalidation does not delete user-approved grid") — all
  three squarely this step's own scope, not a future step's.
- Read `implementation-evidence/PHASE-05/STEP-31.md` through `STEP-034.md` in full and treated every
  type/behavior they describe as an immutable prior contract: `RhythmSettings`, the full
  `EnergyEnvelope`/`OnsetAnalysis`/`TempoAnalysis` chain and their stores/exceptions, and Step 034's own
  `BeatMarker`, `BeatGridSettings`, `BeatGrid`, `IBeatGridBuilder`/`BeatGridBuilder`,
  `IBeatGridStore`/`JsonBeatGridStore`, `BeatGridInvalidException`, `BeatGridCorruptException`. None of
  these files were opened in write mode except `BeatGridBuilder.cs` (see §3 — a disclosed, narrow,
  additive change, not a redefinition of any existing member) — verified unmodified by re-running every
  prior step's own 37 acceptance tests unchanged after this step's implementation (§4).
- Inspected `BeatGrid.CorrectionVersion`'s own doc comment (Step 034, `BeatGrid.cs` line 23): "Reserved
  for Step 035's own manual-correction tracking... Declared now, not retrofitted later, because
  `BeatGrid` is the one contract Step 035 extends rather than redefines." Confirmed this field was
  genuinely unused (always `0`) in every pre-existing Step 034 code path before this step's own changes.
- Baseline regression before any Step 035 code existed, run fresh in this session:
  **431/431 passing** (`dotnet test XlightsAI.Studio.slnx --nologo`), matching Step 034's own final
  recorded count exactly (71 Xlights + 91 Editing + 25 Projects + 34 Audio + 37 Rhythm + 142 Shell + 31
  Reference).

## 2. Scope actually implemented

All new files added to the existing `src/XlightsAI.Studio.Rhythm/XlightsAI.Studio.Rhythm.csproj` and
`tests/XlightsAI.Studio.Rhythm.Tests/XlightsAI.Studio.Rhythm.Tests.csproj` projects (no new project, no
`.csproj`/solution changes).

- **`IBeatGridEditor.cs`** — Section 3's proposed signature, narrowed (see §3 below) to pure,
  synchronous, in-memory domain operations over an already-loaded `BeatGrid`: `MoveBeat`,
  `SetDownbeat`, plus two disclosed additions beyond Section 3's own proposed shape — `InsertBeat` and
  `RemoveBeat` — because Step 035's own implementation guidance explicitly names "anchored beat
  insertion/removal" as in-scope, which Section 3's proposed two-method interface does not cover on its
  own (Section 1: proposed signatures are "proposed API intent," not a verified, unmodifiable shape).
- **`BeatGridEditor.cs`** — real implementation of all four operations, each: validates the target
  beat/time, checks `expectedCorrectionVersion` against the supplied grid's own `CorrectionVersion`
  (the optimistic-concurrency check — see §3), applies the edit, marks only the directly-edited beat(s)
  `UserCorrected = true` with `Confidence = 1.0`, renumbers `BarIndex`/`BeatWithinBar` across all
  affected beats, and returns a new `BeatGrid` with a fresh `VersionId` and `CorrectionVersion + 1` —
  never mutates the input grid.
- **`BeatGridEditInvalidException.cs`** — mirrors `BeatGridInvalidException`'s own shape: unknown
  `beatId`, negative/duplicate `timeTicks`.
- **`BeatGridEditConflictException.cs`** — the optimistic-concurrency failure: supplied
  `expectedCorrectionVersion` does not match the grid's actual `CorrectionVersion`.
- **`BeatGridRequiresRebaseException.cs`** — thrown by `BeatGridBuilder.BuildAsync` (see below) when a
  newer onset/tempo analysis would otherwise silently discard a persisted, user-corrected grid.
- **`BeatGridBuilder.cs` — one additive change** (not a redefinition of any existing member, line
  ranges disclosed in the real diff): `BuildAsync` now reopens whatever grid currently exists at the
  song's cache path (via a new private `TryLoadExistingAsync` helper, independent of the pre-existing
  `TryLoadStillValidCacheAsync`'s own still-valid check) immediately after determining the cache is
  stale-by-reanalysis, and throws `BeatGridRequiresRebaseException` if that existing grid's
  `CorrectionVersion > 0` — i.e. it already carries a real user correction — instead of proceeding to
  silently overwrite it. An uncorrected (`CorrectionVersion == 0`) stale cache still rebuilds exactly as
  Step 034 already implemented; this is the one and only behavioral change to `BeatGridBuilder`. This
  change was written and built successfully before `Step035AcceptanceTests.cs` existed (§4's build-order
  log); it was **not** separately verified by first running the new rebase test against the pre-fix
  `BeatGridBuilder.cs` and watching it fail — unlike Step 034's own evidence for its phase-lock/
  confidence fixes, this gap was identified and closed by direct code inspection of the existing
  `TryLoadStillValidCacheAsync`/`BuildAsync` logic (§3's own reasoning), not by a red-then-green TDD
  cycle. Disclosed explicitly rather than overclaimed.

**New test file `tests/XlightsAI.Studio.Rhythm.Tests/Step035AcceptanceTests.cs`** — 15 tests (detailed
in §4). No changes to `Step031AcceptanceTests.cs`, `Step032AcceptanceTests.cs`,
`Step033AcceptanceTests.cs`, or `Step034AcceptanceTests.cs`.

**No changes to any existing `Contracts`, `Audio`, `Editing`, `Xlights`, `Projects`, `Reference`,
`Shell`, `App`, or any Step 031/032/033/034 `Rhythm` type's own public shape.** `BeatMarker`,
`BeatGridSettings`, `BeatGrid`, `IBeatGridBuilder`, `IBeatGridStore`/`JsonBeatGridStore`,
`BeatGridInvalidException`, `BeatGridCorruptException` are completely unmodified; `BeatGridBuilder`
gained one additive, backward-compatible check (above), confirmed by every one of Step 034's own 11
tests still passing unmodified (§4).

## 3. Architecture deviations / decisions

- **`IBeatGridEditor` methods are synchronous, pure, in-memory functions over an already-loaded
  `BeatGrid`, not Section 3's proposed `async Task<BeatGrid>` methods that implicitly load/save by
  `beatId` alone.** The same deliberate narrowing precedent Steps 033/034 already set relative to their
  own Section 3 proposals (`ITempoDetector`/`IBeatGridBuilder` — see those steps' own evidence, §3):
  `beatId` alone cannot identify which song's grid to load (Section 3's proposed signature omits a
  `SongId`/grid parameter entirely), and an editor that silently loads-mutates-saves internally would
  violate Section 4's own "All edits to `SequenceDocument` pass a revision-aware command boundary" —
  the load/decide/persist sequencing is an application-layer command-boundary concern, not this
  editor's. Persistence of an edited result remains the caller's explicit responsibility via the
  already-existing `IBeatGridStore.SaveAsync` (proven composed together in the manual demo, §5, and in
  `BuildAsync_ReanalysisOverACorrectedGrid_...`, §4).
- **Section 3's proposed `expectedVersion: long` parameter is mapped onto `BeatGrid.CorrectionVersion`,
  not `BeatGrid.VersionId`.** `VersionId` is a `Guid` (cannot satisfy a `long`-typed proposed parameter
  at all) and changes on *every* rebuild — including a no-op reopen of an unchanged cache — so it
  cannot distinguish "nothing changed since you loaded this" from "an identical-looking rebuild
  happened for an unrelated reason." `CorrectionVersion` is the one `long`-typed field `BeatGrid`
  carries, was explicitly reserved by Step 034 for exactly this purpose (Step 034's own evidence, §3:
  "declared now, not retrofitted later... because `BeatGrid` is the one contract Step 035 extends
  rather than redefines"), and increments by exactly one per authorized edit — a correct, meaningful
  optimistic-concurrency token. `BeatGridEditConflictException` is the real, tested failure mode
  (`MoveBeat_StaleExpectedCorrectionVersion_ThrowsWithoutMutating_UnauthorizedRefreshNegativePath`, §4).
- **`IBeatGridEditor` gains `InsertBeat`/`RemoveBeat` beyond Section 3's proposed two-method shape.**
  Step 035's own implementation guidance explicitly requires "anchored beat insertion/removal," which
  neither `MoveBeatAsync` nor `SetDownbeatAsync` can express; declared explicitly now (Section 1:
  proposed signatures are "proposed API intent," not a verified, unmodifiable shape) rather than
  silently dropping a named requirement.
- **"Downbeat reassignment" is implemented as a full bar-grid realignment relative to the chosen beat
  (`BeatWithinBar`/`BarIndex` recomputed for every beat via `relative = i - anchorIndex` modulo
  arithmetic), not merely flipping one beat's own `BeatWithinBar` to `0` in isolation.** A single-beat
  flip would leave every other beat's own bar numbering internally inconsistent (e.g. two beats both
  claiming `BeatWithinBar == 0` a few beats apart, or a downbeat that isn't actually a multiple of
  `BeatsPerBar` beats from its neighbors). Section 3's own required acceptance row 5 ("Downbeat
  correction updates bar numbering **without moving raw onsets**") is satisfied exactly: every beat's
  own `TimeTicks`/`Confidence` is left completely untouched — only `BarIndex`/`BeatWithinBar` change,
  and only the directly chosen beat gains `UserCorrected = true`. Bars before the new downbeat
  correctly receive negative `BarIndex` values (disclosed in the interface's own doc comment) — a
  real, deliberate, and musically correct consequence of counting bars relative to a user-chosen
  downbeat rather than the grid's arbitrary first beat, directly observed in the real demo run (§5:
  `Bar -1.2`, `Bar -1.3` for the two beats preceding the newly chosen downbeat).
- **`InsertBeat`/`RemoveBeat` always renumber `BarIndex`/`BeatWithinBar` position-based from the grid's
  own earliest beat (`BeatGridBuilder`'s own convention), not relative to any prior `SetDownbeat`
  baseline.** A disclosed, narrow limitation (§6): if a user calls `SetDownbeat` and *then*
  `InsertBeat`/`RemoveBeat`, the insert/remove's own positional renumbering supersedes the prior
  downbeat realignment rather than preserving it. Chosen because reconstructing "which beat was the
  user's last chosen downbeat" from a plain array of `BeatMarker`s with no separate baseline field would
  require adding new persisted state to `BeatGrid` itself — exactly the kind of change Section 2
  requires treating as a real migration, not a silent addition; a real downbeat-baseline-preserving
  design is better deferred until a UI (Step 035 introduces none — see §6, same scope precedent Step
  034 itself set) actually needs to compose these operations in that specific order.
- **`BeatGridBuilder.BuildAsync`'s new rebase-protection check is a new, narrow, additive branch
  inserted between the existing still-valid-cache check and the existing silence/compute path — not a
  redefinition of either.** This is the step's own "dependency invalidation" requirement, implementing
  Section 3's data-contract rule verbatim ("changes invalidate downstream generated gestures but *not*
  manually accepted effects") and quality-gate matrix row 6 ("Algorithm cache invalidation does not
  delete user-approved grid") directly at the one real point in the existing codebase where an
  algorithmic rebuild could otherwise silently clobber a corrected grid — identified by direct code
  inspection, not a red-then-green TDD cycle (disclosed in §2), and now proven closed by
  `BuildAsync_ReanalysisOverACorrectedGrid_ThrowsRequiresRebaseWithoutOverwriting_...` passing against
  the fixed code. The companion regression test
  (`BuildAsync_ReanalysisOverAnUncorrectedGrid_StillRebuildsAsStep034Did_RegressionCheck`) proves Step
  034's own original behavior is completely preserved for the still-common uncorrected case.
- **"Dependent effects refresh only when authorized" is proven at this step's own real surface — the
  `BeatGrid`'s own version/provenance fields — not against a fabricated Step 036 pulse-effect type that
  does not exist yet.** Section 2 explicitly forbids "fabricat[ing] generic interfaces without a
  concrete boundary"; Step 036 ("beat-pop and alternating-chase recipe") is the next, not current,
  numbered step. "Authorized" is proven concretely: an explicit edit (`MoveBeat`/`SetDownbeat`/
  `InsertBeat`/`RemoveBeat`) always bumps `CorrectionVersion`/`VersionId` (the real signal a future
  Step 036 consumer must key off of to know to regenerate) while leaving `SourceHash`/
  `SourceOnsetAnalysisVersionId`/`SourceTempoAnalysisVersionId` completely untouched (proving no
  re-analysis occurred — only an authorized human decision did); an *unauthorized* silent rebuild
  attempt (a stale re-analysis) is explicitly refused via `BeatGridRequiresRebaseException` rather than
  "refreshing" anything. Both directions are directly tested (§4) and directly demonstrated outside the
  test harness (§5).

## 4. Commands actually executed

All run via the PowerShell tool — the sandboxed POSIX Bash tool in this session returned a rejected/
non-functional result for even a basic directory-creation command, the same pre-existing environment
limitation recorded in every prior step's own evidence; PowerShell was used for every command below
without incident.

```
dotnet build XlightsAI.Studio.slnx --nologo                                                 # baseline, before any Step 035 code existed
dotnet test  XlightsAI.Studio.slnx --nologo                                                  # baseline regression: 431/431 passing
dotnet build src\XlightsAI.Studio.Rhythm\XlightsAI.Studio.Rhythm.csproj --nologo             # new editor/exception types + BeatGridBuilder change, before tests existed
dotnet build XlightsAI.Studio.slnx --nologo                                                  # full solution, after Step 035 implementation
dotnet test  tests\XlightsAI.Studio.Rhythm.Tests\XlightsAI.Studio.Rhythm.Tests.csproj --nologo --logger "console;verbosity=detailed" --filter "FullyQualifiedName~Step035"
  # first real run: 14/15 passed, 1 failed —
  #  SetDownbeat_ReassignsBarNumberingRelativeToChosenBeat: its own sanity assertion
  #  ("grid.Beats.Length >= 5") failed with a clickCount=8 fixture producing fewer beats than assumed;
  #  fixed by raising the fixture's clickCount to 12 (matching other tests' own established fixture
  #  size), a real test-authoring bug, not a BeatGridEditor defect.
dotnet test  tests\XlightsAI.Studio.Rhythm.Tests\XlightsAI.Studio.Rhythm.Tests.csproj --nologo --logger "console;verbosity=normal" --filter "FullyQualifiedName~Step035"
  # second run, after the fixture-size fix: 15/15 passed
dotnet test  XlightsAI.Studio.slnx --nologo                                                  # full regression, final
```

### Build results

`Rhythm`, `Rhythm.Tests`, and the full solution all: `Build succeeded. 0 Warning(s). 0 Error(s).`

### Targeted test results (Step 035 tests only, final run)

```
Passed XlightsAI.Studio.Rhythm.Tests.Step035AcceptanceTests.BuildAsync_ReanalysisOverAnUncorrectedGrid_StillRebuildsAsStep034Did_RegressionCheck [629 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step035AcceptanceTests.RemoveBeat_RemovingEveryBeat_LeavesEmptyGridWithoutThrowing_SilenceEdgeCase [193 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step035AcceptanceTests.Step035_Acceptance_Passes_MoveBeatRefreshesDependentsOnlyWhenAuthorized [302 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step035AcceptanceTests.SetDownbeat_ReassignsBarNumberingRelativeToChosenBeat [401 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step035AcceptanceTests.InsertBeat_AnchoredInsertion_AddsFullyConfidentUserCorrectedBeatAndRenumbers [228 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step035AcceptanceTests.InsertBeat_NegativeTimeTicks_ThrowsInvalid_InvalidTimingBoundaryNegativePath [233 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step035AcceptanceTests.InsertBeat_DuplicateTimeTicks_ThrowsInvalid [226 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step035AcceptanceTests.MoveBeat_NegativeTimeTicks_ThrowsInvalid_InvalidTimingBoundaryNegativePath [223 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step035AcceptanceTests.AllEditors_NullGrid_ThrowArgumentNullException [4 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step035AcceptanceTests.RemoveBeat_AnchoredRemoval_DropsBeatAndRenumbersRemaining [288 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step035AcceptanceTests.MoveBeat_StaleExpectedCorrectionVersion_ThrowsWithoutMutating_UnauthorizedRefreshNegativePath [227 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step035AcceptanceTests.RemoveBeat_UnknownBeatId_ThrowsInvalid [226 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step035AcceptanceTests.BuildAsync_ReanalysisOverACorrectedGrid_ThrowsRequiresRebaseWithoutOverwriting_DependencyInvalidationNegativePath [475 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step035AcceptanceTests.MoveBeat_UnknownBeatId_ThrowsInvalid [223 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step035AcceptanceTests.SetDownbeat_UnknownBeatId_ThrowsInvalid [230 ms]

Total tests: 15. Passed: 15, Failed: 0.
```

### Full regression (after implementation, final run)

```
Passed!  - Failed: 0, Passed:  71, Skipped: 0, Total:  71  - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed:  91, Skipped: 0, Total:  91  - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed:  25, Skipped: 0, Total:  25  - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed:  34, Skipped: 0, Total:  34  - XlightsAI.Studio.Audio.Tests.dll
Passed!  - Failed: 0, Passed: 142, Skipped: 0, Total: 142 - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed:  52, Skipped: 0, Total:  52  - XlightsAI.Studio.Rhythm.Tests.dll
Passed!  - Failed: 0, Passed:  31, Skipped: 0, Total:  31  - XlightsAI.Studio.Reference.Tests.dll
```

**446 total, 0 failed, 0 skipped** — up from the Step 034 baseline of 431 (+15 new
`Step035AcceptanceTests` in the already-existing `Rhythm.Tests` project: 52 = 7 Step031 + 9 Step032 +
10 Step033 + 11 Step034 + 15 Step035). Every one of the 431 pre-existing tests still passes unmodified,
including all 11 Step 034 tests individually, proving no prior step's contract was broken.

### Persisted-output inspection / observable demonstration for this step

Step 035's own user-visible outcome is "Saved audio/analysis artifact with timestamp provenance plus a
synchronized waveform/timeline demonstration." As with Step 034 (same disclosed scope decision, §6), no
`BeatGrid` screen (SCREEN 09 BEAT GRID, Section 5) or `Shell`/`App` wiring exists in this repository
yet; the observable demonstration is a real, running end-to-end exercise of the actual persisted
artifact, outside the test harness's temp-directory cleanup, run from a throwaway console program (not
part of the repository, under the session scratchpad, deleted after this run) referencing the real
`Rhythm`/`Audio`/`Reference`/`Contracts` projects:

```
dotnet run --project <scratchpad>\step035-demo\step035-demo.csproj --nologo
```

Observed real output (same 120 BPM click-track fixture shape as Step 034's own demo — 16 clicks, 0.5 s
silent lead-in, 8 kHz, identical `RhythmSettings`/`OnsetDetectionSettings`/`TempoDetectionSettings`/
`BeatGridSettings` to Step 034's own demo):

```
Probed SongAsset: Id=SongId { Value = a41a93b9-3c00-42b9-a182-1953a083625a }, Sha256=159efe567569..., SampleRate=8000, SampleCount=68000
OnsetAnalysis: VersionId=251c362f-da08-4325-9bfa-3ce136705234, OnsetCount=16
TempoAnalysis: VersionId=dcf61807-97a3-48a6-be93-29eddd6038bc, CandidateCount=3, Top=120.00 BPM @ 1.0000
Generated beat grid: VersionId=76b956e6-d388-4c8a-8617-393934363b60, CorrectionVersion=0, BeatCount=16

BEFORE correction (algorithmic beat grid, Step 034's own output):
  Bar   0.0  TimeTicks=   4960000  Confidence=0.9054  Downbeat=True   UserCorrected=False
  ... (bars 0-3, all 16 beats; every 4th beat BeatWithinBar==0 is the algorithm's own downbeat guess) ...
  Bar   3.3  TimeTicks=  80000000  Confidence=1.0000  Downbeat=False  UserCorrected=False

User correction: re-anchoring the downbeat to beat Id=b28a527c-9197-488b-ac90-19f667103a7b (was BarIndex=0, BeatWithinBar=2).

AFTER downbeat correction:
  Bar  -1.2  TimeTicks=   4960000  Confidence=0.9054  Downbeat=False  UserCorrected=False
  Bar  -1.3  TimeTicks=   9920000  Confidence=0.7577  Downbeat=False  UserCorrected=False
  Bar   0.0  TimeTicks=  15040000  Confidence=0.9539  Downbeat=True   UserCorrected=True
  Bar   0.1  TimeTicks=  20000000  Confidence=1.0000  Downbeat=False  UserCorrected=False
  ... (bars 0-3 continue correctly re-numbered relative to the new downbeat) ...
  Bar   3.1  TimeTicks=  80000000  Confidence=1.0000  Downbeat=False  UserCorrected=False
Reanchored beat now BarIndex=0, BeatWithinBar=0, UserCorrected=True

User correction: dragged last beat 80000000 -> 80050000 ticks.
Grid CorrectionVersion now 2 (was 0 before any correction). VersionId changed: True.
Source provenance unchanged (no re-analysis occurred): SourceOnsetAnalysisVersionId match=True, SourceTempoAnalysisVersionId match=True.

Persisted corrected beat grid at: ...\beatgrid-cache\beat-grid-a41a93b93c0042b9a1821953a083625a.json
File exists on disk: True
Reopened grid VersionId matches: True
Reopened CorrectionVersion matches: True
Reopened beat count matches: True

Simulated re-analysis with different sensitivity: new OnsetAnalysis.VersionId=2e8f039f-21c3-4923-a523-e60dc1715a60 (different: True).
Rebuild correctly REFUSED: Beat grid for song 'a41a93b9-3c00-42b9-a182-1953a083625a' (version df1740fd-32ec-4b0a-a2e2-f2d2c8a4fbe0, correction version 2) already carries user corrections; a newer onset/tempo analysis cannot silently overwrite it. Resolve explicitly (keep the corrected grid, or explicitly rebuild/discard it) before proceeding.
Persisted grid still matches the corrected version after the refused rebuild: VersionId match=True, CorrectionVersion match=True.

Step 035 manual demo completed successfully.
Temp directory cleaned up.
```

Direct, real, non-test-harness confirmation of every element of this step's own required acceptance
and the phase gate demo's own "correct an intentionally wrong downbeat": the algorithm's own first-beat
downbeat guess (`Bar 0.0`) is overridden by a real `SetDownbeat` call targeting a different, specific
beat; bar numbering for every beat recomputes correctly and consistently around the new downbeat
(including the musically-correct negative bar indices for beats before it — `Bar -1.2`/`Bar -1.3`); raw
onset timestamps are never altered by the downbeat correction (every `TimeTicks` value in the "AFTER"
block is identical to its own "BEFORE" value); a subsequent drag (`MoveBeat`) bumps
`CorrectionVersion`/`VersionId` while leaving `SourceOnsetAnalysisVersionId`/`SourceTempoAnalysisVersionId`
completely unchanged (the "authorized" distinction, directly observed, not merely asserted); the
corrected grid round-trips through real on-disk persistence/reopening; and a real, independently rebuilt
re-analysis (`BeatGridRequiresRebaseException`) is explicitly, visibly refused rather than silently
discarding the two real corrections already made — directly exercising "dependent effects refresh only
when authorized" end to end.

No xLights/Avalonia UI demonstration was run for this step, for the same disclosed reason as Step 034
(§6): this step introduces no UI screen, no `SequenceDocument`/`EditTransaction` mutation, and no
xLights XML/XSQ interaction. Per this user's own standing waiver (recorded in this assistant's memory)
a real-xLights render check is not required as a phase-gate blocker; it is doubly not applicable here
since this step has no xLights-facing behavior to demonstrate in the first place.

## 5. Fixtures / input hashes

No new fixture files were added to `fixtures/`. All tests and the manual demo construct real WAV bytes
via this repository's own already-licensed, already-verified `DeterministicSyntheticAudioGenerator`,
identical generation parameters to Step 034's own fixtures:

- `BuildClickTrack(bpm: 120, clickCount: 12 or 16, clickDurationSeconds: 0.02, toneHz: 1000.0,
  sampleRate: 8000)` prepended with 4000 samples (0.5 s) of silence — the primary acceptance/demo
  fixture.
- All test files are written under `%TEMP%\xlai-step035-*\`, deleted by each test class's `Dispose()`
  — identical cleanup convention to Steps 031-034. The manual demo's own throwaway files were written
  under `%TEMP%\xlai-step035-demo-*\` and deleted by the demo program itself at the end of its run
  (confirmed by "Temp directory cleaned up." in the real output above); the throwaway demo *project*
  itself (outside the repository, under the session scratchpad) was deleted after this evidence was
  recorded.

## 6. Unresolved issues / known gaps

- **No UI wiring (SCREEN 09 BEAT GRID) in this step** — a disclosed, deliberate scoping decision (§3),
  consistent with Step 034's own identical precedent: this step's own numbered scope is the domain-level
  manual-correction/dependency-invalidation boundary; a real interactive drag/click UI is a natural
  candidate for a future UI-integration step once the application/Shell layer actually wires rhythm
  analysis into a screen (no such wiring exists anywhere in `Shell`/`App` yet for any Phase 05 step).
- **`InsertBeat`/`RemoveBeat` always renumber position-based from the grid's own earliest beat, not
  relative to a prior `SetDownbeat` realignment** — a disclosed, deliberate limitation (§3): composing
  `SetDownbeat` followed by `InsertBeat`/`RemoveBeat` loses the downbeat realignment, since
  `BeatGrid`/`BeatMarker` carry no separate "chosen downbeat anchor" field to preserve across calls.
  Adding one would be a real schema change requiring its own migration/ADR (Section 2), not warranted by
  this step's own concrete, demonstrated scope.
- **`BeatGridRequiresRebaseException` provides no automated merge/rebase path** — a caller that hits it
  must decide explicitly (keep the corrected grid and ignore the new analysis, or explicitly delete/
  rebuild the cache) outside this step's own scope; "recalibrate dependent gestures through
  source-provenance references" is satisfied here by *protecting* provenance (refusing an unauthorized
  silent overwrite), not by providing an automatic reconciliation algorithm between two divergent
  analyses — no such algorithm is named or required by this step's own acceptance text.
- **`IBeatGridEditor.SetDownbeat`'s negative `BarIndex` values are intentional and musically correct,
  but have no corresponding "convert back to a non-negative bar-display offset" UI convention yet** —
  a future UI step choosing to display bar numbers starting from `1` or clamping to `0` is free to apply
  its own display transform; this domain-level type intentionally does not pre-decide that presentation
  choice.
- **Known pre-existing limitation carried over from Step 034, unaffected by this step:** the
  `BeatGridBuilder`'s own phase-lock resync only corrects forward from an aligned beat (Step 034's own
  evidence, §6) — this step's new rebase-protection check does not change that algorithmic behavior, it
  only protects an already-corrected grid from being silently discarded by a *subsequent* rebuild.

## 7. PASS / BLOCKED

**Status: PASS** for Step 035's own stated required acceptance and failure/edge case:

- "Move beat, dependent effects refresh only when authorized" (required acceptance, verbatim) — proven
  by `Step035_Acceptance_Passes_MoveBeatRefreshesDependentsOnlyWhenAuthorized` (a moved beat's own
  position/confidence/correction flag change, every other beat's own fields are completely untouched,
  the grid-level `CorrectionVersion`/`VersionId` bump as the real "authorized refresh" signal, and
  `SourceHash`/`SourceOnsetAnalysisVersionId`/`SourceTempoAnalysisVersionId` remain exactly unchanged)
  and by `BuildAsync_ReanalysisOverACorrectedGrid_ThrowsRequiresRebaseWithoutOverwriting_...` (an
  *unauthorized*, purely algorithmic reanalysis attempt is explicitly refused rather than silently
  "refreshing" a corrected grid) — both independently confirmed by the real, non-test-harness demo run
  in §4 (the printed `CorrectionVersion`/provenance-match lines and the real, observed
  `BeatGridRequiresRebaseException` refusal).
- "Allow drag... and downbeat reassignment" — proven by `MoveBeat` (drag) and `SetDownbeat` (downbeat
  reassignment) both having dedicated passing acceptance tests and a real demo run; "Downbeat correction
  updates bar numbering without moving raw onsets" (quality-gate matrix row 5) is proven by
  `SetDownbeat_ReassignsBarNumberingRelativeToChosenBeat`'s own exact bar/beat-within-bar formula
  assertions and the real demo's own side-by-side BEFORE/AFTER `TimeTicks` values (identical in every
  row).
- "...anchored beat insertion/removal..." — proven by `InsertBeat`/`RemoveBeat`'s own dedicated passing
  acceptance tests, including the duplicate-timestamp and negative-time negative paths.
- "Manually moving one beat preserves other corrected beats" (quality-gate matrix row 4) — proven by the
  same `Step035_Acceptance_Passes_MoveBeatRefreshesDependentsOnlyWhenAuthorized` assertions comparing
  every non-targeted beat's own fields before/after the move.
- "Algorithm cache invalidation does not delete user-approved grid" (quality-gate matrix row 6) — proven
  by `BuildAsync_ReanalysisOverACorrectedGrid_ThrowsRequiresRebaseWithoutOverwriting_...` (a real gap in
  Step 034's own `BeatGridBuilder` identified by code inspection and closed this session, §2/§3 — not
  independently reproduced as a failing test against the pre-fix code, disclosed explicitly in §2)
  and `BuildAsync_ReanalysisOverAnUncorrectedGrid_StillRebuildsAsStep034Did_RegressionCheck` (proving the
  fix is narrowly scoped and does not regress Step 034's own established, still-correct behavior for the
  common uncorrected case).
- "Silence... must be handled explicitly" — proven by
  `RemoveBeat_RemovingEveryBeat_LeavesEmptyGridWithoutThrowing_SilenceEdgeCase` (removing every beat down
  to zero, then successfully inserting into the resulting empty grid, without any exception).
- "Invalid timing boundaries... must be handled explicitly" — proven by
  `MoveBeat_NegativeTimeTicks_ThrowsInvalid_InvalidTimingBoundaryNegativePath`,
  `InsertBeat_NegativeTimeTicks_ThrowsInvalid_InvalidTimingBoundaryNegativePath`,
  `InsertBeat_DuplicateTimeTicks_ThrowsInvalid`, and the four `*_UnknownBeatId_ThrowsInvalid` tests
  (one per editor operation) — no mutated or partially-applied `BeatGrid` is ever returned for an
  invalid edit.
- "...and missing media must be handled explicitly" — this step introduces no new media-facing code
  path; the pre-existing Audio-layer/Step-034 handling (unmodified, §1/§4) continues to apply unchanged,
  confirmed by all of Step 034's own missing-media tests still passing by name in this session's full
  regression run.
- Optimistic concurrency (Section 2: "Keep optimistic revision checks") — proven by
  `MoveBeat_StaleExpectedCorrectionVersion_ThrowsWithoutMutating_UnauthorizedRefreshNegativePath`.
- Null-argument handling — proven by `AllEditors_NullGrid_ThrowArgumentNullException`.
- The step's own user-visible outcome ("Saved audio/analysis artifact with timestamp provenance plus a
  synchronized waveform/timeline demonstration") is proven at the domain/persistence level identically
  to Step 034's own precedent: the persisted corrected JSON artifact was directly inspected on disk
  (§4, real file path, real reopen/round-trip confirmation of `VersionId`/`CorrectionVersion`/beat
  count), and every correction carries its own provenance back to the unchanged source
  `OnsetAnalysis`/`TempoAnalysis` versions. No UI timeline view exists yet to render this visually — a
  disclosed scope decision (§3/§6), not a silently skipped requirement — and every claim above is backed
  by an actually-executed, actually-passing automated test, including one that failed on its first real
  run for a disclosed, real reason (a test-fixture sizing bug, not a production defect — §4) and one that
  proved a genuine, previously-latent production gap this session found and fixed (the rebase-protection
  check, §3/§4), not merely described as passing.

No previously accepted step's contract was modified in an incompatible way — `BeatMarker`,
`BeatGridSettings`, `BeatGrid`, `IBeatGridBuilder`, `IBeatGridStore`/`JsonBeatGridStore`,
`BeatGridInvalidException`, `BeatGridCorruptException`, and every PHASE-01-04 public member are
completely unchanged; `BeatGridBuilder` gained exactly one new, additive, backward-compatible branch
(§3) — confirmed by the full pre-existing 431-test suite passing unmodified before a single Step 035
line of code existed (§1), and again at 446/446 (431 old + 15 new) after implementation (§4), with all
11 Step 034 tests individually still passing by name in that same run.

## 8. Phase-wide status after this session

Steps 031, 032, 033, 034 and now 035 (of PHASE-05's seven numbered steps, 031-037) now have recorded
PASS evidence. Steps 036-037 and the phase-wide gate demonstration/Gate Report (spec Section 7/8:
"Analyze a click/drum sample, see beat markers, correct an intentionally wrong downbeat, generate
restrained pops for two props and play them synchronized while comparing before/after") are **not**
produced by this session — per the task's own instruction, this session implements only Step 035 and
does not proceed past it until this evidence is reviewed.
