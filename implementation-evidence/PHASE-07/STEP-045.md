# PHASE 07 — Step 045 Evidence

**Step:** "Define pitch/note/onset and relative melodic gesture contracts, with source/confidence"
**Spec:** `docs/phase-specs/PHASE-07.md` §6, Step 045 (lines 194-208) — the first numbered step of Phase 07 (045-051)
**Date:** 2026-10-12

## 1. Pre-implementation baseline inspection

- Read `docs/phase-specs/PHASE-07.md` in full before writing any code: Section 1 (user outcome/phase
  gate), Section 2 (restated architectural assumptions — canonical tick timebase, half-open
  `[start,end)` windows, cache-key versioning, non-destructive editing), Section 3 (the phase's proposed
  `NoteEvent`/`PitchMotion`/`MelodicGesture`/`MelodyTrack`/interface signatures and architectural design
  notes — "build the gesture mapping from relative motion," "Represent rests explicitly," "Note
  start/end must follow actual source time, not even subdivisions"), Step 045's own required acceptance
  ("Fixture JSON round-trips with units and source IDs"), its own implementation guidance ("Define
  note/timing provenance, integer MIDI pitch, velocity and confidence with explicit units; normalize
  overlaps and invalid duration"), its own failure/edge case ("Silence, invalid timing boundaries and
  missing media must be handled explicitly"), and Section 12's worked example (`C4,D4,E4,G4,E4,D4,C4`
  with a declared pitch-to-index mapping), which this step's own fixture/demo data reuses verbatim.
- Confirmed Phase 07 **depends on Phase 06** (Section 1: "Depends on: Phase 06: Instrument Separation
  and Activity Detection"). Read `implementation-evidence/PHASE-06/STEP-044.md` §9 in full: Phase 06's
  own final gate status is **BLOCKED** — no production stem-separation model has ever been downloaded or
  run under a .NET host in this repository; only a synthetic ONNX fixture has been proven end-to-end.
  This is disclosed explicitly as an architecture risk in §3/§7 below, per the user's own instruction to
  "treat all preceding completed step contracts as immutable" and to "show architecture deviations" —
  it is not silently papered over, nor is it treated as blocking this step's own narrow, disclosed scope
  (see §3).
- Inspected the existing repository structure (`src/`, `tests/`, `XlightsAI.Studio.slnx`) and the most
  recently completed sibling feature area, `XlightsAI.Studio.Stems`, for established conventions this
  step must follow rather than invent: `StemAsset`/`StemManifest` (plain records, no Avalonia types),
  `StemActivityResult`/`StemActivityDetectionInvalidException`/`IStemActivityResultStore`/
  `JsonStemActivityResultStore` (the "pure validator throws a dedicated exception; a JSON store writes
  atomically via temp-file-then-move and reads back via a dedicated corrupt-file exception" pattern used
  by every Stems-era persisted artifact), and the project/csproj layout (`net10.0`, `Nullable` enabled,
  `WarningsAsErrors=Nullable`, `EnableNETAnalyzers`/`AnalysisLevel=latest`).
- Confirmed no `XlightsAI.Studio.Melody` project exists anywhere in the repository yet (`src/`, `tests/`)
  — this step creates it for the first time, per Section 3's own namespace (`XlightsAI.Studio.Melody`)
  and the stable domain-name table (`MelodyTrack`, `NoteEvent` — "First introduced: 07").
- Baseline regression before any Step 045 code existed, run fresh in this session (`dotnet test
  XlightsAI.Studio.slnx --nologo`): **562/562 passing** across 8 test assemblies — exactly Step 044's own
  final recorded count (`implementation-evidence/PHASE-06/STEP-044.md` §4), confirming the starting
  state matched the prior phase's own recorded evidence before any Step 045 line of code existed.
- Decision made before writing code: Section 3's proposed signatures for `NoteEvent`, `PitchMotion`,
  `MelodicGesture` and `MelodyTrack` are adopted **verbatim** (field names, types and order unchanged) —
  no compatibility migration is needed because nothing in the repository defines these types yet. The
  two proposed interfaces (`IMelodyAnalyzer`, `INoteTrackEditor`, `IMelodicGestureMapper`) are **not**
  implemented in this step: Step 045's own scope is explicitly "Define pitch/note/onset and relative
  melodic gesture contracts" (the data shapes and their validation), not the analyzer/editor/mapper
  behaviors those interfaces describe, which are Steps 046-050's own later scope. Implementing them now
  would mean fabricating behavior ahead of its own numbered step.

## 2. Scope actually implemented

### New project: `src/XlightsAI.Studio.Melody/`

- **`NoteEvent.cs`** — Section 3's proposed signature, verbatim: `Id`, `StartTicks`/`EndTicks` (canonical
  monotonic ticks, half-open), `MidiNote` (integer, never raw Hz), `Velocity`/`Confidence` (independent,
  each normalized `[0, 1]`), `UserCorrected` (so a human edit is distinguishable from the original
  transcription forever, per Section 4: "Store original transcription version and corrected MelodyTrack
  as separate revisions").
- **`PitchMotion.cs`** — Section 3's proposed enum, verbatim: `Repeat`, `StepUp`, `StepDown`, `LeapUp`,
  `LeapDown`, `Rest`, `Hold`. Deliberately relative, never an absolute pitch name (Section 3's own design
  note: "an octave error does not necessarily ruin a lighting phrase"). Classifying real contour into
  these values is Step 047's own later scope — this step only defines the fixed vocabulary.
- **`MelodicGesture.cs`** — Section 3's proposed signature, verbatim: `NoteId` (foreign key back to the
  exact `NoteEvent`), `Motion`, `IntervalSemitones` (signed musical distance, not note-index distance —
  Section 3: "measure musical interval not only note index"), `StartTicks`/`EndTicks`, `Confidence`.
- **`MelodyTrack.cs`** — Section 3's proposed signature, verbatim: `Id`, `SourceAssetHash` (provenance —
  Section 4: "source stem hash" is part of every cache key), `Notes`, `Gestures`.
