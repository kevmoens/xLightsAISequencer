using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography;

/// <summary>
/// Build-plan Phase 4 task 3 ("Pixel-weighted coverage: the monotone coverage ladder, individually
/// addressable pixel-heavy props"). Architecture §7: "Coverage rungs form a monotone ladder of strict
/// supersets, so more energy always reads as more house lit, never different props lit... Pixel-heavy
/// props addressable individually, never only via a group constant." Built directly from
/// <see cref="DisplayModel.NodeCount"/> (Phase 1a's own verified pixel table) -- every individual
/// <see cref="DisplayModel"/> is its own ladder entry, so a pixel-heavy model is never hidden behind an
/// all-or-nothing group reference the way the reference show's <c>MATRIXES</c> constant was.
/// </summary>
public static class PixelCoverageLadder
{
    /// <summary>One rung: every model lit at or below this rung's own cumulative pixel coverage, plus the running total.</summary>
    public sealed record Rung(int Index, double TargetCoveragePercent, IReadOnlyList<ModelId> Models, long CumulativePixels, long TotalPixels)
    {
        public double ActualCoveragePercent => TotalPixels <= 0 ? 0 : 100.0 * CumulativePixels / TotalPixels;
    }

    /// <summary>
    /// Builds <paramref name="rungCount"/> evenly-percent-spaced rungs (default 7, matching
    /// <c>EXPORT_AUDIT.md</c>'s own real fix) from <paramref name="layout"/>'s real pixel table. Models
    /// are added lightest-first (so small/common props populate early rungs and the pixel-heaviest props
    /// -- matrices, mega trees -- only join at the highest rungs), producing a strictly monotone
    /// superset chain by construction: rung <c>k</c>'s model set is always rung <c>k-1</c>'s plus zero or
    /// more additional models, never fewer.
    /// </summary>
    public static IReadOnlyList<Rung> Build(DisplayGraph layout, int rungCount = 7)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (rungCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rungCount), rungCount, "Must be positive.");
        }

        var weighted = layout.Models
            .Select(m => (m.Id, Pixels: (long)Math.Max(0, m.NodeCount)))
            .OrderBy(p => p.Pixels)
            .ThenBy(p => p.Id.Value, StringComparer.Ordinal)
            .ToList();

        var totalPixels = weighted.Sum(p => p.Pixels);

        var rungs = new List<Rung>(rungCount);
        var cumulativeModels = new List<ModelId>();
        long cumulativePixels = 0;
        var nextIndex = 0;

        for (var rung = 1; rung <= rungCount; rung++)
        {
            var targetPercent = rung * (100.0 / rungCount);
            var targetPixels = totalPixels * targetPercent / 100.0;

            while (nextIndex < weighted.Count && cumulativePixels < targetPixels)
            {
                cumulativeModels.Add(weighted[nextIndex].Id);
                cumulativePixels += weighted[nextIndex].Pixels;
                nextIndex++;
            }

            rungs.Add(new Rung(rung, targetPercent, cumulativeModels.ToList(), cumulativePixels, totalPixels));
        }

        return rungs;
    }

    /// <summary>
    /// Picks the rung whose <see cref="Rung.TargetCoveragePercent"/> is the smallest one at or above
    /// <paramref name="energyFraction"/>'s own percent (so a 42% energy level selects the first rung
    /// covering at least 42% of the house, never one covering less) -- the lowest rung for
    /// <c>energyFraction &lt;= 0</c>, the highest for <c>energyFraction &gt;= 1</c>.
    /// </summary>
    public static Rung RungForEnergy(IReadOnlyList<Rung> rungs, double energyFraction)
    {
        ArgumentNullException.ThrowIfNull(rungs);
        if (rungs.Count == 0)
        {
            throw new ArgumentException("At least one rung is required.", nameof(rungs));
        }

        var targetPercent = Math.Clamp(energyFraction, 0.0, 1.0) * 100.0;
        return rungs.FirstOrDefault(r => r.TargetCoveragePercent >= targetPercent) ?? rungs[^1];
    }

    /// <summary>
    /// True only when every rung's own model set is a (non-strict, i.e. allowing an unchanged rung when
    /// no new model crosses the next threshold) superset of the previous rung's, and cumulative pixel
    /// coverage never decreases -- the ladder's own required invariant.
    /// </summary>
    public static bool IsMonotoneSupersetChain(IReadOnlyList<Rung> rungs)
    {
        for (var i = 1; i < rungs.Count; i++)
        {
            var previousSet = new HashSet<ModelId>(rungs[i - 1].Models);
            if (!previousSet.IsSubsetOf(rungs[i].Models))
            {
                return false;
            }

            if (rungs[i].CumulativePixels < rungs[i - 1].CumulativePixels)
            {
                return false;
            }
        }

        return true;
    }
}
