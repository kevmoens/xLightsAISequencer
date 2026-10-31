# Real multi-layer Tree/Matrix exemplars (raw research extracts)

Generated 2026-10-30 by scanning every real, non-backup `.xsq` under `C:\xLights` (the user's own real
sequence library — roughly 30 distinct shows once backup/duplicate-folder copies are excluded) for the
`Tree`/`Matrix*`/`Mega Tree`-named elements with the most simultaneous `EffectLayer`s, then extracting
the single busiest 6-second window (most distinct active layers × most overlapping effect instances) for
each, with every effect's real xLights settings string and palette resolved from the sequence's own
`EffectDB`/`ColorPalettes` lookup tables (not guessed — `ref`/`palette` indices resolved directly against
the real tables in each file).

**This is dev-time reference material only. See the licensing note below before using any of it beyond
local research.**

## Full scan results

The full ranked scan (every Tree/Matrix/Mega Tree element across the whole library, sorted by layer
count) is not committed here (it is large/ephemeral terminal output) — rerun it from
`docs/AI-CHOREOGRAPHY-BUILD-PLAN-PHASE-10-SIGNAL-UTILIZATION.md`'s §6 if needed. The highlights that
justified picking these 8 files:

| Source show | Element | Layers | Effects (whole song) |
|---|---|---|---|
| Dance of the Sugar Plum Fairy (JayKode Trap Remix) | Tree | 16 | 960 |
| Dance of the Sugar Plum Fairy (JayKode Trap Remix) | Matrix | 14 | 1038 |
| Chuck Berry — Run Rudolph Run | Tree | 14 | 194 |
| Chuck Berry — Run Rudolph Run | Matrix | 12 | 206 |
| Carrie Underwood — Away In A Manger (Lite) | Tree | 12 | 62 |
| Carrie Underwood — Away In A Manger (Lite) | Matrix | 12 | 63 |
| The Weeknd — Blinding Lights | Matrix | 8 | 168 |
| Rockin' Around the Christmas Tree | Mega Tree | 9 | 68 |

These were deliberately chosen to span energy levels — Sugar Plum Fairy (EDM/trap remix, maximal) down to
Carrie Underwood (a quiet ballad, still genuinely multi-layered) — so templates exist across the style
spectrum, not just the loudest show in the library.

## Files

Each `*.json` is one extracted window: `sourceFile`, `element`, `windowStartMs`/`windowEndMs` (relative
to that song's own timeline), and `layers[]` — each with its own `effects[]` list of
`{effect, startMs, endMs, settings, palette}`, `startMs`/`endMs` relative to the window start. `settings`
and `palette` are the real, raw xLights key=value strings exactly as stored in that sequence's own
`EffectDB`/`ColorPalettes` tables.

## Licensing note — why these are not shipped with the app

The architecture's own corpus-mining design (`docs/AI-CHOREOGRAPHY-ARCHITECTURE.md` §8b,
`XlightsAI.Studio.Knowledge.CorpusEntry`'s own doc comment) already commits to a "ship the rules, not the
content" stance for exactly this reason: most of the source shows above are vendor-downloaded or
fan-made sequences for commercial, copyrighted songs (The Weeknd, Mariah Carey, Chuck Berry, Carrie
Underwood, Bruno Mars, ...), several carrying their own vendor branding (Boscoyo props, "JayKode Trap
Remix," etc.). The user has a personal copy for their own show; that is not the same as this project
having redistribution rights to bundle the raw extracted effect/timing/palette data from someone else's
sequence inside a public application build.

**Rule going forward**: this raw, per-song extract data stays local dev reference material (used to spot
real multi-layer patterns and, eventually, feed `CorpusMiningRunner`'s induction step). Only the
*generic, derived* patterns it produces — a structural description like "a dense Tree Look during a
high-energy section commonly stacks a fast SingleStrand chase layer + a short-burst Shockwave accent layer
+ a steady Color Wash hold layer underneath," with no copied literal color palette or song-specific timing
— are candidates for anything actually shipped in the app build. See
`AI-CHOREOGRAPHY-BUILD-PLAN-PHASE-10-SIGNAL-UTILIZATION.md` §6 for the task that turns this raw research
into that shippable, derived form.

## Shipped catalog and its growth path

The three patterns above were manually curated into the first shippable version of
`src/XlightsAI.Studio.Knowledge/SeedData/multilayer-tree-matrix-templates.json`
(`XlightsAI.Studio.Knowledge.MultiLayerTemplateCatalog`, loaded via
`MultiLayerTemplateCatalogLoader.Load()` and fed to the Director through
`DirectorKnowledgeContext.Templates` -- see `DirectorPromptBuilder`'s own "Seed multi-layer Tree/Matrix
templates" section). That file is the catalog's `Version: 1`.

**Adding more templates later**: unlike the vendor-downloaded/fan-made shows mined for this first pass,
the user's own future sequences (hand-edited or AI-generated ones the user keeps) carry no licensing
caveat -- they are the user's own original work, so patterns mined from them can be curated (or, once
`CorpusMiningRunner`'s induction step is wired to this catalog's shape, mechanically derived) and folded
straight into the same JSON file with no redistribution concern to work around:

1. Scan the user's own real `.xsq` library the same way §6a's research did (busiest-window extraction
   per Tree/Matrix/Mega Tree element), but scoped to sequences the user actually authored or generated
   and kept, not third-party downloads.
2. Derive the generic structural shape (layer roles, effect families, approximate duty-cycle/cadence
   relationships) the same way the three patterns above were derived -- still palette-free and
   song-specific-timing-free, since a reusable template should generalize across songs even when there is
   no licensing reason to strip it.
3. Append the new `MultiLayerTemplate` entries to `multilayer-tree-matrix-templates.json` (give the
   catalog's own `Version` a bump when the shape of an existing template changes, not just when entries
   are added) and add matching cases to `MultiLayerTemplateCatalogLoaderTests` confirming every new
   layer's `Effect` is a real registered xLights effect name.
4. No other wiring changes are needed -- the Director prompt, the Show Bible's own `Look.TemplateRef`
   field, and the Generate & Export screen's "Seed templates used" disclosure all key off the catalog's
   own `Id`s generically, with no per-template code anywhere else in the pipeline.
