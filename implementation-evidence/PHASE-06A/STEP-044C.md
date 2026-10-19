# PHASE 06A — Step 044C Evidence

**Step:** "Define the dual-model spectrogram separator composition and prove its masking/reconstruction math"
**Spec:** `docs/phase-specs/PHASE-06A.md` §6, Step 044C — third numbered step of this addendum (044A-044F),
inserted between Phase 06's Step 044 and Phase 07's Step 045
**Date:** 2026-10-13

## 1. Pre-implementation baseline inspection

- Read `docs/phase-specs/PHASE-06A.md` in full before writing any code: required acceptance ("A synthetic
  end-to-end round-trip proving the real masking/reconstruction math is correct -- e.g. a trivial,
  hand-computed two-stem synthetic case (two known, analytically-separable synthetic spectrograms standing
  in for 'vocals_estimate'/'accompaniment_estimate') where the expected separated output is analytically
  known -- independent of the real model weights, so this step's own correctness does not depend on the
  real model producing musically good output"), implementation guidance (frame stereo audio into the real
  `(2, num_splits, 512, 1024)` tensor shape; run both real single-stem ONNX sessions; apply the real soft
  mask formula verbatim; apply the padded mask to the *original* complex STFT, never a model-estimated
  phase; reconstruct via Step 044B's real ISTFT), and failure/edge cases (wrong rank/shape model output
  rejected via the existing `StemSeparationOutputShapeException`, reused not reinvented, before any mask
  arithmetic runs; a digitally silent input never divides by zero in the soft-mask formula).
- Read the explicit task instruction for this session, verbatim: this step's required acceptance "must be
  provable WITHOUT the real downloaded model (synthetic, analytically-known masking/reconstruction math
  only) -- do not pull in Step 044D's real model to make this step pass." Confirmed this is also the
  addendum spec's own literal required-acceptance text (not an added constraint) -- Step 044D (not this
  step) is where the real `sherpa-onnx-spleeter-2stems` archive, `IModelArchiveExpander`, and a real
  end-to-end run against real model weights are introduced.
- Read §3's proposed `SpectrogramSeparationRecipe`/`ExtractedModelFile`/`IModelArchiveExpander`/
  `SpectrogramModelBundle`/`ISpectrogramStemSeparator`/`SpectrogramOnnxStemSeparator` C# signatures verbatim,
  and the architectural design note explaining why `ISpectrogramStemSeparator` is a disclosed sibling to
  `IStemSeparator` (same relationship Phase 07 Step 048 established for `IMidiTranscriptionBackend`
  relative to `IMelodyAnalyzer`), never a redefinition of it.
- Inspected every existing file in `src/XlightsAI.Studio.Stems/` before writing anything, in particular:
  `IStemSeparator.cs`, `StemSeparationRecipe.cs`, `OnnxStemSeparator.cs` (confirmed untouched by this step,
  per the explicit instruction not to modify `IStemSeparator`/`StemSeparationRecipe`/`OnnxStemSeparator`),
  `StemSeparationOutputShapeException.cs` and `StemSeparationIncompatibleSourceException.cs` (the two
  existing exception types this step reuses, never duplicates), `StemKind.cs`, `StemAsset.cs`,
  `StemManifest.cs` (the canonical output shape this step's interface still returns, unmodified),
  `StftConfig.cs`/`ShortTimeFourierTransform.cs` (Step 044B's own real STFT/ISTFT module, reused unmodified
  -- in particular `ComputeTimeAxisZeroPadding`, already implemented by Step 044B specifically so this step
  would not need to re-derive the formula), and `StemWavWriter.cs` (the existing mono-only WAV writer,
  confirmed to have no stereo counterpart yet).
- Confirmed no prior phase/step already declares `SpectrogramSeparationRecipe`, `ISpectrogramStemSeparator`,
  `SpectrogramOnnxStemSeparator`, `SpectrogramModelBundle`, `ExtractedModelFile`, `IModelArchiveExpander`,
  `SpectrogramStemMasking`, or `SpectrogramTensorFraming` anywhere in the repository (`Glob`'d
  `src/XlightsAI.Studio.Stems/*.cs` directly).
- Confirmed Steps 044A and 044B (`implementation-evidence/PHASE-06A/STEP-044A.md`,
  `implementation-evidence/PHASE-06A/STEP-044B.md`) are the only already-completed steps of this addendum;
  both report **PASS** with a final regression of 658/658 and 667/667 respectively. Nothing in either
  step's own scope (`IStereoAudioDecoder`, `ShortTimeFourierTransform`) is modified by this step -- both are
  only ever called/reused.
- Baseline regression before any Step 044C code existed (`dotnet test XlightsAI.Studio.slnx --nologo`, run
  fresh in this session): matched Step 044B's own final recorded total — `Stems.Tests` = 84, full solution
  = 667 — before any Step 044C file existed.

## 2. Scope actually implemented

### New files in `src/XlightsAI.Studio.Stems/`

- **`SpectrogramSeparationRecipe.cs`** -- `public sealed record SpectrogramSeparationRecipe(string
  RecipeVersion, int ExpectedSampleRate, int ExpectedChannels, StftConfig Stft, int RetainedFrequencyBins,
  int TimeSplitFrames, string InputTensorName, string OutputTensorName, float SoftMaskEpsilon = 1e-10f)`,
  exactly as proposed in the addendum spec §3.
- **`ExtractedModelFile.cs`** -- `public sealed record ExtractedModelFile(string ArchiveEntryName, string
  LocalPath, long Length)`, exactly as proposed. Declared in this step (not deferred to 044D) because
  `SpectrogramOnnxStemSeparator`'s own constructor/plumbing (also this step's scope, per the addendum's
  own class-relationship diagram) needs the type to compile; the real, concrete, tar+bzip2-capable
  `IModelArchiveExpander` *implementation* -- and its own disclosed third-party dependency choice -- remains
  Step 044D's own scope, never pre-empted here.
