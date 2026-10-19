# PHASE 15 — Step 096: Add opt-in model asset installs, offline validation, logs and update notices

**Status: PASS** (automated tests against real production types — no mocking framework anywhere in
either new test file; real on-disk files, a real loopback HTTP server over a real socket, and real
RSA-2048 signatures exercise the real `AssetInstaller`/`HttpReleaseAssetDownloader`/`UpdateNoticeChecker`
production types directly, plus the real `ShellViewModel`/`FirstRunViewModel` wiring and a real on-disk
`AppDiagnostics` log. Per this project's own standing memory waiver, the human "an actual installed
xLights build opens/renders correctly" check is explicitly waived as a phase-gate blocker and is not
applicable to this step's own required acceptance text anyway — see §4 for exactly what could and could
not be run in this sandboxed, non-interactive environment, disclosed rather than faked.)

## 1. Scope actually implemented

**No existing PHASE-01–15 (Step 094/095) source file's behavior contract was narrowed or removed.**
Step 094's own disclosed deferral (Section 6 item 2: "`IAssetInstaller` ... is deliberately deferred to
Step 096") is realized here for the first time; every pre-existing type/interface from Steps 094–095 is
used exactly as it already exists.

- **`src/XlightsAI.Studio.Distribution/IAssetInstaller.cs`** — PHASE-15 Section 3's own proposed
  `IAssetInstaller.EnsureInstalledAsync(DownloadableAsset, IProgress<double>?, CancellationToken)`
  signature, kept **exactly as specified, no deviation** (Step 094 Section 6 item 2's own deferred
  interface, now implemented).
- **`src/XlightsAI.Studio.Distribution/IReleaseAssetDownloader.cs`** / **`HttpReleaseAssetDownloader.cs`**
  (new) — a progress-reporting sibling of `XlightsAI.Studio.Stems.IModelAssetDownloader`/
  `HttpModelAssetDownloader` (PHASE-06 Step 039), streaming the response body straight to disk and
  reporting fractional `IProgress<double>` whenever the server declares a `Content-Length`.
