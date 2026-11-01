using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Pipeline.Tests;

public sealed class ExemplarOverridesTests
{
    private static ExemplarEntry BuildEntry(string songTitle, int start, int end, SectionKind kind = SectionKind.Chorus) =>
        new(new ExemplarFingerprint(songTitle, CorpusProvenanceTag.UserAuthored, start, end, 120, 0.5, kind, null, null), []);

    [Fact]
    public void ApplyOverrides_RemovesOnlyEntriesExplicitlyDisabled()
    {
        var enabled = BuildEntry("Song A", 0, 8);
        var disabled = BuildEntry("Song B", 8, 16);
        var notYetCurated = BuildEntry("Song C", 16, 24);
        var index = new ExemplarIndex(DateTimeOffset.UtcNow, [enabled, disabled, notYetCurated]);

        var overrides = new Dictionary<string, bool>
        {
            [ExemplarOverrides.KeyFor(enabled.Fingerprint)] = false,
            [ExemplarOverrides.KeyFor(disabled.Fingerprint)] = true,
        };

        var filtered = ExemplarOverrides.ApplyOverrides(index, overrides);

        Assert.Equal(2, filtered.Entries.Count);
        Assert.Contains(filtered.Entries, e => e.Fingerprint.SongTitle == "Song A");
        Assert.Contains(filtered.Entries, e => e.Fingerprint.SongTitle == "Song C");
        Assert.DoesNotContain(filtered.Entries, e => e.Fingerprint.SongTitle == "Song B");
    }

    [Fact]
    public void ApplyOverrides_WithEmptyMap_ReturnsSameIndexUnchanged()
    {
        var index = new ExemplarIndex(DateTimeOffset.UtcNow, [BuildEntry("Song A", 0, 8)]);

        var filtered = ExemplarOverrides.ApplyOverrides(index, new Dictionary<string, bool>());

        Assert.Same(index, filtered);
    }

    [Fact]
    public void KeyFor_DistinguishesByBarRange_NotJustSongTitle()
    {
        var first = BuildEntry("Song A", 0, 8).Fingerprint;
        var second = BuildEntry("Song A", 8, 16).Fingerprint;

        Assert.NotEqual(ExemplarOverrides.KeyFor(first), ExemplarOverrides.KeyFor(second));
    }

    [Fact]
    public async Task JsonExemplarOverrideStore_MissingFile_ReturnsEmptyMap_NeverThrows()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"exemplar-overrides-test-{Guid.NewGuid():N}");
        var store = new JsonExemplarOverrideStore(directory);

        var overrides = await store.LoadAsync(CancellationToken.None);

        Assert.Empty(overrides);
    }

    [Fact]
    public async Task JsonExemplarOverrideStore_RoundTripsSaveAndLoad()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"exemplar-overrides-test-{Guid.NewGuid():N}");
        try
        {
            var store = new JsonExemplarOverrideStore(directory);
            var overrides = new Dictionary<string, bool> { ["Song A::0-8"] = false, ["Song B::8-16"] = true };

            await store.SaveAsync(overrides, CancellationToken.None);
            var loaded = await store.LoadAsync(CancellationToken.None);

            Assert.Equal(overrides, loaded);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task JsonExemplarOverrideStore_SurvivesEvenIfExemplarIndexIsRebuilt()
    {
        // Build-plan Phase 17 §2e acceptance criterion: re-running corpus mining (which only ever
        // touches exemplar-index.json) must never reset a user's curation choices in the separate file.
        var directory = Path.Combine(Path.GetTempPath(), $"exemplar-overrides-test-{Guid.NewGuid():N}");
        try
        {
            var overrideStore = new JsonExemplarOverrideStore(directory);
            await overrideStore.SaveAsync(new Dictionary<string, bool> { ["Song A::0-8"] = false }, CancellationToken.None);

            var indexStore = new JsonExemplarIndexStore(directory);
            await indexStore.SaveAsync(new ExemplarIndex(DateTimeOffset.UtcNow, [BuildEntry("Song A", 0, 8)]), CancellationToken.None);
            await indexStore.SaveAsync(new ExemplarIndex(DateTimeOffset.UtcNow, [BuildEntry("Song A", 0, 8), BuildEntry("Song B", 0, 8)]), CancellationToken.None);

            var overridesAfterReMine = await overrideStore.LoadAsync(CancellationToken.None);

            Assert.False(overridesAfterReMine["Song A::0-8"]);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
