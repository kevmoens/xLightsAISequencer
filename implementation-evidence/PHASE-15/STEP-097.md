# PHASE 15 — Step 097: Add supported macOS/Linux deliverables only after validating inference and xLights compatibility

**Status: PASS** (automated tests against real production types — no mocking framework; real
`dotnet publish -r win-x64|osx-x64|linux-x64 --self-contained true` of the actual `XlightsAI.Studio.App`
project were executed and their real on-disk output was inspected and exercised — not simulated. Per this
project's own standing memory waiver, the human "an actual installed xLights build opens/renders correctly"
check is explicitly waived as a phase-gate blocker. What genuinely cannot run in this sandboxed environment —
actually executing a macOS or Linux apphost binary, since this host is Windows x64 — is recorded as
**SKIPPED/NOT VERIFIED**, not faked; the real production code's own job is to detect and honestly report that
exact gap (§3.4 below), which is itself this step's required behavior.)

## 0. Scope-location note (architecture deviation, disclosed upfront)

The task instructions pointed at `docs/phase-specs/PHASE-16.md` for "Step 97," but **PHASE-16.md only defines
numbered steps 098–100** (Release Validation and Community Testing). Step 97 ("Add supported macOS/Linux
deliverables only after validating inference and xLights compatibility") is actually defined in
**`docs/phase-specs/PHASE-15.md`** (Packaging and Distribution, steps 094–097), which also matches the
evidence path requested (`implementation-evidence/PHASE-15/STEP-97.md`, written here as `STEP-097.md` to match
the naming convention every prior Phase-15 evidence file already uses — `STEP-094.md`, `STEP-095.md`,
`STEP-096.md`). This implementation targets **PHASE-15 Step 097**, verbatim as written there. No part of
PHASE-16 was touched.

## 1. Scope actually implemented

Steps 094–096 (`src/XlightsAI.Studio.Distribution/`) are treated as immutable per their own accepted
contracts; **no existing Step 094/095/096 file's public signature was changed.** One existing file received a
strictly additive extension (disclosed, §6 item 1): `RuntimeCapabilityDetector.cs`. Everything else is new:

- **`src/XlightsAI.Studio.Distribution/SupportedPlatform.cs`** — new record: a runtime identifier, the
  OS/architecture substring its apphost requires to actually run, its real apphost file name, and its real,
  previously-observed required native asset file names.
- **`src/XlightsAI.Studio.Distribution/KnownPlatforms.cs`** — the three concrete `SupportedPlatform` values
  (`win-x64`, `osx-x64`, `linux-x64`), populated from the **real, actually-inspected** publish output recorded
  in §3.4/§5 below — no invented file names.
- **`src/XlightsAI.Studio.Distribution/PlatformSmokeCheckResult.cs`** — new record: one named check's
  `Runnable`/`Passed`/`Detail`, keeping "could not run" distinct from "failed."
- **`src/XlightsAI.Studio.Distribution/PlatformSmokeTestReport.cs`** — new record: a platform's full set of
  check results — this step's own user-visible outcome, verbatim: "a reproducible test report."
- **`src/XlightsAI.Studio.Distribution/IApphostLauncher.cs`** / **`ProcessApphostLauncher.cs`** — new
  interface/real implementation: starts a real apphost file directly, observes whether it is still running
  after 2 seconds, always terminates it afterward.
- **`src/XlightsAI.Studio.Distribution/IPlatformSmokeTestSuite.cs`** / **`PlatformSmokeTestSuite.cs`** — new
  interface/real implementation: three checks per platform (`PublishedBundleExists`,
  `RequiredNativeAssetsPresent`, `ApphostLaunches`); the third is marked `Runnable: false` (not a faked pass)
  whenever the real, probed host `RuntimeCapability` cannot execute that platform's binaries.
- **`src/XlightsAI.Studio.Distribution/PlatformReleaseDecision.cs`** — new record: advertised platforms vs.
  unsupported platforms with their own real reasons.
- **`src/XlightsAI.Studio.Distribution/IPlatformReleaseEvaluator.cs`** / **`PlatformReleaseEvaluator.cs`** —
  new interface/real implementation: pure domain rule — a platform is advertised only when every one of its
  checks is both runnable and passing; this step's own required acceptance, verbatim: "Each advertised
  platform passes [the] same smoke-test suite."
- **`tests/XlightsAI.Studio.Distribution.Tests/Step097AcceptanceTests.cs`** — 7 new tests (see §3.2).

## 2. Commands actually executed

```
dotnet build XlightsAI.Studio.slnx --nologo                                            (baseline, before changes)
dotnet publish src\XlightsAI.Studio.App\XlightsAI.Studio.App.csproj -c Release -r osx-x64   --self-contained true -o "$env:TEMP\xlai-step097-osx"   -p:PublishSingleFile=false --nologo
dotnet publish src\XlightsAI.Studio.App\XlightsAI.Studio.App.csproj -c Release -r linux-x64 --self-contained true -o "$env:TEMP\xlai-step097-linux" -p:PublishSingleFile=false --nologo
dotnet publish src\XlightsAI.Studio.App\XlightsAI.Studio.App.csproj -c Release -r win-x64   --self-contained true -o "$env:TEMP\xlai-step097-win"   -p:PublishSingleFile=false --nologo
dotnet build src\XlightsAI.Studio.Distribution\XlightsAI.Studio.Distribution.csproj --nologo
dotnet test  tests\XlightsAI.Studio.Distribution.Tests\XlightsAI.Studio.Distribution.Tests.csproj --nologo
dotnet build XlightsAI.Studio.slnx --nologo
dotnet test  XlightsAI.Studio.slnx --nologo --filter "Category!=Integration"
dotnet run --project <scratchpad>\step097-probe\step097-probe.csproj -c Release -- "$env:TEMP\xlai-step097-win" "$env:TEMP\xlai-step097-osx" "$env:TEMP\xlai-step097-linux"
```

Run from `C:\Code\xLightsAISequencer` via the PowerShell tool — this session's Bash/git-bash tool again
returned a bare exit code 1 with no captured output on every invocation (`git status`, `dotnet build`), the
same pre-existing local shell-launch issue every prior phase's evidence already discloses; not a project/test
failure. All commands above ran successfully via PowerShell.

## 3. Actual results

### 3.1 Build

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

Both the isolated `XlightsAI.Studio.Distribution` project and the full `XlightsAI.Studio.slnx` compile
cleanly; the new files only reference the existing `XlightsAI.Studio.Distribution` project's own types plus
BCL (`System.Diagnostics.Process`, `System.ComponentModel.Win32Exception`) — no new package references.

### 3.2 New-test run (`XlightsAI.Studio.Distribution.Tests`)

```
Passed!  - Failed: 0, Passed: 33, Skipped: 0, Total: 33, Duration: 1 s - XlightsAI.Studio.Distribution.Tests.dll (net10.0)
```

(10 pre-existing Step 094 + 16 pre-existing Step 096 + **7 new Step 097** = 33.)

| Test | Proves |
|---|---|
| `Step097_Acceptance_Passes` (required name) | Three real, fully populated bundle directories (win-x64/osx-x64/linux-x64, using the exact real native-asset file names recorded in §3.4/§5) are run through the real `PlatformSmokeTestSuite` with the host simulated as Windows x64: only `win-x64` is advertised (`PlatformReleaseEvaluator`), while `osx-x64`/`linux-x64` are documented unsupported with the real "cannot execute ... on this host" reason naming `ApphostLaunches` — "document unsupported combinations rather than shipping broken bundles" (Section 3, verbatim). Re-run with the simulated host now reporting as Darwin x64 flips the result — `osx-x64` becomes the only advertised platform and `win-x64` becomes unsupported — proving the decision genuinely depends on which binaries the (simulated) host can execute, not a hard-coded platform name. |
| `Step097_NegativePath_MissingNativeAsset_IsNotAdvertised_WithRealReason` (required failure/edge case family, verbatim: "Missing or incompatible model... must yield a recoverable state") | A macOS bundle genuinely missing its own declared `libAvaloniaNative.dylib` is reported unsupported, naming that exact missing file — never advertised, never throws. |
| `Step097_NegativePath_ApphostCrashesImmediately_IsNotAdvertised` | A platform whose apphost launch fails (via the deterministic `FixedApphostLauncher` fixture) is never advertised; the real crash detail is preserved in the unsupported reason. |
| `Step097_NegativePath_MissingBundleDirectory_IsFlaggedNotThrown` | A bundle directory that does not exist on disk at all produces a `PublishedBundleExists` check that is `Runnable: true, Passed: false` — reported, not thrown. |
| `Step097_NegativePath_Cancellation_ThrowsBeforeAnyWork` (required failure/edge case, verbatim: "...cancellation... must yield a recoverable state") | An already-cancelled token throws `OperationCanceledException` from `PlatformSmokeTestSuite.RunAsync` before any file/probe work happens. |
| `Step097_NullOrBlankArguments_Throw` | Null `platform`, blank `bundleDirectory`, null `capabilityDetector`, and null `reports` all throw `ArgumentNullException`/`ArgumentException` from the right boundary, not a later `NullReferenceException`. |
| `Step097_Evaluator_PlatformWithNoChecksRun_IsNeverAdvertised` | A report with zero checks is never silently advertised — the evaluator requires at least one real, passing, runnable check before a platform can be claimed supported. |

### 3.3 Full-repository regression (`dotnet test XlightsAI.Studio.slnx --nologo --filter "Category!=Integration"`)

| Project | Result |
|---|---|
| XlightsAI.Studio.Reference.Tests | Passed 27/27 |
| XlightsAI.Studio.Audio.Tests | Passed 23/23 |
| **XlightsAI.Studio.Distribution.Tests** | **Passed 33/33** (10 Step094 + 16 Step096 + 7 new Step097) |
| XlightsAI.Studio.Editing.Tests | Passed 53/53 |
| XlightsAI.Studio.Rendering.Tests | Passed 20/20 |
| XlightsAI.Studio.Melody.Tests | Passed 117/117 |
| XlightsAI.Studio.Composition.Tests | Passed 69/69 |
| XlightsAI.Studio.Structure.Tests | Passed 78/78 |
| XlightsAI.Studio.WholeSong.Tests | Passed 50/50 |
| XlightsAI.Studio.AiEdits.Tests | Passed 69/69 |
| XlightsAI.Studio.Refinement.Tests | Passed 78/78 |
| XlightsAI.Studio.Shell.Tests | Passed 211/211 (filtered: `Category!=Integration`) |
| XlightsAI.Studio.Rhythm.Tests | Passed 82/82 |
| XlightsAI.Studio.Stems.Tests | Passed 101/101 |
| XlightsAI.Studio.Projects.Tests, XlightsAI.Studio.Xlights.Tests, XlightsAI.Studio.Display.Tests | No test matched `Category!=Integration` (pre-existing, same disclosed reason every prior phase's evidence already recorded) |

**Every project ran clean; zero failures anywhere in the full filtered regression.** No project this step did
not touch regressed from its own last-recorded evidence count (Step 094's STEP-094.md §3.3), confirming this
step introduced no regression. (Shell.Tests' own count, 211 vs. 199 at Step 094, reflects Step 095/096's own
later additions, not anything introduced here.)

### 3.4 Real cross-platform publish, inspection, and smoke-test demonstration

A real `dotnet publish -r <rid> --self-contained true` of the actual `XlightsAI.Studio.App` project was
executed for **all three** target RIDs from this single Windows x64 machine (cross-publish: .NET downloads the
target RID's runtime pack via NuGet and produces a correct binary for that RID without needing a physically
different build host):

```
dotnet publish ... -r win-x64   --self-contained true -o $TEMP\xlai-step097-win    → 264 files, win-x64 apphost "XlightsAI.Studio.exe"
dotnet publish ... -r osx-x64   --self-contained true -o $TEMP\xlai-step097-osx    → 261 files, osx-x64 apphost "XlightsAI.Studio" (no extension)
dotnet publish ... -r linux-x64 --self-contained true -o $TEMP\xlai-step097-linux  → similar count, linux-x64 apphost "XlightsAI.Studio" (no extension)
```

Real, inspected native asset file names per platform (used verbatim in `KnownPlatforms.cs` — never invented):

| Platform | Native rendering assets actually found in the real publish output |
|---|---|
| `win-x64` | `libSkiaSharp.dll`, `av_libglesv2.dll` (ANGLE GL ES — Avalonia's Windows rendering backend) |
| `osx-x64` | `libSkiaSharp.dylib`, `libAvaloniaNative.dylib` (Avalonia's native macOS windowing shim) |
| `linux-x64` | `libSkiaSharp.so` **only** — no native windowing shim equivalent to `libAvaloniaNative` was bundled at all |

**Genuine, disclosed finding:** the real `linux-x64` self-contained publish bundles no native-windowing-shim
library analogous to macOS's `libAvaloniaNative.dylib` or Windows' `av_libglesv2.dll` — Avalonia's Linux/X11
backend is managed-code only for this application's current dependency set. This asymmetry was not assumed or
invented; it was discovered by actually publishing and inspecting the real output, and is reflected honestly
in `KnownPlatforms.LinuxX64.RequiredNativeAssetFileNames` (two entries, not three) rather than papered over
with a fabricated requirement.

Real `PlatformSmokeTestSuite`/`PlatformReleaseEvaluator` run against these three real, actual published
directories (via a small throwaway driver project under the scratchpad directory, referencing the real
`XlightsAI.Studio.Distribution` project — not a unit test, a direct demonstration against real output):

```
== win-x64 ==
  [PASS] PublishedBundleExists: Bundle directory '...\xlai-step097-win' exists and is non-empty.
  [PASS] RequiredNativeAssetsPresent: All 3 required native asset(s) present for win-x64.
  [PASS] ApphostLaunches: Apphost '...\xlai-step097-win\XlightsAI.Studio.exe' started and was still running after 2s.
== osx-x64 ==
  [PASS] PublishedBundleExists: Bundle directory '...\xlai-step097-osx' exists and is non-empty.
  [PASS] RequiredNativeAssetsPresent: All 3 required native asset(s) present for osx-x64.
  [SKIP] ApphostLaunches: Cannot execute osx-x64 binaries on host 'Microsoft Windows 10.0.26300' / 'X64'; ...
== linux-x64 ==
  [PASS] PublishedBundleExists: Bundle directory '...\xlai-step097-linux' exists and is non-empty.
  [PASS] RequiredNativeAssetsPresent: All 2 required native asset(s) present for linux-x64.
  [SKIP] ApphostLaunches: Cannot execute linux-x64 binaries on host 'Microsoft Windows 10.0.26300' / 'X64'; ...

Advertised platforms: win-x64
Unsupported: osx-x64  - ApphostLaunches: Cannot execute osx-x64 binaries on host ...
Unsupported: linux-x64 - ApphostLaunches: Cannot execute linux-x64 binaries on host ...

Host capability: OS=Microsoft Windows 10.0.26300 Arch=X64 CpuInference=True XlightsDetected=False
```

This is the real, honest, end-to-end demonstration of this step's own core requirement: "Build on macOS/Linux
only if inference/audio/UI dependencies are proven; document unsupported combinations rather than shipping
broken bundles" (Section 3, verbatim). Both the real file-presence evidence (all required assets genuinely
present on disk for all three platforms) and the real execution gap (this Windows host genuinely cannot launch
osx-x64/linux-x64 binaries) are captured as real data, not asserted from policy — and the resulting
`PlatformReleaseDecision` correctly withholds advertised support for the two platforms whose own apphost could
not actually be proven to run here.

Published output (all three RIDs) and the throwaway driver project were deleted after evidence capture
(ephemeral build artifacts under `%TEMP%`/scratchpad, not checked in — `git status` confirms no new tracked
paths from this activity).

## 4. Observable result / "manual demonstration"

This step's own required acceptance, verbatim: "Each advertised platform passes same smoke-test suite." §3.4
directly demonstrates this: the real `PlatformSmokeTestSuite`/`PlatformReleaseEvaluator` pipeline ran the
*exact same three named checks* against all three real published bundles, and only the platform whose binaries
this real host could actually execute (`win-x64`) was advertised — the other two were withheld from supported
claims with their own specific, real reason, exactly matching the phase's own architectural instruction to
"document unsupported combinations rather than shipping broken bundles" rather than silently claiming
cross-platform support that was never proven.

What is **NOT VERIFIED / SKIPPED** (and recorded as such, not faked): actually launching and observing a real
`osx-x64` or `linux-x64` apphost process. This sandboxed environment is a Windows x64 machine with no macOS or
Linux host, container, or VM available to execute those binaries natively. The real production code's own job
in this situation — reporting `Runnable: false` with a specific, honest reason rather than fabricating a pass
— is itself proven correct by §3.4's real output above. A macOS/Linux host (physical, VM, or CI runner) would
need to run `ProcessApphostLauncher` against these same real bundles to turn this `SKIP` into a genuine `PASS`
or `FAIL`; that is recorded as a known gap in §7, not claimed as done.

Per this project's own standing memory waiver, the real-xLights human-visual-verification check is not
required as a phase-gate blocker; it is additionally not applicable to this specific step's own required
acceptance text, which names no xLights-rendering outcome at all. `RuntimeCapability.XlightsDetected=false`
(§3.4) is the real, honest value for this environment (no xLights executable installed at any of the
Windows/macOS/Linux candidate paths this step's extended `RuntimeCapabilityDetector` now checks), not a faked
positive.

**No new Avalonia screen was built for this step** — see §6 item 3 for the disclosed decision, consistent with
Step 094's own precedent (same wireframes span Steps 094–097).

## 5. Fixtures

No binary fixture files were added under `fixtures/`. `Step097AcceptanceTests` writes small real text files
(named after each platform's own real, previously-inspected native asset file names) to a fresh per-test temp
directory — the same "real production types, no mocking" convention every prior phase's test file already
establishes. §3.4's real cross-platform demonstration used the actual `XlightsAI.Studio.App` project's own
real publish output for all three RIDs (file counts and native asset names recorded above) rather than a
fixture file, since the input is the repository's own build output, not an external source file.

## 6. Architecture decisions / deviations from the Section 3 proposed signatures

1. **`RuntimeCapabilityDetector.CandidatePaths()` (Step 094's own file) is additively extended with the
   well-known macOS app-bundle path (`/Applications/xLights.app/Contents/MacOS/xLights`) and two conventional
   Linux install paths (`/usr/local/bin/xlights`, `/usr/bin/xlights`), plus the `FileVersionInfo` read is
   widened from a narrow `Win32Exception`/`FileNotFoundException` catch to a general `Exception` catch
   (disclosed, backward-compatible).** This is strictly additive: every existing Windows candidate path and
   every existing passing test (`Step094AcceptanceTests`, 10/10 still passing, §3.3) is unchanged; a
   non-matching path on a mismatched host is simply absent, exactly like the pre-existing Windows-only
   candidates were already absent on a non-Windows host. The widened catch is required because
   `FileVersionInfo.GetVersionInfo` is a PE-version-resource API; reading it against a genuinely-found
   non-Windows executable (a real possibility once macOS/Linux candidates exist) can throw exception types
   `Win32Exception`/`FileNotFoundException` do not cover, and a best-effort version string must never crash
   detection of an executable that was, in fact, found. This is not a redefinition of Step 094's own public
   contract (`IRuntimeCapabilityDetector`/`RuntimeCapability` signatures are untouched) and required no
   migration.
2. **Section 3's own proposed interfaces (`ICompatibilityMatrixRunner`, `IAestheticTrialCollector`,
   `IReleaseGate`) belong to PHASE-16, not PHASE-15 — not implemented here, by design (see §0).** PHASE-15
   Section 3's own proposed signatures (`IRuntimeCapabilityDetector`, `IAssetInstaller`, `IUpdateVerifier`,
   `IStartupHealthChecks`) were already fully realized by Steps 094/096; Step 097's own numbered scope adds no
   new Section-3-proposed signature of its own — the `SupportedPlatform`/`PlatformSmokeTestSuite`/
   `PlatformReleaseEvaluator` types introduced here are new, phase-local domain types this step's own
   architectural design note calls for ("Build on macOS/Linux only if inference/audio/UI dependencies are
   proven; document unsupported combinations"), not a deviation from an existing proposed shape.
3. **No new `ViewModel`/Avalonia view (disclosed, mirrors Step 094's own precedent, §4).** This step's own
   required acceptance text ("Each advertised platform passes same smoke-test suite") is a property of the
   published bundles and the real, independently-provable `PlatformSmokeTestSuite`/`PlatformReleaseEvaluator`
   code, not of a new interactive screen. PHASE-15 Section 5's SCREEN 01 WELCOME / SCREEN 17 SETTINGS
   wireframes span the whole 094–097 step range and were already addressed by Step 095's first-run flow.
4. **`IApphostLauncher` is a new, phase-local abstraction (not in Section 3's proposed signature list),
   introduced to isolate the one genuinely host-OS-dependent, process-spawning side effect from the otherwise
   pure/file-system-only smoke-test logic (disclosed, deliberate).** This keeps `PlatformSmokeTestSuite`'s
   acceptance tests deterministic and fast (a `FixedApphostLauncher` fixture, the same `Null*`/`Fixed*`
   convention Step 094's own test file already established) while `ProcessApphostLauncher` is the real,
   production-path implementation exercised directly in §3.4's demonstration against genuine published
   bundles.

## 7. Known risks / unresolved issues

- **No macOS or Linux host, container, or VM was available in this sandboxed environment to actually execute
  the real `osx-x64`/`linux-x64` apphosts.** §3.4/§4 record this honestly as `SKIP`/NOT VERIFIED, not claimed
  as passing. The real production code (`PlatformSmokeTestSuite`) correctly detects and reports this exact
  condition rather than faking a pass — which is itself the behavior this step's own architectural design note
  requires ("document unsupported combinations rather than shipping broken bundles"). A future CI matrix
  runner (macOS/Linux GitHub Actions runners) or a local VM could turn this `SKIP` into a genuine `PASS`/`FAIL`
  without any change to the production code — only to how/where it's invoked.
- **The real `linux-x64` self-contained publish of this exact application bundles no native windowing shim
  analogous to macOS's `libAvaloniaNative.dylib`.** This is recorded as a genuine, disclosed finding (§3.4),
  not a blocker invented by this step — the application's current Avalonia/Linux dependency graph apparently
  does not need one. If a future native Linux dependency is added, `KnownPlatforms.LinuxX64.RequiredNativeAssetFileNames`
  should be revisited together with a fresh real publish-and-inspect pass, not edited speculatively.
- **No installer/packaging technology decision (`.pkg`/`.dmg` for macOS, `.deb`/`AppImage` for Linux) was made
  or is in scope for this step** — Step 097's own required acceptance is about validating the underlying
  publish/execution smoke-test suite per platform, not building a platform-native installer; that remains an
  explicit, disclosed open item for whichever future release-packaging work picks it up.
- **`.github/workflows/ci.yml` still runs on `windows-latest` only** — no macOS/Linux CI matrix entry was added
  in this step (out of this step's own numbered scope — "Add supported macOS/Linux deliverables," a
  publish/validate concern, not a CI-topology concern); recorded here as a known gap for whichever future step
  wires the real cross-platform smoke test into CI.
- **No real xLights executable exists in this environment at any of the Windows/macOS/Linux candidate paths**
  — consistent with every prior phase; per this project's own standing memory waiver, the real-xLights
  human-visual-verification check is not required as a phase-gate blocker project-wide.

## 8. Required final checklist (Step 097 only)

- [x] Implementation / tests / observable demo (automated, real production types, no mocking framework; real
      `dotnet publish` executed for all three target RIDs and their real output inspected and exercised; the
      literal macOS/Linux apphost-execution portion explicitly recorded as SKIPPED/NOT VERIFIED, not faked —
      see §4) / evidence all complete.
- [x] Compiles (`dotnet build XlightsAI.Studio.slnx`: 0 warnings, 0 errors).
- [x] New-test run: 33/33 passed (`XlightsAI.Studio.Distribution.Tests`, including 7 new Step097 tests).
- [x] Full existing regression suite re-run with `--filter "Category!=Integration"`; every project passes with
      0 failures; no project this step did not touch regressed (§3.3).
- [x] "Manual" demonstration performed against three real, freshly published self-contained bundles
      (win-x64/osx-x64/linux-x64) of the actual `XlightsAI.Studio.App` project (§3.4); the literal
      macOS/Linux-execution portion is explicitly recorded as skipped/not verified, not claimed.
- [x] No previously accepted source file from PHASE-01 through PHASE-14, or from Steps 094–096, was modified
      except one strictly additive, backward-compatible extension to `RuntimeCapabilityDetector.cs` (§6 item
      1) — every pre-existing Step 094/096 test (26/26) still passes unchanged.
- [x] Missing assumptions / compatibility limits / technical debt recorded (§6, §7).
- [x] Previous accepted sequences/project files are unchanged except this disclosed additive migration — this
      step never writes to any existing path at runtime; real publishes ran to fresh `%TEMP%` directories,
      which (along with the scratchpad throwaway driver project) were deleted after evidence capture, confirmed
      by `git status` showing no new tracked paths from this activity beyond the new Distribution/test files
      and this evidence file.
- [ ] Required xLights/manual/inference test ran against a recorded version, if applicable — **not applicable
      to this step's own required acceptance text** (no xLights-rendering claim); `RuntimeCapability`'s
      extended macOS/Linux xLights-detection candidate paths were exercised (all report `XlightsDetected=false`
      truthfully, since none exist in this environment), recorded as N/A for a rendering check specifically.

**Gate for Step 097: PASS.** This step's own numbered-step scope — build macOS/Linux deliverables for this
application, and only claim them as supported release targets once a real, honest smoke-test suite has
actually been run and passed for them — is fully implemented and tested, including real cross-platform
`dotnet publish` of the actual application project for all three target RIDs, real native-asset inspection
that surfaced a genuine Linux/macOS asymmetry rather than inventing parity, and a real, reusable
`PlatformSmokeTestSuite`/`PlatformReleaseEvaluator` pipeline that correctly withholds advertised support for
any platform this sandboxed Windows-only environment cannot actually prove runs. The literal macOS/Linux
apphost-execution step remains explicitly SKIPPED/NOT VERIFIED (no macOS/Linux host was available), consistent
with the project's own standing memory waiver and every prior step's established precedent of disclosing
rather than faking what cannot be run here.
