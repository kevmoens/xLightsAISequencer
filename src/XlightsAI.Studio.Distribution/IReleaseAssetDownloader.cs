namespace XlightsAI.Studio.Distribution;

/// <summary>
/// Transfers the bytes of a declared <see cref="DownloadableAsset.Url"/> to a local file, reporting
/// fractional progress when the server declares a content length -- same "deliberately scheme-agnostic at
/// this interface level, so it can be exercised directly against a real local loopback server in tests"
/// convention as <c>XlightsAI.Studio.Stems.IModelAssetDownloader</c> (PHASE-06 Step 039), plus progress
/// reporting for this step's own <see cref="IAssetInstaller.EnsureInstalledAsync"/> signature.
/// </summary>
public interface IReleaseAssetDownloader
{
    Task DownloadAsync(Uri source, string destinationPath, IProgress<double>? progress, CancellationToken ct);
}
