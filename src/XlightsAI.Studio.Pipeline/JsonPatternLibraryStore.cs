using System.Text.Json;

namespace XlightsAI.Studio.Pipeline;

/// <summary>JSON-file-backed persistence for a single <see cref="PatternLibrary"/>, one per corpus root. Atomic writes — the same temp-file-then-<see cref="File.Move(string, string, bool)"/> pattern as <c>XlightsAI.Studio.Rhythm.JsonBeatGridStore</c>.</summary>
public sealed class JsonPatternLibraryStore
{
    private const string FileName = "pattern-library.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _directory;

    public JsonPatternLibraryStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string Path => System.IO.Path.Combine(_directory, FileName);

    public async Task<string> SaveAsync(PatternLibrary library, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(library);
        Directory.CreateDirectory(_directory);
        var finalPath = Path;
        var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";
        try
        {
            await File.WriteAllTextAsync(tempPath, JsonSerializer.Serialize(library, JsonOptions), ct).ConfigureAwait(false);
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

    public async Task<PatternLibrary> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(Path))
        {
            throw new FileNotFoundException($"No pattern library exists at '{Path}'.", Path);
        }

        await using var stream = File.OpenRead(Path);
        var library = await JsonSerializer.DeserializeAsync<PatternLibrary>(stream, cancellationToken: ct).ConfigureAwait(false);
        return library ?? throw new InvalidOperationException($"Pattern library at '{Path}' deserialized to null.");
    }
}
