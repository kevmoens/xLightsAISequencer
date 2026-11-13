using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Pipeline;

/// <summary>
/// Build-plan Phase 25 Part D.1/D.2: the single, shared "how many distinct effect layers are genuinely
/// stacked on this model right now" primitive. Generalizes the clustering/peak-concurrency sweep
/// <see cref="MultiLayerStackMiner.FindStackOccurrences"/> (Phase 20) used to own privately -- both that
/// miner (now a thin wrapper that also extracts the real effect-name stack at the peak instant) and the
/// new training-only layer-complexity mining (Part C §3 bullet 3) and <see cref="ExemplarIndexBuilder"/>'s
/// own per-phrase-window complexity field (Part D.2) now share this one implementation rather than three
/// drifting copies of the same sweep.
///
/// Unlike <see cref="CraftRuleInducer.MaxConcurrentOverlap"/> (which counts raw overlapping intervals),
/// this counts <i>distinct</i> <see cref="XlightsAI.Studio.Xlights.ParsedEffectInstance.LayerIndex"/>
/// values simultaneously active -- two placements on the same layer that happen to overlap (e.g. a
/// re-triggered effect) are one layer, not two, which is the real "how many layers deep" measure a
/// multi-layer stack needs.
/// </summary>
public static class LayerComplexityAnalyzer
{
    /// <summary>Build-plan Phase 25 Part D.1: the complexity bar raised from 2+ to 3+ distinct layers per the ask -- "The threshold today is 2+ layers, not 3+."</summary>
    public const int DefaultMinDistinctLayers = 3;

    /// <summary>
    /// One real time-connected run of a single model's own effect placements whose peak instant reaches
    /// at least the requested distinct-layer minimum.
    /// </summary>
    /// <param name="ClusterStart">First placement's own start time across the whole time-connected run.</param>
    /// <param name="ClusterEnd">Last placement's own end time across the whole time-connected run.</param>
    /// <param name="PeakSegmentStart">Start of the single busiest (maximal distinct-layer-count) instant within the run.</param>
    /// <param name="PeakSegmentEnd">End of that same busiest instant.</param>
    /// <param name="PeakConcurrentLayerCount">The real distinct-layer count active at the busiest instant.</param>
    /// <param name="DominantSectionKind">The <see cref="SectionKind"/> most of this run's placements started inside, or <see langword="null"/> when none/mixed.</param>
    /// <param name="ClusterEffects">Every placement in the whole time-connected run (not just the ones active at the peak instant) -- needed by callers (e.g. duty-cycle calculation) that reason over the run's full span.</param>
    /// <param name="ActiveAtPeak">Only the placements genuinely active during <paramref name="PeakSegmentStart"/>-<paramref name="PeakSegmentEnd"/>.</param>
    public sealed record LayerWindow(
        long ClusterStart,
        long ClusterEnd,
        long PeakSegmentStart,
        long PeakSegmentEnd,
        int PeakConcurrentLayerCount,
        SectionKind? DominantSectionKind,
        IReadOnlyList<AlignedEffectInstance> ClusterEffects,
        IReadOnlyList<AlignedEffectInstance> ActiveAtPeak);

