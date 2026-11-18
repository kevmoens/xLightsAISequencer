using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Director.Tests;

/// <summary>User feedback (2026-11-15), confirmed against a real generation ("Carol of the Bells"): <see cref="LeadInstrumentProminenceComplianceChecker"/>'s own compliance logic, hermetic (no real audio/LLM). Mirrors <see cref="TimedCueComplianceCheckerTests"/>.</summary>
public sealed class LeadInstrumentProminenceComplianceCheckerTests
{
    private static long Seconds(double s) => TimeSpan.FromSeconds(s).Ticks;

    private static EffectInstance ShortEffectAt(double startSeconds, double durationSeconds) => new(
        Guid.NewGuid(), new ModelId("Tree"), 0, Seconds(startSeconds), Seconds(startSeconds + durationSeconds), "recipe", "1", new Dictionary<string, string>());

    private static LeadInstrumentWindow Window(double startSeconds, double endSeconds, double relativeProminence = 2.0) =>
        new(Seconds(startSeconds), Seconds(endSeconds), relativeProminence);

    [Fact]
    public void FindMissedWindows_ShortEffectInsideWindow_SatisfiesTheWindow()
    {
        var ir = new ChoreographyIR([], [ShortEffectAt(24.3, 0.2)], []);
        var window = Window(24.0, 27.0);

        var missed = LeadInstrumentProminenceComplianceChecker.FindMissedWindows(ir, [window]);

        Assert.Empty(missed);
    }

    [Fact]
    public void FindMissedWindows_NoEffectNearby_WindowIsMissed()
    {
        var ir = new ChoreographyIR([], [ShortEffectAt(90.0, 0.2)], []);
        var window = Window(24.0, 27.0);

        var missed = LeadInstrumentProminenceComplianceChecker.FindMissedWindows(ir, [window]);

        Assert.Single(missed);
    }

    [Fact]
    public void FindMissedWindows_EffectTooLongToCountAsAHardHit_WindowIsMissed()
    {
        // A long sustained wash spanning the window must not count as "a distinct hit near here."
        var ir = new ChoreographyIR([], [new EffectInstance(Guid.NewGuid(), new ModelId("Tree"), 0, Seconds(0), Seconds(60), "recipe", "1", new Dictionary<string, string>())], []);
        var window = Window(24.0, 27.0);

        var missed = LeadInstrumentProminenceComplianceChecker.FindMissedWindows(ir, [window]);

        Assert.Single(missed);
    }

    [Fact]
    public void FindMissedWindows_EmptyWindowList_ReturnsEmpty_NeverThrows()
    {
        var ir = new ChoreographyIR([], [], []);

        var missed = LeadInstrumentProminenceComplianceChecker.FindMissedWindows(ir, []);

        Assert.Empty(missed);
    }

    [Fact]
    public void FindMissedWindows_MultipleWindows_OnlyUnsatisfiedOnesReturned()
    {
        var ir = new ChoreographyIR([], [ShortEffectAt(24.3, 0.2)], []);
        var satisfied = Window(24.0, 27.0);
        var unsatisfied = Window(90.0, 91.0);

        var missed = LeadInstrumentProminenceComplianceChecker.FindMissedWindows(ir, [satisfied, unsatisfied]);

        var only = Assert.Single(missed);
        Assert.Equal(unsatisfied, only);
    }
}
