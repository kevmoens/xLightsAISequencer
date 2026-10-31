# Build Plan: Phase 10 — Full Signal Utilization and Choreographer Craft

**Status:** proposed, not yet implemented. Written in response to a real-generation audit (2026-10-28)
that confirmed significant analysis output is computed and then discarded before it ever reaches the
Director (`DirectorPromptBuilder.cs`) or Choreographer (`SectionScriptPromptBuilder.cs`) prompts, or the
compiled IR (`LookCompiler.cs`/`ChoreographyEngine.cs`). This phase closes that gap and, separately,
gives the Director/Choreographer prompts an explicit *craft brief* — concrete, named rules for how each
musical signal should turn into a visual choice, not just raw numbers to interpret unguided.

This slots in after Phase 9 in `AI-CHOREOGRAPHY-BUILD-PLAN.md`'s numbering, but is independent of it —
none of Phase 9's items block this, and this does not block Phase 9.

---

## 1. Why this phase exists

Every project in this codebase that "breaks a song down" — `Rhythm`, `Melody`, `Structure`, `Stems`,
`Composition`, `Choreography`, `Knowledge` — computes real, often sophisticated signals. But a forward
trace from each computed type through `AudioAnalysisRunner`/`StructureAnalysisRunner` →
`AiChoreographyWholeSongGenerator` → `MusicModel` → the two prompts shows most of that work never
survives the trip. Two different failure patterns, both real:

- **Dead wiring**: downstream code already exists (prompt text, compiler logic, timing-track builders)
  but the analyzer that would feed it real data is never invoked, and a `null` is hardcoded at the one
  real call site instead. The feature *looks* built and tested, but is inert in every real run.
- **Truncation**: an analyzer's full output is only used as an intermediate feature for a different,
  coarser signal, and then thrown away — so only the coarse conclusion (a section label, a phrase
  boundary) survives, never the richer signal that produced it.

There is also a **duplication** problem: `XlightsAI.Studio.Composition` is a complete second
choreography system (musical-role assignment, visual budgets, transition planning, its own compiler)
that the real `AiChoreographyWholeSongGenerator` path does not use at all. It is not wrong, just
unreachable — a second implementation of "which prop does what, when" sitting next to the one that is
actually live, which invites future confusion and wasted effort if anyone edits it expecting it to
matter.

### 1a. Confirmed findings (2026-10-28 audit)

| Signal | Computed by | Current fate |
|---|---|---|
| `VocalActivityEnvelope` | `Stems.VocalActivityAnalyzer` | **Dead wiring.** Analyzer never instantiated anywhere in `src`; `AiChoreographyWholeSongGenerator.cs` hardcodes `VocalActivity: null`. Both prompts and `LookCompiler`'s vocal-hold-bias already have real code paths for this — all inert. |
| `BandEnergyEnvelope` (bass/mid/high split) | `Rhythm.BandEnergyAnalyzer` | **Dead wiring.** Same pattern: never instantiated; `BandEnergy: null` hardcoded. `ChoreographyEngine` already has code to build a `"Percussion"` timing track from it — inert. |
| `OnsetAnalysis` | `Rhythm.OnsetDetector` | **Truncated.** Feeds `TempoDetector`/`BeatGridBuilder` only, then discarded; never reaches `MusicModel`. |
| Pitch contour / `NoteEvent` / `MelodicGesture` | `Melody` (via `Pipeline.PitchFrameNoteSegmenter`, `MelodicContourClassifier`) | **Truncated.** Used as a bar-fingerprint feature for section/phrase detection, and for the Piano Roll UI. Never reaches either prompt — no "melody rises here" signal exists today. |
| `MotifClusteringAnalysis` (repeated-phrase identity) | `Structure.MotifClusteringAnalysis` | **Truncated.** Computed, forwarded only to the UI's Structure Inspector. Never reaches the Choreographer, even though it answers exactly the question the Choreographer's own repeat-escalation logic needs ("is this the same musical idea as an earlier section?"). |
| Per-section phrase boundaries/count | `Structure.PhraseSegmentationAnalysis` | **Truncated to one whole-song statistic.** `DirectorPromptBuilder` reads only `Phrases[0].Bars` as a single global phrase-length guess. A section is always one monolithic Look assignment; no per-phrase variation is possible today. |
| `VisemeMark` (lyric phoneme/word timing) | `Stems` (phoneme alignment) | **Wired to the wrong place.** `ChoreographyEngine.Input.VisemeMarks` has a real consumer (`LyricTimingTrackBuilder`), but `ChoreographyGenerationPipeline` never passes them in — parameter left at its `null` default. |
| `CorpusManifest` → `CraftRule`s / `ExemplarIndex` | `Knowledge`/`Pipeline.CorpusMiningRunner` | **Never invoked.** No menu command or startup call anywhere in `App`/`Shell`. `AiChoreographyWholeSongGenerator.cs` hardcodes `Promoted: []` and an empty `ExemplarIndex`. The Director prompt's "Verified craft rules" and "Retrieved exemplars" sections are permanent placeholder text in production. |
| `XlightsAI.Studio.Composition` (entire project) | — | **Unreachable duplicate.** Backs the old `WholeSongGenerator`, which is constructed in `Program.cs` but never assigned to `App.WholeSongGenerator` — only the unrelated `AiChoreographyWholeSongGenerator` is. |

