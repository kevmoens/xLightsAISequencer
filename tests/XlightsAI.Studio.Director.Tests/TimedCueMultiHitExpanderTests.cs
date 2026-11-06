using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Stems;

namespace XlightsAI.Studio.Director.Tests;

/// <summary>Build-plan "richer analysis tools" multi-hit phase (2026-11-02): <see cref="TimedCueMultiHitExpander"/>.</summary>
public sealed class TimedCueMultiHitExpanderTests
{
    [Fact]
    public void Expand_RangeWithThreeRepeatedTranscriptWords_ExpandsToThreeInstantCues()
    {
        var cue = new TimedCue(TimeSpan.FromSeconds(37), TimeSpan.FromSeconds(39), "hit the word bang", TimeSpan.FromSeconds(1.5));
        var transcript = new[]
        {
            new TranscribedWord("bang", TimeSpan.FromSeconds(37.1).Ticks, TimeSpan.FromSeconds(37.3).Ticks, 0.9f),
            new TranscribedWord("bang", TimeSpan.FromSeconds(37.8).Ticks, TimeSpan.FromSeconds(38.0).Ticks, 0.9f),
            new TranscribedWord("bang", TimeSpan.FromSeconds(38.5).Ticks, TimeSpan.FromSeconds(38.7).Ticks, 0.9f),
        };

        var expanded = TimedCueMultiHitExpander.Expand([cue], transcript, transientHits: null);

        Assert.Equal(3, expanded.Count);
        Assert.All(expanded, c => Assert.Equal("hit the word bang", c.Instruction));
        Assert.Equal(TimeSpan.FromSeconds(37.1), expanded[0].Start);
        Assert.Equal(TimeSpan.FromSeconds(37.8), expanded[1].Start);
        Assert.Equal(TimeSpan.FromSeconds(38.5), expanded[2].Start);
        Assert.All(expanded, c => Assert.Equal(TimedCueTranscriptResolver.SnappedTolerance, c.Tolerance));
    }

    [Fact]
    public void Expand_WordSignalPreferredOverTransientSignal_WhenBothAvailable()
    {
        var cue = new TimedCue(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(5), "hit the word bang", TimeSpan.FromSeconds(1));
        var transcript = new[]
        {
            new TranscribedWord("bang", TimeSpan.FromSeconds(1).Ticks, TimeSpan.FromSeconds(1.2).Ticks, 0.9f),
            new TranscribedWord("bang", TimeSpan.FromSeconds(2).Ticks, TimeSpan.FromSeconds(2.2).Ticks, 0.9f),
        };
        var transientHits = new[]
        {
            new TransientHit(TimeSpan.FromSeconds(3).Ticks, 0.9f),
            new TransientHit(TimeSpan.FromSeconds(3.5).Ticks, 0.9f),
            new TransientHit(TimeSpan.FromSeconds(4).Ticks, 0.9f),
        };

        var expanded = TimedCueMultiHitExpander.Expand([cue], transcript, transientHits);

        // Word signal wins (2 word matches), not the transient signal (3 hits) -- the user's own named
        // word is the stronger, more specific anchor.
        Assert.Equal(2, expanded.Count);
        Assert.Equal(TimeSpan.FromSeconds(1), expanded[0].Start);
        Assert.Equal(TimeSpan.FromSeconds(2), expanded[1].Start);
    }

    [Fact]
    public void Expand_NoTranscriptWordMatch_FallsBackToMultipleTransients()
    {
        var cue = new TimedCue(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(5), "flash hard here", TimeSpan.FromSeconds(1));
        var transientHits = new[]
        {
            new TransientHit(TimeSpan.FromSeconds(1).Ticks, 0.9f),
            new TransientHit(TimeSpan.FromSeconds(2).Ticks, 0.9f),
            new TransientHit(TimeSpan.FromSeconds(3).Ticks, 0.9f),
        };

        var expanded = TimedCueMultiHitExpander.Expand([cue], transcript: null, transientHits);

        Assert.Equal(3, expanded.Count);
        Assert.All(expanded, c => Assert.Null(c.End));
        Assert.All(expanded, c => Assert.Equal(TimedCueTransientResolver.SnappedTolerance, c.Tolerance));
    }

