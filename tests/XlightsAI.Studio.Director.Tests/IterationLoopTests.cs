using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Refinement;
using XlightsAI.Studio.Refinement.Harness;
using XlightsAI.Studio.Rendering;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Director.Tests;

public sealed class IterationLoopTests
{
    private static readonly BeatGrid EmptyBeatGrid = new(
        Guid.NewGuid(), new SongId(Guid.NewGuid()), "hash", Guid.NewGuid(), Guid.NewGuid(), "v1",
        new BeatGridSettings(4, 0), SelectedTempo: null, Beats: [], Candidates: [], CorrectionVersion: 0);

    private static ChoreographyEngine.Result FakeCompiledResult() =>
        new(ChoreographyIR.Empty, [], [], BarGrid.FromBeatGrid(EmptyBeatGrid, 0));

    private static ExportResult FakeExportResult(string tag) => new($"C:\\fake\\{tag}.xsq", "sha", []);

    private static IterationLoop BuildLoop(
        SectionScriptBatchGenerator batchGenerator,
        Func<IReadOnlyDictionary<Guid, SectionScript>, Task<(ChoreographyEngine.Result, ExportResult)>> compileAndExport,
        Func<ExportResult, CancellationToken, Task<IReadOnlyList<PreviewFrame>>> render,
        VisualCritic? visualCritic = null) =>
        new(batchGenerator, "stable prefix", new Dictionary<Guid, string>(), new Dictionary<Guid, string>(),
            new LookLibrary([]), new DisplayGraph("none", [], [], []), compileAndExport, render,
            new Dictionary<Guid, (long, long)>(), totalDisplayPointCount: 10, visualCritic);

    [Fact]
    public async Task RunFirstAsync_NoDirectivesYet_ProducesIterationZeroWithEmptyRegressions()
    {
        var fake = new FakeChatCompletionClient([]);
        var generator = new SectionScriptBatchGenerator(fake);

        var frame = new PreviewFrame(0, [new PixelPoint(new ModelId("Tree"), 0.5, 0.5, 100, 0, 0)], []);
        var loop = BuildLoop(generator, _ => Task.FromResult((FakeCompiledResult(), FakeExportResult("v0"))), (_, _) => Task.FromResult<IReadOnlyList<PreviewFrame>>([frame]));

        var result = await loop.RunFirstAsync(Guid.NewGuid(), new Dictionary<Guid, SectionScript>(), CancellationToken.None);

        Assert.Equal(0, result.IterationNumber);
        Assert.Empty(result.DirectivesAppliedThisIteration);
        Assert.Empty(result.RegressionsVsPrevious);
        Assert.Equal(0, fake.CallCount); // No regeneration on the first iteration -- it measures/critiques the starting point as-is.
    }

    /// <summary>
    /// Iteration 0 can never have produced a real visual-critique directive (nothing existed yet to compare
    /// its render against), so a zero-directive result there must NOT be treated as convergence -- that
    /// would stop the loop before architecture §9's "compare against the previous" was ever genuinely
    /// attempted. This asserts <see cref="IterationLoop.RunNextAsync"/> instead runs one directive-free
    /// re-measurement pass (no regeneration call) so iteration 1 gets that first real comparison.
    /// </summary>
    [Fact]
    public async Task RunNextAsync_NoActionableFindingsAfterIterationZero_ForcesOneComparisonPassInsteadOfStopping()
    {
        var fake = new FakeChatCompletionClient([]);
        var generator = new SectionScriptBatchGenerator(fake);
        var frame = new PreviewFrame(0, [new PixelPoint(new ModelId("Matrix"), 0.5, 0.5, 100, 100, 100)], []);
        var loop = BuildLoop(generator, _ => Task.FromResult((FakeCompiledResult(), FakeExportResult("v1"))), (_, _) => Task.FromResult<IReadOnlyList<PreviewFrame>>([frame]));

        // A report with no violations and no low soft metrics -> NumericCritic produces no findings -> no directive.
        var cleanReport = new Refinement.QualityReport(Guid.NewGuid(), "harness-v1", [], []);
        var previous = new IterationResult(0, new Dictionary<Guid, SectionScript>(), FakeCompiledResult(), FakeExportResult("v0"), cleanReport, [], [], [], []);

        var next = await loop.RunNextAsync(Guid.NewGuid(), previous, CancellationToken.None);

        Assert.NotNull(next);
        Assert.Equal(1, next!.IterationNumber);
        Assert.Empty(next.DirectivesAppliedThisIteration); // Nothing was regenerated -- this is a measurement-only pass.
        Assert.Equal(0, fake.CallCount); // No targeted regeneration call either -- same scripts, just genuinely re-rendered.
    }

