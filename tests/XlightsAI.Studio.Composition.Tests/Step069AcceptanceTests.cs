using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Composition.Tests;

/// <summary>
/// PHASE-10 Step 069 -- "Build sectional energy and color/brightness story." Required acceptance:
/// "Verse/chorus intensity curve follows plan." Implementation guidance: "Plan section intensity normalized
/// across song; preserve energy hierarchy and avoid maximum brightness continuously." Failure/edge case:
/// "Invalid input, cancellation or stale persisted state must not corrupt the project." These tests exercise
/// the real <see cref="SectionVisualStoryPlanner"/>/<see cref="VisualStoryCatalog"/>/<see cref="JsonVisualStoryStore"/>
/// (not mocks).
/// </summary>
public sealed class Step069AcceptanceTests
{
    private static string NewCacheDirectory(string suffix) =>
        Path.Combine(Path.GetTempPath(), $"xlai-step069-{suffix}-{Guid.NewGuid():N}");

    [Fact]
    public void Step069_Acceptance_VerseChorusIntensityCurveFollowsPlan()
    {
        // "Verse/chorus intensity curve follows plan" -- required acceptance, verbatim. A typical pop-song
        // excerpt: Intro (quiet) -> Verse (medium) -> Chorus (loud) -> Verse (medium) -> Chorus (louder) ->
        // Outro (quiet).
        var intro = new SectionEnergyInput(Guid.NewGuid(), SectionKind.Intro, RawEnergy: 0.1);
        var verse1 = new SectionEnergyInput(Guid.NewGuid(), SectionKind.Verse, RawEnergy: 0.4);
        var chorus1 = new SectionEnergyInput(Guid.NewGuid(), SectionKind.Chorus, RawEnergy: 0.8);
        var verse2 = new SectionEnergyInput(Guid.NewGuid(), SectionKind.Verse, RawEnergy: 0.45);
        var chorus2 = new SectionEnergyInput(Guid.NewGuid(), SectionKind.Chorus, RawEnergy: 0.95);
        var outro = new SectionEnergyInput(Guid.NewGuid(), SectionKind.Outro, RawEnergy: 0.05);

        var sections = new List<SectionEnergyInput> { intro, verse1, chorus1, verse2, chorus2, outro };
        var planner = new SectionVisualStoryPlanner();

        var story = planner.Plan(sections);

        var byId = story.Sections.ToDictionary(s => s.SectionId);
        var verse1Direction = byId[verse1.SectionId];
        var chorus1Direction = byId[chorus1.SectionId];
        var verse2Direction = byId[verse2.SectionId];
        var chorus2Direction = byId[chorus2.SectionId];
        var introDirection = byId[intro.SectionId];
        var outroDirection = byId[outro.SectionId];

        // The verse/chorus intensity curve follows the plan: every chorus is strictly louder than both
        // verses, which are in turn strictly louder than the intro/outro.
        Assert.True(chorus1Direction.Intensity > verse1Direction.Intensity);
        Assert.True(chorus1Direction.Intensity > verse2Direction.Intensity);
        Assert.True(chorus2Direction.Intensity > verse1Direction.Intensity);
        Assert.True(chorus2Direction.Intensity > verse2Direction.Intensity);
        Assert.True(verse1Direction.Intensity > introDirection.Intensity);
        Assert.True(verse1Direction.Intensity > outroDirection.Intensity);

        // "preserve energy hierarchy" -- the louder chorus (0.95) is planned strictly more intense than the
        // quieter chorus (0.8); the louder verse (0.45) strictly more intense than the quieter verse (0.4).
        Assert.True(chorus2Direction.Intensity > chorus1Direction.Intensity);
        Assert.True(verse2Direction.Intensity > verse1Direction.Intensity);

        // "avoid maximum brightness continuously" -- even the single loudest section in the whole song is
        // never planned at literal maximum brightness.
        Assert.True(story.Sections.All(s => s.Intensity < 1.0));
        Assert.True(story.Sections.Max(s => s.Intensity) < 1.0);

        // Deterministic palette assignment, derived from each section's own already-classified kind.
        Assert.Equal("Calm", verse1Direction.PaletteId);
        Assert.Equal("Vivid", chorus1Direction.PaletteId);
    }

