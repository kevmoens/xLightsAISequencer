using XlightsAI.Studio.Composition;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.WholeSong.Tests;

/// <summary>
/// PHASE-14 Step 090 -- "Create song-level creative direction with palettes, motif identities, energy and
/// focal points." Required acceptance: "Plan renders as inspectable timeline summary." Implementation
/// guidance: "Define song-level direction and priorities with user-tunable target contrast; link palette and
/// focal role to existing section IDs." Failure/edge case: "Invalid input, cancellation or stale persisted
/// state must not corrupt the project." These tests exercise the real
/// <see cref="SongDirectionPlanner"/>/<see cref="SongDirectionTimelineProjector"/>/<see cref="SongDirectionCatalog"/>/
/// <see cref="JsonSongDirectionPlanStore"/> (no mocking) against real PHASE-08 <see cref="SectionLabelAnalysis"/>/
/// <see cref="MotifClusteringAnalysis"/> fixtures built directly via their own production constructors.
/// </summary>
public sealed class Step090AcceptanceTests
{
    private static string NewCacheDirectory(string suffix) =>
        Path.Combine(Path.GetTempPath(), $"xlai-step090-{suffix}-{Guid.NewGuid():N}");

    /// <summary>
    /// Builds the phase-gate's own required demo song shape -- "Intro/Verse/Chorus/Verse/Chorus/Bridge/Final
    /// chorus" (Section 1) -- as a real <see cref="SectionLabelAnalysis"/>, eight bars per section.
    /// </summary>
    private static (SectionLabelAnalysis Labels, SectionLabelCandidate Intro, SectionLabelCandidate Verse1,
        SectionLabelCandidate Chorus1, SectionLabelCandidate Verse2, SectionLabelCandidate Chorus2,
        SectionLabelCandidate Bridge, SectionLabelCandidate FinalChorus) BuildSectionLabels(SongId songId)
    {
        SectionLabelCandidate Make(int startBar, SectionKind kind) => new(
            Guid.NewGuid(), Guid.NewGuid(), startBar, startBar + 8, kind, 0.9, ["fixture evidence"], UserCorrected: false);

        var intro = Make(0, SectionKind.Intro);
        var verse1 = Make(8, SectionKind.Verse);
        var chorus1 = Make(16, SectionKind.Chorus);
        var verse2 = Make(24, SectionKind.Verse);
        var chorus2 = Make(32, SectionKind.Chorus);
        var bridge = Make(40, SectionKind.Bridge);
        var finalChorus = Make(48, SectionKind.Chorus);

        var labels = new SectionLabelAnalysis(
            Guid.NewGuid(),
            songId,
            "fixture-source-hash",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "fixture-analyzer-v1",
            new SectionLabelSettings(0.8, 0.5, "fixture-recipe"),
            [intro, verse1, chorus1, verse2, chorus2, bridge, finalChorus]);

        return (labels, intro, verse1, chorus1, verse2, chorus2, bridge, finalChorus);
    }

    private static MotifClusteringAnalysis BuildMotifs(SongId songId, out MotifGroupCandidate chorusMotif)
    {
        var phraseA = Guid.NewGuid();
        var phraseB = Guid.NewGuid();
        chorusMotif = new MotifGroupCandidate(
            Guid.NewGuid(), [phraseA, phraseB], phraseA, 0.93, ["choruses share the same hook"]);

        return new MotifClusteringAnalysis(
            Guid.NewGuid(),
            songId,
            "fixture-source-hash",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "fixture-clusterer-v1",
            new MotifClusteringSettings(16, 0.85, "fixture-recipe"),
            [chorusMotif],
            [new MotifLinkCandidate(phraseA, phraseB, 0.93, ["fixture link evidence"])]);
    }

