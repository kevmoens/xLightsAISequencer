using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Rendering.Tests;

public sealed class RenderedFrameProjectorTests
{
    private static DisplayGraph Layout(params DisplayModel[] models) => new("hash", models, [], []);

    private static Dictionary<ModelId, IReadOnlyList<ModelStringChannelSpan>> SingleSpan(ModelId id, long offset, int nodeCount) =>
        new() { [id] = [new ModelStringChannelSpan(offset, nodeCount)] };

    [Fact]
    public void Project_LitNodesBecomeIlluminatedPixelsWithRealDecodedColor()
    {
        var model = new DisplayModel(
            new ModelId("ModelA"), "ModelA", "Single Line", [], [new Point2(0.25, 0.5)],
            StringType: "RGB Nodes", NodeCount: 2);
        var layout = Layout(model);
        var spans = SingleSpan(model.Id, 0, 2);

        // 2 nodes * 3 channels = 6 channels total. Node 0 lit red, node 1 fully black.
        var decoded = new DecodedRenderOutput(6, 25, [new DecodedRenderFrame(0, [255, 0, 0, 0, 0, 0])]);

        var result = RenderedFrameProjector.Project(decoded, layout, spans);

        Assert.Empty(result.SkippedModels);
        var frame = Assert.Single(result.Frames);
        var pixel = Assert.Single(frame.Illuminated); // the all-black node must not appear
        Assert.Equal(model.Id, pixel.Model);
        Assert.Equal(0.25, pixel.X);
        Assert.Equal(0.5, pixel.Y);
        Assert.Equal(255, pixel.R);
        Assert.Equal(0, pixel.G);
        Assert.Equal(0, pixel.B);
    }

    [Fact]
    public void Project_MultiStringModel_ProjectsEachStringsOwnChannels()
    {
        var model = new DisplayModel(
            new ModelId("Tree"), "Tree", "Tree 180", [], [new Point2(0.5, 0.5)], StringType: "RGB Nodes", NodeCount: 2);
        var layout = Layout(model);
        // String 1 at offset 0 (1 node), string 2 at offset 10 (1 node) -- disjoint, non-contiguous.
        var spans = new Dictionary<ModelId, IReadOnlyList<ModelStringChannelSpan>>
        {
            [model.Id] = [new ModelStringChannelSpan(0, 1), new ModelStringChannelSpan(10, 1)],
        };
        var channels = new byte[13];
        channels[0] = 255; // string 1's node: red
        channels[10] = 0; channels[11] = 255; channels[12] = 0; // string 2's node: green
        var decoded = new DecodedRenderOutput(13, 25, [new DecodedRenderFrame(0, channels)]);

        var result = RenderedFrameProjector.Project(decoded, layout, spans);

        Assert.Empty(result.SkippedModels);
        var frame = Assert.Single(result.Frames);
        Assert.Equal(2, frame.Illuminated.Count);
        Assert.Contains(frame.Illuminated, p => p.R == 255 && p.G == 0 && p.B == 0);
        Assert.Contains(frame.Illuminated, p => p.R == 0 && p.G == 255 && p.B == 0);
    }

    [Fact]
    public void Project_ModelWithNoChannelSpans_IsSkippedSilently()
    {
        var model = new DisplayModel(new ModelId("GroupMember"), "GroupMember", "Single Line", [], [], StringType: "RGB Nodes", NodeCount: 1);
        var layout = Layout(model);
        var decoded = new DecodedRenderOutput(3, 25, [new DecodedRenderFrame(0, [1, 2, 3])]);

        var result = RenderedFrameProjector.Project(decoded, layout, new Dictionary<ModelId, IReadOnlyList<ModelStringChannelSpan>>());

        Assert.Empty(result.SkippedModels); // no channel span at all is not an error -- just nothing to project.
        Assert.Empty(Assert.Single(result.Frames).Illuminated);
    }

    [Fact]
    public void Project_UnsupportedStringType_IsReportedAsSkipped()
    {
        var model = new DisplayModel(new ModelId("ModelA"), "ModelA", "Single Line", [], [], StringType: "White Nodes", NodeCount: 1);
        var layout = Layout(model);
        var decoded = new DecodedRenderOutput(3, 25, [new DecodedRenderFrame(0, [1, 2, 3])]);

        var result = RenderedFrameProjector.Project(decoded, layout, SingleSpan(model.Id, 0, 1));

        Assert.Contains(result.SkippedModels, w => w.Contains("unsupported StringType", StringComparison.Ordinal));
    }

    [Fact]
    public void Project_ChannelSpanExceedsDecodedChannelCount_IsReportedAsSkipped()
    {
        var model = new DisplayModel(new ModelId("ModelA"), "ModelA", "Single Line", [], [], StringType: "RGB Nodes", NodeCount: 10);
        var layout = Layout(model);
        var decoded = new DecodedRenderOutput(6, 25, [new DecodedRenderFrame(0, [0, 0, 0, 0, 0, 0])]);

        var result = RenderedFrameProjector.Project(decoded, layout, SingleSpan(model.Id, 0, 10));

        Assert.Contains(result.SkippedModels, w => w.Contains("exceeds the decoded channel count", StringComparison.Ordinal));
    }

    [Fact]
    public void Project_NoPreviewPoint_FallsBackToBoundsThenOrigin()
    {
        var withBounds = new DisplayModel(
            new ModelId("ModelB"), "ModelB", "Single Line", [], [],
            StringType: "RGB Nodes", NodeCount: 1, Bounds: new DisplayBounds(X: 7, Y: 9, Z: 0));
        var layout = Layout(withBounds);
        var decoded = new DecodedRenderOutput(3, 25, [new DecodedRenderFrame(0, [10, 10, 10])]);

        var result = RenderedFrameProjector.Project(decoded, layout, SingleSpan(withBounds.Id, 0, 1));

        var pixel = Assert.Single(Assert.Single(result.Frames).Illuminated);
        Assert.Equal(7, pixel.X);
        Assert.Equal(9, pixel.Y);
    }
}
