# PHASE 07 — Step 051 Evidence

**Step:** "Handle polyphony, rests, note gaps, variable durations and frame quantization gracefully"
**Spec:** `docs/phase-specs/PHASE-07.md` §6, Step 051 (lines 290-304) — the seventh and final numbered step of
Phase 07 (045-051)
**Date:** 2026-10-14

## 1. Pre-implementation baseline inspection

- Read `docs/phase-specs/PHASE-07.md` in full before writing any code: required acceptance ("Fallback to
  contour or onsets when pitch unreliable; no invalid effects"), implementation guidance ("Represent musical
  rests and sustained notes explicitly; preserve sub-beat timing where valid and make uncertainty visible"),
  failure/edge case (verbatim, shared with Steps 045/047/050: "Silence, invalid timing boundaries and missing
  media must be handled explicitly"), Section 3's fallback ladder ("trustworthy MIDI -> high-confidence
  isolated-stem note events -> pitch contour direction and onset -> rhythm-only events"), and Section 12's own
  instruction: "verify xLights frame snapping doesn't reverse event order."
- Read `implementation-evidence/PHASE-07/STEP-045.md` through `STEP-050.md` in full per the user's own
  instruction to "treat all preceding completed step contracts as immutable unless the current step has an
  accepted migration plan." **No file from Steps 045-050 was modified in a way that changes any existing,
  already-tested behavior** (see §3 for the one additive, backward-compatible exception: two new `init`
  properties on `GestureMapSettings`, both defaulting in a way that preserves Step 050's exact behavior for
  every pre-existing construction site unless a caller opts in).
- Read every file in `src/XlightsAI.Studio.Melody/` in full. Confirmed two already-committed, deliberate
  deferrals to this exact step: `IMidiTranscriptionBackend.cs` ("Section 3's own phase-wide polyphony
  deferral -- Step 051's later scope") and `MidiImportSettings.cs` ("this step's own scope is a single
  monophonic melody line... polyphony is Step 051's scope"); and `PitchFrame.cs`'s own "preserve raw contour
  for fallback... low-pitch-confidence fallback (Step 051)" remark. This step's own scope was bounded to
  exactly these three previously-deferred items, plus frame quantization (the step's own title), rather than
  re-opening already-accepted Step 045/047/048/049/050 contracts.
- Inspected `MelodyTrackValidator.cs` in full, including its own already-committed, locked negative-path test
  `FullyContainedOverlap_CannotBeNormalized_ThrowsMelodyTrackInvalidException_NegativePath`
  (`Step045AcceptanceTests.cs:214`) — confirmed this exact throw behavior is Step 045's own immutable
  contract and must not be weakened or removed. This directly shaped this step's own architecture decision
  (see §3): polyphony is resolved *before* a track ever reaches `MelodyTrackValidator`, never by changing the
  validator's own strict single-line invariant.
- Inspected `MelodicContourClassifier.cs`, `GestureClassificationSettings.cs`, `MelodicGesture.cs`,
  `MelodicGestureMapper.cs` and `GestureMapSettings.cs` (Steps 047/050) in full to confirm the exact
  already-committed semantics this step must build on without breaking: `MelodicGesture.Confidence` is
  `min(note.Confidence, previous.Confidence)` for a pitch-based motion and `note.Confidence` for a `Rest` —
  already present, unused by the mapper until this step.
- Searched `src/XlightsAI.Studio.Xlights/` for any existing frame-quantization logic (grep for
  `Frame|Quantiz`): found the one real, already-accepted implementation,
  `XsqExporter.FrameIntervalMs`/`RoundToFrame` (PHASE-02 Steps 013/015) — the only place in this repository
  that actually converts ticks to xLights frames. Read the full per-(target,layer) export loop
  (`XsqExporter.cs` lines ~196-230) before touching it.
- Baseline taken by running the real, targeted test suites *before* any Step 051 code was written:
  `XlightsAI.Studio.Melody.Tests.dll`: **104/104** passing (matches Step 050's own recorded final count
  exactly — no drift). `XlightsAI.Studio.Xlights.Tests.dll`: **71/71** passing (matches Step 050's own
  recorded baseline exactly).

## 2. Scope actually implemented

### New file in `src/XlightsAI.Studio.Melody/`

- **`MelodyPolyphonyReducer.cs`** — step title's own first named concern, "Handle polyphony... gracefully."
  A pure, synchronous, no-I/O static class (same shape as `MelodyTrackValidator`/`MelodicContourClassifier`).
  `Reduce(MelodyTrack track)` sweeps `Notes` in start-time order, keeping one deterministic "lead" note
  active at any instant: a genuinely overlapping candidate either loses outright and is dropped (fully
  subordinate to the current lead) or wins and causes the current lead to be trimmed to the winning note's
  own start (never dropped outright if it already started sounding). Dominance order: higher
  `NoteEvent.Confidence` wins; tie-break on higher `Velocity`; tie-break on earlier `StartTicks`. Always
  finishes by calling the real, unmodified `MelodyTrackValidator.Validate` so every already-proven unit/media
  failure is enforced exactly once, in exactly one place — never re-implemented here. Discards any incoming
  `Gestures` (same precedent as `MelodicContourClassifier.Classify`): a caller re-classifies the reduced
  result afterward.

### Modified files

- **`src/XlightsAI.Studio.Melody/GestureMapSettings.cs`** — two new additive `init`-only properties,
  `MinGestureConfidence` and `OnsetOnlyConfidenceFloor` (not positional parameters — every existing Step 050
  construction site/test keeps compiling and behaving identically). Both default to `0.0` on the bare record
  (meaning "never downgrade" — exact Step 050 behavior preserved for any caller not opting in), but
  `GestureMapSettings.Default` itself now sets them to `0.5`/`0.25` respectively (mirroring
  `GestureClassificationSettings.Default.MinNoteConfidence`), so the new fallback is actually demonstrable
  through the one settings instance every Step 050 test already used, without having silently changed any of
  those tests' own outcomes (none of their fixtures use a confidence below `0.5` — verified by inspection and
  by the unchanged 104/104 regression count).
- **`src/XlightsAI.Studio.Melody/MelodicGestureMapper.cs`** — required acceptance, verbatim: "Fallback to
  contour or onsets when pitch unreliable; no invalid effects." Added `ApplyConfidenceFallback`: below
  `OnsetOnlyConfidenceFloor`, any pitch-based motion (`Repeat`/`StepUp`/`StepDown`/`LeapUp`/`LeapDown`)
  degrades to a plain onset pulse at the walk's own current, unmoved position ("onset" fallback — rhythm
  only, the lowest rung of Section 3's own fallback ladder). Between the floor and `MinGestureConfidence`, a
  leap downgrades to the matching step ("contour" fallback — direction trusted, exact interval not).
  `Hold`/`Rest` are never touched by this fallback (a hold's duration and a rest's silence are timing
  decisions, not pitch decisions). Also added three new settings-validation checks (range/ordering of the two
  new thresholds), each throwing the already-existing `EffectPlanInvalidException` — no new exception type.
- **`src/XlightsAI.Studio.Xlights/XsqExporter.cs`** — step title's own "frame quantization... gracefully";
  Section 12's own instruction, verbatim: "verify xLights frame snapping doesn't reverse event order." See §3
  for the full disclosed architecture decision and why the real triggering condition is a genuinely
  raw-overlapping `EffectInstance` pair on the same target+layer (not rounding-induced reversal, which is
  mathematically impossible for monotonic grid rounding applied to already-ordered, non-overlapping inputs).
  Step 013/015's own original "rounds to a non-positive duration" throw is unchanged and still fires first,
  for the single-effect case it always covered.

### New test files

- `tests/XlightsAI.Studio.Melody.Tests/Step051AcceptanceTests.cs` — 13 tests against the real
  `MelodyPolyphonyReducer`/`MelodicGestureMapper`/`MelodicContourClassifier`/`EffectRecipeRegistry` (not a
  mock), including the required
  `Step051_Acceptance_Passes_LowConfidencePitchFallsBackToContourThenOnset_NoInvalidEffects` test (a
  four-note fixture exercising both fallback tiers in one real mapped sequence), four dedicated polyphony
  tests, a `ZeroFallbackThresholds_...` backward-compatibility test, a `RestAndHold_...` test proving `Hold`/
  `Rest` are immune to the new fallback, and five negative-path tests (missing media, invalid timing
  boundary, null track, out-of-range/mis-ordered new thresholds).
- `tests/XlightsAI.Studio.Xlights.Tests/Step051FrameQuantizationTests.cs` — 3 tests against the real
  `XsqExporter`/`XsqStructuralReader`, real file-system I/O (not a mock), including the required
  `Step051_Acceptance_Passes_OverlappingEffectsOnSameLayer_FrameSnapNeverReversesOrder_NoInvalidEffects` test
  (two genuinely raw-overlapping `EffectInstance`s exported and round-tripped through the real reader), a
  regression guard confirming ordinary well-separated effects are never shifted, and the original Step 013/
  015 "rounds to non-positive duration" negative path, unaffected by this step's own change.

**No changes to any Step 045/046/047/048/049/050 file's own previously-tested behavior.**

## 3. Architecture deviations / decisions

- **Polyphony is resolved by a new pre-validation pass (`MelodyPolyphonyReducer`), never by weakening
  `MelodyTrackValidator`'s own strict single-melodic-line invariant.** `MelodyTrackValidator`'s "fully
  contained overlap throws" behavior is Step 045's own already-committed, locked negative-path test
  (`FullyContainedOverlap_CannotBeNormalized_ThrowsMelodyTrackInvalidException_NegativePath`) — changing it
  would violate the user's own "treat all preceding completed step contracts as immutable" instruction. A
  genuinely polyphonic source (two or more simultaneous real onsets) is instead reduced to one deterministic
  monophonic "lead" line *before* it ever reaches the validator — "gracefully" means producing a usable
  result instead of raising `MelodyTrackInvalidException` on real-world chordal/interference input, not
  promising polyphonic transcription (Section 1 remains explicit: "no promise of polyphonic note-perfect
  transcription" is out of scope). Directly tested by
  `Polyphony_TwoSimultaneousNotes_ReducedToOneDeterministicLeadLine_NotRejected` and
  `Polyphony_OverlappingDominantNoteStartsLater_TrimsPriorLeadRatherThanDroppingIt` — independently confirmed
  by the real demo's Scenario 1 (showing the validator's own real exception message, then the reducer's own
  real, non-throwing result side by side).
- **`GestureMapSettings.MinGestureConfidence`/`OnsetOnlyConfidenceFloor` are additive `init`-only properties,
  not new positional-record parameters.** Disclosed, deliberate: a positional-parameter change would require
  touching every existing Step 050 construction site (`new GestureMapSettings(...)`), several of which are
  committed test fixtures this step must not alter per the user's own immutability instruction. An `init`
  property with a `0.0` default on the bare record preserves exact prior behavior for any caller who
  constructs the record directly; only `GestureMapSettings.Default` (the one shared static instance every
  Step 050 test actually uses) is updated to set non-zero values, and only because every existing fixture's
  own note confidence (`0.9` or higher by convention) sits above both new thresholds — verified by inspection
  of every Step 050 fixture and reconfirmed by the unchanged 104-before/117-after (+13 new) regression count
  with zero prior-test modifications.
- **The low-confidence fallback never touches `Hold` or `Rest`.** A sustained note's own duration and a
  musical rest's own silence are timing decisions already fully determined by gap/overlap arithmetic
  (Steps 047/050), independent of how confident the pitch estimate was — downgrading either through the new
  "onset pulse" path would silently break an already-proven, unrelated behavior (Step 050's own
  `SustainedNote_...`/`Rest_...` tests). Directly tested by
  `RestAndHold_NeverAffectedByConfidenceFallback_RegardlessOfConfidence` with deliberately near-zero note
  confidence on both the sustained and rest-arriving notes.
- **Frame-quantization collision handling targets a genuinely raw-overlapping `EffectInstance` pair, not
  "rounding reverses an already-ordered, non-overlapping pair."** Before writing any code, this was proven
  out: rounding-to-nearest-grid (`RoundToFrame`) is a monotonic non-decreasing function of its millisecond
  input, so for any two effects already satisfying `StartTicks_B >= EndTicks_A` in raw ticks, their rounded
  values satisfy `roundedStart_B >= roundedEnd_A` automatically — reversal from rounding alone, applied to
  already-ordered non-overlapping inputs, is mathematically impossible. The real, previously-unguarded gap is
  that nothing upstream of `XsqExporter` enforces non-overlap between two `EffectInstance`s on the same
  target+layer the way `MelodyTrackValidator` enforces it for `NoteEvent`s — a genuinely raw-overlapping pair
  was previously written to the exported XSQ completely unchecked. The fix pushes a colliding effect's own
  rounded start (and end, preserving its own rounded duration) forward to meet the immediately preceding
  effect's own rounded end — never backward, never reordering. Step 013/015's own original "one effect's own
  duration independently collapses to non-positive at the grid" throw is left completely untouched and still
  fires first, exactly as before. Directly tested by
  `Step051_Acceptance_Passes_OverlappingEffectsOnSameLayer_FrameSnapNeverReversesOrder_NoInvalidEffects`,
  `NonOverlappingWellSeparatedEffects_FrameQuantizeIndependently_NeverShifted` (regression guard — the new
  branch must never fire for the ordinary case) and
  `EffectWhoseOwnDurationCollapsesAtFrameGrid_StillFailsSafely_NoFileWritten_NegativePath` (the original
  Step 013/015 path, unaffected) — independently confirmed by the real demo's Scenario 3 (a real `.xsq` file
  written to disk, then round-tripped through the real `XsqStructuralReader`).
- **No new `IPolyphonyReducer`/`IFrameQuantizer` interface was introduced** — both `MelodyPolyphonyReducer`
  and the `XsqExporter` fix are static/`sealed`, matching the already-established precedent of
  `MelodyTrackValidator`/`MelodicContourClassifier` (phase-local, pure, synchronous, no ambient state), not a
  speculative abstraction for a second implementation that does not exist (per the user's own "don't
  design for hypothetical future requirements" instruction).
- **No full `IMelodyAnalyzer`/pitch-frame-to-`NoteEvent` segmentation pipeline was built.** Section 3's own
  proposed `IMelodyAnalyzer.AnalyzeAsync(StemAsset, ...)` has no concrete implementation anywhere in this
  repository yet (confirmed by a solution-wide search before starting); building that full pipeline is a
  materially larger scope than this step's own named title/acceptance text ("polyphony, rests, note gaps,
  variable durations and frame quantization... fallback to contour or onsets when pitch unreliable") and is
  not fabricated here. This step's own fallback logic operates on whatever `MelodyTrack`/`MelodicGesture`
  values already exist (from MIDI import, Step 048, or any future segmentation step), consistent with every
  prior Melody-project component's own narrow scope boundary.
- **No `SequenceDocument`/`IEditTransactionService`/UI wiring for this step**, consistent with Step 050's own
  identical, disclosed precedent. `MelodyPolyphonyReducer.Reduce` and the updated `MapToEffects` remain pure
  domain/adapter functions; no `ShellViewModel`-level command exists yet for either.
- **Phase 06 (this phase's own declared dependency) remains BLOCKED** at the phase-gate level, carried
  forward unresolved from Steps 045-050 (unchanged by this step; no stem/audio input is touched here).

## 4. Commands actually executed

All run via the PowerShell tool — the sandboxed POSIX Bash tool in this session again returned bare exit
code 1 with no output for plain commands (confirmed at the very start of this step and again at the end),
the same pre-existing environment limitation recorded in every prior step's own evidence; PowerShell was used
for every command below without incident.

```
# Baseline, before any Step 051 code:
dotnet test tests\XlightsAI.Studio.Melody.Tests\XlightsAI.Studio.Melody.Tests.csproj --nologo
  # Passed! - Failed: 0, Passed: 104, Skipped: 0, Total: 104

# After MelodyPolyphonyReducer.cs, GestureMapSettings.cs, MelodicGestureMapper.cs changes:
dotnet build src\XlightsAI.Studio.Melody\XlightsAI.Studio.Melody.csproj --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).

# After XsqExporter.cs change:
dotnet build src\XlightsAI.Studio.Xlights\XlightsAI.Studio.Xlights.csproj --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).

# Regression check immediately after the production-code changes, before any new test file existed:
dotnet test tests\XlightsAI.Studio.Xlights.Tests\XlightsAI.Studio.Xlights.Tests.csproj --nologo
  # Passed! - Failed: 0, Passed: 71, Skipped: 0, Total: 71  (unchanged from baseline)
dotnet test tests\XlightsAI.Studio.Melody.Tests\XlightsAI.Studio.Melody.Tests.csproj --nologo
  # Passed! - Failed: 0, Passed: 104, Skipped: 0, Total: 104  (unchanged from baseline)

# After Step051AcceptanceTests.cs and Step051FrameQuantizationTests.cs added:
dotnet test tests\XlightsAI.Studio.Xlights.Tests\XlightsAI.Studio.Xlights.Tests.csproj --nologo `
  --filter "FullyQualifiedName~Step051" --logger "console;verbosity=detailed"
  # 3/3 passed, first run, no fixture fixes needed

dotnet test tests\XlightsAI.Studio.Melody.Tests\XlightsAI.Studio.Melody.Tests.csproj --nologo `
  --filter "FullyQualifiedName~Step051" --logger "console;verbosity=detailed"
  # 13/13 passed, first run, no fixture fixes needed

dotnet build XlightsAI.Studio.slnx --nologo       # full solution -> 0 Warnings, 0 Errors
dotnet test  XlightsAI.Studio.slnx --nologo       # full regression, final (run in background; output captured and inspected after completion)

dotnet new console -o <scratchpad>\xlai-step051-demo-proj --no-restore
dotnet add <scratchpad>\xlai-step051-demo-proj reference src\XlightsAI.Studio.Melody\XlightsAI.Studio.Melody.csproj
dotnet add <scratchpad>\xlai-step051-demo-proj reference src\XlightsAI.Studio.Xlights\XlightsAI.Studio.Xlights.csproj
dotnet add <scratchpad>\xlai-step051-demo-proj reference src\XlightsAI.Studio.Contracts\XlightsAI.Studio.Contracts.csproj
dotnet run --project <scratchpad>\xlai-step051-demo-proj --nologo   # real, non-test-harness manual demo (see §5)
```

### Targeted test results (Step 051 tests only, real run, first attempt, no fixture fixes needed)

```
Passed XlightsAI.Studio.Xlights.Tests.Step051FrameQuantizationTests.EffectWhoseOwnDurationCollapsesAtFrameGrid_StillFailsSafely_NoFileWritten_NegativePath
Passed XlightsAI.Studio.Xlights.Tests.Step051FrameQuantizationTests.Step051_Acceptance_Passes_OverlappingEffectsOnSameLayer_FrameSnapNeverReversesOrder_NoInvalidEffects
Passed XlightsAI.Studio.Xlights.Tests.Step051FrameQuantizationTests.NonOverlappingWellSeparatedEffects_FrameQuantizeIndependently_NeverShifted
Total tests: 3. Passed: 3.

Passed XlightsAI.Studio.Melody.Tests.Step051AcceptanceTests.OutOfRangeMinGestureConfidence_ThrowsEffectPlanInvalidException_NegativePath(min: 1.1, floor: 0)
Passed XlightsAI.Studio.Melody.Tests.Step051AcceptanceTests.OutOfRangeMinGestureConfidence_ThrowsEffectPlanInvalidException_NegativePath(min: -0.1, floor: 0)
Passed XlightsAI.Studio.Melody.Tests.Step051AcceptanceTests.Polyphony_OverlappingDominantNoteStartsLater_TrimsPriorLeadRatherThanDroppingIt
Passed XlightsAI.Studio.Melody.Tests.Step051AcceptanceTests.InvalidTimingBoundary_NoteEndNotAfterStart_ThrowsMelodyTrackInvalidException_NegativePath
Passed XlightsAI.Studio.Melody.Tests.Step051AcceptanceTests.RestAndHold_NeverAffectedByConfidenceFallback_RegardlessOfConfidence
Passed XlightsAI.Studio.Melody.Tests.Step051AcceptanceTests.Step051_Acceptance_Passes_LowConfidencePitchFallsBackToContourThenOnset_NoInvalidEffects
Passed XlightsAI.Studio.Melody.Tests.Step051AcceptanceTests.Silence_EmptyTrack_PolyphonyReductionProducesEmptyTrack_NoCrash_EdgeCase
Passed XlightsAI.Studio.Melody.Tests.Step051AcceptanceTests.Polyphony_TwoSimultaneousNotes_ReducedToOneDeterministicLeadLine_NotRejected
Passed XlightsAI.Studio.Melody.Tests.Step051AcceptanceTests.NullTrack_ThrowsArgumentNullException_NegativePath
Passed XlightsAI.Studio.Melody.Tests.Step051AcceptanceTests.MissingMedia_EmptySourceAssetHash_ThrowsMelodyTrackInvalidException_NegativePath
Passed XlightsAI.Studio.Melody.Tests.Step051AcceptanceTests.OnsetOnlyConfidenceFloorAboveMinGestureConfidence_ThrowsEffectPlanInvalidException_NegativePath
Passed XlightsAI.Studio.Melody.Tests.Step051AcceptanceTests.Polyphony_NonOverlappingNotes_PassThroughUnchanged
Passed XlightsAI.Studio.Melody.Tests.Step051AcceptanceTests.ZeroFallbackThresholds_PreservesStep050BehaviorExactly_NoDowngrade
Total tests: 13. Passed: 13.
```

### Full regression (final, after all Step 051 implementation and tests)

```
Passed!  - Failed: 0, Passed:  74, Skipped: 0, Total:  74  - XlightsAI.Studio.Xlights.Tests.dll   (71 baseline + 3 new Step 051)
Passed!  - Failed: 0, Passed:  91, Skipped: 0, Total:  91  - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed: 117, Skipped: 0, Total: 117  - XlightsAI.Studio.Melody.Tests.dll    (104 baseline + 13 new Step 051)
Passed!  - Failed: 0, Passed:  25, Skipped: 0, Total:  25  - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed:  38, Skipped: 0, Total:  38  - XlightsAI.Studio.Audio.Tests.dll
Passed!  - Failed: 0, Passed:  82, Skipped: 0, Total:  82  - XlightsAI.Studio.Rhythm.Tests.dll
Passed!  - Failed: 0, Passed: 159, Skipped: 0, Total: 159  - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed:  31, Skipped: 0, Total:  31  - XlightsAI.Studio.Reference.Tests.dll
Passed!  - Failed: 0, Passed: 101, Skipped: 0, Total: 101  - XlightsAI.Studio.Stems.Tests.dll
```

**718 total (across all 9 reported assemblies), 0 failed, 0 skipped.** Every pre-existing test from Steps
001-050 still passes unmodified (every assembly's own count either matches its own Step 050-era baseline
exactly, or increases only by this step's own newly added tests — Xlights +3, Melody +13).

## 5. Persisted-output inspection / observable demonstration for this step

A real, non-test-harness console program (not part of the repository, built under the session scratchpad,
deleted after this run) exercises the real `MelodyPolyphonyReducer`/`MelodicGestureMapper`/
`MelodicContourClassifier`/`XsqExporter`/`XsqStructuralReader` directly — no test harness, no mocks:

```
dotnet run --project <scratchpad>\xlai-step051-demo-proj --nologo
```

Observed real output:

```
=== Step 051 manual demo: polyphony, pitch-confidence fallback, frame quantization ===

--- Scenario 1: two simultaneous real onsets (polyphony) reduced to one monophonic lead ---
  Confirmed: MelodyTrackValidator alone rejects raw polyphony outright -- NoteEvent 7e2f3efe-...
  (0-4000) is fully contained by/coincides with NoteEvent 3daf2cf1-... (0-10000) -- cannot
  normalize without producing a zero-length note.
  MelodyPolyphonyReducer instead produces 1 note(s) -- MidiNote=67 (the higher-confidence lead
  survives; the weaker harmony note does not)

--- Scenario 2: fallback to contour direction (not full leap magnitude) when pitch is low-confidence ---
  Target=Arch1  Start=         0 End=      1000
  Target=Arch2  Start=      1100 End=      2000
  Target=Arch3  Start=      2100 End=      3000
  Target=Arch3  Start=      3100 End=      4000
  Expected Arch1,Arch2,Arch3,Arch3 (the low-confidence leap only moves one position like a step;
  the very-low-confidence step doesn't move the walk at all)

--- Scenario 3: frame quantization gracefully resolves a genuinely overlapping effect pair ---
  Exported real .xsq: C:\Users\kmoens.LAKECO\AppData\Local\Temp\xlai-step051-demo-.../overlap-demo.xsq
  Effect 1: start=0ms end=25ms
  Effect 2: start=25ms end=75ms
  Order preserved (effect 2 start >= effect 1 end): True

Step 051 manual demo completed successfully.
```

Direct, real, non-test-harness confirmation: (1) a genuinely polyphonic two-note input is first shown
provoking the real, unmodified `MelodyTrackValidator`'s own exception (proving the pre-existing Step 045
contract is untouched), then the real `MelodyPolyphonyReducer` resolves the same input to one deterministic
note ("Handle polyphony... gracefully," step title, verbatim); (2) a real four-note sequence shows a
confident `StepUp` unaffected, a low-confidence `LeapUp` downgraded to a one-position step, and a
very-low-confidence `StepDown` reduced to an onset-only pulse at the unmoved current position ("Fallback to
contour or onsets when pitch unreliable," required acceptance, verbatim); (3) a real `.xsq` file is written
to disk and round-tripped through the real `XsqStructuralReader`, showing two genuinely raw-overlapping
source effects quantized to a non-colliding, correctly-ordered pair ("frame quantization... gracefully";
Section 12: "verify xLights frame snapping doesn't reverse event order," both verbatim).

## 6. Fixtures / input hashes

No binary fixture *file* is committed to the repository for this step — every `NoteEvent`/`MelodyTrack`/
`EffectInstance` fixture is constructed in-memory at test/demo run time (the same approach every prior Melody
and Xlights step already uses for its own in-line fixtures). `SourceAssetHash` is the literal placeholder
string `"deadbeef"` in the Melody unit tests (matching every prior Melody step's own already-committed test
convention) and `"deadbeef-demo-hash"` in the demo; the Xlights frame-quantization tests use the same
`"TEST-HASH-NOT-A-REAL-FILE"` `DisplayGraph.SourceLayoutHash` placeholder Step013AcceptanceTests already
established. The throwaway demo *project* itself (outside the repository, under the session scratchpad) was
deleted after this evidence was recorded; the demo's own real exported `.xsq` file (written under the OS temp
directory, outside the repository and outside the scratchpad) was deleted by the demo program itself before
exit.

## 7. Unresolved issues / known gaps

- **Phase 06 (this phase's declared dependency) remains BLOCKED** at the phase-gate level, carried forward
  unresolved from Steps 045-050 (§3) — no production stem-separation model exists anywhere in this
  repository. This step's own new components have no stem/audio input of their own and do not touch this gap
  either way.
- **No full `IMelodyAnalyzer`/pitch-frame-to-`NoteEvent` segmentation pipeline exists.** Disclosed in §3 — no
  concrete implementation of Section 3's proposed `IMelodyAnalyzer` exists anywhere in this repository yet
  (confirmed by solution-wide search); this step's own fallback logic operates on whatever `MelodyTrack`
  already exists (currently produced only via Step 048's MIDI import path), not on a raw `PitchTrackAnalysis`
  contour directly. A future step converting `PitchTrackAnalysis`'s own raw `PitchFrame` contour into
  segmented `NoteEvent`s (the literal "pitch contour direction and onset" rung of Section 3's fallback
  ladder) remains open.
- **`MelodyPolyphonyReducer`'s dominance policy (confidence, then velocity, then earliest start) is one
  declared, deterministic choice, not the only defensible one.** Disclosed in §3 — consistent with every
  prior Melody step's own "declared policy, not a silent default" precedent (e.g. Step 050's
  `PitchPolicy`); a future step could add an alternate dominance policy (e.g. always prefer the higher pitch)
  behind the same explicit-parameter convention if a real use case demands it.
- **No real `SequenceDocument`/`IEditTransactionService`/UI integration for either new component.** Disclosed
  in §3, consistent with Step 050's own identical precedent.
- **The `XsqExporter` collision guard only pushes a colliding effect's own start *forward*** (to meet the
  immediately preceding effect's own rounded end) **and never re-validates against overlap on a *third*
  target/layer dimension beyond the one already grouped on** (i.e., cross-layer bleed is out of scope — xLights
  layers are independently rendered/composited by design, so this is not a gap, but is recorded here for
  completeness).

## 8. PASS / BLOCKED

**Status: PASS** (for this step's own narrow, disclosed scope — graceful polyphony reduction, confidence-
gated contour/onset fallback in the gesture mapper, and genuinely-overlapping-effect frame-quantization
collision handling — not a claim that Phase 06's own separately-tracked, carried-forward BLOCKED verdict is
resolved).

- "Fallback to contour or onsets when pitch unreliable; no invalid effects" (required acceptance, verbatim)
  — proven by `Step051_Acceptance_Passes_LowConfidencePitchFallsBackToContourThenOnset_NoInvalidEffects` (a
  real four-note sequence exercising both fallback tiers, every produced effect still tracing to a verified
  recipe and its own real source note timing) — independently confirmed by the real demo's Scenario 2.
- "Handle polyphony... gracefully" (step title, verbatim) — proven by
  `Polyphony_TwoSimultaneousNotes_ReducedToOneDeterministicLeadLine_NotRejected` and
  `Polyphony_OverlappingDominantNoteStartsLater_TrimsPriorLeadRatherThanDroppingIt` (genuinely overlapping
  input resolved to a valid, non-crashing, deterministic monophonic result, where
  `MelodyTrackValidator` alone would reject it outright) — independently confirmed by the real demo's
  Scenario 1.
- "...frame quantization gracefully"; Section 12: "verify xLights frame snapping doesn't reverse event
  order" (verbatim) — proven by
  `Step051_Acceptance_Passes_OverlappingEffectsOnSameLayer_FrameSnapNeverReversesOrder_NoInvalidEffects` (a
  real, genuinely-overlapping `EffectInstance` pair exported to a real `.xsq` file and round-tripped through
  the real structural reader, confirmed non-colliding and correctly ordered) — independently confirmed by the
  real demo's Scenario 3.
- "Silence, invalid timing boundaries and missing media must be handled explicitly" (failure/edge case,
  verbatim) — proven by `Silence_EmptyTrack_PolyphonyReductionProducesEmptyTrack_NoCrash_EdgeCase`,
  `MissingMedia_EmptySourceAssetHash_ThrowsMelodyTrackInvalidException_NegativePath`,
  `InvalidTimingBoundary_NoteEndNotAfterStart_ThrowsMelodyTrackInvalidException_NegativePath`, and (on the
  export side) `EffectWhoseOwnDurationCollapsesAtFrameGrid_StillFailsSafely_NoFileWritten_NegativePath`.
- Full regression: **718/718 passing** (across the 9 reported assemblies), 0 failed, 0 skipped; every
  pre-existing test from Steps 001-050 still passes unmodified (per-assembly counts match their own Step
  050-era baselines exactly, plus only this step's own new tests).
- **Carried-forward, disclosed phase-level/dependency-level gap (not this step's own blocker):** Phase 06's
  own gate remains BLOCKED (§3/§7, carried forward from Steps 045-050) — no production stem-separation model
  exists in this repository.

### Phase 07 full-phase gate — not separately re-run in this evidence file

Per Section 8's own instruction ("Do not start the next phase when blocked") and per Step 050's own
evidence's own closing note, the full-phase gate (Section 7's full-phase test matrix, the phase gate
demonstration itself — "Import trusted MIDI or simple isolated melody, view notes on piano roll, correct an
incorrect note, select five arches, preview stepping up and reversing on descent, export and validate in
xLights" — and Section 8's required final gate report) is a separate, broader exercise than any one numbered
step's own evidence. This file proves Step 051's own required acceptance text only; it does not itself
constitute a Phase 07 gate PASS claim. Per the user's own standing waiver
(`memory: feedback_skip_xlights_human_verification`), the real-xLights render/desktop-interaction check is
not treated as a blocker for this step's own scope and is disclosed, not fabricated, here — this step's own
required acceptance text does not itself name an xLights-specific manual action (it is a pure domain/adapter
claim, proven directly against the real `MelodicGestureMapper`/`MelodyPolyphonyReducer`/`XsqExporter`, with
the export-side claim additionally round-tripped through a real file and the real `XsqStructuralReader`).
