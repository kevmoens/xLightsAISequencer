using System.Text.Json;

namespace XlightsAI.Studio.WholeSong;

/// <summary>
/// JSON-file-backed <see cref="ISongDirectionPlanStore"/>. Writes are atomic (same-directory temp file +
/// <see cref="File.Move(string, string, bool)"/> replace), the exact same pattern
/// <see cref="XlightsAI.Studio.Composition.JsonVisualStoryStore"/> already uses, plus an explicit
/// optimistic-concurrency re-check of the currently-persisted version immediately before the write (this
/// step's own required failure/edge case: a stale persisted state must not corrupt the project).
/// </summary>
public sealed class JsonSongDirectionPlanStore : ISongDirectionPlanStore
{
    private const string Prefix = "song-direction-plan-";
    private const string Suffix = ".json";

    private readonly string _directory;

    public JsonSongDirectionPlanStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string GetPath(Guid songId) => Path.Combine(_directory, $"{Prefix}{songId:N}{Suffix}");

    public async Task<string> SaveAsync(SongDirectionPlanRecord record, int? expectedCurrentVersion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(record);

        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(_directory);
        var finalPath = GetPath(record.SongId.Value);

        var actualCurrentVersion = await TryReadCurrentVersionAsync(finalPath, ct).ConfigureAwait(false);
        if (actualCurrentVersion != expectedCurrentVersion)
        {
            throw new SongDirectionStaleException(expectedCurrentVersion, actualCurrentVersion);
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

    public async Task<SongDirectionPlanRecord> LoadAsync(string path, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No song direction plan exists at '{path}'.", path);
        }

        await using var stream = File.OpenRead(path);
        try
        {
            var record = await JsonSerializer.DeserializeAsync<SongDirectionPlanRecord>(stream, cancellationToken: ct).ConfigureAwait(false);
            return record ?? throw new SongDirectionCorruptException(path, new InvalidOperationException("Deserialized to null."));
        }
        catch (JsonException ex)
        {
            throw new SongDirectionCorruptException(path, ex);
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
