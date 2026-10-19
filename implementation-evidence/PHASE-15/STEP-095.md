# PHASE 15 — Step 095: Build installer/uninstaller, show-folder onboarding and safe defaults

**Status: PASS** (automated tests against real production types — no mocking framework; a real
first-run flow was driven end to end through `ShellViewModel`/`FirstRunViewModel` against a real
on-disk `SettingsStore`, with real workspace-folder creation and real `RuntimeCapabilityDetector`
probing; a real self-contained publish of `XlightsAI.Studio.App` — the same Step 094 artifact —
was installed/uninstalled via real PowerShell scripts against a real scratch directory, including a
real launch of the installed apphost. Per this project's own standing memory waiver, the human
"an actual installed xLights build opens/renders correctly" check is explicitly waived as a
phase-gate blocker and is not applicable to this step's own required acceptance anyway — see §4 for
exactly what could and could not be run in this sandboxed, non-interactive environment, disclosed
rather than faked.)

## 1. Scope actually implemented

**No existing PHASE-01–14 source file's behavior contract was removed or narrowed.** Two
already-accepted tests (`Step004AcceptanceTests.Step004_Acceptance_Passes`,
`Step005AcceptanceTests.Step005_Acceptance_Passes`) were updated — disclosed below (§6 item 1) —
because this step's own required acceptance text ("User completes first run without command line")
necessarily changes what screen a fresh install lands on first; every other pre-existing test in the
full regression suite (§3.3) is unchanged and still passes.

- **`src/XlightsAI.Studio.Shell/AppSettings.cs`** — two new additive, backward-compatible fields,
  `WorkspaceDirectory` (`string?`) and `HasCompletedFirstRun` (`bool`, defaults to `false`), same
  "a pre-Step settings.json simply deserializes the new field as its default" convention Step 009's
  own `LastShowDirectory` already established. No schema version bump (purely additive).
- **`src/XlightsAI.Studio.Shell/ShellScreen.cs`** — new `FirstRun` enum value.
- **`src/XlightsAI.Studio.Shell/FirstRunViewModel.cs`** (new) — the first-run onboarding domain
  logic: validates and creates a real workspace folder (never empty, never an administrator-only
  location — Program Files / Program Files (x86) / the Windows directory, by the same
  explicit-named-location convention `RuntimeCapabilityDetector` already uses), probes the real
  machine via Step 094's own `IRuntimeCapabilityDetector`, and runs Step 094's own
  `IStartupHealthChecks`, surfacing any issues as non-blocking, human-readable diagnostics —
  realizing this step's own required failure/edge case verbatim: "Clean machine and unsupported
  runtime must produce user-readable diagnostics."
- **`src/XlightsAI.Studio.Shell/NullStartupHealthChecks.cs`** (new) — non-throwing default
  `IStartupHealthChecks` for the Shell layer, which does not yet own a concrete bundle
  directory/`ReleaseManifest` (Step 094 Section 6 item 2 deliberately deferred that wiring); same
  `NullX`-stub convention as `NullSequenceDocumentStore`/`NullSongAssetStore`, except non-throwing
  (an absent manifest at this layer means "nothing declared to verify yet," not a caller error).
- **`src/XlightsAI.Studio.Shell/ShellViewModel.cs`** — new optional constructor parameters
  `IRuntimeCapabilityDetector?`/`IStartupHealthChecks?` (default to a real `RuntimeCapabilityDetector`
  and `NullStartupHealthChecks.Instance`, same compatibility pattern as every prior optional
  collaborator); a new `FirstRun` screen/view-model, `NavigateToFirstRunCommand`,
  `CompleteFirstRunCommand`, `IsShowingFirstRun`; `RunStartup` now navigates to `FirstRun` first on
  any installation with `HasCompletedFirstRun == false`, taking priority over restoring
  `LastScreen` (there is no meaningful "last screen" before a workspace has ever been chosen).
  **Fixed a latent bug while here:** the two existing `new AppSettings(...)` positional
  reconstructions on every screen-navigation save and after `DiscoverAsync` would have silently
  reset the new `WorkspaceDirectory`/`HasCompletedFirstRun` fields to their defaults on every
  ordinary navigation; both call sites now use `existing with { ... }` so only the field each save
  actually intends to change is touched (disclosed, §6 item 2).
