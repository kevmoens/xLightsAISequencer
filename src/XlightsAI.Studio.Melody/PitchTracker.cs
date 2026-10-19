using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Stems;

namespace XlightsAI.Studio.Melody;

/// <summary>
/// PHASE-07 Step 046 — real implementation of <see cref="IPitchTracker"/>: a time-domain YIN
/// fundamental-frequency estimator (Section 3 architectural design notes: "Keep pitch in MIDI note
/// numbers plus pitch confidence"; this step's own implementation guidance: "Implement pitch detector
/// with voice/instrument-specific limitations... preserve raw contour for fallback"). Decodes an
/// isolated-lead <see cref="StemAsset"/> exactly once via the injected <see cref="IAudioDecoder"/>,
/// computes the cumulative mean normalized difference function (de Cheveigné &amp; Kawahara, 2002) per
/// hop, and reports a sub-sample-refined (parabolic-interpolated) fundamental frequency, MIDI
/// note/cents and confidence for every analyzed frame -- including unvoiced/silent ones (see
/// <see cref="PitchFrame"/>'s own "preserve raw contour for fallback" remarks).
/// </summary>
public sealed class PitchTracker : IPitchTracker
{
    /// <summary>Burned into every <see cref="PitchTrackAnalysis.AnalyzerVersion"/> this tracker produces.</summary>
    public const string Version = "XlightsAI.Studio.Melody.PitchTracker/1.0.0";

    /// <summary>100 ns ticks/s, same canonical unit conversion every other analyzer in this repository uses.</summary>
    private const long TicksPerSecond = 10_000_000L;

    private readonly IAudioDecoder _decoder;
    private readonly IPitchTrackStore _store;

