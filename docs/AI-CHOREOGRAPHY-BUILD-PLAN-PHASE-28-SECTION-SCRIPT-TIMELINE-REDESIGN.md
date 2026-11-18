# Phase 28 -- Section Script Timeline Redesign: giving per-section generation real timing, model, and emphasis control

## How to read this doc

This is written as a senior architect's handoff to a new engineer picking up the work. Every file
path and type name below is real and already in the codebase unless marked **NEW**. Scattered
through it are boxed notes in a second voice:

> **Choreographer's note:** a working lighting designer's reason *why* a given field/rule exists --
> not architecture for its own sake. If a design choice below has no Choreographer's note next to
> it, that's a signal to go find a real craft reason before building it, not just build it because
> it's technically possible.

---

## 0. The problem, as diagnosed this session (do not re-litigate -- build from this)

This phase exists because a deep investigation (this session, real `studio.log` data pulled and
read, not guessed) found a specific, structural ceiling on quality that no amount of better
prompting can fix, because it's a **schema** problem, not a **reasoning** problem:

1. **The per-section Choreographer call receives ~6,200 tokens of real, grounded music/layout data**
   (melodic contour, real vocal-note onset timestamps, real transient-hit timestamps, motif links,
   full Look library, full layout) **and returns one `LookId` string plus a filler list.** Confirmed
   from a real captured request/response pair in `studio.log`. The entire creative decision space is
   "which of 8-20 pre-written Looks," never "what should happen, where, and when."
2. **`Look.Intent` and every `Annotation` field are never read by the compiler.** Confirmed in
   `SectionScriptCompiler.cs:182-184,210-212` -- `Annotation` is copied through via a `with`
   expression, never branched on. The LLM's stated creative reasoning has zero causal effect on the
   compiled output.
3. **Real location data exists (`DisplayBounds` X/Y) but never reaches either prompt.**
   `LayoutSummary.ToPromptText()` only ever emits pixel-weight + resolution tier. `Look.Travel`
   internally sorts by position, but only when the Director declared `Travel` for an entire Look up
   front -- the per-section call can never reason about position itself.
4. **Real musical movement is collapsed to one summary tag per section.** `MelodicContourBriefBuilder`
   gives one contour shape + one peak beat for the whole section; `BandEnergyGroupRoleMapper` gives one
   dominant-band label for the whole section. The real underlying note-by-note / frame-by-frame data
   exists upstream and is thrown away before the prompt is built.
5. **`LeadInstrumentWindows` never reaches the Choreographer prompt at all** -- only checked
   post-compile by `LeadInstrumentProminenceComplianceChecker`, as a reactive repair.
6. **"Too few concurrent models" and "no real dim-before-bright contrast" are both already known,
   named problems with reactive-only fixes** (`ChoreographyLegalityValidator.RuleSectionModelBreadth`,
   `EmphasisContrastPlanner.ApplyContrastFallback`). Both exist because the *generation* contract has
   no way to ask for them directly -- they're patched in after compilation, mechanically, with no
   creative judgment behind the patch.
7. **Two concrete prompt contradictions were found in a real captured call** (occurrence-restraint vs.
   motif-escalation; steady-hold-melodic-guidance vs. dim-every-emphasis-instant guidance) with no
   stated precedence. These are a *symptom* of cramming too much decision-making into one flat
   `LookId` choice -- not something more prose can fix.

**The fix is not "better prompting." It's giving the per-section output somewhere to put a real
decision, and giving the compiler a reason to honor it.**

> **Choreographer's note:** think about how a real lighting designer actually works a song. They
> don't pick one "scene" for an entire 8-bar verse and walk away. They watch/listen through the
> section and place specific moments: "the bass hits here, so the foundation glows here"; "the vocal
> climbs here, so the sweep moves left-to-right ending exactly on the peak note"; "right before that
> hit, kill the brightness for half a beat so the hit actually reads." That's a sequence of *cues*,
> each tied to a real moment in the music, each naming a real prop, each with a real intensity. A
# single Look ID can never represent that. The redesign below exists to let the model finally speak
in that vocabulary.

