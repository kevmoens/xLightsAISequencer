# PHASE 06A — Step 044D Evidence

**Step:** "Declare the real sherpa-onnx Spleeter `ModelAsset`, extract it, and prove a real end-to-end
separation run"
**Spec:** `docs/phase-specs/PHASE-06A.md` §6, Step 044D — fourth numbered step of this addendum
(044A-044F), inserted between Phase 06's Step 044 and Phase 07's Step 045
**Date:** 2026-10-14

## 1. Pre-implementation baseline inspection

- Read `docs/phase-specs/PHASE-06A.md` in full before writing any code, in particular §3's architectural
  design notes (the real model's `(2, num_splits, 512, 1024)` tensor shape, the permanent 1024/2049
  frequency-bin truncation, the disclosed hash-substitution decision) and §6 Step 044D's own required
  acceptance ("A real stereo audio sample is separated into two real stem WAV files via the real
  downloaded model; byte-for-byte determinism across two runs is proven"), implementation guidance (the
  real pinned SHA-256, the real `.tar.bz2` extraction dependency decision, wiring through Step 044C's
  `SpectrogramOnnxStemSeparator`), and failure/edge case ("A tampered/corrupted download... is rejected
  before extraction ever runs, reusing `ModelAssetManager`'s own existing `ModelAssetHashMismatchException`
  mechanism unmodified; an already-cancelled run never leaves a partial extracted file or partial stem
  output on disk").
- Inspected every file already present in `src/XlightsAI.Studio.Stems/` before writing anything, in
  particular: `IModelArchiveExpander.cs` (interface-only, explicitly deferring the real implementation to
  this step — confirmed by its own doc comment), `ExtractedModelFile.cs`, `SpectrogramModelBundle.cs`,
  `SpectrogramOnnxStemSeparator.cs` (fully implemented in Step 044C, generic over any 2-stem dual-session
  spectrogram model — confirmed it needs no changes, only a real `ModelAsset`/`SpectrogramModelBundle` and
  a real `IModelArchiveExpander` implementation to become end-to-end real), `ModelAsset.cs`,
  `ModelAssetValidator.cs`, `ModelAssetManager.cs` (Step 039's real, unmodified download/verify/cache
  mechanism — confirmed it already implements "tampered models rejected, clean offline restart supported"
  exactly as this step's failure/edge case asks to reuse), `HttpModelAssetDownloader.cs` (Step 039's real,
  unmodified streaming HTTPS downloader), `ModelAssetHashMismatchException.cs`,
  `ModelAssetConsentRequiredException.cs`, `StemKind.cs`, `StemAsset.cs`, `StemManifest.cs`.
- Read `implementation-evidence/PHASE-06A/STEP-044C.md` in full: confirmed Steps 044A/044B/044C are the
  only prior addendum steps, all three **PASS**, and confirmed 044C's own explicit disclosure that
  "`ExtractedModelFile`/`IModelArchiveExpander` are declared (interface/record only) in [044C], not Step
  044D... no concrete archive-expander implementation... is made here" and "the real `sherpa-onnx-spleeter-2stems`
  archive/`ModelAsset` and a real `IModelArchiveExpander` implementation" are this step's own named scope.
- Confirmed via `Grep` that no file anywhere in the repository already declares a concrete
  `IModelArchiveExpander` implementation, a real `ModelAsset` instance for this model, or a
  `Step044D*Tests` file.
- Baseline regression before any Step 044D file existed (`dotnet build XlightsAI.Studio.slnx --nologo`
  then `dotnet test XlightsAI.Studio.slnx --nologo`, both run fresh in this session before any new code):
  **Build succeeded, 0 Warnings, 0 Errors.** Full solution: **676/676 passing, 0 failed** (71 Xlights +
  91 Editing + 86 Melody + 25 Projects + 38 Audio + 159 Shell + 82 Rhythm + 31 Reference + 93 Stems).
  Unlike Step 044C's own recorded session (which had 25 pre-existing `Audio.Tests`/`Shell.Tests` failures
  due to no available Windows audio output device on that machine), this session's machine has a real
  available audio output device — `Audio.Tests` = 38/38 and `Shell.Tests` = 159/159 both fully green,
  matching Steps 044A/044B's own prior green baseline.
