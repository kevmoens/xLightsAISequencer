using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Composition.Tests;

/// <summary>
/// PHASE-10 Step 068 -- "Create configurable visual density and focus budget." Required acceptance: "Clutter
/// fixture is reduced while preserving focal musical event." Implementation guidance: "Build measurable
/// visibility budget (concurrent focal count, event density, brightness proxy, rest fraction); define
/// emergency over-budget resolution." Failure/edge case: "Invalid input, cancellation or stale persisted
/// state must not corrupt the project." These tests exercise the real <see cref="VisualBudgetResolver"/>/
/// <see cref="RolePrecedenceResolver"/>/<see cref="VisualBudgetPresetCatalog"/>/<see cref="JsonVisualBudgetPresetStore"/>
/// (not mocks).
/// </summary>
public sealed class Step068AcceptanceTests
{
    private const long TicksPerSecond = 10_000_000;

    private static string NewCacheDirectory(string suffix) =>
        Path.Combine(Path.GetTempPath(), $"xlai-step068-{suffix}-{Guid.NewGuid():N}");

    private static MusicalRoleAssignment Assignment(
        Guid phraseId, string trackId, MusicalRole role, string tier, int priority, double weight) =>
        new(phraseId, trackId, role, Guid.NewGuid(), tier, priority, weight, Version: 0);

    [Fact]
    public void Step068_Acceptance_ClutterFixtureIsReducedWhilePreservingFocalEvent()
    {
        // "Clutter fixture is reduced while preserving focal musical event" -- required acceptance, verbatim.
        var phraseId = Guid.NewGuid();
        var evaluationWindow = new TimeWindow(0, 10 * TicksPerSecond); // a 10-second phrase excerpt.

        // The one focal musical event this budget must preserve: the lead piano, spanning the whole excerpt.
        var leadPiano = new VisualBudgetCandidate(
            Assignment(phraseId, "T-Piano", MusicalRole.Lead, PrecedenceTier.Manual, priority: 5, weight: 0.9),
            evaluationWindow, Brightness: 0.6, IsFocal: true);

        // A second, lower-precedence focal competitor fully overlapping the lead -- must not survive the
        // MaxConcurrentFocalProps=1 cap at all (no non-saturated sub-interval exists to trim it into).
        var competingFocalSnare = new VisualBudgetCandidate(
            Assignment(phraseId, "T-Snare", MusicalRole.Accent, PrecedenceTier.Automated, priority: 1, weight: 0.3),
            evaluationWindow, Brightness: 0.5, IsFocal: true);

        // Ten low-priority, non-focal "clutter" candidates spread across the excerpt -- more than the event
        // density cap allows.
        var clutter = new List<VisualBudgetCandidate>();
        for (var i = 0; i < 10; i++)
        {
            var start = i * TicksPerSecond;
            clutter.Add(new VisualBudgetCandidate(
                Assignment(phraseId, $"T-Clutter{i}", MusicalRole.Atmosphere, PrecedenceTier.Automated, priority: i, weight: 0.2),
                new TimeWindow(start, start + (TicksPerSecond / 10)), Brightness: 0.2, IsFocal: false));
        }

        var candidates = new List<VisualBudgetCandidate> { leadPiano, competingFocalSnare };
        candidates.AddRange(clutter);

        var budget = new VisualBudget(
            MaxConcurrentFocalProps: 1,
            MaxMeanBrightness: 1.0,
            MaxEventDensityPerSecond: 0.5, // at most ~5 admitted events across this 10-second excerpt.
            DarknessFractionTarget: 0.3);

        var resolver = new VisualBudgetResolver(new RolePrecedenceResolver());

        // Fed in a different order each time -- resolution must not depend on caller input order, only on
        // IRolePrecedenceResolver's own deterministic precedence rule.
        var resultA = resolver.Resolve(candidates, budget, evaluationWindow);
        var shuffled = ((IReadOnlyList<VisualBudgetCandidate>)candidates).Reverse().ToList();
        var resultB = resolver.Resolve(shuffled, budget, evaluationWindow);

        var leadResultA = resultA.Results.Single(r => r.Candidate == leadPiano);
        Assert.Equal(VisualBudgetDecision.Accepted, leadResultA.Decision);
        Assert.Equal(evaluationWindow, leadResultA.AdmittedWindow);

        var snareResultA = resultA.Results.Single(r => r.Candidate == competingFocalSnare);
        Assert.Equal(VisualBudgetDecision.Dropped, snareResultA.Decision);
        Assert.Contains("concurrent focal", snareResultA.Reason, StringComparison.OrdinalIgnoreCase);

        // The clutter fixture is reduced: out of 10 low-priority candidates, only the budget's own remaining
        // density headroom survives (1 lead already consumes 1/10 = 0.1 of the 0.5/sec cap; 4 more clutter
        // events exactly reach 0.5/sec; a 6th would exceed it).
        var admittedClutter = clutter.Count(c => resultA.Results.Single(r => r.Candidate == c).Decision != VisualBudgetDecision.Dropped);
        Assert.Equal(4, admittedClutter);
        Assert.True(admittedClutter < clutter.Count, "Clutter fixture must be reduced, not fully admitted.");

        // Deterministic regardless of input order -- compare by track id since each resolution's own
        // `Results` order mirrors that call's own input order, not a shared canonical order.
        var decisionsA = resultA.Results.ToDictionary(r => r.Candidate.Assignment.SourceTrackId, r => r.Decision);
        var decisionsB = resultB.Results.ToDictionary(r => r.Candidate.Assignment.SourceTrackId, r => r.Decision);
        Assert.Equal(decisionsA, decisionsB);
        Assert.Equal(resultA.ObservedEventDensityPerSecond, resultB.ObservedEventDensityPerSecond);
        Assert.Equal(resultA.ObservedMeanBrightness, resultB.ObservedMeanBrightness);
    }

