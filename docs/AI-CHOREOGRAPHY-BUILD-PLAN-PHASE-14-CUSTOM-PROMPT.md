# Build Plan: Phase 14 — User-Directed Custom Generation Prompt

**Status:** proposed, not yet implemented. Written 2026-10-30 at the user's direct request: let a user add
their own free-text creative direction (colors, effects, layers, intensity, and whatever else they want to
say) before running a generation, surfaced as its own tab in the Shell UI that comes before Generate.

This slots in after Phase 10-13 in `AI-CHOREOGRAPHY-BUILD-PLAN.md`'s own numbering, independent of all four
— it adds a new user-facing input channel into the existing Director/Choreographer prompt pipeline rather
than changing what that pipeline does with its existing signals.

---

## 1. Why this phase exists

### 1a. There is currently no user-facing creative control at all

Confirmed against the real code: `AiChoreographyWholeSongGenerator`'s own constructor doc comment states it
plainly — *"No style-selection UI/setting exists on the Show Import screen yet"* — the style preset id
(`StylePresetCatalog.TraditionalClassicId` by default) is a hardcoded constructor parameter, not something a
user picks anywhere in the Shell. The only way to influence a generation today is to edit source and
rebuild. A user who wants "more red and gold," "lean into Shockwave on the big tree," or "keep this calm,
it's for a quiet street" currently has no way to say so at all.

### 1b. The prompt architecture already has a natural seam for this

`DirectorPromptBuilder.Build` and `SectionScriptPromptBuilder.BuildVolatileSuffix` already split every
prompt into a **stable prefix** (vocabulary, craft rules, exemplars, style — must stay byte-for-byte
identical across calls for prompt caching, architecture §14) and a **volatile suffix** (the per-song music
model, layout, and per-section content that legitimately changes every call). A free-text custom prompt is
exactly a volatile-suffix citizen: it is set once per generation run by the user, is not guaranteed stable
across runs the way the corpus/style inputs are, and must never be silently folded into the cacheable prefix
(doing so would either invalidate the cache needlessly or, worse, "stick" a stale custom instruction onto a
later run that never asked for it).

### 1c. This is additive guidance, not a new authority level

