namespace XlightsAI.Studio.Stems;

/// <summary>
/// Thrown by <see cref="IModelAssetManifestStore"/> when a persisted manifest file exists but cannot be
/// parsed back into a valid <see cref="ModelAssetManifest"/> -- mirrors
/// <see cref="ModelFeasibilityReportCorruptException"/>'s own Step 038 precedent for a cache file that is
/// present but unreadable.
/// </summary>
public sealed class ModelAssetManifestCorruptException : Exception
{
    public ModelAssetManifestCorruptException(string path, Exception inner)
        : base($"Model asset manifest at '{path}' exists but could not be parsed.", inner)
    {
    }
}
