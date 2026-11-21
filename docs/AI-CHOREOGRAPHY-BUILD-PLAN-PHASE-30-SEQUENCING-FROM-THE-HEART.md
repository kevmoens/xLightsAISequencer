# Phase 30 — Blend-mode exploration, hit+motion compounding, reusable presets, and effect-family guardrails

## Goal and decisions

Ron Howard's "Sequencing from the Heart" session (xEssentials S2E26) frames unique, non-robotic sequencing around a handful of practices: isolate voices by timbre, give static note-hits movement instead of flat on/off, push contrasting colors so the eye knows where to look, treat xLights layer-blend modes (Mask/Unmask/Reveal/Average/Subtractive/Shadow) as a creative search space rather than a fixed default, save good combinations as reusable/recolorable presets, and deliberately avoid leaning on any one effect family (his example: shader) past the point where it's doing real work.

A codebase survey against the existing pipeline found five of his twelve practices already implemented to a reasonable standard — contrast-driven emphasis (Phase 22), submodel/group structure and ordering (native `DisplaySubmodel`/`PropGroupResolver`), per-section Look-diversity floor (Phase 24), corpus-mined multi-layer templates (Phase 20/25), and whole-Look expressive intent (Phase 18's `Look.Intent`). This phase targets the four gaps that compose cleanly with that existing infrastructure without new audio-analysis investment:

1. **Layer-blend-mode exploration** — nothing today models xLights blend modes as something the generator chooses or mines; `MultiLayerTemplate` records which effects stack, not which blend mode made the stack read well.
2. **Hit+motion compounding** — accent/percussive cues never get paired with a secondary movement layer (Ron's bell: frame-adjust image + shockwave value curve riding a note hit).
3. **User/generator-reusable, recolorable effect presets** — `VisualBudgetPresetCatalog`/`IVisualStoryStore` persist generation-setting presets, not a reusable palette-parameterized effect-combo unit comparable to an xLights Effect Preset.
4. **Anti-overreliance guardrail** — no validator flags a section leaning on one effect family past the point of diminishing returns.

Out of scope for this phase (tracked as a follow-up, not attempted here): true per-instrument onset detection and a configurable sequencing-timing-resolution knob (Ron's hand-tapped isolation and 40fps-for-32nd-notes practice). `BandEnergyAnalyzer` is frequency-band driven, not onset driven, and existing frame-rate code is export/encoding only (`XsqExporter`, `EffectFixtureVideoEncoder`); doing this properly needs a transient/onset-detection pass feeding cue placement, which is a larger, separate audio-analysis phase.

- Scope: `XlightsAI.Studio.Choreography`, `XlightsAI.Studio.Director`, `XlightsAI.Studio.Knowledge`, `XlightsAI.Studio.Composition`. Reuse current audio analysis, display mapping, and XSQ export; no new audio/ML models.
- Target xLights version for export/preview verification: `2026.03` (matches Phase 29's pinned fixtures). Use version-pinned XSQ fixtures; do not invent effect XML settings — confirm blend-mode and value-curve XML keys against a real `2026.03` XSQ fixture before relying on them.
- Build on, not replace: Phase 20 (`MultiLayerTemplate`, corpus mining), Phase 21 (`EffectVisualCharacterCatalog` brightness tiers), Phase 22 (`EmphasisContrastPlanner`), Phase 25 (whole-canvas role mining).

## Current state

- `MultiLayerTemplateCatalogLoader`/corpus mining (Phase 20/25) records which effect types stack in a Look's layers and in what order, but has no field for the xLights layer-blend mode (`T_CHOICE_LayerMethod`/mask-reveal settings) connecting one layer to the one beneath it. Blend mode defaults to whatever the effect-placement code already uses (Normal/Overlay), never varied as a decision.
- `EffectVisualCharacterCatalogBuilder`/`EffectVisualCharacterCatalogLoader` (Phase 18/21) catalog per-effect appearance and brightness tier, used by `EmphasisContrastPlanner` (Phase 22) to sequence dim-before-bright transitions. Nothing in that catalog or planner composes a secondary movement layer on top of an accent/hit cue.
- `ChoreographyEngine` and `SectionTimelineCompiler` (Phase 28/29) place accent, hold, and takeover cues from the section timeline, each resolving to a single effect or layered template; there is no compound type that pairs a hit effect with an independent movement overlay (value curve or shockwave-style effect) scoped to the same cue.
- `VisualBudgetPresetCatalog`/`IVisualStoryStore` (Composition) persist generation-time settings presets (visual budget, density knobs) keyed for the generator's own use, not a named, recolorable multi-layer effect combo a user or a later generation pass can stamp onto a new target and recolor via palette substitution.
- `SectionTimelineValidator` (Phase 22/29) validates contrast and hold/accent/takeover legality but has no signal for effect-family concentration across a section's placed effects.

## Acceptance definitions

- **Blend-mode fixture confirmation:** before any blend mode other than the pipeline's current default ships in generated output, its exact xLights `2026.03` XML key/value is confirmed against a real XSQ fixture checked into the repo (same bar as Phase 29's brightness-setting rule). Until confirmed for a given mode, that mode is excluded from the generator's choices, not guessed.
- **Blend-mode diversity:** across a song's generated Looks with 2+ layers, at least N distinct non-default blend modes appear (N set from mined corpus data in step 30.1; record the chosen value and its source in evidence), and no single non-default mode accounts for more than 60% of blended layer pairs.
- **Hit+motion compound integrity:** a compound cue exports as exactly two layers over its cue's resolved span (base hit effect + movement overlay), both within frame tolerance (±1 frame) of the cue's declared bounds, and the movement layer's value-curve or effect parameters differ from the pipeline's flat/static default for that effect type.
- **Preset reusability:** a saved preset reproduces its recorded layer stack (effect types, relative layer order, blend modes) on a different target and a different palette, with only palette-mapped colors changing — effect types, blend modes, and relative timing parameters are unchanged. A round-trip test: save a preset from a generated Look, apply it to a different model/group with a different palette, and diff the two placements' structure (ignoring color).
- **Effect-family concentration:** per section, no single effect family (as grouped in `EffectVisualCharacterCatalog`) accounts for more than 50% of placed layers unless the section's declared intent (`Look.Intent`) explicitly calls for sustained reliance on that family. Reported as an advisory (like Phase 22's contrast advisories), not a hard validator failure, since some sections legitimately want one dominant look.
- **Human preview gate:** blend-mode variety, hit+motion compounds, and preset reuse are judged by a person in xLights `2026.03` preview on at least one full-length song plus the existing short fixtures. Record reviewer and PASS/BLOCKED in the final evidence file; this is separate from and does not replace the automated gates.

## Steps

Each step writes targeted tests, implements its vertical path, runs those tests, inspects exported output where it applies, and records `implementation-evidence/PHASE-30-<STEP>.md` with PASS or BLOCKED. Do not start a step whose prerequisite is BLOCKED.

### 30.0 Blend-mode XML confirmation and baseline (`blend-mode-baseline`)

- Produce or obtain a real xLights `2026.03` XSQ with each candidate blend mode (Mask, Unmask, Reveal 1-on-2, Reveal 2-on-1, Average, Subtractive, Shadow 1-on-2, Shadow 2-on-1) applied between two known layers; check it into fixtures and confirm the exact XML key/value per mode.
- Measure baseline blend-mode usage in current generator output (expect ~100% default) and current effect-family concentration per section on the existing fixture songs, for later comparison.
- Gate: every blend mode used in later steps has a confirmed fixture; modes without confirmation are recorded as excluded, not silently skipped.

### 30.1 Blend-mode mining and selection (`blend-mode-mining`)

- Extend corpus mining (Phase 20/25 pipeline) to record the blend mode present between adjacent layers in mined `MultiLayerTemplate`s, alongside the effect types already captured.
- Build a compatibility signal from mined data: which blend modes commonly pair with which effect-type pairs and read well (not muddy) — reuse the existing brightness/contrast catalog rather than inventing a new quality metric.
- Add a `LayerBlendPlanner` (Choreography) that, given a Look's planned layer stack, selects a blend mode per adjacent layer pair from the confirmed set, informed by the mined compatibility signal, instead of leaving the pipeline's current default.
- Gate: mined templates carry blend-mode data; `LayerBlendPlanner` selects only confirmed modes; blend-mode diversity acceptance definition passes on the baseline fixture songs.

### 30.2 Hit+motion compounding (`hit-motion-compound`)

- Define a `HitMotionCompound` template type: a base hit effect (from existing accent/percussive cue placement) plus a movement overlay layer (value-curve-driven parameter sweep, or a motion-oriented effect such as a directional shockwave/ripple) scoped to the same cue's span.
- Wire `ChoreographyEngine`'s accent-cue placement (Phase 29's `accent` cue kind) to optionally emit a compound instead of a flat single-layer hit, chosen when the target model/group supports layered placement and the section's intent/energy favors it (not applied to every accent — keep this a choice, not a blanket rule, matching Phase 29's own guardrail against blanket requirements).
- Reuse `EffectVisualCharacterCatalog`/`EmphasisContrastPlanner` so the compound's brightness/contrast behavior is measured the same way existing accents are; this step changes layer composition, not the contrast rules.
- Gate: hit+motion compound integrity passes in exported XSQ tests; a section using compounds still passes existing contrast/hold/accent validation unchanged.

