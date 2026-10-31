using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Pipeline;

/// <summary>PHASE-2b (build plan §2b.7): aligns parsed effects onto an already-analyzed song's beat/bar/section grid. See <see cref="CorpusSongAnalyzer"/> for the pipeline that produces its inputs.</summary>
public static class EffectAligner
{
    private const long TicksPerMillisecond = 10_000L;

    public static IReadOnlyList<AlignedEffectInstance> Align(
        IReadOnlyList<ParsedEffectInstance> effects,
        BeatGrid beatGrid,
        SongFingerprintSet fingerprints,
        SectionLabelAnalysis labels,
        PhraseSegmentationAnalysis phrases)
    {
        ArgumentNullException.ThrowIfNull(effects);
        ArgumentNullException.ThrowIfNull(beatGrid);
        ArgumentNullException.ThrowIfNull(fingerprints);
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(phrases);

        var bars = fingerprints.Bars.OrderBy(b => b.BarIndex).ToList();
        var beats = beatGrid.Beats.OrderBy(b => b.TimeTicks).ToList();

        var result = new List<AlignedEffectInstance>(effects.Count);
        foreach (var effect in effects)
        {
            var startTicks = effect.StartTimeMs * TicksPerMillisecond;

            var barIndex = FindBarIndex(bars, startTicks);
            var (beatWithinBar, offsetMs) = FindBeat(beats, startTicks);
            var sectionKind = barIndex >= 0 ? FindSectionKind(labels, barIndex) : null;
            var (phraseStart, phraseBars) = barIndex >= 0 ? FindPhrase(phrases, barIndex) : (null, null);

            result.Add(new AlignedEffectInstance(effect, barIndex, beatWithinBar, offsetMs, sectionKind, phraseStart, phraseBars));
        }

        return result;
    }

    private static int FindBarIndex(IReadOnlyList<BarFingerprint> bars, long startTicks)
    {
        for (var i = bars.Count - 1; i >= 0; i--)
        {
            if (bars[i].Window.StartTicks <= startTicks)
            {
                return bars[i].BarIndex;
            }
        }

        return -1;
    }

    private static (int BeatWithinBar, double? OffsetMs) FindBeat(IReadOnlyList<BeatMarker> beats, long startTicks)
    {
        if (beats.Count == 0)
        {
            return (-1, null);
        }

        BeatMarker? containing = null;
        BeatMarker? nearest = null;
        var nearestDistance = long.MaxValue;

        foreach (var beat in beats)
        {
            if (beat.TimeTicks <= startTicks)
            {
                containing = beat;
            }

            var distance = Math.Abs(beat.TimeTicks - startTicks);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = beat;
            }
        }

        var beatWithinBar = containing?.BeatWithinBar ?? -1;
        var offsetMs = nearest is null ? (double?)null : (startTicks - nearest.TimeTicks) / (double)TicksPerMillisecond;
        return (beatWithinBar, offsetMs);
    }

    private static SectionKind? FindSectionKind(SectionLabelAnalysis labels, int barIndex)
    {
        foreach (var label in labels.Labels)
        {
            if (barIndex >= label.StartBarIndex && barIndex < label.EndBarIndexExclusive)
            {
                return label.Kind;
            }
        }

        return null;
    }

    private static (int? StartBarIndex, int? Bars) FindPhrase(PhraseSegmentationAnalysis phrases, int barIndex)
    {
        foreach (var phrase in phrases.Phrases)
        {
            if (barIndex >= phrase.StartBarIndex && barIndex < phrase.EndBarIndexExclusive)
            {
                return (phrase.StartBarIndex, phrase.Bars);
            }
        }

        return (null, null);
    }
}
