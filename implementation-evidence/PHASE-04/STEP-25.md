# PHASE 04 — Step 025 Evidence

**Step:** "Add audio playback abstraction and actual desktop audio backend"
**Spec:** `docs/phase-specs/PHASE-04.md` §6, Step 025 (lines 217–231)
**Date:** 2026-10-11

## 0. Addendum (same date): Mac/Linux placeholder backend

After this step was recorded PASS below, the user asked for a standing convention: any OS-specific
concrete implementation that has no cross-platform equivalent yet must ship a stub for the platforms
it doesn't cover, which throws a clear, typed, user-facing message rather than leaving those
platforms to hit a raw native failure or a silent no-op. This was added as a new
`implementation-evidence`-adjacent cross-cutting rule in `docs/ARCHITECTURE-AND-GUARDRAILS.md`
("Platform coverage" section) and restated locally in `docs/phase-specs/PHASE-04.md` §2.

**New code (no change to any already-PASS'd behavior):**
- `UnsupportedAudioPlatformException : NotSupportedException` — the typed, specific exception.
- `UnsupportedPlatformAudioTransport : IAudioTransport` — the stub. Read-only members
  (`CurrentSong`/`IsPlaying`/`Loop`/`PositionTicks`) return safe neutral defaults so merely binding a
  view to it can never crash; `OpenAsync`/`PlayAsync`/`PauseAsync`/`SeekAsync`/`SetLoop` all throw
  `UnsupportedAudioPlatformException` naming the gap; `DisposeAsync` is a no-op (disposing something
  that was never opened must always succeed).
- `ShellViewModel.CreateDefaultAudioTransport`, and equivalent logic in `App.axaml.cs`/`Program.cs`,
  select `WaveOutAudioTransport` vs. `UnsupportedPlatformAudioTransport` via an explicit
  `OperatingSystem.IsWindows()` `if` guard (not a ternary, so the CA1416 analyzer recognizes the
  guard) — this also let the previously-added `#pragma warning disable CA1416` suppressions in
  `ShellViewModel.cs`/`App.axaml.cs`/`Program.cs` be removed entirely; the build is 0 warnings/0
  errors without any suppression now.

**New tests:**
- `UnsupportedPlatformAudioTransportTests` (5 tests, Audio project, platform-independent — doesn't
  need `[SupportedOSPlatform("windows")]` since the stub itself has no OS constraint): every action
  method throws `UnsupportedAudioPlatformException` with a non-empty message and platform
  description; read-only properties return safe defaults; `DisposeAsync` never throws.
- `Step025ShellWiringTests.ShellViewModel_WithoutAudioTransportCollaborator_DefaultsToRealWorkingTransportOnWindows` —
  proves behaviorally (not via reflection) that on this Windows machine, omitting the
  `audioTransport` constructor argument still resolves to the real, working `WaveOutAudioTransport`
  (`OpenAudioPlaybackCommand` actually succeeds against the real device), not the stub.

**Commands run:**
```
dotnet build XlightsAI.Studio.slnx --nologo       # 0 Warning(s), 0 Error(s)
dotnet test XlightsAI.Studio.slnx --nologo        # 309/309 passed (303 + 5 new Audio.Tests + 1 new Shell.Tests)
```

**Known gap, disclosed, not fixed by this addendum:** this proves the *selection logic* and the
*stub's own behavior* on Windows; it cannot prove the stub is actually selected/exercised on a real
Mac or Linux machine, since none is available in this environment. A future session with access to
macOS/Linux should verify `OperatingSystem.IsWindows()` resolves to `false` there and that the app
still starts and surfaces `UnsupportedAudioPlatformException`'s message cleanly through the UI rather
than crashing, before any real macOS/Linux `IAudioTransport` implementation is attempted.

**Status: PASS** for this addendum's own scope (the "Platform coverage" convention + Mac/Linux audio
stub). Step 025's own original required acceptance (§7 below) is unaffected and remains PASS.

---

## 1. Pre-implementation baseline inspection

