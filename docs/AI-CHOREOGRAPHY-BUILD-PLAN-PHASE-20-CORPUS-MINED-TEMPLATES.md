# Build Plan: Phase 20 — Corpus-Mined Multi-Layer Templates & Density Grounding

**Status:** proposed, not yet implemented. Written 2026-11-09. First of a 4-phase arc (20-23) answering the
user's own follow-up design brief below -- Phase 20 builds the *reusable pattern library* half of it; Phase
21 builds a *real, measured* notion of effect brightness/intensity; Phase 22 is the actual *contrast-driven
emphasis* mechanism (why an "on" moment doesn't register as an accent unless it's reached FROM something
dimmer/off); Phase 23 is a corpus-validation follow-up once Phase 22 has run for real.

---

## 0. Framing prompt (the mission brief this 4-phase arc answers)

> I'm a senior software developer, but I think about this product the way a real light choreographer does.
> Right now the app can analyze a song's structure, beats, lyrics, vocal melody, and musical key better than
> it ever has -- but the actual light output still falls back on a small, repeated handful of effect
> combinations, and even when it DOES hit an emphasis moment, it often just swaps one bright effect for
> another bright effect. That never reads as a hit to a viewer. A real accent is felt because of CONTRAST --
> something was dim, or off, or quiet, and then it popped. Sameness-to-sameness is invisible, no matter how
> "on-beat" the timing technically is.
>
> I want two things built, in order: first, wire up a real pattern library mined from our own real sequence
> corpus (not guesses) so the Choreographer has actual reusable multi-layer effect combinations to draw
> from -- more variety, not just more repetition of the same 2-3 looks. Second, and more importantly: teach
> the system that repetition and emphasis are opportunities to turn things OFF or DOWN before turning them
> back ON or UP. When a song repeats a hook, or hits a big lyric, or lands on the beat, the light show should
> visibly breathe -- dim-to-bright or off-to-on, not bright-to-bright -- so the viewer actually FEELS it,
> not just technically has an effect scheduled at the right millisecond.
>
> Build me a plan. Tell me how many phases, give me the first prompt to kick off the first phase, and tell
> me what you still need decided before you start.

---

## 1. Why this phase exists

Phase 19 (Corpus-Grounded Key, Layering & Effect-Combination Mining) proved the real corpus (31 songs,
66,568 effects, now excluding every bit of this app's own generated output) is rich enough to teach real
patterns, not guesses -- one new rule promoted at 98.2% confidence, two others honestly rejected rather than
forced. But two real, computed artifacts from that same mining run are not reaching generation at all:

- `pattern-library.json`'s own `DensityBySection` (real measured effect-change density: Chorus 13.05 vs
  Verse 5.19 vs Intro/Outro ~3.5-4.8) and `LayerCountPerModelDistribution` (real per-model layer-depth
  counts) -- computed, persisted, **never loaded by `DirectorKnowledgeContext`**.
- The shippable `MultiLayerTemplateCatalog` (`src/XlightsAI.Studio.Knowledge/SeedData/multilayer-tree-matrix-templates.json`)
  is still only 3 hand-curated templates, derived from 5 songs back in Phase 10 §6a/§6b -- the exact
  "reusable multi-layer effect" concept the user asked about, just never grown past its first hand-curated
  pass even though the real corpus is now 6x larger and has every `AlignedEffectInstance` (model, effect
  name, start/end time, layer index) needed to extract real stacks automatically.

## 2. Proposed technical changes

### 2a. Auto-extract real multi-layer stacks from the corpus
For each mined song, group `AlignedEffects` by `ModelName`; wherever 2+ `EffectLayer`s are genuinely
time-overlapping (reuse Phase 19's own `MaxConcurrentOverlap` sweep), record the real effect-name stack (in
z-order by `LayerIndex`), the real measured duty cycle per layer (fraction of the overlap window each layer
is actually present), and a coarse cadence bucket (beat/phrase/section, derived from how its own start/end
compares to the song's beat grid -- already available via `AlignedEffectInstance.StartBeatWithinBar`/
`PhraseBars`). Cluster the resulting real stacks by `SectionKind`'s own energy tier (reuse
`EnergyCeilingByKind`'s existing high/mid/low banding).

### 2b. Promote the most common real stacks into an expanded `MultiLayerTemplateCatalog`
Same shippable, palette-free, song-free shape `MultiLayerTemplate` already declares -- this is an additive
generator, not a new schema. Keep a minimum real-occurrence threshold (seen in at least N distinct songs,
same "never forced" discipline Phase 19 established) before a mined stack becomes a shipped template, so the
catalog grows from 3 to however many real patterns actually clear the bar -- reported honestly either way.

### 2c. Wire `PatternLibrary`'s real density/layer statistics into the Director prompt
Load `pattern-library.json` (the same way `CraftRules`/`ExemplarIndex` are already loaded) into
`DirectorKnowledgeContext`, and render `DensityBySection` as a real, measured per-`SectionKind` baseline
next to the existing hand-tuned `EnergyCeilingByKind` guidance -- not a replacement (the ceiling still
drives the real compiled density), a grounding cross-check so the Director's own stated expectations line up
with what real shows actually do.

## 3. Sequencing and dependencies
Directly extends Phase 19 (reuses its corpus, its `MaxConcurrentOverlap` helper, its "promote or honestly
reject" discipline). Must land before Phase 22 (Phase 22's contrast mechanism is far more useful once the
Choreographer has a wider real template vocabulary to alternate between, not just the original 3).

## 4. Acceptance criteria
- `MultiLayerTemplateCatalog` contains more than 3 templates, each traceable to real corpus occurrences
  (song count + example source songs recorded, same transparency Phase 19's `CraftRule` reporting already
  has).
- `DensityBySection`/`LayerCountPerModelDistribution` are visibly present in a real logged Director prompt.
- A real regenerated song shows measurably more distinct multi-layer stacks used across its own Looks than
  the current 3-template ceiling allowed.
