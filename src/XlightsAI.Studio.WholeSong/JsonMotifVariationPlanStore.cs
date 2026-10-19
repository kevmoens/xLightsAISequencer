using System.Text.Json;

namespace XlightsAI.Studio.WholeSong;

/// <summary>
/// JSON-file-backed <see cref="IMotifVariationPlanStore"/>. Writes are atomic (same-directory temp file +
/// <see cref="File.Move(string, string, bool)"/> replace), the exact same pattern
/// <see cref="JsonSongDirectionPlanStore"/> already uses, plus an explicit optimistic-concurrency re-check of
/// the currently-persisted version immediately before the write (this step's own required failure/edge case:
/// a stale persisted state must not corrupt the project).
/// </summary>
public sealed class JsonMotifVariationPlanStore : IMotifVariationPlanStore
{
    private const string Prefix = "motif-variation-plan-";
    private const string Suffix = ".json";

    private readonly string _directory;

    public JsonMotifVariationPlanStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string GetPath(Guid songId) => Path.Combine(_directory, $"{Prefix}{songId:N}{Suffix}");

    public async Task<string> SaveAsync(MotifVariationPlanRecord record, int? expectedCurrentVersion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(record);

        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(_directory);
        var finalPath = GetPath(record.SongId.Value);

        var actualCurrentVersion = await TryReadCurrentVersionAsync(finalPath, ct).ConfigureAwait(false);
        if (actualCurrentVersion != expectedCurrentVersion)
        {
            throw new MotifVariationStaleException(expectedCurrentVersion, actualCurrentVersion);
        }

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

    public async Task<MotifVariationPlanRecord> LoadAsync(string path, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No motif variation plan exists at '{path}'.", path);
        }

        await using var stream = File.OpenRead(path);
        try
        {
            var record = await JsonSerializer.DeserializeAsync<MotifVariationPlanRecord>(stream, cancellationToken: ct).ConfigureAwait(false);
            return record ?? throw new MotifVariationCorruptException(path, new InvalidOperationException("Deserialized to null."));
        }
        catch (JsonException ex)
        {
            throw new MotifVariationCorruptException(path, ex);
        }
    }

    private async Task<int?> TryReadCurrentVersionAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var record = await LoadAsync(path, ct).ConfigureAwait(false);
        return record.Version;
    }
}
