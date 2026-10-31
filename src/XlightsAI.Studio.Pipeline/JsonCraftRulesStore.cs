using System.Text.Json;

namespace XlightsAI.Studio.Pipeline;

/// <summary>JSON-file-backed persistence for a single <see cref="CraftRules"/> result, one per corpus root. Atomic writes — the same temp-file-then-<see cref="File.Move(string, string, bool)"/> pattern as <c>XlightsAI.Studio.Rhythm.JsonBeatGridStore</c>.</summary>
public sealed class JsonCraftRulesStore
{
    private const string FileName = "craft-rules.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _directory;

    public JsonCraftRulesStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string Path => System.IO.Path.Combine(_directory, FileName);

    public async Task<string> SaveAsync(CraftRules rules, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(rules);
        Directory.CreateDirectory(_directory);
        var finalPath = Path;
        var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";
        try
        {
            await File.WriteAllTextAsync(tempPath, JsonSerializer.Serialize(rules, JsonOptions), ct).ConfigureAwait(false);
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

    public async Task<CraftRules> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(Path))
        {
            throw new FileNotFoundException($"No craft rules exist at '{Path}'.", Path);
        }

        await using var stream = File.OpenRead(Path);
        var rules = await JsonSerializer.DeserializeAsync<CraftRules>(stream, cancellationToken: ct).ConfigureAwait(false);
        return rules ?? throw new InvalidOperationException($"Craft rules at '{Path}' deserialized to null.");
    }
}
