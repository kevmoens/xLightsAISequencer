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
}
