# LLM-Driven Choreography Architecture for XlightsAI.Studio

*Design document — architecture, reasoning, and evidence. Deliberately not a task breakdown.*

---

## 1. Context

XlightsAI.Studio does the hard engineering well and the creative work not at all. Its pipeline
genuinely analyzes a song — stem separation, onset/tempo/beat detection, pitch→note→gesture
extraction, bar/section/phrase/motif structure with real musical section labels — then throws nearly
all of that understanding away at the final step, because the only thing it knows how to *draw* is a
flat on/off block.

The gap is measurable. The app's own export against the sequences in `C:\xLights`:

| | App output (`Bad Feelings/exports/sequence.xsq`) | Corpus (31 hand/vendor sequences) |
|---|---|---|
| Effects | **207** | 1,114 – 36,453 |
| Color palettes | **2** | 32 – 78 |
| Effect layers | **23** (exactly one per element) | 60 – 103 |
| Effect types used | **1** (`On`) | 38+ distinct |
| Timing tracks | **0** | 4–6 (Beats, Bars, Percussion, Moments, Lyrics) |
| Longest single block | **26.5 s** | sub-bar |

Every effect it emits is `<Effect ref="0" name="On" palette="0">`. A 26-second `On` block violates
the project's own most important rule — *nothing sits unchanged longer than one bar* — by fifteen
times. This is not a mediocre sequence; it is a placeholder.

The root cause is precisely identifiable and is **not** the analysis. `XlightsAI.Studio.Composition`
and `.WholeSong` already contain a creative-direction layer — `VisualStory`, `VisualBudget`,
`TransitionPlan`, `MotifVariationScheduler`, `PhraseSeamAssembler`, `SongContinuityEvaluator` — and
**none of it is wired into the generation path**. The CHANGELOG names the reason: the only effect
recipe is a flat on/off with no intensity parameter to scale. One missing abstraction at the bottom
of the stack has stranded an entire creative layer above it.

Meanwhile the hardest part has *already been solved by hand*. The notes in `C:\xLights` —
`CLAUDE.md`, `MUSIC_FLOW_ANALYSIS.md`, `EXPORT_AUDIT.md`, `SEQUENCE_PLAN_v2.md`, `RENDER_CRITIQUE.md`,
`patterns.md` — are an empirically validated craft rulebook plus a worked generate → render →
measure → critique → revise loop, with reference-video comparison and numeric verification tables.
**That manual process is the architecture.** This document describes how to make it a system.

**Intended outcome:** sequences indistinguishable from vendor work, from a song plus a show layout,
with taste decided by an LLM and correctness guaranteed by deterministic code — a few dollars per
song on the customer's own key, provider-agnostic. The baseline being competed against is roughly
**18–24 hours of hand sequencing per song**.

---

## 2. The central design decision

> **The LLM emits *intent*. Deterministic code emits XML. And wherever xLights can do the
> beat-locking itself, we let it.**

A 3-minute song on a 23-model house could need 10,000–35,000 `<Effect>` elements. No model emits
that reliably, and the failure mode is silent: ask for 30,000 timed events and you get something
structurally plausible that renders as garbage.

But the *creative content* is small — maybe 2,000 tokens of real decision-making: which 8–20
reusable **Looks** the song uses, which palette belongs to which section, which prop carries the
melody, where the climax is and what makes it unmistakable, when to withhold. Professional lighting
practice says the same: *"if you have a cue for each section of the song structure, you're probably
headed in the right direction"* — roughly eight looks per song, not hundreds.

Four independent lines of evidence say to split here:

1. **Structured decoding taxes reasoning.** Strict JSON/XML format constraints cost a documented
   10–30% reasoning quality, with value accuracy degrading further as nesting deepens. One
   schema-constrained call emitting a flat array of 500 timed events is close to worst case: the tax
   lands on exactly the creative decisions that matter. Keep schemas **flat, wide, shallow and short
   per call**; let taste happen with room to reason, and serialize separately.
2. **Pose-to-pose beats straight-ahead.** Animators working linearly from t=0 produce work that
   drifts and loses shape. The long-form-generation literature converged independently: explicit
   pre-writing planning plus per-section expansion yields 10–50% quality gains over direct generation.
3. **The knowing–doing gap.** An LLM will correctly state "don't use rainbow palettes" and then emit
   one. Anything expressible as a checkable predicate must be a predicate, not a prompt instruction.
4. **xLights already does beat-locking natively** — see §6. Much of the per-beat emission this design
   originally assumed turns out to be *unidiomatic*, not merely expensive.

---

## 3. System layers

```
┌─ L0  ANALYSIS ──────── exists; needs 4 specific extensions ───────────────┐
│  audio → MusicModel: beat grid by index, bars, sections, phrases,         │
│  motifs, per-band energy, onsets, vocal activity, notes/gestures          │
└───────────────────────────────────────────────────────────────────────────┘
                                    ↓
┌─ L1  KNOWLEDGE ─────── new ───────────────────────────────────────────────┐
│  effect metadata schema + corpus .xsq/audio → EffectVocabulary,           │
│  PatternLibrary (verified priors), ExemplarIndex, CraftRules              │
└───────────────────────────────────────────────────────────────────────────┘
                                    ↓
┌─ L2  DIRECTION ─────── new · strong model · once per song ────────────────┐
│  MusicModel + Layout + Style + retrieved exemplars → SHOW BIBLE           │
│  palettes · prop roles · Look library · energy arc · peaks · withholding  │
└───────────────────────────────────────────────────────────────────────────┘
                                    ↓
┌─ L3  CHOREOGRAPHY ──── new · cheap model · per section · batched ─────────┐
│  ShowBible + one section's events → SectionScript                        │
└───────────────────────────────────────────────────────────────────────────┘
                                    ↓
┌─ L4  COMPILATION ───── new deterministic + rebuilt exporter ──────────────┐
│  ChoreographyIR → timing tracks + effects + palettes → CONSTRAINT ENGINE  │
│  → .xsq.  No LLM in this layer.                                           │
└───────────────────────────────────────────────────────────────────────────┘
                                    ↓
┌─ L5  VERIFICATION ──── new deterministic · extends .Rendering ────────────┐
│  render via xLights automation → FSEQ/frames → metric battery             │
└───────────────────────────────────────────────────────────────────────────┘
                                    ↓
┌─ L6  CRITIQUE ──────── new · LLM + vision · external feedback only ───────┐
│  metrics + frames → graded critique → RevisionDirectives → back to L3     │
└───────────────────────────────────────────────────────────────────────────┘
```

L0–L2 run once per song; L3–L6 form the iteration loop.

---

## 4. The Look — the core creative abstraction

A **Look** is a named, reusable, complete composition: what every prop is doing at once. It is the
unit lighting designers actually think in, and exactly what the current app lacks.

