# PHASE 01 — Step 005 Evidence

**Step:** Add structured logging, crash handling, settings and diagnostics page.
**Spec:** [`docs/phase-specs/PHASE-01.md`](../../docs/phase-specs/PHASE-01.md), Section 6, Step 005.
**Date:** 2026-10-10
**Status: PASS**

> Filename note: continuing the Step 002/003/004 precedent (`STEP-02.md`, `STEP-03.md`,
> `STEP-04.md`), this file uses `STEP-05.md` rather than the phase spec's own `STEP-005.md` form —
> an open, unresolved naming inconsistency (carried forward again, see Step 003 §10 item 1 / Step
> 004 §10 item 1).

## 1. Baseline check (prior step contracts, unmodified)

Re-ran the full existing suite before starting, unmodified:

```text
$ dotnet build XlightsAI.Studio.slnx
Build succeeded. 0 Warning(s) 0 Error(s)

$ dotnet test XlightsAI.Studio.slnx --logger "console;verbosity=normal"
XlightsAI.Studio.Shell.Tests: Total tests: 9, Passed: 9
XlightsAI.Studio.Reference.Tests: Total tests: 27, Passed: 27
```

No file under `src/XlightsAI.Studio.Reference/**` (Steps 001–003) or any fixture under
`fixtures/**` was touched. `tests/XlightsAI.Studio.Shell.Tests/Step004AcceptanceTests.cs` is also
**unmodified** — see §9 for the compatibility decision that made this possible despite
`ShellViewModel`'s constructor gaining two new collaborators.

## 2. What was built

