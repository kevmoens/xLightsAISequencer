using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Composition.Tests;

/// <summary>
/// PHASE-10 Step 070 -- "Implement build, silence, transitions, release and phrase-ending recipes." Required
/// acceptance: "Musical break can intentionally darken all permitted props." Implementation guidance: "Add
/// transition gestures, builds, blackouts and resolutions as reusable semantics with deterministic recipe
/// strategies." Failure/edge case: "Clean machine and unsupported runtime must produce user-readable
/// diagnostics." These tests exercise the real <see cref="TransitionRecipePlanner"/>/<see cref="TransitionPlanCatalog"/>/
/// <see cref="JsonTransitionPlanStore"/> (not mocks).
/// </summary>
public sealed class Step070AcceptanceTests
{
    private static string NewCacheDirectory(string suffix) =>
        Path.Combine(Path.GetTempPath(), $"xlai-step070-{suffix}-{Guid.NewGuid():N}");

    private static TimeWindow Window(long startSeconds, long endSeconds) =>
        new(startSeconds * 10_000_000L, endSeconds * 10_000_000L);

    private static IReadOnlyList<ModelId> ArchesAndRoofline() => [new ModelId("Arches"), new ModelId("Roofline"), new ModelId("Tree")];

    [Fact]
    public void Step070_Acceptance_MusicalBreakCanIntentionallyDarkenAllPermittedProps()
    {
        // Required acceptance, verbatim: "Musical break can intentionally darken all permitted props."
        var permittedProps = ArchesAndRoofline();
        var planner = new TransitionRecipePlanner();

        var plan = planner.Plan([new TransitionRecipeRequest(TransitionKind.Blackout, Window(10, 12), permittedProps)]);

        var gesture = Assert.Single(plan.Gestures);
        Assert.Equal(TransitionKind.Blackout, gesture.Kind);
        Assert.Equal(permittedProps.Count, gesture.TargetProps.Count);
        Assert.All(permittedProps, p => Assert.Contains(p, gesture.TargetProps));
        Assert.Equal(0.0, gesture.StartIntensity);
        Assert.Equal(0.0, gesture.EndIntensity);
    }

    [Fact]
    public void Plan_EachKindProducesItsOwnDeterministicRecipe()
    {
        // "Add transition gestures, builds, blackouts and resolutions as reusable semantics with deterministic
        // recipe strategies" -- every kind maps to its own fixed, repeatable start/end intensity recipe.
        var props = ArchesAndRoofline();
        var planner = new TransitionRecipePlanner();

        var plan = planner.Plan([
            new TransitionRecipeRequest(TransitionKind.Build, Window(0, 4), props),
            new TransitionRecipeRequest(TransitionKind.Blackout, Window(4, 6), props),
            new TransitionRecipeRequest(TransitionKind.Transition, Window(6, 8), props),
            new TransitionRecipeRequest(TransitionKind.Release, Window(8, 12), props),
            new TransitionRecipeRequest(TransitionKind.PhraseEnding, Window(12, 16), props),
        ]);

        var byKind = plan.Gestures.ToDictionary(g => g.Kind);

        // Build rises.
        Assert.True(byKind[TransitionKind.Build].EndIntensity > byKind[TransitionKind.Build].StartIntensity);
        // Blackout darkens fully, both ends.
        Assert.Equal(0.0, byKind[TransitionKind.Blackout].StartIntensity);
        Assert.Equal(0.0, byKind[TransitionKind.Blackout].EndIntensity);
        // Release falls.
        Assert.True(byKind[TransitionKind.Release].EndIntensity < byKind[TransitionKind.Release].StartIntensity);
        // Phrase-ending tapers down but never reaches literal darkness -- distinct from Blackout.
        Assert.True(byKind[TransitionKind.PhraseEnding].EndIntensity > 0.0);
        Assert.True(byKind[TransitionKind.PhraseEnding].EndIntensity < byKind[TransitionKind.PhraseEnding].StartIntensity);
        // No non-blackout recipe reaches literal maximum brightness.
        Assert.All(plan.Gestures.Where(g => g.Kind != TransitionKind.Blackout),
            g => Assert.True(g.StartIntensity < 1.0 && g.EndIntensity < 1.0));

        // Re-planning the exact same requests produces the exact same recipe values (deterministic strategy).
        var replanned = planner.Plan([new TransitionRecipeRequest(TransitionKind.Build, Window(0, 4), props)]);
        Assert.Equal(byKind[TransitionKind.Build].StartIntensity, replanned.Gestures[0].StartIntensity);
        Assert.Equal(byKind[TransitionKind.Build].EndIntensity, replanned.Gestures[0].EndIntensity);
    }

