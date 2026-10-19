using System.Text.Json;
using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Structure;

/// <summary>
/// JSON-file-backed <see cref="IBarFingerprintStore"/>. Writes are atomic (same-directory temp file +
/// <see cref="File.Move(string, string, bool)"/> replace) -- the same pattern
/// <see cref="XlightsAI.Studio.Rhythm.JsonBeatGridStore"/> already uses. Each set is written to its own
/// file named deterministically by its <see cref="SongFingerprintSet.SongId"/>, so re-running
/// <see cref="IBarFingerprintBuilder.BuildAsync"/> for the same song always resolves to the same path.
/// </summary>
public sealed class JsonBarFingerprintStore : IBarFingerprintStore
{
    private readonly string _directory;

    public JsonBarFingerprintStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string GetPath(SongId songId) =>
        Path.Combine(_directory, $"bar-fingerprints-{songId.Value:N}.json");

    public async Task<string> SaveAsync(SongFingerprintSet set, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(set);

        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(_directory);
        var finalPath = GetPath(set.SongId);
        var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";

        try
        {
            var json = JsonSerializer.Serialize(set);
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

    public async Task<SongFingerprintSet> LoadAsync(string path, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No bar fingerprint cache exists at '{path}'.", path);
        }

        await using var stream = File.OpenRead(path);
        try
        {
            var set = await JsonSerializer.DeserializeAsync<SongFingerprintSet>(stream, cancellationToken: ct).ConfigureAwait(false);
            return set ?? throw new BarFingerprintCorruptException(path, new InvalidOperationException("Deserialized to null."));
        }
        catch (JsonException ex)
        {
            throw new BarFingerprintCorruptException(path, ex);
        }
    }
}
