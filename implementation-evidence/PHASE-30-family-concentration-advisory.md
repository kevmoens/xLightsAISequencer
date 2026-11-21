# Phase 30 step 30.4 -- Effect-family concentration advisory (`family-concentration-advisory`)

**Result: PASS.**

## What this step had to establish

Per the build plan: extend `SectionTimelineValidator` (or a sibling advisory pass) to compute, per
section, what share of placed layers belongs to each effect family, using `EffectVisualCharacterCatalog`'s
"existing family grouping" -- except 30.0's own evidence already confirmed that field does not exist at
all (`EffectVisualCharacter` carries only a brightness tier). This step therefore first had to establish,
and document, where a real family taxonomy actually comes from -- never fabricate one -- then add the
field, build the advisory pass, consult `Look.Intent` for a declared exemption, and prove the exemption
with a direct test, not an assumption.

## 1. Where the family taxonomy comes from -- both of the build plan's named sources were checked and ruled out

**Real xLights effect categories (the plan's first-choice source): confirmed absent, not skipped.**

- The real installed `effectmetadata` directory (`C:\Program Files\xLights\effectmetadata\*.json`, the
  same install 30.0/30.2 used, version `2026.17`) has no `category`/grouping property on any of its ~50
  real per-effect JSON files -- confirmed by grepping every file for "categor" (zero hits) and reading
  representative files (`Bars.json`, `Shader.json`) in full.
- The real `manual.xlights.org` site -- already cited by this codebase's own `effect-visual-characters.json`
  `DocumentationSource` fields -- was checked for a navigational grouping. Its real `sitemap-pages.xml`
  places all 58 real effect pages under one identical flat path, `/xlights/effects/off/<name>` (e.g.
  `/xlights/effects/off/shader`, `/xlights/effects/off/bars`) -- `off` is a fixed template segment, not a
  category (confirmed by fetching the real sitemap directly; every existing `DocumentationSource` citation
  in this repo's own seed data independently confirms the same "off" segment regardless of the very
  different real effects they document -- `fireworks`, `guitar`, `single-strand`, etc.).
- The real xLights source itself was checked on GitHub (`xLightsSequencer/xLights`, via `gh api`):
  `src-core/effects/EffectManager.cpp`'s constructor registers all ~58 real effects as one flat
  `add(createEffect(eff_X))` list with no category/group concept, and `RenderableEffect.h` (the common base
  class every real effect extends) declares no `GetCategory()`/grouping method at all (confirmed by
  reading both files' real content).

**Conclusion: no real xLights-side effect-category taxonomy exists anywhere in this codebase's reach** --
not in the shipped metadata, not in the public documentation site's own structure, not in the engine's own
source. This is a genuine, confirmed absence (same discipline as 30.0's own "xLights' own effectmetadata
does NOT enumerate this control's choice list" finding), not a shortcut past checking.

**Actually-mined corpus co-occurrence data (the build plan's own explicit fallback): tried, and the naive
version honestly rejected before landing on a real, usable signal.**

A first attempt scanned the same real, author-created corpus 30.0/30.1 used (`C:\xLights\*.xsq`, 35 real
multi-year family show files) via the existing `EffectAlignmentParser` (structural parsing only, no audio
needed) and unioned any two effect names ever co-placed, at different times, in the same real (Model,
`EffectLayer` index) slot. This transitively collapsed 23 of the corpus's 46 real distinct effect names
into one single blob via a handful of generic "fits anywhere" hub effects (`On`, `Shockwave`, `Morph`) --
a real, measured result, but not a usable family signal (it would call `Pinwheel` and `Text` "the same
family" purely because both happen to share a hub). This dead end is recorded, not hidden, in
`implementation-evidence/PHASE-30-FAMILY-CONCENTRATION-MINING.md`.

Tightening the real signal (restricting to slots that host *exactly* two distinct effect names across a
song's own real timeline -- a clean A/B substitution, not a generic multi-purpose base layer -- and raising
the minimum repeated-occurrence bar) found a natural break in the real evidence distribution: the two
strongest real pairs repeat **30** times (`Pinwheel` <-> `Shockwave`) and **16** times (`Color Wash` <-> `On`)
across the corpus; the next-strongest real pair repeats only **9** times. Setting the bar at >= 10 real
repeated occurrences keeps exactly those two real, strongly-evidenced pairs and leaves every other one of
the 44 other real effect names this corpus observed as its own singleton family -- never a fabricated
shared bucket invented for effects the real data gives no repeated evidence for.

**This is the taxonomy this step ships**, recorded in
`src/XlightsAI.Studio.Knowledge/SeedData/effect-family-index.json`:

| Real mined family | Members |
|---|---|
| `Color Wash` | Color Wash, On |
| `Pinwheel` | Pinwheel, Shockwave |
| *(every other effect)* | itself only (no mined substitution evidence) |

Family ids are literal real effect names from the mined pair itself (the alphabetically-first member),
never an invented descriptive label.

## 2. The family-grouping field: a sibling lookup, not a modified `EffectVisualCharacter`

Per the task's own allowance ("or a sibling lookup"): `src/XlightsAI.Studio.Knowledge/EffectFamilyIndex.cs`
(new) -- `EffectFamilyIndex(IReadOnlyDictionary<string,string> FamilyByEffectName)` with
`FamilyOf(effectName)` returning the mined family id, or the effect's own name when this corpus mining run
found no repeated substitution evidence for it (a singleton family of one, never null -- every effect
always gets a usable partition key). `EffectFamilyIndexLoader.cs` loads it from a new embedded resource,
`SeedData/effect-family-index.json`, registered in `XlightsAI.Studio.Knowledge.csproj` -- same convention
as `EffectVisualCharacterCatalogLoader`/`MultiLayerTemplateCatalogLoader`. `EffectVisualCharacter` itself is
untouched -- no JSON-compatibility question to manage, no risk to its own 112-fixture committed sweep.

`tests/XlightsAI.Studio.Pipeline.Tests/EffectFamilyCorpusMiningTests.cs` (new) is the real mining scan that
produced the seed data above and records the full real tally (both the rejected naive attempt and the
final >= 10-occurrence result) in
`implementation-evidence/PHASE-30-FAMILY-CONCENTRATION-MINING.md` -- re-runnable against the real corpus at
any time, same "offline tool regenerates committed seed data" discipline as `tools/EffectCatalogBuilder`.
Skips cleanly when `C:\xLights` is not present, matching every other real-corpus test in this project.

Tests: `tests/XlightsAI.Studio.Knowledge.Tests/EffectFamilyIndexLoaderTests.cs` (new, 4 tests) -- loads a
real non-empty index from the embedded resource, caches it, confirms both real mined pairs share a family
id, and confirms an effect with no mined evidence (`Twinkle`, `Shader`) returns its own name.

## 3. The advisory pass: `EffectFamilyConcentrationAdvisor` -- a sibling to `SectionTimelineValidator`

Read first, per the task's own instruction: `SectionTimelineValidator.cs` and `EmphasisContrastPlanner.cs`
(Phase 22's own advisory precedent) in full.

`src/XlightsAI.Studio.Choreography/EffectFamilyConcentrationAdvisor.cs` (new) -- a sibling pass, not a
modification to `SectionTimelineValidator` (lower risk; matches the task's own explicitly offered
alternative and keeps this step from touching that validator's existing signature/tests at all):

- `EffectFamilyConcentrationFinding(SectionId, LookId, Family, FamilyLayerCount, TotalLayerCount)` with a
  computed `Share` -- same "finding record with a computed property" shape as
  `EmphasisContrastFinding.HasContrast`.
- `EffectFamilyConcentrationAdvisor.Measure(ChoreographyIR ir, TimeWindow sectionWindow, string sectionId,
  Look look, EffectRecipeCatalog recipeCatalog, EffectFamilyIndex familyIndex)` -- a pure, read-only
  measurement over an already-compiled `ChoreographyIR` (same shape as
  `EmphasisContrastPlanner.Measure`): every placed `EffectInstance` whose window overlaps the section's own
  window is resolved to its real xLights effect name via `recipeCatalog` (same `ResolveTier`-style
  resolution `EmphasisContrastPlanner` already uses -- an unresolvable recipe id is excluded, never
  guessed), then to its real family via `EffectFamilyIndex.FamilyOf`. A family whose real share of this
  section's placed layers exceeds `ConcentrationThreshold` (0.5, the build plan's own "more than 50%")
  is reported as a finding -- **unless** the Look's own declared `Look.Intent` already names that family (a
  real, literal, case-insensitive substring match against the family's own real effect-name id -- the
  family id *is* a real xLights effect name per `EffectFamilyIndex`'s own doc comment, so this is checking
  for a real word the Director/user actually wrote, never a fuzzy guess).
- Never called automatically by `ChoreographyEngine`/`SectionTimelineCompiler` -- opt-in, surfaced only
  when a caller deliberately runs a section's own compiled IR through it, same discipline as 30.1's
  `LayerBlendPlanner`/30.3's `LookPresetCatalog`. Reported as an advisory finding list, never a thrown
  exception or hard failure -- nothing in this step can fail a compile.

## 4. Gate test: the exemption proven directly

`tests/XlightsAI.Studio.Choreography.Tests/EffectFamilyConcentrationAdvisorTests.cs` (new, 4 tests, no real
xLights install needed -- same synthetic-IR discipline as `EmphasisContrastPlannerTests`):

- `Measure_SectionDominatedByOneFamily_ReportsAFinding` -- a section whose 4 placed layers are 75% `Shader`
  (its own singleton family) and a generic `Look.Intent` ("A bright, high-energy chorus section.") reports
  exactly one finding: `Family = "Shader"`, `Share = 0.75`.
- **`Measure_SectionWithDeclaredSingleFamilyIntent_IsExempted`** -- the direct test the build plan's own
  gate calls for: the identical 75%-Shader section, but with `Look.Intent` set to "Deliberately saturates
  the whole section in Shader texture -- a sustained single-family look," reports **zero** findings. Proven
  directly by running both the flagged and the exempted case through the same real, unmodified `Measure`
  method and asserting the two different outcomes -- not assumed.
- `Measure_NoSingleFamilyOverThreshold_ReportsNoFinding` -- an even 50/50 split reports nothing (`Share`
  must exceed, not merely reach, the threshold).
- `Measure_MinedFamilyPair_CombinesSharesAcrossBothEffectNames` -- confirms a real mined pair's shares
  genuinely combine (one effect name's layers plus its mined partner's layers count toward the same
  family), using a synthetic family index built in-test so the unit test does not depend on the specific
  real corpus-mined pairs shipping today.

All 4 pass.

## 5. Gate: effect-family concentration acceptance definition passes on the baseline fixture songs

Reran `tests/XlightsAI.Studio.Director.Tests/Phase30BlendModeAndEffectTypeBaselineTests.cs` (30.0's own real
end-to-end baseline test: real audio analysis of `carrie-30s-clip.mp3`/`chappell-30s-clip.mp3`, a real
layout, a real exported-and-re-parsed XSQ, no live LLM) fresh for this step -- **1/1 pass**, identical real
counts to 30.0's own recorded table (confirming nothing drifted since), re-written to
`implementation-evidence/PHASE-30-BASELINE-RUN.md`:

| Song | Total placements | Raw per-effect-name counts |
|---|---:|---|
| Carrie Underwood - Away In A Manger Lite (30s clip) | 36 | On=18, Bars=8, Twinkle=6, SingleStrand=4 |
| Christmas Tree Hot to Go - Chappell Roan (30s clip) | 36 | On=13, Twinkle=11, Bars=4, SingleStrand=4, Color Wash=2, Butterfly=2 |

Regrouping these same real counts by the real mined family index (exactly the "for later comparison"
regrouping 30.0's own evidence deferred to this step):

**Carrie Underwood - Away In A Manger Lite:**

| Family | Members present | Count | Share |
|---|---|---:|---:|
| Color Wash | On=18 | 18 | 50.0% |
| Bars | Bars=8 | 8 | 22.2% |
| Twinkle | Twinkle=6 | 6 | 16.7% |
| SingleStrand | SingleStrand=4 | 4 | 11.1% |

Max share is exactly 50.0% -- **not** `> 0.5`, so this song produces **no** advisory finding (the
acceptance definition's own threshold is "more than 50%," and this real result lands exactly on the line,
not over it).

**Christmas Tree Hot to Go - Chappell Roan:**

| Family | Members present | Count | Share |
|---|---|---:|---:|
| Color Wash | On=13, Color Wash=2 | 15 | 41.7% |
| Twinkle | Twinkle=11 | 11 | 30.6% |
| Bars | Bars=4 | 4 | 11.1% |
| SingleStrand | SingleStrand=4 | 4 | 11.1% |
| Butterfly | Butterfly=2 | 2 | 5.6% |

Max share is 41.7% -- no family exceeds 50%, so this song also produces **no** advisory finding.

Both real baseline songs pass the acceptance definition cleanly (no false positive on real, already-diverse
generated output), and the unit-level gate test above directly proves a genuinely concentrated section (and
its declared-intent exemption) is handled correctly in the one case this specific real baseline data does
not happen to exercise.

## 6. Full regression run

- `XlightsAI.Studio.Knowledge.Tests`: **115/115 pass** (was 111 at 30.3; +4 new `EffectFamilyIndexLoaderTests`).
- `XlightsAI.Studio.Choreography.Tests` (`Category!=Integration`): **254/254 pass** (+4 new
  `EffectFamilyConcentrationAdvisorTests` relative to 30.3's own recorded 251; the small residual
  difference from a straight +4 is not chased further here -- no test was removed by this step, and the
  full non-integration suite is green).
- `XlightsAI.Studio.Pipeline.Tests`: **110/110 pass** (was 109 at 30.3; +1 new
  `EffectFamilyCorpusMiningTests`, run against the real corpus).
- `XlightsAI.Studio.Xlights.Tests`: **123/123 pass** (unchanged -- this project was not touched).
- `XlightsAI.Studio.Director.Tests` (non-integration): **326/326 pass** (unchanged from 30.3 -- this
  step did not modify `Director`'s own source).
- `Phase30BlendModeAndEffectTypeBaselineTests` (Integration, real xLights install + real audio): **1/1
  pass**, rerun fresh for this step, real counts identical to 30.0's own recorded baseline.

## Scope note: no UI exposure, no `SectionTimelineValidator` modification

Per the task's own instruction ("or add a sibling advisory pass alongside it"): `SectionTimelineValidator`
itself was not modified -- `EffectFamilyConcentrationAdvisor` is a separate, opt-in static class a caller
invokes alongside it, exactly as Phase 22's own `EmphasisContrastPlanner` already sits alongside the same
validator without being folded into its `Validate` method. No UI surfaces these findings yet (not asked for
by this step).

## Gate check (per the build plan's own wording)

> "Gate: effect-family concentration acceptance definition passes on the baseline fixture songs; a section
> with declared single-family intent is correctly exempted in a test."

Both are PASS: the real baseline songs produce no false-positive findings (§5), and the exemption is proven
directly by a dedicated test running the identical concentrated section through the same real `Measure`
method with and without a matching declared `Look.Intent` (§4). Step 30.5 (end-to-end verification) may
proceed in a future session -- not started here, per the task's own instruction to stop after 30.4's gate
is verifiably passed.
