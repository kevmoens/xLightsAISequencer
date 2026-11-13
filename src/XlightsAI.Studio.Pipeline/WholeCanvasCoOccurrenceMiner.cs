using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Pipeline;

/// <summary>
/// Build-plan Phase 25 Part B.4/C: for every real time window where a structurally-classified
/// whole-canvas group has an active effect in one corpus song, records which of that song's
/// Foundation/Subject/Accent-classified groups/models also have an active effect overlapping that same
/// window, plus the window's own section kind and energy percentile — the co-occurrence signal
/// <c>BandEnergyGroupRoleMapper.ComputeSectionProfiles</c>/<c>ComputeSongWideShare</c> (per-section band
/// *share*) and <c>EmphasisContrastPlanner</c> (single-instant brightness contrast) neither answer.
///
/// Classification here deliberately <b>duplicates</b> <c>XlightsAI.Studio.Director.BandEnergyGroupRoleMapper</c>'s
/// coverage-ratio-then-keyword rule rather than calling it directly: <c>Director</c> depends on
/// <c>Pipeline</c> (not the reverse), so <c>Pipeline</c> cannot reference <c>Director</c>'s
/// <c>BandRole</c>/<c>BandEnergyGroupRoleMapper</c> without a circular project reference. Keep
/// <see cref="WholeCanvasCoverageThreshold"/> and the three keyword lists in sync with that file if
/// either changes.
///
/// <b>Build-plan Phase 25 Part C real-corpus finding (confirmed against the real 31-sequence
/// <c>C:\xLights</c> training run, 2026-11-11):</b> role lookups are keyed by name ALONE, never by the
/// owning <c>&lt;Element type="..."&gt;</c> value, even though a real layout's own <c>DisplayGraph</c>
/// cleanly separates a <see cref="DisplayModelGroup"/> from a <see cref="DisplayModel"/>. A real exported
/// <c>.xsq</c> (confirmed across every sequence in that corpus, zero exceptions) never writes
/// <c>type="group"</c> at all -- every addressable grid row, whether it is a true model or a Model Group
/// dragged into the sequencer, is written as <c>type="model"</c> (<c>"timing"</c> is the only other value
/// ever seen). An earlier version of this miner keyed whole-canvas membership as a (type, name) pair --
/// <c>("group", "everything")</c> -- which could never match a real aligned effect's own
/// <c>("model", "everything")</c> key, so a catch-all group's own directly-placed effect (confirmed to
/// exist in the real corpus: e.g. "ALL_Display"/"ALL_House Props"/"ALL_Roof" are real <c>&lt;modelGroup&gt;</c>
/// entries with effects placed directly on them) was silently invisible to this miner no matter how many
/// real songs used one -- producing <c>WindowSampleCount: 0</c>/<c>SongsWithWholeCanvasCount: 0</c> even
/// though <c>CorpusTrainingRunner</c>'s own independent <see cref="CoverageRatioDistribution"/> (pure
/// group-coverage math, no effect-alignment lookup at all) found exactly one qualifying catch-all group
/// per song across all 30 songs with a layout. Fixed by dropping the type half of the key entirely; a
/// name collision between a real model and a real group of the same name is not a real-world scenario
/// xLights itself allows (both share one flat addressable-element namespace).
///
/// Every output <see cref="WholeCanvasCoOccurrenceSample"/> field is a boolean/number/enum — never a
/// <c>ModelName</c>/<c>GroupName</c>/<c>EffectName</c> string from the source sequence, the same
/// licensing-boundary discipline <see cref="CorpusTrainingReport"/>'s own doc comment states.
/// </summary>
public static class WholeCanvasCoOccurrenceMiner
{
    /// <summary>
    /// Mirrors <c>XlightsAI.Studio.Director.BandEnergyGroupRoleMapper.WholeCanvasCoverageThreshold</c> (see
    /// class doc comment for why this is a duplicate, not a shared reference) -- see that constant's own
    /// doc comment for the real measured distribution this value is calibrated against.
    /// </summary>
    public const double WholeCanvasCoverageThreshold = 0.90;

    private static readonly string[] AccentKeywords = ["minitree", "mini tree", "icicle", "strand", "star", "sparkle", "spike", "snowflake"];
    private static readonly string[] SubjectKeywords = ["megatree", "mega tree", "tree", "arch", "matrix"];
    private static readonly string[] FoundationKeywords = ["outline", "gutter", "roof", "horizontal", "eave", "ridge", "fence"];

    private enum MirroredRole
    {
        Foundation,
        Subject,
        Accent,
        WholeCanvas,
    }

    /// <summary>
    /// Mines one song's own already-analyzed effects/layout for whole-canvas co-occurrence samples.
    /// Returns an empty list when this song's layout carries no group/model that classifies as
    /// whole-canvas (most songs), or when the layout has no addressable pixels at all.
    /// </summary>
    public static IReadOnlyList<WholeCanvasCoOccurrenceSample> Mine(CorpusSongAnalysis song, DisplayGraph layout)
    {
        ArgumentNullException.ThrowIfNull(song);
        ArgumentNullException.ThrowIfNull(layout);

        var totalPixels = layout.Models.Sum(m => (long)Math.Max(0, m.NodeCount));
        if (totalPixels <= 0)
        {
            return [];
        }

        var modelPixelsByName = layout.Models.ToDictionary(m => m.XlightsName, m => (long)Math.Max(0, m.NodeCount), StringComparer.Ordinal);

        var roleByKey = new Dictionary<string, MirroredRole>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in layout.Groups)
        {
            var memberPixels = group.Members
                .Where(m => m.Kind == DisplayGroupMemberKind.Model)
                .Sum(m => modelPixelsByName.GetValueOrDefault(m.RawName, 0));
            if (Classify(group.Name, memberPixels, totalPixels) is { } role)
            {
                roleByKey[group.Name] = role;
            }
        }

