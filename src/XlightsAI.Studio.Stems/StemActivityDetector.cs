using XlightsAI.Studio.Audio;

namespace XlightsAI.Studio.Stems;

/// <summary>
/// PHASE-06 Step 043 -- "Detect instrument activity segments with scores." Real (no third-party
/// dependency) implementation of <see cref="IStemActivityDetector"/>: decodes a <see cref="StemAsset"/>
/// exactly once via the injected <see cref="IAudioDecoder"/>, computes a fixed-window peak-normalized
/// RMS envelope (the same windowed-RMS technique <c>XlightsAI.Studio.Rhythm.EnergyEnvelopeAnalyzer</c>
/// already established for whole songs), and segments it into contiguous active regions using a
/// two-threshold hysteresis state machine (Step 043's own implementation guidance, verbatim: "Calculate
/// activity using stem RMS/onset features with hysteresis and confidence"). This is a loudness-based
/// activity signal, not an instrument classifier -- a loud bleed from another instrument into this
/// stem's own channel is reported as activity like any other energy, per the same implementation
/// guidance's explicit "avoid treating bleed as precise instrument classification" warning.
///
/// Before decoding anything, checks whether an existing cache at
/// <see cref="IStemActivityResultStore.GetPath"/> is still valid for <paramref name="stem"/>'s current
/// content/detector/settings versions (Section 4: "every cache key includes algorithm/model/settings
/// versions") and, if so, returns it immediately without recomputing.
/// </summary>
public sealed class StemActivityDetector : IStemActivityDetector
{
    /// <summary>Burned into every <see cref="StemActivityResult.DetectorVersion"/> this detector produces.</summary>
    public const string Version = "XlightsAI.Studio.Stems.StemActivityDetector/1.0.0";

    /// <summary>100 ns ticks/s, the same canonical unit conversion <see cref="SongAsset.DurationTicks"/> uses.</summary>
    private const long TicksPerSecond = 10_000_000L;

    private readonly IAudioDecoder _decoder;
    private readonly StemActivityDetectionSettings _settings;
    private readonly IStemActivityResultStore? _store;

    public StemActivityDetector(IAudioDecoder decoder, StemActivityDetectionSettings settings, IStemActivityResultStore? store = null)
    {
        _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        ArgumentNullException.ThrowIfNull(settings);
        Validate(settings);
        _settings = settings;
        _store = store;
    }

    public async Task<IReadOnlyList<StemActivity>> DetectAsync(StemAsset stem, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(stem);
        ct.ThrowIfCancellationRequested();

        var cachePath = _store?.GetPath(stem.Id);
        if (cachePath is not null &&
            await TryLoadStillValidCacheAsync(cachePath, stem, ct).ConfigureAwait(false) is { } cached)
        {
            return cached.Activities;
        }

        // Re-probe the real on-disk file (the same cross-check StemSongAssetFactory performs for the
        // mixdown/waveform boundary) so a moved/corrupted/stale stem is caught here rather than
        // silently analyzed from the wrong bytes.
        var probed = await _decoder.ProbeAsync(stem.FilePath, ct).ConfigureAwait(false);
        if (probed.SampleRate != stem.SampleRate || probed.SampleCount != stem.SampleCount || probed.Sha256 != stem.Sha256)
        {
            throw new StemActivityDetectionInvalidException(
                stem.FilePath,
                $"on-disk file (declared: {stem.SampleRate} Hz / {stem.SampleCount} samples / {stem.Sha256}; " +
                $"actual: {probed.SampleRate} Hz / {probed.SampleCount} samples / {probed.Sha256}) no longer matches " +
                "its own declared manifest metadata -- stale or corrupt stem file.");
        }

        var samples = new List<float>(stem.SampleCount > 0 && stem.SampleCount <= int.MaxValue ? (int)stem.SampleCount : 0);
        await foreach (var block in _decoder.DecodeMonoAsync(probed, stem.SampleRate, ct).ConfigureAwait(false))
        {
            samples.AddRange(block.Span);
            ct.ThrowIfCancellationRequested();
        }

        if (samples.Count != stem.SampleCount)
        {
            throw new StemActivityDetectionInvalidException(
                stem.FilePath,
                $"decoded sample count ({samples.Count}) does not match the declared sample count ({stem.SampleCount}).");
        }

        var activities = DetectActivities(samples, stem);

        if (_store is not null && cachePath is not null)
        {
            var result = new StemActivityResult(
                VersionId: Guid.NewGuid(),
                StemId: stem.Id,
                SourceHash: stem.Sha256,
                DetectorVersion: Version,
                Settings: _settings,
                Activities: activities);

            await _store.SaveAsync(result, ct).ConfigureAwait(false);
        }

        return activities;
    }

    private async Task<StemActivityResult?> TryLoadStillValidCacheAsync(string path, StemAsset stem, CancellationToken ct)
    {
        if (_store is null || !File.Exists(path))
        {
            return null;
        }

        StemActivityResult existing;
        try
        {
            existing = await _store.LoadAsync(path, ct).ConfigureAwait(false);
        }
        catch (StemActivityResultCorruptException)
        {
            return null;
        }

        var stillValid = existing.StemId == stem.Id
            && existing.SourceHash == stem.Sha256
            && existing.DetectorVersion == Version
            && existing.Settings == _settings;

        return stillValid ? existing : null;
    }

