namespace XlightsAI.Studio.Stems;

/// <summary>
/// Thrown by <see cref="ModelAssetManager.EnsureVerifiedAsync"/> when no already-verified local copy of
/// the requested <see cref="ModelAsset"/> exists (no offline pre-provisioned asset) and the caller did
/// not pass <c>userConsent: true</c>. Section 3: "User consents to download" — a download is never
/// started implicitly. This is a recoverable state: the caller may re-invoke with consent later, and no
/// partial/cache state is created or mutated by this exception.
/// </summary>
public sealed class ModelAssetConsentRequiredException : Exception
{
    public ModelAssetConsentRequiredException(string modelAssetId)
        : base($"Model asset '{modelAssetId}' is not already verified locally and the user has not consented to download it.")
    {
    }
}
