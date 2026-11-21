# Phase 30 step 30.2 — Hit+motion compounding (`hit-motion-compound`)

**Result: PASS.**

## What this step had to establish

Per the build plan: define a `HitMotionCompound` template type pairing an accent cue's own base hit
effect with a second, independent movement-overlay layer scoped to the identical cue span; wire
`ChoreographyEngine`'s accent-cue placement (via `SectionTimelineCompiler`, which is where accent cues are
actually compiled) to optionally emit a compound instead of a flat single-layer hit — opt-in, per cue,
never a blanket rule; reuse `EffectVisualCharacterCatalog`/`EmphasisContrastPlanner` rather than inventing
new contrast rules; and pass the acceptance definition's own wording: *"a compound cue exports as exactly
two layers over its cue's resolved span (base hit effect + movement overlay), both within frame tolerance
(±1 frame) of the cue's declared bounds, and the movement layer's value-curve or effect parameters differ
from the pipeline's flat/static default for that effect type. A section using compounds must still pass
existing contrast/hold/accent validation unchanged."*

Hard constraints carried forward from 30.0/30.1 and never relaxed here: never invent a `T_CHOICE_LayerMethod`
value outside `ConfirmedLayerBlendModeCatalog`; never invent an xLights value-curve type, effect, or
parameter not confirmed against a real `.xsq`/real `effectmetadata`; keep every new behavior opt-in per
cue/section.

## 1. Why `ChoreographyEngine` itself was not touched

`ChoreographyEngine.cs` does not place accent cues directly — it only resolves section windows and
delegates per-section compilation to `SectionTimelineCompiler.CompileResolved` (for sections with a real
`SectionTimeline`) or plain `LookCompiler.Compile` (for sections with none). Phase 29's own accent-cue
placement logic — anchor resolution, duty-cycle application, the single `EmitAt` call per cue/target — all
lives in `SectionTimelineCompiler.cs`. That is the one real place a "flat single-layer hit" is actually
produced, so that is where this step's opt-in compound path is wired, exactly as the build plan's own
step-3 prerequisite ("Read ... ChoreographyEngine's existing accent-cue compilation path first") concluded
after reading it.

## 2. The confirmed movement shape — not guessed

Per the task's own instruction to check existing value-curve support before confirming anything new:
`EffectValueCurve`/`EffectSettingsSerializer`'s `C_VALUECURVE_*`/`E_VALUECURVE_*` serialization (Phase 4)
already works end to end — confirmed directly by the existing, unmodified
`Phase04VocabularyAwareExportTests.ExportAsync_WithVocabulary_SerializesValueCurve_BoundToTimingTrack`,
which serializes a real `"Ramp"` curve (`Type=Ramp`) bound via `TT=`/`FT=` and round-trips through a real
exported `.xsq`. **No new XsqExporter/EffectSettingsSerializer wiring was needed for this step** — unlike
30.1, which had to add new blend-mode wiring into `LookCompiler`/`XsqExporter` because `BlendMode` was
previously disclosed as "IR-only, never serialized." Value curves had no such gap; `LookCompiler.CompileBoundLayer`
already builds `Xlights.EffectValueCurve` instances from a Look's own declared curve bindings and they
already reach export unchanged. This step only needed to build one more `EffectInstance` carrying a
`ValueCurves` entry, using the exact same confirmed mechanism.

What still needed confirming was the **motion effect and parameter** themselves, never shipped before:

- **Effect**: `"Shockwave"` — already a real, registered xLights effect in this codebase (used by mined
  `MultiLayerTemplate`s in `multilayer-tree-matrix-templates.json`, catalogued in
  `effect-visual-characters.json`, and already listed in `EmissionStrategySelector.TimingTrackBoundEffects`
  as one of architecture §6's seven real timing-track-bound effects, verified in an earlier phase against
  the real installed `effectmetadata` property ids). Not a new effect name — reused as-is.