---

## 1. Goals and non-goals

**Goals**
- The per-section call can name specific real props/groups, specific real moments (anchored to real
  timing marks, never invented timestamps), specific effects, and a coarse but enforceable
  brightness tier, for more than one thing happening at once.
- The LLM's creative decision (which cue, why) has a direct, traceable path into what actually
  compiles -- no more silent discard of `Intent`/`Annotation`.
- Dim-before-bright contrast and multi-model concurrency become things the schema *asks for*, not
  things a later mechanical pass patches in after the fact.
- A "handoff"/"travel" primitive: motion or emphasis that explicitly moves from one named prop/group
  to another, tied to real successive timing marks (not just a whole-Look `Travel` declared once for
  the entire song).
- Backward-compatible, additive rollout -- the existing Look-selection path keeps working at every
  step; nothing is ripped out in one big-bang change.

**Non-goals (explicitly out of scope for this phase)**
- Replacing the Show Bible / Look library concept. Looks still matter: they still provide the
  palette, energy ceiling, motion style, and a default/fallback composition. This phase adds a
  **sparse, time-anchored override layer on top of a Look**, it does not replace Looks with raw
  effect lists.
- Full per-frame/per-pixel control. The output stays coarse (cue-level, not keyframe-level) --
  matching the "reasoning tax" discipline already established for this system (`SectionScript`'s own
  doc comment).
- Solving prompt-instruction precedence/contradiction in prose. That is Phase 29's job (see "Out of
  scope" below) -- this phase removes the *need* for many of those contradictions by giving the model
  room to express "both," e.g. restrained AND motif-echoing, as different cues at different moments,
  rather than one Look that must pick a single global tone.

---

## 2. The new output contract: `SectionTimeline`

**NEW FILE**: `src/XlightsAI.Studio.Choreography/SectionTimeline.cs`