- Re-read `docs/phase-specs/PHASE-04.md` in full again (not only Step 025's own numbered block),
  focusing on Section 3's proposed `IAudioTransport` signature, the architectural design notes
  ("The playback component owns one authoritative monotonic transport clock; views subscribe to
  position snapshots, not their own timers... build synchronization around actual playback
  position... provide stop/pause/seek recovery on device unplug"), and the Section 2 timing rule
  ("internally represent position with an explicit canonical monotonic clock tied to source audio
  origin... All time scopes are half-open `[start,end)`").
- Re-read `implementation-evidence/PHASE-04/STEP-24.md` in full: Step 024's own disclosed
  extensions/decisions (treated as the immutable prior-step contract for this step), specifically
  that `SongAsset`/`IAudioDecoder`/`WavAudioDecoder` are real, already-accepted, unmodified by this
  step, and that `IAudioTransport` was explicitly *not* implemented in Step 024 (named as this
  step's own scope).
- Inspected every file this step could interact with: `SongAsset.cs`, `IAudioDecoder.cs`,
  `WavAudioDecoder.cs` (read fully, unmodified by this step), `AudioImportViewModel.cs`,
  `ShellViewModel.cs` (the exact optional-collaborator/compatibility-default pattern every prior
  step already established — `IAudioDecoder audioDecoder = null` defaulting to a real
  `WavAudioDecoder`, `ISongAssetStore songAssetStore = null` defaulting to a throwing
  `NullSongAssetStore` depending on whether the collaborator needs external configuration),
  `ShellScreen.cs`, `MainWindow.axaml`'s existing SCREEN 06 AUDIO panel, `DelegateCommand.cs`,
  `ObservableObject.cs`.
- Confirmed via `Get-CimInstance Win32_SoundDevice` that this machine has real, working audio output
  devices (`AMD Streaming Audio Device`, `USB Audio Device`, `Realtek Audio`,
  `AMD High Definition Audio Device`, all `Status: OK`) — the real desktop audio backend this step
  implements is exercised against actual hardware, not a mock, in every test below.
- Confirmed the App project (`XlightsAI.Studio.App.csproj`) is `OutputType=WinExe` with an
  `app.manifest` and no other `RuntimeIdentifiers`/target platform declared — i.e. the product's
  actual current scope is already Windows-only; this step's own Windows-only backend (§3 below)
  does not narrow anything already shipped.
- Baseline regression before any Step 025 code existed, run fresh in this session:
  **288/288 passing** (`dotnet test XlightsAI.Studio.slnx --nologo`), matching Step 024's own
  recorded final count exactly.

## 2. Scope actually implemented

**New in `XlightsAI.Studio.Audio`** (still references only `XlightsAI.Studio.Contracts` — no
Avalonia types):

- `IAudioTransport` — Section 3's proposed `OpenAsync`/`PlayAsync`/`PauseAsync`/`SeekAsync`/
  `SetLoop`/`PositionTicks` interface, verbatim shape, **extended** (disclosed in §3 below) with
  `IsPlaying`, `CurrentSong`, `Loop`, and `IAsyncDisposable`.
- `AudioOutputDeviceUnavailableException` — the "missing output device handled" failure-path
  exception type, carrying the raw winmm `MMRESULT` code plus a human-readable reason.
- `WinmmNativeMethods` — internal-only raw Win32 `winmm.dll` (`waveOut*`) P/Invoke surface
  (`waveOutOpen`/`Prepare/UnprepareHeader`/`Write`/`Pause`/`Restart`/`Reset`/`Close`/`GetPosition`),
  with `WAVEFORMATEX`/`WAVEHDR`/`MMTIME` structs laid out to match the native ABI exactly. No
  third-party/NuGet audio package is referenced — `winmm.dll` ships with every Windows install
  (Section 2: "no mandatory... runtime"). `WAVEHDR` instances are always allocated via
  `Marshal.AllocHGlobal` (never a managed/pinned array), because the OS driver retains a pointer to
  that memory for as long as the buffer is queued — outliving any single P/Invoke call, which a
  managed-array pin (valid only for the call's duration) would not survive safely.
- `WaveOutAudioTransport : IAudioTransport` — the real desktop audio backend:
  - `OpenAsync` fully decodes the given `SongAsset` via the injected `IAudioDecoder`
    (`DecodeMonoAsync`) into an in-memory 16-bit PCM buffer, opens a real wave-out device handle
    (`waveOutOpen`), and copies the PCM data into native (non-GC) memory.
  - `PlayAsync`/`PauseAsync` use `waveOutWrite`/`waveOutPause`/`waveOutRestart` to start, pause, and
    seamlessly resume the *same* queued buffer in place (no buffer re-submission on a plain
    pause/resume, so no discontinuity).
  - `SeekAsync` validates the target against `[0, SongAsset.DurationTicks]` (an explicit invalid-
    timing-boundary rejection — `ArgumentOutOfRangeException`), resets the device
    (`waveOutReset`), and resubmits a new header pointing at the PCM buffer offset for the target
    sample, preserving whether playback was running across the seek.
  - `PositionTicks` always reads the device's own actually-played sample count
    (`waveOutGetPosition`, `TIME_SAMPLES`) while playing — never a UI timer or elapsed-wall-clock
    estimate — per Section 3's "build synchronization around actual playback position" guidance.
  - `SetLoop`/a background polling task (20 ms) implement the loop-wrap behavior: once the real
    device position reaches the loop's `EndTicks`, the device is reset and a new buffer is
    submitted starting at `StartTicks`, without stopping playback.
  - A missing/invalid output device (`waveOutOpen`/`waveOutPrepareHeader`/`waveOutWrite`/
    `waveOutRestart` returning a non-zero `MMRESULT`) always throws
    `AudioOutputDeviceUnavailableException` with the specific `MMRESULT` and a human-readable
    reason — never a silent failure or an unhandled native crash.
  - `DisposeAsync` cancels the polling task, resets/unprepares/closes the device, and frees all
    native memory — "stop/pause/seek recovery" scaffolding: a torn-down transport leaves no
    dangling native handles for the next `OpenAsync` to collide with.

**Shell/App wiring** (the required minimum complete UI → application → domain → adapter slice):

- `AudioImportViewModel` — extended (same file, same screen) with `IsPlaybackOpen`, `IsPlaying`,
  `PlaybackPositionTicks`, `PlaybackErrorMessage`, `SeekTargetTicksText`, and
  `OpenPlaybackAsync`/`PlayAsync`/`PauseAsync`/`SeekAsync`/`RefreshPlaybackPosition` methods — same
  "catch, set an error property, return `false`; never let an exception reach the UI" convention as
  `ProbeAudioFileAsync`. A new probe resets the view model's own playback-open bookkeeping (the real
  transport is reopened by the next `OpenPlaybackAsync` call regardless).
- `ShellViewModel` — new optional constructor parameter `audioTransport` (defaults to a real
  `WaveOutAudioTransport(_audioDecoder)`, same compatibility pattern as every other optional
  collaborator), five new commands (`OpenAudioPlaybackCommand`, `PlayAudioCommand`,
  `PauseAudioCommand`, `SeekAudioCommand`, `RefreshAudioPositionCommand`) with `Last*Task`
  properties for deterministic test awaiting, wired into the existing
  `OnNavigatorChanged`/property-changed `RaiseCanExecuteChanged` broadcast lists the same way every
  prior command was.
- `App.axaml.cs`/`Program.cs` — new `App.AudioTransport` hand-off static (same pattern as
  `App.AudioDecoder`), real composition in `Program.Main`.
- `MainWindow.axaml` — new "Playback" sub-section on SCREEN 06 AUDIO: Open Playback / Play / Pause /
  Refresh Position buttons, a Seek-to-ticks text box + button, and read-only display of
  `IsPlaybackOpen`/`IsPlaying`/`PlaybackPositionTicks`/`PlaybackErrorMessage`.

**No changes to any existing `Contracts`, `Projects`, `Xlights`, `Editing`, `Reference`, or
PHASE-01–03 `Shell`/`App` type's existing members, nor to Step 024's own `SongAsset`/
`IAudioDecoder`/`WavAudioDecoder`/`ISongAssetStore`/`JsonSongAssetStore`/`NullSongAssetStore`** —
purely additive.

## 3. Architecture deviations / decisions

- **`IAudioTransport` is extended beyond Section 3's bare proposed shape.** The spec's proposed
  interface has only `PositionTicks`/`OpenAsync`/`PlayAsync`/`PauseAsync`/`SeekAsync`/`SetLoop`.
  Since `IAudioTransport` is first introduced *by this step* (Step 024's own evidence explicitly
  named it as deferred, not yet accepted), the same "first definition, not a migration" reasoning
  Step 024 already applied to `SongAsset` applies here: `IsPlaying`, `CurrentSong`, `Loop`, and
  `IAsyncDisposable` are added directly. Every member Section 3 does name is preserved with its
  original name, signature, and meaning.
- **The real backend is Windows-only (`winmm.dll`), not cross-platform.** Section 2 only requires
  "no Python, Docker, Git or mandatory LLM subscription"; it does not mandate a specific
  cross-platform audio stack, and the App project (`OutputType=WinExe`, `app.manifest`, no other
  `RuntimeIdentifiers`) is already Windows-only in its actual current shipped scope. Rather than
  fabricate false cross-platform support (e.g. a stub Linux/macOS backend that silently does
  nothing), `WaveOutAudioTransport` is marked `[SupportedOSPlatform("windows")]` and is the only
  `IAudioTransport` implementation this step ships. A later step that needs real cross-platform
  playback should add a second implementation behind the same interface rather than widen this one
  dishonestly.
- **No third-party/NuGet audio package is referenced.** `winmm.dll`'s `waveOut*` API ships with
  every Windows install; using raw P/Invoke avoids adding any new license/redistribution surface
  (Section 2: "legally redistributable runtime/model fit" — trivially satisfied by depending on
  nothing beyond the OS itself) and avoids `AllowUnsafeBlocks` (classic `DllImport`/
  `Marshal.AllocHGlobal` needs no `unsafe` code; the source-generated `LibraryImport` attribute was
  tried first and rejected specifically because it requires `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`
  project-wide for no behavioral benefit here — see the commands log in §4).
- **The full track is decoded and buffered in memory on `OpenAsync`, not streamed incrementally
  during playback.** Section 3's `IAudioDecoder.DecodeMonoAsync` signature is itself a streaming
  `IAsyncEnumerable`; this step's transport fully drains it once per `OpenAsync` into one pinned
  native PCM buffer. This is a deliberate simplification scoped to this step: it makes
  `PositionTicks`/seek/loop math exact and trivially correct (a single buffer offset, not
  inter-chunk bookkeeping across multiple in-flight `WAVEHDR`s), and every fixture/real-world song
  this phase targets (seconds to a few minutes) fits comfortably in memory as 16-bit mono PCM (a
  3-minute 44.1 kHz mono track is ~16 MB). If a later step's real-world usage proves this
  insufficient for very long shows, chunked double-buffered streaming can replace this internally
  without changing `IAudioTransport`'s public contract.
- **Playback is mono 16-bit PCM, matching Step 024's decoder output.** `WavAudioDecoder.DecodeMonoAsync`
  already downmixes to mono; this step's transport plays that mono signal through a 1-channel wave-out
  device rather than re-expanding to stereo. Multi-channel output is not required by this step's own
  acceptance text ("Play/pause/seek works... missing output device handled") and is left to a later
  step if genuinely needed.
- **"Stop/pause/seek recovery on device unplug" (implementation guidance) is implemented as
  explicit, surfaced failure, not silent self-healing.** A real mid-playback USB/device unplug is
  not something this environment can automate deterministically (no scriptable hardware removal);
  the guidance is satisfied by every native call site (`waveOutWrite`/`waveOutRestart`/
  `waveOutPrepareHeader`) checking its `MMRESULT` and throwing `AudioOutputDeviceUnavailableException`
  rather than crashing or silently continuing — "recovery" here means the transport fails cleanly
  and leaves itself in a disposable, reopenable state (`DisposeAsync`/`OpenAsync` again), not that it
  automatically re-attaches to a different device. This is disclosed as a known gap in §6, proven
  instead via the deterministic "invalid device ID" negative-path tests (§4), which exercise the same
  `MMRESULT`-checking code paths a real unplug would hit.
- **`SetLoop`'s functional loop-wrap behavior is implemented and tested now, even though Step 029
  ("Add zoom-to-selection, A/B loop playback and keyboard navigation") owns the loop *UI*.** Section
  3 names `SetLoop` as part of `IAudioTransport` itself (this step's own scope), and leaving it
  unimplemented would make the interface a lie. The UI does not yet expose a way to set a loop
  window (deliberately deferred to Step 029, which owns that screen behavior); `SetLoop`'s real
  device-position-based wrap behavior is proven directly against `WaveOutAudioTransport` in
  `Step025AcceptanceTests.SetLoop_ValidWindow_WrapsPlaybackWithoutExceedingLoopEnd`.

## 4. Commands actually executed

All run via the PowerShell tool (the sandboxed POSIX Bash tool in this session is non-functional —
every invocation, including a plain `echo`/`true`, returns exit code 1 with no output — the same
pre-existing environment limitation recorded in every prior step's own evidence).

```
dotnet test XlightsAI.Studio.slnx --nologo                                                     # baseline
dotnet build src\XlightsAI.Studio.Audio\XlightsAI.Studio.Audio.csproj --nologo                  # [LibraryImport] attempt: SYSLIB1062 (AllowUnsafeBlocks required) — rejected, switched to DllImport
dotnet build src\XlightsAI.Studio.Audio\XlightsAI.Studio.Audio.csproj --nologo                  # succeeded after switching to DllImport
dotnet test tests\XlightsAI.Studio.Audio.Tests\XlightsAI.Studio.Audio.Tests.csproj --nologo --logger "console;verbosity=normal"
dotnet build src\XlightsAI.Studio.Shell\XlightsAI.Studio.Shell.csproj --nologo
dotnet build src\XlightsAI.Studio.App\XlightsAI.Studio.App.csproj --nologo
dotnet test tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj --nologo --logger "console;verbosity=normal" --filter "FullyQualifiedName~Step025"
dotnet build XlightsAI.Studio.slnx --nologo                                                     # full regression build
dotnet test XlightsAI.Studio.slnx --nologo                                                      # full regression, final
```

Plus, before implementation began, confirming real audio hardware on this machine:

```
Get-CimInstance Win32_SoundDevice | Select-Object Name, Status | Format-Table -AutoSize
```
```
Name                             Status
----                             ------
AMD Streaming Audio Device       OK
USB Audio Device                 OK
Realtek Audio                    OK
AMD High Definition Audio Device OK
```

### Baseline (before any Step 025 code existed)

```
Passed!  - Failed: 0, Passed: 71, Skipped: 0, Total: 71 - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed:  9, Skipped: 0, Total:  9 - XlightsAI.Studio.Audio.Tests.dll
Passed!  - Failed: 0, Passed: 75, Skipped: 0, Total: 75 - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed: 25, Skipped: 0, Total: 25 - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed: 77, Skipped: 0, Total: 77 - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed: 31, Skipped: 0, Total: 31 - XlightsAI.Studio.Reference.Tests.dll
```

288 total, 0 failed — matches Step 024's own recorded final count exactly, confirmed independently
in this session before a single Step 025 line of code existed.

### Build results (after implementation)

`Audio`, `Shell`, `App`, and the full solution all: `Build succeeded. 0 Warning(s). 0 Error(s).`
(One `CA1416` platform-compatibility warning at each of the two real call sites that construct
`WaveOutAudioTransport` in product code — `ShellViewModel.cs`/`Program.cs` — was deliberately
suppressed with an explanatory `#pragma warning disable CA1416`/`restore`, not silently ignored; see
§3's "Windows-only" decision. The `Step025AcceptanceTests`/`Step025ShellWiringTests` test classes are
marked `[SupportedOSPlatform("windows")]` for the same reason, which fully resolves the warning at
their call sites without any suppression needed there.)

### Targeted test results (new tests only)

```
tests\XlightsAI.Studio.Audio.Tests — 19/19 passed (9 pre-existing Step024 + 10 new Step025):
  Step025_Acceptance_Passes_PlayPauseSeekWorkOnRealAudioDevice          [534 ms]
  OpenAsync_InvalidDeviceId_ThrowsAudioOutputDeviceUnavailableException_NegativePath [10 ms]
  PlayAsync_WithoutOpen_ThrowsInvalidOperationException_NegativePath    [2 ms]
  SeekAsync_WithoutOpen_ThrowsInvalidOperationException_NegativePath
  SeekAsync_NegativeTicks_ThrowsArgumentOutOfRangeException_NegativePath [167 ms]
  SeekAsync_BeyondDuration_ThrowsArgumentOutOfRangeException_NegativePath [168 ms]
  SetLoop_InvalidWindow_ThrowsArgumentOutOfRangeException_NegativePath  [407 ms]
  SetLoop_ValidWindow_WrapsPlaybackWithoutExceedingLoopEnd              [1 s]
  OpenAsync_SilentAudio_PlaysAndAdvancesWithoutThrowing_HandlesSilenceExplicitly [379 ms]
  OpenAsync_MissingMediaFile_PropagatesFileNotFoundException_NegativePath [49 ms]

tests\XlightsAI.Studio.Shell.Tests — filter "~Step025": 5/5 passed:
  Step025_Acceptance_Passes_OpenPlayPauseSeekDriveTheRealDeviceThroughShellViewModel [379 ms]
  OpenAudioPlaybackCommand_WithoutProbedAsset_IsDisabled
  PlayPauseSeekCommands_BeforeOpen_AreDisabled
  OpenAudioPlaybackCommand_InvalidDevice_SetsErrorRatherThanThrowing_NegativePath [173 ms]
  ShellViewModel_WithoutAudioTransportCollaborator_StillConstructsAndStarts_BackwardCompatible
```

Every test above runs against the real `WaveOutAudioTransport` and this machine's real default
audio output device — no fake/mock `IAudioTransport` implementation exists anywhere in this
repository. `Step025_Acceptance_Passes_PlayPauseSeekWorkOnRealAudioDevice` specifically proves,
against real device-reported position: Play advances position, Pause freezes it, Seek-while-paused
updates it immediately, and Play-after-Seek resumes advancing from the sought position.

### Full regression (after implementation, final run)

```
Passed!  - Failed: 0, Passed: 71, Skipped: 0, Total: 71, Duration: 402 ms - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed: 75, Skipped: 0, Total: 75, Duration: 260 ms - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed: 25, Skipped: 0, Total: 25, Duration: 2 s   - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed: 82, Skipped: 0, Total: 82, Duration: 1 s   - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed: 19, Skipped: 0, Total: 19, Duration: 4 s   - XlightsAI.Studio.Audio.Tests.dll
Passed!  - Failed: 0, Passed: 31, Skipped: 0, Total: 31, Duration: 33 s  - XlightsAI.Studio.Reference.Tests.dll
```

**303 total, 0 failed, 0 skipped** — up from the Step 024 baseline of 288 (+10 new `Audio.Tests`,
77→82 in `Shell.Tests` for the +5 new Step025 wiring tests). `Xlights.Tests`, `Editing.Tests`,
`Projects.Tests`, `Reference.Tests` are unchanged in count (no source in those projects was modified
this step). All 288 pre-existing tests still pass unmodified.

### Manual/desktop demonstration

```
$p = Start-Process -FilePath "src\XlightsAI.Studio.App\bin\Debug\net10.0\XlightsAI.Studio.exe" -PassThru
Start-Sleep -Seconds 5
# Still running after 5s: True
Stop-Process -Id $p.Id -Force
```

The real compiled Avalonia desktop app, now including the new "Playback" sub-section on SCREEN 06
AUDIO (Open Playback / Play / Pause / Refresh Position buttons, a Seek field, and
open/playing/position/error display), started and ran for 5+ seconds with no exception and no early
exit — proving the new UI wiring did not regress app startup. This is a process-alive smoke test,
**not** a human-driven click-through of the Play/Pause/Seek buttons (disclosed, not silently
implied, in §6 below) — no UI-automation driver is available in this environment for the native
Avalonia window, the same standing limitation every prior PHASE-02/03/04 step's own evidence already
records for its own screen additions. The full vertical slice (probed asset → real
`WaveOutAudioTransport` → real device output → bindable `AudioImportViewModel` state) is instead
proven, end to end, by the real-hardware Shell-wiring tests in §4 above
(`Step025ShellWiringTests`), which exercise the exact same `ShellViewModel`/`AudioImportViewModel`
code path the UI binds to, through the real (not mocked) `WaveOutAudioTransport` and this machine's
real default audio device.

## 5. Fixtures / input hashes

No new fixture files were added to `fixtures/`. All tests construct real WAV bytes via this
repository's own already-licensed, already-verified `DeterministicSyntheticAudioGenerator`
(`XlightsAI.Studio.Reference.Fixtures`, PHASE-01 Step 003) — the same synthesizer Step 024's own
tests use. Specific inputs exercised:

- `BuildClickTrack(bpm: 240, clickCount: 6, clickDurationSeconds: 0.02, toneHz: 1000.0, sampleRate: 8000)`
  — a 1.5-second real-time click track, short enough to keep real-device playback tests fast while
  long enough to observe genuine device-reported position advancing, pausing, and seeking.
- `BuildClickTrack(bpm: 240, clickCount: 8, ...)` — a 2-second track for the loop-wrap test (loops
  the first quarter-second).
- A true-silence 1-second 8 kHz mono buffer (all-zero samples) — proving silent audio still plays
  and advances real device position (not mistaken for "no signal, skip playback").
- A `SongAsset` constructed via `song with { FilePath = <nonexistent path> }` — the "audio file moved
  after import" disclosed Step 024 risk, now proven to fail clearly (`FileNotFoundException`) when a
  transport actually tries to (re-)decode it for playback, not merely at probe time.

All test files are written under `%TEMP%\xlai-step025-*\` / `%TEMP%\xlai-step025-shell-*\`, deleted
by each test class's `Dispose()`/`DisposeAsync()` — identical cleanup convention to every prior
step's acceptance test class. Each test's `WaveOutAudioTransport` is itself disposed
(`IAsyncLifetime.DisposeAsync` in the Audio project's tests; `await using` in the Shell-wiring
tests) so the real device handle is released before the next test opens it.

## 6. Unresolved issues / known gaps

- **No literal "unplug the audio device mid-playback" test** — not automatable in this environment
  (no scriptable hardware removal). The same `MMRESULT`-checking/exception-throwing code paths a
  real unplug would exercise are proven instead via the deterministic "invalid device ID"
  negative-path tests (§3/§4). Recorded as a known gap, not a defect in the required acceptance
  ("missing output device handled" — proven for the "device never available" case; "device removed
  mid-stream" is a narrower sub-case of the same error-handling code path, not separately exercised).
- **Windows-only backend** (§3) — disclosed, not silently claimed; matches the App project's own
  actual current Windows-only scope. A second `IAudioTransport` implementation would be needed for
  genuine cross-platform support.
- **Whole-track in-memory buffering, not chunked streaming** (§3) — correct and simple for this
  phase's fixture/real-song scale; a known, disclosed scalability limit for very long shows, not a
  defect against this step's own required acceptance.
- **Mono-only playback** (§3) — matches Step 024's decoder output exactly; stereo/multi-channel
  output is not implemented.
- **No real resampling** — `WaveOutAudioTransport.OpenAsync` calls `DecodeMonoAsync(song,
  song.SampleRate, ct)` (the source's own native rate), consistent with Step 024's own disclosed
  "no real resampling in `DecodeMonoAsync`" gap; the device itself is opened at the source's native
  sample rate, so no resampling is needed for correct pitch/speed in this step's scope.
- **`SetLoop` has no UI exposure yet** (§3) — deliberately deferred to Step 029's own "A/B loop
  playback" screen scope; the method itself is implemented and tested now since it is part of
  Section 3's own `IAudioTransport` contract.
- **No UI-automation-driven click-through of the new Play/Pause/Seek buttons** — same disclosed,
  standing limitation as every prior step (no accessibility/UI-driver tooling available in this
  environment for the native Avalonia window). The full vertical slice is proven instead at the
  `ShellViewModel`/`AudioImportViewModel` layer through the real, un-mocked
  `WaveOutAudioTransport` and this machine's real audio device (`Step025ShellWiringTests`).
- **No real xLights installation available in this environment** — per the user's own standing
  waiver on requiring a real-xLights render check as a phase-gate blocker (recorded in this
  assistant's memory). Not applicable to this step's own required acceptance in any case: Step 025's
  acceptance is about audio playback, not xLights rendering.

## 7. PASS / BLOCKED

**Status: PASS** for Step 025's own stated required acceptance and failure/edge case:

- "Play/pause/seek works without Python" — proven by
  `Step025_Acceptance_Passes_PlayPauseSeekWorkOnRealAudioDevice` (Audio project) and
  `Step025_Acceptance_Passes_OpenPlayPauseSeekDriveTheRealDeviceThroughShellViewModel` (Shell
  wiring), both against this machine's real Windows `winmm` audio device — no Python process or
  dependency is invoked anywhere in this implementation (pure C#/P/Invoke against an OS DLL).
- "missing output device handled" — proven by
  `OpenAsync_InvalidDeviceId_ThrowsAudioOutputDeviceUnavailableException_NegativePath` (Audio
  project) and `OpenAudioPlaybackCommand_InvalidDevice_SetsErrorRatherThanThrowing_NegativePath`
  (Shell wiring): a non-existent device ID produces a specific, typed, human-readable exception at
  the transport layer, and a clean `PlaybackErrorMessage` (never an unhandled exception reaching the
  UI) at the view-model layer.
- "Silence, invalid timing boundaries and missing media must be handled explicitly" — proven by
  three distinct tests: `OpenAsync_SilentAudio_PlaysAndAdvancesWithoutThrowing_HandlesSilenceExplicitly`
  (true silence still plays and advances real position); `SeekAsync_NegativeTicks_...`/
  `SeekAsync_BeyondDuration_...` (both throw `ArgumentOutOfRangeException` — an invalid timing
  boundary is rejected, never silently clamped or ignored); `OpenAsync_MissingMediaFile_...` (a
  `SongAsset` whose file has moved/vanished since probing throws `FileNotFoundException` when a
  transport tries to actually decode it for playback).
- "Wrap platform audio output behind one transport interface; build synchronization around actual
  playback position; provide stop/pause/seek recovery on device unplug" (implementation guidance) —
  `IAudioTransport` is the one interface every caller (Shell, future steps) depends on;
  `PositionTicks` is always read from `waveOutGetPosition`'s real device-reported sample count, never
  a UI timer; every native call site checks its `MMRESULT` and surfaces failure via
  `AudioOutputDeviceUnavailableException` rather than crashing or silently continuing (§6 discloses
  the one sub-case — literal mid-stream unplug — that cannot be automated as a test here).
- The step's own user-visible outcome ("Saved audio/analysis artifact with timestamp provenance plus
  a synchronized waveform/timeline demonstration" — the waveform-rendering half is Steps 026/027's
  own future scope) is proven for its own Step-025-scoped half: a probed, already-saved `SongAsset`
  (Step 024) can be opened, played, paused, and sought through a real desktop audio backend, with its
  real playback position observable end to end through `ShellViewModel`/`AudioImportViewModel`.

No previously accepted step's contract was modified — `SongAsset`, `IAudioDecoder`,
`WavAudioDecoder`, `ISongAssetStore`/`JsonSongAssetStore`/`NullSongAssetStore`, and every other
PHASE-01–03 public member are completely unchanged, confirmed by the full pre-existing 288-test
suite passing unmodified before a single Step 025 line of code existed (§4), and again at 303/303
(288 old + 15 new) after implementation.

## 8. Phase-wide status after this session

Steps 024 and 025 (the two steps with recorded PASS evidence) are complete for PHASE 04. Steps
026–030 (waveform pyramid cache, synchronized waveform rendering, single master clock, zoom/loop/
keyboard navigation, snapping/diagnostics) and the phase-wide gate demonstration ("Import a click
track, display waveforms, drag an effect to a click, loop 1 second, seek to song midpoint and back,
close/reopen and confirm saved media identity and correct timing") remain unimplemented — per the
task's own instruction, this session implements only Step 025 and does not proceed to Step 026 until
this evidence is reviewed.