    private StemActivity[] DetectActivities(List<float> samples, StemAsset stem)
    {
        var frames = BuildNormalizedRmsFrames(samples, _settings.WindowSamples, _settings.HopSamples);
        if (frames.Count == 0)
        {
            return [];
        }

        var segments = new List<(long StartSample, long EndSample, List<double> Scores)>();
        var active = false;
        long segmentStart = 0;
        List<double> scores = [];

        foreach (var frame in frames)
        {
            if (!active)
            {
                if (frame.NormalizedEnergy >= _settings.OnThreshold)
                {
                    active = true;
                    segmentStart = frame.StartSample;
                    scores = [frame.NormalizedEnergy];
                }
            }
            else
            {
                if (frame.NormalizedEnergy >= _settings.OffThreshold)
                {
                    scores.Add(frame.NormalizedEnergy);
                }
                else
                {
                    segments.Add((segmentStart, frame.StartSample, scores));
                    active = false;
                }
            }
        }

        if (active)
        {
            segments.Add((segmentStart, frames[^1].EndSample, scores));
        }

        var activities = new List<StemActivity>();
        foreach (var (startSample, endSample, segmentScores) in segments)
        {
            var startTicks = stem.SourceOriginTicks + SamplesToTicks(startSample, stem.SampleRate);
            var endTicks = stem.SourceOriginTicks + SamplesToTicks(endSample, stem.SampleRate);

            if (endTicks - startTicks < _settings.MinActiveDurationTicks)
            {
                // Too short to be real activity -- discarded as noise/bleed rather than reported
                // (Step 043's own "avoid treating bleed as precise instrument classification").
                continue;
            }

            var score = segmentScores.Count > 0 ? Math.Clamp(segmentScores.Average(), 0.0, 1.0) : 0.0;
            activities.Add(new StemActivity(stem.Kind, startTicks, endTicks, score));
        }

        return activities.ToArray();
    }

    private static long SamplesToTicks(long sampleIndex, int sampleRate) =>
        sampleRate <= 0 ? 0 : (long)Math.Round(sampleIndex * (TicksPerSecond / (double)sampleRate));

    private static List<RmsFrame> BuildNormalizedRmsFrames(List<float> samples, int windowSamples, int hopSamples)
    {
        var frames = new List<RmsFrame>();

        long start = 0;
        while (start < samples.Count)
        {
            var end = Math.Min(start + windowSamples, samples.Count);
            double sumSquares = 0;
            for (var i = start; i < end; i++)
            {
                var sample = samples[(int)i];
                sumSquares += (double)sample * sample;
            }

            var count = end - start;
            var rms = count > 0 ? Math.Sqrt(sumSquares / count) : 0.0;
            frames.Add(new RmsFrame(start, end, rms, 0.0));
            start += hopSamples;
        }

        // Peak normalization (same convention as EnergyEnvelopeAnalyzer): a fully silent signal has a
        // zero peak, so every frame's NormalizedEnergy stays 0 rather than an undefined 0/0 division --
        // "Silent source yields finite output/no NaNs" (full-phase test matrix item 5).
        var peakRms = 0.0;
        foreach (var frame in frames)
        {
            if (frame.Rms > peakRms)
            {
                peakRms = frame.Rms;
            }
        }

        if (peakRms > 0.0)
        {
            for (var i = 0; i < frames.Count; i++)
            {
                frames[i] = frames[i] with { NormalizedEnergy = frames[i].Rms / peakRms };
            }
        }

        return frames;
    }

    private static void Validate(StemActivityDetectionSettings settings)
    {
        if (settings.WindowSamples <= 0)
        {
            throw new StemActivityDetectionInvalidException("<settings>", $"WindowSamples ({settings.WindowSamples}) must be positive.");
        }

        if (settings.HopSamples <= 0)
        {
            throw new StemActivityDetectionInvalidException("<settings>", $"HopSamples ({settings.HopSamples}) must be positive.");
        }

        if (settings.OnThreshold is <= 0.0 or > 1.0)
        {
            throw new StemActivityDetectionInvalidException("<settings>", $"OnThreshold ({settings.OnThreshold}) must be in (0, 1].");
        }

        if (settings.OffThreshold < 0.0 || settings.OffThreshold >= settings.OnThreshold)
        {
            throw new StemActivityDetectionInvalidException(
                "<settings>", $"OffThreshold ({settings.OffThreshold}) must be in [0, OnThreshold) -- got OnThreshold {settings.OnThreshold}.");
        }

        if (settings.MinActiveDurationTicks < 0)
        {
            throw new StemActivityDetectionInvalidException("<settings>", $"MinActiveDurationTicks ({settings.MinActiveDurationTicks}) must not be negative.");
        }
    }

    private readonly record struct RmsFrame(long StartSample, long EndSample, double Rms, double NormalizedEnergy);
}