```csharp
namespace XlightsAI.Studio.Choreography;

/// <summary>
/// Which real, already-named timing mark a SectionCue is anchored to -- NEVER a free-floating
/// invented timestamp. The Choreographer picks from a concrete enumerated list the prompt gives it
/// (see SectionScriptPromptBuilder's new "Real timing marks" block); SectionTimelineValidator rejects
/// any AnchorId that doesn't match one of the marks actually offered for this section.
/// </summary>
public static class AnchorKind
{
    public const string Beat = "Beat";
    public const string Bar = "Bar";
    public const string Transient = "Transient";
    public const string VocalNote = "VocalNote";
    public const string LeadInstrumentWindowStart = "LeadInstrumentWindowStart";
    public const string PhraseStart = "PhraseStart";
    public const string SectionStart = "SectionStart"; // the one allowed "no specific mark needed" anchor
}

/// <summary>
/// Coarse, enforceable brightness/density tier -- NOT a raw float. Four tiers only, matching the
/// four tiers EmphasisContrastPlanner already reasons in internally, so this finally gives the LLM
/// the SAME vocabulary the mechanical contrast-fallback pass uses, instead of two disconnected
/// brightness models existing side by side.
/// </summary>
public static class IntensityTier
{
    public const string Off = "Off";
    public const string Dim = "Dim";
    public const string Bright = "Bright";
    public const string Full = "Full";
}

/// <summary>
/// One real, timed, model-level decision. Replaces "pick one LookId for the whole section" with
/// "here is what happens, where, and when" -- a sparse list, not a keyframe track: a prop with no
/// cue simply continues whatever its last cue declared (or the base Look's own default if it has no
/// cue at all).
/// </summary>
/// <param name="AnchorId">
/// The id of a real timing mark offered in this section's own prompt (e.g. "transient-2",
/// "vocalnote-5", "beat-7") -- resolved server-side to a real tick value via
/// SectionTimelineCompiler.ResolveAnchor. Never a value the Choreographer invented.
/// </param>
/// <param name="Props">A real prop/group reference, same convention as LookLayer.Props.</param>
/// <param name="Effect">A real registered effect name, validated the same way LookLayer.Effect is.</param>
/// <param name="Intensity">One of IntensityTier -- the enforceable brightness/density step.</param>
/// <param name="Role">subject|support|secondary|foundation -- same vocabulary as LookLayer.Role.</param>
/// <param name="HoldUntilAnchorId">
/// Optional -- which later real anchor this cue holds through before the NEXT cue on the same
/// Props takes over. Omitted means "until superseded by the next cue naming this same Props, or
/// section end."
/// </param>
/// <param name="HandoffFromProps">
/// Optional -- names the prop/group this cue's motion/emphasis is explicitly "handing off" from,
/// e.g. a sound/sweep moving from "group:Arches Left" to "group:Arches Right". When set,
/// SectionTimelineCompiler builds a real cross-fade/stagger between the two real props timed to
/// this cue's own anchor, rather than two independent, disconnected effects.
/// </param>
/// <param name="Annotation">
/// Grounded, one-sentence why -- same grounding discipline as LookLayer.Annotation, but THIS ONE IS
/// CONSUMED: see "3. Closing the intent loop" below. Never a guess; omit rather than invent.
/// </param>
public sealed record SectionCue(
    string AnchorId,
    string Props,
    string Effect,
    string Intensity,
    string Role,
    string? HoldUntilAnchorId = null,
    string? HandoffFromProps = null,
    string? Annotation = null);

/// <summary>
/// Build-plan Phase 28: the per-section output, additive alongside the existing SectionScript.
/// LookId is kept -- it still anchors palette/energy-ceiling/motion-style discipline and supplies the
/// DEFAULT composition for any prop/moment this timeline doesn't explicitly cue. Cues is the new,
/// sparse, time-anchored override layer.
/// </summary>
public sealed record SectionTimeline(
    Guid SectionId,
    string LookId,
    IReadOnlyList<SectionCue>? Cues = null,
    IReadOnlyList<string>? FillerEffects = null,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? FillerEffectsByRole = null)
{
    public IReadOnlyList<SectionCue> CuesOrEmpty => Cues ?? [];
}
```

> **Choreographer's note on `HandoffFromProps`:** this is the literal answer to "move from one
> model/group to another tied to the music." A real designer doing a left-to-right sweep across
> three arches doesn't think "Travel order=LeftToRight" as an abstract Look property decided months
> before the song was heard -- they think "the vocal climbs through these three notes, so the light
> walks from Arches Left, to the Matrix, to Arches Right, landing exactly on each note." That's
> *per-section*, built from *this section's real pitch movement*, not a property fixed once on a Look
> for the whole song. `HandoffFromProps` plus a real anchor is what lets the Choreographer say that,
> while `Look.Travel` (unchanged, still useful) stays available for a Look-wide, declared-up-front
> sweep style.

> **Choreographer's note on `IntensityTier`:** four tiers, not a float, on purpose. A real designer
> doesn't think in `0.73` brightness -- they think "dark," "a little something," "there," "all the
> way." Four tiers is enough to make the dim-before-bright contrast rule *mechanically checkable*
> (did Intensity step up across this cue vs. the previous one on this Props?) instead of advisory
> prose the model has to remember to apply "every single time."

---

## 3. Closing the intent loop -- making `Annotation` and cue sequencing causally real

This is the part that actually fixes "another piece of code breaks it up and has no intent."

**Rule, enforced in `SectionTimelineValidator` (NEW FILE,
`src/XlightsAI.Studio.Choreography/SectionTimelineValidator.cs`), same validate-and-retry tier as
`SectionScriptValidator`:**

