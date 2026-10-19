namespace XlightsAI.Studio.Distribution;

/// <summary>
/// Thrown by <see cref="AssetInstaller.EnsureInstalledAsync"/> when a freshly downloaded (or a stale,
/// previously-cached) asset's actual bytes do not hash to its declared <see cref="DownloadableAsset.Sha256"/>
/// -- phase test matrix row 3, "Model download hash tampering rejected." Never leaves the tampered bytes at
/// the installer's verified path; the caller receives a clear, named, recoverable error instead of a crash or
/// a silently-trusted file (this step's own required failure/edge case, verbatim).
/// </summary>
public sealed class AssetInstallationHashMismatchException : Exception
{
    public string AssetId { get; }

    public string ExpectedSha256 { get; }

    public string ActualSha256 { get; }

    public AssetInstallationHashMismatchException(string assetId, string expectedSha256, string actualSha256)
        : base(
            $"Asset '{assetId}' failed integrity verification: expected sha256 {expectedSha256} but found " +
            $"{actualSha256}. Install aborted; no partial or tampered file was kept.")
    {
        AssetId = assetId;
        ExpectedSha256 = expectedSha256;
        ActualSha256 = actualSha256;
    }
}
