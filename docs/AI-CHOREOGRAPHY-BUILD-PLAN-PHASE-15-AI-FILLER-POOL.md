# Build Plan: Phase 15 — AI-Driven Filler Pool

**Status:** implemented and verified 2026-10-31. All six design decisions (§2a-§2f) and the structural
consequence (§3) are implemented; `Choreography.Tests`/`Director.Tests`/`Shell.Tests` all pass (including
the real-track integration tests); a real `tools/GenerateOnce` run against the real Azure-backed
Director/Choreographer with the prompt "Do not use the butterfly effect." produced a real exported
`.xsq` (`C:\xLights\generated\Bad Feeling - Jagwar Twin.xsq`) with zero Butterfly instances (9 distinct
effect names: Bars, Color Wash, Fireworks, Pinwheel, Ripple, Shockwave, SingleStrand, Strobe, Twinkle)
and 8 distinct per-section `FillerEffects` lists across the song's 9 sections -- confirming both §6
acceptance criteria that depend on a real run, not just unit tests.

This slots in after Phase 14 (`AI-CHOREOGRAPHY-BUILD-PLAN-PHASE-14-CUSTOM-PROMPT.md`) and is a direct
consequence of a real bug Phase 14 verification surfaced.

---

## 1. Why this phase exists

### 1a. The real bug that surfaced it

A user gave a custom prompt including "Do not use the butterfly effect." Verified against the real
run's own artifacts (Show Bible JSON, AI call logs, exported `.xsq`):

- The Director's own Show Bible correctly declared **zero** Looks using Butterfly — the LLM-facing
  half of Phase 14 worked.
- Butterfly still appeared **374 times** in the exported sequence.
- Root cause: `AiChoreographyWholeSongGenerator.BuildFillerPool` (`src/XlightsAI.Studio.Director/AiChoreographyWholeSongGenerator.cs`)
  builds one **hardcoded, deterministic** `FillerPool` — `["On","Bars","SingleStrand","Color Wash","Twinkle","Butterfly"]`,
  Butterfly diluted to roughly 1-in-11 — used by `AlwaysMovingGapSplitter.FillGaps` (architecture §7's
  "always-moving" rule) to fill negative-space gaps on props the Director/Choreographer didn't
  otherwise animate. This pool has **no visibility into the custom prompt at all**, so an explicit
  exclusion request has no way to reach it.

A same-session interim fix (`CustomPromptEffectExclusionParser`, a text-heuristic backstop that strips
matching effect names from the hardcoded pool) is **superseded by this phase** and will be deleted —
see §2f.

### 1b. A second, related finding from the same investigation (not this phase's scope)

While diagnosing the above, inspecting the real Choreographer output (`SectionScript.LookId` picks
across all 26 sections of one real song) also showed: the Director declared 12 distinct Looks
(including one specifically targeting the Arches group), but the Choreographer picked only 5 of them,
with 20 of 26 sections concentrated on just 2 Looks. This is a **Look-selection diversity** problem,
separate from filler — it explains both "the arches never do anything" and a good share of "every song
feels the same." Tracked as a candidate future phase, not designed yet (see §5).

---

## 2. Proposed technical changes

Six design decisions were made with the user, in order; each subsection below is one of them.

### 2a. Approach: fully dynamic, per-moment AI choice (not a precomputed per-song list)

Rejected alternative: have the Director declare one filler list in the Show Bible (one extra JSON
field, zero new LLM calls, fully reproducible compiles). The user chose the more dynamic option
instead: filler effect choice should come from the Choreographer's own per-section reasoning, not a
single whole-song decision made before any section-level nuance exists.

### 2b. Granularity: folded into the existing per-section Choreographer call

A real song can have hundreds of individual filler gaps (every uncovered gap on every prop). A
separate LLM call per gap was explicitly rejected (cost/latency, and `ChoreographyEngine.Compile`'s
deterministic compile loop would need to become async/LLM-aware). Instead: the Choreographer's
existing per-section `SectionScript` response — one call per section, already happening — gains new
field(s) declaring that section's own filler plan. No new LLM calls at all.

### 2c. Shape: a flat default list *and* optional per-role lists

`SectionScript` (`src/XlightsAI.Studio.Choreography/SectionScript.cs`) gains:

