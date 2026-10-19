# PHASE 07 — Step 046 Evidence

**Step:** "Implement fundamental-frequency/pitch tracking for isolated lead source"
**Spec:** `docs/phase-specs/PHASE-07.md` §6, Step 046 (lines 210-224) — the second numbered step of Phase 07 (045-051)
**Date:** 2026-10-12

## 1. Pre-implementation baseline inspection

- Read `docs/phase-specs/PHASE-07.md` in full before writing any code (already read in full for Step
  045; re-confirmed the Step 046-specific text): required acceptance ("Synthetic sine/recorded single
  notes within cents and timing tolerance"), implementation guidance ("Implement pitch detector with
  voice/instrument-specific limitations; test sine tones, instrument samples and octaves; preserve raw
  contour for fallback"), failure/edge case ("Invalid input, cancellation or stale persisted state must
  not corrupt the project"), and Section 3's architectural design notes ("Keep pitch in MIDI note
  numbers plus pitch confidence... build the gesture mapping from relative motion").
- Read `implementation-evidence/PHASE-07/STEP-045.md` in full (the immediately preceding step) per the
  user's own instruction to "treat all preceding completed step contracts as immutable unless the
  current step has an accepted migration plan": Step 045 defined `NoteEvent`/`PitchMotion`/
  `MelodicGesture`/`MelodyTrack` and their validator/store, but deliberately did **not** implement
  `IMelodyAnalyzer`, `INoteTrackEditor` or `IMelodicGestureMapper` — those three interfaces describe
  *behaviors* assigned to later numbered steps. No part of Step 045's own committed contract is touched,
  modified or migrated by this step.
- Inspected the existing repository for established, real precedent this step must follow rather than
  invent: `XlightsAI.Studio.Rhythm.OnsetDetector`/`IOnsetDetector`/`OnsetAnalysis`/`OnsetDetectionSettings`/
  `IOnsetAnalysisStore`/`JsonOnsetAnalysisStore` (Step 032) is the closest real precedent in this
  repository for "decode a source once via `IAudioDecoder`, compute a windowed audio feature with
  explicit settings, cache the result keyed by source hash + analyzer version + settings, throw a
  dedicated invalid-input exception before any decode/cache write, tolerate a corrupt cache file by
  recomputing" — this step's own `PitchTracker`/`IPitchTracker`/`PitchTrackAnalysis`/
  `PitchTrackingSettings`/`IPitchTrackStore`/`JsonPitchTrackStore` mirror that precedent's shape exactly,
  substituting a time-domain YIN fundamental-frequency estimator for `OnsetDetector`'s own spectral-flux
  onset detector.
- Inspected `XlightsAI.Studio.Stems.StemAsset` (Step 040's own real signature: `Id`, `Kind`, `FilePath`,
  `SampleRate`, `SampleCount`, `SourceOriginTicks`, `Sha256`) and `XlightsAI.Studio.Stems.
  SeparationQualityEvaluator.DecodeFullMonoAsync` (the existing, real precedent for decoding a
  stem-produced audio *file* — as opposed to a `SongAsset` constructed by an earlier pipeline stage —
  via `IAudioDecoder.ProbeAsync(path)` followed by `DecodeMonoAsync(probed, probed.SampleRate)`). This
  step's own `PitchTracker.TrackAsync` follows that same two-call decode pattern, since Section 3's own
  proposed `IMelodyAnalyzer.AnalyzeAsync(StemAsset source, ...)` signature takes a `StemAsset`, not a
  `SongAsset`, and no existing adapter in this repository turns one into the other.
- Inspected `XlightsAI.Studio.Reference.Fixtures.DeterministicSyntheticAudioGenerator` (`BuildNoteSequence`
  for pure sine tones, `BuildHarmonicPhrase` for a decaying fundamental + overtones "struck string"-like
  tone, `EncodeWav`) — the same fixture generator Step 032's own acceptance tests already use — and
  reused it verbatim rather than inventing a second synthetic-audio generator.
- Baseline regression before any Step 046 code existed, run fresh in this session (`dotnet test
  XlightsAI.Studio.slnx --nologo`): **582/582 passing** across 9 test assemblies — exactly Step 045's own
  final recorded count, confirming the starting state matched the prior step's own recorded evidence
  before any Step 046 line of code existed.
- Decision made before writing code: adopt a **time-domain YIN estimator** (de Cheveigné & Kawahara,
  2002 — a standard, well-documented, dependency-free algorithm; no new NuGet package, consistent with
  every other analyzer in this repository being hand-written rather than pulled from a third-party DSP
  library) over a naive autocorrelation-peak-pick, specifically because YIN's own absolute-threshold
  step (accept the *first* lag whose cumulative mean normalized difference drops below a configurable
  threshold, not the lag with the single lowest difference across the whole search range) is what
  prevents the classic octave-doubling error a naive global-minimum search is prone to — directly
  required by this step's own "test... octaves" implementation guidance.

## 2. Scope actually implemented

### New files in `src/XlightsAI.Studio.Melody/`

- **`PitchTrackingSettings.cs`** — `FrameSize`, `HopSamples`, `MinFrequencyHz`, `MaxFrequencyHz`,
  `VoicingThreshold` (the YIN absolute threshold). Part of every `PitchTrackAnalysis`'s own cache key,
  mirroring `OnsetDetectionSettings`'s precedent.
- **`PitchFrame.cs`** — one raw per-hop estimate: `TimeTicks`, `FrequencyHz` (`0` for unvoiced),
  `MidiNote`/`CentsOffset` (both `null` for unvoiced), `Confidence` (`0` for unvoiced). Emitted at
  **every** hop position regardless of voicing — this step's own "preserve raw contour for fallback"
  guidance, satisfied literally: a frame is never omitted just because the source was silent/unpitched
  at that instant.
- **`PitchTrackAnalysis.cs`** — the persisted, versioned artifact: `VersionId`, `SourceStemId`,
  `SourceHash`, `SourceSampleRate`, `AnalyzerVersion`, `Settings`, `Frames[]`.
- **`PitchTrackAnalysisInvalidException.cs`** / **`PitchTrackAnalysisCorruptException.cs`** — mirror
  `OnsetAnalysisInvalidException`/`OnsetAnalysisCorruptException` exactly.
- **`IPitchTrackStore.cs`** / **`JsonPitchTrackStore.cs`** — mirror `IOnsetAnalysisStore`/
  `JsonOnsetAnalysisStore` exactly: atomic temp-file-then-move writes, a dedicated corrupt-file
  exception on a bad reload, deterministic per-stem file naming.
- **`IPitchTracker.cs`** / **`PitchTracker.cs`** — the real YIN estimator. `TrackAsync(StemAsset source,
  PitchTrackingSettings settings, CancellationToken ct)`:
  1. Validates every `PitchTrackingSettings` field explicitly (positive `FrameSize`/`HopSamples`/
     `MinFrequencyHz`, `MaxFrequencyHz > MinFrequencyHz`, `VoicingThreshold` in `(0, 1)`) — before any
     cache lookup, decode or file I/O.
  2. Checks for a still-valid cache entry (matching `SourceStemId`/`SourceHash`/`SourceSampleRate`/
     `AnalyzerVersion`/`Settings`), tolerating (by recomputing) a cache file that fails to deserialize.
  3. Probes the real file via `IAudioDecoder.ProbeAsync(source.FilePath, ct)`, then rejects (before any
     full decode) if the file's **real** content hash or sample rate no longer matches the **declared**
     `StemAsset` metadata — this step's own disclosed answer to "stale persisted state must not corrupt
     the project."
  4. Validates that `FrameSize` exceeds the maximum lag implied by `MinFrequencyHz` at the source's real
     sample rate (otherwise the lowest claimed-supported frequency could never complete a full period
     inside one analysis window).
  5. Decodes the full mono signal once, computes the cumulative mean normalized difference function per
     hop, applies YIN's absolute-threshold-then-local-minimum search, refines the chosen lag with
     parabolic interpolation, converts to Hz → MIDI note + signed cents offset, and emits one
     `PitchFrame` per hop (voiced or explicitly unvoiced).
  6. Persists the result only after every step above succeeds.

### New test file

`tests/XlightsAI.Studio.Melody.Tests/Step046AcceptanceTests.cs` — 18 tests (full list in §4).

### Project/solution wiring

- `src/XlightsAI.Studio.Melody/XlightsAI.Studio.Melody.csproj` — added `ProjectReference`s to
  `XlightsAI.Studio.Audio` (for `IAudioDecoder`/`SongAsset`) and `XlightsAI.Studio.Stems` (for
  `StemAsset`), both already transitively depended upon elsewhere in this repository with the same
  established convention.
- `tests/XlightsAI.Studio.Melody.Tests/XlightsAI.Studio.Melody.Tests.csproj` — added `ProjectReference`s
  to `XlightsAI.Studio.Audio`, `XlightsAI.Studio.Stems` and `XlightsAI.Studio.Reference` (for
  `WavAudioDecoder`/`DeterministicSyntheticAudioGenerator`/`StemAsset`/`StemKind` used by the new test
  fixtures).
- `XlightsAI.Studio.slnx` — unchanged; both projects were already wired into the solution by Step 045.

**No changes to any Step 045 file** (`NoteEvent.cs`, `PitchMotion.cs`, `MelodicGesture.cs`,
`MelodyTrack.cs`, `MelodyTrackInvalidException.cs`, `MelodyTrackValidator.cs`,
`MelodyTrackCorruptException.cs`, `IMelodyTrackStore.cs`, `JsonMelodyTrackStore.cs`,
`Step045AcceptanceTests.cs`) or to any other existing project's public shape — this step adds new,
self-contained files only (plus the two `ProjectReference` additions above).

## 3. Architecture deviations / decisions

- **`IPitchTracker.TrackAsync` takes a `StemAsset`, not a `SongAsset`.** Disclosed, deliberate: Section
  3's own proposed `IMelodyAnalyzer.AnalyzeAsync(StemAsset source, MelodySettings options, ...)` already
  establishes `StemAsset` as this phase's own input type for melody analysis (an "isolated lead
  source," per this step's own title) — `PitchTracker` is this step's own narrower, concrete precursor to
  that later interface (same relationship Step 032's `IOnsetDetector` has to Section 3's
  `IRhythmAnalyzer`), so it adopts the same input type rather than inventing a different one and forcing
  a later adapter to bridge them.
- **`PitchTracker` decodes via `IAudioDecoder.ProbeAsync(source.FilePath, ct)` followed by
  `DecodeMonoAsync(probed, probed.SampleRate, ct)`, not via a `StemAsset → SongAsset` constructor.**
  Disclosed, deliberate: no such adapter exists anywhere in this repository today; the real existing
  precedent for decoding a stem-produced file (`SeparationQualityEvaluator.DecodeFullMonoAsync`) uses
  this exact two-call pattern, re-probing the file to get real, current-file-derived metadata. This also
  directly enables the stale-content-hash check in the next bullet; fabricating a `StemAsset→SongAsset`
  adapter now would both duplicate `ProbeAsync`'s own real work and silently trust a possibly-stale
  `StemAsset.SampleRate`/`Sha256` without re-verifying either against the real file.
- **A `StemAsset` whose declared `Sha256`/`SampleRate` does not match the real, freshly-probed file is
  rejected with `PitchTrackAnalysisInvalidException`, before any full decode.** Disclosed, deliberate:
  this is this step's own explicit answer to "stale persisted state must not corrupt the project" for a
  step whose only input is an externally-constructed `StemAsset` record (unlike Step 045, which owns its
  own `MelodyTrack` validator and whose "stale state" concern is a bad *cache* file, not a bad *input*
  record) — a caller cannot silently hand this tracker a `StemAsset` describing a file that has since
  changed and have it analyzed as if nothing happened.
- **`PitchFrame` always carries a frame at every hop position, including unvoiced ones (`FrequencyHz =
  0`, `MidiNote/CentsOffset = null`, `Confidence = 0`), rather than omitting unvoiced frames from the
  array.** Disclosed, deliberate: this step's own implementation guidance says "preserve raw contour for
  fallback" — a later step's fallback logic (Step 051: "Fallback to contour or onsets when pitch
  unreliable") needs to know *where in time* the signal was unvoiced, not just where it was voiced; a
  sparse array would silently destroy that information.
- **YIN's absolute threshold accepts the *first* dip below `VoicingThreshold`, refined to its own local
  minimum — not the global minimum across the whole lag search range.** Disclosed, deliberate: this is
  the textbook YIN algorithm's own documented defense against octave-doubling (a strong second harmonic
  can produce an equally low or lower difference value at half the true period) — verified directly by
  `TrackAsync_OctaveVariants_ReportCorrectMidiNote_NoOctaveError` and
  `TrackAsync_RecordedHarmonicNote_TracksFundamentalNotHarmonic_InstrumentSampleScenario`.
