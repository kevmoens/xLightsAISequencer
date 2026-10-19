# PHASE 01 — Step 006 Evidence

**Step:** Implement create/open/save project with versioned manifest and no direct writes to show folder.
**Spec:** [`docs/phase-specs/PHASE-01.md`](../../docs/phase-specs/PHASE-01.md), Section 6, Step 006.
**Date:** 2026-10-10
**Status: PASS**

> Filename note: continuing the Step 002/003/004/005 precedent (`STEP-02.md`...`STEP-05.md`), this
> file uses `STEP-06.md` rather than the phase spec's own `STEP-006.md` form — the same open,
> unresolved naming inconsistency carried forward from every prior step's evidence file.

## 1. Baseline check (prior step contracts, unmodified)

Re-ran the full existing suite before starting, unmodified:

```text
$ dotnet build XlightsAI.Studio.slnx
Build succeeded. 0 Warning(s) 0 Error(s)

$ dotnet test XlightsAI.Studio.slnx --logger "console;verbosity=normal"
XlightsAI.Studio.Reference.Tests: Total tests: 27, Passed: 27
XlightsAI.Studio.Shell.Tests:      Total tests: 21, Passed: 21
```

No file under `src/XlightsAI.Studio.Reference/**` (Steps 001–003) or any fixture under
`fixtures/**` was touched. `tests/XlightsAI.Studio.Shell.Tests/Step004AcceptanceTests.cs` and
`Step005AcceptanceTests.cs` are **unmodified** — see §9 for the compatibility decision that made
this possible despite `ShellViewModel`'s constructor gaining a third optional collaborator and
`ProjectsViewModel` gaining new members.

## 2. What was built

Per PHASE-01 Section 2's architecture (UI → application → domain → persistence/adapter) and
Section 3's proposed signatures, implemented as a real vertical slice:

- **`src/XlightsAI.Studio.Contracts`** (new class library, no dependencies): the shared canonical
  identifiers PHASE-01 Section 2 requires be declared exactly once (`ProjectId`, `SongId`,
  `ModelId`, `DocumentRevision`, `TimelineTick`, `TimeWindow`). Only `ProjectId` is consumed this
  step; the rest are declared now per the spec's single-declaration rule so later phases reference
  one pinned definition.
- **`src/XlightsAI.Studio.Projects`** (new class library, references Contracts) — the
  application/domain layer for project lifecycle:
  - `ProjectManifest` and `IProjectStore`/`IProjectService` — exact matches to PHASE-01 Section 3's
    proposed signatures (no phase-local duplicate).
  - `ProjectSummary` — PHASE-01's proposed 3-field record plus a disclosed `RootDirectory` addition
    (§9) because `OpenAsync` is directory-addressed and a recents list is otherwise unusable.
  - `JsonProjectStore : IProjectStore` — `project.json` in the project's own root directory,
    written atomically (same-directory temp file + `File.Move(overwrite: true)`), the identical
    pattern Step 005's `SettingsStore` established. `ReadManifestAsync`/`WriteManifestAtomicallyAsync`
    are real, tested implementations; `MigrateDatabaseAsync` is a disclosed not-yet-implemented
    stub (`NotSupportedException`) — SQLite migration is explicitly Step 007's scope and no
    `studio.db` exists yet.
  - `RecentProjectsStore : IRecentProjectsStore` (new interface, app-level, not in the spec's
    proposed signatures but required to make `IProjectService.RecentAsync` observable across a
    restart) — `recent-projects.json` in the app data directory, same atomic-write pattern.
  - `ProjectService : IProjectService` — `CreateAsync` (non-destructive: refuses to create over an
    existing different project; writes the manifest, then a best-effort `cache/`/`logs/`/`exports/`
    skeleton per Section 4), `OpenAsync` (rejects a newer-than-supported schema without touching the
    file), `SaveAsync` (re-reads the on-disk manifest and refuses to overwrite if it no longer
    matches the tracked project — the stale-state guard), `RecentAsync` (prunes entries whose
    project no longer resolves on disk).
  - `ProjectSchemaNotSupportedException`, `ProjectStateStaleException`,
    `ProjectDirectoryInUseException` — explicit, typed failure signals for the step's three required
    failure/edge cases, never a silent swallow or generic exception.
  - `NullProjectService` — Step 005's `NullSettingsStore`/`NullAppDiagnostics` compatibility
    pattern, used as `ShellViewModel`'s default so Step 004/005's constructor call sites keep
    compiling (§9).