    /// <summary>
    /// From iteration 1 onward, a real cross-iteration comparison was already possible -- a zero-directive
    /// result there IS real convergence, and the special iteration-0 forcing rule above must not apply.
    /// </summary>
    [Fact]
    public async Task RunNextAsync_NoActionableFindingsAfterIterationOne_ReturnsNull_RealConvergence()
    {
        var fake = new FakeChatCompletionClient([]);
        var generator = new SectionScriptBatchGenerator(fake);
        var frame = new PreviewFrame(0, [new PixelPoint(new ModelId("Matrix"), 0.5, 0.5, 100, 100, 100)], []);
        var loop = BuildLoop(generator, _ => Task.FromResult((FakeCompiledResult(), FakeExportResult("v2"))), (_, _) => Task.FromResult<IReadOnlyList<PreviewFrame>>([frame]));

        var cleanReport = new Refinement.QualityReport(Guid.NewGuid(), "harness-v1", [], []);
        var previous = new IterationResult(
            1, new Dictionary<Guid, SectionScript>(), FakeCompiledResult(), FakeExportResult("v1"), cleanReport, [], [], [], [], [frame]);

        var next = await loop.RunNextAsync(Guid.NewGuid(), previous, CancellationToken.None);

        Assert.Null(next);
        Assert.Equal(0, fake.CallCount);
    }

    /// <summary>Iteration 0 must never fabricate a self-vs-self visual comparison, even when a real vision-capable Critic is configured.</summary>
    [Fact]
    public async Task RunFirstAsync_WithVisualCriticConfigured_NeverCallsIt_NoSelfComparison()
    {
        var sectionId = Guid.NewGuid();
        var fake = new FakeChatCompletionClient([]);
        var generator = new SectionScriptBatchGenerator(fake);
        var visionFake = new FakeVisionClient(_ => new ChatCompletionResult("{\"winner\":\"A\",\"confidence\":0.9}", new AiUsage(1, 1, null, null)));
        var visualCritic = new VisualCritic(visionFake, "fake-vision-model", new Random(1));

        var frame = new PreviewFrame(0, [new PixelPoint(new ModelId("Matrix"), 0.5, 0.5, 100, 0, 0)], []);
        var loop = new IterationLoop(
            generator, "stable prefix", new Dictionary<Guid, string>(), new Dictionary<Guid, string> { [sectionId] = "intent" },
            new LookLibrary([]), new DisplayGraph("none", [], [], []),
            _ => Task.FromResult((FakeCompiledResult(), FakeExportResult("v0"))),
            (_, _) => Task.FromResult<IReadOnlyList<PreviewFrame>>([frame]),
            new Dictionary<Guid, (long, long)> { [sectionId] = (0, 1000) },
            totalDisplayPointCount: 10, visualCritic);

        var result = await loop.RunFirstAsync(Guid.NewGuid(), new Dictionary<Guid, SectionScript>(), CancellationToken.None);

        Assert.Empty(result.VisualCritiques);
        Assert.Equal(0, visionFake.CallCount);
    }

