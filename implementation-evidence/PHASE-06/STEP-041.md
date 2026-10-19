# PHASE 06 — Step 041 Evidence

**Step:** "Add chunking/overlap, caching, progress, cancellation and bounded resource usage"
**Spec:** `docs/phase-specs/PHASE-06.md` §6, Step 041 (lines 252-266)
**Date:** 2026-10-12

## 1. Pre-implementation baseline inspection

- Read `docs/phase-specs/PHASE-06.md` in full again for this step: Step 041's own required acceptance
  ("Long fixture cancels cleanly and partial output never marked complete"), its own implementation
  guidance ("Add overlapping chunks, merge normalization, cancellation and resumable complete-chunk
  cache; enforce upper bound on working buffers"), its failure/edge case ("Invalid input, cancellation
  or stale persisted state must not corrupt the project"), and Section 3's architectural note ("Process
  large songs in bounded chunks, overlap-add/crossfade appropriately, preserve exact
  `OriginalOriginTicks`").
- Read `implementation-evidence/PHASE-06/STEP-040.md` in full (immediately preceding step). Confirmed
  its own disclosed scope boundary, verbatim: "Whole-clip, single-pass inference only — no chunking, no
  overlap-add, no bounded-memory enforcement, no mid-run/resumable cancellation… Step 041's own disclosed
  scope." This step closes exactly that disclosed gap.
- Inspected every existing file in `src/XlightsAI.Studio.Stems/` before writing anything:
  `OnnxStemSeparator.cs`, `StemSeparationRecipe.cs`, `StemSeparationRecipeValidator.cs`,
  `StemWavWriter.cs`, `IStemSeparator.cs`, `ModelAssetManager.cs`, and the Step 038/039 model-feasibility
  types (left untouched). Confirmed `OnnxStemSeparator`'s existing whole-clip flow: decode mono → peak
  normalize → one `InferenceSession.Run` call over the entire signal → validate output shape →
  de-normalize → write one WAV per stem.
- Baseline regression before any Step 041 code existed, run fresh in this session:
  **514/514 passing** (`dotnet test XlightsAI.Studio.slnx --nologo`) — exactly Step 040's own final
  count, confirming the starting state matched the prior step's own recorded evidence before any
  Step 041 line of code existed.
- Decision made before writing code: extend the existing `OnnxStemSeparator`/`StemSeparationRecipe` in
  place rather than introducing a parallel "chunked" implementation. `OnnxStemSeparator` is a concrete
  class, not one of PHASE-06.md §2's named, cross-phase-stable aggregates (`StemManifest`/`StemAsset`
  are; the separator implementation is not), so no migration record is required — this is the natural,
  purely-additive evolution the step's own title implies ("Add chunking/overlap… " to the existing
  mechanism Step 040 built). All new `StemSeparationRecipe` fields are appended with defaults that
  reproduce Step 040's exact prior behavior (`ChunkSamples = int.MaxValue`, `OverlapSamples = 0`), so
  every pre-existing recipe construction (including all of Step 040's own tests, unmodified) still
  resolves to a single whole-clip chunk — confirmed directly: all 8 pre-existing `Step040AcceptanceTests`
  pass byte-for-byte unmodified after this step's implementation (§4).

## 2. Scope actually implemented

### Changed existing files

- **`StemSeparationRecipe.cs`** — two new, additive, default-valued positional parameters appended at
  the end: `ChunkSamples` (default `int.MaxValue`) and `OverlapSamples` (default `0`). Non-breaking: no
  existing call site (including every Step 040 test) needed to change.
- **`StemSeparationRecipeValidator.cs`** — three new checks: `ChunkSamples` must be positive,
  `OverlapSamples` must not be negative, and `OverlapSamples` must be strictly less than `ChunkSamples`
  (this step's own failure/edge case, "invalid input… must not corrupt the project," applied to the
  recipe itself).
- **`OnnxStemSeparator.cs`** — rewritten internals (constructor signature and `SeparateAsync` public
  contract unchanged, plus one new optional trailing constructor parameter): the single whole-clip
  `RunInferenceAndWriteStemsAsync` was replaced by `RunChunkedInferenceAndWriteStemsAsync`, which:
  1. Splits the normalized signal into `ChunkSamples`-sized (optionally `OverlapSamples`-overlapping)
     chunks, computed from `ChunkSamples`/`OverlapSamples`/total length — never more than `ChunkSamples`
     samples are ever held in a single ONNX input tensor, regardless of total song length (this step's
     own implementation guidance, verbatim: "enforce upper bound on working buffers").
  2. Before computing each chunk, checks the new `IStemSeparationChunkCache` for an already-completed
     result for that exact chunk index under a cache key derived deterministically (SHA-256) from
     `source.Sha256 | model.Sha256 | model.Id | recipe.RecipeVersion | ChunkSamples | OverlapSamples` —
     never from a random run ID, so re-invoking `SeparateAsync` with the same source/model/recipe after a
     cancellation naturally resumes rather than recomputing finished chunks (this step's own
     implementation guidance, verbatim: "resumable complete-chunk cache").
  3. Checks `CancellationToken` at the top of every chunk iteration; an already-cancelled or
     mid-run-cancelled token throws cleanly before any further chunk work begins, propagating up through
     a `finally` that disposes the `InferenceSession`.
  4. Merges overlapping chunk results back together via a weighted overlap-add (crossfade): each chunk's
     local sample contributes `ComputeWindow(...)` weight (a linear ramp in the shared overlap region with
     its neighbor, `1.0` in its own non-overlapping core), accumulated into a per-sample weighted sum and
     a per-sample weight total, then divided out at the end — this step's own implementation guidance,
     verbatim: "merge normalization."
  5. Writes each final merged stem to a `.tmp` path first, and only `File.Move`s it to its real final path
     once fully written; any `.tmp` path still pending in a `finally` block (cancellation or exception
     during the write loop) is deleted — guaranteeing a cancelled/failed run never leaves a half-written
     file at its final path. This is the concrete mechanism behind the required acceptance's own
     "partial output never marked complete."
  6. Reports incremental `"Inference"` progress per completed chunk (not just once at the start), plus a
     new `"Writing"` stage before the final `"Complete"` report — the step's own implementation guidance,
     verbatim: "progress."

### New files

- **`IStemSeparationChunkCache.cs`** — new interface: `TryGet(cacheKey, chunkIndex, stemCount,
  sampleCount, out stems, out discardedCorruptEntry)` / `Put(cacheKey, chunkIndex, stems)`. A cache miss
  is always a `bool false` return, never a thrown exception — including when a cached entry exists but is
  corrupt/structurally stale, which is reported via `discardedCorruptEntry` for caller-visible disclosure
  rather than ever being deserialized into bad data.
- **`FileSystemStemSeparationChunkCache.cs`** — real (no third-party dependency), file-backed
  implementation. One small binary file per chunk under
  `<outputDirectory>/.chunk-cache/<cacheKey>/chunk-<index>.bin`: a 12-byte header (magic, declared stem
  count, declared sample count) followed by raw float32 samples. A file whose length does not exactly
  match its own declared header, or whose magic does not match, is deleted and reported as a corrupt-miss
  rather than ever being trusted — the concrete mechanism behind this step's own disclosed "stale
  persisted state must not corrupt the project" guarantee. Writes go through a `.tmp`-then-`File.Move`
  sequence for the same reason stem files do.
- **No new exception types were needed**: corrupt/stale cache entries are a recoverable miss (silently
  discarded + disclosed via `StemManifest.Warnings`), not an error condition, since recomputing one chunk
  from the already-available source audio is always possible and strictly safer than surfacing a
  cache-internal failure to the caller.

### New test file

`tests/XlightsAI.Studio.Stems.Tests/Step041AcceptanceTests.cs` — 6 tests (full list in §4), reusing the
exact same committed, honestly-synthetic `fixtures/onnx/synthetic-stem-separator-v1.onnx` fixture Step 040
already registered (no new fixture needed — this step's own scope is the chunking/caching/cancellation
*mechanism*, not separation quality).

**No changes to any `Contracts`, `Editing`, `Xlights`, `Projects`, `Reference`, `Rhythm`, `Shell`, `App`,
or Step 038/039 `Stems` file** (`ModelFeasibilityReport*`/`ModelAsset*`/`ModelAssetManager`/
`JsonModelAssetManifestStore`/`HttpModelAssetDownloader` all untouched). `StemAsset`, `StemManifest`,
`StemKind`, `SeparationProgress`, `IStemSeparator`, `StemWavWriter`, and all three existing Step 040
exception types (`StemSeparationRecipeInvalidException`, `StemSeparationIncompatibleSourceException`,
`StemSeparationOutputShapeException`) are reused completely unchanged — no new exception type was
introduced by this step.

## 3. Architecture deviations / decisions

- **No new `IStemSeparator` implementation class.** `OnnxStemSeparator` was extended in place rather than
  adding a parallel `ChunkedOnnxStemSeparator`. Disclosed, deliberate: Step 041's own title ("Add
  chunking/overlap, caching, progress, cancellation and bounded resource usage") reads as augmenting
  Step 040's own mechanism, not replacing it with a second implementation of the same interface; keeping
  one implementation also means a caller who never sets `ChunkSamples`/`OverlapSamples` gets identical
  behavior to Step 040, verified directly (all 8 pre-existing `Step040AcceptanceTests` pass unmodified,
  §4).
- **`StemSeparationRecipe`'s two new fields are additive with defaults reproducing Step 040's exact prior
  behavior.** Disclosed: `ChunkSamples = int.MaxValue` combined with `OverlapSamples = 0` always produces
  exactly one chunk spanning the whole signal with a uniform overlap-add weight of `1.0` everywhere —
  mathematically identical to Step 040's own single-pass code path, not merely similar. Proven directly by
  `ChunkedAndWholeClip_SameSourceAndRecipeShape_ProduceSameStemLengthsAndSync` (same source, default vs.
  explicit chunking, identical stem shapes/sync) and by every pre-existing Step 040 test passing
  unmodified.
- **Resumability is keyed off source/model/recipe identity, not an explicit job ID.** Disclosed,
  deliberate: `IStemSeparator.SeparateAsync`'s signature (Section 3's own stable proposed interface) has
  no job-ID parameter and this step does not add one — re-invoking `SeparateAsync` with the same
  `SongAsset`/`ModelAsset`/recipe is itself the resume operation, since the deterministic cache key is
  derived entirely from those three already-present inputs. A caller that wants a *fresh* recompute of an
  already-fully-cached run must change one of those three identities (e.g. bump `RecipeVersion`) — the
  same principle Section 4 already establishes phase-wide ("every cache key includes algorithm/model/
  settings versions").
- **A corrupt/stale chunk-cache entry is a silent, disclosed miss — never an exception.** Disclosed,
  deliberate: since the original source audio is always still available to recompute the one affected
  chunk, failing the entire separation run over one corrupted 100 KB cache file would be strictly worse
  for the user than quietly recomputing it and recording a `StemManifest.Warnings` entry. Proven directly
  by `CorruptStaleChunkCacheEntry_IsDiscardedAndRecomputed_NegativePath` (a hand-planted, deliberately
  truncated cache file at the real cache key's chunk-0 path is discarded and chunk 0 is recomputed
  correctly, with a warning disclosed in the returned manifest).
- **Bounded resource usage applies to the per-chunk ONNX inference working set, not to the full-song mono
  decode/merge buffers.** Disclosed, consistent with Step 040's own already-disclosed decode-stage
  limitation: `DecodeFullMonoAsync` still loads the entire song into one `float[]` up front (Step 024's
  `WavAudioDecoder` has no streaming decode path), and the final overlap-add merge still allocates one
  `float[total]` accumulator per stem. What this step actually bounds — and what Section 3's own
  "Process large songs in bounded chunks" text is really about — is the ONNX `InferenceSession.Run` input
  tensor size, which is now capped at `ChunkSamples` regardless of total song length; a future step
  streaming the decode/merge stages themselves would need to revisit the decoder, not this separator.
- **No UI wiring, no SCREEN 08 STEMS integration.** Disclosed, consistent with Steps 038-040's own
  precedent; UI wiring remains Step 042's own explicit later scope.
- **The phase gate's own "abort a long run" text is now substantively addressed (mid-run, chunk-granular
  cancellation), but the phase gate itself is not claimed as passed by this step alone** — it also
  requires the stem-mixer UI (Step 042) and instrument-activity detection (Step 043), both still pending.

