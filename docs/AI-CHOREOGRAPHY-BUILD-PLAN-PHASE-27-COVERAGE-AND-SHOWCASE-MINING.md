# Phase 27 -- Group coverage, showcase mining, rhythmic-stab lesson, lead-instrument signal

## Why this phase exists

A fresh, fully-updated regeneration of Carol of the Bells (2026-11-14, after the negative-space
scoping, locally-adaptive transient detection, lessons-library, and retry-budget fixes earlier the
same session) was compared directly against real effect counts on disk, and a real 5-layer `Tree`
example from another real show (`All I Want For Christmas Is You shortened.xsq`) was reviewed by the
user. Both produced concrete, well-evidenced gaps documented here.

## Findings

1. **The generated show never once used `Everything`, `Roof`, `Roof and uprights`, or
   `Horizontals`** as their own real group-level effects, despite the Director's own prompt listing
   all four as real groups in this layout. No existing validator rule can catch this: `RuleModelCoverage`
   only checks *member*-level coverage (satisfiable by some other effect entirely), and
   `RuleIndividualModelDiversity` only applies to groups with 3+ *positioned* members and only fires
   on "same single member repeated," never on "never targeted as a group at all."
2. **Matrix/Matrix-2/Tree were nearly empty** (1-2 layers, as few as 2 total effects on `Tree`)
   against the original hand-made show's 3 layers / 76 effects.
3. **During the song's horn phrase (00:08-00:22)**, the generated show runs one generic
   synchronized flourish across a dozen unrelated props at the same instant; the original runs a
   tight, short-duty, REPEATING "stab" pattern (450ms Twinkle+On+Bars stacks, staccato Shockwave
   bursts) -- structurally different, not just differently timed. Nothing in the prompt currently
   describes this "stab" shape as its own technique.
4. **The real 5-layer `Tree` example contributes nothing to the mined-template catalog.** Confirmed
   not a scan/discovery miss: `CorpusManifestBuilder.cs`'s `KnownResolutions[0]` deliberately records
   it as a duplicate of a different canonical file and silently folds it in during manifest `Build()`.
   Same song's audio does not guarantee the same real choreography, and this file's own real layering
   is being thrown away on that assumption.
5. **Even where mining finds a rich pattern, the promotion bar throws away good single-song
   examples.** `MultiLayerStackCandidate` already carries `SongCount`, but `MultiLayerTemplate` has
   no field marking "observed once, inspirational" vs. "corpus-validated," and no code path converts
   an unpromoted candidate into a template at all.
6. **Nothing measures "a real lead instrument/section is prominent right now."** No prominence/
   contrast/salience DSP exists anywhere. `BandEnergyEnvelope` already has real per-frame Mid/High
   energy and a real `AverageInWindow` -- just no baseline/contrast computation on top of it yet.

## Approach, item by item

### 1. Fix the duplicate-resolution assumption (small)

`src/XlightsAI.Studio.Knowledge/CorpusManifestBuilder.cs`, `KnownResolutions[0]` (~line 52-59):
remove `"All I Want For Christmas Is You shortened.xsq"` from that entry's `DuplicateRelativePaths`
and add it as its OWN standalone `Resolution` entry (reusing the same real audio file,
`All I Want For Christmas Is You shortened.mp3`, since it is genuinely the same song's audio even if
the choreography differs) so it is mined as its own distinct corpus entry instead of discarded.

### 2. "Showcase" single-song mining tier (medium)

- `src/XlightsAI.Studio.Knowledge/MultiLayerTemplate.cs` -- add `ConfidenceTier` (defaulting to
  `"corpus-validated"` so every existing seed/mined template keeps compiling/behaving identically).
- `src/XlightsAI.Studio.Pipeline/MultiLayerStackMiner.cs` -- add `BuildShowcase(songs,
  minDistinctLayers)` reusing the existing `BuildAggregates`/`BuildCandidates` machinery, selecting
  candidates below the real promotion bar but still a genuine 3+-layer stack, converting each via a
  new overload of `BuildTemplate` that sets `ConfidenceTier: "single-song-showcase"` and an honest
  `SourceNote`.
- `tools/CorpusMiner/Program.cs` -- write the showcase catalog to a new additive
  `multilayer-templates-showcase.json`, never replacing the existing mined file.