    /// <summary>
    /// Once the forced comparison pass (first test above) runs, its visual critique must use iteration 0's
    /// genuinely-saved frame as the reference -- never its own freshly-rendered frame -- proving the
    /// self-comparison bug is actually fixed, not just hidden behind a changed stopping condition.
    /// </summary>
    [Fact]
    public async Task RunNextAsync_ForcedComparisonPass_UsesPreviousIterationsSavedFrame_NotItsOwnRender()
    {
        var sectionId = Guid.NewGuid();
        var fake = new FakeChatCompletionClient([]);
        var generator = new SectionScriptBatchGenerator(fake);

        var iterationZeroFrame = new PreviewFrame(0, [new PixelPoint(new ModelId("Matrix"), 0.5, 0.5, 0, 255, 0)], []); // green
        var forcedPassFrame = new PreviewFrame(0, [new PixelPoint(new ModelId("Matrix"), 0.5, 0.5, 255, 0, 0)], []); // red -- genuinely different render

        var iterationZeroPng = PreviewFrameRasterizer.RasterizeToPng(iterationZeroFrame, pointRadiusPixels: VisualCritic.FrameRasterPointRadiusPixels);
        var visionFake = new FakeVisionClient(request =>
        {
            // Whichever image is iteration 0's own PNG must be present exactly once, as the REFERENCE --
            // never compared against itself, and the candidate (forced-pass render) must be the other one.
            Assert.Contains(request.ImagesPng, img => img.AsSpan().SequenceEqual(iterationZeroPng));
            return new ChatCompletionResult("{\"winner\":\"A\",\"confidence\":0.9}", new AiUsage(1, 1, null, null));
        });
        var visualCritic = new VisualCritic(visionFake, "fake-vision-model", new Random(1));

        var loop = new IterationLoop(
            generator, "stable prefix", new Dictionary<Guid, string>(), new Dictionary<Guid, string> { [sectionId] = "intent" },
            new LookLibrary([]), new DisplayGraph("none", [], [], []),
            _ => Task.FromResult((FakeCompiledResult(), FakeExportResult("v1"))),
            (_, _) => Task.FromResult<IReadOnlyList<PreviewFrame>>([forcedPassFrame]),
            new Dictionary<Guid, (long, long)> { [sectionId] = (0, 1000) },
            totalDisplayPointCount: 10, visualCritic);

        var cleanReport = new Refinement.QualityReport(Guid.NewGuid(), "harness-v1", [], []);
        var previous = new IterationResult(
            0, new Dictionary<Guid, SectionScript>(), FakeCompiledResult(), FakeExportResult("v0"), cleanReport, [], [], [], [], [iterationZeroFrame]);

        var next = await loop.RunNextAsync(Guid.NewGuid(), previous, CancellationToken.None);

        Assert.NotNull(next);
        Assert.Equal(2, visionFake.CallCount); // Both pairwise orderings genuinely issued.
        Assert.Single(next!.VisualCritiques);
    }

