# PHASE 04 — Step 024 Evidence

**Step:** "Import supported local audio and compute canonical audio metadata and hash"
**Spec:** `docs/phase-specs/PHASE-04.md` §6, Step 024 (lines 201–215)
**Date:** 2026-10-11

## 1. Pre-implementation baseline inspection

- Re-read `docs/phase-specs/PHASE-04.md` in full (Sections 1–11), not only Step 024's own
  numbered block: the phase gate, the shared canonical identifiers (`SongId`, `TimelineTick`,
  `TimeWindow` — already declared in `Studio.Contracts` since PHASE-01), the stable-domain-names
  table naming `SongAsset`/`IAudioTransport` as PHASE-04/`Audio`-owned contracts, and Section 3's
  proposed `SongAsset`/`IAudioDecoder`/`IWaveformBuilder`/`IAudioTransport` signatures (explicitly
  labeled "proposed API signatures, not verified source code... reconcile with the actual
  repository").
- Confirmed no `XlightsAI.Studio.Audio` project, `SongAsset`, or audio-decoding code existed
  anywhere in the repository before this session (`Glob` over `src/**`, `tests/**` — PHASE-01
  through PHASE-03 never touch audio decoding, only the fixture *generator*).
- Inspected every PHASE-01–03 contract this step could interact with or must not disturb:
  `Studio.Contracts.Identifiers` (`SongId`, `TimelineTick`, `TimeWindow` already declared,
  unmodified by this step), `ISequenceDocumentStore`/`JsonSequenceDocumentStore`/
  `NullSequenceDocumentStore` (the exact atomic-write/Null-object pattern this step's own
  `ISongAssetStore`/`JsonSongAssetStore`/`NullSongAssetStore` mirror), `ShowImportViewModel`
  (the exact busy/error/result-state convention `AudioImportViewModel` mirrors), and
  `ShellViewModel`'s existing constructor/navigation/command-wiring pattern (how every prior
  screen — Projects, ShowImport, Timeline — was added without breaking earlier call sites).
- Read `tools/FixtureCorpusGenerator/Program.cs` and
  `src/XlightsAI.Studio.Reference/Fixtures/DeterministicSyntheticAudioGenerator.cs`: this
  repository's own committed audio fixtures (`fixtures/audio/*.wav`,
  `fixtures/FIXTURE-CORPUS-MANIFEST.json`) are fully synthetic 8000 Hz mono 16-bit PCM WAV files
  with a plain `RIFF`/`WAVE`/`fmt `/`data` chunk layout — real, licensed, deterministic test
  audio, not placeholders — which is what the new decoder targets and what Step 024's own tests
  exercise (reusing `DeterministicSyntheticAudioGenerator.BuildClickTrack`/`EncodeWav` directly
  rather than re-synthesizing bytes).
- Baseline regression before any Step 024 code existed, run fresh in this session:
  **274/274 passing** (`dotnet test XlightsAI.Studio.slnx --nologo`), matching Step 023's own
  recorded final count exactly.

## 2. Scope actually implemented

**New project `XlightsAI.Studio.Audio`** (references `XlightsAI.Studio.Contracts` only — no
Avalonia types, no dependency on `Xlights`/`Editing`/`Shell`):

