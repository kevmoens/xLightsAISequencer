namespace XlightsAI.Studio.Audio;

/// <summary>
/// PHASE-06A Step 044A — thrown by <see cref="IStereoAudioDecoder.DecodeStereoAsync"/> when
/// <see cref="SongAsset.Channels"/> is not exactly 2, before any sample data is read. This step's
/// own named failure/edge case, verbatim: "A mono (1-channel) or &gt;2-channel source passed to
/// DecodeStereoAsync throws... before any sample data is read" — never silently duplicating a mono
/// signal across both channels or dropping extra channels.
/// </summary>
public sealed class StereoAudioDecodeChannelMismatchException : Exception
{
    public StereoAudioDecodeChannelMismatchException(string path, int actualChannels)
        : base($"'{path}' has {actualChannels} channel(s); stereo (exactly 2) is required for DecodeStereoAsync.")
    {
        Path = path;
        ActualChannels = actualChannels;
    }

    public string Path { get; }

    public int ActualChannels { get; }
}