    [Fact]
    public async Task RunNextAsync_VisualLossDirective_RegeneratesOnlyThatSection_AndDiffsAgainstPrevious()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return; // SectionScriptValidator needs the real layout fixture -- same convention as other section-regeneration tests.
        }

        var layout = await new ShowLayoutImporter().ImportReadOnlyAsync(RealXLightsFixture.ShowDirectory, CancellationToken.None);
        var lookLibrary = new LookLibrary([
            new Look("golden-hold", "Golden Hold", "Warm hold.", 0.45, "hold", "warm-gold", "group:Outline",
                [new LookLayer(1, "group:Outline", "On", LookLayer.RoleSubject, new Dictionary<string, string>(), "Normal", "Bars", 0.9)]),
        ]);

        var directedSectionId = Guid.NewGuid();
        var regeneratedScript = new SectionScript(directedSectionId, "golden-hold", PropBindings: [new SectionEventBinding(1, "group:Arches")]);
        var fake = new FakeChatCompletionClient([_ => new ChatCompletionResult(System.Text.Json.JsonSerializer.Serialize(regeneratedScript), new AiUsage(10, 5, null, null))]);
        var generator = new SectionScriptBatchGenerator(fake);

        var previousFrame = new PreviewFrame(0, [new PixelPoint(new ModelId("Matrix"), 0.5, 0.5, 200, 0, 0)], []);
        var nextFrame = new PreviewFrame(0, [new PixelPoint(new ModelId("Matrix"), 0.5, 0.5, 200, 0, 0)], []);

        var loop = new IterationLoop(
            generator, "stable prefix",
            volatileSuffixBySectionId: new Dictionary<Guid, string> { [directedSectionId] = "## This section\nSectionId: " + directedSectionId },
            sectionIntentById: new Dictionary<Guid, string> { [directedSectionId] = "a bold red snap" },
            lookLibrary, layout,
            compileAndExportAsync: _ => Task.FromResult((FakeCompiledResult(), FakeExportResult("v1"))),
            renderAsync: (_, _) => Task.FromResult<IReadOnlyList<PreviewFrame>>([nextFrame]),
            sectionWindowsById: new Dictionary<Guid, (long, long)>(),
            totalDisplayPointCount: 10);

        var previousReport = new Refinement.QualityReport(Guid.NewGuid(), "harness-v1",
            [new MetricValue("PixelHeavyDutyCycle", 0.9, 1.0, false, "ok")], []);
        var visualLoss = new SectionVisualCritique(
            directedSectionId, new PairwiseVerdict(PairwiseWinner.Reference, 0.9), new PairwiseVerdict(PairwiseWinner.Reference, 0.9),
            FlippedBetweenOrderings: false, AgreedWinner: PairwiseWinner.Reference);

        var previous = new IterationResult(
            0, new Dictionary<Guid, SectionScript> { [directedSectionId] = new(directedSectionId, "golden-hold") },
            FakeCompiledResult(), FakeExportResult("v0"), previousReport, [], [visualLoss], [], []);

        var next = await loop.RunNextAsync(Guid.NewGuid(), previous, CancellationToken.None);

        Assert.NotNull(next);
        Assert.Equal(1, next!.IterationNumber);
        Assert.Equal(1, fake.CallCount); // Exactly one targeted regeneration call, for the directed section only.
        Assert.Single(next.DirectivesAppliedThisIteration);
        Assert.Equal(directedSectionId, next.DirectivesAppliedThisIteration[0].SectionId);
    }

    [Fact]
    public async Task RunBestOfNAsync_RejectsOutOfRangeCandidateCount()
    {
        var fake = new FakeChatCompletionClient([]);
        var generator = new SectionScriptBatchGenerator(fake);
        var loop = BuildLoop(generator, _ => Task.FromResult((FakeCompiledResult(), FakeExportResult("v"))), (_, _) => Task.FromResult<IReadOnlyList<PreviewFrame>>([]));
        var previous = new IterationResult(0, new Dictionary<Guid, SectionScript>(), FakeCompiledResult(), FakeExportResult("v0"),
            new Refinement.QualityReport(Guid.NewGuid(), "v1", [], []), [], [], [], []);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => loop.RunBestOfNAsync(Guid.NewGuid(), previous, candidateCount: 2, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => loop.RunBestOfNAsync(Guid.NewGuid(), previous, candidateCount: 9, CancellationToken.None));
    }

    /// <summary>Minimal local fake -- same shape as <c>VisualCriticTests</c>'s own nested fake, kept file-local since no shared one exists yet.</summary>
    private sealed class FakeVisionClient : IVisionCapableChatCompletionClient
    {
        private readonly Func<MultimodalChatCompletionRequest, ChatCompletionResult> _respond;
        public int CallCount { get; private set; }

        public FakeVisionClient(Func<MultimodalChatCompletionRequest, ChatCompletionResult> respond) => _respond = respond;

        public Task<ChatCompletionResult> CompleteAsync(MultimodalChatCompletionRequest request, CancellationToken ct)
        {
            CallCount++;
            return Task.FromResult(_respond(request));
        }
    }
}
