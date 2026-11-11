using System.Text.Json;
using XlightsAI.Studio.Knowledge;

namespace XlightsAI.Studio.Pipeline;

/// <summary>
/// Build-plan Phase 20 §2b: JSON-file-backed persistence for the real, corpus-mined (additive) half of a
/// <see cref="MultiLayerTemplateCatalog"/> -- one per knowledge directory, same atomic-write convention as
/// <see cref="JsonPatternLibraryStore"/>. This is deliberately a SEPARATE file from the 3 hand-curated seed
/// templates embedded in <c>XlightsAI.Studio.Knowledge</c>'s own assembly -- <see cref="MultiLayerTemplateCatalogLoader"/>
/// merges the two at load time, so re-running corpus mining can never silently overwrite/lose the
/// hand-curated seed catalog.
/// </summary>
public sealed class JsonMultiLayerTemplateCatalogStore
{
    private const string FileName = "multilayer-templates-mined.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _directory;

    public JsonMultiLayerTemplateCatalogStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string Path => System.IO.Path.Combine(_directory, FileName);

    public async Task<string> SaveAsync(MultiLayerTemplateCatalog catalog, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        Directory.CreateDirectory(_directory);
        var finalPath = Path;
        var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";
        try
        {
            await File.WriteAllTextAsync(tempPath, JsonSerializer.Serialize(catalog, JsonOptions), ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            File.Move(tempPath, finalPath, overwrite: true);
            return finalPath;
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            throw;
        }
    }

    public async Task<MultiLayerTemplateCatalog> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(Path))
        {
            throw new FileNotFoundException($"No mined multi-layer template catalog exists at '{Path}'.", Path);
        }

        await using var stream = File.OpenRead(Path);
        var catalog = await JsonSerializer.DeserializeAsync<MultiLayerTemplateCatalog>(stream, cancellationToken: ct).ConfigureAwait(false);
        return catalog ?? throw new InvalidOperationException($"Mined multi-layer template catalog at '{Path}' deserialized to null.");
    }
}
