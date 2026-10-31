using XlightsAI.Studio.Composition;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Pipeline;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Pipeline.Tests;

/// <summary>
/// End-to-end proof that M1-M4 genuinely compose: a real chorus/verse Structure fixture → the two
/// deterministic heuristics → the real, unmodified <see cref="ChoreographyCompiler"/> → a real,
/// exportable <see cref="SequenceDocument"/> using every real prop in the layout, not a hand-typed 3-name
/// list. This is the milestone that actually delivers the original ask.
/// </summary>
public sealed class ChoreographyCompositionRunnerTests : IDisposable
{
    private const long BarTicks = 8_000_000L;
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"xlai-choreo-runner-{Guid.NewGuid():N}");

    public ChoreographyCompositionRunnerTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static (SectionLabelAnalysis Labels, SongFingerprintSet Fingerprints, PhraseSegmentationAnalysis Phrases, MotifClusteringAnalysis Motifs) BuildStructureFixture()
    {
        var songId = new SongId(Guid.NewGuid());
        const string hash = "deadbeef";

        var verseLabel = new SectionLabelCandidate(Guid.NewGuid(), Guid.NewGuid(), 0, 8, SectionKind.Verse, 0.9, ["evidence"], false);
        var chorusLabel = new SectionLabelCandidate(Guid.NewGuid(), Guid.NewGuid(), 8, 16, SectionKind.Chorus, 0.9, ["evidence"], false);
        var labels = new SectionLabelAnalysis(Guid.NewGuid(), songId, hash, Guid.NewGuid(), Guid.NewGuid(),
            "test/1.0.0", new SectionLabelSettings(0.85, 0.5, "test/1.0.0"), [verseLabel, chorusLabel]);

        var bars = new List<BarFingerprint>();
        for (var i = 0; i < 16; i++)
        {
            var energy = i < 8 ? 0.3 : 0.9;
            bars.Add(new BarFingerprint(i, new TimeWindow(i * BarTicks, (i + 1) * BarTicks), [energy, 0.05, 0, 0], Confidence: 1.0, IsSilent: false));
        }

        var fingerprints = new SongFingerprintSet(Guid.NewGuid(), songId, hash, Guid.NewGuid(), "test/1.0.0",
            new BarFingerprintSettings(2, "test/1.0.0"), bars);

        var versePhrase = new PhraseCandidate(Guid.NewGuid(), verseLabel.Id, 0, 8, 0, 8 * BarTicks, 8,
            PhraseBoundarySource.BarHypothesis, 1.0, ["evidence"], "sig-verse", false);
        var chorusPhrase = new PhraseCandidate(Guid.NewGuid(), chorusLabel.Id, 8, 16, 8 * BarTicks, 16 * BarTicks, 8,
            PhraseBoundarySource.BarHypothesis, 1.0, ["evidence"], "sig-chorus", false);
        var phrases = new PhraseSegmentationAnalysis(Guid.NewGuid(), songId, hash, Guid.NewGuid(), fingerprints.VersionId, null,
            "test/1.0.0", new PhraseSegmentationSettings(8, 1, 1_000_000L, "test/1.0.0"), [versePhrase, chorusPhrase]);

        var motifs = new MotifClusteringAnalysis(Guid.NewGuid(), songId, hash, phrases.VersionId, fingerprints.VersionId,
            "test/1.0.0", new MotifClusteringSettings(8, 0.8, "test/1.0.0"), [], []);

        return (labels, fingerprints, phrases, motifs);
    }

    private static DisplayGraph BuildLayout() => new(
        SourceLayoutHash: "TEST-HASH-NOT-A-REAL-FILE",
        Models:
        [
            new DisplayModel(new ModelId("Arches"), "Arches", "ModelGroup", [], []),
            new DisplayModel(new ModelId("Roofline"), "Roofline", "Star", [], []),
            new DisplayModel(new ModelId("Tree"), "Tree", "Tree", [], []),
        ],
        Groups: [],
        Warnings: []);

    [Fact]
    public async Task Compose_RealFixtureAllProps_ProducesAcceptedEffectsForEveryPropAndExportsValidXsq()
    {
        var (labels, fingerprints, phrases, motifs) = BuildStructureFixture();
        var structure = new StructureAnalysisResult(fingerprints,
            new SectionBoundaryAnalysis(Guid.NewGuid(), labels.SongId, labels.SourceHash, fingerprints.VersionId, "test/1.0.0",
                new SectionBoundarySettings(0.6, 0.3, 1, 10, "test/1.0.0"), [], new SectionBoundaryErrorReport(0, true, false, [])),
            labels, phrases, motifs);
        var sectionInputs = DeterministicSectionDirectionPlanner.BuildSectionInputs(structure);
        var layout = BuildLayout();
        var roleAssignments = DeterministicPropRoleAssigner.AssignRoles(layout, phrases, sectionInputs);

        var runner = new ChoreographyCompositionRunner();
        var result = runner.Compose(layout, phrases, roleAssignments, songDurationTicks: 16 * BarTicks);

        // Every real prop gets a candidate per phrase (3 props * 2 phrases = 6); none conflict (different
        // targets within a phrase, non-overlapping windows across phrases), so every one is accepted.
        Assert.Equal(6, result.Results.Count);
        Assert.All(result.Results, r => Assert.Equal(ChoreographyResolutionDecision.Accepted, r.Decision));
        Assert.Equal(6, result.Document.Effects.Count);
        foreach (var model in layout.Models)
        {
            Assert.Equal(2, result.Document.Effects.Count(e => e.Target == model.Id));
        }

        var outputPath = Path.Combine(_root, "whole-song.xsq");
        await new XsqExporter().ExportNewAsync(layout, result.Document, outputPath, "2026.03", CancellationToken.None);

        var structural = new XsqStructuralReader().Read(outputPath);
        foreach (var model in layout.Models)
        {
            var element = structural.ElementEffects.Single(e => e.Name == model.XlightsName);
            Assert.Equal(2, element.Effects.Count);
        }
    }

    [Fact]
    public void Compose_EmptyRoleAssignments_Throws()
    {
        var (_, _, phrases, _) = BuildStructureFixture();
        var runner = new ChoreographyCompositionRunner();
        Assert.Throws<ArgumentException>(() => runner.Compose(BuildLayout(), phrases, [], songDurationTicks: 16 * BarTicks));
    }

    [Fact]
    public void Compose_NonPositiveDuration_Throws()
    {
        var (_, _, phrases, _) = BuildStructureFixture();
        var layout = BuildLayout();
        var runner = new ChoreographyCompositionRunner();
        var assignment = new MusicalRoleAssignment(phrases.Phrases[0].Id, "Arches", MusicalRole.Lead, Guid.NewGuid(), PrecedenceTier.Automated, 0, 1.0, 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => runner.Compose(layout, phrases, [assignment], songDurationTicks: 0));
    }
}
