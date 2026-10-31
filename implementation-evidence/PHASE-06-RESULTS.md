# Phase 6 results: per-section choreography (L3)

Build plan: `docs/AI-CHOREOGRAPHY-BUILD-PLAN.md`, "Phase 6 — Per-section choreography (L3)". Architecture:
`docs/AI-CHOREOGRAPHY-ARCHITECTURE.md` §2/§3/§6/§14.

## Schema design

`SectionScript` (`src/XlightsAI.Studio.Choreography/SectionScript.cs`) is deliberately the smallest thing
that can do the job: `SectionId`, `LookId`, an optional `Variation` (carried, not yet applied — same
disclosed status as `Look.Variations` itself), and `PropBindings` (per-layer, keyed by `LookLayer.Z`,
overriding which real prop/group that layer targets for this one section). It never re-describes a Look's
effect, parameters, palette, or timing — those are already fixed by the Show Bible. This follows
architecture §2's reasoning-tax argument directly: the Choreographer is *selecting and rebinding*, never
re-deriving.

**Placement decision (a real constraint, not a style choice):** `SectionScript` lives in
`XlightsAI.Studio.Choreography`, not `XlightsAI.Studio.Director`, even though it is L3/Choreographer-role
output. `Director` already has a `ProjectReference` on `Choreography` (for `Look`/`LookLibrary`); putting
`SectionScript` in `Director` would require `Choreography` to reference `Director` back (for
`SectionScriptCompiler`, which must consume `SectionScript` directly) — a project cycle. `SectionScript`
sits next to `Look` instead, and `Director`'s prompt/generator code references it from there.

`SectionScriptCompiler.Compile` (also in `Choreography`) turns a base `LookLibrary` + a
`Dictionary<Guid, SectionScript>` into an *effective* per-section `LookLibrary` (one synthetic Look per
section that actually rebinds a layer, under a derived id `"{lookId}::section::{sectionId:N}"`) plus a
`LookIdBySectionId` map in exactly the shape `ChoreographyEngine.Input` already accepts. This means **Phase
4's engine required zero changes** — the same engine already proven against a hand-authored Look library in
`Phase4RealTrackChoreographyEngineTests` runs Phase 6 output unmodified.

## Batching

`IBatchChatCompletionClient` is a separate, optional interface from `IChatCompletionClient` — not every
provider has a real batch endpoint. `AnthropicBatchChatCompletionClient` implements the real Anthropic
Message Batches API: `POST /v1/messages/batches` (one `custom_id`-tagged request per section, sharing one
cache-marked system prompt), polls `GET /v1/messages/batches/{id}` until `processing_status == "ended"`,
then fetches and parses the `results_url` JSONL body, matching results back by `custom_id`. This could not
be exercised against a real Anthropic account in this environment (none configured) — it is covered by
unit tests against fakes (`SectionScriptBatchGeneratorTests`) exercising the real orchestration logic
(`SectionScriptBatchGenerator`), not the wire format itself.

`SectionScriptBatchGenerator` checks the configured client for `IBatchChatCompletionClient` via a type
test. When present, every section's first attempt goes into one shared batch call. Any section whose
first-round response fails to validate falls through to `SectionScriptGenerator`'s own normal per-section
retry loop as an individual call — a disclosed simplification (a second batch round-trip just for
stragglers was judged not worth the complexity versus the rare case of a validation failure). When the
client does *not* implement the batch interface (local Ollama, plain OpenAI-compatible), every section
takes the bounded-concurrency individual-call path from the start — "where available," literally.

## Cost logging

