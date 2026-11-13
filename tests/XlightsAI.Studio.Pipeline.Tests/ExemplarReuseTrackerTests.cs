using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Pipeline.Tests;

/// <summary>
/// Build-plan Phase 25 Part E.1/E.2: hermetic unit tests for <see cref="ExemplarReuseTracker"/> and
/// <see cref="RepeatablePieceGrouper"/> -- "nothing tracks how many times one specific ExemplarEntry has
/// already been drawn on within a single generation run" and "cluster exemplars that are structurally
/// near-duplicates... into a named group."
/// </summary>
public sealed class ExemplarReuseTrackerTests
{
    private static ExemplarEntry Entry(string title, int startBar, int endBar, SectionKind kind, double energyPercentile) =>
        new(new ExemplarFingerprint(title, CorpusProvenanceTag.UserAuthored, startBar, endBar, 120, energyPercentile, kind, null, null), []);

    [Fact]
    public void Record_SameExemplarDrawnTwice_DrawCountReflectsBoth()
    {
        var entry = Entry("Song A", 0, 8, SectionKind.Chorus, 0.5);
        var tracker = new ExemplarReuseTracker();

        tracker.Record([entry]);
        tracker.Record([entry]);

        var report = tracker.BuildReport();
        var key = ExemplarOverrides.KeyFor(entry.Fingerprint);
        Assert.Equal(2, report.DrawCountByExemplarKey[key]);
    }

    [Fact]
    public void Record_DistinctExemplars_EachOwnKeyCountedSeparately()
    {
        var a = Entry("Song A", 0, 8, SectionKind.Chorus, 0.5);
        var b = Entry("Song B", 0, 8, SectionKind.Chorus, 0.5);
        var tracker = new ExemplarReuseTracker();

        tracker.Record([a, b]);

        var report = tracker.BuildReport();
        Assert.Equal(1, report.DrawCountByExemplarKey[ExemplarOverrides.KeyFor(a.Fingerprint)]);
        Assert.Equal(1, report.DrawCountByExemplarKey[ExemplarOverrides.KeyFor(b.Fingerprint)]);
    }

    [Fact]
    public void Record_TwoDifferentExemplarsInTheSameRepeatablePieceGroup_GroupTotalsBothButDistinctCountIsTwo()
    {
        // Same phrase length (8 bars), same SectionKind, same energy tier (both 0.5 -> "Mid") -- same
        // RepeatablePieceGrouper bucket, but two distinct real exemplars.
        var a = Entry("Song A", 0, 8, SectionKind.Chorus, 0.5);
        var b = Entry("Song B", 16, 24, SectionKind.Chorus, 0.55);
        var tracker = new ExemplarReuseTracker();

        tracker.Record([a]);
        tracker.Record([b]);
        tracker.Record([a]);

        var report = tracker.BuildReport();
        var group = RepeatablePieceGrouper.GroupKeyFor(a.Fingerprint);
        Assert.Equal(group, RepeatablePieceGrouper.GroupKeyFor(b.Fingerprint));

        var stats = report.GroupStats[group];
        Assert.Equal(3, stats.TotalDraws);
        Assert.Equal(2, stats.DistinctKeysDrawn);
    }

    [Fact]
    public void BuildReport_NoDraws_EmptyReport()
    {
        var report = new ExemplarReuseTracker().BuildReport();

        Assert.Empty(report.DrawCountByExemplarKey);
        Assert.Empty(report.GroupStats);
    }

    [Fact]
    public void GroupKeyFor_DifferentPhraseLengthsOrKindsOrEnergyTiers_NeverShareAGroup()
    {
        var baseline = Entry("Song A", 0, 8, SectionKind.Chorus, 0.5);
        var differentLength = Entry("Song A", 0, 4, SectionKind.Chorus, 0.5);
        var differentKind = Entry("Song A", 0, 8, SectionKind.Verse, 0.5);
        var differentTier = Entry("Song A", 0, 8, SectionKind.Chorus, 0.9);

        var baseGroup = RepeatablePieceGrouper.GroupKeyFor(baseline.Fingerprint);
        Assert.NotEqual(baseGroup, RepeatablePieceGrouper.GroupKeyFor(differentLength.Fingerprint));
        Assert.NotEqual(baseGroup, RepeatablePieceGrouper.GroupKeyFor(differentKind.Fingerprint));
        Assert.NotEqual(baseGroup, RepeatablePieceGrouper.GroupKeyFor(differentTier.Fingerprint));
    }
}