- **Contrast rule, made literal, not advisory:** for any two consecutive cues on the SAME `Props`,
  if the later cue's `Intensity` is `Bright` or `Full`, the immediately preceding cue on that same
  `Props` (by anchor order) must be `Off` or `Dim` -- OR the later cue must itself declare
  `HandoffFromProps` pointing at a DIFFERENT prop that WAS already bright (a deliberate handoff, not
  a contrast failure). A violation here is a retry-worthy validation error with the exact two cues
  named, same discipline as `look-peak-layer-floor`.
- **Concurrency rule, made literal, not a post-hoc patch:** `RuleSectionModelBreadth`'s existing
  floor (`MinDistinctModelsPerSection = 3`, `MinDistinctModelsPerDenseSection = 5`) gets a NEW
  pre-compile twin in `SectionTimelineValidator`: count distinct `Props` values across `Cues` plus
  the base Look's own layers: if that union is below the section-kind's real floor, reject and retry
  BEFORE compiling, with the exact count and floor named -- catching this at generation time, not
  discovering it after a full compile+legality pass.

**Where `Annotation` becomes real, not decorative:** `SectionTimelineCompiler` (replacing
`LookCompiler`'s silent passthrough for any `LookLayer` a cue overrides) takes a cue's `Annotation`
and writes it into a new `EffectInstance.SourceAnnotation` field (**NEW**, additive, nullable, on the
existing `EffectInstance` record in `ChoreographyIR`). This alone doesn't change compiled timing --
but it means:
1. A human (or a later automated audit) can trace every compiled effect back to the stated reason it
   exists, closing the "has no intent" gap you identified.
2. `ChoreographyLegalityValidator`'s existing grounding checks (the ones that already compare an
   `Annotation` against `EffectVisualCharacterCatalog`) can now run against the COMPILED result, not
   just the pre-compile Look/SectionScript JSON -- catching a case where the stated intent and the
   actually-compiled effect have drifted apart (e.g. `LookCompiler`'s vocal-hold bias silently
   overrode `Duty`/`Timescale` after the annotation was written).

> **Choreographer's note:** this is the equivalent of a real designer leaving a comment in their own
> cue sheet -- "accent syncs to the word 'rise'" -- that a tech director can actually check against
> what's on the board, instead of a note that gets thrown away the moment the show file is saved.

---

## 4. What has to change on the INPUT side

All four gaps from the diagnosis (§0) get fixed here -- this is what lets the LLM "understand
location of models and music with the information you give it," per your instruction, rather than
inventing it.

### 4a. Real timing marks, enumerated (not invented)

**File**: `src/XlightsAI.Studio.Director/SectionScriptPromptBuilder.cs` -- new
`AppendTimingMarks(StringBuilder sb, SectionMusicContext context, ...)`, called from the (renamed)
`BuildVolatileSuffix`.

For THIS section only, enumerate every real mark with a stable, short id and its real timestamp:

```
## Real timing marks in this section (use these exact AnchorId values -- never invent a timestamp)
- beat-1 @ 0:49.00
- beat-2 @ 0:49.50
- beat-3 @ 0:50.00
- bar-1 @ 0:49.00
- transient-1 @ 0:50.10 (strength 0.82)
- vocalnote-1 @ 0:50.00
- vocalnote-2 @ 0:51.00
- phrase-1-start @ 0:49.00
```

This is a direct, mechanical re-render of data the pipeline ALREADY computes and ALREADY passes
through as prose summaries (`BeatGrid`, `TransientHit.TimeTicks`, `NoteEvent.StartTicks`,
`PhraseCandidate.StartBarIndex`) -- no new DSP, just a new rendering of existing real data as a
lookup table instead of a narrative sentence.

### 4b. Location-aware layout block

