using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Pipeline.Tests;

/// <summary>
/// User feedback (2026-10-31): regenerating a song always retrieved the identical nearest exemplars via
/// <see cref="ExemplarIndex.FindSimilar"/> -- these tests cover the new <see cref="ExemplarIndex.FindVaried"/>
/// alternative (weighted random sampling without replacement) that deliberately reaches further than the
/// single closest match when a user opts into effect variety over staying close to the corpus.
/// </summary>
public sealed class ExemplarIndexFindVariedTests
{
    private static ExemplarEntry Entry(string title, double energyPercentile) =>
        new(new ExemplarFingerprint(title, CorpusProvenanceTag.UserAuthored, 0, 8, 120, energyPercentile, SectionKind.Chorus, null, null), []);

    [Fact]
    public void FindVaried_RespectsSectionKindFiltering_SameAsFindSimilar()
    {
        var chorus = Entry("Chorus Song", 0.5);
        var verse = new ExemplarEntry(new ExemplarFingerprint("Verse Song", CorpusProvenanceTag.UserAuthored, 0, 8, 120, 0.5, SectionKind.Verse, null, null), []);
        var index = new ExemplarIndex(DateTimeOffset.UtcNow, [chorus, verse]);
        var query = new ExemplarFingerprint("(query)", CorpusProvenanceTag.UserAuthored, 0, 0, 120, 0.5, SectionKind.Chorus, null, null);

        var result = index.FindVaried(query, 5, new Random(1));

        Assert.All(result.Entries, e => Assert.Equal(SectionKind.Chorus, e.Fingerprint.SectionKind));
        Assert.False(result.IsFallback);
    }

    [Fact]
    public void FindVaried_FallsBackAcrossKinds_WhenNoneOfTheQueriedKindExists()
    {
        var verse = new ExemplarEntry(new ExemplarFingerprint("Verse Song", CorpusProvenanceTag.UserAuthored, 0, 8, 120, 0.5, SectionKind.Verse, null, null), []);
        var index = new ExemplarIndex(DateTimeOffset.UtcNow, [verse]);
        var query = new ExemplarFingerprint("(query)", CorpusProvenanceTag.UserAuthored, 0, 0, 120, 0.5, SectionKind.Chorus, null, null);

        var result = index.FindVaried(query, 1, new Random(1));

        Assert.True(result.IsFallback);
        Assert.Single(result.Entries);
    }

    [Fact]
    public void FindVaried_CanReturnADistantMatch_NotJustTheClosestOne()
    {
        // The closest entry by energy percentile is "close" (0.51 vs query's 0.5); "far" (0.0) is a much
        // worse match. With enough distinct seeds, weighted sampling with a nonzero floor must sometimes
        // pick "far" -- proving this isn't just FindSimilar in disguise.
        var close = Entry("Close Song", 0.51);
        var far = Entry("Far Song", 0.0);
        var index = new ExemplarIndex(DateTimeOffset.UtcNow, [close, far]);
        var query = new ExemplarFingerprint("(query)", CorpusProvenanceTag.UserAuthored, 0, 0, 120, 0.5, SectionKind.Chorus, null, null);

        var everPickedFar = Enumerable.Range(0, 200)
            .Select(seed => index.FindVaried(query, 1, new Random(seed)).Entries[0].Fingerprint.SongTitle)
            .Any(title => title == "Far Song");

        Assert.True(everPickedFar);
    }

    [Fact]
    public void FindVaried_WithEmptyIndex_ReturnsEmptyNotFallback()
    {
        var index = new ExemplarIndex(DateTimeOffset.UtcNow, []);
        var query = new ExemplarFingerprint("(query)", CorpusProvenanceTag.UserAuthored, 0, 0, 120, 0.5, SectionKind.Chorus, null, null);

        var result = index.FindVaried(query, 3, new Random(1));

        Assert.Empty(result.Entries);
    }

    [Fact]
    public void FindVaried_ZeroCount_ReturnsEmpty()
    {
        var index = new ExemplarIndex(DateTimeOffset.UtcNow, [Entry("Song", 0.5)]);
        var query = new ExemplarFingerprint("(query)", CorpusProvenanceTag.UserAuthored, 0, 0, 120, 0.5, SectionKind.Chorus, null, null);

        var result = index.FindVaried(query, 0, new Random(1));

        Assert.Empty(result.Entries);
    }
}
