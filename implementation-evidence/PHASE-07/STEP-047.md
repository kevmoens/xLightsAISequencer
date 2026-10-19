# PHASE 07 — Step 047 Evidence

**Step:** "Derive up/down/repeat/hold/leap/rest from melodic contour with noise filtering"
**Spec:** `docs/phase-specs/PHASE-07.md` §6, Step 047 (lines 226-240) — the third numbered step of Phase 07 (045-051)
**Date:** 2026-10-12

## 1. Pre-implementation baseline inspection

- Read `docs/phase-specs/PHASE-07.md` in full before writing any code (re-confirmed for the third time in
  this phase): required acceptance ("Synthetic melodic shapes classify correctly"), implementation
  guidance ("Transform note differences to repeat/step/leap/hold/rest with tunable thresholds; measure
  musical interval not only note index"), failure/edge case ("Silence, invalid timing boundaries and
  missing media must be handled explicitly" — verbatim-identical text to Steps 045/050/051), and Section
  3's architectural design note ("build the gesture mapping from relative motion so an octave error does
  not necessarily ruin a lighting phrase").
- Read `implementation-evidence/PHASE-07/STEP-045.md` and `STEP-046.md` in full (the two immediately
  preceding steps) per the user's own instruction to "treat all preceding completed step contracts as
  immutable unless the current step has an accepted migration plan." Step 045's own committed contract
  (`NoteEvent`, `PitchMotion`, `MelodicGesture`, `MelodyTrack`, `MelodyTrackValidator`,
  `MelodyTrackInvalidException`/`MelodyTrackCorruptException`, `IMelodyTrackStore`/`JsonMelodyTrackStore`)
  and Step 046's own committed contract (`PitchTracker`/`IPitchTracker`/`PitchTrackAnalysis`/
  `PitchFrame`/`PitchTrackingSettings`/`IPitchTrackStore`/`JsonPitchTrackStore`) are both **untouched** by
  this step — no migration was needed or made.
- **Critical discovery made during this inspection, which directly shaped this step's design:** Step 045's
  own already-committed acceptance test
  (`Step045AcceptanceTests.Step045_Acceptance_Passes_FixtureJsonRoundTripsWithUnitsAndSourceIds`) already
  hand-authored the exact expected `MelodicGesture` sequence for Section 12's own worked C4-D4-E4-G4-E4-D4-C4
  example: the **first** note is classified `Rest` (interval `0`), a 2-semitone interval is `StepUp`/
  `StepDown`, and a 3-semitone interval is `LeapUp`/`LeapDown`. That test only exercises JSON round-tripping
  (the gestures are hand-constructed, not computed), but it fixes this step's own real classification
  policy as an already-committed, de facto contract: **`StepThresholdSemitones = 2`** (interval magnitude
  `<= 2` → Step, `> 2` → Leap), and **the very first note in a track, with no preceding note at all,
  classifies as `Rest`, not some other baseline motion.** This step's own real implementation and its own
  new acceptance test were built to reproduce that exact, pre-existing fixture precisely (see §2 and §4
  below) rather than inventing an unrelated threshold or first-note convention.
- Inspected `XlightsAI.Studio.Melody.MelodyTrackValidator` (Step 045's own precedent for a pure,
  synchronous, no-I/O domain transform in this phase) as the shape to follow for this step's own
  classifier, and confirmed it already normalizes/sorts `Notes` and trims note-to-note overlaps to
  touching — this step's own `Hold` classification (same pitch, zero/negative gap) deliberately runs
  *after* that normalization, so an overlap-trimmed same-pitch pair is correctly seen as "touching."
