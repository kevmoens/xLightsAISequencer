using XlightsAI.Studio.Pipeline;

namespace XlightsAI.Studio.Director.Tests;

/// <summary>
/// Build-plan Phase 13 §2b/§2d.1: the Choreographer's own stable prefix should surface a computed
/// effect-usage distribution across the Show Bible's own Look library (so a Choreographer can avoid
/// over-relying on the same one or two effects across many sections), and its phrase-scoped-staging
/// guidance should carry a concrete worked example, not just prose.
/// </summary>
public sealed class SectionScriptPromptBuilderDiversityTests
{
    [Fact]
    public void BuildStablePrefix_RendersEffectUsageDistribution_AcrossTheLookLibrary()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var bible = ShowBibleContentFixture.BuildValid(lookCount: 10);
        var vocabulary = RealXLightsFixture.BuildCatalogs().Vocabulary;
        var knowledge = new DirectorKnowledgeContext(vocabulary, new CraftRules(DateTimeOffset.UtcNow, 0.7, 3, [], []), new ExemplarIndex(DateTimeOffset.UtcNow, []));

        var prefix = SectionScriptPromptBuilder.BuildStablePrefix(bible, knowledge, "Traditional / Classic");

        Assert.Contains("Effect usage across this song's own Look library", prefix);
        Assert.Contains("Onx10", prefix); // every ShowBibleContentFixture.BuildValid Look uses a single "On" layer.
    }

    [Fact]
    public void BuildStablePrefix_ContainsAConcretePhraseStagingWorkedExample()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var bible = ShowBibleContentFixture.BuildValid();
        var vocabulary = RealXLightsFixture.BuildCatalogs().Vocabulary;
        var knowledge = new DirectorKnowledgeContext(vocabulary, new CraftRules(DateTimeOffset.UtcNow, 0.7, 3, [], []), new ExemplarIndex(DateTimeOffset.UtcNow, []));

        var prefix = SectionScriptPromptBuilder.BuildStablePrefix(bible, knowledge, "Traditional / Classic");

        Assert.Contains("Concrete worked example", prefix);
        Assert.Contains("phraseIndex 2, activeLayerZs [1,2]", prefix);
    }
}
