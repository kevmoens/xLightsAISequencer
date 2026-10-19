using System.Text.Json;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Display.Tests;

/// <summary>
/// PHASE-09 Step 060 -- "Infer neighbors, left/right, center and vertical relations with confidence."
/// Required acceptance: "Expected order recognized; uncertain order flagged." Failure/edge case: "Invalid
/// input, cancellation or stale persisted state must not corrupt the project." These tests exercise the
/// real <see cref="ShowLayoutImporter"/>, <see cref="DisplayGraphBuilder"/>, <see cref="SpatialRelationInferrer"/>
/// and <see cref="JsonSpatialDisplayGraphStore"/> (not mocks) and assert the persisted/returned domain
/// state itself, not merely that a call happened.
/// </summary>
[Trait("Category", "Integration")]
public sealed class Step060AcceptanceTests
{
    private static string FixtureDirectory(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    // Wider than Step059AcceptanceTests' own DefaultPolicy (0.3) so both the arches row (gap 0.25) and the
    // roofline row (gap 0.375) are within range -- this step's own test, not a change to Step 059's policy.
    private static readonly GeometryPolicy WideNeighborPolicy = new(MaxNeighborDistance: 0.4, RespectGroups: true);

    [Fact]
    public async Task Step060_Acceptance_Passes_ExpectedOrderRecognized()
    {
        var importer = new ShowLayoutImporter();
        var builder = new DisplayGraphBuilder();
        var inferrer = new SpatialRelationInferrer();
        var directory = FixtureDirectory("sample-show-spatial-display");

        var imported = await importer.ImportReadOnlyAsync(directory, CancellationToken.None);
        var graph = builder.Build(imported, WideNeighborPolicy);

        var result = inferrer.Infer(graph, WideNeighborPolicy);

        // Expected order recognized: the five uniformly-spaced arches produce a full-confidence left-to-
        // right chain, with Arch-03 as the row's own unambiguous (odd-count) center.
        var archLeftOf = result.Edges.Where(e => e.Relation == "LeftOf" && IsArch(e.From) && IsArch(e.To)).ToList();
        Assert.Equal(4, archLeftOf.Count);
        Assert.All(archLeftOf, e => Assert.Equal(1.0, e.Confidence, precision: 10));
        Assert.Equal(
            [("Arch-01", "Arch-02"), ("Arch-02", "Arch-03"), ("Arch-03", "Arch-04"), ("Arch-04", "Arch-05")],
            archLeftOf.Select(e => (e.From.Value, e.To.Value)));
        Assert.Contains(new ModelId("Arch-03"), result.CenterNodeIds);

        // The three uniformly-spaced roofline models produce the same shape at their own row, centered on
        // Roof-02.
        var roofLeftOf = result.Edges.Where(e => e.Relation == "LeftOf" && e.From.Value.StartsWith("Roof")).ToList();
        Assert.Equal(2, roofLeftOf.Count);
        Assert.All(roofLeftOf, e => Assert.Equal(1.0, e.Confidence, precision: 10));
        Assert.Contains(new ModelId("Roof-02"), result.CenterNodeIds);
        Assert.Equal(2, result.CenterNodeIds.Count);

        // Every "LeftOf" edge has a matching "Neighbor" edge at the same confidence (symmetric adjacency
        // claim, not merely a directional one), and no edge is ever marked UserDefined by this inferrer.
        Assert.Equal(6, result.Edges.Count(e => e.Relation == "Neighbor"));
        Assert.Equal(6, result.Edges.Count(e => e.Relation == "LeftOf"));
        Assert.All(result.Edges, e => Assert.False(e.UserDefined));

        // Vertical order: the arches row (lower Y) is Below the roofline row (higher Y), represented by
        // each row's own center node -- the only inter-row gap, so trivially full confidence.
        var below = Assert.Single(result.Edges, e => e.Relation == "Below");
        Assert.Equal(new ModelId("Arch-03"), below.From);
        Assert.Equal(new ModelId("Roof-02"), below.To);
        Assert.Equal(1.0, below.Confidence, precision: 10);

        // This step never mutates Step 059's own already-accepted Build() output -- the caller must
        // explicitly merge the inferred edges in.
        Assert.Equal(10, graph.Edges.Count);
    }

    /// <summary>
    /// Required acceptance, second half: "uncertain order flagged." An unevenly-spaced row (one pair far
    /// too close together relative to the row's own typical spacing, one pair far too wide) surfaces a
    /// low confidence on the outlier pairs rather than asserting a false, confident order -- and an
    /// even-sized row (four nodes) has no single unambiguous center, so none is guessed.
    /// </summary>
    [Fact]
    public void Step060_Acceptance_Passes_UncertainOrderFlagged()
    {
        var builder = new DisplayGraphBuilder();
        var inferrer = new SpatialRelationInferrer();
        var imported = new DisplayGraph(
            "hash",
            [
                new DisplayModel(new ModelId("A"), "A", "Arches", [], [new Point2(0, 0)]),
                new DisplayModel(new ModelId("B"), "B", "Arches", [], [new Point2(10, 0)]),
                new DisplayModel(new ModelId("C"), "C", "Arches", [], [new Point2(11, 0)]),
                new DisplayModel(new ModelId("D"), "D", "Arches", [], [new Point2(40, 0)]),
            ],
            [],
            []);
        var policy = new GeometryPolicy(MaxNeighborDistance: 0.8, RespectGroups: true);

        var graph = builder.Build(imported, policy);
        var result = inferrer.Infer(graph, policy);

        var leftOf = result.Edges.Where(e => e.Relation == "LeftOf").OrderBy(e => e.From.Value).ToList();
        Assert.Equal(3, leftOf.Count);

        var aToB = Assert.Single(leftOf, e => e.From == new ModelId("A") && e.To == new ModelId("B"));
        var bToC = Assert.Single(leftOf, e => e.From == new ModelId("B") && e.To == new ModelId("C"));
        var cToD = Assert.Single(leftOf, e => e.From == new ModelId("C") && e.To == new ModelId("D"));

        // Gap A->B (0.25) matches the row's own median gap exactly: expected order recognized, full
        // confidence.
        Assert.Equal(1.0, aToB.Confidence, precision: 10);

        // Gap B->C (0.025) and C->D (0.725) are both outliers relative to the row's own median (0.25):
        // uncertain order flagged via a confidence at or below this step's own disclosed threshold, not a
        // bare, overconfident assertion.
        Assert.True(bToC.Confidence <= SpatialRelationInferrer.UncertainConfidenceThreshold);
        Assert.True(cToD.Confidence <= SpatialRelationInferrer.UncertainConfidenceThreshold);
        Assert.Equal(0.1, bToC.Confidence, precision: 10);
        Assert.Equal(0.25 / 0.725, cToD.Confidence, precision: 10);

        // Four nodes is an even-sized row: no single unambiguous center, so none is guessed.
        Assert.Empty(result.CenterNodeIds);
    }

    [Fact]
    public void Infer_NegativeMaxNeighborDistance_Throws_Invalid_NegativePath()
    {
        var builder = new DisplayGraphBuilder();
        var inferrer = new SpatialRelationInferrer();
        var validPolicy = new GeometryPolicy(MaxNeighborDistance: 0.3, RespectGroups: true);
        var imported = new DisplayGraph(
            "hash",
            [new DisplayModel(new ModelId("A"), "A", "Arches", [], [new Point2(0, 0)])],
            [],
            []);
        var graph = builder.Build(imported, validPolicy);

        var invalidPolicy = new GeometryPolicy(MaxNeighborDistance: -1, RespectGroups: true);

        Assert.Throws<SpatialDisplayGraphInvalidException>(() => inferrer.Infer(graph, invalidPolicy));
    }

    [Fact]
    public void Infer_NullGraph_Throws_ArgumentNullException_NegativePath()
    {
        var inferrer = new SpatialRelationInferrer();
        var policy = new GeometryPolicy(MaxNeighborDistance: 0.3, RespectGroups: true);

        Assert.Throws<ArgumentNullException>(() => inferrer.Infer(null!, policy));
    }

    /// <summary>
    /// Failure/edge case, "stale persisted state must not corrupt the project": a project saved before
    /// this step's own <see cref="SpatialEdge.Confidence"/> field existed (every edge's own JSON is
    /// missing that property entirely) must still load cleanly via the real, unmodified
    /// <see cref="JsonSpatialDisplayGraphStore"/> -- not throw, not silently drop edges, not require a
    /// forced migration -- defaulting the absent field to full confidence (Step 059's own edges were
    /// literal fact, not an inference).
    /// </summary>
    [Fact]
    public async Task StalePersistedGraph_WithoutConfidenceField_LoadsWithoutCorruption_NegativePath()
    {
        var builder = new DisplayGraphBuilder();
        var imported = new DisplayGraph(
            "hash",
            [new DisplayModel(new ModelId("A"), "A", "Arches", [], [new Point2(0, 0)])],
            [new DisplayModelGroup("G", [new DisplayGroupMember("A", DisplayGroupMemberKind.Model)])],
            []);
        var graph = builder.Build(imported, new GeometryPolicy(MaxNeighborDistance: 0.3, RespectGroups: true));
        Assert.NotEmpty(graph.Edges);

        var preStep060Json = JsonSerializer.Serialize(graph).Replace(",\"Confidence\":1", string.Empty);
        Assert.DoesNotContain("Confidence", preStep060Json, StringComparison.Ordinal);

        var cacheDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step060-stale-{Guid.NewGuid():N}");
        Directory.CreateDirectory(cacheDirectory);
        var path = Path.Combine(cacheDirectory, "stale.json");
        await File.WriteAllTextAsync(path, preStep060Json);

        try
        {
            var store = new JsonSpatialDisplayGraphStore(cacheDirectory);

            var reloaded = await store.LoadAsync(path, CancellationToken.None);

            Assert.Equal(graph.Nodes.Count, reloaded.Nodes.Count);
            Assert.Equal(graph.Edges.Count, reloaded.Edges.Count);
            Assert.All(reloaded.Edges, e => Assert.Equal(1.0, e.Confidence));
        }
        finally
        {
            Directory.Delete(cacheDirectory, recursive: true);
        }
    }

    /// <summary>
    /// Failure/edge case, "cancellation... must not corrupt the project": an already-canceled token on a
    /// second, would-be-overwriting save never reaches the atomic <see cref="File.Move(string, string, bool)"/>
    /// step, so the first, real, relation-augmented save remains byte-identical.
    /// </summary>
    [Fact]
    public async Task CancelledSecondSave_DoesNotCorruptPreviouslyPersistedRelationAugmentedGraph_NegativePath()
    {
        var importer = new ShowLayoutImporter();
        var builder = new DisplayGraphBuilder();
        var inferrer = new SpatialRelationInferrer();
        var directory = FixtureDirectory("sample-show-spatial-display");
        var imported = await importer.ImportReadOnlyAsync(directory, CancellationToken.None);
        var graph = builder.Build(imported, WideNeighborPolicy);
        var inferred = inferrer.Infer(graph, WideNeighborPolicy);
        var augmented = graph with { Edges = [.. graph.Edges, .. inferred.Edges] };

        var cacheDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step060-cancel-{Guid.NewGuid():N}");
        try
        {
            var store = new JsonSpatialDisplayGraphStore(cacheDirectory);
            var path = await store.SaveAsync(augmented, CancellationToken.None);
            var bytesBeforeCancelledAttempt = await File.ReadAllBytesAsync(path);

            var corrupted = augmented with { Edges = [] };
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            await Assert.ThrowsAsync<OperationCanceledException>(() => store.SaveAsync(corrupted, cts.Token));

            Assert.Equal(bytesBeforeCancelledAttempt, await File.ReadAllBytesAsync(path));
            var reloaded = await store.LoadAsync(path, CancellationToken.None);
            Assert.Equal(augmented.Edges.Count, reloaded.Edges.Count);
        }
        finally
        {
            if (Directory.Exists(cacheDirectory))
            {
                Directory.Delete(cacheDirectory, recursive: true);
            }
        }
    }

    private static bool IsArch(ModelId id) => id.Value.StartsWith("Arch", StringComparison.Ordinal);
}
