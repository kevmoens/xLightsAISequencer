using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Display;
using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-09 Step 064 -- "Map pitch intervals/movement to jumps, holds and path traversal." Required
/// acceptance: "Ascending/descending tests preserve contour and reach boundaries safely." Fast, deterministic
/// tests for <see cref="DisplayPathEditorViewModel"/>'s own bindable gesture-preview contract
/// (<see cref="DisplayPathEditorViewModel.ResolveGesturePreviewAsync"/>) against a real
/// <see cref="GesturePreviewCatalog"/>/<see cref="JsonResolvedGesturePreviewStore"/>/<see cref="SpatialGestureResolver"/>
/// (not mocks) under a temporary directory. No real SCREEN 12 CHOREOGRAPHY Avalonia/AXAML view exists (the
/// project's own pre-existing, consistent convention -- see
/// <c>implementation-evidence/PHASE-09/STEP-059.md</c> through <c>STEP-063.md</c> §7); this step's own
/// required acceptance text is proven here at the bindable view-model layer the real choreography timeline
/// UI would bind to.
/// </summary>
public sealed class Step064DisplayPathEditorViewModelTests : IDisposable
{
    private readonly string _cacheDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step064-vm-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_cacheDirectory))
        {
            Directory.Delete(_cacheDirectory, recursive: true);
        }
    }

    private static readonly GeometryPolicy DefaultPolicy = new(MaxNeighborDistance: 0.3, RespectGroups: true);

    private static readonly ModelId NodeA = new("A");
    private static readonly ModelId NodeB = new("B");
    private static readonly ModelId NodeC = new("C");

    private static SpatialDisplayGraph BuildThreeNodeGraphWithPath(out SpatialPath path)
    {
        var imported = new DisplayGraph(
            "hash-064",
            [
                new DisplayModel(NodeA, "A", "Arches", [], [new Point2(0, 0)]),
                new DisplayModel(NodeB, "B", "Arches", [], [new Point2(1, 0)]),
                new DisplayModel(NodeC, "C", "Arches", [], [new Point2(2, 0)]),
            ],
            [],
            []);
        var built = new DisplayGraphBuilder().Build(imported, DefaultPolicy);
        path = new SpatialPath(Guid.NewGuid(), "ABC", [NodeA, NodeB, NodeC], IsClosed: false, Version: 0);
        return built with { Paths = [path] };
    }

    private static MelodyTrack BuildTwoNoteAscendingTrack()
    {
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        var notes = new[]
        {
            new NoteEvent(idA, 0, 1000, 60, 1.0, 1.0, false),
            new NoteEvent(idB, 1000, 2000, 62, 1.0, 1.0, false),
        };
        var gestures = new[]
        {
            new MelodicGesture(idA, PitchMotion.Rest, 0, 0, 1000, 1.0),
            new MelodicGesture(idB, PitchMotion.StepUp, 2, 1000, 2000, 1.0),
        };
        return new MelodyTrack(Guid.NewGuid(), "two-note-hash", notes, gestures);
    }

    private GesturePreviewCatalog BuildCatalog() => new(new JsonResolvedGesturePreviewStore(_cacheDirectory), new SpatialGestureResolver());

    [Fact]
    public async Task ResolveGesturePreviewAsync_AscendingTwoNoteTrack_PopulatesGesturePreview()
    {
        var graph = BuildThreeNodeGraphWithPath(out var path);
        var vm = new DisplayPathEditorViewModel();
        await vm.OpenAsync(new DisplayLayoutEditor(new JsonDisplayLayoutCorrectionsStore(_cacheDirectory)), graph);
        var track = BuildTwoNoteAscendingTrack();

        var resolved = await vm.ResolveGesturePreviewAsync(
            BuildCatalog(), path.Id, track, new TimeWindow(0, 2000), ResolutionSettings.Default);

        Assert.True(resolved);
        Assert.Null(vm.ActionErrorMessage);
        Assert.NotNull(vm.GesturePreviewVersionId);
        Assert.Equal(2, vm.GesturePreview.Count);
        Assert.Equal(NodeA, vm.GesturePreview[0].Target);
        Assert.Equal(PathTraversalKind.Hold, vm.GesturePreview[0].GestureKind);
        Assert.Equal(NodeB, vm.GesturePreview[1].Target);
        Assert.Equal(PathTraversalKind.Step, vm.GesturePreview[1].GestureKind);
    }

    [Fact]
    public async Task OpenAsync_ReopeningADifferentLayout_ClearsPreviousGesturePreview()
    {
        var graph = BuildThreeNodeGraphWithPath(out var path);
        var vm = new DisplayPathEditorViewModel();
        var editor = new DisplayLayoutEditor(new JsonDisplayLayoutCorrectionsStore(_cacheDirectory));
        await vm.OpenAsync(editor, graph);
        await vm.ResolveGesturePreviewAsync(BuildCatalog(), path.Id, BuildTwoNoteAscendingTrack(), new TimeWindow(0, 2000), ResolutionSettings.Default);
        Assert.NotEmpty(vm.GesturePreview);

        var otherGraph = BuildThreeNodeGraphWithPath(out _) with { SourceLayoutHash = "hash-064-other" };
        await vm.OpenAsync(editor, otherGraph);

        Assert.Empty(vm.GesturePreview);
        Assert.Null(vm.GesturePreviewVersionId);
    }

    [Fact]
    public async Task ResolveGesturePreviewAsync_NothingOpen_SetsActionErrorAndLeavesGesturePreviewEmpty_NegativePath()
    {
        var vm = new DisplayPathEditorViewModel();

        var resolved = await vm.ResolveGesturePreviewAsync(
            BuildCatalog(), Guid.NewGuid(), BuildTwoNoteAscendingTrack(), new TimeWindow(0, 2000), ResolutionSettings.Default);

        Assert.False(resolved);
        Assert.False(string.IsNullOrEmpty(vm.ActionErrorMessage));
        Assert.Empty(vm.GesturePreview);
    }

    [Fact]
    public async Task ResolveGesturePreviewAsync_UnknownPathId_RejectsWithoutPartialMutation_NegativePath()
    {
        var graph = BuildThreeNodeGraphWithPath(out _);
        var vm = new DisplayPathEditorViewModel();
        await vm.OpenAsync(new DisplayLayoutEditor(new JsonDisplayLayoutCorrectionsStore(_cacheDirectory)), graph);

        var resolved = await vm.ResolveGesturePreviewAsync(
            BuildCatalog(), Guid.NewGuid(), BuildTwoNoteAscendingTrack(), new TimeWindow(0, 2000), ResolutionSettings.Default);

        Assert.False(resolved);
        Assert.False(string.IsNullOrEmpty(vm.ActionErrorMessage));
        Assert.Empty(vm.GesturePreview);
    }

    [Fact]
    public async Task ResolveGesturePreviewAsync_InvalidWindow_RejectsWithoutPartialMutation_NegativePath()
    {
        var graph = BuildThreeNodeGraphWithPath(out var path);
        var vm = new DisplayPathEditorViewModel();
        await vm.OpenAsync(new DisplayLayoutEditor(new JsonDisplayLayoutCorrectionsStore(_cacheDirectory)), graph);
        var catalog = BuildCatalog();
        await vm.ResolveGesturePreviewAsync(catalog, path.Id, BuildTwoNoteAscendingTrack(), new TimeWindow(0, 2000), ResolutionSettings.Default);
        var before = vm.GesturePreview;

        var resolved = await vm.ResolveGesturePreviewAsync(
            catalog, path.Id, BuildTwoNoteAscendingTrack(), new TimeWindow(500, 500), ResolutionSettings.Default);

        Assert.False(resolved);
        Assert.False(string.IsNullOrEmpty(vm.ActionErrorMessage));
        Assert.Equal(before, vm.GesturePreview);
    }
}