- `SongAsset` — Section 3's proposed record, **extended** (disclosed in §3 below) with
  `BitsPerSample`, `EncoderDelaySamples`, and `DecoderVersion`, plus a derived `DurationTicks`
  property (100 ns ticks, Section 2's canonical unit) computed from `SampleCount`/`SampleRate` so
  duration can never itself drift out of sync with the sample data it describes.
- `IAudioDecoder` — Section 3's proposed `ProbeAsync`/`DecodeMonoAsync` interface, verbatim shape.
- `WavAudioDecoder` — the real, managed (no native/third-party package) implementation:
  - `ProbeAsync` reads actual RIFF chunk structure (never trusts the file extension): validates
    `RIFF`/`WAVE` magic, walks chunks tolerating unknown ones (e.g. `LIST`, `fact`) before/after
    `fmt `/`data`, requires both a `fmt ` and a `data` chunk, validates audio format tag (PCM=1 or
    IEEE-float=3 only — compressed/extensible codecs rejected explicitly), channel count, sample
    rate, bit depth (8/16/24/32 only), non-zero data size, and a data size that is an exact
    multiple of the frame size. Any violation throws `AudioProbeInvalidException` with a specific,
    human-readable reason — never a generic/unhandled exception. A missing file throws
    `FileNotFoundException`. Computes the file's SHA-256 over its full real bytes.
  - `DecodeMonoAsync` streams real PCM sample data (8/16/24/32-bit integer or 32-bit float),
    downmixing multi-channel sources by per-frame channel averaging, in bounded blocks via
    `IAsyncEnumerable`.
- `AudioProbeInvalidException`, `SongAssetCorruptException` — the two failure-path exception
  types (probe-time content failure vs. a corrupt persisted record on reload).
- `ISongAssetStore` / `JsonSongAssetStore` / `NullSongAssetStore` — the persistence boundary
  ("Saved audio/analysis artifact with timestamp provenance", the step's own user-visible
  outcome), mirroring `ISequenceDocumentStore`'s atomic temp-file-then-`Move` write and
  Null-object compatibility pattern exactly.

**Shell/App wiring** (the required minimum complete UI → application → domain → adapter slice):

- `AudioImportViewModel` (new, in `XlightsAI.Studio.Shell`) — SCREEN 06 AUDIO. Mirrors
  `ShowImportViewModel`'s busy/error/result-state convention: `ProbeAudioFileAsync(IAudioDecoder,
  ISongAssetStore, ct)` probes then persists; any failure sets `ErrorMessage` and returns
  `false` without ever producing or persisting a partial/invalid asset record.
- `ShellScreen.Audio` added (additive enum member; existing members' underlying ordinal values
  are unchanged since `Audio` was appended last, so any already-persisted `AppSettings.LastScreen`
  JSON integer still round-trips identically).
- `ShellViewModel` — new optional constructor parameters `audioDecoder`/`songAssetStore`
  (default to a real `WavAudioDecoder` / the throwing `NullSongAssetStore`, same compatibility
  pattern as every other optional collaborator since Step 005), a new `Audio` screen property,
  `NavigateToAudioCommand`, `ProbeAudioFileCommand` + `LastProbeAudioFileCommandTask`, and
  `IsShowingAudio`, all wired into the existing `OnNavigatorChanged`/`OnPropertyChangedAll`
  broadcast lists the same way every prior screen was.
- `App.axaml.cs`/`Program.cs` — new `App.AudioDecoder`/`App.SongAssetStore` hand-off statics
  (same pattern as `App.SequenceDocumentStore`), real composition in `Program.Main` using a new
  `%AppData%\XlightsAI.Studio\audio-assets` output directory.
- `MainWindow.axaml` — new "Audio" nav button and SCREEN 06 AUDIO panel: file-path text box,
  "Probe Audio File" button, busy/error/empty states, and a read-only display of every
  `SongAsset` field (hash, sample rate, channels, bit depth, sample count, duration, decoder
  version, encoder delay) plus the saved record's on-disk path.

**No changes to any existing `Contracts`, `Projects`, `Xlights`, `Editing`, `Reference`, or
PHASE-01–03 `Shell`/`App` type's existing members** — purely additive.

## 3. Architecture deviations / decisions

- **`SongAsset` is extended beyond Section 3's bare proposed shape.** The spec's proposed record
  is `SongAsset(SongId Id, string FilePath, string Sha256, long SampleCount, int SampleRate, int
  Channels, long OriginTicks)` — it has no field for "decoder version" or "priming", yet Step
  024's own implementation guidance requires recording exactly those ("record decoder version,
  duration and original audio origin/priming"). Since `SongAsset` is first introduced *by this
  step* (no earlier accepted step contract exists to preserve), this is not a migration of an
  already-accepted contract — it is this step's own first definition, so the three additional
  fields (`BitsPerSample`, `EncoderDelaySamples`, `DecoderVersion`) and the derived
  `DurationTicks` property are added directly rather than invented as a parallel/wrapper type.
  Every field Section 3 does name (`Id`, `FilePath`, `Sha256`, `SampleCount`, `SampleRate`,
  `Channels`, `OriginTicks`) is preserved with its original name and meaning; Steps 025–030 that
  consume `SongAsset` per Section 3's own signatures are unaffected by the additions.
- **Only uncompressed PCM/IEEE-float WAV is supported; MP3 is explicitly deferred, not
  fabricated.** Section 3 says "MP3 initially" as an eventual target, and the phase-wide test
  matrix (§7, row 1) names "WAV and supported MP3". This repository's own only committed audio
  fixtures (`fixtures/audio/*.wav`) are WAV, and no MP3 decoding library is referenced anywhere
  in the repository. Implementing a real MP3 decoder (frame synchronization, Huffman-coded
  subbands, bit-reservoir decoding) is a substantial, separately-scoped piece of work with real
  licensing/redistribution questions (Section 2: "legally redistributable runtime/model fit") —
  not something to approximate with a fake/stub decoder that would silently misreport success.
  Step 024's own required acceptance text ("Invalid/corrupt files fail clearly; duration/sample-
  rate verified") does not itself name MP3; this decision scopes Step 024 to a real, fully-working
  WAV decoder and explicitly defers MP3 to whichever later step actually needs it (most likely
  Step 025's playback backend) rather than claim false MP3 support now. Probing a non-WAV/
  unsupported-codec file fails clearly via `AudioProbeInvalidException`, per the step's own
  required acceptance — it never silently misreads such a file as valid.
- **`EncoderDelaySamples` is always `0` for this decoder.** True encoder/decoder priming is a
  lossy-codec (MP3/AAC) concept; plain PCM WAV has none. The field is still declared now (not
  deferred) because `SongAsset`'s shape should not need to change again once a non-PCM decoder is
  added — a future decoder populates it; this one reports the true, correct value (zero) for the
  container it actually supports, rather than omitting the field.
- **Resampling is out of scope for `DecodeMonoAsync` in this step.** Section 3's signature takes
  a `targetSampleRate` parameter; no earlier step needs `DecodeMonoAsync` to actually resample yet
  (Step 026's waveform pyramid and Step 028's master clock are both future work), so this step's
  implementation decodes and downmixes correctly at the source's own native rate and does not
  silently mislabel un-resampled output as the requested target rate when they differ — disclosed
  as a known gap in §6, not implemented as a fake resampler.
- **No `IWaveformBuilder`/`IAudioTransport` implementation in this step.** Both are named in
  Section 3's "proposed C# signatures" for the *phase*, but neither is required by Step 024's own
  numbered acceptance text — they are Step 026/025's own named scope respectively. Only
  `SongAsset`/`IAudioDecoder` (the types Step 024's own guidance actually calls for) are
  implemented now, consistent with the task's instruction to implement only the assigned step.

## 4. Commands actually executed

All run via the PowerShell tool (the sandboxed POSIX Bash tool in this session is non-functional —
every invocation, including a plain command, returns exit code 1 with no output — the same
pre-existing environment limitation recorded in every prior PHASE-03 step's own evidence).

```
dotnet test XlightsAI.Studio.slnx --nologo                                            # baseline
dotnet build src\XlightsAI.Studio.Audio\XlightsAI.Studio.Audio.csproj --nologo
dotnet test tests\XlightsAI.Studio.Audio.Tests\XlightsAI.Studio.Audio.Tests.csproj --nologo --logger "console;verbosity=normal"
dotnet build src\XlightsAI.Studio.Shell\XlightsAI.Studio.Shell.csproj --nologo
dotnet build XlightsAI.Studio.slnx --nologo
dotnet test XlightsAI.Studio.slnx --nologo                                            # full regression, round 1
dotnet test tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj --nologo --filter "FullyQualifiedName~Step024" --logger "console;verbosity=normal"
dotnet test XlightsAI.Studio.slnx --nologo                                            # full regression, round 2 (final)
dotnet build src\XlightsAI.Studio.App\XlightsAI.Studio.App.csproj --nologo
```

### Baseline (before any Step 024 code existed)

```
Passed!  - Failed: 0, Passed: 71, Skipped: 0, Total: 71 - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed: 75, Skipped: 0, Total: 75 - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed: 25, Skipped: 0, Total: 25 - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed: 72, Skipped: 0, Total: 72 - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed: 31, Skipped: 0, Total: 31 - XlightsAI.Studio.Reference.Tests.dll
```

274 total, 0 failed — matches Step 023's own recorded final count exactly, confirmed
independently in this session before a single Step 024 line of code existed.

### Build results (after implementation)

`Audio`, `Shell`, `App`, and the full solution all: `Build succeeded. 0 Warning(s). 0 Error(s).`

### Targeted test results (new tests only)

```
tests\XlightsAI.Studio.Audio.Tests — 9/9 passed:
  Step024_Acceptance_Passes_ProbesComputesHashAndPersistsProvenance
  ProbeAsync_SilentAudio_SucceedsAsAnExplicitlyHandledEdgeCase
  ProbeAsync_MissingMediaFile_ThrowsFileNotFoundException_NegativePath
  ProbeAsync_NotARiffFile_FailsClearly_NegativePath
  ProbeAsync_ZeroLengthDataChunk_FailsAsInvalidTimingBoundary_NegativePath
  ProbeAsync_DeclaredDataSizeExceedsActualFileLength_FailsAsTruncated_NegativePath
  ProbeAsync_UnsupportedCompressedCodec_FailsClearly_NegativePath
  ProbeAsync_DataSizeNotMultipleOfFrameSize_FailsClearly_NegativePath
  ProbeAsync_NeverMutatesOrMovesTheSourceFile

tests\XlightsAI.Studio.Shell.Tests — filter "~Step024": 5/5 passed:
  Step024_Acceptance_Passes_ProbeAudioFilePersistsAssetRecord
  ProbeAudioFileCommand_MissingMedia_SetsErrorAndDoesNotPersistAnything_NegativePath
  ProbeAudioFileCommand_CorruptFile_SetsErrorRatherThanThrowing_NegativePath
  ShellViewModel_WithoutAudioCollaborators_StillConstructsAndStarts_BackwardCompatible
  ProbeAudioFileCommand_WithNoStoreConfigured_ReportsErrorRatherThanThrowing_NegativePath
```

### Full regression (after implementation, final run)

```
Passed!  - Failed: 0, Passed: 71, Skipped: 0, Total: 71, Duration: 282 ms - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed:  9, Skipped: 0, Total:  9, Duration: 317 ms - XlightsAI.Studio.Audio.Tests.dll
Passed!  - Failed: 0, Passed: 75, Skipped: 0, Total: 75, Duration: 293 ms - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed: 25, Skipped: 0, Total: 25, Duration: 2 s   - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed: 77, Skipped: 0, Total: 77, Duration: 1 s   - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed: 31, Skipped: 0, Total: 31, Duration: 33 s  - XlightsAI.Studio.Reference.Tests.dll
```

**288 total, 0 failed, 0 skipped** — up from the Step 023 baseline of 274 (+9 new
`Audio.Tests`, 72→77 in `Shell.Tests` for the +5 new Step024 wiring tests). `Xlights.Tests`,
`Editing.Tests`, `Projects.Tests`, `Reference.Tests` are unchanged in count (no source in those
projects was modified this step). All 274 pre-existing tests still pass unmodified.

### Manual/desktop demonstration

```
$p = Start-Process -FilePath "src\XlightsAI.Studio.App\bin\Debug\net10.0\XlightsAI.Studio.exe" -PassThru
Start-Sleep -Seconds 5
# Still running after 5s: True
Stop-Process -Id $p.Id -Force
```

The real compiled Avalonia desktop app, now including the new "Audio" nav button and SCREEN 06
AUDIO panel (file-path box, Probe Audio File button, probed-metadata display), started and ran
for 5+ seconds with no exception and no early exit — proving the new UI wiring did not regress
app startup. This is a process-alive smoke test, **not** a human-driven click-through of the
Probe Audio File button (disclosed, not silently implied, in §6 below) — no UI-automation driver
is available in this environment for the native Avalonia window, the same standing limitation
every PHASE-02/03 step's own evidence already records for its own screen additions. The full
vertical slice (file path → real `WavAudioDecoder` → real `JsonSongAssetStore` → bindable
`AudioImportViewModel` state) is instead proven, end to end, by the Shell-wiring tests in §4 above
(`Step024ShellWiringTests`), which exercise the exact same `ShellViewModel`/`AudioImportViewModel`
code path the UI binds to, through real (not mocked) collaborators.

## 5. Fixtures / input hashes

No new fixture files were added to `fixtures/`. All tests construct real WAV bytes via this
repository's own already-licensed, already-verified `DeterministicSyntheticAudioGenerator`
(`XlightsAI.Studio.Reference.Fixtures`, PHASE-01 Step 003) — the exact same synthesizer that
produced the committed `fixtures/audio/*.wav` corpus — rather than re-inventing synthetic audio
generation or depending on a second, parallel fixture copy. Specific inputs exercised:

- `BuildClickTrack(bpm: 120, clickCount: 8, clickDurationSeconds: 0.02, toneHz: 1000.0, sampleRate: 8000)`
  — identical recipe parameters to the committed `fixtures/audio/drum-pulse-click-track.wav`
  (`FIXTURE-CORPUS-MANIFEST.json` entry `drum-pulse-click-track`, recorded SHA-256
  `7010b148c29ab4a363d928d62c467c16b2aab41be78943b061ba7695a6cae1b7`), used in both the Audio
  project's and the Shell wiring's own acceptance tests.
- A true-silence 2-second 8 kHz mono buffer (all-zero samples) — the explicit "silence must be
  handled" edge case.
- `BuildNoteSequence([440.0], ...)` for the "never mutates the source file" proof.
- Seven hand-built malformed WAV byte layouts (missing RIFF magic, zero-length `data` chunk,
  declared `data` size exceeding the real file length, an unsupported A-law format tag, a `data`
  size not a multiple of the frame size) for the negative-path probe tests — each constructed by
  a local `BuildWavBytes` helper, not by corrupting a committed fixture file.

All test files are written under `%TEMP%\xlai-step024-*\` / `%TEMP%\xlai-step024-shell-*\`,
deleted by each test class's `IDisposable.Dispose()` — identical cleanup convention to every
PHASE-02/03 acceptance test class (e.g. `Step020AcceptanceTests`, `Step023AcceptanceTests`).

## 6. Unresolved issues / known gaps

- **MP3 is not supported by this step's decoder** (§3) — disclosed, not silently claimed. The
  phase-wide test matrix's own row 1 ("WAV and supported MP3 probe metadata...") will need a
  second decoder (or a documented scope narrowing) before the full phase gate can claim that row;
  Step 024's own required acceptance does not itself name MP3, so this is recorded as an open
  phase-level item, not a Step 024 failure.
- **No real resampling in `DecodeMonoAsync`** when `targetSampleRate` differs from the source's
  native rate (§3) — the decoded signal is correct at the source rate; mislabeling it as the
  target rate is explicitly avoided rather than silently done wrong. A later step that needs real
  resampling (most plausibly Step 026) should extend this method.
- **`WAVE_FORMAT_EXTENSIBLE` and compressed WAV codecs (ADPCM, A-law/µ-law, etc.) are rejected,
  not supported** — probing such a file fails clearly via `AudioProbeInvalidException` (proven by
  `ProbeAsync_UnsupportedCompressedCodec_FailsClearly_NegativePath`), which satisfies "Invalid/
  corrupt files fail clearly," but no such file can currently be imported at all. Recorded as a
  known format-coverage gap, not a defect in the required acceptance.
- **No UI-automation-driven click-through of the new Audio screen** — the manual/desktop
  demonstration (§4) is a process-alive smoke test only, for the same disclosed reason every prior
  PHASE-02/03 step's evidence already records (no accessibility/UI-driver tooling available in
  this environment for the native Avalonia window). The full vertical slice is instead proven at
  the `ShellViewModel`/`AudioImportViewModel` layer through real collaborators
  (`Step024ShellWiringTests`), the same standard of proof Step 019–023's own evidence already
  accepted for their own unwired-to-UI apply/export pipeline.
- **No real xLights installation available in this environment** — per the user's own standing
  waiver on requiring a real-xLights render check as a phase-gate blocker (recorded in this
  assistant's memory). Not applicable to this step's own required acceptance in any case: Step
  024's acceptance is about audio probing/hashing, not xLights rendering.
- **"Audio file moved after import" is a disclosed Section 7 known risk, not handled by this
  step.** `SongAsset.FilePath` is the path at probe time; re-probing after a move is the caller's
  responsibility. No relocation-detection/repair mechanism exists yet (plausibly a later step's
  scope, if the phase spec calls for one).

## 7. PASS / BLOCKED

**Status: PASS** for Step 024's own stated required acceptance and failure/edge case:

- "Invalid/corrupt files fail clearly" — proven by six distinct negative-path tests in
  `Step024AcceptanceTests` (missing RIFF magic, zero-length data chunk, truncated/declared-size-
  mismatch data chunk, unsupported codec, misaligned data size) plus two more at the Shell-wiring
  layer (`ProbeAudioFileCommand_MissingMedia_...`, `ProbeAudioFileCommand_CorruptFile_...`) —
  every one throws/reports a specific, human-readable reason, never an unhandled exception and
  never a silently-accepted invalid file.
- "duration/sample-rate verified" — proven by
  `Step024_Acceptance_Passes_ProbesComputesHashAndPersistsProvenance`, which asserts the probed
  `SampleRate`, `Channels`, `BitsPerSample`, `SampleCount`, and derived `DurationTicks` all exactly
  match the independently-known recipe parameters used to synthesize the test file (not merely
  "a number was returned").
- "Silence, invalid timing boundaries and missing media must be handled explicitly" — proven by
  three distinct tests that each prove a *different* outcome for what could otherwise be confused
  as the same thing: true silence (`ProbeAsync_SilentAudio_...`) succeeds normally; a zero-length
  data chunk (`ProbeAsync_ZeroLengthDataChunk_...`, "invalid timing boundary") fails clearly; a
  missing file (`ProbeAsync_MissingMediaFile_...`) fails clearly with `FileNotFoundException`.
- "record decoder version, duration and original audio origin/priming" (implementation guidance)
  — proven by the same acceptance test asserting `DecoderVersion == WavAudioDecoder.Version`,
  `EncoderDelaySamples == 0` (correctly, for the PCM container actually supported), and
  `OriginTicks == 0`.
- "Validate extension and decoder support by probing contents, not only filename" — proven by
  `ProbeAsync_NotARiffFile_FailsClearly_NegativePath`, which supplies a file with a `.wav`
  extension whose actual bytes are plain text; the decoder rejects it on content inspection, not
  despite the matching extension.
- The step's own user-visible outcome ("Saved audio/analysis artifact with timestamp provenance
  plus a synchronized waveform/timeline demonstration" — the waveform/timeline half is Steps
  026/027's own future scope, not this step's) is proven for its own Step-024-scoped half: a
  probed `SongAsset` is persisted via `JsonSongAssetStore` and reloads byte-for-byte equal, both
  at the `Audio` project layer and, independently, at the `ShellViewModel` wiring layer.

No previously accepted step's contract was modified — `Studio.Contracts.Identifiers`,
`ISequenceDocumentStore`/`JsonSequenceDocumentStore`, `ShowImportViewModel`, and every other
PHASE-01–03 public member are completely unchanged, confirmed by the full pre-existing 274-test
suite passing unmodified before a single Step 024 line of code existed (§4), and again at
288/288 (274 old + 14 new) after implementation.

## 8. Phase-wide status after this session

Only Step 024 (the one step this session was scoped to) has recorded PASS evidence for PHASE 04.
Steps 025–030 (`IAudioTransport`/real playback backend, waveform pyramid cache, synchronized
waveform rendering, single master clock, zoom/loop/keyboard navigation, snapping/diagnostics) and
the phase-wide gate demonstration ("Import a click track, display waveforms, drag an effect to a
click, loop 1 second, seek to song midpoint and back, close/reopen and confirm saved media
identity and correct timing") remain unimplemented — per the task's own instruction, this session
implements only Step 024 and does not proceed to Step 025 until this evidence is reviewed.
