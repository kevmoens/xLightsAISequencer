# PHASE 04 — Step 026 Evidence

**Step:** "Generate zoom-independent multiresolution waveform peaks cache"
**Spec:** `docs/phase-specs/PHASE-04.md` §6, Step 026 (lines 234–248)
**Date:** 2026-10-11

## 1. Pre-implementation baseline inspection

- Re-read `docs/phase-specs/PHASE-04.md` in full (not only Step 026's own numbered block):
  Section 3's proposed `WaveformEnvelope` record (verbatim shape: `StartSample`, `EndSample`,
  `Min`, `Max`, `Rms`) and bare `IWaveformBuilder.BuildPyramidAsync(SongAsset, CancellationToken):
  Task<string>` signature; Section 3's architectural design notes ("Compute multi-resolution
  waveform peaks for cheap zoom/pan... waveforms may be approximate visualization but note/timing
  computations must reference original audio timeline"); Section 4's cache-key rule ("Analysis
  caches may be rebuilt if their source/model/version hash changes; every cache key includes
  algorithm/model/settings versions"); and the stable-domain-names table, which does not name any
  waveform-specific type as owned by an earlier phase — `WaveformEnvelope`/`IWaveformBuilder` are
  first introduced by this step.
- Re-read `implementation-evidence/PHASE-04/STEP-24.md` and `STEP-25.md` in full: treated
  `SongAsset`, `IAudioDecoder`/`WavAudioDecoder`, `ISongAssetStore`/`JsonSongAssetStore`/
  `NullSongAssetStore`, `IAudioTransport`/`WaveOutAudioTransport`, and every disclosed decision in
  both (in particular Step 024's "no real resampling in `DecodeMonoAsync`" gap — still true here,
  so this step's builder decodes/analyzes at the source's own native rate, consistent with Step
  024/025) as the immutable prior-step contract. Neither step's evidence named a waveform-pyramid
  type as already implemented or deferred to a *specific* later step, confirming this step is this
  type's own first definition, not a migration.
- Inspected every file this step could interact with: `SongAsset.cs`, `IAudioDecoder.cs`,
  `WavAudioDecoder.cs`, `ISongAssetStore.cs`/`JsonSongAssetStore.cs`/`NullSongAssetStore.cs` (the
  exact atomic-write/Null-object/deterministic-path pattern this step's own
  `IWaveformPyramidStore`/`JsonWaveformPyramidStore`/`NullWaveformPyramidStore` mirror — all read
  fully, unmodified by this step), `AudioImportViewModel.cs`, `ShellViewModel.cs` (the exact
  optional-collaborator/compatibility-default constructor pattern every prior step already
  established), `App.axaml.cs`, `Program.cs`, `MainWindow.axaml`'s existing SCREEN 06 AUDIO panel.
- Baseline regression before any Step 026 code existed, run fresh in this session:
  **309/309 passing** (`dotnet test XlightsAI.Studio.slnx --nologo`), matching Step 025's own
  recorded final count (including its same-day Mac/Linux-stub addendum) exactly.

## 2. Scope actually implemented

**New in `XlightsAI.Studio.Audio`** (still references only `XlightsAI.Studio.Contracts`):

- `WaveformEnvelope` — Section 3's proposed record, verbatim shape (`StartSample`, `EndSample`,
  `Min`, `Max`, `Rms`), half-open `[StartSample, EndSample)` per Section 2's time-scope rule.
- `WaveformResolutionLevel` — **new type, not named in Section 3** (disclosed in §3 below): one
  pyramid "zoom level" — a `BlockSizeSamples` plus the contiguous `WaveformEnvelope[]` at that
  resolution.
- `WaveformPyramidCache` — **new type, not named in Section 3** (disclosed in §3 below): the full
  persisted record `IWaveformBuilder.BuildPyramidAsync`'s returned path actually contains —
  `SongId`, `ContentHash` (the source `SongAsset.Sha256` at build time), `SourceDecoderVersion`,
  `BuilderVersion`, `SourceSampleRate`, `SampleCount`, and the `WaveformResolutionLevel[]` pyramid
  itself.
