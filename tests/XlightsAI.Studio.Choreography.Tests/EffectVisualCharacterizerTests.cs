using XlightsAI.Studio.Director;

namespace XlightsAI.Studio.Choreography.Tests;

/// <summary>
/// Build-plan Phase 18 §5 (Phase D). Same "real data by necessity, not choice" convention as
/// <see cref="EffectFixtureRendererTests"/> -- real fixture frames by necessity (no invented effect names),
/// but a scripted fake <see cref="IVisionCapableChatCompletionClient"/> rather than a real, paid AI call --
/// same split <see cref="Director.Tests.VisualCriticTests"/>/<see cref="Director.Tests.RealVisualCriticRedSnapTests"/>
/// already establishes (a deterministic unit test against a fake client, a separate real-model integration
/// test for the actual provider). Skips cleanly (never fails) when <see cref="RealXLightsFixture.IsRenderAvailable"/>
/// is false.
/// </summary>
public sealed class EffectVisualCharacterizerTests : IDisposable
{
    private readonly string _outputRoot = Path.Combine(Path.GetTempPath(), "xlai-effect-fixture-characterize-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_outputRoot))
        {
            Directory.Delete(_outputRoot, recursive: true);
        }
    }

    private async Task<EffectFixtureRenderManifest> RenderSmallManifestAsync(params string[] effectNames)
    {
        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var fullManifest = EffectFixturePlanner.Build(vocabulary, recipes, RealXLightsFixture.EffectMetadataRoot);
        var manifest = fullManifest with
        {
            Fixtures = fullManifest.Fixtures.Where(f => effectNames.Contains(f.EffectName, StringComparer.Ordinal)).ToList(),
        };
        return await EffectFixtureRenderer.RenderAsync(
            manifest, vocabulary, recipes, RealXLightsFixture.XlightsExecutablePath, _outputRoot, log: null, CancellationToken.None);
    }

    private const string ScriptedDescriptionJson =
        "{\"motionType\":\"static-hold\",\"direction\":\"none\",\"apparentSpeed\":\"none\"," +
        "\"textureDensity\":\"sparse\",\"summary\":\"A solid colour holds steady across the whole fixture.\"}";

    [Fact]
    public async Task CharacterizeAsync_WellKnownOnEffect_ProducesARealParsedDescriptionOnBothTiers()
    {
        if (!RealXLightsFixture.IsRenderAvailable)
        {
            return;
        }

        var renderManifest = await RenderSmallManifestAsync("On");
        var fake = new FakeVisionChatCompletionClient(_ => new ChatCompletionResult(ScriptedDescriptionJson, new AiUsage(100, 20, null, null)));

        var manifest = await EffectVisualCharacterizer.CharacterizeAsync(renderManifest, fake, "fake-vision-model", log: null, CancellationToken.None);

        Assert.Equal(2, manifest.Outcomes.Count);
        foreach (var outcome in manifest.Outcomes)
        {
            Assert.True(
                outcome.Status == EffectVisualCharacterizationStatus.Characterized,
                $"{outcome.EffectName} ({outcome.ResolutionTier}) was not characterized: {outcome.FailureReason}");
            Assert.NotNull(outcome.Description);
            Assert.Equal("static-hold", outcome.Description!.MotionType);
            Assert.False(string.IsNullOrWhiteSpace(outcome.Description.Summary));
        }

        // Real evidence the request actually carried multiple real, temporally-ordered stills (not the
        // whole video, per the build plan's own disclosed fallback) -- one call per fixture, 6 images each.
        Assert.Equal(2, fake.Requests.Count);
        Assert.All(fake.Requests, r => Assert.Equal(6, r.ImagesPng.Count));
    }

    [Fact]
    public async Task CharacterizeAsync_RenderSkippedFixture_IsRecordedWithoutAttemptingAnAiCall()
    {
        if (!RealXLightsFixture.IsRenderAvailable)
        {
            return;
        }

        var renderManifest = await RenderSmallManifestAsync("On");
        var skippedOutcome = renderManifest.Outcomes.First() with
        {
            Status = EffectFixtureRenderStatus.RenderFailed,
            FailureReason = "synthetic failure for this test",
            FramesPath = null,
        };
        var manifest = renderManifest with { Outcomes = [skippedOutcome] };
        var fake = new FakeVisionChatCompletionClient(_ => new ChatCompletionResult(ScriptedDescriptionJson, new AiUsage(10, 5, null, null)));

        var result = await EffectVisualCharacterizer.CharacterizeAsync(manifest, fake, "fake-vision-model", log: null, CancellationToken.None);

        var outcome = Assert.Single(result.Outcomes);
        Assert.Equal(EffectVisualCharacterizationStatus.RenderSkipped, outcome.Status);
        Assert.Contains("synthetic failure for this test", outcome.FailureReason);
        Assert.Null(outcome.Description);
        Assert.Empty(fake.Requests); // never attempted a real/fake AI call for a fixture with no real frames.
    }

    [Fact]
    public async Task CharacterizeAsync_MalformedAiResponse_RecordsAiCallFailedWithoutCrashingTheWholeSweep()
    {
        if (!RealXLightsFixture.IsRenderAvailable)
        {
            return;
        }

        var renderManifest = await RenderSmallManifestAsync("On");
        var fake = new FakeVisionChatCompletionClient(_ => new ChatCompletionResult("not json at all", new AiUsage(10, 5, null, null)));

        var manifest = await EffectVisualCharacterizer.CharacterizeAsync(renderManifest, fake, "fake-vision-model", log: null, CancellationToken.None);

        Assert.Equal(2, manifest.Outcomes.Count);
        Assert.All(manifest.Outcomes, o =>
        {
            Assert.Equal(EffectVisualCharacterizationStatus.AiCallFailed, o.Status);
            Assert.False(string.IsNullOrWhiteSpace(o.FailureReason));
        });
    }

    [Fact]
    public async Task JsonEffectVisualCharacterizationManifestStore_SaveThenLoad_RoundTripsTheManifest()
    {
        if (!RealXLightsFixture.IsRenderAvailable)
        {
            return;
        }

        var renderManifest = await RenderSmallManifestAsync("On");
        var fake = new FakeVisionChatCompletionClient(_ => new ChatCompletionResult(ScriptedDescriptionJson, new AiUsage(10, 5, null, null)));
        var manifest = await EffectVisualCharacterizer.CharacterizeAsync(renderManifest, fake, "fake-vision-model", log: null, CancellationToken.None);

        var store = new JsonEffectVisualCharacterizationManifestStore(_outputRoot);
        await store.SaveAsync(manifest, CancellationToken.None);

        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(manifest.Outcomes.Count, loaded.Outcomes.Count);
        Assert.Equal(manifest.CharacterizedCount, loaded.CharacterizedCount);
        Assert.Equal(manifest.Outcomes[0].Description?.Summary, loaded.Outcomes[0].Description?.Summary);
    }

    private sealed class FakeVisionChatCompletionClient : IVisionCapableChatCompletionClient
    {
        private readonly Func<MultimodalChatCompletionRequest, ChatCompletionResult> _respond;
        public List<MultimodalChatCompletionRequest> Requests { get; } = [];

        public FakeVisionChatCompletionClient(Func<MultimodalChatCompletionRequest, ChatCompletionResult> respond) => _respond = respond;

        public Task<ChatCompletionResult> CompleteAsync(MultimodalChatCompletionRequest request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.FromResult(_respond(request));
        }
    }
}
