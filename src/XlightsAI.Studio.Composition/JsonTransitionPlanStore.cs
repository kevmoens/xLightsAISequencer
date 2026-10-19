using System.Text.Json;

namespace XlightsAI.Studio.Composition;

/// <summary>
/// JSON-file-backed <see cref="ITransitionPlanStore"/>. Writes are atomic (same-directory temp file +
/// <see cref="File.Move(string, string, bool)"/> replace), the exact same pattern <see cref="JsonVisualStoryStore"/>
/// already uses.
/// </summary>
public sealed class JsonTransitionPlanStore : ITransitionPlanStore
{
    private const string Prefix = "transition-plan-";
    private const string Suffix = ".json";

    private readonly string _directory;

    public JsonTransitionPlanStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string GetPath(Guid phraseId) => Path.Combine(_directory, $"{Prefix}{phraseId:N}{Suffix}");

    public async Task<string> SaveAsync(TransitionPlanRecord record, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(record);

        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(_directory);
        var finalPath = GetPath(record.PhraseId);
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

    public async Task<TransitionPlanRecord> LoadAsync(string path, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No transition plan exists at '{path}'.", path);
        }

        await using var stream = File.OpenRead(path);
        try
        {
            var record = await JsonSerializer.DeserializeAsync<TransitionPlanRecord>(stream, cancellationToken: ct).ConfigureAwait(false);
            return record ?? throw new CompositionCorruptException(path, new InvalidOperationException("Deserialized to null."));
        }
        catch (JsonException ex)
        {
            throw new CompositionCorruptException(path, ex);
        }
    }
}
