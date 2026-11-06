# Build Plan: Phase 18 — Effect Visual Characterization

**Status:** designed (discussed with the user 2026-11-03), not yet implemented. The user explicitly asked
for a multi-step plan to review now and greenlight later ("start on it when I am ready") — no phase here
should be started without that explicit go-ahead.

---

## 1. Why this phase exists

Every real xLights effect name (`Chase`, `Twinkle`, `Wipe`, `Butterfly`, ...) is currently just a bare
string to the Director/Choreographer — the prompt lists the name and a handful of declared parameters, but
nothing in this codebase has ever actually *looked at* what an effect visually does. Two concrete user asks
exposed this gap in the same conversation:

1. **"Does .xsq allow comments?"** — the user wants a way to annotate an effect/layer with what it is
   trying to visually express (e.g. miming a lyric like "I get up, I get down"), so a later edit/regeneration
   knows what a previous choice was *for*. `.xsq` itself has no such field (confirmed: the real `<Effect>`
   element only ever carries `ref`/`name`/`id`/`startTime`/`endTime`/`palette`), and even an XML comment we
   added ourselves would not survive the file being re-saved by real xLights. The right home for this is our
   own already-existing `Look`/`LookLayer` model (`Look.Intent` already does this at the whole-Look level),
   extended one level deeper to the layer — but writing a *meaningful, accurate* per-layer annotation
   requires actually knowing what the chosen effect looks like, which nothing in this codebase currently does.
2. **"Can we make sample effects and run them in xLights to see what each one does, then use that to write
   comments?"** — yes, and the infrastructure to do this for real already exists, just never pointed at this
   use case: the real headless xLights render pipeline (`RenderPipeline`/`XlightsRenderAdapter`), the real
   frame decoder (`FseqRenderedFrameDecoder`), the real frame rasterizer (`PreviewFrameRasterizer`), and a
   real vision-capable AI client (`IVisionCapableChatCompletionClient`, already used by `VisualCritic`) are
   all already proven, all already wired together for the Quality Pass feature — just never run against a
   *single effect in isolation* to build a reusable reference catalog.

This phase builds that catalog once (an offline, infrequent batch job, not something that runs during
normal generation), then wires it into two places: the Director/Choreographer's own effect vocabulary
(richer than a bare name list), and the per-layer annotation feature from the user's first ask above (now
groundable in a real, known visual description instead of a guess).

---

## 2. Phase A — Enumerate real effects and build minimal fixtures

**Goal:** for every real effect name in the xLights vocabulary, produce a tiny, isolated synthetic sequence
that exercises it on its own, with nothing else to confuse a later visual read.

### Tasks
- Enumerate every effect name from the real, already-loaded vocabulary (`EffectCatalogFactory.BuildFromMetadataDirectory`)
  — no invented effect list, exactly what this app already treats as real/usable.
- For each effect, build a minimal synthetic `Look`/compiled IR targeting **one representative model per
  resolution tier** — a low-resolution model (e.g. a short string/outline, under
  `ChoreographyLegalityValidator.LowResolutionNodeCountThreshold`) and a high-resolution model (e.g. a
  Tree/Matrix-shaped fixture) — since many real effects (confirmed by `TexturalHighResolutionEffects`) render
  completely differently depending on available pixel count. A real show's own layout is the natural source
  for these two representative models when one is available; otherwise a small synthetic `DisplayGraph`
  fixture (same convention as existing test fixtures) stands in.
- Default/neutral parameter settings for the first pass — explicitly disclosed as a simplification, not a
  claim of full parameter-space coverage. A real effect can look very different across its own exposed
  parameter range (speed, direction, density, palette); that is an explicit, disclosed, deferred refinement
  (§6 "Future extensions"), not attempted here.
- A short, fixed duration per effect (long enough to see at least one full motion cycle for a periodic
  effect — needs a real, measured decision per effect family, not a single guessed constant applied blindly;
  disclosed as an open design question to resolve with a few real effects before committing to one default).

### Depends on
- Nothing new — `EffectCatalogFactory`, `Look`/`LookLayer`, `SectionScriptCompiler`, `ChoreographyEngine`
  already exist and already do this kind of compile for real generation.

### Acceptance criteria
- A real, inspectable list of every effect name this pass will characterize, with its own resolved
  representative-model assignment(s) and duration, persisted somewhere reviewable (e.g. a manifest file)
  before any rendering is attempted.

