using XlightsAI.Studio.Composition;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Display;
using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Rendering;

namespace XlightsAI.Studio.Refinement.Tests;

/// <summary>
/// PHASE-13 Step 089 -- "Add render/evaluation gate to refinement and report limitations." Required
/// acceptance (verbatim): "Broken/unrenderable proposal cannot auto-apply; clear diagnostics." Required
/// failure/edge case (verbatim, restated from every PHASE-13 step): "Invalid input, cancellation or stale
/// persisted state must not corrupt the project." Implementation guidance (verbatim): "Integrate xLights
/// render results as separate hard and soft evaluation evidence in loop; require user acceptance even when
/// score improves." Every fixture here is built entirely programmatically against the real
/// <see cref="GestureRefinementOrchestrator"/>, <see cref="JsonRefinementRunStore"/>,
/// <see cref="GestureQualityEvaluator"/>, <see cref="RenderGatedQualityEvaluator"/>,
/// <see cref="PreviewRenderEvaluationGate"/>, <see cref="LocalGesturePreviewRenderer"/> (PHASE-13 Step 085)
/// and <see cref="DisplayMetricsCalculator"/> (PHASE-13 Step 088) -- no mocking, the same convention every
/// prior PHASE-12/13 step's own tests already established.
/// </summary>
public sealed class Step089AcceptanceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-step089-" + Guid.NewGuid().ToString("N"));

    private static readonly ModelId NodeA = new("Node-A");
    private static readonly ModelId NodeB = new("Node-B");
    private static readonly ModelId GhostProp = new("Ghost-Prop");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static SpatialDisplayGraph LayoutWithNodes(params ModelId[] ids)
    {
        var nodes = ids.Select((id, i) => new DisplayNode(id, id.Value, X: i, Y: 0.0, "Arches", new HashSet<string>())).ToArray();
        return new SpatialDisplayGraph("layout-hash-step089", nodes, Array.Empty<SpatialEdge>(), Array.Empty<SpatialPath>(), Guid.NewGuid());
    }

    private static EditScope Scope(params ModelId[] models) =>
        new(new TimeWindow(0, 8_000_000), models.ToHashSet());

    private static LoopRun NewQueuedRun(
        EditScope scope,
        TransitionKind family = TransitionKind.Transition,
        int maxAttempts = 3,
        RefinementBudget? budget = null) => new(
        Id: Guid.NewGuid(),
        DocumentId: Guid.NewGuid(),
        BaseRevision: 1,
        Scope: scope,
        InputHash: "hash-of-scope-and-seed",
        Seed: 42,
        MaxAttempts: maxAttempts,
        AttemptsMade: 0,
        Status: RefinementStatus.Queued,
        BestCandidateId: null,
        StopReason: null,
        ClaimToken: null,
        ClaimedAtUtc: null,
        HeartbeatAtUtc: null,
        CreatedAtUtc: DateTimeOffset.UtcNow,
        GestureFamily: family,
        Budget: budget);

    private static IChoreographyQualityEvaluator RenderGatedEvaluator(SpatialDisplayGraph layout) =>
        new RenderGatedQualityEvaluator(
            new GestureQualityEvaluator(),
            new PreviewRenderEvaluationGate(new LocalGesturePreviewRenderer(), new DisplayMetricsCalculator()),
            layout);

    private static GestureRefinementOrchestrator NewOrchestrator(IRefinementRunStore store, SpatialDisplayGraph layout) => new(
        store,
        new DeterministicGestureCandidateGenerator(),
        new GestureTargetedRepairStrategy(),
        RenderGatedEvaluator(layout),
        ChoreographyQualityContext.Empty);

    // ---- Required acceptance, verbatim: "Broken/unrenderable proposal cannot auto-apply; clear diagnostics." ----

    [Fact]
    public async Task Step089_Acceptance_Passes()
    {
        // The run's own permitted scope names GhostProp, which the real display layout does not know about
        // at all -- every attempt DeterministicGestureCandidateGenerator produces targets a prop the local
        // preview renderer cannot resolve, so the render gate's own "RenderPreview" hard gate must fail
        // every single attempt, for a clearly stated reason, and the run must never auto-apply any of them.
        var layoutMissingTheTargetedProp = LayoutWithNodes(NodeA); // Ghost-Prop deliberately absent.
        var run = NewQueuedRun(Scope(GhostProp), maxAttempts: 3);
        var store = new JsonRefinementRunStore(_root);
        await store.CreateAsync(run, CancellationToken.None);
        var orchestrator = NewOrchestrator(store, layoutMissingTheTargetedProp);

        var finished = await orchestrator.ExecuteAsync(run.Id, CancellationToken.None);

        // Never auto-applied: no candidate ever became the retained best, so the run has nothing it could
        // ever auto-commit, and it lands on the one terminal state that reflects that -- Blocked, never
        // AwaitingApproval (which would imply a retained, acceptable candidate) and never Accepted.
        Assert.Null(finished.BestCandidateId);
        Assert.Equal(RefinementStatus.Blocked, finished.Status);
        Assert.NotEqual(RefinementStatus.Accepted, finished.Status);
        Assert.True(RefinementStatusTransitions.IsTerminal(finished.Status));
        Assert.Equal(3, finished.AttemptsMade);

        // Clear diagnostics: re-run the exact same merged evaluation the loop itself used for the first
        // recorded attempt and assert the render hard gate's own explanation names the real, concrete
        // reason -- not a generic/opaque failure.
        var attempts = await store.LoadAttemptsAsync(run.Id, CancellationToken.None);
        Assert.Equal(3, attempts.Count);
        var evaluator = RenderGatedEvaluator(layoutMissingTheTargetedProp);
        var report = await evaluator.EvaluateAsync(run, attempts[0], ChoreographyQualityContext.Empty, CancellationToken.None);

        var renderGate = Assert.Single(report.Metrics, m => m.Name == PreviewRenderEvaluationGate.RenderPreviewMetricName);
        Assert.True(renderGate.HardGate);
        Assert.Equal(0.0, renderGate.Value);
        Assert.Contains("could not be resolved against the display layout", renderGate.Explanation);
        Assert.Contains(report.Violations, v => v.Contains(PreviewRenderEvaluationGate.RenderPreviewMetricName, StringComparison.Ordinal));
    }

    // ---- Implementation guidance, verbatim: "Integrate xLights render results as separate hard and soft
    //      evaluation evidence in loop." ----

    [Fact]
    public async Task Step089_RenderEvidence_IsMergedAlongsideExistingQualityMetrics_WhenRenderable()
    {
        var layout = LayoutWithNodes(NodeA, NodeB);
        var run = NewQueuedRun(Scope(NodeA, NodeB));
        var evaluator = RenderGatedEvaluator(layout);
        var generator = new DeterministicGestureCandidateGenerator();

        var attempt = await generator.GenerateAsync(run, null, CancellationToken.None);
        var report = await evaluator.EvaluateAsync(run, attempt, ChoreographyQualityContext.Empty, CancellationToken.None);

        Assert.Empty(report.Violations);

        // Both tiers of evidence are present on the one merged report -- GestureQualityEvaluator's own
        // already-accepted metrics (Step 081) are untouched, and the render gate's own metrics are added
        // alongside them, never replacing them.
        Assert.Contains(report.Metrics, m => m.Name == "LegalEffects");
        Assert.Contains(report.Metrics, m => m.Name == "Density");
        var renderGate = Assert.Single(report.Metrics, m => m.Name == PreviewRenderEvaluationGate.RenderPreviewMetricName);
        Assert.Equal(1.0, renderGate.Value);
        Assert.Contains(report.Metrics, m => m.Name == "RenderCoverage");
        Assert.Contains(report.Metrics, m => m.Name == "RenderBrightness");
        Assert.Contains(report.Metrics, m => m.Name == "RenderMotion");
        Assert.Contains(report.Metrics, m => m.Name == "RenderDensity");

        // Fully lit for the whole window on both permitted props -- coverage/density/brightness are all at
        // their maximum grounded value.
        Assert.Equal(1.0, report.Metrics.Single(m => m.Name == "RenderCoverage").Value);
        Assert.Equal(1.0, report.Metrics.Single(m => m.Name == "RenderDensity").Value);
        Assert.Equal(1.0, report.Metrics.Single(m => m.Name == "RenderBrightness").Value);

        // Comparable: EvaluatorVersion is still GestureQualityEvaluator's own (merging additively never
        // mints a new, incomparable evaluator identity) -- QualityReport.AreComparable still holds across
        // two render-gated reports from the same inner evaluator.
        Assert.Equal(GestureQualityEvaluator.EvaluatorVersion, report.EvaluatorVersion);
    }

    [Fact]
    public async Task Step089_Blackout_CompilesToNoPreviewEffect_NeverReportedAsUnrenderable()
    {
        // A Blackout gesture is an intentional, legal darkening (PHASE-10's own required acceptance) -- the
        // render gate must never treat it as an unresolved/unsupported effect just because it compiles to
        // nothing visible.
        var layout = LayoutWithNodes(NodeA);
        var run = NewQueuedRun(Scope(NodeA), family: TransitionKind.Blackout);
        var evaluator = RenderGatedEvaluator(layout);
        var generator = new DeterministicGestureCandidateGenerator();

        var attempt = await generator.GenerateAsync(run, null, CancellationToken.None);
        var report = await evaluator.EvaluateAsync(run, attempt, ChoreographyQualityContext.Empty, CancellationToken.None);

        var renderGate = Assert.Single(report.Metrics, m => m.Name == PreviewRenderEvaluationGate.RenderPreviewMetricName);
        Assert.Equal(1.0, renderGate.Value);

        // Nothing was ever illuminated -- the soft metrics correctly report zero, not an error.
        Assert.Equal(0.0, report.Metrics.Single(m => m.Name == "RenderCoverage").Value);
    }

    // ---- Implementation guidance, verbatim: "require user acceptance even when score improves." ----

    [Fact]
    public async Task Step089_Acceptance_NeverAutoCommits_EvenWhenRenderableAndImproving_OnlyExplicitAcceptReaches_Accepted()
    {
        var layout = LayoutWithNodes(NodeA, NodeB);
        var run = NewQueuedRun(Scope(NodeA, NodeB), maxAttempts: 2);
        var store = new JsonRefinementRunStore(_root);
        await store.CreateAsync(run, CancellationToken.None);
        var orchestrator = NewOrchestrator(store, layout);

        var finished = await orchestrator.ExecuteAsync(run.Id, CancellationToken.None);

        // A renderable, fully-scoring candidate was found (not Blocked) -- yet the loop itself still only
        // ever reaches AwaitingApproval, never Accepted, regardless of how high the merged score is.
        Assert.NotNull(finished.BestCandidateId);
        Assert.Equal(RefinementStatus.AwaitingApproval, finished.Status);
        Assert.NotEqual(RefinementStatus.Accepted, finished.Status);

        // The only path to Accepted is the distinct, explicit, human-driven commit service -- never the
        // orchestrator itself.
        var document = new XlightsAI.Studio.Xlights.SequenceDocument(run.DocumentId, run.BaseRevision, Array.Empty<XlightsAI.Studio.Xlights.EffectInstance>());
        var commitService = new GestureCandidateCommitService(new EffectLockPolicy(), new XlightsAI.Studio.Xlights.EffectRecipeRegistry());
        var result = await commitService.AcceptAsync(store, run.Id, finished.BestCandidateId!.Value, document, CancellationToken.None);

        Assert.Equal(2L, result.Document.Revision);
        var reloaded = await store.LoadRunAsync(run.Id, CancellationToken.None);
        Assert.Equal(RefinementStatus.Accepted, reloaded!.Status);
    }

    // ---- Required failure/edge case, verbatim: "Invalid input, cancellation ... must not corrupt the project." ----

    [Fact]
    public async Task Step089_NegativePath_NullArguments_ThrowWithoutProducingEvidence()
    {
        var layout = LayoutWithNodes(NodeA);
        var gate = new PreviewRenderEvaluationGate(new LocalGesturePreviewRenderer(), new DisplayMetricsCalculator());
        var run = NewQueuedRun(Scope(NodeA));
        var candidate = await new DeterministicGestureCandidateGenerator().GenerateAsync(run, null, CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentNullException>(() => gate.EvaluateAsync(null!, candidate, layout, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => gate.EvaluateAsync(run, null!, layout, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => gate.EvaluateAsync(run, candidate, null!, CancellationToken.None));

        var decorator = RenderGatedEvaluator(layout);
        await Assert.ThrowsAsync<ArgumentNullException>(() => decorator.EvaluateAsync(null!, candidate, ChoreographyQualityContext.Empty, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => decorator.EvaluateAsync(run, null!, ChoreographyQualityContext.Empty, CancellationToken.None));
    }

    [Fact]
    public async Task Step089_NegativePath_CancellationRequested_ThrowsWithoutProducingEvidence()
    {
        var layout = LayoutWithNodes(NodeA);
        var gate = new PreviewRenderEvaluationGate(new LocalGesturePreviewRenderer(), new DisplayMetricsCalculator());
        var run = NewQueuedRun(Scope(NodeA));
        var candidate = await new DeterministicGestureCandidateGenerator().GenerateAsync(run, null, CancellationToken.None);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => gate.EvaluateAsync(run, candidate, layout, cts.Token));

        var decorator = RenderGatedEvaluator(layout);
        await Assert.ThrowsAsync<OperationCanceledException>(() => decorator.EvaluateAsync(run, candidate, ChoreographyQualityContext.Empty, cts.Token));
    }

    [Fact]
    public async Task Step089_NegativePath_NoGesturesToRender_PassesTrivially_NeverFalselyReportedAsBroken()
    {
        // An empty/malformed-upstream generation (e.g. empty edit scope) has nothing to render -- the
        // render gate must never fabricate a render failure for a candidate that was already rejected
        // upstream for an unrelated reason (an empty-scope attempt's own gestures list is always empty).
        var layout = LayoutWithNodes(NodeA);
        var gate = new PreviewRenderEvaluationGate(new LocalGesturePreviewRenderer(), new DisplayMetricsCalculator());
        var run = NewQueuedRun(new EditScope(new TimeWindow(0, 1_000_000), new HashSet<ModelId>()));
        var candidate = await new DeterministicGestureCandidateGenerator().GenerateAsync(run, null, CancellationToken.None);

        Assert.NotEmpty(candidate.Violations); // the generator's own empty-scope rejection.
        Assert.Empty(candidate.Gestures!);

        var metrics = await gate.EvaluateAsync(run, candidate, layout, CancellationToken.None);

        var renderGate = Assert.Single(metrics, m => m.Name == PreviewRenderEvaluationGate.RenderPreviewMetricName);
        Assert.Equal(1.0, renderGate.Value);
    }
}