    /// <summary>
    /// Clusters one model's own aligned effects into maximal time-connected runs (adjacent/overlapping
    /// placements merged), and yields one <see cref="LayerWindow"/> per run whose peak distinct-layer
    /// count reaches <paramref name="minDistinctLayers"/> -- every other run (never overlapping, or
    /// overlapping but never stacking enough distinct layers) contributes nothing.
    /// </summary>
    public static IReadOnlyList<LayerWindow> FindLayerWindows(IReadOnlyList<AlignedEffectInstance> modelEffects, int minDistinctLayers = DefaultMinDistinctLayers)
    {
        ArgumentNullException.ThrowIfNull(modelEffects);

        var ordered = modelEffects.OrderBy(a => a.Effect.StartTimeMs).ToList();
        var clusters = new List<List<AlignedEffectInstance>>();
        List<AlignedEffectInstance>? current = null;
        var currentEnd = long.MinValue;

        foreach (var instance in ordered)
        {
            if (current is not null && instance.Effect.StartTimeMs < currentEnd)
            {
                current.Add(instance);
                currentEnd = Math.Max(currentEnd, instance.Effect.EndTimeMs);
                continue;
            }

            current = [instance];
            currentEnd = instance.Effect.EndTimeMs;
            clusters.Add(current);
        }

        var windows = new List<LayerWindow>();
        foreach (var cluster in clusters)
        {
            var distinctLayers = cluster.Select(a => a.Effect.LayerIndex).Distinct().Count();
            if (distinctLayers < minDistinctLayers)
            {
                continue;
            }

            var peakConcurrency = PeakConcurrentLayerCount(cluster);
            if (peakConcurrency < minDistinctLayers)
            {
                continue;
            }

            var (segmentStart, segmentEnd, activeAtPeak) = CraftRuleInducer.FindPeakOverlapSegment(
                cluster, a => a.Effect.StartTimeMs, a => a.Effect.EndTimeMs);
            var activeAtPeakDistinctLayers = activeAtPeak.Select(a => a.Effect.LayerIndex).Distinct().Count();
            if (activeAtPeakDistinctLayers < minDistinctLayers)
            {
                continue;
            }

            var dominantSectionKind = cluster
                .Select(a => a.SectionKind)
                .Where(k => k is not null)
                .GroupBy(k => k!.Value)
                .OrderByDescending(g => g.Count())
                .Select(g => (SectionKind?)g.Key)
                .FirstOrDefault();

            windows.Add(new LayerWindow(
                cluster.Min(a => a.Effect.StartTimeMs),
                cluster.Max(a => a.Effect.EndTimeMs),
                segmentStart,
                segmentEnd,
                activeAtPeakDistinctLayers,
                dominantSectionKind,
                cluster,
                activeAtPeak));
        }

        return windows;
    }

    /// <summary>
    /// The real peak distinct-<see cref="XlightsAI.Studio.Xlights.ParsedEffectInstance.LayerIndex"/> count
    /// across every model present in <paramref name="effects"/> (grouped by <c>Effect.ModelName</c> and
    /// evaluated independently, since layers only genuinely "stack" within one model's own effect layers,
    /// never across two different models) -- the single number <see cref="ExemplarIndexBuilder"/> surfaces
    /// as one exemplar window's own <see cref="ExemplarFingerprint.PeakConcurrentLayerCount"/>. Returns
    /// <c>0</c> for an empty or single-layer-only input, never a fabricated minimum.
    /// </summary>
    public static int PeakConcurrentLayerCount(IEnumerable<AlignedEffectInstance> effects)
    {
        ArgumentNullException.ThrowIfNull(effects);

        return effects
            .GroupBy(a => a.Effect.ModelName, StringComparer.Ordinal)
            .Select(g => PeakConcurrentDistinctLayers(g.Select(a => (a.Effect.StartTimeMs, a.Effect.EndTimeMs, a.Effect.LayerIndex))))
            .DefaultIfEmpty(0)
            .Max();
    }

    /// <summary>A plain sweep-line count of the largest number of distinct <c>LayerIndex</c> values ever simultaneously active among <paramref name="intervals"/> -- the single-model building block <see cref="PeakConcurrentLayerCount"/> maxes across every model present.</summary>
    private static int PeakConcurrentDistinctLayers(IEnumerable<(long Start, long End, int LayerIndex)> intervals)
    {
        var events = new List<(long Time, int Delta, int LayerIndex)>();
        foreach (var (start, end, layerIndex) in intervals)
        {
            if (end <= start)
            {
                continue; // Zero/negative-width placement -- not a real interval, never contributes.
            }

            events.Add((start, 1, layerIndex));
            events.Add((end, -1, layerIndex));
        }

        if (events.Count == 0)
        {
            return 0;
        }

        // A -1 (end, exclusive) at the same instant as a +1 (start) must apply first, matching
        // CraftRuleInducer.MaxConcurrentOverlap's own tie-break -- two back-to-back (never truly
        // overlapping) placements on the same layer must never misread as one instant of double occupancy.
        var refCounts = new Dictionary<int, int>();
        var max = 0;
        foreach (var e in events.OrderBy(e => e.Time).ThenBy(e => e.Delta))
        {
            refCounts.TryGetValue(e.LayerIndex, out var count);
            count += e.Delta;
            if (count <= 0)
            {
                refCounts.Remove(e.LayerIndex);
            }
            else
            {
                refCounts[e.LayerIndex] = count;
            }

            max = Math.Max(max, refCounts.Count);
        }

        return max;
    }
}