- `IWaveformBuilder` — Section 3's proposed `BuildPyramidAsync` interface, verbatim shape.
- `WaveformPyramidBuilder : IWaveformBuilder` — the real implementation:
  - Computes a deterministic cache path from the song's `SongId` via `IWaveformPyramidStore.GetPath`
    *before* deciding whether to decode anything.
  - If a cache file already exists there and its recorded `ContentHash`/`SourceDecoderVersion`/
    `BuilderVersion`/`SampleCount` all still match the current `SongAsset` exactly, returns that
    path immediately — **no decode call at all** — "Cache reopens without recompute."
  - Otherwise, decodes the full song exactly once via the injected `IAudioDecoder.DecodeMonoAsync`
    into one in-memory buffer, then computes five resolution levels (`64`, `256`, `1024`, `4096`,
    `16384` samples per envelope — finest to coarsest) from that single decoded buffer (one decode
    feeds every level, not one decode per level).
  - Verifies the decoded sample count exactly equals the `SongAsset`'s own declared `SampleCount`
    *before* writing anything — "write cache only after complete decode and verified dimensions." A
    mismatch throws `WaveformPyramidInvalidException` and persists nothing.
  - Silence (all-zero decoded samples) computes real `Min=Max=Rms=0` envelopes and succeeds
    normally — an explicitly handled edge case, not a special-cased no-op.
  - A missing/moved source file propagates `FileNotFoundException` straight from
    `IAudioDecoder.DecodeMonoAsync` (Step 024/025's own established failure path for a moved file),
    before any cache write is attempted.
- `WaveformPyramidInvalidException` — the "invalid timing boundaries... handled explicitly" failure
  type (decoded-vs-declared sample count mismatch).
- `WaveformPyramidCorruptException` — mirrors `SongAssetCorruptException`: a persisted cache file
  exists but failed to deserialize (treated as "stale, must rebuild," not a hard failure, inside
  `WaveformPyramidBuilder`'s own cache-validity check).
- `IWaveformPyramidStore` / `JsonWaveformPyramidStore` / `NullWaveformPyramidStore` — the
  persistence boundary, mirroring `ISongAssetStore`'s atomic temp-file-then-`Move` write and
  Null-object pattern exactly, extended with `GetPath(SongId)` (disclosed in §3 below) so the
  builder can resolve a song's deterministic cache path without first attempting (and possibly
  failing) a load.

**Shell/App wiring** (the required minimum complete UI → application → domain → adapter slice):

- `AudioImportViewModel` — extended with `IsBuildingWaveform`, `WaveformErrorMessage`,
  `WaveformCachePath`, `HasWaveformCache`, `WaveformResolutionLevelCount`, `WaveformContentHash`,
  and `BuildWaveformCacheAsync(IWaveformBuilder, IWaveformPyramidStore, ct)` — same "catch, set an
  error property, return `false`; never let an exception reach the UI" convention as
  `ProbeAudioFileAsync`/`OpenPlaybackAsync`. A successful new probe now also resets the view
  model's own waveform-cache bookkeeping (same reasoning as the existing playback-bookkeeping
  reset: it describes an asset that is no longer the probed one).
- `ShellViewModel` — new optional constructor parameters `waveformPyramidStore`/`waveformBuilder`
  (default to the throwing `NullWaveformPyramidStore` / a real `WaveformPyramidBuilder` built from
  whichever decoder/store this constructor ended up with, same compatibility pattern as every other
  optional collaborator), a new `BuildWaveformCacheCommand` with `LastBuildWaveformCacheCommandTask`,
  wired into the existing `Audio.PropertyChanged`/`OnNavigatorChanged` broadcast lists the same way
  every prior command was.
