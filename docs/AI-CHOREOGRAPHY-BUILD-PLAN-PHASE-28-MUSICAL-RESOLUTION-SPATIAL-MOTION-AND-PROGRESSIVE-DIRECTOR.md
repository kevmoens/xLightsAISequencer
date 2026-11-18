# Phase 28 — Musical resolution, spatial motion, multi-layer canvas, user-settable budgets, and a progressive (tool-calling) Director

> **Status: PLAN ONLY. Nothing in this document has been implemented.**
> This file is self-contained: every finding below carries the real measurement that produced it and
> the command that reproduces it, every proposed change names the real file and symbol it touches, and
> every number proposed as a replacement for a hardcoded one states where it comes from.

---

## 0. Scope in one paragraph

A fresh end-to-end generation of *Carol of the Bells* (`C:\xLights\CarolOfTheBells\exports\sequence.xsq`,
generated 2026-11-15) was measured against the hand-made reference for the same song
(`C:\xLights\09. Carol Of The Bells (Instrumental).xsq`), and against two further hand-made shows the
user identified as carrying the specific techniques we are missing
(`C:\xLights\All I Want For Christmas Is You shortened.xsq`,
`C:\xLights\Chuck Berry - Run Rudolph  Run.xsq`). The generated show is not under-detected — the
analysis stage already produces 1,691 Percussion marks and 471 raw onsets for this song. It is
**under-expressed**: a half/third-time beat grid, a fixed 4/4 meter assumption, a hard 2.6 effect-starts/second
compile-time throttle, a one-effect-per-grid-mark emitter that cannot produce a sub-grid "hit", five
total distinct effect-parameter strings for 1,089 effects, no `BufferStyle`/`BufferTransform` emission
at all, no vertical travel axis, and no mechanism that keeps sibling matrixes in sync. This phase
fixes those, exposes every remaining magic number as either a derived value or a user-visible
generation setting, and replaces the Director's single monolithic prompt with a progressive,
tool-calling loop.

---

## 1. Evidence

All measurements below were taken with a throwaway XSQ reader over the four real files. Repro script
shape (any equivalent XML walk works — `ElementEffects/Element/EffectLayer/Effect`, plus `EffectDB`
and `ColorPalettes`):

```python
import xml.etree.ElementTree as ET, collections
r = ET.parse(path).getroot()
db   = [e.text or '' for e in r.find('EffectDB')]
pals = [e.text or '' for e in r.find('ColorPalettes')]
for el in r.findall('ElementEffects/Element'):          # name, type=model|timing
    for li, layer in enumerate(el.findall('EffectLayer')):
        for eff in layer.findall('Effect'):             # name,startTime,endTime,ref,palette
            ...
```

### 1.1 Head-to-head: *Carol of the Bells*, human vs. app

| Measure | Human reference | App output (2026-11-15) |
|---|---|---|
| Total model effects | 458 | 1,089 |
| Models carrying ≥1 effect | 10 of 23 | 28 of 28 |
| **Distinct effect-settings strings (`EffectDB`)** | **71** | **5** (one of which is literally `None`) |
| Distinct color palettes | 68 | 15 |
| `Everything` (whole-house group) | **107 effects on 3 layers** | **never targeted** |
| `Tree` | 76 effects on 3 layers | **3 effects on 3 layers** |
| `Matrix` / `Matrix-2` / `Matrix3` | `Tree` and `Matrix` byte-identical layer structure | 65 / 6 / 24 effects — three different treatments |
| Top effect by count | `On` ×164 (35.8%) | `Twinkle` ×202 (18.6%) |
| `On` usage | **164 (35.8%)** | **2 (0.2%)** |
| Duration median / p10 | 450 ms / 150 ms | 925 ms / 350 ms |
| Effects ≤150 ms ("hits") | 80 (17.5%) | 47 (4.3%) |
| **Effects at exactly 925 or 950 ms** | 34 (7.4%) | **532 (48.9%)** |

The app's entire `EffectDB` is reproduced here in full — this is every distinct parameter
configuration in a 154-second, 1,089-effect show:

```
0 : E_CHOICE_Ripple_TimingTrack=Bars
1 : None
2 : E_CHOICE_SingleStrand_TimingTrack=Beats
3 : E_CHOICE_Shockwave_TimingTrack=Beats
4 : E_CHOICE_Ripple_TimingTrack=Beats
```

Every `Twinkle`, every `Strobe`, every `Bars`, every `Color Wash`, every `Morph`, every `Duplicate`,
every `Pinwheel` and every `Candle` in the show (a combined 686 effects) carries `ref` → entry 1,
`None`: pure xLights defaults. The human reference carries 71 distinct configurations; *Run Rudolph
Run* carries 212.

### 1.2 The timing grid is wrong by a factor of three, then again by four

The app's own emitted `Beats` timing track places marks at 525, 1475, 2450, 3400, 4325, 5275 ms —
a ~950 ms period, i.e. **63.2 BPM**. Its `Bars` track groups four of those into 3,800 ms spans.

The human reference's hand-placed `Carols Timing` track (367 marks across 148.8 s) has this
inter-mark gap histogram:

```
100ms×13   150ms×91   200ms×33   250ms×12   300ms×88   350ms×34
450ms×8    600ms×9    650ms×10   900ms×9    950ms×33   ...
```

Three clusters, and they are unambiguous:

- **~316 ms** (the 300/350 pair, 122 marks) — the real **beat**. 60000/316 = **189.9 BPM**.
- **~150 ms** (91 marks) — the **eighth note**, the bell ostinato itself.
- **~950 ms** (33 marks) — the real **bar**. *Carol of the Bells is in 3/4*: 3 × 316 = 948 ms.

So: the app's "beat" is actually one **bar**; 63.2 × 3 = 189.6 ≈ 189.9. `TempoDetector`'s
autocorrelation locked onto the bar period rather than the beat period, and then
`BeatGridSettings(BeatsPerBar: 4, ...)` — hardcoded at both call sites — grouped four real *bars* into
one reported "bar" of 3.8 s (12 real beats).

Every downstream consumer inherits this. `Phrases` and `Sections` are emitted off the same bar grid
(15 and 13 marks respectively, identical start times to `Bars`), so phrase and section boundaries are
quantized to 3.8 s. The Director is told `Tempo: 63.2 BPM (4/4)` in its volatile suffix
(`DirectorPromptBuilder.BuildVolatileSuffix`), which is wrong on both counts.

### 1.3 The signal is present; the compiler throws it away

The app's exported timing tracks for this song:

| Track | Marks |
|---|---|
| `Percussion` | **1,691** |
| `VocalNotes` | 227 |
| `Beats` | 158 |
| `Lyrics_Phonemes` | 104 |
| `Bars` | 40 |
| **`Transients`** | **43** |
| `Phrases` / `Lyrics_Phrases` | 15 |
| `Sections` | 13 |

1,691 Percussion marks is a genuinely dense sub-beat grid — roughly 11/second, more than enough to
carry the eighth-note ostinato. 75% of the app's placed effects *do* start within 30 ms of a
Percussion mark. The material is there.

What discards it is `LookCompiler.ThinToTargetChangeRate` (`LookCompiler.cs:356`), fed by
`SectionDensityMapper.TargetChangeRatePerSecond` (`SectionDensityMapper.cs:31`):

```csharp
private const double FloorChangeRatePerSecond = 0.2;
private const double CeilingToRateSlope       = 2.4;
// => 0.2 + (ceiling ∈ [0,1]) * 2.4   →   absolute maximum 2.6 starts/second
```

and the method's own documented discipline: *"Only ever throttles (merges consecutive marks together),
never speeds up."* A 1,691-mark track feeding a layer whose section ceiling is 0.85 is merged down to
~2.6 starts/second — `keepEvery = ceil(11 / 2.6) = 5`, i.e. five Percussion marks collapse into one
window. That is exactly how 48.9% of the show arrives at a 925–950 ms duration.