    [Fact]
    public void Resolve_PartiallyOverlappingLowerPrecedenceFocal_IsTrimmedNotDropped()
    {
        // The flowchart's own named emergency resolution, verbatim: "Drop/shorten lower priority support" --
        // this proves the "shorten" half specifically (a fully-saturating competitor is Dropped; this partial
        // overlap instead survives, trimmed into its own non-saturated remainder).
        var phraseId = Guid.NewGuid();
        var window = new TimeWindow(0, 10 * TicksPerSecond);

        var lead = new VisualBudgetCandidate(
            Assignment(phraseId, "T-Piano", MusicalRole.Lead, PrecedenceTier.Manual, priority: 5, weight: 0.9),
            new TimeWindow(0, 5 * TicksPerSecond), Brightness: 0.5, IsFocal: true);

        var partialOverlap = new VisualBudgetCandidate(
            Assignment(phraseId, "T-Horn", MusicalRole.Accent, PrecedenceTier.Automated, priority: 1, weight: 0.3),
            new TimeWindow(2 * TicksPerSecond + TicksPerSecond / 2, 7 * TicksPerSecond + TicksPerSecond / 2), Brightness: 0.3, IsFocal: true);

        var budget = new VisualBudget(MaxConcurrentFocalProps: 1, MaxMeanBrightness: 1.0, MaxEventDensityPerSecond: 10, DarknessFractionTarget: 0.0);
        var resolver = new VisualBudgetResolver(new RolePrecedenceResolver());

        var result = resolver.Resolve([lead, partialOverlap], budget, window);

        var leadResult = result.Results.Single(r => r.Candidate == lead);
        Assert.Equal(VisualBudgetDecision.Accepted, leadResult.Decision);

        var trimmedResult = result.Results.Single(r => r.Candidate == partialOverlap);
        Assert.Equal(VisualBudgetDecision.Trimmed, trimmedResult.Decision);
        Assert.Equal(new TimeWindow(5 * TicksPerSecond, 7 * TicksPerSecond + TicksPerSecond / 2), trimmedResult.AdmittedWindow);
        Assert.NotNull(trimmedResult.Reason);
    }

    [Fact]
    public void Resolve_InvalidBudget_ThrowsBeforeAnyComputation_NegativePath()
    {
        var resolver = new VisualBudgetResolver(new RolePrecedenceResolver());
        var window = new TimeWindow(0, TicksPerSecond);
        var invalidBudget = new VisualBudget(MaxConcurrentFocalProps: -1, MaxMeanBrightness: 0.5, MaxEventDensityPerSecond: 1, DarknessFractionTarget: 0.1);

        Assert.Throws<CompositionInvalidException>(() => resolver.Resolve([], invalidBudget, window));
    }

    [Fact]
    public void Resolve_InvalidEvaluationWindow_Throws_NegativePath()
    {
        var resolver = new VisualBudgetResolver(new RolePrecedenceResolver());
        var budget = new VisualBudget(1, 1.0, 1.0, 0.0);

        Assert.Throws<CompositionInvalidException>(() => resolver.Resolve([], budget, new TimeWindow(100, 100)));
    }

    [Fact]
    public void Resolve_CandidateWithOutOfRangeBrightness_Throws_NegativePath()
    {
        var resolver = new VisualBudgetResolver(new RolePrecedenceResolver());
        var budget = new VisualBudget(1, 1.0, 10.0, 0.0);
        var window = new TimeWindow(0, TicksPerSecond);
        var bad = new VisualBudgetCandidate(
            Assignment(Guid.NewGuid(), "T-Bad", MusicalRole.Lead, PrecedenceTier.Manual, 0, 0),
            window, Brightness: 1.5, IsFocal: true);

        Assert.Throws<CompositionInvalidException>(() => resolver.Resolve([bad], budget, window));
    }

