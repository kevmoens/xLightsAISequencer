using System.Text.Json;

namespace XlightsAI.Studio.Knowledge;

/// <summary>
/// JSON-file-backed persistence for a single <see cref="CorpusManifest"/>. Writes are atomic
/// (same-directory temp file + <see cref="File.Move(string, string, bool)"/> replace) — the same
/// pattern <c>XlightsAI.Studio.Rhythm.JsonBeatGridStore</c> already uses. Unlike the per-song
/// Rhythm/Structure stores, there is exactly one manifest per corpus root, so this is keyed by a fixed
/// file name rather than a <c>SongId</c>.
/// </summary>
public sealed class JsonCorpusManifestStore
{
    private const string FileName = "corpus-manifest.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _directory;

    public JsonCorpusManifestStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string Path => System.IO.Path.Combine(_directory, FileName);

    public async Task<string> SaveAsync(CorpusManifest manifest, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(_directory);
        var finalPath = Path;
        var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";

        try
        {
            var json = JsonSerializer.Serialize(manifest, JsonOptions);
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

    public async Task<CorpusManifest> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(Path))
        {
            throw new FileNotFoundException($"No corpus manifest cache exists at '{Path}'.", Path);
        }

        await using var stream = File.OpenRead(Path);
        var manifest = await JsonSerializer.DeserializeAsync<CorpusManifest>(stream, cancellationToken: ct).ConfigureAwait(false);
        return manifest ?? throw new InvalidOperationException($"Corpus manifest at '{Path}' deserialized to null.");
    }
}
