using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography.Tests;

/// <summary>Build-plan Phase 28a §3/§8 item 2: the four required cases for <see cref="SectionTimelineValidator"/>'s contrast/concurrency/unknown-anchor rules.</summary>
public sealed class SectionTimelineValidatorTests
{
    private const long TicksPerSecond = 10_000_000L;

    private static DisplayGraph BuildLayout() => new(
        "TEST-HASH",
        Models:
        [
            new DisplayModel(new ModelId("Tree"), "Tree", "Tree", [], [], NodeCount: 900),
            new DisplayModel(new ModelId("MiniStar1"), "MiniStar1", "Star", [], [], NodeCount: 60),
            new DisplayModel(new ModelId("MiniStar2"), "MiniStar2", "Star", [], [], NodeCount: 40),
        ],
        Groups: [new DisplayModelGroup("Front", [new DisplayGroupMember("Tree", DisplayGroupMemberKind.Model), new DisplayGroupMember("MiniStar1", DisplayGroupMemberKind.Model)])],
        Warnings: []);

    private static SectionMusicContext BuildContext()
    {
        var beats = new[] { new BeatMarker(Guid.NewGuid(), 0, 0, 0, 1.0, false) };
        var beatGrid = new BeatGrid(Guid.NewGuid(), new SongId(Guid.NewGuid()), "h", Guid.NewGuid(), Guid.NewGuid(), "t/1", new BeatGridSettings(4, 1000), null, beats, [], 0);
        var window = new TimeWindow(0, 10 * TicksPerSecond);
        var barGrid = BarGrid.FromBeatGrid(beatGrid, window.EndTicks);
        return new SectionMusicContext(window, SectionKind.Verse, 0.5, barGrid, new Dictionary<string, TimingTrack>(StringComparer.Ordinal), false);
    }

    private static readonly IReadOnlyList<TransientHit> TwoHits =
    [
        new TransientHit(1 * TicksPerSecond, 0.9f),
        new TransientHit(5 * TicksPerSecond, 0.9f),
    ];

    private static Look BuildLook(params string[] layerProps)
    {
        var layers = layerProps.Select((props, i) => new LookLayer(i + 1, props, "On", LookLayer.RoleSubject, null, "Normal", null, 0.5)).ToList();
        return new Look("test-look", "Test Look", "test", 0.5, "hold", "warm-gold", layerProps[0], layers);
    }

    private static LookLibrary LibraryWith(Look look) => new([look]);

    private static EffectVocabularyCatalog BuildVocabulary() => new(
        new Dictionary<string, EffectVocabulary>(StringComparer.Ordinal) { ["On"] = new EffectVocabulary("On", "E_", []) },
        buffer: new EffectVocabulary("Buffer", "B_", []),
        blending: new EffectVocabulary("Blending", "T_", []),
        color: new EffectVocabulary("Color", "C_", []));

