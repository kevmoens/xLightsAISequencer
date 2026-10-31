using XlightsAI.Studio.Audio;

namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// PHASE-03 (Analysis extensions) Task 1 -- real implementation of <see cref="IBandEnergyAnalyzer"/>:
/// splits the same Hann-windowed spectral-flux machinery <see cref="OnsetDetector"/> already uses into
/// three frequency bands (bass/mid/high) instead of collapsing every bin into one broadband scalar,
/// exposing per-band energy as a first-class analysis output. The per-bin magnitude spectrum and
/// half-wave-rectified flux computation are deliberately the same shape as <see cref="OnsetDetector"/>'s
/// own (same window, same "only positive rises count as energy" rule) -- the one, disclosed difference
/// is that this analyzer sums flux into three per-band buckets (by each bin's own center frequency)
/// instead of summing every bin into a single total.
/// </summary>
public sealed class BandEnergyAnalyzer : IBandEnergyAnalyzer
{
    /// <summary>Burned into every <see cref="BandEnergyEnvelope.AnalyzerVersion"/> this analyzer produces.</summary>
    public const string Version = "XlightsAI.Studio.Rhythm.BandEnergyAnalyzer/1.0.0";

    private const long TicksPerSecond = 10_000_000L;

    private readonly IAudioDecoder _decoder;
    private readonly IBandEnergyEnvelopeStore? _store;

    public BandEnergyAnalyzer(IAudioDecoder decoder, IBandEnergyEnvelopeStore? store = null)
    {
        _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        _store = store;
    }

    public async Task<BandEnergyEnvelope> AnalyzeAsync(SongAsset song, BandEnergySettings settings, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(song);
        ArgumentNullException.ThrowIfNull(settings);

        Validate(song, settings);

        var cachePath = _store?.GetPath(song.Id);
        if (cachePath is not null &&
            await TryLoadStillValidCacheAsync(cachePath, song, settings, ct).ConfigureAwait(false) is { } cached)
        {
            return cached;
        }

        var samples = new List<float>(song.SampleCount > 0 && song.SampleCount <= int.MaxValue ? (int)song.SampleCount : 0);
        await foreach (var block in _decoder.DecodeMonoAsync(song, song.SampleRate, ct).ConfigureAwait(false))
        {
            samples.AddRange(block.Span);
            ct.ThrowIfCancellationRequested();
        }

        if (samples.Count != song.SampleCount)
        {
            throw new BandEnergyAnalysisInvalidException(
                song.FilePath,
                $"decoded sample count ({samples.Count}) does not match the probed sample count ({song.SampleCount}).");
        }

        var frames = ComputeFrames(samples, settings, song.SampleRate);

        var envelope = new BandEnergyEnvelope(
            VersionId: Guid.NewGuid(),
            SongId: song.Id,
            SourceHash: song.Sha256,
            SourceDecoderVersion: song.DecoderVersion,
            AnalyzerVersion: Version,
            Settings: settings,
            Frames: frames);

        if (_store is not null && cachePath is not null)
        {
            await _store.SaveAsync(envelope, ct).ConfigureAwait(false);
        }

        return envelope;
    }

    private static void Validate(SongAsset song, BandEnergySettings settings)
    {
        if (settings.Rhythm.FftSize <= 0)
        {
            throw new BandEnergyAnalysisInvalidException(song.FilePath, $"BandEnergySettings.Rhythm.FftSize ({settings.Rhythm.FftSize}) must be positive.");
        }

        if (settings.Rhythm.HopSamples <= 0)
        {
            throw new BandEnergyAnalysisInvalidException(song.FilePath, $"BandEnergySettings.Rhythm.HopSamples ({settings.Rhythm.HopSamples}) must be positive.");
        }

        if (settings.Rhythm.AnalysisSampleRate != song.SampleRate)
        {
            throw new BandEnergyAnalysisInvalidException(
                song.FilePath,
                $"BandEnergySettings.Rhythm.AnalysisSampleRate ({settings.Rhythm.AnalysisSampleRate}) must equal the song's native sample rate ({song.SampleRate}); resampling is not supported by IAudioDecoder.");
        }

        if (settings.BassCutoffHz <= 0)
        {
            throw new BandEnergyAnalysisInvalidException(song.FilePath, $"BandEnergySettings.BassCutoffHz ({settings.BassCutoffHz}) must be positive.");
        }

        if (settings.HighCutoffHz <= settings.BassCutoffHz)
        {
            throw new BandEnergyAnalysisInvalidException(
                song.FilePath,
                $"BandEnergySettings.HighCutoffHz ({settings.HighCutoffHz}) must be greater than BassCutoffHz ({settings.BassCutoffHz}).");
        }
    }

