# Build Plan: Phase 21 — Real, Measured Effect Intensity Profiles

**Status:** proposed, not yet implemented. Written 2026-11-09. Second of the 4-phase arc (20-23) -- see
Phase 20 §0 for the full framing prompt. This phase builds the one missing piece Phase 22 (contrast-driven
emphasis) cannot work without: a real, measured notion of how bright/full a given effect actually reads,
not a guess and not just prose.

---

**Design decisions confirmed with the user (2026-11-09):**
- The brightness metric is **average brightness per pixel per effect** (§2a), confirmed verbatim.
- Critically, this must be averaged across the **whole frame**, not just the lit pixels -- the user named
  real effects that should land in the `Dim` tier specifically *because* most of their own canvas is dark
  at any instant: "fireworks, snowflakes, snowstorm, meteors... all things that have a lot of pixels that
  are not on or dim." A sparse, scattered effect with a few intensely bright pixels must still measure low
  on this metric, the same as a uniformly low-brightness effect -- coverage sparsity IS dimness for this
  purpose, not a separate concept. §2a is updated below to make this explicit, and §4's own spot-check now
  names these effects directly.

---

## 1. Why this phase exists

The user's own insight, verbatim: "if things are just from an being on to being on or a bright effect to
another bright effect that will not make the emphasis felt... it has to go from off or dim to bright." To
act on that, the system needs to know, for a real effect (and its real settings), roughly how bright/full
it reads -- and today it does not. The closest existing data is Phase 18's `EffectVisualCharacterCatalog`
(`src/XlightsAI.Studio.Knowledge/SeedData/effect-visual-characters.json`, 106 entries) -- but every entry is
a free-text, AI-observed *description* ("a dense, uniform amber grid holds steady," "broad bands slide
across the fully lit matrix"), never a number. There is no structured "Off/Dim/Bright" tier or a 0-1
brightness value anywhere in the codebase today -- confirmed by inspection, not assumed.

The good news: Phase 18 already built the exact infrastructure this phase needs to MEASURE brightness for
real instead of asking an LLM to guess at it a second time. `EffectFixtureRenderer`/`EffectFixtureSyntheticShow`
(`src/XlightsAI.Studio.Choreography/`) already render every effect onto a real synthetic fixture and produce
real pixel frames (the same frames Phase 18's Description text was written from). A real average-pixel-
brightness/percent-of-pixels-lit measurement over those SAME frames is a cheap, honest, zero-guessing
addition -- not a new rendering pipeline.

## 2. Proposed technical changes

### 2a. Measure real brightness from the existing Phase 18 fixture renders
Extend the offline Phase D/E characterization sweep (`tools/EffectCharacterizer`,
`EffectVisualCharacterizer.cs`) to also compute, per rendered frame, the mean pixel luminance **averaged
over every pixel in the fixture, lit or not** (never only the subset that happens to be lit) -- this is the
one metric the user explicitly confirmed, and it is what makes a sparse/scattered effect (fireworks,
snowflakes, snowstorm, meteors -- named directly as real `Dim`-tier examples) measure as dim even though its
individual lit pixels may be intense: most of the canvas is dark at any instant, so the whole-frame average
is low. Aggregate across an effect's own render into a real `MeanBrightness` (0-1, whole-frame average) and
`PeakBrightness` (0-1, the single brightest frame's own whole-frame average) -- measured, not estimated.

### 2b. A coarse, real tier on top of the continuous measurement
Bucket `MeanBrightness` into `Off` (effectively dark), `Dim` (sparse/low coverage), `Bright` (dense/high
coverage) using real, disclosed thresholds (not arbitrary -- derive the two cut points from the real
distribution across all 106+ characterized effects, e.g. tercile boundaries, so the tiers mean something
relative to this app's own real effect vocabulary rather than a guessed absolute number).

### 2c. Extend `EffectVisualCharacter`/`EffectVisualCharacterCatalog` with the new fields
Add `MeanBrightness`, `PeakBrightness`, `BrightnessTier` to the existing record (additive, backward
compatible -- every existing consumer of `Description` is unaffected). Re-run the committed offline sweep
once to populate real values for all 106+ entries; ships the same way the catalog already does (committed
seed data, never computed at runtime).

### 2d. Per-settings adjustment, not just per-effect-name
A brightness measured from the DEFAULT settings render is a reasonable baseline, but a real `SectionScript`
can rebind palette/duty-cycle-affecting settings. Disclose this explicitly as a known simplification in
this phase (Phase 22 consumes the effect-name-level tier as a baseline signal, not a perfectly
settings-aware one) rather than silently pretending it's exact -- a more precise per-settings measurement is
a candidate for a later pass if Phase 22's real results show the coarse version isn't good enough.

## 3. Sequencing and dependencies
Independent of Phase 20 (different data: effect intensity, not multi-layer composition). Hard dependency for
Phase 22 -- nothing in Phase 22 can be built before this phase's `BrightnessTier` exists.

## 4. Acceptance criteria
- Every effect in `EffectVisualCharacterCatalog` carries a real, measured `MeanBrightness`/`BrightnessTier`
  (not null, not a placeholder).
- The tier boundaries are derived from this app's own real effect-vocabulary distribution and documented
  (the two cut values, and why).
- A real spot-check specifically confirms the user's own named examples land as expected: "Fireworks,"
  "Snowflakes," "Snowstorm," and "Meteors" (or this app's closest real equivalent effect names) measure into
  the `Dim` tier despite having individually bright pixels, while "On"/"Color Wash"/"Fill" measure `Bright` --
  before Phase 22 starts consuming any of this.
