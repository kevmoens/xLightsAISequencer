namespace XlightsAI.Studio.Audio;

/// <summary>
/// Thrown by <see cref="TranscodingAudioDecoder"/> when a file's actual content is not RIFF/WAVE and no
/// local ffmpeg installation is available to transcode it. Lets <c>AudioImportViewModel.ProbeAudioFileAsync</c>
/// distinguish "offer the user a Download ffmpeg action" from an ordinary, unrecoverable
/// <see cref="AudioProbeInvalidException"/>.
/// </summary>
public sealed class FfmpegNotAvailableException : Exception
{
    public FfmpegNotAvailableException(string path)
        : base($"'{path}' is not a WAV file and no ffmpeg installation was found to transcode it. Download ffmpeg and try again.")
    {
        Path = path;
    }

    public string Path { get; }
}