    [Fact]
    public void Plan_EmptyRequests_ThrowsBeforeAnyComputation_NegativePath()
    {
        var planner = new TransitionRecipePlanner();
        Assert.Throws<CompositionInvalidException>(() => planner.Plan([]));
    }

    [Fact]
    public void Plan_UndefinedTransitionKind_Throws_NegativePath()
    {
        // "Unsupported runtime" diagnostic: an undefined/future TransitionKind value must be rejected with a
        // clear, actionable message, never silently planned or thrown as an unhandled KeyNotFoundException.
        var planner = new TransitionRecipePlanner();
        var request = new TransitionRecipeRequest((TransitionKind)999, Window(0, 4), ArchesAndRoofline());

        var ex = Assert.Throws<CompositionInvalidException>(() => planner.Plan([request]));
        Assert.Contains("Unsupported transition kind", ex.Message);
    }

    [Fact]
    public void Plan_EmptyPermittedProps_Throws_NegativePath()
    {
        // "Clean machine" diagnostic: a clean machine with no layout imported has zero permitted props -- this
        // must be reported clearly, never planned as a silent no-op gesture with no targets.
        var planner = new TransitionRecipePlanner();
        var request = new TransitionRecipeRequest(TransitionKind.Build, Window(0, 4), []);

        var ex = Assert.Throws<CompositionInvalidException>(() => planner.Plan([request]));
        Assert.Contains("permitted prop", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Plan_DuplicatePermittedProp_Throws_NegativePath()
    {
        var planner = new TransitionRecipePlanner();
        var duplicateProps = new List<ModelId> { new("Arches"), new("Arches") };
        var request = new TransitionRecipeRequest(TransitionKind.Build, Window(0, 4), duplicateProps);

        Assert.Throws<CompositionInvalidException>(() => planner.Plan([request]));
    }

    [Fact]
    public void Plan_InvalidWindow_Throws_NegativePath()
    {
        var planner = new TransitionRecipePlanner();
        var request = new TransitionRecipeRequest(TransitionKind.Build, Window(5, 5), ArchesAndRoofline());

        Assert.Throws<CompositionInvalidException>(() => planner.Plan([request]));
    }

    [Fact]
    public void TransitionGesture_Validate_RejectsBlackoutWithNonZeroIntensity_NegativePath()
    {
        // Defends the domain invariant even against direct record construction, not only planner output.
        var gesture = new TransitionGesture(Guid.NewGuid(), TransitionKind.Blackout, Window(0, 4), ArchesAndRoofline(), 0.1, 0.0);
        Assert.Throws<CompositionInvalidException>(gesture.Validate);
    }

    [Fact]
    public void TransitionGesture_Validate_RejectsEmptyTargetProps_NegativePath()
    {
        var gesture = new TransitionGesture(Guid.NewGuid(), TransitionKind.Build, Window(0, 4), [], 0.1, 0.9);
        Assert.Throws<CompositionInvalidException>(gesture.Validate);
    }

    [Theory]
    [InlineData(-0.1, 0.5)]
    [InlineData(0.5, 1.1)]
    [InlineData(double.NaN, 0.5)]
    public void TransitionGesture_Validate_RejectsOutOfRangeIntensity_NegativePath(double start, double end)
    {
        var gesture = new TransitionGesture(Guid.NewGuid(), TransitionKind.Build, Window(0, 4), ArchesAndRoofline(), start, end);
        Assert.Throws<CompositionInvalidException>(gesture.Validate);
    }

    [Fact]
    public void TransitionPlan_Validate_RejectsEmptyGestures_NegativePath()
    {
        var plan = new TransitionPlan(Guid.NewGuid(), []);
        Assert.Throws<CompositionInvalidException>(plan.Validate);
    }

    [Fact]
    public void TransitionPlan_Validate_RejectsDuplicateGestureId_NegativePath()
    {
        var id = Guid.NewGuid();
        var plan = new TransitionPlan(Guid.NewGuid(), [
            new TransitionGesture(id, TransitionKind.Build, Window(0, 4), ArchesAndRoofline(), 0.1, 0.9),
            new TransitionGesture(id, TransitionKind.Release, Window(4, 8), ArchesAndRoofline(), 0.9, 0.1),
        ]);

        Assert.Throws<CompositionInvalidException>(plan.Validate);
    }

    [Fact]
    public async Task TransitionPlanCatalog_SaveAndReload_PersistsAcrossRestart()
    {
        var cacheDirectory = NewCacheDirectory("plan-persist");
        try
        {
            var phraseId = Guid.NewGuid();
            var catalog = new TransitionPlanCatalog(new JsonTransitionPlanStore(cacheDirectory));
            var planner = new TransitionRecipePlanner();
            var plan = planner.Plan([new TransitionRecipeRequest(TransitionKind.Blackout, Window(10, 12), ArchesAndRoofline())]);

            var saved = await catalog.SaveAsync(phraseId, plan, CancellationToken.None);
            Assert.Equal(0, saved.Version);

            var replanned = planner.Plan([new TransitionRecipeRequest(TransitionKind.Build, Window(0, 4), ArchesAndRoofline())]);
            var resaved = await catalog.SaveAsync(phraseId, replanned, CancellationToken.None);
            Assert.Equal(1, resaved.Version);

            // Restart: a brand-new catalog/store instance over the same on-disk directory.
            var reloadCatalog = new TransitionPlanCatalog(new JsonTransitionPlanStore(cacheDirectory));
            var reloaded = await reloadCatalog.GetAsync(phraseId, CancellationToken.None);
            Assert.NotNull(reloaded);
            Assert.Equal(1, reloaded!.Version);
            Assert.Equal(TransitionKind.Build, reloaded.Plan.Gestures.Single().Kind);

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
    public async Task TransitionPlanCatalog_InvalidPlan_RejectsBeforeAnyWrite_NegativePath()
    {
        var cacheDirectory = NewCacheDirectory("plan-invalid");
        try
        {
            var phraseId = Guid.NewGuid();
            var store = new JsonTransitionPlanStore(cacheDirectory);
            var catalog = new TransitionPlanCatalog(store);
            var invalidPlan = new TransitionPlan(Guid.NewGuid(), []);

            await Assert.ThrowsAsync<CompositionInvalidException>(() =>
                catalog.SaveAsync(phraseId, invalidPlan, CancellationToken.None));

            Assert.False(File.Exists(store.GetPath(phraseId)));
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
    public async Task TransitionPlanCatalog_Cancelled_DoesNotPersist_NegativePath()
    {
        var cacheDirectory = NewCacheDirectory("plan-cancel");
        try
        {
            var phraseId = Guid.NewGuid();
            var store = new JsonTransitionPlanStore(cacheDirectory);
            var catalog = new TransitionPlanCatalog(store);
            var planner = new TransitionRecipePlanner();
            var plan = planner.Plan([new TransitionRecipeRequest(TransitionKind.Build, Window(0, 4), ArchesAndRoofline())]);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => catalog.SaveAsync(phraseId, plan, cts.Token));

            Assert.False(File.Exists(store.GetPath(phraseId)));
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
    public async Task TransitionPlanStore_LoadAsync_CorruptFile_ThrowsCorruptException_NegativePath()
    {
        var cacheDirectory = NewCacheDirectory("plan-corrupt");
        Directory.CreateDirectory(cacheDirectory);
        var path = Path.Combine(cacheDirectory, "corrupt.json");
        await File.WriteAllTextAsync(path, "{ not valid json");

        try
        {
            var store = new JsonTransitionPlanStore(cacheDirectory);
            await Assert.ThrowsAsync<CompositionCorruptException>(() => store.LoadAsync(path, CancellationToken.None));
        }
        finally
        {
            Directory.Delete(cacheDirectory, recursive: true);
        }
    }
}
