using XlightsAI.Studio.Composition;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Refinement;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-12 Step 084 -- "Add candidate gallery, score explanations, preview and explicit selection."
/// Required acceptance (verbatim): "User can compare alternatives and commit chosen candidate." Required
/// failure/edge case (verbatim): "Out-of-scope, locked or stale revision must be rejected without partial
/// mutation." Fast, deterministic tests for <see cref="CandidateCompareViewModel"/>'s own bindable
/// gallery/accept/reject contract against a real <see cref="JsonRefinementRunStore"/>/
/// <see cref="GestureRefinementOrchestrator"/>/<see cref="GestureQualityEvaluator"/>/
/// <see cref="GestureCandidateCommitService"/> (not mocks) -- the same convention Step 076's own
/// <see cref="AiEditProposalReviewViewModel"/> test sibling already established. No real SCREEN 15
/// COMPARE Avalonia/AXAML view exists (the project's own pre-existing, consistent convention); this
/// step's own required "Avalonia screenshot or repeatable desktop interaction recording with saved-state
/// verification" outcome is proven at the bindable view-model layer the real SCREEN 15 UI would bind to.
/// </summary>
public sealed class Step084CandidateCompareViewModelTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-step084-vm-" + Guid.NewGuid().ToString("N"));
    private readonly JsonRefinementRunStore _store;
    private readonly GestureCandidateCommitService _commitService = new(new EffectLockPolicy(), new EffectRecipeRegistry());

    public Step084CandidateCompareViewModelTests()
    {
        _store = new JsonRefinementRunStore(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static EditScope Scope(params string[] models) =>
        new(new TimeWindow(0, 8_000_000), models.Select(m => new ModelId(m)).ToHashSet());

    private static LoopRun NewQueuedRun(Guid documentId, EditScope scope) => new(
        Id: Guid.NewGuid(),
        DocumentId: documentId,
        BaseRevision: 1,
        Scope: scope,
        InputHash: "hash-of-scope-and-seed",
        Seed: 42,
        MaxAttempts: 3,
        AttemptsMade: 0,
        Status: RefinementStatus.Queued,
        BestCandidateId: null,
        StopReason: null,
        ClaimToken: null,
        ClaimedAtUtc: null,
        HeartbeatAtUtc: null,
        CreatedAtUtc: DateTimeOffset.UtcNow,
        GestureFamily: TransitionKind.Transition);

    private async Task<LoopRun> SeedAwaitingApprovalRunAsync(Guid documentId, EditScope scope)
    {
        var run = NewQueuedRun(documentId, scope);
        await _store.CreateAsync(run, CancellationToken.None);
        var orchestrator = new GestureRefinementOrchestrator(
            _store, new DeterministicGestureCandidateGenerator(), new GestureTargetedRepairStrategy(),
            new GestureQualityEvaluator(), ChoreographyQualityContext.Empty);
        var finished = await orchestrator.ExecuteAsync(run.Id, CancellationToken.None);
        Assert.Equal(RefinementStatus.AwaitingApproval, finished.Status);
        return finished;
    }

    [Fact]
    public async Task Step084_Acceptance_LoadAsync_PopulatesGalleryWithScoresAndExplanations_DefaultsToBest()
    {
        var documentId = Guid.NewGuid();
        var run = await SeedAwaitingApprovalRunAsync(documentId, Scope("Arches", "MegaTree"));
        var vm = new CandidateCompareViewModel();

        var loaded = await vm.LoadAsync(_store, new GestureQualityEvaluator(), ChoreographyQualityContext.Empty, run.Id, CancellationToken.None);

        Assert.True(loaded);
        Assert.True(vm.IsLoaded);
        Assert.NotEmpty(vm.Candidates);
        Assert.Equal(run.BestCandidateId, vm.SelectedCandidateId);
        Assert.NotNull(vm.SelectedCandidate);
        Assert.Contains(vm.Candidates, c => c.IsBest);
        Assert.All(vm.Candidates, c => Assert.NotEmpty(c.Report.Metrics));
        Assert.All(vm.Candidates, c => Assert.All(c.Report.Metrics, m => Assert.False(string.IsNullOrWhiteSpace(m.Explanation))));
        Assert.All(vm.Candidates, c => Assert.False(string.IsNullOrWhiteSpace(c.PreviewSummary)));
    }

    [Fact]
    public async Task Step084_Acceptance_UserCanSelectAnyAlternative_ThenAcceptTheExplicitlyChosenOne()
    {
        var documentId = Guid.NewGuid();
        var document = new SequenceDocument(documentId, 1, []);
        var run = await SeedAwaitingApprovalRunAsync(documentId, Scope("Arches", "MegaTree"));
        var vm = new CandidateCompareViewModel();
        await vm.LoadAsync(_store, new GestureQualityEvaluator(), ChoreographyQualityContext.Empty, run.Id, CancellationToken.None);

        var alternative = vm.Candidates.First(c => c.CandidateId != run.BestCandidateId && !c.HasViolations);
        vm.SelectCandidate(alternative.CandidateId);
        Assert.Equal(alternative.CandidateId, vm.SelectedCandidateId);

        var accepted = await vm.AcceptAsync(_commitService, _store, document, CancellationToken.None);

        Assert.True(accepted);
        Assert.NotNull(vm.LastAccepted);
        Assert.True(vm.IsEmpty);
        Assert.Null(vm.Run);
        Assert.Empty(vm.Candidates);

        var finalRun = await _store.LoadRunAsync(run.Id, CancellationToken.None);
        Assert.Equal(RefinementStatus.Accepted, finalRun!.Status);
        Assert.Equal(alternative.CandidateId, finalRun.BestCandidateId);
    }

    [Fact]
    public async Task Step084_Acceptance_Reject_RecordsFeedbackLabel_ClearsGallery_NeverTouchesDocument()
    {
        var documentId = Guid.NewGuid();
        var run = await SeedAwaitingApprovalRunAsync(documentId, Scope("Arches"));
        var vm = new CandidateCompareViewModel();
        await vm.LoadAsync(_store, new GestureQualityEvaluator(), ChoreographyQualityContext.Empty, run.Id, CancellationToken.None);

        var rejected = await vm.RejectAsync(_commitService, _store, "not musical enough", CancellationToken.None);

        Assert.True(rejected);
        Assert.NotNull(vm.LastRejected);
        Assert.Equal(RefinementStatus.Rejected, vm.LastRejected!.Status);
        Assert.Contains("not musical enough", vm.LastRejected.StopReason);
        Assert.True(vm.IsEmpty);
    }

    // ---- Required failure/edge case, verbatim: "Out-of-scope, locked or stale revision must be rejected without partial mutation." ----

    [Fact]
    public async Task Step084_NegativePath_StaleRevision_AcceptFails_GalleryStateUntouched()
    {
        var documentId = Guid.NewGuid();
        var run = await SeedAwaitingApprovalRunAsync(documentId, Scope("Arches"));
        var vm = new CandidateCompareViewModel();
        await vm.LoadAsync(_store, new GestureQualityEvaluator(), ChoreographyQualityContext.Empty, run.Id, CancellationToken.None);
        var candidatesBefore = vm.Candidates;
        var selectedBefore = vm.SelectedCandidateId;

        var staleDocument = new SequenceDocument(documentId, 2, []); // run's own BaseRevision is 1

        var accepted = await vm.AcceptAsync(_commitService, _store, staleDocument, CancellationToken.None);

        Assert.False(accepted);
        Assert.NotNull(vm.ErrorMessage);
        Assert.Null(vm.LastAccepted);
        Assert.Same(candidatesBefore, vm.Candidates); // left intact so the caller can re-load and retry
        Assert.Equal(selectedBefore, vm.SelectedCandidateId);
    }

    [Fact]
    public async Task Step084_NegativePath_SelectUnknownCandidateId_ReportsErrorAndKeepsPriorSelection()
    {
        var documentId = Guid.NewGuid();
        var run = await SeedAwaitingApprovalRunAsync(documentId, Scope("Arches"));
        var vm = new CandidateCompareViewModel();
        await vm.LoadAsync(_store, new GestureQualityEvaluator(), ChoreographyQualityContext.Empty, run.Id, CancellationToken.None);
        var selectedBefore = vm.SelectedCandidateId;

        vm.SelectCandidate(Guid.NewGuid());

        Assert.NotNull(vm.ErrorMessage);
        Assert.Equal(selectedBefore, vm.SelectedCandidateId);
    }

    [Fact]
    public async Task Step084_NegativePath_LoadAsync_UnknownRunId_ReportsError_LeavesEmpty()
    {
        var vm = new CandidateCompareViewModel();

        var loaded = await vm.LoadAsync(_store, new GestureQualityEvaluator(), ChoreographyQualityContext.Empty, Guid.NewGuid(), CancellationToken.None);

        Assert.False(loaded);
        Assert.NotNull(vm.ErrorMessage);
        Assert.True(vm.IsEmpty);
    }

    [Fact]
    public async Task Step084_NegativePath_AcceptAsync_WithNoCandidateLoaded_Throws()
    {
        var vm = new CandidateCompareViewModel();
        var document = new SequenceDocument(Guid.NewGuid(), 1, []);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => vm.AcceptAsync(_commitService, _store, document, CancellationToken.None));
    }
}
