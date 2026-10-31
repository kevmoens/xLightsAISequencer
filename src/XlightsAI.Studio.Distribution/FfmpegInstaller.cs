using System.IO.Compression;

namespace XlightsAI.Studio.Distribution;

/// <summary>
/// Real <see cref="IFfmpegInstaller"/>: delegates the download/verify step entirely to
/// <see cref="IAssetInstaller"/> (so it inherits that type's offline-reuse, hash-verification and
/// atomic-write guarantees unchanged), then extracts the verified archive's files into a
/// version-specific directory so a later <see cref="TryGetInstalledExecutablePath"/> needs no network
/// access. Same zip-slip guard and temp-then-atomic-move extraction idiom as
/// <c>XlightsAI.Studio.Stems.TarBzip2ModelArchiveExpander</c>, using the BCL's own
/// <see cref="ZipArchive"/> rather than a third-party archive library since ffmpeg's Windows builds are
/// plain, standard zip archives.
/// </summary>
public sealed class FfmpegInstaller : IFfmpegInstaller
{
    private const string ExecutableFileName = "ffmpeg.exe";

    private readonly string _extractionRootDirectory;
    private readonly IAssetInstaller _assetInstaller;
    private readonly DownloadableAsset _asset;

    public FfmpegInstaller(string extractionRootDirectory, IAssetInstaller assetInstaller, DownloadableAsset? asset = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extractionRootDirectory);
        ArgumentNullException.ThrowIfNull(assetInstaller);
        _extractionRootDirectory = extractionRootDirectory;
        _assetInstaller = assetInstaller;
        _asset = asset ?? FfmpegAsset.Current;
    }

    /// <summary>One directory per pinned version, so upgrading <see cref="FfmpegAsset.Current"/>'s version never mixes an old and new extraction together.</summary>
    private string VersionDirectory => Path.Combine(_extractionRootDirectory, Sanitize(_asset.Version));

    public string? TryGetInstalledExecutablePath()
    {
        if (!Directory.Exists(VersionDirectory))
        {
            return null;
        }

        // The archive's own top-level folder name embeds a build-specific commit hash that is not
        // worth hard-coding and re-verifying on every ffmpeg release bump, so the executable is
        // located by name within the version directory instead of by an assumed fixed relative path.
        return Directory.EnumerateFiles(VersionDirectory, ExecutableFileName, SearchOption.AllDirectories).FirstOrDefault();
    }

    public async Task<string> EnsureInstalledAsync(IProgress<double>? progress, CancellationToken ct)
    {
        var existing = TryGetInstalledExecutablePath();
        if (existing is not null)
        {
            // Already installed -- the offline/idempotent case. No network access.
            progress?.Report(1.0);
            return existing;
        }

        var archivePath = await _assetInstaller.EnsureInstalledAsync(_asset, progress, ct).ConfigureAwait(false);

        Directory.CreateDirectory(VersionDirectory);
        await ExtractAsync(archivePath, VersionDirectory, ct).ConfigureAwait(false);

        var exePath = TryGetInstalledExecutablePath();
        if (exePath is null)
        {
            throw new InvalidOperationException(
                $"The downloaded ffmpeg archive '{_asset.Id}' (version {_asset.Version}) did not contain a '{ExecutableFileName}' entry.");
        }

        return exePath;
    }

    private static async Task ExtractAsync(string archivePath, string destinationDirectory, CancellationToken ct)
    {
        var fullDestinationRoot = Path.GetFullPath(destinationDirectory);
        if (!fullDestinationRoot.EndsWith(Path.DirectorySeparatorChar))
        {
            fullDestinationRoot += Path.DirectorySeparatorChar;
        }

        using var archive = ZipFile.OpenRead(archivePath);
        foreach (var entry in archive.Entries)
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrEmpty(entry.Name))
            {
                // Directory entry (its Name is empty; FullName ends with '/').
                continue;
            }

            var relativePath = entry.FullName.Replace('\\', '/');
            var finalPath = Path.GetFullPath(Path.Combine(fullDestinationRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!finalPath.StartsWith(fullDestinationRoot, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"ffmpeg archive entry '{relativePath}' would extract outside the destination directory (zip-slip guard).");
            }

            var finalDirectory = Path.GetDirectoryName(finalPath)!;
            Directory.CreateDirectory(finalDirectory);

            var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";
            try
            {
                await using (var entryStream = entry.Open())
                await using (var outStream = new FileStream(
                    tempPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true))
                {
                    await entryStream.CopyToAsync(outStream, ct).ConfigureAwait(false);
                }

                ct.ThrowIfCancellationRequested();
                File.Move(tempPath, finalPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }
    }

    private static string Sanitize(string value) =>
        new(value.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
}
