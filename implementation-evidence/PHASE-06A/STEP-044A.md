# PHASE 06A — Step 044A Evidence

**Step:** "Add real stereo audio decode support"
**Spec:** `docs/phase-specs/PHASE-06A.md` §6, Step 044A — the first numbered step of this addendum (044A-044F), inserted between Phase 06's Step 044 and Phase 07's Step 045
**Date:** 2026-10-13

## 1. Pre-implementation baseline inspection

- Read `docs/phase-specs/PHASE-06A.md` in full before writing any code: required acceptance ("A real
  stereo WAV fixture (left channel = one known frequency tone, right channel = a different known
  frequency tone) is decoded via `IStereoAudioDecoder`; each decoded channel's own
  independently-measured frequency... matches its own known tone within tolerance... proving channels
  are read independently, never swapped or downmixed"), implementation guidance ("Add a new,
  phase-local `IStereoAudioDecoder`/`WavStereoAudioDecoder` sibling... Reuse `WavAudioDecoder`'s own
  established RIFF/WAVE chunk-parsing routines rather than re-implementing header parsing a second
  time"), and failure/edge case ("A mono (1-channel) or >2-channel source... throws
  `StereoAudioDecodeChannelMismatchException` before any sample data is read; a truncated/corrupt
  stereo file is rejected the same way `WavAudioDecoder.ProbeAsync` already rejects a truncated mono
  file (reused validation, not reinvented)").
- Read §3's proposed `IStereoAudioDecoder`/`StereoSampleBlock` C# signatures and the architectural
  design note explaining why this is a disclosed sibling to `IAudioDecoder`, not a retrofit — the same
  relationship Phase 07 Step 048 already established for `IMidiTranscriptionBackend` relative to
  `IMelodyAnalyzer`.
- Inspected every existing file in `src/XlightsAI.Studio.Audio/` before writing anything:
  `IAudioDecoder.cs` (its sole method is literally named `DecodeMonoAsync`), `WavAudioDecoder.cs` (its
  real RIFF/WAVE chunk-parsing: `ReadHeaderAsync`/`ReadSample`/`WavHeader`, all `private` before this
  step), `SongAsset.cs` (`Channels` already a declared field, nothing to add), `AudioProbeInvalidException.cs`
  (the existing truncation/corruption exception this step reuses, not reinvents).
- Confirmed no prior phase/step already declares `IStereoAudioDecoder`, `StereoSampleBlock`, or
  `StereoAudioDecodeChannelMismatchException` anywhere in the repository (grepped `src/*/*.cs`).
- Baseline regression before any Step 044A code existed, run fresh in this session
  (`dotnet test XlightsAI.Studio.slnx --nologo`): **654/654 passing** across 8 test assemblies (no
  `Stems`-dependent count change since Step 049 — this addendum's own Steps 044B-044F have not yet
  touched `Stems`), confirming the starting state matched Phase 07 Step 049's own final recorded count
  before any Step 044A line of code existed.

## 2. Scope actually implemented

### Modified file (visibility-only, zero behavior change)

- **`src/XlightsAI.Studio.Audio/WavAudioDecoder.cs`** — three members widened from `private` to
  `internal`: `ReadSample`, `ReadHeaderAsync`, and the `WavHeader` record struct. No signature,
  parameter, return type, or body of any of these changed by a single character — this is a
  visibility-only edit so `WavStereoAudioDecoder` (a new sibling class in the same project/assembly)
  can call the exact same RIFF/WAVE chunk-parsing routine rather than re-implementing header parsing a
  second time, per the step's own implementation guidance verbatim. `IAudioDecoder`'s own public
  contract and every existing caller/test of `WavAudioDecoder` is untouched — confirmed by the full
  pre-existing 34-test `Audio.Tests` suite passing unmodified both before and after this change (§4).

### New files in `src/XlightsAI.Studio.Audio/`

- **`StereoSampleBlock.cs`** — `readonly record struct StereoSampleBlock(ReadOnlyMemory<float> Left,
  ReadOnlyMemory<float> Right)`, exactly as proposed in the addendum spec §3.
- **`StereoAudioDecodeChannelMismatchException.cs`** — thrown with the actual observed channel count,
  before any sample data is read.
- **`IStereoAudioDecoder.cs`** — the phase-local sibling interface to `IAudioDecoder`, exactly as
  proposed: `IAsyncEnumerable<StereoSampleBlock> DecodeStereoAsync(SongAsset song, CancellationToken ct)`.
- **`WavStereoAudioDecoder.cs`** — the real implementation. Validates `song.Channels == 2` (and
  defensively re-validates the freshly-read header's own `Channels` field, in case the on-disk file
  changed after the `SongAsset` was probed) before touching any sample bytes; reuses
  `WavAudioDecoder.ReadHeaderAsync`/`ReadSample` unchanged; deinterleaves left/right samples per block
  (mirroring `WavAudioDecoder.DecodeMonoAsync`'s own block-read/truncation-check shape exactly, except
  producing two channel arrays instead of one averaged-mono array).

### Modified file (additive only)

- **`src/XlightsAI.Studio.Reference/Fixtures/DeterministicSyntheticAudioGenerator.cs`** — added
  `EncodeStereoWav(float[] left, float[] right, int sampleRate)`, a new, additive sibling to the
  existing `EncodeWav` (mono) method. `EncodeWav` itself is not modified; every existing caller/test of
  this shared fixture generator across every prior phase is unaffected (confirmed by the full
  pre-existing 31-test `Reference.Tests` suite passing unmodified, §4).

### New test file

- `tests/XlightsAI.Studio.Audio.Tests/Step044AAcceptanceTests.cs` — 4 tests (full list in §4), including
  the required `Step044A_Acceptance_Passes_ChannelsDecodeIndependentlyAtTheirOwnKnownFrequency` test.

**No changes to `IAudioDecoder.cs`, `SongAsset.cs`, `AudioProbeInvalidException.cs`, or any `Stems`/
`Melody`/`Shell` file.** No `.csproj`/`.slnx` changes were needed — the new Audio-project files depend
only on types already inside `XlightsAI.Studio.Audio`/`XlightsAI.Studio.Contracts`, and
`Audio.Tests.csproj` already references `XlightsAI.Studio.Reference`.

## 3. Architecture deviations / decisions

- **`WavAudioDecoder`'s private header/sample-read helpers were widened to `internal`, not duplicated.**
  Disclosed, deliberate, exactly as the step's own implementation guidance requires ("Reuse
  `WavAudioDecoder`'s own established RIFF/WAVE chunk-parsing routines rather than re-implementing
  header parsing a second time"). This is a pure accessibility change — same assembly, same
  signatures, same bodies, same behavior — not a retrofit of `IAudioDecoder`'s own public contract,
  which remains exactly as Phase 04 Step 024 left it.
- **`WavStereoAudioDecoder` re-validates the header's own `Channels` field defensively, in addition to
  the `SongAsset.Channels` check.** Disclosed, deliberate: a `SongAsset` is an immutable record of a
  *previous* probe; if the underlying file were replaced on disk between probing and decoding (a
  known, already-disclosed risk this repository's own `SongAsset.FilePath` doc comment names — "audio
  file moved after import" — Section 7 of Phase 04), this second check is what actually prevents
  misreading channel-interleaved bytes under a stale channel-count assumption, rather than trusting a
  value that could be out of date.
- **No resampling.** `IStereoAudioDecoder.DecodeStereoAsync` decodes at the source's own native sample
  rate only, the same disclosed scope boundary `IAudioDecoder.DecodeMonoAsync` already carries (no
  step before this one needs real resampling either).
- **Frequency verification uses a zero-crossing-rate estimate, not an FFT.** Disclosed, deliberate:
  Step 044B (not yet implemented) is where this addendum's own real STFT/FFT machinery is introduced;
  using it to verify Step 044A's own decode correctness would create a premature, circular dependency
  between a step and a later step's own not-yet-proven implementation. A zero-crossing-rate estimate is
  simple, independently verifiable, and more than sufficient to distinguish a 440 Hz tone from an 880
  Hz tone unambiguously (a channel swap or downmix would fail this assertion outright, which is the
  actual property this step's required acceptance needs proven).
- **`DeterministicSyntheticAudioGenerator.EncodeStereoWav` was added as a new, separate method, not a
  parameterized extension of `EncodeWav`.** Disclosed: `EncodeWav`'s own signature/behavior is already
  relied upon, unmodified, by every prior phase's own fixtures; adding an optional second-channel
  parameter to it would risk a subtle behavior change for existing callers (even a default-parameter
  addition changes the public signature every existing call site compiles against). A new, sibling
  method with no shared call sites is the safer, fully non-breaking choice.

## 4. Commands actually executed

All run via the PowerShell tool — the sandboxed POSIX Bash tool in this session again returned a bare
exit code 1 with no output for a plain `echo test` command, the same pre-existing environment
limitation recorded in every prior step's own evidence; PowerShell was used for every command below
without incident.

```
dotnet test XlightsAI.Studio.slnx --nologo                                                   # baseline, before any Step 044A code existed -> 654/654 passing

# Modified: src/XlightsAI.Studio.Audio/WavAudioDecoder.cs (visibility-only: ReadSample, ReadHeaderAsync, WavHeader -> internal)
# New: src/XlightsAI.Studio.Audio/{StereoSampleBlock.cs, StereoAudioDecodeChannelMismatchException.cs,
#      IStereoAudioDecoder.cs, WavStereoAudioDecoder.cs}
# Modified (additive only): src/XlightsAI.Studio.Reference/Fixtures/DeterministicSyntheticAudioGenerator.cs (new EncodeStereoWav method)
# New: tests/XlightsAI.Studio.Audio.Tests/Step044AAcceptanceTests.cs
# No .csproj / .slnx changes required.

dotnet build src\XlightsAI.Studio.Audio\XlightsAI.Studio.Audio.csproj --nologo                # 0 Warnings, 0 Errors

dotnet test  tests\XlightsAI.Studio.Audio.Tests\XlightsAI.Studio.Audio.Tests.csproj --nologo --logger "console;verbosity=normal"
  # 38/38 passed (34 pre-existing Step 024/025/026/028 tests + 4 new Step 044A tests) -- first run, no fixes needed

dotnet build XlightsAI.Studio.slnx --nologo                                                   # full solution, after Step 044A implementation -> 0 Warnings, 0 Errors
dotnet test  XlightsAI.Studio.slnx --nologo                                                    # full regression, final

dotnet run --project <scratchpad>\xlai-step044a-demo-proj --nologo                            # real, non-test-harness manual demo (see §5)
```

### Build results

Both the targeted `Audio` project and the full solution: `Build succeeded. 0 Warning(s). 0 Error(s).`

### Targeted test results (Step 044A tests only, real run, first attempt)

```
Passed XlightsAI.Studio.Audio.Tests.Step044AAcceptanceTests.Step044A_Acceptance_Passes_ChannelsDecodeIndependentlyAtTheirOwnKnownFrequency [11 ms]
Passed XlightsAI.Studio.Audio.Tests.Step044AAcceptanceTests.DecodeStereoAsync_MonoSource_ThrowsStereoAudioDecodeChannelMismatchException_BeforeAnySampleRead_NegativePath [62 ms]
Passed XlightsAI.Studio.Audio.Tests.Step044AAcceptanceTests.ProbeAsync_TruncatedStereoFile_ThrowsAudioProbeInvalidException_ReusedValidationNegativePath [16 ms]
Passed XlightsAI.Studio.Audio.Tests.Step044AAcceptanceTests.DecodeStereoAsync_ThreeChannelSource_ThrowsStereoAudioDecodeChannelMismatchException_NegativePath [8 ms]

Total: 38. Passed: 38, Failed: 0. (34 pre-existing + 4 new Step 044A)
```

### Full regression (after implementation, final run)

```
Passed!  - Failed: 0, Passed:  91, Skipped: 0, Total:  91  - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed:  71, Skipped: 0, Total:  71  - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed:  86, Skipped: 0, Total:  86  - XlightsAI.Studio.Melody.Tests.dll
Passed!  - Failed: 0, Passed:  75, Skipped: 0, Total:  75  - XlightsAI.Studio.Stems.Tests.dll
Passed!  - Failed: 0, Passed:  25, Skipped: 0, Total:  25  - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed:  38, Skipped: 0, Total:  38  - XlightsAI.Studio.Audio.Tests.dll
Passed!  - Failed: 0, Passed:  82, Skipped: 0, Total:  82  - XlightsAI.Studio.Rhythm.Tests.dll
Passed!  - Failed: 0, Passed: 159, Skipped: 0, Total: 159  - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed:  31, Skipped: 0, Total:  31  - XlightsAI.Studio.Reference.Tests.dll
```

**658 total, 0 failed, 0 skipped** — up from the Phase 07 Step 049 baseline of 654 (+4 new
`Step044AAcceptanceTests` in `Audio.Tests`). Every one of the 654 pre-existing tests still passes
unmodified, confirming no prior step/phase's contract was broken, including the full pre-existing
`Audio.Tests` and `Reference.Tests` suites (the two assemblies this step actually touched).

## 5. Persisted-output inspection / observable demonstration for this step

A real, non-test-harness console program (not part of the repository, built under the session
scratchpad, deleted after this run) exercises the real `WavStereoAudioDecoder`/`WavAudioDecoder`
directly against a real, hand-built stereo `.wav` byte file (also written to disk for inspection) — no
test harness, no mocks:

```
dotnet run --project <scratchpad>\xlai-step044a-demo-proj --nologo
```

Observed real output:

```
Demo root directory: C:\Users\kmoens.LAKECO\AppData\Local\Temp\xlai-step044a-demo-c5ec98a9962c41578562621d3eeadc9b

=== Scenario 1: real stereo WAV (L=440Hz, R=880Hz) decoded independently, never swapped/downmixed ===
Wrote real stereo WAV file: ...\stereo-440-880.wav (8044 bytes)
Probed (reused WavAudioDecoder.ProbeAsync, unmodified): Channels=2, SampleRate=8000, SampleCount=2000
Decoded 2000 left samples, 2000 right samples.
Left channel own measured frequency:  436.0 Hz (expected ~440 Hz)
Right channel own measured frequency: 876.0 Hz (expected ~880 Hz)

=== Scenario 2: mono source rejected explicitly, before any sample data is read ===
  Correctly rejected: '...\mono.wav' has 1 channel(s); stereo (exactly 2) is required for DecodeStereoAsync.

=== Scenario 3: truncated/corrupt stereo file rejected via reused WavAudioDecoder.ProbeAsync validation ===
  Correctly rejected: '...\truncated.wav' is not a valid/supported audio file: 'data' chunk size exceeds the actual file length (truncated file).

Step 044A manual demo completed successfully.

Cleaning up demo directory.
```

Direct, real, non-test-harness confirmation: a real 8,044-byte stereo WAV file (2000 frames at 8000 Hz,
left = 440 Hz tone, right = 880 Hz tone) is decoded via the real `WavStereoAudioDecoder`, and each
channel's own independently zero-crossing-measured frequency (436.0 Hz / 876.0 Hz — within the
generous-but-unambiguous ±20/±30 Hz tolerance of their own known tones) proves the two channels were
read independently, never swapped or downmixed (required acceptance, verbatim); a mono source is
rejected with the real `StereoAudioDecodeChannelMismatchException` before any sample data is read
(failure/edge case, verbatim); and a truncated stereo file is rejected by the real, reused, unmodified
`WavAudioDecoder.ProbeAsync` validation (failure/edge case, verbatim: "reused validation, not
reinvented").

## 6. Fixtures / input hashes

No binary fixture *file* is committed to the repository for this step — every stereo/mono WAV fixture
is constructed in-memory at test/demo run time via `DeterministicSyntheticAudioGenerator.BuildNoteSequence`/
`EncodeStereoWav`/`EncodeWav` (the same deterministic-synthesis approach every prior phase's own audio
fixtures already use — no copyrighted recordings are read, mixed, or redistributed). The demo's own
`.wav` byte files are written to its own temporary directory for inspection
(`%TEMP%\xlai-step044a-demo-*\*.wav`) and deleted, along with the rest of that directory, by the demo's
own `finally` block (confirmed by "Cleaning up demo directory." in the real output above); the
throwaway demo *project* itself (outside the repository, under the session scratchpad) was deleted
after this evidence was recorded.

## 7. Unresolved issues / known gaps

- **No real stereo audio decoded from an actual music recording.** Disclosed, consistent with every
  prior phase's own synthetic-fixture-first precedent: this step proves the real decode *mechanism*
  (independent per-channel reads, explicit rejection paths) against deterministic synthetic tones; a
  real multi-second stereo music sample is Step 044D's own later scope (declaring/downloading the real
  model and running the full pipeline end-to-end).
- **No resampling.** Disclosed in §3 — `DecodeStereoAsync` decodes at the source's own native sample
  rate only; the real `sherpa-onnx-spleeter-2stems` model requires 44100 Hz specifically, so any source
  recorded at a different rate will need either a real stereo fixture already at 44100 Hz (the
  straightforward path for Step 044D) or a real resampling step added in a later, explicitly-scoped
  step — not fabricated or assumed working here.
- **No UI wiring.** Consistent with every prior Phase 06/06A step's own identical precedent — this
  step's own "Versioned, inspectable project output plus a reproducible test report" user-visible
  outcome is satisfied by the real persisted test/demo output in §4/§5, not by any Avalonia view.

## 8. PASS / BLOCKED

**Status: PASS** (for this step's own narrow, disclosed scope — a real, independently-verified stereo
decode sibling to the existing mono-only `IAudioDecoder`/`WavAudioDecoder` pipeline).

- "A real stereo WAV fixture... is decoded via `IStereoAudioDecoder`; each decoded channel's own
  independently-measured frequency... matches its own known tone within tolerance... proving channels
  are read independently, never swapped or downmixed" (required acceptance, verbatim) — proven by
  `Step044A_Acceptance_Passes_ChannelsDecodeIndependentlyAtTheirOwnKnownFrequency` (a real 440 Hz/880 Hz
  two-tone stereo fixture decodes to each channel's own independently-measured frequency within a
  generous-but-unambiguous tolerance) — independently confirmed by the real demo's Scenario 1.
- "A mono (1-channel) or >2-channel source passed to `DecodeStereoAsync` throws
  `StereoAudioDecodeChannelMismatchException` before any sample data is read" (failure/edge case,
  verbatim) — proven by both
  `DecodeStereoAsync_MonoSource_ThrowsStereoAudioDecodeChannelMismatchException_BeforeAnySampleRead_NegativePath`
  and `DecodeStereoAsync_ThreeChannelSource_ThrowsStereoAudioDecodeChannelMismatchException_NegativePath`
  — the mono case independently confirmed by the real demo's Scenario 2.
- "a truncated/corrupt stereo file is rejected the same way `WavAudioDecoder.ProbeAsync` already
  rejects a truncated mono file (reused validation, not reinvented)" (failure/edge case, verbatim) —
  proven by `ProbeAsync_TruncatedStereoFile_ThrowsAudioProbeInvalidException_ReusedValidationNegativePath`
  (the exact same, completely unmodified `WavAudioDecoder.ProbeAsync` validation rejects a truncated
  stereo file with the identical `AudioProbeInvalidException` type a truncated mono file already
  produces) — independently confirmed by the real demo's Scenario 3.
- Full regression: **658/658 passing**, 0 failed, 0 skipped; every pre-existing test from Phase 01-07
  (Steps 001-049) and this addendum's own baseline still passes unmodified.
- No carried-forward blocker specific to this step. Phase 06's own phase-level `BLOCKED` gate status
  (Step 038's feasibility verdict) is unaffected by this step and remains exactly as recorded in
  `implementation-evidence/PHASE-06/STEP-038.md` through `STEP-044.md` and reiterated through Phase 07's
  own Steps 045-049 — this addendum's own Step 044F is where that status is finally revisited, not this
  step.
