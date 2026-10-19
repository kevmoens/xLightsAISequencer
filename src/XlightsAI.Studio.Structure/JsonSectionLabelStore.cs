using System.Text.Json;
using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Structure;

/// <summary>
/// JSON-file-backed <see cref="ISectionLabelStore"/>. Writes are atomic (same-directory temp file +
/// <see cref="File.Move(string, string, bool)"/> replace) -- the same pattern
/// <see cref="JsonSectionBoundaryStore"/> already uses.
/// </summary>
public sealed class JsonSectionLabelStore : ISectionLabelStore
{
    private readonly string _directory;

    public JsonSectionLabelStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string GetPath(SongId songId) =>
        Path.Combine(_directory, $"section-labels-{songId.Value:N}.json");

    public async Task<string> SaveAsync(SectionLabelAnalysis analysis, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(_directory);
        var finalPath = GetPath(analysis.SongId);
        var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";

        try
        {
            var json = JsonSerializer.Serialize(analysis);
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

    public async Task<SectionLabelAnalysis> LoadAsync(string path, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No section label analysis cache exists at '{path}'.", path);
        }

        await using var stream = File.OpenRead(path);
        try
        {
            var analysis = await JsonSerializer.DeserializeAsync<SectionLabelAnalysis>(stream, cancellationToken: ct).ConfigureAwait(false);
            return analysis ?? throw new SectionLabelCorruptException(path, new InvalidOperationException("Deserialized to null."));
        }
        catch (JsonException ex)
        {
            throw new SectionLabelCorruptException(path, ex);
        }
    }
}