---

## 3. Phase B — Real headless render per effect

**Goal:** actually run each fixture through real xLights, the same way the Quality Pass feature already does
for a whole song, just scoped down to one effect at a time.

### Tasks
- Export each Phase A fixture via the existing `XsqExporter` into its own isolated project/output directory.
- Render each via the existing `RenderPipeline`/`XlightsRenderAdapter`/`XlightsRenderCapabilityProbe` — the
  exact same real, already-proven headless render path the Quality Pass feature uses, never a second render
  implementation.
- Decode the resulting real `.fseq` via the existing `FseqRenderedFrameDecoder`/`RenderedFrameProjector` into
  real per-tick `PreviewFrame`s for the fixture's own representative model(s).

### Depends on
- Phase A's fixtures.
- A configured, real xLights executable (same requirement the Quality Pass toggle already has and already
  discloses via `QualityPassUnavailableException`) — this phase simply cannot run without one; no synthetic/
  approximate renderer substitutes for it, since the entire point is seeing the *real* xLights output.

### Acceptance criteria
- Every enumerated effect/resolution-tier combination has either a real set of decoded `PreviewFrame`s, or an
  explicit, logged failure reason (e.g. an effect name xLights itself rejects, a render timeout) — never a
  silently skipped entry with no record of why.

### Disclosed cost
- This means launching real xLights once per effect/tier combination — potentially 100+ real renders for a
  full vocabulary sweep. Slow (likely hours for a full pass), but a one-time/infrequent cost (re-run only
  when the effect vocabulary changes), not something that runs during ordinary generation.

---

## 4. Phase C — Produce a real video per effect (not just stills)

**Goal:** per the user's own refinement ("export the video of the rendered sequence") — most effects are
defined by their *motion*, which isolated still frames lose. Build a real, literal video (or animated GIF)
per effect/tier, both as a human-reviewable artifact and as the richest available input to the AI
characterization step in Phase D.

### Tasks
- Rasterize **every** decoded frame (not a sampled handful) via the existing `PreviewFrameRasterizer` — same
  rasterizer `VisualCritic` already uses for stills, just applied to the full frame sequence here.
- Encode the rasterized frame sequence into a real video container (e.g. MP4) or animated GIF per effect/
  tier — new, disclosed third-party dependency choice to make explicitly here (this codebase's own "no 3rd-
  party DSP dependency unless disclosed" discipline applies to encoding dependencies the same way), not
  silently picked.
- Persist each video alongside its own effect/tier identity in a human-browsable directory layout — this is
  valuable as a **standalone deliverable** even before any AI step runs: the user (or anyone) can open and
  watch exactly what "Butterfly on a low-res string" actually looks like.

### Depends on
- Phase B's decoded frames.

### Acceptance criteria
- A real, playable video file exists per successfully-rendered effect/tier combination, and a human (the
  user) can open and watch at least a handful of them to sanity-check the whole pipeline before Phase D ever
  runs an AI call against any of it.

---

## 5. Phase D — AI visual characterization

**Goal:** turn each real video/frame sequence into a concise, structured, reusable text description of what
the effect actually does.

### Tasks
- **Resolve the real open question before committing to an implementation:** confirm whether the
  vision-capable client(s) this app already supports (`IVisionCapableChatCompletionClient`, e.g. the
  Anthropic/OpenAI-compatible clients already wired for `VisualCritic`) can accept a video/animated-GIF input
  directly, or only static images. If only images, fall back to submitting a small, evenly-spaced set of
  representative stills from the Phase C frame sequence (not the whole video) — this must be verified against
  the real client/provider, not assumed either way.
- Prompt the model for a compact, structured description per effect/tier: motion type (chase / wipe / pulse /
  twinkle / static-hold / ...), direction (if any), apparent speed, texture/density impression, and a
  one-sentence plain-language summary suitable for embedding directly in a Director/Choreographer prompt.
- Disclosed heuristic, not ground truth — same "disclosed heuristic, not claimed fact" discipline this
  codebase already applies to `VisemeApproximator`/transcript word timing: a single vision-model pass can
  mischaracterize a subtle effect, and this catalog should be treated as a strong default, not infallible.

### Depends on
- Phase B (and, preferably, Phase C) output.
- A configured vision-capable AI role/client.

### Acceptance criteria
- A real generated description exists for every successfully-rendered effect/tier combination.
- A manual spot-check (the user and/or a fresh reviewer, not the model that generated them) confirms a
  sample of descriptions (e.g. 10-15 well-known effects) actually match real, informed expectations before
  the catalog is trusted for Phase E.

---

## 6. Phase E — Persist the catalog and wire it into real generation

**Goal:** make this a real, reusable knowledge artifact the Director/Choreographer actually draws on, the
same way `CraftRules`/`ExemplarIndex`/`MultiLayerTemplateCatalog` already are.

### Tasks
- New persisted type, e.g. `EffectVisualCharacter(string EffectName, string ResolutionTier, string
  Description, DateTimeOffset GeneratedAt)`, stored via the same JSON-store convention every other knowledge
  artifact in this codebase already uses.
- A new standalone offline tool (same pattern as `tools/CorpusMiner`/`tools/GenerateOnce`) that runs Phases
  A-D end to end and writes this catalog — never something that runs automatically during ordinary
  generation.
- `DirectorKnowledgeContext`/the stable prompt builder gains access to this catalog: the "## Effect
  vocabulary" section of the Director/Choreographer prompt (`DirectorPromptBuilder`) names each effect
  alongside its real, known visual description instead of (or in addition to) the bare name.

### Depends on
- Phase D's validated catalog.

### Acceptance criteria
- A real generation run's own stable prefix visibly includes real effect descriptions, verifiable by
  inspecting the persisted prompt (same `JsonAiCallLogStore` artifacts every generation already writes).
- No regression to any existing test that asserts on the stable prefix's exact shape (several already do,
  e.g. `DirectorPromptBuilderTests`) — this is an additive enrichment, not a replacement of the existing
  effect-name list.

