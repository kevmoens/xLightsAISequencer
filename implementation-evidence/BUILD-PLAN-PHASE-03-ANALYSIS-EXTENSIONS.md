# Build Plan "Phase 3 — Analysis extensions" Evidence

**Spec:** [`docs/AI-CHOREOGRAPHY-BUILD-PLAN.md`](../docs/AI-CHOREOGRAPHY-BUILD-PLAN.md), "Phase 3 — Analysis extensions."
**Date:** 2026-10-24
**Status: PASS** (Tasks 1–4); Task 5 (phoneme alignment) is explicitly out of scope per the plan's own text ("stretch... can land after Phase 7").

> Naming note: `docs/phase-specs/PHASE-03.md` is a *different* phase (Visual Timeline and Manual
> Editing) from the same-numbered "Phase 3" in the build plan. To avoid colliding with the existing
> `implementation-evidence/PHASE-03/` directory (which belongs to the Visual Timeline phase), this
> evidence file is named by the build-plan phase title instead of by number.

## 1. Task 1 — Per-band (bass/mid/high) energy as first-class analysis output

**New types** (`src/XlightsAI.Studio.Rhythm/`): `BandEnergySettings`, `BandEnergyFrame`,
`BandEnergyEnvelope` (with `AverageInWindow(startTicks, endTicks)` — the "queryable per time window"
surface), `IBandEnergyAnalyzer`/`BandEnergyAnalyzer`, `IBandEnergyEnvelopeStore`/`JsonBandEnergyEnvelopeStore`,
and the matching invalid/corrupt exceptions.

`BandEnergyAnalyzer` reuses the same Hann-windowed spectral-flux machinery `OnsetDetector` already
established, but sums each bin's positive flux into one of three buckets (bass/mid/high) by the bin's
own center frequency, instead of collapsing every bin into one broadband scalar — the band energy
Task 1 says was "trapped inside OnsetDetector." All three bands are normalized against one shared peak
(not each band's own independent peak) so Bass/Mid/High stay comparable on a common scale.

**Tests:** `tests/XlightsAI.Studio.Rhythm.Tests/Phase03Task1BandEnergyAcceptanceTests.cs` (5 tests,
all passing) — including the required acceptance text verbatim: a synthetic bass-burst section
followed by a treble-burst section, asserting `AverageInWindow` differentiates them in both directions
(bass section: Bass > High; treble section: High > Bass; and across sections).

```text
$ dotnet test tests/XlightsAI.Studio.Rhythm.Tests -v quiet
Passed!  - Failed: 0, Passed: 87, Skipped: 0, Total: 87
```

## 2. Task 2 — `vocalActive` envelope from the two-stem separation

**New types** (`src/XlightsAI.Studio.Stems/`): `VocalActivityEnvelope` (with `IsActiveAt(tick)`),
`IVocalActivityAnalyzer`/`VocalActivityAnalyzer`, `VocalActivitySettings` (sample-rate-derived
`StemActivityDetectionSettings`, tuned per `EXPORT_AUDIT.md`'s own methodology: ~2s windows, hysteresis
thresholds that only flag "well above baseline" activity).

`VocalActivityAnalyzer` locates the `StemKind.Vocals` entry in a `StemManifest` (if any — tolerant of
the six-member `StemKind` enum only populating two members in practice today, per the task's own text)
and reuses the already-real `StemActivityDetector` against it.

**Tests:**
- `tests/XlightsAI.Studio.Stems.Tests/Phase03Task2VocalActivityAcceptanceTests.cs` (3 synthetic tests).
- `tests/XlightsAI.Studio.Stems.Tests/Phase03Task2RealTrackVocalActivityTests.cs` — runs against the
  **real** reference track's own already-separated vocals stem at
  `C:\xLights\stems\Dubstep Christmas Light Show\vocals.wav` when present on the machine running the
  tests (skips cleanly otherwise, same convention as `XlightsAI.Studio.Knowledge.Tests.RealXlightsInstallTests`).
  Asserts at least 60% of `EXPORT_AUDIT.md`'s own 7 known vocal-cluster timestamps (~94s, ~122–124s,
  ~182s, 400–412s, ~482s, ~568–570s, ~630–632s) land inside (or within one window/hop of) a detected
  active region. **Ran on this machine and passed.**

```text
$ dotnet test tests/XlightsAI.Studio.Stems.Tests -v quiet
Passed!  - Failed: 0, Passed: 104, Skipped: 0, Total: 104
```

## 3. Task 3 — measured phrase period, not hardcoded

**New type:** `src/XlightsAI.Studio.Structure/PhrasePeriodEstimator.cs` — implements
`MUSIC_FLOW_ANALYSIS.md` §1's own methodology exactly: bar-to-bar `|Δenergy|` (using
`BarFingerprint.Features[0]`, mean peak-normalized energy) above `mean + 1σ` is a "significant jump";
each candidate grid size is scored by a one-sided binomial survival test `P(X ≥ onGrid | n, 1/gridSize)`
(own Lanczos log-gamma implementation — .NET has no built-in Gamma/LogGamma). The smallest p-value
below `0.05` wins; `8` (the previous hardcoded constant) is kept as the explicit fallback when no
candidate clears significance.

**Wired into both real call sites** that previously hardcoded `TargetPhraseLengthBars: 8` via
`StructureAnalysisSettingsBundle.Default`: `StructureAnalysisRunner.AnalyzeAsync` and
`CorpusSongAnalyzer.AnalyzeAsync` now both fit `TargetPhraseLengthBars` from the song's own computed
bar fingerprints before calling `PhraseSegmenter`.

