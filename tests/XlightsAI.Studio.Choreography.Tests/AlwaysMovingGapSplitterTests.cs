using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography.Tests;

/// <summary>
/// Build-plan Phase 4 task 2 ("The always-moving rule: gap-splitting pass that loops until no gap
/// survives, with a rotating ≥6-state filler pool") and acceptance criterion ("The always-moving rule
/// holds on the output (max gap ≤ 1 bar) for a full song"). Uses a trivial stub
/// <see cref="IEffectRecipeRegistry"/> (no real xLights vocabulary needed to test this purely mechanical
/// algorithm) and a synthetic multi-bar <see cref="BarGrid"/>.
/// </summary>
public sealed class AlwaysMovingGapSplitterTests
{
    private const long TicksPerSecond = 10_000_000L;
    private static readonly ModelId Target = new("Tree");

    private sealed class StubRecipeRegistry : IEffectRecipeRegistry
    {
        public EffectInstance CreateVerified(string recipeId, ModelId target, long startTicks, long endTicks) =>
            new(Guid.NewGuid(), target, 0, startTicks, endTicks, recipeId, "1", new Dictionary<string, string>());

        public bool IsSupportedParameterValue(string recipeId, string key, string value) => true;
    }

    private static readonly FillerPool SixStateFillerPool = new(["a", "b", "c", "d", "e", "f"]);

    /// <summary>12 bars of exactly 2 seconds each -- a simple, evenly-spaced synthetic grid.</summary>
    private static BarGrid BuildBarGrid(int barCount, long barTicks)
    {
        var beats = new List<BeatMarker>();
        for (var bar = 0; bar < barCount; bar++)
        {
            beats.Add(new BeatMarker(Guid.NewGuid(), bar * barTicks, 0, bar, 1.0, false));
        }

        var beatGrid = new BeatGrid(
            Guid.NewGuid(), new SongId(Guid.NewGuid()), "hash", Guid.NewGuid(), Guid.NewGuid(),
            "test/1.0.0", new BeatGridSettings(BeatsPerBar: 4, AlignmentToleranceTicks: 1000),
            SelectedTempo: null, beats.ToArray(), Candidates: [], CorrectionVersion: 0);

        return BarGrid.FromBeatGrid(beatGrid, songEndTicks: barCount * barTicks);
    }

    [Fact]
    public void FillGaps_EmptyExistingEffects_FillsEntireWindow_WithNoGapExceedingOneBar()
    {
        const long barTicks = 2 * TicksPerSecond;
        var barGrid = BuildBarGrid(barCount: 10, barTicks);
        var coverage = new TimeWindow(0, 10 * barTicks);

        var result = AlwaysMovingGapSplitter.FillGaps(Target, [], coverage, barGrid, _ => SixStateFillerPool, sectionEnergyCeiling: 0.5, new StubRecipeRegistry());

        Assert.NotEmpty(result);
        Assert.Equal(0, result.Min(e => e.StartTicks));
        Assert.Equal(coverage.EndTicks, result.Max(e => e.EndTicks));

        var largestGap = AlwaysMovingGapSplitter.LargestGapTicks(Target, result, coverage);
        Assert.True(largestGap <= barTicks, $"Largest gap {largestGap} ticks exceeds one bar ({barTicks} ticks).");

        // No overlaps, no out-of-order placements.
        var ordered = result.OrderBy(e => e.StartTicks).ToList();
        for (var i = 1; i < ordered.Count; i++)
        {
            Assert.True(ordered[i].StartTicks >= ordered[i - 1].EndTicks);
        }
    }

    [Fact]
    public void FillGaps_WithOneLargeExistingGap_SplitsItIntoMultipleFillerSegments()
    {
        const long barTicks = 2 * TicksPerSecond;
        var barGrid = BuildBarGrid(barCount: 10, barTicks);
        var coverage = new TimeWindow(0, 10 * barTicks);

        // A gap spanning 6 bars (12 seconds) with nothing in it -- the original (buggy) one-shot pass
        // would have inserted exactly one filler and declared victory, leaving most of it frozen.
        var registry = new StubRecipeRegistry();
        var existing = new List<EffectInstance>
        {
            registry.CreateVerified("seed", Target, 0, barTicks), // bar 0 covered
            registry.CreateVerified("seed", Target, 7 * barTicks, 10 * barTicks), // bars 7-9 covered
        };

        var result = AlwaysMovingGapSplitter.FillGaps(Target, existing, coverage, barGrid, _ => SixStateFillerPool, sectionEnergyCeiling: 0.2, registry);

        var largestGap = AlwaysMovingGapSplitter.LargestGapTicks(Target, result, coverage);
        Assert.True(largestGap <= barTicks, $"Largest gap {largestGap} ticks exceeds one bar ({barTicks} ticks).");

        // The 6-bar gap must have been split into more than one filler segment -- "loop until no gap
        // survives," not a single beat inserted and declared done.
        var fillersInGap = result.Count(e => e.StartTicks >= barTicks && e.EndTicks <= 7 * barTicks);
        Assert.True(fillersInGap > 1, $"Expected the 6-bar gap to be split into multiple segments; got {fillersInGap}.");
    }

