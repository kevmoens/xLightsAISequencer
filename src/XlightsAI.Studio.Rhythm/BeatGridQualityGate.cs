namespace XlightsAI.Studio.Rhythm;

/// <summary>PHASE-05 Step 037 — real implementation of <see cref="IBeatGridQualityGate"/>. See the interface's own remarks for the overall design.</summary>
public sealed class BeatGridQualityGate : IBeatGridQualityGate
{
    /// <summary>Burned into no persisted artifact (this report is never cached), but kept as an inspectable identity for this gate's own algorithm, matching every other analyzer's <c>Version</c> convention in this phase.</summary>
    public const string Version = "XlightsAI.Studio.Rhythm.BeatGridQualityGate/1.0.0";

    private const long TicksPerSecond = 10_000_000L;

    public BeatGridQualityReport Evaluate(BeatGrid grid, BeatGridQualityGateSettings settings)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.LowConfidenceThreshold is < 0.0 or > 1.0)
        {
            throw new BeatGridQualityGateInvalidException(grid.SongId, $"LowConfidenceThreshold ({settings.LowConfidenceThreshold}) must be in [0, 1].");
        }

        if (settings.DeviationHistogramBucketTicks <= 0)
        {
            throw new BeatGridQualityGateInvalidException(grid.SongId, $"DeviationHistogramBucketTicks ({settings.DeviationHistogramBucketTicks}) must be positive.");
        }

        var beatCount = grid.Beats.Length;

        // "Silence... must be handled explicitly": a grid with no beats cannot support a confident
        // result by definition — reported as an explicit, disclosed warning, never an exception.
        if (beatCount == 0)
        {
            return new BeatGridQualityReport(
                BeatGridVersionId: grid.VersionId,
                CorrectionVersion: grid.CorrectionVersion,
                BeatCount: 0,
                FirstBeatOffsetTicks: null,
                OverallConfidence: 0.0,
                IsLowConfidence: true,
                TimingDeviationHistogram: [],
                Warnings: ["No beats were detected in this grid; no pulses can be generated confidently until beats are added via manual correction."]);
        }

        var ordered = grid.Beats.OrderBy(b => b.TimeTicks).ToArray();
        var overallConfidence = ordered.Average(EffectiveConfidence);
        var isLowConfidence = overallConfidence < settings.LowConfidenceThreshold;

        var warnings = new List<string>();
        if (isLowConfidence)
        {
            warnings.Add($"Overall confidence {overallConfidence:P0} is below the configured threshold {settings.LowConfidenceThreshold:P0}; manual correction (via IBeatGridEditor) is recommended before relying on this grid for a full-song export.");
        }

        return new BeatGridQualityReport(
            BeatGridVersionId: grid.VersionId,
            CorrectionVersion: grid.CorrectionVersion,
            BeatCount: beatCount,
            FirstBeatOffsetTicks: ordered[0].TimeTicks,
            OverallConfidence: overallConfidence,
            IsLowConfidence: isLowConfidence,
            TimingDeviationHistogram: BuildHistogram(ordered, grid.SelectedTempo, settings.DeviationHistogramBucketTicks),
            Warnings: warnings);
    }

    private static double EffectiveConfidence(BeatMarker beat) =>
        beat.UserCorrected ? BeatGridEditor.UserCorrectedConfidence : beat.Confidence;

    private static IReadOnlyList<TimingDeviationBucket> BuildHistogram(BeatMarker[] ordered, TempoHypothesis? selectedTempo, long bucketTicks)
    {
        // No meaningful "ideal" spacing to deviate from with fewer than 2 beats or no selected tempo
        // (e.g. a tempo analysis built from silence) — an explicit, disclosed empty histogram.
        if (ordered.Length < 2 || selectedTempo is null)
        {
            return [];
        }

        var periodTicks = (long)Math.Round(60.0 / selectedTempo.Bpm * TicksPerSecond);
        if (periodTicks <= 0)
        {
            return [];
        }

        var anchorTicks = ordered[0].TimeTicks;
        var counts = new SortedDictionary<long, int>();

        for (var i = 1; i < ordered.Length; i++)
        {
            var expectedTicks = anchorTicks + (i * periodTicks);
            var deviationTicks = Math.Abs(ordered[i].TimeTicks - expectedTicks);
            var bucketIndex = deviationTicks / bucketTicks;
            counts[bucketIndex] = counts.GetValueOrDefault(bucketIndex) + 1;
        }

        return [.. counts.Select(kvp => new TimingDeviationBucket(kvp.Key * bucketTicks, (kvp.Key + 1) * bucketTicks, kvp.Value))];
    }
}
