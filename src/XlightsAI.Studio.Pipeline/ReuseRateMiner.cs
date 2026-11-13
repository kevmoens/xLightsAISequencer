using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Pipeline;

/// <summary>
/// Build-plan Phase 25 Part C §3 bullet 4: training-only pass, parallel to
/// <see cref="WholeCanvasCoOccurrenceMiner"/>/<see cref="LayerComplexityMiner"/>, that measures how often
/// a structurally similar passage recurs <b>within one real song</b> -- "same phrase-length window, same
/// section kind, similar energy percentile" per the ask. Feeds Part E's reuse-budget gauges (not built
/// yet) so that range is grounded in measured behavior, not a guess. Every output field is a
/// count/enum/number -- never a <c>ModelName</c>/<c>EffectName</c> string, same licensing-boundary
/// discipline <see cref="CorpusTrainingReport"/>'s own doc comment states.
/// </summary>
public static class ReuseRateMiner
{
    /// <summary>
    /// Buckets this song's own phrase windows by (phrase bar length, majority section kind, energy
    /// tier) -- the same "structurally similar passage" signature the ask describes -- and returns one
    /// <see cref="ReuseRateBucketSample"/> per distinct bucket, carrying how many of this song's own
    /// phrase windows shared that signature (1 = never recurred, 2+ = recurred that many times).
    /// </summary>
    public static IReadOnlyList<ReuseRateBucketSample> Mine(CorpusSongAnalysis song)
    {
        ArgumentNullException.ThrowIfNull(song);

        var bars = song.FingerprintSet.Bars.OrderBy(b => b.BarIndex).ToList();
        if (bars.Count == 0)
        {
            return [];
        }

        var energyValues = bars.Select(b => b.Features.Count > 0 ? b.Features[0] : 0.0).ToList();

        var windows = new List<(int PhraseBars, SectionKind? SectionKind, string EnergyTier)>();
        foreach (var phrase in song.Phrases.Phrases)
        {
            var phraseBars = bars.Where(b => b.BarIndex >= phrase.StartBarIndex && b.BarIndex < phrase.EndBarIndexExclusive).ToList();
            if (phraseBars.Count == 0)
            {
                continue;
            }

            var meanEnergy = phraseBars.Average(b => b.Features.Count > 0 ? b.Features[0] : 0.0);
            var percentile = PercentileRank(energyValues, meanEnergy);
            var sectionKind = MajoritySectionKind(song.Labels, phrase.StartBarIndex, phrase.EndBarIndexExclusive);

            windows.Add((phrase.EndBarIndexExclusive - phrase.StartBarIndex, sectionKind, EnergyTierFor(percentile)));
        }

        return windows
            .GroupBy(w => (w.PhraseBars, w.SectionKind, w.EnergyTier))
            .Select(g => new ReuseRateBucketSample(g.Count(), g.Key.SectionKind))
            .ToList();
    }

    /// <summary>
    /// Build-plan Phase 25 Part E.2: the same "same phrase-length window, same section kind, similar
    /// energy percentile" bucketing this miner uses to detect in-song reuse, exposed publicly so
    /// <see cref="RepeatablePieceGrouper"/> can cluster real <see cref="ExemplarIndex"/> entries into the
    /// same structural buckets at generation time, rather than re-deriving its own notion of "similar
    /// energy percentile."
    /// </summary>
    public static string EnergyTierFor(double percentile) => percentile switch
    {
        < 1.0 / 3 => "Low",
        < 2.0 / 3 => "Mid",
        _ => "High",
    };

    private static double PercentileRank(IReadOnlyList<double> population, double value)
    {
        if (population.Count == 0)
        {
            return 0;
        }

        var countAtOrBelow = population.Count(v => v <= value);
        return countAtOrBelow / (double)population.Count;
    }

    private static SectionKind? MajoritySectionKind(SectionLabelAnalysis labels, int startBarIndex, int endBarIndexExclusive)
    {
        var counts = new Dictionary<SectionKind, int>();
        foreach (var label in labels.Labels)
        {
            var overlapStart = Math.Max(label.StartBarIndex, startBarIndex);
            var overlapEnd = Math.Min(label.EndBarIndexExclusive, endBarIndexExclusive);
            var overlap = overlapEnd - overlapStart;
            if (overlap > 0)
            {
                counts[label.Kind] = counts.GetValueOrDefault(label.Kind) + overlap;
            }
        }

        return counts.Count == 0 ? null : counts.OrderByDescending(kv => kv.Value).First().Key;
    }
}

/// <summary>
/// One distinct "structurally similar passage" signature (phrase bar length + section kind + energy
/// tier) within one real song, and how many of that song's own phrase windows shared it.
/// </summary>
/// <param name="OccurrenceCount">How many of this song's own phrase windows shared this exact signature -- 1 means this passage never recurred, 2+ means it did, that many times.</param>
/// <param name="SectionKind">The majority section kind shared by every window in this bucket, or <see langword="null"/> when none/mixed.</param>
public sealed record ReuseRateBucketSample(int OccurrenceCount, SectionKind? SectionKind);