- **Structured, correlation-ID'd diagnostics** (`src/XlightsAI.Studio.Shell/DiagnosticEvent.cs`,
  `DiagnosticEventLevel.cs`, `IAppDiagnostics.cs`, `AppDiagnostics.cs`): `IAppDiagnostics` matches
  PHASE-01 Section 3's proposed `void RecordFailure(string code, Exception error)` signature, plus
  `RecordEvent(level, code, message)` and a `RecentEvents`/`Changed` surface for the Diagnostics
  screen. `AppDiagnostics` is a file-backed sink (`<dir>/diagnostics.log`, JSON Lines, append-only)
  that:
  - assigns every event a short correlation ID (`Guid.NewGuid().ToString("N")[..12]`);
  - redacts the current user's home directory path and username from the message text before it
    is kept in memory or written to disk ("redact user filesystem paths from shared diagnostics
    where practical");
  - never throws from `RecordEvent`/`RecordFailure`: if `Directory.CreateDirectory`/file append
    fails (locked directory, a file blocking the expected directory path, etc.), the event is
    still retained in the in-memory `RecentEvents` list — a failed persisted write never drops the
    report the caller asked to record;
  - recovers its in-memory `RecentEvents` from the persisted log file on construction (bounded to
    the most recent 200), skipping any single corrupt JSON line rather than failing the whole
    subsystem over one bad record.
- **App-level settings, independent of the not-yet-existing project store**
  (`AppSettings.cs`, `ISettingsStore.cs`, `SettingsStore.cs`): `AppSettings(SchemaVersion,
  LastScreen)`, persisted as `<dir>/settings.json` via the same atomic same-directory-temp-file +
  `File.Move(overwrite: true)` pattern the architecture notes require for manifests. A missing
  file (clean machine / fresh install) returns `AppSettings.Default` with no diagnostic (expected,
  not an error). A corrupt file or a `SchemaVersion` newer than `AppSettings.CurrentSchemaVersion`
  (this step's analogue of "unsupported runtime": a settings file written by a future, unsupported
  app version) is reported to `IAppDiagnostics` and falls back to defaults — never a thrown
  exception out of `Load()`.
- **Settings and Diagnostics screens** (`ScreenLoadState.cs`, `SettingsViewModel.cs`,
  `DiagnosticsViewModel.cs`): new explicit load-state view models following the same
  Loading/Empty/Error/Loaded pattern `ProjectsViewModel` established in Step 004 (Settings has no
  meaningful Empty state — it always has a value, even if that value is `AppSettings.Default` — so
  it only models Loading/Error/Loaded).
- **`ShellScreen`** gains `Settings` and `Diagnostics` (additive, non-breaking enum change).
- **`ShellViewModel`** now composes `Settings`/`Diagnostics` view models and two new navigation
  commands; `RunStartup` loads settings *before* completing startup and restores the
  previously-visited screen (not just Welcome) once Ready — the "restart restores settings"
  acceptance. Every real (non-restore) navigation persists the new current screen via
  `ISettingsStore.Save`. A `RunStartup` failure is now both (a) turned into the existing `Failed`
  state **and** (b) recorded through `IAppDiagnostics.RecordFailure("startup-failed", ex)` — the
  crash-handling/diagnostics tie-in this step requires ("an injected error is reported without
  silent data loss").
- **Crash handling at the real process level** (`src/XlightsAI.Studio.App/Program.cs`): a real
  `AppDiagnostics`/`SettingsStore` pair (rooted at `%APPDATA%\XlightsAI.Studio`) is constructed
  before `BuildAvaloniaApp().StartWithClassicDesktopLifetime(args)` runs, and
  `AppDomain.CurrentDomain.UnhandledException` / `TaskScheduler.UnobservedTaskException` are
  registered against it, so a crash on any thread — not just inside the existing composition-root
  try/catch — is recorded rather than silently lost. `App.axaml.cs` exposes
  `App.Diagnostics`/`App.SettingsStore` static hand-off points so `Program.cs` and the shell share
  one instance.
- **UI**: `MainWindow.axaml` gains "Settings" and "Diagnostics" nav buttons and two new panels
  following the existing Loading/Empty/Error/Loaded visual pattern (Section 6 screenshots).
- **Tests**: `tests/XlightsAI.Studio.Shell.Tests/Step005AcceptanceTests.cs` (12 tests) — see §5.

## 3. Commands actually executed

```text
$ dotnet build XlightsAI.Studio.slnx
Build succeeded. 0 Warning(s) 0 Error(s)

$ dotnet build XlightsAI.Studio.slnx -c Release
Build succeeded. 0 Warning(s) 0 Error(s)

$ dotnet test tests/XlightsAI.Studio.Shell.Tests/XlightsAI.Studio.Shell.Tests.csproj --logger "console;verbosity=normal"
Total tests: 21. Passed: 21.   # 9 pre-existing Step 004 tests (unmodified) + 12 new Step 005 tests

$ dotnet test XlightsAI.Studio.slnx --logger "console;verbosity=normal"
XlightsAI.Studio.Shell.Tests: Total tests: 21, Passed: 21
XlightsAI.Studio.Reference.Tests: Total tests: 27, Passed: 27
# Full regression: 48/48 passing.
```

One real bug was caught and fixed by the tests during development: the first implementation of
`RunStartup` called `_navigator.CompleteStartup()` *before* loading/restoring settings.
`CompleteStartup()` itself raises `Changed` (with `CurrentScreen == Welcome`, the navigator's
default), which saved `Welcome` to `settings.json`, clobbering the just-loaded `LastScreen` value
before the restore-navigation ever ran. `Step005_Acceptance_Passes` failed on the first run with
`Expected: Settings / Actual: Welcome`, caught before any desktop demo was attempted. Fixed by
loading settings first and guarding both `CompleteStartup()` and the restoring `NavigateTo` with
the existing `_isRestoringLastScreen` flag so neither transition re-saves over the value being
restored (`src/XlightsAI.Studio.Shell/ShellViewModel.cs`, `RunStartup`).

Real desktop launch/manual demo commands are in Section 6. All commands were run via PowerShell;
the Bash tool again failed on every invocation in this environment (exit 1, no output), consistent
with Steps 001–004.

## 4. Required acceptance: "Restart restores settings and an injected error is reported without silent data loss"

Proven both by automated test and by the real desktop executable (Section 6):

- **Restart restores settings** — `Step005_Acceptance_Passes` creates a `ShellViewModel` instance,
  navigates to Settings, then constructs a **second, brand-new** `AppDiagnostics`/`SettingsStore`/
  `ShellViewModel` instance pointed at the same on-disk directory (simulating a process restart)
  and asserts `RunStartup()` lands directly back on the `Settings` screen rather than defaulting to
  `Welcome`. The real-desktop demo (Section 6) reproduces this exactly: close the app on the
  Diagnostics screen, relaunch the real `.exe`, observe it opens directly on Diagnostics with no
  navigation performed.
- **An injected error is reported without silent data loss** — the same test calls
  `diagnostics.RecordFailure("demo-injected-error", new InvalidOperationException("Disk nearly
  full"))` on the first instance, then asserts the **second** (post-restart) instance's
  `Diagnostics.Events` contains that exact event (code, level, message, non-empty correlation ID)
  — i.e. it survived the simulated restart via the persisted log file, not just in-memory state.
  The real-desktop demo reproduces this with the actual `AppDiagnostics` production class writing
  to the real `%APPDATA%\XlightsAI.Studio\logs\diagnostics.log`, then the real running app's
  Diagnostics screen displaying it after a fresh launch (Section 6).

## 5. Automated test results — detail

```text
Passed XlightsAI.Studio.Shell.Tests.Step005AcceptanceTests.Step005_Acceptance_Passes
Passed XlightsAI.Studio.Shell.Tests.Step005AcceptanceTests.SettingsStore_Load_WhenFileMissing_CleanMachine_ReturnsDefaultsWithoutThrowing_NegativePath
Passed XlightsAI.Studio.Shell.Tests.Step005AcceptanceTests.SettingsStore_Load_WhenFileCorrupt_UnsupportedContent_ReportsDiagnosticAndFallsBack_NegativePath
Passed XlightsAI.Studio.Shell.Tests.Step005AcceptanceTests.SettingsStore_Load_WhenSchemaNewerThanSupported_UnsupportedRuntime_ReportsDiagnosticAndFallsBack_NegativePath
Passed XlightsAI.Studio.Shell.Tests.Step005AcceptanceTests.AppDiagnostics_RecordFailure_WhenLogDirectoryMissing_CleanMachine_CreatesItWithoutThrowing
Passed XlightsAI.Studio.Shell.Tests.Step005AcceptanceTests.AppDiagnostics_WhenPersistedLogWriteFails_StillKeepsEventInMemory_NegativePath
Passed XlightsAI.Studio.Shell.Tests.Step005AcceptanceTests.AppDiagnostics_RedactsUserProfilePathAndUserName_FromMessage
Passed XlightsAI.Studio.Shell.Tests.Step005AcceptanceTests.ShellViewModel_RunStartup_WhenStartupWorkThrows_RecordsDiagnosticFailure_NegativePath
Passed XlightsAI.Studio.Shell.Tests.Step005AcceptanceTests.DiagnosticsScreen_WithNoEvents_ShowsExplicitEmptyState_NotBlank
Passed XlightsAI.Studio.Shell.Tests.Step005AcceptanceTests.SettingsScreen_WhenSourceThrows_ShowsExplicitErrorState_NegativePath
Passed XlightsAI.Studio.Shell.Tests.Step005AcceptanceTests.DiagnosticsScreen_WhenSourceThrows_ShowsExplicitErrorState_NegativePath
Passed XlightsAI.Studio.Shell.Tests.Step005AcceptanceTests.SettingsStore_SaveThenLoad_RoundTripsExactly

Total tests: 12. Passed: 12.
```

- **`Step005_Acceptance_Passes`** — the deterministic fixture required by the step: see Section 4.
- **Six dedicated negative-path tests** cover the step's required failure/edge case ("Clean
  machine and unsupported runtime must produce user-readable diagnostics") plus general
  not-silent requirements:
  - `SettingsStore_Load_WhenFileMissing_CleanMachine_...` — a fresh directory with no settings
    file returns defaults, no exception, no spurious diagnostic (this is a normal case, not a
    failure).
  - `SettingsStore_Load_WhenFileCorrupt_UnsupportedContent_...` — malformed JSON on disk produces
    a `settings-load-failed` diagnostic and falls back to defaults, never throws out of `Load()`.
  - `SettingsStore_Load_WhenSchemaNewerThanSupported_UnsupportedRuntime_...` — a settings file with
    `SchemaVersion: 999` (this step's concrete analogue of "unsupported runtime": a file from a
    future, unsupported app version) produces a `settings-unsupported-schema` diagnostic and falls
    back to defaults.
  - `AppDiagnostics_RecordFailure_WhenLogDirectoryMissing_CleanMachine_...` — recording a failure
    when the log directory does not exist yet creates it and writes successfully, proving the
    "clean machine" diagnostics path produces real, readable output rather than failing silently
    because nothing was there yet.
  - `AppDiagnostics_WhenPersistedLogWriteFails_StillKeepsEventInMemory_...` — the log "directory"
    path is made to actually be a pre-existing file, so the internal `Directory.CreateDirectory`
    call genuinely throws `IOException`; asserts `RecordFailure` still does not throw and the event
    is still present in `RecentEvents` (no silent data loss of the event itself when only the
    persisted sink fails).
  - `ShellViewModel_RunStartup_WhenStartupWorkThrows_RecordsDiagnosticFailure_...` — proves the
    crash-handling/diagnostics tie-in: a thrown startup exception is now *also* visible via
    `IAppDiagnostics.RecentEvents`, not only via the pre-existing `Failed` state.
- **`AppDiagnostics_RedactsUserProfilePathAndUserName_...`** — records a message containing the
  real `Environment.GetFolderPath(SpecialFolder.UserProfile)` path and `Environment.UserName`;
  asserts neither appears in the stored message and both are replaced with `<home>`/`<user>`.
- **`DiagnosticsScreen_WithNoEvents_...`** and **`SettingsScreen_WhenSourceThrows_...`** /
  **`DiagnosticsScreen_WhenSourceThrows_...`** — the Empty-state and Error-state equivalents of
  Step 004's `ProjectsScreen_...` tests, applied to the two new screens.
- **`SettingsStore_SaveThenLoad_RoundTripsExactly`** — a direct round-trip test independent of the
  full `ShellViewModel`, isolating the persistence contract itself.

## 6. Manual / environment verification (real desktop launch)

**Environment:** native Windows 11 Pro for Workstations machine (not headless/CI), consistent with
Steps 001–004. `%APPDATA%\XlightsAI.Studio` was deleted before the first launch below to genuinely
start from a clean-machine state (no settings file, no log directory) — not merely asserted.

1. **Clean machine, first launch** — deleted `%APPDATA%\XlightsAI.Studio`, confirmed its absence,
   then `Start-Process` the real `XlightsAI.Studio.exe`. It started, stayed running, and showed the
   Welcome screen with all four nav buttons (Welcome/Projects/Settings/Diagnostics) —
   `step-05-welcome-screen.png`
   (SHA-256 `293259c0069c65cfb80adf259b25ab3c986ad95a5b6ebeb5571f539a3e1f28c6`).
2. **Settings screen (real click)** — simulated a real left-click (`SetCursorPos` +
   `mouse_event`, not a synthetic event) on the "Settings" button; captured
   `step-05-settings-screen.png`
   (SHA-256 `d31de21f51d4b7379e84f2358c8a5f3d46f83eccb869bead131f7a6c870338ee`) showing "Last
   screen: Welcome" / "Settings schema version: 1" — real persisted/default state, not placeholder
   text.
3. **Diagnostics screen, empty state (real click)** — clicked "Diagnostics"; captured
   `step-05-diagnostics-empty-screen.png`
   (SHA-256 `acdb64801f3de9b37ebca5bbcf8d89d2128804857413ed51aceb3a67b080c5a5`) showing the explicit
   "No diagnostic events recorded." text — not a blank panel.
4. **Close on Diagnostics, verify persisted settings file** — `CloseMainWindow()`, confirmed
   process exited, then read the real `%APPDATA%\XlightsAI.Studio\settings.json`:
   `{"SchemaVersion":1,"LastScreen":3}` (`LastScreen` enum value `3` = `Diagnostics`) — proves the
   real app, not a test double, persisted the navigation state on exit.
5. **Restart restores settings (real relaunch)** — started a **second, independent** real process
   of the same `.exe` with no navigation performed by the automation; captured
   `step-05-restart-restores-diagnostics-screen.png`
   (SHA-256 `ce18d40263bbfc9d16f4c6497f24ec5616a37accbbf4286d81bb58163f4c24006`) showing the app
   opened directly on the Diagnostics screen — the literal "restart restores settings" acceptance,
   demonstrated against the real executable, not inferred from code or from the unit test alone.
6. **Injected error is reported (real production code, real file)** — used the already-built
   `XlightsAI.Studio.Shell.dll`'s real `AppDiagnostics` class (not a mock/stand-in — the exact type
   `Program.cs` uses) via reflection to call
   `RecordFailure("manual-demo-injected-crash", new InvalidOperationException(...))` against the
   real `%APPDATA%\XlightsAI.Studio\logs` directory; confirmed `diagnostics.log` was written with a
   correlation ID (`96fcb23727a3`). Relaunched the real `.exe` a third time and captured
   `step-05-diagnostics-shows-injected-error.png`
   (SHA-256 `fd2bd7143c9432d7ebc122a976d1851d762945385f4fb6d0a59e21a6873c4350`) showing the
   Diagnostics screen displaying that exact event (`CorrelationId = 96fcb23727a3`, `Level =
   Error`, `Code = manual-de[mo-injected-crash]`, truncated by window width — see §10 item 3) —
   proving the real app reads back a diagnostic event that was never produced by that process's
   own code path, the same way a true prior-run crash would be read back and reported today.
7. **No Python runtime:** confirmed by inspection — no new `PackageReference`, `ProjectReference`,
   process launch or `.py`/embedded-interpreter dependency was added anywhere in this step; the
   only new dependency is `System.Text.Json`, which ships in the shared .NET 10 runtime (no new
   `PackageReference` was required).
8. **xLights validation:** not applicable — this step produces no XSQ/layout/rendering artifact.

## 7. Changed/added paths

```
src/XlightsAI.Studio.Shell/DiagnosticEventLevel.cs
src/XlightsAI.Studio.Shell/DiagnosticEvent.cs
src/XlightsAI.Studio.Shell/IAppDiagnostics.cs
src/XlightsAI.Studio.Shell/AppDiagnostics.cs
src/XlightsAI.Studio.Shell/NullAppDiagnostics.cs
src/XlightsAI.Studio.Shell/AppSettings.cs
src/XlightsAI.Studio.Shell/ISettingsStore.cs
src/XlightsAI.Studio.Shell/SettingsStore.cs
src/XlightsAI.Studio.Shell/NullSettingsStore.cs
src/XlightsAI.Studio.Shell/ScreenLoadState.cs
src/XlightsAI.Studio.Shell/SettingsViewModel.cs
src/XlightsAI.Studio.Shell/DiagnosticsViewModel.cs
src/XlightsAI.Studio.Shell/ShellScreen.cs                 (modified: + Settings, Diagnostics)
src/XlightsAI.Studio.Shell/ShellViewModel.cs              (modified: see §9)
src/XlightsAI.Studio.App/Program.cs                       (modified: crash handlers, diagnostics/settings composition)
src/XlightsAI.Studio.App/App.axaml.cs                     (modified: static hand-off, wiring)
src/XlightsAI.Studio.App/Views/MainWindow.axaml           (modified: Settings/Diagnostics nav + panels)
tests/XlightsAI.Studio.Shell.Tests/Step005AcceptanceTests.cs
implementation-evidence/PHASE-01/step-05-welcome-screen.png
implementation-evidence/PHASE-01/step-05-settings-screen.png
implementation-evidence/PHASE-01/step-05-diagnostics-empty-screen.png
implementation-evidence/PHASE-01/step-05-restart-restores-diagnostics-screen.png
implementation-evidence/PHASE-01/step-05-diagnostics-shows-injected-error.png
implementation-evidence/PHASE-01/STEP-05.md
```

`tests/XlightsAI.Studio.Shell.Tests/Step004AcceptanceTests.cs` and everything under
`src/XlightsAI.Studio.Reference/**`, `fixtures/**`, `reference/**`, `tools/**` were **not**
modified. No xLights show files exist in or near this repository; this step produces no
XSQ/show-folder writes.

## 8. Package/dependency inventory (new, this step only)

No new `PackageReference` was added. `System.Text.Json` (used for settings/diagnostics
serialization) ships in the .NET 10 shared framework and required no explicit package reference.
No Python, Docker, Git-at-runtime, or native ML/audio dependency was added.

## 9. Architecture deviations

- **`ShellViewModel`'s constructor gained two new parameters, both optional with no-op defaults —
  not a breaking change to Step 004's contract.** The phase instruction for this task says "Treat
  all preceding completed step contracts as immutable unless the current step has an accepted
  migration plan." Rather than rewriting Step 004's call sites (its own `App.axaml.cs` wiring and
  `Step004AcceptanceTests.cs`) to pass the new collaborators, `ISettingsStore settingsStore = null`
  and `IAppDiagnostics diagnostics = null` default to `NullSettingsStore.Instance` /
  `NullAppDiagnostics.Instance` (new no-op implementations) when omitted. This is the
  "accepted migration plan": Step 004's two-argument constructor call keeps compiling and behaving
  exactly as before (always starts on Welcome, nothing persisted, no diagnostics recorded) with
  **zero changes** to `Step004AcceptanceTests.cs` (verified: all 9 tests still pass unmodified,
  Section 1/3). The real `App.axaml.cs` composition for this step is explicitly updated to pass the
  real `SettingsStore`/`AppDiagnostics` instances.
