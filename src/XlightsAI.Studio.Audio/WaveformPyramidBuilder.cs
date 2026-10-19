namespace XlightsAI.Studio.Audio;

/// <summary>
/// PHASE-04 Step 026 — real implementation of <see cref="IWaveformBuilder"/>: decodes a
/// <see cref="SongAsset"/> exactly once via the injected <see cref="IAudioDecoder"/> and computes a
/// zoom-independent multiresolution min/max/RMS peaks pyramid from that single decoded buffer (one
/// full decode feeds every <see cref="WaveformResolutionLevel"/>, not one decode per level). Before
/// decoding anything, checks whether an existing cache at <see cref="IWaveformPyramidStore.GetPath"/>
/// is still valid for <paramref name="song"/>'s current content/decoder/builder versions
/// (Section 4: "every cache key includes algorithm/model/settings versions") and, if so, returns it
/// immediately without recomputing — the "Cache reopens without recompute" required acceptance.
/// </summary>
public sealed class WaveformPyramidBuilder : IWaveformBuilder
{
    /// <summary>Burned into every <see cref="WaveformPyramidCache.BuilderVersion"/> this builder produces (Section 4's cache-key rule, applied to the pyramid-building algorithm itself).</summary>
    public const string Version = "XlightsAI.Studio.Audio.WaveformPyramidBuilder/1.0.0";

    /// <summary>
    /// Samples summarized per envelope at each pyramid level, finest to coarsest. Chosen so the
    /// finest level (64 samples, ~8 ms at an 8 kHz fixture rate) resolves individual clicks/
    /// transients, and the coarsest level (16384 samples) keeps a multi-minute song's full-overview
    /// envelope count small enough to render cheaply at any zoom — "zoom-independent... cheap
    /// zoom/pan" (Section 3 architectural design notes).
    /// </summary>
    private static readonly int[] BlockSizesSamples = [64, 256, 1024, 4096, 16384];

    private readonly IAudioDecoder _decoder;
    private readonly IWaveformPyramidStore _store;

    public WaveformPyramidBuilder(IAudioDecoder decoder, IWaveformPyramidStore store)
    {
        _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<string> BuildPyramidAsync(SongAsset song, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(song);

        var path = _store.GetPath(song.Id);

        if (await TryLoadStillValidCacheAsync(path, song, ct).ConfigureAwait(false))
        {
            return path;
        }

        // Full decode once; every resolution level below is computed from this same in-memory
        // buffer rather than re-decoding per level.
        var samples = new List<float>(song.SampleCount > 0 && song.SampleCount <= int.MaxValue ? (int)song.SampleCount : 0);
        await foreach (var block in _decoder.DecodeMonoAsync(song, song.SampleRate, ct).ConfigureAwait(false))
        {
            samples.AddRange(block.Span);
            ct.ThrowIfCancellationRequested();
        }

        // "write cache only after complete decode and verified dimensions" — a mismatch between the
        // song's own declared SampleCount and what was actually decodable is an invalid timing
        // boundary; refuse to persist a pyramid that would silently misreport its own coverage.
        if (samples.Count != song.SampleCount)
        {
            throw new WaveformPyramidInvalidException(
                song.FilePath,
                $"decoded sample count ({samples.Count}) does not match the probed sample count ({song.SampleCount}).");
        }

        var resolutions = new WaveformResolutionLevel[BlockSizesSamples.Length];
        for (var i = 0; i < BlockSizesSamples.Length; i++)
        {
            resolutions[i] = BuildResolutionLevel(samples, BlockSizesSamples[i]);
        }

        var cache = new WaveformPyramidCache(
            SongId: song.Id,
            ContentHash: song.Sha256,
            SourceDecoderVersion: song.DecoderVersion,
            BuilderVersion: Version,
            SourceSampleRate: song.SampleRate,
            SampleCount: song.SampleCount,
            Resolutions: resolutions);

        return await _store.SaveAsync(cache, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// <see langword="true"/> only if a cache file already exists at <paramref name="path"/> and its
    /// recorded content hash/decoder version/builder version/sample count all still match
    /// <paramref name="song"/> exactly — any mismatch (changed audio content, a decoder/builder
    /// upgrade, or a corrupt file) means the existing file is stale/invalid and must be rebuilt, not
    /// silently reused.
    /// </summary>
    private async Task<bool> TryLoadStillValidCacheAsync(string path, SongAsset song, CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        WaveformPyramidCache existing;
        try
        {
            existing = await _store.LoadAsync(path, ct).ConfigureAwait(false);
        }
        catch (WaveformPyramidCorruptException)
        {
            return false;
        }

        return existing.ContentHash == song.Sha256
            && existing.SourceDecoderVersion == song.DecoderVersion
            && existing.BuilderVersion == Version
            && existing.SampleCount == song.SampleCount;
    }

    private static WaveformResolutionLevel BuildResolutionLevel(List<float> samples, int blockSizeSamples)
    {
        var envelopes = new List<WaveformEnvelope>();

        long start = 0;
        while (start < samples.Count)
        {
            var end = Math.Min(start + blockSizeSamples, samples.Count);
            var min = float.MaxValue;
            var max = float.MinValue;
            double sumSquares = 0;

            for (var i = start; i < end; i++)
            {
                var sample = samples[(int)i];
                if (sample < min)
                {
                    min = sample;
                }

                if (sample > max)
                {
                    max = sample;
                }

                sumSquares += (double)sample * sample;
            }

            var count = end - start;
            var rms = count > 0 ? (float)Math.Sqrt(sumSquares / count) : 0f;
            envelopes.Add(new WaveformEnvelope(start, end, min, max, rms));
            start = end;
        }

        return new WaveformResolutionLevel(blockSizeSamples, envelopes.ToArray());
    }
}
