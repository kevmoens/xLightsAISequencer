# PHASE 06 — Step 042 Evidence

**Step:** "Add waveform tracks, solo/mute and synchronized stem playback"
**Spec:** `docs/phase-specs/PHASE-06.md` §6, Step 042 (lines 268-282)
**Date:** 2026-10-12

## 1. Pre-implementation baseline inspection

- Read `docs/phase-specs/PHASE-06.md` in full again for this step: Step 042's own required acceptance
  ("Audibly isolated stems stay aligned with source"), its own implementation guidance ("Use single
  transport timebase to solo/mute stems; keep origin offset and resample alignment visible; cache
  separate waveform peaks"), its failure/edge case (shared phase-wide boilerplate text, reinterpreted
  for this step's own mixdown boundary), and Section 3's architectural note, verbatim: "Solo/mute UI is
  built on existing shared audio transport, not six independent drifting clocks."
- Read `implementation-evidence/PHASE-06/STEP-041.md` in full (immediately preceding step). Confirmed
  its own disclosed carry-forward gaps: "no stem-mixer UI exists yet (Step 042's own later scope)." This
  step closes exactly that disclosed gap.
- Inspected every existing file in `src/XlightsAI.Studio.Stems/` (`StemAsset`, `StemManifest`,
  `StemKind`, `StemWavWriter`, `IStemSeparator`/`OnnxStemSeparator`, the Step 038/039 model-feasibility
  types, the Step 041 chunk-cache types) before writing anything. Confirmed that for a non-chunked (or
  chunked-but-correct) separation run, every `StemAsset` in one `StemManifest` shares the same
  `SampleRate`, `SampleCount` and `SourceOriginTicks` as the source song (Step 040's own "audio sync
  assertion", proven by its own still-passing tests) — this step's mixdown engine treats that as a
  checked invariant, not an assumption.
- Inspected the existing `XlightsAI.Studio.Audio` project in full before writing anything:
  `IAudioTransport`/`WaveOutAudioTransport` (the one real device transport — no volume/multi-channel
  mixing primitive exists there), `MasterPlaybackClock`, `IAudioDecoder`/`WavAudioDecoder`,
  `IWaveformBuilder`/`WaveformPyramidBuilder`/`IWaveformPyramidStore`/`JsonWaveformPyramidStore`
  (Step 026's already-real, per-`SongAsset.Id` waveform-pyramid cache — reused as-is, not reimplemented),
  and `SongAsset` itself (a probed file always gets a fresh random `SongId` and `OriginTicks = 0` —
  important below).
- Inspected `src/XlightsAI.Studio.Shell/AudioImportViewModel.cs` and
  `tests/XlightsAI.Studio.Shell.Tests/Step028ShellWiringTests.cs` in full to confirm this project's own
  established view-model conventions (methods take collaborator interfaces as call-site parameters, not
  constructor-injected singletons; every action is `async Task<bool>` that catches and reports via an
  `ErrorMessage` property rather than throwing to the UI; a real `WaveOutAudioTransport` — never a mock —
  is used directly in "ShellWiringTests" real-device test files). `StemMixerViewModel` (below) follows
  this exact shape.
- Baseline regression before any Step 042 code existed, run fresh in this session:
  **520/520 passing** (`dotnet test XlightsAI.Studio.slnx --nologo`) — exactly Step 041's own final
  count, confirming the starting state matched the prior step's own recorded evidence before any
  Step 042 line of code existed.
- Decision made before writing code: a `SongAsset` built by a plain `IAudioDecoder.ProbeAsync` call on a
  stem file always mints a fresh random `SongId` and resets `OriginTicks` to `0` — silently losing
  Section 2's "preserve exact OriginalOriginTicks" rule and breaking `WaveformPyramidBuilder`'s own
  per-`SongId` cache-reuse guarantee (a new random Id every call defeats its cache). This step therefore
  introduces one new, narrow adapter (`StemSongAssetFactory`) rather than calling `ProbeAsync` directly
  at every call site — the one place a `StemAsset` is turned into a playable/cacheable `SongAsset`,
  cross-checking the re-probed file against the stem's own already-declared metadata (catches a
  moved/corrupted/stale stem file) and then restoring the real, preserved `OriginTicks` and a
  deterministic `SongId` derived from the stem's own identity.

## 2. Scope actually implemented

### New files — `src/XlightsAI.Studio.Stems/`

- **`StemChannelState.cs`** — pure data: `(Guid StemId, bool Muted, bool Soloed)`.
- **`StemMixRules.cs`** — pure, no-I/O solo/mute resolution rule shared by the mixdown engine and any
  future UI preview: mute always silences its own channel even if also soloed; solo (when at least one
  channel is soloed) silences every other, non-soloed channel. A stem with no explicit channel state
  defaults to unmuted/unsoloed.
- **`StemSongAssetFactory.cs`** — turns one `StemAsset` into a playable/waveform-able `SongAsset`: real
  re-probe of the file (so a moved/corrupted stem is still caught), cross-checked against the stem's own
  declared `SampleRate`/`SampleCount`/`Sha256` (mismatch throws
  `StemMixdownIncompatibleStemsException`), then overrides `Id`/`OriginTicks` so repeated calls for the
  same stem resolve to the same deterministic `SongId` while preserving the real source-origin alignment.
- **`StemMixdownIncompatibleStemsException.cs`** — thrown when stems being mixed do not share a common
  sample rate/sample count/source origin, or a stem's file no longer matches its own declared metadata.
- **`IStemMixdownRenderer.cs`** / **`StemMixdownRenderer.cs`** — the real (no third-party dependency)
  mixdown engine: validates every stem in the manifest shares the same shape, resolves the audible subset
  via `StemMixRules`, sums their real decoded samples (via the already-real `WavAudioDecoder`),
  clamps to `[-1, 1]`, and writes the result to a deterministically-keyed on-disk cache file
  (`<cacheDirectory>/.mixdown-cache/<sha256-of-runId+active-stem-ids>.wav`) via an atomic
  `.tmp`-then-`File.Move` sequence (same pattern as Step 039/041's own cache writers). A cache hit is
  re-probed and reused without touching the stem files at all — proven directly (§4) by deleting the
  source stem files after the first render and confirming the second, identical-selection render still
  succeeds. A corrupt/stale cache entry (failed re-probe or shape mismatch) is silently discarded and
  recomputed, the same "stale persisted state must not corrupt the project" guarantee Step 041
  established for its own chunk cache.

### New files — `src/XlightsAI.Studio.Shell/`

- **`StemMixerViewModel.cs`** (also defines `StemChannelViewModel`, one bindable row per stem) — SCREEN
  08 STEMS' view model, following `AudioImportViewModel`'s exact conventions:
  - `LoadManifest(manifest)` — one fresh unmuted/unsoloed channel per stem.
  - `ToggleMute`/`ToggleSolo` — pure state mutation on a channel.
  - `ApplyMixAndReopenAsync(renderer, transport, ct)` — the step's own "single transport timebase"
    mechanism: renders the current selection via `IStemMixdownRenderer`, opens the result on the one
    caller-supplied `IAudioTransport` (never a second/parallel transport), and — if a mix was already
    open and playing — restores the exact same song-relative position and resumes playback afterward
    (every mix shares the same `OriginTicks`/duration regardless of selection, so the position carries
    over unchanged across a remix). Any failure sets `ErrorMessage` and returns `false`.
  - `PlayAsync`/`PauseAsync`/`RefreshPlaybackPosition` — thin delegations to the shared transport,
    mirroring `AudioImportViewModel`'s own identical methods.
  - `BuildWaveformsAsync(decoder, builder, ct)` — builds (or reopens) a real `WaveformPyramidCache` per
    stem via the already-real `WaveformPyramidBuilder`/`StemSongAssetFactory`, populating each channel's
    `WaveformCachePath` — the step's own "waveform tracks" / "cache separate waveform peaks" scope.
- **`XlightsAI.Studio.Shell.csproj`** — added a `ProjectReference` to `XlightsAI.Studio.Stems` (Shell did
  not previously depend on Stems at all).

### New test files

- **`tests/XlightsAI.Studio.Stems.Tests/Step042AcceptanceTests.cs`** — 8 tests against
  `StemMixdownRenderer`/`StemMixRules` directly, using real on-disk WAV stems written by the already-real
  `StemWavWriter` (no ONNX/model fixture needed — this step mixes already-produced stems, it does not
  separate anything).
- **`tests/XlightsAI.Studio.Shell.Tests/Step042StemMixerViewModelTests.cs`** — 6 fast, hardware-free unit
  tests for `StemMixerViewModel`'s own orchestration, using a real in-memory `IAudioTransport` test
  double (an independently-verifiable clock model with no hidden timer — position only ever changes via
  an explicit `Open`/`Seek`/`Advance` call) to isolate the view model's own reopen/reseek/resume logic
  from real device timing, mirroring this project's own established `Step028TimelineViewModelTests` /
  `Step028ShellWiringTests` split (fast unit tests for logic, a separate real-device file for the
  hardware slice — see next).
- **`tests/XlightsAI.Studio.Shell.Tests/Step042ShellWiringTests.cs`** — 2 real-device integration tests
  (`[Trait("Category", "Integration")]`, Windows-only), driving a real `WaveOutAudioTransport` (the real
  Windows winmm device on this machine) through `StemMixerViewModel`: this step's own required
  acceptance text, verbatim ("Audibly isolated stems stay aligned with source"), proven against real,
  device-reported playback position, not an estimate.

**No changes to any `Contracts`, `Editing`, `Xlights`, `Projects`, `Reference`, `Rhythm`, or Step 038-041
`Stems`/`Audio` file** — `StemAsset`, `StemManifest`, `StemKind`, `StemWavWriter`,
`OnnxStemSeparator`/`IStemSeparator`, every Step 038/039/041 model-asset/chunk-cache type, `SongAsset`,
`IAudioTransport`/`WaveOutAudioTransport`, `IWaveformBuilder`/`WaveformPyramidBuilder`/
`IWaveformPyramidStore`/`JsonWaveformPyramidStore`, and `AudioImportViewModel` are all reused completely
unchanged.

## 3. Architecture deviations / decisions

- **No `ShellScreen.Stems` / `ShellViewModel` navigation wiring in this step.** Disclosed, deliberate:
  Steps 038-041 each explicitly disclosed "no UI wiring (SCREEN 08 STEMS)" as out of their own scope and
  deferred it to this step; this step ships the complete, independently-testable `StemMixerViewModel`
  application-layer slice (load/toggle/apply-mix/build-waveforms) and proves it end-to-end against a real
  device transport, but does **not** also invent a `ShellScreen.Stems` enum value, navigation commands,
  and `ShellViewModel` property-changed cascades for a screen whose upstream trigger (a "Run Separation"
  action that actually produces a `StemManifest` from inside the shell) does not exist anywhere in
  `ShellViewModel` yet either — Steps 038-041 only ever exercise `OnnxStemSeparator` directly from test
  code, never from the shell. Wiring one new screen's navigation shell around a feature whose own
  upstream producer is still entirely headless would be scaffolding ahead of an actual need, not the
  "smallest complete... pathway" this project's own instructions call for. `StemMixerViewModel` is
  constructed exactly the same way `AudioImportViewModel`/`TimelineViewModel` were before their own later
  `ShellViewModel` integration steps — a real, fully tested, standalone view model ready for that wiring,
  not a stub.
- **Solo/mute reached via full remix-and-reopen, not live per-channel gain/volume control.** Disclosed,
  deliberate, and the direct, literal reading of Section 3's own text: "Solo/mute UI is built on existing
  shared audio transport, not six independent drifting clocks." `IAudioTransport` has no per-channel
  volume primitive today (`WinmmNativeMethods` only exposes a single-stream `waveOut*` surface), and
  `WaveOutAudioTransport.OpenAsync` already always-fully-decodes its input up front (Step 025's own
  established pattern) — so re-rendering the currently-audible subset to one new mono `SongAsset` and
  reopening the *same* transport on it is the smallest change consistent with both the existing transport
  contract and the "one shared transport" rule, rather than inventing a six-stream native mixing layer.
  The real cost (one re-decode-and-sum per distinct solo/mute combination) is bounded by
  `StemMixdownRenderer`'s own content-addressed cache (§2/§4): the same combination is never recomputed
  twice.
- **Position is preserved across a remix by re-seeking after reopen, not by any special "seamless
  switch" transport feature.** Disclosed: because every rendered mix shares the exact same
  `OriginTicks`/`SampleCount` (enforced by `StemMixdownRenderer`'s own alignment check), the
  song-relative position recorded before `OpenAsync` replaces the transport's `CurrentSong` is still
  valid immediately afterward — `StemMixerViewModel.ApplyMixAndReopenAsync` simply records
  `transport.PositionTicks` before the reopen and re-issues it as a `SeekAsync` afterward (clamped to the
  new mix's own duration, defensively, even though it is always identical in practice). This is
  observably not a "seamless" crossfade — there is a brief, real gap while the new mix opens — but the
  user's place in the song is never lost or reset, satisfying the step's own "stay aligned with source"
  text.
- **All-muted (zero active stems) is a valid, silent mix, not an error.** Disclosed: Step 040 already
  established "silent source yields finite output/no NaNs" as a first-class, tested case for separation;
  this step applies the identical principle to mixdown — an all-muted selection writes
  `SampleCount`-length silence rather than throwing, proven directly by
  `MuteWinsOverSolo_MutedAndSoloedChannelIsSilent_AndAllMutedYieldsFiniteSilence_NegativePath`.
- **Mute always wins over solo for the same channel.** A disclosed, explicit mixer-semantics choice
  (`StemMixRules`): a channel that is both muted and soloed is silent, not audible — the spec text itself
  does not mandate a specific precedence, so this follows the conventional DAW-mixer rule (mute is the
  more specific, per-channel override) rather than leaving the interaction undefined. Proven directly by
  the same negative-path test above.
- **No per-stem resampling.** Carried forward, disclosed, unchanged from Steps 038-041: `StemMixdownRenderer`
  requires every stem (and therefore every mix) to already share one common sample rate (an already-enforced
  Step 040 invariant); mixing stems recorded at genuinely different native sample rates remains out of
  scope, exercised here only as a rejected, explicit failure (`IncompatibleStemSampleRates_Throws...`),
  never a silent/lossy auto-resample.
- **Waveform caching reuses Step 026's `WaveformPyramidBuilder`/`IWaveformPyramidStore` verbatim, with no
  Stems-specific pyramid format.** Disclosed, deliberate: the only new piece needed was making a stem
  resolve to a stable `SongId` (`StemSongAssetFactory`) so the existing per-`SongId` cache path actually
  reopens instead of rebuilding on every call — proven directly by
  `BuildWaveformsAsync_PopulatesPerStemCachePathsWithOriginPreserved`.

## 4. Commands actually executed

All run via the PowerShell tool — the sandboxed POSIX Bash tool in this session again returned a bare
exit code 1 with no output for a plain command, the same pre-existing environment limitation recorded in
every prior step's own evidence; PowerShell was used for every command below without incident.

```
dotnet test XlightsAI.Studio.slnx --nologo                                                  # baseline, before any Step 042 code existed -> 520/520 passing

# New: StemChannelState.cs, StemMixRules.cs, StemSongAssetFactory.cs,
#      StemMixdownIncompatibleStemsException.cs, IStemMixdownRenderer.cs, StemMixdownRenderer.cs
# New: StemMixerViewModel.cs (+ StemChannelViewModel)
# Changed: XlightsAI.Studio.Shell.csproj (+ ProjectReference to XlightsAI.Studio.Stems)

dotnet build src\XlightsAI.Studio.Stems\XlightsAI.Studio.Stems.csproj --nologo               # 0 Warnings, 0 Errors
dotnet build src\XlightsAI.Studio.Shell\XlightsAI.Studio.Shell.csproj --nologo               # 0 Warnings, 0 Errors

dotnet build tests\XlightsAI.Studio.Stems.Tests\XlightsAI.Studio.Stems.Tests.csproj --nologo # 0 Warnings, 0 Errors
dotnet test  tests\XlightsAI.Studio.Stems.Tests\XlightsAI.Studio.Stems.Tests.csproj --nologo --logger "console;verbosity=detailed" --filter "FullyQualifiedName~Step042"
  # 8/8 passed

dotnet build tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj --nologo # one real compile error, fixed (see below); then 0 Warnings, 0 Errors
dotnet test  tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj --nologo --logger "console;verbosity=detailed" --filter "FullyQualifiedName~Step042"
  # 6/6 (StemMixerViewModelTests) + 2/2 (ShellWiringTests, real device) = 8/8 passed

dotnet build XlightsAI.Studio.slnx --nologo                                                  # full solution, after Step 042 implementation -> 0 Warnings, 0 Errors
dotnet test  XlightsAI.Studio.slnx --nologo                                                   # full regression, final
```

### Real compile-error fix (disclosed, not hidden)

`StemAsset`'s sixth positional constructor parameter is named `SourceOriginTicks` (not `originTicks`);
the first draft of `Step042StemMixerViewModelTests.cs` used the wrong named-argument spelling
(`originTicks:`), which the C# compiler correctly rejected with `CS1739` ("best overload... does not have
a parameter named 'originTicks'"). Fixed by correcting the named-argument spelling to match the real
record's actual parameter name — a real compiler-caught mismatch, fixed before any test was run, not a
hypothetical.

### Build results

Full solution: `Build succeeded. 0 Warning(s). 0 Error(s).`

### Targeted test results (Step 042 tests only, real runs)

```
# Stems.Tests — Step042AcceptanceTests (8/8)
Passed RenderMixAsync_AlreadyCancelled_ThrowsOperationCanceledException_NoPartialFile_NegativePath [40 ms]
Passed SoloOverridesOtherStems_OnlySoloedStemIsAudible [64 ms]
Passed IncompatibleStemSampleRates_ThrowsStemMixdownIncompatibleStemsException_NegativePath [13 ms]
Passed StemFileModifiedAfterManifest_ThrowsStemMixdownIncompatibleStemsException_NegativePath [16 ms]
Passed StemMixRules_MissingChannelDefaultsToUnmutedUnsoloed [14 ms]
Passed Step042_Acceptance_Passes_MixdownSumsActiveStemsAndStaysAligned [22 ms]
Passed MuteWinsOverSolo_MutedAndSoloedChannelIsSilent_AndAllMutedYieldsFiniteSilence_NegativePath [21 ms]
Passed RenderMixAsync_SecondCallSameSelection_ReusesCacheEvenAfterStemFilesAreDeleted [24 ms]
Total tests: 8. Passed: 8, Failed: 0.

# Shell.Tests — Step042StemMixerViewModelTests (6/6, fake-transport, hardware-free)
Passed BuildWaveformsAsync_PopulatesPerStemCachePathsWithOriginPreserved [204 ms]
Passed LoadManifest_CreatesOneUnmutedUnsoloedChannelPerStem [15 ms]
Passed ApplyMixAndReopenAsync_IncompatibleStems_SetsErrorMessageAndLeavesMixClosed_NegativePath [18 ms]
Passed ToggleMute_ThenApplyMix_RendersWithoutThatStem [34 ms]
Passed ApplyMixAndReopenAsync_NoManifestLoaded_SetsErrorMessage_NegativePath [1 ms]
Passed ApplyMixAndReopenAsync_WhilePlaying_PreservesPositionAndResumesAcrossRemix [33 ms]
Total tests: 6. Passed: 6, Failed: 0.

# Shell.Tests — Step042ShellWiringTests (2/2, real WaveOutAudioTransport / real Windows winmm device)
Passed Step042_Acceptance_Passes_MutingAStemProducesAudiblyDifferentSignalThanFullMix [997 ms]
Passed Step042_Acceptance_Passes_SoloVocalsAndDrumsStayAlignedWithSourceOnRealDevice [313 ms]
Total tests: 2. Passed: 2, Failed: 0.
```

### Full regression (after implementation, final run)

```
Passed!  - Failed: 0, Passed:  71, Skipped: 0, Total:  71  - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed:  91, Skipped: 0, Total:  91  - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed:  52, Skipped: 0, Total:  52  - XlightsAI.Studio.Stems.Tests.dll
Passed!  - Failed: 0, Passed:  25, Skipped: 0, Total:  25  - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed:  34, Skipped: 0, Total:  34  - XlightsAI.Studio.Audio.Tests.dll
Passed!  - Failed: 0, Passed:  82, Skipped: 0, Total:  82  - XlightsAI.Studio.Rhythm.Tests.dll
Passed!  - Failed: 0, Passed: 150, Skipped: 0, Total: 150 - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed:  31, Skipped: 0, Total:  31  - XlightsAI.Studio.Reference.Tests.dll
```

**536 total, 0 failed, 0 skipped** — up from the Step 041 baseline of 520
(+8 `Step042AcceptanceTests` in `Stems.Tests`: 14 Step038 + 16 Step039 + 8 Step040 + 6 Step041 + 8 Step042
= 52, matching the reported `52` above; +6 `Step042StemMixerViewModelTests` + 2 `Step042ShellWiringTests`
in `Shell.Tests`, bringing it from 142 to 150). Every one of the 520 pre-existing tests still passes
unmodified, confirming no prior step's contract was broken.

## 5. Persisted-output inspection / observable demonstration for this step

Unlike Step 041 (where a standalone scratchpad demo was attempted and the user explicitly chose to accept
the xUnit suite instead), this step's own `Step042ShellWiringTests.cs` **is** the real, non-mocked,
hardware-level observable demonstration — run directly via the xUnit test harness, but against a real
Windows audio device, not an in-process fake:

- `Step042_Acceptance_Passes_SoloVocalsAndDrumsStayAlignedWithSourceOnRealDevice` opens a real
  two-stem mix on a real `WaveOutAudioTransport`, plays it, waits for the real device-reported
  `PositionTicks` to actually advance past zero, then (while still playing) solos every channel through
  `StemMixerViewModel.ApplyMixAndReopenAsync` — a real re-render + a real `OpenAsync` on the *same*
  transport instance — and asserts the real device position never regresses and resumes advancing
  afterward. This is the literal, direct proof of the required acceptance text: "Audibly isolated stems
  stay aligned with source."
- `Step042_Acceptance_Passes_MutingAStemProducesAudiblyDifferentSignalThanFullMix` opens the full
  two-stem mix, captures its real decoded PCM content, then mutes vocals and reopens — proving the real
  signal fed to the real transport actually changes (not just a UI flag) while `OriginTicks`/`SampleRate`
  stay identical ("keep origin offset... visible").
- Every Stems-level `Step042AcceptanceTests` assertion reads back real `.wav` files written to a real
  temp directory on disk via the real, already-committed `StemWavWriter`/`WavAudioDecoder` round trip —
  not an in-memory substitute — including the cache-reuse test, which deletes the real source stem files
  on disk and confirms the second render still succeeds purely from the real on-disk mixdown cache file.

No xLights-specific manual verification applies to this step (this step does not touch the `.xsq`/
`xlights_rgbeffects.xml` pathway at all); per the user's own standing, previously recorded waiver
(`memory: feedback_skip_xlights_human_verification`), the xLights human-render check is not treated as a
blocker for this or any other step, and is not fabricated here.

## 6. Fixtures / input hashes

No new committed fixture file was added for this step. Every stem WAV used by every Step 042 test is
generated in-process, deterministically, by the already-real `StemWavWriter.WriteMonoFloatAsync` against
a pure-function deterministic sine-wave generator (same `DeterministicSineWave` pattern Step 040/041
already used) — there is nothing to pin a SHA-256 against ahead of time the way the committed
`fixtures/onnx/*.onnx` files are pinned in Steps 038-041, since this step mixes already-produced stems
rather than running a model. All test-run temp directories (`%TEMP%\xlai-step042-*\`) are deleted by each
test's own `IDisposable.Dispose()`/`IAsyncDisposable.DisposeAsync()`.

## 7. Unresolved issues / known gaps

- **No `ShellScreen.Stems` navigation wiring** (§3) — `StemMixerViewModel` is a complete, independently
  real-device-tested application-layer slice, but is not yet reachable from `ShellViewModel`'s own
  navigation surface, since the upstream "run a separation and produce a `StemManifest` from inside the
  shell" action does not exist in `ShellViewModel` either (Steps 038-041 only ever exercise
  `OnnxStemSeparator` from test code). A future step that wires a "Run Separation" shell action should
  wire this screen's navigation at the same time, not before.
- **Solo/mute is implemented via full mix-and-reopen, not live per-channel gain** (§3) — real, audible,
  content-correct, but there is a brief, real gap while the new mix opens on every toggle (bounded by
  `StemMixdownRenderer`'s own cache for a previously-seen selection, but not eliminated for a first-time
  selection). A true zero-gap crossfade would require a native multi-stream mixing transport this project
  does not have yet.
- **No per-stem resampling / no mixing of stems recorded at different native sample rates** — carried
  forward, disclosed, unchanged from Steps 038-041; exercised here only as an explicit, rejected failure
  path, never a silent auto-resample.
- **No production separation-quality claim exists anywhere in the repository** — carried forward,
  disclosed, unchanged from Step 038's own BLOCKED feasibility verdict (Step 044's own later scope). This
  step mixes/plays back whatever stems a separator already produced; it makes no claim about their
  separation quality.
- **Instrument activity detection (Step 043) is not yet implemented** — this step's own waveform/mixer UI
  does not yet overlay activity regions; that is Step 043's own explicit later scope.

## 8. PASS / BLOCKED

**Status: PASS**

- "Audibly isolated stems stay aligned with source" (required acceptance, verbatim) — proven by
  `Step042_Acceptance_Passes_SoloVocalsAndDrumsStayAlignedWithSourceOnRealDevice` (real device, real
  position never regresses across a live solo-driven remix) and
  `Step042_Acceptance_Passes_MutingAStemProducesAudiblyDifferentSignalThanFullMix` (real device, real
  content changes while origin/sample-rate stay identical), both run against a real
  `WaveOutAudioTransport`, never a mock.
- "Use single transport timebase to solo/mute stems; keep origin offset and resample alignment visible;
  cache separate waveform peaks" (implementation guidance, verbatim) — proven by: single shared transport
  (`StemMixerViewModel.ApplyMixAndReopenAsync` takes one `IAudioTransport` and only ever calls
  `OpenAsync`/`SeekAsync`/`PlayAsync` on it, never constructing a second transport); origin/alignment
  visible (`Step042_Acceptance_Passes_MixdownSumsActiveStemsAndStaysAligned` asserts the rendered mix's
  `OriginTicks`/`SampleRate`/`SampleCount` exactly match the source stems); cached waveform peaks
  (`BuildWaveformsAsync_PopulatesPerStemCachePathsWithOriginPreserved`, real `WaveformPyramidCache` files
  on disk, reused via Step 026's own cache-by-`SongId` mechanism).
- "Missing or incompatible model, cancellation and offline mode must yield a recoverable state"
  (failure/edge case, reinterpreted for this step's own mixdown boundary) — proven three ways:
  incompatible stems (`IncompatibleStemSampleRates_Throws...`, `StemFileModifiedAfterManifest_Throws...`,
  no mixdown file ever written); cancellation
  (`RenderMixAsync_AlreadyCancelled_ThrowsOperationCanceledException_NoPartialFile_NegativePath`, no
  partial cache file left behind); recoverable state at the ViewModel layer
  (`ApplyMixAndReopenAsync_NoManifestLoaded_SetsErrorMessage_NegativePath` and
  `ApplyMixAndReopenAsync_IncompatibleStems_SetsErrorMessageAndLeavesMixClosed_NegativePath`, both set
  `ErrorMessage` and leave `IsMixOpen == false` rather than throwing to the UI or leaving a half-open
  session).
- Additional rigor beyond the minimum: `SoloOverridesOtherStems_OnlySoloedStemIsAudible` and
  `MuteWinsOverSolo_MutedAndSoloedChannelIsSilent_AndAllMutedYieldsFiniteSilence_NegativePath` prove the
  full solo/mute interaction matrix, not just the "both active" happy path;
  `RenderMixAsync_SecondCallSameSelection_ReusesCacheEvenAfterStemFilesAreDeleted` proves the mixdown
  cache is real and load-bearing, not merely present; `ApplyMixAndReopenAsync_WhilePlaying_
  PreservesPositionAndResumesAcrossRemix` proves the position-preservation contract deterministically
  (fake-transport unit test) in addition to the real-device proof above.
- Full regression: **536/536 passing**, 0 failed, 0 skipped; every pre-existing test from Steps 001-041
  still passes unmodified.
- **Carried-forward, disclosed phase-level gaps (not this step's own blocker):** no `ShellScreen.Stems`
  navigation wiring yet (§3/§7); no production separation-quality claim exists yet anywhere in the
  repository (Step 038's own BLOCKED feasibility verdict, Step 044's own later scope); instrument activity
  detection does not exist yet (Step 043's own later scope). Step 042 does not silently resolve or hide
  any of these.
- Per the user's standing waiver (`memory: feedback_skip_xlights_human_verification`), no xLights
  human-render verification was required or attempted for this step, and none is claimed.
