using System.Text.Json;
using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Knowledge;

namespace XlightsAI.Studio.Director.Tests;

/// <summary>
/// Build-plan Phase 6 task 2: "issue the ~30 per-section calls via the provider's Batch API where
/// available... sharing the cached Show Bible + vocabulary + craft-rules prefix." Covers both branches:
/// a client implementing <see cref="IBatchChatCompletionClient"/> (real batch path) and one that does not
/// (bounded-concurrency individual-call fallback, "where available" literally).
/// </summary>
public sealed class SectionScriptBatchGeneratorTests
{
    private static async Task<(XlightsAI.Studio.Xlights.DisplayGraph Layout, LookLibrary Looks)> LoadFixturesAsync()
    {
        var layout = await new XlightsAI.Studio.Xlights.ShowLayoutImporter().ImportReadOnlyAsync(RealXLightsFixture.ShowDirectory, CancellationToken.None);
        return (layout, new LookLibrary([
            new Look("golden-hold", "Golden Hold", "Warm hold.", 0.45, "hold", "warm-gold", "group:Outline",
                [new LookLayer(1, "group:Outline", "On", LookLayer.RoleSubject, new Dictionary<string, string>(), "Normal", "Bars", 0.9)]),
        ]));
    }

    private static string ToResponseText(SectionScript script) => JsonSerializer.Serialize(script);

    private static EffectVocabularyCatalog FakeVocabulary() => new(
        RealXLightsFixture.SixCommonEffectNames.ToDictionary(name => name, name => new EffectVocabulary(name, "E_", []), StringComparer.Ordinal),
        buffer: new EffectVocabulary("Buffer", "B_", []),
        blending: new EffectVocabulary("Blending", "T_", []),
        color: new EffectVocabulary("Color", "C_", []));

    [Fact]
    public async Task GenerateAllAsync_BatchCapableClient_AllFirstAttemptsValid_OneSharedBatchCall_NoIndividualFallback()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var (layout, looks) = await LoadFixturesAsync();
        var items = Enumerable.Range(0, 5).Select(_ => new SectionScriptBatchItem(Guid.NewGuid(), "VOLATILE")).ToList();

        var client = new FakeBatchChatCompletionClient(requests =>
            requests.Select((r, i) => BatchItemResult.Success(new ChatCompletionResult(
                ToResponseText(new SectionScript(items[i].SectionId, "golden-hold", FillerEffects: RealXLightsFixture.SixCommonEffectNames)), new AiUsage(500, 100, 400, 0)))).ToList());

        var generator = new SectionScriptBatchGenerator(client);
        var songId = Guid.NewGuid();

        var reports = await generator.GenerateAllAsync("STABLE", items, songId, looks, layout, FakeVocabulary(), 1000, CancellationToken.None);

        Assert.Equal(5, reports.Count);
        Assert.All(reports.Values, r => Assert.True(r.ValidatedOnFirstAttempt));
        Assert.Single(client.BatchCalls); // one shared batch call for every section.
        Assert.Equal(0, client.SingleCallCount);
    }

    [Fact]
    public async Task GenerateAllAsync_BatchCapableClient_OneSectionFailsValidation_FallsBackToIndividualRetryForThatSectionOnly()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var (layout, looks) = await LoadFixturesAsync();
        var goodSection = Guid.NewGuid();
        var badSection = Guid.NewGuid();
        var items = new List<SectionScriptBatchItem> { new(goodSection, "VOLATILE-GOOD"), new(badSection, "VOLATILE-BAD") };

        var client = new RetryCapableBatchClient(
            firstBatchResponse: requests =>
            [
                BatchItemResult.Success(new ChatCompletionResult(ToResponseText(new SectionScript(goodSection, "golden-hold", FillerEffects: RealXLightsFixture.SixCommonEffectNames)), new AiUsage(500, 100, 0, 0))),
                BatchItemResult.Success(new ChatCompletionResult(ToResponseText(new SectionScript(badSection, "no-such-look")), new AiUsage(500, 100, 0, 0))),
            ],
            individualResponse: _ => new ChatCompletionResult(ToResponseText(new SectionScript(badSection, "golden-hold", FillerEffects: RealXLightsFixture.SixCommonEffectNames)), new AiUsage(500, 100, 0, 0)));

        var generator = new SectionScriptBatchGenerator(client);
        var reports = await generator.GenerateAllAsync("STABLE", items, Guid.NewGuid(), looks, layout, FakeVocabulary(), 1000, CancellationToken.None);

        Assert.Equal(2, reports.Count);
        Assert.True(reports[goodSection].ValidatedOnFirstAttempt);
        Assert.NotNull(reports[badSection].Script);
        Assert.Equal("golden-hold", reports[badSection].Script!.LookId);
        Assert.Equal(1, client.IndividualCallCount); // only the bad section fell back to an individual call.
    }

    [Fact]
    public async Task GenerateAllAsync_NonBatchCapableClient_FallsBackToIndividualCallsForEverySection()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var (layout, looks) = await LoadFixturesAsync();
        var items = Enumerable.Range(0, 3).Select(_ => new SectionScriptBatchItem(Guid.NewGuid(), "VOLATILE")).ToList();

        var client = new FakeChatCompletionClient(
            items.Select(item => (Func<ChatCompletionRequest, ChatCompletionResult>)(_ =>
                new ChatCompletionResult(ToResponseText(new SectionScript(item.SectionId, "golden-hold", FillerEffects: RealXLightsFixture.SixCommonEffectNames)), new AiUsage(500, 100, 0, 0)))));

        var generator = new SectionScriptBatchGenerator(client);
        var reports = await generator.GenerateAllAsync("STABLE", items, Guid.NewGuid(), looks, layout, FakeVocabulary(), 1000, CancellationToken.None);

        Assert.Equal(3, reports.Count);
        Assert.Equal(3, client.CallCount);
    }

    /// <summary>A fake that implements both interfaces but routes individual calls through a separate responder -- used to verify the fallback actually re-runs only the sections that need it.</summary>
    private sealed class RetryCapableBatchClient : IChatCompletionClient, IBatchChatCompletionClient
    {
        private readonly Func<IReadOnlyList<ChatCompletionRequest>, IReadOnlyList<BatchItemResult>> _firstBatchResponse;
        private readonly Func<ChatCompletionRequest, ChatCompletionResult> _individualResponse;

        public RetryCapableBatchClient(
            Func<IReadOnlyList<ChatCompletionRequest>, IReadOnlyList<BatchItemResult>> firstBatchResponse,
            Func<ChatCompletionRequest, ChatCompletionResult> individualResponse)
        {
            _firstBatchResponse = firstBatchResponse;
            _individualResponse = individualResponse;
        }

        public int IndividualCallCount { get; private set; }

        public Task<ChatCompletionResult> CompleteAsync(ChatCompletionRequest request, CancellationToken ct)
        {
            IndividualCallCount++;
            return Task.FromResult(_individualResponse(request));
        }

        public Task<IReadOnlyList<BatchItemResult>> CompleteBatchAsync(IReadOnlyList<ChatCompletionRequest> requests, CancellationToken ct) =>
            Task.FromResult(_firstBatchResponse(requests));
    }
}