**File**: `src/XlightsAI.Studio.Director/LayoutSummary.cs` -- extend `LayoutSummaryEntry` with
optional `RelativePosition` (**NEW**, nullable, e.g. `"left"`/`"center-left"`/`"center"`/
`"center-right"`/`"right"`, bucketed from the real `DisplayBounds.X` already present on
`DisplayModel`/group-member positions -- the same data `Look.Travel`'s internal sort already reads,
just finally exposed as text). `LayoutSummary.Build` buckets every model/group with known bounds into
5 horizontal buckets relative to the layout's own total width; a model/group with no known bounds
gets `RelativePosition: null` and is rendered without a position clause, never a guessed one.

`ToPromptText()` output changes from:
```
- Matrix: 1488px, high-resolution
```
to:
```
- Matrix: 1488px, high-resolution, center
- Arches Left: 400px, high-resolution, left
- Arches Right: 400px, high-resolution, right
```

> **Choreographer's note:** 5 buckets, not real coordinates. A designer doesn't think "x=420," they
> think "stage left," "center," "stage right." Buckets are also easier to validate against (does a
> `HandoffFromProps` chain actually move monotonically left-to-right or right-to-left, or does it
> jump around incoherently?) than raw pixel math would be.

### 4c. Section-local musical movement as a short timeline, not one tag

**File**: `src/XlightsAI.Studio.Structure/MelodicContourBriefBuilder.cs` -- new
`ComputeSectionCurve` (additive, alongside the existing `ComputeSectionBriefs`, which stays for the
stable-prefix contour-shape prose) returning a short ordered list of (beat offset, direction,
magnitude) samples WITHIN the section, capped at a small count (e.g. one sample per real note onset,
already-known data, never densified).

**File**: `src/XlightsAI.Studio.Director/BandEnergyGroupRoleMapper.cs` -- same treatment: a new
`ComputeSectionCurve` returning 2-4 (time, dominant-band) samples across the section instead of one
static label, reusing the exact same per-frame `BandEnergyEnvelope` data already read for the
single-label version.

`SectionScriptPromptBuilder`'s volatile suffix renders these as a few extra lines, anchored to the
SAME AnchorIds from §4a so "the melody peaks at vocalnote-2" is something the model can read and then
directly reference in a cue's `AnchorId`.

### 4d. `LeadInstrumentWindows` reaches the Choreographer, not just the post-compile checker

**File**: `src/XlightsAI.Studio.Director/SectionScriptPromptBuilder.cs` -- new
`AppendLeadInstrumentWindowLine`, mirroring the existing `AppendTransientHitLine`/
`AppendVocalNoteEmphasisLine` pattern exactly: render only the windows whose real time overlaps this
section, with a new anchor kind `LeadInstrumentWindowStart` added to §4a's enumerated marks so a cue
can target it directly instead of the model never knowing it exists until a repair pass fires.

---

## 5. Compiler changes

**NEW FILE**: `src/XlightsAI.Studio.Choreography/SectionTimelineCompiler.cs`

Responsibilities, composed from pieces that already exist -- this is assembly, not new DSP:

1. **`ResolveAnchor(string anchorId, SectionMusicContext context) -> long ticks`** -- a lookup against
   the exact same `TimingTrack`/`BeatGrid`/`TransientHit`/`NoteEvent`/`LeadInstrumentWindow` data
   §4a already rendered into the prompt, keyed by the same ids. An `AnchorId` that doesn't match any
   offered mark is a hard compile-time rejection (`ChoreographyCompilationException`), never a
   best-effort guess -- this is the mechanism that makes "never invent a timestamp" actually
   enforced, not just requested.
2. **Base layer, unchanged**: start from `LookCompiler.Compile(look, section, layout, settings)`
   exactly as today -- this is the fallback for any Props/time window no cue touches.
