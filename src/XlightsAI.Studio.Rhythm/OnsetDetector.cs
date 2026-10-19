using XlightsAI.Studio.Audio;

namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// PHASE-05 Step 032 — real implementation of <see cref="IOnsetDetector"/>: a spectral-flux onset
/// detector (Section 3 architectural design notes: "Build spectral flux/energy rise onsets"). Decodes
/// a <see cref="SongAsset"/> exactly once via the injected <see cref="IAudioDecoder"/>, computes a
/// Hann-windowed magnitude spectrum per <see cref="RhythmSettings.FftSize"/>/<see cref="RhythmSettings.HopSamples"/>
/// frame, derives half-wave-rectified spectral flux between consecutive frames, and peak-picks onsets
/// against a local adaptive threshold scaled by <see cref="OnsetDetectionSettings.SensitivityMultiplier"/>.
/// </summary>
/// <remarks>
/// <b>Group delay correction</b> (Section 3: "correct algorithmic group delay" — the real gap Step 031's
/// own evidence disclosed and deferred to this step): a windowed spectral feature's flux value computed
/// from the frame <c>[start, start+FftSize)</c> reflects energy spread across that *entire* window, not
/// a single instant — naively reporting the window's <i>start</i> sample as the onset time systematically
/// under-estimates the true transient position by up to half a window. This detector reports the onset
/// at the window's <i>center</i> sample (<c>start + FftSize/2</c>) instead, which is the standard
/// correction for this class of detector and is directly exercised by
/// <c>Step032AcceptanceTests</c>'s timestamp-accuracy assertions.
/// </remarks>
public sealed class OnsetDetector : IOnsetDetector
{
    /// <summary>Burned into every <see cref="OnsetAnalysis.AnalyzerVersion"/> this detector produces.</summary>
    public const string Version = "XlightsAI.Studio.Rhythm.OnsetDetector/1.0.0";

    /// <summary>Burned into every <see cref="OnsetEvent.SourceAlgorithm"/> this detector produces.</summary>
    public const string AlgorithmName = "SpectralFlux";

    /// <summary>100 ns ticks/s, same canonical unit conversion <see cref="SongAsset.DurationTicks"/> uses.</summary>
    private const long TicksPerSecond = 10_000_000L;

    /// <summary>Radius, in frames, of the local window used to compute the adaptive peak-picking threshold's mean/standard deviation.</summary>
    private const int AdaptiveWindowRadiusFrames = 8;

    /// <summary>
    /// Absolute floor (as a fraction of this run's own peak normalized flux) a candidate must clear
    /// regardless of the local adaptive threshold. A sustained, steady-state tone whose window length
    /// is not an exact multiple of its own period produces a slow, cycle-to-cycle spectral-leakage
    /// "beat" as the window's phase drifts relative to the waveform — numerically tiny (far below any
    /// real transient) but still a local maximum against an equally tiny near-zero local baseline, so
    /// the purely-relative adaptive threshold alone cannot reject it. This floor is what "test false
    /// triggers from sustained chords" (Section 3 architectural design notes) requires catching.
    /// </summary>
    private const double MinimumAbsoluteNormalizedFlux = 0.05;

    private readonly IAudioDecoder _decoder;
    private readonly IOnsetAnalysisStore _store;

