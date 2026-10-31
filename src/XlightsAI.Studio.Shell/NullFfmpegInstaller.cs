using XlightsAI.Studio.Distribution;

namespace XlightsAI.Studio.Shell;

/// <summary>
/// Throwing default <see cref="IFfmpegInstaller"/>, same "optional collaborator that needs real external
/// configuration this layer does not yet own" reasoning as <see cref="NullAssetInstaller"/> — a real
/// <see cref="XlightsAI.Studio.Distribution.FfmpegInstaller"/> needs a concrete extraction directory only
/// a real published composition root can supply. Downloading is an explicit, consent-gated user action
/// (the Audio screen's "Download ffmpeg" button), never invoked implicitly, so a caller that invokes it
/// without a real collaborator configured gets a clear, disclosed failure rather than a silently-failing
/// no-op.
/// </summary>
public sealed class NullFfmpegInstaller : IFfmpegInstaller
{
    public static readonly NullFfmpegInstaller Instance = new();

    private NullFfmpegInstaller()
    {
    }

    public string? TryGetInstalledExecutablePath() => null;

    public Task<string> EnsureInstalledAsync(IProgress<double>? progress, CancellationToken ct)
    {
        throw new InvalidOperationException(
            "No real ffmpeg installer is configured for this composition root yet (no install directory to download/extract ffmpeg into).");
    }
}
