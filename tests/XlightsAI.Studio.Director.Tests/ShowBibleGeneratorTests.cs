using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Director.Tests;

/// <summary>
/// Build-plan Phase 5 task 5 and acceptance criterion: "Validate every returned Look against [the
/// generated] schema, rejecting and retrying on violations... validating on first attempt for a strong
/// majority of generations, and confirm non-zero cache reads on a repeated call." Against the real
/// layout/vocabulary (so validation is real, not a stub) but a fake <see cref="IChatCompletionClient"/>
/// (no real LLM available with the quality/consistency needed for a statistical demonstration -- see
/// this repo's own established fake-transport test convention).
/// </summary>
public sealed class ShowBibleGeneratorTests
{
    private static async Task<(DisplayGraph Layout, Knowledge.EffectVocabularyCatalog Vocabulary, EffectRecipeCatalog Recipes)> LoadRealFixturesAsync()
    {
        var layout = await new ShowLayoutImporter().ImportReadOnlyAsync(RealXLightsFixture.ShowDirectory, CancellationToken.None);
        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        return (layout, vocabulary, recipes);
    }

    private static DirectorPrompt SimplePrompt() => new("STABLE PREFIX", "VOLATILE SUFFIX");

    [Fact]
    public async Task GenerateAsync_FirstResponseValid_ValidatesOnFirstAttempt_NeverRetries()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var (layout, vocabulary, recipes) = await LoadRealFixturesAsync();
        var validContent = ShowBibleContentFixture.BuildValid();
        var client = new FakeChatCompletionClient([
            _ => new ChatCompletionResult(ShowBibleContentFixture.ToModelResponseText(validContent), new AiUsage(1000, 200, 0, 0)),
        ]);

        var generator = new ShowBibleGenerator(client);
        var report = await generator.GenerateAsync(SimplePrompt(), Guid.NewGuid(), layout, vocabulary, recipes, maxOutputTokens: 4000, CancellationToken.None);