- **`ShellScreen` enum extended, not replaced.** Adding `Settings`/`Diagnostics` members is
  additive; existing `Welcome`/`Projects` values and their underlying ordinals are unchanged, so
  anything (including the persisted `LastScreen` int value in `settings.json`) serialized against
  the old two-value enum remains valid.
- **A new `ScreenLoadState` enum, not a retrofit of `ProjectsLoadState`.** `ProjectsLoadState` is a
  Step 004 contract; rather than generalizing it (which would touch an already-accepted type),
  this step introduces its own shared `ScreenLoadState` for the two screens it adds. `ProjectsViewModel`
  itself is untouched.
- **Crash handling registered at the process level (`Program.cs`), not only inside Avalonia.**
  `AppDomain.CurrentDomain.UnhandledException` and `TaskScheduler.UnobservedTaskException` are
  registered before `BuildAvaloniaApp()` runs, so a crash during Avalonia's own startup — not just
  inside the shell's `RunStartup` — is still captured. A true UI-thread Avalonia `Dispatcher`
  unhandled-exception hook was **not** added (Avalonia core does not expose a simple public
  equivalent to WPF's `DispatcherUnhandledException` without an additional package); this is
  disclosed as a scoping gap in §10, not silently omitted.
- **Settings are read-only in this step's UI**, per the implementation guidance's own framing
  ("Test read-only settings") and the phase's "do not present empty future functionality as
  implemented" rule — there is no settings-editing form; the Settings screen only ever displays
  what is currently persisted (today: just the last-visited screen and schema version). A full
  settings-editing UI is not in this step's scope.
- **Settings/diagnostics persistence is synchronous**, matching Step 004's existing synchronous
  `RunStartup` pattern, rather than introducing `async`/`CancellationToken` plumbing a step ahead of
  Step 006/007's own project-store contracts (which do define the `async`/`CancellationToken`
  shape for project-scoped persistence). This app-level store is a narrower, simpler concern and
  does not need to anticipate that contract.

