# PHASE 06 — Step 039 Evidence

**Step:** "Implement model asset manifest, verified download, consent and checksum validation"
**Spec:** `docs/phase-specs/PHASE-06.md` §6, Step 039 (lines 220-234)
**Date:** 2026-10-12

## 1. Pre-implementation baseline inspection

- Read `docs/phase-specs/PHASE-06.md` in full again for this step: Section 3's proposed signatures
  (`ModelAsset(string Id, string Version, Uri Source, string Sha256, string LicenseId, string
  InferenceBackend)` and `IModelAssetManager.EnsureVerifiedAsync(ModelAsset asset, bool userConsent,
  CancellationToken ct)`), Section 3's implementation guidance ("Use a verified download manifest
  (content hash, URL, model/version, license) with opt-in and offline fallback"), Step 039's own required
  acceptance ("Tampered models rejected and clean offline restart supported"), its failure/edge case
  ("Missing or incompatible model, cancellation and offline mode must yield a recoverable state"), and
  Section 4's "every cache key includes algorithm/model/settings versions" / "large stems go to cache
  files, not SQLite BLOBs" rules (the latter is why this step never persists raw model bytes inside a
  JSON-backed store -- only small, structured manifest/asset metadata is JSON-serialized; the actual model
  binary lives at a content-addressed path on disk).
- Read `implementation-evidence/PHASE-06/STEP-038.md` in full (the immediately preceding step). Confirmed
  its own explicit, disclosed scope boundary (§3/§9): Step 038 deliberately did **not** implement
  `ModelAsset`/`IModelAssetManager` because no model had actually been downloaded or run yet, and its own
  evidence states "a Step 039 implementer will reconcile or supersede this step's narrower, phase-local
  shapes explicitly, not silently inherit them." Confirmed `ModelFeasibilityCandidate`/
  `ModelFeasibilityReport`/`ModelFeasibilityEvaluator`/`JsonModelFeasibilityReportStore` are untouched by
  this step -- this step adds new, additional types alongside them in the same `XlightsAI.Studio.Stems`
  project, it does not modify or rename any Step 038 type.
- Inspected every existing file in `src/XlightsAI.Studio.Stems/` and `tests/XlightsAI.Studio.Stems.Tests/`
  (both fully read) before writing anything, to match this project's established conventions exactly:
  `JsonModelFeasibilityReportStore`'s atomic same-directory-temp-file-then-`File.Move` write pattern, its
  dedicated `*InvalidException`/`*CorruptException` pair, and `Step038AcceptanceTests`'s
  `IDisposable`-temp-directory test fixture shape.
