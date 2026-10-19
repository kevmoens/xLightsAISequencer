using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.WholeSong.Tests;

/// <summary>
/// PHASE-14 Step 092 -- "Apply motif variations by occurrence and climax scheduling." Required acceptance:
/// "Final chorus preserves recognizable motif with intentional lift." Implementation guidance: "Reapply
/// motifs by semantic inheritance with controlled color/intensity/arrangement changes; display inherited vs
/// instance-specific effects." Failure/edge case: "Invalid input, cancellation or stale persisted state must
/// not corrupt the project." These tests exercise the real <see cref="MotifVariationScheduler"/>/
/// <see cref="ChoreographyTemplatePlanner"/>/<see cref="MotifVariationCatalog"/>/<see cref="JsonMotifVariationPlanStore"/>
/// (no mocking) against real PHASE-08 <see cref="ChoreographyTemplateAnalysis"/>/<see cref="PhraseSegmentationAnalysis"/>
/// fixtures built directly via their own production constructors.
/// </summary>
public sealed class Step092AcceptanceTests
{
    private static readonly ModelId TreeModel = new("Tree");

    private static string NewCacheDirectory(string suffix) =>
        Path.Combine(Path.GetTempPath(), $"xlai-step092-{suffix}-{Guid.NewGuid():N}");

    private static PhraseCandidate Phrase(Guid id, int startBar, long startTicks, long endTicks) => new(
        id, Guid.NewGuid(), startBar, startBar + 8, startTicks, endTicks, 8,
        PhraseBoundarySource.BarHypothesis, 1.0, ["fixture evidence"], "fixture-signature", UserCorrected: false);

    /// <summary>
    /// Builds the phase-gate's own required demo shape -- a chorus motif that recurs at Chorus 1, Chorus 2
    /// and the Final Chorus, each a real <see cref="PhraseSegmentationAnalysis"/> phrase, plus a real
    /// <see cref="ChoreographyTemplateAnalysis"/> template captured from Chorus 1 (the source occurrence)
    /// carrying one "Intensity."-tagged effect.
    /// </summary>
    private static (PhraseSegmentationAnalysis Phrases, ChoreographyTemplateAnalysis Templates, Guid TemplateId,
        Guid Chorus1Id, Guid Chorus2Id, Guid FinalChorusId, Guid SourceEffectId) BuildFixture(SongId songId)
    {
        var chorus1 = Guid.NewGuid();
        var chorus2 = Guid.NewGuid();
        var finalChorus = Guid.NewGuid();

        var phraseAnalysis = new PhraseSegmentationAnalysis(
            Guid.NewGuid(), songId, "fixture-source-hash", Guid.NewGuid(), Guid.NewGuid(), null,
            "fixture-segmenter-v1", new PhraseSegmentationSettings(8, 2, 1000, "fixture-recipe"),
            [
                Phrase(chorus1, 16, 16_000, 24_000),
                Phrase(chorus2, 32, 32_000, 40_000),
                Phrase(finalChorus, 48, 48_000, 56_000),
            ]);

        var sourceEffectId = Guid.NewGuid();
        var templateEffect = new ChoreographyTemplateEffect(
            sourceEffectId, TreeModel, Layer: 0, RelativeStartTicks: 0, RelativeEndTicks: 8000,
            RecipeId: "fixture-recipe", RecipeVersion: "v1",
            Parameters: new Dictionary<string, string> { ["Intensity.Brightness"] = "0.5" },
            IsRhythmLayer: false);

        var template = new ChoreographyTemplate(
            Guid.NewGuid(), MotifGroupId: Guid.NewGuid(), SourcePhraseId: chorus1,
            Mask: new ChoreographyInheritanceMask(Motion: true, ChoreographyTimingPolicy.Copy, Palette: true, RhythmLayer: true),
            Effects: [templateEffect], Instances: []);

        var templateAnalysis = new ChoreographyTemplateAnalysis(
            Guid.NewGuid(), songId, "fixture-source-hash", Guid.NewGuid(), SourceDocumentRevision: 1,
            AnalyzerVersion: "fixture-planner-v1", Templates: [template]);

        return (phraseAnalysis, templateAnalysis, template.Id, chorus1, chorus2, finalChorus, sourceEffectId);
    }

