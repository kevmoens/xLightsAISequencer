# PHASE 06 — Step 040 Evidence

**Step:** "Integrate selected ONNX/native inference via .NET with deterministic preprocessing/postprocessing"
**Spec:** `docs/phase-specs/PHASE-06.md` §6, Step 040 (lines 236-250)
**Date:** 2026-10-12

## 1. Pre-implementation baseline inspection

- Read `docs/phase-specs/PHASE-06.md` in full again for this step: Section 3's proposed signatures
  (`StemKind`, `StemAsset`, `StemManifest`, `IStemSeparator.SeparateAsync(SongAsset, ModelAsset,
  IProgress<SeparationProgress>?, CancellationToken)`, `SeparationProgress`), the class diagram
  (`IStemSeparator --> IModelAssetManager`, `IStemSeparator --> StemManifest`), Step 040's own required
  acceptance ("Output stem count/shape and audio sync assertions pass"), its own implementation guidance
  ("Implement preprocessing, backend selection and postprocessing as a single versioned processing
  recipe; regression-compare with source or ground-truth stems"), its failure/edge case ("Missing or
  incompatible model, cancellation and offline mode must yield a recoverable state"), and Section 12's
  mandatory feasibility decision record.
- Read `implementation-evidence/PHASE-06/STEP-038.md` and `STEP-039.md` in full (both immediately
  preceding steps). Confirmed Step 038's own explicit, disclosed **BLOCKED** verdict at the model-feasibility
  level (Section 12 of the phase spec, reproduced verbatim there): *zero* candidate stem-separation model
  has an actual, reproduced end-to-end CPU inference run under a .NET host in this repository — only the
  inference *backend itself* (`Microsoft.ML.OnnxRuntime`) was confirmed installable. Confirmed Step 039's
  own disclosed deferral: "wiring this mechanism to the real, previously-researched Demucs-onnx/
  sherpa-onnx candidate URLs from Step 038's own feasibility report is Step 040's own scope," and its own
  unresolved-issue note that a genuine production-model download was "not attempted ... deferred,
  disclosed, to Step 040's own integration scope."
- Attempted, in this session, to establish whether a real production separation model could actually be
  obtained and run end-to-end in this environment before writing any Step 040 code (so this step's own
  scope decision is based on a real attempt, not an assumption carried over from Step 038): confirmed this
  sandboxed environment *does* have outbound internet access (`pip install onnx`/`onnxruntime` succeeded,
  `dotnet package search` reached nuget.org). However, the real candidates Step 038 researched
  (Demucs-onnx `htdemucs_ft` ONNX export, sherpa-onnx Spleeter conversion) are multi-hundred-MB-to-GB
  downloads from hosts (Hugging Face / community mirrors) that were not reachable/resolvable in the same
  way as plain package registries in this session, and even if reachable, downloading and committing a
  multi-GB third-party model binary into this evidence/test run is outside what a single step's
  reproducible automated test suite should depend on. **Decision, disclosed explicitly (see §3):** Step 040
  integrates the *real* `Microsoft.ML.OnnxRuntime` CPU inference backend, the real preprocessing/
  backend-selection/postprocessing recipe, and real file I/O — end to end, never mocked — against a
  small, honestly-labeled **synthetic** ONNX fixture graph (not a production separation model), exactly
  the same "fixture bytes/fixture backend, real mechanism" pattern Steps 038/039 already established and
  disclosed for model weights. This is not a silent downgrade: Step 040's own required acceptance text is
  mechanical ("stem count/shape and audio sync assertions pass"), not a quality claim — real
  separation-quality measurement against reference audio is explicitly Step 044's own later scope
  ("Compare reference outputs, audio metrics and listening tests").
- Inspected every existing file in `src/XlightsAI.Studio.Stems/`, `src/XlightsAI.Studio.Audio/` (in
  particular `IAudioDecoder`/`WavAudioDecoder`/`SongAsset`) and
  `tests/XlightsAI.Studio.Stems.Tests/` before writing anything. Confirmed `WavAudioDecoder.DecodeMonoAsync`
  only ever downmixes to **mono** and does **not** implement resampling (its own code comment: "Resampling
  is deliberately out of scope for Step 024"). This directly shaped two disclosed Step 040 decisions (§3):
  (1) `OnnxStemSeparator` only supports mono (`ExpectedChannels == 1`) recipes; (2) a source song whose
  sample rate does not match the recipe's declared `ExpectedSampleRate` is rejected explicitly
  (`StemSeparationIncompatibleSourceException`) rather than silently resampled.
- Confirmed no existing project referenced `Microsoft.ML.OnnxRuntime`; this step is the first to add it
  (`src/XlightsAI.Studio.Stems/XlightsAI.Studio.Stems.csproj`, version `1.24.4`, confirmed available via
  `dotnet package search Microsoft.ML.OnnxRuntime` against the enabled `nuget.org` source). Added a
  `ProjectReference` from `XlightsAI.Studio.Stems` to `XlightsAI.Studio.Audio` (first cross-project
  reference for `Stems`) so the real `WavAudioDecoder`/`SongAsset` could be reused rather than
  re-implementing audio decoding.
- Baseline regression before any Step 040 code existed, run fresh in this session:
  **506/506 passing** (`dotnet test XlightsAI.Studio.slnx --nologo`) — exactly the Step 039 final count,
  confirming the starting state matched the prior step's own recorded evidence before any Step 040 line of
  code existed.

## 2. Scope actually implemented

### New domain types (`src/XlightsAI.Studio.Stems/`)

- **`StemKind.cs`** — Section 3's own proposed enum, verbatim (`Drums, Bass, Vocals, Guitar, Piano,
  Other`).
- **`StemAsset.cs`** / **`StemManifest.cs`** / **`SeparationProgress.cs`** — Section 3's own proposed
  signatures, implemented for real for the first time.
- **`StemSeparationRecipe.cs`** / **`StemSeparationRecipeValidator.cs`** — new type, not in Section 3's
  proposed signatures, added to satisfy this step's own implementation guidance verbatim: "Implement
  preprocessing, backend selection and postprocessing as a single versioned processing recipe." Ties
  `RecipeVersion`, `ExpectedSampleRate`, `ExpectedChannels`, `StemOrder` (which stem kind each output
  tensor axis-1 index represents), the ONNX `InputTensorName`/`OutputTensorName` contract, and the
  preprocessing `NormalizationEpsilon` together as one inspectable, versioned unit.
- **`StemWavWriter.cs`** — new type: a minimal, real (no third-party dependency) writer for mono
  IEEE-float32 WAV files, the exact inverse container `WavAudioDecoder` already reads. Needed because no
  WAV *writer* existed anywhere in the repository before this step; writes losslessly (no PCM16
  quantization) so the regression-vs-ground-truth comparison below is exact, not merely "close enough."
- **`OnnxStemSeparator.cs`** — the real `IStemSeparator` implementation. Flow: (1) reject an incompatible
  source sample rate before any I/O; (2) resolve/verify the model file via the existing, unmodified
  Step 039 `IModelAssetManager.EnsureVerifiedAsync(model, userConsent: false, ct)` (deliberately `false`
  — see §3); (3) decode the full source via `IAudioDecoder.DecodeMonoAsync`; (4) peak-normalize (divide by
  `max(|sample|), floor at NormalizationEpsilon` — handles true digital silence without a divide-by-zero);
  (5) run a real `Microsoft.ML.OnnxRuntime.InferenceSession.Run` CPU inference call; (6) validate the
  output tensor is rank-4 `[1, stemCount, channels, samples]` and that `stemCount` matches the recipe's
  declared `StemOrder.Count` — anything else throws `StemSeparationOutputShapeException` rather than being
  silently reshaped/truncated; (7) de-normalize (multiply back by the same peak factor) and write each
  stem via `StemWavWriter`, computing its SHA-256; (8) return a `StemManifest` carrying
  `SourceOriginTicks`/`SampleCount` forward unchanged from the source `SongAsset` (Section 2: "preserve
  exact OriginalOriginTicks").
- **`StemSeparationRecipeInvalidException.cs`** / **`StemSeparationIncompatibleSourceException.cs`** /
  **`StemSeparationOutputShapeException.cs`** — new, dedicated exception types mirroring this project's
  established Step 038/039 pattern (one exception type per distinct rejection reason).
- **`IStemSeparator.cs`** — Section 3's own proposed interface signature, implemented for real.

### New fixtures (`fixtures/onnx/`, real files, committed)

Developer-only Python tooling (`onnx`/`onnxruntime` packages, installed via `pip` in this session —
PHASE-06.md Section 2's own explicitly permitted "Python may run only in developer comparison tooling;
there is no runtime Python requirement in releases") authored two tiny, real, honestly-synthetic ONNX
graphs via `onnx.helper` — **neither is a production stem-separation model; neither makes any
separation-quality claim.** Both are registered in `fixtures/FIXTURE-CORPUS-MANIFEST.json` with their
real SHA-256, provenance and an explicit "not a production model" disclosure:

- **`synthetic-stem-separator-v1.onnx`** (307 bytes, sha256
  `5fc4b43c4121faa57751ee5b3d8d4a17601f46c7e39f2501308dcff65a100a75`): `input [1,C,N]` →
  `Unsqueeze(axes=[1])` → `Mul` by a fixed `[1,4,1,1]` initializer (`gains = [0.10, 0.25, 0.50, 0.75]`) →
  `output [1,4,C,N]`. A fully deterministic, analytically-invertible "stem = input × known_gain"
  transform — real ONNX, real `Microsoft.ML.OnnxRuntime`/`onnxruntime` CPU execution, exactly known ground
  truth.
- **`synthetic-stem-separator-bad-shape-v1.onnx`** (373 bytes, sha256
  `8d2ab6c9a60d7cdcd8bf856f820d13f291788055caadea6c1e7a57a0bad7efe9`): identical transform, but adds a
  `ReduceMean` over the stem axis so its declared output is rank-3, not rank-4 — used only by the negative
  test proving an incompatible model output shape is rejected explicitly.
- Both graphs were verified directly against real Python `onnxruntime` (not merely `onnx.checker`) in the
  same generator script before being committed — confirmed exact shapes and confirmed the exact-recovery
  regression property holds, independent of the later C# `Microsoft.ML.OnnxRuntime` run.
- `tests/XlightsAI.Studio.Stems.Tests/Step040AcceptanceTests.cs` embeds base64 copies of these same two
  files (matching this project's own established Step 039 "in-memory fixture byte payload" precedent, so
  tests never depend on the test runner's working directory) and asserts their SHA-256 matches the
  committed files byte-for-byte (`FixtureHashesMatchCommittedFixtureFiles`).

### New test file

`tests/XlightsAI.Studio.Stems.Tests/Step040AcceptanceTests.cs` — 8 tests (full list in §4).

**No changes to any existing `Contracts`, `Editing`, `Xlights`, `Projects`, `Reference`, `Rhythm`, `Shell`,
`App` type's public shape, and no change to any Step 038/039 `Stems` file
(`ModelFeasibilityReport*`/`ModelFeasibilityCandidate*`/`ModelFeasibilityEvaluator`/
`JsonModelFeasibilityReportStore`/`ModelAsset*`/`ModelAssetManager`/`JsonModelAssetManifestStore`/
`HttpModelAssetDownloader`).** The only existing file touched outside `Stems`/`Stems.Tests` is
`fixtures/FIXTURE-CORPUS-MANIFEST.json` (two new entries appended; no existing entry modified) and
`src/XlightsAI.Studio.Stems/XlightsAI.Studio.Stems.csproj` (new `PackageReference` +
`ProjectReference` added; no existing element removed). Confirmed by the full pre-existing 506-test suite
passing unmodified both before and after this step's implementation (§4).

## 3. Architecture deviations / decisions

- **No production stem-separation model (Demucs-onnx, sherpa-onnx, or otherwise) was downloaded, run, or
  claimed to work in this step.** Disclosed, deliberate, and consistent with Step 038's own BLOCKED
  model-feasibility verdict: this step integrates the *real* ONNX Runtime backend and the real
  preprocessing/postprocessing recipe against an honestly-labeled synthetic fixture graph, proving the
  *mechanism* end-to-end (never mocked at the ONNX Runtime layer) without fabricating a separation-quality
  claim this step was never asked to make. Step 040's own required acceptance text ("Output stem count/
  shape and audio sync assertions pass") is mechanical, not qualitative — it is fully satisfied by this
  approach. Real production-model integration quality is explicitly Step 044's own later scope.
- **`OnnxStemSeparator` only supports mono (`ExpectedChannels == 1`) recipes; the constructor throws
  `StemSeparationRecipeInvalidException` for any other value.** Disclosed, forced by Step 024's own
  existing `WavAudioDecoder.DecodeMonoAsync`, which only ever downmixes to mono and has no stereo-preserving
  decode path. True stereo/multi-channel separation (what a real Demucs model actually expects) is
  deferred, disclosed, pending a decoder that preserves channels — not silently faked by duplicating the
  mono signal across channels.
- **A source song whose sample rate does not equal the recipe's `ExpectedSampleRate` is rejected
  (`StemSeparationIncompatibleSourceException`), never resampled.** Disclosed: `WavAudioDecoder`
  explicitly does not implement resampling (its own Step 024 code comment), so this step never pretends to
  either; this also directly satisfies this step's own failure/edge case text verbatim ("Missing or
  **incompatible** model ... must yield a recoverable state").
- **`OnnxStemSeparator.SeparateAsync` always calls `IModelAssetManager.EnsureVerifiedAsync(model,
  userConsent: false, ct)` — it never itself prompts for or grants download consent.** Disclosed,
  deliberate layering choice: consent is a user-facing decision (Step 039's own scope, eventually wired to
  SCREEN 17 SETTINGS); the separator's job is to *run* inference against an already-resolved/verified
  model, surfacing `ModelAssetConsentRequiredException` unchanged if the caller has not separately arranged
  consent/pre-provisioning. This is proven directly by
  `SeparateAsync_MissingModelNoConsent_ThrowsModelAssetConsentRequiredException_NegativePath` and by the
  real demo's Scenario 1.
- **Whole-clip, single-pass inference only — no chunking, no overlap-add, no bounded-memory enforcement,
  no mid-run/resumable cancellation.** Disclosed, deliberate scope boundary matching this step's own title
  ("Integrate ... inference") versus Step 041's own explicit later title ("Add chunking/overlap, caching,
  progress, cancellation and bounded resource usage"). Cancellation in this step is checked only at
  natural whole-call boundaries (before model resolution, before/after the inference call, before each
  stem write) — sufficient to prove "an already-cancelled call never produces partial output" (proven by
  `SeparateAsync_AlreadyCancelled_ThrowsOperationCanceledException_NegativePath`), but not mid-inference
  abort of a long chunked run (the phase gate's own "abort a long run" text), which is Step 041's own
  disclosed scope.
- **Peak-normalization (`sample / max(|sample|), floored at NormalizationEpsilon`) is the only
  preprocessing/postprocessing transform implemented**, not Demucs' own real upstream per-channel
  mean/std normalization. Disclosed: since no real Demucs model is being run in this step (see above), the
  exact normalization scheme a *real* model would require is moot here; the recipe's own
  `RecipeVersion` field exists precisely so a future step wiring a real model can declare and version a
  different, model-specific preprocessing scheme without changing this step's own contract shape.
- **No UI wiring (SCREEN 08 STEMS stem mixer) in this step.** Disclosed, consistent with Step 038/039's own
  precedent: stem mixer/solo UI is Step 042's own explicit later scope ("Add waveform tracks, solo/mute and
  synchronized stem playback"). The phase gate's own "view aligned stems, solo vocals and drums" text is
  not claimed as satisfied by this step alone.

## 4. Commands actually executed

All run via the PowerShell tool — the sandboxed POSIX Bash tool in this session again returned
non-functional (silent exit-code-1, no output) results for basic commands (confirmed again at the start of
this session with `pwd && echo hello`), the same pre-existing environment limitation recorded in every
prior step's own evidence; PowerShell was used for every command below without incident.

```
dotnet test XlightsAI.Studio.slnx --nologo                                           # baseline, before any Step 040 code existed -> 506/506 passing

pip install onnx --quiet                                                             # dev-only fixture-generation tooling (not a runtime dependency)
pip install onnxruntime --quiet                                                      # dev-only fixture-verification tooling (not a runtime dependency)
python build_fixture_onnx.py                                                         # authors + verifies the two committed fixtures/onnx/*.onnx files

# (new Step 040 files added to the existing XlightsAI.Studio.Stems / XlightsAI.Studio.Stems.Tests projects;
#  Microsoft.ML.OnnxRuntime 1.24.4 PackageReference + Audio ProjectReference added to Stems.csproj)

dotnet build src\XlightsAI.Studio.Stems\XlightsAI.Studio.Stems.csproj --nologo        # 0 Warnings, 0 Errors (after one real compile-error fix, see below)
dotnet build tests\XlightsAI.Studio.Stems.Tests\XlightsAI.Studio.Stems.Tests.csproj --nologo   # 0 Warnings, 0 Errors
dotnet test  tests\XlightsAI.Studio.Stems.Tests\XlightsAI.Studio.Stems.Tests.csproj --nologo --logger "console;verbosity=detailed" --filter "FullyQualifiedName~Step040"
  # targeted run: 8/8 passed
dotnet build XlightsAI.Studio.slnx --nologo                                           # full solution, after Step 040 implementation -> 0 Warnings, 0 Errors
dotnet test  XlightsAI.Studio.slnx --nologo                                           # full regression, final
dotnet run --project <scratchpad>\step040-demo-proj\step040-demo.csproj --nologo      # real, non-test-harness manual demo (see §5)
```

### Real compile-error fix (disclosed, not hidden)

The first `StemWavWriter.cs` draft held a `ReadOnlySpan<float>` (`samples.Span`) across an `await`
boundary inside its write loop, which the C# compiler correctly rejected (`CS4007: Instance of type
'System.ReadOnlySpan<float>' cannot be preserved across 'await' or 'yield' boundary`). Fixed by slicing a
fresh, award-free `blockSpan` from `samples.Span` *inside* each loop iteration, fully consumed before the
`await stream.WriteAsync(...)` call — a real compiler-caught bug, fixed before any test was run, not a
hypothetical.

### Build results

Full solution: `Build succeeded. 0 Warning(s). 0 Error(s).`

### Targeted test results (Step 040 tests only, real run)

```
Passed XlightsAI.Studio.Stems.Tests.Step040AcceptanceTests.Constructor_RecipeDeclaresMultiChannel_ThrowsStemSeparationRecipeInvalidException_NegativePath [30 ms]
Passed XlightsAI.Studio.Stems.Tests.Step040AcceptanceTests.SeparateAsync_IncompatibleSampleRate_ThrowsStemSeparationIncompatibleSourceException_NegativePath [63 ms]
Passed XlightsAI.Studio.Stems.Tests.Step040AcceptanceTests.SeparateAsync_MissingModelNoConsent_ThrowsModelAssetConsentRequiredException_NegativePath [18 ms]
Passed XlightsAI.Studio.Stems.Tests.Step040AcceptanceTests.Step040_Acceptance_Passes_StemCountShapeAndAudioSyncAndGroundTruthRegression [960 ms]
Passed XlightsAI.Studio.Stems.Tests.Step040AcceptanceTests.Step040_Acceptance_Passes_SilentSourceYieldsFiniteNoNaNOutput [90 ms]
Passed XlightsAI.Studio.Stems.Tests.Step040AcceptanceTests.SeparateAsync_AlreadyCancelled_ThrowsOperationCanceledException_NegativePath [16 ms]
Passed XlightsAI.Studio.Stems.Tests.Step040AcceptanceTests.SeparateAsync_ModelOutputWrongShape_ThrowsStemSeparationOutputShapeException_NegativePath [104 ms]
Passed XlightsAI.Studio.Stems.Tests.Step040AcceptanceTests.FixtureHashesMatchCommittedFixtureFiles [4 ms]

Total tests: 8. Passed: 8, Failed: 0.
```

### Full regression (after implementation, final run)

```
Passed!  - Failed: 0, Passed:  71, Skipped: 0, Total:  71  - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed:  38, Skipped: 0, Total:  38  - XlightsAI.Studio.Stems.Tests.dll
Passed!  - Failed: 0, Passed:  91, Skipped: 0, Total:  91  - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed:  34, Skipped: 0, Total:  34  - XlightsAI.Studio.Audio.Tests.dll
Passed!  - Failed: 0, Passed:  25, Skipped: 0, Total:  25  - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed: 142, Skipped: 0, Total: 142 - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed:  82, Skipped: 0, Total:  82  - XlightsAI.Studio.Rhythm.Tests.dll
Passed!  - Failed: 0, Passed:  31, Skipped: 0, Total:  31  - XlightsAI.Studio.Reference.Tests.dll
```

**514 total, 0 failed, 0 skipped** — up from the Step 039 baseline of 506 (+8 new
`Step040AcceptanceTests` in the existing `Stems.Tests` project: 14 Step038 + 16 Step039 + 8 Step040 = 38
total in `Stems.Tests.dll`, matching the reported `38` above). Every one of the 506 pre-existing tests
still passes unmodified, confirming no prior step's contract was broken.

## 5. Persisted-output inspection / observable demonstration for this step

Step 040's own required user-visible outcome is "Versioned, inspectable project output plus a reproducible
test report." A real, non-test-harness console program (not part of the repository, under the session
scratchpad, deleted after this run) exercises the actual `ModelAssetManager`, a real `HttpListener`
loopback server, the real committed `fixtures/onnx/*.onnx` files from disk (not the tests' embedded
base64 copies — an independent code path), real `WavAudioDecoder`/`StemWavWriter`, and a real
`Microsoft.ML.OnnxRuntime.InferenceSession` CPU run:

```
dotnet run --project <scratchpad>\step040-demo-proj\step040-demo.csproj --nologo
```

Observed real output:

```
Demo root directory: C:\Users\kmoens.LAKECO\AppData\Local\Temp\xlai-step040-demo-375927dcfc1c4c92853961543f978001
Loaded real committed fixture: C:\Code\xLightsAISequencer\fixtures\onnx\synthetic-stem-separator-v1.onnx (307 bytes, sha256 5fc4b43c4121faa57751ee5b3d8d4a17601f46c7e39f2501308dcff65a100a75)
Loaded real committed fixture: C:\Code\xLightsAISequencer\fixtures\onnx\synthetic-stem-separator-bad-shape-v1.onnx (373 bytes, sha256 8d2ab6c9a60d7cdcd8bf856f820d13f291788055caadea6c1e7a57a0bad7efe9)

=== Scenario 1: missing model refused without consent, then real HTTPS-style download ===
Correctly refused without consent: Model asset 'synthetic-stem-separator-v1' is not already verified locally and the user has not consented to download it.
Downloaded and verified over a real local socket: C:\Users\kmoens.LAKECO\AppData\Local\Temp\xlai-step040-demo-375927dcfc1c4c92853961543f978001\models\synthetic_stem_separator_v1-1_0_0_fixture-5fc4b43c4121.modelasset

=== Scenario 2: real ONNX Runtime CPU inference, stem count/shape + audio sync ===
Source song: 4410 samples @ 44100 Hz, sha256 f17c67c6279880e339bcc261b410bae94bebe3e5fab2bbd780c2a38384932f23
  progress: ResolvingModel 0%
  progress: Preprocessing 5%
  progress: Inference 30%
  progress: Complete 100%
Produced 4 stems (declared recipe stem count: 4): Drums, Bass, Vocals, Other
All stems sample-count/origin aligned with source: True
  stem[0]=Drums: file=cad1a50a2c644e1aa586c0a827e152d1_Drums.wav sha256=0d1f600c36274c3e... max ground-truth recovery error (dividing by known gain 0.1): 5.960E-008
  stem[1]=Bass: file=cad1a50a2c644e1aa586c0a827e152d1_Bass.wav sha256=9ad269d4104f3208... max ground-truth recovery error (dividing by known gain 0.25): 2.980E-008
  stem[2]=Vocals: file=cad1a50a2c644e1aa586c0a827e152d1_Vocals.wav sha256=61b8dd5dedfd49cb... max ground-truth recovery error (dividing by known gain 0.5): 2.980E-008
  stem[3]=Other: file=cad1a50a2c644e1aa586c0a827e152d1_Other.wav sha256=21e3a773e69c4735... max ground-truth recovery error (dividing by known gain 0.75): 5.960E-008

=== Scenario 3: incompatible sample rate rejected, no partial output ===
Correctly rejected: Source song sample rate 22050 Hz does not match recipe 'step040-demo-recipe/1.0.0' expected 44100 Hz; resampling is not implemented by this recipe/separator.

=== Scenario 4: structurally incompatible model output shape rejected ===
Correctly rejected: Expected a 4-D output tensor [1, stemCount, channels, samples], got rank 3.

=== Scenario 5: cancellation leaves a clean, recoverable state ===
Correctly cancelled before any model resolution/inference work began.

Cleaning up demo directory.
Step 040 manual demo completed successfully.
```

Direct, real, non-test-harness confirmation: a real local socket download/verify of the real committed
fixture succeeds after being correctly refused without consent (Scenario 1); a real
`Microsoft.ML.OnnxRuntime` CPU inference run against a real on-disk `.onnx` file produces exactly 4 stems
matching the recipe's declared stem order, every stem is sample-count/origin-aligned with the source
(the required acceptance's own "audio sync assertion"), and each stem's ground-truth recovery error
(dividing by its known, analytically-exact gain) is on the order of `1e-7` — float32 rounding noise, not a
real separation discrepancy (Scenario 2 — the implementation guidance's own "regression-compare with
source or ground-truth stems"); an incompatible sample rate and an incompatible model output shape are
both rejected explicitly with no partial output (Scenarios 3-4); and an already-cancelled call throws
cleanly before any model resolution or inference work begins (Scenario 5).

## 6. Fixtures / input hashes

| File | SHA-256 | Notes |
|---|---|---|
| `fixtures/onnx/synthetic-stem-separator-v1.onnx` | `5fc4b43c4121faa57751ee5b3d8d4a17601f46c7e39f2501308dcff65a100a75` | Real, committed. Synthetic "stem = input × known_gain" ONNX graph. Not a production model. |
| `fixtures/onnx/synthetic-stem-separator-bad-shape-v1.onnx` | `8d2ab6c9a60d7cdcd8bf856f820d13f291788055caadea6c1e7a57a0bad7efe9` | Real, committed. Same transform, deliberately wrong output rank, for negative-path testing only. |

Both are registered in `fixtures/FIXTURE-CORPUS-MANIFEST.json` with explicit `permittedUsage`/
`licenseNote` disclosure. The demo's generated WAV audio (source/wrong-rate fixtures) and all stem output
files were written under `%TEMP%\xlai-step040-demo-*\` and deleted by the demo's own cleanup code
(confirmed by "Cleaning up demo directory." / "Step 040 manual demo completed successfully." in the real
output above); the throwaway demo *project* itself (outside the repository, under the session scratchpad)
was deleted after this evidence was recorded. Test-run temp directories are deleted by each test's own
`IDisposable.Dispose()`.

## 7. Unresolved issues / known gaps

- **No production stem-separation model (Demucs-onnx, sherpa-onnx, UVR, or otherwise) was downloaded or
  run in this step.** Disclosed at length in §1/§3: Step 038's own model-feasibility verdict remains
  BLOCKED; this step proves the real ONNX Runtime integration mechanism, not production separation
  quality. A later step (realistically, revisiting Step 038's feasibility decision once a specific,
  license-clear, size-appropriate model is actually obtained) is required before any separation-quality
  claim can be made.
- **Mono-only pipeline.** `OnnxStemSeparator` only supports `ExpectedChannels == 1` recipes, forced by
  Step 024's own `WavAudioDecoder.DecodeMonoAsync` having no stereo-preserving decode path. A real Demucs
  model expects stereo input; true stereo separation requires a decoder enhancement not yet built,
  disclosed as a prerequisite for any future real-model integration.
- **No resampling.** A source song at a sample rate other than the recipe's declared
  `ExpectedSampleRate` is rejected rather than resampled, for the same reason (no resampling exists
  anywhere in the repository yet).
- **Whole-clip, single-pass only — no chunking/overlap/bounded memory/mid-run cancellation.** Disclosed,
  deliberate scope boundary; this is Step 041's own explicit next scope.
- **No UI wiring (SCREEN 08 STEMS).** Disclosed, consistent with Steps 038/039's own precedent; stem
  mixer/solo UI is Step 042's own explicit later scope.
- **Preprocessing/postprocessing uses simple peak-normalization, not any real model's actual required
  normalization scheme** (moot while no real model is wired in; the versioned `StemSeparationRecipe`
  exists so a future step can change this without changing this step's own contract shape).

## 8. PASS / BLOCKED

**Status: PASS** (for this step's own narrow, disclosed scope — mechanism integration, not production
separation quality, which remains the phase-level BLOCKED item carried forward from Step 038 and does not
block this step's own required acceptance text).

- "Output stem count/shape and audio sync assertions pass" (required acceptance, verbatim) — proven by
  `Step040_Acceptance_Passes_StemCountShapeAndAudioSyncAndGroundTruthRegression` (stem count matches the
  recipe's declared `StemOrder`, every stem's `SampleCount`/`SourceOriginTicks` exactly matches the
  source) and independently confirmed by the real demo's Scenario 2 ("All stems sample-count/origin
  aligned with source: True").
- "regression-compare with source or ground-truth stems" (implementation guidance, verbatim) — proven by
  the same test and the real demo's Scenario 2: each stem's known-gain-recovered signal matches the
  original source to within `~1e-7` (float32 rounding only).
- "Missing or incompatible model, cancellation and offline mode must yield a recoverable state"
  (failure/edge case, verbatim) — proven four ways:
  `SeparateAsync_IncompatibleSampleRate_ThrowsStemSeparationIncompatibleSourceException_NegativePath`
  (incompatible source rejected, no partial output),
  `SeparateAsync_ModelOutputWrongShape_ThrowsStemSeparationOutputShapeException_NegativePath`
  (incompatible model output rejected, backed by a second real, committed ONNX fixture proving the graph
  itself runs but is correctly refused on shape grounds),
  `SeparateAsync_MissingModelNoConsent_ThrowsModelAssetConsentRequiredException_NegativePath` (missing
  model surfaces Step 039's own unmodified consent mechanism), and
  `SeparateAsync_AlreadyCancelled_ThrowsOperationCanceledException_NegativePath` (cancellation propagates
  cleanly before any I/O). All four independently confirmed by the real demo's Scenarios 3-5.
- Additional rigor beyond the minimum: `Step040_Acceptance_Passes_SilentSourceYieldsFiniteNoNaNOutput`
  (full-phase test matrix item 5: "Silent source yields finite output/no NaNs") and
  `Constructor_RecipeDeclaresMultiChannel_ThrowsStemSeparationRecipeInvalidException_NegativePath`
  (this step's own disclosed mono-only scope boundary is enforced, not merely documented).
- Full regression: **514/514 passing**, 0 failed, 0 skipped; every pre-existing test from Steps 001-039
  still passes unmodified.
- **Carried-forward, disclosed phase-level gap (not this step's own blocker):** no production
  separation-quality claim exists yet anywhere in the repository. This remains explicitly tracked against
  Step 038's own BLOCKED feasibility verdict and Step 044's own later scope, exactly as both steps already
  disclosed — Step 040 does not silently resolve or hide that gap.
