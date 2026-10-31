using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Refinement;
using XlightsAI.Studio.Refinement.Harness;
using XlightsAI.Studio.Rendering;

namespace XlightsAI.Studio.Director.Tests;

public sealed class VisualCriticTests
{
    [Fact]
    public async Task CompareAsync_IssuesTwoCallsInOppositeOrder_AndAgreesWhenBothPickCandidate()
    {
        var candidate = SolidFrame(255, 0, 0); // genuinely red
        var reference = SolidFrame(0, 255, 0); // green, so the two frames are distinguishable by content
        // VisualCritic itself rasterizes at VisualCritic.FrameRasterPointRadiusPixels internally -- match
        // that here so this fake's own byte-equality check actually lines up with what it receives.
        var candidatePng = PreviewFrameRasterizer.RasterizeToPng(candidate, pointRadiusPixels: VisualCritic.FrameRasterPointRadiusPixels);

        var fake = new FakeVisionChatCompletionClient(request =>
            // Both orderings: pick whichever position the *candidate* frame actually landed in, by
            // comparing raw bytes -- a real provider never needs this, but it lets the test assert the
            // critic genuinely alternates order between its two calls rather than sending the same
            // request twice.
            new ChatCompletionResult(
                $"{{\"winner\":\"{(request.ImagesPng[0].AsSpan().SequenceEqual(candidatePng) ? "A" : "B")}\",\"confidence\":0.9}}",
                new AiUsage(10, 5, null, null)));

        var critic = new VisualCritic(fake, "fake-vision-model", new Random(1));

        var result = await critic.CompareAsync(Guid.NewGuid(), candidate, reference, "the hero prop should read as red", CancellationToken.None);

        Assert.Equal(2, fake.Requests.Count);
        // The two calls' first images must differ (order genuinely alternated).
        Assert.False(fake.Requests[0].ImagesPng[0].AsSpan().SequenceEqual(fake.Requests[1].ImagesPng[0]));
        Assert.False(result.FlippedBetweenOrderings);
        Assert.Equal(PairwiseWinner.Candidate, result.AgreedWinner);
    }

    [Fact]
    public async Task CompareAsync_OrderingsDisagree_ReportsFlippedWithNoAgreedWinner()
    {
        // Always picking the literal same position label ("A") across both calls means picking the
        // *opposite* underlying frame each time, since VisualCritic reverses which frame occupies "A"
        // between its two calls -- i.e. a pure, unconditional position bias, which is exactly the real
        // failure mode architecture §9's "randomize order and run both" discipline exists to catch.
        var fake = new FakeVisionChatCompletionClient(_ =>
            new ChatCompletionResult("{\"winner\":\"A\",\"confidence\":0.6}", new AiUsage(10, 5, null, null)));

        var critic = new VisualCritic(fake, "fake-vision-model", new Random(1));

        var result = await critic.CompareAsync(
            Guid.NewGuid(), SolidFrame(255, 0, 0), SolidFrame(0, 255, 0), "intent text", CancellationToken.None);

        Assert.True(result.FlippedBetweenOrderings);
        Assert.Null(result.AgreedWinner);
    }

    [Fact]
    public async Task CompareAsync_MalformedWinnerField_ThrowsRejected()
    {
        var fake = new FakeVisionChatCompletionClient(_ => new ChatCompletionResult("{\"winner\":\"C\",\"confidence\":0.5}", new AiUsage(10, 5, null, null)));
        var critic = new VisualCritic(fake, "fake-vision-model", new Random(1));

        await Assert.ThrowsAsync<AiCallRejectedException>(() =>
            critic.CompareAsync(Guid.NewGuid(), SolidFrame(255, 0, 0), SolidFrame(0, 255, 0), "intent", CancellationToken.None));
    }

    /// <summary>
    /// Build-plan Phase 7c's required acceptance criterion, made deterministic: "the visual critique
    /// catches at least one defect class the numeric battery alone would have missed (replicate the 'red
    /// snap doesn't read as red' scenario deliberately to confirm)." This drives the real
    /// <see cref="SequenceMetricBattery"/> (Phase 7b) over both frames to show the numeric channel alone
    /// has no way to prefer the genuinely-red frame (it has no "redness" metric at all -- only brightness/
    /// coverage/hue-family-*count*, none of which single out one specific hue as correct), then drives the
    /// real <see cref="VisualCritic"/> (scripted judge, so the result is deterministic rather than
    /// dependent on one small free local model's real-world accuracy -- see
    /// <see cref="RealVisualCriticRedSnapTests"/> for the real-model empirical run) to show it correctly
    /// identifies the true-red frame given a judge that actually looks at hue.
    /// </summary>
    [Fact]
    public async Task CompareAsync_RedSnapDefect_NumericBatteryWouldMissIt_VisualCriticCatchesItGivenACorrectJudge()
    {
        var trueRed = SolidFrame(200, 0, 0);
        var redSnapMix = new PreviewFrame(0,
        [
            new PixelPoint(new ModelId("Matrix"), 0.2, 0.5, 255, 255, 255),
            new PixelPoint(new ModelId("Matrix"), 0.5, 0.5, 0, 255, 0),
            new PixelPoint(new ModelId("Matrix"), 0.8, 0.5, 255, 0, 0),
        ], []);

        // The numeric battery has no metric that singles out "redness" as correct -- MeanHueFamilies
        // penalizes *many* hues regardless of which ones, and no brightness/coverage metric encodes a
        // target color at all. A battery run over the red-snap mix does not flag it as defective on hue
        // grounds the way a "must read as red" intent would require.
        var context = new HarnessSequenceContext(Frames: [redSnapMix], TotalDisplayPointCount: 10);
        var report = SequenceMetricBattery.Evaluate(Guid.NewGuid(), context);
        Assert.DoesNotContain(report.Metrics, m => m.Name == "MeanHueFamilies" && m.Value == 0.0 && m.HardGate);

        var scriptedJudge = new FakeVisionChatCompletionClient(request =>
        {
            // A judge that actually looks at hue: picks whichever image is the solid-red one (detected
            // here by its PNG bytes, the same way a real vision model would detect it by pixels).
            var trueRedPng = PreviewFrameRasterizer.RasterizeToPng(trueRed, pointRadiusPixels: VisualCritic.FrameRasterPointRadiusPixels);
            var firstIsTrueRed = request.ImagesPng[0].AsSpan().SequenceEqual(trueRedPng);
            return new ChatCompletionResult($"{{\"winner\":\"{(firstIsTrueRed ? "A" : "B")}\",\"confidence\":0.95}}", new AiUsage(10, 5, null, null));
        });

        var critic = new VisualCritic(scriptedJudge, "scripted-correct-judge", new Random(7));
        var result = await critic.CompareAsync(
            Guid.NewGuid(), candidateFrame: trueRed, referenceFrame: redSnapMix,
            sectionIntent: "This moment must read as a bold, dominant RED snap.", CancellationToken.None);

        Assert.Equal(PairwiseWinner.Candidate, result.AgreedWinner);
        Assert.False(result.FlippedBetweenOrderings);
    }

    private static PreviewFrame SolidFrame(byte r, byte g, byte b) =>
        new(0, [new PixelPoint(new ModelId("Hero"), 0.5, 0.5, r, g, b)], []);

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
