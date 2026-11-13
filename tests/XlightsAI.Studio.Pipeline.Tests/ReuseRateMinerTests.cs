using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Pipeline.Tests;

/// <summary>
/// Build-plan Phase 25 Part C §3 bullet 4: hermetic unit tests for <see cref="ReuseRateMiner"/> -- "how
/// often a structurally similar passage (same phrase-length window, same section kind, similar energy
/// percentile) recurs within one real song."
/// </summary>
public sealed class ReuseRateMinerTests
{
    private const long TicksPerBar = 20_000_000L;

    [Fact]
    public void TwoPhrasesSharingTheSameSignature_BucketedTogether_AsOneReusedOccurrence()
    {
        // 16 bars, uniform energy throughout (every bar has the same feature value), one Chorus label
        // covering the whole song, and two consecutive 8-bar phrases -- same phrase length, same section
        // kind, same energy tier (since every bar is identical) -- this is the "structurally similar
        // passage recurring within one real song" the ask describes.
        var song = BuildSong(barCount: 16, phraseLengths: [8, 8], sectionKind: SectionKind.Chorus);

        var buckets = ReuseRateMiner.Mine(song);

        var bucket = Assert.Single(buckets);
        Assert.Equal(2, bucket.OccurrenceCount);
        Assert.Equal(SectionKind.Chorus, bucket.SectionKind);
    }

    [Fact]
    public void DifferentPhraseLengths_NeverBucketedTogether()
    {
        var song = BuildSong(barCount: 12, phraseLengths: [8, 4], sectionKind: SectionKind.Chorus);

        var buckets = ReuseRateMiner.Mine(song);

        Assert.Equal(2, buckets.Count);
        Assert.All(buckets, b => Assert.Equal(1, b.OccurrenceCount));
    }

    [Fact]
    public void SinglePhrase_NeverReused()
    {
        var song = BuildSong(barCount: 8, phraseLengths: [8], sectionKind: SectionKind.Verse);

        var bucket = Assert.Single(ReuseRateMiner.Mine(song));
        Assert.Equal(1, bucket.OccurrenceCount);
    }

    private static CorpusSongAnalysis BuildSong(int barCount, IReadOnlyList<int> phraseLengths, SectionKind sectionKind)
    {
        var entry = new CorpusEntry(@"C:\fake\Song.xsq", @"C:\fake\Song.mp3", "Song", CorpusProvenanceTag.ExternalReference, [], "fixture");

        // Uniform energy across every bar -- every phrase's own mean energy (and therefore percentile
        // rank/energy tier) is identical, isolating phrase-length + section-kind as the only remaining
        // bucket dimensions these tests actually exercise.
        var bars = Enumerable.Range(0, barCount)
            .Select(i => new BarFingerprint(
                BarIndex: i, Window: new TimeWindow(i * TicksPerBar, (i + 1) * TicksPerBar),
                Features: [0.5, 0.0], Confidence: 1.0, IsSilent: false))
            .ToList();
        var fingerprintSet = new SongFingerprintSet(
            Guid.NewGuid(), new SongId(Guid.NewGuid()), "hash", Guid.NewGuid(), "fixture/1.0",
            new BarFingerprintSettings(12, "fixture-recipe"), bars);

        var beats = Enumerable.Range(0, barCount * 4)
            .Select(i => new BeatMarker(Guid.NewGuid(), i * (TicksPerBar / 4), i % 4, i / 4, 1.0, false))
            .ToArray();
        var beatGrid = new BeatGrid(
            Guid.NewGuid(), fingerprintSet.SongId, "hash", Guid.NewGuid(), Guid.NewGuid(), "fixture/1.0",
            new BeatGridSettings(4, 1000), new TempoHypothesis(120, 1.0, "4/4"), beats, [new TempoHypothesis(120, 1.0, "4/4")], 0);

        var label = new SectionLabelCandidate(Guid.NewGuid(), Guid.NewGuid(), 0, barCount, sectionKind, 1.0, ["fixture"], false);
        var labels = new SectionLabelAnalysis(
            Guid.NewGuid(), fingerprintSet.SongId, "hash", Guid.NewGuid(), fingerprintSet.VersionId, "fixture/1.0",
            new SectionLabelSettings(0.85, 0.5, "fixture-recipe"), [label]);

        var phraseCandidates = new List<PhraseCandidate>();
        var cursor = 0;
        foreach (var length in phraseLengths)
        {
            phraseCandidates.Add(new PhraseCandidate(
                Guid.NewGuid(), label.Id, cursor, cursor + length, cursor * TicksPerBar, (cursor + length) * TicksPerBar, length,
                PhraseBoundarySource.BarHypothesis, 1.0, ["fixture"], "sig", false));
            cursor += length;
        }

        var phrases = new PhraseSegmentationAnalysis(
            Guid.NewGuid(), fingerprintSet.SongId, "hash", labels.VersionId, fingerprintSet.VersionId, null, "fixture/1.0",
            new PhraseSegmentationSettings(8, 1, 2_000_000, "fixture-recipe"), phraseCandidates);

        var aligned = EffectAligner.Align([], beatGrid, fingerprintSet, labels, phrases);

        return new CorpusSongAnalysis(entry, beatGrid, fingerprintSet, labels, phrases, aligned);
    }
}