## 4. Commands actually executed

All run via the PowerShell tool — the sandboxed POSIX Bash tool in this session again returned a bare
exit code 1 with no output for a plain `git log`, the same pre-existing environment limitation recorded in
every prior step's own evidence; PowerShell was used for every command below without incident.

```
git log --oneline -15 ; git status --short                                            # repo has no commits yet (working tree only); confirmed before any edits

dotnet test XlightsAI.Studio.slnx --nologo                                            # baseline, before any Step 041 code existed -> 514/514 passing

# StemSeparationRecipe.cs: + ChunkSamples, + OverlapSamples (additive, defaulted)
# StemSeparationRecipeValidator.cs: + 3 new validation checks
# New: IStemSeparationChunkCache.cs, FileSystemStemSeparationChunkCache.cs
# Rewritten: OnnxStemSeparator.cs (chunking/overlap-add/cache/atomic-write internals; public contract unchanged + 1 optional ctor param)

dotnet build src\XlightsAI.Studio.Stems\XlightsAI.Studio.Stems.csproj --nologo
  # first attempt: CS8602 possible-null-dereference on chunkStems[s, local] -- real compiler-caught issue, fixed
  # by introducing a non-null local (`chunkStemsNonNull`) once both the cache-hit and freshly-computed
  # branches have unconditionally assigned it
  # second attempt: 0 Warnings, 0 Errors

dotnet build tests\XlightsAI.Studio.Stems.Tests\XlightsAI.Studio.Stems.Tests.csproj --nologo   # 0 Warnings, 0 Errors

dotnet test  tests\XlightsAI.Studio.Stems.Tests\XlightsAI.Studio.Stems.Tests.csproj --nologo --filter "FullyQualifiedName~Step040"
  # re-run of every pre-existing Step 040 test, unmodified -> 8/8 passed (proves the new chunking code
  # path is 100% behavior-compatible with Step 040 when the new recipe fields are left at their defaults)

dotnet test  tests\XlightsAI.Studio.Stems.Tests\XlightsAI.Studio.Stems.Tests.csproj --nologo --logger "console;verbosity=detailed" --filter "FullyQualifiedName~Step041"
  # targeted run: 6/6 passed

dotnet build XlightsAI.Studio.slnx --nologo                                           # full solution, after Step 041 implementation -> 0 Warnings, 0 Errors
dotnet test  XlightsAI.Studio.slnx --nologo                                           # full regression, final
```

