# Phase 29 — XSQ generation: sustained phrasing, beat accents, and reusable patterns

## Goal and decisions

Make the AI-generated show follow musical ideas across time instead of switching several models on every note. Keep the Show Bible for the show's palette, energy, and visual direction; extend the existing section timeline so the Choreographer plans what actually runs, where, and for how long.

- Surface the full duration of credible sustained notes. The LLM decides whether a concurrent beat leaves the sustain alone, gets an independent accent, or takes over. No default preference among those choices; an unrelated beat must not cut a hold unless the plan calls for it. A major shift is an opportunity for a whole-display moment, not a mandatory flash or a reason to require a prominent sustain.
- Give recurring choruses a recognizable signature, with variation in supporting layers, movement, or intensity. Do not equate every same-label section with the same musical theme.
- Require traceable adaptation when an eligible, enabled local exemplar fits. Allow original choreography otherwise. Reuse the musical and visual structure, not the source's physical target names, absolute song timestamps, or literal palette.
- Scope: the AI whole-song path and affected generation, repair, and export behavior. Reuse current audio analysis, display mapping, effect recipes, and XSQ format; no new model training, provider, or editor redesign.
- Target xLights version for export and preview verification: `2026.03` (the version `ChoreographyGenerationPipeline` already exports against). Use version-pinned XSQ fixtures; do not invent effect XML settings.

## Current state

- `AiChoreographyWholeSongGenerator` loads mined exemplars and curation overrides. `DirectorPromptBuilder` retrieves by section kind with song-level/placeholder features and shows descriptions and effect counts. `ExemplarReuseTracker` and `ShowBibleValidator` measure retrieval, not choreography applied to a section. Seed `TemplateRef` is a separate Show Bible feature, not mined-exemplar provenance. Phase 16 kind-aware retrieval already exists; this phase extends it rather than replacing it.
- `ExemplarIndexBuilder` selects effects by start bar, so effects that begin before an exemplar window and continue through it (often the sustained foundation) are dropped. `ExemplarEntry`/`AlignedEffectInstance` store start alignment only — no beat-aligned endpoint and no source beat grid — so placements cannot yet be retimed into reusable gestures.
- `SectionScriptPromptBuilder` receives occurrence and motif-link hints, but the per-section calls are independent. Later choruses don't receive an authoritative shared signature or know what another section actually chose.
- `SectionScriptPromptBuilder` (~lines 395–398) instructs dim-before-bright at every emphasis moment and calls a bright hold through two hits "WRONG"; `SectionTimelineValidator` (~lines 95–117) rejects consecutive bright cues on the same literal prop without a handoff. Both actively push against holding through beats. The Phase 28 timeline redesign deferred this conflict.
- `IntensityTierMapping` maps Dim/Bright/Full to duty 0.15/0.6/0.95, and `SectionTimelineCompiler` (~lines 167–176) multiplies the placement span by that duty. A 10-second Bright hold therefore exports as 6 seconds.
- `SectionTimingMarks` offers vocal-note onsets, not end anchors. Prompt/validation use a salient-note subset, while compilation enumerates the full note list, so an indexed note ID can resolve to a different note. `TranscribedWord` timings are distributed across each decoded segment by character length, and `Confidence` belongs to the segment, so word boundaries are estimates, not measurements.
- `SectionTimelineCompiler` supports holds and handoffs, but its implicit successor uses the literal prop reference, appends effects over the base look, and clamps placements to the section window. It doesn't distinguish independent layers from replacement when a group and an individual model cover the same target. In `ChoreographyEngine`, phrase overrides bypass section timeline cues; `ChoreographyGenerationPipeline` persists flat scripts and repairs sections independently, and repaired scripts can invalidate their original timeline.

## Acceptance definitions

These are the measurable gates the steps below refer to. Thresholds are initial values; adjusting one requires recording the reason in that step's evidence file.

