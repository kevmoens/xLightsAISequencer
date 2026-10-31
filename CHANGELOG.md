# Changelog

All notable changes to xLights AI Choreography Studio are recorded in this file. Dates are UTC.

## [Unreleased]

### Added

- **Phase 10 §3g (docs/AI-CHOREOGRAPHY-BUILD-PLAN-PHASE-10-SIGNAL-UTILIZATION.md): resolved the
  `XlightsAI.Studio.Composition` duplication decision.** Confirmed `XlightsAI.Studio.Composition`
  (musical-role assignment, `VisualBudget` density budgeting, `TransitionPlan` gesture recipes,
  `ChoreographyCompiler`) backs only the old M1-M4 `WholeSongGenerator`/`ChoreographyCompositionRunner`
  path, which `src/XlightsAI.Studio.App/Program.cs` still constructs but never assigns to
  `App.WholeSongGenerator` — the real app always runs `AiChoreographyWholeSongGenerator`'s LLM-driven
  Phases 5-8 pipeline instead, so `Composition` has never executed inside a real generation. Decision
  (confirmed with the user): archive, don't port or delete. `MusicalRoleAssignment`/`TransitionPlan`/the
  precedence-tier system are superseded by the live path's own band-energy prop-role mapping (§3d) and
  prompt-driven transition guidance. `VisualBudget`-style deterministic density/concurrency capping was
  judged a plausible future enhancement (the live path's only density control today is advisory prompt
  text in `DirectorPromptBuilder`/`SectionScriptPromptBuilder`, and `ChoreographyLegalityValidator` — the
  live path's real deterministic post-pass — checks darkness/coverage/peak-uniqueness but not density or
  concurrent-focal-effect count) but was deliberately deferred rather than implemented, since it requires
  a real design pass against `ChoreographyIR` rather than a mechanical port of Composition's own types.
  `Composition`, `WholeSongGenerator`, and `ChoreographyCompositionRunner` are now marked superseded/unused
  in their own doc comments and in `docs/MASTER-SYSTEM-ARCHITECTURE.md` §9a, so future work does not land
  in a path that never executes.

- **Phase 11 §2a-§2d (docs/AI-CHOREOGRAPHY-BUILD-PLAN-PHASE-11-PROVIDER-RELIABILITY.md): fixed the real
  Azure OpenAI Responses API endpoint-construction bug and added request/endpoint observability.**
  `ProviderEndpointUri` gained `IsResponsesApiEndpoint`/`BuildResponses`, recognizing the
  `.../openai/responses?api-version=...` shape distinctly from the classic `v1/chat/completions`/
  `v1/messages` suffixes it already stripped -- previously this shape fell through untouched and had its
  own path and `api-version` query string silently dropped in favor of a different, unintended endpoint
  (confirmed as the actual root cause of a real 1h14m `GenerateOnce` run, one call alone ~57 minutes).
  New `OpenAiResponsesChatCompletionClient` speaks that API's own real wire shape (`input`/`output`, not
  `messages`/`choices`); `ChatCompletionClientFactory` now routes to it automatically, no new user-facing
  setting required. `RoleProviderConfig` gained an optional `ReasoningEffort`, forwarded as
  `reasoning.effort` on every Responses API call and defaulted to a conservative `"low"` when unset (surfaced
  in the Settings screen with latency/richness tradeoff help text) -- so an unconfigured role no longer
  silently inherits the model's own unbounded reasoning budget. Every chat-completion call (success or
  rejection) now carries a new `RequestDiagnostics` (real request URI, bounded request body, and an
  `EndpointSanityChecker`-computed warning when the configured endpoint's own path diverges from the actual
  request path) through to `AiCallLogEntry`, so this class of bug is diagnosable from the call log alone
  going forward. Unit tests reproduce the real incident endpoint string against the unmodified (buggy)
  `ProviderEndpointUri.Build` first, then prove the fix. Re-verified against the real, unmodified incident
  endpoint/credential via `tools/GenerateOnce`: wall-clock generation time dropped from 1h14m to **7m20s**
  with no other variable changed, confirmed via the new call-log `Diagnostics` showing the correct endpoint,
  wire shape, and `reasoning.effort` value were actually sent -- see
  `implementation-evidence/PHASE-11-RESULTS.md` for the full before/after record.

