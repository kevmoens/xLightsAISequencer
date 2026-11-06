using XlightsAI.Studio.Stems;

namespace XlightsAI.Studio.Director.Tests;

/// <summary>Build-plan "richer analysis tools" Phase 6 (2026-11-02): <see cref="TimedCueTranscriptResolver"/>.</summary>
public sealed class TimedCueTranscriptResolverTests
{
    [Fact]
    public void Resolve_InstructionNamesWordFoundInTranscriptWithinWindow_SnapsToRealWordTiming()
    {
        var cue = new TimedCue(TimeSpan.FromSeconds(24), TimeSpan.FromSeconds(25), "full on and off to hit the word bang", TimeSpan.FromSeconds(1.5));
        var transcript = new[] { new TranscribedWord("bang", TimeSpan.FromSeconds(24.3).Ticks, TimeSpan.FromSeconds(24.6).Ticks, 0.9f) };

        var resolved = TimedCueTranscriptResolver.Resolve([cue], transcript);

        var result = Assert.Single(resolved);
        Assert.Equal(TimeSpan.FromSeconds(24.3), result.Start);
        Assert.Equal(TimeSpan.FromSeconds(24.6), result.End);
        Assert.Equal(TimedCueTranscriptResolver.SnappedTolerance, result.Tolerance);
    }

    [Fact]
    public void Resolve_WordInWindowButNotNamedInInstruction_LeavesCueUnchanged()
    {
        var cue = new TimedCue(TimeSpan.FromSeconds(24), null, "big moment here", TimeSpan.FromSeconds(1.5));
        var transcript = new[] { new TranscribedWord("bang", TimeSpan.FromSeconds(24.2).Ticks, TimeSpan.FromSeconds(24.5).Ticks, 0.9f) };

        var resolved = TimedCueTranscriptResolver.Resolve([cue], transcript);

        Assert.Equal(cue, Assert.Single(resolved));
    }

    [Fact]
    public void Resolve_NamedWordOutsideWindow_LeavesCueUnchanged()
    {
        var cue = new TimedCue(TimeSpan.FromSeconds(24), null, "hit the word bang", TimeSpan.FromSeconds(1.0));
        var transcript = new[] { new TranscribedWord("bang", TimeSpan.FromSeconds(40).Ticks, TimeSpan.FromSeconds(40.3).Ticks, 0.9f) };

        var resolved = TimedCueTranscriptResolver.Resolve([cue], transcript);

        Assert.Equal(cue, Assert.Single(resolved));
    }

    [Fact]
    public void Resolve_ShortCommonWordNamedInInstruction_NeverMatches()
    {
        // "to" is in the instruction and in the transcript, but is too short/common to count as a real anchor.
        var cue = new TimedCue(TimeSpan.FromSeconds(24), null, "go to the chorus", TimeSpan.FromSeconds(1.0));
        var transcript = new[] { new TranscribedWord("to", TimeSpan.FromSeconds(24.1).Ticks, TimeSpan.FromSeconds(24.2).Ticks, 0.9f) };

        var resolved = TimedCueTranscriptResolver.Resolve([cue], transcript);

        Assert.Equal(cue, Assert.Single(resolved));
    }

    [Fact]
    public void Resolve_CaseInsensitiveWordMatch_StillSnaps()
    {
        var cue = new TimedCue(TimeSpan.FromSeconds(10), null, "hit it on the word JOY", TimeSpan.FromSeconds(1.0));
        var transcript = new[] { new TranscribedWord("joy,", TimeSpan.FromSeconds(10.1).Ticks, TimeSpan.FromSeconds(10.4).Ticks, 0.9f) };

        var resolved = TimedCueTranscriptResolver.Resolve([cue], transcript);

        Assert.Equal(TimeSpan.FromSeconds(10.1), Assert.Single(resolved).Start);
    }

    [Fact]
    public void Resolve_NullTranscript_ReturnsSameListInstance()
    {
        var cue = new TimedCue(TimeSpan.FromSeconds(10), null, "hit the word bang", TimeSpan.FromSeconds(1.0));
        IReadOnlyList<TimedCue> cues = [cue];

        Assert.Same(cues, TimedCueTranscriptResolver.Resolve(cues, null));
    }

    [Fact]
    public void Resolve_EmptyCueList_ReturnsSameListInstance()
    {
        IReadOnlyList<TimedCue> cues = [];
        var transcript = new[] { new TranscribedWord("bang", 0, 1, 0.9f) };

        Assert.Same(cues, TimedCueTranscriptResolver.Resolve(cues, transcript));
    }
}
