using System.Text.Json;
using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Structure;

/// <summary>
/// JSON-file-backed <see cref="IPhraseBoundaryOverrideStore"/>. Writes are atomic (same-directory temp
/// file + <see cref="File.Move(string, string, bool)"/> replace) -- the same pattern every other store in
/// this project already uses. A missing file is a valid, empty override set (no manual corrections
/// recorded yet), never an error -- distinct from a present-but-corrupt file, which is.
/// </summary>
public sealed class JsonPhraseBoundaryOverrideStore : IPhraseBoundaryOverrideStore
{
    private readonly string _directory;

    public JsonPhraseBoundaryOverrideStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string GetPath(SongId songId) =>
        Path.Combine(_directory, $"phrase-boundary-overrides-{songId.Value:N}.json");

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<int>>> LoadAsync(SongId songId, CancellationToken ct)
    {
        var path = GetPath(songId);

        if (!File.Exists(path))
        {
            return new Dictionary<Guid, IReadOnlyList<int>>();
        }

        await using var stream = File.OpenRead(path);
        try
        {
            var overrides = await JsonSerializer.DeserializeAsync<Dictionary<Guid, List<int>>>(stream, cancellationToken: ct).ConfigureAwait(false);
            if (overrides is null)
            {
                throw new PhraseSegmentationCorruptException(path, new InvalidOperationException("Deserialized to null."));
            }

            return overrides.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<int>)kv.Value);
        }
        catch (JsonException ex)
        {
            throw new PhraseSegmentationCorruptException(path, ex);
        }
    }

    public async Task SaveAsync(SongId songId, IReadOnlyDictionary<Guid, IReadOnlyList<int>> overrides, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(overrides);

        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(_directory);
        var finalPath = GetPath(songId);
        var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";

        try
        {
            var json = JsonSerializer.Serialize(overrides.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()));
            await File.WriteAllTextAsync(tempPath, json, ct).ConfigureAwait(false);

            ct.ThrowIfCancellationRequested();

            File.Move(tempPath, finalPath, overwrite: true);
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
}