### Real compile-error fix (disclosed, not hidden)

The first `RunChunkedInferenceAndWriteStemsAsync` draft indexed `chunkStems[s, local]` directly inside the
overlap-add accumulation loop, where `chunkStems` is declared as a nullable `float[,]?` (because it may
come from either a `TryGet` out-parameter or a fresh `RunOneChunk` call). The C# compiler correctly
flagged `CS8602: Dereference of a possibly null reference` even though both branches unconditionally
assign it before that point. Fixed by introducing `var chunkStemsNonNull = chunkStems!;` immediately after
both branches converge, and indexing through that non-null local instead — a real compiler-caught
flow-analysis limitation, fixed before any test was run, not a hypothetical.

### Build results

Full solution: `Build succeeded. 0 Warning(s). 0 Error(s).`

### Targeted test results (Step 041 tests only, real run)

```
Passed XlightsAI.Studio.Stems.Tests.Step041AcceptanceTests.CorruptStaleChunkCacheEntry_IsDiscardedAndRecomputed_NegativePath [319 ms]
Passed XlightsAI.Studio.Stems.Tests.Step041AcceptanceTests.Step041_Acceptance_Passes_ChunkedOverlapAddMatchesGroundTruth [96 ms]
Passed XlightsAI.Studio.Stems.Tests.Step041AcceptanceTests.CancellationMidRun_LeavesNoPartialOutputAndNoFinalManifest_NegativePath [40 ms]
Passed XlightsAI.Studio.Stems.Tests.Step041AcceptanceTests.ResumeAfterCancellation_SkipsRecomputingAlreadyCachedChunks [89 ms]
Passed XlightsAI.Studio.Stems.Tests.Step041AcceptanceTests.ChunkedAndWholeClip_SameSourceAndRecipeShape_ProduceSameStemLengthsAndSync [104 ms]
Passed XlightsAI.Studio.Stems.Tests.Step041AcceptanceTests.Constructor_InvalidChunkOverlapRecipe_ThrowsStemSeparationRecipeInvalidException_NegativePath [6 ms]

Total tests: 6. Passed: 6, Failed: 0.
```

