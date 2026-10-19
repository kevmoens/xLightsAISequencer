namespace XlightsAI.Studio.Stems;

/// <summary>
/// Validates a whole <see cref="ModelAssetManifest"/> before it is ever persisted by
/// <see cref="IModelAssetManifestStore"/>: every declared entry must itself be structurally valid
/// (<see cref="ModelAssetValidator"/>), and no two entries may declare the same
/// (<see cref="ModelAsset.Id"/>, <see cref="ModelAsset.Version"/>) pair -- an ambiguous "which hash is
/// the real one for this version" manifest is rejected rather than silently accepting the first/last entry.
/// </summary>
public static class ModelAssetManifestValidator
{
    public static void Validate(ModelAssetManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(manifest.Assets);

        var seen = new HashSet<(string Id, string Version)>();
        foreach (var asset in manifest.Assets)
        {
            try
            {
                ModelAssetValidator.Validate(asset);
            }
            catch (ModelAssetInvalidException ex)
            {
                throw new ModelAssetManifestInvalidException(
                    $"Manifest contains an invalid asset entry: {ex.Message}");
            }

            if (!seen.Add((asset.Id, asset.Version)))
            {
                throw new ModelAssetManifestInvalidException(
                    $"Manifest declares asset '{asset.Id}' version '{asset.Version}' more than once.");
            }
        }
    }
}