- `src/XlightsAI.Studio.Knowledge/MultiLayerTemplateCatalogLoader.cs` -- merge the showcase file too
  (same additive, never-fatal-on-missing/corrupt pattern as the mined file).
- `src/XlightsAI.Studio.Director/DirectorPromptBuilder.cs` (`AppendMultiLayerTemplates`) -- say so
  explicitly in the prompt text when a template's `ConfidenceTier` is `"single-song-showcase"`.

### 3+4. A real "group never directly used" advisory rule (small-medium)

`src/XlightsAI.Studio.Choreography/ChoreographyLegalityValidator.cs` -- new rule
`RuleGroupNeverTargetedDirectly`, same soft/advisory tier as `RuleModelCoverage`/
`RuleIndividualModelDiversity`, in `ValidateShow`: for every real `layout.Groups` entry, check
whether any compiled effect's `Target` is exactly that group's own `ModelId` -- independent of
whether its members happen to be covered some other way. No minimum member count or position
requirement (unlike `RuleIndividualModelDiversity`).

### 5. A "rhythmic stab" lesson (small)

New `src/XlightsAI.Studio.Knowledge/SeedData/director-lessons/rhythmic-stab-pattern.md`
(`applies: always`): describes a short-duty (under ~500ms per hit), REPEATING, timing-track-bound
stack, or a staccato single-effect burst pattern -- distinct from a single accent flash
(hit-alignment-emphasis lesson) and a long held base layer (long-base-layer lesson).

### 6. Lead-instrument-prominence window detector (largest lift)

- New `src/XlightsAI.Studio.Rhythm/LeadInstrumentWindow.cs` and `LeadInstrumentWindowDetector.cs` --
  pure computation over an existing `BandEnergyEnvelope`: a trailing rolling-baseline average of
  Mid+High energy, windows where current energy exceeds that baseline by a real disclosed
  multiplier for at least a real minimum duration (reject single-frame spikes -- `TransientHitDetector`
  already covers those). Mirrors `EnergyTransitionDetector`'s existing before/after-window pattern,
  sustained rather than instantaneous.
- `src/XlightsAI.Studio.Director/MusicModel.cs` -- new nullable `LeadInstrumentWindows` field.
- `src/XlightsAI.Studio.Pipeline/CorpusSongAnalyzer.cs` /
  `src/XlightsAI.Studio.Director/AiChoreographyWholeSongGenerator.cs` -- run the detector wherever
  `BandEnergy` is already computed.
- `DirectorPromptBuilder.cs` -- new `AppendLeadInstrumentWindows` method (same disclosed-empty-when-
  null idiom as every other optional `MusicModel` signal).

**Calibration caveat**: the rolling-baseline window length and prominence-multiplier threshold need
real-song verification before trusting the exact numbers -- implemented as named
constants/settings so it is one real, measurable tuning pass after the mechanism lands, not a second
guess baked into the detector itself.

## Out of scope this phase

- Re-running the full corpus mining pipeline (hours-long, API-cost-bearing) -- a separate,
  explicit, user-triggered follow-up once this lands.
- Retrofitting `EmphasisContrastPlanner` to consume a sustained window -- confirmed it would need
  new logic, not a parameter swap; the new lead-instrument signal and stab lesson are independent,
  additive instead.
- A hard validator floor for `RuleGroupNeverTargetedDirectly` -- advisory-only, no calibrated "how
  often should Everything really fire" number exists yet.

## Verification

1. `dotnet build`/`dotnet test` every touched project -- all green.
2. New `LeadInstrumentWindowDetectorTests.cs`: a synthetic envelope with a clear sustained
   elevation over a known span produces exactly one window matching it; a brief single-frame spike
   produces none.
3. New `ChoreographyLegalityValidatorTests.cs` cases: a group used directly at least once never
   flags; a group whose members are covered only via some OTHER target (never the group itself) does.
4. Manually re-run `DirectorPromptBuilder.Build` against the real Carol of the Bells data already
   used this session and confirm the new lesson/signals appear as expected.
5. The corpus-manifest fix and a real re-mine to populate the showcase catalog are a real,
   explicit, user-triggered follow-up action, not part of this phase's own automated verification.