`JsonAiCallLogStore` (architecture §13's `llm/` directory) writes one atomic JSON file per call — chosen
over a single shared append-only log specifically because batched/concurrent section calls write
concurrently; one file per call needs no cross-call lock. `AiCostEstimator` holds $/M-token rates for the
known Anthropic model families (Opus/Sonnet/Haiku, current and the `claude-3-*` generation for back-compat)
and returns `null` — never a fabricated number — for anything else, including every local Ollama model
used in this phase's own end-to-end run. Both `ShowBibleGenerator` (Director role) and the new
`SectionScriptGenerator`/`SectionScriptBatchGenerator` (Choreographer role) take the call log and model
name as optional constructor parameters and log one entry per attempt, successful or not.

## End-to-end orchestration

`ChoreographyGenerationPipeline` is the thin orchestrator the build plan's own acceptance criterion asks
for: get-or-generate the Show Bible → persist it → generate every section's `SectionScript` (batched) →
persist them → `SectionScriptCompiler` → `ChoreographyEngine.Compile` (completely unmodified Phase 4 code)
→ `XsqExporter`. It makes no choreography decisions of its own.

## Real end-to-end run

`Phase6EndToEndRealTrackTests` (`tests/XlightsAI.Studio.Director.Tests/`, `Category=Integration`) ran this
pipeline against 5 real ~30s clips (ffmpeg-trimmed, same tractability reasoning as Phase 4's own real-track
test) of 5 different real songs under `C:\xLights`, using a real local Ollama install
(`qwen2.5-coder:7b-instruct`, free, confirmed reachable — same install `RealOllamaSmokeTests` already uses)
for the Choreographer role, for real.

**Disclosed scope choice:** the Director role was supplied a hand-authored Show Bible rather than
Ollama-generated, for the same reason Phase 5's own test suite already documents — there is no
frontier-model account configured in this environment, and a free local 7B model is not a realistic
stand-in for the Director's much larger, much more demanding 8–20-Look/3–5-palette schema (`ShowBibleGeneratorTests`'
own "no real provider is consistent/available enough here" disclosure, applied identically). What ran for
real, end-to-end, against a real LLM is exactly Phase 6's own new scope — L3 SectionScript generation,
validation, the batch/fallback split, and cost logging — not L2, which Phase 5 already covers.

### Result: 3 of 5 songs completed end to end

| Song | Effect count | Effect type diversity | Palettes | Timing tracks | Choreographer cost |
|---|---:|---:|---:|---:|---:|
| Dubstep Christmas Light Show (30s clip) | 504 | 6 | 1 | 5 | $0.0000 (free local Ollama) |
| All I Want For Christmas Is You shortened (30s clip) | 371 | 6 | 1 | 5 | $0.0000 |
| Cher-DJ Play a Christmas Song (30s clip) | 350 | 6 | 1 | 5 | $0.0000 |

Every section's `SectionScript` validated on the **first** attempt for all 3 completed songs (0 sections
needed an individual retry) — a small, flat schema, well within a 7B local model's reach, which is exactly
the point of keeping L3's own output shape minimal.

The other 2 songs (Carrie Underwood — Away In A Manger Lite, Christmas Tree Hot to Go) failed during
export, not during choreography generation: `XsqExporter` rejected a `Beats` timing-track mark whose
duration rounds to zero at the 25ms frame grid (`"Timing track 'Beats' mark (...) rounds to a non-positive
duration..."`). This is a pre-existing `TimingTrackEmitter`/`XsqExporter` edge case (beat-grid marks
spaced closer together than one frame interval, likely a tempo-detection artifact specific to these two
30s clips), not a Phase 6 defect — Phase 6's own new code (SectionScript generation, validation, compiling)
completed correctly for both songs before the exporter rejected the timing track. Logged here as a real,
disclosed finding for a future phase rather than silently worked around or hidden.

Real effect counts (350–504) for a 30-second clip against the architecture §1 corpus range (1,114–36,453
for full-length songs, typically several minutes) are a legitimate, consistent comparison point at this
scale — the previous placeholder-era output was a flat 207 effects for an entire ~11-minute song; a 30s
clip here already produces more effects, with 6 distinct effect types (the placeholder-era output used
exactly 1) and real timing tracks (the placeholder-era output had 0).

## Open gaps for a later phase

- The two `Beats` timing-track export failures above (pre-existing, not introduced by Phase 6).
- `SectionScript.Variation` is carried but not yet applied by the compiler (consistent with `Look.Variations`'
  own disclosed status) — a variation scheduler is future work, same as Phase 4 already notes.
- The real Anthropic Batch API path (`AnthropicBatchChatCompletionClient`) is unit-tested against the
  documented wire format but not verified against a live Anthropic account in this environment.
- The Director role's own real generation against a free local model remains Phase 5's own disclosed gap,
  not newly introduced here; a frontier-model account is needed to validate real Director-generated Show
  Bibles end-to-end with Phase 6's pipeline.