- **Frame tolerance:** ±1 frame at the sequence's own frame timing.
- **Hold integrity:** each planned hold exports as one placement — or contiguous placements with no gap greater than 1 frame — on the same target set and layer, covering the hold's resolved `[start, end)` within frame tolerance, regardless of intensity tier.
- **Undeclared interruption:** during a hold, a new effect starting on the hold's own targets and layer that is not a cue the timeline explicitly declared as a takeover or handoff. Must be **0** in deterministic tests.
- **Unrelated switch count:** during each sustained span, the number of effect starts on models that are neither in the hold's targets nor in a declared accent or takeover cue. Reported per span and averaged per song.
- **Baseline:** step 29.0 measures the unrelated switch count and undeclared interruptions for the current generator on fixed songs (for example the existing `carrie-30s-clip.mp3` and `chappell-30s-clip.mp3` fixtures plus one full-length song), with a fixed layout and recorded AI responses. The real-run target is at least a 50% reduction in the average unrelated switch count on the same songs and layout. Report the real-run comparison; deterministic tests enforce 0 undeclared interruptions.
- **Sustain eligibility:** a vocal note is a sustain candidate when its duration is at least one beat at the local tempo, or at least 0.75 s, whichever is longer. Word labels attach to a sustain only when the estimated word interval overlaps the note by at least 50%; word timing never defines a hold's endpoints.
- **Exemplar eligibility (required use):** enabled; same section kind (not a cross-kind fallback); tempo within ±15% (or half/double time); energy percentile within ±0.25; and at least two of its gesture roles map to targets in the current layout with registered recipes. Anything else is inspiration only and never required.
- **Meaningful adaptation:** compiled placements in the section reproduce at least two of the exemplar gesture's roles, with the same relative order, onsets within ±1 beat of the retimed gesture, and duration ratios within ±25%. A declared exemplar ID or an effect-name match alone fails.
- **Chorus signature:** the plan declares the stable dimensions for a motif family (from: subject target set, signature effect/recipe, palette slot, rhythm anchor kind). Every linked occurrence must keep all declared stable dimensions and differ from its predecessor in at least one permitted dimension (supporting layer count, added group, intensity tier, motion direction).
- **Human preview gate:** "recognizable chorus" and overall look are judged by a person in xLights `2026.03` preview. Record the songs, the reviewer, and PASS/BLOCKED in the final evidence file. This gate is separate from the automated checks and does not replace them.

## Ownership rules

- **Seam-spanning holds:** a hold is owned by the section in which it starts. It may extend into the next section up to its resolved end, and the next section's timeline is told it exists (target set, layer, end anchor) so it can accent around it or declare a takeover. If both sections claim the same target and layer over the same interval, the owning section wins and the conflict is logged.
- **Chorus signatures:** a motif family's signature is fixed when planned. Repairing or regenerating one occurrence may change only its permitted dimensions. Changing a signature requires replanning and revalidating every linked occurrence.

## Steps

Each step writes targeted tests, implements its vertical path, runs those tests, inspects the exported output where it applies, and records `implementation-evidence/PHASE-29-<STEP>.md` with PASS or BLOCKED. Do not start a step whose prerequisite is BLOCKED.

### 29.0 Baseline and blocking fixes (`baseline-and-blockers`)

