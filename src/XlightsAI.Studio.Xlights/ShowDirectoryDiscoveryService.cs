namespace XlightsAI.Studio.Xlights;

/// <summary>
/// Default <see cref="IShowDirectoryDiscoveryService"/>. Only reads files under the selected show
/// directory (PHASE-02 Step 009 implementation guidance) and resolves every candidate path's real,
/// final target (<see cref="FileSystemInfo.ResolveLinkTarget(bool)"/>) before deciding whether it
/// lies within the selected directory's own real location — a symlink/junction that points a
/// required/optional filename outside the selected directory is treated as not-present rather than
/// silently followed and read.
/// </summary>
public sealed class ShowDirectoryDiscoveryService : IShowDirectoryDiscoveryService
{
    public Task<ShowDirectoryDiscovery> DiscoverAsync(string showDirectory, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(showDirectory);

        ct.ThrowIfCancellationRequested();

        var fullDirectory = Path.GetFullPath(showDirectory);
        if (!Directory.Exists(fullDirectory))
        {
            throw new DirectoryNotFoundException($"Show directory '{fullDirectory}' does not exist.");
        }

        // Realpath boundary (implementation guidance: "Detect realpath/symlink boundary"): resolve
        // the directory itself first, so every file containment check below compares against where
        // the directory actually lives, not an alias/junction path.
        var realDirectory = ResolveRealPath(fullDirectory);

        var warnings = new List<string>();
        var missingRequired = new List<string>();

        var hasRgbEffects = ProbeFile(realDirectory, ShowDirectoryFileNames.RgbEffects, required: true, warnings, missingRequired);
        var hasNetworks = ProbeFile(realDirectory, ShowDirectoryFileNames.Networks, required: false, warnings, missingRequired);

        var result = new ShowDirectoryDiscovery(fullDirectory, hasRgbEffects, hasNetworks, missingRequired, warnings);
        return Task.FromResult(result);
    }

    private static bool ProbeFile(
        string realDirectory,
        string fileName,
        bool required,
        List<string> warnings,
        List<string> missingRequired)
    {
        var candidatePath = Path.Combine(realDirectory, fileName);
        if (!File.Exists(candidatePath))
        {
            if (required)
            {
                missingRequired.Add(fileName);
            }
            else
            {
                warnings.Add($"Optional file '{fileName}' was not found under the selected show directory.");
            }

            return false;
        }

        var realFilePath = ResolveRealPath(candidatePath);
        if (!IsWithinDirectory(realDirectory, realFilePath))
        {
            // The name exists, but its real target escapes the selected directory via a
            // symlink/junction. Never read it; report the escape explicitly instead of silently
            // treating it as present or silently ignoring the discrepancy.
            warnings.Add($"File '{fileName}' resolves outside the selected show directory via a link and was ignored.");
            if (required)
            {
                missingRequired.Add(fileName);
            }

            return false;
        }

        return true;
    }

    /// <summary>True when <paramref name="realFilePath"/> lies at or under <paramref name="realDirectory"/>.</summary>
    internal static bool IsWithinDirectory(string realDirectory, string realFilePath)
    {
        var directoryWithSeparator = realDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? realDirectory
            : realDirectory + Path.DirectorySeparatorChar;

        return realFilePath.StartsWith(directoryWithSeparator, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Resolves <paramref name="path"/> to its real, final location: walks symlink/junction
    /// targets to the end. A path that is not a link resolves to its own full path.
    /// </summary>
    internal static string ResolveRealPath(string path)
    {
        FileSystemInfo info = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
        var finalTarget = info.ResolveLinkTarget(returnFinalTarget: true);
        return Path.GetFullPath(finalTarget?.FullName ?? path);
    }
}
