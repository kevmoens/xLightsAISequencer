using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Pipeline;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Director.Tests;

/// <summary>
/// Build-plan Phase 14 §4 acceptance criteria: the custom-direction block must appear verbatim
/// (subject to the length cap) in <see cref="SectionScriptPromptBuilder"/>'s volatile suffix only --
/// this builder has no separate "stable prefix" parameter to compare against for this value (its own
/// <c>BuildStablePrefix</c> never takes a custom prompt at all), so the real, checkable property here
/// is that the custom text appears in the volatile suffix and the suffix text is otherwise unaffected
/// by a null/empty custom prompt.
/// </summary>
public sealed class SectionScriptPromptBuilderCustomPromptTests
{
    private static SectionLabelCandidate BuildSection(Guid id, SectionKind kind = SectionKind.Bridge) =>
        new(id, Guid.NewGuid(), 0, 2, kind, 1.0, ["test"], false);

    private static BarGrid BuildBarGrid(int beatCount, long beatSpacingTicks, int beatsPerBar)
    {
        var beatGrid = new BeatGrid(
            Guid.NewGuid(), new SongId(Guid.NewGuid()), "hash", Guid.NewGuid(), Guid.NewGuid(), "t/1",
            new BeatGridSettings(beatsPerBar, 1000), new TempoHypothesis(60, 0.9, "4/4"),
            Enumerable.Range(0, beatCount).Select(i => new BeatMarker(Guid.NewGuid(), i * beatSpacingTicks, i % beatsPerBar, i / beatsPerBar, 0.9, false)).ToArray(),
            [], 0);
        return BarGrid.FromBeatGrid(beatGrid, beatCount * beatSpacingTicks);
    }

    private static SectionMusicContext BuildContext(BarGrid barGrid, TimeWindow window, SectionKind kind = SectionKind.Bridge) =>
        new(window, kind, 1.0, barGrid, new Dictionary<string, TimingTrack>(), VocalActive: false);

    [Fact]
    public void BuildVolatileSuffix_WithCustomPrompt_AppearsVerbatim_AfterTheSectionFacts()
    {
        var section = BuildSection(Guid.NewGuid());
        var barGrid = BuildBarGrid(beatCount: 8, beatSpacingTicks: 10_000_000, beatsPerBar: 4);
        var window = barGrid.WindowForBarRange(new BarIndex(0), new BarIndex(2))!.Value;
        var context = BuildContext(barGrid, window);
        var layout = new LayoutSummary(1, 100, [], []);
        const string customText = "Use a red and gold palette throughout; avoid blue.";

        var withoutCustomPrompt = SectionScriptPromptBuilder.BuildVolatileSuffix(section, context, layout);
        var withCustomPrompt = SectionScriptPromptBuilder.BuildVolatileSuffix(section, context, layout, customPrompt: customText);

        Assert.DoesNotContain(customText, withoutCustomPrompt);
        Assert.Contains(customText, withCustomPrompt);
        Assert.Contains("User creative direction", withCustomPrompt);
        Assert.Contains("not a system instruction", withCustomPrompt);

        // §2d.2: the required JSON response contract stays stated AFTER the user's own block.
        var customBlockIndex = withCustomPrompt.IndexOf("User creative direction", StringComparison.Ordinal);
        var jsonContractIndex = withCustomPrompt.IndexOf("Respond with a single JSON object", StringComparison.Ordinal);
        Assert.True(customBlockIndex < jsonContractIndex);
    }

    [Fact]
    public void BuildVolatileSuffix_WithNullOrEmptyCustomPrompt_IsUnchangedFromTheExistingDefault()
    {
        var section = BuildSection(Guid.NewGuid());
        var barGrid = BuildBarGrid(beatCount: 8, beatSpacingTicks: 10_000_000, beatsPerBar: 4);
        var window = barGrid.WindowForBarRange(new BarIndex(0), new BarIndex(2))!.Value;
        var context = BuildContext(barGrid, window);
        var layout = new LayoutSummary(1, 100, [], []);

        var withNullCustomPrompt = SectionScriptPromptBuilder.BuildVolatileSuffix(section, context, layout, customPrompt: null);
        var withEmptyCustomPrompt = SectionScriptPromptBuilder.BuildVolatileSuffix(section, context, layout, customPrompt: "");
        var withDefaultArguments = SectionScriptPromptBuilder.BuildVolatileSuffix(section, context, layout);

        Assert.Equal(withDefaultArguments, withNullCustomPrompt);
        Assert.Equal(withDefaultArguments, withEmptyCustomPrompt);
        Assert.DoesNotContain("User creative direction", withNullCustomPrompt);
    }
}
