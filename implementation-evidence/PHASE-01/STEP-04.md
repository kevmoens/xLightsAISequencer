# PHASE 01 — Step 004 Evidence

**Step:** Create .NET 10 Avalonia solution with basic navigation and no Python runtime.
**Spec:** [`docs/phase-specs/PHASE-01.md`](../../docs/phase-specs/PHASE-01.md), Section 6, Step 004.
**Date:** 2026-10-10
**Status: PASS**

> Filename note: continuing the Step 002/003 precedent (`STEP-02.md`, `STEP-03.md`), this file
> uses `STEP-04.md` rather than the phase spec's own `STEP-004.md` form — still an open,
> unresolved naming inconsistency (carried forward again, see Step 003 §10 item 1).

## 1. Baseline check (prior step contracts, unmodified)

Re-ran the full existing suite before starting, unmodified:

```text
$ dotnet build XlightsAI.Studio.slnx
Build succeeded. 0 Warning(s) 0 Error(s)

$ dotnet test XlightsAI.Studio.slnx --logger "console;verbosity=detailed"
Total tests: 27, Passed: 27, Failed: 0
```

No file under `src/XlightsAI.Studio.Reference/**` (Steps 001–003) or any fixture under `fixtures/`
was modified by this step. Only new files were added, in two new projects (`XlightsAI.Studio.Shell`,
`XlightsAI.Studio.App`) plus one new test project, alongside the existing ones.

## 2. What was built