- **`IModelArchiveExpander.cs`** -- the phase-local archive-expansion contract, exactly as proposed. Only
  the interface is declared by this step; no concrete implementation exists yet (disclosed, deliberate --
  see §3 below).
- **`SpectrogramModelBundle.cs`** -- `public sealed record SpectrogramModelBundle(ModelAsset Archive,
  IReadOnlyDictionary<StemKind, string> StemArchiveEntryNames)`, exactly as proposed, including the
  addendum's own disclosed `StemKind.Other` == "accompaniment" semantic-overload decision, carried into
  this file's own doc comment verbatim.
- **`ISpectrogramStemSeparator.cs`** -- the phase-local sibling interface to `IStemSeparator`, exactly as
  proposed: `Task<StemManifest> SeparateAsync(SongAsset source, SpectrogramModelBundle modelBundle,
  SpectrogramSeparationRecipe recipe, IProgress<SeparationProgress>? progress, CancellationToken ct)`.
- **`SpectrogramStemMasking.cs`** -- the real, pure, independently-testable masking/reconstruction
  composition this step's own required acceptance is about:
  - `ComputeSoftMask(float[,] stemEstimate, float[,] otherStemEstimate, float epsilon)` -- the real
    published formula verbatim, `mask = (stem^2 + eps/2) / (stem^2 + other^2 + eps)`. Validates both
    estimate tensors have identical shape and throws the existing `StemSeparationOutputShapeException`
    *before* computing a single mask value if they do not.
  - `ApplyMaskToOriginalStft((float[,] Real, float[,] Imag) originalStft, float[,] retainedMask, int
    fullFrequencyBinCount)` -- pads the retained-bin mask back to the full bin count with an implicit zero
    mask for every bin at or beyond the retained range (the real model's own disclosed, permanent
    frequency-domain truncation -- bins 1024-2048 are always zeroed in every separated stem, never
    recovered), then applies the padded mask to the *original* mixed-signal complex STFT's real/imaginary
    parts, never to a model-estimated phase (verbatim implementation-guidance requirement). Validates shape
    compatibility (frame counts, bin counts) and throws `StemSeparationOutputShapeException` before
    applying any mask arithmetic on mismatch.
- **`SpectrogramTensorFraming.cs`** -- the real `(2, num_splits, 512, 1024)` tensor-framing contract,
  deliberately separated from any ONNX session call so it, too, can be proven directly against hand-built
  tensors, independent of any real or fixture model:
  - `BuildInputTensor((float[,] Real, float[,] Imag) leftStft, (float[,] Real, float[,] Imag) rightStft,
    SpectrogramSeparationRecipe recipe, out int originalFrameCount, out int numSplits)` -- reuses Step
    044B's own `ShortTimeFourierTransform.ComputeTimeAxisZeroPadding` formula (never re-derived), builds a
    `DenseTensor<float>` of per-channel magnitude (`sqrt(real^2+imag^2)`), channels stacked on axis 0,
    relying on `DenseTensor<float>`'s own zero-initialized backing array for the padded tail frames.
  - `ExtractChannelEstimate(Tensor<float> modelOutput, int channel, int originalFrameCount,
    SpectrogramSeparationRecipe recipe)` -- validates the model output tensor's rank and shape against the
    recipe's own declared contract, throwing `StemSeparationOutputShapeException` (reusing the existing
    type, never inventing a parallel one) before cropping the real-frame-range estimate back out of the
    time-split-padded tensor.