    [Fact]
    public void Plan_SameInputsInAnyOrder_ProducesTheSamePerSectionIntensities()
    {
        // "Same inputs+seed produce repeatable plan" -- the full-phase test matrix's own regression concern.
        var sections = new List<SectionEnergyInput>
        {
            new(Guid.NewGuid(), SectionKind.Intro, 0.1),
            new(Guid.NewGuid(), SectionKind.Verse, 0.4),
            new(Guid.NewGuid(), SectionKind.Chorus, 0.9),
            new(Guid.NewGuid(), SectionKind.Bridge, 0.6),
        };
        var planner = new SectionVisualStoryPlanner();

        var storyA = planner.Plan(sections);
        var shuffled = ((IReadOnlyList<SectionEnergyInput>)sections).Reverse().ToList();
        var storyB = planner.Plan(shuffled);

        var intensitiesA = storyA.Sections.ToDictionary(s => s.SectionId, s => s.Intensity);
        var intensitiesB = storyB.Sections.ToDictionary(s => s.SectionId, s => s.Intensity);
        Assert.Equal(intensitiesA, intensitiesB);

        // VersionId is intentionally fresh every plan -- never compared for equality across two independent calls.
        Assert.NotEqual(storyA.VersionId, storyB.VersionId);
    }

    [Fact]
    public void Plan_AllSectionsHaveEqualEnergy_AssignsAModerateNonMaxNonZeroIntensity()
    {
        // No energy hierarchy exists to preserve when every input is flat -- the planner must not collapse
        // this to either silence (0) or maximum brightness (1), both of which would misrepresent a flat
        // input as a deliberate artistic choice this step did not make.
        var sections = new List<SectionEnergyInput>
        {
            new(Guid.NewGuid(), SectionKind.Verse, 0.5),
            new(Guid.NewGuid(), SectionKind.Verse, 0.5),
            new(Guid.NewGuid(), SectionKind.Verse, 0.5),
        };
        var planner = new SectionVisualStoryPlanner();

        var story = planner.Plan(sections);

        Assert.All(story.Sections, s => Assert.True(s.Intensity is > 0.0 and < 1.0));
        Assert.Single(story.Sections.Select(s => s.Intensity).Distinct());
    }

    [Fact]
    public void Plan_EmptyInput_ThrowsBeforeAnyComputation_NegativePath()
    {
        var planner = new SectionVisualStoryPlanner();
        Assert.Throws<CompositionInvalidException>(() => planner.Plan([]));
    }

