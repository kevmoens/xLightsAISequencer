namespace XlightsAI.Studio.Distribution;

/// <summary>
/// Resolves a local, verified ffmpeg executable, downloading/extracting <see cref="FfmpegAsset.Current"/>
/// only when explicitly asked to (consent to download at all is the caller's responsibility — same
/// convention as <see cref="IAssetInstaller"/>). Never invoked implicitly; <c>TranscodingAudioDecoder</c>
/// only calls <see cref="TryGetInstalledExecutablePath"/> (no network) and throws a typed, user-facing
/// exception when it returns <see langword="null"/>, letting the UI offer an explicit "Download ffmpeg"
/// action that calls <see cref="EnsureInstalledAsync"/>.
/// </summary>
public interface IFfmpegInstaller
{
    /// <summary>Local path to an already-installed, verified ffmpeg executable, or <see langword="null"/> if none is installed yet. No network access; at most a few <c>File.Exists</c> checks.</summary>
    string? TryGetInstalledExecutablePath();

    /// <summary>Downloads (if needed), verifies and extracts ffmpeg, returning the executable's local path. Idempotent: a second call with an already-installed copy does no network/extraction work.</summary>
    Task<string> EnsureInstalledAsync(IProgress<double>? progress, CancellationToken ct);
}
