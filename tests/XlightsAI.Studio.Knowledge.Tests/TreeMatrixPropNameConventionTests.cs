namespace XlightsAI.Studio.Knowledge.Tests;

public sealed class TreeMatrixPropNameConventionTests
{
    [Theory]
    [InlineData("Tree1", true)]
    [InlineData("group:Matrixes", true)]
    [InlineData("MegaTree", true)]
    [InlineData("matrix-left", true)]
    [InlineData("group:Outline", false)]
    [InlineData("Arch1", false)]
    public void IsTreeOrMatrixName_MatchesCaseInsensitively(string name, bool expected) =>
        Assert.Equal(expected, TreeMatrixPropNameConvention.IsTreeOrMatrixName(name));
}
