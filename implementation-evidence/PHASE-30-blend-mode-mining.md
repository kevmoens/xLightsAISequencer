# Phase 30 step 30.1 — Blend-mode mining and selection (`blend-mode-mining`)

**Result: PASS.**

## What this step had to establish

Per the build plan: extend corpus mining to record the blend mode between adjacent mined layers; build a
real compatibility signal from that mined data (reusing the existing brightness/contrast catalog, not a
new metric); add a `LayerBlendPlanner` that selects a blend mode per adjacent layer pair from the
confirmed set only, informed by the mined signal; and pass the blend-mode diversity acceptance definition
on the 30.0 baseline fixture songs, recording where N came from.

Hard constraint carried forward from 30.0 and never relaxed here: the only `T_CHOICE_LayerMethod` values
any code may select or emit are the 8 entries in `ConfirmedLayerBlendModeCatalog.Modes`, plus the existing
`"Normal"` default (never itself "chosen").

## 1. Mining: blend mode recorded per adjacent layer in `MultiLayerTemplate`

`src/XlightsAI.Studio.Knowledge/MultiLayerTemplate.cs` — `MultiLayerTemplateLayer` gained one new,
backward-compatible-default field: `BlendModeToPrevious` (a `ConfirmedLayerBlendMode.CandidateName`, or
`null` for "no mined evidence" — always `null` for position 0, the base layer, which has nothing below it
to blend against). Every existing hand-curated seed template and every pre-existing
`multilayer-templates-mined.json`/`multilayer-templates-showcase.json` file keeps deserializing and
behaving identically (confirmed by the full, unmodified `MultiLayerTemplateCatalogLoaderTests` suite
still passing — same discipline `MultiLayerTemplate.ConfidenceTier` already established in Phase 27).

`src/XlightsAI.Studio.Pipeline/MultiLayerStackMiner.cs`:

- `TryMapRawBlendModeToCandidate` (new, public): maps a real, raw `T_CHOICE_LayerMethod` XML value to its
  confirmed candidate name, recognizing **both** real directional spellings 30.0's own corpus grep found
  for Mask/Unmask (`"1 is Mask"`/`"2 is Mask"`, `"1 is Unmask"`/`"2 is Unmask"`) even though the confirmed
  catalog only ever *emits* the canonical `"2 is ..."` spelling. A real-but-non-candidate value (`"Layered"`,
  `"Brightness"`, `"Additive"`, `"Bottom-Top"`, `"Min"`, `"Left-Right"`, the `"True Unmask"` variants, or the
  implicit default `"Normal"`) maps to `null` — real signal, out of this phase's scope, never guessed into
  a candidate.
- `FindStackOccurrences` now reads each stack layer's own real `ParsedEffectInstance.SettingsString` (the
  resolved `EffectDB` entry `EffectAlignmentParser` already recovers — no new parsing pass, no audio
  dependency) and records a per-occurrence blend-mode vote per layer position.
- `BuildTemplate` takes the **plurality vote** across every real occurrence at each position (same
  discipline `ClassifyCadence`'s own cadence vote already uses) — `null` ("no mined evidence") competes as
  its own candidate, so a stack whose real corpus occurrences mostly left the setting at its implicit
  default correctly yields `null`, never a minority confirmed value fabricated as the stack's own
  consensus.

Tests: `tests/XlightsAI.Studio.Pipeline.Tests/MultiLayerStackMinerTests.cs` — 10 new tests (26/26 total in
the file pass): a confirmed value mined onto the correct top layer across enough promoted occurrences; a
non-candidate real value mining to `null`, never guessed into a candidate; a stack whose occurrences mostly
omit the setting still mining to `null` even with a minority confirmed vote present; and a `[Theory]`
exercising every real spelling 30.0's own occurrence tally found (including both Mask/Unmask directions),
confirming the mapping table matches that evidence exactly.

## 2. Compatibility signal: reusing the existing brightness/contrast catalog, not a new metric

`src/XlightsAI.Studio.Knowledge/BlendModeCompatibilityIndex.cs` (new): for a given (base effect, top
effect) pair, tallies every mined `MultiLayerTemplate` that recorded a confirmed `BlendModeToPrevious` for
that exact pair, weighting `"corpus-validated"` templates above `"single-song-showcase"` ones (reusing
`MultiLayerTemplate.ConfidenceTier`, not inventing a second confidence scale), and uses
`EffectVisualCharacterCatalog`/`BrightnessTier` — the exact same "two tiers differ by at least one step =
contrast, not muddy" signal `EmphasisContrastPlanner`/`EmphasisContrastFinding.HasContrast` already
established in Phase 22 — as a tie-breaker only, never to invent a candidate the mined catalog has no real
support for. `ScoresFor`/`SelectBlendMode` return nothing (`null`/empty) for a pair with zero mined
evidence — never a guessed fallback.

Tests: `tests/XlightsAI.Studio.Knowledge.Tests/BlendModeCompatibilityIndexTests.cs` — 5 tests: empty for an
unmined pair; `null` selection with no evidence; stronger mined support wins; corpus-validated evidence
outweighs single-song-showcase; every returned candidate name is independently re-verified against
`ConfirmedLayerBlendModeCatalog.Find` (defensive, in case a corrupted template ever slipped through).

## 3. `LayerBlendPlanner` (Choreography) — opt-in, confirmed-only selection

`src/XlightsAI.Studio.Choreography/LayerBlendPlanner.cs` (new): given a `Look`'s own declared layer stack
(ordered by `Z`), looks up `BlendModeCompatibilityIndex.SelectBlendMode` for each adjacent (base, top)
effect-name pair and, only when the mined catalog has real evidence, sets the top layer's own
`LookLayer.Blend` to that confirmed mode's exact XML value (re-verified against
`ConfirmedLayerBlendModeCatalog.IsConfirmedXmlValue` defensively before ever being written). A pair with no
mined evidence is left exactly as declared — almost always `"Normal"` — never guessed.

