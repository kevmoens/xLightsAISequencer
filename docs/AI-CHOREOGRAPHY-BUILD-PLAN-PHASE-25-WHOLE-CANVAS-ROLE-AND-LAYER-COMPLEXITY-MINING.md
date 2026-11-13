# Phase 25 — Whole-Canvas Band Role, External-Sequence Training, and Layer-Complexity Exemplars

Status: planning only. No code changes in this document.

## Why

Two gaps surfaced from a review of the exemplar library and `BandEnergyGroupRoleMapper`:

1. A whole-layout catch-all group (e.g. a group named `Everything`) is **structurally excluded** from
   `BandEnergyGroupRoleMapper.Classify` (`src/XlightsAI.Studio.Director/BandEnergyGroupRoleMapper.cs:38-52,78-98`)
   because it doesn't match any Foundation/Subject/Accent keyword, and forcing a whole-canvas group into one
   of those three band-specific roles would misdescribe it. It's correctly omitted today -- but that also
   means the system never learns to *use* it at all.
2. Mined multi-layer complexity (`MultiLayerStackMiner`, Phase 20) only feeds a separate
   `MultiLayerTemplate` catalog (`src/XlightsAI.Studio.Knowledge/MultiLayerTemplate.cs`) that isn't wired
   into `ExemplarIndex`/`ExemplarIndexBuilder` at all -- so the Director's exemplar-retrieval path
   (`ExemplarIndex.FindSimilar`/`FindVaried`) never deliberately reaches for a genuinely complex,
   multi-layer moment. The existing corpus (real xLights sequences not produced by this app) also isn't
   mined at all for statistics beyond the hand-audited allowlist in `CorpusManifestBuilder`.

This phase has five parts: (A) a new `BandRole`, (B) wiring it in, (C) a training pass over external
sequences, (D) layer-complexity exemplars, and (E) reuse-budget gauges. Parts C and D/E both depend on
having a training pipeline, so C ships first.

## Part A — A new `BandRole`: `WholeCanvas`

### Finding: no other named-keyword gap, but the detection method itself is the real gap

Searching corpus data and tests for other skipped names (beyond `Everything`) didn't turn up a second
*keyword* gap worth a fourth keyword list. The real problem is that keyword matching is the wrong tool for
this category in general -- a catch-all group can be named `Everything`, `All`, `AllModels`, `Master`,
`WholeHouse`, etc., and no fixed keyword list ever fully covers user naming. The right fix is **structural
detection**, which also future-proofs against names we haven't seen yet:

- `LayoutSummary`/`LayoutSummaryEntry` (`src/XlightsAI.Studio.Director/LayoutSummary.cs:6,13-17,27,35`)
  already carries `TotalPixels` (layout-wide) and each group's own `NodeCount` (summed member pixels).
  **A coverage ratio (`NodeCount / TotalPixels`) is computable today with zero new data** -- no group
  membership list needs to be added.
- The existing test fixture (`tests/XlightsAI.Studio.Director.Tests/BandEnergyGroupRoleMapperTests.cs:95,106`)
  already uses a `group:Everything` entry at 1000px against a 1000px-total layout (100% coverage) as its
  example of an excluded group -- confirming this is exactly the right signal to key off of.

### Design

Add a fourth `BandRole`:

```csharp
public enum BandRole
{
    Foundation,
    Subject,
    Accent,
    WholeCanvas,
}
```

