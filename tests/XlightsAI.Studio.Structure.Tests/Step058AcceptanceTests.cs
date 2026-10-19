using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Structure.Tests;

/// <summary>
/// PHASE-08 / Step 058 -- "Build phrase relationship/propagation inspector." Required acceptance: "User
/// links/unlinks motifs and previews related effects." Failure/edge case: "Invalid input, cancellation or
/// stale persisted state must not corrupt the project." These tests exercise the real, domain-layer
/// building blocks (<see cref="MotifGroupOverrideApplier"/>, <see cref="JsonMotifLinkOverrideStore"/>) the
/// real SCREEN 11 STRUCTURE view model (<c>PhraseRelationshipInspectorViewModel</c> in
/// <c>XlightsAI.Studio.Shell</c>, exercised separately in <c>Step058PhraseRelationshipInspectorViewModelTests</c>)
/// is built on top of -- not a mock.
/// </summary>
public sealed class Step058AcceptanceTests : IDisposable
{
    private const long BarTicks = 10_000_000L;

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"xlai-step058-{Guid.NewGuid():N}");

    public Step058AcceptanceTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private static PhraseCandidate Phrase(int startBar, int endBarExclusive) =>
        new(
            Id: Guid.NewGuid(),
            SectionLabelId: Guid.NewGuid(),
            StartBarIndex: startBar,
            EndBarIndexExclusive: endBarExclusive,
            StartTicks: startBar * BarTicks,
            EndTicks: endBarExclusive * BarTicks,
            Bars: endBarExclusive - startBar,
            Source: PhraseBoundarySource.BarHypothesis,
            Confidence: 1.0,
            Evidence: ["test fixture"],
            SignatureHash: "unused-in-this-step",
            UserCorrected: false);

    /// <summary>Chorus A/B/C automatically share a motif (all three pairwise links above threshold); Verse D is a distinct, unlinked passage.</summary>
    private sealed record Fixture(SongId SongId, PhraseCandidate ChorusA, PhraseCandidate ChorusB, PhraseCandidate ChorusC, PhraseCandidate VerseD, IReadOnlyList<PhraseCandidate> Phrases, IReadOnlyList<MotifLinkCandidate> AutomaticLinks);

    private static Fixture BuildFixture()
    {
        var songId = new SongId(Guid.NewGuid());
        var chorusA = Phrase(0, 8);
        var chorusB = Phrase(20, 28);
        var chorusC = Phrase(40, 48);
        var verseD = Phrase(60, 68);

        var links = new List<MotifLinkCandidate>
        {
            new(chorusA.Id, chorusB.Id, 0.98, ["test fixture"]),
            new(chorusA.Id, chorusC.Id, 0.97, ["test fixture"]),
            new(chorusB.Id, chorusC.Id, 0.96, ["test fixture"]),
        };

        return new Fixture(songId, chorusA, chorusB, chorusC, verseD, [chorusA, chorusB, chorusC, verseD], links);
    }

    [Fact]
    public async Task Step058_Acceptance_Passes_UserLinksUnlinksMotifsAndPreviewsRelatedEffects()
    {
        var fx = BuildFixture();

        // Before any override: automatic clustering already groups A/B/C; D is a distinct passage, no group.
        var before = MotifGroupOverrideApplier.Apply(fx.SongId, fx.Phrases, fx.AutomaticLinks, []);
        var beforeGroup = Assert.Single(before);
        Assert.Equal([fx.ChorusA.Id, fx.ChorusB.Id, fx.ChorusC.Id], beforeGroup.PhraseIds);

        // --- User unlinks Chorus A from the group ---
        var unlinkDecisions = new List<MotifLinkOverride>
        {
            new(fx.ChorusA.Id, fx.ChorusB.Id, ForceLinked: false),
            new(fx.ChorusA.Id, fx.ChorusC.Id, ForceLinked: false),
        };
        var afterUnlink = MotifGroupOverrideApplier.Apply(fx.SongId, fx.Phrases, fx.AutomaticLinks, unlinkDecisions);

        // "User can unlink one chorus without affecting others" (phase's own hard-gate test, verbatim):
        // B and C remain linked to each other; A has no group of its own.
        var remainingGroup = Assert.Single(afterUnlink);
        Assert.Equal([fx.ChorusB.Id, fx.ChorusC.Id], remainingGroup.PhraseIds);
        Assert.DoesNotContain(afterUnlink, g => g.PhraseIds.Contains(fx.ChorusA.Id));

        // --- User links the now-isolated Chorus A to the previously distinct Verse D ---
        var linkDecision = new List<MotifLinkOverride>(unlinkDecisions) { new(fx.ChorusA.Id, fx.VerseD.Id, ForceLinked: true) };
        var afterLink = MotifGroupOverrideApplier.Apply(fx.SongId, fx.Phrases, fx.AutomaticLinks, linkDecision);

        Assert.Equal(2, afterLink.Count);
        var newGroup = Assert.Single(afterLink, g => g.PhraseIds.Contains(fx.ChorusA.Id));
        Assert.Equal([fx.ChorusA.Id, fx.VerseD.Id], newGroup.PhraseIds);
        Assert.Equal(1.0, newGroup.Confidence); // user-approved manual link, no automatic similarity score.
        var untouchedGroup = Assert.Single(afterLink, g => g.PhraseIds.Contains(fx.ChorusB.Id));
        Assert.Equal([fx.ChorusB.Id, fx.ChorusC.Id], untouchedGroup.PhraseIds);

        // --- Persistence after reopen: a brand-new store instance over the same directory reproduces the same decisions ---
        var store = new JsonMotifLinkOverrideStore(_tempDir);
        await store.SaveAsync(fx.SongId, linkDecision, CancellationToken.None);

        var reopenedStore = new JsonMotifLinkOverrideStore(_tempDir);
        var reloaded = await reopenedStore.LoadAsync(fx.SongId, CancellationToken.None);
        var afterReopen = MotifGroupOverrideApplier.Apply(fx.SongId, fx.Phrases, fx.AutomaticLinks, reloaded);

        Assert.Equal(afterLink.Select(g => g.Id).OrderBy(id => id), afterReopen.Select(g => g.Id).OrderBy(id => id));
        Assert.Equal(newGroup.Id, Assert.Single(afterReopen, g => g.PhraseIds.Contains(fx.ChorusA.Id)).Id);

        // "...previews related effects": the effective group feeds straight into the already-shipped Step 057
        // ChoreographyTemplatePlanner (exercised end-to-end, against the real planner, in Step057AcceptanceTests;
        // this step's own contribution is that `newGroup`/`untouchedGroup` above are exactly what a caller would
        // pass as `motifGroupId` to IChoreographyTemplatePlanner.DefineTemplateAsync -- proven together with the
        // real planner in Step058PhraseRelationshipInspectorViewModelTests, the real SCREEN 11 view model).
    }

    [Fact]
    public void Apply_UnknownPhraseIdInOverride_ThrowsMotifClusteringInvalidException_NeverPartiallyApplied()
    {
        var fx = BuildFixture();
        var badOverride = new MotifLinkOverride(fx.ChorusA.Id, Guid.NewGuid(), ForceLinked: true);

        var ex = Assert.Throws<MotifClusteringInvalidException>(() =>
            MotifGroupOverrideApplier.Apply(fx.SongId, fx.Phrases, fx.AutomaticLinks, [badOverride]));
        Assert.Contains("not present", ex.Reason);
    }

    [Fact]
    public void Apply_OverrideReferencesSamePhraseTwice_ThrowsMotifClusteringInvalidException()
    {
        var fx = BuildFixture();
        var badOverride = new MotifLinkOverride(fx.ChorusA.Id, fx.ChorusA.Id, ForceLinked: true);

        var ex = Assert.Throws<MotifClusteringInvalidException>(() =>
            MotifGroupOverrideApplier.Apply(fx.SongId, fx.Phrases, fx.AutomaticLinks, [badOverride]));
        Assert.Contains("same phrase twice", ex.Reason);
    }

    [Fact]
    public void Apply_NullArguments_ThrowArgumentNullException()
    {
        var fx = BuildFixture();
        Assert.Throws<ArgumentNullException>(() => MotifGroupOverrideApplier.Apply(fx.SongId, null!, fx.AutomaticLinks, []));
        Assert.Throws<ArgumentNullException>(() => MotifGroupOverrideApplier.Apply(fx.SongId, fx.Phrases, null!, []));
        Assert.Throws<ArgumentNullException>(() => MotifGroupOverrideApplier.Apply(fx.SongId, fx.Phrases, fx.AutomaticLinks, null!));
    }

    [Fact]
    public async Task JsonMotifLinkOverrideStore_LoadAsync_NoFileYet_ReturnsEmpty_NeverThrows()
    {
        var store = new JsonMotifLinkOverrideStore(_tempDir);
        var songId = new SongId(Guid.NewGuid());

        var loaded = await store.LoadAsync(songId, CancellationToken.None);
        Assert.Empty(loaded);
        Assert.False(File.Exists(store.GetPath(songId)));
    }

    [Fact]
    public async Task JsonMotifLinkOverrideStore_LoadAsync_CorruptFile_ThrowsMotifClusteringCorruptException()
    {
        var store = new JsonMotifLinkOverrideStore(_tempDir);
        var songId = new SongId(Guid.NewGuid());
        await File.WriteAllTextAsync(store.GetPath(songId), "{ not valid json");

        await Assert.ThrowsAsync<MotifClusteringCorruptException>(() => store.LoadAsync(songId, CancellationToken.None));
    }

    [Fact]
    public async Task JsonMotifLinkOverrideStore_SaveAsync_CancellationAlreadyRequested_ThrowsOperationCanceledException_NeverWritesFile()
    {
        var store = new JsonMotifLinkOverrideStore(_tempDir);
        var songId = new SongId(Guid.NewGuid());

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            store.SaveAsync(songId, [new MotifLinkOverride(Guid.NewGuid(), Guid.NewGuid(), true)], cts.Token));
        Assert.False(File.Exists(store.GetPath(songId)));
    }

    [Fact]
    public void Apply_NoPhrases_ReturnsEmpty_NeverThrows()
    {
        var songId = new SongId(Guid.NewGuid());
        var result = MotifGroupOverrideApplier.Apply(songId, [], [], []);
        Assert.Empty(result);
    }
}