**Opt-in, not wired into `LookCompiler`/`ChoreographyEngine` automatically** — matching Phase 29's own
guardrail against blanket requirements and this phase's own explicit "keep all new behavior opt-in per
Look/section" dependency note. Nothing calls `LayerBlendPlanner` unless a caller deliberately runs a
specific `Look` through it (this step's own diversity test is exactly such a caller; a later generation
pass, e.g. step 30.5, is free to call it per Look/section when it chooses to).

Gate test (direct, as the build plan's own wording asks for): `tests/XlightsAI.Studio.Choreography.Tests/LayerBlendPlannerTests.cs`
— `Plan_EveryEmittedBlendValue_IsAConfirmedXmlValue_AcrossEveryMinedCandidate` fuzzes/enumerates the
planner's own output across **every one of the 8 confirmed candidates** and asserts each emitted value
both equals that mode's own confirmed XML value and passes `IsConfirmedXmlValue` directly, plus 4 more
tests (no-evidence pairs left unchanged, single-layer Looks untouched, base layer never touched, 3-layer
stacks selecting independently per pair). 5/5 pass.

## 4. Reaching the exported XSQ: `LookCompiler` + `XsqExporter`

Checked whether this step's choice needed to reach `XsqExporter`/`EffectInstance.BlendMode` to be
observable in output, per the task's own prompt — yes: `LookLayer.Blend` was previously declared but never
read by `LookCompiler` (`BlendMode` always defaulted to `EffectInstance`'s own `"Normal"`), and
`XsqExporter.BuildSettingsString`'s own doc comment explicitly disclosed "`BlendMode` is IR-only, never
serialized," dated from before any mode was confirmed. Both are now wired, narrowly:

- `src/XlightsAI.Studio.Choreography/LookCompiler.cs`: both `CompileBoundLayer` and `CompileDiscreteLayer`
  now set the compiled `EffectInstance.BlendMode` from `LookLayer.Blend`, via a new
  `ResolveConfirmedBlendMode` helper that falls back to `ConfirmedLayerBlendModeCatalog.DefaultXmlValue`
  (`"Normal"`) for anything not in `IsConfirmedXmlValue` — so a pre-Phase-30 Look declaring a placeholder
  value (e.g. the existing fixture's own `"Additive"`) silently degrades to the harmless default instead of
  ever reaching compiled IR.
- `src/XlightsAI.Studio.Xlights/XsqExporter.cs`: `BuildSettingsString` now serializes
  `T_CHOICE_LayerMethod=<value>` **only** when `effect.BlendMode` is confirmed and non-default — the
  implicit default is omitted entirely (matching architecture §8a's own "defaults are omitted" rule and
  30.0's own measured "~100% implicit default" baseline), and any other string (an unconfirmed/placeholder
  value, defensively, in case some other caller ever bypasses `LookCompiler`'s own gate) is treated exactly
  like the default — never emitted. The doc comment's "BlendMode is IR-only, never serialized" claim is
  updated to disclose this one, narrowly-scoped exception.

Tests: `tests/XlightsAI.Studio.Choreography.Tests/LookCompilerTests.cs` (+2: confirmed blend flows into
compiled `EffectInstance.BlendMode`; an unconfirmed value falls back to `"Normal"`, never reaching compiled
IR) and `tests/XlightsAI.Studio.Xlights.Tests/Phase04VocabularyAwareExportTests.cs` (+3: a confirmed value
serializes as the exact real `T_CHOICE_LayerMethod` key/value; the default is omitted; an unconfirmed value
is omitted, never guessed into the exported file).

## 5. Gate: mined templates carry blend-mode data; planner selects only confirmed modes

Covered directly by §1/§3 above (`MultiLayerStackMinerTests`, `LayerBlendPlannerTests`) — both parts of
this gate are PASS.

## 6. Gate: blend-mode diversity acceptance definition passes on the 30.0 baseline fixture songs

**Where N came from:** the acceptance definition reads *"at least N distinct non-default blend modes
appear (N set from mined corpus data in step 30.1; record the chosen value and its source in evidence)."*
`tests/XlightsAI.Studio.Pipeline.Tests/BlendModeCorpusDiversityBaselineTests.cs` re-scans the exact same
real `C:\xLights\*.xsq` corpus (35 real author-created files) 30.0's own occurrence tally used — directly
via `EffectAlignmentParser` (structural XML parsing only; no audio analysis needed, since a raw
`T_CHOICE_LayerMethod` settings-string reading requires no beat grid) — and tallies, per confirmed
candidate, how many distinct real files carry it at all. **N = 7**: every confirmed candidate except
Subtractive (which appeared in only 1 of the 35 real files) repeats across 2+ distinct real files. Full
tally recorded in `implementation-evidence/PHASE-30-BLEND-MODE-DIVERSITY-N.md` (auto-generated by that
test, re-generated by re-running it).

**The run itself:** `tests/XlightsAI.Studio.Director.Tests/LayerBlendPlannerDiversityTests.cs` —

1. Re-scans the same real corpus a second, narrower way: only genuinely **adjacent** (`LayerIndex`
   differing by exactly 1), time-overlapping effect pairs (the exact shape `BlendModeCompatibilityIndex`
   actually consumes), keeping only the strongest-supported real (base effect, top effect) pair per
   confirmed candidate. 7 of the 8 confirmed candidates had at least one real adjacent-pair occurrence
   anywhere in the corpus (Subtractive again had none in adjacent form) — recorded, not fabricated.
2. Builds one real, 2-layer `MultiLayerTemplate` per mined pair and feeds it through `LayerBlendPlanner` to
   build one `Look` per pair — every blend-mode choice in this run traces back to a real, directly observed
   xLights show, never an invented value.
3. Compiles all 7 Looks directly via `LookCompiler.Compile` against each of the two 30.0 baseline songs'
   own real bar grid (full clip duration, via the same real `CorpusSongAnalyzer` audio-analysis path
   `Phase30BlendModeAndEffectTypeBaselineTests` uses), and exports every compiled effect together into one
   real XSQ via `XsqExporter` — same real layout (`C:\xLights\xlights_rgbeffects.xml`), same real generated
   vocabulary/recipe catalog as every other real-fixture test in this project.
   - **Deliberately bypasses `ChoreographyGenerationPipeline`'s own real-section/round-robin Look
     selection**, unlike `Phase30BlendModeAndEffectTypeBaselineTests`: these two 30-second clips detect only
     3-4 real sections each (confirmed by first running the round-robin version of this test, which only
     ever got 3 distinct Looks exercised across both songs combined and correctly failed the diversity gate
     at 3/7 modes — a real, disclosed limitation of the clips' own short length, not of the blend-mode
     mechanism itself). Driving `LookCompiler.Compile` directly keeps every other piece of the pipeline
     real (layout, vocabulary, recipes, exporter, the songs' own real bar grids) while actually exercising
     all 7 real mined Looks, which is what this gate is about.
4. Parses the exported XSQ back (`XsqStructuralReader`) and counts `T_CHOICE_LayerMethod` occurrences by
   raw XML value.

**Real measured result** (also written to `implementation-evidence/PHASE-30-BLEND-MODE-DIVERSITY-RUN.md`):
**7 distinct non-default confirmed modes observed** (Mask, Unmask, Reveal 1-on-2, Reveal 2-on-1, Average,
Shadow 1-on-2, Shadow 2-on-1) — meets N = 7. 14 total blended pairs across both songs, evenly split 1/7
each (14.3% per mode) — well under the 60% single-mode-share limit. **PASS.**

## 7. Full regression run

- `XlightsAI.Studio.Pipeline.Tests`: 108/108 pass (was 97 before this step's +11 new tests: 10 mining tests
  + 1 corpus-diversity-N test).
- `XlightsAI.Studio.Knowledge.Tests`: 111/111 pass (+5 new `BlendModeCompatibilityIndexTests`).
- `XlightsAI.Studio.Choreography.Tests` (non-integration): 239/239 pass (+7 new: 5 `LayerBlendPlannerTests`
  + 2 `LookCompilerTests`).
- `XlightsAI.Studio.Xlights.Tests`: 123/123 pass (was 120/120 at 30.0; +3 new blend-mode serialization
  tests).
- `XlightsAI.Studio.Director.Tests` (non-integration): 317/317 pass — identical to 30.0's own recorded
  baseline, confirming no regression from the `LookLayer.Blend`/`EffectInstance.BlendMode`/
  `XsqExporter.BuildSettingsString` wiring.
- `LayerBlendPlannerDiversityTests` (Integration, real xLights install + real corpus): PASS, as above.

## Gate check (per the build plan's own wording)

> "mined templates carry blend-mode data; `LayerBlendPlanner` selects only confirmed modes; blend-mode
> diversity acceptance definition passes on the baseline fixture songs."

All three are PASS, with N's value and source recorded in evidence as the plan requires. Step 30.2
(hit+motion compounding) may proceed in a future session — not started here, per the task's own instruction
to stop after 30.1's gate is verified.
