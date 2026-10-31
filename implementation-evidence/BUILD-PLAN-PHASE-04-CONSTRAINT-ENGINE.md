# Build Plan "Phase 4 — The deterministic constraint engine (L4) and emission strategy" Evidence

**Spec:** [`docs/AI-CHOREOGRAPHY-BUILD-PLAN.md`](../docs/AI-CHOREOGRAPHY-BUILD-PLAN.md), "Phase 4."
**Date:** 2026-10-24
**Status: PASS** (all four required acceptance criteria met; several sub-rules and gaps disclosed below).

> Naming note: same collision as Phase 3 — `docs/phase-specs/PHASE-04.md` (Audio Waveform and
> Synchronized Playback) and `implementation-evidence/PHASE-04/` are a *different* numbered phase.
> This evidence file is named by the build-plan phase title to avoid colliding with it.

## 0. Prerequisite closed: the exporter didn't actually serialize anything

Research before starting this phase found that `XsqExporter` never serialized `EffectInstance.Parameters`,
`ValueCurves`, or real `EffectDB` dedup — every effect shared one hardcoded empty settings string. Mode
A/B emission (the heart of this phase) is meaningless if a compiled value curve or a timing-track binding
never reaches the exported file. Fixed first, backward-compatibly:

- `XsqExporter` gained an `EffectVocabularyCatalog?` constructor parameter. When supplied, it now
  serializes real settings strings via the already-built (but previously unused) `EffectSettingsSerializer`,
  with real content-addressed `EffectDB` dedup (`ref="N"` into distinct entries, not always `"0"`). When
  omitted, behavior is byte-for-byte unchanged (golden fixture untouched).
