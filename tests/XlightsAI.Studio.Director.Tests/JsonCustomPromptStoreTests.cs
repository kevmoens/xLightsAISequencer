using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Director.Tests;

/// <summary>Build-plan Phase 14 §2a.2: same atomic-write/round-trip discipline as <see cref="JsonRoleProviderConfigStoreTests"/>, applied to <see cref="JsonCustomPromptStore"/>.</summary>
public sealed class JsonCustomPromptStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-phase14-custom-prompt-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_NeverSaved_ReturnsEmptyDefault()
    {
        var store = new JsonCustomPromptStore(_root);

        var prompt = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(CustomGenerationPrompt.Empty, prompt);
        Assert.True(prompt.IsEmpty);
    }

    [Fact]
    public async Task SaveAsync_ThenLoad_RoundTrips()
    {
        var store = new JsonCustomPromptStore(_root);
        var prompt = new CustomGenerationPrompt("Use a red and gold palette throughout; avoid blue.");

        await store.SaveAsync(prompt, CancellationToken.None);
        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(prompt, loaded);
    }

    [Fact]
    public async Task SaveAsync_ThenLoad_RoundTripsStyleAndStayCloseToExemplars()
    {
        // User feedback (2026-11-01): the style picker needs this to round-trip across sessions the
        // same way PromptText/StayCloseToExemplars already do.
        var store = new JsonCustomPromptStore(_root);
        var prompt = new CustomGenerationPrompt("Use a red and gold palette throughout.", StayCloseToExemplars: false, Style: "Steady Wash / Accent");

        await store.SaveAsync(prompt, CancellationToken.None);
        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(prompt, loaded);
        Assert.Equal("Steady Wash / Accent", loaded.Style);
        Assert.False(loaded.StayCloseToExemplars);
    }

    [Fact]
    public async Task LoadAsync_PreExistingFileWithNoStyleProperty_DeserializesStyleAsNull()
    {
        // A custom-prompt.json saved before this field existed has no "Style" JSON property at all --
        // must deserialize as null (meaning "use the app's own startup-configured default"), never throw
        // or silently invent a value.
        var store = new JsonCustomPromptStore(_root);
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(store.GetPath(), """{"Text":"legacy text","StayCloseToExemplars":true}""", CancellationToken.None);

        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.Equal("legacy text", loaded.Text);
        Assert.Null(loaded.Style);
    }

    [Fact]
    public async Task SaveAsync_WithDistinctFileNames_NeverOverwritesEachOther()
    {
        // Build-plan Phase 14 §2c.3: ChoreographyGenerationPipeline keys the per-run record by songId
        // so repeated runs never overwrite each other's own record -- this is the same store class,
        // just constructed with a different filename.
        var editorStore = new JsonCustomPromptStore(_root);
        var runRecordStore = new JsonCustomPromptStore(_root, "custom-prompt-used-abc123.json");

        await editorStore.SaveAsync(new CustomGenerationPrompt("Editor text."), CancellationToken.None);
        await runRecordStore.SaveAsync(new CustomGenerationPrompt("Run-specific text."), CancellationToken.None);

        Assert.Equal("Editor text.", (await editorStore.LoadAsync(CancellationToken.None)).Text);
        Assert.Equal("Run-specific text.", (await runRecordStore.LoadAsync(CancellationToken.None)).Text);
    }

    [Fact]
    public void Constructor_TextOverCap_Throws()
    {
        Assert.Throws<ArgumentException>(() => new CustomGenerationPrompt(new string('x', CustomGenerationPrompt.MaxLength + 1)));
    }
}
