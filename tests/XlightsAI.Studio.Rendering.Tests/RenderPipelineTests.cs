using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Rendering.Tests;

public sealed class RenderPipelineTests
{
    private sealed class FakeRenderAdapter(RenderArtifact result) : IRenderAdapter
    {
        public Task<RenderArtifact> RenderAsync(RenderRequest request, CancellationToken ct) => Task.FromResult(result);
    }

    private sealed class FakeFrameDecoder(DecodedRenderOutput output) : IRenderedFrameDecoder
    {
        public DecodedRenderOutput Decode(string fseqPath) => output;
    }

    private sealed class FakeStructuralReader(XsqStructuralDocument document) : IXsqStructuralReader
    {
        public XsqStructuralDocument Read(string path) => document;
    }

    private static XsqStructuralDocument SingleModelDocument(string modelName) => new(
        "2026.03", 25, 1.0, [], [], [new XsqStructuralElement("model", modelName, [])], [], []);

    [Fact]
    public async Task RunAsync_SuccessfulRender_DecodesAndProjectsFrames()
    {
        var model = new DisplayModel(new ModelId("ModelA"), "ModelA", "Single Line", [], [new Point2(0.1, 0.2)], StringType: "RGB Nodes", NodeCount: 1);
        var layout = new DisplayGraph("hash", [model], [], []);

        var artifact = new RenderArtifact(Guid.NewGuid(), RenderAuthority.ActualXlights, Success: true, "1.0", "fake.fseq", [], new string('a', 64));
        var decoded = new DecodedRenderOutput(3, 25, [new DecodedRenderFrame(0, [255, 255, 255])]);
        var pipeline = new RenderPipeline(new FakeRenderAdapter(artifact), new FakeFrameDecoder(decoded), new FakeStructuralReader(SingleModelDocument("ModelA")));

        var result = await pipeline.RunAsync(
            new RenderRequest(Guid.NewGuid(), "show", "seq.xsq", "out", Selection: null, RenderAuthority.ActualXlights),
            layout, CancellationToken.None);

        Assert.True(result.Artifact.Success);
        Assert.NotNull(result.Frames);
        Assert.Single(Assert.Single(result.Frames!).Illuminated);
        Assert.Empty(result.ProjectionWarnings);
    }

    [Fact]
    public async Task RunAsync_FailedRender_ReturnsNullFramesWithoutDecoding()
    {
        var artifact = new RenderArtifact(Guid.NewGuid(), RenderAuthority.ActualXlights, Success: false, "1.0", null, ["render failed"], new string('b', 64));
        var pipeline = new RenderPipeline(
            new FakeRenderAdapter(artifact),
            new FakeFrameDecoder(new DecodedRenderOutput(1, 25, [])),
            new FakeStructuralReader(SingleModelDocument("ModelA")));

        var result = await pipeline.RunAsync(
            new RenderRequest(Guid.NewGuid(), "show", "seq.xsq", "out", Selection: null, RenderAuthority.ActualXlights),
            new DisplayGraph("hash", [], [], []), CancellationToken.None);

        Assert.False(result.Artifact.Success);
        Assert.Null(result.Frames);
    }
}
