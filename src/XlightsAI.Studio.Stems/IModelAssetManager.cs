namespace XlightsAI.Studio.Stems;

/// <summary>
/// PHASE-06 Section 3's own proposed signature. Ensures a declared <see cref="ModelAsset"/> exists
/// locally with bytes matching its pinned <see cref="ModelAsset.Sha256"/>, downloading it (with explicit
/// consent) only if no already-verified local copy exists. Returns the local file path of the verified
/// asset.
/// </summary>
public interface IModelAssetManager
{
    Task<string> EnsureVerifiedAsync(ModelAsset asset, bool userConsent, CancellationToken ct);
}
