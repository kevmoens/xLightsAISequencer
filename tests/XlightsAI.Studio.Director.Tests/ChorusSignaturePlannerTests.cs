using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Director.Tests;

public sealed class ChorusSignaturePlannerTests
{
    [Fact]
    public void LinkedChorusesKeepSignatureAfterAnOccurrenceIsReplanned()
    {
        var sections = Enumerable.Range(0, 3).Select(i => new SectionLabelCandidate(Guid.NewGuid(), Guid.NewGuid(),
            i, i + 1, SectionKind.Chorus, .95, [], false)).ToArray();
        var labels = new SectionLabelAnalysis(Guid.NewGuid(), default, "test", Guid.NewGuid(), Guid.NewGuid(), "test",
            new SectionLabelSettings(.8, .5, "test"), sections);
        var music = new MusicModel(default, 0, null!, labels, null!, null, null);
        var look = new Look("hook", "Hook", "Test", .8, "hold", "hook-palette", "Matrix",
            [new LookLayer(1, "Matrix", "On", LookLayer.RoleSubject, null, "Normal", null, 1)]);
        var signatures = ChorusSignaturePlanner.Build(music, new LookLibrary([look]),
            [new SectionMotifLink(sections[1].Id, sections[0].Id, SectionKind.Chorus, 0, 1, .9)]);
        Assert.False(signatures.ContainsKey(sections[2].Id));
        var catalog = new SectionTimingCatalog(new Dictionary<string, long> { ["section-start"] = 0, ["beat-2"] = 1_000_000 }, []);
        var first = Assert.Single(signatures[sections[0].Id].Apply(new SectionTimeline(sections[0].Id, "hook"), catalog).CuesOrEmpty);
        var second = Assert.Single(signatures[sections[1].Id].Apply(new SectionTimeline(sections[1].Id, "another-look"), catalog).CuesOrEmpty);
        Assert.Equal((first.Props, first.Effect, first.PaletteSlot, first.AnchorId), (second.Props, second.Effect, second.PaletteSlot, second.AnchorId));
        Assert.NotEqual(first.Intensity, second.Intensity);
        Assert.Equal(second, Assert.Single(signatures[sections[1].Id].Apply(
            new SectionTimeline(sections[1].Id, "repair-look", Cues: [second with { Props = "Wrong" }]), catalog).CuesOrEmpty));
    }
}
