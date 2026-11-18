using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Pipeline;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Director.Tests;

/// <summary>
/// Build-plan Phase 5 task 3/4: "structure the prompt so the large stable prefix... precedes the small
/// per-song volatile content" (architecture §14's own prompt-caching discipline). The real, checkable
/// property: the stable prefix must be byte-for-byte identical across two different songs sharing the
/// same knowledge context/style -- that identity is exactly what lets a provider's prompt cache actually
/// hit on the second call.
/// </summary>
public sealed class DirectorPromptBuilderTests
{
    private static BeatGrid BuildBeatGrid(double bpm) => new(
        Guid.NewGuid(), new SongId(Guid.NewGuid()), "hash", Guid.NewGuid(), Guid.NewGuid(), "t/1",
        new BeatGridSettings(4, 1000), new TempoHypothesis(bpm, 0.9, "4/4"), [], [], 0);

    private static SectionLabelAnalysis BuildSections(SongId songId) => new(
        Guid.NewGuid(), songId, "hash", Guid.NewGuid(), Guid.NewGuid(), "t/1",
        new SectionLabelSettings(0.95, 0.4, "t/1"),
        [new SectionLabelCandidate(Guid.NewGuid(), Guid.NewGuid(), 0, 8, SectionKind.Verse, 1.0, ["test"], false)]);

    private static PhraseSegmentationAnalysis BuildPhrases(SongId songId) => new(
        Guid.NewGuid(), songId, "hash", Guid.NewGuid(), Guid.NewGuid(), null, "t/1",
        new PhraseSegmentationSettings(4, 1, 0, "t/1"), []);

    private static MusicModel BuildMusicModel(double bpm, long durationTicks)
    {
        var songId = new SongId(Guid.NewGuid());
        return new MusicModel(songId, durationTicks, BuildBeatGrid(bpm), BuildSections(songId), BuildPhrases(songId), null, null);
    }

    private static BeatGrid BuildBeatGridWithRealBeats(SongId songId, double bpm, int beatCount, long beatSpacingTicks, int beatsPerBar) => new(
        Guid.NewGuid(), songId, "hash", Guid.NewGuid(), Guid.NewGuid(), "t/1",
        new BeatGridSettings(beatsPerBar, 1000), new TempoHypothesis(bpm, 0.9, "4/4"),
        Enumerable.Range(0, beatCount).Select(i => new BeatMarker(Guid.NewGuid(), i * beatSpacingTicks, i % beatsPerBar, i / beatsPerBar, 0.9, false)).ToArray(),
        [], 0);

    /// <summary>A song with real <see cref="BeatMarker"/>s and a real <see cref="BandEnergyEnvelope"/> (bar 0 pure bass) -- what §3a's real wiring actually produces, as opposed to <see cref="BuildMusicModel"/>'s no-beats/no-band-energy stand-in.</summary>
    private static MusicModel BuildMusicModelWithRealBandEnergy()
    {
        var songId = new SongId(Guid.NewGuid());
        const int sampleRate = 44100;
        var beatGrid = BuildBeatGridWithRealBeats(songId, bpm: 60, beatCount: 8, beatSpacingTicks: 10_000_000, beatsPerBar: 4);
        var bandEnergy = new BandEnergyEnvelope(
            Guid.NewGuid(), songId, "hash", "t/1", "t/1",
            new BandEnergySettings(new RhythmSettings(sampleRate, 2048, 512), 150, 4000),
            [new BandEnergyFrame(0, 8L * sampleRate, 1.0f, 0.2f, 0.1f)]);

        // 8 beats at 4 beats/bar = 2 real bars ([0s,4s) and [4s,8s)) -- the section below must span exactly
        // those 2 bars so BarGrid.WindowForBarRange resolves a real window, not null.
        var sections = new SectionLabelAnalysis(
            Guid.NewGuid(), songId, "hash", Guid.NewGuid(), Guid.NewGuid(), "t/1",
            new SectionLabelSettings(0.95, 0.4, "t/1"),
            [new SectionLabelCandidate(Guid.NewGuid(), Guid.NewGuid(), 0, 2, SectionKind.Verse, 1.0, ["test"], false)]);

        return new MusicModel(songId, 8 * 10_000_000, beatGrid, sections, BuildPhrases(songId), bandEnergy, null);
    }

    private static DirectorKnowledgeContext BuildKnowledge()
    {
        var vocabulary = RealXLightsFixture.IsAvailable
            ? RealXLightsFixture.BuildCatalogs().Vocabulary
            : throw new InvalidOperationException("Test only runs when the real vocabulary is available.");

        var craftRules = new CraftRules(DateTimeOffset.UtcNow, 0.7, 3,
            [new CraftRule("rule-1", "Verses hold a single colour.", 10, 9, 5, false)], []);
        var exemplars = new ExemplarIndex(DateTimeOffset.UtcNow, []);

        return new DirectorKnowledgeContext(vocabulary, craftRules, exemplars);
    }

    [Fact]
    public void Build_StablePrefix_IsIdenticalAcrossTwoDifferentSongs_SameKnowledgeAndStyle()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var knowledge = BuildKnowledge();
        var layout = new LayoutSummary(1, 100, [], []);

        var songA = BuildMusicModel(bpm: 120, durationTicks: 1_800_000_000);
        var songB = BuildMusicModel(bpm: 143.67, durationTicks: 4_000_000_000);

        var promptA = DirectorPromptBuilder.Build(knowledge, "High-Energy / EDM", songA, layout);
        var promptB = DirectorPromptBuilder.Build(knowledge, "High-Energy / EDM", songB, layout);

