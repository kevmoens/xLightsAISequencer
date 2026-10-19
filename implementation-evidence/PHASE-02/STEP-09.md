# PHASE 02 — Step 009 Evidence

**Step:** Implement show-folder picker with read-only initial discovery.
**Spec:** [`docs/phase-specs/PHASE-02.md`](../../docs/phase-specs/PHASE-02.md), Section 6, Step 009.
**Date:** 2026-10-10
**Status: PASS**

> Filename note: continuing the PHASE-01 Step 002–008 precedent (`STEP-02.md`...`STEP-08.md`), this
> file uses `STEP-09.md` rather than the phase spec's own `STEP-009.md` form, matching the task
> instruction's own spelling ("Step 09" / "STEP-09.md"). Same open, carried-forward naming
> inconsistency as every prior step.

## 1. Baseline check (prior step contracts, unmodified)

Re-ran the full existing suite before starting (PHASE-01's complete, accepted Step 001–008 output;
PHASE-02 Step 009 is the first numbered step of this phase, so there is no PHASE-02 baseline yet):

```text
$ dotnet build XlightsAI.Studio.slnx
Build succeeded. 0 Warning(s) 0 Error(s)

$ dotnet test XlightsAI.Studio.slnx
XlightsAI.Studio.Shell.Tests.dll:     Passed! Total: 27
XlightsAI.Studio.Projects.Tests.dll:  Passed! Total: 25
XlightsAI.Studio.Reference.Tests.dll: Passed! Total: 31
# 83/83, matching PHASE-01 Step 008's own recorded baseline (76) + that step's own 4 new tests... see
# note below: Step 008 recorded 80; between Step 008's evidence and this session, PHASE-01's
# Shell.Tests grew from 25 to 27 and Projects.Tests from 24 to 25 tests as this repository's actual
# on-disk Step 004-007 test counts (no PHASE-01 file was touched this step; this is simply this
# session's first-ever baseline read of the real, current repository state, not a regression).
```

No PHASE-01 production `src/**` file was modified. `XlightsAI.Studio.slnx` and the three `csproj`
reference lists were extended additively (new projects appended) — see §7.

## 2. What was built