- **`MelodyTrackInvalidException.cs`** — this step's own dedicated exception type for every named
  failure/edge case that is genuinely invalid input: a zero/negative-duration note, an unrecoverable
  (fully-contained) overlap, an out-of-range `MidiNote`/`Velocity`/`Confidence`, an empty
  `SourceAssetHash` ("missing media"), or a `MelodicGesture` whose `NoteId` does not reference any note
  in the same track.
- **`MelodyTrackValidator.cs`** — the real (no third-party dependency), pure, static validator, mirroring
  `XlightsAI.Studio.Stems.ModelFeasibilityEvaluator`'s own "pure, no file I/O, no ambient clock"
  precedent. Implements Step 045's own implementation guidance verbatim ("normalize overlaps and invalid
  duration") as **two distinct, explicitly named behaviors**: (1) a genuinely invalid note/track always
  throws `MelodyTrackInvalidException` — never silently repaired; (2) a merely *overlapping* pair of
  otherwise-valid notes is **normalized** (the earlier note's `EndTicks` is trimmed to the later note's
  `StartTicks`), not rejected, because two analyzer-produced note hypotheses overlapping in time is an
  expected, recoverable condition for a monophonic melody line. An empty `Notes`/`Gestures` list (full
  silence) validates cleanly — this step's own "Silence... must be handled explicitly" failure/edge case,
  satisfied as "explicitly valid, not an error," the same convention `StemActivityDetector` already
  established for a fully silent stem (empty `Activities` list).
- **`MelodyTrackCorruptException.cs`** / **`IMelodyTrackStore.cs`** / **`JsonMelodyTrackStore.cs`** —
  mirror `StemActivityResultCorruptException`/`IStemActivityResultStore`/`JsonStemActivityResultStore`
  exactly: atomic temp-file-then-move writes, a dedicated corrupt-file exception on a bad reload.
  `JsonMelodyTrackStore.SaveAsync` re-validates via `MelodyTrackValidator.Validate` defensively before
  writing, so an invalid track is never persisted to disk regardless of caller discipline (proven by
  `SaveAsync_InvalidTrack_ThrowsBeforeAnyFileIsWritten_NegativePath`).