### 30.3 Reusable, recolorable effect presets (`reusable-presets`)

- Promote a subset of mined `MultiLayerTemplate`s (and any `HitMotionCompound`s from 30.2 worth keeping) to a persisted, named, palette-parameterized preset unit, stored via `IVisualStoryStore` alongside existing presets but distinct from `VisualBudgetPresetCatalog`'s generation-settings presets.
- A preset stores effect types, relative layer order, blend modes (from 30.1), and relative timing/value-curve shape — not baked-in absolute colors; applying a preset maps its color roles through the target palette.
- Expose preset save/apply through the generator's internal reuse path first (a later generation pass can stamp a known-good preset onto a new section); UI exposure in `ExemplarLibraryView` is a stretch goal for this phase, not a gate.
- Gate: preset reusability acceptance definition passes (round-trip structural diff on a different target/palette).

### 30.4 Effect-family concentration advisory (`family-concentration-advisory`)

- Extend `SectionTimelineValidator` (or add a sibling advisory pass alongside it, matching how Phase 22's contrast advisories are surfaced) to compute per-section effect-family concentration from placed layers, using `EffectVisualCharacterCatalog`'s existing family grouping.
- Surface violations as advisories (not hard failures), consulting the section's `Look.Intent` so a section intentionally built around one dominant family doesn't get flagged.
- Gate: effect-family concentration acceptance definition passes on the baseline fixture songs; a section with declared single-family intent is correctly exempted in a test.

### 30.5 End-to-end verification (`end-to-end-verification`)

- Using the existing synthetic timing/layout/scripted-AI-response harness (Phase 29 style), generate a full section sequence exercising: blend-mode variety across multiple Looks, at least one hit+motion compound, at least one applied reusable preset on a fresh target/palette, and a section that intentionally triggers (and one that correctly avoids) the family-concentration advisory.
- Rerun the baseline songs from 30.0 and record blend-mode diversity and family-concentration numbers against that baseline.
- Gate: all automated checks from 30.1–30.4 pass together on the same generated output; the human preview gate is recorded.

## Dependencies and guardrails

- 30.0 precedes everything (no blend mode ships unconfirmed).
- 30.1 precedes 30.3 (presets need blend-mode data to be worth saving).
- 30.2 and 30.4 can be developed independently of 30.1/30.3, then integrated in 30.5.
- Do not invent xLights XML settings for any blend mode, value curve, or effect parameter not confirmed against a real `2026.03` fixture.
- Keep all new behavior opt-in per Look/section, not a blanket requirement — matching Phase 29's explicit stance against mandatory density, coverage, or reuse.
- Onset-level voice isolation and a configurable sequencing-resolution (frame rate) knob are out of scope here; track as a follow-up phase once this phase's compounding/preset infrastructure exists to build on.

## Pipeline-integration follow-up (30.6-30.10)

A post-implementation review found that 30.1-30.4's four features (`LayerBlendPlanner`, `HitMotionCompound`, the Look-preset catalog, `EffectFamilyConcentrationAdvisor`) were each built, confirmed against fixtures, and covered by their own unit/integration tests -- but none is ever called by production code. `ChoreographyEngine`, `SectionTimelineCompiler`'s cue-admission path, `AiChoreographyWholeSongGenerator`, and `Program.cs`'s DI registrations never invoke any of the four, so a real generated sequence shows none of this phase's work: every Look still exports with `T_CHOICE_LayerMethod` defaulted to `"Normal"` (the Director LLM's own `"Bright"`/`"Additive"`/`"Max"` choices are silently discarded by `LookCompiler.ResolveConfirmedBlendMode` since none are confirmed XML values), no cue ever gets `SectionCue.Compound = true`, `LookPresetCatalog`/`JsonLookPresetStore` are not registered in `Program.cs` and are never read or written outside their own tests, and `EffectFamilyConcentrationAdvisor.Measure` is never invoked outside its own test file. These five steps wire all four into the real pipeline. See `C:\Users\kevmo\.claude\plans\typed-stirring-nest.md` for the full exploration/approach detail this summary is drawn from.

### 30.6 Wire `LayerBlendPlanner` into `ChoreographyEngine` (`pipeline-blend-mode-wiring`)

- Add `MultiLayerTemplateCatalog? MultiLayerTemplates = null` to `ChoreographyEngine.Input`.
- In `CompileOneWindow`, after resolving `look` and before `LookCompiler.Compile`, run `look = LayerBlendPlanner.Plan(look, templates, brightness)` whenever both `Input.MultiLayerTemplates` and `Input.BrightnessCatalog` are supplied; no-op (byte-identical to today) otherwise.
- `AiChoreographyWholeSongGenerator` threads its already-loaded `knowledge.TemplatesOrEmpty`/brightness catalog into `ChoreographyEngine.Input` -- no new catalog loading.
- Gate: blend-mode diversity acceptance definition (30.1) passes when exercised through the real `AiChoreographyWholeSongGenerator` entry point, not just a manual `LayerBlendPlanner.Plan` call from a test.

### 30.7 Deterministic hit+motion-compound selection (`pipeline-hit-motion-selection`)

- Add a pure `HitMotionCompoundSelector` that flips `SectionCue.Compound = true` on a bounded subset of resolved accent cues (`Kind == Accent`, `Intensity` in `{Bright, Full}`, `Role == "subject"`), capped per section so this stays a choice, not a blanket rule.
- Call it from `ChoreographyEngine.Compile` where resolved cue windows are admitted, gated behind a new opt-in `Input.EnableHitMotionCompounding = false` default, flipped on only for `AiChoreographyWholeSongGenerator`'s production call so existing fixture tests are unaffected unless they opt in.
- `SectionTimelineCompiler`'s existing `cue.Compound` branches need no change -- they already do the right thing once a cue carries `Compound = true`.
- Gate: hit+motion compound integrity acceptance definition (30.2) passes when exercised through the real generator entry point.
- **Status (implemented, not yet enabled by default):** `HitMotionCompoundSelector` and the `Input.EnableHitMotionCompounding` plumbing are real and unit-tested, but `ChoreographyGenerationPipeline`'s own production call leaves the flag `false`. Turning it `true` regressed `Phase29TimelineGenerationTests.WholeSongExportsSharedChorusAndPreservesOwnershipThroughReloadAndRepair`'s "disabled"/"wrong-kind"/"legacy" corpus variants: those fall back to `ChorusSignature.Apply`'s own synthesized hold/accent cue, which happens to satisfy the selector's (Accent, Bright/Full, subject) criteria, and compounding it forces the duty cycle to 1 (full window) instead of the cue's own intensity-tier duty, changing a real exported effect's duration. Narrowing the heuristic (e.g. excluding signature-/adaptation-synthesized cues) and a human preview pass are needed before flipping this on for every real generation -- tracked as a follow-up, not forced in.

### 30.8 Register and wire the reusable Look-preset catalog (`pipeline-preset-wiring`) -- DONE

- Register `ILookPresetStore`/`JsonLookPresetStore` (directory `<appData>/ai-choreography/look-presets`) and `LookPresetCatalog` in `Program.cs`'s `RegisterServices`, matching the existing `JsonCustomPromptStore` registration pattern. -- done.
- Thread an optional `LookPresetCatalog?` into `AiChoreographyWholeSongGenerator`. Save path: promote a qualifying hero Look (2+ layers) to a named preset after a successful compile (`SaveHeroLookPresetsAsync`). -- done.
- Apply path -- done: `AiChoreographyWholeSongGenerator` threads its `LookPresetCatalog?` into `ChoreographyGenerationPipeline`'s constructor, which calls a new `ApplyLookPresetsAsync` step ONCE, right after the Show Bible is obtained and before the (synchronous) compile retry loop -- `CompileSectionScripts` itself stays fully synchronous and untouched. For every Look the Director authored, a saved preset whose name contains that Look's own sanitized `Intent` text (same literal/grounded substring convention `EffectFamilyConcentrationAdvisor`'s own intent match already uses -- no fuzzy matching) is stamped onto that Look's existing id/name/intent/energy ceiling/palette/subject/target via `LookPresetCatalog.ApplyAsync`, preserving every other declared field (`Uniqueness`, `AllowedSectionKinds`, etc.). Opt-in/best-effort throughout: a null catalog, no saved presets, no matching intent, or any apply failure leaves the Director-authored Look untouched.
- Gate: preset reusability acceptance definition (30.3) passes end-to-end through the real pipeline -- a second real generation run reuses at least one preset saved by a prior run. Confirmed by `Phase30LookPresetApplyPipelineTests.GenerateAsync_SavedPresetMatchesFreshLookIntent_StampsPresetStructureOntoFreshLook`: a preset saved from a (simulated) first generation's hero Look is picked up and stamped onto a second, real `ChoreographyGenerationPipeline.GenerateAsync` run's freshly-authored Look sharing the same declared intent, confirmed structurally (layer count/roles/blends/duties) through the real pipeline entry point, not a hand-built catalog call.

### 30.9 Wire `EffectFamilyConcentrationAdvisor` into `ChoreographyEngine` (`pipeline-family-concentration-wiring`)

- Add `IReadOnlyList<EffectFamilyConcentrationFinding>? FamilyConcentration = null` to `ChoreographyEngine.Result`, mirroring `EmphasisContrast`.
- After the existing emphasis-contrast block, call `EffectFamilyConcentrationAdvisor.Measure(...)` per section assignment using `EffectFamilyIndexLoader.Load()` (already-embedded data, no new plumbing), accumulating findings onto `Result.FamilyConcentration`.
- Log an aggregate count in `ChoreographyGenerationPipeline` alongside the existing violation-count log line; never route into the `RevisionDirectiveDeriver` repair filters -- stays permanently advisory, matching `RuleContrastAtEmphasis`.
- Gate: effect-family concentration acceptance definition (30.4) passes when exercised through the real pipeline.

### 30.10 End-to-end pipeline-integration re-verification (`pipeline-integration-verification`)

- Re-run the 30.5 end-to-end scenario through the real production entry points (`AiChoreographyWholeSongGenerator` as registered in `Program.cs`), confirming all four features fire together on a real generated song.
- Record `implementation-evidence/PHASE-30-PIPELINE-INTEGRATION.md` with PASS/BLOCKED per sub-step and a re-recorded human preview gate in xLights `2026.03`.
- Gate: all of 30.6-30.9 pass together on the same generated output, through the app's real DI-composed pipeline, not a test harness standing in for it.