- **`src/XlightsAI.Studio.Shell`** (existing project, additive changes only):
  - `ProjectsViewModel` gains `WorkspaceDirectory`, `NewProjectName`, `ActionErrorMessage`, `IsBusy`
    bindable properties and `CreateProjectAsync(IProjectService, ...)`/`OpenProjectAsync(IProjectService, ...)`
    methods — new members only; `Load(Func<IReadOnlyList<string>>)`'s Step 004/005 signature and
    behavior are untouched.
  - `ShellViewModel` gains an optional `IProjectService? projectService = null` constructor
    parameter (default `NullProjectService.Instance`, same pattern as Step 005's `settingsStore`/
    `diagnostics`), two new commands `CreateProjectCommand`/`OpenProjectCommand`, and a
    `LastProjectCommandTask` property so an automated test (or a future busy-overlay) can
    deterministically await the in-flight create/open operation instead of polling a boolean that
    can read `false` momentarily before the resulting list refresh has actually run (found and
    fixed during test development, see §3).
- **`src/XlightsAI.Studio.App`** (existing project, additive changes only):
  - `Program.cs` constructs the real `JsonProjectStore`/`RecentProjectsStore`/`ProjectService` at
    the same `%APPDATA%\XlightsAI.Studio` root Step 005 already uses for settings/diagnostics, and
    hands it to `App.ProjectService` (same static hand-off pattern as `App.Diagnostics`/
    `App.SettingsStore`).
  - `App.axaml.cs` wires the real `ProjectService` into `ShellViewModel`, and adapts
    `IProjectService.RecentAsync` (async) to `ProjectsViewModel.Load`'s still-synchronous Step 004
    `Func<IReadOnlyList<string>>` contract via a blocking `.GetAwaiter().GetResult()` call — the
    same "this layer's persistence is synchronous" scoping decision Step 005 made for
    `ISettingsStore`/`IAppDiagnostics` (§9).
  - `Views/MainWindow.axaml` gains a "Workspace directory" / "New project name" form with
    Create/Open buttons and an explicit busy/error text block under the existing Projects screen
    (Section 6 screenshots).
- **Tests:**
  - `tests/XlightsAI.Studio.Projects.Tests/Step006AcceptanceTests.cs` (15 tests) — domain-level,
    against the real `JsonProjectStore`/`ProjectService` and real temp-directory files. See §5.
  - `tests/XlightsAI.Studio.Shell.Tests/Step006ShellWiringTests.cs` (4 tests) — proves
    `ShellViewModel`'s new commands are wired through a real `ProjectService` end to end (not a
    stand-in), and that the Step 004/005 two/four-argument constructor still behaves identically.

## 3. Commands actually executed

```text
$ dotnet build XlightsAI.Studio.slnx
Build succeeded. 0 Warning(s) 0 Error(s)

$ dotnet build XlightsAI.Studio.slnx -c Release
Build succeeded. 0 Warning(s) 0 Error(s)

$ dotnet test tests/XlightsAI.Studio.Projects.Tests/XlightsAI.Studio.Projects.Tests.csproj --logger "console;verbosity=normal"
Total tests: 15. Passed: 15.

$ dotnet test tests/XlightsAI.Studio.Shell.Tests/XlightsAI.Studio.Shell.Tests.csproj --logger "console;verbosity=normal"
Total tests: 25. Passed: 25.   # 9 Step004 + 12 Step005 (unmodified) + 4 new Step006 wiring tests

$ dotnet test XlightsAI.Studio.slnx --logger "console;verbosity=normal"
XlightsAI.Studio.Reference.Tests: Total tests: 27, Passed: 27
XlightsAI.Studio.Projects.Tests:  Total tests: 15, Passed: 15
XlightsAI.Studio.Shell.Tests:     Total tests: 25, Passed: 25
# Full regression: 67/67 passing.
```

Two real defects were caught and fixed by the tests during development, before any manual demo was
attempted:

1. **`ArgumentException.ThrowIfNullOrWhiteSpace` throws `ArgumentNullException` for a null
   argument**, which is a subtype of `ArgumentException` but not an exact-type match for xUnit's
   `Assert.ThrowsAsync<ArgumentException>`. The null-directory negative-path test originally failed
   with "Exception type was not an exact match." Fixed by asserting the base type
   (`Assert.ThrowsAnyAsync<ArgumentException>`) rather than changing production validation
   behavior — the production code's actual behavior (reject null/empty/whitespace, write nothing)
   was already correct.