3. **Cue overlay**: for each `SectionCue`, resolve `Props` against `DisplayGraph` (reusing
   `LookCompiler.TryResolveModels`), resolve `Effect` against the recipe catalog (reusing the same
   `RecipeCatalog.TryGet` + `EffectSettingsValidator` path `ShowBibleValidator`/`LookCompiler` already
   use), map `Intensity` to a real `Duty`/blend adjustment via a small, explicit, named lookup table
   (**NEW**, `IntensityTierMapping.cs` -- e.g. `Off -> Duty 0, Dim -> Duty 0.15, Bright -> Duty 0.6,
   Full -> Duty 0.95`, tunable constants, not invented per-call), and emits/overrides an
   `EffectInstance` spanning `[ResolveAnchor(AnchorId), ResolveAnchor(HoldUntilAnchorId) ?? next cue on
   same Props ?? section end]`.
4. **Handoff cues**: when `HandoffFromProps` is set, build a real two-prop stagger (reusing
   `PropDelayResolver`'s existing stagger-offset math) between the FROM and TO props, anchored to this
   cue's own resolved tick -- a genuine timed handoff, not two independent effects that happen to be
   adjacent in the output list.
5. Runs `SectionTimelineValidator`'s contrast/concurrency rules (§3) BEFORE touching
   `AlwaysMovingGapSplitter`/`EmphasisContrastPlanner` -- so a timeline that already satisfies real
   contrast/concurrency never even reaches those mechanical fallback passes; they remain only a
   safety net for whatever a timeline's sparse cues don't cover, same role they play today.

`ChoreographyGenerationPipeline.CompileSectionScripts` gets a small branch: if a returned
`SectionTimeline` has any `Cues`, call `SectionTimelineCompiler`; otherwise fall back to today's
`LookCompiler` path unchanged. This is the backward-compatibility seam -- see §6.

---

## 6. Migration plan (additive, three sub-phases, no big-bang)

**28a -- Schema + validator + compiler, OFF by default.** Ship `SectionTimeline`,
`SectionTimelineValidator`, `SectionTimelineCompiler`, §4's new prompt sections, all gated behind a
new `ChoreographyGenerationSettings.EnableSectionTimelines` flag (default `false`, same
disclosed-default-off convention `EnableQualityPass` already uses). Every existing test, every
existing call site, keeps compiling and behaving identically. `SectionScriptPromptBuilder`'s output
contract line changes to document BOTH shapes (`SectionScript`'s old flat shape stays valid; a
response may ALSO include `cues`), and `SectionScriptBatchGenerator`'s deserialization target becomes
`SectionTimeline` (a strict superset -- an old-shape response with no `cues` key deserializes
unchanged, since `Cues` is nullable/optional).

**28b -- Real-run comparison.** Regenerate Carol of the Bells (and at least one other real corpus
song) with the flag on vs. off, same discipline as every other phase's own real-run verification in
this build plan. Confirm via the compiled-effect target tally (same diagnostic table format already
used in `docs/sequence-generation-flow-carol-of-the-bells.md`) that: (a) `RuleSectionModelBreadth`
violations drop, (b) a manual read of 3-4 real compiled cues shows the stated `Annotation` actually
matches what got compiled, (c) latency/cost per section (today: ~6,200 input / ~300 output tokens,
3-5s) stays in a similar order of magnitude -- cues add prompt length on both sides, this must be
measured, not assumed acceptable.

**28b.1 -- Real finding: the model puts every cue on one Prop, never spreading breadth. Prerequisite
fix before re-attempting 28c.** A real Carol of the Bells 28b run, diagnosed with temporary verbose
logging added to `SectionScriptGenerator.GenerateAsync` (full violation text + raw response JSON per
attempt, not a count), found that of 76 real logged Choreographer attempts, 67 were real Azure
`HTTP 429` rate-limit transport rejections (this session's own repeated real runs exhausting a shared
quota -- infrastructure noise, unrelated to this phase's own design) and only 3 were genuine validation
rejections. **All 3 genuine rejections were the identical rule, `section-timeline-concurrency`**, e.g.:

```
[section-timeline-concurrency] SectionTimeline for section '9d36ae1e-...' names only 2 distinct
Props across its Cues and base Look 'verse-cutback' -- expected at least 5 for this section kind.
```
with the model's own real raw cues for that response:
```json
"cues":[
  {"anchorId":"beat-3","props":"Matrix-2","effect":"Color Wash","intensity":"Off", ...},
  {"anchorId":"vocalnote-2","props":"Matrix-2","effect":"Color Wash","intensity":"Bright", ...},
  {"anchorId":"beat-10","props":"Matrix-2","effect":"Color Wash","intensity":"Off", ...},
  {"anchorId":"vocalnote-4","props":"Matrix-2","effect":"Color Wash","intensity":"Bright", ...}
]
```
Every cue targets `Matrix-2` -- a correct, valid, real dim-before-bright-before-dim-before-bright
pattern (right anchors, right `holdUntilAnchorId` chaining, right intensity ordering) on exactly ONE
prop. A second real example (Look `bridge-pressure`) shows the identical shape: 2 cues, both on
`group:Gutter`, a clean dim/bright pair. Zero `section-timeline-contrast` violations and zero
unknown-`AnchorId` violations were observed anywhere in this real run -- the model reliably gets the
cue MECHANICS right (anchors, hold-chains, intensity ordering); it just does not, on its own, spread
cues (or lean on the base Look's own other layers) across enough distinct real Props to also clear
`RuleSectionModelBreadth`'s own floor for that section's kind. One of the three failures self-corrected
on its very next attempt (switched Look entirely, from 1 prop/2 cues to 3 props/4 cues) once handed the
real violation text -- evidence the model CAN satisfy both rules at once when told to, it just does not
default to it.

**Diagnosis, not speculation:** this reads as a prompt-emphasis gap, not a schema-complexity problem.
§3's own contrast guidance is the single most heavily emphasized rule in the whole volatile suffix
(worked example, repeated "every single time, not just once" language); nothing in the current prompt
states the distinct-Props/breadth floor with anything like that same force -- the model only ever
learns about it after the fact, as an opaque validator error naming a number it was never shown in
advance. **Fix (prompt-guidance layer only, per this build plan's own §9 scope boundary -- this is NOT
the separate, deliberately out-of-scope "Bible-authoring coverage" gap):** render this section's own
real `ChoreographyLegalityValidator.MinDistinctModelsForSectionKind` floor directly in the volatile
suffix, with the same concrete, worked-example weight the contrast guidance already gets, so the model
sees the real number it must clear BEFORE generating cues, not only after failing to. This is a
prerequisite to re-attempting 28b's own real-run comparison (and therefore to 28c) -- the 28b numbers
already measured are not a clean "cues vs. no cues" read until this specific, now-diagnosed gap is
closed and re-measured.

**28c -- Flip the default once 28b is clean.** Flag flips to `true` by default; the old flat-`LookId`-
only path remains permanently available as the fallback for any section whose first-round `Cues`
response fails validation (reusing the exact "fails validation -> plain per-section retry" structure
`SectionScriptBatchGenerator` already has) -- never a hard dependency, since a model that can't
produce valid cues should degrade to the old behavior, not fail the whole section.

---

## 7. File-by-file task list

| File | Change |
|---|---|
| `src/XlightsAI.Studio.Choreography/SectionTimeline.cs` | **NEW** -- `AnchorKind`, `IntensityTier`, `SectionCue`, `SectionTimeline` |
| `src/XlightsAI.Studio.Choreography/SectionTimelineValidator.cs` | **NEW** -- contrast rule, concurrency rule, anchor-exists rule |
| `src/XlightsAI.Studio.Choreography/SectionTimelineCompiler.cs` | **NEW** -- anchor resolution, cue overlay on `LookCompiler`'s base output, handoff staggering |
| `src/XlightsAI.Studio.Choreography/IntensityTierMapping.cs` | **NEW** -- named Duty/blend constants per tier |
| `src/XlightsAI.Studio.Choreography/ChoreographyIR.cs` (or wherever `EffectInstance` lives) | add nullable `SourceAnnotation` |
| `src/XlightsAI.Studio.Director/LayoutSummary.cs` | add `RelativePosition` bucketing from real `DisplayBounds.X` |
| `src/XlightsAI.Studio.Director/SectionScriptPromptBuilder.cs` | new `AppendTimingMarks`, `AppendLeadInstrumentWindowLine`; render section-local contour/band curves; update required-JSON-shape block to document `cues` |
| `src/XlightsAI.Studio.Structure/MelodicContourBriefBuilder.cs` | add `ComputeSectionCurve` (additive) |
| `src/XlightsAI.Studio.Director/BandEnergyGroupRoleMapper.cs` | add `ComputeSectionCurve` (additive) |
| `src/XlightsAI.Studio.Director/SectionScriptBatchGenerator.cs` / `SectionScriptGenerator.cs` | deserialize target becomes `SectionTimeline`; validate via `SectionTimelineValidator` when `Cues` present, else today's `SectionScriptValidator` |
| `src/XlightsAI.Studio.Director/ChoreographyGenerationPipeline.cs` | branch in `CompileSectionScripts`: `Cues` present -> `SectionTimelineCompiler`, else -> today's `LookCompiler` path; new `EnableSectionTimelines` setting |
| `src/XlightsAI.Studio.Director/ChoreographyGenerationSettings.cs` | add `EnableSectionTimelines` (default `false`) |

---

## 8. Testing / verification

1. `dotnet build`/`dotnet test` every touched project -- all green, every existing test unchanged in
   behavior (the flag defaults off).
2. **New `SectionTimelineValidatorTests.cs`**: a cue sequence that steps `Dim -> Bright` on the same
   Props passes; `Bright -> Bright` with no handoff fails with the exact two cues named; a timeline
   whose union of Props is below the section-kind's floor fails with the real count/floor named; an
   `AnchorId` not present in the offered marks fails.
3. **New `SectionTimelineCompilerTests.cs`**: a cue referencing a real transient anchor compiles to an
   `EffectInstance` whose tick span matches that transient's real `TimeTicks`; a `HandoffFromProps`
   cue compiles to two staggered instances, not two independent ones at the same tick.
4. **Real-run manual check (28b)**: pull 3-4 compiled cues from a real Carol of the Bells regeneration
   and confirm by hand that the compiled effect's prop/time/intensity matches the cue's own stated
   `Annotation` -- this is the test that actually answers "does intent survive to compilation now."
5. Latency/cost regression check against the real numbers already measured this session
   (~6,200 input / ~300 output tokens, 3.4-4.7s per section) -- flag if either grows by more than a
   small, named threshold.

---

## 9. Out of scope this phase (deliberately deferred)

- **Prompt-instruction contradiction resolution** (restraint-vs-escalate, steady-hold-vs-dim-every-
  instant) -- this phase reduces the PRESSURE toward those contradictions (cues let different moments
  within one section express different tones), but an explicit stated precedence rule between
  occurrence-restraint and motif-escalation guidance is a separate, smaller follow-up (call it Phase
  29) once real 28b data shows whether it's still needed.
- **Rewriting the Show Bible to require Look coverage by model/group type.** Flagged in this session
  as a real, separate gap (`RuleModelCoverage`/`RuleModelOnlyGroupCovered`/`RuleHeroLookNeverUsed` are
  all reactive patches for the same underlying issue: the Bible has no structural requirement that
  every major group gets at least one Look built around it). Worth its own phase; this phase's cue
  mechanism can OPTIONALLY help here later (a cue naming an uncovered group directly, even without a
  dedicated Look) but fixing Bible-authoring itself is not this phase's job.
- **Per-frame/keyframe-level control.** Deliberately coarse, cue-level, matching the existing
  reasoning-tax discipline.
- **Removing `Look.Travel`/`PropDelays`.** They stay; `HandoffFromProps` is a per-section,
  per-moment complement to them, not a replacement.