### Full regression (after implementation, final run)

```
Passed!  - Failed: 0, Passed:  71, Skipped: 0, Total:  71  - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed:  91, Skipped: 0, Total:  91  - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed:  44, Skipped: 0, Total:  44  - XlightsAI.Studio.Stems.Tests.dll
Passed!  - Failed: 0, Passed:  34, Skipped: 0, Total:  34  - XlightsAI.Studio.Audio.Tests.dll
Passed!  - Failed: 0, Passed:  25, Skipped: 0, Total:  25  - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed: 142, Skipped: 0, Total: 142 - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed:  82, Skipped: 0, Total:  82  - XlightsAI.Studio.Rhythm.Tests.dll
Passed!  - Failed: 0, Passed:  31, Skipped: 0, Total:  31  - XlightsAI.Studio.Reference.Tests.dll
```

**520 total, 0 failed, 0 skipped** — up from the Step 040 baseline of 514 (+6 new `Step041AcceptanceTests`
in the existing `Stems.Tests` project: 14 Step038 + 16 Step039 + 8 Step040 + 6 Step041 = 44 total in
`Stems.Tests.dll`, matching the reported `44` above). Every one of the 514 pre-existing tests still passes
unmodified, confirming no prior step's contract was broken.

## 5. Persisted-output inspection / observable demonstration for this step