- **Phase 10 §3b (docs/AI-CHOREOGRAPHY-BUILD-PLAN-PHASE-10-SIGNAL-UTILIZATION.md): per-section melodic
  contour digest in the Choreographer prompt.** New `MelodicContourBriefBuilder`
  (`XlightsAI.Studio.Director`) joins the real `MelodyTrack` (computed by `XlightsAI.Studio.Melody`, but
  previously only consumed by the bar-fingerprint/section-detection path and the Piano Roll UI -- never
  reaching either prompt) against each real section's own bar range via `BarGrid`, producing a compact
  `SectionMelodicBrief(SectionId, ContourShape, NoteDensity, PeakNoteBeatOffset)` -- `ContourShape` a
  closed `Rising`/`Falling`/`Mixed`/`StaticSparse` enum derived from real up/down `MelodicGesture` counts
  in that window, never a continuous curve the LLM would have to interpret unguided. `MusicModel` gained a
  `Melody` field (wired from `AiChoreographyWholeSongGenerator`'s existing `audio.Melody`, previously
  unused by the LLM path). `SectionScriptPromptBuilder.BuildVolatileSuffix` now prints one line per
  section, e.g. `"Melodic contour: Rising, high note density, peak at beat 4 of 28."`, and
  `BuildStablePrefix` carries the §2c rules verbatim (rising -> climb-to-peak, falling -> settle, dense ->
  faster subject cadence independent of energy ceiling, sparse/held -> the existing "hold is a steady
  state" rule). Verified against a real end-to-end `GenerateOnce` run (`Bad Feeling-Jagwar Twin`) by
  reconstructing the real digest from the run's own cached analysis (raw prompts are not logged in
  production) and cross-referencing the real exported Show Bible/SectionScripts: this song is
  `highs`-dominant for every section (ruling out §3d's band-energy mapping as a confound), yet across six
  `Chorus`-kind sections sharing that same dominant band, the Choreographer's independent per-section Look
  choice split cleanly by melodic contour -- both `Rising` sections (bars [0,7) and [18,23), non-adjacent
  repeat occurrences #1 and #4) picked `L02` (Matrix/Bars), both `Falling` sections (bars [11,18) and
  [23,26), occurrences #3 and #5) picked the distinct `L06` (MiniTree1/SingleStrand), and two of three
  `Static/Sparse` sections picked a third distinct Look (`L15`) -- a real, measurable behavioral
  correlation in the compiled output, not just prompt text.

- **Phase 10 §3d (docs/AI-CHOREOGRAPHY-BUILD-PLAN-PHASE-10-SIGNAL-UTILIZATION.md): band-energy-grounded
  group-role mapping in the Director prompt.** New `BandEnergyGroupRoleMapper`
  (`XlightsAI.Studio.Director`) computes each real section's own bass/mid/high share of total energy from
  §3a's now-real `BandEnergyEnvelope` (via `BarGrid.WindowForBarRange` + `AverageInWindow`, never a
  whole-song average standing in for every section), and classifies the real imported layout's own
  groups/models by name into §2b's foundation/subject/accent roles. `DirectorPromptBuilder`'s volatile
  suffix now prints each section's measured dominant band next to its bar range, plus a new "Band-to-group
  mapping" block naming the song's *actual* foundation/subject/accent groups and this song's own measured
  song-wide band balance -- a concrete, song-grounded version of the bass->foundation/mid->subject/
  highs->accent rule, consistent for the whole song rather than re-decided per Look, extending the existing
  per-style `PropRoleBias` weights with a real measured basis. Verified against a real end-to-end
  `GenerateOnce` run (`Bad Feeling-Jagwar Twin`): the real exported Show Bible's `PropRoles` assigned
  `bass` to exactly the classified foundation groups (`Outline`, `Gutter`, `Roof`, `Roof and uprights`,
  `Horizontals`), `melody` to the classified subject groups/models (`Matrixes`, `Tree`, `Arches`), and
  `highs` to the classified accent group (`MiniTrees`) -- matching the named rule, not coincidence.

- **Phase 10 §3a (docs/AI-CHOREOGRAPHY-BUILD-PLAN-PHASE-10-SIGNAL-UTILIZATION.md): closed the
  vocal-activity/band-energy/viseme dead-wiring gap.** `AudioAnalysisRunner` now instantiates
  `VocalActivityAnalyzer` and `BandEnergyAnalyzer` (each with its own `IStemActivityResultStore`/
  `IBandEnergyEnvelopeStore` JSON cache, mirroring the existing onset/tempo store pattern) and runs
  `VisemeApproximator` against the real vocal-activity regions it produces; `AudioAnalysisResult` carries
  all three as new fields. `AiChoreographyWholeSongGenerator` no longer hardcodes
  `VocalActivity: null, BandEnergy: null` -- real values flow into `MusicModel`, and real `VisemeMark`s now
  reach `ChoreographyEngine.Input.VisemeMarks` via `ChoreographyGenerationPipeline` (previously left at its
  `null` default). Verified against a real end-to-end `GenerateOnce` run (`Bad Feeling-Jagwar Twin`): the
  exported `.xsq`'s `Percussion` timing track carries 1,270 real band-energy marks and `Lyrics_Phonemes`/
  `Lyrics_Words`/`Lyrics_Phrases` carry 656/39/9 real viseme/phrase marks -- all previously empty under the
  old hardcoded-null path, confirming `LyricTimingTrackBuilder` and the Director/Choreographer prompts'
  vocal-region and `VocalActive` fields are now live rather than permanently inert.

- **Phase 6 (docs/AI-CHOREOGRAPHY-BUILD-PLAN.md): per-section choreography (L3), batched generation, and
  the first complete LLM-driven generation path.** A new `SectionScript` schema (`XlightsAI.Studio.Choreography`)
  is the Choreographer's entire output shape -- which Look a section uses, an optional declared variation,
  and per-layer prop/group rebindings -- validated by `SectionScriptValidator` and compiled by
  `SectionScriptCompiler` into a synthetic per-section effective Look library, so Phase 4's
  `ChoreographyEngine`/`LookCompiler` consume Phase 6 output with their own existing contract completely
  unmodified. `XlightsAI.Studio.Director` gained `SectionScriptPromptBuilder` (the Choreographer's own
  stable Show-Bible/vocabulary/craft-rules prefix, shared byte-for-byte across every section of one song)
  and `SectionScriptGenerator` (the same validate/retry-with-correction discipline Phase 5's
  `ShowBibleGenerator` already established). A new `IBatchChatCompletionClient` capability plus
  `AnthropicBatchChatCompletionClient` implement the real Anthropic Message Batches API (submit, poll,
  fetch JSONL results) for the ~30 per-section calls; `SectionScriptBatchGenerator` uses it when the
  configured client supports it and falls back to bounded-concurrency individual calls otherwise ("where
  available," per the build plan). A new `llm/` call log (`JsonAiCallLogStore`, one atomic file per call)
  plus `AiCostEstimator` (known Anthropic model rates, `null` -- never fabricated -- for an unrecognized
  model) wire into both the Director's and Choreographer's generators. `ChoreographyGenerationPipeline`
  orchestrates the whole path end to end: song + layout -> (generate or reuse) Show Bible -> per-section
  SectionScripts -> compiled IR -> exported `.xsq`, with no render/critique loop yet (Phase 7's scope).
  Verified end-to-end against real ~30s clips of real songs under `C:\xLights` and a real local Ollama
  install for the Choreographer role (free, no API key) -- see `implementation-evidence/PHASE-06-RESULTS.md`
  for the full write-up, including the disclosed choice to supply a hand-authored Show Bible for these
  particular runs rather than a Director-generated one (no frontier-model account is configured in this
  environment, and Phase 5's own test suite already established that a free local small model is not a
  realistic stand-in for the Director's much larger, much more demanding schema).

- **Phase 1 foundation (docs/AI-CHOREOGRAPHY-BUILD-PLAN.md): real pixel geometry, an extensible effect
  recipe system, and a richer sequence IR.** `ShowLayoutImporter` now reads `parm1`/`parm2`/`parm3`,
  `StringType`, `WindowRows`/`WindowColumns`, normalizes `DisplayAs` into a `ModelCategory`, computes a
  verified per-model node/pixel count (cross-checked against the XML's own `StringN` connection
  attributes), captures full position/scale/rotation geometry (`DisplayBounds`), and enumerates
  `<subModel>` elements as first-class addressable targets. A new `IEffectRecipe`/`EffectRecipeCatalog`
  registration mechanism replaces the single hardcoded "On" recipe check that previously required
  parallel edits across `EffectRecipeRegistry`, `XsqExporter` and a plan factory — adding a recipe now
  means registering one `IEffectRecipe` implementation. `SequenceDocument`/`EffectInstance` gained
  `SequenceMetadata`, a real `ColorPaletteDefinition` table, first-class `TimingTrack`s, and
  `PaletteIndex`/`BlendMode`/`Transitions`/`ValueCurves`, and `XsqExporter` now emits a real multi-entry
  palette table, real `<head>` metadata, and labelled `<Element type="timing">` tracks. Verified against
  a real headless xLights render of a 2-layer, 2-palette, timing-track-bearing sequence (exit code 0,
  clean import). All new fields are trailing optional parameters, so every existing construction site
  across the solution keeps compiling unchanged.
- **Real whole-song choreography generation, end to end.** New `XlightsAI.Studio.Pipeline` project wires
  the previously-isolated analysis/planning libraries into one working pipeline: stem separation →
  Rhythm (onset/tempo/beat grid/energy) → Melody (pitch tracking → note segmentation → gesture
  classification) → Structure (bar fingerprints → section boundaries/labels → phrases → motif
  clustering) → deterministic per-section creative direction and per-prop role assignment → the existing
  `ChoreographyCompiler` → a real, exportable `SequenceDocument`. Surfaced as a new "Generate Whole-Song
  Plan" action on the Show Import screen, which uses **every real prop in the imported layout** rather
  than the three manually-typed names the original Step 012 demo action requires.
- `PitchFrameNoteSegmenter`: new deterministic note-segmentation algorithm converting Melody's continuous
  per-frame pitch contour into the discrete `NoteEvent`s downstream analysis needs — no prior code in the
  repository performed this conversion.
- Consent-gated, SHA-256-verified download of the `sherpa-onnx-spleeter-2stems` separation model (Show
  Import screen → "Download separation model"), mirroring the existing ffmpeg download flow. See
  `THIRD-PARTY-NOTICES.md` for the disclosed, unresolved upstream weight-licensing question.

### Changed

- Non-WAV audio imports are now transcoded to **44.1 kHz stereo** rather than the source's own rate and
  channel count. The real separation model accepts only that exact format, so a 48 kHz or mono import
  previously probed successfully and then failed several steps later with a far-removed error.
- The transcode cache key now includes the transcoder's own output-format version, not just the source
  content hash, so changing an output setting can no longer serve a file produced under the previous
  settings (this repository's own "every cache key includes algorithm/model/settings versions" rule).

### Known limitations

- A **native** WAV that is not 44.1 kHz stereo is probed directly (never transcoded) and is therefore
  rejected by the separation model; the Show Import screen reports the measured mismatch plus what to do
  about it. Non-WAV imports are unaffected, being normalized automatically.
- Phrase-seam continuity and motif variation (`IPhraseSeamAssembler`/`IMotifVariationScheduler`) are
  deliberately **not** wired: generated effects are already phrase-bounded by construction so there is no
  seam to resolve, and motif variation intensity-scales effect parameters that the sole implemented
  effect recipe (`EffectRecipeRegistry.SimpleOnRecipeId`, a flat on/off) does not have. Both become
  meaningful once a richer effect recipe exists.
- The `Refinement` candidate/score/repair loop is likewise not wired; it requires a real rendering and
  quality-evaluation subsystem that does not exist yet.

## [0.16.0] - 2026-10-19

Release candidate produced at the completion of PHASE 16 ("Release Validation and Community Testing"),
Step 100 ("Finalize documentation, attribution/licenses, privacy defaults, diagnostics, changelog and
versioned release"). See `implementation-evidence/PHASE-16/STEP-100.md` for the full, real release-gate
evidence (compatibility matrix, A/B musicality trials, checksum manifest, documentation audit).

### Added

- Automated OS/xLights/audio/layout compatibility matrix with real XSQ smoke evidence (Step 098).
- Blind A/B musicality and manual-effort trial collection, randomized ordering, honest sample-size
  reporting (Step 099).
- Reproducible release-artifact checksum manifest (`ReleaseArtifactChecksumBuilder`), a real
  documentation/license audit (`ReleaseDocumentationAuditor`), and a final, auditable
  `IReleaseGate`/`ReleaseReadiness` decision that withholds release on any unresolved blocker (Step 100).
- First-run onboarding walkthrough (workspace folder selection, runtime capability probe, non-blocking
  startup diagnostics) — see PHASE-15 Step 095.

### Known limitations

- No real human beta-tester pool was available during automated evidence collection; A/B musicality
  ratings used in this evidence pass are disclosed, deterministic fixtures standing in for a genuine
  crowd study, never presented as real community feedback (`implementation-evidence/PHASE-16/STEP-099.md`).
- macOS/Linux apphost execution could not be exercised in the Windows-only sandboxed environment used to
  produce this release's automated evidence; required-asset presence was verified, actual process launch
  was not (`implementation-evidence/PHASE-15/STEP-097.md`).
- No real xLights installation is present in the environment that produced this release's automated
  evidence; XSQ artifacts are schema/structure-verified but not confirmed to render correctly in a real
  xLights session for this specific release build.
