# PHASE 05 — Step 037 Evidence

**Step:** "Add confidence/quality gate and diagnostics"
**Spec:** `docs/phase-specs/PHASE-05.md` §6, Step 037 (lines 298-312) — the final numbered step of Phase 05 (031-037)
**Date:** 2026-10-12

## 1. Pre-implementation baseline inspection

- Re-read `docs/phase-specs/PHASE-05.md` in full: Section 2's architecture assumptions ("AI trust...
  optional local or BYOK cloud AI returns validated structured proposals only. Do not execute
  LLM-produced code, scripts or XML directly" and "Verification... Do not claim tests ran when they did
  not"), Section 3's own class-relationship diagram and proposed signatures (Section 3 proposes **no**
  explicit confidence/quality-gate interface for this step — Step 037's own required acceptance and
  implementation guidance are therefore the sole source of truth for this step's shape, not a Section 3
  signature to narrow), Step 037's own required acceptance ("Low-confidence results are flagged and
  correction is possible"), implementation guidance ("Add timing deviation histograms, first-beat offset
  and confidence; false positives must show warning, not trigger a blind whole-song export"), and
  failure/edge case ("Malformed proposal, no-improvement and exhausted retry budget must not
  auto-commit").
- Re-read the phase-wide quality-gate matrix (§7) in full, including row 7 ("Generated effects align
  selected props and remain confined to time scope," already proven by Step 036) and the phase gate
  demonstration (§7: "Analyze a click/drum sample, see beat markers, correct an intentionally wrong
  downbeat, generate restrained pops for two props and play them synchronized while comparing
  before/after") — this step's own diagnostics sit directly on top of that already-corrected grid, not a
  separate workflow.
- Read `implementation-evidence/PHASE-05/STEP-031.md` through `STEP-036.md` in full and treated every
  type/behavior they describe as an immutable prior contract: the full `OnsetAnalysis`/`TempoAnalysis`
  chain, `BeatMarker`, `BeatGridSettings`, `BeatGrid` (including `CorrectionVersion`), `IBeatGridBuilder`/
  `BeatGridBuilder`, `IBeatGridEditor`/`BeatGridEditor` (`MoveBeat`/`SetDownbeat`/`InsertBeat`/
  `RemoveBeat`, all synchronous, all optimistic-concurrency-checked via `CorrectionVersion`), and
  `IRhythmGesturePlanner`/`RhythmGesturePlanner`. None of these files were opened in write mode this
  session.
- Read the existing `BeatGridEditor.cs` source directly (not just its own evidence) to confirm the exact,
  real behavior this step's diagnostics and auto-correction loop build on: `MoveBeat`/`InsertBeat` set
  `Confidence = UserCorrectedConfidence` (`1.0`) **and** `UserCorrected = true` on the affected beat;
  `SetDownbeat` sets only `UserCorrected = true` (leaves `Confidence` as-is) on the beat it reassigns as
  the downbeat. This directly informed this step's own "effective confidence" design decision (§3): a
  human-confirmed beat (`UserCorrected == true`) is always treated as fully certain for this step's own
  quality scoring, regardless of its own raw, possibly-stale `Confidence` value — the same convention
  `BeatGridEditor.UserCorrectedConfidence` already establishes, reused rather than redefined.
- Baseline regression before any Step 037 code existed, run fresh in this session:
  **461/461 passing** (`dotnet test XlightsAI.Studio.slnx --nologo`), matching Step 036's own final
  recorded count exactly (71 Xlights + 91 Editing + 25 Projects + 34 Audio + 142 Shell + 67 Rhythm + 31
  Reference).

## 2. Scope actually implemented

**No new `ProjectReference`, no new project, no solution changes.** All new files added to the existing
`src/XlightsAI.Studio.Rhythm/XlightsAI.Studio.Rhythm.csproj` and
`tests/XlightsAI.Studio.Rhythm.Tests/XlightsAI.Studio.Rhythm.Tests.csproj` projects — this step is
entirely a diagnostics/quality layer over the already-complete `BeatGrid`/`BeatGridEditor` surface
(Steps 034/035); it needs no new cross-project dependency (unlike Step 036's own disclosed
`Rhythm -> Xlights` reference).

### New domain types (`src/XlightsAI.Studio.Rhythm/`)

- **`BeatGridQualityGateSettings.cs`** — `(double LowConfidenceThreshold, long DeviationHistogramBucketTicks)`,
  the phase's own cache-key-style settings-record convention (even though this gate is never itself
  cached — a pure, synchronous recomputation, same precedent as `IBeatGridEditor`/`IRhythmGesturePlanner`).
- **`TimingDeviationBucket.cs`** — `(long BucketStartTicks, long BucketEndTicksExclusive, int Count)`,
  Section 2's own half-open `[start, end)` convention applied to a deviation-magnitude axis.
- **`BeatGridQualityReport.cs`** — `(Guid BeatGridVersionId, long CorrectionVersion, int BeatCount,
  long? FirstBeatOffsetTicks, double OverallConfidence, bool IsLowConfidence,
  IReadOnlyList<TimingDeviationBucket> TimingDeviationHistogram, IReadOnlyList<string> Warnings)`.
- **`BeatGridQualityGateInvalidException.cs`** — mirrors `BeatGridInvalidException`'s own shape;
  thrown for an out-of-range `LowConfidenceThreshold` or a non-positive `DeviationHistogramBucketTicks`.
- **`IBeatGridQualityGate.cs`** / **`BeatGridQualityGate.cs`** — real implementation. For an empty grid,
  returns an explicit, non-throwing report (`BeatCount=0`, `FirstBeatOffsetTicks=null`,
  `OverallConfidence=0.0`, `IsLowConfidence=true`, one disclosed warning) — this step's own "silence...
  must be handled explicitly" convention, consistent with every prior step. For a non-empty grid:
  `FirstBeatOffsetTicks` is the earliest beat's own `TimeTicks` (implementation guidance, verbatim:
  "first-beat offset"); `OverallConfidence` is the mean of each beat's *effective* confidence
  (`UserCorrected ? BeatGridEditor.UserCorrectedConfidence : Confidence`); `IsLowConfidence` is
  `OverallConfidence < LowConfidenceThreshold`, and a disclosed `Warnings` entry is added only when
  flagged (implementation guidance, verbatim: "false positives must show warning"); the
  `TimingDeviationHistogram` buckets each beat-after-the-first's absolute deviation from the position a
  perfectly constant tempo (anchored at the grid's own first beat) would predict, using
  `BeatGrid.SelectedTempo`'s own BPM — empty (not fabricated) when there are fewer than 2 beats or no
  selected tempo exists (implementation guidance, verbatim: "Add timing deviation histograms").
- **`RhythmExportBlockedException.cs`** / **`IRhythmExportGate.cs`** / **`RhythmExportGate.cs`** — the
  concrete realization of this step's own "false positives must show warning, not trigger a blind
  whole-song export" guidance. `EnsureExportAllowed(songId, report, requestedRange, fullSongRange,
  lowConfidenceAcknowledged)` throws only when `requestedRange == fullSongRange` **and**
  `report.IsLowConfidence` **and** `!lowConfidenceAcknowledged` — a deliberately narrower, partial-range
  export (e.g. a scrubbed preview) is always allowed regardless of confidence, since only a literal,
  indiscriminate *whole-song* export is named by this step's own guidance.
- **`BeatGridCorrectionProposalKind.cs`** (`MoveBeat` | `SetDownbeat`) / **`BeatGridCorrectionProposal.cs`**
  (`(Kind, Guid BeatId, long? NewTimeTicks)`) / **`BeatGridCorrectionOutcome.cs`**
  (`(BeatGrid Grid, BeatGridQualityReport ReportBefore, BeatGridQualityReport? ReportAfter,
  int AttemptsUsed, bool Committed, string? RejectionReason)`) / **`BeatGridCorrectionInvalidException.cs`**
  (thrown only for a non-positive `maxAttempts` — a caller programming error, distinct from a per-item
  "malformed proposal," which is reported through `RejectionReason` instead of thrown).
- **`IBeatGridAutoCorrectionLoop.cs`** / **`BeatGridAutoCorrectionLoop.cs`** — real implementation. This
  step's own concrete answer to the failure/edge case, verbatim: "Malformed proposal, no-improvement and
  exhausted retry budget must not auto-commit." Given a `BeatGrid`, an ordered list of candidate
  `BeatGridCorrectionProposal`s and a retry budget (`maxAttempts`), tries up to
  `min(proposals.Count, maxAttempts)` candidates in order, applying each through the real, already-tested
  `IBeatGridEditor` (never fabricating an edit of its own — Section 2's own "AI trust... do not execute...
  directly" rule, applied to this step's own validated-proposal boundary). A proposal the real editor
  itself rejects (`BeatGridEditInvalidException` — unknown beat id, missing required field) is caught,
  recorded, and skipped, consuming one attempt but never committed. A structurally valid edit whose
  resulting `BeatGridQualityReport.OverallConfidence` is not strictly greater than the original report's
  own confidence is discarded, not kept. The loop commits and returns immediately on the first candidate
  that produces a real, measured improvement; if the budget is exhausted without one, it returns the
  **original, completely unmodified grid** (`Committed: false`), with a disclosed `RejectionReason` that
  explicitly names "retry budget exhausted" whenever more candidates existed than the budget allowed
  trying.

### New test file

`tests/XlightsAI.Studio.Rhythm.Tests/Step037AcceptanceTests.cs` — 15 tests (detailed in §4). No changes
to `Step031AcceptanceTests.cs` through `Step036AcceptanceTests.cs`.

**No changes to any existing `Contracts`, `Audio`, `Editing`, `Xlights`, `Projects`, `Reference`,
`Shell`, `App`, or any prior Phase 05 `Rhythm` type's own public shape.** `BeatMarker`,
`BeatGridSettings`, `BeatGrid`, `IBeatGridBuilder`/`BeatGridBuilder`, `IBeatGridEditor`/`BeatGridEditor`,
`IBeatGridStore`/`JsonBeatGridStore`, every exception type, `IRhythmGesturePlanner`/
`RhythmGesturePlanner` — all completely unmodified, confirmed by every one of the 461 pre-existing tests
still passing unmodified (§4).

## 3. Architecture deviations / decisions

- **Section 3 proposes no explicit interface for this step** (its class diagram and proposed signatures
  stop at `IRhythmGesturePlanner`). Step 037's own required acceptance/implementation guidance text is
  therefore this step's sole source of truth, not a Section 3 signature to narrow — disclosed explicitly
  as a real, positive finding (same as Step 036's own disclosed "no narrowing needed" finding), not
  silently skipped as "nothing to report."
- **"Effective confidence" treats every `UserCorrected == true` beat as fully certain
  (`BeatGridEditor.UserCorrectedConfidence`, `1.0`), regardless of its own raw `BeatMarker.Confidence`.**
  This is the one deliberate modeling decision connecting this step's own diagnostics to Step 035's
  already-existing correction surface: `BeatGridEditor.SetDownbeat` does not itself update `Confidence`
  (only `MoveBeat`/`InsertBeat` do, per §1's own direct source inspection) — without this "effective
  confidence" rule, confirming a beat via `SetDownbeat` would not visibly raise this step's own quality
  score at all, which would make "correction is possible" (this step's own required acceptance, verbatim)
  untestable for that one specific, already-existing edit kind. This does not change
  `BeatMarker.Confidence` itself anywhere — it is purely how `BeatGridQualityGate.Evaluate` *reads* an
  already-corrected beat, never a mutation.
- **The auto-correction loop only ever drives the existing, already-tested `IBeatGridEditor` — it
  introduces no new kind of edit.** A `BeatGridCorrectionProposal` is explicitly modeled as untrusted,
  unexecuted *intent* (Section 2: "AI trust... do not execute LLM-produced code, scripts or XML
  directly," generalized here to "do not execute a proposed edit directly — only ever realize it by
  calling the real, validated editor"). This is a deliberate scope choice: Step 037's own failure/edge
  case text ("Malformed proposal, no-improvement and exhausted retry budget must not auto-commit") reads
  as a generic "bounded, validated-proposal loop" pattern (the same shape Section 1's table reserves for
  `LoopRun`/`Candidate`/`QualityReport`, formally introduced in Phase 12) — applied here at phase-local
  scope, over this phase's own real `IBeatGridEditor`, rather than waiting for that later phase's formal
  contract or fabricating a parallel, duplicate editing surface.
- **The loop commits on the *first* candidate that measurably improves confidence, not the *best* of all
  candidates.** A disclosed, deliberate simplicity choice: Step 037's own required acceptance only names
  "low-confidence results are flagged and correction is possible," not "the optimal correction is always
  found." Exhaustively evaluating every candidate before choosing the best would also silently discard
  the caller's own ordering/priority intent in `proposals` — first-improving-wins respects that ordering.
- **`IRhythmExportGate` only gates a requested range that is byte-for-byte equal to the full song range**
  — a narrower, explicitly-scoped partial export (e.g. a scrubbed preview over a selected time window) is
  never blocked, regardless of confidence. This is the literal reading of this step's own guidance,
  verbatim: "...not trigger a **blind whole-song export**" (not "not trigger any export of a low-confidence
  grid") — blocking every export of a flagged grid would make even a deliberate, scoped preview
  impossible, which this step's own guidance does not ask for.
- **No wiring into the real `XsqExporter`/`SequenceDocument` export path.** `IRhythmExportGate` is a
  phase-local domain guard a caller is expected to invoke before calling into the real PHASE-02 export
  pipeline; it does not itself call `XsqExporter`. Disclosed scope decision, consistent with Steps
  034/035/036's own identical precedent of not wiring new Rhythm logic into `Shell`/`App`/`Xlights` beyond
  what each step's own required acceptance strictly demands (Step 036 needed the `Xlights` reference only
  to construct real `EffectInstance` values for its own export-roundtrip acceptance test; this step's own
  acceptance text names a *warning*/block behavior, which is fully provable at the domain level alone —
  see §4's real demo, which does exercise the gate against a real derived `fullSongRange`, just without
  an actual `.xsq` write).
- **No UI wiring (SCREEN 09 BEAT GRID / a confidence-diagnostics panel) in this step** — identical,
  disclosed scope decision to every prior Phase 05 step; no such wiring exists anywhere in `Shell`/`App`
  for any Phase 05 step yet.

## 4. Commands actually executed

All run via the PowerShell tool — the sandboxed POSIX Bash tool in this session again returned
non-functional results for basic commands (`git log`, `mkdir`), the same pre-existing environment
limitation recorded in every prior step's own evidence; PowerShell was used for every command below
without incident.

```
dotnet build XlightsAI.Studio.slnx --nologo                                                 # baseline, before any Step 037 code existed
dotnet test  XlightsAI.Studio.slnx --nologo                                                  # baseline regression: 461/461 passing
dotnet build src\XlightsAI.Studio.Rhythm\XlightsAI.Studio.Rhythm.csproj --nologo             # new quality-gate/export-gate/correction-loop types, before tests existed
dotnet build tests\XlightsAI.Studio.Rhythm.Tests\XlightsAI.Studio.Rhythm.Tests.csproj --nologo
dotnet test  tests\XlightsAI.Studio.Rhythm.Tests\XlightsAI.Studio.Rhythm.Tests.csproj --nologo --logger "console;verbosity=detailed" --filter "FullyQualifiedName~Step037"
  # first and only real run: 15/15 passed (no red-then-green cycle needed; every arithmetic fixture
  # value — confidence averages, histogram bucket boundaries, period-tick math — was worked out by hand
  # before writing the test, then confirmed by the real run; disclosed explicitly rather than overclaimed
  # as "no reasoning required")
dotnet build XlightsAI.Studio.slnx --nologo                                                  # full solution, after Step 037 implementation
dotnet test  XlightsAI.Studio.slnx --nologo                                                   # full regression, final
```

### Build results

`Rhythm`, `Rhythm.Tests`, and the full solution all: `Build succeeded. 0 Warning(s). 0 Error(s).`

### Targeted test results (Step 037 tests only, real run)

```
Passed XlightsAI.Studio.Rhythm.Tests.Step037AcceptanceTests.ExportGate_NullReport_ThrowsArgumentNullException [8 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step037AcceptanceTests.ExportGate_AllowsWholeSongExport_WhenAcknowledged [10 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step037AcceptanceTests.ExportGate_AllowsNarrowerPartialRangeExport_EvenWhenLowConfidence [< 1 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step037AcceptanceTests.TryApply_NullArguments_ThrowArgumentNullException [3 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step037AcceptanceTests.Evaluate_EmptyGrid_FlagsLowConfidence_SilenceEdgeCase [13 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step037AcceptanceTests.ExportGate_BlocksBlindWholeSongExport_WhenLowConfidenceAndNotAcknowledged_NegativePath [1 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step037AcceptanceTests.Evaluate_InvalidThreshold_ThrowsInvalid_NegativePath [1 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step037AcceptanceTests.TryApply_NonPositiveMaxAttempts_ThrowsInvalid_NegativePath [< 1 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step037AcceptanceTests.TryApply_ProposalDoesNotImprove_DoesNotAutoCommit_NegativePath [16 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step037AcceptanceTests.Evaluate_NullGrid_ThrowsArgumentNullException [< 1 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step037AcceptanceTests.Evaluate_MultipleDeviations_BucketsHistogramCorrectly [3 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step037AcceptanceTests.Step037_Acceptance_Passes_LowConfidenceFlaggedAndCorrectionIsPossible [53 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step037AcceptanceTests.TryApply_MalformedProposal_UnknownBeatId_DoesNotAutoCommit_NegativePath [1 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step037AcceptanceTests.TryApply_RetryBudgetExhausted_StopsBeforeTryingEveryProposal_DoesNotAutoCommit_NegativePath [< 1 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step037AcceptanceTests.TryApply_NoProposalsSupplied_DoesNotAutoCommit_NegativePath [< 1 ms]

Total tests: 15. Passed: 15, Failed: 0.
```

### Full regression (after implementation, final run)

```
Passed!  - Failed: 0, Passed:  71, Skipped: 0, Total:  71  - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed:  91, Skipped: 0, Total:  91  - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed:  34, Skipped: 0, Total:  34  - XlightsAI.Studio.Audio.Tests.dll
Passed!  - Failed: 0, Passed:  25, Skipped: 0, Total:  25  - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed: 142, Skipped: 0, Total: 142 - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed:  82, Skipped: 0, Total:  82  - XlightsAI.Studio.Rhythm.Tests.dll
Passed!  - Failed: 0, Passed:  31, Skipped: 0, Total:  31  - XlightsAI.Studio.Reference.Tests.dll
```

**476 total, 0 failed, 0 skipped** — up from the Step 036 baseline of 461 (+15 new
`Step037AcceptanceTests` in the already-existing `Rhythm.Tests` project: 82 = 7 Step031 + 9 Step032 +
10 Step033 + 11 Step034 + 15 Step035 + 15 Step036 + 15 Step037). Every one of the 461 pre-existing tests
still passes unmodified, confirming no prior step's contract was broken by this step's new, purely
additive types.

### Persisted-output inspection / observable demonstration for this step

Step 037's own required acceptance is "Low-confidence results are flagged and correction is possible";
its implementation guidance names timing-deviation histograms, first-beat offset, confidence, and
blocking a blind whole-song export on a flagged (and unacknowledged) low-confidence result. As with
Steps 034/035/036 (same disclosed scope decision, §3), no `BeatGrid`/diagnostics screen or
`Shell`/`App` wiring exists in this repository yet; the observable demonstration is a real, running
end-to-end exercise of this step's actual diagnostics and gating behavior against a real, analyzer-derived
`BeatGrid` — not a hand-built literal — run from a throwaway console program (not part of the repository,
under the session scratchpad, deleted after this run) referencing the real `Rhythm`/`Audio`/`Reference`/
`Contracts` projects:

```
dotnet run --project <scratchpad>\step037-demo\step037-demo.csproj --nologo
```

Observed real output (a 120 BPM click-track fixture, same generation shape as Steps 034-036's own demos —
10 clicks, 0.5 s silent lead-in, 8 kHz; `LowConfidenceThreshold` deliberately set to `0.95` for this demo
run so the gate's flagging/blocking behavior is actually exercised against this fixture's own real,
analyzer-produced confidence rather than only proven in isolation by hand-built test fixtures):

```
Probed SongAsset: Id=SongId { Value = f68df63d-1c4c-422f-8709-7df8563b513f }, Sha256=00153bacb3fa..., SampleCount=44000
Built beat grid: BeatCount=10, Top=120.00 BPM

=== Quality report (before correction) ===
BeatCount=10  FirstBeatOffsetTicks=4960000  OverallConfidence=89.0%  IsLowConfidence=True
TimingDeviationHistogram: [0,320000)=9
WARNING: Overall confidence 89% is below the configured threshold 95%; manual correction (via IBeatGridEditor) is recommended before relying on this grid for a full-song export.

Export gate: narrower preview-range export allowed even while low-confidence (as expected).
Export gate: blocked blind whole-song export as expected: Blocked a full-song export for song 'f68df63d-1c4c-422f-8709-7df8563b513f': the beat grid is flagged low-confidence and has not been acknowledged. Overall confidence 89% is below the configured threshold 95%; manual correction (via IBeatGridEditor) is recommended before relying on this grid for a full-song export.
Blind whole-song export blocked: True

Lowest-confidence beat: Id=2f4c09ce-9bb1-44d7-b8d4-8526bdf4e1b6, Confidence=75.8%
Malformed-proposal attempt: Committed=False, Reason=Proposal #0 (MoveBeat on beat 'd346f5cb-b5b4-48be-8aaa-a2af5ad5477a') was malformed and was not committed: No beat with Id 'd346f5cb-b5b4-48be-8aaa-a2af5ad5477a' exists in this grid.
Confirm-in-place attempt: Committed=True
Quality after correction: OverallConfidence=91.4%  IsLowConfidence=True
Re-read persisted corrected grid: CorrectionVersion=1, matches in-memory: True

Step 037 manual demo completed successfully.
Temp directory cleaned up.
```

Direct, real, non-test-harness confirmation of every element of this step's own required acceptance and
implementation guidance: the real, analyzer-derived grid's overall confidence (`89.0%`) and first-beat
offset (`4960000` ticks) are computed and printed; a real, non-empty timing-deviation histogram bucket is
produced from real beat timings; the grid is correctly flagged low-confidence against the configured
threshold with a real, disclosed warning message ("Low-confidence results are flagged," required
acceptance, verbatim); a real attempted whole-song export is actually blocked with a real thrown
`RhythmExportBlockedException` carrying that same warning, while a real, narrower preview-range export of
the identical grid is allowed without exception ("false positives must show warning, not trigger a blind
whole-song export," implementation guidance, verbatim); a real malformed proposal (an unknown beat id) is
rejected without mutating the grid, proven by the printed `Committed=False` outcome ("malformed
proposal... must not auto-commit," failure/edge case, verbatim); and a real, genuinely improving
correction (confirming the grid's own lowest-confidence beat in place via the already-existing, unmodified
`IBeatGridEditor.MoveBeat`) is committed and measurably raises `OverallConfidence` from `89.0%` to
`91.4%`, and that corrected grid is independently re-read back from a real, persisted `JsonBeatGridStore`
file on disk with a matching `CorrectionVersion` ("...and correction is possible," required acceptance,
verbatim, proven as a real, persisted, round-tripped result — not merely an in-memory assertion).

## 5. Fixtures / input hashes

No new fixture files were added to `fixtures/`. All tests and the manual demo construct real WAV bytes
via this repository's own already-licensed, already-verified `DeterministicSyntheticAudioGenerator`,
identical generation parameters to Steps 034-036's own fixtures:

- `BuildClickTrack(bpm: 120, clickCount: 10, clickDurationSeconds: 0.02, toneHz: 1000.0, sampleRate: 8000)`
  prepended with 4000 samples (0.5 s) of silence — the manual demo's own fixture, run end-to-end through
  the real, unmodified `OnsetDetector` -> `TempoDetector` -> `BeatGridBuilder` chain.
- Hand-constructed `BeatGrid`/`BeatMarker` literals (no audio decoding) for every deterministic unit test
  in `Step037AcceptanceTests.cs` — the same precedent Step 036's own silence/empty-grid edge-case tests
  already established; chosen here because this step's own arithmetic (confidence averages, deviation
  bucket boundaries) needed exact, hand-verifiable input values rather than whatever a real detector
  happens to produce.
- All test files are written under `%TEMP%\xlai-step035-*\`/in-memory only (this step's own tests perform
  no file I/O at all — `BeatGridQualityGate`/`BeatGridAutoCorrectionLoop`/`RhythmExportGate` are all pure,
  synchronous, in-memory functions). The manual demo's own throwaway files were written under
  `%TEMP%\xlai-step037-demo-*\` and deleted by the demo program itself at the end of its run (confirmed by
  "Temp directory cleaned up." in the real output above); the throwaway demo *project* itself (outside the
  repository, under the session scratchpad) was deleted after this evidence was recorded.

## 6. Unresolved issues / known gaps

- **No UI wiring (SCREEN 09 BEAT GRID / a confidence-diagnostics panel) in this step** — a disclosed,
  deliberate scoping decision, consistent with Steps 034/035/036's own identical precedent: no such
  wiring exists anywhere in `Shell`/`App` yet for any Phase 05 step.
- **`IRhythmExportGate` is not wired into the real `XsqExporter`/`SequenceDocument` export pipeline.** It
  is a phase-local domain guard a caller must choose to invoke before calling into PHASE-02's real export
  path; this step does not modify `XsqExporter` itself to call it automatically. A caller that bypasses
  `IRhythmExportGate` entirely and calls `XsqExporter` directly would not be blocked — the gate protects
  only call sites that actually use it. Making the real export pipeline itself unconditionally
  gate-checked is a disclosed, out-of-scope integration decision for a later phase/step, not fabricated
  here.
- **The auto-correction loop never auto-*selects* which beat(s) to propose correcting** — it only ever
  evaluates caller-supplied `BeatGridCorrectionProposal`s. An "automatically find and propose the
  lowest-confidence beat" convenience helper was deliberately not added: Step 037's own required
  acceptance says "correction is possible," not "correction is automatically discovered," and inventing an
  auto-selection heuristic risks quietly becoming an unrequested auto-commit path of its own.
- **The loop commits on the first improving candidate, not the best of all candidates** — disclosed in §3
  as a deliberate simplicity choice consistent with this step's own required acceptance text.
- **This step's own quality/diagnostics model is phase-local** — it is not the formally-named
  `LoopRun`/`Candidate`/`QualityReport` triad Section 1's cross-phase ownership table reserves for Phase
  12. This step's own `BeatGridQualityReport`/`BeatGridCorrectionOutcome` are deliberately *not* aliased to
  or pre-declared as those future types; a Phase 12 implementer will need to reconcile or supersede this
  step's own narrower, phase-local shapes, not silently inherit them — disclosed explicitly per Section 8's
  own "Update architecture decision records for genuine tradeoffs" guidance.

## 7. PASS / BLOCKED

**Status: PASS** for Step 037's own stated required acceptance and failure/edge case:

- "Low-confidence results are flagged" (required acceptance, verbatim) — proven by
  `Step037_Acceptance_Passes_LowConfidenceFlaggedAndCorrectionIsPossible` (a hand-built, deterministic
  grid below the configured threshold is flagged `IsLowConfidence: true` with a real, non-empty
  `Warnings` entry) and independently confirmed by the real, non-test-harness demo run (§4: a real,
  analyzer-derived grid at `89.0%` confidence is flagged against a `95%` threshold with the same real
  warning text).
- "...and correction is possible" (required acceptance, verbatim) — proven by the same acceptance test
  (confirming the grid's own low-confidence beat through the real, unmodified `IBeatGridEditor.MoveBeat`
  raises `OverallConfidence` from `55%` to `95%` and clears `IsLowConfidence`) and by the real demo (a
  real analyzer-derived grid's confidence rises from `89.0%` to `91.4%` after a real, committed correction,
  independently re-read back from a real persisted `JsonBeatGridStore` file).
- "Add timing deviation histograms" (implementation guidance, verbatim) — proven by
  `Evaluate_MultipleDeviations_BucketsHistogramCorrectly` (four hand-placed beats with known, distinct
  deviations land in exactly the three expected, correctly-bounded buckets) and
  `Evaluate_EmptyGrid_FlagsLowConfidence_SilenceEdgeCase` (an empty/no-tempo grid explicitly produces an
  empty histogram, never a fabricated one).
- "...first-beat offset and confidence" (implementation guidance, verbatim) — proven by the same
  acceptance test (`FirstBeatOffsetTicks` and `OverallConfidence` both asserted against hand-computed
  expected values) and directly observed in the real demo's own printed report line.
- "...false positives must show warning, not trigger a blind whole-song export" (implementation guidance,
  verbatim) — proven by `ExportGate_BlocksBlindWholeSongExport_WhenLowConfidenceAndNotAcknowledged_NegativePath`,
  `ExportGate_AllowsWholeSongExport_WhenAcknowledged`, and
  `ExportGate_AllowsNarrowerPartialRangeExport_EvenWhenLowConfidence` (a real, full-range request against
  a flagged report is blocked by a real thrown `RhythmExportBlockedException` carrying the real warnings;
  the identical flagged report is allowed through once acknowledged, and is never blocked at all for a
  genuinely narrower, partial-range request) — and independently confirmed by the real demo (§4: a real
  full-song export attempt is actually blocked; a real, narrower preview-range export of the identical
  grid is allowed).
- "Malformed proposal... must not auto-commit" (failure/edge case, verbatim) — proven by
  `TryApply_MalformedProposal_UnknownBeatId_DoesNotAutoCommit_NegativePath` (a proposal targeting a
  nonexistent beat id is rejected, the original grid is returned completely unmodified, and the real
  rejection reason is disclosed) and the real demo (an unknown-beat-id proposal is rejected with
  `Committed=False`).
- "...no-improvement... must not auto-commit" (failure/edge case, verbatim) — proven by
  `TryApply_ProposalDoesNotImprove_DoesNotAutoCommit_NegativePath` (a structurally valid `SetDownbeat`
  proposal against an already-fully-confident grid succeeds as an edit but does not raise confidence, and
  is explicitly discarded rather than kept).
- "...and exhausted retry budget must not auto-commit" (failure/edge case, verbatim) — proven by
  `TryApply_RetryBudgetExhausted_StopsBeforeTryingEveryProposal_DoesNotAutoCommit_NegativePath`: a
  genuinely improving third proposal exists in the supplied list, but a budget of `2` stops the loop
  before reaching it (`AttemptsUsed == 2`, `Committed == false`, original grid returned unmodified,
  disclosed "retry budget exhausted" reason) — and the same test then re-runs the identical proposal list
  with a budget of `3` to independently prove the third proposal really would have committed, making the
  budget enforcement a real, load-bearing limit rather than a fabricated "nothing would have helped"
  claim.
- Settings validation — proven by `Evaluate_InvalidThreshold_ThrowsInvalid_NegativePath` (both an
  out-of-`[0,1]` threshold and a non-positive bucket size are rejected before any computation).
- Null-argument handling — proven by `Evaluate_NullGrid_ThrowsArgumentNullException` and
  `TryApply_NullArguments_ThrowArgumentNullException` (covers a null grid, null proposals list, null
  settings, and both constructor dependencies of `BeatGridAutoCorrectionLoop`), plus
  `ExportGate_NullReport_ThrowsArgumentNullException`.
- Request-shape validation for the correction loop itself — proven by
  `TryApply_NonPositiveMaxAttempts_ThrowsInvalid_NegativePath` and
  `TryApply_NoProposalsSupplied_DoesNotAutoCommit_NegativePath` (an empty proposal list is a valid,
  non-throwing "nothing to try" result, not an error — consistent with this phase's own "silence... must
  be handled explicitly" convention).
- Every claim above is backed by an actually-executed, actually-passing automated test — all 15 of which
  passed on their first real run (§4) — plus a real, non-test-harness manual demonstration against a
  real, analyzer-derived `BeatGrid`, not merely a hand-built fixture, independently confirming the same
  behaviors end-to-end.

No previously accepted step's contract was modified in any way — every PHASE-01-04 public member and
every prior Phase 05 `Rhythm` type (`BeatMarker`, `BeatGridSettings`, `BeatGrid`, `IBeatGridBuilder`/
`BeatGridBuilder`, `IBeatGridEditor`/`BeatGridEditor`, `IRhythmGesturePlanner`/`RhythmGesturePlanner`,
every exception type) is completely unchanged — confirmed by the full pre-existing 461-test suite passing
unmodified before a single Step 037 line of code existed (§1), and again at 476/476 (461 old + 15 new)
after implementation (§4), with every prior step's own tests individually still passing by name in that
same run. This step introduced no new project reference and no solution-level change.

## 8. Phase-wide status after this session

Steps 031, 032, 033, 034, 035, 036 and now 037 (all seven of PHASE-05's numbered steps, 031-037) now have
recorded PASS evidence. Per Section 7/8's own required final gate report, the phase-wide Gate Report
(aggregating all seven steps' evidence, the full-phase test plan/quality-gate matrix of §7, and the exact
phase gate demonstration workflow: "Analyze a click/drum sample, see beat markers, correct an
intentionally wrong downbeat, generate restrained pops for two props and play them synchronized while
comparing before/after") is **not** produced by this session — per the task's own instruction, this
session implements only Step 037 and does not proceed past it (including not authoring the phase-wide Gate
Report itself) until this evidence is reviewed.

**Note on real-xLights human verification:** per this user's own standing waiver (recorded in this
assistant's memory), a real-xLights render/open check is not required as a phase-gate blocker for this
step or this phase. This is recorded as a waiver, not re-requested here.