`WholeCanvas` means "this group's effects are not tied to one frequency band's spatial home -- they are a
song-wide synchronized moment across the whole layout." It is a different *kind* of role than the other
three (it's a When, not a Where), so downstream consumers must not treat it as a fourth peer in the
"foundation/subject/accent" band-to-group mapping -- it gets its own prompt section (Part B).

### Classification rule

In `BandEnergyGroupRoleMapper.Classify`/`ClassifyName`, check coverage **before** keyword matching:

- Compute `coverageRatio = entry.NodeCount / layout.TotalPixels` for every group (models typically won't
  reach a high ratio; this is a group-level signal).
- If `coverageRatio >= WholeCanvasCoverageThreshold` (propose `0.90` -- high enough that a merely "large"
  group like a big megatree doesn't false-positive, low enough to tolerate a handful of pixels the catch-all
  group's definition happens to exclude), classify as `WholeCanvas` and skip keyword checks for that entry.
- Keep keyword matching as the fallback for everything under the threshold, unchanged.
- `ClassifyName` currently only takes a name string; it needs to become coverage-aware, so its signature
  must change to take the entry's node count + the layout's total (or take the whole `LayoutSummaryEntry`
  and `LayoutSummary`).

### Threshold needs calibration, not guessing

`0.90` is a placeholder. Part C's training pass should report the actual coverage-ratio distribution of
real catch-all groups across external sequences so this constant is evidence-based before it ships, the
same way Phase 24's `MinLooks=8`/`MaxLooks=20` are stated as measured gauges, not arbitrary picks.

## Part B — Wiring `WholeCanvas` into the Director

1. **`ClassifiedLayoutEntry` consumers.** `DirectorPromptBuilder.AppendBandToGroupMapping`
   (`DirectorPromptBuilder.cs:397-430`) currently emits only Foundation/Subject/Accent group names under
   "## Band-to-group mapping". Add a sibling section, e.g. "## Whole-canvas group", listing any
   `WholeCanvas`-classified group by name, present only when one exists (same "append nothing when there's
   nothing to ground it in" convention already used for the band mapping section).

2. **Usage guidance in that section must come from training (Part C/D), not an engineer's guess.** The
   whole point of flagging this as a gap in the original conversation was that we don't yet know *when* real
   designers fire a whole-canvas group (every chorus? only the final hit? never longer than N beats?). Once
   Part C produces real statistics, encode them here as concrete, measured guidance -- mirroring how
   `AppendBandToGroupMapping` already states its rule as "measured for this song, not re-decided per Look."

3. **Pipeline-level support.** Audit `ChoreographyGenerationPipeline.cs` for whether `BandRole` currently
   only affects prompt text or also affects which exemplars/effects get selected for a group. If the former,
   confirm that's acceptable for v1 (prompt-only guidance, Director decides placement) before expanding
   scope to compiled-choreography logic.

4. **Co-occurrence: what else is active when `WholeCanvas` fires.** This is new analysis, not an existing
   capability -- `BandEnergyGroupRoleMapper.ComputeSectionProfiles`/`ComputeSongWideShare`
   (`BandEnergyGroupRoleMapper.cs:109-157`) give per-section band *share*, and `EmphasisContrastPlanner.cs`
   tracks brightness-tier contrast around one emphasis instant on one target model -- neither answers "which
   roles are simultaneously emphasized when the whole-canvas group fires." Build a new, purpose-built
   co-occurrence pass (feeds off Part C's mining, see below) that, for every time window where a
   `WholeCanvas`-classified group has an active effect in a corpus song, records which of that song's
   Foundation/Subject/Accent groups also have an active effect in the same window, plus the section kind and
   energy percentile. Aggregate across songs into something like "WholeCanvas co-fires with Subject 70% of
   the time, almost never with Foundation alone, concentrated in final-chorus/bridge sections, median
   duration 1-2 bars." That aggregate is what Part B.2's prompt guidance should actually say.

## Part C — Training from external (non-app-authored) sequences

### Why this is currently blocked, and why that block is partly intentional

- `CorpusManifestBuilder` (`src/XlightsAI.Studio.Pipeline/...`) is **entirely hardcoded**: a fixed
  `KnownResolutions[]` array hand-lists ~25 specific sequence/audio pairs, each manually tagged
  `UserAuthored` / `LikelyVendorOrCommunityDownload` / `ExternalReference` (`CorpusProvenanceTag.cs:10-20`).
  Anything on disk not in that list is excluded with an explicit "re-audit needed if this is a new real
  sequence" message.
- `CorpusMiningRunner.RunAsync` skips analysis entirely for `ExternalReference` provenance, and
  `ExemplarIndexBuilder`/`MultiLayerStackMiner`/`PatternLibraryBuilder`/`CraftRuleInducer` all filter it out
  too. That's a **licensing control**, not an oversight: `ExemplarFingerprint.Provenance`'s own doc comment
  says it's "carried through so a future redistribution decision can filter exemplars by license" -- the
  system is deliberately refusing to reconstruct/redistribute a real third-party sequence's actual effect
  content as an exemplar.
- So: we can't simply flip a switch and mine arbitrary downloaded sequences into the exemplar library wholesale.
  We *can* learn aggregate statistics (counts, ratios, timing patterns) from sequences we have rights to
  analyze locally, without ever re-emitting their actual effect data as a retrievable exemplar. That
  distinction -- "statistics about" vs. "reconstruction of" -- is what makes this safe to build.

### Plan

1. **New ingestion path, separate from the exemplar-eligible allowlist.** Add a training-only discovery
   mode that scans an arbitrary user-specified folder for `.xsq` files, tags everything found
   `ExternalReference` by default (reusing the existing enum value -- no new provenance category needed),
   and does **not** require the file to appear in `CorpusManifestBuilder.KnownResolutions`. This is
   explicitly for sequences "not created from this app" per the ask -- it should not touch or need changes
   to the existing audited allowlist used for exemplar-eligible corpus songs.

2. **New CLI surface.** Either a new flag on `tools/CorpusMiner/Program.cs` (e.g. `--train-only
   <externalSequencesRoot>`) or a new sibling tool (`tools/CorpusTrainer/Program.cs`) that takes a folder
   path and knowledge-output directory, runs `CorpusSongAnalyzer` against every discovered sequence, and
   writes a **separate statistics artifact** (e.g. `corpus-training-report.json` under the knowledge
   directory) rather than appending to `exemplar-index.json` or `multilayer-templates-mined.json`.

3. **What the training pass actually computes**, all aggregate/statistical, never raw effect
   reconstruction:
   - Coverage-ratio distribution of catch-all/whole-canvas groups (feeds Part A's threshold).
   - `WholeCanvas` co-occurrence statistics (feeds Part B.4): which roles co-fire, in which section kinds,
     at what duration/frequency.
   - Per-model layer-complexity distribution using a generalized `MultiLayerStackMiner` (Part D) -- *how
     many* models/groups across the external corpus reach 3+, 4+, 5+ simultaneous layers, in which section
     kinds, and for how long -- without storing which specific effects/settings produced them.
   - Exemplar/motif reuse-rate statistics (feeds Part E): how often a structurally similar passage (same
     phrase-length window, same section kind, similar energy percentile) recurs within one real song, to
     ground the reuse-budget gauges in measured behavior instead of a guess.

4. **Respect the license boundary explicitly in the output schema.** The training report should contain
   only numbers/ratios/histograms, no `ModelName`/`EffectName`/`SettingsString` strings copied verbatim from
   source files, so it can't accidentally become a backdoor way to redistribute third-party content under a
   different file name.

## Part D — Multi-layer complexity into the exemplar library

### Current state

- `MultiLayerStackMiner.FindStackOccurrences` (Phase 20) clusters time-overlapping effects per model,
  requiring `distinctLayers >= 2` and peak concurrent overlap `>= 2`. **The threshold today is 2+ layers,
  not 3+.**
- It's restricted to `TreeMatrixPropNameConvention.IsTreeOrMatrixName(ModelName)` -- it never looks at any
  other model or group, including a `WholeCanvas` group (consistent with the user's instruction that this
  effort is *not* for `WholeCanvas` -- it's a separate axis entirely).
- Its output feeds only the `MultiLayerTemplate` catalog (merged hand-curated + mined, deduped by `Id`),
  consumed via a `Look`'s `TemplateRef` -- a system entirely disjoint from `ExemplarEntry`/`ExemplarIndex`.
  `PatternLibraryBuilder` separately computes a `LayerCountPerModelDistribution` for reporting, but that's
  descriptive statistics only, not retrievable content.

### Plan

1. **Raise the complexity bar to 3+ distinct layers** per the ask (`MultiLayerStackMiner`'s `distinctLayers
   >= 2` check becomes a configurable minimum, default raised to `3`), and **generalize beyond
   tree/matrix-only models** -- drop (or make optional) the `IsTreeOrMatrixName` restriction so any
   model/group with genuinely complex layering is discoverable, not just trees/matrices.

2. **Surface layer complexity on `ExemplarFingerprint` itself**, not just the separate template catalog.
   Add a field (e.g. `PeakConcurrentLayerCount` or `LayerComplexity`) populated by reusing
   `MultiLayerStackMiner`'s own clustering logic, but evaluated against `ExemplarIndexBuilder`'s
   phrase-window granularity so every exemplar window -- not just ones that happen to match the separate
   miner's own windowing -- carries a complexity number.

3. **Make `ExemplarIndexBuilder.Build` complexity-aware.** Ensure high-complexity windows aren't
   under-sampled or dropped by whatever per-song/per-phrase selection logic currently governs which
   `PhraseCandidate` windows become exemplars -- a complex 3+ layer moment should be *more* likely to be
   indexed, not equally likely, since the explicit goal is "these complex effects of multiple layers built
   into Exemplar Library ... so they are used."

4. **Make retrieval complexity-aware.** `ExemplarIndex.FindSimilar`/`FindVaried` currently compare on
   `ToComparisonVector()` (tempo/energy/mid-band/vocal-activity) only. Add an optional complexity
   bias/filter parameter so the Director can deliberately request "give me a genuinely layered moment here"
   for sections where the show bible calls for added density/complexity, rather than complexity being an
   accidental byproduct of whichever window the similarity search happens to prefer.

5. **Exemplar library UI.** Alongside the `EffectCount` column just added
   (`src/XlightsAI.Studio.Shell/ExemplarLibraryViewModel.cs`), add a layer-complexity badge/column (e.g.
   "3 layers peak") so curators can see and selectively enable/disable these entries, consistent with the
   screen's existing per-entry curation model.

## Part E — Reuse/repeatability gauges

### Current state (confirmed gap)

- Phase 24 already establishes the *pattern* for this kind of gauge at the Look level:
  `ShowBibleValidator` has `MinLooks=8`, `MaxLooks=20`, `MinPalettes=3`, `MaxPalettes=5`,
  `MinOccurrencesForDiversityFloor=3`, and `DirectorPromptBuilder.AppendRepeatingKindStructure` computes
  `requiredDistinctLooks = Ceiling(occurrences / 2.0)` for any `SectionKind` with 3+ occurrences.
- There is a separate, unrelated "how many exemplars to retrieve" gauge,
  `DirectorPromptBuilder.ExemplarCountForKind`: `Clamp(available / 20, 1, 5)`.
- **Neither of these is a reuse budget.** Nothing tracks how many times one specific `ExemplarEntry` (or
  one mined multi-layer stack signature) has already been drawn on within a single generation run, and
  nothing groups structurally-similar exemplars into a "repeatable piece" cluster the way Looks are already
  grouped and capped. `FindSimilar`/`FindVaried` just independently query per section/kind each call, with
  no memory of prior picks across the run.

### Plan

1. **Per-exemplar reuse counter for the duration of one generation run.** Add reuse tracking (keyed the same
   way `ExemplarOverrides.KeyFor` already keys entries) to whichever component orchestrates whole-song
   generation (`ChoreographyGenerationPipeline`/`AiChoreographyWholeSongGenerator`), with a configurable
   min/max range for how many times a single exemplar may be reused in one song -- mirroring
   `ShowBibleValidator`'s existing Min/Max constant style, not a new ad hoc mechanism.

2. **"Repeatable piece" grouping.** Cluster exemplars that are structurally near-duplicates (same phrase
   length + section kind + close energy percentile, or sharing a mined multi-layer stack signature) into a
   named group, then gauge draws at the *group* level the same way `requiredDistinctLooks =
   Ceiling(occurrences/2.0)` already gauges distinct-Look draws per repeating `SectionKind` -- i.e. reuse the
   existing ratio-based pattern rather than inventing a new shape of rule.

3. **Ground the actual min/max numbers in Part C's training data**, not a guess -- mine how often a real
   song revisits a structurally similar passage with near-identical lighting, and set the range from that
   measured distribution, consistent with how this project treats every other such constant (explicitly
   labeled "measured," never silently assumed).

4. **Validation gate.** Surface the reuse budget as a new check alongside `ShowBibleValidator`'s existing
   Min/Max checks so a song that over-reuses one exemplar (or under-diversifies its repeatable pieces) fails
   validation the same way an out-of-range Look count already does.

## Sequencing

Parts A/B (WholeCanvas role) and Part D (layer-complexity exemplars) can be scaffolded in parallel, but
both need real numbers from Part C (training) before the *values* they ship with (coverage threshold,
co-occurrence guidance text, complexity-bias weighting) are evidence-based rather than placeholders. Part E
depends on Part C's reuse-rate statistics specifically. Recommended order:

1. Part C groundwork: training-only ingestion path + CLI surface (no statistics logic yet beyond basic
   counts), so later parts have somewhere to write results.
2. Part A: `WholeCanvas` role + structural detection (ships with a placeholder threshold, flagged as
   needing Part C's calibration).
3. Part C statistics: coverage-ratio distribution, co-occurrence mining, layer-complexity mining, reuse-rate
   mining -- all four in one pass over the same external corpus.
4. Part B: wire calibrated threshold + co-occurrence-grounded prompt guidance into `DirectorPromptBuilder`.
5. Part D: generalized `MultiLayerStackMiner` (3+ layers, any model), `ExemplarFingerprint` complexity field,
   complexity-aware `ExemplarIndexBuilder`/`ExemplarIndex`, UI badge.
6. Part E: reuse counters + repeatable-piece grouping + validation gate, using Part C's reuse-rate numbers.