    [Fact]
    public void Resolve_CandidateWithUnrecognizedPrecedenceTier_Throws_NegativePath()
    {
        var resolver = new VisualBudgetResolver(new RolePrecedenceResolver());
        var budget = new VisualBudget(1, 1.0, 10.0, 0.0);
        var window = new TimeWindow(0, TicksPerSecond);
        var bad = new VisualBudgetCandidate(
            Assignment(Guid.NewGuid(), "T-Bad", MusicalRole.Lead, "NotARealTier", 0, 0),
            window, Brightness: 0.1, IsFocal: true);

        Assert.Throws<CompositionInvalidException>(() => resolver.Resolve([bad], budget, window));
    }

    [Theory]
    [InlineData(-1, 0.5, 1.0, 0.1)]
    [InlineData(1, 1.5, 1.0, 0.1)]
    [InlineData(1, 0.5, -1.0, 0.1)]
    [InlineData(1, 0.5, 1.0, 1.1)]
    public void VisualBudget_Validate_RejectsOutOfRangeFields_NegativePath(int maxFocal, double maxBrightness, double maxDensity, double darknessTarget)
    {
        var budget = new VisualBudget(maxFocal, maxBrightness, maxDensity, darknessTarget);
        Assert.Throws<CompositionInvalidException>(budget.Validate);
    }

    [Fact]
    public async Task VisualBudgetPresetCatalog_SaveAndReload_PersistsAcrossRestart()
    {
        var cacheDirectory = NewCacheDirectory("preset-persist");
        try
        {
            var catalog = new VisualBudgetPresetCatalog(new JsonVisualBudgetPresetStore(cacheDirectory));
            var budget = new VisualBudget(2, 0.7, 3.0, 0.25);

            var saved = await catalog.SaveAsync("Verse", budget, CancellationToken.None);
            Assert.Equal(0, saved.Version);

            var resaved = await catalog.SaveAsync("Verse", budget with { MaxConcurrentFocalProps = 3 }, CancellationToken.None);
            Assert.Equal(1, resaved.Version);

            var names = await catalog.ListNamesAsync(CancellationToken.None);
            Assert.Contains("Verse", names);

            // Restart: a brand-new catalog/store instance over the same on-disk directory.
            var reloadCatalog = new VisualBudgetPresetCatalog(new JsonVisualBudgetPresetStore(cacheDirectory));
            var reloaded = await reloadCatalog.GetAsync("Verse", CancellationToken.None);
            Assert.NotNull(reloaded);
            Assert.Equal(1, reloaded!.Version);
            Assert.Equal(3, reloaded.Budget.MaxConcurrentFocalProps);

            var missing = await reloadCatalog.GetAsync("NeverSaved", CancellationToken.None);
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
    public async Task VisualBudgetPresetCatalog_InvalidInput_RejectsBeforeAnyWrite_NegativePath()
    {
        var cacheDirectory = NewCacheDirectory("preset-invalid");
        try
        {
            var store = new JsonVisualBudgetPresetStore(cacheDirectory);
            var catalog = new VisualBudgetPresetCatalog(store);
            var validBudget = new VisualBudget(1, 0.5, 1.0, 0.1);
            var invalidBudget = new VisualBudget(-1, 0.5, 1.0, 0.1);

            await Assert.ThrowsAsync<CompositionInvalidException>(() =>
                catalog.SaveAsync("   ", validBudget, CancellationToken.None)); // blank name

            await Assert.ThrowsAsync<CompositionInvalidException>(() =>
                catalog.SaveAsync("bad/name", validBudget, CancellationToken.None)); // unsafe characters

            await Assert.ThrowsAsync<CompositionInvalidException>(() =>
                catalog.SaveAsync("Verse", invalidBudget, CancellationToken.None)); // invalid budget

            Assert.False(Directory.Exists(cacheDirectory) && Directory.EnumerateFiles(cacheDirectory).Any());
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
    public async Task VisualBudgetPresetCatalog_Cancelled_DoesNotPersist_NegativePath()
    {
        var cacheDirectory = NewCacheDirectory("preset-cancel");
        try
        {
            var store = new JsonVisualBudgetPresetStore(cacheDirectory);
            var catalog = new VisualBudgetPresetCatalog(store);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                catalog.SaveAsync("Verse", new VisualBudget(1, 0.5, 1.0, 0.1), cts.Token));

            Assert.False(File.Exists(store.GetPath("Verse")));
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
    public async Task VisualBudgetPresetStore_LoadAsync_CorruptFile_ThrowsCorruptException_NegativePath()
    {
        var cacheDirectory = NewCacheDirectory("preset-corrupt");
        Directory.CreateDirectory(cacheDirectory);
        var path = Path.Combine(cacheDirectory, "corrupt.json");
        await File.WriteAllTextAsync(path, "{ not valid json");

        try
        {
            var store = new JsonVisualBudgetPresetStore(cacheDirectory);
            await Assert.ThrowsAsync<CompositionCorruptException>(() => store.LoadAsync(path, CancellationToken.None));
        }
        finally
        {
            Directory.Delete(cacheDirectory, recursive: true);
        }
    }
}
