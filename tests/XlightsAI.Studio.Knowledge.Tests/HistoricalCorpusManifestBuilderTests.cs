namespace XlightsAI.Studio.Knowledge.Tests;

public sealed class HistoricalCorpusManifestBuilderTests
{
    [Fact]
    public void Build_PairsSameStemAudio_AndRecordsSourceCommit()
    {
        var root = Path.Combine(Path.GetTempPath(), $"historical-manifest-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "Some Song.xsq"), "<xsequence/>");
            File.WriteAllText(Path.Combine(root, "Some Song.mp3"), "audio");

            var manifest = HistoricalCorpusManifestBuilder.Build(root, "abc1234");

            var entry = Assert.Single(manifest.Entries);
            Assert.Equal("Some Song", entry.SongTitle);
            Assert.Equal("abc1234", entry.SourceCommit);
            Assert.Equal(CorpusProvenanceTag.UserAuthored, entry.Provenance);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Build_FallsBackToSoleAudioFileInDirectory_WhenNoSameStemMatch()
    {
        var root = Path.Combine(Path.GetTempPath(), $"historical-manifest-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var subdir = Path.Combine(root, "SomeShow");
            Directory.CreateDirectory(subdir);
            File.WriteAllText(Path.Combine(subdir, "sequence.xsq"), "<xsequence/>");
            File.WriteAllText(Path.Combine(subdir, "Totally Different Name.mp3"), "audio");

            var manifest = HistoricalCorpusManifestBuilder.Build(root, "abc1234");

            var entry = Assert.Single(manifest.Entries);
            Assert.EndsWith("Totally Different Name.mp3", entry.AudioPath);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Build_ExcludesAmbiguousOrUnresolvableAudio_NeverGuesses()
    {
        var root = Path.Combine(Path.GetTempPath(), $"historical-manifest-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var ambiguousDir = Path.Combine(root, "Ambiguous");
            Directory.CreateDirectory(ambiguousDir);
            File.WriteAllText(Path.Combine(ambiguousDir, "sequence.xsq"), "<xsequence/>");
            File.WriteAllText(Path.Combine(ambiguousDir, "one.mp3"), "audio1");
            File.WriteAllText(Path.Combine(ambiguousDir, "two.mp3"), "audio2");

            var noAudioDir = Path.Combine(root, "NoAudio");
            Directory.CreateDirectory(noAudioDir);
            File.WriteAllText(Path.Combine(noAudioDir, "sequence.xsq"), "<xsequence/>");

            var manifest = HistoricalCorpusManifestBuilder.Build(root, "abc1234");

            Assert.Empty(manifest.Entries);
            Assert.Equal(2, manifest.ExcludedPaths.Count);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Build_UsesKnownProvenanceCrossReference_WhenTitleMatches()
    {
        var root = Path.Combine(Path.GetTempPath(), $"historical-manifest-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "Vendor Show.xsq"), "<xsequence/>");
            File.WriteAllText(Path.Combine(root, "Vendor Show.mp3"), "audio");

            var knownProvenance = new Dictionary<string, CorpusProvenanceTag>(StringComparer.OrdinalIgnoreCase)
            {
                ["Vendor Show"] = CorpusProvenanceTag.LikelyVendorOrCommunityDownload,
            };

            var manifest = HistoricalCorpusManifestBuilder.Build(root, "abc1234", knownProvenance);

            Assert.Equal(CorpusProvenanceTag.LikelyVendorOrCommunityDownload, manifest.Entries.Single().Provenance);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Build_ThrowsForMissingDirectory()
    {
        Assert.Throws<DirectoryNotFoundException>(() =>
            HistoricalCorpusManifestBuilder.Build(Path.Combine(Path.GetTempPath(), $"does-not-exist-{Guid.NewGuid():N}"), "abc1234"));
    }
}