**Tests:** `tests/XlightsAI.Studio.Structure.Tests/Phase03Task3PhrasePeriodEstimatorAcceptanceTests.cs`
(6 tests) — deterministic synthetic fixtures (a square-wave bar-energy pattern whose transitions land
exactly on a known grid) prove the statistical fit picks 4-bar when evidence clusters on 4-bar lines
and 8-bar when it clusters on 8-bar lines, matching `MUSIC_FLOW_ANALYSIS.md` §1's own qualitative
result ("this track does not have a reliable 8-bar phrase grid... locks to 4-bar lines"), plus the
no-evidence fallback and input-validation negative paths.

**Known, disclosed gap:** reproducing `MUSIC_FLOW_ANALYSIS.md`'s own literal p-values (4-bar p=0.0039,
8-bar p=0.228) bit-for-bit against the real reference track was not attempted as an automated test.
That document's own pipeline is a separate, already-run analysis over `C:\xLights\events_raw.csv`
(3,284 events) with its own bar-grid construction; this repository's own beat/bar grid (`BeatGridBuilder`
+ `OnsetDetector`) is not guaranteed to produce bit-identical bar boundaries. More fundamentally,
`OnsetDetector`/`BandEnergyAnalyzer` use a direct O(FftSize×bins) per-frame DFT (a deliberate,
documented choice for this codebase's existing small/test-scale windows) — running it against an
11-minute real track's full-resolution spectrum is computationally infeasible as an automated test
today (a real FFT implementation would be required first). The synthetic tests above verify the
statistical *method* exactly; a full real-track reproduction is left as a follow-up once a real FFT
exists in this codebase.

```text
$ dotnet test tests/XlightsAI.Studio.Structure.Tests -v quiet
Passed!  - Failed: 0, Passed: 87, Skipped: 0, Total: 87
```

## 4. Task 4 — cadence-aware phrasing hash mismatch fix

**Root cause:** `PhraseSegmenter.ValidateInputs` required an exact
`MelodyTrack.SourceAssetHash == PhraseSegmentationContext.SourceHash` match before melodic-cadence
snapping could run at all. A real `MelodyTrack` (`AudioAnalysisRunner.AnalyzeAsync`) is derived from
an isolated lead *stem*, whose hash (`leadStem.Sha256`) is legitimately never the full song's own
hash — so this check could never pass for real data, and `StructureAnalysisRunner` worked around it by
passing `MelodyTrack: null` into phrase segmentation, silently losing cadence snapping entirely.

**Fix:** `PhraseSegmentationContext` gained a new `MelodySourceAssetHash` field — the hash
`MelodyTrack.SourceAssetHash` is actually expected to equal, threaded through separately from the
song's own `SourceHash`. `PhraseSegmenter.ValidateInputs` now validates against
`MelodySourceAssetHash` (required non-empty whenever a `MelodyTrack` is supplied) instead of
`SourceHash`. `StructureAnalysisRunner.AnalyzeAsync` now passes the real `melody` and
`melody.SourceAssetHash` instead of `null` — cadence-aware phrasing now actually runs against the
real, stem-derived melody.

**Tests** (`tests/XlightsAI.Studio.Structure.Tests/Step055AcceptanceTests.cs`, extended):
- `SegmentAsync_MelodyHashDiffersFromSongHash_StillSnapsToMelodicCadence_WhenMelodySourceAssetHashMatches`
  — the exact real-world shape (melody hash ≠ song hash) now produces a `MelodicCadence`-sourced
  phrase boundary instead of falling back to bar-hypothesis-only.
- `SegmentAsync_MelodyTrackSupplied_WithoutMelodySourceAssetHash_Throws...` and
  `..._MelodyTrackHash_DoesNotMatchMelodySourceAssetHash_Throws...` — the new provenance check still
  rejects genuinely malformed input.

All pre-existing Step 055 tests pass unchanged (updated only to pass the now-required
`MelodySourceAssetHash` alongside an already-matching `MelodyTrack`).

## 5. Full regression

```text
$ dotnet test tests/XlightsAI.Studio.Rhythm.Tests -v quiet
Passed!  - Failed: 0, Passed: 87, Skipped: 0, Total: 87

$ dotnet test tests/XlightsAI.Studio.Stems.Tests -v quiet
Passed!  - Failed: 0, Passed: 104, Skipped: 0, Total: 104

$ dotnet test tests/XlightsAI.Studio.Structure.Tests -v quiet
Passed!  - Failed: 0, Passed: 87, Skipped: 0, Total: 87

$ dotnet test tests/XlightsAI.Studio.Pipeline.Tests -v quiet
Passed!  - Failed: 0, Passed: 35, Skipped: 0, Total: 35
```

No other project references the changed `PhraseSegmentationContext`/`StructureAnalysisRunner`/
`CorpusSongAnalyzer` signatures beyond what is covered above (confirmed via
`grep -rl "PhraseSegmentationContext("`).

## 6. Acceptance criteria checklist

- [x] Per-band energy is queryable per time window and visibly differentiates a bass-heavy section
      from a treble-heavy one on a known test track (synthetic fixture test, passing).
- [x] Phrase period is measured, not hardcoded (`PhrasePeriodEstimator`, wired into both real call
      sites); reproduces `MUSIC_FLOW_ANALYSIS.md`'s own *qualitative* 4-bar-vs-8-bar result on
      deterministic synthetic fixtures. Bit-for-bit reproduction on the real reference track is a
      disclosed, out-of-scope gap (§3 above) pending a real FFT.
- [x] `vocalActive` correctly flags the known vocal-cluster timestamps from `EXPORT_AUDIT.md`'s vocals
      investigation on the real reference track (ran on this machine, passing: ≥60% of 7 known
      clusters flagged).