- **Parameter**: `Shockwave_End_Radius` (label "Radius2") — confirmed **directly against the installed
  schema**, `C:\Program Files\xLights\effectmetadata\Shockwave.json`:
  ```json
  {
      "id": "Shockwave_End_Radius", "label": "Radius2", "type": "int", "controlType": "slider",
      "default": 10, "min": 0, "max": 750, "valueCurve": true, "lockable": true,
      "description": "Ending radius of the expanding ring at the end of each cycle."
  }
  ```
  `"valueCurve": true` with real `min`/`max` (0/750) is the same machine-readable confirmation method
  30.0 used for its own `Blending.json` byte-diff check — this is a real, typed JSON schema property (not
  a custom/undocumented control like `T_CHOICE_LayerMethod` was), so there is no "schema doesn't enumerate
  it" gap here at all.
  Checked into the repo, byte-identical to the real install (confirmed via `diff`, zero output):
  `tests/XlightsAI.Studio.Xlights.Tests/Fixtures/effectmetadata/Shockwave.json`. Same installed xLights
  build as 30.0/30.1 recorded (`2026.17`, per
  `%AppData%\xLights\Logs\xLights_spdlog.log: Version: 2026.17`).
- **Real-naming-convention derivation, not guessed**: `EffectVocabularyBuilder.ConvertScalarProperty`/
  `BuildSettingKey`/`BuildValueCurveSettingKey` (unmodified, pre-existing code) deterministically derive
  `SettingKey = "E_SLIDER_Shockwave_End_Radius"` and `ValueCurveSettingKey = "E_VALUECURVE_Shockwave_End_Radius"`
  from this one JSON property — confirmed directly by running the real `EffectVocabularyBuilder` against the
  real installed `effectmetadata` directory in `HitMotionCompoundTests.TryBuild_RealVocabulary_ReturnsShockwaveWithRealRampAcrossConfirmedMinMax`
  (below), not hand-derived/assumed.

`src/XlightsAI.Studio.Choreography/HitMotionCompound.cs` (new) carries exactly these three confirmed
constants (`MovementEffect = "Shockwave"`, `MovementValueCurveParameterKey = "E_SLIDER_Shockwave_End_Radius"`,
`MovementCurveType = "Ramp"`) and nothing else — this is the ONE confirmed motion shape 30.2 ships, per
the build plan's own "a motion-oriented effect such as a directional shockwave/ripple" wording.

## 2a. `TryBuild`: resolve-or-skip, never invent

`HitMotionCompound.TryBuild(target, start, end, baseLayerZ, settings, sourceAnnotation, cueTraceId)`:

1. Resolves `"Shockwave"` to a registered recipe id via the existing `LookCompiler.RecipeIdFor` helper; if
   the supplied `settings.RecipeCatalog` has no such recipe registered (e.g. a portable/synthetic test
   vocabulary with no real Shockwave entry), returns `null` — skipped, never invented, same discipline
   every other Choreography compiler path already follows for an unresolvable reference.
2. Looks up `MovementValueCurveParameterKey` in `settings.Vocabulary`; if it's absent or
   `SupportsValueCurve` is `false` (e.g. a portable test vocabulary that declares "Shockwave" with no real
   properties at all), returns `null`.
3. Builds the real `EffectInstance` via the same `settings.RecipeRegistry.CreateVerified(...)` every other
   compiled effect in this codebase uses, with:
   - **Identical `[start, end)` window** as the base hit — never shortened/different, per the acceptance
     definition's own "scoped to the same cue's span" wording.
   - **`Layer = -baseLayerZ`** — the base hit's own cue layer (`placement.Index + 1`, always `>= 1` per
     `SectionTimelineCompiler`'s existing convention), negated. Every real base-Look layer and every real
     cue layer in this codebase is always `>= 0` (`baseLayerOffset`/`cueLayerCount` in
     `SectionTimelineCompiler.CompileResolved` only ever shift upward from 1), so a negative layer can
     never collide with any of them. `XsqExporter`'s own per-target `GroupBy(e => e.Layer)` (confirmed by
     reading its export loop directly) only needs distinctness, never contiguity or a particular sign, to
     produce a separate real `<EffectLayer>` — confirmed empirically below, not just by reading the code.
   - **A real `"Ramp"` value curve** on `MovementValueCurveParameterKey`, spanning the confirmed real
     `Min`/`Max` (0/750) with `P1 = Min`, `P2 = Max` — a real, non-flat ramp across the effect's own full
     confirmed range, which by construction can never equal the effect's own flat/static default (a single
     fixed `10`, per the metadata's own `"default": 10`), satisfying the acceptance definition's
     "differ from the pipeline's flat/static default" wording directly, not by assertion.

## 3. Wiring into `SectionTimelineCompiler` — opt-in, per cue

