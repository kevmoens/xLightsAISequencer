using System.Text.Json;
using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Pipeline;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Director.Tests;

/// <summary>
/// Real-run finding (2026-11-08): a user's post-processing touch-up prompt crashed with
/// "SectionScript for section '...' declares 1 phrase-scoped variation(s), but no phrase segmentation was
/// supplied for that section" -- even though that exact section's script had already compiled fine during
/// its own original generation. Root cause: <see cref="ChoreographyGenerationPipeline.ApplyPostProcessingPromptAsync"/>
/// reconstructs its own <see cref="MusicModel"/> from the same cached analysis stages rather than reusing
/// the original run's own instance, and Structure's own section-label/phrase classifiers are not
/// guaranteed to reproduce byte-identical phrase data for every section (a single-bar section's own phrase
/// boundary is the observed real case) across two separate analysis passes -- so a section's own untouched,
/// previously-valid <see cref="SectionScript"/> (reused verbatim by <see cref="TargetedSectionRegenerator"/>
/// for every section a touch-up doesn't target) can end up compiled against phrase data that has since
/// drifted out from under it. These tests prove the compile step now degrades that one section's own
/// stale phrase-scoped staging back to its base Look instead of crashing the whole touch-up.
/// </summary>
public sealed class ChoreographyGenerationPipelinePhraseDriftTests
{
    private static EffectVocabularyCatalog FakeVocabulary() => new(
        RealXLightsFixture.SixCommonEffectNames.ToDictionary(name => name, name => new EffectVocabulary(name, "E_", []), StringComparer.Ordinal),
        buffer: new EffectVocabulary("Buffer", "B_", []),
        blending: new EffectVocabulary("Blending", "T_", []),
        color: new EffectVocabulary("Color", "C_", []));

    private static ShowBible BuildBible()
    {
        var palettes = new List<ShowBiblePalette> { new("warm-gold", "Warm Gold", [new ShowBiblePaletteColor("#FFC880", PaletteRole.Base)]) };
        var look = new Look(
            Id: "golden-hold", Name: "Golden Hold", Intent: "Warm full-house steady state.", EnergyCeiling: 0.45,
            Motion: "hold", PaletteRef: "warm-gold", Subject: "group:Outline",
            Layers: [new LookLayer(1, "group:Outline", "On", LookLayer.RoleSubject, new Dictionary<string, string>(), "Normal", "Bars", 0.9)]);
        var content = new ShowBibleContent(
            Palettes: palettes,
            PropRoles: [],
            Looks: new LookLibrary([look]),
            EnergyArc: Enum.GetValues<SectionKind>().Select(k => new EnergyArcEntry(k, 0.5, 0.5, 2)).ToList(),
            PeakMoments: [],
            SignatureGestures: [],
            Withholding: new WithholdingPlan([], "none"));
        return new ShowBible(Guid.NewGuid(), Guid.NewGuid(), content, DateTimeOffset.UtcNow);
    }

    /// <summary>5 bars @ 60bpm/4-4 (bar = 4s): section A is bars [0,4) (the touch-up's own target), section B is the single bar [4,5) -- the real crash's own "very short section" shape.</summary>
    private static (BeatGrid BeatGrid, SectionLabelAnalysis Sections, Guid SectionAId, Guid SectionBId) BuildStructure(SongId songId)
    {
        const long beatSpacingTicks = 10_000_000; // 1s/beat at this TicksPerSecond convention used throughout this test project.
        const int beatsPerBar = 4;
        var beatGrid = new BeatGrid(
            Guid.NewGuid(), songId, "hash", Guid.NewGuid(), Guid.NewGuid(), "t/1",
            new BeatGridSettings(beatsPerBar, 1000), new TempoHypothesis(60, 0.9, "4/4"),
            Enumerable.Range(0, 20).Select(i => new BeatMarker(Guid.NewGuid(), i * beatSpacingTicks, i % beatsPerBar, i / beatsPerBar, 0.9, false)).ToArray(),
            [], 0);

        var sectionAId = Guid.NewGuid();
        var sectionBId = Guid.NewGuid();
        var sections = new SectionLabelAnalysis(
            Guid.NewGuid(), songId, "hash", Guid.NewGuid(), Guid.NewGuid(), "t/1",
            new SectionLabelSettings(0.95, 0.4, "t/1"),
            [
                new SectionLabelCandidate(sectionAId, Guid.NewGuid(), 0, 4, SectionKind.Verse, 1.0, ["test"], false),
                new SectionLabelCandidate(sectionBId, Guid.NewGuid(), 4, 5, SectionKind.Bridge, 1.0, ["test"], false),
            ]);
        return (beatGrid, sections, sectionAId, sectionBId);
    }