    [Fact]
    public void Expand_OnlyOneRepeatedWord_LeavesCueUnchanged()
    {
        var cue = new TimedCue(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(5), "hit the word bang", TimeSpan.FromSeconds(1));
        var transcript = new[] { new TranscribedWord("bang", TimeSpan.FromSeconds(1).Ticks, TimeSpan.FromSeconds(1.2).Ticks, 0.9f) };

        var expanded = TimedCueMultiHitExpander.Expand([cue], transcript, transientHits: null);

        Assert.Equal(cue, Assert.Single(expanded));
    }

    [Fact]
    public void Expand_OnlyOneTransient_LeavesCueUnchanged()
    {
        var cue = new TimedCue(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(5), "flash hard here", TimeSpan.FromSeconds(1));
        var transientHits = new[] { new TransientHit(TimeSpan.FromSeconds(1).Ticks, 0.9f) };

        var expanded = TimedCueMultiHitExpander.Expand([cue], transcript: null, transientHits);

        Assert.Equal(cue, Assert.Single(expanded));
    }

    [Fact]
    public void Expand_BareInstantCue_NeverExpanded_EvenWithManyNearbyHits()
    {
        var cue = new TimedCue(TimeSpan.FromSeconds(24), null, "hit the word bang", TimeSpan.FromSeconds(1.5));
        var transcript = new[]
        {
            new TranscribedWord("bang", TimeSpan.FromSeconds(24.1).Ticks, TimeSpan.FromSeconds(24.3).Ticks, 0.9f),
            new TranscribedWord("bang", TimeSpan.FromSeconds(24.5).Ticks, TimeSpan.FromSeconds(24.7).Ticks, 0.9f),
        };

        var expanded = TimedCueMultiHitExpander.Expand([cue], transcript, transientHits: null);

        Assert.Equal(cue, Assert.Single(expanded));
    }

    [Fact]
    public void Expand_WordOutsideRange_NotCounted()
    {
        var cue = new TimedCue(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(5), "hit the word bang", TimeSpan.FromSeconds(1));
        var transcript = new[]
        {
            new TranscribedWord("bang", TimeSpan.FromSeconds(1).Ticks, TimeSpan.FromSeconds(1.2).Ticks, 0.9f),
            new TranscribedWord("bang", TimeSpan.FromSeconds(10).Ticks, TimeSpan.FromSeconds(10.2).Ticks, 0.9f), // outside [0,5]
        };

        var expanded = TimedCueMultiHitExpander.Expand([cue], transcript, transientHits: null);

        Assert.Equal(cue, Assert.Single(expanded));
    }

    [Fact]
    public void Expand_MultipleCues_OnlyRangedClustersExpand_OthersPassThroughUnchanged()
    {
        var instantCue = new TimedCue(TimeSpan.FromSeconds(50), null, "single hit here", TimeSpan.FromSeconds(1));
        var clusterCue = new TimedCue(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(5), "flash hard here", TimeSpan.FromSeconds(1));
        var transientHits = new[]
        {
            new TransientHit(TimeSpan.FromSeconds(1).Ticks, 0.9f),
            new TransientHit(TimeSpan.FromSeconds(2).Ticks, 0.9f),
        };

        var expanded = TimedCueMultiHitExpander.Expand([instantCue, clusterCue], transcript: null, transientHits);

        Assert.Equal(3, expanded.Count); // instantCue unchanged + 2 expanded from clusterCue
        Assert.Contains(expanded, c => c == instantCue);
    }

    [Fact]
    public void Expand_EmptyCueList_ReturnsSameListInstance()
    {
        IReadOnlyList<TimedCue> cues = [];

        Assert.Same(cues, TimedCueMultiHitExpander.Expand(cues, null, null));
    }

    [Fact]
    public void Expand_NullCues_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => TimedCueMultiHitExpander.Expand(null!, null, null));
    }
}