    [Fact]
    public void Plan_DuplicateSectionId_Throws_NegativePath()
    {
        var planner = new SectionVisualStoryPlanner();
        var id = Guid.NewGuid();
        var sections = new List<SectionEnergyInput>
        {
            new(id, SectionKind.Verse, 0.4),
            new(id, SectionKind.Chorus, 0.8),
        };

        Assert.Throws<CompositionInvalidException>(() => planner.Plan(sections));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-0.1)]
    public void Plan_InvalidRawEnergy_Throws_NegativePath(double badEnergy)
    {
        var planner = new SectionVisualStoryPlanner();
        var sections = new List<SectionEnergyInput> { new(Guid.NewGuid(), SectionKind.Verse, badEnergy) };

        Assert.Throws<CompositionInvalidException>(() => planner.Plan(sections));
    }

    [Fact]
    public void VisualStory_Validate_RejectsEmptySections_NegativePath()
    {
        var story = new VisualStory(Guid.NewGuid(), []);
        Assert.Throws<CompositionInvalidException>(story.Validate);
    }

    [Fact]
    public void VisualStory_Validate_RejectsDuplicateSectionId_NegativePath()
    {
        var sectionId = Guid.NewGuid();
        var story = new VisualStory(Guid.NewGuid(), [
            new SectionVisualDirection(sectionId, SectionKind.Verse, "Calm", 0.4),
            new SectionVisualDirection(sectionId, SectionKind.Chorus, "Vivid", 0.8),
        ]);

        Assert.Throws<CompositionInvalidException>(story.Validate);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    public void VisualStory_Validate_RejectsOutOfRangeIntensity_NegativePath(double badIntensity)
    {
        var story = new VisualStory(Guid.NewGuid(), [
            new SectionVisualDirection(Guid.NewGuid(), SectionKind.Verse, "Calm", badIntensity),
        ]);

        Assert.Throws<CompositionInvalidException>(story.Validate);
    }

    [Fact]
    public void VisualStory_Validate_RejectsBlankPaletteId_NegativePath()
    {
        var story = new VisualStory(Guid.NewGuid(), [
            new SectionVisualDirection(Guid.NewGuid(), SectionKind.Verse, "   ", 0.4),
        ]);

        Assert.Throws<CompositionInvalidException>(story.Validate);
    }

    [Fact]
    public async Task VisualStoryCatalog_SaveAndReload_PersistsAcrossRestart()
    {
        var cacheDirectory = NewCacheDirectory("story-persist");
        try
        {
            var songId = Guid.NewGuid();
            var catalog = new VisualStoryCatalog(new JsonVisualStoryStore(cacheDirectory));
            var planner = new SectionVisualStoryPlanner();
            var story = planner.Plan([
                new SectionEnergyInput(Guid.NewGuid(), SectionKind.Verse, 0.4),
                new SectionEnergyInput(Guid.NewGuid(), SectionKind.Chorus, 0.9),
            ]);

            var saved = await catalog.SaveAsync(songId, story, CancellationToken.None);
            Assert.Equal(0, saved.Version);

            var replanned = planner.Plan([
                new SectionEnergyInput(Guid.NewGuid(), SectionKind.Verse, 0.5),
                new SectionEnergyInput(Guid.NewGuid(), SectionKind.Chorus, 0.95),
            ]);
            var resaved = await catalog.SaveAsync(songId, replanned, CancellationToken.None);
            Assert.Equal(1, resaved.Version);

            // Restart: a brand-new catalog/store instance over the same on-disk directory.
            var reloadCatalog = new VisualStoryCatalog(new JsonVisualStoryStore(cacheDirectory));
            var reloaded = await reloadCatalog.GetAsync(songId, CancellationToken.None);
            Assert.NotNull(reloaded);
            Assert.Equal(1, reloaded!.Version);
            Assert.Equal(replanned.Sections.Count, reloaded.Story.Sections.Count);

            var missing = await reloadCatalog.GetAsync(Guid.NewGuid(), CancellationToken.None);
            Assert.Null(missing);
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
    public async Task VisualStoryCatalog_InvalidStory_RejectsBeforeAnyWrite_NegativePath()
    {
        var cacheDirectory = NewCacheDirectory("story-invalid");
        try
        {
            var songId = Guid.NewGuid();
            var store = new JsonVisualStoryStore(cacheDirectory);
            var catalog = new VisualStoryCatalog(store);
            var invalidStory = new VisualStory(Guid.NewGuid(), []);

            await Assert.ThrowsAsync<CompositionInvalidException>(() =>
                catalog.SaveAsync(songId, invalidStory, CancellationToken.None));

            Assert.False(File.Exists(store.GetPath(songId)));
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
    public async Task VisualStoryCatalog_Cancelled_DoesNotPersist_NegativePath()
    {
        var cacheDirectory = NewCacheDirectory("story-cancel");
        try
        {
            var songId = Guid.NewGuid();
            var store = new JsonVisualStoryStore(cacheDirectory);
            var catalog = new VisualStoryCatalog(store);
            var planner = new SectionVisualStoryPlanner();
            var story = planner.Plan([new SectionEnergyInput(Guid.NewGuid(), SectionKind.Verse, 0.4)]);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => catalog.SaveAsync(songId, story, cts.Token));

            Assert.False(File.Exists(store.GetPath(songId)));
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
    public async Task VisualStoryStore_LoadAsync_CorruptFile_ThrowsCorruptException_NegativePath()
    {
        var cacheDirectory = NewCacheDirectory("story-corrupt");
        Directory.CreateDirectory(cacheDirectory);
        var path = Path.Combine(cacheDirectory, "corrupt.json");
        await File.WriteAllTextAsync(path, "{ not valid json");

        try
        {
            var store = new JsonVisualStoryStore(cacheDirectory);
            await Assert.ThrowsAsync<CompositionCorruptException>(() => store.LoadAsync(path, CancellationToken.None));
        }
        finally
        {
            Directory.Delete(cacheDirectory, recursive: true);
        }
    }
}