**No standalone, non-test-harness manual demo program was run for this step.** Step 040's evidence
established a precedent of running a real throwaway console program under the session scratchpad
directory to exercise the mechanism independently of the xUnit test harness. When attempting the same
pattern for this step, the tool call to create that scratchpad demo project's directory was explicitly
rejected by the user; asked directly how to proceed, the user chose to rely on the xUnit test suite as
this step's observable evidence rather than retry or relocate the demo. This is disclosed here
truthfully rather than fabricating a demo run that did not happen.

In place of a standalone demo, the following already constitute real, non-mocked, persisted-output
evidence, run via the xUnit test harness against real on-disk files (not in-process fakes):

- Every test in `Step041AcceptanceTests.cs` uses a real `Microsoft.ML.OnnxRuntime.InferenceSession`
  running the real, committed `fixtures/onnx/synthetic-stem-separator-v1.onnx` graph (embedded as the
  same base64 payload Step 040 already verified byte-for-byte against the committed file), a real
  `ModelAssetManager` resolving a real pre-provisioned model file from a real temp directory, a real
  `WavAudioDecoder`/`StemWavWriter` round-trip through real `.wav` files on disk, and a real
  `FileSystemStemSeparationChunkCache` reading/writing real binary cache files under a real
  `.chunk-cache/` directory on disk (not an in-memory substitute) — only the *counting*/cancellation-
  trigger wrapper (`CountingChunkCache`) is test-local instrumentation layered transparently over the
  real file-backed cache, never a replacement for it.
