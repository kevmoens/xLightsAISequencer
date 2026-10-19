using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace XlightsAI.Studio.Display;

/// <summary>
/// JSON-file-backed <see cref="IDisplayLayoutCorrectionsStore"/>. Writes are atomic (same-directory temp
/// file + <see cref="File.Move(string, string, bool)"/> replace), the exact same pattern
/// <see cref="JsonSpatialDisplayGraphStore"/> already uses. Never opens, reads for writing, or otherwise
/// touches any <c>xlights_rgbeffects.xml</c>/show-directory file or the separate spatial-display-graph
/// cache file -- only its own cache directory.
/// </summary>
public sealed class JsonDisplayLayoutCorrectionsStore : IDisplayLayoutCorrectionsStore
{
    private readonly string _directory;

    public JsonDisplayLayoutCorrectionsStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string GetPath(string sourceLayoutHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceLayoutHash);
        var shortHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sourceLayoutHash)))[..16];
        return Path.Combine(_directory, $"display-layout-corrections-{shortHash}.json");
    }

    public async Task<string> SaveAsync(DisplayLayoutCorrections corrections, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(corrections);

        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(_directory);
        var finalPath = GetPath(corrections.SourceLayoutHash);
        var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";

        try
        {
            var json = JsonSerializer.Serialize(corrections);
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

    public async Task<DisplayLayoutCorrections> LoadAsync(string path, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No display layout corrections exist at '{path}'.", path);
        }

        await using var stream = File.OpenRead(path);
        try
        {
            var corrections = await JsonSerializer.DeserializeAsync<DisplayLayoutCorrections>(stream, cancellationToken: ct).ConfigureAwait(false);
            return corrections ?? throw new SpatialDisplayGraphCorruptException(path, new InvalidOperationException("Deserialized to null."));
        }
        catch (JsonException ex)
        {
            throw new SpatialDisplayGraphCorruptException(path, ex);
        }
    }
}