Architecture already has a real hierarchy: the deterministic constraint engine (L4) enforces hard legality
(real xLights limits, timing, prop capability); craft rules/exemplars are soft grounding the Director can
deviate from with reason; style presets are a coherent bundle the Director composes within. A user's custom
prompt belongs at the same "soft guidance" tier as craft rules/style — it should visibly influence Director/
Choreographer choices, but it can never be used to ask for something L4 would reject (a Look that exceeds a
model's channel capacity, say). This phase should say so explicitly in both the prompt text itself and the
UI, so a user who types something infeasible gets a plan that approximates their intent within real
constraints, not a silent failure or a constraint violation.

---

## 2. Proposed technical changes

### 2a. A small, persisted custom-prompt model
1. Add a `CustomGenerationPrompt` record (`Text`, with a documented length cap — e.g. 2000 characters,
   enforced both in the UI and at the store, mirroring the "bound everything that reaches an LLM call" habit
   `RoleProviderConfig.MaxResponseBytes` already established for responses and Phase 11 §2c now established
   for request bodies).
2. Persist it per-project via a new `JsonCustomPromptStore`, same one-file, atomic-temp-file-plus-move
   pattern every other JSON store in this codebase already uses (`JsonRoleProviderConfigStore`,
   `JsonShowBibleStore`) — so it round-trips across sessions exactly like the Director/Choreographer provider
   settings already do, rather than living only in transient view-model state.

### 2b. A new Shell screen/tab, shown before Generate
1. Add a `CustomPrompt` (or similarly named) value to the `ShellScreen` enum, placed in the navigation flow
   immediately before `GenerateExport` — a natural "last stop before you generate" tab, not a blocking
   gate (an empty custom prompt is a fully valid, unchanged default — see §4's non-regression criterion).
2. A `CustomPromptEditorViewModel` (same shape/persistence discipline as `AiProviderSettingsEditorViewModel`)
   backing a single multi-line text box, with a live remaining-character count against the §2a cap.
3. **Example prompts, since the user explicitly asked the UI show what to say.** A row of clickable example
   chips/buttons, each inserting (or offering to replace) template text, spanning the categories named in
   the request plus a couple more worth surfacing:
   - **Colors**: *"Use a red and gold palette throughout; avoid blue."*
   - **Effects**: *"Favor Shockwave and Meteors; avoid plain Bars/Color Wash."*
   - **Layers**: *"Keep the Mega Tree densely layered (3+ concurrent effects) during every chorus."*
   - **Intensity**: *"Keep this calm and low-intensity overall — this is for a quiet residential street."*
   - **Pacing**: *"Make the first chorus a big surprise moment relative to the verses before it."*
   - **Prop focus**: *"Give the Matrix the most visual variety; keep the icicle lights simple and steady."*

### 2c. Threading the custom prompt into the existing pipeline
1. Add an optional `CustomGenerationPrompt?` parameter to `AiChoreographyWholeSongGenerator` and
   `ChoreographyGenerationPipeline`, defaulting to `null`/empty so every existing call site (including every
   current test and `tools/GenerateOnce`) keeps compiling and behaving identically — this is additive, never
   a required input.
2. When non-empty, append a clearly delimited `## User creative direction` section to **both**
   `DirectorPromptBuilder`'s volatile suffix and `SectionScriptPromptBuilder`'s volatile suffix (never the
   stable prefix — see §1b), with wording that frames it as guidance subordinate to the required JSON output
   shape and hard legality constraints — e.g. *"The user has requested the following creative direction.
   Follow it wherever it does not conflict with the JSON schema above or with hard legality limits; if it
   cannot be fully honored, approximate the user's intent within what is actually possible."* This is the
   same "soft, can be overridden with reason" framing Phase 10 §2f already uses for craft rules, applied to
   a second, user-authored guidance source.
3. Record the exact custom-prompt text actually used for a run alongside that run's other project artifacts
   (next to the Show Bible/call log under architecture §13's own per-project directory layout), so "why does
   this generation look like this" stays answerable from disk later — the same discipline Phase 11 §2c just
   established for request diagnostics, applied here to user-authored input instead of provider wire traffic.

### 2d. Light defensive framing, not a sandbox
A free-text field feeding an LLM prompt is always somewhat injection-adjacent, even when (as here) it is the
user's own text steering their own generation, not third-party content. This phase does not attempt to
"sandbox" the model from its own user's instructions (that would defeat the feature's purpose) — but it
should: (1) visibly label the block as creative guidance, not a system instruction, in the prompt text itself
(§2c.2); (2) keep the hard JSON-schema/legality framing stated *after* the user block in the stable
instructions the model already must satisfy, so the required output contract is never something the user
block could plausibly override; (3) document in the UI, near the example prompts, that direction is
best-effort and may be partially honored when it conflicts with what's actually producible.

---

## 3. Sequencing and dependencies

Independent of Phase 11 (provider reliability), Phase 12 (dynamic density), and Phase 13 (layering/
diversity) — none of those change what a prompt says the user wants; this phase only adds a new, optional
input to what already gets said. It pairs naturally with Phase 10 §6b's shippable multi-layer template
catalog (a user could eventually reference a named pattern, e.g. "use the EDM dense-canvas Tree pattern"),
but does not require that catalog to exist first — free-text guidance works against the current prompt
pipeline today.

## 4. Acceptance criteria

- A new Shell tab exists, reachable before Generate, with a multi-line custom-prompt field, a visible
  character-count/cap, and at least the five example prompts in §2b.3 a user can click to populate the field.
- A real generation run with a concrete custom prompt (e.g. "use a red and gold palette throughout") produces
  a Show Bible/compiled Look whose declared palette is visibly red/gold-dominant, verified against the real
  exported `.xsq`/Show Bible content — not just confirmed present in the rendered prompt text.
- The exact custom-prompt text used for a given run is retrievable afterward from that run's own persisted
  project artifacts.
- A blank/default custom prompt (the pre-existing default for every current caller) produces generation
  behavior unchanged from before this phase — a real non-regression check, not just "the parameter is
  optional" by inspection.
- A unit test on `DirectorPromptBuilder`/`SectionScriptPromptBuilder` confirms the custom-direction block
  appears verbatim (subject to the length cap) in the volatile suffix only, and that the stable prefix is
  byte-for-byte identical whether or not a custom prompt is supplied (prompt-cache safety, per §1b).
