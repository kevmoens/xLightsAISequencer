using System.Diagnostics;
using System.Runtime.InteropServices;
using XlightsAI.Studio.Audio;

namespace XlightsAI.Studio.Stems;

/// <summary>
/// PHASE-06A Step 044E. Real <see cref="IMixReconstructionSelfConsistencyEvaluator"/>: runs the real
/// <see cref="ISpectrogramStemSeparator"/> (never mocked) against the real original mix, decodes both the
/// real produced stem files and the real original mix via the real, unmodified <see cref="IAudioDecoder"/>
/// mono-downmix pathway -- the identical decode pattern <see cref="SeparationQualityEvaluator"/> already
/// established for this repository -- sums the decoded stems sample-by-sample into a reconstructed mix, and
/// scores it against the real decoded original via the pure, unmodified
/// <see cref="SeparationQualityMetricCalculator.Compute"/>. Wall-clock
/// time (<see cref="Stopwatch"/>) and process memory (<see cref="Process.PeakWorkingSet64"/>) are measured
/// directly around the real separation call -- the exact same <see cref="HardwareProfile"/> measurement
/// pattern <see cref="SeparationQualityEvaluator"/> already established, completely unmodified.
/// </summary>
public sealed class MixReconstructionSelfConsistencyEvaluator : IMixReconstructionSelfConsistencyEvaluator
{
    /// <summary>
    /// Disclosed, deliberate: <see cref="SeparationQualityMetric.Kind"/> requires a <see cref="StemKind"/>,
    /// but this measurement is over the *whole reconstructed mix*, never any one instrument/stem.
    /// <see cref="StemKind.Other"/> is reused here as the least-wrong existing value (no "Mix" member
    /// exists on <see cref="StemKind"/>, and adding one is out of this step's own scope) -- a disclosed,
    /// *third* overload of <see cref="StemKind.Other"/> beyond <see cref="SpectrogramModelBundle"/>'s own
    /// "whole undifferentiated backing track" overload (PHASE-06A.md §3). Callers must read
    /// <see cref="SeparationQualityMetric.ClipId"/> (this constant), never
    /// <see cref="SeparationQualityMetric.Kind"/>, to know what was actually measured.
    /// </summary>
    public const string ReconstructionClipId = "reconstructed-mix-self-consistency";

    private readonly IAudioDecoder _audioDecoder;

    public MixReconstructionSelfConsistencyEvaluator(IAudioDecoder audioDecoder)
    {
        ArgumentNullException.ThrowIfNull(audioDecoder);
        _audioDecoder = audioDecoder;
    }