    [Fact]
    public void FillGaps_RotatesThroughAtLeastSixDistinctFillerStates_NeverOneRepeatedFiller()
    {
        const long barTicks = 1 * TicksPerSecond; // short bars -> many filler segments across a long gap.
        var barGrid = BuildBarGrid(barCount: 60, barTicks);
        var coverage = new TimeWindow(0, 60 * barTicks);

        var result = AlwaysMovingGapSplitter.FillGaps(Target, [], coverage, barGrid, _ => SixStateFillerPool, sectionEnergyCeiling: 0.9, new StubRecipeRegistry());

        var distinctRecipesUsed = result.Select(e => e.RecipeId).Distinct().Count();
        Assert.True(distinctRecipesUsed >= FillerPool.MinimumDistinctStates,
            $"Expected at least {FillerPool.MinimumDistinctStates} distinct filler states across a long gap; got {distinctRecipesUsed}.");
    }

    [Fact]
    public void FillGaps_CalmSection_UsesLongerFillerSegments_ThanEnergeticSection_ButNeverExceedsOneBar()
    {
        const long barTicks = 4 * TicksPerSecond;
        var calmDuration = AlwaysMovingGapSplitter.ComputeFillerDurationTicks(barTicks, sectionEnergyCeiling: 0.1);
        var energeticDuration = AlwaysMovingGapSplitter.ComputeFillerDurationTicks(barTicks, sectionEnergyCeiling: 0.95);

        Assert.True(calmDuration > energeticDuration, "Calm sections should use a slower grid (longer filler segments) than energetic ones.");
        Assert.True(calmDuration <= barTicks);
        Assert.True(energeticDuration > 0, "Never zero motion, even for the most energetic grid.");
    }

    /// <summary>
    /// Confirmed as a real bug against a real export: a gap before the first detected bar (a song's own
    /// lead-in/pre-roll, where <see cref="BarGrid.BarAt"/> returns null) used to fall back to "the rest of
    /// the gap itself" as the bar window, producing one giant filler (observed: a 60-SECOND single "On"
    /// effect) instead of looping bar-by-bar -- silently violating this method's own documented "never
    /// exceeds one bar" guarantee for any position outside the known bar range.
    /// </summary>
    [Fact]
    public void FillGaps_GapBeforeTheFirstDetectedBar_StillNeverExceedsOneBar()
    {
        const long barTicks = 2 * TicksPerSecond;
        const long preRollTicks = 30 * barTicks; // a long lead-in before the first detected beat/bar.

        // Bars start at preRollTicks, not 0 -- BarAt(position) is null for any position < preRollTicks.
        var beats = Enumerable.Range(0, 10)
            .Select(bar => new BeatMarker(Guid.NewGuid(), preRollTicks + (bar * barTicks), 0, bar, 1.0, false))
            .ToArray();
        var beatGrid = new BeatGrid(
            Guid.NewGuid(), new SongId(Guid.NewGuid()), "hash", Guid.NewGuid(), Guid.NewGuid(),
            "test/1.0.0", new BeatGridSettings(BeatsPerBar: 4, AlignmentToleranceTicks: 1000),
            SelectedTempo: null, beats, Candidates: [], CorrectionVersion: 0);
        var barGrid = BarGrid.FromBeatGrid(beatGrid, songEndTicks: preRollTicks + (10 * barTicks));

        var coverage = new TimeWindow(0, preRollTicks + (10 * barTicks));
        var result = AlwaysMovingGapSplitter.FillGaps(Target, [], coverage, barGrid, _ => SixStateFillerPool, sectionEnergyCeiling: 0.2, new StubRecipeRegistry());

        var largestGap = AlwaysMovingGapSplitter.LargestGapTicks(Target, result, coverage);
        Assert.True(largestGap <= barTicks, $"Largest gap {largestGap} ticks exceeds one bar ({barTicks} ticks) -- the pre-roll gap before the first detected bar was not split.");

        // The pre-roll span itself must have been split into more than one filler segment, not one giant one.
        var fillersInPreRoll = result.Count(e => e.StartTicks < preRollTicks);
        Assert.True(fillersInPreRoll > 1, $"Expected the {preRollTicks}-tick pre-roll gap to be split into multiple segments; got {fillersInPreRoll}.");
    }

    [Fact]
    public void FillerPool_FewerThanSixDistinctRecipes_Throws()
    {
        Assert.Throws<ArgumentException>(() => new FillerPool(["a", "b", "c", "d", "e"]));
    }

    /// <summary>
    /// User feedback: "[Butterfly], have it use that less often... sparingly." <see cref="FillerPool"/>'s
    /// rotation has no native weighting concept, so a caller dilutes a disliked-but-not-forbidden state by
    /// repeating the OTHER ids and listing the disliked one once -- this only works if duplicates are kept
    /// verbatim in the rotation (not collapsed down to the distinct set), which is exactly what this
    /// asserts.
    /// </summary>
    [Fact]
    public void FillerPool_RepeatedNonRareIds_DilutesTheRareIdsShareOfTheRotation_RatherThanDeduplicating()
    {
        var pool = new FillerPool(["a", "b", "c", "d", "e", "a", "b", "c", "d", "e", "rare"]);

        var cycle = Enumerable.Range(0, 11).Select(pool.RecipeIdAt).ToList();
        Assert.Equal(1, cycle.Count(id => id == "rare"));
        Assert.Equal(2, cycle.Count(id => id == "a"));

        var distinctStatesSeen = cycle.Distinct(StringComparer.Ordinal).Count();
        Assert.Equal(FillerPool.MinimumDistinctStates, distinctStatesSeen); // still satisfies the "≥6 distinct states" invariant.
    }
}
