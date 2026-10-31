using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Distribution;

namespace XlightsAI.Studio.Pipeline;

/// <summary>
/// PHASE-2b corpus mining only: a minimal <see cref="IFfmpegInstaller"/> that resolves whatever ffmpeg
/// is already on the developer machine's <c>PATH</c> (or a path supplied directly), rather than the
/// production <see cref="FfmpegInstaller"/>'s pinned-version download/extract flow — corpus mining runs
/// on the developer's own machine against audio that already needs transcoding (mp3/m4a), and requiring
/// a fresh network download of a specific pinned ffmpeg build for a one-off local analysis run would be
/// real friction for no real benefit. Never downloads anything itself.
/// </summary>
public sealed class LocalPathFfmpegInstaller : IFfmpegInstaller
{
    private readonly string? _explicitPath;

    public LocalPathFfmpegInstaller(string? explicitPath = null)
    {
        _explicitPath = explicitPath;
    }

    public string? TryGetInstalledExecutablePath()
    {
        if (_explicitPath is not null && File.Exists(_explicitPath))
        {
            return _explicitPath;
        }

        var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var exeName = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";

        foreach (var directory in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory, exeName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public Task<string> EnsureInstalledAsync(IProgress<double>? progress, CancellationToken ct) =>
        TryGetInstalledExecutablePath() is { } path
            ? Task.FromResult(path)
            : throw new FfmpegNotAvailableException("(PATH lookup)");
}
