namespace XlightsAI.Studio.Distribution;

/// <summary>
/// PHASE-15 Section 3's own proposed signature, kept exactly as specified. Ensures a declared, optional
/// <see cref="DownloadableAsset"/> exists locally with bytes matching its pinned <see cref="DownloadableAsset.Sha256"/>,
/// downloading it only if no already-verified local copy exists -- the same "offline pre-provisioned asset /
/// already-verified copy needs zero network access" convention <c>XlightsAI.Studio.Stems.IModelAssetManager</c>
/// (PHASE-06 Step 039) already established, applied here to a release/model asset declared in a
/// <see cref="ReleaseManifest"/> instead. Step 094 Section 6 item 2 deliberately deferred this interface's
/// implementation to this step. Realizes this step's own title, verbatim: "Add opt-in model asset installs,
/// offline validation, logs and update notices."
/// </summary>
public interface IAssetInstaller
{
    /// <summary>
    /// Returns the local file path of the verified asset. Never throws for "missing or incompatible model,
    /// cancellation and offline mode" (this step's own required failure/edge case, verbatim) by silently
    /// corrupting state -- a failed attempt leaves no partial file behind and the prior verified copy (if
    /// any) is untouched, so the caller can safely retry or continue in a degraded/offline mode. Consent to
    /// download at all is the caller's responsibility (Section 1/Core process flowchart: "Explain model
    /// size/license and ask consent" happens before this is ever invoked for a not-yet-installed asset).
    /// </summary>
    Task<string> EnsureInstalledAsync(DownloadableAsset asset, IProgress<double>? progress, CancellationToken ct);
}
