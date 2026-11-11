using System.Security.Cryptography;

namespace XlightsAI.Studio.Stems;

/// <summary>
/// Real <see cref="IModelAssetManager"/>. Implements Step 039's own required acceptance verbatim:
/// "Tampered models rejected and clean offline restart supported."
///
/// Flow: (1) validate the declared asset shape; (2) if a file already sits at the content-addressed
/// verified path and its bytes actually hash to the declared <see cref="ModelAsset.Sha256"/>, return it
/// immediately without any network access at all -- this is both the idempotent "already downloaded"
/// case and the "offline pre-provisioned asset" case (Section 1's phase-gate demo: "install model through
/// app (or use offline pre-provisioned asset)"); (3) otherwise, require <c>userConsent</c> before
/// downloading at all; (4) download to a same-directory temp file, verify its hash, and only then
/// atomically promote it to the verified path -- a tampered or interrupted download never reaches the
/// verified path and leaves no partial file behind.
/// </summary>
public sealed class ModelAssetManager : IModelAssetManager
{
    private readonly string _cacheDirectory;
    private readonly IModelAssetDownloader _downloader;
    private readonly IReadOnlyList<string> _bundledSearchDirectories;

    /// <param name="bundledSearchDirectories">
    /// User-requested real feature (2026-11-08): "all models should ship with our app" -- additional
    /// directories (e.g. a folder installed alongside the app executable) checked, in order, for an
    /// already-verified copy of an asset BEFORE <paramref name="cacheDirectory"/> is consulted at all.
    /// This is exactly this type's own pre-existing "offline pre-provisioned asset" design goal (see this
    /// class's own doc comment), generalized from "pre-provisioned inside the cache directory itself" to
    /// "pre-provisioned anywhere the app ships a copy" -- a hit here never touches the network and never
    /// requires <c>userConsent</c>, the same as a hit in <paramref name="cacheDirectory"/>. Defaults to
    /// empty so every pre-existing caller/test keeps compiling and behaving identically.
    /// </param>
    public ModelAssetManager(string cacheDirectory, IModelAssetDownloader downloader, IReadOnlyList<string>? bundledSearchDirectories = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheDirectory);
        ArgumentNullException.ThrowIfNull(downloader);
        _cacheDirectory = cacheDirectory;
        _downloader = downloader;
        _bundledSearchDirectories = bundledSearchDirectories ?? [];
    }

    /// <summary>
    /// Deterministic, content-addressed local path for a declared asset: identity + version + a hash
    /// fragment, so two different pinned versions (or a corrected hash for the same version) never
    /// collide on disk.
    /// </summary>
    public string GetVerifiedPath(ModelAsset asset) => GetVerifiedPath(asset, _cacheDirectory);

    private static string GetVerifiedPath(ModelAsset asset, string directory)
    {
        ModelAssetValidator.Validate(asset);

        var safeId = new string(asset.Id.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        var safeVersion = new string(asset.Version.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        return Path.Combine(directory, $"{safeId}-{safeVersion}-{asset.Sha256[..12]}.modelasset");
    }

    public async Task<string> EnsureVerifiedAsync(ModelAsset asset, bool userConsent, CancellationToken ct)
    {
        ModelAssetValidator.Validate(asset);
        ct.ThrowIfCancellationRequested();

        foreach (var bundledDirectory in _bundledSearchDirectories)
        {
            var bundledPath = GetVerifiedPath(asset, bundledDirectory);
            if (!File.Exists(bundledPath))
            {
                continue;
            }

            var bundledSha256 = await ComputeSha256Async(bundledPath, ct).ConfigureAwait(false);
            if (string.Equals(bundledSha256, asset.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                return bundledPath;
            }

            // A bundled file that fails to hash-verify is never trusted (same "tampered models rejected"
            // discipline as the cache-directory check below) -- just not treated as a hit; fall through to
            // the next bundled directory, then to the cache directory/download path.
        }

        Directory.CreateDirectory(_cacheDirectory);
        var finalPath = GetVerifiedPath(asset);

        if (File.Exists(finalPath))
        {
            var existingSha256 = await ComputeSha256Async(finalPath, ct).ConfigureAwait(false);
            if (string.Equals(existingSha256, asset.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                return finalPath;
            }

            // Stale/corrupt leftover from a prior run; never silently trusted -- fall through to
            // (re)download, gated by the same consent check as any other missing asset.
            File.Delete(finalPath);
        }

        if (!userConsent)
        {
            throw new ModelAssetConsentRequiredException(asset.Id);
        }

        var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";
        try
        {
            await _downloader.DownloadAsync(asset.Source, tempPath, ct).ConfigureAwait(false);

            ct.ThrowIfCancellationRequested();

            var actualSha256 = await ComputeSha256Async(tempPath, ct).ConfigureAwait(false);
            if (!string.Equals(actualSha256, asset.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new ModelAssetHashMismatchException(asset.Id, asset.Sha256, actualSha256);
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

    private static async Task<string> ComputeSha256Async(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
