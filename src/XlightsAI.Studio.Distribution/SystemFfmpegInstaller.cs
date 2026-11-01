namespace XlightsAI.Studio.Distribution;

/// <summary>
/// <see cref="IFfmpegInstaller"/> for platforms without a pinned ffmpeg download (macOS/Linux): resolves an
/// ffmpeg the user already installed (for example via Homebrew) from well-known locations and <c>PATH</c>.
/// GUI-launched macOS apps inherit a minimal <c>PATH</c>, so the Homebrew/MacPorts directories are probed
/// explicitly. Never downloads anything; when ffmpeg is missing it fails with install instructions.
/// </summary>
public sealed class SystemFfmpegInstaller : IFfmpegInstaller
{
    private static readonly string[] WellKnownDirectories =
    [
        "/opt/homebrew/bin",
        "/usr/local/bin",
        "/opt/local/bin",
        "/usr/bin",
    ];

    private readonly IReadOnlyList<string> _searchDirectories;

    public SystemFfmpegInstaller(IEnumerable<string>? searchDirectories = null)
    {
        _searchDirectories = (searchDirectories ?? DefaultSearchDirectories()).ToList();
    }

    public string? TryGetInstalledExecutablePath()
    {
        var fileName = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
        return _searchDirectories
            .Select(directory => Path.Combine(directory, fileName))
            .FirstOrDefault(File.Exists);
    }

    public Task<string> EnsureInstalledAsync(IProgress<double>? progress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var path = TryGetInstalledExecutablePath();
        if (path is null)
        {
            throw new AssetInstallationFailedException(
                "ffmpeg",
                "was not found on this machine; install it yourself (for example `brew install ffmpeg`) and try again",
                innerException: null);
        }

        progress?.Report(1.0);
        return Task.FromResult(path);
    }

    private static IEnumerable<string> DefaultSearchDirectories()
    {
        var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        return pathVariable
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Concat(WellKnownDirectories)
            .Distinct(StringComparer.Ordinal);
    }
}
