namespace XlightsAI.Studio.Audio;

/// <summary>
/// PHASE-06A Step 044A — phase-local sibling to <see cref="IAudioDecoder"/>. Disclosed, deliberate:
/// <see cref="IAudioDecoder"/>'s only decode method is literally named <c>DecodeMonoAsync</c> and
/// every existing caller (Phase 06's <c>OnnxStemSeparator</c>, Phase 06 Step 043's
/// <c>StemActivityDetector</c>, Phase 07's pitch tracker) depends on its averaged-mono-downmix
/// behavior; retrofitting it to also emit channel-preserving stereo would either break that existing
/// contract or require a second, differently-shaped method bolted onto an interface whose name
/// already promises mono. A new, narrowly-scoped sibling interface is introduced instead — the same
/// relationship Phase 07 Step 048 established for <c>IMidiTranscriptionBackend</c> relative to
/// <c>IMelodyAnalyzer</c>. <see cref="IAudioDecoder"/>/<see cref="WavAudioDecoder"/> are not modified
/// or retired by this addendum.
/// </summary>
public interface IStereoAudioDecoder
{
    /// <summary>
    /// Streams the full audio signal for <paramref name="song"/> as independently-addressable left/right
    /// 32-bit float channels, at the source's own native sample rate (no resampling — the same disclosed
    /// scope boundary <see cref="IAudioDecoder.DecodeMonoAsync"/> already carries). <paramref name="song"/>
    /// must be the result of a previous <see cref="IAudioDecoder.ProbeAsync"/> call against the same file.
    /// </summary>
    /// <exception cref="StereoAudioDecodeChannelMismatchException"><paramref name="song"/> was not probed as a 2-channel file. Thrown before any sample data is read.</exception>
    IAsyncEnumerable<StereoSampleBlock> DecodeStereoAsync(SongAsset song, CancellationToken ct);
}
