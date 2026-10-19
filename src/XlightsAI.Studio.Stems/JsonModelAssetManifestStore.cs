using System.Text.Json;

namespace XlightsAI.Studio.Stems;

/// <summary>
/// JSON-file-backed <see cref="IModelAssetManifestStore"/>. Writes are atomic (same-directory temp file +
/// <see cref="File.Move(string, string, bool)"/> replace) -- the identical pattern
/// <see cref="JsonModelFeasibilityReportStore"/> already established in this same project for Step 038's
/// own decision artifact, itself mirroring <c>JsonBeatGridStore</c>'s Phase 05 precedent.
/// </summary>
public sealed class JsonModelAssetManifestStore : IModelAssetManifestStore
{
    private readonly string _directory;

    public JsonModelAssetManifestStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string GetPath(Guid manifestId) =>
        Path.Combine(_directory, $"model-asset-manifest-{manifestId:N}.json");

    public async Task<string> SaveAsync(ModelAssetManifest manifest, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ModelAssetManifestValidator.Validate(manifest);

        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(_directory);
        var finalPath = GetPath(manifest.Id);
        var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";

        try
        {
            var json = JsonSerializer.Serialize(manifest);
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

    public async Task<ModelAssetManifest> LoadAsync(string path, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No model asset manifest exists at '{path}'.", path);
        }

        await using var stream = File.OpenRead(path);
        try
        {
            var manifest = await JsonSerializer.DeserializeAsync<ModelAssetManifest>(stream, cancellationToken: ct)
                .ConfigureAwait(false);
            return manifest ?? throw new ModelAssetManifestCorruptException(path, new InvalidOperationException("Deserialized to null."));
        }
        catch (JsonException ex)
        {
            throw new ModelAssetManifestCorruptException(path, ex);
        }
    }
}