    [Fact]
    public async Task Step092_Acceptance_Passes()
    {
        // Arrange: three occurrences of the same chorus motif -- Chorus 1 and Chorus 2 at moderate energy,
        // the Final Chorus at the phase gate's own required highest energy.
        var songId = new SongId(Guid.NewGuid());
        var (phraseAnalysis, templateAnalysis, templateId, chorus1Id, chorus2Id, finalChorusId, sourceEffectId) =
            BuildFixture(songId);

        var destinationDocument = new SequenceDocument(Guid.NewGuid(), Revision: 1, Effects: []);
        var scheduler = new MotifVariationScheduler(new ChoreographyTemplatePlanner(
            new JsonChoreographyTemplateStore(NewCacheDirectory("unused-store"))));

        var requests = new List<MotifOccurrenceRequest>
        {
            new(templateId, chorus1Id, SectionEnergy: 0.80),
            new(templateId, chorus2Id, SectionEnergy: 0.90),
            new(templateId, finalChorusId, SectionEnergy: 0.97),
        };

        var createdAt = DateTimeOffset.UtcNow;

        // Act.
        var plan = scheduler.Schedule(songId, templateAnalysis, phraseAnalysis, destinationDocument, requests, createdAt, CancellationToken.None);

        // Assert: provenance.
        Assert.Equal(songId, plan.SongId);
        Assert.Equal(templateAnalysis.VersionId, plan.SourceTemplateAnalysisVersionId);
        Assert.Equal(phraseAnalysis.VersionId, plan.SourcePhraseSegmentationVersionId);
        Assert.Equal(createdAt, plan.CreatedAtUtc);
        Assert.Equal(3, plan.Occurrences.Count);

        var byPhrase = plan.Occurrences.ToDictionary(o => o.TargetPhraseId);

        // Assert: "climax scheduling" (this step's own title, verbatim) -- only the single highest-energy
        // occurrence of this template is scheduled as the climax.
        Assert.False(byPhrase[chorus1Id].IsClimax);
        Assert.False(byPhrase[chorus2Id].IsClimax);
        Assert.True(byPhrase[finalChorusId].IsClimax);

        // Assert: "Final chorus preserves recognizable motif with intentional lift" -- required acceptance,
        // verbatim. Recognizable: same recipe/target/layer/relative-timing shape across every occurrence.
        foreach (var occurrence in plan.Occurrences)
        {
            var effect = Assert.Single(occurrence.Effects);
            Assert.Equal(TreeModel, effect.Target);
            Assert.Equal(0, effect.Layer);
            Assert.Equal("fixture-recipe", effect.RecipeId);
            Assert.Equal("v1", effect.RecipeVersion);
        }

        // Intentional lift: the final chorus's own intensity multiplier -- and its own resulting scaled
        // "Intensity.Brightness" parameter -- strictly exceeds both earlier, lower-energy occurrences.
        var finalMultiplier = byPhrase[finalChorusId].IntensityMultiplier;
        Assert.True(finalMultiplier > byPhrase[chorus1Id].IntensityMultiplier);
        Assert.True(finalMultiplier > byPhrase[chorus2Id].IntensityMultiplier);

        double Brightness(MotifOccurrenceVariation o) =>
            double.Parse(o.Effects[0].Parameters["Intensity.Brightness"], System.Globalization.CultureInfo.InvariantCulture);

        Assert.True(Brightness(byPhrase[finalChorusId]) > Brightness(byPhrase[chorus1Id]));
        Assert.True(Brightness(byPhrase[finalChorusId]) > Brightness(byPhrase[chorus2Id]));

        // Assert: "display inherited vs instance-specific effects" -- this occurrence has no override, so
        // its one effect slot is classified purely as inherited.
        Assert.Contains(sourceEffectId, byPhrase[finalChorusId].InheritedSourceEffectIds);
        Assert.Empty(byPhrase[finalChorusId].InstanceOverrideSourceEffectIds);

        // Act: persist -- "Versioned, inspectable project output" (User-visible outcome, verbatim).
        var cacheDirectory = NewCacheDirectory("acceptance");
        try
        {
            var store = new JsonMotifVariationPlanStore(cacheDirectory);
            var catalog = new MotifVariationCatalog(store);

            var saved = await catalog.SaveAsync(songId.Value, plan, CancellationToken.None);
            Assert.Equal(0, saved.Version);

            // Assert: persisted domain state survives a restart (brand-new store/catalog instance over the
            // same on-disk directory) -- not only a mock invocation.
            var reloadCatalog = new MotifVariationCatalog(new JsonMotifVariationPlanStore(cacheDirectory));
            var reloaded = await reloadCatalog.GetAsync(songId.Value, CancellationToken.None);
            Assert.NotNull(reloaded);
            Assert.Equal(0, reloaded!.Version);
            Assert.Equal(plan.Id, reloaded.Plan.Id);
            Assert.Equal(3, reloaded.Plan.Occurrences.Count);
            Assert.True(reloaded.Plan.Occurrences.Single(o => o.TargetPhraseId == finalChorusId).IsClimax);
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
    public void Step092_SingleOccurrence_IsItsOwnClimax()
    {
        var songId = new SongId(Guid.NewGuid());
        var (phraseAnalysis, templateAnalysis, templateId, chorus1Id, _, _, _) = BuildFixture(songId);
        var destinationDocument = new SequenceDocument(Guid.NewGuid(), 0, []);
        var scheduler = new MotifVariationScheduler(new ChoreographyTemplatePlanner(
            new JsonChoreographyTemplateStore(NewCacheDirectory("unused-store"))));

        var plan = scheduler.Schedule(
            songId, templateAnalysis, phraseAnalysis, destinationDocument,
            [new MotifOccurrenceRequest(templateId, chorus1Id, SectionEnergy: 0.3)],
            DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.True(Assert.Single(plan.Occurrences).IsClimax);
    }

    [Fact]
    public void Step092_NegativePath_EmptyRequests_Throws()
    {
        var songId = new SongId(Guid.NewGuid());
        var (phraseAnalysis, templateAnalysis, _, _, _, _, _) = BuildFixture(songId);
        var destinationDocument = new SequenceDocument(Guid.NewGuid(), 0, []);
        var scheduler = new MotifVariationScheduler(new ChoreographyTemplatePlanner(
            new JsonChoreographyTemplateStore(NewCacheDirectory("unused-store"))));

        Assert.Throws<MotifVariationInvalidException>(() => scheduler.Schedule(
            songId, templateAnalysis, phraseAnalysis, destinationDocument, [], DateTimeOffset.UtcNow, CancellationToken.None));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    public void Step092_NegativePath_OutOfRangeSectionEnergy_Throws(double energy)
    {
        var songId = new SongId(Guid.NewGuid());
        var (phraseAnalysis, templateAnalysis, templateId, chorus1Id, _, _, _) = BuildFixture(songId);
        var destinationDocument = new SequenceDocument(Guid.NewGuid(), 0, []);
        var scheduler = new MotifVariationScheduler(new ChoreographyTemplatePlanner(
            new JsonChoreographyTemplateStore(NewCacheDirectory("unused-store"))));

        Assert.Throws<MotifVariationInvalidException>(() => scheduler.Schedule(
            songId, templateAnalysis, phraseAnalysis, destinationDocument,
            [new MotifOccurrenceRequest(templateId, chorus1Id, energy)],
            DateTimeOffset.UtcNow, CancellationToken.None));
    }

    [Fact]
    public void Step092_NegativePath_DuplicateOccurrenceRequest_Throws()
    {
        var songId = new SongId(Guid.NewGuid());
        var (phraseAnalysis, templateAnalysis, templateId, chorus1Id, _, _, _) = BuildFixture(songId);
        var destinationDocument = new SequenceDocument(Guid.NewGuid(), 0, []);
        var scheduler = new MotifVariationScheduler(new ChoreographyTemplatePlanner(
            new JsonChoreographyTemplateStore(NewCacheDirectory("unused-store"))));

        Assert.Throws<MotifVariationInvalidException>(() => scheduler.Schedule(
            songId, templateAnalysis, phraseAnalysis, destinationDocument,
            [
                new MotifOccurrenceRequest(templateId, chorus1Id, 0.5),
                new MotifOccurrenceRequest(templateId, chorus1Id, 0.6),
            ],
            DateTimeOffset.UtcNow, CancellationToken.None));
    }

    [Fact]
    public void Step092_NegativePath_UnknownTemplateId_Throws()
    {
        var songId = new SongId(Guid.NewGuid());
        var (phraseAnalysis, templateAnalysis, _, chorus1Id, _, _, _) = BuildFixture(songId);
        var destinationDocument = new SequenceDocument(Guid.NewGuid(), 0, []);
        var scheduler = new MotifVariationScheduler(new ChoreographyTemplatePlanner(
            new JsonChoreographyTemplateStore(NewCacheDirectory("unused-store"))));

        Assert.Throws<ChoreographyTemplateInvalidException>(() => scheduler.Schedule(
            songId, templateAnalysis, phraseAnalysis, destinationDocument,
            [new MotifOccurrenceRequest(Guid.NewGuid(), chorus1Id, 0.5)],
            DateTimeOffset.UtcNow, CancellationToken.None));
    }

    [Fact]
    public void Step092_NegativePath_NonNumericIntensityParameter_Throws()
    {
        var songId = new SongId(Guid.NewGuid());
        var (phraseAnalysis, _, _, chorus1Id, _, _, sourceEffectId) = BuildFixture(songId);

        var badEffect = new ChoreographyTemplateEffect(
            sourceEffectId, TreeModel, 0, 0, 8000, "fixture-recipe", "v1",
            new Dictionary<string, string> { ["Intensity.Brightness"] = "not-a-number" }, false);
        var badTemplate = new ChoreographyTemplate(
            Guid.NewGuid(), Guid.NewGuid(), chorus1Id,
            new ChoreographyInheritanceMask(true, ChoreographyTimingPolicy.Copy, true, true),
            [badEffect], []);
        var badTemplateAnalysis = new ChoreographyTemplateAnalysis(
            Guid.NewGuid(), songId, "fixture-source-hash", Guid.NewGuid(), 1, "fixture-planner-v1", [badTemplate]);

        var destinationDocument = new SequenceDocument(Guid.NewGuid(), 0, []);
        var scheduler = new MotifVariationScheduler(new ChoreographyTemplatePlanner(
            new JsonChoreographyTemplateStore(NewCacheDirectory("unused-store"))));

        Assert.Throws<MotifVariationInvalidException>(() => scheduler.Schedule(
            songId, badTemplateAnalysis, phraseAnalysis, destinationDocument,
            [new MotifOccurrenceRequest(badTemplate.Id, chorus1Id, 0.5)],
            DateTimeOffset.UtcNow, CancellationToken.None));
    }

    [Fact]
    public void Step092_NegativePath_Cancellation_ThrowsBeforeAnyWork()
    {
        var songId = new SongId(Guid.NewGuid());
        var (phraseAnalysis, templateAnalysis, templateId, chorus1Id, _, _, _) = BuildFixture(songId);
        var destinationDocument = new SequenceDocument(Guid.NewGuid(), 0, []);
        var scheduler = new MotifVariationScheduler(new ChoreographyTemplatePlanner(
            new JsonChoreographyTemplateStore(NewCacheDirectory("unused-store"))));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => scheduler.Schedule(
            songId, templateAnalysis, phraseAnalysis, destinationDocument,
            [new MotifOccurrenceRequest(templateId, chorus1Id, 0.5)],
            DateTimeOffset.UtcNow, cts.Token));
    }

    [Fact]
    public async Task Step092_NegativePath_StalePersistedState_RejectsWriteAndLeavesFileUnchanged()
    {
        // "stale persisted state must not corrupt the project" -- required failure/edge case, verbatim.
        var songId = new SongId(Guid.NewGuid());
        var (phraseAnalysis, templateAnalysis, templateId, chorus1Id, chorus2Id, _, _) = BuildFixture(songId);
        var destinationDocument = new SequenceDocument(Guid.NewGuid(), 0, []);
        var scheduler = new MotifVariationScheduler(new ChoreographyTemplatePlanner(
            new JsonChoreographyTemplateStore(NewCacheDirectory("unused-store"))));

        var plan = scheduler.Schedule(
            songId, templateAnalysis, phraseAnalysis, destinationDocument,
            [new MotifOccurrenceRequest(templateId, chorus1Id, 0.5)], DateTimeOffset.UtcNow, CancellationToken.None);

        var cacheDirectory = NewCacheDirectory("stale");
        try
        {
            var store = new JsonMotifVariationPlanStore(cacheDirectory);

            var record0 = new MotifVariationPlanRecord(songId, plan, 0);
            await store.SaveAsync(record0, expectedCurrentVersion: null, CancellationToken.None);

            var replanned = scheduler.Schedule(
                songId, templateAnalysis, phraseAnalysis, destinationDocument,
                [new MotifOccurrenceRequest(templateId, chorus2Id, 0.6)], DateTimeOffset.UtcNow, CancellationToken.None);
            var record1 = new MotifVariationPlanRecord(songId, replanned, 1);
            await store.SaveAsync(record1, expectedCurrentVersion: 0, CancellationToken.None);

            var staleWrite = new MotifVariationPlanRecord(songId, plan, 1);
            var ex = await Assert.ThrowsAsync<MotifVariationStaleException>(
                () => store.SaveAsync(staleWrite, expectedCurrentVersion: 0, CancellationToken.None));
            Assert.Equal(0, ex.ExpectedCurrentVersion);
            Assert.Equal(1, ex.ActualCurrentVersion);

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
    public async Task Step092_NegativePath_CorruptFile_ThrowsCorruptException()
    {
        var cacheDirectory = NewCacheDirectory("corrupt");
        Directory.CreateDirectory(cacheDirectory);
        var path = Path.Combine(cacheDirectory, "corrupt.json");
        await File.WriteAllTextAsync(path, "{ not valid json");

        try
        {
            var store = new JsonMotifVariationPlanStore(cacheDirectory);
            await Assert.ThrowsAsync<MotifVariationCorruptException>(() => store.LoadAsync(path, CancellationToken.None));
        }
        finally
        {
            Directory.Delete(cacheDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task Step092_NegativePath_InvalidPlan_RejectsBeforeAnyWrite_ViaCatalog()
    {
        var songId = new SongId(Guid.NewGuid());
        var invalidPlan = new MotifVariationPlan(
            Guid.NewGuid(), songId, Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, []);

        var cacheDirectory = NewCacheDirectory("invalid");
        try
        {
            var store = new JsonMotifVariationPlanStore(cacheDirectory);
            var catalog = new MotifVariationCatalog(store);

            await Assert.ThrowsAsync<MotifVariationInvalidException>(
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
}
