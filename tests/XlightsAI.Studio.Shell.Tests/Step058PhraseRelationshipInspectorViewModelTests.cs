using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-08 Step 058 — SCREEN 11 STRUCTURE. Fast, deterministic tests for
/// <see cref="PhraseRelationshipInspectorViewModel"/>'s own bindable link/unlink/template-selection/preview
/// contract against a real <see cref="JsonMotifLinkOverrideStore"/>/<see cref="ChoreographyTemplatePlanner"/>/
/// <see cref="JsonChoreographyTemplateStore"/> (not a mock) under a temporary directory.
/// </summary>
public sealed class Step058PhraseRelationshipInspectorViewModelTests : IDisposable
{
    private const long BarTicks = 10_000_000L;
    private const string SourceHash = "deadbeef";
    private static readonly ModelId MotionModel = new("Model/Tree");

    private readonly string _rootDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step058-vm-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
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

    private sealed record Fixture(
        SongId SongId, PhraseCandidate ChorusA, PhraseCandidate ChorusB, PhraseCandidate ChorusC,
        PhraseSegmentationAnalysis PhraseAnalysis, MotifClusteringAnalysis MotifClustering, SequenceDocument Document, EffectInstance MotionEffect);

    /// <summary>Chorus A/B/C (all 8 bars) automatically share a motif via three pairwise links. Chorus A's own document carries one motion effect.</summary>
    private static Fixture BuildFixture()
    {
        var songId = new SongId(Guid.NewGuid());
        var chorusA = Phrase(0, 8);
        var chorusB = Phrase(20, 28);
        var chorusC = Phrase(40, 48);

        var phraseAnalysis = new PhraseSegmentationAnalysis(
            VersionId: Guid.NewGuid(), SongId: songId, SourceHash: SourceHash,
            SourceSectionLabelVersionId: Guid.NewGuid(), SourceFingerprintSetVersionId: Guid.NewGuid(), SourceMelodyTrackId: null,
            AnalyzerVersion: "test-fixture/1.0.0", Settings: new PhraseSegmentationSettings(8, 1, 5_000_000L, "test-fixture-recipe/1.0.0"),
            Phrases: [chorusA, chorusB, chorusC]);

        var links = new List<MotifLinkCandidate>
        {
            new(chorusA.Id, chorusB.Id, 0.98, ["test fixture"]),
            new(chorusA.Id, chorusC.Id, 0.97, ["test fixture"]),
            new(chorusB.Id, chorusC.Id, 0.96, ["test fixture"]),
        };

        var motifClustering = new MotifClusteringAnalysis(
            VersionId: Guid.NewGuid(), SongId: songId, SourceHash: SourceHash,
            SourcePhraseSegmentationVersionId: phraseAnalysis.VersionId, SourceFingerprintSetVersionId: Guid.NewGuid(),
            AnalyzerVersion: "test-fixture/1.0.0", Settings: new MotifClusteringSettings(4, 0.9, "test-fixture-recipe/1.0.0"),
            Groups: [], Links: links); // Groups left empty here -- this view model always recomputes the effective view via MotifGroupOverrideApplier.

        // Captured from Chorus B's own window -- this fixture's own test scenario unlinks Chorus A from the
        // automatic {A,B,C} group and then selects Chorus B (not A) as the remaining {B,C} group's template source.
        var motionEffect = new EffectInstance(
            Id: Guid.NewGuid(), Target: MotionModel, Layer: 0,
            StartTicks: chorusB.StartTicks + 1_000_000, EndTicks: chorusB.StartTicks + 3_000_000,
            RecipeId: "Twinkle", RecipeVersion: "1.0", Parameters: new Dictionary<string, string> { ["Intensity"] = "0.8" });

        var document = new SequenceDocument(Id: Guid.NewGuid(), Revision: 1, Effects: [motionEffect]);

        return new Fixture(songId, chorusA, chorusB, chorusC, phraseAnalysis, motifClustering, document, motionEffect);
    }

    private static readonly ChoreographyInheritanceMask DefaultMask = new(
        Motion: true, Timing: ChoreographyTimingPolicy.Retime, Palette: true, RhythmLayer: true);

    private static readonly IReadOnlySet<int> NoRhythmLayers = new HashSet<int>();

