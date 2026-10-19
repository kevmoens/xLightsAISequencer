namespace XlightsAI.Studio.Stems;

/// <summary>
/// Thrown by <see cref="ModelAssetManifestValidator.Validate"/> when a <see cref="ModelAssetManifest"/>
/// contains a structurally invalid entry (see <see cref="ModelAssetValidator"/>) or two entries declaring
/// the same (<see cref="ModelAsset.Id"/>, <see cref="ModelAsset.Version"/>) pair with different pinned
/// hashes -- an ambiguous manifest is never silently persisted.
/// </summary>
public sealed class ModelAssetManifestInvalidException : Exception
{
    public ModelAssetManifestInvalidException(string message) : base(message)
    {
    }
}
