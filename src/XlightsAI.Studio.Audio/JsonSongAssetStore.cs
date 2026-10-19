using System.Text.Json;

namespace XlightsAI.Studio.Audio;

/// <summary>
/// JSON-file-backed <see cref="ISongAssetStore"/>. Writes are atomic (same-directory temp file +
/// <see cref="File.Move(string, string, bool)"/> replace) — the same pattern
/// <c>JsonSequenceDocumentStore</c>/<c>JsonProjectStore</c>/<c>SettingsStore</c> already use for
/// every other app-owned manifest in this repository. Each asset record is written to its own file
/// named by its own <see cref="SongAsset.Id"/>, so concurrently probed songs never share a temp/
/// final path. Never touches the original source audio file.
/// </summary>
public sealed class JsonSongAssetStore : ISongAssetStore
{
    private readonly string _directory;

    public JsonSongAssetStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public async Task<string> SaveAsync(SongAsset asset, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(asset);

        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(_directory);
        var finalPath = Path.Combine(_directory, $"song-asset-{asset.Id.Value:N}.json");
        var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";

        try
        {
            var json = JsonSerializer.Serialize(asset);
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

    public async Task<SongAsset> LoadAsync(string path, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No song asset record exists at '{path}'.", path);
        }

        await using var stream = File.OpenRead(path);
        try
        {
            var asset = await JsonSerializer.DeserializeAsync<SongAsset>(stream, cancellationToken: ct).ConfigureAwait(false);
            return asset ?? throw new SongAssetCorruptException(path, new InvalidOperationException("Deserialized to null."));
        }
        catch (JsonException ex)
        {
            throw new SongAssetCorruptException(path, ex);
        }
    }
}
