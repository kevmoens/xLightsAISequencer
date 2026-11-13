using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Pipeline;

/// <summary>
/// Build-plan Phase 25 Part C §3 bullet 3: training-only pass, parallel to
/// <see cref="WholeCanvasCoOccurrenceMiner"/>, that generalizes <see cref="MultiLayerStackMiner"/>'s own
/// clustering logic (via the shared <see cref="LayerComplexityAnalyzer"/>) across the external training
/// corpus -- counting how many models/groups reach 3+/4+/5+ simultaneous layers, broken down by section
/// kind and duration, without ever recording which model/effect produced them. Deliberately not restricted
/// to <c>TreeMatrixPropNameConvention.IsTreeOrMatrixName</c> models -- "any model/group," per Part D.1's
/// own generalization of <see cref="MultiLayerStackMiner"/>.
///
/// Every output <see cref="LayerComplexitySample"/> field is a count/enum/number -- never a
/// <c>ModelName</c>/<c>EffectName</c> string from the source sequence, the same licensing-boundary
/// discipline <see cref="CorpusTrainingReport"/>'s own doc comment states.
/// </summary>
public static class LayerComplexityMiner
{
    /// <summary>
    /// Mines every model/group's own already-aligned effects for layer-complexity windows reaching at
    /// least <see cref="LayerComplexityAnalyzer.DefaultMinDistinctLayers"/> (3) distinct concurrent
    /// layers -- the lowest of the three report tiers (3+/4+/5+), so a single pass captures every window
    /// a higher tier would also count.
    /// </summary>
    public static IReadOnlyList<LayerComplexitySample> Mine(CorpusSongAnalysis song)
    {
        ArgumentNullException.ThrowIfNull(song);

        var bars = song.FingerprintSet.Bars.OrderBy(b => b.BarIndex).ToList();
        var averageBarDurationMs = AverageBarDurationMs(bars);

        var samples = new List<LayerComplexitySample>();
        foreach (var modelGroup in song.AlignedEffects.GroupBy(a => a.Effect.ModelName, StringComparer.Ordinal))
        {
            foreach (var window in LayerComplexityAnalyzer.FindLayerWindows(modelGroup.ToList(), LayerComplexityAnalyzer.DefaultMinDistinctLayers))
            {
                double? durationBars = averageBarDurationMs > 0
                    ? (window.ClusterEnd - window.ClusterStart) / averageBarDurationMs
                    : null;

                samples.Add(new LayerComplexitySample(window.PeakConcurrentLayerCount, window.DominantSectionKind, durationBars));
            }
        }

        return samples;
    }

    /// <summary>Same median-bar-duration computation <see cref="WholeCanvasCoOccurrenceMiner"/> uses -- see that type for why this is a median, not a mean, across this song's own real bar windows.</summary>
    private static double AverageBarDurationMs(IReadOnlyList<BarFingerprint> bars)
    {
        var durationsTicks = bars.Select(b => b.Window.EndTicks - b.Window.StartTicks).Where(d => d > 0).ToList();
        return durationsTicks.Count == 0 ? 0 : durationsTicks.Average() / 10_000.0;
    }
}

/// <summary>
/// One real time window where a model/group's own effects reached at least a 3-distinct-layer peak in a
/// corpus song -- aggregate/statistical only (no raw model/effect name), per
/// <see cref="LayerComplexityMiner"/>'s own licensing-boundary doc comment.
/// </summary>
/// <param name="PeakConcurrentLayerCount">The real peak distinct-layer count this window reached (always >= 3 -- the lowest report tier).</param>
/// <param name="SectionKind">The section kind most of this window's placements started inside, or <see langword="null"/> when none/mixed.</param>
/// <param name="DurationBars">This window's own duration in bars, or <see langword="null"/> when this song had no measurable bar duration to divide by.</param>
public sealed record LayerComplexitySample(int PeakConcurrentLayerCount, SectionKind? SectionKind, double? DurationBars);