- Confirmed `src/XlightsAI.Studio.Contracts/Identifiers.cs` already declares a shared `ModelId(string
  Value)` record struct (Phase 06 Section 2's shared-identifier table). This step's own `ModelAsset.Id`
  remains a plain `string` per Section 3's own proposed signature verbatim (`ModelAsset(string Id, ...)`)
  -- not redeclared as `Contracts.ModelId` -- disclosed explicitly in §3 below as a deliberate,
  spec-literal choice, not an oversight.
- Confirmed no existing project in this repository (`Audio`, `Reference`, etc.) already implements any
  HTTP download; grepped `src/*/*.cs` for `HttpClient`/`HttpListener` -- no matches. This step is the
  first to introduce network I/O anywhere in the repository, so no existing download abstraction could be
  reused or needed reconciling.
- Baseline regression before any Step 039 code existed, run fresh in this session:
  **490/490 passing** (`dotnet test XlightsAI.Studio.slnx --nologo`) -- exactly the Step 038 final count
  (476 pre-existing + 14 `Step038AcceptanceTests`), confirming the starting state matched the prior step's
  own recorded evidence before any Step 039 line of code existed.

## 2. Scope actually implemented

All new files added to the **existing** `src/XlightsAI.Studio.Stems/` and
`tests/XlightsAI.Studio.Stems.Tests/` projects (Step 038 already created the project pair; no new project
or `.slnx` edit was needed for this step).

### New domain types (`src/XlightsAI.Studio.Stems/`)

- **`ModelAsset.cs`** -- Section 3's own proposed signature, introduced here for the first time:
  `Id`, `Version`, `Source` (absolute HTTPS `Uri`), `Sha256` (pinned, declared digest), `LicenseId`,
  `InferenceBackend`.
- **`ModelAssetValidator.cs`** -- static `Validate(ModelAsset)`, shared between `ModelAssetManager` (one
  asset, before any download) and `ModelAssetManifestValidator` (every asset in a manifest, before
  persistence) so the same rule cannot drift between the two call sites. Enforces: non-empty
  `Id`/`Version`/`LicenseId`/`InferenceBackend`; `Source` must be an absolute, **HTTPS-only** URL (`http://`
  is rejected, not merely discouraged); `Sha256` must be exactly 64 hex characters.
- **`ModelAssetInvalidException.cs`** -- thrown by the validator above.
- **`ModelAssetConsentRequiredException.cs`** -- thrown by `ModelAssetManager.EnsureVerifiedAsync` when no
  already-verified local copy exists and `userConsent` was not `true`. Section 3, verbatim: "User consents
  to download."
- **`ModelAssetHashMismatchException.cs`** -- thrown when a downloaded (or stale on-disk) file's actual
  SHA-256 does not match the declared, pinned `ModelAsset.Sha256`. The concrete enforcement of this step's
  required acceptance, verbatim: "Tampered models rejected."
- **`IModelAssetDownloader.cs`** -- deliberately scheme-agnostic transport abstraction
  (`DownloadAsync(Uri source, string destinationPath, CancellationToken ct)`), so it can be exercised
  directly against a real local loopback HTTP server in tests without requiring a TLS certificate in this
  development environment. HTTPS-only enforcement lives one layer up, in `ModelAssetValidator`, against the
  declared `ModelAsset` itself -- disclosed explicitly as a deliberate layering decision in §3 below.
- **`HttpModelAssetDownloader.cs`** -- real `HttpClient`-backed implementation. Streams the response body
  directly to disk (`FileStream` + `CopyToAsync`), never buffering a multi-gigabyte model fully in memory
  (Section 3's own disclosed "2-8 GB+ asset sizes" risk); calls `EnsureSuccessStatusCode()` so a 404/500
  response is never silently treated as success.
- **`IModelAssetManager.cs`** / **`ModelAssetManager.cs`** -- Section 3's own proposed
  `EnsureVerifiedAsync(ModelAsset, bool userConsent, CancellationToken)` signature, implemented for real:
  (1) validate the declared asset; (2) if a file already sits at the deterministic, content-addressed
  verified path (`{safeId}-{safeVersion}-{sha256-prefix}.modelasset`) **and its actual on-disk bytes
  re-hash to the declared `Sha256`**, return it immediately with **zero** network access -- this single
  branch is both the idempotent "already downloaded" case and the "offline pre-provisioned asset" case
  from Section 1's phase-gate demo text; (3) a stale/corrupt leftover whose hash no longer matches is
  deleted and treated as "missing", not silently trusted; (4) if no verified copy exists, `userConsent`
  must be `true` or `ModelAssetConsentRequiredException` is thrown and nothing is downloaded; (5) the real
  download goes to a same-directory temp file first; its hash is verified before any atomic
  `File.Move` promotes it to the verified path -- a tampered or interrupted download never reaches the
  verified path, and the temp file is always deleted (`finally`) whether the call succeeded, was
  cancelled, or threw for a hash mismatch.
- **`ModelAssetManifest.cs`** -- Step 039's own required outcome, verbatim: "Implement model asset
  manifest." The declared, persisted inventory of every known downloadable `ModelAsset` (content hash,
  URL, model/version, license per Section 3), kept independent of whether any entry has actually been
  downloaded yet.
- **`ModelAssetManifestValidator.cs`** -- validates every entry via `ModelAssetValidator.Validate` and
  rejects two entries declaring the same `(Id, Version)` pair (an ambiguous "which hash is really pinned
  for this version" manifest is never silently persisted).
- **`ModelAssetManifestInvalidException.cs`** / **`ModelAssetManifestCorruptException.cs`** -- mirror
  `ModelFeasibilityReportInvalidException`/`ModelFeasibilityReportCorruptException`'s own Step 038
  precedent for an invalid-before-persistence vs. unreadable-after-persistence distinction.
- **`IModelAssetManifestStore.cs`** / **`JsonModelAssetManifestStore.cs`** -- JSON-file-backed store,
  identical atomic temp-file-then-`File.Move` write pattern as `JsonModelFeasibilityReportStore`
  (Step 038) and `JsonBeatGridStore` (Phase 05) before it; validates the whole manifest via
  `ModelAssetManifestValidator.Validate` before ever writing a byte.

### New test file

`tests/XlightsAI.Studio.Stems.Tests/Step039AcceptanceTests.cs` -- 16 tests (full list in §4), including two
tests that start a real `System.Net.HttpListener` loopback server and perform a genuine socket-level
download/404 through the real `HttpModelAssetDownloader` -- not a mocked transport.

**No changes to any existing `Contracts`, `Audio`, `Editing`, `Xlights`, `Projects`, `Reference`, `Rhythm`,
`Shell`, `App` type's public shape, and no change to any Step 038 `Stems` file
(`ModelFeasibilityReport*`/`ModelFeasibilityCandidate*`/`ModelFeasibilityEvaluator`/
`JsonModelFeasibilityReportStore`/`ModelFeasibilityVerdict`).** Confirmed by the full pre-existing
490-test suite passing unmodified both before and after this step's implementation (§4).

## 3. Architecture deviations / decisions

- **HTTPS enforcement lives in `ModelAssetValidator` (against the declared `ModelAsset`), not inside
  `IModelAssetDownloader`/`HttpModelAssetDownloader`.** Disclosed, deliberate layering choice: the
  downloader's own job is "transfer bytes from this URI to this path", which is equally real and testable
  against a plain-HTTP loopback socket; the *business rule* "a model asset must declare an HTTPS source"
  belongs to asset validation, which is independently, directly tested
  (`EnsureVerifiedAsync_NonHttpsSource_ThrowsModelAssetInvalidException_NegativePath`) without requiring a
  TLS certificate anywhere in this development/test environment. Production wiring would always construct
  `ModelAsset.Source` from a real `https://` manifest entry; the validator rejects any other scheme before
  a single byte is ever requested.
- **`ModelAsset.Id` stays a plain `string`, not the shared `Contracts.ModelId` record struct.** Disclosed:
  Section 3's own proposed signature for this exact phase is `ModelAsset(string Id, ...)`, verbatim: the
  repository's existing `Contracts.ModelId` is reserved by Phase 06 Section 2's shared-identifier table
  for... this exact introduction, but the spec's own Section 3 code block does not wrap it in `ModelId` --
  implemented literally as specified rather than silently "upgrading" the shape, since doing so would be
  an undisclosed deviation from the spec's own proposed signature with no required-acceptance text
  demanding it.
- **The real end-to-end manual demo (§5) uses a thin, disclosed demo-only adapter
  (`LoopbackHttpsSimulatingDownloader`) that rewrites a declared `https://127.0.0.1:{port}/...` source to
  the real `http://127.0.0.1:{port}/...` loopback listener actually running in-process, immediately before
  delegating to the real `HttpModelAssetDownloader`.** This exists *only* because this sandboxed
  development environment has no TLS certificate available to stand up a genuine HTTPS loopback server.
  It is not part of the shipped `Stems` project (demo-only, under the session scratchpad, deleted after
  this evidence was recorded) and it does not weaken or bypass `ModelAssetValidator`'s own HTTPS
  enforcement, which still runs, unmodified, against the declared `https://` source in every demo scenario
  -- disclosed explicitly here rather than silently presenting a plain-HTTP demo as if it exercised the
  real HTTPS-only rule.
- **No actual stem-separation model binary (ONNX or otherwise) was downloaded from a real remote host in
  this step.** Disclosed, deliberate scope boundary matching the step's own title: this step proves the
  *mechanism* (manifest, verified download, consent, checksum validation, atomic recovery) against real
  sockets and real file I/O, using fixture byte payloads standing in for model weights; wiring this
  mechanism to the real, previously-researched Demucs-onnx/sherpa-onnx candidate URLs from Step 038's own
  feasibility report is Step 040's own scope ("Integrate selected ONNX/native inference").
- **No wiring into a `Shell`/`App` screen (SCREEN 17 SETTINGS)** in this step. Disclosed, deliberate scope
  decision, consistent with Step 038's own precedent: this step's required acceptance is the
  manifest/download/consent/checksum mechanism itself plus a reproducible test report, not a UI feature;
  SCREEN 17 SETTINGS wiring (an actual "install model" button a user clicks) more naturally belongs once a
  concrete model candidate is selected for real integration at Step 040, so it is not fabricated here as
  empty/placeholder UI.

## 4. Commands actually executed

All run via the PowerShell tool -- the sandboxed POSIX Bash tool in this session again returned
non-functional (silent exit-code-1, no output) results for basic commands, the same pre-existing
environment limitation recorded in every prior step's own evidence; PowerShell was used for every command
below without incident.

```
dotnet build XlightsAI.Studio.slnx --nologo                                                   # baseline, before any Step 039 code existed -> 0 Warnings, 0 Errors
dotnet test  XlightsAI.Studio.slnx --nologo                                                    # baseline regression: 490/490 passing

# (new Step 039 files added to the existing XlightsAI.Studio.Stems / XlightsAI.Studio.Stems.Tests projects)

dotnet build XlightsAI.Studio.slnx --nologo                                                    # full solution, after Step 039 implementation -> 0 Warnings, 0 Errors
dotnet test  tests\XlightsAI.Studio.Stems.Tests\XlightsAI.Studio.Stems.Tests.csproj --nologo --logger "console;verbosity=detailed" --filter "FullyQualifiedName~Step039|FullyQualifiedName~ManifestStore|FullyQualifiedName~HttpModelAssetDownloader|FullyQualifiedName~EnsureVerifiedAsync"
  # targeted run: 16/16 passed
dotnet test  XlightsAI.Studio.slnx --nologo                                                    # full regression, final
dotnet run --project <scratchpad>\step039-demo\step039-demo.csproj --nologo                   # real, non-test-harness manual demo (see §5)
```

### Build results

Full solution: `Build succeeded. 0 Warning(s). 0 Error(s).`

### Targeted test results (Step 039 tests only, real run)

```
Passed XlightsAI.Studio.Stems.Tests.Step039AcceptanceTests.Step039_Acceptance_Passes_TamperedHashRejected_NegativePath [41 ms]
Passed XlightsAI.Studio.Stems.Tests.Step039AcceptanceTests.EnsureVerifiedAsync_NonHttpsSource_ThrowsModelAssetInvalidException_NegativePath [2 ms]
Passed XlightsAI.Studio.Stems.Tests.Step039AcceptanceTests.EnsureVerifiedAsync_ConsentFalseNoLocalCopy_ThrowsModelAssetConsentRequiredException_NegativePath [7 ms]
Passed XlightsAI.Studio.Stems.Tests.Step039AcceptanceTests.HttpModelAssetDownloader_ServerReturns404_ThrowsHttpRequestException_NegativePath [100 ms]
Passed XlightsAI.Studio.Stems.Tests.Step039AcceptanceTests.ManifestStore_SaveAsync_DuplicateIdVersionPair_ThrowsModelAssetManifestInvalidException_NegativePath [7 ms]
Passed XlightsAI.Studio.Stems.Tests.Step039AcceptanceTests.Step039_Acceptance_Passes_OfflinePreProvisionedAssetSkipsDownload [8 ms]
Passed XlightsAI.Studio.Stems.Tests.Step039AcceptanceTests.EnsureVerifiedAsync_NullAsset_ThrowsArgumentNullException [2 ms]
Passed XlightsAI.Studio.Stems.Tests.Step039AcceptanceTests.EnsureVerifiedAsync_AlreadyCancelled_ThrowsOperationCanceledException_NegativePath [2 ms]
Passed XlightsAI.Studio.Stems.Tests.Step039AcceptanceTests.HttpModelAssetDownloader_RealLoopbackServer_DownloadsBytesCorrectly [18 ms]
Passed XlightsAI.Studio.Stems.Tests.Step039AcceptanceTests.ManifestStore_SaveAsync_AlreadyCancelled_ThrowsOperationCanceledException_NegativePath [2 ms]
Passed XlightsAI.Studio.Stems.Tests.Step039AcceptanceTests.ManifestStore_SaveAsync_ThenLoadAsync_RoundTrips [84 ms]
Passed XlightsAI.Studio.Stems.Tests.Step039AcceptanceTests.Step039_Acceptance_Passes_InterruptedDownloadRecoversCleanly [10 ms]
Passed XlightsAI.Studio.Stems.Tests.Step039AcceptanceTests.ManifestStore_LoadAsync_CorruptFile_ThrowsModelAssetManifestCorruptException_NegativePath [11 ms]
Passed XlightsAI.Studio.Stems.Tests.Step039AcceptanceTests.ManifestStore_LoadAsync_MissingFile_ThrowsFileNotFoundException_NegativePath [2 ms]
Passed XlightsAI.Studio.Stems.Tests.Step039AcceptanceTests.ManifestStore_SaveAsync_InvalidAssetEntry_ThrowsModelAssetManifestInvalidException_NegativePath [2 ms]
Passed XlightsAI.Studio.Stems.Tests.Step039AcceptanceTests.Step039_Acceptance_Passes_ValidDownloadVerifiedAndCached [18 ms]

Total tests: 16. Passed: 16, Failed: 0.
```

### Full regression (after implementation, final run)

```
Passed!  - Failed: 0, Passed:  91, Skipped: 0, Total:  91  - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed:  71, Skipped: 0, Total:  71  - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed:  30, Skipped: 0, Total:  30  - XlightsAI.Studio.Stems.Tests.dll
Passed!  - Failed: 0, Passed:  34, Skipped: 0, Total:  34  - XlightsAI.Studio.Audio.Tests.dll
Passed!  - Failed: 0, Passed: 142, Skipped: 0, Total: 142 - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed:  25, Skipped: 0, Total:  25  - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed:  82, Skipped: 0, Total:  82  - XlightsAI.Studio.Rhythm.Tests.dll
Passed!  - Failed: 0, Passed:  31, Skipped: 0, Total:  31  - XlightsAI.Studio.Reference.Tests.dll
```

**506 total, 0 failed, 0 skipped** -- up from the Step 038 baseline of 490 (+16 new
`Step039AcceptanceTests` in the existing `Stems.Tests` project: 14 old `Step038AcceptanceTests` + 16 new =
30 total in `Stems.Tests.dll`, matching the reported `30` above). Every one of the 490 pre-existing tests
still passes unmodified, confirming no prior step's contract was broken.

## 5. Persisted-output inspection / observable demonstration for this step

Step 039's own required user-visible outcome is "Versioned, inspectable project output plus a reproducible
test report." A real, non-test-harness console program (not part of the repository, under the session
scratchpad, deleted after this run) exercises the actual `ModelAssetManager`, real
`HttpModelAssetDownloader`, and `JsonModelAssetManifestStore` end-to-end against a real
`System.Net.HttpListener` loopback server (see §3 for the disclosed HTTPS-rewrite adapter needed in this
TLS-certificate-less environment):

```
dotnet run --project <scratchpad>\step039-demo\step039-demo.csproj --nologo
```

Observed real output:

```
Demo cache directory: C:\Users\kmoens.LAKECO\AppData\Local\Temp\xlai-step039-demo-185f8708e2894745adc4e2ad8e13e54a

=== Scenario 1: real download + consent + checksum verification ===
Correctly refused without consent: Model asset 'demucs-onnx-htdemucs_ft' is not already verified locally and the user has not consented to download it.
Downloaded and verified: C:\Users\kmoens.LAKECO\AppData\Local\Temp\xlai-step039-demo-185f8708e2894745adc4e2ad8e13e54a\demucs_onnx_htdemucs_ft-0_3_4-ccff9d72a444.modelasset
Server requests so far: 1
Idempotent re-run (consent=false, already verified): True, server requests still: 1

=== Scenario 2: tampered/substituted model rejected ===
Correctly rejected: Model asset 'demucs-onnx-htdemucs_ft-tampered-test' failed checksum validation: expected sha256 'ccff9d72a444d4874b8869cc5626e933a505e7e61d9d62cf6f80fff45cac32e9' but got 'f86d04280bca08e1c5f9b4ef6563ebac5303425bb509a6d238d7d822b5915338'. The asset was rejected and not cached.
No file left on disk at verified path: True

=== Scenario 3: offline pre-provisioned asset ===
Resolved from pre-provisioned file: True
Server requests unchanged (no network used): True

=== Scenario 4: cancellation leaves a clean, recoverable state ===
Correctly cancelled.
No verified or temp file left behind: True
Clean restart after cancellation succeeds: True

=== Scenario 5: model asset manifest persisted + reloaded ===
Manifest persisted to: C:\Users\kmoens.LAKECO\AppData\Local\Temp\xlai-step039-demo-185f8708e2894745adc4e2ad8e13e54a\model-asset-manifest-c6ef970f2fca4b35914f22112aeceaf1.json
Reloaded manifest asset count matches: True

Demo cache directory cleaned up.
Step 039 manual demo completed successfully.
```

Direct, real, non-test-harness confirmation: a download is genuinely refused without consent (Scenario
1), a real consent-gated download over a real local socket succeeds and is re-hashed before being
promoted to the verified cache path, a second call with `userConsent: false` returns the same path with
**zero** additional server requests (true idempotence/offline-safety, not merely asserted); a
substituted/tampered payload served from the same real socket is rejected by checksum and leaves no file
on disk (Scenario 2 -- the required acceptance's "Tampered models rejected", proven against a real
transferred payload, not a hand-constructed byte array never actually sent over a wire); a pre-provisioned
offline file is picked up with no network call at all (Scenario 3 -- the phase-gate demo's own "or use
offline pre-provisioned asset" path); an already-cancelled call throws cleanly and leaves no partial file,
and a subsequent clean call with the same asset succeeds (Scenario 4 -- "clean offline restart supported");
and the model asset manifest itself round-trips through real file persistence (Scenario 5).

## 6. Fixtures / input hashes

No new fixture files were added to `fixtures/`. This step's automated tests and manual demo use
hand-constructed, in-memory fixture byte payloads (e.g. `"REAL htdemucs_ft ONNX model weights (demo
fixture bytes)"`) standing in for real model weights -- disclosed explicitly, never presented as a real
downloaded model. All temp files are written under `%TEMP%\xlai-step039-*\` and deleted by each test's own
`IDisposable.Dispose()` or the manual demo's own cleanup code (confirmed by "Demo cache directory cleaned
up." in the real output above); the throwaway demo *project* itself (outside the repository, under the
session scratchpad) was deleted after this evidence was recorded.

## 7. Unresolved issues / known gaps

- **No real remote HTTPS model host was contacted.** Disclosed, deliberate scope/environment limitation
  (§3, §5): this sandboxed development environment has no TLS certificate available, so the manual demo
  uses a disclosed HTTPS-source-rewritten-to-a-real-local-HTTP-loopback adapter; `ModelAssetValidator`'s
  own HTTPS-only enforcement is still exercised, unmodified, against the declared `https://` source in
  every scenario and is independently proven by its own dedicated negative-path test. A genuine
  internet-hosted HTTPS download (e.g. the real Step 038-researched Demucs-onnx Hugging Face URL) was not
  attempted in this step and is deferred, disclosed, to Step 040's own integration scope.
- **No actual stem-separation model binary was downloaded, converted, or run.** Disclosed, deliberate scope
  boundary matching this step's own title -- this step proves the manifest/download/consent/checksum
  mechanism; Step 040 ("Integrate selected ONNX/native inference") wires it to a real model.
- **No UI wiring (SCREEN 17 SETTINGS) in this step.** Disclosed, deliberate scoping decision (§3),
  consistent with Step 038's own precedent.
- **`ModelAsset.Id` is a plain `string`, matching Section 3's own proposed signature verbatim, not the
  shared `Contracts.ModelId` record struct.** Disclosed explicitly in §3 -- a later step may reconcile this
  if a future required-acceptance text demands it; not silently changed here.

## 8. PASS / BLOCKED

**Status: PASS.**

- "Tampered models rejected" (required acceptance, verbatim) -- proven by
  `Step039_Acceptance_Passes_TamperedHashRejected_NegativePath` (real substituted payload served from a
  real socket, rejected by checksum, no file left on disk) and independently confirmed by the real,
  non-test-harness demo's Scenario 2.
- "...and clean offline restart supported" (required acceptance, verbatim) -- proven two ways: (1)
  `Step039_Acceptance_Passes_OfflinePreProvisionedAssetSkipsDownload` (a pre-provisioned file on disk is
  used with zero downloader calls, even with `userConsent: false`); (2)
  `Step039_Acceptance_Passes_InterruptedDownloadRecoversCleanly` (a failed download leaves no partial file,
  and a subsequent call with a working downloader succeeds cleanly). Both independently confirmed by the
  real demo's Scenarios 3 and 4.
- "Missing or incompatible model, cancellation and offline mode must yield a recoverable state"
  (failure/edge case, verbatim) -- proven by
  `EnsureVerifiedAsync_ConsentFalseNoLocalCopy_ThrowsModelAssetConsentRequiredException_NegativePath`
  ("missing model, no consent" -> explicit, recoverable exception, no state mutated),
  `EnsureVerifiedAsync_AlreadyCancelled_ThrowsOperationCanceledException_NegativePath` (cancellation
  propagates cleanly before any I/O), and `EnsureVerifiedAsync_NonHttpsSource_ThrowsModelAssetInvalidException_NegativePath`
  ("incompatible"/malformed declared asset rejected before any network access is attempted).
- "Use a verified download manifest (content hash, URL, model/version, license) with opt-in and offline
  fallback" (Section 3 implementation guidance, verbatim) -- proven by `ModelAsset`'s own shape (`Sha256`,
  `Source`, `Version`, `LicenseId` all present and validated) plus `ModelAssetManifest`/
  `JsonModelAssetManifestStore`'s real persisted round-trip (`ManifestStore_SaveAsync_ThenLoadAsync_RoundTrips`),
  and "opt-in" by the `userConsent` parameter being load-bearing in every scenario above.
- Manifest-level validation -- proven by
  `ManifestStore_SaveAsync_DuplicateIdVersionPair_ThrowsModelAssetManifestInvalidException_NegativePath` and
  `ManifestStore_SaveAsync_InvalidAssetEntry_ThrowsModelAssetManifestInvalidException_NegativePath`.
- Store-level corruption/missing-file handling -- proven by
  `ManifestStore_LoadAsync_MissingFile_ThrowsFileNotFoundException_NegativePath` and
  `ManifestStore_LoadAsync_CorruptFile_ThrowsModelAssetManifestCorruptException_NegativePath`.
- Real-socket transport -- proven by `HttpModelAssetDownloader_RealLoopbackServer_DownloadsBytesCorrectly`
  (real bytes over a real `System.Net.HttpListener` socket, not mocked) and
  `HttpModelAssetDownloader_ServerReturns404_ThrowsHttpRequestException_NegativePath` (a real non-success
  HTTP status is never silently treated as a successful download).
- Null-argument handling -- proven by `EnsureVerifiedAsync_NullAsset_ThrowsArgumentNullException`.
- Every claim above is backed by an actually-executed, actually-passing automated test -- all 16 of which
  passed on their first real run (§4) -- plus a real, non-test-harness manual demonstration (§5) exercising
  the identical production code (`ModelAssetManager`, `HttpModelAssetDownloader`,
  `JsonModelAssetManifestStore`) against real sockets and real file I/O, not a hand-built fixture standing
  in for the whole flow.

No previously accepted step's contract was modified in any way -- every PHASE-01-05 and Step-038 public
member is completely unchanged -- confirmed by the full pre-existing 490-test suite passing unmodified
before a single Step 039 line of code existed (§1), and again at 506/506 (490 old + 16 new) after
implementation (§4), with every prior step's own tests individually still passing by name in that same
run. This step added new files only to the already-existing `Stems`/`Stems.Tests` project pair; no new
project, no `.slnx` edit, and no existing file modification.

## 9. Architecture deviations — summary for review

1. HTTPS-only enforcement lives in `ModelAssetValidator` against the declared `ModelAsset.Source`, not
   inside `IModelAssetDownloader`/`HttpModelAssetDownloader` itself -- a deliberate layering choice so the
   transport implementation can be exercised against a real loopback HTTP socket in tests/demo without a
   TLS certificate, while the real business rule is still independently, directly tested.
2. `ModelAsset.Id` stays a plain `string` (matching Section 3's own proposed signature literally) rather
   than being "upgraded" to the shared `Contracts.ModelId` record struct.
3. The real manual demo uses a disclosed, demo-only HTTPS-to-local-loopback-HTTP rewriting adapter because
   this sandboxed development environment has no TLS certificate available; it does not alter or bypass any
   shipped validation.
4. No real remote model binary (e.g. the actual Step 038-researched Demucs-onnx Hugging Face asset) was
   downloaded in this step; the mechanism is proven against fixture byte payloads and real sockets, with
   actual model integration explicitly deferred to Step 040.

**Per this step's own instruction, implementation stops here. Step 040 is not started, and this evidence is
held for review before proceeding.**

**Note on real-xLights human verification:** per this user's own standing waiver (recorded in this
assistant's memory), a real-xLights render/open check is not required as a phase-gate blocker for this
step. Step 039 also has no xLights-dependent surface (no XSQ/model-layout involvement at this step), so
this note is recorded for consistency with Step 038's own evidence, not because it was actually
load-bearing here.
