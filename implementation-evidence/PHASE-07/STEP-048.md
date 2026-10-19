# PHASE 07 — Step 048 Evidence

**Step:** "Support MIDI import and evaluate transcription backend behind an interface"
**Spec:** `docs/phase-specs/PHASE-07.md` §6, Step 048 (lines 242-256) — the fourth numbered step of Phase 07 (045-051)
**Date:** 2026-10-12

## 1. Pre-implementation baseline inspection

- Read `docs/phase-specs/PHASE-07.md` in full before writing any code: required acceptance ("Imported MIDI
  note timings map to canonical event structure"), implementation guidance ("Parse MIDI note-on/note-off
  and tempo changes; create adapter to same track; use MIDI as ground-truth alternative where present"),
  failure/edge case ("Invalid input, cancellation or stale persisted state must not corrupt the project" —
  verbatim-identical text to Step 046), and Section 3's architectural design note ("trustworthy MIDI ->
  high-confidence isolated-stem note events -> pitch contour direction and onset -> rhythm-only events").
- Read `implementation-evidence/PHASE-07/STEP-045.md`, `STEP-046.md`, and `STEP-047.md` in full per the
  user's own instruction to "treat all preceding completed step contracts as immutable unless the current
  step has an accepted migration plan." Steps 045's `NoteEvent`/`MelodyTrack`/`MelodyTrackValidator`/
  `MelodyTrackInvalidException`, 046's `PitchTracker`/`PitchTrackAnalysis`, and 047's
  `MelodicContourClassifier`/`GestureClassificationSettings` are all **untouched** by this step — no
  migration was needed or made.
- Inspected every existing file in `src/XlightsAI.Studio.Melody/` (`NoteEvent.cs`, `MelodyTrack.cs`,
  `MelodyTrackValidator.cs`, `MelodyTrackInvalidException.cs`, `MelodicGesture.cs`, `PitchMotion.cs`,
  `GestureClassificationSettings.cs`, `MelodicContourClassifier.cs`, `IMelodyTrackStore.cs`,
  `JsonMelodyTrackStore.cs`) to confirm this step's own importer could be built as a pure adapter *on top
  of* these already-committed contracts, reusing `MelodyTrackValidator.Validate` and
  `MelodicContourClassifier.Classify` rather than re-implementing timing/unit validation or gesture
  classification a second time.
- Searched the whole repository for any existing MIDI-reading dependency or NuGet `PackageReference`
  (`grep -r "PackageReference"`, `grep -r "Midi|DryWetMidi|NAudio"`): **zero** NuGet package references
  exist anywhere in the solution (every `.csproj` only has `ProjectReference`s), and no MIDI library of any
  kind is referenced. This is a disclosed architecture decision (§3 below): this step implements a minimal,
  dependency-free Standard MIDI File (SMF) reader rather than adding a new third-party dependency.
- Baseline regression before any Step 048 code existed, run fresh in this session
  (`dotnet test XlightsAI.Studio.slnx --nologo`): **617/617 passing** across 9 test assemblies, exactly
  Step 047's own final recorded count, confirming the starting state matched the prior step's own recorded
  evidence before any Step 048 line of code existed.

## 2. Scope actually implemented

### New files in `src/XlightsAI.Studio.Melody/`

- **`MidiImportSettings.cs`** — `MidiImportSettings(int? MelodyChannel, double MinNoteConfidence)`. A
  Standard MIDI File commonly interleaves several channels (melody, bass, drums); this step's own scope is
  a single monophonic melody line (Section 3: polyphony is Step 051's later scope), so the caller may pin
  an explicit 0-based channel, or leave it `null` to auto-select. `MinNoteConfidence` defaults to `1.0`
  ("use MIDI as ground-truth alternative," implementation guidance verbatim) but is exposed, not hard-coded,
  for a future caller blending trust levels.
- **`MidiImportInvalidException.cs`** — this step's own named "Invalid input" failure/edge case: malformed
  chunk structure, unsupported SMPTE time division, truncated track chunk, invalid settings, an unmatched
  note-on/note-off pair, or a selected channel with zero note events. Mirrors
  `MelodyTrackInvalidException`'s own "rejected before any persistence" precedent.
- **`StandardMidiFileReader.cs`** (internal) — a minimal, dependency-free SMF chunk/event binary parser:
  reads the `MThd` header (format 0/1 only; rejects SMPTE/format-2 division explicitly), each `MTrk` chunk's
  variable-length-quantity delta-times, running status, note-on/note-off channel-voice events, and `FF 51
  03` Set Tempo meta events. Returns an unconverted `ParsedMidiFile` (raw MIDI-tick positions plus the
  file's own ticks-per-quarter-note division) — tempo-map conversion to canonical ticks is a separate,
  later step (next bullet), so a parse failure is always caught before any canonical-tick arithmetic runs.
- **`IMidiTranscriptionBackend.cs`** — this step's own title, verbatim: "evaluate transcription backend
  behind an interface." `Task<MelodyTrack> ImportAsync(byte[] midiData, string sourceAssetHash,
  MidiImportSettings settings, CancellationToken ct)`. Disclosed, deliberate naming/scope decision: see §3.
- **`StandardMidiFileTranscriptionBackend.cs`** — the real, single implementation of
  `IMidiTranscriptionBackend`, "evaluated" (exercised against real/synthetic SMF fixtures; see
  `Step048AcceptanceTests`) in this step. Pipeline: `StandardMidiFileReader.Parse` -> select one melody
  channel (explicit `MidiImportSettings.MelodyChannel`, or auto-select the channel with the most note-on
  events excluding General MIDI percussion channel 9) -> pair note-on/note-off events per MIDI pitch (a
  `Queue<MidiRawNoteEvent>` per pitch number, FIFO-matched) -> convert each paired event's raw MIDI ticks to
  canonical 100 ns ticks via a private `MidiTempoMap` (a piecewise-linear tempo map built from every Set
  Tempo meta event found anywhere in the file, defaulting to 500,000 microseconds/quarter note — 120 BPM,
  the MIDI specification's own default — until the first explicit tempo event, if any) ->
  `MelodyTrackValidator.Validate` -> `MelodicContourClassifier.Classify` with
  `GestureClassificationSettings.Default`. The result is a complete, gesture-classified `MelodyTrack` —
  "create adapter to same track" (implementation guidance, verbatim): the exact same canonical shape a
  pitch-tracking-derived track already has.

### New test files

- `tests/XlightsAI.Studio.Melody.Tests/MidiFixtureBuilder.cs` — a hand-rolled SMF byte builder (format 0,
  single track) for constructing real, valid (and deliberately invalid) `.mid` byte fixtures directly from
  the same documented chunk/event grammar `StandardMidiFileReader` consumes — the same "construct the real
  binary fixture, not a mock" precedent `DeterministicSyntheticAudioGenerator` already established for WAV
  fixtures in Step 046.
- `tests/XlightsAI.Studio.Melody.Tests/Step048AcceptanceTests.cs` — 19 tests (full list in §4), including
  the required `Step048_Acceptance_Passes_ImportedMidiNoteTimingsMapToCanonicalEventStructure` test.

### Project/solution wiring

No `.csproj`/`.slnx` changes were needed — the five new `src` files depend only on types already inside
`XlightsAI.Studio.Melody` (no new `ProjectReference`, no new NuGet `PackageReference`), and the test project
already references `XlightsAI.Studio.Melody`.

**No changes to any Step 045/046/047 file.** This step adds five new, self-contained source files and two
new test files only.

## 3. Architecture deviations / decisions

- **No MIDI-parsing NuGet dependency added.** Disclosed, deliberate: the repository has zero existing NuGet
  `PackageReference`s anywhere (confirmed by repository-wide search, §1); adding one here for a well-defined
  ~200-line binary chunk grammar the project can fully own, test and audit was judged not worth the new
  external dependency surface, consistent with Section 2's own "no mandatory... subscription" posture (not
  a license requirement here, but the same minimal-dependency preference). If a richer MIDI feature set
  (SMPTE timecode, System Exclusive content inspection, MIDI *file writing* for export) is needed in a later
  phase, that is an explicit, disclosed future decision, not assumed here.
- **`IMidiTranscriptionBackend` is a new interface, not Section 3's own proposed `IMelodyAnalyzer`.**
  Disclosed, deliberate: `IMelodyAnalyzer.AnalyzeAsync` takes a `StemAsset` (audio) source; a trusted MIDI
  file is not audio and has no `StemAsset` identity. This step's own title ("evaluate transcription backend
  behind an interface") is satisfied by a narrower, phase-local sibling interface for Section 3's own
  "trustworthy MIDI" rung of its fallback ladder, mirroring the same "step's own narrower concrete
  precursor to a later proposed interface" relationship Steps 046/047 already established for
  `PitchTracker`/`MelodicContourClassifier` relative to `IMelodyAnalyzer`/`IMelodicGestureMapper`. Only one
  concrete implementation (`StandardMidiFileTranscriptionBackend`) exists; "evaluate... behind an
  interface" is satisfied by exercising that one real implementation against real/synthetic SMF fixtures
  (`Step048AcceptanceTests`), not by building a runtime multi-backend selection mechanism nothing in this
  step's own text actually asks for.
- **Monophonic melody-channel selection, not full polyphonic import.** Disclosed, consistent with Section 3
  ("polyphony... is Step 051's own later scope"): `MidiImportSettings.MelodyChannel` selects exactly one
  0-based MIDI channel (explicitly, or auto-selected by note-on count, excluding General MIDI percussion
  channel 9); multiple simultaneous notes on the *same* selected channel are paired FIFO per pitch (a
  reasonable, disclosed interpretation for a monophonic line that may still have slightly overlapping
  adjacent-note hypotheses, trimmed by the already-existing `MelodyTrackValidator.NormalizeOverlaps`), but
  true multi-voice polyphony reconstruction is intentionally out of scope here.
- **An unmatched note-on or note-off is a hard rejection (`MidiImportInvalidException`), never silently
  dropped or auto-closed at end-of-track.** Disclosed, deliberate: this step's own failure/edge case,
  verbatim, is "Invalid input... must be handled explicitly" — an SMF with a hanging note-on (missing its
  note-off) or a stray note-off with no matching note-on is a malformed/unsupported input for *this step's*
  own narrow scope, not evidence to guess from.
- **The imported `MelodyTrack` is always returned with `Gestures` already populated** (via
  `MelodicContourClassifier.Classify`), not a bare `Notes`-only track. Disclosed, deliberate: "create
  adapter to same track" (implementation guidance, verbatim) is read here as "the same *complete* canonical
  structure," so a MIDI-sourced track is a true drop-in replacement for a pitch-tracking-derived one
  anywhere downstream (Steps 049/050) expects a fully classified `MelodyTrack`.
- **No MIDI *file-writing*/export, no piano-roll UI wiring, no `IMelodyAnalyzer`/audio-pitch-tracking
  integration in this step.** Disclosed, consistent with Steps 045/046/047's own identical precedent: this
  step's own shared phase-wide "user-visible outcome" boilerplate text is satisfied by the real, persisted,
  classified `MelodyTrack` JSON artifact and the real console timeline in §5 — Steps 049/050 own that later
  scope.
- **Phase 06 (this phase's own declared dependency) remains BLOCKED at the phase-gate level**, carried
  forward unresolved from Steps 045/046/047 (unchanged by this step). This step's own importer has no
  stem/audio input of its own at all (it consumes raw MIDI bytes directly), so it does not depend on, or
  attempt to resolve, Phase 06's own separately-tracked production stem-separation gap.

## 4. Commands actually executed

All run via the PowerShell tool — the sandboxed POSIX Bash tool in this session again returned a bare exit
code 1 with no output for plain commands, the same pre-existing environment limitation recorded in every
prior step's own evidence; PowerShell was used for every command below without incident.

```
dotnet test XlightsAI.Studio.slnx --nologo                                                   # baseline, before any Step 048 code existed -> 617/617 passing

# New: src/XlightsAI.Studio.Melody/{MidiImportSettings.cs, MidiImportInvalidException.cs,
#      StandardMidiFileReader.cs, IMidiTranscriptionBackend.cs, StandardMidiFileTranscriptionBackend.cs}
# New: tests/XlightsAI.Studio.Melody.Tests/{MidiFixtureBuilder.cs, Step048AcceptanceTests.cs}
# No .csproj / .slnx changes required.

dotnet build src\XlightsAI.Studio.Melody\XlightsAI.Studio.Melody.csproj --nologo              # 0 Warnings, 0 Errors

dotnet test  tests\XlightsAI.Studio.Melody.Tests\XlightsAI.Studio.Melody.Tests.csproj --nologo --logger "console;verbosity=detailed"
  # first run: 1 failure (ImportAsync_UnmatchedNoteOff_... -- the fixture had no note-on anywhere, so
  # channel auto-selection itself failed first with a different, but still correct, MidiImportInvalidException
  # message than the test asserted). Fixed by adding a valid note-on/off pair on channel 0 to the fixture so
  # channel 0 auto-selects, then an unmatched note-off for a different pitch on that same channel.
  # second run: 74/74 passed (55 pre-existing Step 045/046/047 tests + 19 new Step 048 tests)

dotnet build XlightsAI.Studio.slnx --nologo                                                   # full solution, after Step 048 implementation -> 0 Warnings, 0 Errors
dotnet test  XlightsAI.Studio.slnx --nologo                                                    # full regression, final

dotnet run --project <scratchpad>\xlai-step048-demo-proj --nologo                              # real, non-test-harness manual demo (see §5)
```

### Build results

Full solution: `Build succeeded. 0 Warning(s). 0 Error(s).`

### Targeted test results (Step 048 tests only, real run, after the one fixture fix)

```
Passed Step048AcceptanceTests.Step048_Acceptance_Passes_ImportedMidiNoteTimingsMapToCanonicalEventStructure [10 ms]
Passed Step048AcceptanceTests.ImportAsync_AutoSelectsChannelWithMostNotes_SkippingPercussionChannel9 [5 ms]
Passed Step048AcceptanceTests.ImportAsync_ExplicitMelodyChannel_SelectsThatChannelEvenIfNotTheMajority [5 ms]
Passed Step048AcceptanceTests.ImportAsync_InvalidHeaderMagic_ThrowsMidiImportInvalidException_InvalidInputNegativePath [4 ms]
Passed Step048AcceptanceTests.ImportAsync_SmpteTimeDivision_ThrowsMidiImportInvalidException_InvalidInputNegativePath [2 ms]
Passed Step048AcceptanceTests.ImportAsync_TruncatedTrackChunk_ThrowsMidiImportInvalidException_InvalidInputNegativePath [4 ms]
Passed Step048AcceptanceTests.ImportAsync_UnmatchedNoteOn_ThrowsMidiImportInvalidException_InvalidInputNegativePath [3 ms]
Passed Step048AcceptanceTests.ImportAsync_UnmatchedNoteOff_ThrowsMidiImportInvalidException_InvalidInputNegativePath [< 1 ms]
Passed Step048AcceptanceTests.ImportAsync_NoNoteEventsAtAll_ThrowsMidiImportInvalidException_InvalidInputNegativePath [4 ms]
Passed Step048AcceptanceTests.ImportAsync_RequestedChannelHasNoNotes_ThrowsMidiImportInvalidException_InvalidInputNegativePath [17 ms]
Passed Step048AcceptanceTests.ImportAsync_MelodyChannelOutOfRange_ThrowsMidiImportInvalidException_InvalidInputNegativePath(channel: -1) [3 ms]
Passed Step048AcceptanceTests.ImportAsync_MelodyChannelOutOfRange_ThrowsMidiImportInvalidException_InvalidInputNegativePath(channel: 16) [6 ms]
Passed Step048AcceptanceTests.ImportAsync_MinNoteConfidenceOutOfRange_ThrowsMidiImportInvalidException_InvalidInputNegativePath(confidence: -0.1) [5 ms]
Passed Step048AcceptanceTests.ImportAsync_MinNoteConfidenceOutOfRange_ThrowsMidiImportInvalidException_InvalidInputNegativePath(confidence: 1.1) [2 ms]
Passed Step048AcceptanceTests.ImportAsync_EmptySourceAssetHash_ThrowsMidiImportInvalidException_InvalidInputNegativePath [3 ms]
Passed Step048AcceptanceTests.ImportAsync_CancelledBeforeParsing_ThrowsWithoutProducingATrack_CancellationNegativePath [30 ms]
Passed Step048AcceptanceTests.ImportAsync_InvalidReimport_DoesNotCorruptPreviouslyPersistedTrack_StalePersistedStateNegativePath [120 ms]
Passed Step048AcceptanceTests.ImportAsync_NullMidiData_ThrowsArgumentNullException_NegativePath [4 ms]
Passed Step048AcceptanceTests.ImportAsync_NullSettings_ThrowsArgumentNullException_NegativePath [3 ms]

Total: 74. Passed: 74, Failed: 0. (55 pre-existing Step 045/046/047 + 19 new Step 048)
```

### Full regression (after implementation, final run)

```
Passed!  - Failed: 0, Passed:  71, Skipped: 0, Total:  71  - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed:  91, Skipped: 0, Total:  91  - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed:  75, Skipped: 0, Total:  75  - XlightsAI.Studio.Stems.Tests.dll
Passed!  - Failed: 0, Passed:  74, Skipped: 0, Total:  74  - XlightsAI.Studio.Melody.Tests.dll
Passed!  - Failed: 0, Passed:  25, Skipped: 0, Total:  25  - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed:  34, Skipped: 0, Total:  34  - XlightsAI.Studio.Audio.Tests.dll
Passed!  - Failed: 0, Passed: 153, Skipped: 0, Total: 153 - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed:  82, Skipped: 0, Total:  82  - XlightsAI.Studio.Rhythm.Tests.dll
Passed!  - Failed: 0, Passed:  31, Skipped: 0, Total:  31  - XlightsAI.Studio.Reference.Tests.dll
```

**636 total, 0 failed, 0 skipped** — up from the Step 047 baseline of 617 (+19 new
`Step048AcceptanceTests` in the already-existing `Melody.Tests` project). Every one of the 617 pre-existing
tests still passes unmodified, confirming no prior step's contract was broken.

## 5. Persisted-output inspection / observable demonstration for this step

A real, non-test-harness console program (not part of the repository, built under the session scratchpad,
deleted after this run) exercises the real `StandardMidiFileTranscriptionBackend`/`JsonMelodyTrackStore`
directly against a real, hand-built `.mid` byte file (also written to disk for inspection) — no test
harness, no mocks:

```
dotnet run --project <scratchpad>\xlai-step048-demo-proj --nologo
```

Observed real output:

```
Demo root directory: C:\Users\kmoens.LAKECO\AppData\Local\Temp\xlai-step048-demo-8a8e360203f74562baa411325fd45405

=== Scenario 1: real Standard MIDI File bytes (C4-D4-E4-G4-E4-D4-C4, with a real tempo change) imported, classified and persisted as a real JSON MelodyTrack artifact ===
Saved real imported+classified MelodyTrack JSON artifact: C:\Users\kmoens.LAKECO\AppData\Local\Temp\xlai-step048-demo-8a8e360203f74562baa411325fd45405\tracks\melody-track-d9cdb232c5f44728b64f6db1ff6d9ab2.json
Reloaded from disk matches saved import (units + source IDs round-trip): True

Timeline (imported MIDI note timings mapped to canonical event structure, as canonical ticks -> MIDI note -> motion):
  [ 0.000s] MIDI note  60  velocity=0.787  confidence=1.0  motion=Rest      interval=  0 semitones
  [ 0.500s] MIDI note  62  velocity=0.787  confidence=1.0  motion=StepUp    interval= +2 semitones
  [ 1.000s] MIDI note  64  velocity=0.787  confidence=1.0  motion=StepUp    interval= +2 semitones
  [ 1.500s] MIDI note  67  velocity=0.787  confidence=1.0  motion=LeapUp    interval= +3 semitones
  [ 2.000s] MIDI note  64  velocity=0.787  confidence=1.0  motion=LeapDown  interval= -3 semitones
  [ 3.000s] MIDI note  62  velocity=0.787  confidence=1.0  motion=StepDown  interval= -2 semitones
  [ 4.000s] MIDI note  60  velocity=0.787  confidence=1.0  motion=StepDown  interval= -2 semitones

=== Scenario 2: auto-selecting the melody channel, skipping General MIDI percussion channel 9 ===
Auto-selected melody channel produced 2 notes (percussion channel's own 3 hits correctly excluded): 60, 62

=== Scenario 3: invalid MIDI input rejected explicitly, before any file write ===
Correctly rejected: Expected 'MThd' chunk id at byte offset 0, found other data.

=== Scenario 4: stale persisted state -- the earlier valid save above is untouched by the rejected re-import ===
Previously saved file unchanged after the rejected re-import attempt: True

=== Scenario 5: cancellation propagated before any note is produced ===
Correctly threw OperationCanceledException for an already-cancelled token.

Step 048 manual demo completed successfully.

Cleaning up demo directory.
```

Direct, real, non-test-harness confirmation: a real 7-note `.mid` byte file with an explicit mid-phrase
tempo change (500,000 -> 1,000,000 microseconds/quarter note at the tick-1920 boundary) is parsed, its
note-on/note-off events are paired and tempo-converted, and the resulting `MelodyTrack` is saved to and
reloaded from a real on-disk JSON file (required acceptance, verbatim: "Imported MIDI note timings map to
canonical event structure" — note timing correctly reflects the real tempo change: the first four notes are
each exactly 0.5 s apart, the remaining three are each exactly 1.0 s apart); channel auto-selection
correctly skips General MIDI percussion channel 9's own 3 hits (implementation guidance's own ground-truth
framing: only real melodic channel content becomes the imported track); an invalid MIDI header is rejected
explicitly before any file write (this step's own failure/edge case, verbatim: "Invalid input... must be
handled explicitly"); a previously saved track is provably untouched by a subsequent failed re-import
("stale persisted state must not corrupt the project"); and an already-cancelled token is honored before any
note is produced ("cancellation... must not corrupt the project").

## 6. Fixtures / input hashes

No binary fixture *file* is committed to the repository for this step — every `.mid` byte fixture is
constructed in-memory at test/demo run time via `MidiFixtureBuilder`/the demo's own equivalent inline
builder (the same approach Steps 045/047 already use for their own in-line `NoteEvent` fixtures, and the
same binary-byte-builder approach `DeterministicSyntheticAudioGenerator` already uses for WAV fixtures in
Step 046). `SourceAssetHash` in the demo is a real SHA-256 digest of the real constructed `.mid` bytes
(`Sha256Of(midiBytes)`), not a placeholder; in the unit tests it is the literal string `"deadbeef"`
(matching Steps 045/046/047's own already-committed test convention for a placeholder hash in a context
where the real audio/MIDI file identity is irrelevant to the behavior under test). The demo's own `.mid`
byte file is written to its own temporary directory for inspection
(`%TEMP%\xlai-step048-demo-*\worked-example.mid`) and deleted, along with the rest of that directory, by the
demo's own `finally` block (confirmed by "Cleaning up demo directory." in the real output above); the
throwaway demo *project* itself (outside the repository, under the session scratchpad) was deleted after
this evidence was recorded.

## 7. Unresolved issues / known gaps

- **Phase 06 (this phase's declared dependency) remains BLOCKED** at the phase-gate level, carried forward
  unresolved from Steps 045/046/047 (§3) — no production stem-separation model exists anywhere in this
  repository. This step's importer has no stem/audio input of its own and does not touch this gap either
  way.
- **No MIDI file *export*/writing.** This step only reads/imports a trusted MIDI file as a ground-truth
  `MelodyTrack` source; producing a `.mid` file from a `MelodyTrack` (e.g. for a user to re-export their
  correction) is not implemented and not required by this step's own text.
- **No SMPTE time-division support, no System Exclusive content interpretation, no MIDI format 2.**
  Disclosed (§3): these are explicitly rejected as unsupported/invalid input rather than silently
  mishandled; adding support for them is an explicit, undone future decision, not a hidden gap.
- **No `IMelodyAnalyzer` implementation wrapping `PitchTracker`+`MelodicContourClassifier`.** Section 3's
  own proposed `IMelodyAnalyzer`/`INoteTrackEditor`/`IMelodicGestureMapper` remain unimplemented — deferred
  to Steps 049/050, per Step 045's own disclosed scope boundary (unchanged by this step). This step adds
  only the MIDI-specific sibling interface (`IMidiTranscriptionBackend`) named in its own title.
  Reconciling/unifying these two interfaces (if ever desirable) is an explicit, undone architectural
  question for a later phase, not resolved here.
- **No UI/piano-roll wiring.** This step's own shared phase-wide "user-visible outcome" boilerplate text is
  satisfied by the real persisted JSON artifact and the real console timeline in §5, not by any Avalonia
  view — consistent with Steps 045/046/047's own identical, already-disclosed scope boundary (Screen 10's
  own piano roll is Step 049's scope).

## 8. PASS / BLOCKED

**Status: PASS** (for this step's own narrow, disclosed scope — parsing a trusted Standard MIDI File's
note-on/note-off and tempo events into the canonical `MelodyTrack` structure behind a dedicated transcription
interface — not a claim that Phase 06's own separately-tracked, carried-forward BLOCKED verdict is resolved).

- "Imported MIDI note timings map to canonical event structure" (required acceptance, verbatim) — proven by
  `Step048_Acceptance_Passes_ImportedMidiNoteTimingsMapToCanonicalEventStructure` (a real 7-note `.mid` byte
  file with a real mid-phrase tempo change produces exactly the expected canonical-tick `NoteEvent`
  timings, MIDI note numbers, velocities and confidence, plus the correctly re-derived `MelodicGesture`
  sequence matching Section 12's own worked example) — independently confirmed by the real demo's
  Scenario 1.
- "Parse MIDI note-on/note-off and tempo changes; create adapter to same track; use MIDI as ground-truth
  alternative where present" (implementation guidance, verbatim) — proven by the same acceptance test (a
  real tempo change at tick 1920 is correctly reflected in the canonical timing of every note after it),
  `ImportAsync_AutoSelectsChannelWithMostNotes_SkippingPercussionChannel9`, and
  `ImportAsync_ExplicitMelodyChannel_SelectsThatChannelEvenIfNotTheMajority` — independently confirmed by
  the real demo's Scenario 2; "ground-truth" (`Confidence = 1.0`, `UserCorrected = false`) is asserted
  directly in the acceptance test.
- "Invalid input, cancellation or stale persisted state must not corrupt the project" (failure/edge case,
  verbatim) — proven by 10 distinct invalid-input tests (corrupt header, SMPTE division, truncated chunk,
  unmatched note-on, unmatched note-off, zero note events, out-of-range channel, out-of-range requested
  channel, out-of-range `MinNoteConfidence`, empty `sourceAssetHash`) plus two `ArgumentNullException`
  negative paths, one cancellation test, and
  `ImportAsync_InvalidReimport_DoesNotCorruptPreviouslyPersistedTrack_StalePersistedStateNegativePath` (a
  previously saved file's bytes are asserted byte-for-byte unchanged after a subsequent failed re-import) —
  independently confirmed by the real demo's Scenarios 3, 4 and 5.
- Full regression: **636/636 passing**, 0 failed, 0 skipped; every pre-existing test from Steps 001-047
  still passes unmodified.
- **Carried-forward, disclosed phase-level/dependency-level gap (not this step's own blocker):** Phase 06's
  own gate remains BLOCKED (§3/§7, carried forward from Steps 045/046/047) — no production stem-separation
  model exists in this repository. This does not block Step 048's own required acceptance text, which this
  step satisfies in full. Per the user's own standing waiver (`memory:
  feedback_skip_xlights_human_verification`), the xLights human-render check is not treated as a blocker
  for this or any other step, and is not fabricated here; no xLights-specific manual verification applies
  to this step in any case (it does not touch the `.xsq`/`xlights_rgbeffects.xml` pathway). Per Section 8's
  own instruction ("Do not start the next phase when blocked"), **Step 049 should not begin against a
  silent assumption that this evidence constitutes a Phase 07 gate PASS** — only Step 048's own narrow
  scope is proven here; the phase-level gate (Section 7's full-phase test matrix, the phase gate
  demonstration, and Section 8's required final gate report) remains open until Steps 049-051 are each
  independently implemented and evidenced in turn.
