using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Stems;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Pipeline;

/// <summary>
/// PHASE-2b (build plan §2b.9; architecture §8b): builds an <see cref="ExemplarIndex"/> from a corpus
/// of already-aligned song analyses.
///
/// <b>Granularity choice, disclosed:</b> architecture §8b describes indexing "each corpus section," but
/// this builder indexes by <see cref="PhraseCandidate"/> instead of <see cref="SectionLabelCandidate"/>.
/// Real measurement against this corpus (see the Phase 2b final report) found
/// <see cref="SectionBoundaryDetector"/>'s default novelty threshold produces very few boundaries on
/// several real songs — one song in this corpus classified as a *single* whole-song
/// <see cref="SectionKind.Unknown"/> "section." <see cref="PhraseSegmenter"/>'s bar-hypothesis recipe, by
/// contrast, reliably produces consistent ~8-bar windows for every song regardless of how well section
/// labeling converged (it falls back to pure bar-count phrasing when melodic cadence snapping is
/// unavailable — see <see cref="CorpusSongAnalyzer"/>'s own disclosed melody gap). Phrases are a strictly
/// finer, always-populated granularity that a labeled section's own <see cref="SectionKind"/> is still
/// attached to (<see cref="MajoritySectionKind"/>), so no information from architecture §8b's named
/// fingerprint shape is lost — only the grouping unit changes.
/// </summary>
public static class ExemplarIndexBuilder
{
    public static ExemplarIndex Build(IReadOnlyList<CorpusSongAnalysis> songs)
    {
        ArgumentNullException.ThrowIfNull(songs);

        var entries = new List<ExemplarEntry>();

        foreach (var song in songs.Where(s => s.Entry.Provenance != CorpusProvenanceTag.ExternalReference))
        {
            var bars = song.FingerprintSet.Bars.OrderBy(b => b.BarIndex).ToList();
            if (bars.Count == 0)
            {
                continue;
            }

            var energyValues = bars.Select(b => b.Features.Count > 0 ? b.Features[0] : 0.0).ToList();
            var tempoBpm = song.BeatGrid.SelectedTempo?.Bpm ?? 0.0;

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

                var fingerprint = new ExemplarFingerprint(
                    SongTitle: song.Entry.SongTitle,
                    Provenance: song.Entry.Provenance,
                    StartBarIndex: phrase.StartBarIndex,
                    EndBarIndexExclusive: phrase.EndBarIndexExclusive,
                    TempoBpm: tempoBpm,
                    EnergyPercentile: percentile,
                    SectionKind: sectionKind,
                    BandBalance: MeanBandBalance(song.BandEnergy, phrase.StartBarIndex, phrase.EndBarIndexExclusive, bars),
                    VocalActivityHint: VocalActivityWithinWindow(song.VocalActivity, phrase.StartBarIndex, phrase.EndBarIndexExclusive, bars));

                var effects = song.AlignedEffects
                    .Where(a => a.StartBarIndex >= phrase.StartBarIndex && a.StartBarIndex < phrase.EndBarIndexExclusive)
                    .OrderBy(a => a.Effect.StartTimeMs)
                    .ToList();

                entries.Add(new ExemplarEntry(fingerprint, effects));
            }
        }

        return new ExemplarIndex(DateTimeOffset.UtcNow, entries);
    }

    /// <summary>
    /// Build-plan Phase 16 §2c: this phrase window's mean normalized [bass, mid, high] energy share,
    /// averaged over its own real bar-tick range via <see cref="BandEnergyEnvelope.AverageInWindow"/>, or
    /// <see langword="null"/> when no band-energy analysis is available (an entry mined before this phase
    /// shipped) or the window has no bars to derive a tick range from.
    /// </summary>
    private static IReadOnlyList<double>? MeanBandBalance(BandEnergyEnvelope? bandEnergy, int startBarIndex, int endBarIndexExclusive, IReadOnlyList<BarFingerprint> bars)
    {
        if (bandEnergy is null || !TryGetWindowTicks(bars, startBarIndex, endBarIndexExclusive, out var startTicks, out var endTicks))
        {
            return null;
        }

        var (bass, mid, high) = bandEnergy.AverageInWindow(startTicks, endTicks);
        return [bass, mid, high];
    }

    /// <summary>
    /// Build-plan Phase 16 §2c: whether this phrase window's own real bar-tick range overlaps any
    /// detected vocal-active region, or <see langword="null"/> when no vocal-activity analysis is
    /// available (an entry mined before this phase shipped) or the window has no bars to derive a tick
    /// range from.
    /// </summary>
    private static bool? VocalActivityWithinWindow(VocalActivityEnvelope? vocalActivity, int startBarIndex, int endBarIndexExclusive, IReadOnlyList<BarFingerprint> bars)
    {
        if (vocalActivity is null || !TryGetWindowTicks(bars, startBarIndex, endBarIndexExclusive, out var startTicks, out var endTicks))
        {
            return null;
        }

        return vocalActivity.ActiveRegions.Any(r => r.StartTicks < endTicks && r.EndTicks > startTicks);
    }

    private static bool TryGetWindowTicks(IReadOnlyList<BarFingerprint> bars, int startBarIndex, int endBarIndexExclusive, out long startTicks, out long endTicks)
    {
        var windowBars = bars.Where(b => b.BarIndex >= startBarIndex && b.BarIndex < endBarIndexExclusive).ToList();
        if (windowBars.Count == 0)
        {
            startTicks = 0;
            endTicks = 0;
            return false;
        }

        startTicks = windowBars.Min(b => b.Window.StartTicks);
        endTicks = windowBars.Max(b => b.Window.EndTicks);
        return true;
    }

    private static double PercentileRank(IReadOnlyList<double> population, double value)
    {
        if (population.Count == 0)
        {
            return 0;
        }

        var countAtOrBelow = population.Count(v => v <= value);
        return countAtOrBelow / (double)population.Count;
    }

    private static SectionKind MajoritySectionKind(SectionLabelAnalysis labels, int startBarIndex, int endBarIndexExclusive)
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

        return counts.Count == 0 ? SectionKind.Unknown : counts.OrderByDescending(kv => kv.Value).First().Key;
    }
}
