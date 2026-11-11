# PHASE 06A — Step 044G Evidence

**Step:** "Record the user/maintainer's explicit waiver of the Spleeter pretrained-weight redistribution
license risk, and bundle both the Spleeter and Whisper models with the shipped app."
**Spec:** Not a numbered step in `docs/phase-specs/PHASE-06A.md` itself — this is the explicit, named
decision point Step 044F's own text required before any `Feasible` verdict could be asserted for the
Spleeter candidate ("record whether the user/maintainer later chooses to accept the residual license risk
and formally upgrade the verdict to `Feasible` via a named, visible waiver — that decision point is a
future, explicit maintainer action, not something this document resolves on its own authority").
**Date:** 2026-11-08

## 1. What triggered this

The user asked (2026-11-08, in conversation) for lyric-word-timed cues ("each time it says 'Bang'") to
work. Investigating why showed the Whisper transcription model had never been downloaded on the
developer's own machine — and more generally, that neither the Whisper nor Spleeter model had any way to
reach an end user without an explicit runtime consent/download step (and, for Whisper specifically, no UI
entry point to grant that consent existed anywhere in the Shell app at all — a separate, now-fixed gap).
The user's explicit instruction: "the speech to text model should ship with our app," then, on being told
Spleeter's own pretrained-weight redistribution rights are documented as contested/unresolved (Step 044F),
explicitly confirmed: "yes the speech to text model should ship with our app" → "all models should ship
with our app" → and, when asked directly whether to accept the disclosed Spleeter redistribution risk
specifically, chose **"Yes, bundle it anyway (accept the risk)."**

## 2. The explicit waiver (required by Step 044F)

Per Step 044F's own text, verbatim: *"Spleeter's own official README states only that the code is
MIT-licensed; Deezer has made no explicit statement extending that to the released pretrained weights, no
official Deezer guidance on this specific question could be found anywhere... this is a real, unresolved
question with no existing official answer to cite, not a settled policy either way."* That remains true
today — no new Deezer guidance has appeared. The user/maintainer was told this plainly (see §1) and chose
to proceed anyway, in full knowledge of the disclosed risk. This document records that as the named,
visible waiver Step 044F required: **the `sherpa-onnx-spleeter-2stems` candidate's `LicenseRedistributable`
is hereby asserted `true` and its `Verdict` upgraded from `FeasibleWithDisclosedLicenseRisk` to
`Feasible`, on the sole basis of this explicit user/maintainer waiver — not on any new legal clarification.**
This is a business/risk decision by the project's own owner, not a claim that the underlying legal
question has been resolved.

No committed JSON `ModelFeasibilityReport` exists anywhere in this repository (confirmed in
`implementation-evidence/PHASE-06A/STEP-044F.md` §1 — every report Step 038/044F produced was written to
a per-test temp directory and deleted). This document is therefore the equivalent durable record for this
decision, the same way Step 044F's own evidence file was the durable record for its own revision — see
`tests/XlightsAI.Studio.Stems.Tests/Step044FAcceptanceTests.cs` (unchanged) for the still-correct proof
that `ModelFeasibilityEvaluator` rejects a `Feasible` candidate whose `LicenseRedistributable` is `false`;
this waiver does not touch or weaken that invariant — it only changes which boolean value is now, with
explicit authorization, asserted for this one candidate.

Whisper's model weights carry no equivalent caveat (`SherpaOnnxWhisperTinyEnModelAsset`'s own doc comment:
"MIT (conversion tooling) -- underlying OpenAI Whisper model weights are MIT-licensed") — bundling it
needed no waiver at all.

## 3. What was actually built

- **`ModelAssetManager`** (`src/XlightsAI.Studio.Stems/ModelAssetManager.cs`): new optional
  `bundledSearchDirectories` constructor parameter. `EnsureVerifiedAsync` now checks each bundled
  directory (hash-verifying any hit, never trusting a tampered file) before the pre-existing cache
  directory — a bundled hit never touches the network and never requires `userConsent`. This generalizes
  this type's own pre-existing "offline pre-provisioned asset" design goal (stated in its own doc comment
  since Step 039) from "pre-provisioned inside the cache directory" to "pre-provisioned anywhere the app
  ships a copy." Additive/optional — every pre-existing caller/test behaves identically.
- **`src/XlightsAI.Studio.App/Program.cs`**: both the stem-separation and Whisper `ModelAssetManager`
  instances now pass `bundledSearchDirectories: [Path.Combine(AppContext.BaseDirectory, "models")]` — a
  folder shipped next to the installed executable. A dev build with no such folder present simply finds
  nothing there and falls back to the pre-existing AppData-cached, consent-gated download flow, unchanged.
- **`tools/BundledModelFetcher`** (new console tool): run at build/release time only, never at app
  runtime. Reuses the real `ModelAssetManager`/`HttpModelAssetDownloader`/pinned `ModelAsset` declarations
  the shipped app itself uses to download, hash-verify, and place both archives into a given output
  directory using the exact same content-addressed naming convention `ModelAssetManager.GetVerifiedPath`
  computes — so a bundled hit is byte-identical, in content and in filename, to what a consent-gated
  runtime download would have produced. Never duplicates the pinned Source URL/SHA-256 values.
- **`.github/workflows/release.yml`**: new "Fetch bundled models" step runs
  `tools/BundledModelFetcher` against `publish/models` between `dotnet publish` and `vpk pack`, so the
  packaged installer ships with both models already inside it. The model archives themselves are **not**
  committed to source control (explicit user instruction, 2026-11-08) — they are fetched fresh at
  build/release time, and (for local development) fetched once directly into the developer's own
  `bin/Debug/net10.0/models` output folder so the same bundled-lookup path is exercised locally too.

## 4. Tests

`tests/XlightsAI.Studio.Stems.Tests/Step039AcceptanceTests.cs` — two new tests:
- `EnsureVerifiedAsync_BundledSearchDirectoryHasVerifiedCopy_SkipsDownloadAndNeverTouchesCacheDirectory`:
  a bundled directory with a real, hash-matching copy is found and returned; the downloader is never
  invoked; the cache directory is never even created.
- `EnsureVerifiedAsync_BundledSearchDirectoryHasTamperedCopy_FallsThroughToCacheDirectoryDownload_NegativePath`:
  a bundled copy that fails hash verification is never trusted — falls through to the normal
  cache-directory download path instead of being silently accepted.

Both pass; the full pre-existing `Step039AcceptanceTests` suite (17 tests before this change) still
passes unmodified (19 total after the two additions).

## 5. Gate

**PASS.** This is a disclosed, explicit risk-acceptance decision by the project's own owner — not a claim
that Spleeter's pretrained-weight redistribution question is legally resolved. If Deezer ever issues
official guidance that contradicts this assumption, this decision should be revisited.
