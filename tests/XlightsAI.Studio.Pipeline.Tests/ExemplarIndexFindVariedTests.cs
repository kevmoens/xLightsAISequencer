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
    private static ExemplarEntry Entry(string title, double energyPercentile, int peakConcurrentLayerCount = 0) =>
        new(new ExemplarFingerprint(title, CorpusProvenanceTag.UserAuthored, 0, 8, 120, energyPercentile, SectionKind.Chorus, null, null, peakConcurrentLayerCount), []);

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

    [Fact]
    public void FindSimilar_ComplexityFilter_ExcludesEntriesBelowTheFloor()
    {
        // Build-plan Phase 25 Part D.4: a complexity floor is a hard filter applied before ranking --
        // the simple/closer-matching entry must never win just because it's the better cosine match.
        var simple = Entry("Simple Song", 0.5, peakConcurrentLayerCount: 1);
        var layered = Entry("Layered Song", 0.0, peakConcurrentLayerCount: 4);
        var index = new ExemplarIndex(DateTimeOffset.UtcNow, [simple, layered]);
        var query = new ExemplarFingerprint("(query)", CorpusProvenanceTag.UserAuthored, 0, 0, 120, 0.5, SectionKind.Chorus, null, null);

        var result = index.FindSimilar(query, 1, minPeakConcurrentLayerCount: 3);

        var picked = Assert.Single(result.Entries);
        Assert.Equal("Layered Song", picked.Fingerprint.SongTitle);
    }

    [Fact]
    public void FindSimilar_ComplexityFilter_NeverFabricatesAMatch_WhenNoRealEntryMeetsIt()
    {
        var simple = Entry("Simple Song", 0.5, peakConcurrentLayerCount: 1);
        var index = new ExemplarIndex(DateTimeOffset.UtcNow, [simple]);
        var query = new ExemplarFingerprint("(query)", CorpusProvenanceTag.UserAuthored, 0, 0, 120, 0.5, SectionKind.Chorus, null, null);

        var result = index.FindSimilar(query, 1, minPeakConcurrentLayerCount: 5);

        Assert.Empty(result.Entries);
    }

    [Fact]
    public void FindVaried_ComplexityFilter_OnlyEverDrawsFromEntriesReachingTheFloor()
    {
        var simple = Entry("Simple Song", 0.5, peakConcurrentLayerCount: 1);
        var layered = Entry("Layered Song", 0.0, peakConcurrentLayerCount: 4);
        var index = new ExemplarIndex(DateTimeOffset.UtcNow, [simple, layered]);
        var query = new ExemplarFingerprint("(query)", CorpusProvenanceTag.UserAuthored, 0, 0, 120, 0.5, SectionKind.Chorus, null, null);

        var everPickedSimple = Enumerable.Range(0, 50)
            .Select(seed => index.FindVaried(query, 1, new Random(seed), minPeakConcurrentLayerCount: 3).Entries[0].Fingerprint.SongTitle)
            .Any(title => title == "Simple Song");

        Assert.False(everPickedSimple);
    }

    /// <summary>
    /// Build-plan Phase 26 bugfix: real-run finding (2026-11-11, project "Bang") -- several different
    /// SectionKind queries with zero real same-kind entries each independently fell back to the same
    /// cross-kind pool and, since the comparison vector is otherwise identical for every Kind's query,
    /// deterministically re-picked the SAME single top match every time -- a corpus-reuse-diversity
    /// violation with no real variety ever having been considered. <c>excludeKeys</c> must make a second
    /// call prefer the next-best distinct entry over repeating the first call's own pick.
    /// </summary>
    [Fact]
    public void FindSimilar_ExcludeKeys_PrefersAFreshEntry_OverRepeatingAnAlreadyDrawnOne()
    {
        var best = Entry("Best Match", 0.5);
        var secondBest = Entry("Second Best Match", 0.4);
        var index = new ExemplarIndex(DateTimeOffset.UtcNow, [best, secondBest]);
        var query = new ExemplarFingerprint("(query)", CorpusProvenanceTag.UserAuthored, 0, 0, 120, 0.5, SectionKind.Chorus, null, null);

        var firstDraw = index.FindSimilar(query, 1);
        Assert.Equal("Best Match", Assert.Single(firstDraw.Entries).Fingerprint.SongTitle);

        var alreadyDrawn = new HashSet<string>(StringComparer.Ordinal) { ExemplarOverrides.KeyFor(firstDraw.Entries[0].Fingerprint) };
        var secondDraw = index.FindSimilar(query, 1, excludeKeys: alreadyDrawn);

        Assert.Equal("Second Best Match", Assert.Single(secondDraw.Entries).Fingerprint.SongTitle);
    }

    [Fact]
    public void FindSimilar_ExcludeKeys_StillReturnsARepeat_WhenNoFreshCandidateRemains()
    {
        // The pool only has one real entry at all -- excluding it must never leave the caller short.
        var only = Entry("Only Song", 0.5);
        var index = new ExemplarIndex(DateTimeOffset.UtcNow, [only]);
        var query = new ExemplarFingerprint("(query)", CorpusProvenanceTag.UserAuthored, 0, 0, 120, 0.5, SectionKind.Chorus, null, null);

        var alreadyDrawn = new HashSet<string>(StringComparer.Ordinal) { ExemplarOverrides.KeyFor(only.Fingerprint) };
        var result = index.FindSimilar(query, 1, excludeKeys: alreadyDrawn);

        Assert.Equal("Only Song", Assert.Single(result.Entries).Fingerprint.SongTitle);
    }

    [Fact]
    public void FindVaried_ExcludeKeys_PrefersFreshEntries_OverAlreadyDrawnOnes()
    {
        var drawn = Entry("Already Drawn", 0.5);
        var fresh = Entry("Fresh", 0.5);
        var index = new ExemplarIndex(DateTimeOffset.UtcNow, [drawn, fresh]);
        var query = new ExemplarFingerprint("(query)", CorpusProvenanceTag.UserAuthored, 0, 0, 120, 0.5, SectionKind.Chorus, null, null);
        var alreadyDrawn = new HashSet<string>(StringComparer.Ordinal) { ExemplarOverrides.KeyFor(drawn.Fingerprint) };

        // With both entries equally similar, only excludeKeys ordering determines which one wins across
        // every seed -- never the already-drawn entry, since a genuinely fresh alternative exists.
        var everPickedDrawn = Enumerable.Range(0, 50)
            .Select(seed => index.FindVaried(query, 1, new Random(seed), excludeKeys: alreadyDrawn).Entries[0].Fingerprint.SongTitle)
            .Any(title => title == "Already Drawn");

        Assert.False(everPickedDrawn);
    }
}