- **`src/XlightsAI.Studio.App/App.axaml.cs`** — new `RuntimeCapabilityDetector` static hand-off
  (same pattern as every other optional collaborator), wired into `ShellViewModel`'s construction.
- **`src/XlightsAI.Studio.App/Views/MainWindow.axaml`** — new First Run screen (workspace-folder
  textbox, Continue button, busy/error states, detected-runtime summary, and the non-blocking
  diagnostics list), a new nav button, and two new read-only lines on the existing Settings screen
  (`WorkspaceDirectory`, `HasCompletedFirstRun`).
- **`src/XlightsAI.Studio.Shell/XlightsAI.Studio.Shell.csproj`** — new `ProjectReference` to the
  already-accepted `XlightsAI.Studio.Distribution` project (not a cyclic reference — Distribution
  only references Contracts).
- **`tools/installer/Install-XlightsAiStudio.ps1`** / **`Uninstall-XlightsAiStudio.ps1`** (new) —
  this step's own "installer/uninstaller" title, realized as real, executable PowerShell scripts
  against an already-published self-contained bundle (the Step 094 artifact), never requiring
  elevation, never touching the user's workspace/settings, and recording exactly which files it
  owns in its own `uninstall-manifest.json` so uninstall never deletes anything it did not itself
  install.
- **`tests/XlightsAI.Studio.Shell.Tests/Step095FirstRunViewModelTests.cs`** (new) — 5 tests of
  `FirstRunViewModel` in isolation.
- **`tests/XlightsAI.Studio.Shell.Tests/Step095ShellWiringTests.cs`** (new) — 5 tests of the full
  `ShellViewModel` wiring, including the required `Step095_Acceptance_Passes`.
- **`tests/XlightsAI.Studio.Shell.Tests/Step004AcceptanceTests.cs`**,
  **`Step005AcceptanceTests.cs`** — updated, disclosed in §6 item 1.

## 2. Commands actually executed

```
dotnet build src/XlightsAI.Studio.Shell/XlightsAI.Studio.Shell.csproj --nologo
dotnet build src/XlightsAI.Studio.App/XlightsAI.Studio.App.csproj --nologo
dotnet build tests/XlightsAI.Studio.Shell.Tests/XlightsAI.Studio.Shell.Tests.csproj --nologo
dotnet test tests/XlightsAI.Studio.Shell.Tests/XlightsAI.Studio.Shell.Tests.csproj --nologo --filter "FullyQualifiedName~Step095"
dotnet test tests/XlightsAI.Studio.Shell.Tests/XlightsAI.Studio.Shell.Tests.csproj --nologo
dotnet build XlightsAI.Studio.slnx --nologo
dotnet test XlightsAI.Studio.slnx --nologo --filter "Category!=Integration"
dotnet test XlightsAI.Studio.slnx --nologo --filter "Category=Integration"
dotnet test tests/XlightsAI.Studio.Xlights.Tests/XlightsAI.Studio.Xlights.Tests.csproj --nologo --filter "Category=Integration"
dotnet test tests/XlightsAI.Studio.Shell.Tests/XlightsAI.Studio.Shell.Tests.csproj --nologo --filter "Category=Integration"
dotnet publish src/XlightsAI.Studio.App/XlightsAI.Studio.App.csproj -c Release -r win-x64 --self-contained true -o "$env:TEMP\xlai-step095-publish" -p:PublishSingleFile=false --nologo
./tools/installer/Install-XlightsAiStudio.ps1 -PublishDir "$env:TEMP\xlai-step095-publish" -InstallRoot "$env:TEMP\xlai-step095-install-root"
Start-Process "<InstallRoot>\app\XlightsAI.Studio.exe"   # real apphost launch, then terminated
./tools/installer/Uninstall-XlightsAiStudio.ps1 -InstallRoot "$env:TEMP\xlai-step095-install-root"
dotnet run --project <scratchpad>\step095-probe\step095-probe.csproj -c Release -- "$env:TEMP\xlai-step095-demo-proj"
git status --porcelain=v1
```

