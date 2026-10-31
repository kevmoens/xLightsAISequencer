using System.Text.Json;

namespace XlightsAI.Studio.Pipeline;

/// <summary>JSON-file-backed persistence for a single <see cref="ExemplarIndex"/>, one per corpus root. Atomic writes — the same temp-file-then-<see cref="File.Move(string, string, bool)"/> pattern as <c>XlightsAI.Studio.Rhythm.JsonBeatGridStore</c>.</summary>
public sealed class JsonExemplarIndexStore
{
    private const string FileName = "exemplar-index.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly string _directory;

    public JsonExemplarIndexStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string Path => System.IO.Path.Combine(_directory, FileName);

    public async Task<string> SaveAsync(ExemplarIndex index, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(index);
        Directory.CreateDirectory(_directory);
        var finalPath = Path;
        var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";
        try
        {
            await File.WriteAllTextAsync(tempPath, JsonSerializer.Serialize(index, JsonOptions), ct).ConfigureAwait(false);
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

    public async Task<ExemplarIndex> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(Path))
        {
            throw new FileNotFoundException($"No exemplar index exists at '{Path}'.", Path);
        }

        await using var stream = File.OpenRead(Path);
        var index = await JsonSerializer.DeserializeAsync<ExemplarIndex>(stream, cancellationToken: ct).ConfigureAwait(false);
        return index ?? throw new InvalidOperationException($"Exemplar index at '{Path}' deserialized to null.");
    }
}
