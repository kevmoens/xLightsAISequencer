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
    public void Project_SingleLineModelWithEndpointBounds_SpreadsNodesAcrossRealDistinctPositions()
    {
        var model = new DisplayModel(
            new ModelId("Line"), "Line", "Single Line", [], [],
            Category: ModelCategory.SingleLine, StringType: "RGB Nodes", NodeCount: 3,
            Bounds: new DisplayBounds(X: 0.0, Y: 0.5, Z: 0.0, EndX: 1.0, EndY: 0.5));
        var layout = Layout(model);
        // 3 nodes * 3 channels = 9 channels, all lit (never black) so every node's own position is checkable.
        var decoded = new DecodedRenderOutput(9, 25, [new DecodedRenderFrame(0, [1, 0, 0, 0, 1, 0, 0, 0, 1])]);

        var result = RenderedFrameProjector.Project(decoded, layout, SingleSpan(model.Id, 0, 3));

        var frame = Assert.Single(result.Frames);
        Assert.Equal(3, frame.Illuminated.Count);
        // Node 0 at the start of the line, node 1 at the midpoint, node 2 at the end -- real, distinct
        // positions, not all three collapsed onto one point.
        Assert.Equal(0.0, frame.Illuminated[0].X);
        Assert.Equal(0.5, frame.Illuminated[1].X);
        Assert.Equal(1.0, frame.Illuminated[2].X);
    }

    [Fact]
    public void Project_SingleLineModelWithAnOffNodeInTheMiddle_StillPlacesTheLitNodeAtItsRealIndex()
    {
        var model = new DisplayModel(
            new ModelId("Line"), "Line", "Single Line", [], [],
            Category: ModelCategory.SingleLine, StringType: "RGB Nodes", NodeCount: 3,
            Bounds: new DisplayBounds(X: 0.0, Y: 0.5, Z: 0.0, EndX: 1.0, EndY: 0.5));
        var layout = Layout(model);
        // Only node 2 (the real last node) is lit; nodes 0 and 1 are black and must not shift node 2's
        // own position leftward (a bug this test guards against: deriving position from list order
        // instead of real node index would wrongly place the sole illuminated point at the start).
        var decoded = new DecodedRenderOutput(9, 25, [new DecodedRenderFrame(0, [0, 0, 0, 0, 0, 0, 1, 0, 0])]);

        var result = RenderedFrameProjector.Project(decoded, layout, SingleSpan(model.Id, 0, 3));

        var pixel = Assert.Single(Assert.Single(result.Frames).Illuminated);
        Assert.Equal(1.0, pixel.X);
    }

    [Fact]
    public void Project_MatrixModelWithParm1Parm2_SpreadsNodesAcrossARealTwoDimensionalGrid()
    {
        // 2 columns (strings) x 3 rows (pixels/string) = 6 nodes -- real xLights node numbering for a
        // multi-string model is string-major (string 0's pixels first, then string 1's).
        var model = new DisplayModel(
            new ModelId("Matrix"), "Matrix", "Vert Matrix", [], [],
            Category: ModelCategory.Matrix, StringType: "RGB Nodes", NodeCount: 6, Parm1: 2, Parm2: 3);
        var layout = Layout(model);
        var channels = new byte[18];
        for (var node = 0; node < 6; node++)
        {
            channels[node * 3] = (byte)(node + 1); // every node lit with a distinct, non-zero red value.
        }

        var decoded = new DecodedRenderOutput(18, 25, [new DecodedRenderFrame(0, channels)]);
        var result = RenderedFrameProjector.Project(decoded, layout, SingleSpan(model.Id, 0, 6));

        var frame = Assert.Single(result.Frames);
        Assert.Equal(6, frame.Illuminated.Count);

        // Node 0: column 0, row 0 -> (0, 0). Node 2: column 0, row 2 (last row of string 0) -> (0, 1).
        // Node 3: column 1, row 0 (first pixel of string 1) -> (1, 0). Node 5: column 1, row 2 -> (1, 1).
        Assert.Equal((0.0, 0.0), (frame.Illuminated[0].X, frame.Illuminated[0].Y));
        Assert.Equal((0.0, 1.0), (frame.Illuminated[2].X, frame.Illuminated[2].Y));
        Assert.Equal((1.0, 0.0), (frame.Illuminated[3].X, frame.Illuminated[3].Y));
        Assert.Equal((1.0, 1.0), (frame.Illuminated[5].X, frame.Illuminated[5].Y));
    }

    [Fact]
    public void Project_MatrixModelWithMismatchedParm1Parm2_FallsBackToSinglePointRatherThanGuessing()
    {
        // Parm1*Parm2 (2*2=4) does not equal the real NodeCount (6) -- never trust mismatched geometry data.
        var model = new DisplayModel(
            new ModelId("Matrix"), "Matrix", "Vert Matrix", [], [],
            Category: ModelCategory.Matrix, StringType: "RGB Nodes", NodeCount: 6, Parm1: 2, Parm2: 2);
        var layout = Layout(model);
        var channels = new byte[18];
        for (var node = 0; node < 6; node++)
        {
            channels[node * 3] = 255;
        }

        var decoded = new DecodedRenderOutput(18, 25, [new DecodedRenderFrame(0, channels)]);
        var result = RenderedFrameProjector.Project(decoded, layout, SingleSpan(model.Id, 0, 6));

        var frame = Assert.Single(result.Frames);
        Assert.Equal(6, frame.Illuminated.Count);
        Assert.All(frame.Illuminated, p => Assert.Equal(0.0, p.X)); // falls back to the single representative point.
    }

    [Fact]
    public void Project_NonSingleLineCategory_KeepsExistingSinglePointBehaviorEvenWithEndpointBounds()
    {
        var model = new DisplayModel(
            new ModelId("Matrix"), "Matrix", "Vert Matrix", [], [],
            Category: ModelCategory.Matrix, StringType: "RGB Nodes", NodeCount: 2,
            Bounds: new DisplayBounds(X: 0.0, Y: 0.5, Z: 0.0, EndX: 1.0, EndY: 0.5));
        var layout = Layout(model);
        var decoded = new DecodedRenderOutput(6, 25, [new DecodedRenderFrame(0, [1, 0, 0, 0, 1, 0])]);

        var result = RenderedFrameProjector.Project(decoded, layout, SingleSpan(model.Id, 0, 2));

        var frame = Assert.Single(result.Frames);
        Assert.Equal(2, frame.Illuminated.Count);
        Assert.All(frame.Illuminated, p => Assert.Equal(0.0, p.X)); // unchanged: every node at the same representative point.
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
