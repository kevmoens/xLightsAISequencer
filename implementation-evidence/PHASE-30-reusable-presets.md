# Phase 30 step 30.3 — Reusable, recolorable effect presets (`reusable-presets`)

**Result: PASS.**

## What this step had to establish

Per the build plan: promote a subset of mined `MultiLayerTemplate`s (and any `HitMotionCompound`s from
30.2 worth keeping) to a persisted, named, palette-parameterized preset unit, distinct from
`VisualBudgetPresetCatalog`'s own generation-settings presets; a preset stores effect types, relative layer
order, blend modes (30.1) and relative timing/value-curve shape — never a baked-in absolute color;
preset save/apply is exposed through the generator's own internal reuse path first (UI in
`ExemplarLibraryView` is an explicit stretch goal, not a gate); and the acceptance definition's own gate —
a round-trip test saving a preset from a generated Look, applying it to a different model/group with a
different palette, and diffing the two placements' structure (ignoring color) — passes.

Hard constraints carried forward from 30.0/30.1/30.2 and never relaxed here: a preset's own stored blend
mode must only ever be one of `ConfirmedLayerBlendModeCatalog.Modes` (plus `"Normal"`); a preset's own
stored value curves/effect parameters must only ever reuse already-confirmed shapes (30.1's blend modes,
30.2's `Shockwave`/`Shockwave_End_Radius`/`"Ramp"` confirmation); every new behavior stays opt-in.

## 1. Why a new preset type, and where it lives

Read first, per the task's own instruction: `MultiLayerTemplate.cs`/`MultiLayerTemplateCatalogLoader.cs`
(Knowledge), `IVisualStoryStore.cs`/`VisualBudgetPresetCatalog.cs`/`IVisualBudgetPresetStore.cs`/
`JsonVisualBudgetPresetStore.cs` (Composition).

`VisualBudgetPresetCatalog` persists a named `VisualBudget` (density/coverage *generation knobs*) — an
entirely different kind of "preset" from what 30.3 asks for (a reusable *effect composition*). `MultiLayerTemplate`
is close in spirit (effect types + relative order + duty/cadence + 30.1's own `BlendModeToPrevious` field)
but is pre-compile research data read by a Director prompt, not something a caller saves from and applies
onto a specific, already-compiled `Look`.

The build plan's own wording — "stored via `IVisualStoryStore` alongside existing presets" — does not
literally fit this codebase's project boundaries: `IVisualStoryStore`/`VisualBudgetPresetCatalog` live in
`XlightsAI.Studio.Composition`, which does not (and, by its own `.csproj` comment, should not) reference
`XlightsAI.Studio.Choreography` — `Composition`'s own "Choreography" types (`ChoreographyCandidate`,
`ChoreographyCompiler`) are a completely separate, role-precedence-based mechanism (confirmed by reading
`Composition/ChoreographyCompiler.cs` in full) with no `Look`/`LookLayer` concept at all. A `LookPreset`
necessarily deals in `Look`/`LookLayer` (`Choreography`'s own types), so it cannot live in `Composition`
without either inventing a second, parallel Look-like shape there (worse) or adding a new cross-project
dependency Composition has never needed before, just for this feature.

Instead, this step followed the build plan's own intent (a persisted, named store mirroring the existing
preset infrastructure's conventions, reachable from "the generator's internal reuse path") rather than its
literal project name:

- **`XlightsAI.Studio.Choreography/LookPreset.cs`** (new, pure, no I/O — this project's own `.csproj`
  comment: "this project is itself a pure compiler library"): `LookPresetLayer`/`LookPreset` records plus
  `LookPresetConverter.FromLook`/`.Apply` — the structural capture/re-expand logic itself.
- **`XlightsAI.Studio.Director`** (new: `ILookPresetStore.cs`, `JsonLookPresetStore.cs`, `LookPresetCatalog.cs`,
  `LookPresetCorruptException.cs`) — `Director` is the one project that already references `Choreography`
  *and* already does real file I/O with this exact atomic-write/name-validated/monotonic-version pattern
  (`JsonShowBibleStore`, and `Composition.VisualBudgetPresetCatalog`'s own convention, mirrored here
  verbatim) — and it is literally "the generator," i.e. exactly the "generator's internal reuse path" the
  build plan's own step-3 bullet asks this to be exposed through first. This choice and its reasoning are
  recorded here, not silently substituted.

## 2. What a `LookPreset` stores (and deliberately does not)

`LookPresetLayer` mirrors every structural field of `LookLayer` — `Role`, `Effect`, `Z`, `Blend`, `Duty`,
`Timescale`, `Params`, `Curves` — and drops exactly one: `Props` (the per-layer target reference), the one
field that is specific to *where* the source Look happened to be compiled. `LookPreset` carries `Motion`
and the ordered layer list, plus `SourceLookId`/`SourceNote`/`Version` for traceability — never
`Look.PaletteRef`, never `Look.Subject`, never a literal color.

This is sufficient because of a real, confirmed fact about this codebase's own compiled IR (checked by
reading `LookCompiler.CompileBoundLayer`/`CompileDiscreteLayer` directly): every compiled
`EffectInstance.PaletteIndex` is unconditionally `0`, regardless of layer — a Look's color is carried
entirely by its single declared `Look.PaletteRef`, resolved once per compile against the caller's own
`LookCompilerSettings.PaletteLibrary`. "Mapping color roles through the target palette" is therefore not a
new mechanism to invent — it already IS "compile the applied Look against a different
`ColorPaletteDefinition`," which `LookCompiler.Compile` already does unchanged.

`LookPresetConverter.Apply` re-targets every layer onto a single supplied `targetProps`/`subject` pair —
the same single-target convention every Look in this codebase (hand-authored or generated) already
follows; there is no existing multi-target-per-Look shape to preserve.

## 3. Promoting a 30.2 `HitMotionCompound` into preset form

30.2's `HitMotionCompound.TryBuild` constructs its movement overlay directly as compiled IR (bypassing
`LookLayer`/`LookCompiler`) because it is scoped to one `SectionCue`'s resolved span, not a whole-Look
layer. A preset operates one level up, on `Look`/`LookLayer` — so "promoting a HitMotionCompound worth
keeping" is demonstrated here by composing the SAME three confirmed constants 30.2 shipped
(`HitMotionCompound.MovementEffect` = `"Shockwave"`, `MovementValueCurveParameterKey` =
`"E_SLIDER_Shockwave_End_Radius"`, `MovementCurveType` = `"Ramp"`) into an ordinary second `LookLayer`
carrying a `LookCurveBinding(MovementCurveType)` on that key — never a new confirmed value, never a second
independently-invented motion family. `LookPresetTests.FromLook_HitMotionCompoundShapedLook_RoundTripsTheConfirmedMovementLayerUnchanged`
and the gate test below both exercise this directly: the same confirmed Shockwave/Ramp shape survives a
save→apply round trip through the general Look/preset path, not just through 30.2's own cue-compound path.

