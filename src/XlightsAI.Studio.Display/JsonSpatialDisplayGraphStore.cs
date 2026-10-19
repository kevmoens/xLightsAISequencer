using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace XlightsAI.Studio.Display;

/// <summary>
/// JSON-file-backed <see cref="ISpatialDisplayGraphStore"/>. Writes are atomic (same-directory temp file
/// + <see cref="File.Move(string, string, bool)"/> replace), the same pattern
/// <see cref="XlightsAI.Studio.Structure.JsonBarFingerprintStore"/> already uses. Never opens, reads for
/// writing, or otherwise touches any <c>xlights_rgbeffects.xml</c>/show-directory file -- only its own
/// cache directory.
/// </summary>
public sealed class JsonSpatialDisplayGraphStore : ISpatialDisplayGraphStore
{
    private readonly string _directory;

    public JsonSpatialDisplayGraphStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string GetPath(string sourceLayoutHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceLayoutHash);
        var shortHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sourceLayoutHash)))[..16];
        return Path.Combine(_directory, $"spatial-display-graph-{shortHash}.json");
    }

    public async Task<string> SaveAsync(SpatialDisplayGraph graph, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(graph);

        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(_directory);
        var finalPath = GetPath(graph.SourceLayoutHash);
        var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";

        try
        {
            var json = JsonSerializer.Serialize(graph);
            await File.WriteAllTextAsync(tempPath, json, ct).ConfigureAwait(false);

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

    public async Task<SpatialDisplayGraph> LoadAsync(string path, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No spatial display graph cache exists at '{path}'.", path);
        }

        await using var stream = File.OpenRead(path);
        try
        {
            var graph = await JsonSerializer.DeserializeAsync<SpatialDisplayGraph>(stream, cancellationToken: ct).ConfigureAwait(false);
            return graph ?? throw new SpatialDisplayGraphCorruptException(path, new InvalidOperationException("Deserialized to null."));
        }
        catch (JsonException ex)
        {
            throw new SpatialDisplayGraphCorruptException(path, ex);
        }
    }
}