    public PitchTracker(IAudioDecoder decoder, IPitchTrackStore store)
    {
        _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<PitchTrackAnalysis> TrackAsync(StemAsset source, PitchTrackingSettings settings, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(settings);

        // "Invalid input... must not corrupt the project" (Step 046 failure/edge case): reject
        // nonsensical windowing/frequency-range/threshold values before any decode or cache lookup.
        if (settings.FrameSize <= 0)
        {
            throw new PitchTrackAnalysisInvalidException(source.FilePath, $"PitchTrackingSettings.FrameSize ({settings.FrameSize}) must be positive.");
        }

        if (settings.HopSamples <= 0)
        {
            throw new PitchTrackAnalysisInvalidException(source.FilePath, $"PitchTrackingSettings.HopSamples ({settings.HopSamples}) must be positive.");
        }

        if (settings.MinFrequencyHz <= 0)
        {
            throw new PitchTrackAnalysisInvalidException(source.FilePath, $"PitchTrackingSettings.MinFrequencyHz ({settings.MinFrequencyHz}) must be positive.");
        }

        if (settings.MaxFrequencyHz <= settings.MinFrequencyHz)
        {
            throw new PitchTrackAnalysisInvalidException(
                source.FilePath,
                $"PitchTrackingSettings.MaxFrequencyHz ({settings.MaxFrequencyHz}) must exceed MinFrequencyHz ({settings.MinFrequencyHz}).");
        }

        if (settings.VoicingThreshold is <= 0 or >= 1)
        {
            throw new PitchTrackAnalysisInvalidException(source.FilePath, $"PitchTrackingSettings.VoicingThreshold ({settings.VoicingThreshold}) must be in (0, 1).");
        }

        var path = _store.GetPath(source.Id);

        if (await TryLoadStillValidCacheAsync(path, source, settings, ct).ConfigureAwait(false) is { } cached)
        {
            return cached;
        }

        ct.ThrowIfCancellationRequested();

        var probed = await _decoder.ProbeAsync(source.FilePath, ct).ConfigureAwait(false);

        // "stale persisted state must not corrupt the project": the declared StemAsset metadata is
        // never trusted blindly -- the real file on disk must still match it.
        if (!string.Equals(probed.Sha256, source.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new PitchTrackAnalysisInvalidException(
                source.FilePath,
                $"the file's actual content hash ({probed.Sha256}) does not match the declared StemAsset.Sha256 ({source.Sha256}) -- stale or modified source file.");
        }

        if (probed.SampleRate != source.SampleRate)
        {
            throw new PitchTrackAnalysisInvalidException(
                source.FilePath,
                $"the file's actual sample rate ({probed.SampleRate}) does not match the declared StemAsset.SampleRate ({source.SampleRate}).");
        }

        var tauMin = Math.Max(1, (int)Math.Floor(probed.SampleRate / settings.MaxFrequencyHz));
        var tauMax = (int)Math.Ceiling(probed.SampleRate / settings.MinFrequencyHz);

        if (settings.FrameSize <= tauMax)
        {
            throw new PitchTrackAnalysisInvalidException(
                source.FilePath,
                $"PitchTrackingSettings.FrameSize ({settings.FrameSize}) must exceed the maximum lag implied by MinFrequencyHz " +
                $"({settings.MinFrequencyHz} Hz) at this source's sample rate ({probed.SampleRate} Hz), which is {tauMax} samples.");
        }

        var samples = new List<float>(probed.SampleCount is > 0 and <= int.MaxValue ? (int)probed.SampleCount : 0);
        await foreach (var block in _decoder.DecodeMonoAsync(probed, probed.SampleRate, ct).ConfigureAwait(false))
        {
            samples.AddRange(block.Span);
            ct.ThrowIfCancellationRequested();
        }

        if (samples.Count != probed.SampleCount)
        {
            throw new PitchTrackAnalysisInvalidException(
                source.FilePath,
                $"decoded sample count ({samples.Count}) does not match the probed sample count ({probed.SampleCount}).");
        }

        var frames = ComputeFrames(samples, probed.SampleRate, settings, tauMin, tauMax, ct);

        var analysis = new PitchTrackAnalysis(
            VersionId: Guid.NewGuid(),
            SourceStemId: source.Id,
            SourceHash: source.Sha256,
            SourceSampleRate: probed.SampleRate,
            AnalyzerVersion: Version,
            Settings: settings,
            Frames: frames);

        await _store.SaveAsync(analysis, ct).ConfigureAwait(false);
        return analysis;
    }

    private async Task<PitchTrackAnalysis?> TryLoadStillValidCacheAsync(
        string path, StemAsset source, PitchTrackingSettings settings, CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        PitchTrackAnalysis existing;
        try
        {
            existing = await _store.LoadAsync(path, ct).ConfigureAwait(false);
        }
        catch (PitchTrackAnalysisCorruptException)
        {
            return null;
        }

        var stillValid = existing.SourceStemId == source.Id
            && existing.SourceHash == source.Sha256
            && existing.SourceSampleRate == source.SampleRate
            && existing.AnalyzerVersion == Version
            && existing.Settings == settings;

        return stillValid ? existing : null;
    }

    private static PitchFrame[] ComputeFrames(
        List<float> samples, int sampleRate, PitchTrackingSettings settings, int tauMin, int tauMax, CancellationToken ct)
    {
        var frameSize = settings.FrameSize;
        var hop = settings.HopSamples;
        var diff = new double[tauMax + 1];
        var cmnd = new double[tauMax + 1];
        var frames = new List<PitchFrame>();

        if (samples.Count == 0)
        {
            return [];
        }

        for (long start = 0; start < samples.Count; start += hop)
        {
            ct.ThrowIfCancellationRequested();

            // Difference function d(tau), zero-padded past the end of the decoded signal (same
            // convention XlightsAI.Studio.Rhythm.OnsetDetector's own spectrum computation uses), so
            // every frame -- including the final, partially-out-of-range one -- is analyzed rather
            // than dropped.
            for (var tau = 0; tau <= tauMax; tau++)
            {
                double sum = 0;
                for (var j = 0; j < frameSize; j++)
                {
                    var i1 = start + j;
                    var i2 = start + j + tau;
                    var s1 = i1 < samples.Count ? samples[(int)i1] : 0f;
                    var s2 = i2 < samples.Count ? samples[(int)i2] : 0f;
                    var delta = s1 - s2;
                    sum += delta * delta;
                }

                diff[tau] = sum;
            }

            // Cumulative mean normalized difference function: d'(0) := 1 by definition (YIN step 2).
            cmnd[0] = 1.0;
            double runningSum = 0;
            for (var tau = 1; tau <= tauMax; tau++)
            {
                runningSum += diff[tau];
                cmnd[tau] = runningSum > 0 ? diff[tau] * tau / runningSum : 1.0;
            }

            var timeTicks = (long)Math.Round(start * (TicksPerSecond / (double)sampleRate));
            frames.Add(BuildFrame(cmnd, timeTicks, sampleRate, settings, tauMin, tauMax));
        }

        return [.. frames];
    }

    private static PitchFrame BuildFrame(
        double[] cmnd, long timeTicks, int sampleRate, PitchTrackingSettings settings, int tauMin, int tauMax)
    {
        int? chosenTau = null;
        for (var tau = tauMin; tau <= tauMax; tau++)
        {
            if (cmnd[tau] >= settings.VoicingThreshold)
            {
                continue;
            }

            // Absolute thresholding (YIN step 4): the *first* dip below threshold, refined to its own
            // local minimum, not necessarily the global minimum across all lags -- this is what lets
            // YIN reject the well-known octave-doubling error a naive global-minimum search is prone to.
            var t = tau;
            while (t + 1 <= tauMax && cmnd[t + 1] < cmnd[t])
            {
                t++;
            }

            chosenTau = t;
            break;
        }

        if (chosenTau is not int tauChosen)
        {
            return new PitchFrame(timeTicks, FrequencyHz: 0.0, MidiNote: null, CentsOffset: null, Confidence: 0.0);
        }

        var refinedTau = ParabolicInterpolate(cmnd, tauChosen, tauMin, tauMax);
        var frequencyHz = sampleRate / refinedTau;

        if (frequencyHz < settings.MinFrequencyHz || frequencyHz > settings.MaxFrequencyHz || double.IsNaN(frequencyHz) || double.IsInfinity(frequencyHz))
        {
            return new PitchFrame(timeTicks, FrequencyHz: 0.0, MidiNote: null, CentsOffset: null, Confidence: 0.0);
        }

        var confidence = Math.Clamp(1.0 - cmnd[tauChosen], 0.0, 1.0);
        var exactMidi = 69.0 + (12.0 * Math.Log2(frequencyHz / 440.0));
        var midiNote = (int)Math.Round(exactMidi);
        var centsOffset = (exactMidi - midiNote) * 100.0;

        return new PitchFrame(timeTicks, frequencyHz, midiNote, centsOffset, confidence);
    }

    /// <summary>
    /// Standard YIN parabolic sub-sample refinement (de Cheveigné &amp; Kawahara, 2002, step 5): fits a
    /// parabola through the chosen lag and its two neighbors in the cumulative mean normalized
    /// difference function, returning the parabola's vertex as a fractional lag. Falls back to the
    /// unrefined integer lag at either array boundary or when the three points are collinear (a
    /// degenerate, not musically meaningful, fit).
    /// </summary>
    private static double ParabolicInterpolate(double[] cmnd, int tau, int tauMin, int tauMax)
    {
        if (tau <= tauMin || tau >= tauMax)
        {
            return tau;
        }

        var s0 = cmnd[tau - 1];
        var s1 = cmnd[tau];
        var s2 = cmnd[tau + 1];
        var denominator = s0 - (2 * s1) + s2;
        if (Math.Abs(denominator) < 1e-12)
        {
            return tau;
        }

        var delta = 0.5 * (s0 - s2) / denominator;
        return tau + delta;
    }
}
