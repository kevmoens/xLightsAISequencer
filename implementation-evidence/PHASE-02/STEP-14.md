# PHASE 02 — Step 014 Evidence

**Step:** Open output in installed xLights; verify duration, timing, models and visible effects.
**Spec:** [`docs/phase-specs/PHASE-02.md`](../../docs/phase-specs/PHASE-02.md), Section 6, Step 014.
**Date:** 2026-10-10
**Status: BLOCKED** (this step's own required acceptance — "Human-attested integration test with
screenshot/render notes," i.e. a real xLights visual render check — cannot be performed on this
machine; see §1/§9/§10/§11. The automatable UI → application → domain → adapter slice this step also
calls for is fully implemented, tested, and demonstrated below.)

## 1. Baseline check (prior step contracts, unmodified)

Re-ran the full existing suite before starting (PHASE-01 Steps 001-008 + PHASE-02 Steps 009-013, every
prior step):

```text
$ dotnet build XlightsAI.Studio.slnx
Build succeeded. 0 Warning(s) 0 Error(s)

$ dotnet test XlightsAI.Studio.slnx
XlightsAI.Studio.Xlights.Tests.dll:   Passed! Total: 56
XlightsAI.Studio.Shell.Tests.dll:     Passed! Total: 43
XlightsAI.Studio.Projects.Tests.dll:  Passed! Total: 25
XlightsAI.Studio.Reference.Tests.dll: Passed! Total: 31
# 155/155, exactly matching Step 013's own recorded final total.
```

No Step 009-013 (or PHASE-01) production `src/**` type's existing shape was changed or removed — this
step only **adds** new files (`IXlightsLauncher.cs`, `XlightsLaunchResult.cs`,
`XlightsLaunchInvalidException.cs`, `XlightsLauncher.cs`) plus additive extensions to
`ShowImportViewModel.cs`/`ShellViewModel.cs`/`MainWindow.axaml` (new optional constructor parameter
with a safe default, new bindable properties/commands, new UI section) — proven non-breaking by the
unmodified Step 009-013 test suites still passing in full below (§3).

**Real xLights installation:** still none found on this machine. Checked explicitly for this step
(not merely carried forward by assertion):

```text
$ Test-Path "C:\Program Files\xLights"                                   -> False
$ Test-Path "C:\Program Files (x86)\xLights"                             -> False
$ Test-Path "$env:LOCALAPPDATA\Programs\xLights"                         -> False
$ Get-Command xlights -ErrorAction SilentlyContinue                      -> (nothing)
$ Get-ChildItem "C:\Program Files*" -Filter "*xLights*"                  -> (nothing)
$ Get-ItemProperty HKLM:\...\Uninstall\* | Where DisplayName -like *xLights* -> (nothing)
```

This is why this step's own required acceptance — "Human-attested integration test with screenshot/
render notes" (and Section 1's "visually verify timing, props, colors and duration" / Section 7 row 7's
"Gold-standard test opens and visibly renders in installed xLights" / Section 9's copy/paste prompt's
"NEVER CLAIM THE GATE PASSED WITHOUT" line) — cannot be discharged in this environment and is reported
**BLOCKED**, not silently substituted with a mock, per this task's explicit instruction. Everything else
this step's implementation guidance calls for ("Run installed target xLights... Save screenshot/log and
record installed xLights version") is built, tested, and demonstrated below using a disclosed real
stand-in executable — never claimed to be xLights.

## 2. What was built