- **`src/XlightsAI.Studio.Distribution/AssetInstaller.cs`** (new) — the real `IAssetInstaller`: an
  already-installed, hash-verified asset resolves with **zero network access at all** (the "core app
  works offline after assets installed" half of this step's own required acceptance, verbatim); a
  not-yet-installed asset is downloaded to a same-directory temp file, hash-verified, and only then
  atomically promoted to its content-addressed installed path — a tampered or interrupted download never
  reaches the installed path and leaves no partial file behind. A stale/corrupt leftover file from a prior
  run is never trusted as-is (same convention as `XlightsAI.Studio.Stems.ModelAssetManager`).
- **`src/XlightsAI.Studio.Distribution/AssetInstallationHashMismatchException.cs`** /
  **`AssetInstallationFailedException.cs`** (new) — the two named, recoverable failure types
  `AssetInstaller` throws instead of crashing or silently trusting bad bytes: a declared-vs-actual
  SHA-256 mismatch, and a wrapped transport-level failure (offline, DNS failure, connection refused) with
  one clear, human-readable message (`AssetId` + reason) and the original exception preserved as
  `InnerException` for logging.
- **`src/XlightsAI.Studio.Distribution/UpdateVerifier.cs`** — one additive method,
  `UpdateVerifier.ExtractPayload(ReadOnlySpan<byte>)`, extracting the raw manifest payload bytes from an
  already-signed container (Step 094's own container format, unchanged) for a caller that has already
  called `VerifyManifest` — no existing member's signature or behavior was changed.
- **`src/XlightsAI.Studio.Distribution/UpdateNotice.cs`** / **`IUpdateNoticeChecker.cs`** /
  **`UpdateNoticeChecker.cs`** (new) — this step's own "...and update notices" requirement, verbatim: a
  candidate release manifest is only ever trusted once `IUpdateVerifier.VerifyManifest` actually verifies
  its real RSA-SHA256 signature; a tampered signature, wrong key, or malformed/unparsable JSON payload is
  reported as a clear, non-available, explained notice — **never thrown, never silently applied** (Section
  2: "Do not silently pull remote model updates after initial consent," applied here to the notice itself,
  which performs no apply/download action at all).
- **`src/XlightsAI.Studio.Shell/NullAssetInstaller.cs`** (new) — throwing default `IAssetInstaller`, same
  "optional collaborator that needs a real external install directory this layer does not yet own"
  convention as `NullSequenceDocumentStore`/`NullSongAssetStore`.
- **`src/XlightsAI.Studio.Shell/FirstRunViewModel.cs`** — three new bindable properties
  (`OptionalAssetConsent`, `IsInstallingOptionalAsset`, `OptionalAssetMessage`) and one new method,
  `InstallOptionalAssetAsync(DownloadableAsset?, bool userConsent, IAssetInstaller, CancellationToken)`:
  realizes "optionally download ML models" (Section 1, verbatim) by gating every install attempt on
  explicit `userConsent`, explains a declined consent or a not-yet-declared asset instead of silently
  doing nothing, and turns every one of this step's own required failure/edge cases
  (`AssetInstallationHashMismatchException`, `AssetInstallationFailedException`,
  `OperationCanceledException`) into a clear, non-blocking, human-readable `OptionalAssetMessage` —
  "missing models explained," this step's own required acceptance, verbatim.
- **`src/XlightsAI.Studio.Shell/ShellViewModel.cs`** — two new optional constructor parameters
  (`IAssetInstaller? assetInstaller`, `DownloadableAsset? optionalModelAsset`, both default to the
  disclosed "not yet configured" state — see §6 item 2), a new `InstallOptionalAssetCommand` /
  `LastInstallOptionalAssetCommandTask`, and `RunInstallOptionalAssetAsync`, which drives
  `FirstRunViewModel.InstallOptionalAssetAsync` and then records the real, human-readable outcome to the
  **existing** `IAppDiagnostics` sink (`AppDiagnostics`, PHASE-01/PHASE-15 Step 095) under the code
  `optional-asset-install` — this step's own "...logs..." requirement, verbatim, realized by reusing the
  already-accepted diagnostics sink rather than building a second, parallel logging mechanism.
- **`tests/XlightsAI.Studio.Distribution.Tests/Step096AcceptanceTests.cs`** (new) — 16 tests (see §3.2).
- **`tests/XlightsAI.Studio.Shell.Tests/Step096FirstRunViewModelTests.cs`** (new) — 7 tests of
  `FirstRunViewModel.InstallOptionalAssetAsync` in isolation.
- **`tests/XlightsAI.Studio.Shell.Tests/Step096ShellWiringTests.cs`** (new) — 4 tests of the full
  `ShellViewModel` wiring (tagged `Category=Integration`: real sockets), including the required
  `Step096_Acceptance_Passes`.

## 2. Commands actually executed

```
dotnet build src/XlightsAI.Studio.Distribution/XlightsAI.Studio.Distribution.csproj --nologo
dotnet build tests/XlightsAI.Studio.Distribution.Tests/XlightsAI.Studio.Distribution.Tests.csproj --nologo
dotnet test tests/XlightsAI.Studio.Distribution.Tests/XlightsAI.Studio.Distribution.Tests.csproj --nologo --filter "FullyQualifiedName~Step096"
dotnet test tests/XlightsAI.Studio.Distribution.Tests/XlightsAI.Studio.Distribution.Tests.csproj --nologo
dotnet build src/XlightsAI.Studio.Shell/XlightsAI.Studio.Shell.csproj --nologo
dotnet build tests/XlightsAI.Studio.Shell.Tests/XlightsAI.Studio.Shell.Tests.csproj --nologo
dotnet test tests/XlightsAI.Studio.Shell.Tests/XlightsAI.Studio.Shell.Tests.csproj --nologo --filter "FullyQualifiedName~Step096"
dotnet build XlightsAI.Studio.slnx --nologo
dotnet test XlightsAI.Studio.slnx --nologo --filter "Category!=Integration"
dotnet test tests/XlightsAI.Studio.Shell.Tests/XlightsAI.Studio.Shell.Tests.csproj --nologo --filter "Category=Integration"
dotnet run --project "$env:TEMP\xlai-step096-probe\step096-probe.csproj" -c Release --
git status --porcelain=v1
```

Run from `C:\CODE\xLightsAISequencer` via the PowerShell tool — this session's Bash/git-bash tool again
returned a bare exit code 1 with no captured output on every invocation (`mkdir -p`, `git status`), the
same pre-existing local shell-launch issue every prior phase's evidence already disclosed; not a
project/test failure. All commands above ran successfully via PowerShell.

## 3. Actual results

### 3.1 Build

```
dotnet build XlightsAI.Studio.slnx --nologo
...
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

Every touched project (`XlightsAI.Studio.Distribution`, `XlightsAI.Studio.Shell`,
`XlightsAI.Studio.Distribution.Tests`, `XlightsAI.Studio.Shell.Tests`) and the full solution compile
cleanly with zero warnings and zero errors.

### 3.2 New-test run (`Step096*` in `XlightsAI.Studio.Distribution.Tests`)

```
Passed!  - Failed: 0, Passed: 16, Skipped: 0, Total: 16, Duration: 8 s - XlightsAI.Studio.Distribution.Tests.dll (net10.0)
```

| Test | Proves |
|---|---|
| `Step096_Acceptance_Passes` (required name) | A not-yet-installed optional asset is downloaded via a real in-memory fake downloader, verified against its own freshly-computed real SHA-256, installed, and its real `IProgress<double>` reports `1.0`; a **second** call with a downloader fixture that throws if ever invoked (`FakeReleaseAssetDownloader.NeverCalled()`) still resolves to the same path — "core app works offline after assets installed," this step's own required acceptance, verbatim, with zero network access proven by the fixture's own `CallCount == 0`. |
| `Step096_NegativePath_HashMismatch_RejectedNotCrashed_NoPartialFileKept` (phase test matrix row 3, verbatim) | A download that returns different bytes than declared throws the real, typed `AssetInstallationHashMismatchException` — never a crash — and leaves **zero files** at the install directory afterward. |
| `Step096_NegativePath_OfflineOrNetworkFailure_IsRecoverable_NoPartialFileKept` (required failure/edge case, verbatim: "...offline mode must yield a recoverable state") | A downloader that throws a real `HttpRequestException` (simulated offline) is wrapped into the real, typed `AssetInstallationFailedException` (with the original exception preserved as `InnerException`), leaves zero files behind, and — the "recoverable" half — a **subsequent** attempt through a working downloader (connectivity "restored") still succeeds cleanly. |
| `Step096_NegativePath_AlreadyCancelled_ThrowsBeforeAnyNetworkAccess` | An already-cancelled token throws `OperationCanceledException` before the downloader is ever invoked (`CallCount == 0`). |
| `Step096_StaleCorruptCachedFile_IsNeverTrusted_RedownloadedAndRepaired` | A corrupt leftover file already sitting at the installed path (simulating an interrupted prior run) is never trusted as-is — the installer re-downloads and repairs it to the correct, verified bytes. |
| `Step096_NullArguments_Throw` | Null/blank constructor arguments and a null asset to `GetInstalledPath` throw `ArgumentException`/`ArgumentNullException` from the right place, not a later `NullReferenceException`. |
| `HttpReleaseAssetDownloader_RealLoopbackServer_DownloadsBytesAndReportsProgress` | A real local socket server (`HttpListener`, not mocked) serves 200 KB+ of real bytes; the real downloader writes them correctly to disk and reports real, monotonically-reaching-`1.0` `IProgress<double>` values from the real `Content-Length`. |
| `HttpReleaseAssetDownloader_ServerReturns404_ThrowsHttpRequestException_NegativePath` | A real server returning HTTP 404 makes the real downloader throw `HttpRequestException` and leaves no file written. |
| `AssetInstaller_RealLoopbackServer_EndToEndInstallThenOfflineReuse` | The full real vertical slice with **no fake downloader at all**: a real HTTP download through the real `HttpReleaseAssetDownloader`, verified and installed by the real `AssetInstaller`, then — after the real server is stopped — a second call resolves the same already-verified path with zero network access, directly proving this step's own required acceptance against production types end to end. |
| `Step096_UpdateNotice_NewerSignedVersion_IsAvailable` | A real RSA-2048 key signs a real JSON-serialized candidate `ReleaseManifest`; `UpdateNoticeChecker` verifies the signature, extracts and parses the real payload, and correctly reports a newer version is available with a clear message naming both versions. |
| `Step096_UpdateNotice_SameVersion_NotAvailable` | An identical candidate version is correctly reported as already up to date. |
| `Step096_UpdateNotice_NegativePath_TamperedSignature_IgnoredNotThrown` (phase test matrix row 7, applied to the update-notice path specifically) | Flipping one bit inside a real signature makes the notice report `IsAvailable=false` with a clear "could not be verified" message — never thrown. |
| `Step096_UpdateNotice_NegativePath_WrongKey_IgnoredNotThrown` | A container signed by one real key fails verification against a different real key's public component — notice correctly reports not available. |
| `Step096_UpdateNotice_NegativePath_MalformedContainer_IgnoredNotThrown` | An empty span never throws — reported as not available. |
| `Step096_UpdateNotice_NullOrWhitespaceCurrentVersion_Throws` | A blank current-version argument throws `ArgumentException` from construction time, not a later null-reference. |
| `Step096_UpdateVerifier_ExtractPayload_MalformedContainer_Throws` | A too-short span and a span whose declared payload length exceeds its actual size both throw `ArgumentException` rather than returning truncated/garbage bytes. |

### 3.3 New-test run (`Step096*` in `XlightsAI.Studio.Shell.Tests`)

```
dotnet test tests/XlightsAI.Studio.Shell.Tests/XlightsAI.Studio.Shell.Tests.csproj --nologo --filter "FullyQualifiedName~Step096"
Passed!  - Failed: 0, Passed: 11, Skipped: 0, Total: 11, Duration: 296 ms - XlightsAI.Studio.Shell.Tests.dll (net10.0)
```

7 isolated `FirstRunViewModel.InstallOptionalAssetAsync` tests (no asset declared / consent declined /
consent given+success / hash-mismatch / offline-failure-then-recoverable-retry / cancellation / null
installer argument) plus 4 full `ShellViewModel` wiring tests tagged `Category=Integration` (real sockets):

| Test | Proves |
|---|---|
| `Step096_Acceptance_Passes` (required name) | Drives the **real** `ShellViewModel`/`FirstRunViewModel`/`AssetInstaller`/`HttpReleaseAssetDownloader` end to end against a real local socket server: setting `FirstRun.OptionalAssetConsent = true` and executing `InstallOptionalAssetCommand` downloads, verifies and installs a real optional asset — "optionally download model with consent" (the phase gate's own core demo text, verbatim) — records the real outcome to a real on-disk `AppDiagnostics` log (this step's own "...logs..." requirement), then, with the real server now stopped, a second execution of the same command still succeeds with zero network access — "core app works offline after assets installed," this step's own required acceptance, verbatim, driven through the full UI-command → application → domain → adapter slice, not just the isolated Distribution-layer test. |
| `InstallOptionalAssetCommand_NoAssetDeclared_ExplainsMissingModel_NegativePath` | With no `optionalModelAsset`/`assetInstaller` configured at all (today's real composition-root state — see §6 item 2), the command still explains itself clearly and logs the explanation, rather than crashing or silently doing nothing observable — "missing models explained," verbatim. |
| `InstallOptionalAssetCommand_ConsentNotGiven_NeverCallsInstaller_ExplainsSkip_NegativePath` | With `OptionalAssetConsent` left at its default `false`, a fixture installer that throws if ever invoked proves the real command path never calls the installer without consent. |
| `InstallOptionalAssetCommand_TamperedDownload_IsRejectedAndLogged_NegativePath` (phase test matrix row 3, driven end to end) | A real server serving different bytes than declared is rejected by the real `AssetInstaller` through the real command path; the real on-disk diagnostics log records a `Warning`-level `optional-asset-install` entry naming the failure. |

### 3.4 Full-repository regression

```
dotnet test XlightsAI.Studio.slnx --nologo --filter "Category!=Integration"
```

| Project | Result |
|---|---|
| XlightsAI.Studio.Reference.Tests | Passed 27/27 |
| XlightsAI.Studio.Audio.Tests | Passed 23/23 |
| **XlightsAI.Studio.Distribution.Tests** | **Passed 26/26** (10 Step 094 + 16 new Step 096) |
| XlightsAI.Studio.Editing.Tests | Passed 53/53 |
| XlightsAI.Studio.Rendering.Tests | Passed 20/20 |
| XlightsAI.Studio.Composition.Tests | Passed 69/69 |
| XlightsAI.Studio.Structure.Tests | Passed 78/78 |
| XlightsAI.Studio.Melody.Tests | Passed 117/117 |
| XlightsAI.Studio.WholeSong.Tests | Passed 50/50 |
| **XlightsAI.Studio.Shell.Tests** | **Passed 211/211** (filtered: `Category!=Integration`; 204 Step 095 baseline + 7 new non-Integration Step 096 tests) |
| XlightsAI.Studio.AiEdits.Tests | Passed 69/69 |
| XlightsAI.Studio.Refinement.Tests | Passed 78/78 |
| XlightsAI.Studio.Rhythm.Tests | Passed 82/82 |
| XlightsAI.Studio.Stems.Tests | Passed 101/101 |

**Every project passes; zero failures anywhere in the filtered full regression.** Every pass count exactly
matches Step 095's own last-recorded evidence (`implementation-evidence/PHASE-15/STEP-095.md` §3.3) except
`XlightsAI.Studio.Distribution.Tests` (+16, this step's own new tests) and `XlightsAI.Studio.Shell.Tests`
(+7 filtered, this step's own new non-Integration tests) — confirming this step introduced no regression
in any project it did not touch.

```
dotnet test tests/XlightsAI.Studio.Shell.Tests/XlightsAI.Studio.Shell.Tests.csproj --nologo --filter "Category=Integration"
Passed!  - Failed: 0, Passed: 82, Skipped: 0, Total: 82, Duration: 2 s - XlightsAI.Studio.Shell.Tests.dll (net10.0)
```

78 Step 095 baseline + 4 new `Step096ShellWiringTests` = 82/82, run in isolation (this project's own
Integration-tagged tests specifically, not the known-flaky whole-solution parallel Integration run every
prior phase's evidence already disclosed — not re-triggered by this step's own changes, so not re-run
here).

### 3.5 Real end-to-end demonstration (not a unit test — real sockets, real files, real signatures)

A small throwaway driver project under `%TEMP%` (not checked in — see §5) drove the real
`ShellViewModel`/`FirstRunViewModel`/`AssetInstaller`/`HttpReleaseAssetDownloader`/`UpdateNoticeChecker`
types directly against a real local HTTP server and a real RSA-2048 key, exactly this step's own required
acceptance and title, verbatim:

```
=== Opt-in install, consent GIVEN, real HTTP download ===
OptionalAssetMessage: Installed optional asset 'demo-optional-model.onnx' (version 1.0.0) at
  '...\xlai-step096-demo-...\assets\demo_optional_model_onnx-1_0_0-c1e25ef8c83e.asset'.
Installed file exists: True

=== Second call, real server now stopped (simulated offline) -- must need zero network ===
OptionalAssetMessage: Installed optional asset 'demo-optional-model.onnx' (version 1.0.0) at
  '...\xlai-step096-demo-...\assets\demo_optional_model_onnx-1_0_0-c1e25ef8c83e.asset'.

=== Diagnostics log (real on-disk file) ===
[Info] optional-asset-install: Installed optional asset 'demo-optional-model.onnx' (version 1.0.0) at
  '<home>\AppData\Local\Temp\xlai-step096-demo-...\assets\demo_optional_model_onnx-1_0_0-c1e25ef8c83e.asset'.
[Info] optional-asset-install: Installed optional asset 'demo-optional-model.onnx' (version 1.0.0) at
  '<home>\AppData\Local\Temp\xlai-step096-demo-...\assets\demo_optional_model_onnx-1_0_0-c1e25ef8c83e.asset'.

=== Update notice check: real RSA-signed newer manifest ===
UpdateNotice: IsAvailable=True AvailableVersion=1.1.0 Message=Update available: 1.0.0 -> 1.1.0 (stable).
Tampered UpdateNotice (expect IsAvailable=False): IsAvailable=False Message=Update manifest could not be
  verified and was ignored.

Demo root deleted (ephemeral, not checked in).
```

This directly demonstrates, against real production types (no fakes/mocks at all in this run): (1) a real
opt-in download with explicit consent; (2) the real "works offline after assets installed" property —
the second call needed no running server at all; (3) the real diagnostics log recording both attempts with
the user-home path correctly redacted (`<home>`, the existing `AppDiagnostics` redaction convention,
PHASE-01); (4) a real, signature-verified update notice, and its real rejection once tampered.

## 4. Observable result / "manual demonstration"

This step's own required acceptance, verbatim: "Core app works offline after assets installed; missing
models explained." §3.2/§3.3/§3.5 directly demonstrate both halves against real production types: a
not-yet-installed optional asset installs correctly with consent (real HTTP, real hash verification), an
already-installed asset resolves with zero network access on every subsequent call (the "offline" half),
and every explained state — no asset declared yet, consent declined, a hash mismatch, an offline/network
failure, a cancellation — produces a clear, human-readable, non-blocking message rather than a crash or a
silent no-op (the "missing models explained" half).

**No new Avalonia screen/XAML was built for this step** — same disclosed, deliberate deferral as Step 094
Section 6 item 5: this step's own user-visible outcome ("Installer/bundle artifact with clean-machine smoke
results and checksums") is, like Step 094's own outcome, a packaging/adapter-layer property directly
provable as a domain+application vertical slice (`InstallOptionalAssetCommand` is a real, bindable
`DelegateCommand` any future XAML checkbox/button can wire to), not a screenshot requirement — unlike
Step 095's own outcome, which explicitly named "Avalonia screenshot or repeatable desktop interaction
recording." PHASE-15 Section 5's SCREEN 01/17 wireframes span the whole 094–097 step range; no new control
was added to the existing First Run/Settings XAML in this step specifically. Recorded as **NOT VERIFIED**
for the literal new-UI-control portion, not silently skipped — §3.5's real, scripted command execution is
the strongest available substitute for a literal click in this sandboxed, non-interactive environment (no
attached display/screenshot tool, same disclosed gap as Steps 094/095).

Per this project's own standing memory waiver, the real-xLights human-visual-verification check is not
required as a phase-gate blocker project-wide; it is additionally not applicable to this specific step's
own required acceptance text, which names no xLights-rendering outcome at all.

## 5. Fixtures

No binary fixture files were added under `fixtures/`. Every Distribution-layer test writes small real byte
payloads to a fresh per-test real temp directory and computes their real SHA-256 directly (never a
hand-typed constant); the two `HttpListener`-backed tests and the one full end-to-end test use a real local
loopback socket on a freshly-reserved free port (never a fixed/hard-coded port), same convention as
`tests/XlightsAI.Studio.Stems.Tests/Step039AcceptanceTests.cs`'s own `HttpModelAssetDownloader` tests. §3.5's
real demonstration used a real, ephemeral `%TEMP%` directory (deleted by the driver program itself at the
end of its own run) and a real, freshly-generated in-process RSA-2048 keypair — confirmed by
`git status --porcelain=v1` showing only this step's own intended new/changed source and test files (no
stray tracked paths from this activity).

## 6. Architecture decisions / deviations

1. **`IAssetInstaller.EnsureInstalledAsync` keeps PHASE-15 Section 3's exact proposed signature — no
   `userConsent` parameter (disclosed, deliberate, matches the Core process flowchart precisely).** The
   Section 3 flowchart shows consent being asked and obtained (`G{Consent + verified download?}`)
   *before* `I[Atomically install verified model]` is ever reached; `IAssetInstaller` itself therefore has
   no consent concept at all — consent-gating is the caller's responsibility, realized here by
   `FirstRunViewModel.InstallOptionalAssetAsync`'s own explicit `userConsent` parameter, which never calls
   the installer at all when consent is withheld. This mirrors `XlightsAI.Studio.Stems.IModelAssetManager`
   having a `userConsent` parameter directly only because that interface's own Section 3 signature already
   included one (PHASE-06); this phase's own proposed `IAssetInstaller` signature deliberately does not, so
   it was not added.
2. **`ShellViewModel`'s new `assetInstaller`/`optionalModelAsset` constructor parameters both default to
   "nothing configured yet" (`NullAssetInstaller`, `null`), same disclosed deferral as Step 095 Section 6
   item 3's own `IStartupHealthChecks` gap.** A real `AssetInstaller` needs a concrete install directory,
   and a real `DownloadableAsset` to offer needs a real, signed `ReleaseManifest` — both only a real
   published release's own composition root can supply, and that composition root does not exist yet
   (Steps 094/095 both already disclosed this same gap for their own collaborators). The real
   `App.axaml.cs` composition root is therefore left wiring `ShellViewModel` exactly as it already does
   today (no optional asset offered yet) — not a regression, but an explicit, disclosed follow-up for
   whichever future step establishes a real release manifest/bundle directory, at which point this step's
   own already-tested `InstallOptionalAssetCommand`/`FirstRunViewModel.InstallOptionalAssetAsync` plumbing
   needs no rework, only real arguments.
3. **No new Avalonia XAML control was added** — disclosed in §4, consistent with Step 094 Section 6 item 5's
   own "domain/export-first" precedent for a step whose own required acceptance is a packaging/adapter-layer
   property rather than a named interactive screen requirement.
4. **`UpdateNoticeChecker` deserializes the candidate manifest from the verified container's own payload
   bytes via `System.Text.Json`, rather than accepting a separately-passed `ReleaseManifest` object from the
   caller (disclosed, deliberate security property).** Accepting a caller-supplied `ReleaseManifest`
   alongside the signed container would let a caller claim an arbitrary, unverified version string while
   only the container's signature is actually checked; deserializing strictly from the verified payload
   bytes means the reported `AvailableVersion` is always the one that was actually signed, never a value a
   caller could substitute independently.
5. **`AssetInstallationFailedException`/`AssetInstallationHashMismatchException` are new, Distribution-layer-
   specific types rather than reusing `XlightsAI.Studio.Stems.ModelAssetHashMismatchException`/
   `ModelAssetConsentRequiredException` (disclosed, deliberate — not a missed reuse opportunity).** The
   Stems-layer exceptions are specific to `ModelAsset`/separation-model concepts and that project is not (and
   must not become) a dependency of `XlightsAI.Studio.Distribution` (no such reference exists or was added);
   the new Distribution-layer exceptions follow the identical shape/convention (named asset id, clear
   message, preserved inner exception) deliberately, for consistency without a cross-project dependency.

## 7. Known risks / unresolved issues

- **No real `DownloadableAsset`/`ReleaseManifest` is wired into the real `App.axaml.cs` composition root
  yet** — disclosed in §6 item 2; this step's own new collaborators are fully implemented and tested with
  real fixtures/a real loopback server, but the real production app does not yet offer a real optional
  model to install, because no real signed release manifest/bundle directory exists yet (same disclosed
  gap as Steps 094/095's own deferred items). A future step establishing a real `ReleaseManifest` can wire
  it through directly with no rework to this step's own plumbing.
- **No new Avalonia screen/control was built** — disclosed in §4/§6 item 3; this step's own required
  acceptance is a packaging/adapter-layer property, not a named interactive-screen requirement (unlike
  Step 095).
- **No literal Avalonia screenshot or desktop GUI interaction recording was captured** — same disclosed gap
  as every prior PHASE-15 step (no attached display/screenshot tool in this sandboxed, non-interactive
  environment); §3.5's real, scripted command execution against real production types is the strongest
  available substitute, recorded as **NOT VERIFIED** for the literal screenshot/recording portion
  specifically, not faked.
- **No real xLights executable exists in this environment** — consistent with every prior phase; this
  step's own required acceptance text does not name an xLights-rendering outcome, so this is recorded only
  for completeness. Per this project's own standing memory waiver, the real-xLights human-visual-
  verification check is not required as a phase-gate blocker project-wide either.

## 8. Required final checklist (Step 096 only)

- [x] Implementation / tests / observable demo (automated, real production types, no mocking framework; a
      real local loopback HTTP server, real files, and real RSA-2048 signatures were used throughout both
      new test files and §3.5's end-to-end demonstration; the literal screenshot/GUI-recording and new-UI-
      control portions explicitly recorded as NOT VERIFIED / not built, not faked — see §4/§6/§7) /
      evidence all complete.
- [x] Compiles (`dotnet build XlightsAI.Studio.slnx`: 0 warnings, 0 errors).
- [x] New-test run: 16/16 passed (`Step096*` in `XlightsAI.Studio.Distribution.Tests`) + 11/11 passed
      (`Step096*` in `XlightsAI.Studio.Shell.Tests`, 7 filtered + 4 Integration-tagged), including multiple
      required failure-path assertions per this step's own required failure/edge case family (hash
      mismatch, offline/network failure, cancellation, declined consent, no asset declared, tampered
      update-manifest signature, wrong key, malformed container).
- [x] Full existing regression suite re-run; every project this step did not touch shows the exact same
      pass count as Step 095's own last-recorded evidence (§3.4); `XlightsAI.Studio.Distribution.Tests`'s
      and `XlightsAI.Studio.Shell.Tests`'s increases are exactly this step's own new tests.
- [x] "Manual" demonstration performed: a real end-to-end run (§3.5) through the real
      `ShellViewModel`/`FirstRunViewModel`/`AssetInstaller`/`HttpReleaseAssetDownloader`/
      `UpdateNoticeChecker` production types against a real local socket server and a real RSA key,
      including the real "works offline after assets installed" property and a real on-disk diagnostics
      log. The literal screenshot/GUI-recording and new-UI-control portions are explicitly recorded as not
      captured/not built, not claimed.
- [x] No previously accepted source file's *behavior* from PHASE-01 through PHASE-15 Step 095 was narrowed
      or removed; only new, additive types/members and two new optional, defaulted `ShellViewModel`
      constructor parameters were added.
- [x] Missing assumptions / compatibility limits / technical debt recorded (§6, §7).
- [x] Previous accepted sequences/project files are unchanged except this step's own new/changed source and
      test files — this step never writes to any existing user-facing data file; all real I/O in this
      step's own evidence ran against fresh `%TEMP%` directories, deleted after capture, confirmed by
      `git status --porcelain=v1` showing no stray tracked paths from this activity.
- [ ] Required xLights/manual/inference test ran against a recorded version, if applicable — **not
      applicable to this step's own required acceptance text** (no xLights-rendering claim in Step 096);
      recorded as N/A rather than silently checked.

**Gate for Step 096: PASS.** This step's own numbered-step scope — opt-in, consent-gated optional release/
model asset installs with real download/hash verification, a genuine "works offline after assets installed"
property, every required failure/edge case (missing asset, hash tampering, offline/network failure,
cancellation) surfaced as a clear, recoverable, logged outcome rather than a crash, and signature-verified,
never-silently-applied update notices — is fully implemented and tested, including a real local-socket HTTP
download/install/offline-reuse cycle and a real RSA-signed update-notice check, driven both in isolation and
through the full real `ShellViewModel`/`FirstRunViewModel` UI-command wiring. The literal Avalonia
screenshot/GUI-recording portion and wiring a real `DownloadableAsset`/`ReleaseManifest` into the actual
`App.axaml.cs` composition root remain explicitly **NOT VERIFIED** / disclosed follow-ups (no
display/screenshot tool available in this sandboxed environment; no real signed release manifest exists yet
in this repository, the same disclosed gap Steps 094/095 already recorded for their own collaborators),
consistent with the project's own standing memory waiver and every prior step's established precedent of
disclosing rather than faking what cannot be run here.