- **`SpectrogramOnnxStemSeparator.cs`** -- the real `ISpectrogramStemSeparator`: validates source
  compatibility (`StemSeparationIncompatibleSourceException` reused); resolves/verifies the declared
  archive via the existing, unmodified `IModelAssetManager`; expands it via the injected
  `IModelArchiveExpander`; decodes real stereo audio via the existing, unmodified `IStereoAudioDecoder`
  (Step 044A); computes real per-channel STFT via `ShortTimeFourierTransform.Forward` (Step 044B); builds
  the real input tensor via `SpectrogramTensorFraming.BuildInputTensor`; runs one real
  `Microsoft.ML.OnnxRuntime.InferenceSession` per declared stem; applies the real soft mask via
  `SpectrogramStemMasking`; reconstructs via `ShortTimeFourierTransform.Inverse`; writes real stereo WAV
  stem files (via this step's own new `StemWavWriter.WriteStereoFloatAsync`, see below) to a temp path,
  atomically moved into place only once fully written (reusing `OnnxStemSeparator`'s own disclosed
  "partial output never marked complete" pattern). Generic over any 2-stem, dual-single-stem-session
  spectrogram model matching the recipe's own tensor contract -- Step 044D's own later scope is only to
  supply the real, downloaded `sherpa-onnx-spleeter-2stems` archive/`ModelAsset` and a real
  `IModelArchiveExpander` implementation; this class does not change.

### Modified file (additive only)

- **`src/XlightsAI.Studio.Stems/StemWavWriter.cs`** -- added `WriteStereoFloatAsync(string path,
  ReadOnlyMemory<float> left, ReadOnlyMemory<float> right, int sampleRate, CancellationToken ct)`, a new,
  additive sibling to the existing `WriteMonoFloatAsync` (never a retrofit of it -- the same "new sibling,
  no shared call sites" precedent this addendum's own Step 044A already established for
  `DeterministicSyntheticAudioGenerator.EncodeStereoWav`). `WriteMonoFloatAsync` itself is byte-for-byte
  unmodified; every existing caller (`OnnxStemSeparator`, Phase 06's own tests) is unaffected.

### New test file

- `tests/XlightsAI.Studio.Stems.Tests/Step044CAcceptanceTests.cs` -- 9 tests (full list in §4), including
  the required `Step044C_Acceptance_Passes_TwoStemSyntheticSeparationMatchesAnalyticallyKnownExpectedOutput`
  acceptance test and explicit negative-path tests for both named failure/edge cases.

**No changes to `IStemSeparator.cs`, `StemSeparationRecipe.cs`, `OnnxStemSeparator.cs`, `IAudioDecoder.cs`,
`WavAudioDecoder.cs`, `IStereoAudioDecoder.cs`, `WavStereoAudioDecoder.cs`, `StftConfig.cs`,
`ShortTimeFourierTransform.cs`, or any `Melody`/`Shell`/`Audio` file.** No `.csproj`/`.slnx` changes were
needed -- the new Stems-project files depend only on types already referenced by
`XlightsAI.Studio.Stems.csproj` (`Microsoft.ML.OnnxRuntime`, already a pinned `PackageReference`) or already
inside the project/`XlightsAI.Studio.Audio`/`XlightsAI.Studio.Contracts`.

## 3. Architecture deviations / decisions

- **This step's own required acceptance is proven entirely without an ONNX `InferenceSession` call.**
  Disclosed, deliberate, per both the addendum spec's own literal required-acceptance text ("independent of
  the real model weights... e.g. a trivial, hand-computed two-stem synthetic case... standing in for
  'vocals_estimate'/'accompaniment_estimate'") and this session's own explicit task instruction. The
  masking/reconstruction composition (`SpectrogramStemMasking`) and the tensor-framing contract
  (`SpectrogramTensorFraming`) are both pure, static, independently-testable methods taking plain
  `float[,]`/`Tensor<float>` arguments -- never requiring a loaded ONNX model to exercise. The acceptance
  test hand-computes "vocals_estimate"/"accompaniment_estimate" directly as the real STFT magnitude of two
  known, isolated synthetic tones (an analytically-known quantity, not a model's output), exactly matching
  the spec's own named example. `SpectrogramOnnxStemSeparator`'s own real `InferenceSession` wiring is
  still implemented in full for production use (so Step 044D only needs to supply real model files, never
  rewrite this class), but is not itself exercised by any Step 044C test -- that full, real, ONNX-backed
  end-to-end run is explicitly Step 044D's own required acceptance ("A real stereo audio sample is
  separated into two real stem WAV files via the real downloaded model"), not duplicated or pre-empted here.
- **`ExtractedModelFile`/`IModelArchiveExpander` are declared (interface/record only) in this step, not
  Step 044D, because `SpectrogramOnnxStemSeparator`'s own constructor signature (this step's own scope per
  the addendum's class-relationship diagram) requires the type to compile.** No concrete archive-expander
  implementation, and no decision about the real tar+bzip2 third-party dependency, is made here --
  disclosed, deliberately deferred to Step 044D's own named scope exactly as the addendum's numbered-step
  text assigns it ("Extract the archive via a real, deterministic `IModelArchiveExpander`... this step's
  `IModelArchiveExpander` implementation may take a dependency on one established... library").
- **Masking/reconstruction math and tensor framing are each their own new, named, public static class
  (`SpectrogramStemMasking`, `SpectrogramTensorFraming`) rather than private helper methods buried inside
  `SpectrogramOnnxStemSeparator`.** Disclosed, deliberate: the addendum's own required acceptance text
  explicitly asks for the masking/reconstruction math to be provable independent of any model, which is
  only possible if that math is reachable from a test without constructing a full separator (which itself
  needs `IModelAssetManager`/`IModelArchiveExpander`/`IStereoAudioDecoder` collaborators and a real or
  fixture ONNX session). Extracting both into separate, pure, public static classes is a narrow,
  disclosed scope addition beyond the spec's own literal §3 type list, but changes no proposed signature --
  `SpectrogramOnnxStemSeparator.SeparateAsync`'s own public contract is implemented exactly as proposed.
- **The soft-mask-sums-to-exactly-one property is asserted directly in the acceptance test as a disclosed,
  load-bearing cross-check**, not merely implied: `vocalsMask[f,b] + accompanimentMask[f,b] == 1` at every
  bin, directly from the formula's own algebraic structure (`(a+eps/2)/(a+b+eps) + (b+eps/2)/(a+b+eps) =
  (a+b+eps)/(a+b+eps) = 1`). This independently corroborates the formula was transcribed correctly before
  relying on the full STFT-mask-ISTFT round trip's own reconstruction-fidelity tolerance to catch a
  transcription error indirectly.
- **`StemKind.Other`'s own real archive-entry mapping, and any real `ModelAsset`/license data, are not
  declared by this step.** Disclosed, deliberate: those are Step 044D's own named scope (declaring the real
  `sherpa-onnx-spleeter-2stems` `ModelAsset`) and Step 044F's own named scope (the revised
  `ModelFeasibilityReport`), neither pre-empted here. This step's own fixture recipe/bundle types in tests
  use placeholder/fixture identifiers only.

## 4. Commands actually executed

All run via the PowerShell tool -- the sandboxed POSIX Bash tool in this session again returned a bare
`exit code 1` with no output for plain `mkdir`/`git status` commands, the same pre-existing environment
limitation recorded in every prior step's own evidence; PowerShell was used for every command below without
incident.

```
dotnet build src\XlightsAI.Studio.Stems\XlightsAI.Studio.Stems.csproj --nologo
  # First attempt: 2 errors (CS4007 "Instance of type 'System.ReadOnlySpan<float>' cannot be preserved
  # across 'await' or 'yield' boundary" in the new StemWavWriter.WriteStereoFloatAsync -- a local
  # ReadOnlySpan<float> variable captured across an awaited stream write). Fixed by removing the
  # intermediate span-typed local variables and indexing left.Span[i]/right.Span[i] inline per iteration
  # (never persisting a span-typed value across the await). Re-ran:
  # Build succeeded. 0 Warning(s). 0 Error(s).

# New: src/XlightsAI.Studio.Stems/{SpectrogramSeparationRecipe.cs, ExtractedModelFile.cs,
#      IModelArchiveExpander.cs, SpectrogramModelBundle.cs, ISpectrogramStemSeparator.cs,
#      SpectrogramStemMasking.cs, SpectrogramTensorFraming.cs, SpectrogramOnnxStemSeparator.cs}
# Modified (additive only): src/XlightsAI.Studio.Stems/StemWavWriter.cs (new WriteStereoFloatAsync method)
# New: tests/XlightsAI.Studio.Stems.Tests/Step044CAcceptanceTests.cs
# No .csproj / .slnx changes required.

dotnet test tests\XlightsAI.Studio.Stems.Tests\XlightsAI.Studio.Stems.Tests.csproj --nologo \
  --logger "console;verbosity=normal" --filter "FullyQualifiedName~Step044C"
  # 9/9 passed on first attempt, no production-code changes needed after the build fix above.

dotnet build XlightsAI.Studio.slnx --nologo       # full solution -> 0 Warnings, 0 Errors
dotnet test  XlightsAI.Studio.slnx --nologo        # full regression, final (see results/caveat below)
```

### Build results

`dotnet build XlightsAI.Studio.slnx --nologo`: `Build succeeded. 0 Warning(s). 0 Error(s).`

### Targeted test results (Step 044C tests only, final run)

```
Passed XlightsAI.Studio.Stems.Tests.Step044CAcceptanceTests.ApplyMaskToOriginalStft_MaskFrameCountMismatch_ThrowsStemSeparationOutputShapeException_NegativePath [7 ms]
Passed XlightsAI.Studio.Stems.Tests.Step044CAcceptanceTests.ApplyMaskToOriginalStft_BinsBeyondRetainedRangeAreZeroed_PermanentQualityCeilingDisclosed [7 ms]
Passed XlightsAI.Studio.Stems.Tests.Step044CAcceptanceTests.ExtractChannelEstimate_WrongShapeModelOutput_ThrowsStemSeparationOutputShapeException_NegativePath [10 ms]
Passed XlightsAI.Studio.Stems.Tests.Step044CAcceptanceTests.ExtractChannelEstimate_WrongRankModelOutput_ThrowsStemSeparationOutputShapeException_BeforeAnyMaskArithmetic_NegativePath [2 ms]
Passed XlightsAI.Studio.Stems.Tests.Step044CAcceptanceTests.ExtractChannelEstimate_RoundTripsKnownValues [69 ms]
Passed XlightsAI.Studio.Stems.Tests.Step044CAcceptanceTests.BuildInputTensor_ShapeMatchesRealDisclosedConvention [120 ms]
Passed XlightsAI.Studio.Stems.Tests.Step044CAcceptanceTests.Step044C_Acceptance_Passes_TwoStemSyntheticSeparationMatchesAnalyticallyKnownExpectedOutput [1 s]
Passed XlightsAI.Studio.Stems.Tests.Step044CAcceptanceTests.ComputeSoftMask_SilentInputBothZero_NeverDividesByZero_ProducesFiniteHalfMask_NegativePathEdgeCase [< 1 ms]
Passed XlightsAI.Studio.Stems.Tests.Step044CAcceptanceTests.ComputeSoftMask_MismatchedEstimateShapes_ThrowsStemSeparationOutputShapeException_NegativePath [< 1 ms]

Total tests: 9. Passed: 9. Failed: 0.
```

### Full regression (after implementation, final run)

```
Passed!  - Failed:  0, Passed:  71, Skipped: 0, Total:  71 - XlightsAI.Studio.Xlights.Tests.dll
Failed!  - Failed: 10, Passed:  28, Skipped: 0, Total:  38 - XlightsAI.Studio.Audio.Tests.dll
Passed!  - Failed:  0, Passed:  91, Skipped: 0, Total:  91 - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed:  0, Passed:  86, Skipped: 0, Total:  86 - XlightsAI.Studio.Melody.Tests.dll
Passed!  - Failed:  0, Passed:  25, Skipped: 0, Total:  25 - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed:  0, Passed:  93, Skipped: 0, Total:  93 - XlightsAI.Studio.Stems.Tests.dll
Failed!  - Failed: 15, Passed: 144, Skipped: 0, Total: 159 - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed:  0, Passed:  82, Skipped: 0, Total:  82 - XlightsAI.Studio.Rhythm.Tests.dll
Passed!  - Failed:  0, Passed:  31, Skipped: 0, Total:  31 - XlightsAI.Studio.Reference.Tests.dll
```

**676 total (667 + 9 new Step 044C tests), 25 failed, 0 skipped.** `Stems.Tests` itself is **93/93 passing**
(84 pre-existing + 9 new), confirming every prior Phase 06/06A test this step could plausibly affect still
passes unmodified. The 25 failures are **all** in `Audio.Tests` (10) and `Shell.Tests` (15) -- **disclosed
below as a pre-existing environment limitation, not a regression caused by this step.**

#### Disclosed regression caveat: real-audio-device tests fail in this session's environment

Every one of the 25 failing tests throws the identical real exception,
`XlightsAI.Studio.Audio.AudioOutputDeviceUnavailableException: No audio output device is available: the
specified device ID is out of range; no such output device exists. (winmm result code 2).`, originating
from `WaveOutAudioTransport.OpenAsync` (`src/XlightsAI.Studio.Audio/WaveOutAudioTransport.cs:117`) --
real tests that open a real Windows audio output device (`Step025`/`Step028`/`Step029`/`Step030`
`Audio`/`Shell` wiring tests, `Step042` stem-mixer real-device tests). This machine currently has no
available `winmm` audio output device at all (not merely busy -- "no such output device exists"), which is
an environment/hardware condition of this specific session, not a code regression:

- **This step (`044C`) touches only `src/XlightsAI.Studio.Stems/` and `StemWavWriter.cs`.** No file in
  `src/XlightsAI.Studio.Audio/` or `src/XlightsAI.Studio.Shell/` was created, modified, or deleted by this
  step (confirmed by this step's own file list in §2 and by `git status` showing no changes outside
  `Stems`/`tests/XlightsAI.Studio.Stems.Tests`/`implementation-evidence`).
  `Audio.Tests`/`Shell.Tests` were re-run in complete isolation
  (`dotnet test tests\XlightsAI.Studio.Audio.Tests\...`/`tests\XlightsAI.Studio.Shell.Tests\...` directly,
  with no other test project running concurrently) and failed identically, with the identical exception,
  confirming this is not test-run interference from this step's own new Stems tests.
  Steps 044A (`implementation-evidence/PHASE-06A/STEP-044A.md`) and 044B
  (`implementation-evidence/PHASE-06A/STEP-044B.md`), run earlier in this same addendum, both recorded
  `Audio.Tests` = 38/38 and `Shell.Tests` = 159/159 passing with **zero** failures -- the audio-device
  availability condition changed in this session's underlying machine/environment between Step 044B's own
  run and this step's run, not because of any line of code this step (or any prior addendum step) wrote.
- This is disclosed here honestly rather than silently omitted or worked around by weakening a real
  assertion, per Section 2's own verification rule ("Do not claim tests ran when they did not") and this
  repository's own standing "never silently substitute a mock" rule for hardware/backend unavailability.
  **No `Stems`/Step 044C test depends on `WaveOutAudioTransport` or any real audio output device** -- this
  step's own full targeted suite (9/9) and the full `Stems.Tests` assembly (93/93) are unaffected and
  genuinely green.
- This caveat is a known, disclosed, **unresolved pre-existing environment gap** for this specific session,
  not something this step's own narrow scope can or should fix (Step 044C owns spectrogram-separator
  composition math, not audio *output device* playback). It is carried forward here, honestly, rather than
  silently narrowed out of the regression report.

## 5. Persisted-output inspection / observable demonstration for this step

This step's own user-visible outcome, per the spec (§6, Step 044C), is "Versioned, inspectable project
output plus a reproducible test report" -- not a UI/xLights demonstration (that belongs to Step 044D's real
end-to-end separation run). The reproducible test report is the full targeted test output in §4 above, run
directly against the real, non-mocked `SpectrogramStemMasking`/`SpectrogramTensorFraming` static classes --
no interface was mocked; every test calls the real methods directly against real, deterministically
hand-computed synthetic tensors.

Representative real, directly-observed numeric results from this step's own test run (not fabricated):

- A real 3-second mixed synthetic signal (300 Hz "vocals" tone + 3000 Hz "accompaniment" tone, both summed
  into one real mixed signal, 132,300 samples at 44,100 Hz) is separated via the real
  STFT -> `ComputeSoftMask` -> `ApplyMaskToOriginalStft` -> ISTFT composition using hand-computed
  "vocals_estimate"/"accompaniment_estimate" tensors (the real, directly-computed STFT magnitude of each
  tone in isolation -- never a trained model's output). Both real separated outputs reconstruct their own
  known, isolated tone with a real measured RMS-relative error under the test's own `1e-3` acceptance
  tolerance over the fully-overlapped interior (Step 044B's own disclosed Hann-window edge-taper margin
  excluded, exactly as Step 044B's own round-trip test already does) -- the test passed on its first
  attempt with no tolerance loosening or production-code change needed.
- At every one of the real computed mask's `(frame, bin)` positions, `vocalsMask + accompanimentMask`
  equals real `1.0` to 5 decimal places, directly, numerically confirming the soft-mask formula's own
  algebraic identity holds for this real implementation (not merely asserted by inspection).
- A real all-zero (silent) pair of estimate tensors produces a real, finite mask value of exactly `0.5` at
  every one of its 32 (4 frames x 8 bins) positions -- directly, numerically proving the disclosed epsilon
  term prevents a zero-over-zero division (never a NaN), per this step's own named failure/edge case.
- A real hand-built original STFT with every bin nonzero (`7.0`), masked by a real pass-through mask
  covering only the first 1024 of 2049 bins, produces a real masked output of exactly `7.0` within the
  retained range and exactly `0.0` at every one of the 1025 discarded bins (1024-2048) -- directly,
  numerically confirming the real model's own disclosed, permanent frequency-domain quality ceiling is
  implemented as specified (zeroed, never silently passed through or approximated).
- A real mismatched-shape pair of estimate tensors (`[3,1024]` vs `[3,512]`) is rejected by the real
  `ComputeSoftMask` via a real, observed `StemSeparationOutputShapeException` whose message contains
  "mismatched shapes" -- confirmed thrown before any mask value is computed (the exception is raised by an
  explicit shape check preceding the computation loop, not caught mid-loop).
- A real rank-3 `DenseTensor<float>` (instead of the required rank-4 model output shape) is rejected by the
  real `ExtractChannelEstimate` via a real, observed `StemSeparationOutputShapeException` whose message
  contains "rank" -- confirmed thrown before any per-element extraction or mask arithmetic runs.
- A real 600-frame pair of (empty, zero-filled) stereo STFTs builds a real `(2, 2, 512, 1024)` input tensor
  via `BuildInputTensor` -- directly, numerically confirming the real zero-padding formula (`600 % 512 =
  88`, so `padding = 512 - 88 = 424`; `600 + 424 = 1024 = 2 x 512`) produces exactly 2 time splits, reusing
  Step 044B's own `ComputeTimeAxisZeroPadding` formula without re-deriving it.

## 6. Fixtures / input hashes

No binary fixture file (and no ONNX model file, real or synthetic) is read or written by this step's own
tests -- every tensor/signal is constructed deterministically in-memory at test run time (sine-tone sums,
hand-set `DenseTensor<float>` values, hand-built `float[,]` arrays), exactly matching this step's own
required acceptance ("independent of the real model weights") and every prior addendum step's own
synthetic-fixture-first precedent. No external model, archive, or audio recording is read by this step.

## 7. Unresolved issues / known gaps

- **No real or fixture ONNX model is loaded or run by this step.** Disclosed, consistent with the spec's
  own explicit instruction (§6, Step 044C's own required acceptance, verbatim: "independent of the real
  model weights"). `SpectrogramOnnxStemSeparator`'s real `InferenceSession` wiring is implemented in full
  for production use but is not exercised end-to-end by any test in this step -- a real, ONNX-backed
  end-to-end run against the real `sherpa-onnx-spleeter-2stems` model is explicitly Step 044D's own later,
  separately-gated scope, not fabricated or assumed working here.
- **No `IModelArchiveExpander` concrete implementation exists yet.** Disclosed in §2/§3 above -- the real
  tar+bzip2-capable implementation and its own disclosed third-party dependency choice is Step 044D's own
  named scope.
- **Pre-existing, disclosed real-audio-device test-environment gap** (§4's own regression caveat): this
  session's machine currently has no available Windows audio output device, causing 25 pre-existing
  `Audio.Tests`/`Shell.Tests` tests (none of them touched by this step) to fail with a real, consistent
  `AudioOutputDeviceUnavailableException`. Confirmed unrelated to this step's own files via isolated re-runs
  and via comparison against Steps 044A/044B's own prior, fully-green recorded runs. This is carried
  forward here as an honest, unresolved environment limitation for whoever next runs this solution's full
  regression on this machine -- not a defect in this step's own narrow scope.
- **No UI wiring.** Consistent with every prior Phase 06/06A step's own identical precedent -- this step's
  own "Versioned, inspectable project output plus a reproducible test report" user-visible outcome is
  satisfied by the real persisted test output in §4/§5, not by any Avalonia view.

## 8. PASS / BLOCKED

**Status: PASS** (for this step's own narrow, disclosed scope -- the real dual-model spectrogram separator
composition's masking/reconstruction math, proven correct against hand-computed, analytically-known
synthetic tensors, independent of any real model weights).

- "A synthetic end-to-end round-trip proving the real masking/reconstruction math is correct... independent
  of the real model weights" (required acceptance, verbatim) -- proven by
  `Step044C_Acceptance_Passes_TwoStemSyntheticSeparationMatchesAnalyticallyKnownExpectedOutput` (a real
  mixed two-tone signal is separated via the real STFT -> soft-mask -> ISTFT composition using hand-computed
  "vocals_estimate"/"accompaniment_estimate" tensors, each stem reconstructing its own known, isolated tone
  within a tight tolerance).
- "A model output tensor of the wrong rank/shape is rejected via `StemSeparationOutputShapeException`
  (reusing the existing exception type, not inventing a parallel one) before any mask arithmetic runs"
  (failure/edge case, verbatim) -- proven by
  `ExtractChannelEstimate_WrongRankModelOutput_ThrowsStemSeparationOutputShapeException_BeforeAnyMaskArithmetic_NegativePath`,
  `ExtractChannelEstimate_WrongShapeModelOutput_ThrowsStemSeparationOutputShapeException_NegativePath`,
  `ComputeSoftMask_MismatchedEstimateShapes_ThrowsStemSeparationOutputShapeException_NegativePath`, and
  `ApplyMaskToOriginalStft_MaskFrameCountMismatch_ThrowsStemSeparationOutputShapeException_NegativePath` --
  all four reuse the exact same, existing `StemSeparationOutputShapeException` type (never a new parallel
  exception), each confirmed to throw before any mask value is computed/applied.
- "a digitally silent input never divides by zero in the soft-mask formula (the epsilon term is
  load-bearing, not decorative)" (failure/edge case, verbatim) -- proven by
  `ComputeSoftMask_SilentInputBothZero_NeverDividesByZero_ProducesFiniteHalfMask_NegativePathEdgeCase` (a
  real all-zero pair of estimate tensors produces a real, finite `0.5` mask value at every position, never
  a NaN).
- Full targeted suite: **9/9 passing**, 0 failed. Full `Stems.Tests` assembly: **93/93 passing** (84
  pre-existing Step 038-044B tests + 9 new Step 044C tests), 0 failed -- every prior Phase 06/06A Stems test
  still passes unmodified.
- **Disclosed, carried-forward caveat (not a blocker for this step's own scope):** 25 pre-existing
  `Audio.Tests`/`Shell.Tests` tests that open a real Windows audio output device fail in this session's
  environment due to no audio output device currently being available on this machine (§4) -- confirmed
  unrelated to any file this step touches. This is reported honestly rather than silently omitted; it does
  not affect this step's own PASS status (Step 044C owns spectrogram-separator composition math, not audio
  output device playback), but is flagged for whoever next runs this solution's full regression.
- No carried-forward blocker specific to this step's own scope. Phase 06's own phase-level `BLOCKED` gate
  status (Step 038's feasibility verdict) is unaffected by this step and remains exactly as recorded through
  `STEP-044.md` and this addendum's own `STEP-044A.md`/`STEP-044B.md` -- this addendum's own Step 044F is
  where that status is finally revisited, not this step.

## 9. Architecture deviations to flag for review

- `SpectrogramStemMasking` and `SpectrogramTensorFraming` are new, named, public static classes not
  explicitly listed among §3's proposed type names -- disclosed and justified in §3 above (needed so this
  step's own required "independent of the real model weights" acceptance could be tested directly, without
  constructing a full `SpectrogramOnnxStemSeparator` and a real or fixture ONNX session). No proposed
  signature in §3 (`SpectrogramSeparationRecipe`, `ISpectrogramStemSeparator`,
  `SpectrogramOnnxStemSeparator`'s own public constructor/method signature) was changed.
- `ExtractedModelFile`/`IModelArchiveExpander` are declared (type/interface only, no implementation) in this
  step rather than Step 044D -- disclosed and justified in §3 above (required for
  `SpectrogramOnnxStemSeparator`'s own constructor to compile; the real archive-expansion implementation and
  its own dependency decision remain entirely Step 044D's scope).
- `StemWavWriter.WriteStereoFloatAsync` is a new, additive sibling method -- disclosed and justified in §2/§3
  above, following this addendum's own established "new sibling, never a retrofit" precedent.
- Do not proceed to Step 044D until this evidence is reviewed, per the addendum's own step-gating rule (§6:
  "Implementation / tests / observable demo / evidence all complete before next numbered step"). In
  particular, Step 044D's own implementer should re-check whether this session's real-audio-device
  environment gap (§4/§7) has been resolved on the machine that runs that step's own full regression, since
  Step 044D's own required acceptance explicitly runs on "CPU-only Windows" (audio *inference*, not audio
  *output device playback* -- this step's own disclosed gap is about `WaveOutAudioTransport`, not ONNX CPU
  inference, so it should not block Step 044D's own real-model separation run, but is worth re-confirming).