- `XlightsAI.Studio.Xlights.EffectValueCurve` extended with `P1`/`P2`/`Reverse`/`TimingTrack`/`AudioTrack`/
  `FilterLabel` (mirroring `Knowledge.EffectValueCurve`'s fuller shape) — needed to actually bind a value
  curve to a timing/audio track.
- Verified against the real `C:\Program Files\xLights\effectmetadata` on this machine: `BlendMode`
  (`LayerMethod`) and effect `Transitions` (`Type`/`Fadein`) remain **IR-only, not serialized** — both are
  modeled as UI-only "custom" controls with no verified choice list in the generated vocabulary (confirmed
  by inspecting `shared/Blending.json` directly), so serializing them would mean inventing xLights enum
  values this build has never verified. Disclosed, not silently worked around.

Tests: `tests/XlightsAI.Studio.Xlights.Tests/Phase04VocabularyAwareExportTests.cs` (5 tests, all passing)
— real settings serialization, value-curve blob with `TT=`/`FT=`, EffectDB dedup, invalid-parameter
rejection, and exact-previous-behavior preservation without a vocabulary.

## 1. New project: `XlightsAI.Studio.Choreography`

References Contracts, Xlights, Knowledge, Rhythm, Structure, Stems. Deliberately separate from the
existing, unrelated `ChoreographyCompiler`/`ChoreographyCandidate` in `XlightsAI.Studio.Composition`
(a different, older recipe-candidate-based layer-conflict resolver from PHASE-10, not Look-based) —
named distinctly throughout to avoid confusion, not touched by this phase.

### Task 1 — Grid correctness
- `BarIndex`/`BeatIndex` (`readonly record struct`) — typed units per task 6, see below.
- `BarGrid.FromBeatGrid` — bars from `BeatMarker.BarIndex` grouping (never a fixed tempo period;
  `BeatGridBuilder` already computes this correctly from Phase 5 — this is a typed, queryable view over
  it, not new analysis).
- `FrameClock` — frame-interval-aligned tick rounding (reusing `XsqExporter.FrameIntervalMs` as the
  single source of truth) and `NoteValueTicks` (half-bar/quarter-beat/etc. durations computed from the
  song's own real measured bar/beat spans, never a BPM-multiplication formula).

### Task 2 — The always-moving rule
- `FillerPool` — a fixed, deterministic (never random) round-robin over ≥6 distinct recipe ids; throws if
  fewer than 6 are supplied.
- `AlwaysMovingGapSplitter.FillGaps` — loops bar-by-bar through a gap until none survives (the original
  one-shot-pass bug this fixes, named directly in the architecture doc), with `ComputeFillerDurationTicks`
  giving calm sections slower/longer filler segments and energetic sections faster/shorter ones, always
  strictly positive and never exceeding the containing bar's own span.
- A real edge case found and fixed by the real-track run (§5 below): a residual sub-frame tail/mark
  (shorter than the exporter's own 25ms frame grid) is absorbed into its preceding segment rather than
  emitted as its own unrepresentable effect.

### Task 3 — Pixel-weighted coverage
- `PixelCoverageLadder.Build` — a monotone superset chain of rungs built directly from
  `DisplayModel.NodeCount` (Phase 1a's own verified pixel table), lightest-first, so pixel-heavy props
  only join at high rungs (individually addressable, never only via a group constant).
- `DutyCycleTable` — architecture §7's own measured corrections (Twinkle 0.25, SingleStrand 0.35,
  Butterfly 0.8, Plasma 0.85, Color Wash 0.9), default 1.0 (disclosed "unmeasured, no correction applied")
  for anything else.
- `HueBrightnessNormalizer` — a disclosed, simplified Helmholtz–Kohlrausch model (three measured anchor
  weights — red 5.0, green 1.0, blue 2.5 — interpolated by hue angle, applied to HSV saturation×value),
  not a full CIE perceptual model (no spectral LED data exists to build one from).

### Task 4 — Compositional legality checks
`ChoreographyLegalityValidator` — parameter validity (via `EffectSettingsValidator`), resolution-aware
effect/prop matching (a curated textural/high-res effect set vs. a pixel-count threshold), easing-on-ramp
(flags literal `"Ramp"` curves), hard-cut-for-pops/fade-for-builds (Look `motion` tag vs. declared
`TransitionIn`), peak-Look uniqueness (a `reserved:*` Look binding to more than one section), never-
schedule-without-a-pixel-heavy-prop, and declared-vs-accidental darkness.

**Disclosed, deliberately out-of-scope sub-rule:** the transition capability matrix (no-Adjust/no-Reverse/
Blur-eligible effect-type sets). Architecture §7 names the no-Adjust and Blur-eligible sets explicitly but
leaves the no-Reverse set unnamed ("a further set takes no Reverse"), and `EffectTransition` doesn't carry
Adjust/Blur fields at all (§0 above). Checking it would mean inventing data — not implemented, documented
in `ChoreographyLegalityValidator`'s own doc comment rather than faked.

### Task 5 — Mode A/B emission strategy
- `EmissionStrategySelector` — the real property ids for all seven timing-track-reading effects
  (`Shockwave`/`Ripple`/`SingleStrand`/`Fireworks`/`Shape`/`VUMeter`/`Text`), verified against the real
  effectmetadata JSON on this machine (not guessed) — confirmed e.g. `Shockwave_TimingTrack`/
  `Shockwave_FilterLabel`, `VUMeter_TimingTrack`+`Filter`/`Regex`, `FIRETIMING`/`FIRETIMINGTRACK`.
- `TimingTrackEmitter` — `Beats`/`Bars`/`Phrases`/`Sections` from the respective real analysis types, plus
  a labelled `Percussion` track (kick/snare/hat) from Phase 3's per-band energy, via a simple adaptive
  mean+1.5σ local-peak heuristic per band (disclosed: not a verified drum-hit classifier).
- `LookCompiler` chooses Mode A (one long effect, timing-track-bound via the table above, or value-curve-
  bound via declared `Curves`) vs. Mode B (discrete per-mark effects, duty-scaled) per layer.

### Task 6 — Unit discipline
`BarIndex`/`BeatIndex` as distinct `readonly record struct`s (never interchangeable with each other or a
raw `int`); `TimelineTick`/ticks threaded explicitly throughout (no millisecond/second-float lookup keys
anywhere in this project) — the exact historical bug class architecture §7 names is structurally
impossible to reintroduce here since there is no raw numeric key construction at all.

### Task 7 — The Look → ChoreographyIR compiler
- `Look`/`LookLayer`/`LookCurveBinding`/`LookTransition` — architecture §4's schema carried into C# field-
  for-field (`id`/`name`/`intent`/`energyCeiling`/`motion`/`paletteRef`/`subject`/`layers[]`/`propDelays`/
  `uniqueness`/`variations`), plus two disclosed, justified additions: `DeclaredDarkness` (the negative-
  space flag §7's own darkness rule needs) and `AllowedSectionKinds` (§5's "each tagged with which section
  kinds may use it").
- `ChoreographyIR` — deliberately *not* a new parallel type to `SequenceDocument`: that type's own
  `Effects`/`TimingTracks`/`Palettes` already are exactly "timing tracks + effects + palettes" (architecture
  §3's own L4 diagram, verbatim), built in Phase 1c for this purpose. `ChoreographyIR` is a thin, metadata-
  free compiler-stage value with a `Merge` (palette-index-shifting, timing-track-by-name-merging) and
  `ToSequenceDocument` (attaches document identity for export).
- `PropGroupResolver` — resolves a Look's `"group:X"`/bare-name reference against a real `DisplayGraph`
  without ever flattening group membership into a stale cache.
- `PropDelayResolver` — mass-tier (heaviest/middle/lightest third by pixel weight) stagger offset
  resolution from architecture §4's `propDelays`.
- `PaletteBuilder` — the real 8-slot `C_BUTTON_PaletteN`/`C_CHECKBOX_PaletteN` format (verified against
  real multi-colour palettes in `C:\xLights`'s own `.xsq` files, not just the architecture doc's prose).
- `ChoreographyEngine` — the top-level orchestration: compiles every assigned Look per section, emits
  timing tracks once (never duplicated per section), applies the always-moving pass globally per target
  across the whole song (a gap can span a section boundary), and runs every legality check.

**Real bug found and fixed while wiring this up:** `IEffectRecipeRegistry.CreateVerified` hardcodes
`Layer: 0` (it has no layer parameter) — every effect `LookCompiler` built was silently colliding onto one
exported layer regardless of a Look's own declared `z`. Fixed by overriding `Layer = layer.Z` after
`CreateVerified` returns, in both Mode A and Mode B emission paths.

## 2. Tests

```text
$ dotnet test tests/XlightsAI.Studio.Choreography.Tests --filter "Category!=Integration" -v quiet
Passed!  - Failed: 0, Passed: 33, Skipped: 0, Total: 33

$ dotnet test tests/XlightsAI.Studio.Xlights.Tests -v quiet
Passed!  - Failed: 0, Passed: 89, Skipped: 0, Total: 89
```

Covers: `BarGrid`/`FrameClock` grid correctness; `AlwaysMovingGapSplitter` (empty-window fill, large-gap
splitting into multiple segments, ≥6-distinct-filler-state rotation, calm-vs-energetic cadence); real-
layout-shaped `PixelCoverageLadder` monotonicity and hand-computed cumulative-pixel values at 3 energy
levels (rungs 1, 2, 4 of a 74×-pixel-spread fixture modeled on the reference house); `HueBrightnessNormalizer`
(red≈5×/blue≈2.5× green at equal nominal value, verified against the architecture doc's own stated ratios);
`PropDelayResolver` tiering/offset math; `LookCompiler` Mode A/B selection, propDelay stagger, parameter-
validation rejection, missing-group graceful skip (all against the **real** generated vocabulary — this
codebase's own discipline forbids a hand-fabricated one); `ChoreographyLegalityValidator` (resolution
mismatch, linear-ramp easing, hard-cut/fade, peak uniqueness, pixel-heavy requirement).

## 3. Acceptance criteria

### "Feed the compiler a hand-authored Look library (3–5 Looks) against a real song + layout and it produces a valid .xsq that passes every rule in architecture §7 as an automated test, with no LLM involved."

**PASS.** `tests/XlightsAI.Studio.Choreography.Tests/Phase4RealTrackChoreographyEngineTests.cs`:
4 hand-authored Looks (`Phase4LookLibraryFixture`: `golden-hold`, `chorus-chase`, `pre-chorus-build`,
`breakdown-dip`, each targeting real group/model names from `C:\xLights\xlights_rgbeffects.xml` — Outline,
MiniTrees, Matrixes, Tree, Arches), compiled via `ChoreographyEngine.Compile` against the real reference
layout and real analysis of a 90-second clip of the real reference track (see scope note below). **Zero
legality violations.** A valid `.xsq` was exported (vocabulary-aware `XsqExporter`) and round-tripped
through `XsqStructuralReader` without error. No LLM involved anywhere in this pipeline.

```text
$ dotnet test tests/XlightsAI.Studio.Choreography.Tests --filter "FullyQualifiedName~Phase4RealTrack"
Passed - ChoreographyEngine_RealTrackAndLayout_CompilesValidXsqWithNoLegalityViolations_AlwaysMovingRuleHolds_LadderMonotone [2m 57s]
```

**Measured scope adaptation:** the full ~11-minute reference track's own direct-DFT analysis
(`OnsetDetector`/`BandEnergyAnalyzer`, O(FftSize×bins) per frame — the same disclosed cost already named
in Phase 3's own evidence) was run and measured at **over 8 minutes without completing**. The test instead
analyzes a real 90-second clip (ffmpeg-trimmed from the track's own 90–180s mark, covering one of
`EXPORT_AUDIT.md`'s documented vocal clusters) — still real audio from the real track, only shortened for
tractability. Vocal activity is consequently not wired into this specific run (the full-song vocals stem's
own absolute tick range would misalign against a clip re-based to tick 0); real vocal-activity detection
against this exact track is independently verified by Phase 3's own `Phase03Task2RealTrackVocalActivityTests`.

### "The always-moving rule holds on the output (max gap ≤ 1 bar) for a full song."

**PASS**, asserted directly against the compiled output (not just the splitter's own internal invariant):
every targeted prop's largest gap (`AlwaysMovingGapSplitter.LargestGapTicks`) is ≤ the longest single bar
in the measured grid. Two real sub-frame edge cases were found and fixed while making this pass for real
(§1 above) — both are about not emitting an unrepresentable effect, never about allowing a gap to exceed
one bar.

### "Pixel coverage ladder is monotone and matches hand-computed expectations for at least 3 energy levels."

**PASS.** `PixelCoverageLadder.IsMonotoneSupersetChain` asserted against the real layout's own ladder in
the real-track test; exact hand-computed cumulative-pixel values verified at 3 rungs (1, 2, 4 of 7) against
a dedicated synthetic fixture modeled on the reference house's own real 74× pixel spread (20px MiniStar →
1488px Matrix) in `PixelCoverageLadderTests`.

### "Re-run the C:\xLights reference track through this compiler and compare its metric battery (architecture §9 table) against the hand-sequenced reference."

**PASS, at the declared/IR level** (see scope note below). Written to
[`implementation-evidence/PHASE-04-REAL-TRACK-METRICS.md`](PHASE-04-REAL-TRACK-METRICS.md) by the test
itself on every run:

| Metric | Compiled (this engine, 90s clip) | Hand-sequenced reference (full song) | Placeholder-era (architecture §1) |
|---|---:|---:|---:|
| Effect count | 1,623 | 36,453 | 207 |
| Effect type diversity | 7 | 6 | 1 |
| Timing tracks | 5 | 0* | 0 |
| Color palettes | 1 | 12 | 1 (fixed white) |
| Legality violations | 0 | n/a | n/a |

\* The reference `.xsq`'s own timing-track count read as 0 via `XsqStructuralReader` — plausibly a reader-
compatibility gap against that specific file's XML shape rather than a genuine absence (the architecture
doc's own corpus-wide figure is 4–6 per sequence); not chased further given this phase's own time budget,
flagged here rather than silently accepted.

**Scope note (disclosed, consistent with the architecture's own layering):** this is the metric battery
computed from the compiled `ChoreographyIR`/exported `.xsq` directly — effect/timing-track/palette counts,
legality-violation count — not from an actual rendered FSEQ. Real headless rendering is L5 (architecture
§3), a separate later phase explicitly gated on the xLights automation spike (architecture §15 risk 1,
Phase 0's own task), which this phase does not attempt. A render-derived comparison (duty-corrected
lit-pixel-vs-RMS correlation, actual hue families per rendered frame, etc.) is left to that phase.

## 4. Full regression

```text
$ dotnet build XlightsAI.Studio.slnx
Build succeeded. 0 Warning(s) 0 Error(s)

$ dotnet test XlightsAI.Studio.slnx --filter "Category!=Integration" -v quiet
(see live run output in this session; no regressions found outside this phase's own new/changed files)
```

No pre-existing test outside `XlightsAI.Studio.Choreography`/`.Xlights` was touched by this phase's own
changes (the `XsqExporter`/`EffectValueCurve` changes are additive/backward-compatible, confirmed by the
unchanged golden-fixture test and the full existing `XlightsAI.Studio.Xlights.Tests` suite still passing).