    /// <summary>
    /// Separates <paramref name="originalMix"/> via the real <paramref name="separator"/>, sums the two
    /// produced stems back into a reconstructed mix, and scores how closely that reconstructed mix matches
    /// the real original input -- an energy/correlation self-consistency check, explicitly never a
    /// separation-quality claim. <see cref="MixReconstructionSelfConsistencyReport.Decision"/> never claims
    /// parity with reference/professional separation quality, regardless of whether the metric passes.
    /// </summary>
    public async Task<MixReconstructionSelfConsistencyReport> EvaluateAsync(
        Guid reportId,
        long createdAtTicks,
        ISpectrogramStemSeparator separator,
        SpectrogramModelBundle modelBundle,
        SpectrogramSeparationRecipe recipe,
        SongAsset originalMix,
        SeparationQualityThresholds thresholds,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(separator);
        ArgumentNullException.ThrowIfNull(modelBundle);
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(originalMix);
        ArgumentNullException.ThrowIfNull(thresholds);
        thresholds.Validate();

        var stopwatch = Stopwatch.StartNew();
        var manifest = await separator.SeparateAsync(originalMix, modelBundle, recipe, progress: null, ct).ConfigureAwait(false);
        stopwatch.Stop();

        if (manifest.Stems.Count == 0)
        {
            throw new SeparationQualityInvalidInputException(
                $"Separator produced zero stems for song '{originalMix.Sha256}' -- cannot measure " +
                "reconstructed-mix self-consistency with no stems to sum.");
        }

        var stemSamples = new List<float[]>(manifest.Stems.Count);
        foreach (var stem in manifest.Stems)
        {
            ct.ThrowIfCancellationRequested();
            var probedStem = await _audioDecoder.ProbeAsync(stem.FilePath, ct).ConfigureAwait(false);
            stemSamples.Add(await DecodeFullMonoAsync(probedStem, ct).ConfigureAwait(false));
        }

        var length = stemSamples[0].Length;
        foreach (var samples in stemSamples)
        {
            if (samples.Length != length)
            {
                throw new SeparationQualityInvalidInputException(
                    $"Produced stems decoded to mismatched sample counts ({samples.Length} vs {length}) " +
                    "-- cannot sum stems of different length into a reconstructed mix.");
            }
        }

        var reconstructed = new float[length];
        foreach (var samples in stemSamples)
        {
            for (var i = 0; i < length; i++)
            {
                reconstructed[i] += samples[i];
            }
        }

        var originalSamples = await DecodeFullMonoAsync(originalMix, ct).ConfigureAwait(false);
        if (originalSamples.Length != reconstructed.Length)
        {
            throw new SeparationQualityInvalidInputException(
                $"Reconstructed mix length ({reconstructed.Length}) does not match the real original mix's " +
                $"own decoded length ({originalSamples.Length}) -- invalid timing boundaries.");
        }

        var metric = SeparationQualityMetricCalculator.Compute(
            ReconstructionClipId, StemKind.Other, reconstructed, originalSamples, thresholds);

        var totalSourceMinutes = originalMix.DurationTicks / 10_000_000.0 / 60.0;
        var hardware = new HardwareProfile(
            OsDescription: RuntimeInformation.OSDescription,
            ProcessArchitecture: RuntimeInformation.ProcessArchitecture.ToString(),
            ProcessorCount: Environment.ProcessorCount,
            TotalAvailableMemoryBytes: GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
            PeakWorkingSetBytes: Process.GetCurrentProcess().PeakWorkingSet64,
            MeasuredWallClockSecondsPerSongMinute: totalSourceMinutes > 0
                ? stopwatch.Elapsed.TotalSeconds / totalSourceMinutes
                : 0.0);

        var decision = metric.PassesThreshold
            ? $"Reconstructed mix (sum of {manifest.Stems.Count} real produced stems from model " +
              $"'{modelBundle.Archive.Id}') self-consistency check passed " +
              $"(SDR {metric.SignalToDistortionRatioDb:F2} dB >= {thresholds.MinSignalToDistortionRatioDb} dB, " +
              $"correlation {metric.CorrelationCoefficient:F4} >= {thresholds.MinCorrelationCoefficient}). " +
              "This is a reconstruction self-consistency proxy only: it measures whether summing the real " +
              "produced stems reproduces the real original input mix. It does NOT measure separation " +
              "quality and makes no claim of parity with reference or professional separation."
            : $"Reconstructed mix (sum of {manifest.Stems.Count} real produced stems from model " +
              $"'{modelBundle.Archive.Id}') self-consistency check did not meet the declared threshold " +
              $"(measured SDR {metric.SignalToDistortionRatioDb:F2} dB, correlation " +
              $"{metric.CorrelationCoefficient:F4}; required SDR >= {thresholds.MinSignalToDistortionRatioDb} dB, " +
              $"correlation >= {thresholds.MinCorrelationCoefficient}). This is a reconstruction " +
              "self-consistency proxy only: it does NOT measure separation quality and makes no claim of " +
              "parity with reference or professional separation.";

        return new MixReconstructionSelfConsistencyReport(
            reportId, createdAtTicks, modelBundle.Archive.Id, recipe.RecipeVersion, metric, hardware, decision);
    }

    private async Task<float[]> DecodeFullMonoAsync(SongAsset probed, CancellationToken ct)
    {
        var samples = new List<float>(probed.SampleCount is > 0 and < int.MaxValue ? (int)probed.SampleCount : 0);
        await foreach (var block in _audioDecoder.DecodeMonoAsync(probed, probed.SampleRate, ct).ConfigureAwait(false))
        {
            samples.AddRange(block.Span.ToArray());
        }

        return samples.ToArray();
    }
}