        foreach (var model in layout.Models)
        {
            if (Classify(model.XlightsName, Math.Max(0, model.NodeCount), totalPixels) is { } role)
            {
                roleByKey[model.XlightsName] = role;
            }
        }

        if (!roleByKey.ContainsValue(MirroredRole.WholeCanvas))
        {
            return [];
        }

        var foundationIntervals = IntervalsForRole(song, roleByKey, MirroredRole.Foundation);
        var subjectIntervals = IntervalsForRole(song, roleByKey, MirroredRole.Subject);
        var accentIntervals = IntervalsForRole(song, roleByKey, MirroredRole.Accent);

        var bars = song.FingerprintSet.Bars.OrderBy(b => b.BarIndex).ToList();
        var energyValues = bars.Select(b => b.Features.Count > 0 ? b.Features[0] : 0.0).ToList();
        var barsByIndex = bars.ToDictionary(b => b.BarIndex);
        var averageBarDurationMs = AverageBarDurationMs(bars);

        var samples = new List<WholeCanvasCoOccurrenceSample>();
        foreach (var aligned in song.AlignedEffects)
        {
            if (!roleByKey.TryGetValue(aligned.Effect.ModelName, out var role) || role != MirroredRole.WholeCanvas)
            {
                continue;
            }

            var start = aligned.Effect.StartTimeMs;
            var end = aligned.Effect.EndTimeMs;

            double? durationBars = averageBarDurationMs > 0 ? (end - start) / averageBarDurationMs : null;
            double? energyPercentile = aligned.StartBarIndex >= 0 && barsByIndex.TryGetValue(aligned.StartBarIndex, out var bar)
                ? PercentileRank(energyValues, bar.Features.Count > 0 ? bar.Features[0] : 0.0)
                : null;

            samples.Add(new WholeCanvasCoOccurrenceSample(
                aligned.SectionKind,
                durationBars,
                energyPercentile,
                Overlaps(foundationIntervals, start, end),
                Overlaps(subjectIntervals, start, end),
                Overlaps(accentIntervals, start, end)));
        }

        return samples;
    }

    private static List<(long Start, long End)> IntervalsForRole(CorpusSongAnalysis song, Dictionary<string, MirroredRole> roleByKey, MirroredRole role)
    {
        var keys = roleByKey.Where(kv => kv.Value == role).Select(kv => kv.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (keys.Count == 0)
        {
            return [];
        }

        return song.AlignedEffects
            .Where(a => keys.Contains(a.Effect.ModelName))
            .Select(a => (a.Effect.StartTimeMs, a.Effect.EndTimeMs))
            .ToList();
    }

    private static bool Overlaps(List<(long Start, long End)> intervals, long start, long end) =>
        intervals.Any(i => start < i.End && i.Start < end);

    private static MirroredRole? Classify(string name, long memberPixels, long totalPixels)
    {
        if (totalPixels > 0 && memberPixels / (double)totalPixels >= WholeCanvasCoverageThreshold)
        {
            return MirroredRole.WholeCanvas;
        }

        if (AccentKeywords.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase)))
        {
            return MirroredRole.Accent;
        }

        if (SubjectKeywords.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase)))
        {
            return MirroredRole.Subject;
        }

        if (FoundationKeywords.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase)))
        {
            return MirroredRole.Foundation;
        }

        return null;
    }

    /// <summary>Median bar duration in milliseconds across this song's own real bar fingerprints, or <c>0</c> when fewer than one bar has a measurable (non-degenerate) window.</summary>
    private static double AverageBarDurationMs(IReadOnlyList<BarFingerprint> bars)
    {
        var durationsTicks = bars.Select(b => b.Window.EndTicks - b.Window.StartTicks).Where(d => d > 0).ToList();
        return durationsTicks.Count == 0 ? 0 : durationsTicks.Average() / 10_000.0;
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
}

/// <summary>
/// One real time window where a whole-canvas-classified group/model had an active effect in a corpus
/// song, and what co-fired alongside it — aggregate/statistical only (no raw model/effect names), per
/// <see cref="WholeCanvasCoOccurrenceMiner"/>'s own licensing-boundary doc comment.
/// </summary>
/// <param name="SectionKind">The section kind this window's own start time falls inside, or <see langword="null"/> when no section covers it.</param>
/// <param name="DurationBars">This window's own duration, in bars (the whole-canvas effect's own `EndTimeMs - StartTimeMs` divided by this song's median bar duration), or <see langword="null"/> when this song had no measurable bar duration to divide by.</param>
/// <param name="EnergyPercentile">This window's own energy percentile rank (0-1) against this song's own whole bar-energy distribution, or <see langword="null"/> when the window's start bar has no fingerprint.</param>
/// <param name="CoFiresWithFoundation">Whether any Foundation-classified group/model had an effect overlapping this window.</param>
/// <param name="CoFiresWithSubject">Whether any Subject-classified group/model had an effect overlapping this window.</param>
/// <param name="CoFiresWithAccent">Whether any Accent-classified group/model had an effect overlapping this window.</param>
public sealed record WholeCanvasCoOccurrenceSample(
    SectionKind? SectionKind,
    double? DurationBars,
    double? EnergyPercentile,
    bool CoFiresWithFoundation,
    bool CoFiresWithSubject,
    bool CoFiresWithAccent);