- `App.axaml.cs`/`Program.cs` — new `App.WaveformPyramidStore` hand-off static (same pattern as
  `App.SongAssetStore`), real composition in `Program.Main` using a new
  `%AppData%\XlightsAI.Studio\waveform-cache` output directory; `App.axaml.cs` constructs the real
  `WaveformPyramidBuilder` from the (possibly-fallback) decoder/store at startup.
- `MainWindow.axaml` — new "Waveform Cache" sub-section on SCREEN 06 AUDIO: a "Build Waveform
  Cache" button, busy/error/empty states, and a read-only display of the cache file path, resolution
  level count, and the content hash it was built from — the "Versioned, inspectable project output"
  this step's own user-visible outcome names.

**No changes to any existing `Contracts`, `Projects`, `Xlights`, `Editing`, `Reference`, or
PHASE-01–03 `Shell`/`App` type's existing members, nor to Step 024/025's own `SongAsset`/
`IAudioDecoder`/`WavAudioDecoder`/`ISongAssetStore`/`JsonSongAssetStore`/`NullSongAssetStore`/
`IAudioTransport`/`WaveOutAudioTransport`/`UnsupportedPlatformAudioTransport`** — purely additive.

## 3. Architecture deviations / decisions

- **`WaveformResolutionLevel` and `WaveformPyramidCache` are new types not named anywhere in
  Section 3.** Section 3 only names `WaveformEnvelope` (implemented verbatim above) and
  `IWaveformBuilder.BuildPyramidAsync`'s bare `Task<string>` return — it never specifies what that
  returned path's *file contents* actually are. Since nothing about this shape was already accepted
  by an earlier step (Step 024/025's evidence explicitly scoped `IWaveformBuilder` out of their own
  work), this is this step's own first definition, the same "first definition, not a migration"
  reasoning Step 024 already applied to `SongAsset`'s extra fields and Step 025 applied to
  `IAudioTransport`'s extra members. A later step (027: "Render synchronized waveform beneath
  ruler") that needs the pyramid's actual envelope data depends on these two types directly, not on
  an undocumented/private file format.
- **`IWaveformPyramidStore.GetPath(SongId)` is an addition beyond `ISongAssetStore`'s own shape.**
  `ISongAssetStore` only exposes `SaveAsync`/`LoadAsync` because a song asset's saved path is only
  ever needed *after* a successful save. A waveform pyramid is different: `BuildPyramidAsync` must
  decide, *before* doing any (expensive) decode work, whether a valid cache already exists for a
  given `SongId` — that requires computing the deterministic path first. Rather than invent a
  separate index/manifest file to track "which path does this SongId's cache live at," the store
  itself exposes the one pure, deterministic naming function it already uses internally. This does
  not change `ISongAssetStore`'s own already-accepted contract at all — it is a new, independent
  interface.