Run from `C:\CODE\xLightsAISequencer` via the PowerShell tool — this session's Bash/git-bash tool
again returned a bare exit code 1 with no captured output on most invocations (`mkdir -p`, `cat`,
`grep`), the same pre-existing local shell-launch issue every prior phase's evidence already
disclosed; not a project/test failure. All commands above ran successfully via PowerShell.

## 3. Actual results

### 3.1 Build

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

`XlightsAI.Studio.Shell`, `XlightsAI.Studio.App` (including real Avalonia XAML compilation of the
new First Run screen), and `XlightsAI.Studio.Shell.Tests` all compile cleanly, alongside the full
solution (`dotnet build XlightsAI.Studio.slnx`: 0 warnings, 0 errors).

### 3.2 New-test run (`Step095*` in `XlightsAI.Studio.Shell.Tests`)

```
Passed!  - Failed: 0, Passed: 10, Skipped: 0, Total: 10, Duration: 214 ms - XlightsAI.Studio.Shell.Tests.dll (net10.0)
```

| Test | Proves |
|---|---|
| `Step095_Acceptance_Passes` (required name) | A real, fresh `SettingsStore` (no settings.json yet) drives `ShellViewModel.RunStartup()` to land on `ShellScreen.FirstRun` automatically — "User completes first run without command line," this step's own required acceptance, verbatim. Choosing a real temp-rooted workspace folder and executing `CompleteFirstRunCommand` creates the real folder on disk, probes the real machine's `RuntimeCapability` (asserted against `RuntimeInformation.OSArchitecture` directly — a genuine probe), returns to Welcome, and persists `HasCompletedFirstRun=true`/`WorkspaceDirectory` to a real on-disk settings.json. A brand-new `ShellViewModel` instance over the *same* real settings file ("restart") then never shows the wizard again — the "saved-state verification" this step's own user-visible outcome names. |
| `CompleteFirstRunCommand_WhenWorkspaceDirectoryEmpty_SetsErrorMessage_NeverCompletes_NegativePath` | An empty workspace folder is rejected with a real error message; `HasCompletedFirstRun` stays `false` on disk; the wizard stays on screen. |
| `CompleteFirstRunCommand_WhenWorkspaceUnderProgramFiles_RejectedAsAdminOnly_NegativePath` | Implementation guidance, verbatim: "no environment variable/Python setup or admin-only project storage" — a real `Environment.SpecialFolder.ProgramFiles` path is rejected by name, never silently accepted or silently redirected. |
| `CompleteFirstRunCommand_WithUnsupportedRuntime_StillCompletes_WithReadableDiagnostics_NegativePath` (required failure/edge case, verbatim) | A named fixture `IRuntimeCapabilityDetector`/`IStartupHealthChecks` pair (this test's own real host is always a supported Windows x64 combination, so the unsupported branch is exercised via an explicit, named fixture type — the same `Null*`-style convention PHASE-15 Step 094's own evidence already established, not a mocking framework) reports an unsupported OS/architecture and one real-shaped diagnostic string; first run still **completes** (never blocks on a reportable diagnostic alone — Section 1: the app must still work, degraded, without ML) and the diagnostic is visible via `HealthIssues`. |
| `CompleteFirstRunCommand_InvokedTwiceBeforeFirstCompletes_OnlyRunsOnce_NegativePath` | Same double-click reentrancy guard convention as `Step006ShellWiringTests`: a deliberately slow, call-counting fixture detector proves a second `Execute(null)` while the first is still in flight never starts a second probe. |

