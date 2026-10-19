namespace XlightsAI.Studio.Distribution;

/// <summary>
/// Thrown by <see cref="AssetInstaller.EnsureInstalledAsync"/> when a not-yet-installed asset's download
/// could not complete for a transport-level reason (offline, DNS failure, connection refused, truncated
/// response) -- this step's own required failure/edge case, verbatim: "...and offline mode must yield a
/// recoverable state." Wraps the real underlying transport exception as <see cref="Exception.InnerException"/>
/// so a caller gets one clear, human-readable message (Section 1: "missing models explained") while the
/// original diagnostic detail is still available for logging, rather than a raw <see cref="HttpRequestException"/>
/// or <see cref="IOException"/> surfacing uncaught.
/// </summary>
public sealed class AssetInstallationFailedException : Exception
{
    public string AssetId { get; }

    public AssetInstallationFailedException(string assetId, string reason, Exception? innerException)
        : base($"Asset '{assetId}' {reason}.", innerException)
    {
        AssetId = assetId;
    }
}
