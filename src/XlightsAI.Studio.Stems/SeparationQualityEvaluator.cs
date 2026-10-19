using System.Diagnostics;
using System.Runtime.InteropServices;
using XlightsAI.Studio.Audio;

namespace XlightsAI.Studio.Stems;

/// <summary>
/// Real <see cref="ISeparationQualityEvaluator"/>: for every <see cref="QualityClip"/>, runs the real
/// <see cref="IStemSeparator"/> (never mocked), decodes both the real produced stem files and the real
/// reference files via the real <see cref="IAudioDecoder"/>, and scores each pair with the pure
/// <see cref="SeparationQualityMetricCalculator"/>. Wall-clock time (<see cref="Stopwatch"/>) and process
/// memory (<see cref="Process.PeakWorkingSet64"/>) are measured directly around the real separation
/// calls -- this step's own "establish supported hardware" text, verbatim.
/// </summary>
public sealed class SeparationQualityEvaluator : ISeparationQualityEvaluator
{
    private readonly IAudioDecoder _audioDecoder;

    public SeparationQualityEvaluator(IAudioDecoder audioDecoder)
    {
        ArgumentNullException.ThrowIfNull(audioDecoder);
        _audioDecoder = audioDecoder;
    }

    public async Task<SeparationQualityReport> EvaluateAsync(
        Guid reportId,
        long createdAtTicks,
        IStemSeparator separator,
        ModelAsset model,
        string recipeVersion,
        IReadOnlyList<QualityClip> clips,
        SeparationQualityThresholds thresholds,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(separator);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipeVersion);
        ArgumentNullException.ThrowIfNull(clips);
        ArgumentNullException.ThrowIfNull(thresholds);
        thresholds.Validate();

        if (clips.Count == 0)
        {
            throw new SeparationQualityInvalidInputException(
                "At least one quality clip is required -- the full-phase test matrix's own item 7 requires " +
                "at least two listening reference clips to meet the declared quality bar.");
        }

        // "Missing media must be handled explicitly" (verbatim): every reference file is checked to
        // exist *before* any (expensive, real) separation work runs for any clip.
        foreach (var clip in clips)
        {
            ArgumentNullException.ThrowIfNull(clip);
            if (clip.ReferenceStemFilePaths.Count == 0)
            {
                throw new SeparationQualityInvalidInputException(
                    $"Clip '{clip.ClipId}' declares no reference stems to score against.");
            }

            foreach (var (kind, path) in clip.ReferenceStemFilePaths)
            {
                if (!File.Exists(path))
                {
                    throw new SeparationQualityInvalidInputException(
                        $"Clip '{clip.ClipId}' (stem {kind}): reference media file '{path}' does not exist.");
                }
            }
        }

        var metrics = new List<SeparationQualityMetric>();
        var stopwatch = new Stopwatch();
        var totalSourceMinutes = 0.0;

        foreach (var clip in clips)
        {
            ct.ThrowIfCancellationRequested();

            stopwatch.Start();
            var manifest = await separator.SeparateAsync(clip.Source, model, progress: null, ct).ConfigureAwait(false);
            stopwatch.Stop();

            totalSourceMinutes += clip.Source.DurationTicks / 10_000_000.0 / 60.0;

            foreach (var (kind, referencePath) in clip.ReferenceStemFilePaths)
            {
                var producedStem = manifest.Stems.FirstOrDefault(s => s.Kind == kind);
                if (producedStem is null)
                {
                    throw new SeparationQualityInvalidInputException(
                        $"Clip '{clip.ClipId}': separator did not produce a stem of kind {kind} to score against " +
                        $"reference '{referencePath}'.");
                }

                var candidateSamples = await DecodeFullMonoAsync(producedStem.FilePath, ct).ConfigureAwait(false);
                var referenceSamples = await DecodeFullMonoAsync(referencePath, ct).ConfigureAwait(false);

                var metric = SeparationQualityMetricCalculator.Compute(
                    $"{clip.ClipId}:{kind}", kind, candidateSamples, referenceSamples, thresholds);
                metrics.Add(metric);
            }
        }

        var hardware = new HardwareProfile(
            OsDescription: RuntimeInformation.OSDescription,
            ProcessArchitecture: RuntimeInformation.ProcessArchitecture.ToString(),
            ProcessorCount: Environment.ProcessorCount,
            TotalAvailableMemoryBytes: GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
            PeakWorkingSetBytes: Process.GetCurrentProcess().PeakWorkingSet64,
            MeasuredWallClockSecondsPerSongMinute: totalSourceMinutes > 0
                ? stopwatch.Elapsed.TotalSeconds / totalSourceMinutes
                : 0.0);

        var overallPass = metrics.Count > 0 && metrics.All(m => m.PassesThreshold);
        var decision = overallPass
            ? $"All {metrics.Count} measured clip/stem metrics met the declared acceptance threshold " +
              $"(SDR >= {thresholds.MinSignalToDistortionRatioDb} dB, correlation >= {thresholds.MinCorrelationCoefficient})."
            : $"Only {metrics.Count(m => m.PassesThreshold)} of {metrics.Count} measured clip/stem metrics met the " +
              "declared acceptance threshold; see ClipMetrics for the failing entries.";

        return new SeparationQualityReport(
            reportId,
            createdAtTicks,
            model.Id,
            recipeVersion,
            metrics,
            thresholds,
            hardware,
            overallPass,
            decision);
    }

    private async Task<float[]> DecodeFullMonoAsync(string path, CancellationToken ct)
    {
        var probed = await _audioDecoder.ProbeAsync(path, ct).ConfigureAwait(false);
        var samples = new List<float>(probed.SampleCount is > 0 and < int.MaxValue ? (int)probed.SampleCount : 0);
        await foreach (var block in _audioDecoder.DecodeMonoAsync(probed, probed.SampleRate, ct).ConfigureAwait(false))
        {
            samples.AddRange(block.Span.ToArray());
        }

        return samples.ToArray();
    }
}