## 10. Unresolved issues / risks carried forward

1. **Evidence-filename convention inconsistency** — still unresolved (carried forward again from
   Steps 002/003/004).
2. **No true Avalonia UI-thread dispatcher exception hook** (§9) — an exception thrown directly
   inside a button-click handler on the UI thread, if one existed, would not be guaranteed to flow
   through the registered `AppDomain.UnhandledException` handler before the process terminates on
   some platforms. Recommend revisiting with `Avalonia.Diagnostics`/a dedicated UI-exception
   package if later phases add UI logic with meaningfully more failure surface than this step's
   read-only screens.
3. **Diagnostic event display is `ToString()`-on-the-record, not a formatted/wrapped template** —
   visible in `step-05-diagnostics-shows-injected-error.png`, where the record's default
   `ToString()` output is clipped by the window's right edge rather than wrapped. Functionally
   correct (the data is there and bound) but not yet a polished diagnostics UI; a later step should
   give `DiagnosticEvent` a dedicated `DataTemplate` with `TextWrapping`.
4. **No automated UI-level (headless Avalonia) test was added** — same disclosed gap as Step 004
   §10 item 3; this step's new screens are covered by `ShellViewModel`/`SettingsViewModel`/
   `DiagnosticsViewModel` unit tests plus the real manual demo (Section 6), not an automated UI
   test.
