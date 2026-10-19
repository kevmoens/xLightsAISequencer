using System.Text.Json;
using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Structure;

/// <summary>
/// JSON-file-backed <see cref="IMotifLinkOverrideStore"/>. Writes are atomic (same-directory temp file +
/// <see cref="File.Move(string, string, bool)"/> replace) -- the same pattern every other store in this
/// project already uses. A missing file is a valid, empty decision set (no manual link/unlink recorded
/// yet), never an error -- distinct from a present-but-corrupt file, which is.
/// </summary>
public sealed class JsonMotifLinkOverrideStore : IMotifLinkOverrideStore
{
    private readonly string _directory;

    public JsonMotifLinkOverrideStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string GetPath(SongId songId) =>
        Path.Combine(_directory, $"motif-link-overrides-{songId.Value:N}.json");

    public async Task<IReadOnlyList<MotifLinkOverride>> LoadAsync(SongId songId, CancellationToken ct)
    {
        var path = GetPath(songId);

        if (!File.Exists(path))
        {
            return [];
        }

        await using var stream = File.OpenRead(path);
        try
        {
            var overrides = await JsonSerializer.DeserializeAsync<List<MotifLinkOverride>>(stream, cancellationToken: ct).ConfigureAwait(false);
            if (overrides is null)
            {
                throw new MotifClusteringCorruptException(path, new InvalidOperationException("Deserialized to null."));
            }

            return overrides;
        }
        catch (JsonException ex)
        {
            throw new MotifClusteringCorruptException(path, ex);
        }
    }

    public async Task SaveAsync(SongId songId, IReadOnlyList<MotifLinkOverride> overrides, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(overrides);

        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(_directory);
        var finalPath = GetPath(songId);
        var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";

        try
        {
            var json = JsonSerializer.Serialize(overrides);
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
