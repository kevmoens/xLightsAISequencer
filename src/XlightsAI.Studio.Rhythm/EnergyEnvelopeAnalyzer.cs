using XlightsAI.Studio.Audio;

namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// PHASE-05 Step 031 — real implementation of <see cref="IEnergyEnvelopeAnalyzer"/>: decodes a
/// <see cref="SongAsset"/> exactly once via the injected <see cref="IAudioDecoder"/> and computes a
/// fixed-window RMS/energy envelope from that single decoded buffer. Before decoding anything, checks
/// whether an existing cache at <see cref="IEnergyEnvelopeStore.GetPath"/> is still valid for
/// <paramref name="song"/>'s current content/decoder/analyzer/settings versions (Section 4: "every
/// cache key includes algorithm/model/settings versions") and, if so, returns it immediately without
/// recomputing.
/// </summary>
public sealed class EnergyEnvelopeAnalyzer : IEnergyEnvelopeAnalyzer
{
    /// <summary>Burned into every <see cref="EnergyEnvelope.AnalyzerVersion"/> this analyzer produces.</summary>
    public const string Version = "XlightsAI.Studio.Rhythm.EnergyEnvelopeAnalyzer/1.0.0";

    private readonly IAudioDecoder _decoder;
    private readonly IEnergyEnvelopeStore _store;

    public EnergyEnvelopeAnalyzer(IAudioDecoder decoder, IEnergyEnvelopeStore store)
    {
        _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<EnergyEnvelope> AnalyzeAsync(SongAsset song, RhythmSettings settings, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(song);
        ArgumentNullException.ThrowIfNull(settings);

        // "Invalid input... must not corrupt the project" (Step 031 failure/edge case): reject
        // nonsensical or unsupported windowing before any decode or cache lookup, rather than
        // producing a confusing downstream failure or a silently wrong envelope.
        if (settings.FftSize <= 0)
        {
            throw new EnergyEnvelopeInvalidException(song.FilePath, $"RhythmSettings.FftSize ({settings.FftSize}) must be positive.");
        }

        if (settings.HopSamples <= 0)
        {
            throw new EnergyEnvelopeInvalidException(song.FilePath, $"RhythmSettings.HopSamples ({settings.HopSamples}) must be positive.");
        }

        // IAudioDecoder.DecodeMonoAsync does not resample (WavAudioDecoder's own disclosed "resampling
        // is deliberately out of scope" note) — a mismatched requested rate must fail clearly here
        // rather than silently decode at the wrong (source) rate and mislabel the result.
        if (settings.AnalysisSampleRate != song.SampleRate)
        {
            throw new EnergyEnvelopeInvalidException(
                song.FilePath,
                $"RhythmSettings.AnalysisSampleRate ({settings.AnalysisSampleRate}) must equal the song's native sample rate ({song.SampleRate}); resampling is not supported by IAudioDecoder.");
        }

        var path = _store.GetPath(song.Id);

        if (await TryLoadStillValidCacheAsync(path, song, settings, ct).ConfigureAwait(false) is { } cached)
        {
            return cached;
        }

        // Full decode once; the envelope below is computed from this same in-memory buffer rather
        // than re-decoding per frame.
        var samples = new List<float>(song.SampleCount > 0 && song.SampleCount <= int.MaxValue ? (int)song.SampleCount : 0);
        await foreach (var block in _decoder.DecodeMonoAsync(song, song.SampleRate, ct).ConfigureAwait(false))
        {
            samples.AddRange(block.Span);
            ct.ThrowIfCancellationRequested();
        }

        // "write cache only after complete decode and verified dimensions" (same convention as
        // WaveformPyramidBuilder) — a mismatch between the song's own declared SampleCount and what
        // was actually decodable is an invalid timing boundary; refuse to persist an envelope that
        // would silently misreport its own coverage.
        if (samples.Count != song.SampleCount)
        {
            throw new EnergyEnvelopeInvalidException(
                song.FilePath,
                $"decoded sample count ({samples.Count}) does not match the probed sample count ({song.SampleCount}).");
        }

        var frames = BuildFrames(samples, settings.FftSize, settings.HopSamples);

        var envelope = new EnergyEnvelope(
            VersionId: Guid.NewGuid(),
            SongId: song.Id,
            SourceHash: song.Sha256,
            SourceDecoderVersion: song.DecoderVersion,
            AnalyzerVersion: Version,
            Settings: settings,
            Frames: frames);

        await _store.SaveAsync(envelope, ct).ConfigureAwait(false);
        return envelope;
    }

    /// <summary>
    /// Returns a still-valid cached envelope if one exists at <paramref name="path"/> and its recorded
    /// content hash/decoder version/analyzer version/settings all still match <paramref name="song"/>/
    /// <paramref name="settings"/> exactly; <see langword="null"/> otherwise (missing, corrupt, or
    /// stale — any mismatch means the existing file must be rebuilt, never silently reused).
    /// </summary>
    private async Task<EnergyEnvelope?> TryLoadStillValidCacheAsync(string path, SongAsset song, RhythmSettings settings, CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        EnergyEnvelope existing;
        try
        {
            existing = await _store.LoadAsync(path, ct).ConfigureAwait(false);
        }
        catch (EnergyEnvelopeCorruptException)
        {
            return null;
        }

        var stillValid = existing.SourceHash == song.Sha256
            && existing.SourceDecoderVersion == song.DecoderVersion
            && existing.AnalyzerVersion == Version
            && existing.Settings == settings;

        return stillValid ? existing : null;
    }

    private static EnergyFrame[] BuildFrames(List<float> samples, int fftSize, int hopSamples)
    {
        var frames = new List<EnergyFrame>();

        long start = 0;
        while (start < samples.Count)
        {
            var end = Math.Min(start + fftSize, samples.Count);
            double sumSquares = 0;

            for (var i = start; i < end; i++)
            {
                var sample = samples[(int)i];
                sumSquares += (double)sample * sample;
            }

            var count = end - start;
            var rms = count > 0 ? (float)Math.Sqrt(sumSquares / count) : 0f;
            frames.Add(new EnergyFrame(start, end, rms, NormalizedEnergy: 0f));
            start += hopSamples;
        }

        // "unit normalization" (Section 3 implementation guidance): every frame's Rms is divided by
        // the single largest Rms across the whole envelope. A fully silent signal has a zero peak, so
        // NormalizedEnergy stays 0 for every frame rather than an undefined 0/0 division.
        var peakRms = 0f;
        foreach (var frame in frames)
        {
            if (frame.Rms > peakRms)
            {
                peakRms = frame.Rms;
            }
        }

        if (peakRms > 0f)
        {
            for (var i = 0; i < frames.Count; i++)
            {
                frames[i] = frames[i] with { NormalizedEnergy = frames[i].Rms / peakRms };
            }
        }

        return frames.ToArray();
    }
}