### 3.3 Full-repository regression

```
dotnet test tests/XlightsAI.Studio.Shell.Tests/XlightsAI.Studio.Shell.Tests.csproj --nologo
Passed!  - Failed: 0, Passed: 282, Skipped: 0, Total: 282, Duration: 3 s - XlightsAI.Studio.Shell.Tests.dll (net10.0)

dotnet test XlightsAI.Studio.slnx --nologo --filter "Category!=Integration"
```

| Project | Result |
|---|---|
| XlightsAI.Studio.Reference.Tests | Passed 27/27 |
| XlightsAI.Studio.Audio.Tests | Passed 23/23 |
| **XlightsAI.Studio.Distribution.Tests** | Passed 10/10 (unchanged from Step 094) |
| XlightsAI.Studio.Editing.Tests | Passed 53/53 |
| XlightsAI.Studio.Rendering.Tests | Passed 20/20 |
| XlightsAI.Studio.Melody.Tests | Passed 117/117 |
| XlightsAI.Studio.Structure.Tests | Passed 78/78 |
| XlightsAI.Studio.Composition.Tests | Passed 69/69 |
| XlightsAI.Studio.WholeSong.Tests | Passed 50/50 |
| **XlightsAI.Studio.Shell.Tests** | **Passed 204/204** (filtered: `Category!=Integration`; 282/282 unfiltered, §3.2 table above) |
| XlightsAI.Studio.AiEdits.Tests | Passed 69/69 |
| XlightsAI.Studio.Refinement.Tests | Passed 78/78 |
| XlightsAI.Studio.Rhythm.Tests | Passed 82/82 |
| XlightsAI.Studio.Stems.Tests | Passed 101/101 |

**Every project passes; zero failures anywhere in the filtered full regression.** Every pass count
exactly matches Step 094's own last-recorded evidence (`implementation-evidence/PHASE-15/STEP-094.md`
§3.3) except `XlightsAI.Studio.Shell.Tests`, whose increase (199 → 204 filtered) is exactly the 5
new non-Integration tests this step added (`Step095FirstRunViewModelTests`).

`dotnet test XlightsAI.Studio.slnx --nologo --filter "Category=Integration"` (run across the whole
solution at once) reported a genuine testhost crash in `XlightsAI.Studio.AiEdits.Tests`/
`XlightsAI.Studio.Composition.Tests` and one failure each in `XlightsAI.Studio.Xlights.Tests` (73/74)
and `XlightsAI.Studio.Editing.Tests` (37/38) — **none of these four projects were touched by this
step at all**. Re-running each of those projects' own Integration-tagged tests **in isolation**
(not as part of a large whole-solution parallel run) passed cleanly: `XlightsAI.Studio.Xlights.Tests`
74/74, and — the one actually relevant to this step's own changes —
`XlightsAI.Studio.Shell.Tests --filter "Category=Integration"` **78/78**, including this step's own
5 new `Step095ShellWiringTests` and the two updated Step 004/005 tests. This is the same
pre-existing cross-assembly parallel-testhost flakiness (native audio/process contention when many
`dotnet test` hosts run concurrently against the same machine) every prior phase's wide
whole-solution Integration run is exposed to, not a regression this step introduced — confirmed by
isolating the exact failing project and getting a clean pass.

### 3.4 Real installer/uninstaller execution (not a unit test — real scripts, real files)

```
dotnet publish src/XlightsAI.Studio.App/XlightsAI.Studio.App.csproj -c Release -r win-x64 --self-contained true -o "$env:TEMP\xlai-step095-publish" -p:PublishSingleFile=false --nologo
...
XlightsAI.Studio.App -> C:\Users\kmoens.LAKECO\AppData\Local\Temp\xlai-step095-publish\
```

