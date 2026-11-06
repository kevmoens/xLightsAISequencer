using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Director.Tests;

/// <summary>Build-plan "richer analysis tools" Phase 8 (2026-11-02): <see cref="SingleModelDirectiveParser"/>.</summary>
public sealed class SingleModelDirectiveParserTests
{
    private static DisplayGraph BuildLayout() => new(
        "TEST-HASH",
        Models:
        [
            new DisplayModel(new ModelId("Arch 1"), "Arch 1", "Arch", [], [], NodeCount: 50),
            new DisplayModel(new ModelId("Tree"), "Tree", "Tree", [], [], NodeCount: 900),
        ],
        Groups: [],
        Warnings: []);

    [Fact]
    public void Parse_LineNamesRealModelWithNotAGroupPhrase_ExtractsModelId()
    {
        var layout = BuildLayout();

        var result = SingleModelDirectiveParser.Parse("Make sure Arch 1 is targeted individually, not as a group.", layout);

        Assert.Equal([new ModelId("Arch 1")], result);
    }

    [Fact]
    public void Parse_LineNamesRealModelWithNoNotAGroupPhrase_ExtractsNothing()
    {
        var layout = BuildLayout();

        var result = SingleModelDirectiveParser.Parse("Give Arch 1 a nice color.", layout);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_NotAGroupPhraseWithNoRealModelName_ExtractsNothing()
    {
        var layout = BuildLayout();

        var result = SingleModelDirectiveParser.Parse("Keep things varied, not a group of identical looks.", layout);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_MultipleQualifyingLines_ExtractsEachDistinctModelOnce()
    {
        var layout = BuildLayout();

        var result = SingleModelDirectiveParser.Parse(
            "Arch 1 should be its own model, not a group.\nTree should also be individually targeted.\nArch 1 again, not a group.", layout);

        Assert.Equal(2, result.Count);
        Assert.Contains(new ModelId("Arch 1"), result);
        Assert.Contains(new ModelId("Tree"), result);
    }

    [Fact]
    public void Parse_EmptyText_ReturnsEmpty()
    {
        Assert.Empty(SingleModelDirectiveParser.Parse("", BuildLayout()));
    }

    [Fact]
    public void Parse_NullText_ReturnsEmpty()
    {
        Assert.Empty(SingleModelDirectiveParser.Parse(null, BuildLayout()));
    }
}