    [Fact]
    public async Task Step058_Acceptance_Passes_UserLinksUnlinksMotifsAndPreviewsRelatedEffects()
    {
        var fx = BuildFixture();
        var overrideStore = new JsonMotifLinkOverrideStore(_rootDirectory);
        var planner = new ChoreographyTemplatePlanner(new JsonChoreographyTemplateStore(_rootDirectory));

        var vm = new PhraseRelationshipInspectorViewModel();
        Assert.True(vm.IsEmpty);

        var opened = await vm.OpenAsync(overrideStore, fx.SongId, fx.PhraseAnalysis, fx.MotifClustering, templateAnalysis: null);
        Assert.True(opened);
        Assert.True(vm.IsLoaded);

        // Before any decision: automatic clustering already groups A/B/C.
        var rowA = vm.Rows.Single(r => r.PhraseId == fx.ChorusA.Id);
        var rowB = vm.Rows.Single(r => r.PhraseId == fx.ChorusB.Id);
        var rowC = vm.Rows.Single(r => r.PhraseId == fx.ChorusC.Id);
        Assert.NotNull(rowA.MotifGroupId);
        Assert.Equal(rowA.MotifGroupId, rowB.MotifGroupId);
        Assert.Equal(rowA.MotifGroupId, rowC.MotifGroupId);

        // --- User unlinks Chorus A ---
        var unlinked = await vm.UnlinkPhraseAsync(overrideStore, fx.ChorusA.Id);
        Assert.True(unlinked);
        Assert.Null(vm.ActionErrorMessage);

        rowA = vm.Rows.Single(r => r.PhraseId == fx.ChorusA.Id);
        rowB = vm.Rows.Single(r => r.PhraseId == fx.ChorusB.Id);
        rowC = vm.Rows.Single(r => r.PhraseId == fx.ChorusC.Id);

        // "User can unlink one chorus without affecting others" (phase's own hard-gate test, verbatim).
        Assert.Null(rowA.MotifGroupId);
        Assert.NotNull(rowB.MotifGroupId);
        Assert.Equal(rowB.MotifGroupId, rowC.MotifGroupId);
        Assert.Contains(fx.ChorusC.Id, rowB.LinkedPhraseIds);

        // --- Reopen (new store instance over the same directory) must reproduce the same unlink decision ---
        var reopenedOverrideStore = new JsonMotifLinkOverrideStore(_rootDirectory);
        var vm2 = new PhraseRelationshipInspectorViewModel();
        await vm2.OpenAsync(reopenedOverrideStore, fx.SongId, fx.PhraseAnalysis, fx.MotifClustering, templateAnalysis: null);
        var reopenedRowA = vm2.Rows.Single(r => r.PhraseId == fx.ChorusA.Id);
        var reopenedRowB = vm2.Rows.Single(r => r.PhraseId == fx.ChorusB.Id);
        Assert.Null(reopenedRowA.MotifGroupId);
        Assert.Equal(rowB.MotifGroupId, reopenedRowB.MotifGroupId); // deterministic group id, reproducible after reopen.

        // --- User selects Chorus B as the template source for the remaining (B, C) group ---
        var selected = await vm.SelectTemplateSourceAsync(planner, fx.Document, fx.ChorusB.Id, DefaultMask, NoRhythmLayers);
        Assert.True(selected);
        Assert.Null(vm.ActionErrorMessage);
        Assert.NotNull(vm.SelectedTemplateId);
        Assert.True(vm.Rows.Single(r => r.PhraseId == fx.ChorusB.Id).IsTemplateSource);

        // --- "...previews related effects" ---
        var previewed = vm.PreviewTarget(planner, fx.Document, fx.ChorusC.Id);
        Assert.True(previewed);
        Assert.Null(vm.ActionErrorMessage);
        Assert.NotNull(vm.CurrentPreview);
        Assert.Equal(1, vm.PreviewEffectCount);
        Assert.Empty(vm.PreviewExcludedReasons);

        // --- User now links the isolated Chorus A back to Chorus B's group ---
        var linked = await vm.LinkPhrasesAsync(overrideStore, fx.ChorusA.Id, fx.ChorusB.Id);
        Assert.True(linked);
        var regroupedA = vm.Rows.Single(r => r.PhraseId == fx.ChorusA.Id);
        var regroupedB = vm.Rows.Single(r => r.PhraseId == fx.ChorusB.Id);
        var regroupedC = vm.Rows.Single(r => r.PhraseId == fx.ChorusC.Id);
        Assert.Equal(regroupedB.MotifGroupId, regroupedA.MotifGroupId);
        Assert.Equal(regroupedB.MotifGroupId, regroupedC.MotifGroupId); // union-find: A-B plus already-standing B-C reconnects all three.
    }

    [Fact]
    public async Task UnlinkPhraseAsync_PhraseNotInAnyGroup_ReturnsFalse_ActionErrorMessageSet_NothingPersisted()
    {
        var fx = BuildFixture();
        var overrideStore = new JsonMotifLinkOverrideStore(_rootDirectory);
        var vm = new PhraseRelationshipInspectorViewModel();
        var isolated = Phrase(100, 108);
        var phraseAnalysisWithIsolated = fx.PhraseAnalysis with { Phrases = [.. fx.PhraseAnalysis.Phrases, isolated] };
        await vm.OpenAsync(overrideStore, fx.SongId, phraseAnalysisWithIsolated, fx.MotifClustering, templateAnalysis: null);

        var result = await vm.UnlinkPhraseAsync(overrideStore, isolated.Id);

        Assert.False(result);
        Assert.NotNull(vm.ActionErrorMessage);
        Assert.False(File.Exists(overrideStore.GetPath(fx.SongId)));
    }

