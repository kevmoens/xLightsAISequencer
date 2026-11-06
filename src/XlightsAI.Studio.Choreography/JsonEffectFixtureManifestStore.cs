using System.Text.Json;
using System.Text.Json.Serialization;

namespace XlightsAI.Studio.Choreography;

/// <summary>
/// JSON-file-backed persistence for a single <see cref="EffectFixtureManifest"/> -- Phase 18 §2's own
/// "persisted somewhere reviewable (e.g. a manifest file)" requirement. Same single-current-file, atomic
/// temp-then-<see cref="File.Move(string, string, bool)"/> convention as <c>XlightsAI.Studio.Pipeline.JsonExemplarIndexStore</c>,
/// chosen (over a per-run versioned file, e.g. <c>JsonShowBibleStore</c>'s convention) because re-running
/// Phase A's enumeration is expected to simply replace the prior manifest, not accumulate a version history --
/// only the latest enumeration is ever meaningful input to Phase B's render step. Indented JSON (unlike
/// <see cref="JsonExemplarIndexStore"/>'s compact form) so a human can open and read the file directly, per
/// this phase's own "reviewable" requirement.
/// </summary>
public sealed class JsonEffectFixtureManifestStore
{
    private const string FileName = "effect-fixture-manifest.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _directory;

    public JsonEffectFixtureManifestStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string Path => System.IO.Path.Combine(_directory, FileName);

    public async Task<string> SaveAsync(EffectFixtureManifest manifest, CancellationToken ct)
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

    public async Task<EffectFixtureManifest> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(Path))
        {
            throw new FileNotFoundException($"No effect fixture manifest exists at '{Path}'.", Path);
        }

        await using var stream = File.OpenRead(Path);
        var manifest = await JsonSerializer.DeserializeAsync<EffectFixtureManifest>(stream, JsonOptions, ct).ConfigureAwait(false);
        return manifest ?? throw new InvalidOperationException($"Effect fixture manifest at '{Path}' deserialized to null.");
    }
}