**This contradicts the style preset the run actually used.** The effect vocabulary in the output
(`Shockwave`/`Strobe`/`Ripple`/`Bars`/`Twinkle`/`Color Wash`) is `StylePresetCatalog.HighEnergyEdm`,
whose declared band is `new StyleDensity(2.5, 6.0, ...)` — "2.5–6.0 changes/second". The Director is
told 6.0/s is in range; the compiler cannot exceed 2.6/s under any circumstances.

For reference, peak measured starts-per-second in any 1-second window on any single layer:

| Show | Peak | Minimum inter-start gap |
|---|---|---|
| *All I Want For Christmas* (human) | **8/s** | 100 ms (sustained — p10 gap is also 100 ms) |
| *Carol of the Bells* (human) | 5/s | 150 ms |
| *Carol of the Bells* (app) | 5/s | 100 ms, but p10 gap 200–600 ms |
| *Run Rudolph Run* (human) | 3/s | 350 ms |

### 1.4 The missing horns

The user's report — "we are missing sounds from horns" — reduces to three measurable causes:

1. **Only 43 transients survive detection** out of 471 raw onsets.
   `TransientHitDetector.Detect` requires `onset.Strength > localMean + 1.5 × localStdDev` over a
   ±15-second window (`TransientHitSettings.LocalWindowTicks = 150_000_000L`,
   `LocalSensitivityMultiplier = 1.5`). A horn section playing a *sustained, repeated figure* is by
   construction not locally anomalous — every onset in the figure raises the local mean that each of
   them is then measured against. The gate is built to find isolated bangs and is structurally blind
   to a repeating riff.
2. **Stem separation is 2-stem only.** `SherpaOnnxSpleeterModelAsset` provides `vocals` and
   `accompaniment` (`SpectrogramModelBundle`: *"two stems are 'vocals' and 'accompaniment'"*). Horns,
   bells, strings, guitar and keys all share one undifferentiated stem. There is no per-instrument
   onset track to bind a Look to. §3.6a sketches the 4-stem follow-on this implies; it is deliberately
   out of this phase's own step sequence (§6) — see that section for why and for what ships here
   instead.
3. **`LeadInstrumentWindowDetector` finds the horn phrase but nothing acts on it with the right
   shape.** It measures Mid+High prominence over a trailing 8 s baseline
   (`LeadInstrumentWindowSettings`), and Phase 27 already wired its output into the Director prompt as
   prose ("Favor the rhythmic-stab-pattern technique"). But a "stab" is a 100–200 ms hit, and §1.3
   shows the compiler cannot emit one at a repeating cadence. The instruction is given and then
   mechanically prevented from being followed.

### 1.5 The spatial-motion idiom we do not have

**First 9 seconds of *All I Want For Christmas Is You* (human)** — this is the pattern the user
pointed at, and it is three distinct gestures back to back:

```
  200- 5200  Arches-1 Outside     ┐
  700- 5200  MiniTree3            │  GESTURE A: "accumulate"
 1100- 5200  MiniTree2            │  12 props, onsets 400ms apart,
 1500- 5200  MiniStar4            │  ALL ending on the same instant (5200).
 1900- 5200  MiniTree1            │  Staggered START, shared END.
 2300- 5200  MiniStar2            │
 2700- 5200  Arches-4             │
 3100- 5200  MiniStar1            │
 3500- 5200  Arches-3             │
 3800- 5200  MiniTree4            │
 4200- 5200  Arches-2             │
 4800- 5200  MiniStar3            ┘

 5400- 6200  (all 12 props, identical)   GESTURE B: unison hit
 6200- 8000  (all 12 props, identical)   GESTURE B: unison hold

 6500- 7700  Peak Right, end of house  ┐
 6700- 7700  Peak Left                 │  GESTURE C: outward sweep
 6900- 7700  Single Line-2             │  200ms stagger, shared END (7700)
 7100- 7700  Single Line               │
 7300- 7700  Front Door, gutter 1      ┘

 7700- 9000  Front Door, gutter 1      ┐
 7900- 9000  Single Line               │  GESTURE C′: the SAME sweep,
 8100- 9000  Single Line-2             │  reversed, immediately after.
 8300- 9000  Peak Left                 │
 8500- 9000  Peak Right, end of house  ┘
```

Three mechanics we cannot currently express:

- **Staggered start with a shared end.** Our `PropDelayResolver.ResolveOffsets` staggers the start
  and leaves every member running to `section.Window.EndTicks`
  (`LookCompiler.cs:164-168`) — which coincidentally produces a shared end, but only ever at a
  *section* boundary, never at an arbitrary musical landing point like 5200 ms. Our
  `PropDelayResolver.ResolveTravelWindows` (the `Look.Travel` path) gives each member its own
  *separate* window instead, so it produces a chase, never an accumulation.
- **Immediate reversal.** A sweep and its mirror back-to-back is a single authored gesture here.
  `LookTravel` has exactly two orders (`OrderLeftToRight`, `OrderRightToLeft`) and one Look declares
  one of them; there is no "then reverse" and no way to express the pair as one unit.
- **Vertical axis.** `PropGroupResolver.OrderByPhysicalPosition` orders only on
  `DisplayModel.Bounds.X` (`PropGroupResolver.cs:95-105`). `DisplayBounds` carries `X, Y, Z` plus
  `EndX, EndY, EndZ` — `Y` is read at import and never used for ordering. Top-to-bottom and
  bottom-to-top, which the user explicitly asked for, are not expressible.

### 1.6 The multi-layer canvas idiom we do not have

**`Tree` after 50 s in *All I Want For Christmas Is You*** — four layers, same effect, same exact
time span, differing only in buffer transform, rotation direction and palette:

```
L0  57500-92500  Pinwheel  BufferStyle=Per Preview                            Rotation=1  palette 34 (red)
L1  57500-92500  Pinwheel  BufferStyle=Per Preview  BufferTransform=Flip Horizontal  Rotation=1  palette 34 (red)
L2  57500-92500  Pinwheel  BufferStyle=Per Preview                            Rotation=0  palette 35 (white)
L3  57500-92500  Pinwheel  BufferStyle=Per Preview  BufferTransform=Flip Horizontal  Rotation=0  palette 35 (white)
```

That is a deliberate 2×2 kaleidoscope: {clockwise, counter-clockwise} × {normal, mirrored}, in two
colors, all mapped across the **whole preview canvas** rather than per model. Shared settings on all
four: `E_SLIDER_Pinwheel_Arms=1`, `_ArmSize=100`, `_Thickness=10`, `_Twist=360`, `_Speed=3`,
`E_CHOICE_Pinwheel_3D=3D Inverted`, `E_CHOICE_Pinwheel_Style=Old Render Method`.

***Run Rudolph Run*** goes much deeper. Per-model layer counts:

```
Tree       14 layers   [17, 41, 35, 36, 29, 9, 5, 6, 5, 5, 2, 2, 2, 0]
Matrix     12 layers   [23, 18, 30, 35, 35, 33, 9, 9, 5, 5, 2, 2]
Matrix-2   12 layers   [ 7, 18, 30, 35, 35, 33, 9, 9, 5, 5, 2, 2]   ← identical to Matrix from L1 down
Matrix3    12 layers   [ 7, 18, 30, 35, 35, 33, 9, 9, 5, 5, 2, 2]   ← identical to Matrix-2
```

`Matrix-2` and `Matrix3` are **byte-identical in structure** to each other and identical to `Matrix`
from layer 1 downward (`Matrix` layer 0 carries 23 vs 7 — the extra content is lyric/face material
that belongs only on the primary matrix). This is the user's rule, measured: *show something on one
matrix, show it on all of them.*

A single instant in that `Tree` stack (58–62 s) shows how the layers combine:

```
L0  Pictures      BufferTransform=Flip Horizontal
L3  Pictures
L4  Twinkle
L5  Color Wash
L6  Off                                              ← a deliberate 400ms blackout layer
L7  Spirals       Flip Horizontal   LayerMethod=1 is True Unmask
L8  Twinkle       Flip Horizontal   LayerMethod=1 is True Unmask
L9  SingleStrand  Flip Horizontal
```

Corpus-wide in that one file:

| Setting | Values observed |
|---|---|
| `B_CHOICE_BufferStyle` | `Per Preview` ×512, `Per Model Per Preview` ×202, `Default` ×129, `Single Line` ×124, `Per Model Default` ×109, `Overlay - Centered` ×108, `Per Model Single Line` ×60 |
| `B_CHOICE_BufferTransform` | `Rotate CC 90` ×676, `Flip Horizontal` ×30, `Rotate CW 90` ×9, `Rotate 180` ×2 |
| `T_CHOICE_LayerMethod` | `2 is Mask` ×32, `Normal` ×24, `1 is True Unmask` ×24 (rest default) |

**None of these three settings are emitted by this codebase at any point.** `grep -rn
"BufferStyle\|BufferTransform" src/` returns exactly two hits, both in a code comment in
`EffectFixturePlanner.cs:235-236` describing a failed experiment. `LookLayer.Blend` exists and is
documented as *"carried as IR only; see `XsqExporter`'s own disclosed gap (no verified `LayerMethod`
choice list exists yet)"*. `EffectVocabularyBuilder` does build a `LayerMethod` property row
(`BuildLayerMethodOnBuffer`, line 316) but nothing writes a value to it.

`Rotate CC 90` ×676 is particularly load-bearing: it is how a horizontal `SingleStrand` chase becomes
a *vertical* chase up a mega tree or matrix. Without buffer transforms, every spatial effect we emit
runs in the model's native orientation only.

### 1.7 Why the Tree is empty

`Tree` received **3 effects** in the generated show. The chain:

- The Director authors the whole Look library in **one** call. `AiChoreographyWholeSongGenerator`
  defaults `maxOutputTokens = 16000` for the entire Show Bible — every Look, every layer, every
  palette, every peak moment.