Per PHASE-02 Section 6 Step 014 ("Run installed target xLights; inspect target props visually or via
supported render adapter. Save screenshot/log and record installed xLights version; do not mark passed
on XML parse alone.") and the required failure/edge case ("Missing or incompatible model, cancellation
and offline mode must yield a recoverable state"):

- **`src/XlightsAI.Studio.Xlights/`** (extends the Step 009-013 project; no new project):
  - `IXlightsLauncher.cs` (new) — the phase-local contract this step needed but which PHASE-02's own
    Section 3 "Proposed C# signatures" does not name (that section stops at `IXsqExporter`). This is a
    genuine, concrete boundary — "start a real OS process for an external application" — not a
    fabricated generic interface (Section 2: "do not fabricate generic interfaces without a concrete
    boundary"). `LaunchAsync(string xlightsExecutablePath, string xsqPath, CancellationToken) ->
    Task<XlightsLaunchResult>`.
  - `XlightsLaunchResult.cs` (new) — `(string ExecutablePath, string? ExecutableVersion, string XsqPath,
    int ProcessId)`. This record is explicitly documented as **launch evidence, not render proof**: it
    proves which real executable/version was started against which real file, never that the file
    rendered correctly — that remains the human's own required acceptance.
  - `XlightsLaunchInvalidException.cs` (new) — the one exception type for every rejected-launch-request
    path (blank/missing executable, blank/missing xsq file, the OS refusing to start the process).
    Always thrown before any process is started.
  - `XlightsLauncher.cs` (new) — the actual adapter, built on `System.Diagnostics.Process.Start` with
    `ArgumentList` (never a hand-built/shell-interpolated argument string, so neither the executable
    path nor the xsq path is a command-injection surface) and `UseShellExecute = false` /
    `CreateNoWindow = true` (suppresses an unwanted console-window flash for a console-style stand-in
    executable; has no effect on a real GUI application like xLights). Validates, in order: executable
    path non-blank and exists; xsq path non-blank and exists; cancellation token not already cancelled
    — each check happens **before** `Process.Start` is ever called, and in that specific order so a
    cancelled request never masks a more actionable "missing executable"/"missing xsq" message behind a
    generic cancellation. Captures the executable's own `FileVersionInfo.FileVersion` on a best-effort
    basis (an unreadable version resource never blocks the launch itself — e.g. an executable with no
    version resource at all still launches, with `ExecutableVersion = null`). Does not wait for the
    launched process to exit, read its output, or inspect its window — Section 2's "An XML parse pass
    does not prove xLights renders an effect" generalizes here to "starting a process does not prove it
    rendered anything either"; the actual visual check is deliberately left to the human, not automated
    away by watching stdout or a window title.
- **`src/XlightsAI.Studio.Shell/ShowImportViewModel.cs`** (modified, additive): new bindable
  `XlightsExecutablePath`/`IsLaunchBusy`/`LaunchErrorMessage`/`LastLaunch`/`HasLaunch` properties and a
  new `LaunchXlightsAsync` method — same busy-guard/try-catch-sets-error-state shape as the existing
  `ExportXsqAsync`/`GenerateEffectPlanAsync`, kept as entirely separate state (a failed launch attempt
  never clobbers an already-successful `LastExport`, and launching never re-runs Export).
  `LaunchXlightsAsync` requires `LastExport` to already be populated (Step 013's own output) — it never
  guesses an xsq path.
- **`src/XlightsAI.Studio.Shell/ShellViewModel.cs`** (modified, additive): one new optional constructor
  parameter (`IXlightsLauncher`, defaulting to a real `new XlightsLauncher()` — same "pure, stateless,
  nothing to configure" reasoning Step 010/012/013 already used for
  `IShowLayoutImporter`/`IThreeEffectPlanFactory`/`IEffectRecipeRegistry`/`IXsqExporter`, since
  `XlightsLauncher` takes both its inputs as per-call arguments rather than needing a configured
  directory) and a new `LaunchXlightsCommand`/`LastLaunchXlightsCommandTask`, wired exactly like
  `ExportXsqCommand`/`LastExportXsqCommandTask`.
- **`src/XlightsAI.Studio.App/Views/MainWindow.axaml`** (modified, additive): new "Open in xLights"
  section on the existing Show Import screen (executable-path textbox, Open in xLights button,
  busy/error text, and the resulting launch's executable path/version/process id) — no existing markup
  removed. No `Program.cs`/`App.axaml.cs` change was needed (unlike Step 012's `ISequenceDocumentStore`),
  since `XlightsLauncher` needs no external configuration to default to a real instance.
- **Tests** (new, no existing test file's assertions touched):
  - `tests/XlightsAI.Studio.Xlights.Tests/Step014AcceptanceTests.cs` — 7 tests against the real
    `XlightsLauncher`, real file-system I/O and a real OS process launch, no mocks.
  - `tests/XlightsAI.Studio.Shell.Tests/Step014ShellWiringTests.cs` — 4 tests proving the real
    `ShellViewModel.LaunchXlightsCommand` end-to-end pathway.

### Why `where.exe` appears in the automated tests and the manual demo

No installed xLights executable exists on this machine (§1). `where.exe` — a real, always-present
Windows system executable at `%SystemRoot%\System32\where.exe` — stands in for "an installed target
executable" purely to exercise the real process-launch plumbing end to end (path validation,
`Process.Start`, process id/version reporting) with a real OS process, not a mock object standing in for
one. **It is explicitly and repeatedly disclosed as not xLights, in the test class doc, the test names,
and this evidence file.** No test or demo step claims `where.exe` proves anything about xLights
rendering; they only prove the launcher adapter itself works against a real, arbitrary, real-pathed
executable. The one thing this cannot exercise — because `where.exe` has no sequencer UI to render into
— is the actual required acceptance text itself, which is why that specific item is reported BLOCKED
rather than marked PASS against this stand-in.

## 3. Commands actually executed

```text
$ dotnet build XlightsAI.Studio.slnx
Build succeeded. 0 Warning(s) 0 Error(s)

$ dotnet test tests\XlightsAI.Studio.Xlights.Tests\XlightsAI.Studio.Xlights.Tests.csproj --filter "FullyQualifiedName~Step014" --logger "console;verbosity=normal"
Total tests: 7. Passed: 7.

$ dotnet test tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj --filter "FullyQualifiedName~Step014" --logger "console;verbosity=normal"
Total tests: 4. Passed: 4.

$ dotnet test XlightsAI.Studio.slnx
XlightsAI.Studio.Xlights.Tests.dll:   Passed! Total: 63   (56 baseline + 7 new Step 014 domain tests)
XlightsAI.Studio.Shell.Tests.dll:     Passed! Total: 47   (43 baseline + 4 new Step 014 shell-wiring tests)
XlightsAI.Studio.Projects.Tests.dll:  Passed! Total: 25   (untouched)
XlightsAI.Studio.Reference.Tests.dll: Passed! Total: 31   (untouched)
# Full, final regression: 166/166 passing (155 baseline + 11 new), 0 regressions.

$ dotnet format XlightsAI.Studio.slnx --verify-no-changes
(no output; exit code 0)
```

All commands were run via PowerShell; the Bash tool continues to fail on every invocation in this
environment (exit 1 / no output, including on a bare `ls`/`find`), consistent with every prior step's
evidence (PHASE-01 Steps 001-008, PHASE-02 Steps 009-013).

## 4. Required acceptance: "Human-attested integration test with screenshot/render notes"

**BLOCKED.** No installed xLights executable exists on this machine (§1), and this requirement is
explicitly, unconditionally about a real xLights render ("do not mark passed on XML parse alone";
Section 2: "An XML parse pass does not prove xLights renders an effect"; Section 7 row 7: "Gold-standard
test opens and visibly renders in installed xLights"). There is no substitute action available that
satisfies this specific text — per this task's own instruction, this is reported BLOCKED rather than
pretending a mock, a stand-in executable, or a structural-only check is equivalent.

**What is proven instead** (the complete automatable slice this step also calls for, "Build the minimum
complete UI -> application -> domain -> data/adapter slice"):

- `Step014_Acceptance_Passes_LaunchesConfiguredExecutableAgainstRealExportedXsq`
  (`tests/XlightsAI.Studio.Xlights.Tests/Step014AcceptanceTests.cs`) builds a real exported `.xsq` via
  the real `XsqExporter` (the same Step 013 pathway), then calls the real `XlightsLauncher.LaunchAsync`
  against the real `where.exe` stand-in and the real exported file, asserting the real reported
  `ExecutablePath`/`XsqPath` match exactly and a real (> 0) OS process id was returned.
- `Step014_Acceptance_Passes_LaunchXlightsEndToEnd`
  (`tests/XlightsAI.Studio.Shell.Tests/Step014ShellWiringTests.cs`) runs the **exact phase gate demo
  shape** through the real `ShellViewModel`: Discover → Generate 3 Effects → Export XSQ → Open in
  xLights, then asserts `ShowImport.LastLaunch` is populated with the real process id/path and the
  screen is left in a non-busy, non-error state — the full UI → application → domain → adapter slice,
  not a stubbed collaborator.
- §6 below repeats this exact workflow through the real, running Avalonia desktop application via
  Windows UI Automation, with a screenshot — real UI evidence of the slice, explicitly short of the
  blocked xLights-render requirement.

## 5. Required failure/edge case: "Missing or incompatible model, cancellation and offline mode must yield a recoverable state"

### `tests/XlightsAI.Studio.Xlights.Tests/Step014AcceptanceTests.cs` (7 new tests, real file-system I/O and real process launches, no mocks)

```text
Passed Step014_Acceptance_Passes_LaunchesConfiguredExecutableAgainstRealExportedXsq
Passed MissingExecutable_FailsSafely_NegativePath
Passed MissingXsqFile_FailsSafely_NegativePath
Passed BlankExecutablePath_FailsSafely_NegativePath(rawPath: "")
Passed BlankExecutablePath_FailsSafely_NegativePath(rawPath: "   ")
Passed BlankXsqPath_FailsSafely_NegativePath
Passed Cancellation_BeforeLaunch_FailsSafely_NegativePath

Total: 7. Passed: 7.
```

- **"Missing or incompatible model"**: `MissingExecutable_FailsSafely_NegativePath` proves a configured
  executable path that does not exist (this environment's own actual, real state — no xLights install)
  throws `XlightsLaunchInvalidException` naming the missing path, with no process ever started.
  `MissingXsqFile_FailsSafely_NegativePath` proves the complementary case — a Step 013 export that was
  never run (or whose file was since removed) is also rejected by name, not silently ignored.
  `BlankExecutablePath_FailsSafely_NegativePath`/`BlankXsqPath_FailsSafely_NegativePath` cover
  blank/whitespace inputs.
- **"Cancellation"**: `Cancellation_BeforeLaunch_FailsSafely_NegativePath` proves a pre-cancelled token
  is honored (the real BCL `OperationCanceledException` propagates) strictly before `Process.Start`
  could ever be reached in the method body — no process is started for a cancelled request.
- **"...must yield a recoverable state"**: proven at the shell layer (§ below), not merely "doesn't
  crash" — `LaunchWithMissingExecutable_SetsScreenError_NegativePath` specifically re-attempts the
  launch immediately after a failure with a corrected path and proves it still succeeds, i.e. the
  failure never leaves the screen stuck.
- **"Offline mode"**: `XlightsLauncher` makes no network call of any kind (local `File.Exists`/
  `FileVersionInfo`/`Process.Start` only) — there is nothing in this adapter's own behavior that an
  offline environment changes, so this edge case is satisfied by construction rather than by a
  dedicated test; recorded here rather than silently assumed.

### `tests/XlightsAI.Studio.Shell.Tests/Step014ShellWiringTests.cs` (4 new tests, real file-system I/O and real process launches, no mocks)

```text
Passed Step014_Acceptance_Passes_LaunchXlightsEndToEnd
Passed LaunchWithoutAnExportYet_SetsScreenError_NegativePath
Passed LaunchWithMissingExecutable_SetsScreenError_NegativePath
Passed ShellViewModel_WithoutExplicitXlightsLauncher_StillLaunchesForReal_BackwardCompatible

Total: 4. Passed: 4.
```

- `LaunchWithoutAnExportYet_SetsScreenError_NegativePath` proves opening in xLights before Export XSQ
  (Step 013) has ever populated `LastExport` fails safely with an explicit screen error, never an
  unhandled exception or a guessed/invented xsq path.
- `LaunchWithMissingExecutable_SetsScreenError_NegativePath` is this step's own explicit "recoverable
  state" proof: after Discover → Generate → Export, launching against a missing executable sets
  `LaunchErrorMessage` and resets `IsLaunchBusy` to `false` (not stuck busy) — then, **without
  reconstructing the screen**, correcting `XlightsExecutablePath` to the real `where.exe` stand-in and
  retrying succeeds, proving the failed attempt left no corrupted or stuck state behind.
- `ShellViewModel_WithoutExplicitXlightsLauncher_StillLaunchesForReal_BackwardCompatible` proves every
  earlier step's existing `ShellViewModel` call-site shape (no explicit launcher argument) still
  compiles and now really launches (via the real default `XlightsLauncher` instance), not a
  silently-succeeding fake.

### Pre-existing regression (unmodified acceptance text)

```text
XlightsAI.Studio.Xlights.Tests: 56/56 pre-existing Step009-013 tests passing, unmodified.
XlightsAI.Studio.Shell.Tests:   43/43 pre-existing tests (Step 004-013) passing, unmodified.
XlightsAI.Studio.Projects.Tests: 25/25 passing, untouched by this step.
XlightsAI.Studio.Reference.Tests: 31/31 passing, untouched by this step.
# Full regression with this step's new tests included: 166/166, 0 regressions.
```

No PHASE-01 or Step 009-013 test file's assertions were edited. No prior-step production type's existing
public shape was changed or removed; `ShowImportViewModel`/`ShellViewModel` only gained new members, and
`MainWindow.axaml` only gained new additive wiring.

## 6. Manual / desktop UI verification — real Avalonia app, real file system, real OS process launch

Per the task's "Build the minimum complete UI -> application -> domain -> data/adapter slice" and "run
... xLights or native inference manual tests when this step requires them. If an external environment is
unavailable, label the real integration test BLOCKED instead of pretending a mock is equivalent"
instructions, the real compiled desktop app
(`src\XlightsAI.Studio.App\bin\Debug\net10.0\XlightsAI.Studio.exe`, started via `Start-Process` — the
exact same executable a user double-clicks) was launched for real and driven via Windows UI Automation
(`System.Windows.Automation`, real `InvokePattern`/`ValuePattern` calls against the real running window —
not simulated input, not a test harness substitute). A fixture show directory was created directly under
`C:\temp\xlai-step014-demo-show` (outside the repository, same convention every prior PHASE-02 step's
manual demo used) using the phase gate's own exact prop names — byte-identical to Step 012/013's own
fixture:

```xml
<xrgb FormatVersion="2" BaseChannel="0">
  <models>
    <model name="Roofline" DisplayAs="Star" WorldPosX="0" WorldPosY="5" />
    <model name="Arch 1" DisplayAs="Arches" WorldPosX="-3" WorldPosY="0" />
    <model name="Arch 2" DisplayAs="Arches" WorldPosX="3" WorldPosY="0" />
  </models>
  <modelGroups>
    <modelGroup name="Front-Arches" models="Roofline,Arch 1,Arch 2" />
  </modelGroups>
</xrgb>
```

SHA256 of `C:\temp\xlai-step014-demo-show\xlights_rgbeffects.xml`:
`792C35E91EFBB345D5C7A7D37A4651D19DC4FF1402BCEC8E2DCDE56820971937` (matches Step 013's own recorded hash
for the same content exactly — same fixture reused for continuity of the gate-demo workflow).

1. **Full workflow + success-path launch** —
   `implementation-evidence/PHASE-02/step-014-launch-xlights-success.png`: navigated to Show Import via
   a real `InvokePattern.Invoke()`, set the real show-directory textbox to the fixture above, clicked
   **Discover** (real parsed `Roofline`/`Arch 1`/`Arch 2`/`Front-Arches`), set "Three real props" to
   `Roofline, Arch 1, Arch 2` and clicked **Generate 3 Effects** (real 3-effect plan), set the **Output
   path** to `C:\temp\xlai-step014-demo-show\exports\sequence.xsq` and clicked **Export XSQ** (real
   SHA256 `051C1334E322278E7C049B792FFF2324B2C90FD02B908428196A20463742DCD2` — same exact bytes/hash as
   Step 013's own demo, since the same fixture/plan shape was used), then set the new **Installed
   xLights executable path** textbox to `C:\WINDOWS\System32\where.exe` (the disclosed real stand-in;
   see §2) and clicked the real **Open in xLights** button. The screen shows a real result
   (`Launched: C:\WINDOWS\System32\where.exe`, `Reported version: 10.0.26100.1 (WinBuild.160101.0800)`,
   `Process id: 122216`) — real, end-to-end UI evidence of the full slice through a real OS process
   launch, not a blank screen or a crash. The spawned process was independently confirmed running
   (`Get-Process -Id 122216`) before being cleaned up at the end of this manual session (disclosed in
   §10 below as a side effect, same as every prior step's own file-write disclosures).
2. **Negative path** —
   `implementation-evidence/PHASE-02/step-014-launch-xlights-missing-executable-error.png`: with the
   same exported sequence still in hand, the **Installed xLights executable path** textbox was changed
   to `C:\Program Files\xLights\xLights.exe` (the real, standard install location — genuinely absent on
   this machine, §1) and **Open in xLights** was clicked again. The screen shows the real red error
   line: `No xLights executable was found at 'C:\Program Files\xLights\xLights.exe'. Install the target
   xLights version (or correct the configured path) before opening a sequence for real integration
   verification.` — not a crash, not a silently-ignored click. This is simultaneously the required
   negative-path demonstration *and* an honest, live illustration of exactly why §4's required
   acceptance is BLOCKED in this specific environment.

**Disclosed side effect**: this manual verification wrote real files under
`C:\temp\xlai-step014-demo-show\exports\` (one `sequence.xsq`, byte-identical to Step 013's own) and
under this machine's own `%AppData%\XlightsAI.Studio\exports\` (one additional `sequence-plan-*.json`
from the Generate step, same precedent as every prior step's own disclosure) — neither is part of the
committed repository. It also started and later terminated one real `where.exe` process (pid 122216) —
`where.exe` itself completes its own (irrelevant, ignored) search near-instantly and does not persist
running; the explicit `Stop-Process` was a defensive cleanup, not a response to a hang.

## 7. Fixture hashes

```text
C:\temp\xlai-step014-demo-show\xlights_rgbeffects.xml (manual-demo fixture, outside the repository)
  SHA256=792C35E91EFBB345D5C7A7D37A4651D19DC4FF1402BCEC8E2DCDE56820971937 (identical content to Step 012/013's own fixture)

C:\temp\xlai-step014-demo-show\exports\sequence.xsq (real app output, manual verification)
  SHA256=051C1334E322278E7C049B792FFF2324B2C90FD02B908428196A20463742DCD2 (identical to Step 013's own recorded export, since the same fixture/plan was used)
```

This step's automated tests (§4/§5) build their `DisplayGraph`/`SequenceDocument` fixtures in-memory via
the real `XsqExporter` (no new checked-in XML fixture file was needed), so there is no new
`tests/.../Fixtures/*.xml` hash table analogous to Step 010/011's.

## 8. Changed/added paths

```
src/XlightsAI.Studio.Xlights/IXlightsLauncher.cs                                 (new)
src/XlightsAI.Studio.Xlights/XlightsLaunchResult.cs                              (new)
src/XlightsAI.Studio.Xlights/XlightsLaunchInvalidException.cs                    (new)
src/XlightsAI.Studio.Xlights/XlightsLauncher.cs                                  (new)
src/XlightsAI.Studio.Shell/ShowImportViewModel.cs                                (modified: +XlightsExecutablePath/+IsLaunchBusy/+LaunchErrorMessage/+LastLaunch/+HasLaunch/+LaunchXlightsAsync)
src/XlightsAI.Studio.Shell/ShellViewModel.cs                                     (modified: +1 optional ctor param, +LaunchXlightsCommand, +LastLaunchXlightsCommandTask)
src/XlightsAI.Studio.App/Views/MainWindow.axaml                                  (modified: +Open in xLights section)
tests/XlightsAI.Studio.Xlights.Tests/Step014AcceptanceTests.cs                   (new; 7 tests)
tests/XlightsAI.Studio.Shell.Tests/Step014ShellWiringTests.cs                    (new; 4 tests)
implementation-evidence/PHASE-02/STEP-14.md                                      (new, this file)
implementation-evidence/PHASE-02/step-014-launch-xlights-success.png             (new)
implementation-evidence/PHASE-02/step-014-launch-xlights-missing-executable-error.png (new)
```

No PHASE-01 file, no Step 009-013 production file's existing members, and no Step 009-013 test file's
assertions were edited or removed.

## 9. Architecture deviations

- **`IXlightsLauncher`/`XlightsLaunchResult`/`XlightsLaunchInvalidException` are new types this step
  introduces that PHASE-02 Section 3's "Proposed C# signatures" does not name.** Section 3 stops at
  `IXsqExporter`/`ExportResult` (Step 013's own scope); Step 014's own implementation guidance ("Run
  installed target xLights... Save screenshot/log and record installed xLights version") names a
  concrete, narrow boundary — starting one specific external process with one specific argument — that
  has no existing phase-local contract to reuse. This is deliberately **not** a generic
  "IExternalProcessRunner" or similar (Section 2: "do not fabricate generic interfaces without a
  concrete boundary"); the interface is shaped exactly around this one real need (an xLights executable
  path and an xsq path in, a launch result or a named failure out).
- **The "On" effect's xLights-rendered correctness is still, and remains, unverified.** This step cannot
  resolve the gap Step 013 disclosed (an empty `EffectDB` settings placeholder) because resolving it
  requires the same missing xLights install this step is itself blocked on (Section 12's own "golden
  fixture process": capture a real xLights-saved sample). Step 014 does not attempt to work around this
  by guessing — it surfaces the blocker honestly instead (§1/§4/§10).
- **`where.exe` is used as a real stand-in executable in both automated tests and the manual demo,
  never as a claim that it is or resembles xLights.** This was a deliberate choice over two
  alternatives considered and rejected: (a) mocking `IXlightsLauncher` itself in the shell-wiring tests
  — rejected because it would prove nothing about the real `Process.Start` plumbing, exactly the "not
  only a mock invocation" instruction this task gives; (b) launching a real GUI application (e.g.
  `notepad.exe`) — rejected because it would leave a visible, user-facing window open for the duration
  of a CI/test run with no reliable, fast way to close it, where `where.exe` is a console utility that
  exits itself almost immediately and never shows a window (`CreateNoWindow = true`).
- **`CreateNoWindow = true` was added to `ProcessStartInfo`.** Not named in PHASE-02's own Section 3
  signatures (there is no process-launch adapter specified there at all, see above) but a deliberate,
  disclosed choice: it suppresses an unwanted console-window flash for a console-style stand-in
  executable (`where.exe`, and this test suite's own CI runs) and has zero effect on a real GUI
  application like xLights, which never allocates a console window regardless of this flag.
- **No `IXlightsLauncher` Null-object fallback.** Unlike `ISequenceDocumentStore` (needs a configured
  output directory), `XlightsLauncher` takes both its inputs as per-call arguments and needs no external
  configuration, so `ShellViewModel` defaults it to a real instance directly — the same reasoning Step
  010/012/013 already recorded for `IShowLayoutImporter`/`IThreeEffectPlanFactory`/
  `IEffectRecipeRegistry`/`IXsqExporter`, applied consistently here rather than introducing a different
  convention for a fifth "pure, stateless" collaborator.

## 10. Unresolved issues / risks carried forward

1. **No installed, runnable xLights executable was available on this machine** — this step's own
   blocking dependency, now directly and explicitly realized rather than merely anticipated (§1/§4).
   This step's required acceptance — "Human-attested integration test with screenshot/render notes" —
   is reported **BLOCKED**, not PASS, and not silently satisfied by a mock or stand-in. Resolving this
   requires either installing a target xLights build on a machine this agent can drive, or a human
   operator performing the check manually using the real exported `.xsq`
   (`C:\temp\xlai-step014-demo-show\exports\sequence.xsq`, SHA256 recorded in §7) and reporting back
   observed timing/props/colors/duration.
2. **The "On" effect's `EffectDB` settings remain the same empty, explicitly-disclosed placeholder Step
   013 introduced** (§9) — still blocked on the same missing xLights install. Even once an xLights
   install becomes available, Step 014's own required visual check and Section 12's golden-fixture
   capture are the same underlying blocked action; completing one substantially informs the other.
3. **`XlightsLauncher` does not wait for the launched process to exit, nor inspect its window/output in
   any way.** This is by design (§2/§9: starting a process does not prove it rendered anything), but it
   also means nothing in this codebase can detect "xLights launched but immediately crashed" versus
   "xLights launched and is running normally" — that distinction is left entirely to the human performing
   the required visual check, consistent with this step's own required-acceptance text naming a human
   attestation, not an automated render-detection mechanism.
4. **Atomic/collision-suffix/backup export handling remains only partially built** (carried forward from
   Step 013's own §10 item 2) — still Step 015's own named scope, not duplicated or started here.
5. **`xlights_networks.xml` is still not parsed** (carried forward from Step 011's own §10 item 2) —
   unaffected by this step's own scope.
6. **This step's manual verification wrote real files** under `C:\temp\xlai-step014-demo-show\exports\`
   and this machine's own `%AppData%\XlightsAI.Studio\exports\`, and started/stopped one real
   `where.exe` process (§6) — same precedent as every prior step's own disclosure, carried forward again
   here; none of the files are part of the committed repository.
7. Git Bash remains non-functional in this environment (carried forward from every prior step); every
   command in this evidence was run via PowerShell.
8. **Step 015 (atomic export, filename safety, backups, golden XSQ regression tests) is the natural next
   step** — not started by this step, per its own narrow scope and per this task's explicit instruction
   not to execute the next step until this one is proven. Step 015's own required acceptance text does
   not itself require a real xLights render ("no regression in fixture output"), so it is not expected to
   inherit this step's own BLOCKED status, but that determination belongs to Step 015's own evidence, not
   this one.

## 11. Gate checklist (this step only)

- [x] Ran `dotnet build`/`dotnet test` for the full existing suite before starting; 155/155 passing,
      matching Step 013's own recorded final total exactly (§1, §3).
- [x] Checked explicitly (not merely asserted from precedent) for an installed xLights executable on
      this machine via multiple standard install-location/registry/PATH checks; none found (§1).
- [x] Implements the smallest complete UI → application → domain → adapter slice this step's own
      implementation guidance calls for: a real `IXlightsLauncher`/`XlightsLauncher` adapter, wired
      through `ShowImportViewModel`/`ShellViewModel`, with a real UI section (§2, §8).
- [x] Turned the step's acceptance text into executable tests: `Step014_Acceptance_Passes_*` tests at
      both the domain (`Xlights.Tests`) and shell-wiring (`Shell.Tests`) layers (§4, §5).
- [x] At least one failure-path assertion per layer, and specifically the named failure/edge case text
      ("Missing or incompatible model, cancellation and offline mode must yield a recoverable state") —
      7 domain-level negative tests plus 2 shell-level negative tests, including an explicit
      fail-then-recover-without-reconstructing-the-screen proof (§5).
- [x] Full regression suite re-run, final: 166/166 passing (155 PHASE-01/Step009-013 baseline + 11 new
      Step 014 tests), zero regressions (§3, §5).
- [x] `dotnet format --verify-no-changes` passes cleanly against every new and modified file (§3).
- [x] No original/show file or PHASE-01 file touched; no Step 009-013 test *assertion* touched — every
      prior-step production file edited gained only new, additive members (§5, §8).
- [x] Never overwrites source show files or any pre-existing export target — this step performs no
      file-writing of its own at all (only process-launch); the exported `.xsq` it opens was already
      produced, unmodified, by Step 013's own exporter (§2, §6).
- [x] Real UI demonstration performed (not simulated/mocked) — two screenshots via real Windows UI
      Automation against the real running app: the full phase-gate workflow through a real successful
      launch of a disclosed real stand-in executable, and an explicit, honest missing-executable
      negative path using the real standard xLights install location (§6).
- [x] **The real xLights open/render check itself was explicitly NOT claimed as performed or passed.**
      No installed xLights executable exists on this machine; this task's own instruction — "If an
      external environment is unavailable, label the real integration test BLOCKED instead of
      pretending a mock is equivalent" — is followed exactly: this step's status is **BLOCKED**, not
      PASS, and every test/demo above is clearly scoped to what it actually proves (process-launch
      plumbing) versus what it does not (xLights rendering) (§1, §4, §6, §9, §10).
- [x] Architecture deviations disclosed in full (§9), per this task's explicit instruction.

**Step 014 status: BLOCKED** — scoped precisely: the automatable UI → application → domain → adapter
slice (real process-launch plumbing, full positive/negative test coverage, a real desktop UI
demonstration) is complete and PASSING; the step's own required acceptance text — a human-attested real
xLights visual render check — cannot be performed on this machine and is reported BLOCKED, not silently
satisfied by a mock or a stand-in executable. Step 015 is not started, as instructed ("do not execute the
next step until this step is proven").

## 12. Explicit scoped waiver (user-authorized, 2026-10-10)

The user explicitly instructed this agent to skip the human-attested real-xLights visual-render
requirement as a condition for this step's/phase's gate, invoking PHASE-02.md Section 8's own escape
hatch: **"Next phase begins only after PASS or an explicit scoped architecture waiver."**

- **What is waived:** the single remaining blocker named in §4/§10/§11 above — a human operator opening
  the exported `.xsq` in a real installed xLights build and visually confirming timing/props/colors/
  duration. Nothing else about this step is waived; every other required acceptance/failure-path item
  (§4, §5, §6, §11) was already independently proven, not waived.
- **What is NOT claimed:** this waiver does not assert that the real xLights render was observed, that
  it would succeed, or that the "On" effect's still-placeholder `EffectDB` settings (§9/§10 item 2) are
  verified correct. The underlying technical limitation — no xLights installation exists on this
  development machine (§1, re-checked via Program Files/registry/PATH, all negative) — is unchanged.
- **What still stands, unwaived:** the real `IXlightsLauncher`/`XlightsLauncher` process-launch adapter
  (§2) continues to exist and is fully tested specifically so the real check can be performed the moment
  an xLights install becomes available on a machine this agent (or a human) can drive — the waiver covers
  "proceed without the check now," not "stop building the capability to perform it."
- **Scope of the waiver going forward:** per the user's own "again" (indicating this is a standing,
  recurring instruction rather than a one-off for this step), this same waiver is treated as carrying
  forward to later phases' own equivalent real-xLights-render gate items, unless the user says otherwise.
  Each future phase's evidence should still explicitly disclose this waiver rather than silently omitting
  the gate item.
- **Revised step status with waiver applied: PASS (waived)** — distinct from an unwaived PASS (the real
  render check was never performed) and distinct from the unwaived BLOCKED recorded above (§4/§11, left
  intact as the honest technical finding this waiver is layered on top of, not a replacement for it).