- Measure the baseline defined above before changing behavior.
- Add a minimal cue-kind field to `SectionCue` (`hold`, `accent`, `takeover`; omitted means `accent`, so existing responses keep today's meaning). Step 29.2 builds the rest of the hold/accent schema on top of it.
- Fix intensity (blocker #2) and contrast (blocker #1) as described below, then test that a hold through beats, an independent accent, and a takeover all pass validation and export, with no preferred answer.
- Gate: baseline recorded; hold integrity passes for all tiers; tests for all three overlap decisions pass; tests show a Full accent dims every model except running holds, a Bright accent dims only its own targets, and an accent overlapping a running hold's targets is rejected. The brightness setting ships only if its 2026.03 fixture check passes; otherwise record it as BLOCKED in the evidence file and keep effect choice only.

**Decisions for the blockers (confirmed by the user):**

| Topic | Today | Decision |
|---|---|---|
| What Dim/Bright/Full means (#2) | `IntensityTierMapping` duty (0.15/0.6/0.95) sets how much of the cue window is lit; `SectionTimelineCompiler.EmitAt` cuts the placement to that fraction. | `hold` and `takeover` cues always run their full resolved span. Intensity is expressed by effect choice, using the existing effect brightness catalog that `EmphasisContrastPlanner` already uses; the validator flags a cue whose effect's catalog tier is far from its declared intensity (for example a Dim cue using an inherently bright effect). **Also** apply a per-tier xLights brightness setting on the placed effect, but only after its exact XML key and value range are confirmed against a real xLights 2026.03 XSQ fixture checked into the repo; until then, effect choice alone carries intensity. The lit-time fraction applies only to `accent` cues, which keeps their short punchy shape. `Off` emits nothing for its span instead of a one-frame placement. |
| Where contrast is required (#1) | `SectionTimelineValidator` rejects any two consecutive bright cues on the same literal `Props` unless a handoff is declared. | Contrast applies only to back-to-back `accent` cues, and its reach scales with the accent's intensity. Before a **Full** accent, everything is dimmed (except running holds, see below). Before a **Bright** accent, only that accent's own targets are dimmed. A `hold` may stay bright through any number of beats. A `takeover` ends the hold it replaces and is not a contrast violation. |
| Prompt wording (#1) | The section-script prompt calls contrast "the single most important rule", requires it at every hit, and calls a bright hold through two hits "WRONG". | Keep contrast strongly stated for accents, including the Full-dims-everything / Bright-dims-own-targets rule. Explicitly add that holding through beats is equally valid, and that at each emphasis moment the LLM chooses hold through, accent, or takeover. Remove "every single time" and the "WRONG" hold-through example. |
| Automatic contrast patch (#1) | `EmphasisContrastPlanner` inserts dim effects before emphasis moments that lack contrast, on whatever is playing. | Never patch a hold's own targets while the hold runs. Still add missing contrast before declared `accent` cues, using the same scope rule (whole display for Full, own targets for Bright). Sections with no timeline cues keep today's behavior. |
| Accent on a model that is holding (#1) | Not distinguished; overlay cues are simply appended. | Not allowed. Accents must target models outside any running hold (for example the arches while a matrix holds). The validator rejects an accent whose targets, after expanding groups, overlap a running hold's targets. To change what a holding model does, the LLM declares a `takeover`, which ends the hold. |
| Running hold during a Full accent's dim (#1) | n/a | The hold keeps running unchanged. "Dim everything" means every model except the targets of running holds. |

### 29.1 Shared musical anchors and spans (`musical-spans`)

- Create one section timing catalog used by prompting, schema validation, and compilation, so an anchor ID always resolves to the same moment.
- Include paired start/end anchors for **sustain-eligible** vocal notes, existing beats/transients, and detected energy shifts. Attach word labels only per the eligibility rule; show duration, strength, and concurrent short hits compactly.
- Preserve anchor IDs and absolute endpoints across phrase and section boundaries; use existing signals when notes or transcripts are missing.
- Gate: regression test for the salient-subset/full-list mismatch; a test where a high-confidence word estimate disagrees with the sustained note and the note's endpoints win.

### 29.2 Explicit sustain and accent choreography (`layered-choreography`)

- Extend the timeline schema additively: a held role on a model, model set, group, or group set; bounded independent accents; and explicit takeovers and handoffs.
- Offer ordered real targets from existing display order and travel mapping, so the LLM can chase arches 1–4, then loop or move to mini trees one at a time. Give it enough context to choose matrices or a whole-canvas group for a continuous sweep, individual props for short hits, and broad coverage at a structural shift.
- Compile holds as continuous placements, including across section boundaries per the **ownership rules**. Resolve overlap against actual target membership (groups included), preserve legal xLights layers and precedence, and keep holds and intentional quiet windows intact through filler and contrast handling. Don't duplicate an existing base layer just to express a hold.
- Gate: hold integrity, 0 undeclared interruptions, and correct group/member overlap in exported XSQ tests; a seam-spanning hold test, including a conflicting adjacent-section claim.

### 29.3 Chorus signatures (`chorus-signatures`)

- From existing motif and structure relationships, build a compact plan before the per-section calls that declares each family's stable dimensions. Pass the same signature and a variation brief to every linked occurrence. Leave uncertain or unrelated sections independent.
- When an eligible exemplar is chosen for a family, it supplies the signature instead of a competing decision.
- Gate: the **chorus signature** check passes on final timelines; repairing one occurrence cannot change the signature.

### 29.4a Exemplar gesture extraction and backfill (`exemplar-gestures`)

- Re-mine exemplars with the source beat grid and enough target metadata to normalize placements into gestures: role, relative beat/phrase onset, duration, cadence, and overlaps.
- Include effects that start before an exemplar window and continue through it; clip them to the window and record that they continue in from earlier.
- Version the index and provide a backfill or re-mine path for existing knowledge directories. Older indexes still load, are treated as inspiration only, and the UI or log says re-mining is needed.
- Gate: tests on a real-format indexed fixture, including an effect starting before the window and a variable-tempo source.

### 29.4b Exemplar adaptation (`exemplar-adaptation`)

- Retrieve a short enabled candidate list for actual sections or motif families using tempo, energy, vocal, and structural context. Classify each candidate against **exemplar eligibility**.
- Give the planner stable exemplar and gesture IDs with explicit mapping options onto current anchors, props, palettes, and recipes.
- When an eligible exemplar exists, require **meaningful adaptation** in the compiled placements. Record whether it was applied, or why the section stayed original. Keep enable/disable curation working, and report exemplar attribution separately from seed `TemplateRef` usage.
- Gate: an eligible exemplar produces recognizable retimed layers; a bogus reference or a name-only match is rejected; disabled, ineligible, or missing exemplars fall back to original choreography.

### 29.5 Preserve intent through generation and export (`generation-roundtrip`)

- Thread anchors, timelines, chorus signatures, and exemplar decisions through the active AI generation path. Compile phrase-specific base looks together with the section's cues instead of bypassing them.
- Persist validated timelines alongside the flat-script compatibility path. On repair, refinement, regenerate, and reload, keep untouched timelines and revalidate or replace affected ones, following the **ownership rules**.
- Report final (not pre-repair) hold/accent decisions and exemplar attribution, tied to compiled targets and time ranges. Keep existing behavior for legacy flat script files, missing analysis or corpus, and rejected or unavailable timeline output, and report a fallback explicitly instead of attributing effects that aren't in the final sequence.
- Gate: round-trip tests for persistence, reload, and repair preserve or intentionally replace timelines.

### 29.6 End-to-end XSQ verification (`xsq-verification`)

- Using synthetic timing and layout, a curated synthetic exemplar, and scripted AI responses, parse the generated XSQ and assert actual model/group membership, intervals, layers, and effect counts.
- Cover: a held note with no accent, with rotating independent accents, and with an intentional takeover; a seam-spanning hold; major-shift coverage without a dominant hold; overlapping group/member targets; chorus returns that pass the signature check; exemplar adaptation and its fallbacks; and preservation and intentional replacement through repair.
- Rerun the baseline songs and record the unrelated switch count against the 29.0 baseline.
- Gate: all automated checks pass; the real-run comparison is recorded; the **human preview gate** is recorded.

## Dependencies and guardrails

- 29.0 precedes everything. 29.1 precedes 29.2. 29.4a precedes 29.4b.
- 29.3 and 29.4a/b can be developed separately, then integrated so a family can carry one eligible exemplar across its linked occurrences.
- 29.5 integrates 29.2, 29.3, and 29.4b; 29.6 verifies the result.
- Keep prompts bounded and reuse per-section batching where practical. Do not invent timing, target names, or capabilities; do not make high density, all-model coverage, or exemplar reuse a blanket requirement.