    [Fact]
    public void Validate_DimThenBrightOnSameProps_Passes()
    {
        var layout = BuildLayout();
        var context = BuildContext();
        var library = LibraryWith(BuildLook("Tree", "MiniStar1", "MiniStar2"));

        var timeline = new SectionTimeline(Guid.NewGuid(), "test-look", Cues:
        [
            new SectionCue("transient-1", "Tree", "On", IntensityTier.Dim, LookLayer.RoleSubject),
            new SectionCue("transient-2", "Tree", "On", IntensityTier.Bright, LookLayer.RoleSubject),
        ]);

        var errors = SectionTimelineValidator.Validate(timeline, library, layout, context, transientHits: TwoHits);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_BackToBackAccents_AllowsCompilerToProvideContrast()
    {
        var layout = BuildLayout();
        var context = BuildContext();
        var library = LibraryWith(BuildLook("Tree", "MiniStar1", "MiniStar2"));

        var timeline = new SectionTimeline(Guid.NewGuid(), "test-look", Cues:
        [
            new SectionCue("transient-1", "Tree", "On", IntensityTier.Bright, LookLayer.RoleSubject),
            new SectionCue("transient-2", "Tree", "On", IntensityTier.Bright, LookLayer.RoleSubject),
        ]);

        var errors = SectionTimelineValidator.Validate(timeline, library, layout, context, transientHits: TwoHits);

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(CueKind.Accent, "group:Front", "section-timeline-held-target")]
    [InlineData(CueKind.Accent, "MiniStar2", null)]
    [InlineData(CueKind.Takeover, "Tree", null)]
    public void Validate_HeldTargetMayContinueOrBeExplicitlyTakenOver(string kind, string target, string? errorRule)
    {
        var timeline = new SectionTimeline(Guid.NewGuid(), "test-look", Cues:
        [
            new SectionCue("section-start", "Tree", "On", IntensityTier.Bright, LookLayer.RoleSubject, Kind: CueKind.Hold),
            new SectionCue("transient-1", target, "On", IntensityTier.Full, LookLayer.RoleSupport, Kind: kind),
        ]);
        var errors = SectionTimelineValidator.Validate(timeline, LibraryWith(BuildLook("Tree", "MiniStar1", "MiniStar2")),
            BuildLayout(), BuildContext(), transientHits: TwoHits);
        if (errorRule is null)
        {
            Assert.Empty(errors);
        }
        else
        {
            Assert.Equal(errorRule, Assert.Single(errors).Rule);
        }
    }

    [Fact]
    public void Validate_PropsUnionBelowSectionKindFloor_FailsNamingRealCountAndFloor()
    {
        var layout = BuildLayout();
        var context = BuildContext(); // SectionKind.Verse -> floor 3.
        var library = LibraryWith(BuildLook("Tree"));

        var timeline = new SectionTimeline(Guid.NewGuid(), "test-look", Cues:
        [
            new SectionCue("transient-1", "Tree", "On", IntensityTier.Bright, LookLayer.RoleSubject),
        ]);

        var errors = SectionTimelineValidator.Validate(timeline, library, layout, context, transientHits: TwoHits);

        Assert.Contains(errors, e => e.Message.Contains("only 1 distinct Props", StringComparison.Ordinal) && e.Message.Contains("at least 3", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_UnknownAnchorId_Fails()
    {
        var layout = BuildLayout();
        var context = BuildContext();
        var library = LibraryWith(BuildLook("Tree", "MiniStar1", "MiniStar2"));

        var timeline = new SectionTimeline(Guid.NewGuid(), "test-look", Cues:
        [
            new SectionCue("transient-99", "Tree", "On", IntensityTier.Bright, LookLayer.RoleSubject),
        ]);

        var errors = SectionTimelineValidator.Validate(timeline, library, layout, context, transientHits: TwoHits);

        Assert.Contains(errors, e => e.Message.Contains("transient-99", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_CueEffectNamesAnIntensityTierInsteadOfARealEffect_Fails()
    {
        var layout = BuildLayout();
        var context = BuildContext();
        var library = LibraryWith(BuildLook("Tree", "MiniStar1", "MiniStar2"));

        var timeline = new SectionTimeline(Guid.NewGuid(), "test-look", Cues:
        [
            new SectionCue("transient-1", "Tree", IntensityTier.Bright, IntensityTier.Bright, LookLayer.RoleSubject),
        ]);

        var errors = SectionTimelineValidator.Validate(timeline, library, layout, context, transientHits: TwoHits, vocabulary: BuildVocabulary());

        Assert.Contains(errors, e => e.Rule == "section-timeline-unknown-effect" && e.Message.Contains("'Bright'", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_NoVocabularySupplied_SkipsUnknownEffectCheck()
    {
        var layout = BuildLayout();
        var context = BuildContext();
        var library = LibraryWith(BuildLook("Tree", "MiniStar1", "MiniStar2"));

        var timeline = new SectionTimeline(Guid.NewGuid(), "test-look", Cues:
        [
            new SectionCue("transient-1", "Tree", "Bright", IntensityTier.Bright, LookLayer.RoleSubject),
        ]);

        var errors = SectionTimelineValidator.Validate(timeline, library, layout, context, transientHits: TwoHits);

        Assert.DoesNotContain(errors, e => e.Rule == "section-timeline-unknown-effect");
    }
}