## 4. Tests written and run

**`tests/XlightsAI.Studio.Choreography.Tests/LookPresetTests.cs`** (new, 4 tests, pure — no real xLights
install needed): `FromLook` captures every layer field (role/effect/z/blend/duty/timescale/params/curves)
and drops `Props`; an empty-layer Look throws rather than saving a useless preset; `Apply` retargets every
layer to the new `Props`/palette while leaving effect/blend/duty/timescale/role identical to the source;
and the HitMotionCompound-shaped round trip above. **4/4 pass.**

**`tests/XlightsAI.Studio.Director.Tests/LookPresetCatalogTests.cs`** (new, 8 tests): real file-backed
save→load round trip (`JsonLookPresetStore` via a real temp directory); re-saving the same name increments
`Version` (0 → 1, matching `VisualBudgetPresetCatalog`'s own convention); `GetAsync`/`ApplyAsync` on an
unknown name return `null`, never fabricated; `ApplyAsync` on a real saved preset retargets/repalettes
while keeping effect/duty identical; `ListNamesAsync` is ordinal-sorted; an invalid name (empty, or
containing `/`) throws before any file touches disk. **8/8 pass.**

**`tests/XlightsAI.Studio.Director.Tests/Phase30ReusablePresetRoundTripTests.cs`** (new) — **the direct gate
test**, run against the acceptance definition's own wording verbatim (gated on `RealXLightsFixture.IsAvailable`,
same discipline as every other real-vocabulary test in this codebase):