    private async Task<BandEnergyEnvelope?> TryLoadStillValidCacheAsync(string path, SongAsset song, BandEnergySettings settings, CancellationToken ct)
    {
        if (_store is null || !File.Exists(path))
        {
            return null;
        }

        BandEnergyEnvelope existing;
        try
        {
            existing = await _store.LoadAsync(path, ct).ConfigureAwait(false);
        }
        catch (BandEnergyAnalysisCorruptException)
        {
            return null;
        }

        var stillValid = existing.SourceHash == song.Sha256
            && existing.SourceDecoderVersion == song.DecoderVersion
            && existing.AnalyzerVersion == Version
            && existing.Settings == settings;

        return stillValid ? existing : null;
    }

    private static BandEnergyFrame[] ComputeFrames(List<float> samples, BandEnergySettings settings, int sampleRate)
    {
        var fftSize = settings.Rhythm.FftSize;
        var hop = settings.Rhythm.HopSamples;
        var bins = (fftSize / 2) + 1;

        var frameStarts = new List<long>();
        for (long start = 0; start < samples.Count; start += hop)
        {
            frameStarts.Add(start);
        }

        if (frameStarts.Count == 0)
        {
            return [];
        }

        var window = BuildHannWindow(fftSize);
        var spectra = new double[frameStarts.Count][];
        for (var t = 0; t < frameStarts.Count; t++)
        {
            spectra[t] = ComputeMagnitudeSpectrum(samples, frameStarts[t], fftSize, bins, window);
        }

        // Each bin's own center frequency decides which band its positive flux is summed into.
        var bandByBin = new int[bins];
        for (var k = 0; k < bins; k++)
        {
            var frequencyHz = k * (double)sampleRate / fftSize;
            bandByBin[k] = frequencyHz < settings.BassCutoffHz ? 0 : frequencyHz < settings.HighCutoffHz ? 1 : 2;
        }

        var bass = new double[frameStarts.Count];
        var mid = new double[frameStarts.Count];
        var high = new double[frameStarts.Count];

        for (var t = 1; t < frameStarts.Count; t++)
        {
            double bassSum = 0, midSum = 0, highSum = 0;
            for (var k = 0; k < bins; k++)
            {
                var rise = spectra[t][k] - spectra[t - 1][k];
                if (rise <= 0)
                {
                    continue;
                }

                switch (bandByBin[k])
                {
                    case 0: bassSum += rise; break;
                    case 1: midSum += rise; break;
                    default: highSum += rise; break;
                }
            }

            bass[t] = bassSum;
            mid[t] = midSum;
            high[t] = highSum;
        }

        // One shared peak across every band so Bass/Mid/High stay comparable on a common scale --
        // see BandEnergyFrame's own doc comment for why per-band-independent normalization would erase
        // the very bass-vs-treble distinction this analyzer exists to expose.
        var sharedPeak = 0.0;
        for (var t = 0; t < frameStarts.Count; t++)
        {
            sharedPeak = Math.Max(sharedPeak, Math.Max(bass[t], Math.Max(mid[t], high[t])));
        }

        var result = new BandEnergyFrame[frameStarts.Count];
        for (var t = 0; t < frameStarts.Count; t++)
        {
            var end = Math.Min(frameStarts[t] + fftSize, samples.Count);
            var normalizedBass = sharedPeak > 0 ? (float)(bass[t] / sharedPeak) : 0f;
            var normalizedMid = sharedPeak > 0 ? (float)(mid[t] / sharedPeak) : 0f;
            var normalizedHigh = sharedPeak > 0 ? (float)(high[t] / sharedPeak) : 0f;
            result[t] = new BandEnergyFrame(frameStarts[t], end, normalizedBass, normalizedMid, normalizedHigh);
        }

        return result;
    }

    private static double[] BuildHannWindow(int fftSize)
    {
        var window = new double[fftSize];
        for (var i = 0; i < fftSize; i++)
        {
            window[i] = fftSize > 1
                ? 0.5 - (0.5 * Math.Cos(2 * Math.PI * i / (fftSize - 1)))
                : 1.0;
        }

        return window;
    }

    private static double[] ComputeMagnitudeSpectrum(List<float> samples, long start, int fftSize, int bins, double[] window)
    {
        var windowed = new double[fftSize];
        for (var i = 0; i < fftSize; i++)
        {
            var sampleIndex = start + i;
            var sample = sampleIndex < samples.Count ? samples[(int)sampleIndex] : 0f;
            windowed[i] = sample * window[i];
        }

        var magnitude = new double[bins];
        for (var k = 0; k < bins; k++)
        {
            double real = 0;
            double imag = 0;
            var angleStep = -2 * Math.PI * k / fftSize;
            for (var n = 0; n < fftSize; n++)
            {
                var angle = angleStep * n;
                real += windowed[n] * Math.Cos(angle);
                imag += windowed[n] * Math.Sin(angle);
            }

            magnitude[k] = Math.Sqrt((real * real) + (imag * imag));
        }

        return magnitude;
    }
}