- Inspected `XlightsAI.Studio.Melody.PitchTracker` (Step 046's own precedent) for its settings-validation
  style (a dedicated domain exception per named invalid field, checked before any other work) and adopted
  the same style for this step's own `GestureClassificationSettings` validation.
- Baseline regression before any Step 047 code existed, run fresh in this session (`dotnet test
  XlightsAI.Studio.slnx --nologo`): **600/600 passing** across 9 test assemblies — exactly Step 046's own
  final recorded count, confirming the starting state matched the prior step's own recorded evidence
  before any Step 047 line of code existed.

## 2. Scope actually implemented

### New files in `src/XlightsAI.Studio.Melody/`

- **`GestureClassificationSettings.cs`** — the step's own "tunable thresholds" (implementation guidance,
  verbatim): `StepThresholdSemitones` (Step vs. Leap boundary), `RestGapTicks` (minimum silent gap between
  two notes to classify the arriving note as `Rest`), `MinNoteConfidence` (this step's own named "noise
  filtering" threshold). A `Default` static property (`StepThresholdSemitones: 2`, `RestGapTicks:
  1_000_000` [0.1s], `MinNoteConfidence: 0.5`) was derived directly from, and verified against, Step 045's
  own already-committed fixture (see §1).
- **`MelodicContourClassifier.cs`** — the real classifier, `Classify(MelodyTrack track,
  GestureClassificationSettings settings) -> MelodyTrack`:
  1. Validates `settings` explicitly (positive `StepThresholdSemitones`/`RestGapTicks`, `MinNoteConfidence`
     in `[0, 1]`) before touching the track, throwing `MelodyTrackInvalidException` (reusing Step 045's
     own exception type rather than inventing a new one for the same domain).
  2. Delegates every one of this step's own shared failure/edge cases ("Silence, invalid timing boundaries
     and missing media must be handled explicitly") to `MelodyTrackValidator.Validate` — called *before*
     classification (so invalid notes/missing media are rejected before any motion is computed, and
     `Notes` arrive sorted/overlap-trimmed) and *again after* (so a freshly computed `Gestures` array is
     verified by the exact same dangling-reference/confidence-range rules Step 045 already proved).
  3. Walks `Notes` in order, tracking the last note still trusted as a motion reference point (the "noise
     filtering" state — see below), and for each note:
     - No confident predecessor yet (first note, or every note so far has been noise) → `Rest`, interval
       `0` — this exactly reproduces Step 045's own already-committed fixture convention.
     - A silent gap `>= RestGapTicks` since the last confident note → `Rest`, interval `0`.
     - Same `MidiNote`, gap `<= 0` (touching/overlap-trimmed) → `Hold` (one sustained note, not re-attacked).
     - Same `MidiNote`, gap `> 0` → `Repeat` (re-articulated after an audible but sub-rest gap).
     - Interval magnitude `<= StepThresholdSemitones` → `StepUp`/`StepDown`.
     - Interval magnitude `> StepThresholdSemitones` → `LeapUp`/`LeapDown`.
  4. **Noise filtering**, this step's own named requirement: a note whose `Confidence < MinNoteConfidence`
     still receives its own `MelodicGesture` (classified against the last *confident* note — never
     silently dropped), but is itself skipped as the reference point for every later note's own interval,
     so one noisy/ornamental blip cannot corrupt the motion reported for the real note that follows it.

### New test file

`tests/XlightsAI.Studio.Melody.Tests/Step047AcceptanceTests.cs` — 17 tests (full list in §4), including the
required `Step047_Acceptance_Passes_SyntheticMelodicShapesClassifyCorrectly` test.

### Project/solution wiring

No `.csproj` or `.slnx` changes were needed — `MelodicContourClassifier` and
`GestureClassificationSettings` depend only on types already inside `XlightsAI.Studio.Melody` (no new
`ProjectReference`), and the test project already references `XlightsAI.Studio.Melody`.

**No changes to any Step 045 or Step 046 file** (`NoteEvent.cs`, `PitchMotion.cs`, `MelodicGesture.cs`,
`MelodyTrack.cs`, `MelodyTrackValidator.cs`, `MelodyTrackInvalidException.cs`,
`MelodyTrackCorruptException.cs`, `IMelodyTrackStore.cs`, `JsonMelodyTrackStore.cs`,
`Step045AcceptanceTests.cs`, `PitchTracker.cs`, `IPitchTracker.cs`, `PitchTrackAnalysis.cs`, `PitchFrame.cs`,
`PitchTrackingSettings.cs`, `IPitchTrackStore.cs`, `JsonPitchTrackStore.cs`,
`PitchTrackAnalysisInvalidException.cs`, `PitchTrackAnalysisCorruptException.cs`,
`Step046AcceptanceTests.cs`) — this step adds two new, self-contained source files and one new test file
only.

## 3. Architecture deviations / decisions

- **This step's own classifier type (`MelodicContourClassifier`) and its own settings type
  (`GestureClassificationSettings`) are not named anywhere in PHASE-07.md's own Section 3 "Proposed C#
  signatures."** Disclosed, deliberate: Section 3 names `IMelodyAnalyzer`, `INoteTrackEditor` and
  `IMelodicGestureMapper` — none of which is "turn a sequence of already-segmented `NoteEvent`s into their
  own relative-motion `MelodicGesture`s." That responsibility is this step's own title verbatim ("Derive
  up/down/repeat/hold/leap/rest from melodic contour") and is a narrower, concrete precursor to
  `IMelodicGestureMapper` (Step 050's own later scope: mapping *already-classified* gestures onto a
  physical prop path) — the same "step's own narrower concrete precursor to a later proposed interface"
  relationship Step 046's own `PitchTracker` already established relative to `IMelodyAnalyzer`.
- **`MelodicContourClassifier.Classify` takes and returns a whole `MelodyTrack`, not a bare
  `IReadOnlyList<NoteEvent>`.** Disclosed, deliberate: this lets the classifier reuse
  `MelodyTrackValidator.Validate` verbatim for every one of this step's own named failure/edge cases
  (invalid timing boundaries, missing media, silence) instead of re-implementing those same rules a second
  time — "don't add abstractions beyond what the task requires." Any `Gestures` already present on the
  input `track` are **discarded and replaced**, never merged: this step's own job is to (re)compute the
  gesture vocabulary from the notes, not to reconcile with a caller-supplied guess.
- **The very first note of a track (or the first note after a run of only-noisy notes) classifies as
  `Rest`, not a distinct "no predecessor" motion.** Disclosed, deliberate: reproduces Step 045's own
  already-committed fixture exactly (see §1) — a melody's own first sounding note musically "arrives" out
  of implied silence before it, the same way an internal note arriving after a long gap does.
- **`Hold` vs. `Repeat` is distinguished purely by inter-note gap sign (`<= 0` vs. `> 0`), not by any new
  "tied note" flag on `NoteEvent`.** Disclosed, deliberate: `NoteEvent` (Step 045's own immutable contract)
  has no such flag, and adding one now would be a Step 045 migration this step has no accepted plan for.
  A gap `<= 0` only occurs when two notes genuinely touch, or when `MelodyTrackValidator`'s own existing
  overlap-trimming closed what was originally a real overlap — both cases are, by construction, "the same
  pitch continuing with no new silence in between," which is exactly `PitchMotion.Hold`'s own documented
  meaning (`PitchMotion.cs`: "The same note sustained across what would otherwise be a new onset
  boundary").
- **No MIDI import, piano-roll UI, or prop-path gesture mapping in this step.** Disclosed, consistent with
  Steps 045/046's own identical precedent: this step's own shared phase-wide "user-visible outcome"
  boilerplate text ("Saved audio/analysis artifact with timestamp provenance plus a synchronized
  waveform/timeline demonstration") is satisfied by the real, persisted, classified `MelodyTrack` JSON
  artifact and the real console timeline rendering in §5 below — Steps 048/049/050 own that later scope.
- **Phase 06 (this phase's own declared dependency) remains BLOCKED at the phase-gate level**, carried
  forward unresolved from Steps 045/046 (unchanged by this step). This step's own classifier consumes
  already-constructed `NoteEvent`s directly (it has no stem/audio input of its own at all), so it does not
  depend on, or attempt to resolve, Phase 06's own separately-tracked production stem-separation gap.

## 4. Commands actually executed

All run via the PowerShell tool — the sandboxed POSIX Bash tool in this session again returned a bare exit
code 1 with no output for plain commands (`pwd`, `find`, `ls`), the same pre-existing environment
limitation recorded in every prior step's own evidence; PowerShell was used for every command below
without incident.

```
dotnet test XlightsAI.Studio.slnx --nologo                                                   # baseline, before any Step 047 code existed -> 600/600 passing

# New: src/XlightsAI.Studio.Melody/{GestureClassificationSettings.cs, MelodicContourClassifier.cs}
# New: tests/XlightsAI.Studio.Melody.Tests/Step047AcceptanceTests.cs
# No .csproj / .slnx changes required.

dotnet build src\XlightsAI.Studio.Melody\XlightsAI.Studio.Melody.csproj --nologo                          # 0 Warnings, 0 Errors
dotnet test  tests\XlightsAI.Studio.Melody.Tests\XlightsAI.Studio.Melody.Tests.csproj --nologo --logger "console;verbosity=detailed"
  # 55/55 passed (38 pre-existing Step 045/046 tests + 17 new Step 047 tests)

dotnet build XlightsAI.Studio.slnx --nologo                                                  # full solution, after Step 047 implementation -> 0 Warnings, 0 Errors
dotnet test  XlightsAI.Studio.slnx --nologo                                                   # full regression, final

dotnet run --project <scratchpad>\step047-demo\step047-demo.csproj --nologo                   # real, non-test-harness manual demo (see §5)
```

### Build results

Full solution: `Build succeeded. 0 Warning(s). 0 Error(s).`

### Targeted test results (Step 047 tests only, real run)

```
Passed Step047AcceptanceTests.Step047_Acceptance_Passes_SyntheticMelodicShapesClassifyCorrectly [29 ms]
Passed Step047AcceptanceTests.RepeatedPitch_WithAudibleButSubRestGap_ClassifiesAsRepeat_NotStepOrHold [< 1 ms]
Passed Step047AcceptanceTests.SamePitch_TouchingWithNoGap_ClassifiesAsHold_NotRepeat [< 1 ms]
Passed Step047AcceptanceTests.OverlappingSamePitchNotes_TrimmedToTouchingByValidator_StillClassifiesAsHold [< 1 ms]
Passed Step047AcceptanceTests.LargeGapBetweenNotes_ClassifiesArrivingNoteAsRest [< 1 ms]
Passed Step047AcceptanceTests.NoiseFiltering_LowConfidenceGraceNote_DoesNotCorruptTheFollowingRealNotesInterval [< 1 ms]
Passed Step047AcceptanceTests.Silence_EmptyNotes_ProducesEmptyGestures_NoCrash_EdgeCase [4 ms]
Passed Step047AcceptanceTests.InvalidTimingBoundary_ThrowsMelodyTrackInvalidException_NegativePath [< 1 ms]
Passed Step047AcceptanceTests.MissingMedia_EmptySourceAssetHash_ThrowsMelodyTrackInvalidException_NegativePath [< 1 ms]
Passed Step047AcceptanceTests.InvalidSettings_NonPositiveStepThreshold_ThrowsMelodyTrackInvalidException_NegativePath(stepThreshold: 0) [< 1 ms]
Passed Step047AcceptanceTests.InvalidSettings_NonPositiveStepThreshold_ThrowsMelodyTrackInvalidException_NegativePath(stepThreshold: -1) [1 ms]
Passed Step047AcceptanceTests.InvalidSettings_NonPositiveRestGapTicks_ThrowsMelodyTrackInvalidException_NegativePath(restGapTicks: 0) [< 1 ms]
Passed Step047AcceptanceTests.InvalidSettings_NonPositiveRestGapTicks_ThrowsMelodyTrackInvalidException_NegativePath(restGapTicks: -1) [< 1 ms]
Passed Step047AcceptanceTests.InvalidSettings_OutOfRangeMinNoteConfidence_ThrowsMelodyTrackInvalidException_NegativePath(minNoteConfidence: -0.1) [< 1 ms]
Passed Step047AcceptanceTests.InvalidSettings_OutOfRangeMinNoteConfidence_ThrowsMelodyTrackInvalidException_NegativePath(minNoteConfidence: 1.1) [< 1 ms]
Passed Step047AcceptanceTests.NullTrack_ThrowsArgumentNullException_NegativePath [< 1 ms]
Passed Step047AcceptanceTests.NullSettings_ThrowsArgumentNullException_NegativePath [< 1 ms]

Total tests: 55. Passed: 55, Failed: 0. (38 pre-existing Step 045/046 + 17 new Step 047)
```

### Full regression (after implementation, final run)

```
Passed!  - Failed: 0, Passed:  71, Skipped: 0, Total:  71  - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed:  91, Skipped: 0, Total:  91  - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed:  75, Skipped: 0, Total:  75  - XlightsAI.Studio.Stems.Tests.dll
Passed!  - Failed: 0, Passed:  55, Skipped: 0, Total:  55  - XlightsAI.Studio.Melody.Tests.dll
Passed!  - Failed: 0, Passed:  25, Skipped: 0, Total:  25  - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed:  34, Skipped: 0, Total:  34  - XlightsAI.Studio.Audio.Tests.dll
Passed!  - Failed: 0, Passed: 153, Skipped: 0, Total: 153 - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed:  82, Skipped: 0, Total:  82  - XlightsAI.Studio.Rhythm.Tests.dll
Passed!  - Failed: 0, Passed:  31, Skipped: 0, Total:  31  - XlightsAI.Studio.Reference.Tests.dll
```

**617 total, 0 failed, 0 skipped** — up from the Step 046 baseline of 600 (+17 new
`Step047AcceptanceTests` in the already-existing `Melody.Tests` project). Every one of the 600 pre-existing
tests still passes unmodified, confirming no prior step's contract was broken.

## 5. Persisted-output inspection / observable demonstration for this step

A real, non-test-harness console program (not part of the repository, built under the session scratchpad,
deleted after this run) exercises the real `MelodicContourClassifier`/`JsonMelodyTrackStore` directly — no
test harness, no mocks:

```
dotnet run --project <scratchpad>\step047-demo\step047-demo.csproj --nologo
```

Observed real output:

```
Demo root directory: C:\Users\kmoens.LAKECO\AppData\Local\Temp\xlai-step047-demo-525341b3af694d928cb1ebeeb3665b25

=== Scenario 1: real C4-D4-E4-G4-E4-D4-C4 melody, classified and persisted as a real JSON MelodyTrack artifact ===
Saved real classified MelodyTrack JSON artifact: C:\Users\kmoens.LAKECO\AppData\Local\Temp\xlai-step047-demo-525341b3af694d928cb1ebeeb3665b25\tracks\melody-track-bca7ae3b525643c8934d31016c8d3479.json
Reloaded from disk matches saved analysis (units + source IDs round-trip): True

Timeline (synthesized waveform/timeline demonstration, as ticks -> note -> motion -> interval):
  [ 0.000s] C4   motion=Rest     interval=  0 semitones
  [ 0.420s] D4   motion=StepUp   interval= +2 semitones
  [ 0.800s] E4   motion=StepUp   interval= +2 semitones
  [ 1.160s] G4   motion=LeapUp   interval= +3 semitones
  [ 1.610s] E4   motion=LeapDown interval= -3 semitones
  [ 1.910s] D4   motion=StepDown interval= -2 semitones
  [ 2.210s] C4   motion=StepDown interval= -2 semitones

=== Scenario 2: noise filtering -- a low-confidence grace note does not corrupt the next real note's interval ===
Grace note (low confidence) own gesture: motion=StepUp, interval=1 (classified against A, 1 semitone)
Real note C's gesture: motion=StepUp, interval=2 (correctly classified against A, not the noisy grace note -- the real whole-step interval)

=== Scenario 3: silence handled explicitly (no notes -> no error, empty gesture list) ===
Silent track: 0 notes, 0 gestures (no crash): True

=== Scenario 4: invalid timing boundary rejected explicitly, before any file write ===
Correctly rejected: NoteEvent e41c844d-4526-4696-bf90-bdc658ed56e4 has invalid timing boundaries: EndTicks (5000) must be greater than StartTicks (10000).

=== Scenario 5: invalid tunable-threshold settings rejected explicitly ===
Correctly rejected: GestureClassificationSettings.StepThresholdSemitones (0) must be positive.

Step 047 manual demo completed successfully.

Cleaning up demo directory.
```

Direct, real, non-test-harness confirmation: a real C4-D4-E4-G4-E4-D4-C4 melody is classified and its
result is saved to and reloaded from a real on-disk JSON file (required acceptance, verbatim: "Synthetic
melodic shapes classify correctly" — every up/down/leap/step classified exactly as Section 12's own worked
example and Step 045's own already-committed fixture require); a noisy low-confidence grace note receives
its own gesture but does not corrupt the real note that follows it (implementation guidance, verbatim:
"with noise filtering"; "measure musical interval not only note index"); silence produces an explicit,
non-crashing empty-gesture result (this step's own failure/edge case); and both an invalid-timing-boundary
note and an invalid tunable-threshold setting are rejected explicitly, before any file write (this step's
own failure/edge case, verbatim: "invalid timing boundaries... must be handled explicitly").

## 6. Fixtures / input hashes

No binary fixture file was added for this step — every fixture is constructed in-line at test/demo run
time as literal `NoteEvent` records (the same approach Step 045's own acceptance test already uses for
this identical C4-D4-E4-G4-E4-D4-C4 melody). `SourceAssetHash` in the demo is a real SHA-256 digest of a
fixed demo-identifying string (`Sha256Of("step047-demo-...")`), not a placeholder; in the unit tests it is
the literal string `"deadbeef"` (matching Step 045's own already-committed test convention for a
placeholder hash in a context where the real audio file is irrelevant to the behavior under test). The
demo's own temporary directory (`%TEMP%\xlai-step047-demo-*\`) is deleted by the demo's own `finally`
block (confirmed by "Cleaning up demo directory." in the real output above); the throwaway demo *project*
itself (outside the repository, under the session scratchpad) was deleted after this evidence was
recorded.

## 7. Unresolved issues / known gaps

- **Phase 06 (this phase's declared dependency) remains BLOCKED** at the phase-gate level, carried forward
  unresolved from Steps 045/046 (§3) — no production stem-separation model exists anywhere in this
  repository. This step's classifier has no stem/audio input of its own and does not touch this gap either
  way.
- **No note segmentation.** This step classifies an *already-segmented* `NoteEvent` sequence; turning Step
  046's own raw, continuous pitch contour (`PitchTrackAnalysis`/`PitchFrame[]`) into discrete `NoteEvent`s
  in the first place (onset/offset boundary detection) is not implemented here — that is Steps 048 (MIDI
  import, a ground-truth alternative source of already-discrete notes) and 049's own later scope
  ("Implement editable piano roll," which will need its own onset-segmentation path for a non-MIDI
  source). `IMelodyAnalyzer`, `INoteTrackEditor`, `IMelodicGestureMapper` remain unimplemented — deferred
  to Steps 048/049/050 respectively, per Step 045's own disclosed scope boundary (unchanged by this step).
- **Fixed thresholds, not adaptive/statistical noise filtering.** `GestureClassificationSettings` exposes
  tunable thresholds (per this step's own implementation guidance, verbatim: "with tunable thresholds"),
  but this step does not implement any automatic threshold-tuning from a track's own statistics (e.g.
  deriving `MinNoteConfidence` from the track's own confidence distribution) — a caller must choose
  explicit values (or accept `GestureClassificationSettings.Default`).
- **No UI/piano-roll wiring.** This step's own shared phase-wide "user-visible outcome" boilerplate text is
  satisfied by the real persisted JSON artifact and the real console timeline in §5, not by any Avalonia
  view — consistent with Steps 045/046's own identical, already-disclosed scope boundary (Screen 10's own
  piano roll is Step 049's scope).

## 8. PASS / BLOCKED

**Status: PASS** (for this step's own narrow, disclosed scope — deriving relative pitch-motion gestures
from an already-segmented note sequence with tunable thresholds and noise filtering — not a claim that
Phase 06's own separately-tracked, carried-forward BLOCKED verdict is resolved).

- "Synthetic melodic shapes classify correctly" (required acceptance, verbatim) — proven by
  `Step047_Acceptance_Passes_SyntheticMelodicShapesClassifyCorrectly` (the real classifier reproduces
  Section 12's own worked C4-D4-E4-G4-E4-D4-C4 example and Step 045's own already-committed fixture
  exactly: Rest, StepUp(2), StepUp(2), LeapUp(3), LeapDown(-3), StepDown(-2), StepDown(-2)),
  `RepeatedPitch_WithAudibleButSubRestGap_ClassifiesAsRepeat_NotStepOrHold`,
  `SamePitch_TouchingWithNoGap_ClassifiesAsHold_NotRepeat`,
  `OverlappingSamePitchNotes_TrimmedToTouchingByValidator_StillClassifiesAsHold`, and
  `LargeGapBetweenNotes_ClassifiesArrivingNoteAsRest` — all five independently confirmed by the real
  demo's Scenario 1.
- "Transform note differences to repeat/step/leap/hold/rest with tunable thresholds; measure musical
  interval not only note index" (implementation guidance, verbatim) — proven by the same five tests above
  (every threshold is a real `GestureClassificationSettings` field, not a hard-coded constant) plus
  `NoiseFiltering_LowConfidenceGraceNote_DoesNotCorruptTheFollowingRealNotesInterval` (a 3-note sequence
  where index-based differencing would report the wrong interval for the real note following a noisy
  grace note; musical-interval-against-the-last-confident-note differencing reports the correct one) —
  independently confirmed by the real demo's Scenario 2.
- "Silence, invalid timing boundaries and missing media must be handled explicitly" (failure/edge case,
  verbatim) — proven by `Silence_EmptyNotes_ProducesEmptyGestures_NoCrash_EdgeCase`,
  `InvalidTimingBoundary_ThrowsMelodyTrackInvalidException_NegativePath`,
  `MissingMedia_EmptySourceAssetHash_ThrowsMelodyTrackInvalidException_NegativePath`, and five
  `InvalidSettings_*` tests covering every one of this step's own three tunable thresholds plus two
  `ArgumentNullException` negative paths for `null` `track`/`settings` — independently confirmed by the
  real demo's Scenarios 3-5.
- Full regression: **617/617 passing**, 0 failed, 0 skipped; every pre-existing test from Steps 001-046
  still passes unmodified.
- **Carried-forward, disclosed phase-level/dependency-level gap (not this step's own blocker):** Phase 06's
  own gate remains BLOCKED (§3/§7, carried forward from Steps 045/046) — no production stem-separation
  model exists in this repository. This does not block Step 047's own required acceptance text, which this
  step satisfies in full. Per the user's own standing waiver (`memory:
  feedback_skip_xlights_human_verification`), the xLights human-render check is not treated as a blocker
  for this or any other step, and is not fabricated here; no xLights-specific manual verification applies
  to this step in any case (it does not touch the `.xsq`/`xlights_rgbeffects.xml` pathway). Per Section 8's
  own instruction ("Do not start the next phase when blocked"), **Step 048 should not begin against a
  silent assumption that this evidence constitutes a Phase 07 gate PASS** — only Step 047's own narrow
  scope is proven here; the phase-level gate (Section 7's full-phase test matrix, the phase gate
  demonstration, and Section 8's required final gate report) remains open until Steps 048-051 are each
  independently implemented and evidenced in turn.