    [Fact]
    public async Task Step090_Acceptance_Passes()
    {
        // Arrange: the phase-gate's own required demo song shape, plus a motif group shared by both
        // choruses -- "show section intensity arc and shared motif variants" (Section 1).
        var songId = new SongId(Guid.NewGuid());
        var (labels, intro, verse1, chorus1, verse2, chorus2, bridge, finalChorus) = BuildSectionLabels(songId);
        var motifs = BuildMotifs(songId, out var chorusMotif);

        var inputs = new List<SongSectionDirectionInput>
        {
            new(intro.Id, 0.10, 0.10, "Calm", MusicalRole.Atmosphere, []),
            new(verse1.Id, 0.40, 0.30, "Calm", MusicalRole.Lead, []),
            new(chorus1.Id, 0.80, 0.70, "Vivid", MusicalRole.Lead, [chorusMotif.Id]),
            new(verse2.Id, 0.45, 0.30, "Calm", MusicalRole.Lead, []),
            new(chorus2.Id, 0.90, 0.80, "Vivid", MusicalRole.Lead, [chorusMotif.Id]),
            new(bridge.Id, 0.55, 0.60, "Moody", MusicalRole.Harmony, []),
            new(finalChorus.Id, 0.97, 0.95, "Vivid", MusicalRole.Lead, [chorusMotif.Id]),
        };

        ISongDirectionPlanner planner = new SongDirectionPlanner();
        var createdAt = DateTimeOffset.UtcNow;

        // Act: plan.
        var plan = planner.Plan(labels, motifs, inputs, baseRevision: 5, createdAt);

        // Assert: domain-level provenance and user-tunable fields survived planning.
        Assert.Equal(songId, plan.SongId);
        Assert.Equal(labels.VersionId, plan.SourceSectionLabelVersionId);
        Assert.Equal(motifs.VersionId, plan.SourceMotifClusteringVersionId);
        Assert.Equal(5, plan.BaseRevision);
        Assert.Equal(createdAt, plan.CreatedAtUtc);
        Assert.Equal(7, plan.Sections.Count);

        var byId = plan.Sections.ToDictionary(s => s.SectionId);
        Assert.Equal(SectionKind.Chorus, byId[chorus1.Id].Kind);
        Assert.Equal("Vivid", byId[chorus1.Id].PaletteId);
        Assert.Equal(MusicalRole.Lead, byId[chorus1.Id].FocalRole);
        Assert.Contains(chorusMotif.Id, byId[chorus1.Id].MotifGroupIds);
        Assert.Contains(chorusMotif.Id, byId[chorus2.Id].MotifGroupIds);
        Assert.Contains(chorusMotif.Id, byId[finalChorus.Id].MotifGroupIds);
        Assert.Empty(byId[intro.Id].MotifGroupIds);

        // Act: persist -- "Saved audio/analysis artifact with timestamp provenance" (User-visible outcome).
        var cacheDirectory = NewCacheDirectory("acceptance");
        try
        {
            var store = new JsonSongDirectionPlanStore(cacheDirectory);
            var catalog = new SongDirectionCatalog(store);

            var saved = await catalog.SaveAsync(songId.Value, plan, CancellationToken.None);
            Assert.Equal(0, saved.Version);

            // Assert: persisted domain state survives a restart (brand-new store/catalog instance over the
            // same on-disk directory) -- not only a mock invocation.
            var reloadCatalog = new SongDirectionCatalog(new JsonSongDirectionPlanStore(cacheDirectory));
            var reloaded = await reloadCatalog.GetAsync(songId.Value, CancellationToken.None);
            Assert.NotNull(reloaded);
            Assert.Equal(0, reloaded!.Version);
            Assert.Equal(plan.Id, reloaded.Plan.Id);
            Assert.Equal(plan.CreatedAtUtc, reloaded.Plan.CreatedAtUtc);
            Assert.Equal(7, reloaded.Plan.Sections.Count);

            // Act: render as an inspectable timeline summary -- required acceptance, verbatim.
            ISongDirectionTimelineProjector projector = new SongDirectionTimelineProjector();
            var timeline = projector.Project(reloaded.Plan, labels);

            // Assert: the timeline is ordered by bar position and shows the section intensity (energy) arc,
            // including the required "final chorus is strongest" shape from the phase gate's own demo.
            Assert.Equal(7, timeline.Count);
            Assert.Equal(
                [intro.Id, verse1.Id, chorus1.Id, verse2.Id, chorus2.Id, bridge.Id, finalChorus.Id],
                timeline.Select(e => e.SectionId));
            Assert.Equal(0, timeline[0].StartBarIndex);
            Assert.Equal(48, timeline[6].StartBarIndex);
            Assert.Equal(56, timeline[6].EndBarIndexExclusive);

            var finalChorusEntry = timeline.Single(e => e.SectionId == finalChorus.Id);
            var chorus1Entry = timeline.Single(e => e.SectionId == chorus1.Id);
            Assert.True(finalChorusEntry.TargetEnergy > chorus1Entry.TargetEnergy);
            Assert.Contains(chorusMotif.Id, finalChorusEntry.MotifGroupIds);
            Assert.Equal(MusicalRole.Lead, finalChorusEntry.FocalRole);
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
    public void Step090_NegativePath_UnknownSectionId_Throws_BeforeAnyPlanIsBuilt()
    {
        var songId = new SongId(Guid.NewGuid());
        var (labels, intro, _, _, _, _, _, _) = BuildSectionLabels(songId);
        var motifs = BuildMotifs(songId, out _);
        var unknownSectionId = Guid.NewGuid();

        ISongDirectionPlanner planner = new SongDirectionPlanner();
        var inputs = new List<SongSectionDirectionInput>
        {
            new(intro.Id, 0.1, 0.1, "Calm", MusicalRole.Atmosphere, []),
            new(unknownSectionId, 0.5, 0.5, "Calm", MusicalRole.Lead, []),
        };

        var ex = Assert.Throws<SongDirectionInvalidException>(
            () => planner.Plan(labels, motifs, inputs, baseRevision: 0, DateTimeOffset.UtcNow));
        Assert.Contains(unknownSectionId.ToString(), ex.Reason);
    }

    [Fact]
    public void Step090_NegativePath_UnknownMotifGroupId_Throws()
    {
        var songId = new SongId(Guid.NewGuid());
        var (labels, intro, _, _, _, _, _, _) = BuildSectionLabels(songId);
        var motifs = BuildMotifs(songId, out _);
        var unknownMotifGroupId = Guid.NewGuid();

        ISongDirectionPlanner planner = new SongDirectionPlanner();
        var inputs = new List<SongSectionDirectionInput>
        {
            new(intro.Id, 0.1, 0.1, "Calm", MusicalRole.Atmosphere, [unknownMotifGroupId]),
        };

        Assert.Throws<SongDirectionInvalidException>(
            () => planner.Plan(labels, motifs, inputs, baseRevision: 0, DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData(-0.1, 0.5)]
    [InlineData(1.1, 0.5)]
    [InlineData(double.NaN, 0.5)]
    [InlineData(0.5, -0.1)]
    [InlineData(0.5, 1.1)]
    public void Step090_NegativePath_OutOfRangeEnergyOrContrast_Throws(double energy, double contrast)
    {
        var songId = new SongId(Guid.NewGuid());
        var (labels, intro, _, _, _, _, _, _) = BuildSectionLabels(songId);
        var motifs = BuildMotifs(songId, out _);

        ISongDirectionPlanner planner = new SongDirectionPlanner();
        var inputs = new List<SongSectionDirectionInput>
        {
            new(intro.Id, energy, contrast, "Calm", MusicalRole.Atmosphere, []),
        };

        Assert.Throws<SongDirectionInvalidException>(
            () => planner.Plan(labels, motifs, inputs, baseRevision: 0, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Step090_NegativePath_EmptyInputs_Throws()
    {
        var songId = new SongId(Guid.NewGuid());
        var (labels, _, _, _, _, _, _, _) = BuildSectionLabels(songId);
        var motifs = BuildMotifs(songId, out _);

        ISongDirectionPlanner planner = new SongDirectionPlanner();

        Assert.Throws<SongDirectionInvalidException>(
            () => planner.Plan(labels, motifs, [], baseRevision: 0, DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task Step090_NegativePath_StalePersistedState_RejectsWriteAndLeavesFileUnchanged()
    {
        // "stale persisted state must not corrupt the project" -- required failure/edge case, verbatim.
        var songId = new SongId(Guid.NewGuid());
        var (labels, intro, _, _, _, _, _, _) = BuildSectionLabels(songId);
        var motifs = BuildMotifs(songId, out _);
        ISongDirectionPlanner planner = new SongDirectionPlanner();
        var plan = planner.Plan(
            labels, motifs,
            [new SongSectionDirectionInput(intro.Id, 0.1, 0.1, "Calm", MusicalRole.Atmosphere, [])],
            baseRevision: 0, DateTimeOffset.UtcNow);

        var cacheDirectory = NewCacheDirectory("stale");
        try
        {
            var store = new JsonSongDirectionPlanStore(cacheDirectory);

            // v0: first save, no prior record expected.
            var record0 = new SongDirectionPlanRecord(songId, plan, 0);
            await store.SaveAsync(record0, expectedCurrentVersion: null, CancellationToken.None);

            // v1: a second, independent save correctly advances the version (simulates another session's edit).
            var replanned = planner.Plan(
                labels, motifs,
                [new SongSectionDirectionInput(intro.Id, 0.2, 0.2, "Calm", MusicalRole.Atmosphere, [])],
                baseRevision: 1, DateTimeOffset.UtcNow);
            var record1 = new SongDirectionPlanRecord(songId, replanned, 1);
            await store.SaveAsync(record1, expectedCurrentVersion: 0, CancellationToken.None);

            // Act/Assert: a caller still holding the stale v0 expectation must be rejected, not silently
            // overwrite the now-current v1 record.
            var staleWrite = new SongDirectionPlanRecord(songId, plan, 1);
            var ex = await Assert.ThrowsAsync<SongDirectionStaleException>(
                () => store.SaveAsync(staleWrite, expectedCurrentVersion: 0, CancellationToken.None));
            Assert.Equal(0, ex.ExpectedCurrentVersion);
            Assert.Equal(1, ex.ActualCurrentVersion);

            // The project's persisted state is left exactly as it was (still the v1 record, not corrupted).
            var current = await store.LoadAsync(store.GetPath(songId.Value), CancellationToken.None);
            Assert.Equal(1, current.Version);
            Assert.Equal(replanned.Id, current.Plan.Id);
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
    public async Task Step090_NegativePath_Cancellation_ThrowsWithoutPersisting()
    {
        var songId = new SongId(Guid.NewGuid());
        var (labels, intro, _, _, _, _, _, _) = BuildSectionLabels(songId);
        var motifs = BuildMotifs(songId, out _);
        ISongDirectionPlanner planner = new SongDirectionPlanner();
        var plan = planner.Plan(
            labels, motifs,
            [new SongSectionDirectionInput(intro.Id, 0.1, 0.1, "Calm", MusicalRole.Atmosphere, [])],
            baseRevision: 0, DateTimeOffset.UtcNow);

        var cacheDirectory = NewCacheDirectory("cancel");
        try
        {
            var store = new JsonSongDirectionPlanStore(cacheDirectory);
            var catalog = new SongDirectionCatalog(store);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => catalog.SaveAsync(songId.Value, plan, cts.Token));

            Assert.False(File.Exists(store.GetPath(songId.Value)));
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
    public async Task Step090_NegativePath_InvalidPlan_RejectsBeforeAnyWrite_ViaCatalog()
    {
        var songId = new SongId(Guid.NewGuid());
        var invalidPlan = new SongDirectionPlan(
            Guid.NewGuid(), songId, Guid.NewGuid(), Guid.NewGuid(), 0, DateTimeOffset.UtcNow, []);

        var cacheDirectory = NewCacheDirectory("invalid");
        try
        {
            var store = new JsonSongDirectionPlanStore(cacheDirectory);
            var catalog = new SongDirectionCatalog(store);

            await Assert.ThrowsAsync<SongDirectionInvalidException>(
                () => catalog.SaveAsync(songId.Value, invalidPlan, CancellationToken.None));

            Assert.False(File.Exists(store.GetPath(songId.Value)));
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
    public async Task Step090_NegativePath_CorruptFile_ThrowsCorruptException()
    {
        var cacheDirectory = NewCacheDirectory("corrupt");
        Directory.CreateDirectory(cacheDirectory);
        var path = Path.Combine(cacheDirectory, "corrupt.json");
        await File.WriteAllTextAsync(path, "{ not valid json");

        try
        {
            var store = new JsonSongDirectionPlanStore(cacheDirectory);
            await Assert.ThrowsAsync<SongDirectionCorruptException>(() => store.LoadAsync(path, CancellationToken.None));
        }
        finally
        {
            Directory.Delete(cacheDirectory, recursive: true);
        }
    }

    [Fact]
    public void Step090_NegativePath_TimelineProjector_MismatchedSectionLabelVersion_Throws()
    {
        var songId = new SongId(Guid.NewGuid());
        var (labels, intro, _, _, _, _, _, _) = BuildSectionLabels(songId);
        var motifs = BuildMotifs(songId, out _);
        ISongDirectionPlanner planner = new SongDirectionPlanner();
        var plan = planner.Plan(
            labels, motifs,
            [new SongSectionDirectionInput(intro.Id, 0.1, 0.1, "Calm", MusicalRole.Atmosphere, [])],
            baseRevision: 0, DateTimeOffset.UtcNow);

        // A re-analyzed (stale) SectionLabelAnalysis for the same song gets a fresh VersionId even if the
        // segment contents are otherwise identical -- the projector must not silently treat it as current.
        var (staleLabels, _, _, _, _, _, _, _) = BuildSectionLabels(songId);

        ISongDirectionTimelineProjector projector = new SongDirectionTimelineProjector();
        Assert.Throws<SongDirectionInvalidException>(() => projector.Project(plan, staleLabels));
    }
}
