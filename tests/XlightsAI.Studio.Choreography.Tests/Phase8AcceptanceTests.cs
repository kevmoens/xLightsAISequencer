using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Stems;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography.Tests;

/// <summary>
/// Build-plan Phase 8 end-to-end acceptance: "A song with lyrics and a face-capable model produces a
/// working Faces effect bound to a real phoneme timing track." Exercises the real
/// <see cref="ChoreographyEngine"/> compile path (no LLM involved) against the real generated vocabulary,
/// with a hand-authored singing-face Look and hand-constructed viseme marks standing in for a real
/// <see cref="VisemeApproximator"/> run (the approximator's own heuristic nature is exercised and
/// disclosed separately in <c>VisemeApproximatorTests</c>). Skips cleanly when no real xLights install is
/// present, per this test project's own established convention.
/// </summary>
public sealed class Phase8AcceptanceTests
{
    private const long TicksPerSecond = 10_000_000L;
    private const long BarTicks = 2 * TicksPerSecond;
    private const int BarCount = 5;
    private const long SongEndTicks = BarCount * BarTicks;

    private static DisplayGraph BuildLayout() => new(
        "TEST-HASH",
        Models: [new DisplayModel(new ModelId("Tree"), "Tree", "Tree", [], [], NodeCount: 900)],
        Groups: [],
        Warnings: []);

    private static BeatGrid BuildBeatGrid()
    {
        var beats = Enumerable.Range(0, BarCount).Select(bar => new BeatMarker(Guid.NewGuid(), bar * BarTicks, 0, bar, 1.0, false)).ToArray();
        return new BeatGrid(Guid.NewGuid(), new SongId(Guid.NewGuid()), "h", Guid.NewGuid(), Guid.NewGuid(), "t/1", new BeatGridSettings(4, 1000), new TempoHypothesis(120, 0.9, "4/4"), beats, [], 0);
    }

    private static (SectionLabelAnalysis Sections, Guid SectionId) BuildSections(SongId songId)
    {
        var sectionId = Guid.NewGuid();
        var analysis = new SectionLabelAnalysis(
            Guid.NewGuid(), songId, "hash", Guid.NewGuid(), Guid.NewGuid(), "t/1",
            new SectionLabelSettings(0.95, 0.4, "t/1"),
            [new SectionLabelCandidate(sectionId, Guid.NewGuid(), 0, BarCount, SectionKind.Verse, 1.0, ["test"], false)]);
        return (analysis, sectionId);
    }

    private static PhraseSegmentationAnalysis BuildPhrases(SongId songId) => new(
        Guid.NewGuid(), songId, "hash", Guid.NewGuid(), Guid.NewGuid(), null, "t/1",
        new PhraseSegmentationSettings(4, 1, 0, "t/1"), []);

    private static VocalActivityEnvelope BuildVocalActivity() => new(
        Guid.NewGuid(), Guid.NewGuid(), "stem-hash", "test-vocal-activity.v1",
        new StemActivityDetectionSettings(1000, 500, 0.1, 0.05, 1_000_000),
        [new StemActivity(StemKind.Vocals, 0, SongEndTicks, 1.0)]);

    /// <summary>Hand-constructed, standing in for a real <see cref="VisemeApproximator"/> run: two non-valley runs (words) separated by an "MBP"/"rest" valley, covering the whole song.</summary>
    private static IReadOnlyList<VisemeMark> BuildVisemeMarks()
    {
        var windowTicks = BarTicks / 2;
        var codes = new[] { "AI", "E", "L", "O", "U", "WQ", "MBP", "AI", "E", "rest" };
        var marks = new List<VisemeMark>();
        for (var i = 0; i < codes.Length; i++)
        {
            marks.Add(new VisemeMark(codes[i], i * windowTicks, (i + 1) * windowTicks));
        }

        return marks;
    }

    [Fact]
    public void Compile_SingingFaceLook_ProducesFacesEffectBoundToPhonemeTrack_AndTextBoundToWordTrack()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var registry = new EffectRecipeRegistry(recipes);
        var palettes = new Dictionary<string, ColorPaletteDefinition> { ["warm-gold"] = PaletteBuilder.Build(["#FFC880"]) };
        var compilerSettings = new LookCompilerSettings(vocabulary, recipes, registry, palettes, RealXLightsFixture.BuildFillerPool(recipes));

        var layout = BuildLayout();
        var beatGrid = BuildBeatGrid();
        var (sections, sectionId) = BuildSections(beatGrid.SongId);
        var phrases = BuildPhrases(beatGrid.SongId);
        var vocalActivity = BuildVocalActivity();
        var visemeMarks = BuildVisemeMarks();

        var look = new Look(
            "singing-face", "Singing Face", "A singing face telling the song's lyrics.", 0.6, "hold", "warm-gold", "Tree",
            [
                new LookLayer(1, "Tree", "Faces", LookLayer.RoleSubject, null, "Normal", LyricTimingTrackBuilder.PhonemesTrackName, 1.0),
                new LookLayer(2, "Tree", "Text", LookLayer.RoleSupport, null, "Normal", LyricTimingTrackBuilder.WordsTrackName, 1.0),
            ]);

        var input = new ChoreographyEngine.Input(
            layout, beatGrid, sections, phrases, BandEnergy: null, VocalActivity: vocalActivity,
            new LookLibrary([look]), new Dictionary<Guid, string> { [sectionId] = look.Id },
            new Dictionary<SectionKind, double> { [SectionKind.Verse] = 1.0 },
            SongEndTicks, compilerSettings, VisemeMarks: visemeMarks);

        var result = ChoreographyEngine.Compile(input);

        var phonemeTrack = Assert.Single(result.IR.TimingTracks, t => t.Name == LyricTimingTrackBuilder.PhonemesTrackName);
        Assert.NotEmpty(phonemeTrack.Marks);

        var wordTrack = Assert.Single(result.IR.TimingTracks, t => t.Name == LyricTimingTrackBuilder.WordsTrackName);
        Assert.NotEmpty(wordTrack.Marks);

        var phraseTrack = Assert.Single(result.IR.TimingTracks, t => t.Name == LyricTimingTrackBuilder.PhrasesTrackName);
        Assert.NotNull(phraseTrack);

        var mouthMovementsKey = vocabulary.Effects["Faces"].FindByPropertyId("Faces_MouthMovements")!.SettingKey;
        var facesEffect = Assert.Single(result.IR.Effects, e => e.RecipeId.StartsWith("effect.generated.faces.", StringComparison.Ordinal));
        Assert.Equal(LyricTimingTrackBuilder.PhonemesTrackName, facesEffect.Parameters[mouthMovementsKey]);

        var lyricTrackKey = vocabulary.Effects["Text"].FindByPropertyId("Text_LyricTrack")!.SettingKey;
        var textEffect = Assert.Single(result.IR.Effects, e => e.RecipeId.StartsWith("effect.generated.text.", StringComparison.Ordinal));
        Assert.Equal(LyricTimingTrackBuilder.WordsTrackName, textEffect.Parameters[lyricTrackKey]);
    }
}