    private static PhraseCandidate Phrase(Guid sectionId, int start, int endExclusive, long startTicks, long endTicks) => new(
        Guid.NewGuid(), sectionId, start, endExclusive, startTicks, endTicks, endExclusive - start,
        PhraseBoundarySource.BarHypothesis, 1.0, ["test"], "sig", false);

    [Fact]
    public async Task ApplyPostProcessingPromptAsync_UntouchedSectionsStalePhraseVariation_DegradesInsteadOfThrowing()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return; // ChoreographyEngine needs a real layout to resolve "group:Outline" -- same convention as neighboring tests.
        }

        var layout = await new ShowLayoutImporter().ImportReadOnlyAsync(RealXLightsFixture.ShowDirectory, CancellationToken.None);
        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var registry = new EffectRecipeRegistry(recipes);
        var layoutSummary = LayoutSummary.Build(layout);

        var songId = new SongId(Guid.NewGuid());
        var (beatGrid, sections, sectionAId, sectionBId) = BuildStructure(songId);

        // Only section A has a real phrase at touch-up time -- section B's prior phrase has since gone
        // missing (the real run's own observed drift), yet section B's EXISTING script (below) still
        // declares a phrase-scoped variation for it, carried over unchanged by TargetedSectionRegenerator
        // because this touch-up never targets section B at all.
        var phrases = new PhraseSegmentationAnalysis(
            Guid.NewGuid(), songId, "hash", Guid.NewGuid(), Guid.NewGuid(), null, "t/1",
            new PhraseSegmentationSettings(4, 1, 0, "t/1"),
            [Phrase(sectionAId, 0, 4, 0, 40_000_000)]);

        var music = new MusicModel(songId, 200_000_000, beatGrid, sections, phrases, BandEnergy: null, VocalActivity: null);

        var bible = BuildBible();
        var settingsRoot = Path.Combine(Path.GetTempPath(), "xlai-phrase-drift-" + Guid.NewGuid().ToString("N"));
        try
        {
            var settings = new ChoreographyGenerationSettings(
                layout, vocabulary, recipes, registry, new Dictionary<SectionKind, double> { [SectionKind.Verse] = 0.5, [SectionKind.Bridge] = 0.3 }, settingsRoot);

            var existingScripts = new Dictionary<Guid, SectionScript>
            {
                [sectionAId] = new(sectionAId, "golden-hold", FillerEffects: RealXLightsFixture.SixCommonEffectNames),
                [sectionBId] = new(sectionBId, "golden-hold", PhraseVariations: [new SectionPhraseVariation(PhraseIndex: 1)], FillerEffects: RealXLightsFixture.SixCommonEffectNames),
            };

            var regeneratedSectionAScript = new SectionScript(sectionAId, "golden-hold", FillerEffects: RealXLightsFixture.SixCommonEffectNames);
            var fakeResponse = new ChatCompletionResult(JsonSerializer.Serialize(regeneratedSectionAScript), new AiUsage(10, 5, null, null));
            var fake = new FakeChatCompletionClient(Enumerable.Repeat<Func<ChatCompletionRequest, ChatCompletionResult>>(_ => fakeResponse, 3));

            var pipeline = new ChoreographyGenerationPipeline(
                new DirectorKnowledgeContext(vocabulary, new CraftRules(DateTimeOffset.UtcNow, 0.7, 3, [], []), new ExemplarIndex(DateTimeOffset.UtcNow, [])),
                "High-Energy / EDM", directorClient: null, directorModelName: null, fake, "fake-model", maxOutputTokens: 500);

            // "at 0:00" sits inside section A's own window [0s, 16s) -- section B is never targeted.
            var report = await pipeline.ApplyPostProcessingPromptAsync(
                music, layoutSummary, settings, Guid.NewGuid(), bible, existingScripts, "at 0:00 do a color change", CancellationToken.None);

            Assert.NotNull(report);
            Assert.NotNull(report.CompiledResult);
        }
        finally
        {
            if (Directory.Exists(settingsRoot))
            {
                Directory.Delete(settingsRoot, recursive: true);
            }
        }
    }
}
