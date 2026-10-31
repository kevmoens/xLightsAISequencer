namespace XlightsAI.Studio.Audio;

/// <summary>
/// Invokes an already-resolved external decoder executable (ffmpeg) to transcode one audio file to a
/// WAV file at <paramref name="destinationPath"/>. Separated from <see cref="TranscodingAudioDecoder"/>
/// so that decoder's own caching/dispatch orchestration logic is unit-testable without launching a real
/// OS process (see <see cref="FfmpegProcessTranscoder"/> for the real implementation).
/// </summary>
public interface IExternalAudioTranscoder
{
    /// <summary>
    /// Identifies the exact output format/settings this transcoder produces (codec, sample rate, channel
    /// count, and this implementation's own version). Part of <see cref="TranscodingAudioDecoder"/>'s own
    /// transcode-cache key — Section 4's "every cache key includes algorithm/model/settings versions"
    /// rule, applied here so changing an output setting can never silently serve a cached file produced
    /// under the previous settings. Must be non-empty and safe to use inside a file name.
    /// </summary>
    string OutputFormatVersion { get; }

    /// <exception cref="AudioProbeInvalidException">The source could not be transcoded (unsupported/corrupt content, no audio stream, etc.).</exception>
    Task TranscodeToWavAsync(string ffmpegExecutablePath, string sourcePath, string destinationPath, CancellationToken ct);
}
