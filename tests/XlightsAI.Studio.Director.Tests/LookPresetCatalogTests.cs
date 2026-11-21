using XlightsAI.Studio.Choreography;

namespace XlightsAI.Studio.Director.Tests;

/// <summary>
/// Build-plan Phase 30 step 30.3: the generator's own internal reuse path -- <see cref="LookPresetCatalog"/>
/// wrapping <see cref="JsonLookPresetStore"/> with the same name-validation/monotonic-versioning
/// convention <c>Composition.VisualBudgetPresetCatalog</c> already established for its own, unrelated
/// generation-settings presets.
/// </summary>
public sealed class LookPresetCatalogTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "xlai-look-presets-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static Look BuildLook() => new(
        "src", "Src", "intent", 0.8, "chase", "warm-gold", "Tree",
        [new LookLayer(1, "Tree", "On", LookLayer.RoleSubject, null, "Normal", "Bars", 0.9)]);

    [Fact]
    public async Task SaveAsync_ThenGetAsync_RoundTripsTheSamePreset()
    {
        var catalog = new LookPresetCatalog(new JsonLookPresetStore(_directory));

        var saved = await catalog.SaveAsync("my-preset", BuildLook(), "test note", CancellationToken.None);
        Assert.Equal(0, saved.Version);

        var loaded = await catalog.GetAsync("my-preset", CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal("my-preset", loaded!.Name);
        Assert.Equal("test note", loaded.SourceNote);
        Assert.Single(loaded.Layers);
        Assert.Equal("On", loaded.Layers[0].Effect);
    }

    [Fact]
    public async Task SaveAsync_Twice_IncrementsVersion()
    {
        var catalog = new LookPresetCatalog(new JsonLookPresetStore(_directory));

        await catalog.SaveAsync("versioned", BuildLook(), null, CancellationToken.None);
        var second = await catalog.SaveAsync("versioned", BuildLook(), null, CancellationToken.None);

        Assert.Equal(1, second.Version);
    }

    [Fact]
    public async Task GetAsync_UnknownName_ReturnsNull()
    {
        var catalog = new LookPresetCatalog(new JsonLookPresetStore(_directory));
        var result = await catalog.GetAsync("never-saved", CancellationToken.None);
        Assert.Null(result);
    }

    [Fact]
    public async Task ApplyAsync_UnknownName_ReturnsNull()
    {
        var catalog = new LookPresetCatalog(new JsonLookPresetStore(_directory));
        var applied = await catalog.ApplyAsync(
            "never-saved", "new-id", "New", "intent", 0.5, "pal", "Subject", "group:Subject", CancellationToken.None);
        Assert.Null(applied);
    }

    [Fact]
    public async Task ApplyAsync_SavedPreset_RetargetsAndRepalettesButKeepsStructure()
    {
        var catalog = new LookPresetCatalog(new JsonLookPresetStore(_directory));
        await catalog.SaveAsync("apply-me", BuildLook(), null, CancellationToken.None);

        var applied = await catalog.ApplyAsync(
            "apply-me", "new-id", "New Look", "new intent", 0.5, "ice-blue", "MiniTrees", "group:MiniTrees", CancellationToken.None);

        Assert.NotNull(applied);
        Assert.Equal("ice-blue", applied!.PaletteRef);
        Assert.Equal("MiniTrees", applied.Subject);
        var layer = Assert.Single(applied.Layers);
        Assert.Equal("On", layer.Effect);
        Assert.Equal("group:MiniTrees", layer.Props);
        Assert.Equal(0.9, layer.Duty);
    }

    [Fact]
    public async Task ListNamesAsync_ReturnsEverySavedPresetOrdinalSorted()
    {
        var catalog = new LookPresetCatalog(new JsonLookPresetStore(_directory));
        await catalog.SaveAsync("zeta", BuildLook(), null, CancellationToken.None);
        await catalog.SaveAsync("alpha", BuildLook(), null, CancellationToken.None);

        var names = await catalog.ListNamesAsync(CancellationToken.None);
        Assert.Equal(["alpha", "zeta"], names);
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad/name")]
    public async Task SaveAsync_InvalidName_Throws(string name)
    {
        var catalog = new LookPresetCatalog(new JsonLookPresetStore(_directory));
        await Assert.ThrowsAsync<ArgumentException>(() => catalog.SaveAsync(name, BuildLook(), null, CancellationToken.None));
    }
}