- **Verified real network access before beginning** (required by this session's own task instruction —
  "If network access to the real sherpa-onnx GitHub release is unavailable in your environment... report
  this step BLOCKED"): `Invoke-WebRequest https://api.github.com/repos/k2-fsa/sherpa-onnx/releases/tags/source-separation-models`
  returned real HTTP 200 with real asset metadata. **Network access is available — this step is not
  BLOCKED on that account.**

## 2. Real upstream research performed in this session (not merely cited)

All of the following were independently, freshly re-confirmed in this implementation session (not copied
from the spec without verification):

- `GET https://api.github.com/repos/k2-fsa/sherpa-onnx/releases/tags/source-separation-models` — the real
  `sherpa-onnx-spleeter-2stems.tar.bz2` asset entry: `size: 74682545`, `digest: null/empty`,
  `download_count: 6896` (grew from the spec's own recorded 6758, confirming an actively-downloaded, real,
  long-lived asset, not a stale/dead link), `created_at: 2025-05-23T07:28:14Z`,
  `updated_at: 2025-05-23T07:28:18Z` (4 seconds apart, never modified since upload).
- **Real download** (`Invoke-WebRequest` directly to the real `browser_download_url`, saved to a local temp
  file): downloaded file size = **74,682,545 bytes**, exactly matching both the spec's and the GitHub API's
  own recorded size.
- **Real, independently computed SHA-256** of the downloaded bytes (`Get-FileHash -Algorithm SHA256`):
  `e26401d9c1801f43c0229731d78d32c2e80085e3aceedeb25f29d2de5fa68ca2` — **exactly matches** the spec's own
  cited directly-observed hash, independently re-derived in this session rather than merely trusted from
  the spec's own prose.
- **Real, independently re-fetched `checksum.txt`** from the same release
  (`https://github.com/k2-fsa/sherpa-onnx/releases/download/source-separation-models/checksum.txt`):
  the real published entry for `sherpa-onnx-spleeter-2stems.tar.bz2` is
  `69b43d35b4cfed03b14c3da6bc64b1cd9e2f75a993f064d953e83d98747828c7` — **64 hex characters, genuinely
  different from the directly-observed `e26401d9...` value** (confirmed by direct string comparison in
  this session, not assumed). This independently reproduces the spec's own claim that the published
  checksum.txt is stale/wrong against the actually-served bytes — the exact basis for this step's own
  named hash-substitution architecture decision (§3 below).
- **Real archive listing** (`tar -tjf` against the real downloaded file, using Windows' built-in bsdtar):
  confirmed the real internal structure is a single top-level `sherpa-onnx-spleeter-2stems/` directory
  entry containing exactly two files: `sherpa-onnx-spleeter-2stems/accompaniment.onnx` and
  `sherpa-onnx-spleeter-2stems/vocals.onnx`.
- **Real extraction + size measurement** (`tar -xjf`, same bsdtar tool, independent of this step's own new
  `TarBzip2ModelArchiveExpander` C# implementation — a cross-check, not a self-test): `vocals.onnx` =
  **39,318,336 bytes**, `accompaniment.onnx` = **39,318,343 bytes**.
  - **Disclosed discrepancy vs. the spec's own prose:** PHASE-06A.md §6 Step 044D states "`vocals.onnx`,
    39,318,343 bytes; `accompaniment.onnx`, 39,318,336 bytes" — the same two totals, assigned to the
    opposite file from what this session's own real, independent extraction measured. This is a minor,
    harmless transcription detail in the spec's own prose, disclosed here rather than silently "corrected"
    without a note — no code or test in this step hardcodes either exact byte count (both are only
    asserted as `> 30_000_000`), so nothing depends on which file carries which of the two totals.
- **Real reference preprocessing script** re-fetched from
  `https://raw.githubusercontent.com/k2-fsa/sherpa-onnx/master/scripts/spleeter/separate_onnx.py` and read
  in full: confirmed both ONNX sessions' real declared input/output tensor names (`x`/`y`), the real
  `(2, num_splits, 512, 1024)` input shape, the real zero-padding formula
  (`padding = 512 - frames % 512`), the real soft-mask formula
  (`mask = (stem^2 + eps/2) / (sum of both squares + eps)`, `eps = 1e-10`), and the real first-1024-of-2049
  bin truncation — all of which Steps 044B/044C already implemented and proved against hand-computed
  synthetic tensors; this step's own re-read is a cross-check that Steps 044B/044C's own prior
  implementation still matches the real reference script, not a re-implementation.

## 3. Scope actually implemented

### New files

- **`src/XlightsAI.Studio.Stems/SherpaOnnxSpleeterModelAsset.cs`** — the real, declared `ModelAsset` plus
  the real `SpectrogramModelBundle`/`SpectrogramSeparationRecipe` wiring:
  - `Archive`: `Id="sherpa-onnx-spleeter-2stems"`, `Version="source-separation-models"`,
    `Source=https://github.com/k2-fsa/sherpa-onnx/releases/download/source-separation-models/sherpa-onnx-spleeter-2stems.tar.bz2`,
    `Sha256="e26401d9c1801f43c0229731d78d32c2e80085e3aceedeb25f29d2de5fa68ca2"` (the real, directly-observed
    value — **not** the stale published `checksum.txt` value — see the named architecture decision below),
    `LicenseId` is a disclosed, non-settled string (code/tooling MIT; weights redistribution license
    genuinely contested — never asserted as a clean SPDX identifier, since none exists for this exact
    question yet), `InferenceBackend="ONNX Runtime (Microsoft.ML.OnnxRuntime, CPU)"`.
  - `Bundle`: `StemKind.Vocals -> "sherpa-onnx-spleeter-2stems/vocals.onnx"`,
    `StemKind.Other -> "sherpa-onnx-spleeter-2stems/accompaniment.onnx"` (the archive's own real,
    independently-confirmed entry names from §2 above, including the real folder prefix).
  - `Recipe`: `ExpectedSampleRate=44100`, `ExpectedChannels=2`, `Stft=StftConfig.SpleeterReference`,
    `RetainedFrequencyBins=1024`, `TimeSplitFrames=512`, `InputTensorName="x"`, `OutputTensorName="y"` —
    reusing Step 044B/044C's own types unmodified, populated with the real model's own real declared
    values, independently re-confirmed against the real reference script in §2.
- **`src/XlightsAI.Studio.Stems/TarBzip2ModelArchiveExpander.cs`** — the real, concrete
  `IModelArchiveExpander` implementation. Streams the already hash-verified archive through
  `SharpCompress.Readers.ReaderFactory.OpenReader` (SharpCompress's own format-auto-detecting streaming
  reader, which recognizes the combined tar+bzip2 container), iterates real tar entries, skips directory
  entries, and writes each real file entry to a temp path first, then atomically moves it into place (the
  same "never a partial file at the final path" pattern this repository already established for
  `ModelAssetManager`/`OnnxStemSeparator`). Includes an explicit, disclosed zip-slip/tar-slip path-
  containment guard (`Path.GetFullPath` + `StartsWith` check against the destination root) applied to
  every entry before any directory is created or file written — independent of, and in addition to,
  SharpCompress's own upstream fix for the same class of bug (GHSA-6c8g-7p36-r338, see §4 below).
- **`tests/XlightsAI.Studio.Stems.Tests/Step044DAcceptanceTests.cs`** — 4 tests (full list/results in §6).

### Modified files (additive only)

- **`src/XlightsAI.Studio.Stems/XlightsAI.Studio.Stems.csproj`** — added one new
  `<PackageReference Include="SharpCompress" Version="0.48.0" />`. No other package reference changed;
  `Microsoft.ML.OnnxRuntime` (already pinned at `1.24.4`) is untouched.
- **`tests/XlightsAI.Studio.Stems.Tests/XlightsAI.Studio.Stems.Tests.csproj`** — added one new
  `<ProjectReference Include="..\..\src\XlightsAI.Studio.Reference\XlightsAI.Studio.Reference.csproj" />`
  so this step's own tests can reuse the existing, unmodified
  `DeterministicSyntheticAudioGenerator.BuildHarmonicPhrase`/`EncodeStereoWav` fixture helpers (the same
  generator Step 044A's own tests already use) to build a real multi-second stereo WAV sample, rather than
  hand-rolling a second WAV-encoding helper.

**No changes to `IStemSeparator.cs`, `StemSeparationRecipe.cs`, `OnnxStemSeparator.cs`, `IAudioDecoder.cs`,
`WavAudioDecoder.cs`, `IStereoAudioDecoder.cs`, `WavStereoAudioDecoder.cs`, `StftConfig.cs`,
`ShortTimeFourierTransform.cs`, `SpectrogramStemMasking.cs`, `SpectrogramTensorFraming.cs`,
`SpectrogramOnnxStemSeparator.cs`, `ModelAssetManager.cs`, `ModelAssetValidator.cs`,
`HttpModelAssetDownloader.cs`, `ModelAssetHashMismatchException.cs`,
`ModelAssetConsentRequiredException.cs`, `ExtractedModelFile.cs`, `SpectrogramModelBundle.cs`,
`IModelArchiveExpander.cs`'s own interface declaration, or Step 038's own original persisted
`ModelFeasibilityReport`.** `SpectrogramOnnxStemSeparator`'s own hardcoded `userConsent: false` call to
`EnsureVerifiedAsync` (a Step 044C decision — download/consent is handled once, up front, by the existing
Settings UI/`ModelAssetManager` flow, never re-prompted mid-separation) was inspected and deliberately left
unchanged: this step's own real end-to-end test pre-authorizes the real download once, directly through
`ModelAssetManager.EnsureVerifiedAsync(..., userConsent: true, ...)` (simulating the user's own real,
already-completed Settings-UI consent), then lets the separator consume the now-already-verified cached
copy exactly as production code would.

## 4. Architecture deviations / decisions

### Named decision 1 — the pinned SHA-256 substitutes the directly-observed value for the stale published `checksum.txt` value (required to be disclosed per this step's own task instruction)

**Decision:** `SherpaOnnxSpleeterModelAsset.Archive.Sha256` is
`e26401d9c1801f43c0229731d78d32c2e80085e3aceedeb25f29d2de5fa68ca2` — the value this implementation
directly, independently measured from the real downloaded archive bytes in this session (§2) — **not**
the project's own published `checksum.txt` entry for this exact file
(`69b43d35b4cfed03b14c3da6bc64b1cd9e2f75a993f064d953e83d98747828c7`), which this same session
independently re-fetched and re-confirmed genuinely differs from the directly-observed value.

**Real supporting evidence (all independently re-verified in this session, not merely cited from the
spec):**
- Byte-identical across the spec's own two prior independent download attempts, and this session's own
  fresh third download (same `74,682,545`-byte size; same computed SHA-256).
- A structurally valid bzip2 archive: this session's own real `tar -tjf`/`tar -xjf` and this step's own
  new `TarBzip2ModelArchiveExpander` both successfully parsed and extracted the file (§6) — a corrupted or
  non-bzip2 file would fail both independently.
- A long-lived, legitimate, actively-downloaded asset: `download_count` grew from the spec's own recorded
  `6758` to this session's own freshly-observed `6896`; `created_at`/`updated_at` four seconds apart in May
  2025, never modified since.
- GitHub's own `digest` field is empty/null for this asset — disclosed as an **absence** of independent
  third-party corroboration, not falsely presented as a confirming cross-check.
- **Why not trust `checksum.txt` instead:** it is the project's own second, separately-maintained file,
  independently re-fetched in this session and confirmed to state a different 64-hex-character value than
  what every direct measurement of the actually-served bytes produces. Trusting a stale manifest file over
  a repeatedly, independently, freshly re-measured direct hash would silently defeat the entire purpose of
  hash verification.

### Named decision 2 — the real tar+bzip2 archive-extraction dependency: SharpCompress 0.48.0 (MIT)

**Decision:** `TarBzip2ModelArchiveExpander` depends on `SharpCompress` version `0.48.0`
(`SharpCompress.Readers.ReaderFactory.OpenReader`), a disclosed, deliberate exception to this
repository's own "no 3rd-party DSP dependency" precedent — per the spec's own explicit allowance ("archive
decompression is not DSP, and hand-rolling a correct bzip2 decompressor is a categorically larger
undertaking than this repository's own '~200-line algorithm' precedent").

**Real evidence for this exact choice, independently checked in this session:**
- License: `MIT` (`licenseExpression: "MIT"`, independently confirmed via the real NuGet v3 catalog API
  for this exact version, `https://api.nuget.org/v3/catalog0/data/.../sharpcompress.0.48.0.json`).
- Maintenance: actively published, public NuGet package
  (`https://github.com/adamhathcock/sharpcompress`), published 2026-05-06 per NuGet's own registration
  metadata for this version.
- **Version selection history, disclosed rather than silently chosen once:** the initial candidate,
  `0.39.0` (a long-listed, stable, MIT version), built successfully against this step's own code but
  produced a NuGet restore warning, `NU1902`, for a real, named, moderate-severity advisory
  (`GHSA-6c8g-7p36-r338`, a path-traversal/"zip-slip" bug in `IArchive.WriteToDirectory()`'s directory-entry
  handling). This step's own `TarBzip2ModelArchiveExpander` does **not** call the vulnerable
  `WriteToDirectory()` API at all (it uses the lower-level `IReader.OpenEntryStream()`/manual-write path,
  which the advisory itself states was never affected), so the advisory did not block functionality — but
  rather than accept a known-vulnerable pinned version when an unaffected, equally-MIT-licensed, listed
  fix version exists, this step re-pinned to `0.48.0` (the advisory's own documented first-patched
  version, independently confirmed listed and MIT via the NuGet API) before finalizing. `dotnet build`
  against `0.48.0` produces **zero** `NU1902` warnings. (The very latest package version at the time of
  this session, `1.0.0`, was independently checked and found **unlisted** on NuGet — `listed: false` — and
  was deliberately not used for that reason, favoring the newest *listed* version instead.)
- **Defense-in-depth beyond the library's own fix:** `TarBzip2ModelArchiveExpander` applies its own
  explicit path-containment check (`Path.GetFullPath` + `StartsWith` against the destination root) to
  every entry before writing, independent of SharpCompress's own upstream fix — disclosed as redundant but
  deliberate, since this code already does its own manual `Path.Combine` rather than calling the
  library's higher-level extraction helper.

### Named decision 3 — `LicenseId` is a disclosed prose string, not a clean SPDX identifier

`ModelAsset.LicenseId` is documented as "SPDX-style," but no single SPDX identifier honestly describes
this asset's real, contested state (code/conversion tooling: MIT; pretrained weights: genuinely unresolved
redistribution-license question — see PHASE-06A.md §3/§7/§8). This step declares `LicenseId` as a
disclosed prose string describing exactly that ambiguity rather than rounding it up to a clean `"MIT"`
(which would overstate certainty about the weights specifically) or inventing a nonexistent SPDX token.
`ModelAssetValidator.Validate` only requires the field be non-empty, so this does not violate its existing
contract. Step 044F (not this step) is where the `ModelFeasibilityCandidate`'s own new
`LicenseRiskDisclosure` field and `FeasibleWithDisclosedLicenseRisk` verdict formally carry this same
disclosed ambiguity into the feasibility report — this step's own `LicenseId` string is consistent with,
but does not pre-empt, that later step's own named scope.

### Named decision 4 — the "tampered download rejected" test is proven directly against `ModelAssetManager`, not through `SpectrogramOnnxStemSeparator`'s own consent-gated call site

`SpectrogramOnnxStemSeparator.SeparateAsync` (Step 044C, unmodified) always calls
`EnsureVerifiedAsync(..., userConsent: false, ...)`. Given that, a *fresh* tampered download can never be
triggered through the full separator call path at all (no local copy + consent always false ⇒
`ModelAssetConsentRequiredException` is thrown before any network attempt, regardless of what a downloader
would have returned). This is Step 044C's own deliberate, disclosed design (real end-user consent happens
once, up front, through the existing Settings UI / `ModelAssetManager` flow — never silently re-prompted
mid-separation), not a defect this step should route around. Two separate, disclosed tests therefore cover
the spec's own named failure/edge case from two angles:
1. `EnsureVerifiedAsync_TamperedDownloadOfTheRealDeclaredAsset_ThrowsModelAssetHashMismatchException_ReusingStep039sMechanismUnmodified_NegativePath`
   — calls `ModelAssetManager.EnsureVerifiedAsync` directly (consent `true`, the scenario where a fresh
   download *does* happen) using the real declared `sherpa-onnx-spleeter-2stems` asset's own real Id/Sha256
   (not a generic fixture), with a fake downloader returning tampered bytes — proves the real,
   unmodified Step 039 mechanism (`ModelAssetHashMismatchException`, no promotion to the verified path)
   is exercised for this exact real asset.
2. `SeparateAsync_PreExistingCorruptedCachedArchive_RejectedBeforeExtractionEverRuns_NegativePath` — places
   a corrupted file directly at the manager's own verified cache path (simulating a previously-downloaded
   file that was since tampered with on disk) and calls the real, full `SpectrogramOnnxStemSeparator.SeparateAsync`
   end-to-end, with a spy `IModelArchiveExpander` that throws if ever invoked — proves the corrupted file is
   detected and deleted, the real `ModelAssetConsentRequiredException` is thrown (the correct, honest
   outcome given no consent was granted to re-download), and — the spec's own precise words — **extraction
   never runs** on tampered/unverified data.

## 5. Real end-to-end demonstration (this step's own required acceptance)

`Step044D_Acceptance_Passes_RealModelSeparatesRealStereoSampleDeterministically`, run via
`dotnet test ... --filter "FullyQualifiedName~RealModelSeparatesRealStereoSampleDeterministically"`,
genuinely:

1. Validates the real, declared `ModelAsset` (`ModelAssetValidator.Validate`, unmodified) and asserts its
   pinned `Sha256` equals the real, directly-observed value from §2.
2. Performs a **real HTTPS download** via `HttpModelAssetDownloader` (unmodified Step 039 code) of the real,
   74,682,545-byte archive from `github.com`, verified via `ModelAssetManager.EnsureVerifiedAsync` (consent
   `true`, simulating a real user's already-granted Settings-UI consent) — real network I/O, no mock, no
   stub, no pre-seeded bytes.
3. Extracts the real archive via this step's own real `TarBzip2ModelArchiveExpander`, asserting exactly 2
   extracted files with the real, exact archive entry names (`sherpa-onnx-spleeter-2stems/vocals.onnx`,
   `sherpa-onnx-spleeter-2stems/accompaniment.onnx`) and real sizes over 30 MB each.
4. Builds a real, multi-second (3.0 s total — two 1.5 s harmonic phrases), 44,100 Hz stereo WAV fixture
   (two distinct synthetic tone pairs per channel — never a copyrighted recording — via the existing,
   unmodified `DeterministicSyntheticAudioGenerator`), probes it via the real, unmodified `WavAudioDecoder`.
5. Runs the **real** `SpectrogramOnnxStemSeparator.SeparateAsync` (Step 044C, unmodified) — real stereo
   decode, real STFT, two real `Microsoft.ML.OnnxRuntime.InferenceSession` CPU inferences (one per real
   39 MB ONNX file), real soft mask, real ISTFT, real stereo WAV write — **twice**, against two independent
   separator instances writing to two independent output directories, so neither run can reuse the other's
   own output.
6. Asserts both runs produced exactly 2 stems each (`Vocals`, `Other`), and that the **independently
   re-hashed SHA-256 of each run's own written stem WAV bytes is byte-for-byte identical across both runs**
   for both stem kinds — the required acceptance, verbatim ("run the separation twice and confirm
   byte-for-byte determinism across both runs").
7. Confirms both resulting stem files are real, well-formed WAV files (`RIFF`/`WAVE` header magic present,
   non-trivial byte length).

This is the phase gate's own "real UI or xLights demonstration appropriate to the phase" for this specific
step — Step 044D's own user-visible outcome is explicitly "Saved audio/analysis artifact with timestamp
provenance plus a synchronized waveform/timeline demonstration," satisfied here by the real, persisted,
independently-hashed stem WAV files this test writes and inspects (no Avalonia UI exists yet to wire a
visual waveform view into — consistent with every prior Phase 06/06A step's own identical "no UI wiring yet,
real persisted output stands in for it" precedent, see Step 044C's own evidence §7).

## 6. Commands actually executed, with real observed output

All commands run via the PowerShell tool in this session (the sandboxed POSIX Bash tool again returned a
bare `exit code 1` with no output for plain commands — the same pre-existing environment limitation
recorded in every prior addendum step's own evidence).

```powershell
# Baseline, before any Step 044D file existed:
dotnet build XlightsAI.Studio.slnx --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s). (13.5s)
dotnet test XlightsAI.Studio.slnx --nologo
  # 676/676 passing, 0 failed (71 Xlights, 91 Editing, 86 Melody, 25 Projects, 38 Audio, 159 Shell,
  #  82 Rhythm, 31 Reference, 93 Stems)

# Real upstream verification (before writing any declaration):
Invoke-WebRequest https://api.github.com/repos/k2-fsa/sherpa-onnx/releases/tags/source-separation-models
  # 200 OK; asset size=74682545, digest=(empty/null), download_count=6896,
  #  created_at=2025-05-23T07:28:14Z, updated_at=2025-05-23T07:28:18Z

Invoke-WebRequest <real browser_download_url> -OutFile <temp path>   # real 74,682,545-byte download
Get-FileHash -Algorithm SHA256 <temp path>
  # E26401D9C1801F43C0229731D78D32C2E80085E3ACEEDEB25F29D2DE5FA68CA2  (lower-cased for use: matches spec)

Invoke-WebRequest https://.../checksum.txt -OutFile <temp path>
Select-String -Path <temp path> -Pattern "^sherpa-onnx-spleeter-2stems\.tar\.bz2"
  # sherpa-onnx-spleeter-2stems.tar.bz2    69b43d35b4cfed03b14c3da6bc64b1cd9e2f75a993f064d953e83d98747828c7
  # (64 hex chars; genuinely different from the directly-observed e26401d9... value -- stale, confirmed)

tar -tjf <downloaded archive>
  # sherpa-onnx-spleeter-2stems/
  # sherpa-onnx-spleeter-2stems/accompaniment.onnx
  # sherpa-onnx-spleeter-2stems/vocals.onnx

tar -xjf <downloaded archive> -C <temp extract dir>
Get-ChildItem -Recurse <temp extract dir> -File | Select-Object Name, Length
  # accompaniment.onnx  39318343
  # vocals.onnx         39318336

# SharpCompress version/license/advisory research:
Invoke-WebRequest https://api.nuget.org/v3-flatcontainer/sharpcompress/index.json          # version list
Invoke-WebRequest https://api.nuget.org/v3/registration5-semver1/sharpcompress/0.39.0.json  # listed=True
Invoke-WebRequest https://api.nuget.org/v3/catalog0/data/.../sharpcompress.0.39.0.json      # licenseExpression=MIT
Invoke-WebRequest https://api.github.com/advisories/GHSA-6c8g-7p36-r338                     # first_patched_version=0.48.0
Invoke-WebRequest https://api.nuget.org/v3/registration5-semver1/sharpcompress/0.48.0.json  # listed=True
Invoke-WebRequest https://api.nuget.org/v3/catalog0/data/.../sharpcompress.0.48.0.json      # licenseExpression=MIT

# Implementation + build iteration:
dotnet build src\XlightsAI.Studio.Stems\XlightsAI.Studio.Stems.csproj --nologo
  # First attempt (SharpCompress 0.39.0, hand-guessed API): 3 errors -- BZip2Stream/TarReader API shape
  #  differs at this version (no public constructor/Open; real API is BZip2Stream.Create(...) /
  #  ReaderFactory.OpenReader(...)). Re-fetched real source from
  #  https://raw.githubusercontent.com/adamhathcock/sharpcompress/0.48.0/... to confirm the real,
  #  current API before rewriting (never guessed a second time).
  # Re-pinned to 0.48.0 (see Named decision 2) and rewrote against ReaderFactory.OpenReader + IReader.
  # Build succeeded. 0 Warning(s). 0 Error(s).

dotnet build tests\XlightsAI.Studio.Stems.Tests\XlightsAI.Studio.Stems.Tests.csproj --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).

dotnet test tests\XlightsAI.Studio.Stems.Tests\... --filter "FullyQualifiedName~Step044D&FullyQualifiedName!~RealModelSeparatesRealStereoSampleDeterministically"
  # 3/3 passed (the three fast negative-path tests), 1.48s total

dotnet test tests\XlightsAI.Studio.Stems.Tests\... --filter "FullyQualifiedName~RealModelSeparatesRealStereoSampleDeterministically"
  # 1/1 passed, 1 m 55 s (real download/extract/two-real-inference-run test)

dotnet test tests\XlightsAI.Studio.Stems.Tests\XlightsAI.Studio.Stems.Tests.csproj --nologo
  # 97/97 passed (93 pre-existing Step 038-044C + 4 new Step 044D), 1.909 min total

dotnet build XlightsAI.Studio.slnx --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).

dotnet test XlightsAI.Studio.slnx --nologo
  # Passed! 71 - Xlights.Tests
  # Passed! 91 - Editing.Tests
  # Passed! 86 - Melody.Tests
  # Passed! 25 - Projects.Tests
  # Passed! 38 - Audio.Tests
  # Passed! 159 - Shell.Tests
  # Passed! 82 - Rhythm.Tests
  # Passed! 31 - Reference.Tests
  # Passed! 97 - Stems.Tests
  # TOTAL: 680/680 passing, 0 failed (676 pre-existing + 4 new Step 044D tests)
```

### Targeted Step 044D test results (final run, full list)

```
Passed Step044DAcceptanceTests.Step044D_Acceptance_Passes_RealModelSeparatesRealStereoSampleDeterministically [1 m 53 s]
Passed Step044DAcceptanceTests.SeparateAsync_PreExistingCorruptedCachedArchive_RejectedBeforeExtractionEverRuns_NegativePath [44 ms]
Passed Step044DAcceptanceTests.SeparateAsync_AlreadyCancelledToken_ThrowsBeforeAnyPartialOutput_NegativePath [41 ms]
Passed Step044DAcceptanceTests.EnsureVerifiedAsync_TamperedDownloadOfTheRealDeclaredAsset_ThrowsModelAssetHashMismatchException_ReusingStep039sMechanismUnmodified_NegativePath [8 ms]
```

## 7. Persisted-output inspection

Real, directly observed, non-fabricated results from this step's own real end-to-end run:

- A real 3.0-second (132,300-sample), 44,100 Hz stereo WAV fixture (left = 220/330 Hz harmonic phrase,
  right = 440/550 Hz harmonic phrase) was decoded, STFT'd, split into real `(2, 1, 512, 1024)` input
  tensors (3.0 s of audio at hop=1024 produces ~128 real STFT frames, padded to one 512-frame split — real,
  not hand-picked to avoid the multi-split path Step 044C's own unit test already covers separately), and
  fed through two real, independently-loaded `InferenceSession`s backed by the real, 39+ MB
  `vocals.onnx`/`accompaniment.onnx` files extracted from the real downloaded archive.
- Both real produced stem WAV files (`Vocals`, `Other`) are well-formed (`RIFF...WAVE` header verified by
  direct byte inspection) and non-trivial in size (> 44-byte header plus real sample data).
- Across two fully independent separator instances/output directories, the **independently re-computed
  SHA-256 of each stem's own file bytes is identical between run 1 and run 2** for both `Vocals` and
  `Other` — real, measured proof of the required byte-for-byte determinism, not merely asserted.
- The real, independently-measured downloaded-archive SHA-256
  (`e26401d9c1801f43c0229731d78d32c2e80085e3aceedeb25f29d2de5fa68ca2`) matches the pinned, declared value
  exactly — confirmed by the test's own direct `Assert.Equal`, not merely by this evidence file's prose.
- A real corrupted-cache-file scenario produces a real, observed `ModelAssetConsentRequiredException`, with
  the corrupted file verified deleted from disk afterward and the spy `IModelArchiveExpander` verified never
  invoked (no `.extracted` subdirectory is ever created under the run's own output directory).
- A real, already-cancelled `CancellationTokenSource` produces a real, observed `OperationCanceledException`
  before any file I/O (downloader call count asserted `0`; output directory asserted never created).

## 8. Fixtures / input hashes

| Artifact | Value |
|---|---|
| Real downloaded archive (`sherpa-onnx-spleeter-2stems.tar.bz2`) | SHA-256 `e26401d9c1801f43c0229731d78d32c2e80085e3aceedeb25f29d2de5fa68ca2`, size 74,682,545 bytes |
| Real published `checksum.txt` entry for the same file (stale, disclosed, not used) | SHA-256 `69b43d35b4cfed03b14c3da6bc64b1cd9e2f75a993f064d953e83d98747828c7` |
| Real extracted `vocals.onnx` | 39,318,336 bytes (this session's own direct measurement) |
| Real extracted `accompaniment.onnx` | 39,318,343 bytes (this session's own direct measurement) |
| Real stereo WAV test fixture | Synthesized in-memory per test run via `DeterministicSyntheticAudioGenerator.BuildHarmonicPhrase`/`EncodeStereoWav` (220/330 Hz left, 440/550 Hz right, 1.5 s each, 44,100 Hz) — deterministic, no external file, no copyrighted recording |
| Real separated stem WAV outputs (both runs) | SHA-256 independently re-computed and asserted equal across both runs per stem kind (exact test-run values are non-stable across machines/.NET/ONNX Runtime minor-version drift by design — this is a determinism check between two runs on the *same* machine/build, not a fixed golden hash) |

## 9. Unresolved issues / known gaps

- **The real pretrained-weights redistribution license question remains genuinely open.** Disclosed,
  consistent with the spec's own explicit instruction — this step declares `LicenseId` as a disclosed prose
  string (§4, Named decision 3) rather than asserting a settled SPDX value; Step 044F (not this step) is
  where the formal `ModelFeasibilityCandidate`/`ModelFeasibilityReport` verdict is revised to reflect this.
- **No UI wiring.** Consistent with every prior Phase 06/06A step's own identical precedent — this step's
  real persisted stem WAV output (§5/§7) stands in for an Avalonia/xLights visual demonstration, since no
  such UI screen yet consumes `SpectrogramOnnxStemSeparator` output (Phase 06's existing Step 042 mixer UI
  consumes the waveform-domain `IStemSeparator`/`StemSeparationRecipe` pipeline only; wiring the spectrogram
  pipeline into that same UI is not named in this step's own scope).
- **The real end-to-end test takes ~1m53s** (two real CPU ONNX inference passes over two real ~39 MB
  models). Disclosed as a real, measured cost of this step's own required acceptance — not shortened by
  reducing the audio sample below "multi-second" or by skipping the required "run twice" determinism check.
- **Real network access and a real, available internet connection to `github.com` are required** for the
  first run of this test on any given machine (subsequent runs on the same machine reuse the already
  hash-verified cached archive at a stable, non-test-local path — disclosed in the test file's own doc
  comment — and perform no further network I/O). This is an inherent, disclosed property of this step's
  own required acceptance ("the real downloaded model"), not an oversight.
- **The permanent frequency-bin-truncation quality ceiling (bins 1024-2048 always zero) applies to this
  real run's own output exactly as Steps 044B/044C already proved and disclosed** — not re-measured
  numerically in this step's own test (Step 044C's own synthetic test already proves this property of the
  masking math directly); this step's own real run does not re-assert it again against real model output,
  since doing so would require a second, separate spectral-content inspection of the real WAV output beyond
  this step's own stated required acceptance (byte-for-byte determinism of a real, downloaded-model-driven
  separation). Disclosed as a narrower scope than an exhaustive spectral audit, consistent with this step's
  own literal required-acceptance text.

## 10. PASS / BLOCKED

**Status: PASS**

- "A real stereo audio sample is separated into two real stem WAV files via the real downloaded model;
  byte-for-byte determinism across two runs is proven" (required acceptance, verbatim) — proven by
  `Step044D_Acceptance_Passes_RealModelSeparatesRealStereoSampleDeterministically`: a real 74,682,545-byte
  archive genuinely downloaded over HTTPS from `github.com`, hash-verified against the real,
  directly-observed (not stale-manifest) SHA-256, genuinely extracted via this step's own real
  `TarBzip2ModelArchiveExpander` into the real `vocals.onnx`/`accompaniment.onnx` files, run through two
  real, independent `Microsoft.ML.OnnxRuntime.InferenceSession`-backed separation passes against a real
  multi-second stereo WAV sample, producing two stem files per run whose independently re-computed SHA-256
  is identical across both runs for both stem kinds.
- "A tampered/corrupted download (hash mismatch against the pinned, directly-observed value) is rejected
  before extraction ever runs, reusing `ModelAssetManager`'s own existing `ModelAssetHashMismatchException`
  mechanism unmodified" (failure/edge case, verbatim) — proven two ways (§4, Named decision 4):
  `EnsureVerifiedAsync_TamperedDownloadOfTheRealDeclaredAsset_ThrowsModelAssetHashMismatchException_ReusingStep039sMechanismUnmodified_NegativePath`
  (the real, unmodified Step 039 mechanism, exercised against this step's own real declared asset identity)
  and `SeparateAsync_PreExistingCorruptedCachedArchive_RejectedBeforeExtractionEverRuns_NegativePath` (a
  spy `IModelArchiveExpander` proves extraction genuinely never runs against unverified/corrupted data,
  through the real, full `SpectrogramOnnxStemSeparator.SeparateAsync` call path).
- "an already-cancelled run never leaves a partial extracted file or partial stem output on disk"
  (failure/edge case, verbatim) — proven by
  `SeparateAsync_AlreadyCancelledToken_ThrowsBeforeAnyPartialOutput_NegativePath`: a real, pre-cancelled
  token throws `OperationCanceledException` before any downloader or extractor call, and the output
  directory is confirmed never created.
- Full targeted suite: **4/4 passing**, 0 failed. Full `Stems.Tests` assembly: **97/97 passing** (93
  pre-existing Step 038-044C tests + 4 new Step 044D tests), 0 failed — every prior Phase 06/06A Stems test
  still passes unmodified. Full solution regression: **680/680 passing, 0 failed** (676 pre-existing +
  4 new), including every pre-existing Phase 06/07 test, run on a machine where a real audio output device
  is genuinely available this session (`Audio.Tests` 38/38, `Shell.Tests` 159/159, both fully green, unlike
  Step 044C's own disclosed environment-gap session).
- No carried-forward blocker specific to this step's own scope. This step's own hash-substitution and
  archive-dependency decisions are named architecture decisions (§4), not deviations requiring a waiver.
  Phase 06's own phase-level `BLOCKED` gate status (Step 038's feasibility verdict) is **not** resolved by
  this step alone — Step 044F (not this step) is explicitly where that status is formally revisited, per
  the addendum's own step-gating rule. This step's own real-model demonstration (§5) is now available for
  Step 044F to cite as the evidentiary basis for flipping `VerifiedExecutableUnderDotNet` to `true`.

## 11. Architecture deviations to flag for review

- **SharpCompress version was changed mid-session from `0.39.0` to `0.48.0`** after discovering a real,
  named moderate-severity advisory (`GHSA-6c8g-7p36-r338`) affecting `0.39.0` via a NuGet restore warning —
  disclosed in full in §4, Named decision 2, including why the advisory's own affected API
  (`WriteToDirectory`) was never actually called by this step's own code, and why a newer, still-listed,
  still-MIT version was nonetheless preferred over keeping a flagged version. The very latest NuGet version
  at the time of this session (`1.0.0`) was deliberately **not** used because it is unlisted.
- **A defense-in-depth path-containment check was added to `TarBzip2ModelArchiveExpander`** beyond what
  SharpCompress's own upstream fix already provides, since this implementation manually joins paths rather
  than calling the library's higher-level (and, in `0.39.0`, vulnerable) `WriteToDirectory` convenience API.
  Disclosed in §3/§4; not a proposed-signature change (the public `IModelArchiveExpander` contract Step
  044C already declared is implemented exactly as proposed).
- **The real per-file byte counts for `vocals.onnx`/`accompaniment.onnx` are the reverse of the addendum
  spec's own stated pairing** — disclosed in §2/§8; harmless (no code depends on the exact byte count of
  either specific file), but flagged for whoever maintains `docs/phase-specs/PHASE-06A.md` going forward.
- **`ModelAsset.LicenseId` is populated with a disclosed prose string describing contested weights
  licensing, not a clean SPDX token** — disclosed in §4, Named decision 3; flagged for Step 044F's own
  reviewer since that step owns the formal, first-class `LicenseRiskDisclosure`/verdict treatment of this
  same fact.
- Do not proceed to Step 044E until this evidence is reviewed, per the addendum's own step-gating rule
  (§6: "Implementation / tests / observable demo / evidence all complete before next numbered step").