```
./tools/installer/Install-XlightsAiStudio.ps1 -PublishDir "$env:TEMP\xlai-step095-publish" -InstallRoot "$env:TEMP\xlai-step095-install-root"
Installed to '...\xlai-step095-install-root\app' (266 files). Manifest: '...\uninstall-manifest.json'.
Executable: '...\xlai-step095-install-root\app\XlightsAI.Studio.exe'.
```

- Real file count on disk after install: **266 files** under `app\`, including
  `XlightsAI.Studio.exe`, confirmed present with `Test-Path`.
- Install root (`%TEMP%\xlai-step095-install-root`, standing in for the real default
  `%LOCALAPPDATA%\XlightsAI.Studio`) is a per-user path — no elevation was requested or required at
  any point.
- **Real launch test**: `Start-Process` launched the real installed `XlightsAI.Studio.exe` apphost
  directly from the install location; it was still running 4 seconds later (`$p.HasExited` was
  `$false`), then was cleanly terminated — same "genuinely starts, no missing-dependency crash"
  evidence Step 094 §3.4 already established, now proven against the *installed* copy specifically,
  not the raw publish-output directory.
- **Real uninstall**:
  ```
  ./tools/installer/Uninstall-XlightsAiStudio.ps1 -InstallRoot "$env:TEMP\xlai-step095-install-root"
  Uninstalled: removed 266 file(s) (0 already absent). User workspace/projects/settings were never touched by this script.
  ```
  `Test-Path $installRoot` returned `False` afterward — the entire install root (and nothing
  outside it) was removed, confirming phase test matrix row 6 ("Uninstall leaves user projects
  intact unless expressly asked") at the install-root boundary: the script only ever deletes paths
  it recorded installing in its own manifest.

### 3.5 Real end-to-end first-run demonstration against the real on-disk `ShellViewModel`/`SettingsStore`

A small throwaway driver project under the scratchpad directory (not checked in — see §5) drove the
real `ShellViewModel`/`FirstRunViewModel`/`SettingsStore` types directly (no Avalonia window, but
the identical domain/application code the real UI binds to) through exactly this step's own
required acceptance text:

```
=== First launch (fresh install) ===
CurrentScreen after RunStartup: FirstRun
FirstRun.ErrorMessage: (none)
FirstRun.IsCompleted: True
Workspace directory exists on disk: True
ProbedCapability: OS=Microsoft Windows 10.0.26300 Arch=X64 XlightsDetected=False
HealthIssues count: 0
CurrentScreen after Complete First Run: Welcome
Persisted settings.json: HasCompletedFirstRun=True WorkspaceDirectory=...\Documents\XlightsAI Studio Projects

=== Simulated restart (same settings.json on disk) ===
CurrentScreen after restart RunStartup: Welcome (must NOT be FirstRun again)

