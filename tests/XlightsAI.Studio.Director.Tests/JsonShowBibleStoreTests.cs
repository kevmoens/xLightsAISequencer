namespace XlightsAI.Studio.Director.Tests;

/// <summary>Build-plan Phase 5 task 6: "Persist the Show Bible as a diffable artifact under the project's bible/ directory using the existing atomic JSON-store pattern."</summary>
public sealed class JsonShowBibleStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-phase5-bible-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static ShowBible BuildBible(Guid songId) => new(
        Guid.NewGuid(), songId, ShowBibleContentFixture.BuildValid(), DateTimeOffset.UtcNow);

    [Fact]
    public async Task SaveNextVersionAsync_FirstSave_WritesV1()
    {
        var store = new JsonShowBibleStore(_root);
        var songId = Guid.NewGuid();

        var path = await store.SaveNextVersionAsync(BuildBible(songId), CancellationToken.None);

        Assert.Equal(store.GetPath(songId, 1), path);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task SaveNextVersionAsync_SecondSave_WritesV2_NeverOverwritesV1()
    {
        var store = new JsonShowBibleStore(_root);
        var songId = Guid.NewGuid();

        var pathV1 = await store.SaveNextVersionAsync(BuildBible(songId), CancellationToken.None);
        var v1Bytes = await File.ReadAllBytesAsync(pathV1);

        var pathV2 = await store.SaveNextVersionAsync(BuildBible(songId), CancellationToken.None);

        Assert.NotEqual(pathV1, pathV2);
        Assert.True(File.Exists(pathV1));
        Assert.Equal(v1Bytes, await File.ReadAllBytesAsync(pathV1)); // untouched.
        Assert.Equal([1, 2], await store.ListVersionsAsync(songId, CancellationToken.None));
    }

    [Fact]
    public async Task LoadLatestAsync_ReturnsHighestVersion_RoundTripsContent()
    {
        var store = new JsonShowBibleStore(_root);
        var songId = Guid.NewGuid();
        await store.SaveNextVersionAsync(BuildBible(songId), CancellationToken.None);
        var secondBible = BuildBible(songId);
        await store.SaveNextVersionAsync(secondBible, CancellationToken.None);

        var loaded = await store.LoadLatestAsync(songId, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(secondBible.Id, loaded!.Id);
        Assert.Equal(secondBible.Content.Looks.Looks.Count, loaded.Content.Looks.Looks.Count);
        Assert.Equal(secondBible.Content.Palettes.Count, loaded.Content.Palettes.Count);
    }

    [Fact]
    public async Task LoadLatestAsync_NeverSaved_ReturnsNull()
    {
        var store = new JsonShowBibleStore(_root);

        var loaded = await store.LoadLatestAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task LoadVersionAsync_CorruptFile_ThrowsShowBibleCorruptException()
    {
        var store = new JsonShowBibleStore(_root);
        var songId = Guid.NewGuid();
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(store.GetPath(songId, 1), "{ not valid json");

        await Assert.ThrowsAsync<ShowBibleCorruptException>(() => store.LoadVersionAsync(songId, 1, CancellationToken.None));
    }
}