---

## 7. Phase F — Ground the per-layer annotation feature in the real catalog

**Goal:** close the loop back to the original ask — once the system has a real, known description of what an
effect looks like, a per-layer annotation (the "comment" feature from the prior conversation) becomes a
grounded statement instead of a guess.

### Tasks
- Add an optional `LookLayer.Annotation` field (free text, same additive-trailing-field convention this
  codebase uses everywhere) — what this specific layer is meant to visually express (e.g. `"mimics 'I get
  up' with an upward chase on the Mega Tree"`).
- The Choreographer is prompted to fill this in when it has the matching effect's own real Phase D
  description available to ground the claim in (e.g. only write "mimics an upward motion" for an effect this
  catalog actually confirms moves upward/has directional motion, not a plausible-sounding guess for an effect
  with an unknown/static character).
- Feed `LookLayer.Annotation` back into future prompts (regeneration, post-processing touch-ups) so a later
  pass genuinely knows what a previous layer was trying to do — this is the real payoff the user originally
  asked for.

### Depends on
- Phase E's wired-in catalog (so there is something real to ground an annotation against).
- The existing post-processing/touch-up pipeline (`ChoreographyGenerationPipeline.ApplyPostProcessingPromptAsync`,
  already shipped) as the natural consumer of this context on a later edit.

### Acceptance criteria
- A real generated `Look`'s layers carry real, catalog-grounded annotations (spot-checked against the Phase D
  descriptions they claim to be consistent with).
- A real post-processing touch-up prompt's own Choreographer call can be shown (via the persisted call log)
  to have received the prior layer's own annotation as context.

---

## 8. Future extensions (explicitly deferred, not in scope for Phases A-F)

- **Parameter-space coverage.** Phase A fixes one neutral parameter setting per effect; a real effect's
  character can change substantially across its own exposed range (speed, density, direction options,
  palette interaction). A later phase could sweep a small number of representative parameter combinations
  per effect, multiplying the Phase B/C/D cost accordingly — deliberately not attempted in the first pass.
- **Per-palette characterization.** Color choice can change perceived character (e.g. a "Twinkle" in a single
  warm color vs. a rapid multi-color palette) — also deferred.
- **Automated re-characterization triggers.** Re-running this pipeline only when the effect vocabulary
  changes requires detecting that change; today this would be a manually-triggered tool run, not an
  automatic one.

---

## 9. Sequencing note

Same discipline as every other multi-phase plan this project has followed: build and verify one phase at a
time, confirm with the user before starting the next, never implement ahead of an explicit go-ahead. Phase D
in particular has one real open question (video vs. image input to the vision client) that must be resolved
with a quick, cheap spot-check before committing to its full implementation shape.
