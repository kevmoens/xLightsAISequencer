using System.Text.Json;
using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// JSON-file-backed <see cref="IBeatGridStore"/>. Writes are atomic (same-directory temp file +
/// <see cref="File.Move(string, string, bool)"/> replace) — the same pattern
/// <see cref="JsonTempoAnalysisStore"/> already uses. Each grid is written to its own file named
/// deterministically by its <see cref="BeatGrid.SongId"/>, so re-running
/// <see cref="IBeatGridBuilder.BuildAsync"/> for the same song always resolves to the same path.
/// </summary>
public sealed class JsonBeatGridStore : IBeatGridStore
{
    private readonly string _directory;

    public JsonBeatGridStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string GetPath(SongId songId) =>
        Path.Combine(_directory, $"beat-grid-{songId.Value:N}.json");

    public async Task<string> SaveAsync(BeatGrid grid, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(grid);

        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(_directory);
        var finalPath = GetPath(grid.SongId);
        var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";

        try
        {
            var json = JsonSerializer.Serialize(grid);
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

    public async Task<BeatGrid> LoadAsync(string path, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No beat grid cache exists at '{path}'.", path);
        }

        await using var stream = File.OpenRead(path);
        try
        {
            var grid = await JsonSerializer.DeserializeAsync<BeatGrid>(stream, cancellationToken: ct).ConfigureAwait(false);
            return grid ?? throw new BeatGridCorruptException(path, new InvalidOperationException("Deserialized to null."));
        }
        catch (JsonException ex)
        {
            throw new BeatGridCorruptException(path, ex);
        }
    }
}