---

## 2. The choreographer's brief: how each signal should actually drive a visual choice

This is the creative core of this phase — not just "wire the data through," but specific, opinionated
rules for what each signal should *mean* visually, written the way a human lightshow choreographer would
reason about a song. These rules become new Director/Choreographer prompt guidance once their data is
wired in (Section 3), and they are the acceptance bar for whether the wiring actually helped.

### 2a. Vocal activity → focus and restraint, not just hold-vs-chase

The existing hold-vs-chase gating (steady during vocals, frequency-reactive during instrumental breaks)
is right but incomplete. A real choreographer also uses vocal activity to decide **where the eye should
go**:

- During a vocal-active phrase, pick one "lead" prop or small prop cluster (the tallest/most central —
  typically `Tree` or a `Faces`-capable model if present) to carry the main visual interest at a readable,
  un-busy cadence, while supporting groups (`Outline`, `Gutter`) recede to a quiet, steady role. This is
  the visual equivalent of a singer having a spotlight while the band plays underneath.
- During an instrumental break/solo, that restriction lifts — spread motion and brightness across more
  groups at once, since there is no lyric/vocal line competing for attention.
- A rapid vocal on/off alternation (call-and-response, a stutter hook) is itself a rhythmic signal: treat
  it like a fast `Timescale` change, not like a slow section-level mode switch — a pop/flicker on each
  vocal-active onset reads as "the light show is listening," which is the whole point of this project.

### 2b. Band energy (bass/mid/highs) → which physical layer of the display reacts to which part of the mix

This is the single most direct way to make a show feel "mixed" instead of "flat" — give each frequency
band a physical/spatial home, consistently, for the whole song:

- **Bass** → foundation/structural groups: `Outline`, `Roof`/`Roof and uprights`, `Gutter`. These are
  usually the biggest, most "whole house" groups — bass hits read best as a full-canvas pulse/flash
  there, since bass is felt as impact, not detail.
- **Mids (melody/harmony)** → the carrying prop(s): `Tree`, `Arches`, `Matrixes`. This is where the
  Look's subject layer (per existing layering guidance) should track the melodic line's energy — a
  rising mid-band energy phrase should brighten or accelerate this layer, not the bass-driven groups.
- **Highs (hats, sparkle, air)** → accent/detail groups: `MiniTrees`, icicle/strand models, any small
  high-pixel-density accessory. These are the natural home for `Twinkle`/`Sparkles`-family overlay
  layers — a hi-hat pattern should visibly live here, distinct from the bass pulse and the mid-band
  subject layer.
- This three-way split should be treated as close to a *standing rule* for a section, not re-decided
  every Look — consistency across the song is what makes the mapping readable to a viewer, the same way
  a real DJ/VJ assigns consistent visual roles to kick/bass/lead.

