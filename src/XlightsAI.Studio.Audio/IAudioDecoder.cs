namespace XlightsAI.Studio.Audio;

/// <summary>
/// PHASE-04 Section 3's proposed decoder boundary. A documented, licensed managed backend per
/// supported container/codec (Section 3 architectural design notes); Step 024 ships the first
/// implementation (<see cref="WavAudioDecoder"/>, PCM/IEEE-float WAV). Probing must inspect actual
/// file contents, never trust the filename/extension alone.
/// </summary>
public interface IAudioDecoder
{
    /// <summary>
    /// Validates and reads canonical metadata (sample rate, channels, sample count, decoder
    /// version/priming) for the audio file at <paramref name="path"/>, without decoding full PCM
    /// sample data. Never mutates or moves the source file.
    /// </summary>
    /// <exception cref="FileNotFoundException"><paramref name="path"/> does not exist.</exception>
    /// <exception cref="AudioProbeInvalidException">The file is not valid/supported audio content.</exception>
    Task<SongAsset> ProbeAsync(string path, CancellationToken ct);

    /// <summary>
    /// Streams the full audio signal for <paramref name="song"/> as mono 32-bit float samples,
    /// resampled to <paramref name="targetSampleRate"/> if it differs from <see cref="SongAsset.SampleRate"/>.
    /// Multi-channel sources are downmixed by averaging channels. <paramref name="song"/> must be
    /// the result of a previous <see cref="ProbeAsync"/> call against the same file.
    /// </summary>
    IAsyncEnumerable<ReadOnlyMemory<float>> DecodeMonoAsync(SongAsset song, int targetSampleRate, CancellationToken ct);
}