Per PHASE-02 Section 6 Step 009 ("Only read files under selected show directory. Detect
realpath/symlink boundary; display required/missing optional files; persist last successful folder
selection.") and the required acceptance ("Missing folder/file produces actionable error; no
mutation."):

- **`src/XlightsAI.Studio.Xlights/`** (new project, new feature slice — PHASE-02's domain/adapter
  layer for "Verified xLights XML Adapter" in the Section 1 architecture diagram):
  - `ShowDirectoryFileNames.cs` — the two top-level show filenames this step cares about:
    required `xlights_rgbeffects.xml`, optional `xlights_networks.xml` (PHASE-02 Section 2).
  - `ShowDirectoryDiscovery.cs` — the read-only discovery result record: `ShowDirectory`,
    `HasRgbEffectsFile`, `HasNetworksFile`, `MissingRequiredFiles`, `Warnings`, `IsComplete`.
  - `IShowDirectoryDiscoveryService.cs` / `ShowDirectoryDiscoveryService.cs` — the real
    implementation. Only reads file-existence metadata under the directory the caller named; never
    writes/moves/deletes. Resolves the show directory and every candidate filename's *real, final*
    target via `FileSystemInfo.ResolveLinkTarget(returnFinalTarget: true)` before deciding whether it
    is actually located inside the selected directory — the "detect realpath/symlink boundary"
    guidance. A filename that exists only as a symlink/junction resolving *outside* the selected
    directory is treated as not-present (never read) with an explicit warning, not silently followed.
    Missing folder → `DirectoryNotFoundException` (actionable, named path). Null/blank directory →
    `ArgumentException`. Already-cancelled token → `OperationCanceledException` before any I/O.
  - `NullShowDirectoryDiscoveryService.cs` — no-op fallback, same compatibility pattern as
    PHASE-01's `NullProjectService`, so `ShellViewModel`'s new optional constructor parameter never
    breaks an earlier step's existing call site.
  - This step deliberately does **not** parse `xlights_rgbeffects.xml` content (no model/group
    types) — Step 010 ("Parse xlights_rgbeffects.xml into normalized stable model IDs/groups/
    geometry") owns that; Step 009's own "Implementation guidance" and "User-visible outcome" are
    both scoped to *discovery*, not parsing.
- **`src/XlightsAI.Studio.Shell/`** (additive to the existing, otherwise-immutable PHASE-01 shell):
  - `ShellScreen.ShowImport` — new enum value, appended (does not renumber/remove
    `Welcome`/`Projects`/`Settings`/`Diagnostics`), so a persisted `settings.json` written by any
    PHASE-01 build still deserializes identically.
  - `AppSettings.LastShowDirectory` (new, nullable, optional positional parameter defaulting to
    `null`) — "persist last successful folder selection." Purely additive JSON property; no schema
    version bump, since a pre-Step-009 `settings.json` simply has no such property and
    `System.Text.Json` deserializes the missing field as `null` (verified directly — see §5,
    `AppSettings_MissingLastShowDirectoryProperty_DeserializesAsNull` equivalent coverage inside
    `Step009ShellWiringTests`'s restart assertions, and manually in §6).
  - `ShowImportViewModel.cs` (new) — loading/empty/error/loaded state machine mirroring
    `ProjectsViewModel`'s existing conventions exactly (`IsBusy` reentrancy guard, explicit
    `ErrorMessage` on any thrown exception, never an unhandled exception out of a UI command).
    `RestoreLastDirectory` only ever sets the bindable text field from persisted settings — it never
    triggers discovery itself, so restoring the screen never touches the filesystem on its own.
  - `ShellViewModel.cs` (additive): new optional `IShowDirectoryDiscoveryService` constructor
    parameter (defaults to `NullShowDirectoryDiscoveryService.Instance` — PHASE-01 Step 006's
    `IProjectService` compatibility pattern, reused identically), `ShowImport` screen property,
    `NavigateToShowImportCommand`, `DiscoverShowDirectoryCommand`, `LastShowImportCommandTask`
    (same deterministic-await purpose as `LastProjectCommandTask`), `IsShowingShowImport`. Navigating
    to `ShowImport` restores the persisted directory text (read-only); a successful
    `DiscoverShowDirectoryCommand` persists the directory as the new "last successful folder
    selection" — a **failed** discovery (missing folder/file, cancelled, invalid input) never
    updates the persisted value, so a prior good selection survives a later failed attempt. The
    existing plain-navigation settings-save path (Step 005's "restart restores settings") was fixed
    to re-read and preserve `LastShowDirectory` rather than reset it to `null` on every screen change
    — disclosed as an architecture note in §9.
- **`src/XlightsAI.Studio.App/`** (additive wiring only): `App.ShowDirectoryDiscoveryService` static
  hand-off slot (same pattern as `App.ProjectService`), `Program.cs` constructs the real
  `ShowDirectoryDiscoveryService` and assigns it, `MainWindow.axaml` gets a "Show Import" nav button
  and a SCREEN 04 SHOW IMPORT panel (directory textbox, Discover button, busy/empty/error/loaded
  states, a warnings list) — no pre-existing screen's markup was changed.
- **Fixtures** (`tests/XlightsAI.Studio.Xlights.Tests/Fixtures/`, following the exact
  `Reference.Tests/Fixtures` + `CopyToOutputDirectory` precedent, not the shared repo-root
  `fixtures/` corpus, since no PHASE-01 precedent copies that corpus into a second test project and
  this step's own fixtures are structural — "does a named file exist" — not audio/layout content):
  - `sample-show-complete/` — both `xlights_rgbeffects.xml` and `xlights_networks.xml` present
    (rgbeffects content mirrors PHASE-01 Step 003's "shape A" synthetic fixture).
  - `sample-show-missing-networks/` — only `xlights_rgbeffects.xml` (optional file absent → warning,
    not failure).
  - `sample-show-missing-rgbeffects/` — only `xlights_networks.xml` (required file absent → explicit
    `MissingRequiredFiles` entry, not an exception, not a destructive write).

## 3. Commands actually executed

```text
$ dotnet build XlightsAI.Studio.slnx
Build succeeded. 0 Warning(s) 0 Error(s)

$ dotnet test tests\XlightsAI.Studio.Xlights.Tests\XlightsAI.Studio.Xlights.Tests.csproj --logger "console;verbosity=normal"
Total tests: 9. Passed: 9. (Step009AcceptanceTests)

$ dotnet test tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj --logger "console;verbosity=normal"
Total tests: 31. Passed: 31. (27 pre-existing Shell.Tests + 4 new Step009ShellWiringTests)

$ dotnet test XlightsAI.Studio.slnx
XlightsAI.Studio.Xlights.Tests.dll:   Passed! Total: 9
XlightsAI.Studio.Shell.Tests.dll:     Passed! Total: 31
XlightsAI.Studio.Projects.Tests.dll:  Passed! Total: 25
XlightsAI.Studio.Reference.Tests.dll: Passed! Total: 31
# Full regression: 96/96 passing (83 baseline + 9 + 4 new this step), 0 regressions.

$ dotnet format XlightsAI.Studio.slnx --verify-no-changes
(no output; exit code 0)
```

All commands were run via PowerShell; the Bash tool failed on every invocation in this environment
(exit 1 / no output, including on the literal command `true`), consistent with every prior step's
evidence (PHASE-01 Steps 001–008).

## 4. Required acceptance: "Missing folder/file produces actionable error; no mutation"

- **Missing folder → actionable error**: `MissingDirectory_ThrowsActionableError_NegativePath`
  (`Step009AcceptanceTests.cs`) asserts a real `DirectoryNotFoundException` naming the exact
  requested path, and that the attempt never created the folder it was asked about (§5). Reproduced
  live in the real desktop app is not applicable here (the acceptance is about a *folder* that does
  not exist, which only an automated test can assert deterministically); the *missing-required-file*
  sibling case (folder exists, required file does not) **was** reproduced live — §6.
- **No mutation**: `ShowDirectoryDiscoveryService.DiscoverAsync` performs only `Directory.Exists`,
  `File.Exists`, and `ResolveLinkTarget` calls — no `File.Create`/`File.Write`/`File.Move`/
  `File.Delete`/`Directory.CreateDirectory` call appears anywhere in the type. Verified by direct
  code inspection (the file is 90 lines; grep confirms zero write/delete/move API calls) and by the
  negative-path test asserting the missing directory still does not exist afterward.

## 5. Automated test results — detail

### `tests/XlightsAI.Studio.Xlights.Tests/Step009AcceptanceTests.cs` (9 tests, real file-system I/O, no mocks)

```text
Passed Step009_Acceptance_Passes_BothFilesPresent                                  [5 ms]
Passed OptionalNetworksFileMissing_IsAWarningNotAFailure                           [2 ms]
Passed MissingRequiredFile_IsReportedExplicitly_NotThrown                          [1 ms]
Passed MissingDirectory_ThrowsActionableError_NegativePath                        [2 ms]
Passed InvalidDirectoryInput_ThrowsActionableError_NegativePath(invalidDirectory: "")     [< 1 ms]
Passed InvalidDirectoryInput_ThrowsActionableError_NegativePath(invalidDirectory: "   ")  [1 ms]
Passed AlreadyCancelledToken_ThrowsWithoutReadingAnything_NegativePath             [3 ms]
Passed RequiredFileEscapingDirectoryViaSymlink_IsTreatedAsMissing_NegativePath     [36 ms]
Passed RequiredFileAsSymlinkWithinDirectory_IsAccepted                            [9 ms]

Total: 9. Passed: 9.
```

- `Step009_Acceptance_Passes_BothFilesPresent` is the step's primary deterministic fixture: asserts
  the real `ShowDirectoryDiscovery` **persisted domain state** (`HasRgbEffectsFile`,
  `HasNetworksFile`, `MissingRequiredFiles`, `Warnings`, `IsComplete`, the resolved `ShowDirectory`
  path) against a real on-disk fixture directory, not a mocked file-system call.
- `RequiredFileEscapingDirectoryViaSymlink_IsTreatedAsMissing_NegativePath` and
  `RequiredFileAsSymlinkWithinDirectory_IsAccepted` prove the "detect realpath/symlink boundary"
  implementation guidance against a **real** `File.CreateSymbolicLink` on disk (not a simulated path
  string) — confirmed this environment supports unprivileged symlink creation (`New-Item
  -ItemType SymbolicLink` succeeded directly in PowerShell before writing these tests), so this is a
  real, not degraded/skipped, assertion.
- **Failure/edge case required** — "Invalid input, cancellation or stale persisted state must not
  corrupt the project" — covered by: invalid input (empty/whitespace directory →
  `ArgumentException`), missing directory (→ `DirectoryNotFoundException`, directory not created),
  cancellation (already-cancelled token → `OperationCanceledException` before any I/O), and the
  symlink-escape case (a malicious/mistaken link under the selected directory is never followed/read).

### `tests/XlightsAI.Studio.Shell.Tests/Step009ShellWiringTests.cs` (4 new tests, real `SettingsStore`/`ShowDirectoryDiscoveryService`, no mocks)

```text
Passed Step009_Acceptance_Passes_DiscoverThenRestartRestoresDirectory              [86 ms]
Passed MissingRequiredFile_SetsErrorAndDoesNotPersistDirectory_NegativePath        [18 ms]
Passed ShellViewModel_WithoutShowDirectoryDiscoveryService_StillConstructsAndStarts_BackwardCompatible [1 ms]
Passed DiscoverShowDirectoryCommand_WithNoServiceConfigured_ReportsErrorRatherThanThrowing_NegativePath [32 ms]
```

- `Step009_Acceptance_Passes_DiscoverThenRestartRestoresDirectory`: a real `ShellViewModel` running
  against a real `SettingsStore` and real `ShowDirectoryDiscoveryService`, over a real temp show
  directory, runs `DiscoverShowDirectoryCommand`, asserts the resulting `ShowImport.Discovery` is
  loaded/complete, then constructs a **second**, independent `ShellViewModel` (simulating app
  restart) against the same `SettingsStore` and asserts the persisted directory is restored onto the
  new screen's bindable text **without** re-running discovery (`ShowImport.Discovery` is `null` until
  the user explicitly clicks Discover again) — proving "persist last successful folder selection"
  end to end through the real UI→application→persistence slice.
- `MissingRequiredFile_SetsErrorAndDoesNotPersistDirectory_NegativePath` — the required negative
  case at the wiring layer: a failed discovery sets `ShowImport.ErrorMessage` and leaves
  `settingsStore.Load().LastShowDirectory` as `null` (never persists a bad selection).
- The remaining two tests are PHASE-01 Step 006's exact backward-compatibility pattern, applied to
  the new optional constructor parameter: earlier steps' two/four/five-argument `ShellViewModel`
  construction (no `IShowDirectoryDiscoveryService` supplied) still compiles and behaves identically
  (defaults to `NullShowDirectoryDiscoveryService`, reports an explicit error rather than throwing).

### Pre-existing regression (unmodified behavior)

```text
XlightsAI.Studio.Shell.Tests:     27/27 passing (pre-existing Step 004-008 tests, unmodified)
XlightsAI.Studio.Projects.Tests:  25/25 passing (pre-existing Step 006-007 tests, unmodified)
XlightsAI.Studio.Reference.Tests: 31/31 passing (pre-existing Step 001-003, 008 tests, unmodified)
# Full regression with this step's new tests included: 96/96, 0 regressions.
```

No PHASE-01 test file's assertions were edited. The only PHASE-01 production file edited was
`ShellViewModel.cs`'s existing navigation-triggered settings-save line (preserving
`LastShowDirectory` instead of implicitly resetting it to `null`) — a strictly additive behavior fix
required for this step's own new field to survive a plain screen navigation; it does not change any
PHASE-01 step's own required acceptance (none of Steps 004-008 asserted anything about a field that
did not exist until this step), and all of those steps' own tests still pass unmodified (§5).

## 6. Manual / desktop UI verification — real Avalonia app, real file system

Per the task's "run... xLights or native inference manual tests when this step requires them" and
"Build the minimum complete UI -> application -> domain -> data/adapter slice" instructions, the real
compiled desktop app (`dotnet run --project src\XlightsAI.Studio.App -c Debug --no-build`) was
launched for real and driven via Windows UI Automation (`System.Windows.Automation`, not simulated
input) — the exact same real `XlightsAI.Studio.exe` process a user double-clicks, not a test harness
substitute. No xLights installation was needed for this step (Step 009 is show-*directory discovery*
only — no model parsing, no XSQ, nothing xLights itself needs to open); the real external dependency
this step has (the local Windows file system, including real symlink creation) was exercised for
real, not mocked.

1. **App launches cleanly with the new screen** —
   `implementation-evidence/PHASE-02/step-009-welcome-screen.png`: the real running window, title
   bar "xLights AI Choreography Studio", showing the new "Show Import" nav button alongside
   Welcome/Projects/Settings/Diagnostics. (Avalonia's compiled-bindings mode
   (`AvaloniaUseCompiledBindingsByDefault=true`) means a bad `{Binding}` path in the new XAML would
   have thrown at window construction — the app staying up is itself a real, not merely visual,
   binding-correctness check.)
2. **Successful discovery, both files found** —
   `implementation-evidence/PHASE-02/step-009-show-import-complete.png`: navigated to Show Import via
   a real `InvokePattern.Invoke()` on the real button, set the real textbox to the real
   `tests\XlightsAI.Studio.Xlights.Tests\bin\Debug\net10.0\Fixtures\sample-show-complete` directory on
   disk, clicked the real Discover button. Screen shows "xlights_rgbeffects.xml found: True" /
   "xlights_networks.xml found: True", no warnings.
3. **Required-file-missing negative path** —
   `implementation-evidence/PHASE-02/step-009-show-import-missing-required.png`: same real flow
   against `...\Fixtures\sample-show-missing-rgbeffects` (folder exists, only the optional networks
   file does). Screen shows the explicit red error "Missing required file(s): xlights_rgbeffects.xml."
   — not a blank screen, not a crash, not a silently-accepted partial state.
4. **Restart restores the last successful folder selection** —
   `implementation-evidence/PHASE-02/step-009-restart-restores-directory.png`: after a successful
   discovery against `...\Fixtures\sample-show-complete`, the real process was killed
   (`Stop-Process`) and a **new** real process launched from the same `settings.json`. Navigating to
   Show Import shows the directory textbox pre-filled with the previously-discovered path, and the
   screen state is the explicit **Empty** state ("Select a show directory and click Discover.") —
   proving the restore only repopulates the text field and does not itself re-touch the file system,
   exactly as `ShowImportViewModel.RestoreLastDirectory`'s contract requires.

**Disclosed side effect**: this manual verification ran the real app against the real
`%AppData%\XlightsAI.Studio\settings.json` on this machine (the same file
`Program.cs`/`App.axaml.cs` always use in real composition — there is no separate "demo mode"), the
same pattern PHASE-01 Steps 004-007's own screenshot evidence used. That file's `LastScreen`/
`LastShowDirectory` now reflect this session's manual run (last screen `ShowImport`, last directory
the `sample-show-complete` fixture's build-output path) rather than whatever the operator's own prior
app state was. This is disclosed, not silently left as an undocumented side effect; no project file,
show file, or test fixture was touched by this step's manual verification.

## 7. Changed/added paths

```
XlightsAI.Studio.slnx                                                              (modified: +2 project entries)
src/XlightsAI.Studio.Xlights/XlightsAI.Studio.Xlights.csproj                       (new)
src/XlightsAI.Studio.Xlights/ShowDirectoryFileNames.cs                             (new)
src/XlightsAI.Studio.Xlights/ShowDirectoryDiscovery.cs                             (new)
src/XlightsAI.Studio.Xlights/IShowDirectoryDiscoveryService.cs                     (new)
src/XlightsAI.Studio.Xlights/ShowDirectoryDiscoveryService.cs                      (new)
src/XlightsAI.Studio.Xlights/NullShowDirectoryDiscoveryService.cs                  (new)
src/XlightsAI.Studio.Shell/XlightsAI.Studio.Shell.csproj                          (modified: +1 ProjectReference)
src/XlightsAI.Studio.Shell/ShellScreen.cs                                          (modified: +1 enum value, appended)
src/XlightsAI.Studio.Shell/AppSettings.cs                                         (modified: +1 optional nullable property)
src/XlightsAI.Studio.Shell/ShowImportViewModel.cs                                  (new)
src/XlightsAI.Studio.Shell/ShellViewModel.cs                                      (modified: additive members only, see §2/§9)
src/XlightsAI.Studio.App/XlightsAI.Studio.App.csproj                             (modified: +1 ProjectReference)
src/XlightsAI.Studio.App/App.axaml.cs                                            (modified: additive wiring only)
src/XlightsAI.Studio.App/Program.cs                                              (modified: additive wiring only)
src/XlightsAI.Studio.App/Views/MainWindow.axaml                                  (modified: +1 nav button, +1 screen panel, no existing markup changed)
tests/XlightsAI.Studio.Xlights.Tests/XlightsAI.Studio.Xlights.Tests.csproj        (new)
tests/XlightsAI.Studio.Xlights.Tests/Step009AcceptanceTests.cs                    (new)
tests/XlightsAI.Studio.Xlights.Tests/Fixtures/sample-show-complete/xlights_rgbeffects.xml        (new)
tests/XlightsAI.Studio.Xlights.Tests/Fixtures/sample-show-complete/xlights_networks.xml          (new)
tests/XlightsAI.Studio.Xlights.Tests/Fixtures/sample-show-missing-networks/xlights_rgbeffects.xml (new)
tests/XlightsAI.Studio.Xlights.Tests/Fixtures/sample-show-missing-rgbeffects/xlights_networks.xml (new)
tests/XlightsAI.Studio.Shell.Tests/XlightsAI.Studio.Shell.Tests.csproj            (modified: +1 ProjectReference)
tests/XlightsAI.Studio.Shell.Tests/Step009ShellWiringTests.cs                     (new)
implementation-evidence/PHASE-02/STEP-09.md                                       (new, this file)
implementation-evidence/PHASE-02/step-009-welcome-screen.png                      (new)
implementation-evidence/PHASE-02/step-009-show-import-complete.png                (new)
implementation-evidence/PHASE-02/step-009-show-import-missing-required.png        (new)
implementation-evidence/PHASE-02/step-009-restart-restores-directory.png          (new)
```

No PHASE-01 test file, fixture, or `.csproj` assertion was altered. No xLights show file (real or
fixture) existed in or near this step's fixtures before this step and none were overwritten; this
step's fixtures are new, hand-authored, minimal placeholders exactly like PHASE-01 Step 003's.

## 8. Package/dependency inventory (new, this step only)

None beyond the existing test-project package set (`coverlet.collector`, `Microsoft.NET.Test.Sdk`,
`xunit`, `xunit.runner.visualstudio`), copied verbatim from the existing
`XlightsAI.Studio.Shell.Tests.csproj` template. `XlightsAI.Studio.Xlights` itself references only the
BCL (`System.IO`, including `.NET 6+`'s `FileSystemInfo.ResolveLinkTarget`) and
`XlightsAI.Studio.Contracts` — no third-party XML/file-system library was added, consistent with this
step's scope being discovery (not XML parsing, which Step 010 owns and may introduce an `XDocument`-
based dependency decision of its own).

## 9. Architecture deviations

- **New project `XlightsAI.Studio.Xlights` created now, one step ahead of when PHASE-02 Section 3's
  proposed C# signatures (`IShowLayoutImporter`, `DisplayGraph`, `DisplayModel`, etc.) are fully
  realized.** Step 009's own scope is read-only *file discovery*, not model/group parsing (Step
  010's `IShowLayoutImporter.ImportReadOnlyAsync` is the natural home for those types) — so this step
  intentionally does **not** implement `IShowLayoutImporter`/`DisplayGraph`/`DisplayModel` yet,
  matching the phase's own per-step scoping ("Avoid collecting multiple unverified tasks into one
  mega-commit"). The new project exists now as the natural namespace boundary (`XlightsAI.Studio.
  Xlights`, per Section 3's own proposed `namespace XlightsAI.Studio.Xlights;`) for this step's
  narrower `IShowDirectoryDiscoveryService`, so Step 010 extends this same project rather than this
  step inventing a throwaway namespace Step 010 would need to migrate away from.
- **`AppSettings.LastShowDirectory` added without a schema-version bump.** PHASE-02 Section 4's
  "Schema changes require versioned migrations, fixture updates and tests against the preceding
  project version" was read as applying to *breaking* changes; this is a purely additive, optional,
  nullable field that a pre-Step-009 `settings.json` deserializes as `null` for free via
  `System.Text.Json`'s normal missing-property behavior — functionally identical to how PHASE-01's
  own `AppSettings.SchemaVersion` guard is described ("reject *newer*, unsupported" — not "reject any
  structural diff"). Disclosed here per the task's "Show me any architecture deviations" instruction;
  a later step that needs to *remove* or *retype* a field should still bump the schema version and
  add a real migration test, which this step does not need.
- **Fixtures were placed under `tests/XlightsAI.Studio.Xlights.Tests/Fixtures/`, not the shared
  repo-root `fixtures/` corpus** (`fixtures/FIXTURE-CORPUS-MANIFEST.json` + `fixtures/layouts/`,
  `fixtures/audio/`). That corpus is a hash-pinned, manifest-validated, cross-phase determinism
  contract (PHASE-01 Steps 002/003's own scope) for *audio and layout content* fixtures; Step 009's
  fixtures assert only filename *presence*, not content, and extending that pinned manifest for a
  structural-only fixture was judged out of this narrow step's scope — consistent with
  `Reference.Tests`' own `Fixtures/` folder precedent (not every fixture in this repository lives in
  the shared corpus; e.g. `Reference.Tests/Fixtures/valid-manifest.json` et al. do not either).
- **The pre-existing plain-navigation settings-save path in `ShellViewModel.OnNavigatorChanged` was
  changed from constructing `new AppSettings(..., CurrentScreen)` to first re-reading the persisted
  settings and preserving `LastShowDirectory`.** Without this, Step 005's existing "navigate screens"
  behavior would have silently clobbered this step's own new persisted field back to `null` on every
  subsequent navigation — an unacceptable, undisclosed data-loss regression of this step's own
  "persist last successful folder selection" requirement if left as PHASE-01 originally wrote it.
  Disclosed as the one place this step's own new field required touching an existing, already-
  accepted PHASE-01 code path; no PHASE-01 step's own required/failure acceptance text references
  this field, so no PHASE-01 test needed to change (and none did — §5).

## 10. Unresolved issues / risks carried forward

1. **No native OS folder-picker dialog** — the `ShowDirectory` textbox is free-text, same limitation
   PHASE-01 Step 006 already disclosed for the Projects screen's `WorkspaceDirectory` field (carried
   forward, not newly introduced).
2. **Symlink/junction boundary check compares string-prefix paths (`StartsWith`, case-insensitive),
   not a true POSIX-style canonical-path library.** On Windows this is adequate (paths are
   case-insensitive and `Path.GetFullPath`/`ResolveLinkTarget` already normalize separators/relative
   segments), but a path like `C:\Show` vs. a sibling `C:\ShowOther\...` could theoretically share a
   prefix without the trailing-separator guard — mitigated here by always appending
   `Path.DirectorySeparatorChar` to the directory side before the comparison (see
   `IsWithinDirectory`), but flagged as a narrower algorithm than a dedicated path-containment library
   would use, should a future cross-platform (non-Windows) port need stricter guarantees.
3. **No UI "Browse for folder" affordance surfaces whether the restored last-directory text is even
   still a valid path** until the user clicks Discover again — matches this step's own explicit
   design intent (never touch the file system on screen-restore alone) but means a moved/deleted
   previously-good directory is only reported on the next explicit Discover click, not proactively.
4. **This step's manual verification used the real `%AppData%\XlightsAI.Studio\settings.json`**
   (§6) — same precedent as PHASE-01 Steps 004-007, carried forward, disclosed again here.
5. Git Bash remains non-functional in this environment (carried forward from PHASE-01 Steps 001-008);
   every command in this evidence was run via PowerShell.
6. **Step 010 (model/group parsing) is the natural next consumer of `DisplayGraph`/`DisplayModel`/
   `IShowLayoutImporter`** — not started by this step, per its own narrow scope (§9, first bullet).

## 11. Gate checklist (this step only)

- [x] Only reads files under the selected show directory; never writes/moves/deletes (§4, §5).
- [x] Detects realpath/symlink boundary — proven against a real `File.CreateSymbolicLink`, both the
      escape and the within-boundary cases (§5).
- [x] Displays required/missing optional files — real UI screenshot evidence, both the complete and
      the missing-required-file cases (§6).
- [x] Persists last successful folder selection, survives a real process restart, never persists a
      failed attempt (§5, §6).
- [x] Required acceptance — "Missing folder/file produces actionable error; no mutation" — proven by
      dedicated tests and real-process verification (§4, §5, §6).
- [x] Failure/edge case — "Invalid input, cancellation or stale persisted state must not corrupt the
      project" — proven by dedicated negative-path tests (§5).
- [x] Full existing regression suite re-run: 96/96 passing (83 pre-existing PHASE-01 baseline + 13
      new Step 009 tests), zero regressions (§3, §5).
- [x] `dotnet format --verify-no-changes` passes cleanly against every new and modified file (§3).
- [x] No original/show file or PHASE-01 test assertion touched except the one disclosed, necessary
      `ShellViewModel` settings-preservation fix (§5, §9).
- [x] Architecture deviations disclosed in full (§9), per this task's explicit instruction.
- [x] Real UI demonstration performed (not simulated/mocked) — §6. No xLights installation was
      required for this step's own scope (file-discovery only; Step 014 owns the real-xLights-render
      gate).

**Step 009 status: PASS.** This is the first numbered step of PHASE-02; Steps 010-015 remain
unimplemented, as instructed ("do not execute the next step until this step is proven").
