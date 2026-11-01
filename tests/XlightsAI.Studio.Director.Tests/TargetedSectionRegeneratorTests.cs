using System.Text.Json;
using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Director.Tests;

/// <summary>
/// Build-plan Phase 7d's own requirement: "Only affected SectionScripts re-run; the expensive Show Bible
/// call is usually untouched." These tests prove the untouched-section half of that directly -- a section
/// with no <see cref="RevisionDirective"/> must never appear in any request the underlying
/// <see cref="IChatCompletionClient"/> receives.
/// </summary>
public sealed class TargetedSectionRegeneratorTests
{
    private static async Task<DisplayGraph> LoadLayoutAsync() =>
        await new ShowLayoutImporter().ImportReadOnlyAsync(RealXLightsFixture.ShowDirectory, CancellationToken.None);

    private static EffectVocabularyCatalog FakeVocabulary() => new(
        RealXLightsFixture.SixCommonEffectNames.ToDictionary(name => name, name => new EffectVocabulary(name, "E_", []), StringComparer.Ordinal),
        buffer: new EffectVocabulary("Buffer", "B_", []),
        blending: new EffectVocabulary("Blending", "T_", []),
        color: new EffectVocabulary("Color", "C_", []));

    private static LookLibrary OneLook() => new([
        new Look("golden-hold", "Golden Hold", "Warm hold.", 0.45, "hold", "warm-gold", "group:Outline",
            [new LookLayer(1, "group:Outline", "On", LookLayer.RoleSubject, new Dictionary<string, string>(), "Normal", "Bars", 0.9)]),
    ]);

    [Fact]
    public async Task RegenerateAsync_NoDirectives_NeverCallsTheClient_AndReturnsSameDictionaryInstance()
    {
        var fake = new FakeChatCompletionClient([]);
        var generator = new SectionScriptBatchGenerator(fake);
        var layout = RealXLightsFixture.IsAvailable ? await LoadLayoutAsync() : new DisplayGraph("none", [], [], []);

        var untouchedSectionId = Guid.NewGuid();
        var existing = new Dictionary<Guid, SectionScript> { [untouchedSectionId] = new(untouchedSectionId, "golden-hold") };

        var result = await TargetedSectionRegenerator.RegenerateAsync(
            generator, "stable prefix", existing, directives: [], volatileSuffixBySectionId: new Dictionary<Guid, string>(),
            Guid.NewGuid(), OneLook(), layout, FakeVocabulary(), maxOutputTokens: 500, CancellationToken.None);

        Assert.Equal(0, fake.CallCount);
        Assert.Same(existing, result);
    }

    [Fact]
    public async Task RegenerateAsync_OneDirectedSection_OnlyThatSectionIsSentToTheClient_OthersUnchanged()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return; // SectionScriptValidator needs a real layout to accept "group:Outline" -- skip if absent, same convention as SectionScriptBatchGeneratorTests.
        }

        var layout = await LoadLayoutAsync();

        var untouchedId = Guid.NewGuid();
        var directedId = Guid.NewGuid();
        var untouchedScript = new SectionScript(untouchedId, "golden-hold");
        var staleScriptForDirectedSection = new SectionScript(directedId, "golden-hold");
        var regeneratedScript = new SectionScript(directedId, "golden-hold", PropBindings: [new SectionEventBinding(1, "group:Arches")], FillerEffects: RealXLightsFixture.SixCommonEffectNames);

        var fake = new FakeChatCompletionClient([_ => new ChatCompletionResult(JsonSerializer.Serialize(regeneratedScript), new AiUsage(10, 5, null, null))]);
        var generator = new SectionScriptBatchGenerator(fake);

        var existing = new Dictionary<Guid, SectionScript>
        {
            [untouchedId] = untouchedScript,
            [directedId] = staleScriptForDirectedSection,
        };

        var directives = new List<RevisionDirective> { new(directedId, "visual", "Make this section clearly red.", ["visual:" + directedId]) };
        var suffixes = new Dictionary<Guid, string> { [directedId] = "## This section\nSectionId: " + directedId };

        var result = await TargetedSectionRegenerator.RegenerateAsync(
            generator, "stable prefix", existing, directives, suffixes, Guid.NewGuid(), OneLook(), layout, FakeVocabulary(), maxOutputTokens: 500, CancellationToken.None);

        // Exactly one call, and it must be for the directed section only -- the untouched section's id/
        // content never appears in any request the client received.
        Assert.Equal(1, fake.CallCount);
        Assert.Single(fake.Requests);
        Assert.Contains(directedId.ToString(), fake.Requests[0].UserPrompt);
        Assert.DoesNotContain(untouchedId.ToString(), fake.Requests[0].UserPrompt);
        Assert.Contains("Make this section clearly red.", fake.Requests[0].UserPrompt);

        // The untouched section keeps its exact prior instance; the directed section is replaced with the
        // freshly regenerated one (compared by content -- List<T> vs the JSON deserializer's own backing
        // collection type don't compare equal via SectionScript's record equality, only by element).
        Assert.Same(untouchedScript, result[untouchedId]);
        Assert.Equal(regeneratedScript.LookId, result[directedId].LookId);
        Assert.Equal(regeneratedScript.PropBindingsOrEmpty.Select(b => (b.LayerZ, b.Props)), result[directedId].PropBindingsOrEmpty.Select(b => (b.LayerZ, b.Props)));
        Assert.NotSame(staleScriptForDirectedSection, result[directedId]);
    }

    [Fact]
    public async Task RegenerateAsync_DirectiveWithNoSuppliedSuffix_Throws()
    {
        var layout = RealXLightsFixture.IsAvailable ? await LoadLayoutAsync() : new DisplayGraph("none", [], [], []);
        var fake = new FakeChatCompletionClient([]);
        var generator = new SectionScriptBatchGenerator(fake);
        var sectionId = Guid.NewGuid();

        await Assert.ThrowsAsync<ArgumentException>(() => TargetedSectionRegenerator.RegenerateAsync(
            generator, "stable prefix", new Dictionary<Guid, SectionScript>(),
            [new RevisionDirective(sectionId, "visual", "fix it", [])],
            volatileSuffixBySectionId: new Dictionary<Guid, string>(), // no suffix for sectionId
            Guid.NewGuid(), OneLook(), layout, FakeVocabulary(), maxOutputTokens: 500, CancellationToken.None));
    }
}
