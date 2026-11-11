using System.Text.Json;
using System.Text.Json.Serialization;

namespace XlightsAI.Studio.Director;

/// <summary>
/// JSON-file-backed persistence for a single <see cref="EffectVisualCharacterizationManifest"/> -- Phase 18
/// §5's own sibling artifact to <c>JsonEffectFixtureVideoManifestStore</c>, same single-current-file, atomic
/// temp-then-<see cref="File.Move(string, string, bool)"/>, indented-JSON convention, kept in its own file so
/// no earlier phase's own persisted schema ever needs to change shape to carry Phase D's own outcomes.
/// </summary>
public sealed class JsonEffectVisualCharacterizationManifestStore
{
    private const string FileName = "effect-visual-characterization-manifest.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _directory;

    public JsonEffectVisualCharacterizationManifestStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string Path => System.IO.Path.Combine(_directory, FileName);

    public async Task<string> SaveAsync(EffectVisualCharacterizationManifest manifest, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        Directory.CreateDirectory(_directory);
        var finalPath = Path;
        var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";

        try
        {
            await File.WriteAllTextAsync(tempPath, JsonSerializer.Serialize(manifest, JsonOptions), ct).ConfigureAwait(false);
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

    public async Task<EffectVisualCharacterizationManifest> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(Path))
        {
            throw new FileNotFoundException($"No effect visual characterization manifest exists at '{Path}'.", Path);
        }

        await using var stream = File.OpenRead(Path);
        var manifest = await JsonSerializer.DeserializeAsync<EffectVisualCharacterizationManifest>(stream, JsonOptions, ct).ConfigureAwait(false);
        return manifest ?? throw new InvalidOperationException($"Effect visual characterization manifest at '{Path}' deserialized to null.");
    }
}
