using XlightsAI.Studio.Rhythm;

namespace XlightsAI.Studio.Director.Tests;

/// <summary>Build-plan "richer analysis tools" Phase 2 (2026-11-02): <see cref="TimedCueTransientResolver"/>.</summary>
public sealed class TimedCueTransientResolverTests
{
    [Fact]
    public void Resolve_RealTransientNearTypedCue_SnapsStartAndTightensTolerance()
    {
        var cue = new TimedCue(TimeSpan.FromSeconds(24), TimeSpan.FromSeconds(25), "bang", TimeSpan.FromSeconds(1.5));
        var hits = new[] { new TransientHit(TimeSpan.FromSeconds(24.3).Ticks, 0.95f) };

        var resolved = TimedCueTransientResolver.Resolve([cue], hits);

        var result = Assert.Single(resolved);
        Assert.Equal(TimeSpan.FromSeconds(24.3), result.Start);
        Assert.Equal(TimeSpan.FromSeconds(25.3), result.End);
        Assert.Equal(TimedCueTransientResolver.SnappedTolerance, result.Tolerance);
    }

    [Fact]
    public void Resolve_NoTransientWithinToleranceWindow_LeavesCueUnchanged()
    {
        var cue = new TimedCue(TimeSpan.FromSeconds(24), TimeSpan.FromSeconds(25), "bang", TimeSpan.FromSeconds(1.5));
        var hits = new[] { new TransientHit(TimeSpan.FromSeconds(40).Ticks, 0.95f) };

        var resolved = TimedCueTransientResolver.Resolve([cue], hits);

        var result = Assert.Single(resolved);
        Assert.Equal(cue, result);
    }

    [Fact]
    public void Resolve_MultipleHitsInWindow_PicksClosestToTypedStart()
    {
        var cue = new TimedCue(TimeSpan.FromSeconds(24), null, "bang", TimeSpan.FromSeconds(1.5));
        var hits = new[]
        {
            new TransientHit(TimeSpan.FromSeconds(23.0).Ticks, 0.8f),
            new TransientHit(TimeSpan.FromSeconds(24.2).Ticks, 0.9f),
        };

        var resolved = TimedCueTransientResolver.Resolve([cue], hits);

        Assert.Equal(TimeSpan.FromSeconds(24.2), Assert.Single(resolved).Start);
    }

    [Fact]
    public void Resolve_NoHits_ReturnsSameListInstance()
    {
        var cue = new TimedCue(TimeSpan.FromSeconds(24), null, "bang", TimeSpan.FromSeconds(1.5));
        IReadOnlyList<TimedCue> cues = [cue];

        var resolved = TimedCueTransientResolver.Resolve(cues, null);

        Assert.Same(cues, resolved);
    }

    [Fact]
    public void Resolve_EmptyCueList_ReturnsSameListInstance()
    {
        IReadOnlyList<TimedCue> cues = [];
        var hits = new[] { new TransientHit(0, 0.9f) };

        Assert.Same(cues, TimedCueTransientResolver.Resolve(cues, hits));
    }
}
