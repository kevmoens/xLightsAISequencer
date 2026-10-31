using XlightsAI.Studio.Composition;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Pipeline;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.WholeSong;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Pipeline.Tests;

/// <summary>
/// <see cref="DeterministicSectionDirectionPlanner"/>/<see cref="DeterministicPropRoleAssigner"/> are the
/// two new deterministic heuristics this pipeline adds to fill gaps nothing else in the codebase derives
/// automatically (per-section creative direction, per-prop role assignment across ALL real props instead
/// of a manually-typed 3-name list). These tests build real, hand-constructed Structure/Xlights fixtures
/// (same "real production types, no mocking" convention as the rest of this project's tests) and feed the
/// resulting direction inputs into the real, unmodified <see cref="SongDirectionPlanner"/> to prove they
/// are genuinely valid, not just shaped correctly in isolation.
/// </summary>
public sealed class DeterministicSectionDirectionAndRoleAssignmentTests
{
    private const long BarTicks = 8_000_000L;

    private static (SectionLabelAnalysis Labels, SongFingerprintSet Fingerprints, PhraseSegmentationAnalysis Phrases, MotifClusteringAnalysis Motifs)
        BuildFixture()
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

        var group = new MotifGroupCandidate(Guid.NewGuid(), [versePhrase.Id, chorusPhrase.Id], versePhrase.Id, 0.9, ["evidence"]);
        var motifs = new MotifClusteringAnalysis(Guid.NewGuid(), songId, hash, phrases.VersionId, fingerprints.VersionId,
            "test/1.0.0", new MotifClusteringSettings(8, 0.8, "test/1.0.0"), [group], []);

        return (labels, fingerprints, phrases, motifs);
    }

    [Fact]
    public void BuildSectionInputs_RealChorusVerseFixture_ProducesPlausibleDirectionAndValidatesAgainstRealPlanner()
    {
        var (labels, fingerprints, phrases, motifs) = BuildFixture();
        var structure = new StructureAnalysisResult(fingerprints, new SectionBoundaryAnalysis(
            Guid.NewGuid(), labels.SongId, labels.SourceHash, fingerprints.VersionId, "test/1.0.0",
            new SectionBoundarySettings(0.6, 0.3, 1, 10, "test/1.0.0"),
            [
                new SectionBoundaryCandidate(Guid.NewGuid(), 0, 0, 0, 1.0, true, true),
                new SectionBoundaryCandidate(Guid.NewGuid(), 8, 8 * BarTicks, 0.8, 0.8, true, true),
            ],
            new SectionBoundaryErrorReport(1, true, false, [])),
            labels, phrases, motifs);

        var inputs = DeterministicSectionDirectionPlanner.BuildSectionInputs(structure);

        Assert.Equal(2, inputs.Count);
        var verseInput = inputs.Single(i => i.SectionId == labels.Labels[0].Id);
        var chorusInput = inputs.Single(i => i.SectionId == labels.Labels[1].Id);

        Assert.InRange(verseInput.TargetEnergy, 0.25, 0.35);
        Assert.InRange(chorusInput.TargetEnergy, 0.85, 0.95);
        Assert.Equal(MusicalRole.Lead, chorusInput.FocalRole);
        Assert.Equal(MusicalRole.Atmosphere, verseInput.FocalRole);
        Assert.NotEqual(verseInput.PaletteId, chorusInput.PaletteId);
        Assert.Contains(motifs.Groups[0].Id, verseInput.MotifGroupIds);
        Assert.Contains(motifs.Groups[0].Id, chorusInput.MotifGroupIds);

        // The real, unmodified planner must accept this heuristic's output outright.
        var plan = new SongDirectionPlanner().Plan(labels, motifs, inputs, baseRevision: 0, DateTimeOffset.UtcNow);
        Assert.Equal(2, plan.Sections.Count);
    }

    [Fact]
    public void AssignRoles_RealLayoutAndPhrases_EveryPropParticipatesAndFocalRoleRotates()
    {
        var (labels, fingerprints, phrases, motifs) = BuildFixture();
        var structure = new StructureAnalysisResult(fingerprints,
            new SectionBoundaryAnalysis(Guid.NewGuid(), labels.SongId, labels.SourceHash, fingerprints.VersionId, "test/1.0.0",
                new SectionBoundarySettings(0.6, 0.3, 1, 10, "test/1.0.0"), [], new SectionBoundaryErrorReport(0, true, false, [])),
            labels, phrases, motifs);
        var sectionInputs = DeterministicSectionDirectionPlanner.BuildSectionInputs(structure);

        var layout = new DisplayGraph("layout-hash",
            [
                new DisplayModel(new ModelId("Arch1"), "Arch1", "Arches", [], []),
                new DisplayModel(new ModelId("Arch2"), "Arch2", "Arches", [], []),
                new DisplayModel(new ModelId("Tree1"), "Tree1", "Tree", [], []),
            ], [], []);

        var assignments = DeterministicPropRoleAssigner.AssignRoles(layout, phrases, sectionInputs);

        Assert.Equal(layout.Models.Count * phrases.Phrases.Count, assignments.Count);
        foreach (var model in layout.Models)
        {
            Assert.Contains(assignments, a => a.SourceTrackId == model.XlightsName);
        }

        // Phrase 0's focal prop is Arch1 (index 0); phrase 1's focal prop is Arch2 (index 1) -- rotation.
        var phrase0FocalAssignment = assignments.Single(a => a.PhraseId == phrases.Phrases[0].Id && a.Priority == 0);
        var phrase1FocalAssignment = assignments.Single(a => a.PhraseId == phrases.Phrases[1].Id && a.Priority == 0);
        Assert.Equal("Arch1", phrase0FocalAssignment.SourceTrackId);
        Assert.Equal("Arch2", phrase1FocalAssignment.SourceTrackId);
        Assert.Equal(sectionInputs[0].FocalRole, phrase0FocalAssignment.Role);
        Assert.Equal(sectionInputs[1].FocalRole, phrase1FocalAssignment.Role);
        Assert.All(assignments, a => Assert.Equal(PrecedenceTier.Automated, a.PrecedenceTier));
        Assert.Equal(assignments.Count, assignments.Select(a => a.SpatialPathId).Distinct().Count());
    }

    [Fact]
    public void AssignRoles_EmptyLayout_ReturnsNoAssignments()
    {
        var (_, _, phrases, _) = BuildFixture();
        var layout = new DisplayGraph("layout-hash", [], [], []);

        var assignments = DeterministicPropRoleAssigner.AssignRoles(layout, phrases, []);

        Assert.Empty(assignments);
    }

    [Fact]
    public void AssignRoles_NullArguments_Throw()
    {
        var (_, _, phrases, _) = BuildFixture();
        var layout = new DisplayGraph("h", [], [], []);
        Assert.Throws<ArgumentNullException>(() => DeterministicPropRoleAssigner.AssignRoles(null!, phrases, []));
        Assert.Throws<ArgumentNullException>(() => DeterministicPropRoleAssigner.AssignRoles(layout, null!, []));
        Assert.Throws<ArgumentNullException>(() => DeterministicPropRoleAssigner.AssignRoles(layout, phrases, null!));
    }

    [Fact]
    public void BuildSectionInputs_NullArgument_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => DeterministicSectionDirectionPlanner.BuildSectionInputs(null!));
    }
}