    public OnsetDetector(IAudioDecoder decoder, IOnsetAnalysisStore store)
    {
        _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<OnsetAnalysis> DetectAsync(SongAsset song, RhythmSettings settings, OnsetDetectionSettings onsetSettings, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(song);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(onsetSettings);

        // "Invalid input... must not corrupt the project" (Step 032 failure/edge case, same shape as
        // Step 031's own validation): reject nonsensical windowing or sensitivity before any decode or
        // cache lookup.
        if (settings.FftSize <= 0)
        {
            throw new OnsetAnalysisInvalidException(song.FilePath, $"RhythmSettings.FftSize ({settings.FftSize}) must be positive.");
        }

        if (settings.HopSamples <= 0)
        {
            throw new OnsetAnalysisInvalidException(song.FilePath, $"RhythmSettings.HopSamples ({settings.HopSamples}) must be positive.");
        }

        if (settings.AnalysisSampleRate != song.SampleRate)
        {
            throw new OnsetAnalysisInvalidException(
                song.FilePath,
                $"RhythmSettings.AnalysisSampleRate ({settings.AnalysisSampleRate}) must equal the song's native sample rate ({song.SampleRate}); resampling is not supported by IAudioDecoder.");
        }

        if (onsetSettings.SensitivityMultiplier <= 0f)
        {
            throw new OnsetAnalysisInvalidException(song.FilePath, $"OnsetDetectionSettings.SensitivityMultiplier ({onsetSettings.SensitivityMultiplier}) must be positive.");
        }

        if (onsetSettings.MinInterOnsetSamples < 0)
        {
            throw new OnsetAnalysisInvalidException(song.FilePath, $"OnsetDetectionSettings.MinInterOnsetSamples ({onsetSettings.MinInterOnsetSamples}) must not be negative.");
        }

        var path = _store.GetPath(song.Id);

        if (await TryLoadStillValidCacheAsync(path, song, settings, onsetSettings, ct).ConfigureAwait(false) is { } cached)
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
            throw new OnsetAnalysisInvalidException(
                song.FilePath,
                $"decoded sample count ({samples.Count}) does not match the probed sample count ({song.SampleCount}).");
        }

        var onsets = DetectOnsets(samples, settings, onsetSettings, song.SampleRate);

        var analysis = new OnsetAnalysis(
            VersionId: Guid.NewGuid(),
            SongId: song.Id,
            SourceHash: song.Sha256,
            SourceDecoderVersion: song.DecoderVersion,
            AnalyzerVersion: Version,
            Settings: settings,
            OnsetSettings: onsetSettings,
            Onsets: onsets);

        await _store.SaveAsync(analysis, ct).ConfigureAwait(false);
        return analysis;
    }

    private async Task<OnsetAnalysis?> TryLoadStillValidCacheAsync(
        string path, SongAsset song, RhythmSettings settings, OnsetDetectionSettings onsetSettings, CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        OnsetAnalysis existing;
        try
        {
            existing = await _store.LoadAsync(path, ct).ConfigureAwait(false);
        }
        catch (OnsetAnalysisCorruptException)
        {
            return null;
        }

        var stillValid = existing.SourceHash == song.Sha256
            && existing.SourceDecoderVersion == song.DecoderVersion
            && existing.AnalyzerVersion == Version
            && existing.Settings == settings
            && existing.OnsetSettings == onsetSettings;

        return stillValid ? existing : null;
    }

