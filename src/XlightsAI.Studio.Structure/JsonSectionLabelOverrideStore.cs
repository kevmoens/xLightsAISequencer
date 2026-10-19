using System.Text.Json;
using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Structure;

/// <summary>
/// JSON-file-backed <see cref="ISectionLabelOverrideStore"/>. Writes are atomic (same-directory temp file
/// + <see cref="File.Move(string, string, bool)"/> replace) -- the same pattern every other store in this
/// project already uses. A missing file is a valid, empty override set (no overrides recorded yet), never
/// an error -- distinct from a present-but-corrupt file, which is.
/// </summary>
public sealed class JsonSectionLabelOverrideStore : ISectionLabelOverrideStore
{
    private readonly string _directory;

    public JsonSectionLabelOverrideStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string GetPath(SongId songId) =>
        Path.Combine(_directory, $"section-label-overrides-{songId.Value:N}.json");

    public async Task<IReadOnlyDictionary<Guid, SectionKind>> LoadAsync(SongId songId, CancellationToken ct)
    {
        var path = GetPath(songId);

        if (!File.Exists(path))
        {
            return new Dictionary<Guid, SectionKind>();
        }

        await using var stream = File.OpenRead(path);
        try
        {
            var overrides = await JsonSerializer.DeserializeAsync<Dictionary<Guid, SectionKind>>(stream, cancellationToken: ct).ConfigureAwait(false);
            return overrides ?? throw new SectionLabelCorruptException(path, new InvalidOperationException("Deserialized to null."));
        }
        catch (JsonException ex)
        {
            throw new SectionLabelCorruptException(path, ex);
        }
    }

    public async Task SaveAsync(SongId songId, IReadOnlyDictionary<Guid, SectionKind> overrides, CancellationToken ct)
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