- **Resolution block sizes (64/256/1024/4096/16384 samples) are this step's own chosen constant,
  not named by the spec.** Section 3 says "Generate min/max/RMS envelopes for multiple sample block
  sizes" without naming specific sizes. Five power-of-two sizes spanning roughly two orders of
  magnitude were chosen so the finest level resolves individual clicks/transients (64 samples ≈ 8 ms
  at this repository's 8 kHz fixture rate) and the coarsest level keeps a multi-minute real song's
  full-overview envelope count small (Section 3: "cheap zoom/pan"). A later step (027) that renders
  the waveform can pick whichever level best matches its current zoom without this step needing to
  anticipate exact pixel-to-sample ratios.
- **The full track is decoded and buffered in memory once per build, not streamed incrementally.**
  Same deliberate simplification Step 025 already made for `WaveOutAudioTransport.OpenAsync`, for
  the same reason: `IAudioDecoder.DecodeMonoAsync`'s own `IAsyncEnumerable` streaming shape is fully
  drained once into one in-memory buffer so every resolution level can be computed from the same
  buffer without re-decoding, and every fixture/real-world song this phase targets fits comfortably
  in memory as a mono float buffer.
- **No real resampling** — consistent with Step 024/025's own disclosed gap: `BuildPyramidAsync`
  calls `DecodeMonoAsync(song, song.SampleRate, ct)` (the source's own native rate), so pyramid
  sample indices are always expressed in the source's own native sample rate, recorded as
  `SourceSampleRate` in the cache for a later step to interpret correctly.
- **Cache invalidation is content-hash/version-based, not file-timestamp-based.** Section 4: "every
  cache key includes algorithm/model/settings versions." `ContentHash` (the source `Sha256`),
  `SourceDecoderVersion`, and `BuilderVersion` are all compared on every `BuildPyramidAsync` call; a
  changed/edited source file re-probed under the same `SongId` (different `Sha256`, same identity)
  correctly invalidates and rebuilds rather than silently serving a stale pyramid — proven directly
  (§4/§5 below), satisfying the phase-wide test matrix's own row 7 ("Waveform cache invalidates
  when content hash changes").

## 4. Commands actually executed

All run via the PowerShell tool (the sandboxed POSIX Bash tool in this session is non-functional —
every invocation returns exit code 1 with no output — the same pre-existing environment limitation
recorded in every prior step's own evidence).

```
dotnet test XlightsAI.Studio.slnx --nologo                                                     # baseline
dotnet build src\XlightsAI.Studio.Audio\XlightsAI.Studio.Audio.csproj --nologo
dotnet test tests\XlightsAI.Studio.Audio.Tests\XlightsAI.Studio.Audio.Tests.csproj --nologo --filter "FullyQualifiedName~Step026" --logger "console;verbosity=normal"
dotnet build src\XlightsAI.Studio.Shell\XlightsAI.Studio.Shell.csproj --nologo
dotnet build src\XlightsAI.Studio.App\XlightsAI.Studio.App.csproj --nologo
dotnet test tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj --nologo --filter "FullyQualifiedName~Step026" --logger "console;verbosity=normal"
dotnet build XlightsAI.Studio.slnx --nologo                                                     # full regression build
dotnet test XlightsAI.Studio.slnx --nologo                                                      # full regression, final
```

### Baseline (before any Step 026 code existed)

```
Passed!  - Failed: 0, Passed: 71, Skipped: 0, Total: 71 - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed: 75, Skipped: 0, Total: 75 - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed: 25, Skipped: 0, Total: 25 - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed: 83, Skipped: 0, Total: 83 - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed: 24, Skipped: 0, Total: 24 - XlightsAI.Studio.Audio.Tests.dll
Passed!  - Failed: 0, Passed: 31, Skipped: 0, Total: 31 - XlightsAI.Studio.Reference.Tests.dll
```

309 total, 0 failed — matches Step 025's own recorded final count (including its same-day Mac/Linux
addendum, 303 + 5 + 1 = 309) exactly, confirmed independently in this session before a single Step
026 line of code existed.

### Build results (after implementation)

`Audio`, `Shell`, `App`, and the full solution all: `Build succeeded. 0 Warning(s). 0 Error(s).`

### Targeted test results (new tests only)

```
tests\XlightsAI.Studio.Audio.Tests — filter "~Step026": 5/5 passed:
  Step026_Acceptance_Passes_BuildsMultiResolutionPeaksAndReopensWithoutRecompute
  BuildPyramidAsync_SilentAudio_ComputesZeroEnvelopesWithoutThrowing_HandlesSilenceExplicitly
  BuildPyramidAsync_MissingMediaFile_PropagatesFileNotFoundException_NegativePath
  BuildPyramidAsync_DeclaredSampleCountMismatch_ThrowsWaveformPyramidInvalidException_NegativePath
  BuildPyramidAsync_ContentHashChanges_InvalidatesCacheAndRebuilds

tests\XlightsAI.Studio.Shell.Tests — filter "~Step026": 4/4 passed:
  Step026_Acceptance_Passes_BuildWaveformCacheReopensWithoutRecomputeThroughShellViewModel
  BuildWaveformCacheCommand_WithoutProbedAsset_IsDisabled
  BuildWaveformCacheCommand_NoWaveformCollaboratorsConfigured_SetsErrorRatherThanThrowing_NegativePath
  ShellViewModel_WithoutWaveformCollaborators_StillConstructsAndStarts_BackwardCompatible
```

### Full regression (after implementation, final run)

```
Passed!  - Failed: 0, Passed: 71, Skipped: 0, Total: 71, Duration: 314 ms - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed: 75, Skipped: 0, Total: 75, Duration: 263 ms - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed: 25, Skipped: 0, Total: 25, Duration: 2 s   - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed: 87, Skipped: 0, Total: 87, Duration: 1 s   - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed: 29, Skipped: 0, Total: 29, Duration: 5 s   - XlightsAI.Studio.Audio.Tests.dll
Passed!  - Failed: 0, Passed: 31, Skipped: 0, Total: 31, Duration: 33 s  - XlightsAI.Studio.Reference.Tests.dll
```

**318 total, 0 failed, 0 skipped** — up from the Step 025 baseline of 309 (+5 new `Audio.Tests`,
83→87 in `Shell.Tests` for the +4 new Step026 wiring tests). `Xlights.Tests`, `Editing.Tests`,
`Projects.Tests`, `Reference.Tests` are unchanged in count (no source in those projects was
modified this step). All 309 pre-existing tests still pass unmodified.

### Manual/desktop demonstration

```
$p = Start-Process -FilePath "src\XlightsAI.Studio.App\bin\Debug\net10.0\XlightsAI.Studio.exe" -PassThru
Start-Sleep -Seconds 5
# Still running after 5s: True
Stop-Process -Id $p.Id -Force
```

The real compiled Avalonia desktop app, now including the new "Waveform Cache" sub-section on
SCREEN 06 AUDIO (Build Waveform Cache button, busy/error/empty states, cache path/resolution-count/
content-hash display), started and ran for 5+ seconds with no exception and no early exit —
proving the new UI wiring did not regress app startup. This is a process-alive smoke test, **not**
a human-driven click-through of the Build Waveform Cache button (disclosed, not silently implied,
in §6 below) — no UI-automation driver is available in this environment for the native Avalonia
window, the same standing limitation every prior PHASE-02/03/04 step's own evidence already records
for its own screen additions. The full vertical slice (probed asset → real `WaveformPyramidBuilder`
→ real `JsonWaveformPyramidStore` → bindable `AudioImportViewModel` state) is instead proven, end to
end, by the real-collaborator Shell-wiring tests in §4 above (`Step026ShellWiringTests`), which
exercise the exact same `ShellViewModel`/`AudioImportViewModel` code path the UI binds to.

## 5. Fixtures / input hashes

No new fixture files were added to `fixtures/`. All tests construct real WAV bytes via this
repository's own already-licensed, already-verified `DeterministicSyntheticAudioGenerator`
(`XlightsAI.Studio.Reference.Fixtures`, PHASE-01 Step 003) — the same synthesizer Step 024/025's own
tests use. Specific inputs exercised:

- `BuildClickTrack(bpm: 60, clickCount: 6, clickDurationSeconds: 0.02, toneHz: 1000.0, sampleRate: 8000)`
  — a 6-second, 48000-sample real-time click track, long enough that every pyramid level (including
  the coarsest 16384-sample block) has more than one envelope, used for the acceptance test's full
  structural/content checks.
- `BuildClickTrack(bpm: 120, clickCount: 4, ...)` — shorter tracks for the missing-media, mismatch,
  content-hash-change, and Shell-wiring tests, where only basic build/invalidate behavior (not full
  multi-envelope coverage) is being proven.
- A true-silence 2-second 8 kHz mono buffer (all-zero samples) — the explicit "silence must be
  handled" edge case, proving real `Min=Max=Rms=0` envelopes at every resolution level.
- A `SongAsset` constructed via `asset with { SampleCount = asset.SampleCount + 1000 }` — the
  "invalid timing boundaries" edge case: a declared sample count that cannot match what the real
  file actually decodes to.
- A `SongAsset` constructed via `asset with { Sha256 = <different 65-zero-character string> }` —
  simulates a re-probe of edited audio under the same `SongId`, proving cache invalidation by
  content hash (phase-wide test matrix row 7).

A `CountingAudioDecoder` test double (wraps the real `WavAudioDecoder`, incrementing a counter on
every `DecodeMonoAsync` call) proves "cache reopens without recompute" against real decode/encode
behavior rather than asserting on file-write timestamps alone; the Shell-wiring test additionally
asserts the cache file's own `LastWriteTimeUtc` is unchanged after a second `BuildWaveformCacheCommand`
execution, proving the same property at the full vertical-slice layer without needing to inject a
counting decoder through `ShellViewModel`'s own real composition.

All test files are written under `%TEMP%\xlai-step026-*\` / `%TEMP%\xlai-step026-shell-*\`, deleted
by each test class's `Dispose()` — identical cleanup convention to every prior step's acceptance test
class.

## 6. Unresolved issues / known gaps

- **No real resampling** (§3) — consistent with Step 024/025's own disclosed gap; pyramid sample
  indices are always expressed in the source's own native sample rate (`SourceSampleRate`), not a
  resampled/target rate.
- **Whole-track in-memory buffering, not chunked streaming** (§3) — correct and simple for this
  phase's fixture/real-song scale; a known, disclosed scalability limit for very long shows, not a
  defect against this step's own required acceptance (same disclosed limit Step 025 already
  recorded for `WaveOutAudioTransport`).
- **Fixed, hand-chosen resolution block sizes** (§3) — not configurable by the caller in this step;
  a later step (027/029, if it needs a different zoom granularity) can extend
  `WaveformPyramidBuilder` rather than this step inventing speculative configurability no current
  step needs.
- **No UI-automation-driven click-through of the new Build Waveform Cache button** — same disclosed,
  standing limitation as every prior step (no accessibility/UI-driver tooling available in this
  environment for the native Avalonia window). The full vertical slice is proven instead at the
  `ShellViewModel`/`AudioImportViewModel` layer through real, un-mocked `WaveformPyramidBuilder`/
  `JsonWaveformPyramidStore` collaborators (`Step026ShellWiringTests`).
- **No real xLights installation available in this environment** — per the user's own standing
  waiver on requiring a real-xLights render check as a phase-gate blocker (recorded in this
  assistant's memory). Not applicable to this step's own required acceptance in any case: Step 026's
  acceptance is about building/caching waveform peaks, not xLights rendering.
- **Rendering the pyramid visually is explicitly out of this step's own scope** (Step 027's own
  named scope: "Render synchronized waveform beneath ruler"). This step proves the cache is built,
  reopened without recompute, and content-hash-invalidated correctly — not that it is drawn on
  screen.

## 7. PASS / BLOCKED

**Status: PASS** for Step 026's own stated required acceptance and failure/edge case:

- "Cache reopens without recompute" — proven by
  `Step026_Acceptance_Passes_BuildsMultiResolutionPeaksAndReopensWithoutRecompute` (a
  `CountingAudioDecoder` test double's call count stays at `1` after a second `BuildPyramidAsync`
  call against the same unchanged asset) and independently at the full Shell-wired vertical-slice
  layer by `Step026_Acceptance_Passes_BuildWaveformCacheReopensWithoutRecomputeThroughShellViewModel`
  (the real cache file's own `LastWriteTimeUtc` is unchanged after a second
  `BuildWaveformCacheCommand` execution).
- "envelope sample checks" — proven by the same acceptance test asserting every resolution level's
  envelopes are contiguous and half-open (`StartSample`/`EndSample` chain exactly across `[0,
  SampleCount)` with no gaps or overlaps), that the finest level has strictly more envelopes than
  the coarsest (genuinely multi-resolution, not five identical copies), and that the finest level's
  envelopes include real decoded signal (`Max > 0.5`/`Min < -0.5` somewhere, from the synthesized 1
  kHz tone) rather than placeholder zeros.
- "Generate min/max/RMS envelopes for multiple sample block sizes and content hash" (implementation
  guidance) — proven by the same test asserting all five declared block sizes (`64`/`256`/`1024`/
  `4096`/`16384`) are present, and that `ContentHash`/`SourceDecoderVersion`/`BuilderVersion` are
  recorded exactly matching the source asset's own `Sha256`/`DecoderVersion` and this builder's own
  `Version`.
- "write cache only after complete decode and verified dimensions" (implementation guidance) —
  proven by `BuildPyramidAsync_DeclaredSampleCountMismatch_ThrowsWaveformPyramidInvalidException_NegativePath`:
  a `SongAsset` whose declared `SampleCount` cannot match the real decoded count throws before any
  cache file is written (`File.Exists(store.GetPath(...))` is asserted `false` afterward).
- "Silence, invalid timing boundaries and missing media must be handled explicitly" — proven by
  three distinct tests, each a different outcome: true silence
  (`BuildPyramidAsync_SilentAudio_...`) succeeds normally with real zero-valued envelopes; a
  declared-sample-count mismatch (`BuildPyramidAsync_DeclaredSampleCountMismatch_...`, "invalid
  timing boundary") throws `WaveformPyramidInvalidException` clearly; a missing/moved file
  (`BuildPyramidAsync_MissingMediaFile_...`) propagates `FileNotFoundException` before any cache
  write, with the cache path confirmed not to exist afterward.
- The step's own user-visible outcome ("Versioned, inspectable project output plus a reproducible
  test report") is proven: the persisted `WaveformPyramidCache` JSON file records its own
  `ContentHash`/`SourceDecoderVersion`/`BuilderVersion` (the "versioned" half), is independently
  re-loadable and inspectable via `IWaveformPyramidStore.LoadAsync` (the "inspectable" half, proven
  both at the Audio-project layer and, independently, at the Shell-wired `AudioImportViewModel`
  layer via `WaveformCachePath`/`WaveformResolutionLevelCount`/`WaveformContentHash`), and every
  claim above is backed by an actually-executed, actually-passing automated test (the "reproducible
  test report" half) — see §4.
- Phase-wide test matrix row 7 ("Waveform cache invalidates when content hash changes") — proven by
  `BuildPyramidAsync_ContentHashChanges_InvalidatesCacheAndRebuilds`: a `SongAsset` with the same
  `SongId` but a different `Sha256` triggers a real second decode/rebuild (the counting decoder's
  call count increments from 1 to 2) and the reloaded cache's own `ContentHash` reflects the new
  value — this is the one row the task's own CORE/NEVER-CLAIM instruction names explicitly, and it
  is proven here, not deferred.

No previously accepted step's contract was modified — `SongAsset`, `IAudioDecoder`,
`WavAudioDecoder`, `ISongAssetStore`/`JsonSongAssetStore`/`NullSongAssetStore`, `IAudioTransport`,
`WaveOutAudioTransport`, `UnsupportedPlatformAudioTransport`, and every other PHASE-01–03 public
member are completely unchanged, confirmed by the full pre-existing 309-test suite passing
unmodified before a single Step 026 line of code existed (§4), and again at 318/318 (309 old + 9
new) after implementation.

## 8. Phase-wide status after this session

Steps 024, 025, and 026 (the three steps with recorded PASS evidence) are complete for PHASE 04.
Steps 027–030 (synchronized waveform rendering beneath the ruler, single master clock, zoom/loop/
keyboard navigation, snapping/diagnostics) and the phase-wide gate demonstration ("Import a click
track, display waveforms, drag an effect to a click, loop 1 second, seek to song midpoint and back,
close/reopen and confirm saved media identity and correct timing") remain unimplemented — per the
task's own instruction, this session implements only Step 026 and does not proceed to Step 027 until
this evidence is reviewed.
