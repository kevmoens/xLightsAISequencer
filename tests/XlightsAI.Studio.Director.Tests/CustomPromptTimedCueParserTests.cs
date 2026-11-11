namespace XlightsAI.Studio.Director.Tests;

/// <summary>
/// Build-plan "richer analysis tools" Phase 1 (2026-11-01): <see cref="CustomPromptTimedCueParser"/>'s
/// own real-world motivating example (verbatim) plus edge cases.
/// </summary>
public sealed class CustomPromptTimedCueParserTests
{
    private const string RealExamplePrompt =
        "The first 11 seconds you will find music with a piano, only put an effect on a single model that isn't a group so it feels like it is moving.\n" +
        "When the music goes BANG BANG BANG have effects that do full on and off to hit the word bang 00:00:24-00:00:25  00:00:37-00:00:39  00:00:50-00:00:53  00:01:05-00:01:06  00:01:18-00:01:20 00:01:46-00:01:47 00:02:00-00:02:02  00:02:37-00:02:39  00:02:51-00:02:55";

    [Fact]
    public void Parse_RealMotivatingExample_Extracts9Cues_AllSharingTheBangInstruction_PianoLineIgnored()
    {
        var cues = CustomPromptTimedCueParser.Parse(RealExamplePrompt);

        Assert.Equal(9, cues.Count);
        Assert.All(cues, c => Assert.Contains("full on and off to hit the word bang", c.Instruction));
        Assert.All(cues, c => Assert.DoesNotContain(":", c.Instruction)); // no leftover timestamp token
        Assert.DoesNotContain(cues, c => c.Instruction.Contains("piano", StringComparison.OrdinalIgnoreCase));

        var first = cues[0];
        Assert.Equal(TimeSpan.FromSeconds(24), first.Start);
        Assert.Equal(TimeSpan.FromSeconds(25), first.End);

        var last = cues[^1];
        Assert.Equal(new TimeSpan(0, 2, 51), last.Start);
        Assert.Equal(new TimeSpan(0, 2, 55), last.End);
    }

    /// <summary>
    /// User-reported real bug (2026-11-08): a real post-processing prompt --
    /// "at 00:22 - 00:23 do a build up by reducing effects to twinkle, then at 00:24-00:25 build up and
    /// do 'On' to blue one matrix at a time to each time it says 'Bang'" -- came back with BOTH the 0:22
    /// and the 0:24 cue carrying the entire undivided sentence as their instruction (confirmed via the
    /// real persisted log line), so the section at 0:24 was told to do the twinkle reduction too. Each
    /// cue's own trailing text (up to the next timestamp) must now be specific to it.
    /// </summary>
    [Fact]
    public void Parse_CompoundSequentialLine_EachCueGetsItsOwnTrailingInstruction_NotTheWholeLine()
    {
        const string prompt = "at 00:22 - 00:23 do a build up by reducing effects to twinkle, then at 00:24-00:25 build up and do \"On\" to blue one matrix at a time to each time it says \"Bang\"";

        var cues = CustomPromptTimedCueParser.Parse(prompt);

        Assert.Equal(2, cues.Count);
        var first = cues[0];
        var second = cues[1];

        Assert.Equal(new TimeSpan(0, 0, 22), first.Start);
        Assert.Equal(new TimeSpan(0, 0, 23), first.End);
        Assert.Equal(new TimeSpan(0, 0, 24), second.Start);
        Assert.Equal(new TimeSpan(0, 0, 25), second.End);

        Assert.Contains("twinkle", first.Instruction, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bang", first.Instruction, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("bang", second.Instruction, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("blue", second.Instruction, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("twinkle", second.Instruction, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_LineWithNoTimestamp_ProducesNoCue()
    {
        var cues = CustomPromptTimedCueParser.Parse("Use a red and gold palette throughout; avoid blue.");

        Assert.Empty(cues);
    }

    [Fact]
    public void Parse_SingleTimestampNoRange_EndIsNull_EffectiveEndEqualsStart()
    {
        var cues = CustomPromptTimedCueParser.Parse("Big reveal moment at 01:23, make it dramatic.");

        var cue = Assert.Single(cues);
        Assert.Equal(new TimeSpan(0, 1, 23), cue.Start);
        Assert.Null(cue.End);
        Assert.Equal(cue.Start, cue.EffectiveEnd);
    }

    [Fact]
    public void Parse_MmSsFormat_ParsesAsMinutesSeconds_NotHoursMinutes()
    {
        var cues = CustomPromptTimedCueParser.Parse("Flash here 02:05");

        var cue = Assert.Single(cues);
        Assert.Equal(TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(5), cue.Start);
    }

    [Fact]
    public void Parse_HhMmSsFormat_ParsesCorrectly()
    {
        var cues = CustomPromptTimedCueParser.Parse("Flash here 00:02:05");

        var cue = Assert.Single(cues);
        Assert.Equal(new TimeSpan(0, 2, 5), cue.Start);
    }

    [Fact]
    public void Parse_LineThatIsOnlyTimestamps_ProducesNoCue_NeverFabricatesAnInstruction()
    {
        var cues = CustomPromptTimedCueParser.Parse("00:00:24-00:00:25 00:00:37-00:00:39");

        Assert.Empty(cues);
    }

    [Fact]
    public void Parse_NullOrWhitespace_ReturnsEmpty()
    {
        Assert.Empty(CustomPromptTimedCueParser.Parse(null));
        Assert.Empty(CustomPromptTimedCueParser.Parse("   "));
        Assert.Empty(CustomPromptTimedCueParser.Parse(string.Empty));
    }

    [Fact]
    public void Parse_UsesDefaultToleranceUnlessOverridden()
    {
        var defaultCue = Assert.Single(CustomPromptTimedCueParser.Parse("Hit it at 00:10"));
        Assert.Equal(CustomPromptTimedCueParser.DefaultTolerance, defaultCue.Tolerance);

        var customCue = Assert.Single(CustomPromptTimedCueParser.Parse("Hit it at 00:10", TimeSpan.FromSeconds(3)));
        Assert.Equal(TimeSpan.FromSeconds(3), customCue.Tolerance);
    }
}