- `Step041_Acceptance_Passes_ChunkedOverlapAddMatchesGroundTruth` is this step's own "long fixture": a
  2600-sample sine wave split into 9 real chunks of 320 samples with 64-sample overlap (a non-exact
  multiple, deliberately exercising the final partial chunk), producing real `.wav` files on disk whose
  decoded content recovers the known analytic gain to within float32 rounding error end-to-end through
  real chunking + real overlap-add merge.
- `CancellationMidRun_LeavesNoPartialOutputAndNoFinalManifest_NegativePath` cancels a real, in-flight
  10+-chunk run after two real chunks have completed and been persisted to the real on-disk chunk cache,
  then inspects the real stem output directory on disk and asserts it is empty — proving "partial output
  never marked complete" against the real filesystem, not a mock.
- `ResumeAfterCancellation_SkipsRecomputingAlreadyCachedChunks` runs the real separator twice against the
  same real on-disk chunk-cache directory — once cancelled after two chunks, once to completion — and
  asserts via the counting wrapper that chunks 0/1 were served as real cache hits on the second run
  (never recomputed), directly proving resumability against real persisted state, not merely that the
  final numeric output happens to be correct.
- `CorruptStaleChunkCacheEntry_IsDiscardedAndRecomputed_NegativePath` hand-writes 3 garbage bytes directly
  to the real on-disk path a legitimate chunk-0 cache file would occupy, then runs the real separator and
  confirms it detects the corruption, discards the file, recomputes chunk 0 from the real source audio,
  and discloses the discard in the returned manifest's `Warnings` — proving the corrupt-cache-detection
  mechanism against a real file on a real filesystem, not a simulated condition.

This is weaker than Step 040's own standalone-demo precedent in one respect (no independent, outside-the-
test-harness executable run) but the underlying I/O, ONNX inference, and filesystem operations exercised
by every assertion above are themselves fully real — nothing in this step's evidence is mocked at the
ONNX Runtime, file I/O, or audio-codec layer.

## 6. Fixtures / input hashes

| File | SHA-256 | Notes |
|---|---|---|
| `fixtures/onnx/synthetic-stem-separator-v1.onnx` | `5fc4b43c4121faa57751ee5b3d8d4a17601f46c7e39f2501308dcff65a100a75` | Real, committed (unchanged from Step 040). Reused as-is; no new model fixture needed for this step's own chunking/caching/cancellation scope. |

