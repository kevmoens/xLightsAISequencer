namespace XlightsAI.Studio.Choreography.Tests;

public sealed class TimescaleResolverTests
{
    private static readonly string[] RealTrackNames = ["Beats", "Bars", "Phrases", "Sections", "Lyrics_Phrases"];

    [Theory]
    [InlineData("beat", "Beats")]
    [InlineData("beats", "Beats")]
    [InlineData("Beat", "Beats")]
    [InlineData("BEATS", "Beats")]
    [InlineData("bar", "Bars")]
    [InlineData("bars", "Bars")]
    [InlineData("phrase", "Phrases")]
    [InlineData("phrases", "Phrases")]
    public void Resolve_AiAuthoredFreeFormWord_ResolvesToRealTrackName(string requested, string expected)
    {
        Assert.Equal(expected, TimescaleResolver.Resolve(requested, RealTrackNames));
    }

    [Fact]
    public void Resolve_ExactRealTrackName_ReturnsItUnchanged()
    {
        Assert.Equal("Lyrics_Phrases", TimescaleResolver.Resolve("Lyrics_Phrases", RealTrackNames));
    }

    [Fact]
    public void Resolve_CaseInsensitiveMatchAgainstAnAvailableName_StillResolves()
    {
        Assert.Equal("Lyrics_Phrases", TimescaleResolver.Resolve("lyrics_phrases", RealTrackNames));
    }

    [Fact]
    public void Resolve_UnresolvableReference_ReturnsNull()
    {
        Assert.Null(TimescaleResolver.Resolve("percussion", RealTrackNames)); // not in RealTrackNames for this test.
    }

    [Fact]
    public void Resolve_NullOrWhitespace_ReturnsNull()
    {
        Assert.Null(TimescaleResolver.Resolve(null, RealTrackNames));
        Assert.Null(TimescaleResolver.Resolve("   ", RealTrackNames));
    }

    [Fact]
    public void Resolve_AliasTargetNotActuallyAvailable_ReturnsNull_RatherThanAnUnavailableName()
    {
        Assert.Null(TimescaleResolver.Resolve("percussion", ["Beats", "Bars"]));
    }
}
