using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography.Tests;

/// <summary>
/// Regression for section-wide anchors in a section with multiple phrase-specific base looks.
/// </summary>
public sealed class SectionTimelinePhraseScopedBugfixTests
{
    private const long TicksPerSecond = 10_000_000L;
    private const long BarTicks = 2 * TicksPerSecond;
    private const int BarCount = 4;
    private const long SongEndTicks = BarCount * BarTicks;

    private static DisplayGraph BuildLayout() => new(
        "TEST-HASH",
        Models:
        [
            new DisplayModel(new ModelId("Tree"), "Tree", "Tree", [], [], NodeCount: 900),
            new DisplayModel(new ModelId("Outline"), "Outline", "Outline", [], [], NodeCount: 300),
        ],
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

    [Theory]
    [InlineData(39_925_696L, 4000, false)]
    [InlineData(39_850_000L, 3975, true)]
    public async Task Compile_VocalSustainDropsOnlyTailsWithNoExportFrame(long sustainEnd, int exportedEndMs, bool hasTail)
    {
        var layout = BuildLayout() with
        {
            Groups = [new DisplayModelGroup("Matrixes",
                [new DisplayGroupMember("Tree", DisplayGroupMemberKind.Model), new DisplayGroupMember("Outline", DisplayGroupMemberKind.Model)])],
        };
        var grid = BuildBeatGrid();
        var (sections, sectionId) = BuildSections(grid.SongId);
        var settings = SectionTimelineCompilerTests.BuildPortableSettings();
        var look = new Look("base", "Base", "Test", .5, "hold", "warm-gold", "group:Matrixes",
            [new LookLayer(1, "group:Matrixes", "Twinkle", LookLayer.RoleSubject, null, "Normal", "Bars", 1)]);
        var note = new NoteEvent(Guid.NewGuid(), TicksPerSecond, sustainEnd, 60, .8, .9, false);
        var timeline = new SectionTimeline(sectionId, look.Id, Cues:
        [
            new("beat-3", "Outline", "On", IntensityTier.Full, LookLayer.RoleSupport),
        ]);
        var filler = new FillerPool(settings.RecipeCatalog.Recipes.Select(r => r.RecipeId).ToArray());
        var result = ChoreographyEngine.Compile(new ChoreographyEngine.Input(
            layout, grid, sections, BuildPhrases(grid.SongId), null, null, new LookLibrary([look]),
            new Dictionary<Guid, string> { [sectionId] = look.Id },
            new Dictionary<SectionKind, double>(), SongEndTicks, settings, SectionFillerSchedule.Flat(filler, SongEndTicks),
            SectionTimelinesBySectionId: new Dictionary<Guid, SectionTimeline> { [sectionId] = timeline },
            VocalNotes: [note]));

        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xsq");
        try
        {
            await new XsqExporter(settings.RecipeCatalog, settings.Vocabulary)
                .ExportNewAsync(layout, result.IR.ToSequenceDocument(), path, "2026.03", CancellationToken.None);
            var matrixEffects = Assert.Single(new XsqStructuralReader().Read(path).ElementEffects, e => e.Name == "Matrixes").Effects;
            Assert.Contains(matrixEffects, e => e.StartTimeMs == 0 && e.EndTimeMs == exportedEndMs);
            Assert.Equal(hasTail, matrixEffects.Any(e => e.StartTimeMs == exportedEndMs && e.EndTimeMs == 4000));
            Assert.Contains(matrixEffects, e => e.StartTimeMs == 4000 && e.EndTimeMs == 6000);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Compile_SeamHoldProtectsMembersAndOffCueWithoutSuppressingOtherFillerInXsq()
    {
        var layout = BuildLayout() with
        {
            Groups = [new DisplayModelGroup("Front", [new DisplayGroupMember("Tree", DisplayGroupMemberKind.Model)])],
        };
        var grid = BuildBeatGrid();
        var (sections, ownerId) = BuildSections(grid.SongId);
        var neighbor = sections.Labels[0] with { Id = Guid.NewGuid(), StartBarIndex = 2 };
        sections = sections with { Labels = [sections.Labels[0] with { EndBarIndexExclusive = 2 }, neighbor] };
        var settings = SectionTimelineCompilerTests.BuildPortableSettings();
        var look = new Look("base", "Base", "Test", .5, "hold", "warm-gold", "Tree",
            [new LookLayer(1, "Tree", "On", LookLayer.RoleSubject, null, "Normal", "Bars", .9)]);
        var neighborLook = look with
        {
            Id = "neighbor",
            Subject = "Outline",
            Layers = [look.Layers[0] with { Props = "Outline" }],
        };
        var note = new NoteEvent(Guid.NewGuid(), 2 * TicksPerSecond, 7 * TicksPerSecond, 60, .8, .9, false);
        var owner = new SectionTimeline(ownerId, "base", Cues:
        [
            new($"sustain-{note.Id:N}-start", "group:Front", "On", IntensityTier.Full, LookLayer.RoleSubject,
                $"sustain-{note.Id:N}-end", Kind: CueKind.Hold),
            new("section-start", "Outline", "On", IntensityTier.Off, LookLayer.RoleSupport, $"sustain-{note.Id:N}-start"),
        ]);
        var conflict = new SectionTimeline(neighbor.Id, "neighbor", Cues:
        [
            new("section-start", "Tree", "Twinkle", IntensityTier.Dim, LookLayer.RoleSubject, Kind: CueKind.Hold),
        ]);
        var filler = new FillerPool(settings.RecipeCatalog.Recipes.Select(r => r.RecipeId).ToArray());
        var result = ChoreographyEngine.Compile(new ChoreographyEngine.Input(
            layout, grid, sections, BuildPhrases(grid.SongId), null, null, new LookLibrary([look, neighborLook]),
            new Dictionary<Guid, string> { [ownerId] = "base", [neighbor.Id] = "neighbor" },
            new Dictionary<SectionKind, double>(), SongEndTicks, settings, SectionFillerSchedule.Flat(filler, SongEndTicks),
            SectionTimelinesBySectionId: new Dictionary<Guid, SectionTimeline> { [ownerId] = owner, [neighbor.Id] = conflict },
            VocalNotes: [note]));
        Assert.Contains(result.Violations, v => v.Rule == "section-timeline-ownership" && v.AffectedSectionId == neighbor.Id.ToString());
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xsq");
        try
        {
            await new XsqExporter(settings.RecipeCatalog, settings.Vocabulary)
                .ExportNewAsync(layout, result.IR.ToSequenceDocument(), path, "2026.03", CancellationToken.None);
            var xsq = new XsqStructuralReader().Read(path);
            var hold = Assert.Single(Assert.Single(xsq.ElementEffects, e => e.Name == "Front").Effects,
                effect => effect.StartTimeMs < 7000 && effect.EndTimeMs > 2000);
            Assert.Equal((2000, 7000), (hold.StartTimeMs, hold.EndTimeMs));
            Assert.DoesNotContain(xsq.ElementEffects.Where(e => e.Name == "Tree").SelectMany(e => e.Effects),
                effect => effect.StartTimeMs < 7000 && effect.EndTimeMs > 2000);
            var outline = Assert.Single(xsq.ElementEffects, e => e.Name == "Outline").Effects;
            Assert.DoesNotContain(outline, effect => effect.StartTimeMs < 2000);
            Assert.Contains(outline, effect => effect.StartTimeMs == 2000);
            Assert.Contains(outline, effect => effect.EndTimeMs == 4000);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Compile_PhraseScopedSection_EmitsTheSectionTimelineCueExactlyOnce()
    {
        var compilerSettings = SectionTimelineCompilerTests.BuildPortableSettings();

        var layout = BuildLayout();
        var beatGrid = BuildBeatGrid();
        var (sections, sectionId) = BuildSections(beatGrid.SongId);
        var phrases = BuildPhrases(beatGrid.SongId);

        var look = new Look(
            "full", "Full", "Both layers.", 0.6, "hold", "warm-gold", "Tree",
            [
                new LookLayer(1, "Tree", "Twinkle", LookLayer.RoleSubject, null, "Normal", "Bars", 0.5),
                new LookLayer(2, "Outline", "Color Wash", LookLayer.RoleSupport, null, "Normal", "Bars", 0.9),
            ]);

        // Two real phrase sub-windows: [0,2) and [2,4) bars -- same shape as the real Carol of the Bells
        // run's own persisted PhraseVariations data.
        var phraseAssignments = new List<SectionPhraseLookAssignment>
        {
            new(new BarIndex(0), new BarIndex(2), "full"),
            new(new BarIndex(2), new BarIndex(4), "full"),
        };

        // A real cue naming "beat-3" -- a mark that exists for the WHOLE section's own 4-bar window
        // (beat-1..beat-4) but does NOT exist within the FIRST phrase's own narrower [0,2)-bar window
        // (which only ever offers beat-1/beat-2). Before the fix, compiling the first phrase sub-window
        // incorrectly consulted this section-wide timeline and threw resolving "beat-3" against that
        // phrase's own narrower mark set -- the exact real crash shape.
        var timeline = new SectionTimeline(sectionId, "full", Cues:
        [
            new SectionCue("beat-3", "Tree", "On", IntensityTier.Full, LookLayer.RoleSubject, Annotation: "section-wide"),
        ]);

        var fillerSchedule = SectionFillerSchedule.Flat(new FillerPool(
            compilerSettings.RecipeCatalog.Recipes.Select(r => r.RecipeId).ToArray()), SongEndTicks);

        var input = new ChoreographyEngine.Input(
            layout, beatGrid, sections, phrases, BandEnergy: null, VocalActivity: null,
            new LookLibrary([look]), LookIdBySectionId: new Dictionary<Guid, string> { [sectionId] = "full" },
            EnergyCeilingByKind: new Dictionary<SectionKind, double> { [SectionKind.Verse] = 1.0 },
            SongEndTicks, compilerSettings, fillerSchedule,
            PhraseLookAssignmentsBySectionId: new Dictionary<Guid, IReadOnlyList<SectionPhraseLookAssignment>> { [sectionId] = phraseAssignments },
            SectionTimelinesBySectionId: new Dictionary<Guid, SectionTimeline> { [sectionId] = timeline });

        var result = ChoreographyEngine.Compile(input);
        Assert.Equal(4 * TicksPerSecond, Assert.Single(result.IR.Effects, e => e.SourceAnnotation == "section-wide").StartTicks);
        Assert.Contains(result.IR.Effects, e => e.Target.Value == "Outline" && e.StartTicks < 4 * TicksPerSecond);
        Assert.Contains(result.IR.Effects, e => e.Target.Value == "Outline" && e.StartTicks >= 4 * TicksPerSecond);
    }
}
