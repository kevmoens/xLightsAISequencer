namespace XlightsAI.Studio.Knowledge.Tests;

public sealed class CorpusManifestMergerTests
{
    [Fact]
    public void MergeDeduplicatingByContentHash_KeepsFirstManifestsEntry_WhenBytesAreIdentical()
    {
        var root = Path.Combine(Path.GetTempPath(), $"corpus-merge-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var liveSeq = Path.Combine(root, "live-song.xsq");
            var liveAudio = Path.Combine(root, "live-song.mp3");
            File.WriteAllText(liveSeq, "<xsequence>same bytes</xsequence>");
            File.WriteAllText(liveAudio, "audio");

            var historicalSeq = Path.Combine(root, "historical-song.xsq");
            File.WriteAllText(historicalSeq, "<xsequence>same bytes</xsequence>"); // identical content to liveSeq

            var liveManifest = new CorpusManifest(
                DateTimeOffset.UtcNow, root,
                [new CorpusEntry(liveSeq, liveAudio, "live-song", CorpusProvenanceTag.UserAuthored, [], "live")],
                new Dictionary<string, string>());

            var historicalManifest = new CorpusManifest(
                DateTimeOffset.UtcNow, root,
                [new CorpusEntry(historicalSeq, liveAudio, "historical-song", CorpusProvenanceTag.UserAuthored, [], "historical", SourceCommit: "abc123")],
                new Dictionary<string, string>());

            var merged = CorpusManifestMerger.MergeDeduplicatingByContentHash([liveManifest, historicalManifest]);

            Assert.Single(merged.Entries);
            var kept = merged.Entries[0];
            Assert.Equal(liveSeq, kept.SequencePath); // live manifest (passed first) wins the tie
            Assert.Contains(historicalSeq, kept.DuplicatePaths);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MergeDeduplicatingByContentHash_KeepsBothEntries_WhenContentDiffers()
    {
        var root = Path.Combine(Path.GetTempPath(), $"corpus-merge-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var seqA = Path.Combine(root, "a.xsq");
            var seqB = Path.Combine(root, "b.xsq");
            var audio = Path.Combine(root, "a.mp3");
            File.WriteAllText(seqA, "<xsequence>version 1</xsequence>");
            File.WriteAllText(seqB, "<xsequence>version 2 -- re-edited</xsequence>");
            File.WriteAllText(audio, "audio");

            var manifestA = new CorpusManifest(
                DateTimeOffset.UtcNow, root,
                [new CorpusEntry(seqA, audio, "a", CorpusProvenanceTag.UserAuthored, [], "a")],
                new Dictionary<string, string>());
            var manifestB = new CorpusManifest(
                DateTimeOffset.UtcNow, root,
                [new CorpusEntry(seqB, audio, "b", CorpusProvenanceTag.UserAuthored, [], "b")],
                new Dictionary<string, string>());

            var merged = CorpusManifestMerger.MergeDeduplicatingByContentHash([manifestA, manifestB]);

            Assert.Equal(2, merged.Entries.Count);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MergeDeduplicatingByContentHash_UnionsExcludedPaths()
    {
        var manifestA = new CorpusManifest(DateTimeOffset.UtcNow, "root", [], new Dictionary<string, string> { ["x"] = "reason x" });
        var manifestB = new CorpusManifest(DateTimeOffset.UtcNow, "root", [], new Dictionary<string, string> { ["y"] = "reason y" });

        var merged = CorpusManifestMerger.MergeDeduplicatingByContentHash([manifestA, manifestB]);

        Assert.Equal(2, merged.ExcludedPaths.Count);
    }

    [Fact]
    public void MergeDeduplicatingByContentHash_ThrowsForEmptyList()
    {
        Assert.Throws<ArgumentException>(() => CorpusManifestMerger.MergeDeduplicatingByContentHash([]));
    }
}