```csharp
IReadOnlyList<string>? FillerEffects = null,                                    // required in practice (see §2d) — flat, section-wide default
IReadOnlyDictionary<string, IReadOnlyList<string>>? FillerEffectsByRole = null,  // optional — keys "Foundation"/"Subject"/"Accent"
```

(Appended at the end with `null` defaults — every existing positional/named `SectionScript(...)`
construction across the test suite keeps compiling unchanged, same additive convention as every prior
phase.)

`FillerEffectsByRole`'s keys correspond to `XlightsAI.Studio.Director.BandEnergyGroupRoleMapper.BandRole`
(`Foundation`/`Subject`/`Accent`) — the same foundation/subject/accent classification already used
elsewhere for band-energy-to-prop-group mapping. Kept as plain strings in the `Choreography` project
(not the `BandRole` enum itself) because `SectionScriptCompiler` lives in `Choreography`, which must
never depend on `Director` (Director depends on Choreography, never the reverse — established layering,
confirmed via `AiChoreographyWholeSongGenerator`'s own doc comment). Role-name-to-`BandRole` resolution
happens only at the `Director`-layer integration point (`ChoreographyGenerationPipeline`).

When a target (prop/group) classifies into a role that `FillerEffectsByRole` has an entry for, that
list is used for that target in that section; otherwise `FillerEffects` (the flat list) is used.

### 2d. Required vs optional: required, validated like any other field

Every section's `SectionScript` must declare `FillerEffects`. A missing/empty value is a
**retry-worthy validation failure**, handled by the exact same `SectionScriptValidator` → violation →
retry-prompt loop that an invalid `LookId` already goes through today
(`SectionScriptValidator.Validate` → `SectionScriptBatchGenerator`/`SectionScriptGenerator`'s existing
attempt loop) — not a new mechanism. Concretely: `FillerEffects` stays a nullable C# property (so
`System.Text.Json` deserialization never throws on a missing field), but `SectionScriptValidator` adds
a `filler-effects-missing` violation when it's null/empty, which flows into the existing retry path
unchanged.

### 2e. Minimum variety: the same ≥6-distinct-states rule everywhere

`FillerPool`'s existing hard invariant (`FillerPool.MinimumDistinctStates = 6`, architecture §7's
"rotating pool of ≥6 distinct states") applies identically to `FillerEffects` and to **every**
`FillerEffectsByRole` list, not a relaxed minimum for the narrower per-role lists. New
`SectionScriptValidator` rules:

- `filler-effects-distinct-count`: `FillerEffects` must have ≥6 distinct names.
- `filler-effects-unknown-effect`: every name must be a real effect in the vocabulary the Choreographer
  was given (requires threading an `EffectVocabularyCatalog` into `SectionScriptValidator.Validate`,
  which it does not currently take — a signature change rippling into every call site).
- `filler-effects-by-role-unknown-role` / the same distinct-count/unknown-effect checks, per role key,
  for `FillerEffectsByRole` when present.

### 2f. Trust the AI fully — remove the interim backstop

`CustomPromptEffectExclusionParser` (`src/XlightsAI.Studio.Director/CustomPromptEffectExclusionParser.cs`)
and its test file are deleted once this phase lands. The Choreographer already sees the full custom
prompt in its own volatile suffix (Phase 14) and is now the one actually choosing filler effects, so a
deterministic text-heuristic safety net on top of the old hardcoded pool no longer has anything to
backstop. (Explicit user decision: redundancy without benefit once the AI is the real decision-maker.)

---

## 3. Structural consequence found during planning (not yet resolved by a user decision)

`ChoreographyEngine.Compile`'s gap-filling (`src/XlightsAI.Studio.Choreography/ChoreographyEngine.cs`
lines ~140–157, calling `AlwaysMovingGapSplitter.FillGaps`) currently runs **per target, across the
whole song**, deliberately ignoring section boundaries — the existing doc comment states "a gap can
span a section boundary, which no single section's own compile can see." Moving filler to a per-section
(and per-role) declaration means the compiler now needs to resolve, for every filler segment it emits,
*which section's own plan applies* at that tick position (and which role the target in question
classifies as, if that section declared role-specific lists).

Planned approach (not yet a confirmed decision, flagging for the next design pass):

