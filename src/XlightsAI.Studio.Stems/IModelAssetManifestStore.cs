namespace XlightsAI.Studio.Stems;

/// <summary>
/// Persists/reloads a <see cref="ModelAssetManifest"/> as a versioned, inspectable project output --
/// Step 039's own required user-visible outcome, verbatim: "Versioned, inspectable project output plus a
/// reproducible test report."
/// </summary>
public interface IModelAssetManifestStore
{
    string GetPath(Guid manifestId);

    Task<string> SaveAsync(ModelAssetManifest manifest, CancellationToken ct);

    Task<ModelAssetManifest> LoadAsync(string path, CancellationToken ct);
}
