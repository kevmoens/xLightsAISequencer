using XlightsAI.Studio.Choreography;

namespace XlightsAI.Studio.Director.Tests;

/// <summary>Build-plan "richer analysis tools" post-processing phase (2026-11-02): <see cref="ChoreographyGenerationPipeline.LoadPersistedScriptsAsync"/>.</summary>
public sealed class ChoreographyGenerationPipelineLoadPersistedScriptsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"xlai-load-persisted-scripts-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task WriteScriptFileAsync(Guid songId, SectionScript script)
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, $"section-script-{songId:N}-{script.SectionId:N}.json");
        var json = System.Text.Json.JsonSerializer.Serialize(script);
        await File.WriteAllTextAsync(path, json);
    }

    [Fact]
    public async Task LoadPersistedScriptsAsync_PersistedScripts_RoundTripKeyedBySectionId()
    {
        var songId = Guid.NewGuid();
        var scriptA = new SectionScript(Guid.NewGuid(), "look-a");
        var scriptB = new SectionScript(Guid.NewGuid(), "look-b");
        await WriteScriptFileAsync(songId, scriptA);
        await WriteScriptFileAsync(songId, scriptB);

        var loaded = await ChoreographyGenerationPipeline.LoadPersistedScriptsAsync(_root, songId, CancellationToken.None);

        Assert.Equal(2, loaded.Count);
        Assert.Equal("look-a", loaded[scriptA.SectionId].LookId);
        Assert.Equal("look-b", loaded[scriptB.SectionId].LookId);
    }

    [Fact]
    public async Task LoadPersistedScriptsAsync_DifferentSongId_NeverReturnsAnotherSongsScripts()
    {
        var songA = Guid.NewGuid();
        var songB = Guid.NewGuid();
        await WriteScriptFileAsync(songA, new SectionScript(Guid.NewGuid(), "look-a"));

        var loaded = await ChoreographyGenerationPipeline.LoadPersistedScriptsAsync(_root, songB, CancellationToken.None);

        Assert.Empty(loaded);
    }

    [Fact]
    public async Task LoadPersistedScriptsAsync_DirectoryDoesNotExist_ReturnsEmpty_RatherThanThrowing()
    {
        var loaded = await ChoreographyGenerationPipeline.LoadPersistedScriptsAsync(
            Path.Combine(_root, "never-created"), Guid.NewGuid(), CancellationToken.None);

        Assert.Empty(loaded);
    }
}