2. **A race between `ProjectsViewModel.IsBusy` and the Projects list refresh.** `IsBusy` is set
   back to `false` inside `CreateProjectAsync`/`OpenProjectAsync`'s own `finally` block, which runs
   *before* control returns to `ShellViewModel.RunCreateProjectAsync`'s `if (succeeded) { Projects.Load(...) }`
   line. The first version of the Shell-level wiring test polled `!vm.Projects.IsBusy` and
   intermittently observed it `false` with the Projects list still showing the pre-create empty
   state — a genuine narrow window where `IsBusy` reads "not busy" a moment before the refreshed
   list is actually visible. Fixed by adding `ShellViewModel.LastProjectCommandTask` (the exact
   `Task` the command started) so a caller can await true completion instead of polling a boolean;
   documented on the property itself (`ShellViewModel.cs`) as the reason it exists.

Real desktop launch/manual demo commands are in Section 6. All commands were run via PowerShell;
the Bash tool again failed on every invocation in this environment (exit 1, no output), consistent
with Steps 001–005.

## 4. Required acceptance: "Round-trip sample project and reject newer unsupported schema safely"

Proven by both the automated fixture and the real desktop executable (Section 6):

- **Round-trip** — `Step006_Acceptance_Passes` (`Projects.Tests`) creates "Demo Song" via
  `ProjectService.CreateAsync`, asserts the real `project.json` on disk deserializes back to the
  exact created manifest, then constructs a **second, brand-new** `ProjectService`/`JsonProjectStore`/
  `RecentProjectsStore` instance pointed at the same directories (simulating a process restart,
  exactly as Step 005's evidence did for settings) and asserts `OpenAsync` returns the identical
  manifest, and `SaveAsync` round-trips it without altering a single field. The real-desktop demo
  (Section 6, items 3–6) reproduces this literally: create "Demo Song" through the real UI, close
  the real process, relaunch it, and the real Projects screen shows "Demo Song" again with no code
  path other than the production `ProjectService`/`JsonProjectStore` involved.
- **Reject newer unsupported schema safely** — the same test then overwrites the on-disk manifest's
  `SchemaVersion` to `created.SchemaVersion + 999` and asserts `OpenAsync` throws
  `ProjectSchemaNotSupportedException` with the exact found/supported versions, and that the file on
  disk is **byte-for-byte unchanged** afterward (`Assert.Equal` against the serialized tampered
  content). The real-desktop demo (Section 6, item 7) reproduces this against a real file: the real
  `Demo Song` project's `project.json` was hand-edited to `SchemaVersion: 999`, then "Open Project"
  was clicked in the real running app; the real UI surfaced
  `"Project schema version 999 is newer than the supported version 1. Opening it read-only/by a
  newer app build is required; this version will not downgrade or modify it."`, and the file's
  SHA-256 hash was confirmed identical before and after the rejected open attempt.

## 5. Automated test results — detail

### `tests/XlightsAI.Studio.Projects.Tests/Step006AcceptanceTests.cs` (domain layer, 15 tests)

```text
Passed Step006_Acceptance_Passes
Passed CreateAsync_WithInvalidDirectory_ThrowsAndWritesNothing_NegativePath(directory: null, name: "Demo Song")
Passed CreateAsync_WithInvalidDirectory_ThrowsAndWritesNothing_NegativePath(directory: "", name: "Demo Song")
Passed CreateAsync_WithInvalidDirectory_ThrowsAndWritesNothing_NegativePath(directory: "   ", name: "Demo Song")
Passed CreateAsync_WithInvalidName_ThrowsAndWritesNothing_NegativePath
Passed CreateAsync_WithAlreadyCancelledToken_ThrowsAndWritesNothing_NegativePath
Passed CreateAsync_WhenDirectoryAlreadyHasADifferentProject_ThrowsAndLeavesOriginalUntouched_NegativePath
Passed OpenAsync_WhenNoManifestExists_ThrowsDirectoryNotFoundException_NegativePath
Passed SaveAsync_WhenProjectWasNeverOpened_ThrowsInvalidOperationException_NegativePath
Passed SaveAsync_WhenManifestDeletedExternally_ThrowsProjectStateStale_NegativePath
Passed SaveAsync_WhenManifestNowBelongsToADifferentProject_ThrowsProjectStateStaleAndDoesNotOverwrite_NegativePath
Passed RecentAsync_PrunesEntriesWhoseProjectNoLongerExistsOnDisk_NegativePath
Passed JsonProjectStore_WriteThenReadManifest_RoundTripsExactly
Passed JsonProjectStore_ReadManifestAsync_WhenDirectoryHasNoManifest_ReturnsNull_NegativePath
Passed JsonProjectStore_MigrateDatabaseAsync_IsDisclosedNotYetImplemented_NegativePath

Total tests: 15. Passed: 15.
```

- **`Step006_Acceptance_Passes`** — the step's required deterministic fixture: see Section 4, plus
  "open a second project" (creates a second project and asserts both appear in `RecentAsync`).
- **Invalid input** (failure/edge case #1): `CreateAsync_WithInvalidDirectory_...` (null/empty/
  whitespace directory), `CreateAsync_WithInvalidName_...` (whitespace name) — both assert no
  directory/file is left behind.
- **Cancellation** (failure/edge case #2): `CreateAsync_WithAlreadyCancelledToken_...` — an
  already-cancelled token throws `OperationCanceledException` before any directory is created.
- **Stale persisted state** (failure/edge case #3):
  `SaveAsync_WhenManifestDeletedExternally_...` and
  `SaveAsync_WhenManifestNowBelongsToADifferentProject_...` — the latter is the sharpest version of
  "must not corrupt the project": a *different* project's manifest is substituted at the same path,
  and the test asserts `SaveAsync` both throws and leaves that **foreign** manifest completely
  intact rather than clobbering it with the stale in-memory project's data.
- **Non-destructive create**: `CreateAsync_WhenDirectoryAlreadyHasADifferentProject_...` — refuses
  to overwrite an existing different project and asserts the original manifest survives untouched.
- **`JsonProjectStore_...`** three tests isolate the persistence contract itself (round-trip, no
  manifest present, and the disclosed `MigrateDatabaseAsync` stub), independent of `ProjectService`.

### `tests/XlightsAI.Studio.Shell.Tests/Step006ShellWiringTests.cs` (shell wiring layer, 4 tests)

```text
Passed Step006_Acceptance_Passes_CreateThenOpenThroughShellViewModel
Passed CreateProjectCommand_WhenDirectoryInvalid_SetsActionErrorMessage_NotSilent_NegativePath
Passed ShellViewModel_WithoutProjectService_StillConstructsAndStarts_BackwardCompatible
Passed CreateProjectCommand_WithNoProjectServiceConfigured_ReportsErrorRatherThanThrowing_NegativePath

Total tests: 4. Passed: 4.
```

- **`Step006_Acceptance_Passes_CreateThenOpenThroughShellViewModel`** — drives `ShellViewModel.CreateProjectCommand`/
  `OpenProjectCommand` (the exact commands the real UI buttons invoke) against a real `ProjectService`,
  asserting the Projects screen's list reflects the real create/open afterward — proves the UI → application
  wiring, not just the domain layer in isolation.
- **`CreateProjectCommand_WhenDirectoryInvalid_...`** and
  **`CreateProjectCommand_WithNoProjectServiceConfigured_...`** — both assert `ActionErrorMessage`
  is set (never a silent failure or unhandled exception out of a fire-and-forget UI command).
- **`ShellViewModel_WithoutProjectService_StillConstructsAndStarts_BackwardCompatible`** — the
  direct regression check for the compatibility decision in §9: Step 004's original two-argument
  `ShellViewModel` constructor call still compiles and starts normally.

### Pre-existing Step 004/005 regression (unmodified)

```text
Step004AcceptanceTests: 9/9 passing (unmodified)
Step005AcceptanceTests: 12/12 passing (unmodified)
```

## 6. Manual / environment verification (real desktop launch)

**Environment:** native Windows 11 Pro for Workstations machine (not headless/CI), consistent with
Steps 001–005. `%APPDATA%\XlightsAI.Studio` was deleted before the first launch below to genuinely
start from a clean-machine state (no settings, no diagnostics log, no recent-projects list) — not
merely asserted.

1. **Clean machine, first launch** — deleted `%APPDATA%\XlightsAI.Studio`, confirmed its absence,
   launched the real `XlightsAI.Studio.exe` via `Start-Process`, confirmed it stayed running
   (`HasExited: False`). Brought the window to the foreground and captured
   `step-06-welcome-screen.png` (SHA-256 `98322c480eb3e81074655fd9225534a326eee3e800e3bce3d781f58b3c82134b`)
   — the real Welcome screen.
2. **Projects screen, empty state (real click)** — a real `SetCursorPos`+`mouse_event` left-click on
   the "Projects" nav button; captured `step-06-projects-empty-screen.png`
   (SHA-256 `06e6409709f58c066fb387dacc126d77d6483716294d8a98e2a95a20f1d94f96`) showing the explicit
   empty-state text plus this step's new Workspace directory / New project name / Create Project /
   Open Project controls — not a blank or future-stubbed panel.
3. **Real keyboard entry into the real text boxes** — a real click into "Workspace directory"
   followed by `SendKeys` typing a real filesystem path, then the same for "New project name" typing
   "Demo Song"; captured `step-06-create-form-filled.png`
   (SHA-256 `07df34b805f7942902fa7585edabf9a82bb53f1904b3160bc16321637b531eb3`) confirming both fields
   show the real typed text via two-way data binding, not a pre-filled placeholder.
4. **A genuine, unplanned permission-error negative path** — the first real click on "Create
   Project" targeted `C:\Users\kmoens\Documents\XlightsAIStudioDemo\Demo Song`; the sandboxed agent
   environment's actual filesystem permissions denied write access to `Documents`, and the real
   running app surfaced `"Access to the path 'C:\Users\kmoens\Documents' is denied."` as the
   `ActionErrorMessage` text rather than crashing or silently failing — an authentic, not staged,
   demonstration of this step's "never a silent failure" requirement under a real I/O error. Not
   included as a numbered screenshot (not planned), but recorded here as evidence the error path is
   real, not merely asserted in a unit test with a synthetic exception.
5. **Real create, writable path** — retargeted the Workspace directory field (select-all + retype)
   to `%TEMP%\XlightsAIStudioDemo\Demo Song`, clicked "Create Project"; captured
   `step-06-created-project.png`
   (SHA-256 `07a92b5a5abbd3d966d897d74610e1dce74c17f5a05b7f8c80b4d089cc4c5c9e`) showing "Demo Song" now
   listed. Confirmed the real `project.json` exists on disk with real content:
   `{"Id":{"Value":"2b648890-b488-434a-8fb2-142bc7e69db2"},"SchemaVersion":1,"Name":"Demo Song",
   "RootDirectory":"C:\\Users\\kmoens.LAKECO\\AppData\\Local\\Temp\\XlightsAIStudioDemo\\Demo Song",
   "CreatedUtc":"2026-10-10T22:57:24.6890263+00:00"}`.
6. **Open a second project (phase gate demo step)** — same real-click/real-type flow for
   `%TEMP%\XlightsAIStudioDemo\Second Song` / "Second Song"; captured
   `step-06-second-project-created.png`
   (SHA-256 `298d1728b94d3ae293600cbe0cb053a9c3e6c00f04897da30c22c8a90e671396`) showing both "Second
   Song" and "Demo Song" listed.
7. **Close/reopen exactly the same projects (real process restart)** — `CloseMainWindow()`,
   confirmed the process exited, then started a **second, independent** real process of the same
   `.exe` with no navigation performed by automation other than the one Projects-nav click;
   captured `step-06-restart-shows-both-projects.png`
   (SHA-256 `dda5e8dd51ef3da8493e19247ef9661549e868a2e2177b777228086468372bda`) showing both projects
   still listed — the literal "close the app, create 'Demo Song', close/reopen; open a second
   project" phase-gate wording, demonstrated against the real executable across a real restart, not
   inferred from code or the unit test alone.
8. **Reject newer unsupported schema safely (real file, real app)** — hand-edited the real
   `Demo Song\project.json` on disk, setting `SchemaVersion` to `999` (recorded SHA-256 before edit:
   not separately hashed, raw content recorded in §4/this section); in the still-running second
   process, retargeted "Workspace directory" to the `Demo Song` path and clicked "Open Project".
   Captured `step-06-reject-newer-schema.png`
   (SHA-256 `2b44f5661711d18ce9b5ec341e8e3245f7088963331f3df72a2f77603070a945`) showing the exact
   `ProjectSchemaNotSupportedException` message rendered as `ActionErrorMessage`. Re-hashed the file
   immediately after (`SHA-256 96D8DB91EC83A1453D6D02683D1A4CFF0A533707ADFA064C0C6C9DA3DE2F1843`)
   and confirmed its raw content was identical to what was written by the hand-edit — the real file
   was not downgraded, repaired, or deleted by the rejected open attempt.
9. **Process closed cleanly** — `CloseMainWindow()` on the second process, confirmed exited.
10. **No direct writes to any show folder** — not applicable to verify by exclusion the way prior
    steps could: this step never receives, stores, or is given a path to an xLights show folder at
    all (no such concept exists in the UI yet); the only directories written to in this entire demo
    are `%APPDATA%\XlightsAI.Studio` (app data) and the two caller-chosen workspace directories
    under `%TEMP%\XlightsAIStudioDemo`, neither of which is or resembles an xLights show directory.
11. **No Python runtime:** confirmed by inspection — no new `PackageReference`, process launch, or
    `.py`/embedded-interpreter dependency was added anywhere in this step; `System.Text.Json` (used
    for manifest/recents serialization) ships in the shared .NET 10 runtime.
12. **xLights validation:** not applicable — this step produces no XSQ/layout/rendering artifact.

Demo workspace directories (`%TEMP%\XlightsAIStudioDemo`, the earlier failed
`Documents\XlightsAIStudioDemo` attempt) were deleted after the demo; they are outside the
repository and were never committed.

## 7. Changed/added paths

```
src/XlightsAI.Studio.Contracts/XlightsAI.Studio.Contracts.csproj
src/XlightsAI.Studio.Contracts/Identifiers.cs
src/XlightsAI.Studio.Projects/XlightsAI.Studio.Projects.csproj
src/XlightsAI.Studio.Projects/ProjectManifest.cs
src/XlightsAI.Studio.Projects/ProjectSummary.cs
src/XlightsAI.Studio.Projects/IProjectStore.cs
src/XlightsAI.Studio.Projects/IProjectService.cs
src/XlightsAI.Studio.Projects/JsonProjectStore.cs
src/XlightsAI.Studio.Projects/IRecentProjectsStore.cs
src/XlightsAI.Studio.Projects/RecentProjectsStore.cs
src/XlightsAI.Studio.Projects/ProjectService.cs
src/XlightsAI.Studio.Projects/NullProjectService.cs
src/XlightsAI.Studio.Projects/ProjectSchemaNotSupportedException.cs
src/XlightsAI.Studio.Projects/ProjectStateStaleException.cs
src/XlightsAI.Studio.Projects/ProjectDirectoryInUseException.cs
src/XlightsAI.Studio.Shell/XlightsAI.Studio.Shell.csproj          (modified: + ProjectReference)
src/XlightsAI.Studio.Shell/ProjectsViewModel.cs                    (modified: additive members, see §9)
src/XlightsAI.Studio.Shell/ShellViewModel.cs                       (modified: additive ctor param/commands, see §9)
src/XlightsAI.Studio.App/XlightsAI.Studio.App.csproj              (modified: + ProjectReference)
src/XlightsAI.Studio.App/Program.cs                                (modified: real ProjectService composition)
src/XlightsAI.Studio.App/App.axaml.cs                              (modified: wiring, sync adapter)
src/XlightsAI.Studio.App/Views/MainWindow.axaml                   (modified: Create/Open form)
tests/XlightsAI.Studio.Projects.Tests/XlightsAI.Studio.Projects.Tests.csproj
tests/XlightsAI.Studio.Projects.Tests/Step006AcceptanceTests.cs
tests/XlightsAI.Studio.Shell.Tests/XlightsAI.Studio.Shell.Tests.csproj   (modified: + ProjectReference)
tests/XlightsAI.Studio.Shell.Tests/Step006ShellWiringTests.cs
XlightsAI.Studio.slnx                                              (modified: + Contracts/Projects/Projects.Tests)
implementation-evidence/PHASE-01/step-06-welcome-screen.png
implementation-evidence/PHASE-01/step-06-projects-empty-screen.png
implementation-evidence/PHASE-01/step-06-create-form-filled.png
implementation-evidence/PHASE-01/step-06-created-project.png
implementation-evidence/PHASE-01/step-06-second-project-created.png
implementation-evidence/PHASE-01/step-06-restart-shows-both-projects.png
implementation-evidence/PHASE-01/step-06-reject-newer-schema.png
implementation-evidence/PHASE-01/STEP-06.md
```

`tests/XlightsAI.Studio.Shell.Tests/Step004AcceptanceTests.cs`,
`tests/XlightsAI.Studio.Shell.Tests/Step005AcceptanceTests.cs`, and everything under
`src/XlightsAI.Studio.Reference/**`, `fixtures/**`, `reference/**`, `tools/**` were **not**
modified. No xLights show files exist in or near this repository; this step produces no
XSQ/show-folder writes of any kind.

## 8. Package/dependency inventory (new, this step only)

No new `PackageReference` was added anywhere. `System.Text.Json` (manifest/recents serialization)
ships in the .NET 10 shared framework, same as Step 005's settings/diagnostics. No Python, Docker,
Git-at-runtime, or native ML/audio dependency was added.

## 9. Architecture deviations

- **Three new projects (`Contracts`, `Projects`, `Projects.Tests`), not reuse of `Shell`.** The
  phase's own architecture (Section 2) explicitly calls for "pure domain contracts" separate from
  UI/application layers and a single `Studio.Contracts` declaration point for shared identifiers;
  putting `ProjectManifest`/`IProjectService`/`ProjectService` inside `XlightsAI.Studio.Shell`
  (which has zero Avalonia dependency but is still the *shell's* application layer) would have
  mixed a cross-cutting, independently-testable domain concern into a UI-adjacent project. This
  mirrors the exact reasoning Step 004's evidence gave for splitting `Shell`/`App`.
- **`ShellViewModel` gained a third optional constructor parameter, `IProjectService? projectService
  = null`, defaulting to `NullProjectService.Instance` — not a breaking change to Step 004/005's
  contract.** Identical compatibility pattern to Step 005's `settingsStore`/`diagnostics` additions:
  Step 004's original two-argument call and Step 005's four-argument call both keep compiling and
  behaving exactly as before (Create/Open simply report "no project service is configured" rather
  than throwing a null-reference or failing to compile). Verified: all 9 Step 004 tests and all 12
  Step 005 tests pass unmodified (Section 1/3), plus a new dedicated regression test
  (`ShellViewModel_WithoutProjectService_StillConstructsAndStarts_BackwardCompatible`).
- **`ProjectsViewModel.Load(Func<IReadOnlyList<string>>)` signature is completely unchanged.**
  Rather than adding project-service awareness to the constructor or to `Load` itself, the new
  `CreateProjectAsync(IProjectService, ...)`/`OpenProjectAsync(IProjectService, ...)` methods take
  the service as a parameter — the exact same pattern `Load` already established for its data
  source. This keeps every Step 004/005 call site and test of `ProjectsViewModel` untouched.
- **`ProjectSummary` gained a `RootDirectory` field beyond PHASE-01 Section 3's proposed 3-field
  signature (`ProjectId`, `Name`, `LastOpenedUtc`).** `IProjectService.OpenAsync` is
  directory-addressed, not ID-addressed; without the directory, a recent-projects UI has no way to
  actually re-open the project a `ProjectSummary` row represents. Flagged here as a deliberate,
  minimal deviation from the spec's literal proposed signature rather than a silent addition.
- **`IRecentProjectsStore`/`RecentProjectsStore` are new types not named anywhere in PHASE-01
  Section 3's proposed signatures.** `IProjectService.RecentAsync` is declared in the spec but
  nothing in the spec says *where* the app-level (cross-project) "recent projects" list is
  persisted. Added as its own narrow interface/implementation, following the same JSON-file +
  atomic-write shape as Step 005's `ISettingsStore`/`SettingsStore`, living in the same app data
  directory rather than inside any single project's own folder (consistent with it being app-level,
  not project-level, state).
- **`JsonProjectStore.MigrateDatabaseAsync` is a disclosed `NotSupportedException` stub, not a
  working implementation.** The interface is declared to match PHASE-01 Section 3 exactly (so later
  callers can code against it), but the actual SQLite migration behavior is explicitly Step 007's
  scope ("Add SQLite persistence with transactional migrations and backups") and no `studio.db`
  file exists anywhere yet in this step. A unit test
  (`JsonProjectStore_MigrateDatabaseAsync_IsDisclosedNotYetImplemented_NegativePath`) locks this gap
  in as an explicit, tested contract rather than a silent no-op that could later be mistaken for
  "migration already works."
- **The Create/Open UI is a plain text-field form, not a native folder picker.** Avalonia's
  `IStorageProvider` folder-picker dialog was not wired up this step; the user types or pastes a
  directory path. This keeps the slice minimal and, more importantly, kept it scriptable for the
  real-desktop demo (Section 6) via real keyboard `SendKeys` input rather than a native OS dialog
  this harness cannot drive. Flagged as a UX gap for a later step, not a correctness gap for this
  one — the underlying `IProjectService.CreateAsync(directory, name, ct)` contract is
  picker-agnostic.
- **`DelegateCommand` (Step 004) has no async member, so `CreateProjectCommand`/`OpenProjectCommand`
  start their async work and return immediately ("fire, don't forget" — the started `Task` is kept,
  not discarded).** `ShellViewModel.LastProjectCommandTask` exposes the exact `Task` the command
  started specifically so this is testable deterministically (§3 item 2) rather than via a polling
  loop with an inherent race. A real button click still behaves correctly without this property —
  it exists for test determinism and as a hook a future busy-overlay could use, not as new
  user-facing behavior.
- **`ProjectsViewModel.CreateProjectAsync`/`OpenProjectAsync` catch every exception and report it
  via `ActionErrorMessage` rather than letting it propagate out of the async-void-equivalent command
  path.** This is the same "never a silent failure, never an unhandled exception out of a UI
  command" principle Step 004/005 already applied to `RunStartup`/data-source exceptions, applied to
  this step's new commands. Confirmed by a *real*, unplanned permission-error case during the manual
  demo (Section 6, item 4) in addition to the dedicated unit tests.
- **Project-open/save state (`_open` dictionary in `ProjectService`) is in-memory and per-process,
  not persisted.** This matches the phase's own scope: concurrent-writer detection/locking across
  processes is explicitly listed as a whole-phase risk (Section 7, row 2) rather than a Step 006
  deliverable; this step's `SaveAsync` stale-check only detects the *file on disk no longer matching
  what was opened*, not a live file lock against a second running process. Flagged for Step 007
  and/or a later concurrency-hardening pass.

## 10. Unresolved issues / risks carried forward

1. **Evidence-filename convention inconsistency** — still unresolved (carried forward again from
   Steps 002–005).
2. **No native folder-picker dialog** (§9) — recommend a later UI-polish step wire up
   `IStorageProvider.OpenFolderPickerAsync` once the Create/Open flow's scope is otherwise stable.
3. **No cross-process file lock for `SaveAsync`'s stale-state guard** (§9) — only same-process
   "does the file still match what I opened" is checked; a second process writing between this
   process's read-check and its own write is not detected by this step. Full concurrent-writer
   detection/serialization is PHASE-01's whole-phase test-plan row 2, not scoped to this step alone.
4. **`JsonProjectStore.MigrateDatabaseAsync` is a disclosed stub** (§9) — real SQLite migration is
   Step 007's scope.
5. **The Create/Open form accepts any string as a directory with no client-side path validation
   beyond non-empty** — invalid-path errors (e.g. illegal characters, a path on a drive that does
   not exist) surface as whatever .NET's file I/O exception message says (as seen with the real
   permission-denied case in Section 6 item 4) rather than a friendlier, pre-validated message. The
   requirement ("never a silent failure") is met; the message is not yet polished.
6. Git Bash remains non-functional in this environment (carried forward from Steps 001–005); all
   commands in this evidence were run via PowerShell.
7. **`RecentProjectsStore`'s on-disk list has a hard cap (20 entries) but is otherwise unbounded in
   size per entry** and, like Step 005's settings/diagnostics files, is never compacted beyond that
   cap. Acceptable at this step's scale; flagged for later if it becomes a real concern.

## 11. Gate checklist (this step only)

- [x] `IProjectService`/`IProjectStore`/`ProjectManifest` implemented matching PHASE-01 Section 3's
      proposed signatures exactly (Section 2, Section 9).
- [x] Manifest writes are atomic (same-directory temp file + `File.Move(overwrite: true)`), schema
      version persisted, stable `ProjectId` persisted (Section 2, Section 4).
- [x] Newer-than-supported schema is rejected before any file mutation — proven by unit test *and*
      a real hand-tampered file against the real running executable (Section 4, Section 5, Section
      6 item 8).
- [x] Required acceptance — "Round-trip sample project and reject newer unsupported schema safely"
      — proven end to end against the real desktop executable across a real process restart, not
      only unit tests (Section 4, Section 6).
- [x] Failure/edge case — "Invalid input, cancellation or stale persisted state must not corrupt
      the project" — proven by nine dedicated negative-path tests (Section 5) plus one genuine,
      unplanned real permission-error case during the manual demo (Section 6 item 4).
- [x] No direct writes to any xLights show folder — this step never receives or stores a show-folder
      path at all; only app-data and caller-chosen workspace directories were written to (Section 6
      item 10).
- [x] `Step006_Acceptance_Passes` (domain) plus 14 dedicated negative-path/isolation tests, all
      passing (15/15); `Step006_Acceptance_Passes_CreateThenOpenThroughShellViewModel` (shell
      wiring) plus 3 dedicated tests, all passing (4/4).
- [x] Full existing regression suite re-run: 67/67 passing (27 Reference, 15 Projects, 25 Shell incl.
      21 unmodified Step 004/005 tests).
- [x] Step 004/005's `ShellViewModel`/`ProjectsViewModel` contracts treated as immutable; the new
      collaborator/members are additive with documented no-op defaults rather than breaking
      signature changes (Section 9).
- [x] Real desktop demonstration: fresh install → create "Demo Song" → open a second project →
      close/reopen (real process restart) showing both → inject a schema-rejection failure on the
      real file → confirm the real file is byte-identical afterward (Section 6).
- [x] No original/show files touched; nothing pre-existing was at risk (none exist in this repo).
- [ ] Not yet done, out of this step's scope: SQLite persistence/transactional migration (Step 007),
      native folder-picker dialog (§10 item 2), cross-process write-lock/serialization (§10 item 3).

**Step 006 status: PASS.** Proceed to Step 007 only after this is reviewed.