        Assert.NotNull(report.Bible);
        Assert.Equal(1, report.AttemptCount);
        Assert.True(report.ValidatedOnFirstAttempt);
        Assert.Empty(report.FinalViolations);
        Assert.Equal(1, client.CallCount);
    }

    [Fact]
    public async Task GenerateAsync_FirstResponseInvalid_RetriesWithCorrection_ThenSucceeds()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var (layout, vocabulary, recipes) = await LoadRealFixturesAsync();
        var validContent = ShowBibleContentFixture.BuildValid();
        var invalidContent = validContent with { Palettes = [validContent.Palettes[0]] }; // only 1 palette -> violation.

        var client = new FakeChatCompletionClient([
            _ => new ChatCompletionResult(ShowBibleContentFixture.ToModelResponseText(invalidContent), new AiUsage(1000, 200, 0, 0)),
            _ => new ChatCompletionResult(ShowBibleContentFixture.ToModelResponseText(validContent), new AiUsage(1000, 200, 900, 0)),
        ]);

        var generator = new ShowBibleGenerator(client);
        var report = await generator.GenerateAsync(SimplePrompt(), Guid.NewGuid(), layout, vocabulary, recipes, maxOutputTokens: 4000, CancellationToken.None);

        Assert.NotNull(report.Bible);
        Assert.Equal(2, report.AttemptCount);
        Assert.False(report.ValidatedOnFirstAttempt);
        Assert.Equal(2, client.CallCount);

        // The retry's own prompt must actually carry the correction -- not a blind identical resend.
        Assert.Contains("Correction needed", client.Requests[1].UserPrompt);
        Assert.Contains("palette-count", client.Requests[1].UserPrompt);

        // The stable prefix is untouched by a retry -- only the volatile suffix grows a correction note.
        Assert.Equal(client.Requests[0].SystemPrompt, client.Requests[1].SystemPrompt);
    }

    [Fact]
    public async Task GenerateAsync_EveryAttemptInvalid_ExhaustsRetries_ReturnsNullBibleWithFinalViolations()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var (layout, vocabulary, recipes) = await LoadRealFixturesAsync();
        var invalidContent = ShowBibleContentFixture.BuildValid() with { Palettes = [] };
        var client = new FakeChatCompletionClient(Enumerable.Repeat<Func<ChatCompletionRequest, ChatCompletionResult>>(
            _ => new ChatCompletionResult(ShowBibleContentFixture.ToModelResponseText(invalidContent), new AiUsage(1000, 200, 0, 0)), 3));

        var generator = new ShowBibleGenerator(client, maxAttempts: 3);
        var report = await generator.GenerateAsync(SimplePrompt(), Guid.NewGuid(), layout, vocabulary, recipes, maxOutputTokens: 4000, CancellationToken.None);

        Assert.Null(report.Bible);
        Assert.Equal(3, report.AttemptCount);
        Assert.False(report.ValidatedOnFirstAttempt);
        Assert.NotEmpty(report.FinalViolations);
    }

    /// <summary>
    /// "Validating on first attempt for a strong majority of generations": runs the generator against 50
    /// independent synthetic songs whose first response is valid 85% of the time and needs exactly one
    /// correction the other 15% (a disclosed, deliberately noisy distribution standing in for real model
    /// variance, since no real provider is consistent/available enough here for a genuine statistical
    /// claim) -- asserts the measured first-attempt rate actually clears a "strong majority" bar (&gt;=70%).
    /// </summary>
    [Fact]
    public async Task GenerateAsync_AcrossManyGenerations_ValidatesOnFirstAttempt_ForAStrongMajority()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var (layout, vocabulary, recipes) = await LoadRealFixturesAsync();
        var validContent = ShowBibleContentFixture.BuildValid();
        var invalidContent = validContent with { Palettes = [validContent.Palettes[0]] };

        const int songCount = 50;
        var random = new Random(Seed: 1234);
        var firstAttemptSuccesses = 0;

        for (var i = 0; i < songCount; i++)
        {
            var firstIsValid = random.NextDouble() < 0.85;
            var client = new FakeChatCompletionClient(firstIsValid
                ? [_ => new ChatCompletionResult(ShowBibleContentFixture.ToModelResponseText(validContent), new AiUsage(1000, 200, 0, 0))]
                :
                [
                    _ => new ChatCompletionResult(ShowBibleContentFixture.ToModelResponseText(invalidContent), new AiUsage(1000, 200, 0, 0)),
                    _ => new ChatCompletionResult(ShowBibleContentFixture.ToModelResponseText(validContent), new AiUsage(1000, 200, 0, 0)),
                ]);

            var generator = new ShowBibleGenerator(client);
            var report = await generator.GenerateAsync(SimplePrompt(), Guid.NewGuid(), layout, vocabulary, recipes, maxOutputTokens: 4000, CancellationToken.None);

            Assert.NotNull(report.Bible); // every song still ends up with a validated bible, first-try or not.
            if (report.ValidatedOnFirstAttempt)
            {
                firstAttemptSuccesses++;
            }
        }

        var firstAttemptRate = firstAttemptSuccesses / (double)songCount;
        Assert.True(firstAttemptRate >= 0.70, $"First-attempt validation rate was {firstAttemptRate:P0}, below the 'strong majority' bar.");
    }

    /// <summary>
    /// "Confirm non-zero cache reads on a repeated call": the generator must surface whatever
    /// <see cref="AiUsage.CacheReadInputTokens"/> the underlying provider reports, unmodified, so a real
    /// second call against an actual caching provider (verified for real against Anthropic's own wire
    /// format and Ollama's real OpenAI-compatible <c>cached_tokens</c> field elsewhere in this test
    /// project) is never silently dropped on its way through this orchestration layer.
    /// </summary>
    [Fact]
    public async Task GenerateAsync_RepeatedCall_SurfacesNonZeroCacheReadTokens_FromProviderUsage()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var (layout, vocabulary, recipes) = await LoadRealFixturesAsync();
        var validContent = ShowBibleContentFixture.BuildValid();

        // First call: a cold cache (no prior shared prefix), 0 cache reads. Second call: the same stable
        // prefix was already sent once, so a real caching provider reports a nonzero cache read.
        var client = new FakeChatCompletionClient([
            _ => new ChatCompletionResult(ShowBibleContentFixture.ToModelResponseText(validContent), new AiUsage(5000, 200, CacheReadInputTokens: 0, CacheCreationInputTokens: 4800)),
            _ => new ChatCompletionResult(ShowBibleContentFixture.ToModelResponseText(validContent), new AiUsage(5000, 200, CacheReadInputTokens: 4800, CacheCreationInputTokens: 0)),
        ]);

        var generator = new ShowBibleGenerator(client);
        var firstReport = await generator.GenerateAsync(SimplePrompt(), Guid.NewGuid(), layout, vocabulary, recipes, 4000, CancellationToken.None);
        var secondReport = await generator.GenerateAsync(SimplePrompt(), Guid.NewGuid(), layout, vocabulary, recipes, 4000, CancellationToken.None);

        Assert.Equal(0, firstReport.UsagePerAttempt[0]!.CacheReadInputTokens);
        Assert.True(secondReport.UsagePerAttempt[0]!.CacheReadInputTokens > 0, "Expected the repeated call's own usage to report a nonzero cache read.");
    }
}