- The Choreographer, per section, can only **select** a Look id and optionally rebind a layer's
  target prop (`SectionScriptPromptBuilder.BuildStablePrefix`: *"You never invent a new Look, a new
  effect, or a new parameter — only select from the Look library below and rebind layers to real
  props"*).
- `LookCompiler` then emits each layer's effect with `recipe.CreateDefaultParameters()` plus whatever
  `layer.Params` the Director wrote. The 5-entry `EffectDB` in §1.1 proves the Director wrote
  essentially none.

So the *only* opportunity in the entire system to author a rich, deeply-layered Tree treatment is a
few hundred tokens inside one 16k-token JSON blob that must simultaneously cover 28 props and 13
sections. `ChoreographyLegalityValidator` already has rules that *detect* the symptom after the fact
(`RuleHeroLookNeverUsed`, `RuleModelOnlyGroupCovered`, `RuleGroupNeverTargetedDirectly`,
`RuleConcurrentFocalEffectCap`) — all added in Phases 22–27 — but detection cannot manufacture the
budget the author never had.

---

## 2. Findings, consolidated

| # | Finding | Root cause | Kind |
|---|---|---|---|
| F1 | Beat grid is at ⅓ the true tempo | `TempoDetector` autocorrelation picks the bar period; no beat/bar disambiguation | Analysis |
| F2 | Meter assumed 4/4 for a 3/4 song | `BeatGridSettings(BeatsPerBar: 4)` hardcoded at 2 call sites; `TempoDetector.DefaultMeterGuess = "4/4"` | Hardcoded |
| F3 | No sub-beat grid reaches the compiler | `ThinToTargetChangeRate` only merges, never subdivides | Self-imposed limit |
| F4 | Hard 2.6 starts/s ceiling, contradicting the style's own 6.0/s band | `SectionDensityMapper` `0.2 + 2.4·ceiling` | Hardcoded |
| F5 | 49% of effects are exactly one grid-mark long | `CompileDiscreteLayer` sets duration = `duty × inter-mark span`; no absolute "hit" shape | Design |
| F6 | 5 distinct parameter strings for 1,089 effects | Director authors `Params` rarely; nothing varies them per instance | Design |
| F7 | `On` used 2× vs. the reference's 164× | `On` absent from the `HighEnergyEdm` effect pool | Hardcoded |
| F8 | Horn/riff figures produce no hits | `TransientHitDetector`'s local-anomaly gate is blind to repeating figures | Analysis |
| F9 | No per-instrument onset tracks | 2-stem separation only | Analysis |
| F10 | No staggered-start/shared-end gesture | `PropDelays` ends at section bound; `Travel` gives disjoint windows | Design |
| F11 | No vertical travel axis | `OrderByPhysicalPosition` reads only `Bounds.X` | Design |
| F12 | No sweep-reversal gesture | `LookTravel` has one `Order` per Look | Design |
| F13 | `BufferStyle` never emitted | Not in the Look schema, not in the exporter | Design |
| F14 | `BufferTransform` never emitted | Same | Design |
| F15 | `LayerMethod`/blend never emitted | `LookLayer.Blend` is IR-only by disclosed design | Design |
| F16 | Sibling matrixes get unequal treatment | Nothing models "mirror group" | Design |
| F17 | Tree/Matrix layer depth 1–3 vs. corpus 12–14 | One Director call budgets the whole show | LLM architecture |
| F18 | Style preset never selectable | `AiChoreographyWholeSongGenerator` hardcodes `TraditionalClassicId`; "no style-selection UI exists" (its own doc comment) | Hardcoded |

---

## 3. Design changes

### 3.1 Beat/bar disambiguation and real meter detection *(Rhythm — fixes F1, F2)*

**`src/XlightsAI.Studio.Rhythm/TempoDetector.cs`**

- Keep the existing autocorrelation scan unchanged. After candidates are ranked, add a
  **metrical-level resolution** step: for the top candidate's lag `L`, also score `L/2`, `L/3`, `L/4`
  (and `2L`, `3L`) through the same real autocorrelation function, then choose the reported *beat*
  as the shortest lag whose score is within `BeatLevelTolerance` of the best score **and** whose BPM
  falls inside a configurable preferred-tempo window.
- Replace `private const string DefaultMeterGuess = "4/4"` with a real estimate: for each candidate
  grouping `g ∈ {2, 3, 4, 6}`, measure mean onset strength at beat positions `k ≡ 0 (mod g)` versus
  all others; the grouping with the highest downbeat-to-offbeat strength ratio wins, and the ratio
  itself is reported as the meter's confidence. Report `"3/4"` for `g = 3`, `"4/4"` for `g = 4`, and
  so on. Report `MeterConfidence` alongside, never a bare guess.
- New settings on `TempoDetectionSettings`:

  | Field | Default | Rationale for the default |
  |---|---|---|
  | `PreferredBpmMin` | 85 | Below this a "beat" is almost always a bar in disguise; chosen so 63.2 BPM is rejected and 189.9 is not |
  | `PreferredBpmMax` | 190 | Above this the candidate is almost always a subdivision; 189.9 must remain admissible |
  | `BeatLevelTolerance` | 0.80 | A subdivision lag must score at least 80% of the bar lag's own score before it is preferred — it must be genuinely corroborated, not merely mathematically possible |
  | `CandidateMeters` | `[2, 3, 4, 6]` | The meters this codebase can express; 5/7 are out of scope and should stay out rather than be half-supported |

  `MinBpm = 60` / `MaxBpm = 200` stay as the *scan* range; the preferred window only governs which
  already-scored lag is reported as the beat.

**`src/XlightsAI.Studio.Rhythm/BeatGridSettings.cs`** — `BeatsPerBar` becomes nullable
(`int? BeatsPerBar`). `null` means *"take it from `TempoAnalysis`'s detected meter"*.
`BeatGridBuilder.BuildAsync` resolves it: detected meter first, then the explicit setting, then 4 as
a last resort with that fact recorded on the grid so it is visible rather than silent.

**`src/XlightsAI.Studio.Pipeline/AudioAnalysisSettingsBundle.cs:44`** — change
`BeatGridSettings(BeatsPerBar: 4, ...)` to `BeatGridSettings(BeatsPerBar: null, ...)`.
`EffectFixturePlanner.cs:252` keeps its explicit `4` (a synthetic fixture, not a real song).

**Acceptance (F1/F2):** re-analyze `09. Carol Of The Bells (Instrumental).mp3`; the selected tempo
must be 185–195 BPM with meter `3/4`, and the emitted `Bars` track's median inter-mark gap must be
900–1000 ms (matching the human track's 950 ms cluster), not 3,800 ms.

### 3.2 A real subdivision grid *(Choreography — fixes F3, F5)*

**New: `src/XlightsAI.Studio.Choreography/SubdivisionGrid.cs`**

```csharp
/// <summary>The real note-value subdivisions of this song's own beat grid.</summary>
public enum NoteResolution { Bar, Beat, Eighth, Triplet, Sixteenth }

public static class SubdivisionGrid
{
    /// <summary>Marks for <paramref name="resolution"/>, derived by subdividing each real
    /// consecutive beat pair — never a fixed period extrapolated from an average BPM, so a
    /// tempo-drifting or hand-corrected grid subdivides correctly.</summary>
    public static TimingTrack Build(BeatGrid grid, NoteResolution resolution, long songEndTicks);
}
```

**`src/XlightsAI.Studio.Choreography/TimingTrackEmitter.cs`** gains `Eighths`, `Sixteenths` and
`Triplets` emitters built on it. `ChoreographyEngine` emits them whenever the beat grid has ≥2 beats.

**`src/XlightsAI.Studio.Choreography/TimescaleResolver.cs`** gains the aliases:
`"eighth"/"eighths"/"8th" → "Eighths"`, `"sixteenth"/"sixteenths"/"16th" → "Sixteenths"`,
`"triplet"/"triplets" → "Triplets"`.

**`src/XlightsAI.Studio.Choreography/LookCompiler.cs`** — `ThinToTargetChangeRate` becomes
`FitToTargetChangeRate`, which is allowed to go **both** directions:

- Above target → merge consecutive marks (today's behavior, unchanged).
- Below target by more than `SubdivideHysteresis` → subdivide each window at the next finer
  `NoteResolution`, repeating while still below target and while the resulting window stays ≥
  `MinEffectDurationTicks`.
- Within hysteresis → leave untouched.

The hysteresis matters: without it a layer naturally sitting at 0.9× target oscillates between
resolutions across section boundaries and the show reads as unstable. Proposed default `0.5` —
subdivide only when the natural cadence is under *half* the target, i.e. when a genuine whole
metrical level is missing, never to shave off a rounding difference.

**Acceptance (F3/F5):** regenerate *Carol of the Bells*; the exported duration histogram must show
≥15% of effects at ≤200 ms (human reference: 17.5%; current app: 4.3%), and no single duration value
may account for >15% of all effects (current: 950 ms at 35%).

### 3.3 Density budget: derived, not constant *(Choreography/Composition — fixes F4)*

`SectionDensityMapper.TargetChangeRatePerSecond` currently ignores both the song and the style.
Replace the two constants with a resolved budget:

```csharp
public static double TargetChangeRatePerSecond(
    double energyCeiling,
    StyleDensity styleBand,       // the style preset's own declared Min/Max
    double beatsPerSecond,        // this song's own real, detected beat rate
    DensityBudgetSettings s);
```

Resolution, in order:

1. Interpolate the **style's own declared band** across the ceiling:
   `styleBand.Min + ceiling × (styleBand.Max − styleBand.Min)`. This alone removes the F4
   contradiction — `HighEnergyEdm` at ceiling 0.85 now targets 2.5 + 0.85×3.5 = **5.5/s**, which is
   what its own `StyleDensity` always said.
2. **Snap to a musical level.** Round the result to the nearest available note resolution's own rate
   (`beatsPerSecond × {0.25, 1, 2, 3, 4}` for bar/beat/eighth/triplet/sixteenth). A target of 5.5/s
   against a 3.16 beats/s song snaps to the eighth level (6.32/s), not to an unmusical 5.5.
3. Clamp to `[s.AbsoluteFloorPerSecond, s.AbsoluteCeilingPerSecond]`.

`DensityBudgetSettings` — every field user-settable (§4):

| Field | Default | Where the default comes from |
|---|---|---|
| `AbsoluteFloorPerSecond` | 0.2 | Unchanged from today's `FloorChangeRatePerSecond` — the always-moving gap-filler needs something to build on |
| `AbsoluteCeilingPerSecond` | 8.0 | The real measured peak in a human show (*All I Want For Christmas*, §1.3) — not a guess, the top of observed human practice |
| `SubdivideHysteresis` | 0.5 | §3.2 |
| `MinEffectDurationTicks` | 1,000,000 (100 ms) | The minimum inter-start gap observed in both human references (§1.3); also comfortably above `FrameClock.FrameIntervalTicks` |
| `SnapToMusicalLevel` | `true` | Off is available for a user who wants a literal rate |

`LookCompiler.VocalHoldMinDuty = 0.9` and `VocalHoldTimescale = "Bars"` move onto the same settings
object (`VocalHoldMinDuty`, `VocalHoldTimescale`) rather than staying `private const` — with
`"Bars"` now meaning a real 950 ms bar rather than a 3.8 s one, the existing default becomes
*more* aggressive than intended and needs to be visible and tunable.

### 3.4 Hit shape: an effect can be shorter than its grid cell *(Choreography — fixes F5)*

`CompileDiscreteLayer` currently derives duration purely as `duty × inter-mark span`. Add an
explicit shape to `LookLayer`:

```csharp
public enum LayerEmissionShape { Fill, Hit, Hold }
```

- `Fill` — today's behavior exactly. Default, so every existing Look compiles byte-identically.
- `Hit` — duration is `min(duty × span, HitDurationTicks)`, i.e. an absolute short pop that does not
  grow when the grid is coarse. Default `HitDurationTicks` = 1,500,000 (150 ms), the modal
  short-effect duration in the human reference (79 of 458 effects at exactly 150 ms, §1.1).
- `Hold` — one effect spanning every consecutive mark in the window, i.e. the `On`-as-base-layer
  pattern the reference uses 164 times.

This is the mechanism that makes the Phase 27 "rhythmic stab" prompt guidance actually executable,
and it is what the horn-phrase treatment (§3.6) binds to.

### 3.5 Per-instance parameter variation *(Choreography/Knowledge — fixes F6)*

The 5-entry `EffectDB` is the single most visible difference from every human show. Two changes:

**(a) `src/XlightsAI.Studio.Choreography/Look.cs` — `LookLayer` gains `ParamCurves`:**

```csharp
/// <summary>Per-instance parameter variation across this layer's own emitted effects: a parameter
/// key mapped to a declared traversal of its real vocabulary range (e.g. Speed sweeping 20→80
/// across a section's own marks). Distinct from <see cref="Curves"/>, which binds an xLights
/// value-curve INSIDE one effect; this varies the plain parameter BETWEEN effects.</summary>
IReadOnlyDictionary<string, LookParamSweep>? ParamCurves = null

public sealed record LookParamSweep(string Mode, double From, double To);
// Mode: "linear" | "pingpong" | "accelerate" | "random"
```

`CompileDiscreteLayer` evaluates each sweep at each mark index and writes the resolved value into
that instance's `Parameters`. Every value is validated through the existing
`recipe.IsSupportedParameterValue` path, so nothing invented reaches the exporter.

**(b) A deterministic floor, independent of what the Director writes.** Even with (a) available,
nothing guarantees the Director uses it. Add a new soft rule to
`ChoreographyLegalityValidator.ValidateShow`:

```csharp
public const string RuleParameterMonotony = "effect-parameter-monotony";
```

For any effect name used ≥ `MinInstancesForMonotonyCheck` times (proposed 20) across the show, flag
it when its distinct resolved parameter-string count is below
`MinDistinctSettingsRatio × instanceCount`. Proposed ratio **0.10**, derived from real practice:
*Run Rudolph Run* has 212 `EffectDB` entries for ~1,900 effects (11%); the human *Carol* has 71 for
458 (15.5%); the current app has 5 for 1,089 (**0.46%**). A 10% floor is below both human shows and
twenty times above where we are.

Soft/advisory initially, consistent with `RuleEffectConcentration`. Escalate to the self-healing
tier (`RevisionDirectiveDeriver`) only after a real run shows the advisory firing and the Director
responding to it — same discipline Phases 22/24 used.

### 3.6 Horn/riff detection *(Rhythm — fixes F8, partially F9)*

**New: `src/XlightsAI.Studio.Rhythm/RepeatingFigureDetector.cs`**

`TransientHitDetector` finds what is locally *anomalous*. This finds what is locally *periodic* —
the complementary signal, and the one a horn riff actually produces.

```csharp
public sealed record RepeatingFigureSettings(
    long MinFigurePeriodTicks = 1_000_000L,   // 100ms — faster than this is a roll, not a figure
    long MaxFigurePeriodTicks = 20_000_000L,  // 2s — slower than this is phrase structure, not a figure
    int  MinRepetitions       = 3,            // two occurrences is a coincidence
    double MinAutocorrelation = 0.6);

public sealed record RepeatingFigure(
    long StartTicks, long EndTicks, long PeriodTicks,
    IReadOnlyList<long> OnsetTicks, double Strength);

public static class RepeatingFigureDetector
{
    /// <summary>Scans <paramref name="onsets"/> inside each <paramref name="leadWindows"/> entry for
    /// a repeating inter-onset pattern — pure computation over existing analysis, no new DSP.</summary>
    public static IReadOnlyList<RepeatingFigure> Detect(
        OnsetAnalysis onsets,
        IReadOnlyList<LeadInstrumentWindow> leadWindows,
        BandEnergyEnvelope bandEnergy,
        RepeatingFigureSettings? settings = null);
}
```

Scoped to `LeadInstrumentWindowDetector`'s existing windows, so it reports a repeating figure *that
is also prominent* — which is exactly "the horns are playing a riff right now" and not "the hi-hat
exists".

Wiring:

- `MusicModel` gains `RepeatingFigures`, following the existing nullable-optional-signal convention
  (`LeadInstrumentWindows`, `KeyChanges`, `EnergyTransitions` all do this).
- `TimingTrackEmitter` gains `Figures(...)` → a `"Figures"` track whose marks are every onset inside
  every detected figure. `TimescaleResolver` aliases `"figure"/"figures"/"riff"/"stab" → "Figures"`.
- `DirectorPromptBuilder.AppendLeadInstrumentWindows` is extended to name the figure's real period
  and repetition count, and to state that a `"Figures"`-bound layer with
  `EmissionShape: "Hit"` is the concrete way to realize the stab technique the prompt already asks
  for in prose.

**`TransientHitDetector` settings become user-reachable** (§4) rather than `Default`-only:
`LocalWindowTicks`, `LocalSensitivityMultiplier`, `MinimumAbsoluteStrength`. The 15-second window in
particular is the wrong scale for a dense song and the user should be able to shorten it.

**Out of scope, named explicitly:** 4-stem separation (F9). `SherpaOnnxSpleeterModelAsset` is a
2-stem model; adding drums/bass/other means a new ONNX asset, new download consent flow, new
feasibility evaluation and a re-run of every stem quality gate. That is its own phase, sketched next
(§3.6a) rather than folded into this one's step sequence. §3.6 above is the best available signal
*without* it, and it is a real signal, not a substitute.

### 3.6a Follow-on (Phase 29, not in this phase's step sequence): 4-stem separation

Named here, scoped to the point of being actionable, and deliberately excluded from §6's step
sequence and §5's gates — it is a materially larger, independently-gated unit of work in its own
right (a new model asset, a new consent/download/feasibility path, a new quality-gate pass), not a
natural sub-step of the engine changes above it. This section exists so the gap is a disclosed,
planned follow-on rather than a silent limitation discovered later.

**Why 2-stem is a real ceiling, not just an inconvenience.** §3.6's `RepeatingFigureDetector` finds
*that* a sustained repeating figure is playing; it cannot say *which instrument* is playing it when
the accompaniment stem is horns, bells, strings, guitar and keys all summed together. A Look that
wants to bind specifically to "the horn stabs, not the string pad underneath them" has no signal to
bind to until the stems themselves are separated further. This is the real reason the horn figures in
§1.4 reach the compiler as a generic "something prominent is happening" window rather than a
cleanly-isolated horn track.

**Model selection (needs its own verification pass, not assumed here).** The existing
`SherpaOnnxSpleeterModelAsset`/`SpectrogramOnnxStemSeparator` pair is built around a 2-stem
Spleeter-family architecture (`vocals`/`accompaniment`). A 4-stem split (`vocals`/`drums`/`bass`/`other`)
needs either (a) a 4-stem Spleeter variant run through the same spectrogram-masking pipeline already
in `SpectrogramStemMasking`/`StftConfig`, which this codebase already knows how to drive, or (b) a
different architecture (e.g. a Demucs-family model) that does not share that pipeline and would need
its own `ISpectrogramStemSeparator`-equivalent implementation. Before committing to either, verify
against the same real feasibility criteria `ModelFeasibilityEvaluator`/`ModelFeasibilityReport`
already apply to the current 2-stem model: ONNX exportability, CPU-only inference time on the
project's own target hardware profile (`HardwareProfile`), and real separation quality
(`SeparationQualityEvaluator`/`SeparationQualityThresholds`) measured against held-out real songs —
never assumed from a model card alone.

**Shape of the work, by existing seam:**

| New symbol | Mirrors (existing 2-stem equivalent) |
|---|---|
| `FourStemSpleeterModelAsset` (or a Demucs-family equivalent) | `SherpaOnnxSpleeterModelAsset` |
| `StemKind.Drums` / `StemKind.Bass` / `StemKind.Other` | `StemKind.Vocals` (today's only non-accompaniment member) |
| A 4-output `SpectrogramModelBundle` | Today's 2-output bundle |
| `FourStemSeparationQualityThresholds` | `SeparationQualityThresholds` |
| A new `ModelAssetConsentRequiredException`-gated download | The existing vocals/accompaniment download path — same consent discipline, no shortcut |

**Downstream wiring, once real stems exist:**

- `RhythmSettings`/`OnsetDetector`/`BandEnergyAnalyzer` run unchanged, once each, against the new
  `drums` and `other` (and, for completeness, `bass`) stems exactly as they already run once against
  the vocals stem for `VocalActivityAnalyzer` — no new DSP, only new inputs to DSP that already
  exists.
- `MusicModel` gains a per-stem onset/band-energy result (e.g. `InstrumentActivity` keyed by
  `StemKind`), following the same nullable-optional-signal convention as `LeadInstrumentWindows`/
  `KeyChanges` — absent entirely for any run that has not consented to/does not have the 4-stem model
  installed, so every existing single-stem caller keeps compiling and behaving unchanged.
- `TimingTrackEmitter` gains one track per populated stem (e.g. `"Drums"`, `"Other"`), reusing the
  same `FromEmphasisInstants`-style construction `Transients`/`VocalNotes` already use.
  `TimescaleResolver` gains the matching aliases.
- `DirectorPromptBuilder.AppendLeadInstrumentWindows`/the new `## Canvas mapping` section (§3.8)
  extend to name which stem a given lead-instrument window actually comes from (e.g. *"00:08-00:22,
  Other-stem-dominant — likely the horn section"*) instead of the stem-agnostic prose it falls back
  to without this.
- `BandEnergyGroupRoleMapper`'s existing bass→foundation/mid→subject/high→accent mapping (§ "Band-to-
  group mapping" in the Director prompt) is a natural extension point: once a real `bass` stem exists
  independent of the full accompaniment mix, the foundation-group mapping can bind to it directly
  rather than to the full mix's low end.

**Gates this follow-on would need (not part of §5's G1-G14):**

| Gate | Threshold |
|---|---|
| 4-stem separation quality (SDR or equivalent, each of the 4 stems) | Meets or exceeds the same bar `SeparationQualityThresholds` already enforces for the 2-stem model, measured on the same held-out real songs |
| CPU-only inference time, target hardware profile | Within the same wall-clock budget the 2-stem model is already gated on |
| A horn-dominant window (e.g. Carol of the Bells 00:08-00:22, or an equivalent real song) | The `Other`-stem onset track shows measurably higher onset density in that window than the full-mix accompaniment track does elsewhere — proof the separation is actually isolating the instrument, not just relabeling the same mix |

**Disclosed risk specific to this follow-on:** a 4-stem split is a strictly harder separation problem
than 2-stem and is well-documented in the wider field as prone to cross-talk (bleed between `other`
and `drums`/`bass`). The feasibility pass above must treat a model that merely *runs* as insufficient
— real separation quality on real songs is the actual bar, the same discipline
`ModelFeasibilityEvaluator` already applies to the current model.

### 3.7 Spatial motion *(Choreography — fixes F10, F11, F12)*

**`src/XlightsAI.Studio.Choreography/Look.cs` — `LookTravel` is extended:**

```csharp
public sealed record LookTravel(
    string Order,              // LeftToRight | RightToLeft | TopToBottom | BottomToTop
                               //   | InsideOut | OutsideIn
    string Timescale,
    string Shape    = ShapeChase,   // Chase | Accumulate | Dissipate
    bool   Reverse  = false,        // emit the mirror sweep immediately after
    int    Repeats  = 1)
{
    public const string OrderLeftToRight = "LeftToRight";
    public const string OrderRightToLeft = "RightToLeft";
    public const string OrderTopToBottom = "TopToBottom";
    public const string OrderBottomToTop = "BottomToTop";
    public const string OrderInsideOut   = "InsideOut";
    public const string OrderOutsideIn   = "OutsideIn";

    public const string ShapeChase      = "Chase";       // today: each member its own window
    public const string ShapeAccumulate = "Accumulate";  // staggered START, shared END  (§1.5 Gesture A)
    public const string ShapeDissipate  = "Dissipate";   // shared START, staggered END
}
```

**`src/XlightsAI.Studio.Choreography/PropGroupResolver.cs`** —
`OrderByPhysicalPosition(models, layout, leftToRight)` is replaced by
`OrderByAxis(models, layout, string order)`:

- `LeftToRight` / `RightToLeft` → `Bounds.X` ascending / descending (today's behavior).
- `TopToBottom` / `BottomToTop` → `Bounds.Y` descending / ascending. `DisplayBounds.Y` is already
  imported and currently unused.
- `InsideOut` / `OutsideIn` → distance from the group's own X-centroid, ascending / descending.

The existing "a model with no `Bounds` is never guessed into a position, keeps its relative order,
and is appended last" discipline is preserved on every axis. The old method is kept as a thin
delegating overload so no existing caller or test breaks.

**`src/XlightsAI.Studio.Choreography/PropDelayResolver.cs`** —
`ResolveTravelWindows` gains the shape parameter:

```csharp
public static IReadOnlyDictionary<ModelId, TimeWindow> ResolveTravelWindows(
    IReadOnlyList<ModelId> orderedModels, IReadOnlyList<TimingMark> marks,
    long startTicks, long endTicks, string shape);
```

- `Chase` — unchanged: member *i* gets mark *i*'s own window.
- `Accumulate` — member *i* starts at mark *i*; **every** member ends at
  `marks[orderedModels.Count - 1].EndTicks`. This is Gesture A from §1.5 exactly.
- `Dissipate` — the inverse: all start at `marks[0].StartTicks`, member *i* ends at mark *i*.

**`LookCompiler.cs`** — in the `look.Travel is { } travelSpec` branch, when `Reverse` is true the
compiler emits the sweep, then immediately emits the mirrored sweep over the next
`orderedModels.Count` marks of the same track. `Repeats > 1` repeats the pair. This makes
Gesture C + C′ from §1.5 a single declaration.

**Validation.** A new soft rule, `RuleNoDirectionalMotion`: flag a show in which no compiled
section used any `Travel` at all. The reference shows use directional motion constantly; a show with
none reads as flat regardless of how dense it is. Advisory tier, same as `RuleGroupNeverTargetedDirectly`.

### 3.8 Buffer style, buffer transform and layer blend *(Knowledge/Choreography/Xlights — fixes F13, F14, F15)*

This is the highest-leverage change in the phase and the one with the most unknowns, so it is
sequenced as **verify first, then emit**.

**Step 1 — verify the real choice lists.** The project's standing discipline is "do not invent
xLights effect parameters." `EffectVocabularyBuilder` already has the machinery and already carries
documented hardcoded exceptions for `LayerMethod`/`LayerMorph`/`EffectLayerMix`. Extend it with
`BufferStyle` and `BufferTransform` rows **only** after confirming the acceptable values against
(a) the real xLights effect metadata directory this app already reads, and (b) the values actually
observed in the corpus (§1.6's tables are the ground truth; they came from a real file xLights wrote).
Where the two disagree, trust the corpus and record the discrepancy — a value xLights itself wrote
to disk is verified by definition.

**Step 2 — carry them on the Look.** `LookLayer` gains:

```csharp
string? BufferStyle     = null,   // "Per Preview" | "Per Model Per Preview" | "Default" | ...
string? BufferTransform = null,   // "Rotate CC 90" | "Flip Horizontal" | "Rotate CW 90" | "Rotate 180"
```

`LookLayer.Blend` stops being IR-only and is emitted as `T_CHOICE_LayerMethod` once its choice list
is verified in Step 1.

**Step 3 — emit them.** `XsqExporter` writes `B_CHOICE_BufferStyle`, `B_CHOICE_BufferTransform` and
`T_CHOICE_LayerMethod` into the settings string. `EffectSettingsSerializer` already handles the
`T_`-over-`B_` `LayerMethod` precedence rule (line 82-86) — reuse it unchanged. xLights omits
defaults from the settings string entirely (architecture §8a), so a `null` on any of the three must
emit **nothing**, preserving byte-identical output for every existing Look.

**Step 4 — teach the Director.** `DirectorPromptBuilder` gains a `## Canvas mapping and buffer
transforms` section stating, with the §1.6 counts as the evidence:

- `Per Preview` maps the effect across the whole layout rather than per model — this is how a
  whole-house group reads as one gesture instead of N independent small ones. Measured: 512 uses in
  one real show.
- `Rotate CC 90` turns a horizontal chase into a vertical one — the standard way a `SingleStrand`
  runs *up* a mega tree. Measured: 676 uses.
- `Flip Horizontal` on an otherwise-identical duplicate layer produces mirror symmetry. Measured as
  half of a 4-layer kaleidoscope.

### 3.9 Mirror groups: all matrixes together *(Xlights/Choreography — fixes F16)*

**`src/XlightsAI.Studio.Xlights/ShowLayoutImporter.cs`** — detect **mirror sets**: models of the
same `DisplayModel` type whose `NodeCount` is within `MirrorPixelTolerance` (proposed 15%) and whose
xLights names share a stem modulo a trailing index (`Matrix` / `Matrix-2` / `Matrix3`;
`MiniTree1..4`; `Arches-1..4`). Expose as `DisplayGraph.MirrorSets`.

**`src/XlightsAI.Studio.Director/LayoutSummary.cs`** — surface them in the prompt verbatim, e.g.
*"Mirror set: Matrix, Matrix-2, Matrix3 (3 models, 2,048px each). These read as one surface to a
viewer. Give them the same treatment at the same instant unless the Look is deliberately moving
between them (Travel)."*

**`src/XlightsAI.Studio.Choreography/ChoreographyLegalityValidator.cs`** — new rule:

```csharp
public const string RuleMirrorSetDivergence = "mirror-set-divergence";
```

For each mirror set, compare per-member effect counts; flag when
`max/min > MirrorDivergenceRatio` (proposed **2.0**) **unless** every effect on the diverging members
belongs to a `Travel`-shaped Look (the legitimate exception the user named: *"unless you are working
on moving from one to the next"*). Today's run would fire this immediately: `Matrix` 65 / `Matrix-2`
6 / `Matrix3` 24, a ratio of 10.8.

Escalate to the self-healing tier alongside `RuleModelOnlyGroupCovered` — "two of three matrixes are
nearly empty" is an unambiguous content gap, not a busyness judgment, which is the stated bar for
that tier.

### 3.10 Progressive, tool-calling Director *(Director — fixes F17, and gives F5/F6/F7/F13 somewhere to live)*

The current shape is one 16k-token Director call producing the entire Show Bible, followed by N
select-only Choreographer calls. §1.7 shows why that cannot produce a 12-layer Tree.

**Step 1 — extend the client seam.** `IChatCompletionClient` today is deliberately
`(SystemPrompt, UserPrompt) → text`, documented as *"Kept as this two-part split (not a richer
multi-turn message list) because that is exactly the cache boundary every supported provider's own
prompt-caching mechanism keys on."* That reasoning is sound and must be preserved, so **add a
sibling interface rather than changing this one**:

```csharp
public sealed record ToolDefinition(string Name, string Description, string JsonSchema);
public sealed record ToolCall(string Id, string Name, string ArgumentsJson);
public sealed record ToolResult(string CallId, string ContentJson, bool IsError = false);

public sealed record ToolChatRequest(
    string SystemPrompt,                       // still the cacheable prefix, still first
    IReadOnlyList<ChatTurn> Turns,             // user / assistant / toolResult, in order
    IReadOnlyList<ToolDefinition> Tools,
    bool CacheSystemPrompt,
    int MaxOutputTokens);

public sealed record ToolChatResult(
    string? Text, IReadOnlyList<ToolCall> ToolCalls, AiUsage Usage, RequestDiagnostics? Diagnostics = null);

public interface IToolCallingChatClient
{
    Task<ToolChatResult> CompleteAsync(ToolChatRequest request, CancellationToken ct);
}
```

Implemented on `AnthropicChatCompletionClient` (`tools` / `tool_use` / `tool_result`) and
`OpenAiCompatibleChatCompletionClient` (`tools` / `tool_calls`). `OpenAiResponsesChatCompletionClient`
and `AnthropicBatchChatCompletionClient` may implement it later; a provider that does not implement
it must fall back cleanly to today's single-shot path, which stays fully supported. Caching is
preserved: the system prompt remains the cacheable prefix and the turn list grows only by append, so
every round reuses the previous round's cached prefix.

**Step 2 — the tools.** These are retrieval tools, not actions. All are read-only and all are
backed by data this codebase already computes:

| Tool | Returns | Backing code (already exists) |
|---|---|---|
| `get_effect_parameters(effectName)` | Every real parameter, type, range and acceptable value | `EffectVocabularyCatalog` |
| `get_effect_visual_character(effectName)` | The grounded visual description, or an explicit "not exercised" | `EffectVisualCharacterCatalog` |
| `get_exemplars(sectionKind, minLayers, limit)` | Real corpus passages, on demand | `ExemplarIndex.FindSimilar` |
| `get_multilayer_template(id)` | A full mined stack, layer by layer | `MultiLayerTemplateCatalog` |
| `get_section_audio_detail(sectionId)` | Band balance, contour, onset density, figures, lead windows for one section | `MusicModel` + §3.6 |
| `get_layout_detail(reference)` | One model/group's real pixels, bounds, mirror set, members | `DisplayGraph` |
| `get_timing_track_summary(trackName)` | Mark count, median gap, coverage | `TimingTrackEmitter` output |

**Step 3 — progressive authoring loop.** Replace the single Director call with:

1. **Plan call.** Section list, structure, measured signals, layout summary, mirror sets, lead/figure
   windows. The Director returns a *skeleton*: palettes, the show's arc, and a one-line brief per
   Look it intends to write — not the Looks themselves.
2. **Per-Look authoring calls**, batched a few Looks at a time, each with tools available. A Look
   for the Tree can now spend its whole output budget on the Tree: 6–12 layers, real parameters
   looked up rather than recalled, buffer transforms, a `ParamCurves` sweep.
3. **Assembly and validation.** Existing `ShowBibleValidator` and `ChoreographyLegalityValidator`
   run unchanged. Violations from the self-healing tier feed one targeted re-author call per
   affected Look — reusing the `RevisionDirectiveDeriver` machinery that already exists, rather than
   regenerating the whole bible.

This is strictly additive. `ChoreographyGenerationPipeline` keeps the single-shot path behind a
setting (`DirectorMode: SingleShot | Progressive`), defaulting to `Progressive` only once the
progressive path has passed the §5 gates on both reference songs.

**Cost note, stated plainly:** progressive authoring means more calls. A 20-Look show becomes roughly
1 plan call + 7 authoring calls + repairs instead of 1 call. With the stable prefix cached and the
tool results being small JSON, the expected net increase is on the order of 3–5× input tokens and
2–3× output tokens for the Director stage. The Choreographer stage is unchanged. This should be
surfaced as an estimate in the UI before a run starts, not discovered afterward.

---

## 4. Every hardcoded number, and what happens to it

The user's requirement: *"Any hard coded numbers need to be something we should either calculate
better or have as a setting the user can change when requesting to generate the sequence."*

### 4.1 Becomes derived (no setting needed)

| Constant | File | Becomes |
|---|---|---|
| `BeatGridSettings(BeatsPerBar: 4)` | `AudioAnalysisSettingsBundle.cs:44` | Detected meter (§3.1) |
| `DefaultMeterGuess = "4/4"` | `TempoDetector.cs` | Detected meter + confidence (§3.1) |
| `FloorChangeRatePerSecond = 0.2` / `CeilingToRateSlope = 2.4` | `SectionDensityMapper.cs:25,28` | Style band × ceiling, snapped to a musical level (§3.3) |
| `assumedBeatsPerBar = 4` | `MultiLayerStackMiner.cs:207` | The mined song's own detected meter |

### 4.2 Becomes a user-visible generation setting

New `GenerationTuning` record, surfaced on the generate-sequence screen next to the existing custom
prompt, persisted per project, and passed through `AiChoreographyWholeSongGenerator` →
`ChoreographyGenerationPipeline`. Grouped as the UI should group them:

**Style and energy**

| Setting | Today | Default | Source |
|---|---|---|---|
| `Style` | **hardcoded `TraditionalClassicId`, no UI** (F18) | user picks from `StylePresetCatalog.All` | — |
| `EnergyCeilingByKind` | `DefaultEnergyCeilings.ArchitectureSection10` | unchanged | Architecture §10 |
| `DensityScale` | n/a | 1.0 | Multiplies the §3.3 resolved target; one knob for "busier/calmer" |

**Rhythm and resolution**

| Setting | Today | Default | Source |
|---|---|---|---|
| `PreferredBpmMin` / `Max` | n/a | 85 / 190 | §3.1 |
| `MeterOverride` | n/a | `null` (auto) | User can force 3/4, 4/4, 6/8 |
| `FinestNoteResolution` | n/a | `Sixteenth` | Caps how fine §3.2 may subdivide |
| `MinEffectDurationMs` | n/a | 100 | Measured human minimum (§1.3) |
| `HitDurationMs` | n/a | 150 | Modal human short-effect duration (§1.1) |
| `SubdivideHysteresis` | n/a | 0.5 | §3.3 |
| `AbsoluteCeilingPerSecond` | implicit 2.6 | 8.0 | Measured human peak (§1.3) |

**Emphasis detection**

| Setting | Today | Default | Source |
|---|---|---|---|
| `TransientLocalWindowSeconds` | `const` 15 | 15 | Unchanged default, now adjustable |
| `TransientSensitivity` | `const` 1.5 | 1.5 | Unchanged default, now adjustable |
| `TransientMinStrength` | `const` 0.05 | 0.05 | Unchanged default, now adjustable |
| `FigureMinRepetitions` | n/a | 3 | §3.6 |
| `FigureMinAutocorrelation` | n/a | 0.6 | §3.6 |

**Composition**

| Setting | Today | Default | Source |
|---|---|---|---|
| `VocalHoldMinDuty` | `const` 0.9 | 0.9 | `LookCompiler.cs:28` |
| `VocalHoldTimescale` | `const` "Bars" | "Bars" | `LookCompiler.cs:29` |
| `MaxConcurrentFocalModels` | `const` 4 / 5 | 4 / 5 | `ChoreographyLegalityValidator.cs:78,87` |
| `MaxSingleEffectShare` | `const` 0.35 | 0.35 | `ChoreographyLegalityValidator.cs:194` |
| `MinDistinctSettingsRatio` | n/a | 0.10 | §3.5 |
| `MirrorDivergenceRatio` | n/a | 2.0 | §3.9 |
| `TargetLayerDepth` (tree/matrix) | n/a | 4 | Corpus median for pixel-heavy props; *Run Rudolph* reaches 12–14 |

**AI**

| Setting | Today | Default | Source |
|---|---|---|---|
| `DirectorMode` | n/a | `Progressive` (after gates pass) | §3.10 |
| `MaxOutputTokens` | 16000 hardcoded default | 16000 | `AiChoreographyWholeSongGenerator` |
| `MaxLookAuthoringCalls` | n/a | 10 | Cost ceiling for §3.10 |

### 4.3 Stays constant, with the reason recorded

| Constant | Why it stays |
|---|---|
| `TicksPerSecond = 10_000_000L` (everywhere) | A unit, not a tuning parameter |
| `FrameClock.FrameIntervalTicks` | Determined by the sequence's own frame rate |
| `LowResolutionNodeCountThreshold = 60` | A real pixel-count boundary, already documented against real props |
| `HoldCoverageThreshold = 0.6` | Internal classification for one advisory rule; no user-facing meaning |
| `AlignedConfidenceFloor` / `UnalignedConfidenceDecay` | Confidence bookkeeping, not creative choices |

### 4.4 `On` belongs in the high-energy pool (F7)

`StylePresetCatalog.HighEnergyEdm.EffectPool` has no `On`. The reference uses it for 35.8% of all
effects; the `SteadyWashAccent` preset exists *because* a prior comparison found the same thing
(its own doc comment: *"326 of 462 effects, ~70%... The generated version had zero 'On' usage at all
(not in High-Energy/EDM's effect pool)"*). The finding was recorded and acted on by adding a new
preset, but the EDM pool itself was never corrected. Add `"On"` to it. A held, saturated color *is*
a high-energy gesture — it is the loudest thing a light show can do.

---

## 5. Validation gates

Every gate is measured against real files with the §1 reader. No gate passes on a synthetic fixture.

| # | Gate | Threshold | Current value |
|---|---|---|---|
| G1 | Detected tempo for *Carol of the Bells* | 185–195 BPM, meter `3/4` | 63.2 BPM, `4/4` |
| G2 | Emitted `Bars` median inter-mark gap | 900–1000 ms | 3,800 ms |
| G3 | Effects ≤200 ms | ≥15% of total | 4.3% |
| G4 | No single duration value dominates | ≤15% of total | 35% (950 ms) |
| G5 | Distinct `EffectDB` entries | ≥10% of effect count | 0.46% |
| G6 | Mirror-set divergence (`Matrix`/`Matrix-2`/`Matrix3`) | max/min ≤ 2.0 | 10.8 |
| G7 | `Tree` effect count | ≥40 | 3 |
| G8 | `Tree` layer depth at its densest instant | ≥4 | 3 (carrying 3 effects total) |
| G9 | Whole-house group (`Everything`) targeted directly | ≥1 section | 0 |
| G10 | `Travel`-driven sections | ≥3, including ≥1 vertical axis | 0 |
| G11 | `BufferStyle`/`BufferTransform` emitted | ≥1 each, and the file reopens in xLights without error | 0 |
| G12 | Horn phrase 00:08–00:22 | ≥1 `Figures`-bound `Hit`-shaped layer | none |
| G13 | Regression | Every existing test passes; a Look declaring none of the new fields compiles byte-identically | — |
| G14 | Round-trip | Every regenerated `.xsq` opens in a real installed xLights and renders to `.fseq` without error | — |

**G13 and G14 are not optional.** Every new field on `Look`/`LookLayer`/`LookTravel` is nullable or
defaulted so that a Show Bible written before this phase compiles to the same bytes. And every
setting in §3.8 is a string written into a settings blob that xLights parses — an unverified value
there is a corrupt sequence, which is why §3.8 Step 1 comes before Step 3.

---

## 6. Step sequence

Ordered so that each step is independently verifiable and nothing depends on an unfinished predecessor.

| Step | Work | Gates |
|---|---|---|
| 28.1 | Metrical-level resolution + meter detection (§3.1) | G1, G2 |
| 28.2 | `SubdivisionGrid` + `TimescaleResolver` aliases + emitters (§3.2a) | — |
| 28.3 | `FitToTargetChangeRate` two-way (§3.2b) + derived density budget (§3.3) | G3, G4 |
| 28.4 | `LayerEmissionShape` (§3.4) | G3 |
| 28.5 | `RepeatingFigureDetector` + `Figures` track + prompt wiring (§3.6) | G12 |
| 28.6 | Axis ordering + `Travel` shapes/reverse (§3.7) | G10 |
| 28.7 | **Verify** `BufferStyle`/`BufferTransform`/`LayerMethod` choice lists (§3.8 Step 1) | — |
| 28.8 | Carry + emit them; teach the Director (§3.8 Steps 2–4) | G11, G14 |
| 28.9 | Mirror sets + `RuleMirrorSetDivergence` (§3.9) | G6 |
| 28.10 | `ParamCurves` + `RuleParameterMonotony` (§3.5) | G5 |
| 28.11 | `GenerationTuning` settings object + UI surface (§4.2) + `On` into the EDM pool (§4.4) | — |
| 28.12 | `IToolCallingChatClient` + the seven retrieval tools (§3.10 Steps 1–2) | — |
| 28.13 | Progressive Director loop behind `DirectorMode` (§3.10 Step 3) | G7, G8, G9 |
| 28.14 | Full regeneration of both reference songs; record evidence | G1–G14 |

Steps 28.1–28.11 are deterministic engine work and deliver measurable improvement on their own.
28.12–28.13 are the LLM architecture change and can be deferred without blocking the rest — but G7,
G8 and G9 (an empty Tree, shallow layering, an unused whole-house group) are unlikely to be reached
by the deterministic work alone, because §1.7's root cause is a budget problem, not a mechanism
problem.

---

## 7. Risks and disclosed unknowns

1. **`BufferStyle`/`BufferTransform` values are not yet verified against the shipped xLights
   metadata.** §1.6's tables come from a file xLights itself wrote, so those specific values are
   real — but the *complete* acceptable set is not known, and writing an invalid one produces a
   sequence that may fail to load. This is why 28.7 is its own step and 28.8 cannot start before it.
2. **The meter detector may regress a song that is genuinely 4/4.** G13 must include re-analyzing at
   least three known-4/4 corpus songs and confirming the detected meter does not change.
3. **Subdivision can over-busy a quiet section.** The hysteresis (§3.3) and `FinestNoteResolution`
   cap (§4.2) are the guards; `RuleSectionDensityContrast` already measures quiet-vs-loud spread and
   will catch a flattened show.
4. **Progressive authoring multiplies cost** (§3.10). Mitigated by the cached prefix and a
   `MaxLookAuthoringCalls` ceiling, but it is a real increase and the user should see an estimate
   before a run.
5. **Mirror-set detection is name-and-pixel heuristic.** `Matrix`/`Matrix-2`/`Matrix3` is an easy
   case; a user who names their matrixes `Left Wall` and `Right Wall` will not be detected.
   `MirrorSets` should therefore be user-editable in the layout view, not purely inferred.
6. **Not addressed by this phase:** 4-stem separation — sketched as its own follow-on, not a step
   here, in §3.6a — plus submodel-level targeting, Faces/Text content authoring, and the `Duplicate`
   effect's 157 uses in the current output (worth a separate look — it does not appear in any of the
   three human references).

---

## 8. Appendix — reference-file inventory

| File | Role in this plan |
|---|---|
| `C:\xLights\09. Carol Of The Bells (Instrumental).xsq` | Human reference for the song we generate; source of §1.1, §1.2 |
| `C:\xLights\CarolOfTheBells\exports\sequence.xsq` | App output under test, generated 2026-11-15 |
| `C:\xLights\All I Want For Christmas Is You shortened.xsq` | Spatial-motion idiom (0–9 s, §1.5); 4-layer Tree kaleidoscope (57.5–92.5 s, §1.6); human density ceiling (8 starts/s, §1.3) |
| `C:\xLights\Chuck Berry - Run Rudolph  Run.xsq` | Deep multi-layer stacks (Tree ×14, Matrix ×12, §1.6); mirror-set equality; buffer-setting frequency tables |