5. Git Bash remains non-functional in this environment (carried forward from Steps 001–004); all
   commands in this evidence were run via PowerShell.
6. The log file (`diagnostics.log`) and settings file (`settings.json`) are unbounded/unrotated
   beyond the in-memory 200-event cap already applied to `RecentEvents` — the on-disk log file
   itself is never trimmed. Acceptable for this step's scope; flagged for a later step if long-running
   installs make this a real-disk-usage concern.

## 11. Gate checklist (this step only)

- [x] Structured diagnostics with correlation IDs implemented and user-filesystem-path redaction
      verified by test (Section 5) and real production code (Section 6 item 6).
- [x] Crash handling registered at the process level (`AppDomain.UnhandledException`,
      `TaskScheduler.UnobservedTaskException`), tied into the same diagnostics sink the Diagnostics
      screen reads.
- [x] Settings persist and restore across a real process restart — proven by automated test
      *and* by a real, independent second launch of the actual executable (Section 6 item 5).
- [x] Diagnostics page implemented with explicit Loading/Empty/Error/Loaded states; proven
      non-blank-empty (Section 6 item 3) and showing a real injected/persisted error after restart
      (Section 6 item 6).
- [x] Required acceptance — "Restart restores settings and an injected error is reported without
      silent data loss" — proven end to end against the real desktop executable, not only unit
      tests.
- [x] Failure/edge case — "Clean machine and unsupported runtime must produce user-readable
      diagnostics" — proven by a real clean-machine launch (Section 6 item 1, `%APPDATA%` deleted
      first) and by dedicated negative-path tests for missing/corrupt/future-schema settings files
      (Section 5).
- [x] `Step005_Acceptance_Passes` plus eleven dedicated negative-path/explicit-state tests, all
      passing (12/12).
- [x] Full existing regression suite re-run: 48/48 passing (21 Shell, incl. 9 unmodified Step 004
      tests, + 27 Reference).
- [x] Step 004's `ShellViewModel` contract treated as immutable; the two new required collaborators
      are optional with documented no-op defaults rather than a breaking signature change
      (Section 9).
- [x] No original/show files touched; nothing pre-existing was at risk.
- [ ] Not yet done, out of this step's scope: real project create/open/save (Step 006), SQLite
      persistence (Step 007), a true UI-thread dispatcher exception hook (§10 item 2), a formatted
      diagnostics list template (§10 item 3).

**Step 005 status: PASS.** Proceed to Step 006 only after this is reviewed.
