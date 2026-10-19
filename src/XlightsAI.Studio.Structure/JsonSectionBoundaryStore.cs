using System.Text.Json;
using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Structure;

/// <summary>
/// JSON-file-backed <see cref="ISectionBoundaryStore"/>. Writes are atomic (same-directory temp file +
/// <see cref="File.Move(string, string, bool)"/> replace) -- the same pattern
/// <see cref="JsonBarFingerprintStore"/> already uses.
/// </summary>
public sealed class JsonSectionBoundaryStore : ISectionBoundaryStore
{
    private readonly string _directory;

    public JsonSectionBoundaryStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string GetPath(SongId songId) =>
        Path.Combine(_directory, $"section-boundaries-{songId.Value:N}.json");

    public async Task<string> SaveAsync(SectionBoundaryAnalysis analysis, CancellationToken ct)
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

    public async Task<SectionBoundaryAnalysis> LoadAsync(string path, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No section boundary analysis cache exists at '{path}'.", path);
        }

        await using var stream = File.OpenRead(path);
        try
        {
            var analysis = await JsonSerializer.DeserializeAsync<SectionBoundaryAnalysis>(stream, cancellationToken: ct).ConfigureAwait(false);
            return analysis ?? throw new SectionBoundaryCorruptException(path, new InvalidOperationException("Deserialized to null."));
        }
        catch (JsonException ex)
        {
            throw new SectionBoundaryCorruptException(path, ex);
        }
    }
}