- `SectionScriptCompiler.Compile` gains a new output, name-based (not recipe-id-based, keeping this
  compiler free of `EffectRecipeCatalog`): `IReadOnlyDictionary<Guid, SectionFillerPlan>`, where
  `SectionFillerPlan(IReadOnlyList<string> FlatEffectNames, IReadOnlyDictionary<string, IReadOnlyList<string>>? ByRole)`.
- `ChoreographyGenerationPipeline.GenerateAsync` resolves this (section bar windows via `BarGrid`,
  target→role via `BandEnergyGroupRoleMapper.Classify(layoutSummary)`, name→recipe-id via a promoted
  version of `LookCompiler`'s private `RecipeIdFor` helper) into a new schedule type — e.g.
  `SectionFillerSchedule` with a `Resolve(long tick, ModelId target) -> FillerPool` method — passed
  directly on `ChoreographyEngine.Input` (not nested under `LookCompilerSettings`, which never actually
  used `FillerPool` itself — confirmed `LookCompiler.cs` has zero references to it).
- `LookCompilerSettings.FillerPool` is removed (dead weight — never consumed there).
- `AlwaysMovingGapSplitter.FillGaps`'s `FillerPool fillerPool` parameter becomes a resolver
  (`Func<long, FillerPool>` or similar), with per-distinct-pool rotation cursors (keyed by section, since
  `FillGaps` already processes one fixed target per call) so each section's own declared list rotates
  independently rather than sharing one global cursor across completely different lists.
- `AiChoreographyWholeSongGenerator.BuildFillerPool` and `ChoreographyGenerationSettings.FillerPool` are
  deleted entirely — there is no more whole-song, pre-AI filler construction once every section
  supplies its own.

## 4. Tests known to need updates (surveyed, not yet written)

`AlwaysMovingGapSplitterTests.cs` (signature change to `FillGaps`), `SectionScriptValidatorTests.cs`
(new rules + vocabulary param), `SectionScriptCompilerTests.cs` (new filler-plan output),
`LookCompilerTests.cs`/`Phase4RealTrackChoreographyEngineTests.cs`/`Phase8AcceptanceTests.cs`/
`SectionPhraseVariationChoreographyEngineTests.cs` (lose the `FillerPool` arg to
`LookCompilerSettings`; the three that actually run `ChoreographyEngine.Compile` need a
`SectionFillerSchedule` built instead), `RealXLightsFixture.BuildFillerPool` (shared test helper,
likely becomes the shared place that builds a fixture `SectionFillerSchedule` instead),
`Phase6EndToEndRealTrackTests.cs`/`Phase7RealIterationLoopTests.cs` (own private `BuildFillerPool`
duplicates, exercise the full pipeline most directly), `CustomPromptEffectExclusionParserTests.cs`
(deleted, see §2f).

## 5. Related, not-yet-designed follow-on problems (found during the same investigation)

- **Look-selection diversity** (§1b): the Choreographer under-uses the Director's own declared Look
  library. Candidate directions: surface each Look's running usage count in the per-section prompt so
  the model can see what's under-used as it goes (requires sharing state across otherwise-independent
  per-section calls, a real architectural change to the "stateless, reasoning-tax" per-section design);
  or a deterministic post-pass that detects a badly skewed usage distribution and forces a retry/swap
  for the most-skipped Looks.
- **Hardcoded style preset**: `src/XlightsAI.Studio.App/Program.cs` always passes
  `StylePresetCatalog.HighEnergyEdmId` — no UI exists to vary it per song, so every song gets an
  identical effect vocabulary/density/prop-role bias regardless of genre. Not designed yet.
- **Thin corpus**: corpus mining has been run against only 6 songs; only 2 generic craft rules cleared
  the confidence/song-count threshold. A secondary contributor to "songs feel similar," smaller than
  the two above.

## 6. Acceptance criteria (once implemented)

- A custom prompt saying "do not use the butterfly effect" produces zero Butterfly instances anywhere
  in a real exported `.xsq` — verified against real exported content, not just prompt text.
- A `SectionScript` missing `FillerEffects`, or declaring fewer than 6 distinct names, triggers the
  same retry path a missing/invalid `LookId` does today.
- A real song's compiled output shows per-section filler variety distinguishable from another section's
  own filler choices (not one global rotation reused identically all song long).
- Full existing test suites (`Choreography.Tests`, `Director.Tests`, `Shell.Tests`) still pass.