`src/XlightsAI.Studio.Choreography/SectionTimeline.cs`: `SectionCue` gained one new, additive, default-`false`
field: `Compound`. Every existing construction site (hand-authored tests, any persisted JSON) keeps
compiling/deserializing identically. Only takes effect for a `Kind == CueKind.Accent` cue — same
silent-no-op convention `HandoffFromProps` already uses for a non-accent cue — so declaring it on a Hold or
Takeover cue is simply inert, never an error, matching this phase's own "keep all new behavior opt-in,
never a blanket requirement" guardrail at the data-shape level (a cue must explicitly declare it; nothing
in `ChoreographyEngine`/`SectionTimelineCompiler` ever turns it on automatically).

`src/XlightsAI.Studio.Choreography/SectionTimelineCompiler.cs`, two narrow changes inside `CompileResolved`/`EmitAt`:

1. The existing per-cue `duty` computation gained one additional condition: `&& !cue.Compound`. A compound
   cue's base hit always spans its own full resolved window (duty forced to `1`, never intensity-tier-shortened)
   so the movement overlay built in step 2 below can declare the EXACT same `[start, end)` span as the base
   hit — otherwise the two would differ by the duty-cycle trim and the "both within frame tolerance of the
   cue's declared bounds" gate would not hold for the base hit itself.
2. `EmitAt` (the one real place every accent-cue effect in this codebase is actually constructed), right
   after building and adding the base hit: `if (cue.Kind == CueKind.Accent && cue.Compound && HitMotionCompound.TryBuild(...) is { } movementOverlay) overlayEffects.Add(movementOverlay);` — skips silently when
   `TryBuild` returns `null` (unavailable vocabulary/recipe), same resolve-or-skip discipline as the rest
   of this file.

Not wired into the multi-target "divergent ends" loop's handoff-pair path (`EmitHandoffPair`) — a compound
cue with `HandoffFromProps` set is simply not supported by this step (an edge case the build plan's own
three listed bullets do not call for); every single/multi-target `EmitAt`/`EmitSingle` call site (the
common case) is covered.

## 4. Reusing `EffectVisualCharacterCatalog`/`EmphasisContrastPlanner` — not re-implementing contrast rules

Per the build plan's own instruction ("this step changes layer composition, not the contrast rules"):
`EmphasisContrastPlanner.ApplyTimelineContrast` (called unconditionally at the end of
`SectionTimelineCompiler.CompileResolved` whenever a brightness catalog is supplied) groups and reasons
about accent cues by their own `Window.StartTicks`/`Cue.Intensity` — never by how many `EffectInstance`s a
given cue happened to produce. Adding a second `EffectInstance` (the movement overlay) to `overlayEffects`
changes nothing about that grouping; the pass is untouched and was not modified. "Shockwave" itself already
has two real `BrightnessTier` entries in `effect-visual-characters.json` (seeded before this phase), so a
compound's own movement overlay is measurable by this exact same pre-existing catalog/planner the moment a
caller supplies one — confirmed directly by `Validate_HoldPlusCompoundAccent_PassesTheSameAsHoldPlusPlainAccent`
(below), which is the direct test the build plan's own step-4 bullet calls for ("write a test proving this
directly, not just assuming it").

## 5. Tests written and run

**`tests/XlightsAI.Studio.Choreography.Tests/HitMotionCompoundTests.cs`** (new, 4 tests) — isolated,
direct tests of `HitMotionCompound.TryBuild`:
- `TryBuild_RealVocabulary_ReturnsShockwaveWithRealRampAcrossConfirmedMinMax` (gated on
  `RealXLightsFixture.IsAvailable`): builds the real vocabulary/recipes from the real installed
  `effectmetadata` directory, confirms the returned effect's recipe resolves to the real `"Shockwave"`
  xLights effect name, its `Layer` is the negated base layer, its window is unchanged, and its one value
  curve is `Ramp` from the real confirmed `0` to `750`.
- `TryBuild_VocabularyWithNoShockwaveProperties_ReturnsNull_NeverInvents`: a portable vocabulary declaring
  `"Shockwave"` with zero real properties — confirms the "unavailable parameter" skip path.
- `TryBuild_VocabularyWithNoShockwaveEffectAtAll_ReturnsNull_NeverInvents`: a vocabulary with no
  `"Shockwave"` entry at all — confirms the "unregistered recipe" skip path.
