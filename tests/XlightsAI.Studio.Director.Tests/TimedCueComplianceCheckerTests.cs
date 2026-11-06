using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Director.Tests;

/// <summary>Build-plan "richer analysis tools" Phase 1 (2026-11-01): <see cref="TimedCueComplianceChecker"/>'s own compliance logic, hermetic (no real audio/LLM).</summary>
public sealed class TimedCueComplianceCheckerTests
{
    private static long Seconds(double s) => TimeSpan.FromSeconds(s).Ticks;

    private static EffectInstance ShortEffectAt(double startSeconds, double durationSeconds) => new(
        Guid.NewGuid(), new ModelId("Tree"), 0, Seconds(startSeconds), Seconds(startSeconds + durationSeconds), "recipe", "1", new Dictionary<string, string>());

    private static TimedCue Cue(double startSeconds, double? endSeconds = null, double toleranceSeconds = 1.5) => new(
        TimeSpan.FromSeconds(startSeconds), endSeconds is { } e ? TimeSpan.FromSeconds(e) : null, "hit it", TimeSpan.FromSeconds(toleranceSeconds));

    [Fact]
    public void FindMissedCues_ShortEffectInsideToleranceWindow_SatisfiesTheCue()
    {
        var ir = new ChoreographyIR([], [ShortEffectAt(24.3, 0.2)], []);
        var cue = Cue(24.0, 25.0, toleranceSeconds: 1.0);

        var missed = TimedCueComplianceChecker.FindMissedCues(ir, [cue]);

        Assert.Empty(missed);
    }

    [Fact]
    public void FindMissedCues_NoEffectNearby_CueIsMissed()
    {
        var ir = new ChoreographyIR([], [ShortEffectAt(90.0, 0.2)], []);
        var cue = Cue(24.0, 25.0, toleranceSeconds: 1.0);

        var missed = TimedCueComplianceChecker.FindMissedCues(ir, [cue]);

        Assert.Single(missed);
    }

    [Fact]
    public void FindMissedCues_EffectTooLongToCountAsAHardHit_CueIsMissed()
    {
        // A long sustained wash covering the cue's moment must not count as "a hard hit near here."
        var ir = new ChoreographyIR([], [new EffectInstance(Guid.NewGuid(), new ModelId("Tree"), 0, Seconds(0), Seconds(60), "recipe", "1", new Dictionary<string, string>())], []);
        var cue = Cue(24.0, 25.0);

        var missed = TimedCueComplianceChecker.FindMissedCues(ir, [cue]);

        Assert.Single(missed);
    }

    [Fact]
    public void FindMissedCues_EffectJustOutsideToleranceWindow_CueIsMissed()
    {
        var ir = new ChoreographyIR([], [ShortEffectAt(27.0, 0.2)], []); // cue window is [23,26] with 1s tolerance around [24,25]
        var cue = Cue(24.0, 25.0, toleranceSeconds: 1.0);

        var missed = TimedCueComplianceChecker.FindMissedCues(ir, [cue]);

        Assert.Single(missed);
    }

    [Fact]
    public void FindMissedCues_EmptyCueList_ReturnsEmpty_NeverThrows()
    {
        var ir = new ChoreographyIR([], [], []);

        var missed = TimedCueComplianceChecker.FindMissedCues(ir, []);

        Assert.Empty(missed);
    }

    [Fact]
    public void FindMissedCues_MultipleCues_OnlyUnsatisfiedOnesReturned()
    {
        var ir = new ChoreographyIR([], [ShortEffectAt(24.3, 0.2)], []);
        var satisfied = Cue(24.0, 25.0, toleranceSeconds: 1.0);
        var unsatisfied = Cue(90.0, 91.0, toleranceSeconds: 1.0);

        var missed = TimedCueComplianceChecker.FindMissedCues(ir, [satisfied, unsatisfied]);

        var only = Assert.Single(missed);
        Assert.Equal(unsatisfied, only);
    }
}
