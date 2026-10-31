namespace XlightsAI.Studio.Director.Tests;

/// <summary>Architecture §13's own storage layout: "llm/ full call log with costs." One file per call, atomic, never overwritten.</summary>
public sealed class JsonAiCallLogStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-phase6-calllog-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static AiCallLogEntry Entry(Guid songId, decimal? cost = 1.23m) =>
        new(songId, AiRole.Director, "claude-sonnet-5", DateTimeOffset.UtcNow, new AiUsage(1000, 200, 0, 0), cost);

    [Fact]
    public async Task AppendAsync_WritesOneFilePerCall_NeverOverwrites()
    {
        var store = new JsonAiCallLogStore(_root);
        var songId = Guid.NewGuid();

        await store.AppendAsync(Entry(songId), CancellationToken.None);
        await store.AppendAsync(Entry(songId), CancellationToken.None);

        Assert.Equal(2, Directory.GetFiles(_root, "call-*.json").Length);
    }

    [Fact]
    public async Task ListForSongAsync_ReturnsOnlyEntriesForThatSong_InTimestampOrder()
    {
        var store = new JsonAiCallLogStore(_root);
        var songA = Guid.NewGuid();
        var songB = Guid.NewGuid();

        await store.AppendAsync(Entry(songA), CancellationToken.None);
        await store.AppendAsync(Entry(songB), CancellationToken.None);
        await store.AppendAsync(Entry(songA), CancellationToken.None);

        var entriesForA = await store.ListForSongAsync(songA, CancellationToken.None);

        Assert.Equal(2, entriesForA.Count);
        Assert.All(entriesForA, e => Assert.Equal(songA, e.SongId));
    }

    [Fact]
    public async Task SumCostForSongAsync_SumsKnownCosts_TreatsNullCostAsZero()
    {
        var store = new JsonAiCallLogStore(_root);
        var songId = Guid.NewGuid();

        await store.AppendAsync(Entry(songId, cost: 1.5m), CancellationToken.None);
        await store.AppendAsync(Entry(songId, cost: null), CancellationToken.None); // e.g. a free local Ollama call.
        await store.AppendAsync(Entry(songId, cost: 0.25m), CancellationToken.None);

        var total = await store.SumCostForSongAsync(songId, CancellationToken.None);

        Assert.Equal(1.75m, total);
    }

    [Fact]
    public async Task ListForSongAsync_NoCallsLogged_ReturnsEmpty()
    {
        var store = new JsonAiCallLogStore(_root);

        var entries = await store.ListForSongAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Empty(entries);
    }
}