        Assert.Equal(promptA.StablePrefix, promptB.StablePrefix);
        Assert.NotEqual(promptA.VolatileSuffix, promptB.VolatileSuffix);
    }

    [Fact]
    public void Build_StablePrefix_NamesTheLongBaseLayerExpectation()
    {
        // Build-plan Phase 12 §3b: the Director must be explicitly told to give every section's subject
        // layer a long-duration base layer spanning most of that section's own window, additive to the
        // existing short accent/overlay layer guidance -- closing the "no anchor layer anywhere" gap the
        // 2026-10-30 real-run comparison measured.
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var knowledge = BuildKnowledge();
        var layout = new LayoutSummary(1, 100, [], []);
        var song = BuildMusicModel(bpm: 120, durationTicks: 1_800_000_000);

        var prompt = DirectorPromptBuilder.Build(knowledge, "High-Energy / EDM", song, layout);

        Assert.Contains("long-duration base layer spanning most of that section's own window", prompt.StablePrefix);
    }

    [Fact]
    public void Build_StablePrefix_ExplainsTravelAsDistinctFromPropDelays_AndShowsBothInTheRequiredShape()
    {
        // User feedback (2026-11-13): a real left-to-right/right-to-left sweep across a group's
        // individual members, landed on actual beats/words, needed its own declared mechanism distinct
        // from PropDelays' weight-based (non-beat-aligned) cascade -- and neither field previously
        // appeared in the literal required-JSON-shape example at all, only in prose.
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var knowledge = BuildKnowledge();
        var layout = new LayoutSummary(1, 100, [], []);
        var song = BuildMusicModel(bpm: 120, durationTicks: 1_800_000_000);

        var prompt = DirectorPromptBuilder.Build(knowledge, "High-Energy / EDM", song, layout);

        Assert.Contains("\"Travel\"", prompt.StablePrefix);
        Assert.Contains("LeftToRight", prompt.StablePrefix);
        Assert.Contains("RightToLeft", prompt.StablePrefix);
        Assert.Contains("\"Travel\": \"optional", prompt.StablePrefix); // present in the literal required-shape block, not just prose.
        Assert.Contains("\"PropDelays\": \"optional", prompt.StablePrefix);
        Assert.Contains("never combine with PropDelays", prompt.StablePrefix);
    }

    /// <summary>
    /// User feedback (2026-11-14): the Director's own craft guidance is now externalized into
    /// condition-tagged lesson files (<see cref="DirectorLessonCatalogLoader"/>) instead of always
    /// showing every bullet regardless of this song's own layout -- this is the direct, checkable proof
    /// that conditional inclusion is real, not just relocated text. A layout with no real multi-model
    /// group never shows the movement-across-models lesson; a layout that has one does.
    /// </summary>
    [Fact]
    public void Build_StablePrefix_MovementLesson_OnlyAppearsWhenLayoutHasAMultiModelGroup()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var knowledge = BuildKnowledge();
        var song = BuildMusicModel(bpm: 120, durationTicks: 1_800_000_000);
        const string movementPhrase = "Use a genuine MIX of all three real options";

        var withoutGroup = new LayoutSummary(1, 100, [], [], HasTreeOrMatrixModel: false, HasMultiModelGroupWithKnownPositions: false);
        var promptWithoutGroup = DirectorPromptBuilder.Build(knowledge, "High-Energy / EDM", song, withoutGroup);
        Assert.DoesNotContain(movementPhrase, promptWithoutGroup.StablePrefix);

        var withGroup = new LayoutSummary(1, 100, [], [], HasTreeOrMatrixModel: false, HasMultiModelGroupWithKnownPositions: true);
        var promptWithGroup = DirectorPromptBuilder.Build(knowledge, "High-Energy / EDM", song, withGroup);
        Assert.Contains(movementPhrase, promptWithGroup.StablePrefix);
    }

    [Fact]
    public void Build_StablePrefix_TreeOrMatrixOnlyLessons_OnlyAppearWhenLayoutHasATreeOrMatrixModel()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var knowledge = BuildKnowledge();
        var song = BuildMusicModel(bpm: 120, durationTicks: 1_800_000_000);
        const string treeMatrixPhrase = "Tree and Matrixes/Matrix-2/Matrix3";

        var withoutTreeOrMatrix = new LayoutSummary(1, 100, [], [], HasTreeOrMatrixModel: false, HasMultiModelGroupWithKnownPositions: false);
        var promptWithout = DirectorPromptBuilder.Build(knowledge, "High-Energy / EDM", song, withoutTreeOrMatrix);
        Assert.DoesNotContain(treeMatrixPhrase, promptWithout.StablePrefix);

        var withTreeOrMatrix = new LayoutSummary(1, 100, [], [], HasTreeOrMatrixModel: true, HasMultiModelGroupWithKnownPositions: false);
        var promptWith = DirectorPromptBuilder.Build(knowledge, "High-Energy / EDM", song, withTreeOrMatrix);
        Assert.Contains(treeMatrixPhrase, promptWith.StablePrefix);
    }

    [Fact]
    public void Build_StablePrefix_AlwaysLessons_AppearRegardlessOfLayout()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var knowledge = BuildKnowledge();
        var song = BuildMusicModel(bpm: 120, durationTicks: 1_800_000_000);
        var bareLayout = new LayoutSummary(1, 100, [], [], HasTreeOrMatrixModel: false, HasMultiModelGroupWithKnownPositions: false);

        var prompt = DirectorPromptBuilder.Build(knowledge, "High-Energy / EDM", song, bareLayout);

        Assert.Contains("Deliberate negative space", prompt.StablePrefix);
        Assert.Contains("Vary which effect carries each Look", prompt.StablePrefix);
    }

    [Fact]
    public void Build_StablePrefix_RequiresDistinctZPerLayer_AndShowsATwoLayerExample()
    {
        // Real-run finding (2026-11-11, project "Bang"): Show Bible generation exhausted every retry
        // attempt because nearly every multi-layer Look the model wrote left two layers both at Z=0 --
        // the required-JSON-shape example previously showed only a single layer, giving the model nothing
        // to generalize a second layer's Z from, especially now that the seed multi-layer templates
        // section (below) REQUIRES at least one 2-layer Look. The prompt must both say this explicitly and
        // show a concrete 2-layer example with distinct Z values.
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var knowledge = BuildKnowledge();
        var layout = new LayoutSummary(1, 100, [], []);
        var song = BuildMusicModel(bpm: 120, durationTicks: 1_800_000_000);

        var prompt = DirectorPromptBuilder.Build(knowledge, "High-Energy / EDM", song, layout);

        Assert.Contains("distinct \"Z\" value", prompt.StablePrefix);
        Assert.Contains("\"Z\": 0,", prompt.StablePrefix);
        Assert.Contains("\"Z\": 1,", prompt.StablePrefix);
    }

    [Fact]
    public void Build_StablePrefix_NamesTransientsAndVocalNotesAsValidTimescales_WithAWorkedExample()
    {
        // User follow-up (2026-11-10): measured directly against a real hand-sequenced reference show --
        // its dominant real emphasis technique was a short "On" flash timed to real detected hits, not the
        // regular bar/beat grid. The Director must know "Transients"/"VocalNotes" exist as real Timescale
        // choices, and why a short-duty, non-timing-track-bound layer bound to one of them is the
        // highest-leverage change for making emphasis actually read as emphasis.
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var knowledge = BuildKnowledge();
        var layout = new LayoutSummary(1, 100, [], []);
        var song = BuildMusicModel(bpm: 120, durationTicks: 1_800_000_000);

        var prompt = DirectorPromptBuilder.Build(knowledge, "High-Energy / EDM", song, layout);

        Assert.Contains("Transients|VocalNotes", prompt.StablePrefix);
        Assert.Contains("\"Timescale\": \"Transients\"", prompt.StablePrefix);
        Assert.Contains("real detected transient hit", prompt.StablePrefix);
    }

    [Fact]
    public void Build_VolatileSuffix_ReportsRealEmphasisTrackAvailability_WhenTransientHitsArePresent()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var knowledge = BuildKnowledge();
        var layout = new LayoutSummary(1, 100, [], []);
        var song = BuildMusicModel(bpm: 120, durationTicks: 1_800_000_000) with
        {
            TransientHits = [new XlightsAI.Studio.Rhythm.TransientHit(5_000_000, 0.9f), new XlightsAI.Studio.Rhythm.TransientHit(15_000_000, 0.8f)],
        };

        var prompt = DirectorPromptBuilder.Build(knowledge, "High-Energy / EDM", song, layout);

        Assert.Contains("\"Transients\" (2 real detected hits)", prompt.VolatileSuffix);
        Assert.Contains("no \"VocalNotes\" track (0 real detected sung-note onsets)", prompt.VolatileSuffix);
    }

    [Fact]
    public void Build_VolatileSuffix_OmitsEmphasisTrackAvailabilityLine_WhenNeitherSignalIsPresent()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var knowledge = BuildKnowledge();
        var layout = new LayoutSummary(1, 100, [], []);
        var song = BuildMusicModel(bpm: 120, durationTicks: 1_800_000_000);

        var prompt = DirectorPromptBuilder.Build(knowledge, "High-Energy / EDM", song, layout);

        Assert.DoesNotContain("Real emphasis timing tracks available", prompt.VolatileSuffix);
    }

    private static ExemplarIndex BuildManySameKindExemplars(SectionKind kind, int count) =>
        new(DateTimeOffset.UtcNow, Enumerable.Range(0, count)
            .Select(i => new ExemplarEntry(
                new ExemplarFingerprint($"Song {i}", CorpusProvenanceTag.UserAuthored, 0, 8, 120, i / (double)count, kind, null, null),
                []))
            .ToList());

    [Fact]
    public void Build_StayCloseToExemplars_True_IsDeterministicAcrossRepeatedCalls()
    {
        // User feedback (2026-10-31): the default ("stay close") behavior must remain exactly what it
        // was before -- the identical nearest-match exemplars every time, so the stable prefix (and the
        // provider's own prompt cache) stays stable across a plain regeneration.
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var vocabulary = RealXLightsFixture.BuildCatalogs().Vocabulary;
        var knowledge = new DirectorKnowledgeContext(
            vocabulary, new CraftRules(DateTimeOffset.UtcNow, 0.7, 3, [], []),
            BuildManySameKindExemplars(SectionKind.Chorus, 10));
        var layout = new LayoutSummary(1, 100, [], []);
        var song = BuildMusicModel(bpm: 120, durationTicks: 1_800_000_000);

        var promptA = DirectorPromptBuilder.Build(knowledge, "High-Energy / EDM", song, layout, new CustomGenerationPrompt("", StayCloseToExemplars: true));
        var promptB = DirectorPromptBuilder.Build(knowledge, "High-Energy / EDM", song, layout, new CustomGenerationPrompt("", StayCloseToExemplars: true));

        Assert.Equal(promptA.StablePrefix, promptB.StablePrefix);
    }

    [Fact]
    public void Build_StayCloseToExemplars_False_VariesTheRetrievedExemplarsAcrossCalls()
    {
        // User feedback (2026-10-31): unchecking "stay close" must actually change which exemplars get
        // quoted across repeated regenerations of the same song -- not just vary color downstream.
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var vocabulary = RealXLightsFixture.BuildCatalogs().Vocabulary;
        var knowledge = new DirectorKnowledgeContext(
            vocabulary, new CraftRules(DateTimeOffset.UtcNow, 0.7, 3, [], []),
            BuildManySameKindExemplars(SectionKind.Chorus, 10));
        var layout = new LayoutSummary(1, 100, [], []);
        var song = BuildMusicModel(bpm: 120, durationTicks: 1_800_000_000);

        var prefixes = Enumerable.Range(0, 20)
            .Select(_ => DirectorPromptBuilder.Build(knowledge, "High-Energy / EDM", song, layout, new CustomGenerationPrompt("", StayCloseToExemplars: false)).StablePrefix)
            .Distinct()
            .Count();

        // With 10 same-kind candidates and real randomness, 20 independent draws landing on the exact
        // same result every time is vanishingly unlikely -- this is a real behavior check, not a coin flip.
        Assert.True(prefixes > 1, "Expected at least one different retrieval across 20 'varied' Build calls.");
    }

    [Fact]
    public void Build_WithCustomPrompt_AppearsVerbatimInVolatileSuffixOnly_StablePrefixUnchanged()
    {
        // Build-plan Phase 14 §4 acceptance criteria: the custom-direction block must appear verbatim
        // (subject to the length cap) in the volatile suffix only, and the stable prefix must stay
        // byte-for-byte identical whether or not a custom prompt is supplied (prompt-cache safety).
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var knowledge = BuildKnowledge();
        var layout = new LayoutSummary(1, 100, [], []);
        var song = BuildMusicModel(bpm: 120, durationTicks: 1_800_000_000);
        const string customText = "Use a red and gold palette throughout; avoid blue.";

        var withoutCustomPrompt = DirectorPromptBuilder.Build(knowledge, "High-Energy / EDM", song, layout);
        var withCustomPrompt = DirectorPromptBuilder.Build(knowledge, "High-Energy / EDM", song, layout, new CustomGenerationPrompt(customText));
        var withEmptyCustomPrompt = DirectorPromptBuilder.Build(knowledge, "High-Energy / EDM", song, layout, CustomGenerationPrompt.Empty);

        Assert.Equal(withoutCustomPrompt.StablePrefix, withCustomPrompt.StablePrefix);
        Assert.Equal(withoutCustomPrompt.StablePrefix, withEmptyCustomPrompt.StablePrefix);

        Assert.DoesNotContain(customText, withoutCustomPrompt.VolatileSuffix);
        Assert.DoesNotContain(customText, withoutCustomPrompt.StablePrefix);
        Assert.Contains(customText, withCustomPrompt.VolatileSuffix);
        Assert.DoesNotContain(customText, withCustomPrompt.StablePrefix);

        // §2d: labelled as creative guidance, not a system instruction.
        Assert.Contains("User creative direction", withCustomPrompt.VolatileSuffix);
        Assert.Contains("not a system instruction", withCustomPrompt.VolatileSuffix);

        // A blank/empty custom prompt is the pre-existing default -- no block at all, not an empty one.
        Assert.DoesNotContain("User creative direction", withEmptyCustomPrompt.VolatileSuffix);
        Assert.Equal(withoutCustomPrompt.VolatileSuffix, withEmptyCustomPrompt.VolatileSuffix);
    }

    [Fact]
    public void Build_StablePrefix_NamesAConcreteMinimumLayerCountPerEnergyTier()
    {
        // Build-plan Phase 13 §2a: the Director must be told a concrete, stated minimum concurrent-layer
        // count per energy tier (not just "more layers"), closing the real-run comparison's own measured
        // gap (reference peaked at 8 layers on its richest prop/3 typical; the AI-generated version peaked
        // at 3/0-2).
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var knowledge = BuildKnowledge();
        var layout = new LayoutSummary(1, 100, [], []);
        var song = BuildMusicModel(bpm: 120, durationTicks: 1_800_000_000);

        var prompt = DirectorPromptBuilder.Build(knowledge, "High-Energy / EDM", song, layout);

        Assert.Contains("3 or more concurrent layers", prompt.StablePrefix);
    }

    [Fact]
    public void Build_StablePrefix_NamesTheRealEffectVocabularySpreadGap()
    {
        // Build-plan Phase 13 §1a/§2a: the real-run comparison measured a 22 vs 9 distinct-effect-name gap
        // -- the Director should be told to spread Look design across the vocabulary, not lean on 2 effects.
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var knowledge = BuildKnowledge();
        var layout = new LayoutSummary(1, 100, [], []);
        var song = BuildMusicModel(bpm: 120, durationTicks: 1_800_000_000);

        var prompt = DirectorPromptBuilder.Build(knowledge, "High-Energy / EDM", song, layout);

        Assert.Contains("22 distinct effect names", prompt.StablePrefix);
    }

    [Fact]
    public void Build_StablePrefix_NamesEverySeedMultiLayerTemplateAndItsCatalogId()
    {
        // Build-plan Phase 10 §6b task 3: the Director's own stable prefix must actually name the
        // shippable seed templates (and tell the model to set TemplateRef to the matching Id) -- a
        // Director that never sees this cannot possibly declare it later in a Look.
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var vocabulary = RealXLightsFixture.BuildCatalogs().Vocabulary;
        var craftRules = new CraftRules(DateTimeOffset.UtcNow, 0.7, 3, [], []);
        var knowledge = new DirectorKnowledgeContext(vocabulary, craftRules, new ExemplarIndex(DateTimeOffset.UtcNow, []), MultiLayerTemplateCatalogLoader.Load());
        var layout = new LayoutSummary(1, 100, [], []);
        var song = BuildMusicModel(bpm: 120, durationTicks: 1_800_000_000);

        var prompt = DirectorPromptBuilder.Build(knowledge, "High-Energy / EDM", song, layout);

        Assert.Contains("edm-dense-canvas-tree-matrix", prompt.StablePrefix);
        Assert.Contains("quiet-ballad-hold-plus-pop", prompt.StablePrefix);
        Assert.Contains("mid-energy-chase-and-accent-tree", prompt.StablePrefix);
        Assert.Contains("TemplateRef", prompt.StablePrefix);
    }

    [Fact]
    public void Build_StablePrefix_WithNoTemplateCatalog_StillBuildsAndDisclosesNoneAvailable()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var knowledge = BuildKnowledge();
        var layout = new LayoutSummary(1, 100, [], []);
        var song = BuildMusicModel(bpm: 120, durationTicks: 1_800_000_000);

        var prompt = DirectorPromptBuilder.Build(knowledge, "High-Energy / EDM", song, layout);

        Assert.Contains("(None available for this run.)", prompt.StablePrefix);
    }

    [Fact]
    public void Build_StablePrefix_ChangesWhenStyleChanges()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var knowledge = BuildKnowledge();
        var layout = new LayoutSummary(1, 100, [], []);
        var song = BuildMusicModel(bpm: 120, durationTicks: 1_800_000_000);

        var promptA = DirectorPromptBuilder.Build(knowledge, "High-Energy / EDM", song, layout);
        var promptB = DirectorPromptBuilder.Build(knowledge, "Traditional / Classic", song, layout);

        Assert.NotEqual(promptA.StablePrefix, promptB.StablePrefix);
    }

    [Fact]
    public void Build_StablePrefix_RestrictsEffectVocabularyToTheStyleEffectPool()
    {
        // Build-plan Phase 8 task 1 acceptance: two different styles produce visibly different effect
        // pools/density/palette guidance on the same song.
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var knowledge = BuildKnowledge();
        var layout = new LayoutSummary(1, 100, [], []);
        var song = BuildMusicModel(bpm: 120, durationTicks: 1_800_000_000);

        var traditional = DirectorPromptBuilder.Build(knowledge, "Traditional / Classic", song, layout);
        var edm = DirectorPromptBuilder.Build(knowledge, "High-Energy / EDM", song, layout);

        Assert.Contains("Garlands", traditional.StablePrefix);
        Assert.DoesNotContain("Shockwave", traditional.StablePrefix);

        Assert.Contains("Shockwave", edm.StablePrefix);
        Assert.DoesNotContain("Garlands", edm.StablePrefix);
    }

    [Fact]
    public void Build_StablePrefix_ExplainsHowToSatisfyThePerKindLookDiversityFloor()
    {
        // Real-run finding (2026-11-10): a real Director generation exhausted its retry budget against
        // ShowBibleValidator's own per-Kind diversity floor (look-diversity-per-kind) without ever
        // satisfying it -- the stable prefix told the Director to "design N distinct Looks" per repeating
        // Kind (volatile suffix) but never explained the actual mechanism (AllowedSectionKinds eligibility)
        // the validator checks, so this REQUIRED instruction was added to spell it out up front rather than
        // relying purely on a terse post-failure correction message to teach the model the rule.
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var knowledge = BuildKnowledge();
        var layout = new LayoutSummary(1, 100, [], []);
        var song = BuildMusicModel(bpm: 120, durationTicks: 1_800_000_000);

        var prompt = DirectorPromptBuilder.Build(knowledge, "Traditional / Classic", song, layout);

        Assert.Contains("REQUIRED (build-plan Phase 24 §2a", prompt.StablePrefix);
        Assert.Contains("AllowedSectionKinds", prompt.StablePrefix);
        Assert.Contains("eligible", prompt.StablePrefix);
    }

    [Fact]
    public void Build_StablePrefix_ContainsShowLengthGuidance()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var knowledge = BuildKnowledge();
        var layout = new LayoutSummary(1, 100, [], []);
        var song = BuildMusicModel(bpm: 120, durationTicks: 1_800_000_000);

        var prompt = DirectorPromptBuilder.Build(knowledge, "Traditional / Classic", song, layout);

        Assert.Contains("10-15 minute", prompt.StablePrefix);
    }

    [Fact]
    public void Build_VolatileSuffix_ContainsRealMeasuredTempoAndSections_NeverInvented()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var knowledge = BuildKnowledge();
        var layout = new LayoutSummary(1, 100, [], []);
        var song = BuildMusicModel(bpm: 143.67, durationTicks: 4_000_000_000);

        var prompt = DirectorPromptBuilder.Build(knowledge, "High-Energy / EDM", song, layout);

        Assert.Contains("143.7", prompt.VolatileSuffix);
        Assert.Contains("Verse", prompt.VolatileSuffix);
    }

    [Fact]
    public void Build_VolatileSuffix_NamesEveryBarRangeForARepeatingKind_WhenItOccursThreeOrMoreTimes()
    {
        // Build-plan Phase 24 §2b acceptance criterion: "A real Director prompt log shows the new
        // per-Kind repeat-structure line for any song with a 3+-occurrence Kind."
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var knowledge = BuildKnowledge();
        var layout = new LayoutSummary(1, 100, [], []);
        var song = BuildMusicModel(bpm: 120, durationTicks: 1_800_000_000);
        var songId = song.SongId;
        var repeatingSections = new SectionLabelAnalysis(
            Guid.NewGuid(), songId, "hash", Guid.NewGuid(), Guid.NewGuid(), "t/1",
            new SectionLabelSettings(0.95, 0.4, "t/1"),
            [
                new SectionLabelCandidate(Guid.NewGuid(), Guid.NewGuid(), 48, 64, SectionKind.Chorus, 1.0, ["test"], false),
                new SectionLabelCandidate(Guid.NewGuid(), Guid.NewGuid(), 112, 128, SectionKind.Chorus, 1.0, ["test"], false),
                new SectionLabelCandidate(Guid.NewGuid(), Guid.NewGuid(), 176, 192, SectionKind.Chorus, 1.0, ["test"], false),
                new SectionLabelCandidate(Guid.NewGuid(), Guid.NewGuid(), 240, 256, SectionKind.Chorus, 1.0, ["test"], false),
                new SectionLabelCandidate(Guid.NewGuid(), Guid.NewGuid(), 304, 328, SectionKind.Chorus, 1.0, ["test"], false),
            ]);
        song = song with { Sections = repeatingSections };

        var prompt = DirectorPromptBuilder.Build(knowledge, "High-Energy / EDM", song, layout);

        Assert.Contains("Chorus repeats 5 times:", prompt.VolatileSuffix);
        Assert.Contains("[48,64)", prompt.VolatileSuffix);
        Assert.Contains("[304,328)", prompt.VolatileSuffix);
        Assert.Contains("the last one is 24 bars, longer than the others", prompt.VolatileSuffix);
        Assert.Contains("design at least 3 visibly distinct Looks across these 5 occurrences", prompt.VolatileSuffix);
    }

    [Fact]
    public void Build_VolatileSuffix_OmitsRepeatStructureLine_WhenNoKindOccursThreeOrMoreTimes()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var knowledge = BuildKnowledge();
        var layout = new LayoutSummary(1, 100, [], []);
        var song = BuildMusicModel(bpm: 120, durationTicks: 1_800_000_000); // one lone Verse section

        var prompt = DirectorPromptBuilder.Build(knowledge, "High-Energy / EDM", song, layout);

        Assert.DoesNotContain("repeats", prompt.VolatileSuffix);
    }

    [Fact]
    public void Build_VolatileSuffix_NamesRealGroupsInBandEnergyMapping_WhenBandEnergyIsPresent()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var knowledge = BuildKnowledge();
        var layout = new LayoutSummary(
            ModelCount: 2,
            TotalPixels: 1300,
            Groups:
            [
                new LayoutSummaryEntry("group:Outline", 400, "high-resolution"),
                new LayoutSummaryEntry("group:MiniTrees", 100, "low-resolution"),
            ],
            TopPixelHeavyModels: [new LayoutSummaryEntry("Tree", 900, "high-resolution")]);
        var song = BuildMusicModelWithRealBandEnergy();

        var prompt = DirectorPromptBuilder.Build(knowledge, "High-Energy / EDM", song, layout);

        Assert.Contains("Band-to-group mapping", prompt.VolatileSuffix);
        Assert.Contains("group:Outline", prompt.VolatileSuffix);
        Assert.Contains("Tree", prompt.VolatileSuffix);
        Assert.Contains("group:MiniTrees", prompt.VolatileSuffix);
        Assert.Contains("bass-dominant", prompt.VolatileSuffix);
    }

    [Fact]
    public void Build_VolatileSuffix_RendersMeasuredEnergyTransitions_WhenPresent()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var knowledge = BuildKnowledge();
        var layout = new LayoutSummary(1, 100, [], []);
        var song = BuildMusicModel(bpm: 120, durationTicks: 1_800_000_000) with
        {
            EnergyTransitions = [new EnergyTransition(TimeSpan.FromSeconds(24).Ticks, EnergyTransitionKind.Drop, 0.5f)],
        };

        var prompt = DirectorPromptBuilder.Build(knowledge, "High-Energy / EDM", song, layout);

        Assert.Contains("Measured energy transitions", prompt.VolatileSuffix);
        Assert.Contains("0:24 drop", prompt.VolatileSuffix);
    }

    [Fact]
    public void Build_VolatileSuffix_OmitsMeasuredEnergyTransitions_WhenNoneDetected()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var knowledge = BuildKnowledge();
        var layout = new LayoutSummary(1, 100, [], []);
        var song = BuildMusicModel(bpm: 120, durationTicks: 1_800_000_000);

        var prompt = DirectorPromptBuilder.Build(knowledge, "High-Energy / EDM", song, layout);

        Assert.DoesNotContain("Measured energy transitions", prompt.VolatileSuffix);
    }

    [Fact]
    public void Build_VolatileSuffix_RendersMeasuredLeadInstrumentWindows_WhenPresent()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var knowledge = BuildKnowledge();
        var layout = new LayoutSummary(1, 100, [], []);
        var song = BuildMusicModel(bpm: 120, durationTicks: 1_800_000_000) with
        {
            LeadInstrumentWindows = [new LeadInstrumentWindow(TimeSpan.FromSeconds(8).Ticks, TimeSpan.FromSeconds(22).Ticks, 2.0)],
        };

        var prompt = DirectorPromptBuilder.Build(knowledge, "High-Energy / EDM", song, layout);

        Assert.Contains("Measured lead-instrument/section prominence windows", prompt.VolatileSuffix);
        Assert.Contains("0:08-0:22", prompt.VolatileSuffix);
    }

    [Fact]
    public void Build_VolatileSuffix_OmitsMeasuredLeadInstrumentWindows_WhenNoneDetected()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var knowledge = BuildKnowledge();
        var layout = new LayoutSummary(1, 100, [], []);
        var song = BuildMusicModel(bpm: 120, durationTicks: 1_800_000_000);

        var prompt = DirectorPromptBuilder.Build(knowledge, "High-Energy / EDM", song, layout);

        Assert.DoesNotContain("Measured lead-instrument/section prominence windows", prompt.VolatileSuffix);
    }

    [Fact]
    public void Build_VolatileSuffix_OmitsBandEnergyMapping_WhenNoBandEnergyIsAvailable()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var knowledge = BuildKnowledge();
        var layout = new LayoutSummary(1, 100, [], []);
        var song = BuildMusicModel(bpm: 120, durationTicks: 1_800_000_000);

        var prompt = DirectorPromptBuilder.Build(knowledge, "High-Energy / EDM", song, layout);

        Assert.DoesNotContain("Band-to-group mapping", prompt.VolatileSuffix);
    }

    /// <summary>Build-plan Phase 25 Part B.1: "Everything" covers 100% of this layout's own total pixels -- above the coverage threshold, so it classifies whole-canvas and gets its own section.</summary>
    private static LayoutSummary BuildLayoutWithWholeCanvasGroup() => new(
        ModelCount: 2,
        TotalPixels: 1000,
        Groups: [new LayoutSummaryEntry("group:Everything", 1000, "high-resolution")],
        TopPixelHeavyModels: []);

    [Fact]
    public void Build_VolatileSuffix_DisclosesNoTrainingData_WhenWholeCanvasGroupExistsButCoOccurrenceIsEmpty()
    {
        // Build-plan Phase 25 Part B.2: never fabricate usage-frequency guidance ahead of real data.
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var knowledge = BuildKnowledge();
        var layout = BuildLayoutWithWholeCanvasGroup();
        var song = BuildMusicModel(bpm: 120, durationTicks: 1_800_000_000);

        var prompt = DirectorPromptBuilder.Build(knowledge, "High-Energy / EDM", song, layout);

        Assert.Contains("## Whole-canvas group", prompt.VolatileSuffix);
        Assert.Contains("group:Everything", prompt.VolatileSuffix);
        Assert.Contains("no real corpus training data is available yet", prompt.VolatileSuffix);
    }

    [Fact]
    public void Build_VolatileSuffix_DisclosesMeasuredAbsence_WhenTrainingRanButFoundZeroWholeCanvasWindows()
    {
        // Build-plan Phase 25 Part C real-corpus finding: a real CorpusTrainingRunner run that completed
        // but genuinely found zero whole-canvas windows (WindowSampleCount == 0, a non-null stats object)
        // is a different fact than "training has never been run" (null) -- it must get its own message,
        // never the same "no training data is available yet" text that implies the tool was never run.
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var vocabulary = RealXLightsFixture.BuildCatalogs().Vocabulary;
        var coOccurrence = new WholeCanvasCoOccurrenceStats(
            WindowSampleCount: 0, SongsWithWholeCanvasCount: 0,
            FoundationCoFireRatio: null, SubjectCoFireRatio: null, AccentCoFireRatio: null,
            SectionKindHistogram: new Dictionary<string, int>(), EnergyTierHistogram: new Dictionary<string, int>(),
            MedianDurationBars: null, P90DurationBars: null);
        var knowledge = new DirectorKnowledgeContext(
            vocabulary, new CraftRules(DateTimeOffset.UtcNow, 0.7, 3, [], []), new ExemplarIndex(DateTimeOffset.UtcNow, []),
            WholeCanvasCoOccurrence: coOccurrence);
        var layout = BuildLayoutWithWholeCanvasGroup();
        var song = BuildMusicModel(bpm: 120, durationTicks: 1_800_000_000);

        var prompt = DirectorPromptBuilder.Build(knowledge, "High-Energy / EDM", song, layout);

        Assert.Contains("a real CorpusTrainer run completed and genuinely found no whole-canvas-group effect windows", prompt.VolatileSuffix);
        Assert.Contains("a measured absence, not an untrained gap", prompt.VolatileSuffix);
        Assert.DoesNotContain("no real corpus training data is available yet", prompt.VolatileSuffix);
    }

    [Fact]
    public void Build_VolatileSuffix_StatesMeasuredWholeCanvasUsageGuidance_WhenCoOccurrenceDataIsSupplied()
    {
        // Build-plan Phase 25 Part B.2: once real training data exists, state concrete, measured usage
        // guidance -- mirroring AppendBandEnergyGroupRoleMapping's own "measured for this song" framing.
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var vocabulary = RealXLightsFixture.BuildCatalogs().Vocabulary;
        var coOccurrence = new WholeCanvasCoOccurrenceStats(
            WindowSampleCount: 40, SongsWithWholeCanvasCount: 4,
            FoundationCoFireRatio: 0.2, SubjectCoFireRatio: 0.7, AccentCoFireRatio: 0.3,
            SectionKindHistogram: new Dictionary<string, int> { ["Chorus"] = 24, ["Bridge"] = 10, ["Verse"] = 6 },
            EnergyTierHistogram: new Dictionary<string, int> { ["High"] = 30, ["Mid"] = 8, ["Low"] = 2 },
            MedianDurationBars: 1.5, P90DurationBars: 2.0);
        var knowledge = new DirectorKnowledgeContext(
            vocabulary, new CraftRules(DateTimeOffset.UtcNow, 0.7, 3, [], []), new ExemplarIndex(DateTimeOffset.UtcNow, []),
            WholeCanvasCoOccurrence: coOccurrence);
        var layout = BuildLayoutWithWholeCanvasGroup();
        var song = BuildMusicModel(bpm: 120, durationTicks: 1_800_000_000);

        var prompt = DirectorPromptBuilder.Build(knowledge, "High-Energy / EDM", song, layout);

        Assert.Contains("measured across 4 real corpus song(s), 40 whole-canvas window(s)", prompt.VolatileSuffix);
        Assert.Contains("Subject 70%", prompt.VolatileSuffix);
        Assert.Contains("Foundation 20%", prompt.VolatileSuffix);
        Assert.Contains("Accent 30%", prompt.VolatileSuffix);
        Assert.Contains("Chorus (60%)", prompt.VolatileSuffix);
        Assert.Contains("High most often (75%", prompt.VolatileSuffix);
        Assert.Contains("median 1.5 bar(s), P90 2 bar(s)", prompt.VolatileSuffix);
    }

    /// <summary>Build-plan Phase 18 §6 (Phase E): the "## Effect vocabulary" section enriches each bare effect name with its real, known visual character when one exists in the supplied catalog.</summary>
    [Fact]
    public void Build_StablePrefix_EnrichesEffectNameWithCatalogDescription_WhenCatalogHasAnEntry()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var vocabulary = RealXLightsFixture.BuildCatalogs().Vocabulary;
        var craftRules = new CraftRules(DateTimeOffset.UtcNow, 0.7, 3, [], []);
        var catalog = new EffectVisualCharacterCatalog(DateTimeOffset.UtcNow,
            [new EffectVisualCharacter("Garlands", "High", "a steady repeating garland-like pattern", DateTimeOffset.UtcNow)]);
        var knowledge = new DirectorKnowledgeContext(vocabulary, craftRules, new ExemplarIndex(DateTimeOffset.UtcNow, []), Templates: null, EffectVisualCharacters: catalog);
        var layout = new LayoutSummary(1, 100, [], []);
        var song = BuildMusicModel(bpm: 120, durationTicks: 1_800_000_000);

        var prompt = DirectorPromptBuilder.Build(knowledge, "Traditional / Classic", song, layout);

        Assert.Contains("Garlands -- a steady repeating garland-like pattern", prompt.StablePrefix);
    }

    /// <summary>A documented-behavior entry (an effect this pipeline's own isolated fixture could not meaningfully exercise) is surfaced in preference to the bare AI-observed description, since it says what the effect is actually FOR.</summary>
    [Fact]
    public void Build_StablePrefix_PrefersDocumentedBehaviorOverAiDescription_WhenBothArePresent()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var vocabulary = RealXLightsFixture.BuildCatalogs().Vocabulary;
        var craftRules = new CraftRules(DateTimeOffset.UtcNow, 0.7, 3, [], []);
        var catalog = new EffectVisualCharacterCatalog(DateTimeOffset.UtcNow,
        [
            new EffectVisualCharacter(
                "Garlands", "High", "completely dark, no visible motion", DateTimeOffset.UtcNow,
                DocumentedBehavior: "Real manual-documented purpose of Garlands.",
                DocumentationSource: "https://manual.xlights.org/xlights/effects/off/garlands.md"),
        ]);
        var knowledge = new DirectorKnowledgeContext(vocabulary, craftRules, new ExemplarIndex(DateTimeOffset.UtcNow, []), Templates: null, EffectVisualCharacters: catalog);
        var layout = new LayoutSummary(1, 100, [], []);
        var song = BuildMusicModel(bpm: 120, durationTicks: 1_800_000_000);

        var prompt = DirectorPromptBuilder.Build(knowledge, "Traditional / Classic", song, layout);

        Assert.Contains("Real manual-documented purpose of Garlands.", prompt.StablePrefix);
        Assert.DoesNotContain("completely dark, no visible motion", prompt.StablePrefix);
    }

    /// <summary>When only a Low-tier entry exists for an effect, that is the one surfaced (there is no High-tier entry to prefer).</summary>
    [Fact]
    public void Build_StablePrefix_FallsBackToLowTier_WhenOnlyLowTierCatalogEntryExists()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var vocabulary = RealXLightsFixture.BuildCatalogs().Vocabulary;
        var craftRules = new CraftRules(DateTimeOffset.UtcNow, 0.7, 3, [], []);
        var catalog = new EffectVisualCharacterCatalog(DateTimeOffset.UtcNow,
            [new EffectVisualCharacter("Garlands", "Low", "a low-resolution-only description", DateTimeOffset.UtcNow)]);
        var knowledge = new DirectorKnowledgeContext(vocabulary, craftRules, new ExemplarIndex(DateTimeOffset.UtcNow, []), Templates: null, EffectVisualCharacters: catalog);
        var layout = new LayoutSummary(1, 100, [], []);
        var song = BuildMusicModel(bpm: 120, durationTicks: 1_800_000_000);

        var prompt = DirectorPromptBuilder.Build(knowledge, "Traditional / Classic", song, layout);

        Assert.Contains("Garlands -- a low-resolution-only description", prompt.StablePrefix);
    }

    /// <summary>Build-plan Phase 18 §6 acceptance criterion: no catalog (every existing caller before this phase) renders byte-for-byte the same "## Effect vocabulary" section as before -- an additive enrichment only, never a regression to the existing exact-bare-name assertions.</summary>
    [Fact]
    public void Build_StablePrefix_IsByteForByteIdentical_WhenNoEffectVisualCharacterCatalogIsSupplied()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var layout = new LayoutSummary(1, 100, [], []);
        var song = BuildMusicModel(bpm: 120, durationTicks: 1_800_000_000);

        var withoutExplicitDefault = DirectorPromptBuilder.Build(BuildKnowledge(), "Traditional / Classic", song, layout);

        var vocabulary = RealXLightsFixture.BuildCatalogs().Vocabulary;
        var craftRules = new CraftRules(DateTimeOffset.UtcNow, 0.7, 3,
            [new CraftRule("rule-1", "Verses hold a single colour.", 10, 9, 5, false)], []);
        var withExplicitNullCatalog = new DirectorKnowledgeContext(
            vocabulary, craftRules, new ExemplarIndex(DateTimeOffset.UtcNow, []), Templates: null, EffectVisualCharacters: null);
        var explicitPrompt = DirectorPromptBuilder.Build(withExplicitNullCatalog, "Traditional / Classic", song, layout);

        Assert.Equal(withoutExplicitDefault.StablePrefix, explicitPrompt.StablePrefix);

        // An empty (but non-null) catalog has no entry for any effect, so it must also be a no-op.
        var withEmptyCatalog = withExplicitNullCatalog with { EffectVisualCharacters = new EffectVisualCharacterCatalog(DateTimeOffset.UtcNow, []) };
        var emptyPrompt = DirectorPromptBuilder.Build(withEmptyCatalog, "Traditional / Classic", song, layout);

        Assert.Equal(withoutExplicitDefault.StablePrefix, emptyPrompt.StablePrefix);
    }

    /// <summary>
    /// Build-plan Phase 25 Part E.1: <see cref="DirectorPromptBuilder.Build"/> now owns one
    /// <see cref="Pipeline.ExemplarReuseTracker"/> across its whole retrieved-exemplars loop over every
    /// <see cref="SectionKind"/> -- with only one real same-kind entry in the index, every other Kind's
    /// query falls back to that same entry, so it must show up drawn more than once in the returned report.
    /// </summary>
    [Fact]
    public void Build_ExemplarReuse_RecordsEveryDrawAcrossTheWholeKindLoop()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var vocabulary = RealXLightsFixture.BuildCatalogs().Vocabulary;
        var onlyEntry = new ExemplarEntry(
            new ExemplarFingerprint("Only Song", CorpusProvenanceTag.UserAuthored, 0, 8, 120, 0.5, SectionKind.Verse, null, null), []);
        var knowledge = new DirectorKnowledgeContext(
            vocabulary, new CraftRules(DateTimeOffset.UtcNow, 0.7, 3, [], []), new ExemplarIndex(DateTimeOffset.UtcNow, [onlyEntry]));
        var layout = new LayoutSummary(1, 100, [], []);
        var song = BuildMusicModel(bpm: 120, durationTicks: 1_800_000_000);

        var prompt = DirectorPromptBuilder.Build(knowledge, "High-Energy / EDM", song, layout);

        Assert.NotNull(prompt.ExemplarReuse);
        var key = ExemplarOverrides.KeyFor(onlyEntry.Fingerprint);
        Assert.True(prompt.ExemplarReuse!.DrawCountByExemplarKey[key] > 1,
            "The only real exemplar in the index should be drawn as the cross-kind fallback for every other SectionKind, so its own draw count should exceed 1.");
    }

    /// <summary>
    /// Build-plan Phase 25 Part D.4/loose end: a peak-energy <see cref="SectionKind"/> (Chorus, whose
    /// <see cref="DefaultEnergyCeilings.ArchitectureSection10"/> ceiling is 0.85) must request a genuinely
    /// layered exemplar when one exists in the index -- retrieval should prefer the real 3-layer entry over
    /// an otherwise-closer-matching 0-layer one.
    /// </summary>
    [Fact]
    public void Build_StablePrefix_RequestsALayeredExemplarForAPeakEnergySectionKind()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var vocabulary = RealXLightsFixture.BuildCatalogs().Vocabulary;
        var layered = new ExemplarEntry(
            new ExemplarFingerprint("Layered Chorus", CorpusProvenanceTag.UserAuthored, 0, 8, 120, 0.5, SectionKind.Chorus, null, null, PeakConcurrentLayerCount: 3),
            []);
        var flat = new ExemplarEntry(
            new ExemplarFingerprint("Flat Chorus", CorpusProvenanceTag.UserAuthored, 0, 8, 120, 0.5, SectionKind.Chorus, null, null, PeakConcurrentLayerCount: 0),
            []);
        var knowledge = new DirectorKnowledgeContext(
            vocabulary, new CraftRules(DateTimeOffset.UtcNow, 0.7, 3, [], []), new ExemplarIndex(DateTimeOffset.UtcNow, [flat, layered]));
        var layout = new LayoutSummary(1, 100, [], []);
        var song = BuildMusicModel(bpm: 120, durationTicks: 1_800_000_000);

        var prompt = DirectorPromptBuilder.Build(knowledge, "High-Energy / EDM", song, layout);

        Assert.Contains("Layered Chorus", prompt.StablePrefix);
    }

    /// <summary>
    /// Build-plan Phase 25 Part D.4: when nothing in the index reaches the complexity floor for a
    /// peak-energy Kind, retrieval must still fall back to the plain match rather than silently showing
    /// zero exemplars for that Kind.
    /// </summary>
    [Fact]
    public void Build_StablePrefix_FallsBackWhenNoExemplarMeetsTheComplexityFloor()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var vocabulary = RealXLightsFixture.BuildCatalogs().Vocabulary;
        var flatOnly = new ExemplarEntry(
            new ExemplarFingerprint("Flat Chorus Only", CorpusProvenanceTag.UserAuthored, 0, 8, 120, 0.5, SectionKind.Chorus, null, null, PeakConcurrentLayerCount: 0),
            []);
        var knowledge = new DirectorKnowledgeContext(
            vocabulary, new CraftRules(DateTimeOffset.UtcNow, 0.7, 3, [], []), new ExemplarIndex(DateTimeOffset.UtcNow, [flatOnly]));
        var layout = new LayoutSummary(1, 100, [], []);
        var song = BuildMusicModel(bpm: 120, durationTicks: 1_800_000_000);

        var prompt = DirectorPromptBuilder.Build(knowledge, "High-Energy / EDM", song, layout);

        Assert.Contains("Flat Chorus Only", prompt.StablePrefix);
    }

    /// <summary>Build-plan Phase 25 loose end: no training pass ever run against this knowledge directory -- a disclosed-empty message, never a fabricated "zero real windows" claim.</summary>
    [Fact]
    public void Build_StablePrefix_WithNoLayerComplexityStats_DisclosesNoneAvailable()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var layout = new LayoutSummary(1, 100, [], []);
        var song = BuildMusicModel(bpm: 120, durationTicks: 1_800_000_000);

        var prompt = DirectorPromptBuilder.Build(BuildKnowledge(), "Traditional / Classic", song, layout);

        Assert.Contains("Real corpus layer-complexity baseline", prompt.StablePrefix);
        Assert.Contains("None available for this run", prompt.StablePrefix);
    }

    /// <summary>Build-plan Phase 25 loose end: real measured LayerComplexityStats render concrete tier/section/duration numbers instead of the disclosed-empty message.</summary>
    [Fact]
    public void Build_StablePrefix_WithRealLayerComplexityStats_RendersMeasuredTierNumbers()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var vocabulary = RealXLightsFixture.BuildCatalogs().Vocabulary;
        var stats = new LayerComplexityStats(
            ThreePlus: new LayerComplexityTierStats(12, new Dictionary<string, int> { ["Chorus"] = 9 }, 2.0, 4.0),
            FourPlus: new LayerComplexityTierStats(3, new Dictionary<string, int> { ["Chorus"] = 3 }, 1.5, 2.0),
            FivePlus: new LayerComplexityTierStats(0, new Dictionary<string, int>(), null, null));
        var knowledge = new DirectorKnowledgeContext(
            vocabulary, new CraftRules(DateTimeOffset.UtcNow, 0.7, 3, [], []), new ExemplarIndex(DateTimeOffset.UtcNow, []),
            LayerComplexity: stats);
        var layout = new LayoutSummary(1, 100, [], []);
        var song = BuildMusicModel(bpm: 120, durationTicks: 1_800_000_000);

        var prompt = DirectorPromptBuilder.Build(knowledge, "Traditional / Classic", song, layout);

        Assert.Contains("12 real window(s)", prompt.StablePrefix);
        Assert.Contains("5+ layers: no real window reached this tier.", prompt.StablePrefix);

        var sectionStart = prompt.StablePrefix.IndexOf("Real corpus layer-complexity baseline", StringComparison.Ordinal);
        var sectionEnd = prompt.StablePrefix.IndexOf("## ", sectionStart + 1, StringComparison.Ordinal);
        var layerComplexitySection = prompt.StablePrefix[sectionStart..(sectionEnd < 0 ? prompt.StablePrefix.Length : sectionEnd)];
        Assert.DoesNotContain("None available for this run", layerComplexitySection);
    }
}
