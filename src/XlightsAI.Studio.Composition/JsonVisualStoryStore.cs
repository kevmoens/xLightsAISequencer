using System.Text.Json;

namespace XlightsAI.Studio.Composition;

/// <summary>
/// JSON-file-backed <see cref="IVisualStoryStore"/>. Writes are atomic (same-directory temp file +
/// <see cref="File.Move(string, string, bool)"/> replace), the exact same pattern
/// <see cref="JsonVisualBudgetPresetStore"/> already uses.
/// </summary>
public sealed class JsonVisualStoryStore : IVisualStoryStore
{
    private const string Prefix = "visual-story-";
    private const string Suffix = ".json";

    private readonly string _directory;

    public JsonVisualStoryStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string GetPath(Guid songId) => Path.Combine(_directory, $"{Prefix}{songId:N}{Suffix}");

    public async Task<string> SaveAsync(VisualStoryRecord record, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(record);

        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(_directory);
        var finalPath = GetPath(record.SongId);
        var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";

        try
        {
            var json = JsonSerializer.Serialize(record);
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

    public async Task<VisualStoryRecord> LoadAsync(string path, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No visual story exists at '{path}'.", path);
        }

        await using var stream = File.OpenRead(path);
        try
        {
            var record = await JsonSerializer.DeserializeAsync<VisualStoryRecord>(stream, cancellationToken: ct).ConfigureAwait(false);
            return record ?? throw new CompositionCorruptException(path, new InvalidOperationException("Deserialized to null."));
        }
        catch (JsonException ex)
        {
            throw new CompositionCorruptException(path, ex);
        }
    }
}
