using System.Net.Sockets;
using System.Security.Cryptography;

namespace XlightsAI.Studio.Distribution;

/// <summary>
/// Real <see cref="IAssetInstaller"/>. Flow (same "offline-first, consent-is-the-caller's-job,
/// never-trust-a-partial-write" shape as <c>XlightsAI.Studio.Stems.ModelAssetManager</c>, PHASE-06 Step 039,
/// applied here to a <see cref="DownloadableAsset"/> declared in a <see cref="ReleaseManifest"/>): (1) if a
/// file already sits at the content-addressed installed path and its bytes actually hash to the declared
/// <see cref="DownloadableAsset.Sha256"/>, return it immediately with zero network access at all -- this is
/// both the idempotent "already installed" case and the "offline pre-provisioned asset" case (Core process
/// flowchart: "atomically install verified model" only ever runs once per distinct asset version); (2)
/// otherwise, download to a same-directory temp file, verify its hash, and only then atomically promote it to
/// the installed path -- a tampered or interrupted download never reaches the installed path and leaves no
/// partial file behind, realizing "offline validation" (this step's own title) and this step's own required
/// failure/edge case, verbatim: "Missing or incompatible model, cancellation and offline mode must yield a
/// recoverable state."
/// </summary>
public sealed class AssetInstaller : IAssetInstaller
{
    private readonly string _installDirectory;
    private readonly IReleaseAssetDownloader _downloader;

    public AssetInstaller(string installDirectory, IReleaseAssetDownloader downloader)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installDirectory);
        ArgumentNullException.ThrowIfNull(downloader);
        _installDirectory = installDirectory;
        _downloader = downloader;
    }

    /// <summary>
    /// Deterministic, content-addressed local path for a declared asset: identity + version + a hash
    /// fragment, so two different pinned versions (or a corrected hash for the same version) never collide
    /// on disk -- same convention as <c>XlightsAI.Studio.Stems.ModelAssetManager.GetVerifiedPath</c>.
    /// </summary>
    public string GetInstalledPath(DownloadableAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentException.ThrowIfNullOrWhiteSpace(asset.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(asset.Version);
        ArgumentException.ThrowIfNullOrWhiteSpace(asset.Sha256);

        var safeId = Sanitize(asset.Id);
        var safeVersion = Sanitize(asset.Version);
        var hashFragment = asset.Sha256[..Math.Min(12, asset.Sha256.Length)];
        return Path.Combine(_installDirectory, $"{safeId}-{safeVersion}-{hashFragment}.asset");
    }

    public async Task<string> EnsureInstalledAsync(DownloadableAsset asset, IProgress<double>? progress, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(_installDirectory);
        var finalPath = GetInstalledPath(asset);

        if (File.Exists(finalPath))
        {
            var existingSha256 = await ComputeSha256Async(finalPath, ct).ConfigureAwait(false);
            if (string.Equals(existingSha256, asset.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                // Already installed and verified -- the offline/pre-provisioned path. No network access.
                progress?.Report(1.0);
                return finalPath;
            }

            // Stale/corrupt leftover from a prior run; never silently trusted -- fall through to
            // (re)download.
            File.Delete(finalPath);
        }

        var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";
        try
        {
            try
            {
                await _downloader.DownloadAsync(asset.Url, tempPath, progress, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or SocketException)
            {
                throw new AssetInstallationFailedException(
                    asset.Id, "could not be downloaded (offline, or the server/network is unavailable)", ex);
            }

            ct.ThrowIfCancellationRequested();

            var actualSha256 = await ComputeSha256Async(tempPath, ct).ConfigureAwait(false);
            if (!string.Equals(actualSha256, asset.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new AssetInstallationHashMismatchException(asset.Id, asset.Sha256, actualSha256);
            }

            File.Move(tempPath, finalPath, overwrite: true);
            return finalPath;
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static string Sanitize(string value) =>
        new(value.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());

    private static async Task<string> ComputeSha256Async(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