### New test file

`tests/XlightsAI.Studio.Melody.Tests/Step045AcceptanceTests.cs` — 20 tests (full list in §4).

### Solution/project wiring

- `XlightsAI.Studio.slnx` — added `src/XlightsAI.Studio.Melody/XlightsAI.Studio.Melody.csproj` (under
  `/src/`) and `tests/XlightsAI.Studio.Melody.Tests/XlightsAI.Studio.Melody.Tests.csproj` (under
  `/tests/`).
- `src/XlightsAI.Studio.Melody/XlightsAI.Studio.Melody.csproj` — references only
  `XlightsAI.Studio.Contracts` (for the shared canonical identifiers table, Section 2; not actually
  consumed by name in this step's own types, since `NoteEvent`/`MelodyTrack` use plain `Guid`/`long`
  per Section 3's own proposed signature verbatim, but the reference is kept consistent with every other
  phase-local project's own established dependency on `Contracts`).
- `tests/XlightsAI.Studio.Melody.Tests/XlightsAI.Studio.Melody.Tests.csproj` — references the new
  `XlightsAI.Studio.Melody` project only; same xunit/coverlet package versions as every other test
  project in the solution.

**No changes to any existing `Contracts`, `Audio`, `Rhythm`, `Stems`, `Editing`, `Xlights`, `Projects`,
`Reference`, `Shell`, `App` type's public shape** — this step adds one wholly new, self-contained feature
area and touches no file outside `src/XlightsAI.Studio.Melody/`, `tests/XlightsAI.Studio.Melody.Tests/`,
and the solution file's two new `<Project>` entries.

## 3. Architecture deviations / decisions

- **Phase 06 (this phase's own declared dependency) remains BLOCKED at the phase-gate level, carried
  forward unresolved.** Disclosed at length in §1: no production stem-separation model has ever been
  proven to run end-to-end in this repository. Step 045's own scope (pure domain contracts for melody
  note/gesture data, with no dependency on any separated stem's *audio content*) does not itself require
  a working production separator to be implemented or tested — `MelodyTrack.SourceAssetHash` is a plain
  string identity in this step, never a live `StemAsset`/decoded audio read. Per the user's own explicit
  instruction for this session ("implement ONLY Step 45... treat all preceding completed step contracts
  as immutable"), this step proceeds on that basis rather than re-litigating Phase 06. This is flagged
  here, not hidden, because Section 8's own required final gate report says "Next phase begins only
  after PASS or an explicit scoped architecture waiver" — no such waiver is recorded in this session
  beyond the user's own direct instruction to implement this one step; the Phase 07 gate as a whole
  cannot be claimed PASS until that is resolved (see §8).
- **Neither `IMelodyAnalyzer`, `INoteTrackEditor`, nor `IMelodicGestureMapper` is implemented in this
  step**, despite appearing in Section 3's proposed-signatures block alongside the records this step does
  implement. Disclosed, deliberate: Step 045's own title and required acceptance describe only the data
  contracts ("Define pitch/note/onset and relative melodic gesture contracts... Fixture JSON round-trips
  with units and source IDs") — the three interfaces describe *behaviors* (analysis, user correction,
  gesture-to-effect mapping) that are Steps 046/049/050's own later, individually-gated scope. Defining
  them now with no real implementation would be exactly the "collecting multiple unverified tasks into
  one mega-commit" Section 6 warns against.
- **`MelodyTrackValidator.Validate` normalizes overlap by trimming the *earlier* note's `EndTicks`, never
  the later note's `StartTicks`.** Disclosed, deliberate: a note's `StartTicks` is its onset — the most
  reliable, least ambiguous timing boundary a pitch/onset detector produces — while its `EndTicks` (the
  note-off/decay boundary) is inherently the fuzzier estimate (Phase 07 Section "Failure handling":
  "note-off estimation" is a named known technical risk). Trimming the fuzzier boundary preserves the
  more reliable one on both sides of the overlap.
- **A fully-contained overlap (one note's `StartTicks` at or before another note's own `StartTicks`)
  throws, rather than silently discarding the contained note.** Disclosed, deliberate: trimming in that
  case would require either deleting a note outright or producing a zero/negative-length note — both are
  a materially different, more destructive operation than the "trim to avoid overlap" case Step 045's own
  "normalize overlaps" text names, and silently dropping a detected note is exactly the kind of
  fabricated/hidden data loss Section 2 forbids ("AI trust... validated structured proposals only");
  surfacing it as `MelodyTrackInvalidException` instead keeps the caller in control.
- **No `ShellScreen.Melody`/piano-roll UI wiring in this step.** Disclosed, consistent with Phase 06
  Step 038's and Step 044's own identical precedent for contract/research-shaped steps: this step's own
  shared phase-wide "user-visible outcome" boilerplate text ("Saved audio/analysis artifact with
  timestamp provenance plus a synchronized waveform/timeline demonstration") is satisfied by the real,
  persisted `MelodyTrack` JSON artifact and the real console timeline rendering in §5 below, not by new
  Avalonia UI surface — Step 049 ("Implement editable piano roll...") is the step that actually owns that
  UI.
- **`Velocity` is a normalized `[0, 1]` double, not raw MIDI `0-127`.** Disclosed, deliberate: this keeps
  the contract usable whether a note originates from real trusted MIDI (which does have a native 0-127
  velocity byte, to be mapped down in Step 048) or from pitch-tracked audio (which has no native
  "velocity byte" at all, only an onset-strength estimate) — a single normalized unit avoids two
  different velocity conventions existing in the same contract.

## 4. Commands actually executed

All run via the PowerShell tool — the sandboxed POSIX Bash tool in this session returned a bare exit
code 1 with no output for plain commands (`git status`, `mkdir -p`), the same pre-existing environment
limitation recorded in every prior step's own evidence (e.g. `implementation-evidence/PHASE-06/STEP-044.md`
§4); PowerShell was used for every command below without incident.

```
dotnet test XlightsAI.Studio.slnx --nologo                                                   # baseline, before any Step 045 code existed -> 562/562 passing

# New: src/XlightsAI.Studio.Melody/{NoteEvent.cs, PitchMotion.cs, MelodicGesture.cs, MelodyTrack.cs,
#      MelodyTrackInvalidException.cs, MelodyTrackValidator.cs, MelodyTrackCorruptException.cs,
#      IMelodyTrackStore.cs, JsonMelodyTrackStore.cs, XlightsAI.Studio.Melody.csproj}
# New: tests/XlightsAI.Studio.Melody.Tests/{Step045AcceptanceTests.cs, XlightsAI.Studio.Melody.Tests.csproj}
# Modified: XlightsAI.Studio.slnx (added both new <Project> entries)

dotnet build src\XlightsAI.Studio.Melody\XlightsAI.Studio.Melody.csproj --nologo                         # 0 Warnings, 0 Errors
dotnet build tests\XlightsAI.Studio.Melody.Tests\XlightsAI.Studio.Melody.Tests.csproj --nologo           # 0 Warnings, 0 Errors
dotnet test  tests\XlightsAI.Studio.Melody.Tests\XlightsAI.Studio.Melody.Tests.csproj --nologo --logger "console;verbosity=detailed"
  # 20/20 passed

dotnet build XlightsAI.Studio.slnx --nologo                                                  # full solution, after Step 045 implementation -> 0 Warnings, 0 Errors
dotnet test  XlightsAI.Studio.slnx --nologo                                                   # full regression, final (run twice, identical both times)

dotnet run --project <scratchpad>\xlai-step045-demo-proj\step045-demo.csproj --nologo          # real, non-test-harness manual demo (see §5)
```

### Build results

Full solution: `Build succeeded. 0 Warning(s). 0 Error(s).`

### Targeted test results (Step 045 tests only, real run)

```
Passed XlightsAI.Studio.Melody.Tests.Step045AcceptanceTests.Step045_Acceptance_Passes_FixtureJsonRoundTripsWithUnitsAndSourceIds [25 ms]
Passed XlightsAI.Studio.Melody.Tests.Step045AcceptanceTests.SilentTrack_EmptyNotesAndGestures_ValidatesWithoutError [5 ms]
Passed XlightsAI.Studio.Melody.Tests.Step045AcceptanceTests.SilentTrack_RoundTripsThroughStore [112 ms]
Passed XlightsAI.Studio.Melody.Tests.Step045AcceptanceTests.InvalidTimingBoundary_EndTicksNotAfterStartTicks_ThrowsMelodyTrackInvalidException_NegativePath [2 ms]
Passed XlightsAI.Studio.Melody.Tests.Step045AcceptanceTests.InvalidTimingBoundary_EndTicksBeforeStartTicks_ThrowsMelodyTrackInvalidException_NegativePath [2 ms]
Passed XlightsAI.Studio.Melody.Tests.Step045AcceptanceTests.MissingMedia_EmptySourceAssetHash_ThrowsMelodyTrackInvalidException_NegativePath [3 ms]
Passed XlightsAI.Studio.Melody.Tests.Step045AcceptanceTests.MissingMedia_WhitespaceSourceAssetHash_ThrowsMelodyTrackInvalidException_NegativePath [2 ms]
Passed XlightsAI.Studio.Melody.Tests.Step045AcceptanceTests.OutOfRangeMidiNote_ThrowsMelodyTrackInvalidException_NegativePath(midiNote: -1) [2 ms]
Passed XlightsAI.Studio.Melody.Tests.Step045AcceptanceTests.OutOfRangeMidiNote_ThrowsMelodyTrackInvalidException_NegativePath(midiNote: 128) [4 ms]
Passed XlightsAI.Studio.Melody.Tests.Step045AcceptanceTests.OutOfRangeVelocity_ThrowsMelodyTrackInvalidException_NegativePath(velocity: -0.1) [2 ms]
Passed XlightsAI.Studio.Melody.Tests.Step045AcceptanceTests.OutOfRangeVelocity_ThrowsMelodyTrackInvalidException_NegativePath(velocity: 1.1) [2 ms]
Passed XlightsAI.Studio.Melody.Tests.Step045AcceptanceTests.OutOfRangeConfidence_ThrowsMelodyTrackInvalidException_NegativePath(confidence: -0.1) [2 ms]
Passed XlightsAI.Studio.Melody.Tests.Step045AcceptanceTests.OutOfRangeConfidence_ThrowsMelodyTrackInvalidException_NegativePath(confidence: 1.1) [2 ms]
Passed XlightsAI.Studio.Melody.Tests.Step045AcceptanceTests.DanglingGestureNoteId_ThrowsMelodyTrackInvalidException_NegativePath [3 ms]
Passed XlightsAI.Studio.Melody.Tests.Step045AcceptanceTests.OverlappingNotes_AreNormalizedByTrimmingEarlierNoteEnd_NotRejected [16 ms]
Passed XlightsAI.Studio.Melody.Tests.Step045AcceptanceTests.FullyContainedOverlap_CannotBeNormalized_ThrowsMelodyTrackInvalidException_NegativePath [21 ms]
Passed XlightsAI.Studio.Melody.Tests.Step045AcceptanceTests.LoadAsync_MissingFile_ThrowsFileNotFoundException_NegativePath [3 ms]
Passed XlightsAI.Studio.Melody.Tests.Step045AcceptanceTests.LoadAsync_CorruptFile_ThrowsMelodyTrackCorruptException_NegativePath [16 ms]
Passed XlightsAI.Studio.Melody.Tests.Step045AcceptanceTests.SaveAsync_InvalidTrack_ThrowsBeforeAnyFileIsWritten_NegativePath [3 ms]
Passed XlightsAI.Studio.Melody.Tests.Step045AcceptanceTests.NoteEvent_DurationTicks_MatchesRealSourceTime_NotEvenSubdivision [2 ms]

Total tests: 20. Passed: 20, Failed: 0.
```

### Full regression (after implementation, final run — executed twice, identical both times)

```
Passed!  - Failed: 0, Passed:  20, Skipped: 0, Total:  20  - XlightsAI.Studio.Melody.Tests.dll
Passed!  - Failed: 0, Passed:  71, Skipped: 0, Total:  71  - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed:  91, Skipped: 0, Total:  91  - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed:  75, Skipped: 0, Total:  75  - XlightsAI.Studio.Stems.Tests.dll
Passed!  - Failed: 0, Passed:  25, Skipped: 0, Total:  25  - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed:  34, Skipped: 0, Total:  34  - XlightsAI.Studio.Audio.Tests.dll
Passed!  - Failed: 0, Passed: 153, Skipped: 0, Total: 153 - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed:  82, Skipped: 0, Total:  82  - XlightsAI.Studio.Rhythm.Tests.dll
Passed!  - Failed: 0, Passed:  31, Skipped: 0, Total:  31  - XlightsAI.Studio.Reference.Tests.dll
```

**582 total, 0 failed, 0 skipped** — up from the Step 044 baseline of 562 (+20 new
`Step045AcceptanceTests` in the brand-new `Melody.Tests` project). Every one of the 562 pre-existing
tests still passes unmodified, confirming no prior step's contract was broken.

## 5. Persisted-output inspection / observable demonstration for this step

A real, non-test-harness console program (not part of the repository, built under the session
scratchpad, deleted after this run) exercises the real `MelodyTrack`/`MelodyTrackValidator`/
`JsonMelodyTrackStore` directly — no test harness, no mocks:

```
dotnet run --project <scratchpad>\xlai-step045-demo-proj\step045-demo.csproj --nologo
```

Observed real output:

```
Demo root directory: C:\Users\kmoens.LAKECO\AppData\Local\Temp\xlai-step045-demo-3ff51ccd710245eda1e6581f91e83805

=== Scenario 1: real C4-D4-E4-G4-E4-D4-C4 melody (Section 12 worked example), saved as a real JSON artifact ===
Saved real MelodyTrack JSON artifact: C:\Users\kmoens.LAKECO\AppData\Local\Temp\xlai-step045-demo-3ff51ccd710245eda1e6581f91e83805\tracks\melody-track-6cd3be437b2c49f3971449778d3d23f2.json
Reloaded from disk matches saved track (units + source IDs round-trip): True

Timeline (synthesized waveform/timeline demonstration, as ticks -> seconds -> note/gesture):
  [ 0.000s -  0.400s] MIDI  60 (Rest    , interval   0 semitones, confidence 1.00)
  [ 0.420s -  0.790s] MIDI  62 (StepUp  , interval   2 semitones, confidence 0.90)
  [ 0.800s -  1.150s] MIDI  64 (StepUp  , interval   2 semitones, confidence 0.90)
  [ 1.160s -  1.600s] MIDI  67 (LeapUp  , interval   3 semitones, confidence 0.90)
  [ 1.610s -  1.900s] MIDI  64 (LeapDown, interval  -3 semitones, confidence 0.90)
  [ 1.910s -  2.200s] MIDI  62 (StepDown, interval  -2 semitones, confidence 0.90)
  [ 2.210s -  2.600s] MIDI  60 (StepDown, interval  -2 semitones, confidence 0.90)

=== Scenario 2: silence handled explicitly (empty track is valid, not an error) ===
Silent track saved and reloaded with 0 notes, 0 gestures (no crash, no error).

=== Scenario 3: invalid timing boundary rejected explicitly, before any file write ===
Correctly rejected: NoteEvent 9761d6b8-5fc3-46ad-9d70-d2306598b26a has invalid timing boundaries: EndTicks (5000) must be greater than StartTicks (10000).

=== Scenario 4: missing media (empty SourceAssetHash) rejected explicitly ===
Correctly rejected: MelodyTrack 6e1ce1bc-3083-4154-abe3-c5116eaf650f has no SourceAssetHash -- missing media.

=== Scenario 5: overlapping notes normalized (not rejected) ===
Note A trimmed from EndTicks=5000 to EndTicks=4500 to avoid overlapping Note B (StartTicks=4500).

Cleaning up demo directory.
Step 045 manual demo completed successfully.
```

Direct, real, non-test-harness confirmation: a real 7-note melody (Section 12's own worked example) with
its derived gestures is saved to and reloaded from a real on-disk JSON file, with every unit (ticks, MIDI
note number, confidence) and source ID (`MelodyTrack.Id`, `NoteEvent.Id`, `MelodicGesture.NoteId`)
round-tripping exactly (required acceptance, verbatim: "Fixture JSON round-trips with units and source
IDs"); silence, an invalid timing boundary, and missing media are each handled explicitly with no crash
(this step's own failure/edge case, verbatim); and an overlapping-but-otherwise-valid note pair is
normalized rather than rejected (this step's own implementation guidance, verbatim: "normalize
overlaps").

## 6. Fixtures / input hashes

No binary fixture file was added for this step — `MelodyTrack`/`NoteEvent`/`MelodicGesture` are plain,
self-contained JSON-serializable records; no audio decoding occurs in this step's own scope. The test
fixture is the literal, deterministic 7-note `C4,D4,E4,G4,E4,D4,C4` melody (`Step045AcceptanceTests.
Step045_Acceptance_Passes_FixtureJsonRoundTripsWithUnitsAndSourceIds`) at real, deliberately
non-evenly-subdivided tick onsets, matching Section 12's own worked example and its own declared
pitch-to-index direction (`1,2,3,5,3,2,1` over a 5-arch path — this step records the note/gesture data
only; Step 050 is the step that actually maps it onto a path). `SourceAssetHash` in every test/demo
fixture is a literal placeholder hex string (`"c3b9f1a5e7d2049b8a6f1c4d7e9b0a2f5c8d1e4b7a0f3c6d9e2b5a8f1c4d7e9b"`
/ `"deadbeef"`), not a real SHA-256 of an actual audio file — this step's own scope validates that the
field is present/non-empty (provenance-by-reference), it does not itself decode or verify audio content
(that cross-check is Steps 046/048's own later scope, the same division
`XlightsAI.Studio.Stems.StemActivityDetector` already established between "detect" and "verify the
source file still matches its declared hash"). All test-run and demo temp directories
(`%TEMP%\xlai-step045-*\`) are deleted by each test's own `IDisposable.Dispose()` / the demo's own
cleanup code (confirmed by "Cleaning up demo directory." / "Step 045 manual demo completed
successfully." in the real output above); the throwaway demo *project* itself (outside the repository,
under the session scratchpad) was deleted after this evidence was recorded.

## 7. Unresolved issues / known gaps

- **Phase 06 (this phase's declared dependency) remains BLOCKED** at the phase-gate level (§3) — no
  production stem-separation model exists anywhere in this repository. This step's own contracts do not
  themselves require one, but Steps 046/048 (real pitch tracking against an isolated stem; MIDI import)
  will need a real, decodable isolated-source audio file to analyze, which is unaffected by Phase 06's
  BLOCKED status only because a user can supply an already-isolated recording or trusted MIDI directly
  (Section 3: "Inputs: User-selected stem/source, trusted MIDI where present") — separated-stem input
  specifically remains blocked on Phase 06's own unresolved gap.
- **`IMelodyAnalyzer`, `INoteTrackEditor`, `IMelodicGestureMapper` are not implemented** — disclosed in
  §3 as deliberately out of this step's own narrow scope, deferred to Steps 046/049/050 respectively.
- **No piano-roll/`ShellScreen.Melody` UI wiring** — disclosed in §3, deferred to Step 049 (the step that
  owns the editable piano roll per the phase spec's own numbered-step assignment).
- **`SourceAssetHash` is not cross-checked against any real audio file in this step** — disclosed in §6;
  this step validates only that it is present (non-empty), not that it matches a real file's actual
  content hash.

## 8. PASS / BLOCKED

**Status: PASS** (for this step's own narrow, disclosed scope — the data contracts, their normalization/
validation rules, and their JSON persistence — not a claim that Phase 06's own separately-tracked,
carried-forward BLOCKED verdict is resolved).

- "Fixture JSON round-trips with units and source IDs" (required acceptance, verbatim) — proven by
  `Step045_Acceptance_Passes_FixtureJsonRoundTripsWithUnitsAndSourceIds` (every tick/MIDI-note/velocity/
  confidence unit and every `Id`/`NoteId` source identifier round-trips exactly through a real on-disk
  JSON file) and independently confirmed by the real demo's Scenario 1.
- "Silence, invalid timing boundaries and missing media must be handled explicitly" (failure/edge case,
  verbatim) — proven three ways: `SilentTrack_EmptyNotesAndGestures_ValidatesWithoutError` and
  `SilentTrack_RoundTripsThroughStore` (an empty track is explicitly valid, not an error);
  `InvalidTimingBoundary_EndTicksNotAfterStartTicks_ThrowsMelodyTrackInvalidException_NegativePath` and
  `InvalidTimingBoundary_EndTicksBeforeStartTicks_ThrowsMelodyTrackInvalidException_NegativePath`
  (invalid timing boundaries rejected explicitly); `MissingMedia_EmptySourceAssetHash_..._NegativePath`
  and `MissingMedia_WhitespaceSourceAssetHash_..._NegativePath` (missing media rejected explicitly). All
  three independently confirmed by the real demo's Scenarios 2-4.
- "normalize overlaps and invalid duration" (implementation guidance, verbatim) — proven by
  `OverlappingNotes_AreNormalizedByTrimmingEarlierNoteEnd_NotRejected` (a recoverable overlap is trimmed,
  not rejected) and `FullyContainedOverlap_CannotBeNormalized_ThrowsMelodyTrackInvalidException_NegativePath`
  (an unrecoverable overlap correctly throws instead of silently discarding data), independently
  confirmed by the real demo's Scenario 5.
- Additional rigor beyond the minimum: `OutOfRangeMidiNote_*`/`OutOfRangeVelocity_*`/
  `OutOfRangeConfidence_*` (every declared unit's explicit bound is actually enforced, not a rubber
  stamp); `DanglingGestureNoteId_ThrowsMelodyTrackInvalidException_NegativePath` (a gesture can never
  reference a note that does not exist in its own track); `LoadAsync_MissingFile_*`/
  `LoadAsync_CorruptFile_*`/`SaveAsync_InvalidTrack_ThrowsBeforeAnyFileIsWritten_*` (the persistence
  boundary's own negative paths, mirroring every prior phase's established store-testing convention);
  `NoteEvent_DurationTicks_MatchesRealSourceTime_NotEvenSubdivision` (Section 3's own "not even
  subdivisions" design note is asserted directly against the fixture's deliberately uneven onsets).
- Full regression: **582/582 passing**, 0 failed, 0 skipped (run twice, identical both times); every
  pre-existing test from Steps 001-044 still passes unmodified.
- **Carried-forward, disclosed phase-level/dependency-level gap (not this step's own blocker):** Phase
  06's own gate remains BLOCKED (§3/§7) — no production stem-separation model exists in this repository.
  This does not block Step 045's own required acceptance text, which this step satisfies in full. Per
  the user's own standing waiver (`memory: feedback_skip_xlights_human_verification`), the xLights human-
  render check is not treated as a blocker for this or any other step, and is not fabricated here; no
  xLights-specific manual verification applies to this step in any case (it does not touch the
  `.xsq`/`xlights_rgbeffects.xml` pathway). Per Section 8's own instruction ("Do not start the next phase
  when blocked"), **Step 046 should not begin against a silent assumption that this evidence constitutes
  a Phase 07 gate PASS** — only Step 045's own narrow scope is proven here; the phase-level gate (Section
  7's full-phase test matrix, the phase gate demonstration, and Section 8's required final gate report)
  remains open until Steps 046-051 are each independently implemented and evidenced in turn.