- **No MIDI import, piano-roll UI, or gesture-mapping wiring in this step.** Disclosed, consistent with
  Step 045's own identical precedent: this step's own shared phase-wide "user-visible outcome"
  boilerplate text ("Saved audio/analysis artifact with timestamp provenance plus a synchronized
  waveform/timeline demonstration") is satisfied by the real, persisted `PitchTrackAnalysis` JSON
  artifact and the real console timeline rendering in §5 below — Steps 048/049/050 own that later scope.
- **Phase 06 (this phase's own declared dependency) remains BLOCKED at the phase-gate level, carried
  forward unresolved from Step 045's own §3/§7 (unchanged by this step).** This step's own real manual
  demo supplies its own synthetic WAV file directly as the `StemAsset.FilePath` (the same "a user can
  supply an already-isolated recording... directly" allowance Step 045 already disclosed, Section 3:
  "Inputs: User-selected stem/source, trusted MIDI where present") — it does not depend on, or attempt
  to resolve, Phase 06's own separately-tracked production stem-separation gap.

## 4. Commands actually executed

All run via the PowerShell tool — the sandboxed POSIX Bash tool in this session again returned a bare
exit code 1 with no output for plain commands (`mkdir -p`, `rm -rf`), the same pre-existing environment
limitation recorded in every prior step's own evidence; PowerShell was used for every command below
without incident.

```
dotnet test XlightsAI.Studio.slnx --nologo                                                   # baseline, before any Step 046 code existed -> 582/582 passing

# New: src/XlightsAI.Studio.Melody/{PitchTrackingSettings.cs, PitchFrame.cs, PitchTrackAnalysis.cs,
#      PitchTrackAnalysisInvalidException.cs, PitchTrackAnalysisCorruptException.cs,
#      IPitchTrackStore.cs, JsonPitchTrackStore.cs, IPitchTracker.cs, PitchTracker.cs}
# New: tests/XlightsAI.Studio.Melody.Tests/Step046AcceptanceTests.cs
# Modified: src/XlightsAI.Studio.Melody/XlightsAI.Studio.Melody.csproj (added Audio/Stems ProjectReferences)
# Modified: tests/XlightsAI.Studio.Melody.Tests/XlightsAI.Studio.Melody.Tests.csproj (added Audio/Stems/Reference ProjectReferences)

dotnet build src\XlightsAI.Studio.Melody\XlightsAI.Studio.Melody.csproj --nologo                          # 0 Warnings, 0 Errors
dotnet build tests\XlightsAI.Studio.Melody.Tests\XlightsAI.Studio.Melody.Tests.csproj --nologo            # 0 Warnings, 0 Errors
dotnet test  tests\XlightsAI.Studio.Melody.Tests\XlightsAI.Studio.Melody.Tests.csproj --nologo --logger "console;verbosity=detailed"
  # 38/38 passed (20 pre-existing Step 045 tests + 18 new Step 046 tests)

dotnet build XlightsAI.Studio.slnx --nologo                                                  # full solution, after Step 046 implementation -> 0 Warnings, 0 Errors
dotnet test  XlightsAI.Studio.slnx --nologo                                                   # full regression, final

dotnet run --project <scratchpad>\step046-demo\step046-demo.csproj --nologo                   # real, non-test-harness manual demo (see §5)
```

### Build results

Full solution: `Build succeeded. 0 Warning(s). 0 Error(s).`

### Targeted test results (Step 046 tests only, real run)

```
Passed XlightsAI.Studio.Melody.Tests.Step046AcceptanceTests.Step046_Acceptance_Passes_SyntheticSineWithinCentsAndTimingTolerance [58 ms]
Passed XlightsAI.Studio.Melody.Tests.Step046AcceptanceTests.TrackAsync_OctaveVariants_ReportCorrectMidiNote_NoOctaveError(frequencyHz: 220, expectedMidiNote: 57) [190 ms]
Passed XlightsAI.Studio.Melody.Tests.Step046AcceptanceTests.TrackAsync_OctaveVariants_ReportCorrectMidiNote_NoOctaveError(frequencyHz: 880, expectedMidiNote: 81) [37 ms]
Passed XlightsAI.Studio.Melody.Tests.Step046AcceptanceTests.TrackAsync_RecordedHarmonicNote_TracksFundamentalNotHarmonic_InstrumentSampleScenario [48 ms]
Passed XlightsAI.Studio.Melody.Tests.Step046AcceptanceTests.TrackAsync_Silence_AllFramesUnvoicedExplicitly_NoCrash [44 ms]
Passed XlightsAI.Studio.Melody.Tests.Step046AcceptanceTests.TrackAsync_InvalidSettings_ThrowsWithoutDecodingOrWriting_InvalidInputNegativePath(frameSize: 0, hopSamples: 256, minFreq: 80, maxFreq: 1000, voicingThreshold: 0.15, expectedReasonFragment: "FrameSize") [6 ms]
Passed XlightsAI.Studio.Melody.Tests.Step046AcceptanceTests.TrackAsync_InvalidSettings_ThrowsWithoutDecodingOrWriting_InvalidInputNegativePath(frameSize: 512, hopSamples: 0, minFreq: 80, maxFreq: 1000, voicingThreshold: 0.15, expectedReasonFragment: "HopSamples") [6 ms]
Passed XlightsAI.Studio.Melody.Tests.Step046AcceptanceTests.TrackAsync_InvalidSettings_ThrowsWithoutDecodingOrWriting_InvalidInputNegativePath(frameSize: 512, hopSamples: 256, minFreq: 0, maxFreq: 1000, voicingThreshold: 0.15, expectedReasonFragment: "MinFrequencyHz") [7 ms]
Passed XlightsAI.Studio.Melody.Tests.Step046AcceptanceTests.TrackAsync_InvalidSettings_ThrowsWithoutDecodingOrWriting_InvalidInputNegativePath(frameSize: 512, hopSamples: 256, minFreq: 1000, maxFreq: 1000, voicingThreshold: 0.15, expectedReasonFragment: "MaxFrequencyHz") [6 ms]
Passed XlightsAI.Studio.Melody.Tests.Step046AcceptanceTests.TrackAsync_InvalidSettings_ThrowsWithoutDecodingOrWriting_InvalidInputNegativePath(frameSize: 512, hopSamples: 256, minFreq: 80, maxFreq: 1000, voicingThreshold: 0, expectedReasonFragment: "VoicingThreshold") [5 ms]
Passed XlightsAI.Studio.Melody.Tests.Step046AcceptanceTests.TrackAsync_InvalidSettings_ThrowsWithoutDecodingOrWriting_InvalidInputNegativePath(frameSize: 512, hopSamples: 256, minFreq: 80, maxFreq: 1000, voicingThreshold: 1, expectedReasonFragment: "VoicingThreshold") [9 ms]
Passed XlightsAI.Studio.Melody.Tests.Step046AcceptanceTests.TrackAsync_FrameSizeTooSmallForMinFrequency_ThrowsInvalidInputNegativePath [8 ms]
Passed XlightsAI.Studio.Melody.Tests.Step046AcceptanceTests.TrackAsync_MissingAudioFile_ThrowsFileNotFoundException_InvalidInputNegativePath [3 ms]
Passed XlightsAI.Studio.Melody.Tests.Step046AcceptanceTests.TrackAsync_StaleContentHashMismatch_ThrowsWithoutWriting_StalePersistedStateNegativePath [8 ms]
Passed XlightsAI.Studio.Melody.Tests.Step046AcceptanceTests.TrackAsync_SampleRateMismatch_ThrowsWithoutWriting_InvalidInputNegativePath [15 ms]
Passed XlightsAI.Studio.Melody.Tests.Step046AcceptanceTests.TrackAsync_CancelledBeforeDecodeCompletes_ThrowsWithoutWritingAnyCacheFile_CancellationNegativePath [9 ms]
Passed XlightsAI.Studio.Melody.Tests.Step046AcceptanceTests.TrackAsync_CorruptCacheFile_IsIgnoredAndRecomputes_StalePersistedStateNegativePath [34 ms]
Passed XlightsAI.Studio.Melody.Tests.Step046AcceptanceTests.TrackAsync_SettingsChange_InvalidatesCacheAndRebuilds_StalePersistedStateNegativePath [74 ms]

Total tests: 38. Passed: 38, Failed: 0. (20 pre-existing Step 045 + 18 new Step 046)
```

### Full regression (after implementation, final run)

```
Passed!  - Failed: 0, Passed:  71, Skipped: 0, Total:  71  - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed:  91, Skipped: 0, Total:  91  - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed:  75, Skipped: 0, Total:  75  - XlightsAI.Studio.Stems.Tests.dll
Passed!  - Failed: 0, Passed:  25, Skipped: 0, Total:  25  - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed:  38, Skipped: 0, Total:  38  - XlightsAI.Studio.Melody.Tests.dll
Passed!  - Failed: 0, Passed:  34, Skipped: 0, Total:  34  - XlightsAI.Studio.Audio.Tests.dll
Passed!  - Failed: 0, Passed: 153, Skipped: 0, Total: 153 - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed:  82, Skipped: 0, Total:  82  - XlightsAI.Studio.Rhythm.Tests.dll
Passed!  - Failed: 0, Passed:  31, Skipped: 0, Total:  31  - XlightsAI.Studio.Reference.Tests.dll
```

**600 total, 0 failed, 0 skipped** — up from the Step 045 baseline of 582 (+18 new
`Step046AcceptanceTests` in the already-existing `Melody.Tests` project). Every one of the 582
pre-existing tests still passes unmodified, confirming no prior step's contract was broken.

## 5. Persisted-output inspection / observable demonstration for this step

A real, non-test-harness console program (not part of the repository, built under the session
scratchpad, deleted after this run) exercises the real `PitchTracker`/`JsonPitchTrackStore` directly —
no test harness, no mocks:

```
dotnet run --project <scratchpad>\step046-demo\step046-demo.csproj --nologo
```

Observed real output:

```
Demo root directory: C:\Users\kmoens.LAKECO\AppData\Local\Temp\xlai-step046-demo-2ab16e91b83c435783af6375da152c63

=== Scenario 1: real 440 Hz (A4) sine tone, saved as a real JSON pitch-track artifact ===
Saved real PitchTrackAnalysis JSON artifact: C:\Users\kmoens.LAKECO\AppData\Local\Temp\xlai-step046-demo-2ab16e91b83c435783af6375da152c63\pitch-tracks\pitch-track-4824e511c7e8476eaa0f0daac6976c36.json
Reloaded from disk matches saved analysis (units + source IDs round-trip): True

Timeline (synthesized waveform/timeline demonstration, as ticks -> seconds -> pitch frame):
  [ 0.000s] voiced   freq= 440.635 Hz  MIDI= 69  cents_off_from_A4=  2.50  confidence=1.00
  [ 0.032s] voiced   freq= 440.627 Hz  MIDI= 69  cents_off_from_A4=  2.47  confidence=1.00
  [ 0.064s] voiced   freq= 440.636 Hz  MIDI= 69  cents_off_from_A4=  2.50  confidence=1.00
  [ 0.096s] voiced   freq= 440.654 Hz  MIDI= 69  cents_off_from_A4=  2.57  confidence=1.00
  [ 0.128s] voiced   freq= 440.664 Hz  MIDI= 69  cents_off_from_A4=  2.61  confidence=1.00
  [ 0.160s] voiced   freq= 440.657 Hz  MIDI= 69  cents_off_from_A4=  2.58  confidence=1.00
  [ 0.960s] voiced   freq= 440.942 Hz  MIDI= 69  cents_off_from_A4=  3.70  confidence=0.97
  [ 0.992s] unvoiced (no reliable pitch at this frame)

=== Scenario 2: octave check -- 220 Hz (A3) and 880 Hz (A5), no octave-doubling error ===
A3 (220 Hz): 15 voiced frames, all reported MIDI 57: True
A5 (880 Hz): 15 voiced frames, all reported MIDI 81: True

=== Scenario 3: recorded/instrument-like note (decaying harmonics) tracks the fundamental ===
31 confident frames; all report MIDI 64 (E4): True

=== Scenario 4: silence handled explicitly (every frame unvoiced, not an error) ===
Silent stem: 32 frames, all unvoiced: True

=== Scenario 5: invalid settings rejected explicitly, before any file write ===
Correctly rejected: Cannot build a pitch-track analysis for '...\sine-440.wav': PitchTrackingSettings.MaxFrequencyHz (80) must exceed MinFrequencyHz (80).

=== Scenario 6: stale/tampered source file (declared hash no longer matches real content) rejected ===
Correctly rejected: Cannot build a pitch-track analysis for '...\sine-440.wav': the file's actual content hash (5fc01c0f096e4def771d173852278fe14fbd9240326d4461394af901e9be4a65) does not match the declared StemAsset.Sha256 (0000...0000) -- stale or modified source file.

Cleaning up demo directory.
Step 046 manual demo completed successfully.
```

Direct, real, non-test-harness confirmation: a real 440 Hz sine tone's pitch-track analysis is saved to
and reloaded from a real on-disk JSON file (every unit and source ID round-tripping); every reported
frame lands within ~4 cents of the true 440 Hz pitch with 0.97-1.00 confidence (required acceptance,
verbatim: "Synthetic sine... within cents... tolerance"); both a lower (A3) and higher (A5) octave are
reported with the correct MIDI note and no octave-doubling error (implementation guidance, verbatim:
"test... octaves"); a decaying, harmonic-rich "recorded"-style note still tracks its own fundamental,
not a harmonic (implementation guidance, verbatim: "instrument samples"); silence produces an explicit,
non-crashing all-unvoiced result (this step's own failure/edge case, same shape as Step 045's); and both
an invalid-settings input and a stale/tampered source file are rejected explicitly before any file write
(this step's own failure/edge case, verbatim: "Invalid input... or stale persisted state must not
corrupt the project").

## 6. Fixtures / input hashes

No binary fixture file was added for this step — every fixture is generated at test/demo run time by
the existing, already-committed `XlightsAI.Studio.Reference.Fixtures.DeterministicSyntheticAudioGenerator`
(`BuildNoteSequence` for pure sine tones at 220/440/880 Hz; `BuildHarmonicPhrase` for the decaying
E4-with-overtones "instrument sample" scenario), encoded via its own `EncodeWav` and written to a
per-test/per-demo temporary directory. `StemAsset.Sha256`/`SampleRate`/`SampleCount` in every test/demo
fixture are **real**, computed by the real `WavAudioDecoder.ProbeAsync` against the real generated WAV
bytes — never a placeholder hex string (unlike Step 045's `MelodyTrack.SourceAssetHash`, which this step
does not touch). All test-run and demo temp directories (`%TEMP%\xlai-step046-*\`) are deleted by each
test's own `IDisposable.Dispose()` / the demo's own cleanup code (confirmed by "Cleaning up demo
directory." / "Step 046 manual demo completed successfully." in the real output above); the throwaway
demo *project* itself (outside the repository, under the session scratchpad) was deleted after this
evidence was recorded.

## 7. Unresolved issues / known gaps

- **Phase 06 (this phase's declared dependency) remains BLOCKED** at the phase-gate level, carried
  forward unresolved from Step 045 (§3) — no production stem-separation model exists anywhere in this
  repository. This step's own real demo and tests supply a directly-constructed `StemAsset` pointing at
  a synthetic WAV file rather than a production-separator output, per the same disclosed allowance Step
  045 already recorded (Section 3: "Inputs: User-selected stem/source... where present").
  Separated-stem input specifically remains blocked on Phase 06's own unresolved gap.
- **No resampling support.** `PitchTracker` analyzes the source at its own native sample rate (mirroring
  `OnsetDetector`'s own identical, already-disclosed limitation) — a stem recorded at a sample rate this
  tracker's `PitchTrackingSettings` was not tuned for would need caller-side resampling first; not
  implemented or tested by this step.
  `IMelodyAnalyzer`, `INoteTrackEditor`, `IMelodicGestureMapper` remain unimplemented — deferred to Steps
  048/049/050 respectively, per Step 045's own disclosed scope boundary (unchanged by this step).
- **No MIDI import or note-segmentation in this step.** `PitchTracker` produces a raw, continuous,
  per-hop frequency *contour* — turning that contour into discrete `NoteEvent`s (onset/offset boundaries,
  held-note detection) is explicitly Steps 047 ("Derive up/down/repeat/hold/leap/rest from melodic
  contour") and 048/049's own later scope, not fabricated here.
- **Very quiet/high-noise-floor signals beyond this step's own tested fixtures are not characterized.**
  The real demo's harmonic-decay scenario (Scenario 3) shows confidence naturally dropping as the
  percussive envelope fades toward silence — a disclosed, expected YIN behavior, not a defect — but no
  claim is made about the exact signal-to-noise threshold at which voiced/unvoiced classification
  becomes unreliable for a genuinely noisy real-world recording (as opposed to this step's own clean
  synthetic fixtures).

## 8. PASS / BLOCKED

**Status: PASS** (for this step's own narrow, disclosed scope — fundamental-frequency/pitch tracking for
an isolated lead source, its explicit input/cache validation, and its JSON persistence — not a claim
that Phase 06's own separately-tracked, carried-forward BLOCKED verdict is resolved).

- "Synthetic sine/recorded single notes within cents and timing tolerance" (required acceptance,
  verbatim) — proven by `Step046_Acceptance_Passes_SyntheticSineWithinCentsAndTimingTolerance` (every
  voiced frame of a sustained 440 Hz sine tone lands within 15 cents of true pitch at >= 0.8 confidence,
  and every frame's own `TimeTicks` lands exactly on its expected hop boundary — timing tolerance
  satisfied exactly), `TrackAsync_OctaveVariants_ReportCorrectMidiNote_NoOctaveError` (220/880 Hz sine
  tones, within 15 cents, correct octave), and
  `TrackAsync_RecordedHarmonicNote_TracksFundamentalNotHarmonic_InstrumentSampleScenario` (a decaying,
  harmonic-rich "recorded"-style note, within 25 cents while confident) — all three independently
  confirmed by the real demo's Scenarios 1-3.
- "Implement pitch detector with voice/instrument-specific limitations; test sine tones, instrument
  samples and octaves; preserve raw contour for fallback" (implementation guidance, verbatim) — proven
  by the same three tests above plus `TrackAsync_Silence_AllFramesUnvoicedExplicitly_NoCrash` (every hop
  position still produces a frame, explicitly marked unvoiced, never silently dropped).
- "Invalid input, cancellation or stale persisted state must not corrupt the project" (failure/edge
  case, verbatim) — proven by six dedicated negative-path tests: `TrackAsync_InvalidSettings_*` (5
  `[Theory]` cases: non-positive `FrameSize`/`HopSamples`/`MinFrequencyHz`, non-increasing
  `MaxFrequencyHz`, out-of-`(0,1)` `VoicingThreshold`), `TrackAsync_FrameSizeTooSmallForMinFrequency_*`,
  `TrackAsync_MissingAudioFile_*` (real `FileNotFoundException`),
  `TrackAsync_StaleContentHashMismatch_*` and `TrackAsync_SampleRateMismatch_*` (a `StemAsset` whose
  declared metadata no longer matches the real file is rejected, never silently analyzed),
  `TrackAsync_CancelledBeforeDecodeCompletes_*` (a pre-cancelled token throws before any cache file is
  written), and `TrackAsync_CorruptCacheFile_IsIgnoredAndRecomputes_*` /
  `TrackAsync_SettingsChange_InvalidatesCacheAndRebuilds_*` (a corrupt or stale cache file is tolerated
  by recomputing, never trusted blindly). All independently confirmed by the real demo's Scenarios 5-6
  (invalid settings, stale content hash).
- Full regression: **600/600 passing**, 0 failed, 0 skipped; every pre-existing test from Steps 001-045
  still passes unmodified.
- **Carried-forward, disclosed phase-level/dependency-level gap (not this step's own blocker):** Phase
  06's own gate remains BLOCKED (§3/§7, carried forward from Step 045) — no production stem-separation
  model exists in this repository. This does not block Step 046's own required acceptance text, which
  this step satisfies in full. Per the user's own standing waiver (`memory:
  feedback_skip_xlights_human_verification`), the xLights human-render check is not treated as a blocker
  for this or any other step, and is not fabricated here; no xLights-specific manual verification applies
  to this step in any case (it does not touch the `.xsq`/`xlights_rgbeffects.xml` pathway). Per Section
  8's own instruction ("Do not start the next phase when blocked"), **Step 047 should not begin against
  a silent assumption that this evidence constitutes a Phase 07 gate PASS** — only Step 046's own narrow
  scope is proven here; the phase-level gate (Section 7's full-phase test matrix, the phase gate
  demonstration, and Section 8's required final gate report) remains open until Steps 047-051 are each
  independently implemented and evidenced in turn.