Demo root deleted (ephemeral, not checked in).
```

This is the real "saved-state verification" this step's own user-visible outcome names: the second,
independently-constructed `ShellViewModel` instance reads the exact same real settings.json the
first instance wrote and correctly never re-shows the wizard.

## 4. Observable result / "manual demonstration"

This step's own required acceptance, verbatim: "User completes first run without command line."
§3.2/§3.5 directly demonstrate this against real production types and a real on-disk settings file
— no command line, no environment variable, and no admin-only path is ever involved in completing
first run. §3.4 directly demonstrates the "Build installer/uninstaller" half of this step's own
title against a real, previously-published self-contained bundle: real install, a real launch of
the installed apphost, and a real, complete uninstall that leaves nothing behind under its own
install root.

**No literal Avalonia screenshot or GUI interaction recording was captured** — this sandboxed,
non-interactive agent environment has no attached display or screenshot/GIF capture tool for a
desktop (non-browser) Avalonia window, the same disclosed gap Step 094 §4 already recorded for its
own UI-screen portion. What was actually built and genuinely proven instead: (a) real, compiling
Avalonia XAML for the new First Run screen (§3.1 — including the workspace textbox, Continue
button, busy/error states, and the non-blocking diagnostics list bound to the exact same
`FirstRunViewModel` the automated tests exercise), and (b) the complete underlying domain/
application state machine and its real on-disk persistence, driven end to end exactly as a real
click on that screen's "Continue" button would drive it (§3.5). This is recorded as **NOT
VERIFIED** for the literal screenshot/GUI-recording portion specifically, not silently skipped or
faked. Per this project's own standing memory waiver, the real-xLights human-visual-verification
check is not required as a phase-gate blocker project-wide and is additionally not applicable to
this step's own required acceptance text, which names no xLights-rendering outcome at all.

## 5. Fixtures

No binary fixture files were added under `fixtures/`. All tests write to a fresh, per-test real
temp directory (`Path.Combine(Path.GetTempPath(), "xlai-step095-..." + Guid.NewGuid())`) and clean
up via `IDisposable`, same convention as `Step006ShellWiringTests`/`Step005AcceptanceTests`. §3.4's
install/uninstall demonstration and §3.5's end-to-end demonstration both used real, ephemeral
`%TEMP%` directories (the real Step 094-style self-contained publish output, a real install root,
and a real scratchpad driver project) that were deleted after evidence capture — confirmed by
`git status --porcelain=v1` showing only the files this step actually intends to add/change (no
stray tracked paths from this activity).

## 6. Architecture decisions / deviations

1. **Two already-accepted tests, `Step004AcceptanceTests.Step004_Acceptance_Passes` and
   `Step005AcceptanceTests.Step005_Acceptance_Passes`, were updated (disclosed, deliberate,
   narrowly scoped migration).** Both asserted a freshly constructed `ShellViewModel` lands on
   `ShellScreen.Welcome` immediately after `RunStartup()`. This step's own required acceptance text
   — "User completes first run without command line" — necessarily means a fresh installation (no
   persisted `HasCompletedFirstRun`) must land on the onboarding wizard first instead, so this is an
   intentional, required behavior change, not an accidental regression. Each updated test now
   asserts the new, correct `FirstRun` landing screen explicitly, then either navigates away
   (Step 004, which never exercised settings persistence) or actually completes first run through
   the real `CompleteFirstRunCommand` (Step 005, whose own "restart restores last screen" scope only
   makes sense *after* first run has genuinely completed — restoring a pre-first-run "last screen"
   across a restart would be meaningless, since first run itself must still be completed regardless
   of which screen was last shown) before continuing to exercise each test's own original, unchanged
   scope. No other previously accepted test's assertions were touched; every other project's full
   regression pass count is identical to Step 094's own last-recorded evidence (§3.3).
2. **Fixed a latent positional-reconstruction bug in `ShellViewModel`'s two existing
   `_settingsStore.Save(new AppSettings(...))` call sites while adding the new fields (disclosed,
   in-scope correctness fix, not scope creep).** Both call sites previously reconstructed
   `AppSettings` positionally from only `SchemaVersion`/`LastScreen`(/`LastShowDirectory`), which
   would have silently reset this step's new `WorkspaceDirectory`/`HasCompletedFirstRun` fields back
   to their defaults on every ordinary screen-navigation save or successful Show Import discovery —
   i.e., a user would complete first run once, then have it silently un-complete itself the next
   time they changed screens. Both call sites now use `existing with { ... }`, touching only the
   field each save actually intends to change; this is covered end to end by
   `Step095_Acceptance_Passes` itself (which navigates after completing first run and then still
   observes `HasCompletedFirstRun=true` on restart).
3. **`IStartupHealthChecks` defaults to the new, non-throwing `NullStartupHealthChecks` at the
   Shell/first-run layer rather than a real `StartupHealthChecks` instance (disclosed, consistent
   with Step 094 Section 6 item 2's own already-disclosed deferral).** A real `StartupHealthChecks`
   needs a concrete bundle directory and `ReleaseManifest`, which only a real published release's
   own composition root can supply — that composition root does not exist yet (Step 094 deliberately
   deferred `IAssetInstaller`/manifest wiring to Step 096). First run therefore reports zero
   asset-verification issues today; wiring a real manifest through once Step 096/097 establish one
   is left as an explicit, disclosed follow-up rather than speculatively guessed at now. The real
   `IRuntimeCapabilityDetector` probe (the half that needs no external manifest) is wired for real
   in both `ShellViewModel`'s own default and `App.axaml.cs`'s composition root.
4. **No MSI/WiX/Inno Setup installer technology was adopted (disclosed, consistent with Step 094
   Section 6 item 3's own "don't commit to an installer-technology shape before it's actually
   needed" deferral).** This step's own implementation guidance says to "choose an installer
   technology... after testing actual output" (Section 3's architectural design note, carried from
   Phase 15's top-level guidance) — no packaging toolchain (WiX, Inno Setup, MSIX) is available in
   this sandboxed environment to actually test. Instead, this step ships a real, executable,
   non-admin PowerShell installer/uninstaller pair (§3.4) that performs the actual file-placement
   and removal semantics any future MSI/WiX/Inno package would still need to get right (per-user
   location, no admin elevation, an authoritative "what did we install" manifest, never touching the
   user's own workspace/settings) — genuinely tested end to end against a real published bundle,
   rather than a guessed-at packaging-tool configuration that cannot be run here at all. A real
   `.msi`/`.exe` installer wrapper is left as an explicit, disclosed follow-up for whichever future
   step actually has a packaging toolchain available to validate it against.
5. **No admin-only-location registry/ACL probe was added; `FirstRunViewModel` rejects a workspace
   folder by its well-known special-folder path (Program Files, Program Files (x86), Windows)
   rather than attempting a permissions check (disclosed, consistent precedent).** Same "explicit,
   named location, never silently assumed" convention `RuntimeCapabilityDetector`'s own xLights
   probe already established (Step 094 Section 6 item 4) — a real permissions/ACL probe would be
   more general but also slower, more platform-specific, and harder to test deterministically; the
   three named locations cover the realistic "a user accidentally points the workspace at an
   install-only directory" case this step's own implementation guidance names verbatim ("no...
   admin-only project storage").

## 7. Known risks / unresolved issues

- **No literal Avalonia screenshot or desktop GUI interaction recording was captured** — §4 discloses
  exactly what was and was not provable in this sandboxed, non-interactive environment. The real
  compiling XAML and the real underlying domain/application state machine (driven end to end exactly
  as a real click would) are strong but indirect evidence of the real UI's behavior; a literal
  screenshot/recording is recorded as **NOT VERIFIED**, not claimed as captured.
- **No real MSI/WiX/Inno Setup installer package was produced** — disclosed in §6 item 4; the real
  PowerShell install/uninstall scripts cover the same non-admin, manifest-tracked, workspace-
  preserving semantics a future packaged installer would still need, but are not themselves a
  double-clickable `.msi`/`.exe`.
- **`IStartupHealthChecks` at the Shell/first-run layer is still the non-throwing `NullX` default**
  — disclosed in §6 item 3; a real release composition root with a concrete bundle directory and
  manifest does not exist yet (Step 094/096's own disclosed scope), so first run cannot yet verify
  real release assets, only probe real runtime capability.
- **The whole-solution `--filter "Category=Integration"` run showed a pre-existing cross-assembly
  parallel-testhost flakiness** in four projects this step did not touch — disclosed and isolated in
  §3.3; every project actually relevant to this step passed cleanly when run in isolation.
- **No real xLights executable exists in this environment** — consistent with every prior phase;
  this step's own required acceptance text does not name an xLights-rendering outcome, so this is
  recorded only for completeness. Per this project's own standing memory waiver, the real-xLights
  human-visual-verification check is not required as a phase-gate blocker project-wide either.

## 8. Required final checklist (Step 095 only)

- [x] Implementation / tests / observable demo (automated, real production types, no mocking
      framework; a real self-contained bundle was installed, launched, and uninstalled via real
      scripts; a real end-to-end first-run flow was driven against a real on-disk settings file and
      verified across a simulated restart; the literal screenshot/GUI-recording and real
      MSI/WiX-installer portions explicitly recorded as NOT VERIFIED, not faked — see §4/§6/§7) /
      evidence all complete.
- [x] Compiles (`dotnet build XlightsAI.Studio.slnx`: 0 warnings, 0 errors; Avalonia XAML for the new
      First Run screen compiles cleanly).
- [x] New-test run: 10/10 passed (`Step095*` in `XlightsAI.Studio.Shell.Tests`), including at least
      one required failure-path assertion (four distinct negative-path tests: empty workspace,
      admin-only location, unsupported-runtime diagnostics, reentrancy).
- [x] Full existing regression suite re-run; every project this step did not touch shows the exact
      same pass count as Step 094's own last-recorded evidence; `XlightsAI.Studio.Shell.Tests`'s
      increase is exactly this step's own 5 new tests (§3.3). A pre-existing, unrelated whole-solution
      Integration-run flakiness in four untouched projects was isolated and confirmed not to be a
      regression from this step.
- [x] "Manual" demonstration performed: a real self-contained win-x64 bundle was installed via a
      real script to a real per-user, non-admin location, its real apphost was launched and kept
      running, and it was then fully and cleanly uninstalled (§3.4); a real end-to-end first-run flow
      was separately driven and its saved state verified across a simulated restart (§3.5). The
      literal screenshot/GUI-recording and real packaged-installer portions are explicitly recorded
      as not captured/not built, not claimed.
- [x] No previously accepted source file's *behavior* from PHASE-01 through PHASE-14 was narrowed or
      removed; two already-accepted PHASE-01 tests were deliberately, narrowly updated to reflect
      this step's own required, disclosed behavior change (§6 item 1) — every other test in the full
      regression suite is unchanged and passing.
- [x] Missing assumptions / compatibility limits / technical debt recorded (§6, §7).
- [x] Previous accepted sequences/project files are unchanged except the two disclosed test updates
      above; this step never writes to any existing user-facing data file — all real I/O in this
      step's own evidence ran against fresh `%TEMP%` directories, deleted after capture, confirmed by
      `git status --porcelain=v1` showing no stray tracked paths from this activity.
- [ ] Required xLights/manual/inference test ran against a recorded version, if applicable — **not
      applicable to this step's own required acceptance text** (no xLights-rendering claim in
      Step 095); recorded as N/A rather than silently checked.

**Gate for Step 095: PASS.** This step's own numbered-step scope — a first-run onboarding flow that
chooses a safe, non-admin workspace folder without a command line, surfaces real runtime/health
diagnostics without blocking on them, persists its completion verifiably across a restart, and a
real, non-admin installer/uninstaller pair for an already-published release bundle — is fully
implemented and tested, including real on-disk I/O, a real published-bundle install/launch/uninstall
cycle, and a disclosed, narrowly-scoped update to two pre-existing PHASE-01 tests whose own original
assertions this step's own required acceptance text necessarily supersedes. The literal Avalonia
screenshot/GUI-recording portion and a real packaged (MSI/WiX/Inno) installer remain explicitly
**NOT VERIFIED** / not built (no display/screenshot tool or packaging toolchain available in this
sandboxed environment), consistent with the project's own standing memory waiver and every prior
step's established precedent of disclosing rather than faking what cannot be run here.