- `TryBuild_EmptyWindow_ReturnsNull`: a zero-width `[start, end)` — confirms the guard against an
  unrepresentable span.

**`tests/XlightsAI.Studio.Choreography.Tests/SectionTimelineCompilerTests.cs`** (+4 tests):
- `Compile_CompoundAccentCue_ExportsExactlyTwoLayersWithinFrameToleranceAndARealRampCurve` (gated on
  `RealXLightsFixture.IsAvailable`) — **the direct gate test**, exercising the full real path: a real
  `Look`/`SectionTimeline` with one `Compound: true` accent cue on a real transient anchor, compiled via
  the unmodified `SectionTimelineCompiler.Compile`, then **exported to a real `.xsq` and read back** via
  `XsqExporter`/`XsqStructuralReader` (not just inspected at the IR level):
  - Compiled IR: exactly 2 `EffectInstance`s on the target, one with `Layer > 0` (the base hit) and one
    with `Layer < 0` (the movement overlay) at `-baseHit.Layer` exactly; both spans within one real frame
    (50ms, expressed as 500,000 ticks) of each other; the movement overlay's one value curve has
    `P1 != P2` (a real ramp, never a flat default).
  - **Exported XSQ** (re-parsed, not asserted against the in-memory IR): the target's `<Element>` carries
    **exactly 2** `<Effect>` placements across its `<EffectLayer>`s, one named `"On"` and one named
    `"Shockwave"`, both within one real exported frame (`xsq.FrameIntervalMs`) of each other; the real
    `EffectDB` contains a `E_VALUECURVE_Shockwave_End_Radius` entry — i.e. the value curve genuinely
    reached the real exported file, not just the compiled IR.
- `Compile_NonCompoundAccentCue_CompilesToExactlyOneLayer` (gated) — confirms the opt-in default: a cue
  that does not declare `Compound` still compiles to exactly one effect, unchanged from every pre-existing
  accent cue.
- `Validate_HoldPlusCompoundAccent_PassesTheSameAsHoldPlusPlainAccent` — **the direct test the build plan's
  own gate calls for**: the identical Hold+Accent shape run through the real, unmodified
  `SectionTimelineValidator.Validate`, once with a plain accent cue and once with the same cue declaring
  `Compound: true` — both return an empty error list. Proves a section using a compound still passes
  existing contrast/hold/accent validation, not merely asserted.

## 6. Fixture checked in

`tests/XlightsAI.Studio.Xlights.Tests/Fixtures/effectmetadata/Shockwave.json` — byte-identical copy
(confirmed via `diff`, zero output) of the real installed `C:\Program Files\xLights\effectmetadata\Shockwave.json`.
Verified it does not change any existing Xlights.Tests count-based assertion (none of that project's tests
assert an exact `EffectVocabularyCatalog.Effects.Count`, unlike `XlightsAI.Studio.Knowledge.Tests`'s own
fixture set, which is why this file was added to the Xlights.Tests fixture directory rather than the
Knowledge.Tests one).

## 7. Full regression run

- `XlightsAI.Studio.Choreography.Tests`: **247/247 pass** (was 239 at the end of step 30.1; +8 new: 4
  `HitMotionCompoundTests` + 4 `SectionTimelineCompilerTests` additions).
- `XlightsAI.Studio.Xlights.Tests`: **123/123 pass** (unchanged from 30.1 — the new `Shockwave.json`
  fixture file does not affect any existing test).
- `XlightsAI.Studio.Director.Tests` (non-integration): **317/317 pass** (identical to 30.0/30.1's own
  recorded baseline — no regression from this step).
- `XlightsAI.Studio.Pipeline.Tests`: **109/109 pass** (this step did not touch this project).
- `XlightsAI.Studio.Knowledge.Tests`: **111/111 pass** (this step did not touch this project).

## Gate check (per the build plan's own wording)

> "Gate: hit+motion compound integrity passes in exported XSQ tests; a section using compounds still
> passes existing contrast/hold/accent validation unchanged."

Both are PASS, each backed by a direct test against real exported/re-parsed output or the real,
unmodified validator — not asserted from the IR alone, not assumed. Step 30.3 (reusable, recolorable effect
presets) may proceed in a future session — not started here, per the task's own instruction to stop after
30.2's gate is verifiably passed.