```jsonc
{
  "id": "golden-hold",
  "name": "Golden Hold",
  "intent": "Warm full-house steady state for vocal verses. Many props, one colour.",
  "energyCeiling": 0.45,
  "motion": "hold",
  "paletteRef": "warm-gold",
  "subject": "group:Outline",
  "layers": [
    { "z":1, "props":"group:MiniTrees", "effect":"Twinkle", "role":"secondary",
      "params":{"Twinkle_Steps":12}, "blend":"Bright", "timescale":"bar", "duty":0.25 },
    { "z":2, "props":"group:Matrixes",  "effect":"Plasma",  "role":"support",
      "params":{"Plasma_Speed":8}, "blend":"Normal", "duty":0.85 },
    { "z":3, "props":"group:Outline",   "effect":"Color Wash", "role":"subject",
      "params":{"ColorWash_Cycles":1},
      "curves":{"ColorWash_Cycles":{"type":"Timing Track Fade Proportional","track":"Beats"}},
      "blend":"Normal", "transitionIn":{"type":"Wipe","seconds":"halfBar"}, "duty":0.90 }
  ],
  "propDelays": { "mass":"+0ms", "mid":"+60ms", "accent":"+120ms" },
  "uniqueness": "reserved:vocal-verse",
  "variations": ["escalate","invert","paletteShift","densityUp","addGroup"]
}
```

Why it earns its place:

- **Repetition becomes a feature.** Phrases the analysis identifies as structural rhymes (the
  reference track's 126.2 s ↔ 139.6 s pair at cosine distance 0.340) get the *same* Look. xLights
  itself endorses this: its **Song Structure Regions** feature exists specifically to name
  Intro/Verse/Chorus regions, *copy a chorus's effects to the next chorus* with automatic time
  shifting, and recolor a region's palette wholesale. Repetition-with-variation is first-class
  behaviour in the host application, not a hack.
- **Anti-convergence becomes enforceable.** The biggest musical moment rendering as a near
  pixel-for-pixel repeat of an earlier drop was a real measured defect. `uniqueness:
  reserved:peak` makes it a compile-time constraint.
- **Escalation becomes mechanical.** A build needs `golden-hold` with `escalate` over its final bars,
  not a new Look. Nine build sections silently failing to escalate was a gap between stated design
  and code; declared variations let the compiler apply it uniformly.
- **Staging becomes explicit.** Every Look names one `subject`; everything else is `support` or
  `secondary`. Megatree fast-chasing while arches fast-chase differently is two subjects, which is
  zero subjects.
- **Layer semantics are real.** xLights supports up to 200 layers, **layer 1 is the first
  `<EffectLayer>` in document order and is the topmost**, and blend modes speak of "Effect 1" (this
  layer) vs "Effect 2" (below). The three-layer idiom the community actually uses — *Color Wash or
  Plasma background → the pattern (Bars/Marquee/Spirals) → accents (On pops, Shockwave, Strobe)
  blended Additive or Max* — maps directly onto `z`. The current exporter emits one layer per
  element, which structurally caps how rich it can ever look.
- **Overlapping action becomes data.** `propDelays` keyed to prop mass is what makes a display read
  as having physics rather than being synchronized switches. (xLights also offers this free at the
  buffer level via `B_SLIDER_BufferStagger` — "percent offset applied to effect start time across
  nodes for a cascading appearance".)
- **The LLM output stays tiny**, and a human can review the whole show before a frame renders.

---

## 5. The Show Bible (L2)

One call, strongest model, whole song in view, room to reason before serializing. Produces:

- **Palette set** — 3–5 named palettes, each a real xLights `ColorPalette` string. The mechanism
  matters and is easy to get wrong: a palette is **always eight `C_BUTTON_PaletteN=#RRGGBB` slots
  plus `C_CHECKBOX_PaletteN=1` marking which are active.** *Palette discipline is expressed by which
  checkboxes are ticked, not by which buttons are set.* A two-colour blue/yellow palette is eight
  buttons with checkboxes 4 and 5 ticked. The `C_` string also carries `C_SLIDER_Brightness`,
  `C_SLIDER_Color_SaturationAdjust`, sparkle settings, and `C_VALUECURVE_*` entries.
  Assign **functional roles** — base (environment), key/contrast (keeps the subject readable),
  accent (transitions, rhythm, focal moments) — because converting "pick colours" into "assign
  functions" is far more tractable than open-ended colour choice. Strong form of the rule: *the
  chorus accent colour appears nowhere else in the song*, making chorus arrival legible with no other
  cue. The palette *set* is chosen once per song; what changes per section is which members are in
  play and in what role.
- **Prop roles** — assigned from real geometry and pixel mass, informed by research-backed
  correspondences: **bass → large/low/wide props; melody → the tall or focal prop; highs →
  small/high/fast props.** (Pitch→lightness, pitch→vertical-position, loudness→salience and
  spectral-centroid→colour-brightness are supported by replicated cross-modal research.
  **Pitch-class→hue is not** — Scriabin-style mappings are culturally specific and communicate
  nothing to a general audience. Don't build one.) A useful grouping convention from the community:
  `All`, `House`, `Yard`, `Roof`, `Verticals`, `Arches`, `Mini Trees`, `Stars`, `Left`, `Right`,
  `Mega Tree + Star` — and *put effects on groups, with individual props in groups, never groups
  inside groups*.
- **The Look library** — 8–20 Looks, each tagged with which section kinds may use it.
- **The energy arc** — per section: energy ceiling, target change-rate, target hue-family count. The
  contract L5 measures against. An amateur sequence sits at 80–100% all song and has one dynamic
  level; a composed one defines a ceiling per section and never exceeds it. Chorus 1 lands at ~85%,
  deliberately not 100%, so the final chorus has somewhere to go.
- **Peak moments** — the 2–4 bars that must be unmistakable, each with a reserved Look and an
  explicit statement of what distinguishes it from every *other* peak.
- **Signature gestures** — 2–4 recognizable 1–2 bar movements, established early, varied through,
  restated at full scale at the end. Composed shows have a few; random ones have none or forty.
- **Withholding plan** — which builds hold flat and explode only in their final bars, and what the
  reserved "ace" is for the final chorus. The reference show's pre-drop builds are its *calmest*
  moments (0.5 changes/s). Release is perceivable only in proportion to accumulated tension, so the
  budget is spent in the quiet parts.

---

## 6. Emission strategy — let xLights do the beat-locking

**This is the most important finding of the research and it inverts a premise.** The design does not
need to emit tens of thousands of short effects, because xLights has native machinery for
beat-locking a *single long effect*:

**Seven effects read a timing track directly**, each with a `FilterLabel` / `Regex` so they fire only
on marks with a given label:

| Effect | Setting |
|---|---|
| Shockwave | `E_CHOICE_Shockwave_TimingTrack` + `Shockwave_FilterLabel` / `FilterRegex` |
| Ripple | `E_CHOICE_Ripple_TimingTrack` + `Ripple_FilterLabel` / `FilterRegex` |
| Single Strand | `E_CHOICE_SingleStrand_TimingTrack` |
| Fireworks | `E_CHOICE_FIRETIMINGTRACK` |
| Shape | `Shape_FireTiming` / `Shape_FireTimingTrack` + `Shape_FilterLabel` |
| VU Meter | `E_CHOICE_VUMeter_TimingTrack` |
| Text | `E_CHOICE_Text_LyricTrack` |

**Three value-curve families animate a parameter from music or a timing track**, over any property
flagged `"valueCurve": true`:

- `Music`, `Inverted Music`, `Music Trigger Fade` — parameter tracks the audio envelope of a named
  audio track. This is how a Color Wash *breathes* with the song without placing a single extra
  effect.
- `Timing Track Toggle`, `Timing Track Fade Fixed`, `Timing Track Fade Proportional` — parameter
  driven by marks in a named timing track; proportional fades decay scaled to the gap until the next
  mark. **This is the cleanest way to beat-lock a continuous parameter**, and it exists precisely for
  the "pulse on every beat but keep one effect" problem.

So the compiler has two emission modes and should prefer the first:

> **Mode A — bound.** One long effect across a phrase or section, bound to a labelled timing track
> and/or carrying value curves. Idiomatic, compact, musically exact, and self-adjusting if the beat
> grid is corrected later.
>
> **Mode B — discrete.** Per-event effects, for hits, snaps, hard cuts and anything whose effect type
> has no timing-track input.

An LLM that emits one `Shockwave` with `Shockwave_TimingTrack=Beats` is producing *more* idiomatic
output than one emitting 200 `On` effects. This also makes the per-section LLM output dramatically
smaller and the resulting file far closer to how a human sequencer actually works.

**Which means timing tracks are first-class output, not metadata.** The app currently emits none;
every real sequence has several. Timing rows are `Element type="timing"` whose `<Effect>` children
carry only `label`, `startTime`, `endTime`:

```xml
<Element type="timing" name="Beats">
  <EffectLayer>
    <Effect label="kick" startTime="0" endTime="418"></Effect>
    <Effect label="" startTime="418" endTime="836"></Effect>
  </EffectLayer>
</Element>
```

The analysis pipeline already produces everything needed to emit `Beats`, `Bars`, `Phrases`,
`Sections` and (with per-band energy) `Percussion`. **Labelling those marks is what unlocks the whole
filter mechanism** — a `Beats` track labelled `kick` / `snare` / `hat` lets one Shockwave fire on
kicks only. Lyric tracks are the same structure with *three* `EffectLayer` children: phrases, words,
phonemes — which is what drives singing faces (§10).

---

## 7. The deterministic constraint engine (L4)

Everything here is code, not prompt text. A violation is a hard failure the compiler repairs or
refuses to emit. Most were learned the hard way and are written down in `CLAUDE.md` and
`EXPORT_AUDIT.md`.

**Grid correctness**
- Bars defined by **beat index** (`bar b = beats[4b…4b+4]`), never by multiplying a tempo period.
  Beat times jitter ±0.05 s; a fixed-interval grid fits at σ=0.86 s and puts boundaries 0.1–0.9 s off
  the music. By index, all 381 bar lines in the reference track coincided exactly with detected
  downbeats.
- **Phrase period is measured per song, not assumed.** Current code hardcodes an 8-bar hypothesis
  (`PhraseSegmenter.TargetPhraseLengthBars: 8`). On the reference track the 8-bar grid was *not*
  significant (p=0.23) while 4-bar was (p=0.0039). Fit the grid; don't assume it.
- Every effect boundary lands on a beat, sub-beat, or detected onset, and is an integer multiple of
  the sequence frame interval.
- **Fades are expressed in note values, never milliseconds.** `T_TEXTCTRL_Fadein` is stored in
  *seconds as a string* and converted to integer frame counts at render time — so compute "half a bar
  at 143 BPM" and serialize the result, rather than picking `0.8`.

**The always-moving rule**
- No model holds unchanged longer than one bar. The gap-splitting pass must **loop until no gap
  survives**; the original inserted one beat per gap and declared victory, leaving a 17-second frozen
  block past the last detected beat.
- Gap filler draws from a rotating pool of ≥6 distinct states (a single repeated filler once occupied
  36 seconds of one show).
- Calm sections get *gentler* motion and a slower grid, never *no* motion.
- But: **selection, not response.** A strong sequence treats beat detection as a candidate set and
  chooses from it. *(Flagged honestly: "don't hit every beat" is a design axiom here, not sourced
  community canon — research found it universally practised and never written down. The corpus can
  settle it empirically; see §8.)*

**Pixel-weighted coverage**
- A verified pixel table per model, from `NodesPerString` / `NumArches × NodesPerArch`. Model sizes
  span 74× (MiniStar 20px → Matrix 1488px), so "models lit" is meaningless — two sections both
  showing "19 models lit" rendered at 30% and 83% of the house.
- Coverage rungs form a **monotone ladder of strict supersets**, so more energy always reads as *more
  house lit*, never *different props lit*.
- Pixel-heavy props addressable individually, never only via a group constant. Three matrices plus
  the Tree were 69% of that house's pixels; referencing them only as `MATRIXES` produced an
  all-or-nothing block with no staged escalation.
- **Duty-cycle correction per effect** (Twinkle ≈0.25, SingleStrand ≈0.35, Butterfly ≈0.8, Plasma
  ≈0.85, Color Wash ≈0.9). Declared pixel budget overstated rendered brightness by 3–4× because
  Twinkle-family effects light a fraction of nodes per frame.
- **Per-hue perceptual brightness normalization.** The Helmholtz–Kohlrausch effect is real and
  measurable: for narrow-band LEDs, saturated red reads roughly 5× brighter than green and blue ~2.5×
  brighter than green at equal drive. RGB(255,0,0), (0,255,0) and (0,0,255) do **not** look equally
  bright, so an R→G→B chase at constant nominal value visibly pulses and an "equal brightness"
  palette isn't. Almost certainly a contributing factor in the measured "red snap doesn't read as
  red" and matrix-desaturation defects. Normalize — and exploit it: saturated blue at low drive is
  cheap and still reads as lit, ideal for restraint sections.

**Compositional legality**
- **Effect parameters validate against the real schema.** xLights ships
  `resources/effectmetadata/<Effect>.json` for all 56 effects, giving exact property ids, control
  types, defaults, ranges, choice lists, and a per-property `"valueCurve": true` flag. §8 turns this
  into a generated, machine-checkable vocabulary — so an invalid parameter name, an out-of-range
  value, or a value curve on a property that doesn't accept one is a compile error, not a silently
  ignored setting.
- Transition settings respect the capability matrix: Fade/Dissolve/Zoom/Doorway/Swap/Shatter and
  others take **no Adjust**; a further set takes **no Reverse**; only Wipe, Blobs, Clock, Blinds,
  From Middle, Square/Circle Explode, Blend, Slide Checks and Slide Bars support **Blur**. Setting
  an unsupported one is silently ignored — exactly the kind of failure a validator must catch.
- Effect type must suit prop **resolution**, not just geometry: high-resolution props get continuous,
  gradient, textural material (Fire, Liquid, fine chases, Text, Pictures); low-resolution props get
  discrete, graphic material (solid fills, on/off hits, 2–4 band chases). Running the same per-pixel
  effect on a 1000px tree and a 50px arch produces an expensive prop doing a cheap prop's job and a
  cheap prop doing something unreadable.
- Group-as-canvas moments must be **large, simple gestures** — xLights maps a group effect across the
  combined buffer, so low-resolution members get a downsampled version. Fine for big sweeps, bad for
  detail. (Also: prefer "Minimal Grid" buffers; group member order determines render order.)
- Never schedule an outline-only or small-props-only section without at least one pixel-heavy prop.
- Hue families per frame ≤ target (the reference measures 2.40 against our 3.56).
- Hard cuts for pops (`T_TEXTCTRL_Fadein=0.0`, cloning the EffectDB entry), fades for builds.
- Per-model *effect* stagger by index so props don't change in unison — but **not** palette stagger,
  which is what fragmented frame-level colour unity.
- **Easing on every ramp.** Linear fades and constant-velocity chases are the most reliable tell of
  machine sequencing. The value-curve catalogue is rich — `Ramp`, `Ramp Up/Down Hold`, `Sine`,
  `Decaying Sine`, `Parabolic Up/Down`, `Logarithmic Up/Down`, `Exponential Up/Down`, `Saw Tooth`,
  `Square`, `Custom` — so use ease-out for releases, ease-in for builds, ease-in-out for sweeps, and
  linear only deliberately.
- **Intentional vs. accidental darkness.** Near-black is legitimate and powerful — the pre-drop dip
  of 150–300 ms is the highest-impact move in the vocabulary — but it must be *declared in the Look*,
  not fall out of a low RMS value. Undeclared darkness is a defect; declared darkness is negative
  space. (The breakdown that produced 20 fully-black frames was the former.)
- A Look reserved for a peak binds to exactly one section.

**A note on units.** One real bug came from building a per-section lookup key in milliseconds against
a table keyed by second-floats; `.get(key, default)` silently returned the default for every section
and one prop group took 74% of the show. The codebase's canonical unit is **100 ns ticks**
(`TimelineTick`), with conversion to milliseconds deferred to the exporter. Keep that discipline, use
typed `BarIndex` / `BeatIndex` / `Ticks` rather than raw numbers so the bug class cannot compile, and
thread keys through explicitly rather than reconstructing them.

---

## 8. Knowledge: the effect vocabulary and the corpus (L1)

Two distinct knowledge sources, and the first is more valuable than expected.

### 8a. A generated effect vocabulary — not a hand-written one

xLights ships machine-readable metadata for every effect at
`resources/effectmetadata/<Effect>.json`, with exact property ids, control types (`SLIDER`,
`CHOICE`, `CHECKBOX`, `TEXTCTRL`, `VALUECURVE`, …), defaults, ranges, choice enumerations, and
`"valueCurve"` capability. The shared panels (`Buffer.json`, `Blending.json`, `Color.json`) define
the `B_` / `T_` / `C_` namespaces the same way.

**Generate the system's effect vocabulary from that metadata rather than transcribing it.** This
gives, for free: a JSON Schema constraining the LLM to real parameters; a validator that rejects
invalid names, out-of-range values and illegal value curves; correct serialization; and automatic
tracking as xLights adds effects. Hand-maintaining a 56-effect × ~20-parameter table is a guaranteed
source of silent drift.

The serialization rules the writer must honour, all verified against real files:

- Settings are one flat comma-separated `key=value` string, keys formed
  `<Prefix>_<CONTROLTYPE>_<PropertyId>`. `E_` (effect), `B_` (buffer), `T_` (blending/timing) all
  live in the `EffectDB` entry; **only `C_` lives in the `ColorPalettes` table.**
- **Defaults are omitted.** An all-default effect serializes to the empty string → `<Effect></Effect>`.
  *This vindicates the current exporter's empty-EffectDB-entry placeholder, which is idiomatic and
  correct — the standing "unverified placeholder" warning in `XsqExporter.cs` can be retired.*
- Keys are written **alphabetically sorted** by xLights — match it so diffs and dedup are
  deterministic.
- **Commas inside values must be escaped as `&comma;`.** The current `XDocument` approach handles XML
  escaping but not this; a latent bug the moment a Text effect or a file path is written.
- Decimal-valued sliders serialize under `E_TEXTCTRL_<id>`, integers under `E_SLIDER_<id>`; the
  metadata `divisor` says which.
- `LayerMethod` appears under both `T_` and `B_`; **`T_` is authoritative.** A few legacy keys keep
  `E_` names despite living on the Blending panel (`E_CHECKBOX_LayerMorph`,
  `E_SLIDER_EffectLayerMix`), and `Rotations` / `Zoom` use a bare `SLIDER_` prefix.
- Value curves serialize as a pipe-delimited blob inside the comma list:
  `C_VALUECURVE_Brightness=Active=TRUE|Id=ID_VALUECURVE_Brightness|Type=Ramp|Min=0.00|Max=400.00|P1=400.00|P2=300.00|RV=TRUE|`
  with `Type=` omitted when `Flat`, and `TT=` / `AT=` / `FT=` carrying timing-track, audio-track and
  filter-label bindings.
- xLights deduplicates identical setting strings aggressively; `Effect/@ref` is an index into the
  deduplicated `EffectDB`. Do the same.

### 8b. The corpus — verified priors, exemplars, and rules

31 distinct sequences with paired audio: far too few to fine-tune, ample to mine. **Knowledge
extraction, not model training.** Three products:

**PatternLibrary — verified statistical priors.** Parse each `.xsq`, re-analyze its paired audio,
align every effect onto that song's beat grid, measure what sequencers actually do. The corpus-wide
effect vocabulary already falls out:

```
On 36245 · SingleStrand 36069 · Color Wash 17727 · Shockwave 13812 · Twinkle 11478
Morph 5565 · Ripple 3965 · Butterfly 2851 · Pinwheel 2377 · DMX 1214 · Spirals 1077
Fill 996 · Warp 937 · VU Meter 841 · Shader 819 · Pictures 639 · Curtain 632 · Plasma 590
Bars 474 · Text 414 · Meteors 410 · Garlands 256 · Marquee 218 · Wave 162 · Fan 159 …
```

An empirical prior over the vocabulary — and a direct indictment of the current app, which uses
exactly one of these. The same mining yields effect frequency conditioned on prop geometry and
section energy, density curves by section kind, layer-count and blend-mode distributions, palette
size and active-checkbox statistics, transition conventions, Mode A vs. Mode B emission ratios, and
how often a sequence quotes itself. **It can also settle the open questions research couldn't
source** — beat-hit rate, palette-count limits, mirroring practice — by measurement rather than
assertion.

**ExemplarIndex — retrievable excerpts.** Each corpus section indexed by musical fingerprint (tempo,
energy percentile, band balance, vocal activity, section kind). At generation time, retrieve the few
real sections most similar to the one being planned and show them to the Director as worked examples.
Retrieval-augmented few-shot with similarity-selected exemplars outperforms both zero-shot and
fine-tuned models on style tasks while avoiding training cost entirely.

**CraftRules — a *verified* prose rulebook.** The literature gives a specific recipe
(Hypotheses-to-Theories): an **induction** stage where the model proposes candidate rules from
examples, then — critically — **mechanical verification of each rule against the corpus, filtered by
coverage and confidence**, and only then promotion into a library used at inference. The verification
step is what everyone skips and exactly what separates this from "ask an LLM for sequencing tips."

Concretely: show the model an aligned exemplar (what the music did + what the lights did) and ask
what the sequencer was deciding and why. It proposes *"never more than three active palette slots per
section"* or *"the chorus always opens with a full-width sweep on bar 1."* Then check those claims
across all 31 sequences. Survivors get promoted twice — into prompt content, **and into a
deterministic validator**, because of the knowing–doing gap.

This is precisely what was done by hand against the Johnson Family reference, producing durable
findings (*its second drop is busier than its first; pre-drop builds are its calmest moments; verses
are frequently steady full-house holds in a single colour, not chases*). Automating it across the
corpus builds a rulebook larger than one person can write.

One community rule is concrete enough to encode immediately — **the 2/2/4 eight-bar pattern**: *"I
will do an effect for two bars, and then usually reverse the effect for the next two bars, and repeat
that up to 8. After that I'll switch the main effect from, say, marquee to pinwheel and do it all
again."* That is: one effect family per 8-bar phrase, `Direction`/`Reverse` alternating every 2 bars,
family change at phrase boundaries. A ready-made Look variation schedule.

**Licensing — this constrains the corpus materially.** Paid vendor sequences ($70–$100 each)
explicitly forbid redistribution and commercial use; **do not train on or ship them.** Free community
downloads typically carry non-commercial, non-redistribution terms. Clean sources are: sequences the
user authored, and explicitly OSS-licensed repos (`google/kir-holiday-lights` is Apache-2.0 and
contains realistic 50 ms media-backed sequences with four timing tracks and a lyric track). The
design accommodates this cleanly: **ship the *rules*, not the content.** Statistics and abstracted
rules travel; exemplar excerpts stay local, derived from files the user already owns. Each customer's
corpus personalizes the system on their own machine. Note also that audio is never bundled with
shared sequences and `head/mediaFile` is an absolute host path — shared sequences arrive with a
broken media reference by design, so corpus audio pairing must be resolved locally.

**When fine-tuning becomes worth it:** not at 31 sequences. If the corpus reaches the low hundreds
*with paired audio*, a small model fine-tuned for the mechanical L3 expansion could cut cost. The
Director and Critic stay on frontier models — those are taste and judgment, not pattern completion.

---

## 9. The harness: render → measure → critique → revise (L5/L6)

Without this loop the system is a one-shot guess. This is the piece that separates "better than the
placeholder" from "indistinguishable from vendor work" — and it is the piece already proven manually.

**The governing constraint, from the literature:** LLM self-correction *without external feedback* is
at best neutral and is documented as sometimes actively harmful; the gains attributed to
Reflexion/Self-Refine trace to external signal, not introspection. A critic that re-reads the
generator's own JSON and is told "now critique yourself" is a cost multiplier with negative expected
value. **Every critique here must be grounded in something the generator never saw: measured
properties of the compiled sequence, and pixels from an actual render.**

**Render.** `C:\Program Files\xLights\` ships `xlDo.exe` (xLights' automation interface) alongside
`fseq_convert.exe`, and `XlightsAI.Studio.Rendering` already contains FSEQ frame decoding and an
xLights render adapter with a capability probe. *Still needs a feasibility spike* — the single
biggest technical dependency in the design. The fallback (predict per-frame output from the `.xsq`
plus geometry) is materially weaker, since declared and rendered brightness diverge by 3–4×.

**Measure** — the numeric battery, carried over from the verification tables already in use:

| Metric | Target |
|---|---|
| Correlation: duty-corrected lit-pixel curve vs. smoothed RMS | r ≥ 0.90 |
| Longest stretch with no model changing | ≤ 1 bar |
| Undeclared fully-black frames | 0 |
| Largest single pixel-level plateau, % of runtime | ≤ 20% |
| Mean hue families per frame | ≤ 2.5 |
| Active palette slots per section | within style band |
| Pixel-heavy prop duty cycle | ≥ 65% matrices, ≥ 40% focal |
| Max share of runtime on one repeated composition | ≤ 2% |
| Effect-boundary deviation from nearest beat | ≤ 1 frame |
| Beat-hit rate | within corpus-derived band |
| Pairwise visual similarity between designated peaks | below threshold |
| Per-section energy vs. declared ceiling | never exceeded |

**These are guardrails, not a quality score.** Automatic creativity metrics have near-zero
correlation with human judgment. They reliably detect *breakage*; they do not detect *goodness*.
Treating them as an objective invites the generator to satisfy them while producing something boring
— and deterministic metrics are the easiest thing in the system to game.

**Critique** — two channels, because numbers and eyes fail differently:
- *Numeric* — the battery above. Mechanical, cheap, every iteration.
- *Visual* — extract frames at section boundaries and peak moments and hand them to a vision model
  **with the Show Bible's stated intent for that section**, asking whether it delivered. This catches
  what metrics structurally cannot: the "red snap" that measured 35% *brighter* and rendered
  white/green with a sliver of red, because palette index 0 was a four-colour mix rather than
  red-dominant. No numeric metric would have found it; one human glance did.

Scene-change metrics are structurally blind to isolated small-prop pops against dark frames — in any
dark or sparse section, extract and actually look.

**Judging discipline**, from the evidence:
- **Pairwise, not absolute.** Models compare far more reliably than they score; absolute scores drift
  and cluster. Compare each iteration against the previous and against a retrieved corpus exemplar.
- **Randomize order and run both.** Position bias is worth 5–15 points.
- **Different model family for the critic** than the generator, to blunt self-preference.
- **Do not assume chain-of-thought helps the judge.** On LitBench — creative-writing preference, the
  closest published analog — requiring a rationale *decreased* agreement (73%→72%), opposite to its
  effect on code and math. Test rather than assume.
- **Calibrate expectations.** The best off-the-shelf LLM judge reaches ~73% agreement with human
  preference on creative work, and in one live human study the best judge performed *at chance*. The
  automated layer is a regression detector; only human preference confirms improvement — which is
  what `XlightsAI.Studio.Release`'s existing `BlindTrial` / `AestheticRating` machinery is for.

**Revise.** Critique emits targeted `RevisionDirective`s against specific sections, not a request to
regenerate the song. Only affected SectionScripts re-run; the expensive Show Bible call is usually
untouched. That is what makes iteration affordable.

**Candidate search, bounded.** Best-of-N quality is **unimodal in N** — it rises, then falls, as
reward hacking sets in. Small N (4–8) with a deliberately diverse scorer, never N=64.

**Convergence discipline.** Fixing one thing breaks another: the pass that fixed the black intro and
the dead breakdown simultaneously made 8 of 11 loud sections *dimmer*, because a blunt two-colour
palette constraint traded brightness for cohesion in exactly the sections that most needed punch. So
every iteration re-runs the *full* battery and diffs against the previous iteration, not just against
targets. A metric that regresses is a finding even when it still passes. Keep every iteration's
artifacts and select the best-scoring one rather than assuming the last is best.

---

## 10. Style system

Styles are coherent bundles of palette discipline, effect pool, density curve, prop-role bias and
structural convention — not cosmetic presets. A dubstep track and a lullaby should fail *different*
tests.

**Honest framing:** the vendor market has **no style taxonomy** — vendors categorize by holiday, then
song/artist, then prop requirement (e.g. "EZ Import with Moving Heads", "Mega Tree Singing Face"),
with style expressed only as marketing adjectives. The taxonomy below is therefore *our* synthesis
from the observable axes plus the effect catalogue, and should be presented as such.

| Preset | Palette discipline | Effect vocabulary | Density / blend |
|---|---|---|---|
| **Traditional / Classic** | warm white + red + green, ≤3 active slots | Garlands, Marquee, Single Strand chase, Twinkle, Color Wash | low density, long effects, Normal blend |
| **Elegant / Warm-White** | 2 slots: `#FFFFFF` + `#FFC880` | Color Wash, On with value curves, Fill, slow Bars, Snowflakes | very low density, 1–2 s fades |
| **High-Energy / EDM** | 4–6 saturated slots, high contrast | Shockwave + timing track, Strobe, Morph, Pinwheel speed curves, Fireworks on `FIRETIMINGTRACK`, Single Strand FX | high density, Additive/Max accents, fades <0.2 s |
| **Singing-Face Storytelling** | white face + coloured background | Faces + lyric track, Text `Text_LyricTrack`, Pictures, restrained Color Wash under | face always on, background restrained |
| **Spooky / Halloween** | orange/purple/toxic green | Lightning, Fire, Liquid, Tendrils, Plasma, Shader, upward Meteors | medium, Shadow/Mask blends |
| **Cinematic / Orchestral** | 2–3 slots, deep blues and golds, gradient palettes | Galaxy, Bars with `Bars_Gradient`, Morph, Curtain, Shockwave reserved for stingers | sparse with huge swells; brightness value curves carry everything |
| **Whimsical / Kids** | full rainbow, all 8 slots | Butterfly, Spirograph, Circles (Bubbles), Shape, Marquee | medium-high, Normal |
| *(anti-pattern)* **RGB Chaos** | random per section | everything, changing every 2 bars | maximum — this is what xLights' own "Create Random Effects" produces, and is explicitly what to steer away from |

Style conditions everything downstream: which Looks the Director builds, which exemplars are
retrieved, and what the target bands in the metric battery are.

**The structural arc** the Director works against, as an energy budget — a ceiling per section never
exceeded. This is the practical core of "composed" vs. "loud everywhere". *(Synthesis: no published
framework for light-show arc exists; this is assembled from motion-graphics pedagogy, concert LD
practice, the song-structure literature and the corpus.)*

| Section | Intent | Ceiling |
|---|---|---|
| Intro | Restraint and promise. Establish base colour. Hero prop not at full. | ≤20% |
| Verse 1 | Teach the grammar: which prop is bass, which is melody, what a hit looks like. Independent props, lots of negative space. | 30–40% |
| Pre-chorus | Subdivision acceleration, prop accumulation, then collapse to near-zero in the last beat. | 40→70→~0 |
| Chorus 1 | The reveal: first full canvas, first mirrored symmetry, first hero prop at full. | 85% |
| Verse 2 | Same grammar, exactly one thing different. Clearly quieter than Chorus 1. | 40–50% |
| Chorus 2 | Identical identity to Chorus 1 plus one or two additions. | 90% |
| Bridge | Contrast of *kind*, not degree — change vocabulary; introduce the prop held back all song. Maximum negative space. | 15–30% |
| Final chorus | Everything from Chorus 2 plus the withheld ace. | 100% |
| Outro | Decay, don't cut. Return to the intro's palette to close the loop. | →0 |

**Show length** is a real design parameter: community guidance puts an optimal show at 10–15 minutes,
with most visitors watching 5–20, and xLights preview/memory degrading around 14–15 minutes.

**Vocal awareness** is a capability gap with disproportionate payoff. Stems are already separated,
yet the analysis has zero vocal/instrument discrimination, so vocal and instrumental passages are
sequenced identically. A `vocalActive` signal gating hold-vs-chase (steady single-colour full-house
hold under vocals, frequency-reactive movement under instrumental breaks) matches both the reference
show's behaviour and the craft rules already written down.

**Singing faces** deserve first-class support because they are a major category in the market and a
large part of what reads as "professional." The chain is: a lyric timing track with three
`EffectLayer` levels (phrases → words → phonemes, breakdown-able natively by xLights or importable
from Papagayo `.PGO`), a face definition on the model (single-node, node-ranges, or matrix images),
and the `Faces` effect binding the two via `Faces_MouthMovements` + `Faces_FaceDefinition`, with
`Faces_SuppressWhenNotSinging` and auto-blink. The same lyric track feeds `Text_LyricTrack` for the
karaoke layer. The corpus's `Girl Tree` / `Boy Tree` timing tracks are exactly this, used for
call-and-response. Our pipeline already separates a vocals stem — phoneme alignment is the missing
link, and it is a well-bounded problem.

---

## 11. Prior art — and why this is still worth building

Three open-source projects already turn audio into `.xsq`, and the research found a clear community
verdict on all of them. Engaging with this honestly matters more than ignoring it.

- **`derwin12/xlights-autosequencer`** is the most advanced and the closest to this product: 17
  algorithms across a 7-level hierarchy, librosa/madmom/Demucs analysis, auto-built 8-tier "power
  groups" from `xlights_rgbeffects.xml`, **21 composite themes** with 5-colour palettes and mood
  tags, a 40-effect catalogue with **212 pre-tuned variants** tagged by energy/section-role/genre,
  value-curve generation, and lyric-keyword-triggered Pictures/Text. 60–90 s per song. **It is
  already cited in this repo's own `XLIGHTS-SEQUENCE-ENGINEERING-GUIDE.md` §13 as the pinned
  upstream reference.** Read it before designing the Look library — the 212 tuned variants are an
  enormous head start on the effect-parameter problem, and its weakness (exports only against one
  committed layout) is something this app's layout importer already solves.
- **`JohnBreault/xlights-mcp-server`** publishes explicit heuristic tables that are directly reusable
  as prompt scaffolding: **by model type** (Arches → SingleStrand/Chase/ColorWash/Morph; Trees →
  Spirals/Pinwheel/Meteors/Circles; Single lines → Chase/Morph/SingleStrand/Shimmer) and **by musical
  feature** (strong beats → Shockwave/Morph/Strobe; high-energy chorus → Chase/Meteors/SingleStrand;
  low-energy verse → Twinkle/Shimmer/ColorWash/Snowflakes; sustained notes →
  Plasma/Pinwheel/Spirals/Galaxy). This is very likely the MCP server referenced in the `C:\xLights`
  working notes, whose `create_sequence` was found to be a whole-song black box that lights every
  model in every section and ignores detailed direction — precisely the limitation this design
  exists to fix.
- **xLights itself ships a structure-aware auto-generator**: `MovingHeadSongGenerator.cpp` takes bar
  boundaries plus sections flagged `chorus` and `peak`, classifies every bar as
  `Pool | Chorus | Sweep | Outro`, and emits per-head effects with a spread function. It is the
  closest thing to an official reference implementation of music-structure → choreography and is
  worth reading in full as a design template.

**The community verdict is specific and should set expectations.** On the main xLights forum thread
on AI sequencing: a user converted an `.xsq` to text and had an LLM critique it — the model
*successfully identified real defects* (it flagged "excessive colour palettes" as the top problem)
but *could not apply the fixes across all effects*. Members were "somewhat unimpressed" with AI
generating complete sequences, and the consensus is that AI is useful for **analysis and
recommendation**, not autonomous creation.

That is not a reason to stop; it is a precise statement of where the value is and what the
architecture must supply. The failure mode described — good critique, no reliable application — is
exactly the gap §2's split addresses: let the model judge and direct, let deterministic code apply.
The reason the existing projects plateau is not XML emission, which three of them already do. It is
that (a) raw beat grids produce the RGB-chaos anti-pattern, (b) effect choice is strongly conditioned
on prop geometry the model cannot see, and (c) the community's own quality bar is phrase-level
structure and restraint, which naive beat-locking actively violates. This design attacks all three
directly: the Look library and energy budget for (a), real pixel geometry and resolution-aware
legality for (b), and the Show Bible plus the render-critique loop for (c).

---

## 12. What has to change in the existing code

The analysis stack is the asset. The creative layer and the exporter are what get replaced. The first
four are hard blockers.

**Blockers**

1. **The layout model captures no pixel geometry.** `DisplayModel` is
   `(ModelId, XlightsName, Kind, GroupNames, PreviewPoints)` — `parm1/parm2/parm3`, `StringType`,
   `WindowRows`, `WindowColumns` are present in real `xlights_rgbeffects.xml` files and deliberately
   ignored, and `PreviewPoints` holds at most one point. **Pixel-weighted coverage — the single most
   important correctness mechanism in this design — is impossible until the importer reads node
   counts and real geometry.** `Kind` is also the raw `DisplayAs` string with no normalization, so
   there is nothing to drive resolution-aware or geometry-aware effect legality. Submodels are
   recognized only as a reference shape inside a group member list, never enumerated — yet submodels
   (megatree slices, matrix regions, face parts) are how one prop becomes an eight-element instrument.
2. **`EffectRecipeRegistry` is not an extension point.** There is no `IEffectRecipe` type at all —
   one sealed class of hardcoded `if`s behind a two-method interface, and a recipe receives only
   `(recipeId, ModelId, startTicks, endTicks)`: no palette, no intensity, no musical context, no
   geometry. Adding a recipe today means edits in three coupled places. It needs a real abstraction
   carrying parameters, palette reference, layer, blend mode, transitions and value curves —
   generated from and validated against the xLights effect metadata (§8a).
3. **`SequenceDocument` is too thin to be the IR.** `(Guid Id, long Revision,
   IReadOnlyList<EffectInstance> Effects)` — no palettes, no timing tracks, no metadata, no element
   model; layering is just an `int`. It needs a palette table, **timing tracks as first-class
   content** (§6), per-effect settings, blend modes, transitions, and sequence metadata.
4. **The exporter has several concrete, verified defects** beyond being limited to one effect:
   - `FixedPointTiming="25"` is **wrong** — it is a boolean-ish flag and real xLights writes
     `FixedPointTiming="1"`. The frame interval lives *only* in `head/sequenceTiming`.
   - `type="modelGroup"` on `DisplayElements` is **not what xLights emits** — groups and submodels
     both use `type="model"`. This alone may break import of generated files.
   - Missing root attributes `ChanCtrlBasic="0" ChanCtrlColor="0"`; missing head fields (`author`,
     `song`, `artist`, `album`, `mediaFile`, `comment`, self-closed `<imageDir/>`); missing
     `<nextid>`, `<Jukebox/>`, `<DataLayers>` and `<TimingTags>` (ten `<Tag number="N" position="-1"/>`
     entries).
   - Emits `selected="0"` on every effect; real xLights emits `selected` only when `1`, and emits
     `id="N"` on all but the first effect of a layer.
   - Frame interval hardcoded to 25 ms; the corpus and community default is **50 ms**, which halves
     file and FSEQ size.
   - No `&comma;` escaping in setting values.
   - Conversely, the empty `<Effect></Effect>` EffectDB placeholder it emits is **correct and
     idiomatic** — the standing "unverified placeholder" warning can be retired, which also closes
     one of the project's disclosed limitations.

**Analysis extensions (L0)**

5. **Per-band energy.** Only `EnergyFrame(Rms, NormalizedEnergy)` is exposed; spectral flux lives
   inside `OnsetDetector` and never escapes. The frequency-split prop routing that *sells* dubstep,
   the band×intensity event classification the manual work relied on, and **labelled timing tracks**
   (kick/snare/hat) all need bass/mid/high energy as first-class output.
6. **Vocal activity**, and beyond it phoneme alignment for singing faces. Stems exist; discrimination
   doesn't. In practice the separator yields two stems (vocals + accompaniment) despite a six-member
   `StemKind` enum — enough for a `vocalActive` envelope.
7. **Measured phrase period** instead of the hardcoded 8-bar hypothesis.
8. **Cadence-aware phrasing is disabled by a hash mismatch** — `StructureAnalysisRunner` passes
   `MelodyTrack: null` into `PhraseSegmenter` because the melody's `SourceAssetHash` is the lead
   *stem's* hash rather than the song's, so phrasing is pure bar hypothesis. A plumbing fix with real
   musical payoff.

**Reuse — more exists than expected**

- **`XlightsAI.Studio.Refinement` is already the scoring harness skeleton**: `MetricValue(Name, Value,
  Weight, HardGate, Explanation)` with hard gates constrained to 0.0/1.0, `QualityReport` with
  `Validate()` enforcing that a failed gate appears in violations and `AreComparable()` gating on
  evaluator version, `RefinementBudget(MaxElapsed, MaxCompute, QualityTarget, NoImprovementPatience)`,
  and `CandidateAttempt` with seeds and snapshot hashes. The §9 battery maps onto this almost directly.
- **`XlightsAI.Studio.Rendering`** has FSEQ frame decode, a local gesture preview renderer, and an
  xLights render adapter with a capability probe — the starting point for L5.
- **`XlightsAI.Studio.Release`** has `AestheticRating`, `BlindTrial`, `BlindTrialOrderingService` and
  a trial summary reporter — randomized blind A/B, exactly the human-preference layer §9 says is the
  only real confirmation of improvement.
- **`XlightsAI.Studio.AiEdits`** is a substantial head start built for a different purpose: a provider
  abstraction (`AiProviderKind { OllamaLocal, CloudByok }`), BYOK credentials in a DPAPI-backed
  `ICredentialStore`, `HttpStructuredOutputAiPlanner` with retry/backoff and response size caps, a
  least-privilege request payload, strict schema deserialization
  (`JsonUnmappedMemberHandling.Disallow`), a typed rejection taxonomy, and a full proposal → validate
  → dry-run → approve → audit pipeline. **The safety rails and BYOK plumbing are the reusable part**;
  its closed operation vocabulary (`SetBrightness, SetDirection, SetSpeed, SnapToGrid, ReplaceRole`)
  maps to nothing the current recipe can express and will be superseded.
- **The golden-file test pattern** (`tests/.../Fixtures/golden/golden-three-effect-sequence.xsq`) plus
  `XsqStructuralReader` round-trip assertions give a ready-made shape for evaluating generated
  sequences — and the golden fixture should now be regenerated against real xLights output given the
  defects above. `fixtures/FIXTURE-CORPUS-MANIFEST.json` with per-fixture `expectedOutputs` is a
  known-answer harness already in place.
- **The uniform JSON store pattern** (temp-file + atomic `File.Move`, typed corrupt exceptions,
  `*Catalog` read-modify-write with monotonic versions and append-only history) should be followed for
  every new artifact store.

**Stranded creative layer.** `ISectionVisualStoryPlanner`, `ITransitionRecipePlanner`,
`IVisualBudgetResolver`, `IMotifVariationScheduler`, `IPhraseSeamAssembler`, `ISongContinuityEvaluator`,
`IWholeSongExportAuditor` and the entire `Refinement` loop have implementations, tests, JSON catalogs
and in some cases UI editors — and none are in `WholeSongGenerator`'s path. `SongDirectionPlanner.Plan()`
is even *called* and its result discarded. Some is directly reusable (`VisualBudget`'s
concurrency/brightness/density caps are a coverage engine in miniature); some will be superseded.
Audit each against the final IR rather than assuming either.

**Known defects to fix rather than build on:** `DeterministicPropRoleAssigner` assigns the focal prop
by `phraseIndex % modelCount` — pure round-robin with no regard to pixel mass, geometry or musical
role — and stuffs a model name into `MusicalRoleAssignment.SourceTrackId`, whose documented contract
says it is never a `ModelId`; `ChoreographyCandidateBuilder` then reconstructs a `ModelId` from it.
Also, generated artifacts live in `%AppData%` while the exported `.xsq` lands in the project
directory, which needs resolving once iterations, critiques and corpora are project-scoped.

---

## 13. Storage

Everything the loop produces is worth keeping; iterations must be comparable and revertible.
Project-scoped, following the existing atomic-write store pattern:

```
{project}/
  analysis/        MusicModel, beat grid, band energy, events, stems, vocal activity, lyrics
  knowledge/       EffectVocabulary (generated), PatternLibrary, ExemplarIndex, CraftRules
  bible/           ShowBible v1, v2, … (diffable)
  scripts/         SectionScript per section per iteration
  ir/              compiled ChoreographyIR per iteration
  renders/         FSEQ / frames / metric reports per iteration
  critique/        numeric + visual critique, RevisionDirectives
  exports/         sequence.xsq
  llm/             full call log with costs
```

The existing JSON override stores (melody corrections, layout corrections, motif link overrides) sit
alongside as **human override layers the compiler honours** — the right relationship. A user
correcting a note or re-linking a phrase must survive regeneration.

---

## 14. Provider abstraction

Cost lands on the customer's chosen model, so provider-agnostic BYOK is a product requirement.
Three roles, independently configurable:

| Role | Needs | Shape |
|---|---|---|
| **Director** (L2) | Long context, strong reasoning, taste | Frontier model, 1 call/song |
| **Choreographer** (L3) | Reliable structured output, cheap, parallel | Mid-tier, ~30 calls/song, batchable |
| **Critic** (L6) | Vision + judgment, different family from Director | Frontier multimodal, few calls/iteration |

Design notes that materially affect cost and quality:

- **This pipeline is offline** — it writes a file, it doesn't answer a user in real time. So the
  **Batch API applies**: ~50% off input and output, and it *stacks with prompt caching*. For the ~30
  per-section L3 calls this is close to free money.
- **Prompt caching is the dominant cost lever** when many calls share a large stable prefix (effect
  vocabulary, prop inventory, craft rules, exemplars, Show Bible). Cache reads run at ~0.1× base
  input. It is **prefix-match**: any byte change anywhere invalidates everything after it. Put stable
  content first, volatile per-section content after the last breakpoint, and verify with
  `cache_read_input_tokens` — if that's zero across repeated calls, something (a timestamp, unsorted
  JSON, a varying tool list) is silently invalidating.
- Enforce output shape with the provider's native structured-output mechanism, but **keep schemas
  flat and shallow** given the reasoning tax and depth-related accuracy decay. Where a decision is
  genuinely creative, let the model reason in prose and serialize in a second, cheaper constrained
  step rather than forcing taste through a deep schema.
- **Measure before assuming a multi-model cascade is cheaper.** At least one 2026 study finds
  single-model configurations more cost-efficient than planner/executor splits with weaker models,
  and caches are model-scoped — a cascade forfeits cache reuse across its models. Try
  one-model-at-lower-effort first.
- Expose **tiers** — draft (single pass, no render loop), standard (one critique iteration), showcase
  (bounded candidate search + multiple iterations) — so the cost/quality dial belongs to the customer.
- Log every call's prompt, response, model and cost into the project. When a show comes out wrong you
  need to see what the Director actually said.

---

## 15. Principal risks

1. **Headless render automation may not be viable.** The whole L5/L6 loop depends on it. Spike
   `xlDo.exe` and the existing render adapter early. The fallback lowers the quality ceiling
   materially, because declared and rendered brightness diverge by 3–4×.
2. **Generated files may not import cleanly.** The `type="modelGroup"` and `FixedPointTiming="25"`
   defects suggest the current exporter has never been validated against a real xLights import. Close
   this first — everything downstream is worthless if xLights rejects the file.
3. **The critic may be as biased as the generator.** Mitigate with the numeric battery as an
   independent non-LLM channel, a different model family for the critic, pairwise comparison with
   randomized order, and the knowledge that the realistic agreement ceiling is ~73%.
4. **Metric gaming.** The battery is the easiest thing in the system to satisfy hollowly, and
   best-of-N makes it worse past small N. Keep N small, keep metrics as gates rather than objectives,
   keep a human preference check in the loop.
5. **Iteration may not converge.** Already observed. Score every iteration fully, keep all artifacts,
   select the best rather than the last.
6. **Corpus licensing and audio pairing.** Paid vendor sequences cannot be used as training material
   at all; shared sequences never bundle audio and carry absolute `mediaFile` paths. Audit which of
   the 31 are clean *and* have usable paired audio before relying on beat-aligned mining — without
   audio you get vocabulary statistics but not timing priors, which are the valuable part.
7. **The physical layout can cap quality independent of sequencing.** 92.5% of that preview canvas is
   permanently black because models cluster upper-left and the preview is never fit to frame. No
   sequencing change fixes it, and any render-derived brightness metric is distorted by it. The system
   should **detect and report** this rather than silently optimizing against a broken yardstick.
8. **Cost blowup under iteration.** Bound iterations explicitly, keep tiers honest, show a cost
   estimate before a showcase run.
9. **Craft knowledge is thinner than it looks.** There is no published theory of pixel-light
   choreography; several rules stated confidently above — don't hit every beat, mirroring doctrine,
   palette-count limits, reserve the big moment — are *our axioms*, not sourced community canon. The
   CraftRules verification loop (§8b) matters partly because of this: measure them against the corpus
   rather than trusting them.

---

## 16. Verification

How we will know it worked, in increasing order of strength:

1. **Import** — the generated `.xsq` opens in a real xLights install without error, with a golden
   fixture captured from real xLights output rather than hand-constructed.
2. **Structural** — effect count, layer count, palette count, timing-track count and effect-type
   diversity land in the corpus's range rather than 100× below it.
3. **Constraint** — the full metric battery passes against the Show Bible's declared targets, and
   every emitted setting validates against the generated effect-metadata schema.
4. **Regression** — a fixed set of songs × layouts × styles regenerated on every change, metrics
   tracked over time, so a quality regression surfaces in CI rather than in December.
5. **Comparative** — blind pairwise ranking of generated against corpus sequences on the same song and
   layout, using the existing `BlindTrial` machinery. "Indistinguishable from vendor" is a claim only
   human preference can support.
6. **Real** — rendered in xLights against the actual house and watched. Nothing above substitutes,
   and per the project's own changelog it has never yet been done.