    [Fact]
    public async Task LinkPhrasesAsync_SamePhraseTwice_ReturnsFalse_ActionErrorMessageSet()
    {
        var fx = BuildFixture();
        var overrideStore = new JsonMotifLinkOverrideStore(_rootDirectory);
        var vm = new PhraseRelationshipInspectorViewModel();
        await vm.OpenAsync(overrideStore, fx.SongId, fx.PhraseAnalysis, fx.MotifClustering, templateAnalysis: null);

        var result = await vm.LinkPhrasesAsync(overrideStore, fx.ChorusA.Id, fx.ChorusA.Id);

        Assert.False(result);
        Assert.NotNull(vm.ActionErrorMessage);
    }

    [Fact]
    public async Task LinkPhrasesAsync_UnknownPhraseId_ReturnsFalse_ActionErrorMessageSet_NothingPersisted()
    {
        var fx = BuildFixture();
        var overrideStore = new JsonMotifLinkOverrideStore(_rootDirectory);
        var vm = new PhraseRelationshipInspectorViewModel();
        await vm.OpenAsync(overrideStore, fx.SongId, fx.PhraseAnalysis, fx.MotifClustering, templateAnalysis: null);

        var result = await vm.LinkPhrasesAsync(overrideStore, fx.ChorusA.Id, Guid.NewGuid());

        Assert.False(result);
        Assert.NotNull(vm.ActionErrorMessage);
        Assert.False(File.Exists(overrideStore.GetPath(fx.SongId)));
    }

    [Fact]
    public async Task UnlinkPhraseAsync_CancellationAlreadyRequested_ThrowsOperationCanceledException_NeverPersisted()
    {
        var fx = BuildFixture();
        var overrideStore = new JsonMotifLinkOverrideStore(_rootDirectory);
        var vm = new PhraseRelationshipInspectorViewModel();
        await vm.OpenAsync(overrideStore, fx.SongId, fx.PhraseAnalysis, fx.MotifClustering, templateAnalysis: null);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => vm.UnlinkPhraseAsync(overrideStore, fx.ChorusA.Id, cts.Token));
        Assert.False(File.Exists(overrideStore.GetPath(fx.SongId)));
    }

    [Fact]
    public async Task SelectTemplateSourceAsync_StaleTemplateAnalysis_ReturnsFalse_ActionErrorMessageSet_NeverCorrupted()
    {
        var fx = BuildFixture();
        var overrideStore = new JsonMotifLinkOverrideStore(_rootDirectory);
        var templateStore = new JsonChoreographyTemplateStore(_rootDirectory);
        var planner = new ChoreographyTemplatePlanner(templateStore);

        var vm = new PhraseRelationshipInspectorViewModel();
        await vm.OpenAsync(overrideStore, fx.SongId, fx.PhraseAnalysis, fx.MotifClustering, templateAnalysis: null);

        var first = await vm.SelectTemplateSourceAsync(planner, fx.Document, fx.ChorusA.Id, DefaultMask, NoRhythmLayers);
        Assert.True(first);
        var persistedAfterFirst = await templateStore.LoadAsync(templateStore.GetPath(fx.SongId), CancellationToken.None);

        // A second inspector instance, opened before the first one's own template was defined, holds a now-stale
        // (null) "current" -- selecting a template source through it must be rejected, not silently overwrite.
        var staleVm = new PhraseRelationshipInspectorViewModel();
        await staleVm.OpenAsync(overrideStore, fx.SongId, fx.PhraseAnalysis, fx.MotifClustering, templateAnalysis: null);

        var second = await staleVm.SelectTemplateSourceAsync(planner, fx.Document, fx.ChorusB.Id, DefaultMask, NoRhythmLayers);

        Assert.False(second);
        Assert.NotNull(staleVm.ActionErrorMessage);
        Assert.Null(staleVm.SelectedTemplateId);

        var persistedAfterRejection = await templateStore.LoadAsync(templateStore.GetPath(fx.SongId), CancellationToken.None);
        Assert.Equal(persistedAfterFirst.VersionId, persistedAfterRejection.VersionId); // untouched by the rejected call.
    }

    [Fact]
    public async Task PreviewTarget_NoTemplateSelectedYet_ReturnsFalse_ActionErrorMessageSet()
    {
        var fx = BuildFixture();
        var overrideStore = new JsonMotifLinkOverrideStore(_rootDirectory);
        var planner = new ChoreographyTemplatePlanner(new JsonChoreographyTemplateStore(_rootDirectory));
        var vm = new PhraseRelationshipInspectorViewModel();
        await vm.OpenAsync(overrideStore, fx.SongId, fx.PhraseAnalysis, fx.MotifClustering, templateAnalysis: null);

        var result = vm.PreviewTarget(planner, fx.Document, fx.ChorusB.Id);

        Assert.False(result);
        Assert.NotNull(vm.ActionErrorMessage);
        Assert.Null(vm.CurrentPreview);
    }
}