No new fixture files were added in this step. All test-run temp directories (`%TEMP%\xlai-step041-*\`) are
deleted by each test's own `IDisposable.Dispose()`.

## 7. Unresolved issues / known gaps

- **No standalone, non-test-harness manual demo was run for this step** (disclosed at length in §5) —
  the user explicitly chose to rely on the xUnit suite instead after the scratchpad demo directory
  creation was rejected. Every underlying operation the demo would have exercised (real ONNX inference,
  real file-backed chunk cache, real cancellation, real atomic stem-file writes) is still proven real and
  non-mocked through the test suite itself, but there is no independent, outside-the-harness confirmation
  for this specific step the way Step 040 recorded one.
- **Full-song mono decode and the final overlap-add merge buffers are still proportional to total song
  length**, not chunk-bounded — only the ONNX inference working set itself is bounded by `ChunkSamples`.
  Disclosed in §3: a true end-to-end bounded-memory pipeline would require Step 024's `WavAudioDecoder` to
  gain a streaming (not whole-file) decode path, which remains out of this step's scope.
- **Still mono-only, no resampling, no production separation model** — all three gaps are unchanged,
  disclosed carry-forwards from Step 040 (and, transitively, Step 038's own BLOCKED model-feasibility
  verdict). This step does not touch or resolve any of them.
- **No UI wiring (SCREEN 08 STEMS)** — disclosed, consistent with Steps 038-040's own precedent; remains
  Step 042's own explicit later scope.
- **Chunk cache entries are never explicitly pruned/garbage-collected** once a run completes successfully.
  A fully-completed run's `.chunk-cache/<cacheKey>/` directory is left on disk rather than deleted —
  disclosed, deliberate: leaving it costs only disk space (bounded by one song's chunk count × chunk size)
  and means re-running the exact same separation again (e.g. after deleting only the final stem `.wav`
  files) still resumes instantly from cache; an explicit cache-eviction policy was judged out of this
  step's own narrow scope and is not claimed to exist.

## 8. PASS / BLOCKED

**Status: PASS** (for this step's own narrow, disclosed scope — chunking/overlap/caching/cancellation/
bounded-inference-buffer mechanism, not production separation quality, which remains the phase-level
BLOCKED item carried forward from Step 038 and does not block this step's own required acceptance text).

- "Long fixture cancels cleanly and partial output never marked complete" (required acceptance,
  verbatim) — proven by `CancellationMidRun_LeavesNoPartialOutputAndNoFinalManifest_NegativePath` (a
  real, multi-chunk, in-flight run is cancelled after two chunks complete; the real stem output directory
  is confirmed empty — no `.wav`, no leftover `.tmp`) and by `Step041_Acceptance_Passes_
  ChunkedOverlapAddMatchesGroundTruth`'s own "long fixture" (9 real chunks, non-exact chunk/overlap
  arithmetic).
- "Add overlapping chunks, merge normalization, cancellation and resumable complete-chunk cache; enforce
  upper bound on working buffers" (implementation guidance, verbatim) — proven by: overlapping chunks +
  merge normalization (`Step041_Acceptance_Passes_ChunkedOverlapAddMatchesGroundTruth`, ground-truth
  recovery through real overlap-add); cancellation (`CancellationMidRun_...`); resumable complete-chunk
  cache (`ResumeAfterCancellation_SkipsRecomputingAlreadyCachedChunks`, real cache hits proven, not just
  correct final output); bounded working buffers (every chunked test uses `ChunkSamples` far smaller than
  the total sample count, and the ONNX input tensor is provably never larger than that bound by
  construction in `RunOneChunk`).
- "Invalid input, cancellation or stale persisted state must not corrupt the project" (failure/edge case,
  verbatim) — proven three ways: invalid input
  (`Constructor_InvalidChunkOverlapRecipe_ThrowsStemSeparationRecipeInvalidException_NegativePath`,
  `OverlapSamples >= ChunkSamples` rejected before any run can start); cancellation (`CancellationMidRun_
  ...`, no partial/corrupt output ever reaches the final output directory); stale persisted state
  (`CorruptStaleChunkCacheEntry_IsDiscardedAndRecomputed_NegativePath`, a hand-corrupted real cache file
  is detected, discarded, and recomputed cleanly rather than propagating bad data or crashing).
- Additional rigor beyond the minimum:
  `ChunkedAndWholeClip_SameSourceAndRecipeShape_ProduceSameStemLengthsAndSync` proves this step's new
  chunking machinery is behavior-compatible with Step 040's own whole-clip path for the same source, and
  all 8 pre-existing `Step040AcceptanceTests` were re-run and pass completely unmodified, proving no
  prior step's contract was broken by this step's internal rewrite of `OnnxStemSeparator`.
- Full regression: **520/520 passing**, 0 failed, 0 skipped; every pre-existing test from Steps 001-040
  still passes unmodified.
- **Disclosed, non-blocking gap for this step specifically:** no standalone, outside-the-test-harness
  manual demo was run (§5/§7) — the user explicitly chose to accept the xUnit suite as this step's
  observable evidence after the scratchpad demo directory creation was rejected. This does not change the
  PASS verdict for this step's own required acceptance text, which is fully satisfied by the real,
  non-mocked test evidence above, but it is recorded here as a known, disclosed limitation of this step's
  evidence relative to Step 040's own precedent.
- **Carried-forward, disclosed phase-level gaps (not this step's own blocker):** no production
  separation-quality claim exists yet anywhere in the repository (Step 038's own BLOCKED feasibility
  verdict, Step 044's own later scope); no stem-mixer UI exists yet (Step 042's own later scope). Step 041
  does not silently resolve or hide either gap.