### 2c. Melodic contour → direction and shape of motion, not just "more energy = more effects"

Raw pitch/contour data should answer a question current guidance cannot: *which way should something
move?*

- A rising melodic phrase (ascending contour) → prefer upward/outward-reading motion or a brightness
  ramp that climbs across the phrase (a value-curve fade building toward the phrase's peak note), landing
  the peak exactly on the phrase's highest note — this is "built to the lyric," not just "built to the
  beat."
- A falling/descending phrase → the inverse: a settling, cascading, or dimming motion, used deliberately
  to mark a release after a held high note, a verse ending, or a pre-chorus breath.
- High note density (fast runs, melismatic vocal lines) → favor a faster `Timescale`/shorter duty cycle
  on the subject layer for that phrase specifically, independent of the section's overall energy ceiling
  — a florid vocal run deserves visibly busier motion even inside an otherwise calm Verse.
- A long held note / sparse melodic density → the opposite: this is exactly the moment for the "hold is a
  steady state, never a dead one" rule already in the prompt — a long low-duty-cycle animated hold, not a
  flurry of changes, because the melody itself isn't moving.

### 2d. Motif repetition → a visual leitmotif, with escalation

When the same melodic/phrase motif recurs (chorus repeats, a recurring hook), the show should reuse a
*recognizable visual signature* for it — not necessarily the identical Look, but a shared, identifiable
element (the same accent palette slot, the same subject prop, the same signature effect) every time that
motif returns, the same way a film score reuses a leitmotif for a character or theme. Each repeat should
then escalate (more layers, higher duty, an added group) rather than render identically — this directly
strengthens the "repeat N of M" occurrence-index logic the Choreographer prompt already has, by telling it
*which* earlier section this one is musically the same idea as, rather than only that it shares a
`SectionKind`.

### 2e. Phrase boundaries within a section → sub-section staging, not one monolithic Look

A sixteen-bar Chorus is not one musical idea four times longer than a four-bar phrase — it is four
phrases. A real choreographer stages *within* a section: the first phrase establishes the Look, a middle
phrase introduces a variation (an added layer, a palette-accent swap), and the final phrase either
escalates into the next section's pop or recedes to set one up. The Choreographer should be able to
declare up to a small number of phrase-scoped variations on top of a section's base Look, not just one
Look for the section's entire duration.

### 2f. Corpus-mined craft rules/exemplars → grounding, not decoration

Once corpus mining actually runs, its job is to keep the AI's choices anchored to what real, human-made
sequences actually do for a given section kind/energy level — e.g., "a Chorus at this energy ceiling, in
real corpus sequences, uses N or more active groups" or "this effect is rarely combined with that one in
a held layer." These should be read as soft defaults/sanity checks the Director can deviate from with
reason, not hard constraints — the point is reducing generic/implausible choices, not removing creative
range.

---

## 3. Proposed technical changes

### 3a. Close the dead-wiring gaps (highest priority, lowest risk)

1. Instantiate `VocalActivityAnalyzer` and `BandEnergyAnalyzer` in `AudioAnalysisRunner`, alongside the
   existing tempo/beat/pitch analyzers, each with its own cache store (mirroring the existing
   `JsonOnsetAnalysisStore`/`JsonTempoAnalysisStore` pattern).
2. Add `VocalActivity`/`BandEnergy` fields to `AudioAnalysisResult`.
3. Stop hardcoding `VocalActivity: null, BandEnergy: null` in `AiChoreographyWholeSongGenerator` — pass
   the real values through to `MusicModel`.
4. Pass real `VisemeMark`s into `ChoreographyEngine.Input.VisemeMarks` in `ChoreographyGenerationPipeline`
   (currently left at its `null` default) so the already-built `LyricTimingTrackBuilder` path activates.
5. Verify (via a real generation, same discipline as every other fix this project has used) that: the
   Director prompt's vocal-region line now prints non-trivial counts, the Choreographer prompt's
   `VocalActive` field is sometimes `true`, the `"Percussion"` timing track has real marks, and
   `LookCompiler`'s vocal-hold-bias measurably changes output on a song with clear vocal sections.

### 3b. Surface melodic contour as a per-section digest

1. Add a compact, per-`SectionLabel` summary — not raw frame-by-frame pitch data, which would bloat the
   prompt — e.g. a new `SectionMelodicBrief(SectionId, ContourShape, NoteDensity, PeakNoteBeatOffset)`
   record, computed once from `MelodyTrack` joined against each section's bar range.
   `ContourShape` is a small closed enum (`Rising`, `Falling`, `Mixed`, `Static/Sparse`) — deliberately
   coarse, matching the prompt-guidance granularity in §2c above, not a continuous curve the LLM would
   have to interpret unguided.
2. Surface `SectionMelodicBrief` in `SectionScriptPromptBuilder.BuildVolatileSuffix` as one short line per
   section (e.g. `"Melodic contour: Rising, high note density, peak at beat 12 of 16."`).
3. Add the §2c rules verbatim (rising → climb-to-peak, falling → settle, dense → faster subject
   cadence, sparse/held → animated hold) as new Choreographer prompt guidance referencing this field.

### 3c. Surface motif repeat identity

1. Join `MotifClusteringAnalysis.Groups`/`Links` against `SectionLabelAnalysis` to produce, per section, a
   reference to the *specific earlier section* (if any) sharing its motif — not just its `SectionKind`.
2. Extend `SectionScriptPromptBuilder.BuildVolatileSuffix` (which already computes `occurrenceIndex`/
   `totalOccurrences` per `SectionKind`) to also report this motif-linked predecessor when one exists and
   differs from a same-Kind match (e.g. a Bridge that quotes the Chorus's hook).
3. Add the §2d leitmotif-and-escalate rule as new prompt guidance, naming the specific earlier section so
   the AI can deliberately echo an identifiable visual element from it.

### 3d. Band-energy prop-role mapping

1. Once 3a wires real `BandEnergyEnvelope` data through, compute a simple per-section dominant-band
   profile (e.g. bass/mid/high share of total energy across the section's window).
2. Add §2b's bass→foundation/mid→subject/highs→accent mapping as a *named, concrete* prompt rule (listing
   the real foundation/subject/accent groups from the imported layout, not a generic abstraction) in
   `DirectorPromptBuilder`, so group *role* assignment is consistent for the whole song rather than
   re-decided per Look. This directly extends the existing per-style `PropRoleBias` weights (currently
   static, unconnected to any real per-song signal) with a real measured basis.

### 3e. Phrase-scoped sub-section variation

1. Extend the `SectionScript`/Look-assignment model so the Choreographer may optionally declare up to 2-3
   phrase-scoped variations within one section (e.g. "phrase 2 of 4: add layer X" / "phrase 4 of 4:
   escalate toward the next section"), falling back to today's one-Look-per-section behavior when it
   declares none — additive, not a breaking change to the existing contract.
2. `LookCompiler`/`ChoreographyEngine` need to compile each phrase-scoped variation against its own
   sub-window of the section, re-using the exact per-layer compilation logic already in `LookCompiler`
   (no new compilation mechanism, just a finer window granularity).
3. Add the §2e staging guidance as new Choreographer prompt text, explicit that this is optional per
   section — short/simple sections do not need manufactured phrase-level variation.

### 3f. Activate corpus mining

1. Add a real, reachable invocation path for `CorpusMiningRunner` — at minimum a Developer Tools menu
   action (the app already has a "Developer tools" affordance per the Generate & Export screen), ideally
   also an automatic one-time run the first time a real corpus directory is configured.
2. Persist the resulting `CraftRule`s/`ExemplarIndex` the same way `RoleProviderConfig` and other
   settings are persisted, so `AiChoreographyWholeSongGenerator` can load real, non-empty values instead
   of the current hardcoded `Promoted: []`/empty index.
3. Verify the Director prompt's "Verified craft rules"/"Retrieved exemplars" sections contain real content
   on a real run, and that at least one generated choice visibly reflects a mined rule (e.g. a rejected
   effect combination, a corpus-typical group count for a given section kind/energy).

### 3g. Resolve the Composition duplication — RESOLVED (2026-10-30)

1. Confirm with the user whether any idea in `XlightsAI.Studio.Composition` (musical-role assignment,
   `VisualBudget` density budgeting, `TransitionPlan` gesture recipes) is worth porting into the real
   `AiChoreographyWholeSongGenerator`/`LookCompiler` path as a deterministic pre/post-pass — e.g.
   `VisualBudget`-style density capping could be a useful deterministic backstop alongside the AI's own
   density choices.
2. Whichever ideas are not worth porting: archive or clearly mark `Composition` and the old
   `WholeSongGenerator`/`ChoreographyCompositionRunner` path as superseded/unused in its own doc comments
   and in `MASTER-SYSTEM-ARCHITECTURE.md`, so future work does not land in a path that never executes.
   Deletion is also an option once the user confirms nothing in it is still wanted — this plan does not
   decide that unilaterally.

**Decision (confirmed with the user): archive, don't port or delete.** Nothing in `Composition` is worth
porting as-is: `MusicalRoleAssignment`/`TransitionPlan`/the precedence-tier system are superseded by the
live path's own band-energy prop-role mapping (§3d) and prompt-driven transition guidance.
`VisualBudget`-style deterministic density/concurrency capping was judged a plausible *future* enhancement
(the live path's only density control today is advisory prompt text in
`DirectorPromptBuilder`/`SectionScriptPromptBuilder`'s `StyleDensity` targets, and
`ChoreographyLegalityValidator` — the live path's real deterministic post-pass — checks darkness/coverage/
peak-uniqueness but not density or concurrent-focal-effect count) but was deliberately deferred: it needs a
real design pass against `ChoreographyIR` (defining "focal" in IR terms, trim-vs-drop-vs-warn semantics,
retry-loop integration), not a mechanical port of Composition's own types. `Composition`,
`WholeSongGenerator`, and `ChoreographyCompositionRunner` are now marked superseded/unused in their own doc
comments and in `MASTER-SYSTEM-ARCHITECTURE.md` §9a; see `CHANGELOG.md` for the full record.

---

## 4. Sequencing and dependencies

```
3a (dead-wiring fixes)  ──────────────┬──> 3d (band-energy prop-role mapping, needs real BandEnergy)
                                       │
Structure/Melody already computed  ───┼──> 3b (melodic contour digest)
                                       │
                                       └──> 3c (motif repeat identity)

3e (phrase-scoped variation) ─ independent of 3a-3d, but should land after 3b/3c so a phrase-scoped
                                 variation can itself reference contour/motif data if useful.

3f (corpus mining) ─ fully independent; can be done in parallel with anything above.

3g (Composition duplication) ─ a decision + cleanup task, not blocking any of the above; do last so it
                                 does not distract from the real signal-utilization work.
```

Recommended order: **3a first** (lowest risk, highest leverage — real downstream code already exists and
is simply inert), then **3d** (direct follow-on, same data), then **3b/3c** in either order, then **3e**,
then **3f** and **3g** whenever convenient.

## 5. Real multi-layer Tree/Matrix exemplars found in the user's own xLights library

A research pass (2026-10-30) scanned every real, non-backup `.xsq` under `C:\xLights` (~30 distinct real
shows once backup/duplicate-folder copies are excluded) for the `Tree`/`Matrix*`/`Mega Tree`-named
elements with the most simultaneous `EffectLayer`s, to find real, human-made examples of exactly the
"many layers and effects at the same time" composition style §2.b/§3b-§3d already argue for. The richest
examples, by real `EffectLayer` count on a single element:

| Source show | Element | Layers | Effects (whole song) |
|---|---|---|---|
| Dance of the Sugar Plum Fairy (JayKode Trap Remix) | Tree | 16 | 960 |
| Dance of the Sugar Plum Fairy (JayKode Trap Remix) | Matrix | 14 | 1038 |
| Chuck Berry — Run Rudolph Run | Tree | 14 | 194 |
| Chuck Berry — Run Rudolph Run | Matrix | 12 | 206 |
| Carrie Underwood — Away In A Manger (Lite) | Tree / Matrix | 12 | 62 / 63 |
| The Weeknd — Blinding Lights | Matrix | 8 | 168 |
| Rockin' Around the Christmas Tree | Mega Tree | 9 | 68 |

For each, the single busiest ~6-second window (most distinct active layers × most overlapping effects)
was extracted with every effect's **real, resolved xLights settings and palette** (not guessed — resolved
directly from that sequence's own `EffectDB`/`ColorPalettes` lookup tables). These raw extracts are saved
at `docs/research/multilayer-template-exemplars/*.json`, with `README.md` in that folder explaining the
format and provenance in full.

### 6a. Two tiers — raw research vs. shippable templates, and why

The raw extracts are **dev-time reference material only, not shipped with the app**. Most of the source
shows are vendor-downloaded or fan-made sequences for commercial, copyrighted songs (The Weeknd, Mariah
Carey, Chuck Berry, Carrie Underwood, Bruno Mars, ...), several carrying vendor branding (Boscoyo props,
"JayKode Trap Remix," etc.). This is exactly the distinction the architecture's own corpus-mining design
already commits to (§8b, "ship the rules, not the content" — see `XlightsAI.Studio.Knowledge.CorpusEntry`'s
own doc comment): a personal copy for the user's own show is not the same as this project having
redistribution rights to bundle someone else's raw sequence content inside a public app build.

What **is** safe to derive and ship is a **generic structural pattern** — layer roles, effect-type
sequencing, approximate duty-cycle/timing shape, and blend relationships — with no literal copied palette
or song-specific exact timing attached. Three real patterns already visible across the extracts above
(examples of the derived form, not literal extractions):

- **"EDM dense-canvas Tree/Matrix" pattern** (from the Sugar Plum Fairy extracts): a fast `SingleStrand`
  chase layer (short ~150-250ms bursts, back-to-back, alternating a couple of `Chase_Type` variants) as
  the base texture, with a `Shockwave`/`Meteors` accent layer firing every 1-2 beats on top, both reading
  the active palette rather than a hardcoded color — i.e. a genuinely "always moving" dense canvas built
  from two cheap, fast-cadence layers rather than one busy effect.
- **"Quiet-ballad hold-plus-pop" pattern** (from the Carrie Underwood extract): one `Twinkle` layer at low
  intensity spanning the *entire section* as a steady base (confirming the existing "hold is a steady
  state, never a dead one" prompt rule is exactly what a real quiet sequence does), with a slow `Shockwave`
  layer on top firing only every 5-6 seconds, each burst lasting several seconds with a soft ~0.4s fadeout
  — restraint and a long cadence, not density.
- **"Mid-energy chase-and-accent Tree" pattern** (from the Chuck Berry extract): a `Spirals`/`Bars`-family
  base layer carrying the section's color identity, with short `Pinwheel`/`Twinkle` accent bursts layered
  on top at roughly phrase-length intervals — a middle ground between the two patterns above.

These three patterns alone already span the energy spectrum (EDM / mid / ballad) and could seed real,
shippable template content immediately; a fuller derivation (more patterns, more precise timing/duty
statistics) is Task 1 below.

### 6b. New tasks

1. **Derive the shippable pattern catalog.** Run `CorpusMiningRunner`-style induction (or, as a faster
   first pass, manual curation like §6a above) over the raw extracts in
   `docs/research/multilayer-template-exemplars/` to produce a small, versioned JSON catalog of generic
   multi-layer templates — e.g. `{ "id": "edm-dense-canvas-tree", "energyBand": "high", "props": ["Tree",
   "Matrixes"], "layers": [{"role": "base-texture", "effect": "SingleStrand", "approxDutyMs": [150, 250],
   "notes": "..."}, {"role": "accent", "effect": "Shockwave", "cadence": "1-2 beats", "notes": "..."}] }`
   — palette-free and song-free, matching the real `Look`/`LookLayer` shape (`Effect`, `Role`, `Blend`,
   `Duty`, `Timescale`) this project already uses, so a template can become a real seed `Look` directly.
2. **Pick a shippable storage location and build-inclusion mechanism.** Store the derived catalog as a
   new `src/XlightsAI.Studio.Knowledge/SeedData/multilayer-tree-matrix-templates.json` (or similar),
   marked `<EmbeddedResource>` (or `<Content CopyToOutputDirectory="PreserveNewest">`, matching however
   `effectmetadata` itself is already handled) in `XlightsAI.Studio.Knowledge.csproj`, so a normal
   `dotnet publish`/installer build carries it automatically — no separate manual-copy step a release
   could forget, the same discipline already used for the effect-vocabulary metadata.
3. **Load it at startup and feed it to the Director/Choreographer, the same place `ExemplarIndex` already
   has a real, currently-empty consumer slot** (`AiChoreographyWholeSongGenerator.cs`'s hardcoded empty
   `ExemplarIndex` — see §1a). This makes the templates something the AI actually draws from while
   composing Tree/Matrixes Looks, not just reference documentation.
4. **Also surface them in the Shell UI during generation** — the user's own request that these "should be
   seen during building of the show" reads two ways, and both are worth doing: (a) the AI consumption in
   Task 3, and (b) a visible indicator on the Generate & Export screen (or a Developer Tools panel) naming
   which seed template(s) informed a given Look, so a user watching a generation run can see "this Chorus
   Look is using the EDM dense-canvas Tree pattern" rather than the mechanism being invisible.
5. **Grow the catalog over time** from the user's own future sequences (hand-edited or AI-generated ones
   the user keeps) — since those are the user's own original work, no licensing caveat applies to mining
   patterns from them, unlike the vendor-downloaded shows in §6a.

### Depends on
Task 1 benefits from (but does not strictly require) §3f's corpus-mining activation — manual curation is
a viable first pass. Task 3 depends on §1a's dead-wiring fixes to `ExemplarIndex` plumbing already being
addressed, or at minimum the same hardcoded-empty-index call site being touched once for both fixes.

### Acceptance criteria
- A real generation shows at least one Tree/Matrixes Look whose layer composition visibly matches one of
  the shippable catalog's named patterns (verified the same way every other fix in this project has been
  — inspecting the real exported `.xsq`, not just prompt text).
- The template catalog file is present in a real build/publish output directory without any manual
  post-build step, verified the same way `effectmetadata` presence is already assumed/verified elsewhere
  in this codebase.
- A user running a real generation can see, somewhere in the UI, which seed template(s) a generated Look
  drew from.

---

## 6. Acceptance criteria for this phase

- A real generation on a song with clear verse/chorus vocal structure shows: non-empty vocal-active
  region count in the Director prompt's rendered input, `VocalActive: true` for at least one Choreographer
  section call, and a measurable hold-vs-chase behavior difference between a vocal-active and an
  instrumental section in the compiled IR.
- The same real generation shows a non-empty `"Percussion"` timing track with marks that visibly
  correlate with the song's actual drum/bass hits (spot-checked against the waveform).
- At least one generated Look's subject-layer motion direction (ramp-up vs. settle) measurably correlates
  with a rising vs. falling melodic phrase in the source song.
- At least one repeated section (e.g. a second Chorus) visibly escalates relative to its motif-linked
  predecessor, and the Director/Choreographer prompt's rendered input names that predecessor explicitly.
- At least one section uses a declared phrase-scoped variation (not the same Look rendered flat across
  the whole section) when the section is long/multi-phrase enough to warrant one.
- The Director prompt's "Verified craft rules"/"Retrieved exemplars" sections contain real, non-empty
  content from an actual corpus run, not placeholder text.
- A decision is recorded (in `CHANGELOG.md` or `MASTER-SYSTEM-ARCHITECTURE.md`) on whether
  `XlightsAI.Studio.Composition` is ported-from, archived, or deleted — it is not left silently
  duplicating effort with no resolution.
