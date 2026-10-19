using System.Text.Json;
using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Audio;

/// <summary>
/// JSON-file-backed <see cref="IWaveformPyramidStore"/>. Writes are atomic (same-directory temp file
/// + <see cref="File.Move(string, string, bool)"/> replace) — the same pattern
/// <see cref="JsonSongAssetStore"/> already uses. Each pyramid is written to its own file named
/// deterministically by its <see cref="WaveformPyramidCache.SongId"/>, so re-running
/// <see cref="WaveformPyramidBuilder.BuildPyramidAsync"/> for the same song always resolves to the
/// same path.
/// </summary>
public sealed class JsonWaveformPyramidStore : IWaveformPyramidStore
{
    private readonly string _directory;

    public JsonWaveformPyramidStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string GetPath(SongId songId) =>
        Path.Combine(_directory, $"waveform-pyramid-{songId.Value:N}.json");

    public async Task<string> SaveAsync(WaveformPyramidCache cache, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cache);

        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(_directory);
        var finalPath = GetPath(cache.SongId);
        var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";

        try
        {
            var json = JsonSerializer.Serialize(cache);
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

    public async Task<WaveformPyramidCache> LoadAsync(string path, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No waveform pyramid cache exists at '{path}'.", path);
        }

        await using var stream = File.OpenRead(path);
        try
        {
            var cache = await JsonSerializer.DeserializeAsync<WaveformPyramidCache>(stream, cancellationToken: ct).ConfigureAwait(false);
            return cache ?? throw new WaveformPyramidCorruptException(path, new InvalidOperationException("Deserialized to null."));
        }
        catch (JsonException ex)
        {
            throw new WaveformPyramidCorruptException(path, ex);
        }
    }
}
