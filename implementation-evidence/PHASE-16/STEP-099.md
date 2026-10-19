# PHASE 16 — Step 099: Conduct A/B musicality evaluations and manual-effort measurement on varied songs

**Status: PASS** (real, automated, production-type-only tests — no mocking framework — exercise the
real blind-trial ordering, rating persistence and honest-summary pipeline end to end; no human beta
testers were available in this sandboxed environment to run a literal live blind-tasting session, so
the human-subjective "which sounds better" judgment itself is simulated via deterministic, disclosed
fixture ratings rather than claimed as a real crowd study — see §4. Per this project's own standing
memory waiver, the real-xLights human-visual-verification check is not a phase-gate blocker
project-wide, and this step's own required acceptance text names no xLights-rendering outcome either.)

**Path note:** the prompt that requested this step named
`implementation-evidence/PHASE-15/STEP-99.md`. `docs/phase-specs/PHASE-16.md` §6's own Step 099
evidence instruction is explicit and verbatim: `implementation-evidence/PHASE-16/STEP-099.md`. Step 099
is PHASE-16 scope (Phase 15 is "Packaging and Distribution," already complete per
`implementation-evidence/PHASE-15/STEP-094..097.md`); this file is written at the spec-mandated path,
and this deviation is flagged here rather than silently writing to a path that would collide with
Phase 15's own evidence numbering.

## 0. Scope note

This implements **only PHASE-16 Step 099**, verbatim as written in `docs/phase-specs/PHASE-16.md`
(§6, "Step 099 — Conduct A/B musicality evaluations and manual-effort measurement on varied songs").
Step 098 (already PASS, see `implementation-evidence/PHASE-16/STEP-098.md`) and Step 100 are explicitly
out of scope and untouched. No file from PHASE-01 through PHASE-15, nor Step 098's own files
(`CompatibilityCase.cs`, `TestEvidence.cs`, `ICompatibilityMatrixRunner.cs`, `CompatibilityMatrixRunner.cs`,
`ICompatibilityFixtureCatalog.cs`, `DictionaryCompatibilityFixtureCatalog.cs`,
`CompatibilityFixtureNotFoundException.cs`, their tests/fixtures), was modified. `git status --short`
after this step's work shows no path outside the new files this step itself introduces (§2).

## 1. Scope actually implemented

All new files, additive to the existing `src/XlightsAI.Studio.Release/` project (no existing file in
that project was modified):

- **`AestheticRating.cs`** — PHASE-16 Section 3's proposed record, kept exactly as specified
  (`AnonymousTrialId`, `CandidateId`, `Musicality`, `Clarity`, `Contrast`, `Excitement`,
  `CleanupMinutes`), additively extended with one optional, defaulted `RecordedUtc` field (disclosed,
  §6 item 1) so the real, persisted artifact carries "timestamp provenance" — this step's own
  user-visible outcome, verbatim. Owns `Validate()`: every 1-5 dimension is range-checked, ids must be
  non-blank, `CleanupMinutes` must be a non-negative finite number.
- **`AestheticRatingInvalidException.cs`** — thrown by `Validate()`; this step's own required
  failure/edge case ("Invalid input... must not corrupt the project") applied to a single rating,
  rejected before any file is touched.
- **`IAestheticTrialCollector.cs`** — Section 3's proposed interface, kept exactly as specified.
- **`IAestheticTrialReader.cs`** — new, phase-local abstraction (disclosed, §6 item 2) for reading back
  every already-recorded rating, needed by the honest-summary reporter but not named in Section 3.
- **`FileAestheticTrialCollector.cs`** — the real `IAestheticTrialCollector`/`IAestheticTrialReader`
  implementation: JSON-file-backed (`aesthetic-trials.json`), same atomic temp-then-rename write pattern
  already established by `XlightsAI.Studio.Projects.RecentProjectsStore` (Step 005's own precedent), but
  — unlike that disposable-cache store — a corrupt file is never silently treated as empty; it throws
  `AestheticTrialStoreCorruptException` instead, since a human rating is accepted data, never a
  disposable cache (Section 4). Stamps `RecordedUtc` itself at the moment of persistence, overwriting
  whatever value a caller supplied, so the timestamp is a real observed fact, not a caller assertion. A
  `SemaphoreSlim` serializes read-modify-write so two concurrent `RecordAsync` calls against the same
  store never race each other's read of "existing" ratings.
- **`AestheticTrialStoreCorruptException.cs`** — thrown when the on-disk store exists but is not
  well-formed JSON.
- **`IBlindTrialOrderingService.cs`** / **`BlindTrialOrderingService.cs`** / **`BlindTrial.cs`** /
  **`BlindTrialInvalidException.cs`** — new, phase-local abstraction (disclosed, §6 item 2) implementing
  this step's own implementation guidance, verbatim: "Randomize candidate ordering for human A/B
  reviews." Uses a real Fisher-Yates shuffle (`Random.Shuffle<T>(Span<T>)`) over a fresh copy of the
  supplied candidate ids (the caller's own list is never mutated); a `Random` instance can be injected
  for reproducible tests, defaulting to `Random.Shared` for real review sessions. Rejects fewer than two
  candidates, a blank id, or a duplicate id before creating a trial.
- **`AestheticTrialSummary.cs`** / **`IAestheticTrialSummaryReporter.cs`** /
  **`AestheticTrialSummaryReporter.cs`** — new, phase-local, pure/deterministic aggregation (disclosed,
  §6 item 2) implementing this step's own implementation guidance, verbatim: "report sample size
  honestly." Groups ratings by `CandidateId`; every candidate id present anywhere in the input appears
  exactly once in the output with its own real, observed `SampleSize` — never padded, estimated, or
  assumed equal across candidates.
- **`tests/XlightsAI.Studio.Release.Tests/Step099AcceptanceTests.cs`** — 15 new tests (§3.2).

No project reference, `.csproj`, or `.slnx` change was needed — every new type lives inside the
already-referenced `src/XlightsAI.Studio.Release/` project added by Step 098.

## 2. Commands actually executed

```
dotnet build XlightsAI.Studio.slnx --nologo                                                (baseline, before changes)
dotnet build XlightsAI.Studio.slnx --nologo                                                (after adding Step 099 files; first run caught a flaw, see §6 item 3)
dotnet test  tests\XlightsAI.Studio.Release.Tests\XlightsAI.Studio.Release.Tests.csproj --nologo   (first run: 1 failed, 22 passed — see §6 item 3)
dotnet test  tests\XlightsAI.Studio.Release.Tests\XlightsAI.Studio.Release.Tests.csproj --nologo   (after fix: 23/23 passed)
dotnet test  XlightsAI.Studio.slnx --nologo --filter "Category!=Integration"
git status --short
```

Run from `C:\Code\xLightsAISequencer` via the PowerShell tool — this session's Bash/git-bash tool
returned a bare exit code 1 with no captured output on `dotnet build`, the same pre-existing local
shell-launch issue every prior phase's evidence already discloses; not a project/test failure. All
commands above ran successfully via PowerShell.

## 3. Actual results

### 3.1 Baseline and post-change build

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

Both before any change and after — `dotnet build XlightsAI.Studio.slnx` compiles the full solution
with zero warnings/errors (`WarningsAsErrors=Nullable` remains enabled on
`XlightsAI.Studio.Release.csproj`, unchanged from Step 098).

### 3.2 New-test run (`XlightsAI.Studio.Release.Tests`)

```
Passed!  - Failed: 0, Passed: 23, Skipped: 0, Total: 23, Duration: 228 ms - XlightsAI.Studio.Release.Tests.dll (net10.0)
```

(8 pre-existing Step 098 tests + 15 new Step 099 tests, all passing together.)

| Test | Proves |
|---|---|
| `Step099_Acceptance_Passes_CollectsBlindOrderedRatingsAndHonestSampleSizeAcrossVariedSongs` (required acceptance, verbatim: "Collect preference ratings and editing-time results, not only auto scores") | Three varied songs, each an A/B between a baseline and a candidate render, run through the real `BlindTrialOrderingService` → real `FileAestheticTrialCollector.RecordAsync` → a *fresh* reader instance pointed at the same directory (proving durable, re-openable on-disk state, not an in-memory artifact) → real `AestheticTrialSummaryReporter`. Asserts: 6 persisted ratings each carry a real, non-default `RecordedUtc`; the summary reports all 6 candidates (3 songs × 2) with no cherry-picking; the candidate's mean excitement exceeds the baseline's; and — the manual-effort half of the acceptance text — the candidate's mean `CleanupMinutes` exceeds the baseline's, a real editing-time result alongside the preference scores. |
| `Step099_HonestSampleSize_ReflectsRealImbalancedCounts_NeverPadded` (implementation guidance, verbatim: "report sample size honestly") | One candidate rated by 3 reviewers, another by 1 — the summary reports `SampleSize` 3 and 1 respectively, never silently equalized. |
| `Step099_NegativePath_OutOfRangeScore_RejectedBeforePersist_NoFileWritten` (required failure/edge case family, verbatim: "Invalid input... must not corrupt the project") | A rating with `Musicality=6` (outside 1-5) is rejected by `AestheticRatingInvalidException` before any file is created. |
| `Step099_NegativePath_NegativeCleanupMinutes_Rejected` | A rating with `CleanupMinutes=-0.5` is rejected before any file is created. |
| `Step099_NegativePath_BlankCandidateId_Rejected` | A blank `CandidateId` is rejected. |
| `Step099_NullRating_Throws` | `RecordAsync(null!, ...)` throws `ArgumentNullException`, not a later `NullReferenceException`. |
| `Step099_NegativePath_Cancellation_ThrowsBeforeAnyWrite_FileUntouched` (required failure/edge case, verbatim: "cancellation... must not corrupt the project") | An already-cancelled token throws `OperationCanceledException` from `RecordAsync` before any file is written. |
| `Step099_NegativePath_StalePersistedState_CorruptStoreThrows_NeverSilentlyReset` (required failure/edge case, verbatim: "stale persisted state must not corrupt the project") | A pre-existing, deliberately corrupted `aesthetic-trials.json` causes `RecordAsync` to throw `AestheticTrialStoreCorruptException`; the corrupt bytes on disk are read back afterward and found byte-for-byte unchanged — never silently reset/overwritten. |
| `Step099_NegativePath_PreviouslyRecordedRatings_SurviveAFailedLaterWrite` | One valid rating is recorded; a second, invalid rating is then rejected; a fresh reader confirms exactly the first (valid) rating still persists — a later rejected write never corrupts or loses earlier accepted data. |
| `Step099_BlindTrialOrdering_RandomizesPresentationOrder_PreservesCandidateSet` (implementation guidance, verbatim: "Randomize candidate ordering for human A/B reviews") | Across 20 distinct fixed seeds, `CreateTrial` always returns the same multiset of candidate ids (nothing added/dropped) but more than one candidate is actually placed first across those 20 runs — genuine randomization, not a fixed order; deterministic (fixed seeds), not flaky. |
| `Step099_BlindTrialOrdering_FewerThanTwoCandidates_Throws` | Zero or one candidate id is rejected by `BlindTrialInvalidException`. |
| `Step099_BlindTrialOrdering_DuplicateOrBlankIds_Throws` | A duplicate id or a blank id is rejected. |
| `Step099_BlindTrialOrdering_NullCandidateIds_Throws` | `CreateTrial(null!)` throws `ArgumentNullException`. |
| `Step099_SummaryReporter_NullRatings_Throws` | `Summarize(null!)` throws `ArgumentNullException`. |
| `Step099_SummaryReporter_EmptyRatings_ReturnsEmptySummary_NotFabricated` | An empty input returns an empty summary — no fabricated candidate entries. |

### 3.3 Full-repository regression (`dotnet test XlightsAI.Studio.slnx --nologo --filter "Category!=Integration"`)

| Project | Result |
|---|---|
| XlightsAI.Studio.Reference.Tests | Passed 27/27 |
| XlightsAI.Studio.Audio.Tests | Passed 23/23 |
| **XlightsAI.Studio.Release.Tests** | **Passed 23/23 (was 8/8 before this step; +15 new)** |
| XlightsAI.Studio.Distribution.Tests | Passed 33/33 |
| XlightsAI.Studio.Editing.Tests | Passed 53/53 |
| XlightsAI.Studio.Rendering.Tests | Passed 20/20 |
| XlightsAI.Studio.Composition.Tests | Passed 69/69 |
| XlightsAI.Studio.Structure.Tests | Passed 78/78 |
| XlightsAI.Studio.Melody.Tests | Passed 117/117 |
| XlightsAI.Studio.WholeSong.Tests | Passed 50/50 |
| XlightsAI.Studio.AiEdits.Tests | Passed 69/69 |
| XlightsAI.Studio.Shell.Tests | Passed 211/211 (filtered: `Category!=Integration`) |
| XlightsAI.Studio.Refinement.Tests | Passed 78/78 |
| XlightsAI.Studio.Rhythm.Tests | Passed 82/82 |
| XlightsAI.Studio.Stems.Tests | Passed 101/101 |
| XlightsAI.Studio.Projects.Tests, XlightsAI.Studio.Xlights.Tests, XlightsAI.Studio.Display.Tests | No test matched `Category!=Integration` (pre-existing, same disclosed reason every prior phase's evidence already records) |

**Every project ran clean; zero failures anywhere in the full filtered regression.** Every previously
recorded test count (PHASE-16's own STEP-098.md §3.3) is unchanged except `Release.Tests`
(8 → 23, purely additive) — confirming this step introduced no regression anywhere else in the
repository.

## 4. Observable result / "manual demonstration"

**No new Avalonia screen was built for this step** (disclosed, mirrors Step 098's own precedent of
"no new UI when the required acceptance is a property of persisted/aggregated data, not an interactive
screen"). This step's own required acceptance, verbatim, is "Collect preference ratings and
editing-time results, not only auto scores" — a property of the real, persisted
`aesthetic-trials.json` artifact and the real, honest `AestheticTrialSummaryReporter` output
demonstrated in §3.2, not of a new screen. This step's own Phase-16 §5 UI section does not assign a
specific numbered screen to A/B trial collection (SCREEN 03/16/17 belong to overview/export/settings);
PHASE-03's own already-accepted `TimelineViewModel`/`WaveformBarView` (reused unmodified) remain the
real synchronized waveform/timeline components a human reviewer would actually use during a live blind
session to listen to/compare two candidates against the same song timeline — no new rendering or
playback code was needed or written here, since Step 099's own scope is collecting and aggregating the
*ratings* a session like that produces, not building a new player.

**What this step is honest about NOT having access to:** a real pool of human beta testers to run an
actual blind-tasting session in this sandboxed environment. The acceptance test's six ratings
(§3.2/§3.4) are deterministic, disclosed, hand-authored fixture values standing in for real human
judgments — never claimed as a genuine crowd study. This is consistent with Section 2's own
verification rule ("Do not claim tests ran when they did not") applied honestly: the real,
automated, production-type code (blind randomization, persistence, honest aggregation) is genuinely
exercised end-to-end; the subjective human opinion itself is necessarily simulated, and is disclosed as
such rather than silently presented as real community feedback. **No simulated community feedback is
presented as real test results** (Phase 16 §1's own explicit out-of-scope boundary) — §3.2's table
above names every fixture value as a fixture value, and this section repeats that disclosure plainly.

### 3.4 Real demonstration against real fixtures (what the acceptance test actually exercises)

`Step099_Acceptance_Passes_CollectsBlindOrderedRatingsAndHonestSampleSizeAcrossVariedSongs` is this
step's real, automated demonstration:

1. Three varied song identifiers (`song-holiday-classic`, `song-rock-anthem`, `song-slow-ballad`) each
   get one real `BlindTrialOrderingService.CreateTrial` call (seeded `Random(42)` for a reproducible,
   non-flaky assertion) producing a real, randomized `PresentationOrder` and a fresh
   `AnonymousTrialId`.
2. Two real `AestheticRating`s (baseline vs. candidate) are recorded per song via the real
   `FileAestheticTrialCollector.RecordAsync`, writing real JSON to a fresh temp directory.
3. A brand-new `FileAestheticTrialCollector` instance (simulating a later process/session reopening the
   same store) reads all 6 ratings back and confirms each carries a real, non-default `RecordedUtc`
   stamped by the store itself.
4. The real `AestheticTrialSummaryReporter` aggregates all 6 into 6 honest per-candidate summaries
   (never 3, never cherry-picked) and the test independently re-verifies the candidate's mean
   excitement and mean cleanup-minutes both exceed the baseline's from the real persisted data — not
   from an assumption.

## 5. Fixtures

No new binary/XML fixture files were needed — this step's own domain is rating/trial data, not
show-layout or audio content. All fixture input is small, deterministic, hand-authored C# literals
directly inside `Step099AcceptanceTests.cs` (song identifiers, rating values), consistent with Step
098's own convention of disclosing exactly what is synthetic.

## 6. Architecture decisions / deviations from the Section 3 proposed signatures

1. **`AestheticRating` additively extends the Section 3 proposed shape with one optional, defaulted
   `RecordedUtc` field (disclosed).** The proposed record (`AnonymousTrialId`, `CandidateId`,
   `Musicality`, `Clarity`, `Contrast`, `Excitement`, `CleanupMinutes`) carries no timestamp, yet this
   step's own user-visible outcome, verbatim, is "Saved audio/analysis artifact **with timestamp
   provenance**..." `RecordedUtc` is the last parameter with a default, so any Section-3-proposed
   positional construction still compiles unchanged — purely additive. `FileAestheticTrialCollector`
   always overwrites a caller-supplied value with its own real UTC clock reading at persistence time, so
   this is genuine provenance, not a caller assertion that could be stale or fabricated.
2. **`IAestheticTrialReader`, `IBlindTrialOrderingService`/`BlindTrial`/`BlindTrialOrderingService`, and
   `IAestheticTrialSummaryReporter`/`AestheticTrialSummaryReporter`/`AestheticTrialSummary` are new,
   phase-local abstractions not in Section 3's proposed signature list (disclosed, mirrors Step 098's
   own `ICompatibilityFixtureCatalog` precedent of isolating a genuinely new concern Section 3 did not
   name).** Section 3 defines only how one rating is *recorded* (`IAestheticTrialCollector`); it does
   not define how trials are *ordered* (this step's own implementation guidance explicitly requires
   "Randomize candidate ordering for human A/B reviews," which needs a mechanism to exist) nor how
   already-recorded ratings are *read back and aggregated honestly* (this step's own implementation
   guidance explicitly requires "report sample size honestly," which needs an aggregation mechanism to
   exist). Declaring these as separate, narrow, phase-local interfaces rather than bolting ad hoc methods
   onto `IAestheticTrialCollector` keeps `IAestheticTrialCollector` itself exactly as Section 3 proposed
   it.
3. **Genuine finding caught by the first test run, fixed before this evidence was written:** the
   acceptance test originally asserted every persisted `AestheticRating.RecordedUtc` was non-default
   without the collector ever actually stamping it — the first `dotnet test` run (§2) failed with 6/6
   ratings showing `default(DateTimeOffset)` (`0001-01-01T00:00:00`), since the Section-3-proposed record
   shape has no such field and nothing was yet setting it. This was a real gap, not a copy/paste error:
   `FileAestheticTrialCollector.RecordAsync` was updated to stamp `RecordedUtc = DateTimeOffset.UtcNow`
   on every rating at the moment of persistence (via `rating with { RecordedUtc = ... }`), overwriting
   whatever the caller supplied — re-running the full suite afterward passed 23/23 (§3.2). Recorded here
   per this project's own "do not claim tests ran when they did not" rule and as a disclosed,
   genuine architecture decision (§6 item 1) rather than silently fixed with no trace.
4. **No new `ViewModel`/Avalonia view (disclosed, mirrors Step 098's own precedent, §4).** This step's
   own required acceptance ("Collect preference ratings and editing-time results, not only auto scores")
   is a property of the real, persisted, independently re-verifiable rating store and honest summary
   output, not of a new interactive screen.
5. **`IReleaseGate`/`ReleaseReadiness` (Section 3's remaining proposed signature) was not implemented
   here, by design.** It belongs to Step 100's own numbered scope (the final release gate decision), not
   Step 099's ("Conduct A/B musicality evaluations..."); implementing it now would be scope creep ahead
   of Step 100's own acceptance gate, which this project's own phase-spec instructions explicitly forbid.
6. **Real human beta-tester ratings were not obtainable in this sandboxed environment (disclosed, §4).**
   The acceptance test's six ratings are deterministic, hand-authored fixture values, explicitly
   disclosed as such rather than presented as genuine community feedback — Phase 16 §1's own explicit
   boundary: "no simulated community feedback passed off as real test results."

## 7. Known risks / unresolved issues

- **No real human reviewer pool exists in this sandboxed environment** (§4/§6 item 6) — the acceptance
  test's rating values are disclosed fixtures proving the real collection/aggregation pipeline, not a
  genuine crowd study. A real release gate decision (Step 100) must not treat this step's own fixture
  ratings as evidence of actual community preference; a real blind-tasting session with real reviewers
  against real rendered candidates is required before any public claim of "testers preferred X."
- **Concurrency is serialized per-process via a `SemaphoreSlim`, not cross-process file locking.** Two
  separate OS processes writing to the same `aesthetic-trials.json` concurrently could still race at the
  `File.Move` step; this is acceptable for this step's own single-desktop-app scope (consistent with
  `RecentProjectsStore`'s own same-process assumption) but is not a general multi-process-safe store.
- **No installer/UI/xLights-rendering flow was exercised by this step** — Step 099's own numbered scope
  is A/B rating collection and manual-effort measurement, not installer behavior (Phase 15's own scope)
  or a new review screen; out of scope here by design (§6 item 4).

## 8. Required final checklist (Step 099 only)

- [x] Implementation / tests / observable demo (automated, real production types, no mocking framework;
      real blind-trial randomization, real atomic persistence, real reload-and-reverify, real honest
      aggregation all actually executed — §3.2/§3.4) / evidence all complete.
- [x] Compiles (`dotnet build XlightsAI.Studio.slnx`: 0 warnings, 0 errors).
- [x] New-test run: 23/23 passed (`XlightsAI.Studio.Release.Tests`, 8 pre-existing Step 098 + 15 new
      Step 099), including the required `Step099_Acceptance_Passes_...` test and multiple negative-path
      tests covering invalid input, cancellation, and stale/corrupt persisted state.
- [x] Full existing regression suite re-run with `--filter "Category!=Integration"`; every project
      passes with 0 failures; no project this step did not touch regressed (§3.3).
- [x] "Manual" demonstration performed against real, disclosed fixture data and the real persistence/
      aggregation pipeline (§3.4, §4); no new Avalonia screen was required by this step's own acceptance
      text (§4); a real human blind-tasting session was not available in this environment and is
      explicitly disclosed as not attempted, never faked (§4/§6 item 6/§7).
- [x] No previously accepted source file from PHASE-01 through PHASE-15, nor Step 098's own files, was
      modified — only new files added to `src/XlightsAI.Studio.Release/` and
      `tests/XlightsAI.Studio.Release.Tests/Step099AcceptanceTests.cs`.
- [x] Missing assumptions / compatibility limits / technical debt recorded (§6, §7).
- [x] Previous accepted sequences/project files are unchanged — this step never writes to any existing
      repository path at runtime; all real rating-store output is written under fresh per-test temp
      directories, cleaned up by each test's own `Dispose()`. `git status --short` after this step's
      work shows no modified path outside the new files this step itself introduces.
- [ ] Required xLights/manual/inference test ran against a recorded version, if applicable — **not
      applicable to this step's own required acceptance text** (no xLights-rendering claim is made by
      "Collect preference ratings and editing-time results, not only auto scores"); recorded as N/A for
      a rendering check specifically, consistent with the project's own standing waiver.

**Gate for Step 099: PASS.** This step's own numbered-step scope — collect real human A/B preference
ratings and real editing-time/manual-effort measurements across varied songs, with genuinely randomized
blind presentation ordering and honestly-reported (never padded or cherry-picked) sample sizes — is
fully implemented and tested: a new `BlindTrialOrderingService` genuinely randomizes candidate
presentation order, a new `FileAestheticTrialCollector` durably and atomically persists validated
ratings with real timestamp provenance (never silently corrupting or discarding already-accepted data
on a later invalid write, cancellation, or pre-existing corrupt store), and a new
`AestheticTrialSummaryReporter` honestly aggregates every candidate's real sample size and both
preference and manual-effort dimensions. The one honest gap — no real human beta-tester pool was
available in this sandboxed environment — is disclosed, not hidden, and the fixture ratings standing in
for it are labeled as fixtures throughout this document, never presented as real community feedback. No
PHASE-01 through PHASE-16-Step-098 file was modified. The next step (100) should not begin until this
PASS is accepted.