A real, three-layer vertical slice for the Avalonia shell, split so the navigation/state-machine
logic is Avalonia-free and directly unit-testable (per the architecture's "UI -> application ->
domain" layering) and the Avalonia project stays a thin binding layer:

- **`src/XlightsAI.Studio.Shell`** (new class library, no Avalonia reference at all) —
  the application/domain layer for this step:
  - `StartupState` (`Starting` / `Ready` / `Failed`) and `StartupFailure` (code + user-readable
    message) — the step's own narrower startup state machine. The full project-workspace machine
    from PHASE-01 §3 (`NeedsWorkspace`/`Migrating`/`IncompatibleVersion`) is explicitly *not*
    modeled here — it belongs to Steps 006/007's `IProjectService`/`IProjectStore`, which don't
    exist yet. Documented as a scoping decision in `StartupState.cs`'s own doc comment.
  - `ShellScreen` (`Welcome` / `Projects`) and `IShellNavigator`/`ShellNavigator` — an explicit
    state machine: `NavigateTo` throws `InvalidOperationException` (not a silent no-op) unless
    `State == Ready`; `CompleteStartup`/`FailStartup` are each one-shot and throw if called from
    anything but `Starting`.
  - `ProjectsViewModel` — **the one project feature** this step ships (per the step's
    implementation guidance: "basic navigation and ... one project feature"). It has an explicit
    `ProjectsLoadState` (`Loading`/`Empty`/`Error`/`Loaded`) driven by a pluggable
    `Func<IReadOnlyList<string>>` data source; a thrown exception from that source becomes an
    `Error` state with the real exception message, never a blank/silent screen. It does **not**
    implement real project create/open/persist (that is Step 006's `IProjectService` contract) —
    it proves the navigation/display slice with a stand-in source.
  - `WelcomeViewModel`, `ShellViewModel` (composition root view model: owns the navigator, the two
    screen view models, two `DelegateCommand`s, and boolean convenience properties for the view to
    bind against), `ObservableObject` (minimal `INotifyPropertyChanged` base — no MVVM framework
    dependency), `DelegateCommand` (minimal `ICommand`).
- **`src/XlightsAI.Studio.App`** (new Avalonia Desktop executable project, `net10.0`, `WinExe`,
  assembly name `XlightsAI.Studio`) — the real UI layer:
  - `Program.cs` — `[STAThread] Main` wraps `StartWithClassicDesktopLifetime` in a try/catch so a
    startup error (e.g. a broken graphics backend) is reported to stderr and returns exit code 1
    instead of an unexplained crash/silent exit — this is the step's own "startup errors" guidance
    operationalized.
  - `App.axaml`/`App.axaml.cs` — composition root: builds one real `ShellNavigator` +
    `ShellViewModel` (with an honest empty-list project source, since there is no real project
    store yet), calls `RunStartup()`, and opens `MainWindow` bound to it.
  - `Views/MainWindow.axaml(.cs)` — one real window with two nav buttons (Welcome/Projects) and a
    content area that switches on `IsStarting`/`IsFailed`/`IsShowingWelcome`/`IsShowingProjects`,
    so every one of the state machine's reachable states has a distinct, non-blank visual — this
    is the UI-level answer to the step's "silence ... must be handled explicitly" edge case (see
    §9 for why that literal failure-case text is disclosed as a probable spec-generation
    mismatch for this step).
  - `app.manifest` (DPI-aware, `asInvoker`).
- **Tests:** `tests/XlightsAI.Studio.Shell.Tests/Step004AcceptanceTests.cs` (xUnit, 9 tests) —
  covers the full `Step004_Acceptance_Passes` happy path (Starting → Ready → navigate Projects →
  Welcome) plus five dedicated negative paths (see §5).

## 3. Commands actually executed

```text
$ dotnet --version
10.0.302

$ dotnet new list avalonia
No templates found matching: 'avalonia'.
# No Avalonia project template was installed in this environment; the App/Shell projects and
# .csproj/.axaml files below were hand-authored (not template-scaffolded).

$ dotnet build src/XlightsAI.Studio.Shell/XlightsAI.Studio.Shell.csproj
Build succeeded. 0 Warning(s) 0 Error(s)

$ dotnet test tests/XlightsAI.Studio.Shell.Tests/XlightsAI.Studio.Shell.Tests.csproj --logger "console;verbosity=normal"
Total tests: 9. Passed: 9. Failed: 0.

$ dotnet build src/XlightsAI.Studio.App/XlightsAI.Studio.App.csproj
# first attempt: Build succeeded with 2 warnings (NU1903: Tmds.DBus.Protocol 0.21.2 known high
# severity advisory, GHSA-xrw6-gwf8-vvr9 — a transitive Linux D-Bus dependency of Avalonia.Desktop)
# remediated by pinning a direct PackageReference to Tmds.DBus.Protocol 0.95.1; re-ran:
Build succeeded. 0 Warning(s) 0 Error(s)

$ dotnet build XlightsAI.Studio.slnx
Build succeeded. 0 Warning(s) 0 Error(s)

$ dotnet build XlightsAI.Studio.slnx -c Release
Build succeeded. 0 Warning(s) 0 Error(s)

$ dotnet test XlightsAI.Studio.slnx --logger "console;verbosity=normal"
XlightsAI.Studio.Shell.Tests: Total tests: 9, Passed: 9
XlightsAI.Studio.Reference.Tests: Total tests: 27, Passed: 27

# Real desktop launch (PowerShell, this is a native Windows 11 workstation, not a headless CI box):
$ Start-Process src/XlightsAI.Studio.App/bin/Debug/net10.0/XlightsAI.Studio.exe
HasExited: False
MainWindowTitle: xLights AI Choreography Studio
Id: 105048
# Screenshot captured (System.Drawing + user32 GetWindowRect/CopyFromScreen), see Section 6.
# Simulated a real left-click on the "Projects" nav button (SetCursorPos + mouse_event), screen
# re-captured showing the explicit empty state, see Section 6.
# Process closed cleanly: CloseMainWindow(), verified no longer running.
```

All commands were run via PowerShell; the Bash tool again failed on every invocation in this
environment (exit 1, no output), consistent with Steps 001–003.

## 4. Required acceptance: "Launch Windows desktop executable on a clean build"

Proven as an actual observed process, not inferred from code:

1. Clean `dotnet build` of the full solution (Debug and Release, both 0 warnings/0 errors).
2. `XlightsAI.Studio.exe` (163,328 bytes, built `2026-10-10 17:17:15`) started via
   `Start-Process`, confirmed still running 3 seconds later with the expected window title.
3. Real screen capture of the running window (Section 6) shows the Welcome screen rendered with
   live content bound from `ShellViewModel`/`WelcomeViewModel` — not a placeholder image.
4. A real mouse click (via `user32.dll` `SetCursorPos`/`mouse_event`, not a simulated/mocked
   event) on the "Projects" button produced a second real screen capture showing the Projects
   screen's explicit empty state — proving the navigation command path is wired from the actual
   rendered button through `ShellViewModel.NavigateToProjectsCommand` → `ShellNavigator.NavigateTo`
   → `ProjectsViewModel.Load` → back to the view, end to end.
5. Process closed cleanly afterward (`CloseMainWindow`, verified exited).

## 5. Automated test results — detail

```text
Passed XlightsAI.Studio.Shell.Tests.Step004AcceptanceTests.Step004_Acceptance_Passes
Passed XlightsAI.Studio.Shell.Tests.Step004AcceptanceTests.ProjectsScreen_WithNoProjects_ShowsExplicitEmptyState_NotBlank
Passed XlightsAI.Studio.Shell.Tests.Step004AcceptanceTests.ProjectsScreen_WhenSourceThrows_ShowsExplicitErrorState_NegativePath
Passed XlightsAI.Studio.Shell.Tests.Step004AcceptanceTests.NavigateTo_BeforeStartupCompletes_ThrowsRatherThanSilentlyNoOp_NegativePath
Passed XlightsAI.Studio.Shell.Tests.Step004AcceptanceTests.RunStartup_WhenStartupWorkThrows_TransitionsToFailed_NotCrash_NegativePath
Passed XlightsAI.Studio.Shell.Tests.Step004AcceptanceTests.NavigateTo_AfterStartupFailed_StillThrowsRatherThanSilentlyNoOp_NegativePath
Passed XlightsAI.Studio.Shell.Tests.Step004AcceptanceTests.CompleteStartup_CalledTwice_Throws_NegativePath
Passed XlightsAI.Studio.Shell.Tests.Step004AcceptanceTests.FailStartup_RequiresNonEmptyCodeAndMessage_NegativePath
Passed XlightsAI.Studio.Shell.Tests.Step004AcceptanceTests.ShellNavigator_Changed_FiresOnEveryStateAndScreenTransition

Total tests: 9. Passed: 9.
```

- **`Step004_Acceptance_Passes`** — the deterministic fixture the step requires: a fresh
  `ShellNavigator` + `ShellViewModel` starts `Starting`, cannot navigate yet
  (`NavigateToProjectsCommand.CanExecute() == false`); after `RunStartup()` it is `Ready` on
  `Welcome`; navigating to `Projects` loads and asserts the real `ProjectNames` list came through;
  navigating back to `Welcome` round-trips. Asserts persisted view-model state and the public
  `CanExecute`/`CurrentScreen`/`LoadState` surface, not just that a method was invoked.
- **Five negative-path tests** (the step's required "failure/edge case to prove"): a disclosed
  interpretation of the spec's literal edge-case text ("Silence, invalid timing boundaries and
  missing media") — see §9 for why that text is flagged as likely mismatched for a UI-shell step —
  mapped onto this step's actual observable failure modes: navigating before/after startup
  resolves throws rather than silently doing nothing (`NavigateTo_BeforeStartupCompletes_...`,
  `NavigateTo_AfterStartupFailed_...`); a startup-work exception becomes an explicit `Failed`
  state with the real message, not a crash (`RunStartup_WhenStartupWorkThrows_...`); a data-source
  exception on the Projects screen becomes an explicit `Error` state with the real message, not a
  blank list (`ProjectsScreen_WhenSourceThrows_...`); double-completing startup and
  empty-code/message `FailStartup` calls are both rejected (`CompleteStartup_CalledTwice_...`,
  `FailStartup_RequiresNonEmptyCodeAndMessage_...`).
- **`ProjectsScreen_WithNoProjects_...`** and **`ShellNavigator_Changed_...`** — prove the "empty
  state must look like Empty, not blank" requirement and that the `Changed` event fires exactly
  once per real transition (and not on a redundant same-screen `NavigateTo`), respectively.

## 6. Manual / environment verification (real desktop launch)

- **Environment:** this agent is running directly on a native Windows 11 Pro for Workstations
  machine (not a headless/container CI runner), so a real `Start-Process` + screen capture was
  possible and was used instead of a simulated/mocked substitute.
- **Welcome screen** (`implementation-evidence/PHASE-01/step-04-welcome-screen.png`,
  SHA-256 `f74a2c827382faa9929e66e8b5e43e95edadb900371d85f32276ec70b8b58862`): real screen capture of
  the running `XlightsAI.Studio.exe`, title bar reads "xLights AI Choreography Studio", two nav
  buttons visible, Welcome content bound from the live view model.
- **Projects (empty state) screen**
  (`implementation-evidence/PHASE-01/step-04-projects-empty-screen.png`,
  SHA-256 `cd4fa9e3a6292b193397e5aa5610a466015b06a933b971f3db5208614c71c7d7`): captured immediately
  after a real simulated mouse click on the "Projects" button; shows "No projects yet. Create one
  to get started." — the explicit `ProjectsLoadState.Empty` text, not a blank panel.
- **xLights validation:** not applicable to this step — no XSQ/layout/rendering artifact is
  produced here (Phase 01's own out-of-scope note: "do not ... build an effect editor").
- **No Python runtime:** confirmed by inspection — no `PackageReference`, `ProjectReference`,
  process launch, or `.py`/embedded-interpreter dependency exists anywhere in
  `src/XlightsAI.Studio.App` or `src/XlightsAI.Studio.Shell`; the only runtime dependencies are
  Avalonia/.NET packages (Section 7).

## 7. Changed/added paths

```
XlightsAI.Studio.slnx                                                    (added Shell/App/Shell.Tests projects)
src/XlightsAI.Studio.Shell/XlightsAI.Studio.Shell.csproj
src/XlightsAI.Studio.Shell/StartupState.cs
src/XlightsAI.Studio.Shell/StartupFailure.cs
src/XlightsAI.Studio.Shell/ShellScreen.cs
src/XlightsAI.Studio.Shell/IShellNavigator.cs
src/XlightsAI.Studio.Shell/ShellNavigator.cs
src/XlightsAI.Studio.Shell/ObservableObject.cs
src/XlightsAI.Studio.Shell/ProjectsLoadState.cs
src/XlightsAI.Studio.Shell/ProjectsViewModel.cs
src/XlightsAI.Studio.Shell/WelcomeViewModel.cs
src/XlightsAI.Studio.Shell/DelegateCommand.cs
src/XlightsAI.Studio.Shell/ShellViewModel.cs
src/XlightsAI.Studio.App/XlightsAI.Studio.App.csproj
src/XlightsAI.Studio.App/app.manifest
src/XlightsAI.Studio.App/Program.cs
src/XlightsAI.Studio.App/App.axaml
src/XlightsAI.Studio.App/App.axaml.cs
src/XlightsAI.Studio.App/Views/MainWindow.axaml
src/XlightsAI.Studio.App/Views/MainWindow.axaml.cs
tests/XlightsAI.Studio.Shell.Tests/XlightsAI.Studio.Shell.Tests.csproj
tests/XlightsAI.Studio.Shell.Tests/Step004AcceptanceTests.cs
implementation-evidence/PHASE-01/step-04-welcome-screen.png
implementation-evidence/PHASE-01/step-04-projects-empty-screen.png
implementation-evidence/PHASE-01/STEP-04.md
```

Nothing under Steps 001–003's existing paths (`src/XlightsAI.Studio.Reference/**`, `fixtures/**`,
`reference/**`, `tools/**`) was modified. No xLights show files exist in or near this repository
to put at risk; this step produces no XSQ/show-folder writes of any kind.

## 8. Package/dependency inventory (new, this step only)

| Package | Version | Project | Why |
|---|---|---|---|
| `Avalonia` | 11.3.2 | App | UI framework |
| `Avalonia.Desktop` | 11.3.2 | App | Windows/desktop backend |
| `Avalonia.Themes.Fluent` | 11.3.2 | App | Default theme |
| `Avalonia.Fonts.Inter` | 11.3.2 | App | Bundled font (no system-font dependency) |
| `Avalonia.Diagnostics` | 11.3.2 | App (Debug only) | Dev-time inspector; excluded from Release via `Condition` |
| `Tmds.DBus.Protocol` | 0.95.1 (pinned, direct) | App | Transitive Linux D-Bus dep of `Avalonia.Desktop`; pinned past `GHSA-xrw6-gwf8-vvr9` (originally resolved to vulnerable 0.21.2) |

No Python, Docker, Git-at-runtime, or native ML/audio dependency was added — this step is UI
shell scaffolding only, consistent with the phase's explicit out-of-scope list.

## 9. Architecture deviations

- **Two new projects, not one.** The spec's proposed signatures don't name a project split for
  this step. Split into `XlightsAI.Studio.Shell` (Avalonia-free navigation/view-model logic) and
  `XlightsAI.Studio.App` (the Avalonia executable) specifically so the navigation state machine
  and the "one project feature" required by this step are unit-testable without any UI framework
  or display — directly serving the spec's own verification principle ("unit tests + ... one
  observable UI/xLights demonstration"). This is a narrow, concrete boundary (UI binding vs.
  navigation logic), not a speculative plugin architecture.
- **Disclosed interpretation of this step's literal failure/edge-case text.** The spec's Step 004
  row reads: "Silence, invalid timing boundaries and missing media must be handled explicitly."
  That text matches the audio-transport domain (`SongAsset`/`IAudioTransport`, listed in the
  cross-phase ownership table as introduced in **Phase 04**, not Step 4 of Phase 01) far better
  than it matches "create an Avalonia shell with navigation." This looks like a spec-generation
  artifact — the edge-case column plausibly cross-referenced the wrong "04" (phase number vs.
  step number) — rather than a deliberate requirement that a navigation-shell step must handle
  audio silence. Per this step's own actual implementation guidance ("startup errors") and
  required acceptance ("launch ... on a clean build"), I implemented the closest faithful
  analogue instead of a literal, out-of-place audio check: explicit, non-silent handling of (a)
  startup failure (`RunStartup` catches and reports, never crashes silently), (b) invalid
  navigation-state transitions (`NavigateTo`/`CompleteStartup`/`FailStartup` throw instead of
  silently no-op'ing), and (c) a "missing data" analogue — the Projects screen's data-source
  exception becomes an explicit `Error` state rather than a blank/silent list. Flagged here for
  review rather than silently reinterpreting the requirement away.
- **No Avalonia project template was available** in this environment (`dotnet new list avalonia`
  found none); the `.csproj`/`.axaml`/`Program.cs` files were hand-authored to match the current
  stable Avalonia 11.3.2 desktop-app shape rather than scaffolded, and reviewed against the
  package's own expected `AppBuilder`/`ClassicDesktopStyleApplicationLifetime` API surface during
  the build-and-fix loop in Section 3.
- **No persisted settings/diagnostics yet** — this step is navigation-shell-only; structured
  logging, crash handling and the diagnostics page are explicitly Step 005's scope and are not
  pulled forward here.

## 10. Unresolved issues / risks carried forward

1. **Evidence-filename convention inconsistency** — still unresolved (carried forward again from
   Steps 002/003).
2. **The Step 004 edge-case text mismatch** (§9) is new to this step and not yet resolved at the
   phase-spec level; recommend the phase owner confirm whether PHASE-01.md's Step 004 row should
   be corrected to a UI-shell-appropriate edge case before Step 005 is scoped, since the same
   generation pattern may affect other steps' edge-case text.
3. **No automated UI-level test** (e.g. Avalonia headless testing) was added — `dotnet add
   package Avalonia.Headless.XUnit` was considered but deferred as out of this step's minimum
   scope; the real click-and-screenshot manual demo (Section 6) is the only end-to-end UI
   verification for this step. Recommend adding headless UI tests in a later step if the shell's
   view-binding surface grows past what the real-launch demo can practically keep covering by
   hand.
4. Git Bash remains non-functional in this environment (carried forward from Steps 001–003); all
   commands in this evidence were run via PowerShell.
5. `Avalonia.Diagnostics` is referenced Debug-only via an MSBuild `Condition`; not independently
   verified here that a Release-configuration publish excludes it from the output (Release build
   succeeded, Section 3, but the specific diagnostic-overlay-absence was not re-checked by
   re-running the Release `.exe`).

## 11. Gate checklist (this step only)

- [x] Real Avalonia .NET 10 desktop solution created, builds clean on both Debug and Release
      (0 warnings, 0 errors after the Tmds.DBus.Protocol advisory remediation).
- [x] Basic navigation implemented and proven both by unit test and by a real click on the real
      running executable (Section 4, Section 6).
- [x] One project feature (Projects screen with explicit Loading/Empty/Error/Loaded states)
      shipped; does not reach ahead into Step 006's real persistence contract.
- [x] No Python runtime, Docker, or other excluded dependency introduced (Section 6, Section 8).
- [x] `Step004_Acceptance_Passes` plus five dedicated negative-path tests, all passing.
- [x] Full existing regression suite (27 tests, Steps 001–003) re-run unmodified and still passing.
- [x] Real desktop launch demonstrated: process started, stayed running, correct window title,
      two real screen captures showing distinct, non-blank states, process closed cleanly.
- [x] No original/show files touched; nothing pre-existing was at risk.
- [ ] Not yet done, out of this step's scope: structured logging/diagnostics page (Step 005),
      real project create/open/save (Step 006), SQLite persistence (Step 007).

**Step 004 status: PASS.** Proceed to Step 005 only after this is reviewed.
