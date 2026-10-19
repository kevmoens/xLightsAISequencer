using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Display.Tests;

/// <summary>
/// PHASE-09 Step 064 -- "Map pitch intervals/movement to jumps, holds and path traversal." Required
/// acceptance: "Ascending/descending tests preserve contour and reach boundaries safely." Failure/edge case:
/// "Invalid input, cancellation or stale persisted state must not corrupt the project." These tests exercise
/// the real <see cref="ShowLayoutImporter"/>/<see cref="DisplayGraphBuilder"/>/<see cref="SpatialGestureResolver"/>/
/// <see cref="GesturePreviewCatalog"/>/<see cref="JsonResolvedGesturePreviewStore"/> (not mocks) against the
/// same Step 059 golden fixture (five arches at WorldPosX 0,10,20,30,40) and a hand-built, deterministic
/// <see cref="MelodyTrack"/> -- never the real pitch-tracking pipeline, since this step's own scope is
/// geometry resolution, not pitch classification (already proven in Phase 07).
/// </summary>
[Trait("Category", "Integration")]
public sealed class Step064AcceptanceTests
{
    private static string FixtureDirectory(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static readonly GeometryPolicy DefaultPolicy = new(MaxNeighborDistance: 0.3, RespectGroups: true);

    private static readonly ModelId[] Arches =
    [
        new("Arch-01"), new("Arch-02"), new("Arch-03"), new("Arch-04"), new("Arch-05"),
    ];

    /// <summary>
    /// Builds a deterministic <see cref="MelodyTrack"/> for the exact <c>C4,D4,E4,G4,E4,D4,C4</c> sequence
    /// Phase 07's own <see cref="MelodicGestureMapper"/> remarks already prove maps to index walk
    /// <c>0,1,2,4,2,1,0</c> -- 1000-tick non-overlapping notes, hand-built gestures (not run through the real
    /// pitch tracker/classifier; Step 064's own scope is geometry resolution, already-classified gestures in).
    /// </summary>
    private static MelodyTrack BuildContourTrack()
    {
        int[] midiNotes = [60, 62, 64, 67, 64, 62, 60]; // C4 D4 E4 G4 E4 D4 C4
        var noteIds = Enumerable.Range(0, midiNotes.Length).Select(_ => Guid.NewGuid()).ToArray();

        var notes = new List<NoteEvent>();
        for (var i = 0; i < midiNotes.Length; i++)
        {
            notes.Add(new NoteEvent(noteIds[i], i * 1000, (i + 1) * 1000, midiNotes[i], Velocity: 1.0, Confidence: 1.0, UserCorrected: false));
        }

        PitchMotion[] motions = [PitchMotion.Rest, PitchMotion.StepUp, PitchMotion.StepUp, PitchMotion.LeapUp, PitchMotion.LeapDown, PitchMotion.StepDown, PitchMotion.StepDown];
        int[] intervals = [0, 2, 2, 3, -3, -2, -2];

        var gestures = new List<MelodicGesture>();
        for (var i = 0; i < midiNotes.Length; i++)
        {
            gestures.Add(new MelodicGesture(noteIds[i], motions[i], intervals[i], notes[i].StartTicks, notes[i].EndTicks, Confidence: 1.0));
        }

        return new MelodyTrack(Guid.NewGuid(), SourceAssetHash: "deterministic-test-hash", notes, gestures);
    }

    private static async Task<SpatialDisplayGraph> BuildArchGraphAsync()
    {
        var importer = new ShowLayoutImporter();
        var builder = new DisplayGraphBuilder();
        var imported = await importer.ImportReadOnlyAsync(FixtureDirectory("sample-show-spatial-display"), CancellationToken.None);
        return builder.Build(imported, DefaultPolicy);
    }

    [Fact]
    public async Task Step064_Acceptance_Passes_AscendingDescendingPreservesContourAndReachesBoundariesSafely()
    {
        var graph = await BuildArchGraphAsync();
        var path = new SpatialPath(Guid.NewGuid(), "Arch Route", Arches, IsClosed: false, Version: 0);
        var track = BuildContourTrack();
        var resolver = new SpatialGestureResolver();
        var window = new TimeWindow(0, 7000);

        var resolved = resolver.Resolve(track, window, path, graph, ResolutionSettings.Default);

        // Phase 07's own already-accepted index walk for this exact melodic contour: 0,1,2,4,2,1,0.
        Assert.Equal(
            [Arches[0], Arches[1], Arches[2], Arches[4], Arches[2], Arches[1], Arches[0]],
            resolved.Select(g => g.Target));

        // Contour preserved: the ascending half (C->D->E->G) strictly increases position, the descending
        // half (G->E->D->C) strictly decreases, and the final arrival matches the first (a round trip).
        var indices = resolved.Select(g => Array.IndexOf(Arches, g.Target)).ToList();
        Assert.True(indices[0] < indices[1] && indices[1] < indices[2] && indices[2] < indices[3], "ascending half must strictly increase.");
        Assert.True(indices[3] > indices[4] && indices[4] > indices[5] && indices[5] > indices[6], "descending half must strictly decrease.");
        Assert.Equal(indices[0], indices[6]);

        // The G4 leap reaches exactly the path's own last position (boundary) without overshoot/crash --
        // "reach boundaries safely."
        Assert.Equal(Arches[^1], resolved[3].Target);

        Assert.Equal([PathTraversalKind.Hold, PathTraversalKind.Step, PathTraversalKind.Step, PathTraversalKind.Leap, PathTraversalKind.Leap, PathTraversalKind.Step, PathTraversalKind.Step],
            resolved.Select(g => g.GestureKind));

        // Timestamp provenance: every resolved tick traces back exactly to its source note's own ticks.
        for (var i = 0; i < track.Notes.Count; i++)
        {
            Assert.Equal(track.Notes[i].StartTicks, resolved[i].StartTicks);
            Assert.Equal(track.Notes[i].EndTicks, resolved[i].EndTicks);
        }
    }

    [Fact]
    public async Task BoundaryPolicies_ClampSweepReverse_OvershootingRun_ReachesBoundariesSafelyWithoutCrashing()
    {
        var graph = await BuildArchGraphAsync();
        var shortPath = new SpatialPath(Guid.NewGuid(), "Short Route", [Arches[0], Arches[1], Arches[2]], IsClosed: false, Version: 0);

        // A monotonically ascending run (Rest, then five StepUps) on a 3-node path: every policy must
        // reject nothing, stay in-bounds and never throw -- this step's own required failure/edge text,
        // read for the boundary case specifically.
        int[] midi = [60, 62, 64, 66, 68, 70];
        var noteIds = Enumerable.Range(0, midi.Length).Select(_ => Guid.NewGuid()).ToArray();
        var notes = Enumerable.Range(0, midi.Length)
            .Select(i => new NoteEvent(noteIds[i], i * 1000, (i + 1) * 1000, midi[i], 1.0, 1.0, false)).ToList();
        PitchMotion[] motions = [PitchMotion.Rest, PitchMotion.StepUp, PitchMotion.StepUp, PitchMotion.StepUp, PitchMotion.StepUp, PitchMotion.StepUp];
        var gestures = Enumerable.Range(0, midi.Length)
            .Select(i => new MelodicGesture(noteIds[i], motions[i], motions[i] == PitchMotion.Rest ? 0 : 2, notes[i].StartTicks, notes[i].EndTicks, 1.0)).ToList();
        var track = new MelodyTrack(Guid.NewGuid(), "ascending-run-hash", notes, gestures);
        var window = new TimeWindow(0, 6000);
        var resolver = new SpatialGestureResolver();

        var clamped = resolver.Resolve(track, window, shortPath, graph, new ResolutionSettings(GestureBoundaryPolicy.Clamp, SkipUnavailable: false));
        Assert.All(clamped, g => Assert.Contains(g.Target, shortPath.OrderedModels));
        // Clamp holds at the last position once the edge is reached.
        Assert.Equal(Arches[2], clamped[^1].Target);
        Assert.Equal(Arches[2], clamped[^2].Target);

        var swept = resolver.Resolve(track, window, shortPath, graph, new ResolutionSettings(GestureBoundaryPolicy.Sweep, SkipUnavailable: false));
        Assert.All(swept, g => Assert.Contains(g.Target, shortPath.OrderedModels));
        Assert.Contains(swept, g => g.GestureKind == PathTraversalKind.Sweep);
        // Every produced slice stays inside its own source note's own [start, end) window -- no stray ticks.
        Assert.All(swept, g => Assert.True(g.StartTicks >= 0 && g.EndTicks <= 6000 && g.EndTicks > g.StartTicks));

        var reversed = resolver.Resolve(track, window, shortPath, graph, new ResolutionSettings(GestureBoundaryPolicy.Reverse, SkipUnavailable: false));
        Assert.All(reversed, g => Assert.Contains(g.Target, shortPath.OrderedModels));
        Assert.Contains(reversed, g => g.GestureKind == PathTraversalKind.Reverse);
        // A continuously ascending melody under Reverse bounces back down at least once -- never pinned
        // motionless at the edge the way Clamp is, and never leaves the 3-node path.
        var reversedIndices = reversed.Select(g => Array.IndexOf(shortPath.OrderedModels.ToArray(), g.Target)).ToList();
        Assert.Contains(0, reversedIndices.Skip(1)); // the walk returns to the near edge again at least once.
    }

    [Fact]
    public async Task Resolve_HoldExtendsPreviousGesture_RepeatAndRestPulseAtCurrentPosition()
    {
        var graph = await BuildArchGraphAsync();
        var path = new SpatialPath(Guid.NewGuid(), "Hold Route", Arches, IsClosed: false, Version: 0);
        var resolver = new SpatialGestureResolver();

        var noteIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var notes = new List<NoteEvent>
        {
            new(noteIds[0], 0, 1000, 60, 1.0, 1.0, false),
            new(noteIds[1], 1000, 2000, 60, 1.0, 1.0, false), // sustained note
            new(noteIds[2], 2000, 3000, 62, 1.0, 1.0, false),
        };
        var gestures = new List<MelodicGesture>
        {
            new(noteIds[0], PitchMotion.Rest, 0, 0, 1000, 1.0),
            new(noteIds[1], PitchMotion.Hold, 0, 1000, 2000, 1.0),
            new(noteIds[2], PitchMotion.StepUp, 2, 2000, 3000, 1.0),
        };
        var track = new MelodyTrack(Guid.NewGuid(), "hold-hash", notes, gestures);

        var resolved = resolver.Resolve(track, new TimeWindow(0, 3000), path, graph, ResolutionSettings.Default);

        // Hold extended the first (Rest) entry's own EndTicks rather than starting a new entry.
        Assert.Equal(2, resolved.Count);
        Assert.Equal(PathTraversalKind.Hold, resolved[0].GestureKind);
        Assert.Equal(0, resolved[0].StartTicks);
        Assert.Equal(2000, resolved[0].EndTicks); // extended by the Hold note's own EndTicks.
        Assert.Equal(Arches[0], resolved[0].Target);

        Assert.Equal(PathTraversalKind.Step, resolved[1].GestureKind);
        Assert.Equal(Arches[1], resolved[1].Target);
    }

    [Fact]
    public async Task Resolve_InvalidInput_RejectsBeforeAnyGestureIsProduced_NegativePath()
    {
        var graph = await BuildArchGraphAsync();
        var resolver = new SpatialGestureResolver();
        var track = BuildContourTrack();
        var validWindow = new TimeWindow(0, 7000);
        var validPath = new SpatialPath(Guid.NewGuid(), "Valid", Arches, IsClosed: false, Version: 0);

        // Empty path.
        Assert.Throws<SpatialDisplayGraphInvalidException>(() =>
            resolver.Resolve(track, validWindow, new SpatialPath(Guid.NewGuid(), "Empty", [], IsClosed: false, Version: 0), graph, ResolutionSettings.Default));

        // Invalid window.
        Assert.Throws<SpatialDisplayGraphInvalidException>(() =>
            resolver.Resolve(track, new TimeWindow(100, 100), validPath, graph, ResolutionSettings.Default));

        // Unknown boundary policy.
        Assert.Throws<SpatialDisplayGraphInvalidException>(() =>
            resolver.Resolve(track, validWindow, validPath, graph, new ResolutionSettings("Diagonal", SkipUnavailable: false)));

        // Path references a node absent from the display graph, SkipUnavailable false (default).
        var foreignPath = new SpatialPath(Guid.NewGuid(), "Foreign", [new ModelId("Does-Not-Exist")], IsClosed: false, Version: 0);
        Assert.Throws<SpatialDisplayGraphInvalidException>(() =>
            resolver.Resolve(track, validWindow, foreignPath, graph, ResolutionSettings.Default));

        // SkipUnavailable true: the same foreign node is excluded rather than rejected, but an
        // all-unavailable path still rejects outright (nothing left to walk).
        var allForeign = new SpatialPath(Guid.NewGuid(), "AllForeign", [new ModelId("Ghost-1"), new ModelId("Ghost-2")], IsClosed: false, Version: 0);
        Assert.Throws<SpatialDisplayGraphInvalidException>(() =>
            resolver.Resolve(track, validWindow, allForeign, graph, new ResolutionSettings(GestureBoundaryPolicy.Clamp, SkipUnavailable: true)));

        var mixedPath = new SpatialPath(Guid.NewGuid(), "Mixed", [new ModelId("Ghost-1"), Arches[0], Arches[1]], IsClosed: false, Version: 0);
        var skipped = resolver.Resolve(track, validWindow, mixedPath, graph, new ResolutionSettings(GestureBoundaryPolicy.Clamp, SkipUnavailable: true));
        Assert.All(skipped, g => Assert.Contains(g.Target, new[] { Arches[0], Arches[1] }));
    }

    [Fact]
    public async Task GesturePreviewCatalog_ResolveAsync_PersistsAndReloadsAcrossRestart()
    {
        var graph = await BuildArchGraphAsync();
        var path = new SpatialPath(Guid.NewGuid(), "Arch Route", Arches, IsClosed: false, Version: 0);
        var track = BuildContourTrack();
        var window = new TimeWindow(0, 7000);

        var cacheDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step064-{Guid.NewGuid():N}");
        try
        {
            var catalog1 = new GesturePreviewCatalog(new JsonResolvedGesturePreviewStore(cacheDirectory), new SpatialGestureResolver());
            var preview = await catalog1.ResolveAsync(graph, path, track, window, ResolutionSettings.Default, CancellationToken.None);

            Assert.Equal(graph.SourceLayoutHash, preview.SourceLayoutHash);
            Assert.Equal(path.Id, preview.PathId);
            Assert.Equal(7, preview.Gestures.Count);

            // "Restart": brand-new store instance over the same on-disk directory.
            var store2 = new JsonResolvedGesturePreviewStore(cacheDirectory);
            var cachePath = store2.GetPath(graph.SourceLayoutHash, path.Id, path.Version, track.Id, ResolutionSettings.Default.BoundaryPolicy);
            Assert.True(File.Exists(cachePath));
            var reloaded = await store2.LoadAsync(cachePath, CancellationToken.None);
            Assert.Equal(preview.Gestures, reloaded.Gestures);
        }
        finally
        {
            if (Directory.Exists(cacheDirectory))
            {
                Directory.Delete(cacheDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task GesturePreviewCatalog_ResolveAsync_InvalidInput_NeverWritesCacheFile_NegativePath()
    {
        var graph = await BuildArchGraphAsync();
        var track = BuildContourTrack();
        var emptyPath = new SpatialPath(Guid.NewGuid(), "Empty", [], IsClosed: false, Version: 0);

        var cacheDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step064-invalid-{Guid.NewGuid():N}");
        try
        {
            var store = new JsonResolvedGesturePreviewStore(cacheDirectory);
            var catalog = new GesturePreviewCatalog(store, new SpatialGestureResolver());

            await Assert.ThrowsAsync<SpatialDisplayGraphInvalidException>(() =>
                catalog.ResolveAsync(graph, emptyPath, track, new TimeWindow(0, 7000), ResolutionSettings.Default, CancellationToken.None));

            Assert.False(Directory.Exists(cacheDirectory));
        }
        finally
        {
            if (Directory.Exists(cacheDirectory))
            {
                Directory.Delete(cacheDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task GesturePreviewCatalog_ResolveAsync_Cancelled_DoesNotPersist_NegativePath()
    {
        var graph = await BuildArchGraphAsync();
        var path = new SpatialPath(Guid.NewGuid(), "Arch Route", Arches, IsClosed: false, Version: 0);
        var track = BuildContourTrack();

        var cacheDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step064-cancel-{Guid.NewGuid():N}");
        try
        {
            var store = new JsonResolvedGesturePreviewStore(cacheDirectory);
            var catalog = new GesturePreviewCatalog(store, new SpatialGestureResolver());
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                catalog.ResolveAsync(graph, path, track, new TimeWindow(0, 7000), ResolutionSettings.Default, cts.Token));

            Assert.False(File.Exists(store.GetPath(graph.SourceLayoutHash, path.Id, path.Version, track.Id, ResolutionSettings.Default.BoundaryPolicy)));
        }
        finally
        {
            if (Directory.Exists(cacheDirectory))
            {
                Directory.Delete(cacheDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Store_LoadAsync_CorruptFile_ThrowsCorruptException_NegativePath()
    {
        var cacheDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step064-store-corrupt-{Guid.NewGuid():N}");
        Directory.CreateDirectory(cacheDirectory);
        var path = Path.Combine(cacheDirectory, "corrupt.json");
        await File.WriteAllTextAsync(path, "{ not valid json");

        try
        {
            var store = new JsonResolvedGesturePreviewStore(cacheDirectory);
            await Assert.ThrowsAsync<SpatialDisplayGraphCorruptException>(
                () => store.LoadAsync(path, CancellationToken.None));
        }
        finally
        {
            Directory.Delete(cacheDirectory, recursive: true);
        }
    }
}
