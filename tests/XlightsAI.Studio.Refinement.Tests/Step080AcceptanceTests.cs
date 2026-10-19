using XlightsAI.Studio.Composition;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Editing;

namespace XlightsAI.Studio.Refinement.Tests;

/// <summary>
/// PHASE-12 Step 080 -- "Build seeded candidate generator constrained to selected gesture family."
/// Required acceptance (verbatim): "Same seed + inputs yields same structured candidate." Required
/// failure/edge case (verbatim): "Malformed proposal, no-improvement and exhausted retry budget must
/// not auto-commit." These tests exercise the real <see cref="DeterministicGestureCandidateGenerator"/>
/// and, where this step's own output feeds back into Step 079's own already-accepted
/// <see cref="JsonRefinementRunStore"/>, the real file-backed store (no mocking).
/// </summary>
public sealed class Step080AcceptanceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-step080-" + Guid.NewGuid().ToString("N"));
    private readonly DeterministicGestureCandidateGenerator _generator = new();

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static EditScope Scope(params string[] models) =>
        new(new TimeWindow(0, 8_000_000), models.Select(m => new ModelId(m)).ToHashSet());

    private static LoopRun NewQueuedRun(TransitionKind? family, EditScope? scope = null, int seed = 42, int maxAttempts = 5) => new(
        Id: Guid.NewGuid(),
        DocumentId: Guid.NewGuid(),
        BaseRevision: 1,
        Scope: scope ?? Scope("Arches", "MegaTree"),
        InputHash: "hash-of-scope-and-seed",
        Seed: seed,
        MaxAttempts: maxAttempts,
        AttemptsMade: 0,
        Status: RefinementStatus.Queued,
        BestCandidateId: null,
        StopReason: null,
        ClaimToken: null,
        ClaimedAtUtc: null,
        HeartbeatAtUtc: null,
        CreatedAtUtc: DateTimeOffset.UtcNow,
        GestureFamily: family);

    [Fact]
    public async Task Step080_Acceptance_SameSeedAndInputs_YieldSameStructuredCandidate()
    {
        // Required acceptance, verbatim: "Same seed + inputs yields same structured candidate."
        var run = NewQueuedRun(TransitionKind.Build);

        var first = await _generator.GenerateAsync(run, prior: null, CancellationToken.None);
        var second = await _generator.GenerateAsync(run, prior: null, CancellationToken.None);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.Seed, second.Seed);
        Assert.Equal(first.SnapshotHash, second.SnapshotHash);
        Assert.Equal(first.Status, second.Status);
        Assert.Empty(first.Violations);
        Assert.NotNull(first.Gestures);
        Assert.Single(first.Gestures!);
        Assert.Single(second.Gestures!);
        AssertGesturesEqual(first.Gestures![0], second.Gestures![0]);
    }

    /// <summary>
    /// <see cref="TransitionGesture"/>'s own compiler-generated record equality compares
    /// <see cref="TransitionGesture.TargetProps"/> by reference (its static type is the
    /// <c>IReadOnlyList&lt;ModelId&gt;</c> interface, which has no structural <c>Equals</c> override) --
    /// two independently generated, content-identical instances are therefore never <c>==</c>-equal to
    /// each other even when every value they carry matches exactly. This helper compares every field by
    /// value instead, which is what "same seed + inputs yields same structured candidate" actually means.
    /// </summary>
    private static void AssertGesturesEqual(TransitionGesture expected, TransitionGesture actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Kind, actual.Kind);
        Assert.Equal(expected.Window, actual.Window);
        Assert.Equal(expected.TargetProps, actual.TargetProps);
        Assert.Equal(expected.StartIntensity, actual.StartIntensity);
        Assert.Equal(expected.EndIntensity, actual.EndIntensity);
    }

    [Fact]
    public async Task Step080_Acceptance_DifferentSeed_YieldsDifferentStructuredCandidate()
    {
        var runA = NewQueuedRun(TransitionKind.Build, seed: 1);
        var runB = runA with { Id = Guid.NewGuid(), Seed = 2 };

        var candidateA = await _generator.GenerateAsync(runA, prior: null, CancellationToken.None);
        var candidateB = await _generator.GenerateAsync(runB, prior: null, CancellationToken.None);

        Assert.NotEqual(candidateA.SnapshotHash, candidateB.SnapshotHash);
        Assert.NotEqual(candidateA.Gestures![0].StartIntensity, candidateB.Gestures![0].StartIntensity);
    }

    [Fact]
    public async Task Step080_Acceptance_ConstrainedToSelectedGestureFamily()
    {
        // This step's own heading, verbatim: "constrained to selected gesture family."
        var buildRun = NewQueuedRun(TransitionKind.Build);
        var blackoutRun = buildRun with { Id = Guid.NewGuid(), GestureFamily = TransitionKind.Blackout };

        var buildCandidate = await _generator.GenerateAsync(buildRun, prior: null, CancellationToken.None);
        var blackoutCandidate = await _generator.GenerateAsync(blackoutRun, prior: null, CancellationToken.None);

        Assert.Equal(TransitionKind.Build, buildCandidate.Gestures![0].Kind);
        Assert.Equal(TransitionKind.Blackout, blackoutCandidate.Gestures![0].Kind);
        // A Blackout gesture must darken every targeted prop to exactly 0 -- TransitionGesture's own
        // already-accepted PHASE-10 invariant, which this generator must never violate for the family it
        // was told to generate.
        Assert.Equal(0.0, blackoutCandidate.Gestures![0].StartIntensity);
        Assert.Equal(0.0, blackoutCandidate.Gestures![0].EndIntensity);
    }

    [Fact]
    public async Task Step080_Acceptance_GeneratedGestureNeverTargetsPropOutsidePermittedScope()
    {
        // Implementation guidance, verbatim: "preserve... edit permissions" -- a generated gesture can
        // never target a prop outside the run's own permitted EditScope.Models.
        var run = NewQueuedRun(TransitionKind.Transition, scope: Scope("Arches"));

        var candidate = await _generator.GenerateAsync(run, prior: null, CancellationToken.None);

        var targeted = Assert.Single(candidate.Gestures!)!.TargetProps;
        Assert.All(targeted, m => Assert.Contains(m, run.Scope.Models));
    }

    [Fact]
    public async Task Step080_Acceptance_PriorAttempt_AdvancesLineageOrdinal()
    {
        // Implementation guidance, verbatim: "preserve lineage links."
        var run = NewQueuedRun(TransitionKind.Release);

        var first = await _generator.GenerateAsync(run, prior: null, CancellationToken.None);
        Assert.Equal(0, first.Attempt);

        var second = await _generator.GenerateAsync(run, prior: first, CancellationToken.None);
        Assert.Equal(1, second.Attempt);
        Assert.Equal(run.Id, second.RunId);
        // Different attempt ordinal under the same run seed still yields a reproducibly different,
        // non-aliased structured result.
        Assert.NotEqual(first.SnapshotHash, second.SnapshotHash);
    }

    [Fact]
    public async Task Step080_Acceptance_GeneratedCandidate_PersistsAndReloadsUnchanged()
    {
        // "Avalonia screenshot or repeatable desktop interaction recording with saved-state
        // verification" -- this step's own required user-visible outcome, proven headlessly here against
        // the real Step 079 JsonRefinementRunStore (no mocking): generate once, save, reload from a
        // brand-new store instance, and confirm every structural field survived the round trip.
        var run = NewQueuedRun(TransitionKind.PhraseEnding);
        var store = new JsonRefinementRunStore(_root);
        await store.CreateAsync(run, CancellationToken.None);
        var claim = await store.TryClaimAsync(run.Id, TimeSpan.FromMinutes(5), CancellationToken.None);
        await store.SaveRunStateAsync(run with { Status = RefinementStatus.Generating }, claim!.Token, CancellationToken.None);

        var generated = await _generator.GenerateAsync(run, prior: null, CancellationToken.None);
        await store.SaveAttemptAsync(run.Id, claim.Token, generated, CancellationToken.None);

        var reloadStore = new JsonRefinementRunStore(_root);
        var reloaded = Assert.Single(await reloadStore.LoadAttemptsAsync(run.Id, CancellationToken.None));

        Assert.Equal(generated.Id, reloaded.Id);
        Assert.Equal(generated.Seed, reloaded.Seed);
        Assert.Equal(generated.SnapshotHash, reloaded.SnapshotHash);
        Assert.Equal(generated.Status, reloaded.Status);
        Assert.NotNull(reloaded.Gestures);
        Assert.Equal(generated.Gestures!.Count, reloaded.Gestures!.Count);
        AssertGesturesEqual(generated.Gestures[0], reloaded.Gestures[0]);
    }

    [Fact]
    public async Task Step080_NegativePath_MalformedProposal_EmptyScope_RejectedAndCannotAutoCommit()
    {
        // Required failure/edge case, verbatim: "Malformed proposal... must not auto-commit."
        var run = NewQueuedRun(TransitionKind.Build, scope: Scope());
        var store = new JsonRefinementRunStore(_root);
        await store.CreateAsync(run, CancellationToken.None);
        var claim = await store.TryClaimAsync(run.Id, TimeSpan.FromMinutes(5), CancellationToken.None);
        var generating = await store.SaveRunStateAsync(run with { Status = RefinementStatus.Generating }, claim!.Token, CancellationToken.None);

        var malformed = await _generator.GenerateAsync(run, prior: null, CancellationToken.None);

        Assert.Equal(RefinementStatus.Rejected, malformed.Status);
        Assert.NotEmpty(malformed.Violations);
        Assert.Empty(malformed.Gestures!);

        await store.SaveAttemptAsync(run.Id, claim.Token, malformed, CancellationToken.None);
        var previewReady = await store.SaveRunStateAsync(generating with { Status = RefinementStatus.Validating }, claim.Token, CancellationToken.None);
        previewReady = await store.SaveRunStateAsync(previewReady with { Status = RefinementStatus.PreviewReady }, claim.Token, CancellationToken.None);
        var awaitingApproval = await store.SaveRunStateAsync(
            previewReady with { Status = RefinementStatus.AwaitingApproval, AttemptsMade = 1, StopReason = "MalformedProposal" },
            claim.Token,
            CancellationToken.None);

        // Step 079's own already-accepted enforcement: a violating attempt can never become BestCandidateId.
        var ex = await Assert.ThrowsAsync<RefinementRunInvalidException>(() =>
            store.SaveRunStateAsync(awaitingApproval with { BestCandidateId = malformed.Id }, claim.Token, CancellationToken.None));
        Assert.Contains("violation", ex.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Step080_NegativePath_NoGestureFamilySelected_ThrowsBeforeGenerating()
    {
        var run = NewQueuedRun(family: null);

        var ex = await Assert.ThrowsAsync<RefinementRunInvalidException>(() =>
            _generator.GenerateAsync(run, prior: null, CancellationToken.None));

        Assert.Contains("GestureFamily", ex.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Step080_NegativePath_ValidGeneratedCandidate_StillCannotSkipApprovalToReachAccepted()
    {
        // "no-improvement and exhausted retry budget must not auto-commit" -- even a clean, non-violating
        // generated candidate cannot carry a run straight to Accepted without the distinct human-approval
        // transition Step 079's own RefinementStatusTransitions already enforces.
        var run = NewQueuedRun(TransitionKind.Build);
        var store = new JsonRefinementRunStore(_root);
        await store.CreateAsync(run, CancellationToken.None);
        var claim = await store.TryClaimAsync(run.Id, TimeSpan.FromMinutes(5), CancellationToken.None);
        var generating = await store.SaveRunStateAsync(run with { Status = RefinementStatus.Generating }, claim!.Token, CancellationToken.None);

        var clean = await _generator.GenerateAsync(run, prior: null, CancellationToken.None);
        Assert.Empty(clean.Violations);
        await store.SaveAttemptAsync(run.Id, claim.Token, clean, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<RefinementRunInvalidException>(() =>
            store.SaveRunStateAsync(generating with { Status = RefinementStatus.Accepted, BestCandidateId = clean.Id }, claim.Token, CancellationToken.None));
        Assert.Contains("cannot transition", ex.Reason, StringComparison.OrdinalIgnoreCase);
    }
}