1. **Save**: a "generated" `Look` (two layers — layer 1 `"On"`, 30.1-confirmed blend `"2 is Mask"`, `Duty
   0.5` on a `"Bars"` track, i.e. real Mode B discrete timing shaped by duty, not a flat full-span effect;
   layer 2 the 30.2-shaped `"Shockwave"` movement layer with a real `"Ramp"` curve) is saved as a preset
   named `"hit-and-mask"` via `LookPresetCatalog.SaveAsync`, to a real temp-directory JSON file.
2. **Apply**: `LookPresetCatalog.ApplyAsync` loads that same real file back and re-expands it onto a
   *different* target (`"Arch"` instead of `"Tree"`) and a *different* palette (`"ice-blue"` instead of
   `"warm-gold"`).
3. **Compile**: both the original and the applied `Look` are compiled via the real, unmodified
   `LookCompiler.Compile`, against the exact same `SectionMusicContext`/`BarGrid` (so only target/palette
   can differ), then merged via `ChoreographyIR.Merge` (the same mechanism every multi-section whole-song
   compile already uses to keep two palettes' own indices distinct and non-colliding).
4. **Export + re-parse**: the merged IR is exported together into one real `.xsq` via `XsqExporter` and
   read back via `XsqStructuralReader` — the diff below runs against the real exported/re-parsed file, not
   in-memory IR alone.
5. **Diff** (per target's own flattened effect-placement list, in document order):
   - Same count of placements for `"Tree"` and `"Arch"` (≥3: the Mode B discrete `"On"` windows plus the
     one Mode A `"Shockwave"` movement effect).
   - Position-by-position: same effect name, same `StartTimeMs`/`EndTimeMs` (relative timing/layer order
     unchanged — both compiled against the identical section/bar grid).
   - Position-by-position: the referenced `EffectDB` settings-string text is **exactly equal** between
     `"Tree"`'s and `"Arch"`'s corresponding placement — this is where an unconfirmed-value regression or a
     palette leak into effect settings would show up, and it does not (color is carried only by a separate
     palette-table index, never embedded in an effect's own settings string).
   - Position-by-position: the two placements reference **different** `ColorPalette` table entries, and
     those entries' own raw text differ — i.e. only the color actually differs.
   - The confirmed shapes both genuinely reached the real file: `T_CHOICE_LayerMethod=2 is Mask` and
     `E_VALUECURVE_Shockwave_End_Radius=` both appear in the real exported `EffectDB`.

**Result: all assertions pass.** 1/1 (ran against the real install present on this machine — not skipped).

## 5. Full regression run

- `XlightsAI.Studio.Choreography.Tests`: **251/251 pass** (was 247 at the end of step 30.2; +4 new
  `LookPresetTests`).
- `XlightsAI.Studio.Director.Tests` (full, `Category!=Integration` — the new gate test is not
  integration-tagged, same convention as `LookCompilerTests`'s own real-fixture tests): **326/326 pass**
  (was 317 at 30.2; +9 new: 8 `LookPresetCatalogTests` + 1 `Phase30ReusablePresetRoundTripTests`).
- `XlightsAI.Studio.Xlights.Tests`: **123/123 pass** (unchanged from 30.2 — this project was not touched).
- `XlightsAI.Studio.Pipeline.Tests`: **109/109 pass** (unchanged from 30.2 — not touched).
- `XlightsAI.Studio.Knowledge.Tests`: not re-run — this step touched neither `Knowledge`'s source nor its
  tests (no changes to `MultiLayerTemplate`/`ConfirmedLayerBlendModes`/`BlendModeCompatibilityIndex`); its
  own 30.2-recorded 111/111 stands unchanged.

## 6. Scope note: no UI exposure

Per the build plan's own wording ("UI exposure in `ExemplarLibraryView` is a stretch goal for this phase,
not a gate; do not build UI unless it's trivial, and do not treat it as required for PASS") — no UI was
added. Save/apply is exposed purely through `LookPresetCatalog` in `Director`, the generator's own internal
reuse path, exactly as instructed.

## Gate check (per the build plan's own wording)

> "Gate: preset reusability acceptance definition passes (round-trip structural diff on a different
> target/palette)."

PASS — backed directly by `Phase30ReusablePresetRoundTripTests`, a real save→apply→compile→export→re-parse
round trip, not an in-memory-only assertion. Step 30.4 (effect-family concentration advisory) may proceed
in a future session — not started here, per the task's own instruction to stop after 30.3's gate is
verifiably passed.