    private static OnsetEvent[] DetectOnsets(List<float> samples, RhythmSettings settings, OnsetDetectionSettings onsetSettings, int sampleRate)
    {
        var fftSize = settings.FftSize;
        var hop = settings.HopSamples;
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

        // Half-wave-rectified spectral flux: the sum, across every frequency bin, of only the
        // *positive* rises in magnitude from the previous frame to this one (an energy/magnitude
        // increase is characteristic of a transient onset; falling energy is not). Frame 0 has no
        // predecessor, so its own flux is defined as 0 — the detector cannot claim an onset at the
        // very first frame from spectral flux alone (a disclosed known limitation; see
        // Step032AcceptanceTests's leading-silence fixture convention, which avoids placing a true
        // onset at sample 0 for exactly this reason).
        var flux = new double[frameStarts.Count];
        for (var t = 1; t < frameStarts.Count; t++)
        {
            double sum = 0;
            for (var k = 0; k < bins; k++)
            {
                var rise = spectra[t][k] - spectra[t - 1][k];
                if (rise > 0)
                {
                    sum += rise;
                }
            }

            flux[t] = sum;
        }

        var peakFlux = flux.Length > 0 ? flux.Max() : 0.0;
        var normalizedFlux = new double[flux.Length];
        if (peakFlux > 0)
        {
            for (var t = 0; t < flux.Length; t++)
            {
                normalizedFlux[t] = flux[t] / peakFlux;
            }
        }

        var candidates = new List<(long CenterSample, double Strength)>();
        for (var t = 1; t < normalizedFlux.Length - 1; t++)
        {
            if (normalizedFlux[t] < MinimumAbsoluteNormalizedFlux)
            {
                continue;
            }

            // Local maximum: strictly not weaker than either neighbor, and not a flat run where the
            // previous sample was already accepted as the peak (ties resolve to the first-seen frame).
            if (normalizedFlux[t] < normalizedFlux[t - 1] || normalizedFlux[t] < normalizedFlux[t + 1])
            {
                continue;
            }

            var (mean, stdDev) = ComputeLocalStatistics(normalizedFlux, t, AdaptiveWindowRadiusFrames);
            var threshold = mean + (onsetSettings.SensitivityMultiplier * stdDev);
            if (normalizedFlux[t] <= threshold)
            {
                continue;
            }

            // Group delay correction (see class remarks): report the window's center sample, not its
            // start, clamped to stay within the decoded signal.
            var centerSample = Math.Min(frameStarts[t] + (fftSize / 2), samples.Count - 1);
            candidates.Add((centerSample, normalizedFlux[t]));
        }

        // Enforce the refractory period by accepting candidates strongest-first, skipping any
        // candidate too close (in sample distance) to one already accepted — a single physical
        // transient spread across several overlapping frames collapses to its single strongest frame.
        var accepted = new List<(long CenterSample, double Strength)>();
        foreach (var candidate in candidates.OrderByDescending(c => c.Strength))
        {
            if (accepted.Any(a => Math.Abs(a.CenterSample - candidate.CenterSample) < onsetSettings.MinInterOnsetSamples))
            {
                continue;
            }

            accepted.Add(candidate);
        }

        return accepted
            .OrderBy(a => a.CenterSample)
            .Select(a => new OnsetEvent(
                TimeTicks: (long)Math.Round(a.CenterSample * (TicksPerSecond / (double)sampleRate)),
                Strength: (float)a.Strength,
                SourceAlgorithm: AlgorithmName))
            .ToArray();
    }

    private static (double Mean, double StdDev) ComputeLocalStatistics(double[] values, int centerIndex, int radius)
    {
        var lo = Math.Max(0, centerIndex - radius);
        var hi = Math.Min(values.Length - 1, centerIndex + radius);

        double sum = 0;
        var count = 0;
        for (var i = lo; i <= hi; i++)
        {
            sum += values[i];
            count++;
        }

        var mean = count > 0 ? sum / count : 0.0;

        double sumSquaredDeviation = 0;
        for (var i = lo; i <= hi; i++)
        {
            var deviation = values[i] - mean;
            sumSquaredDeviation += deviation * deviation;
        }

        var stdDev = count > 0 ? Math.Sqrt(sumSquaredDeviation / count) : 0.0;
        return (mean, stdDev);
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

    /// <summary>
    /// Hann-windowed magnitude spectrum of the frame <c>[start, start+fftSize)</c>, zero-padded past
    /// the end of the decoded signal (a fixed-size DFT needs a fixed-size input; unlike Step 031's
    /// <c>EnergyFrame</c>, which truncates its final frame, zero-padding here keeps every frame's
    /// spectral bin count identical so flux between the last two frames remains a like-for-like
    /// comparison). A direct O(fftSize * bins) DFT — this step's fixed, test-scale window sizes (a few
    /// hundred samples) make a dedicated FFT library unnecessary and keep this project's own
    /// no-new-dependency convention intact.
    /// </summary>
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
