using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Structure.Tests;

/// <summary>
/// PHASE-08 / Step 057 -- "Create parameterized choreography template and instance overrides." Required
/// acceptance: "Edit master/motif occurrence without surprising cross-section changes." Implementation
/// guidance: "Create parent/instance pattern inheritance with adjustable mask and transform; preview effect
/// list and respect locks." Failure/edge case: "Invalid input, cancellation or stale persisted state must
/// not corrupt the project."
/// </summary>
public sealed class Step057AcceptanceTests : IDisposable
{
    private const long BarTicks = 10_000_000L;
    private static readonly ModelId MotionModel = new("Model/Tree");
    private static readonly ModelId DrumModel = new("Model/Drum");

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"xlai-step057-{Guid.NewGuid():N}");

    public Step057AcceptanceTests()
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

    private static PhraseSegmentationAnalysis BuildPhraseAnalysis(SongId songId, string sourceHash, IReadOnlyList<PhraseCandidate> phrases) =>
        new(
            VersionId: Guid.NewGuid(),
            SongId: songId,
            SourceHash: sourceHash,
            SourceSectionLabelVersionId: Guid.NewGuid(),
            SourceFingerprintSetVersionId: Guid.NewGuid(),
            SourceMelodyTrackId: null,
            AnalyzerVersion: "test-fixture/1.0.0",
            Settings: new PhraseSegmentationSettings(8, 1, 5_000_000L, "test-fixture-recipe/1.0.0"),
            Phrases: phrases);

    private static MotifClusteringAnalysis BuildMotifClustering(
        SongId songId, string sourceHash, Guid phraseSegmentationVersionId, MotifGroupCandidate group) =>
        new(
            VersionId: Guid.NewGuid(),
            SongId: songId,
            SourceHash: sourceHash,
            SourcePhraseSegmentationVersionId: phraseSegmentationVersionId,
            SourceFingerprintSetVersionId: Guid.NewGuid(),
            AnalyzerVersion: "test-fixture/1.0.0",
            Settings: new MotifClusteringSettings(4, 0.9, "test-fixture-recipe/1.0.0"),
            Groups: [group],
            Links: []);

    private sealed record Fixture(
        SongId SongId,
        string SourceHash,
        PhraseCandidate ChorusA,
        PhraseCandidate ChorusB,
        PhraseCandidate ChorusC,
        PhraseSegmentationAnalysis PhraseAnalysis,
        MotifClusteringAnalysis MotifClustering,
        MotifGroupCandidate Group,
        EffectInstance MotionEffect,
        EffectInstance DrumEffect,
        EffectInstance LockedConflictInB,
        SequenceDocument Document);

    /// <summary>
    /// Chorus A [0,8) (source/master, 8 bars) and Chorus B [20,28) (8 bars, same duration) are one motif
    /// group's two occurrences; Chorus C [40,46) (6 bars, a shorter variant) is a third occurrence used to
    /// exercise re-timing. Chorus A's own document carries one "motion" effect (a palette-tagged parameter
    /// plus a motion parameter) and one "drum" effect on a declared rhythm layer. Chorus B's document
    /// additionally carries a pre-existing locked effect exactly where the retimed motion effect would land.
    /// </summary>
    private static Fixture BuildFixture()
    {
        var songId = new SongId(Guid.NewGuid());
        const string sourceHash = "deadbeef";

        var chorusA = Phrase(0, 8);
        var chorusB = Phrase(20, 28);
        var chorusC = Phrase(40, 46);

        var phraseAnalysis = BuildPhraseAnalysis(songId, sourceHash, [chorusA, chorusB, chorusC]);

        var group = new MotifGroupCandidate(
            Id: Guid.NewGuid(),
            PhraseIds: [chorusA.Id, chorusB.Id, chorusC.Id],
            RepresentativePhraseId: chorusA.Id,
            Confidence: 1.0,
            Evidence: ["test fixture"]);

        var motifClustering = BuildMotifClustering(songId, sourceHash, phraseAnalysis.VersionId, group);

        var motionEffect = new EffectInstance(
            Id: Guid.NewGuid(),
            Target: MotionModel,
            Layer: 0,
            StartTicks: chorusA.StartTicks + 1_000_000,
            EndTicks: chorusA.StartTicks + 3_000_000,
            RecipeId: "Twinkle",
            RecipeVersion: "1.0",
            Parameters: new Dictionary<string, string> { ["Intensity"] = "0.8", ["Palette.Color"] = "Red" });

        var drumEffect = new EffectInstance(
            Id: Guid.NewGuid(),
            Target: DrumModel,
            Layer: 1,
            StartTicks: chorusA.StartTicks + 500_000,
            EndTicks: chorusA.StartTicks + 1_500_000,
            RecipeId: "Pulse",
            RecipeVersion: "1.0",
            Parameters: new Dictionary<string, string>());

        // Exactly where the retimed (ratio 1.0, same 8-bar duration) motion effect would land in Chorus B.
        var lockedConflictInB = new EffectInstance(
            Id: Guid.NewGuid(),
            Target: MotionModel,
            Layer: 0,
            StartTicks: chorusB.StartTicks + 1_000_000,
            EndTicks: chorusB.StartTicks + 3_000_000,
            RecipeId: "Existing",
            RecipeVersion: "1.0",
            Parameters: new Dictionary<string, string>(),
            Locked: true);

        var document = new SequenceDocument(
            Id: Guid.NewGuid(),
            Revision: 1,
            Effects: [motionEffect, drumEffect, lockedConflictInB]);

        return new Fixture(songId, sourceHash, chorusA, chorusB, chorusC, phraseAnalysis, motifClustering, group,
            motionEffect, drumEffect, lockedConflictInB, document);
    }

    private static ChoreographyTemplateContext ContextFor(Fixture fx, SequenceDocument? document = null) =>
        new(fx.SongId, fx.SourceHash, fx.MotifClustering, fx.PhraseAnalysis, document ?? fx.Document);

    private static readonly ChoreographyInheritanceMask DefaultMask = new(
        Motion: true, Timing: ChoreographyTimingPolicy.Retime, Palette: false, RhythmLayer: false);

    private static readonly IReadOnlySet<int> RhythmLayers = new HashSet<int> { 1 };

    [Fact]
    public async Task Step057_Acceptance_Passes_EditMasterOrInstanceOverride_WithoutSurprisingCrossSectionChanges()
    {
        var fx = BuildFixture();
        var planner = new ChoreographyTemplatePlanner(new JsonChoreographyTemplateStore(_tempDir));
        var context = ContextFor(fx);

        var analysis1 = await planner.DefineTemplateAsync(
            null, context, fx.Group.Id, fx.ChorusA.Id, DefaultMask, RhythmLayers, CancellationToken.None);

        var template = Assert.Single(analysis1.Templates);
        Assert.Equal(2, template.Effects.Count); // motion + drum captured; locked conflict (in Chorus B) is not in Chorus A's own window.

        // Preview Chorus B and Chorus C before any override: both inherit the template identically.
        var previewB1 = planner.PreviewInstance(analysis1, template.Id, fx.ChorusB.Id, fx.PhraseAnalysis, fx.Document);
        var previewC1 = planner.PreviewInstance(analysis1, template.Id, fx.ChorusC.Id, fx.PhraseAnalysis, fx.Document);

        // Drum (rhythm layer) is masked out of inheritance entirely -- "leaves destination-specific D untouched" (Section 12).
        Assert.DoesNotContain(previewB1.Effects, e => e.Target == DrumModel);
        Assert.DoesNotContain(previewC1.Effects, e => e.Target == DrumModel);

        // Chorus B's own motion candidate lands exactly on the pre-existing locked effect -- excluded, "respect locks."
        Assert.Empty(previewB1.Effects);
        Assert.Contains(previewB1.ExcludedReasons, r => r.Contains("locked", StringComparison.OrdinalIgnoreCase));

        // Chorus C has no lock conflict: motion effect inherits, re-timed (6/8 duration ratio) and palette-stripped.
        var motionInC1 = Assert.Single(previewC1.Effects);
        Assert.Equal(fx.ChorusC.StartTicks + 750_000, motionInC1.StartTicks);
        Assert.Equal(fx.ChorusC.StartTicks + 2_250_000, motionInC1.EndTicks);
        Assert.Equal("0.8", motionInC1.Parameters["Intensity"]);
        Assert.False(motionInC1.Parameters.ContainsKey("Palette.Color"));

        // --- Override one specific occurrence (Chorus C's own motion effect) ---
        var overrideForC = new ChoreographyInstanceOverride(
            SourceEffectId: fx.MotionEffect.Id,
            Target: MotionModel,
            Layer: 0,
            StartTicks: fx.ChorusC.StartTicks,
            EndTicks: fx.ChorusC.StartTicks + 500_000,
            RecipeId: "InstanceOnlyOverride",
            RecipeVersion: "9.9",
            Parameters: new Dictionary<string, string> { ["Intensity"] = "0.99" });

        var analysis2 = await planner.SetInstanceOverrideAsync(
            analysis1, context, template.Id, fx.ChorusC.Id, overrideForC, CancellationToken.None);

        var previewC2 = planner.PreviewInstance(analysis2, template.Id, fx.ChorusC.Id, fx.PhraseAnalysis, fx.Document);
        var overriddenMotionInC = Assert.Single(previewC2.Effects);
        Assert.Equal("InstanceOnlyOverride", overriddenMotionInC.RecipeId);
        Assert.Equal("0.99", overriddenMotionInC.Parameters["Intensity"]);

        // The override must never leak onto a sibling occurrence that never asked for it.
        var previewB2 = planner.PreviewInstance(analysis2, template.Id, fx.ChorusB.Id, fx.PhraseAnalysis, fx.Document);
        Assert.Empty(previewB2.Effects); // Still excluded by the same lock -- unaffected by Chorus C's own override.

        // --- Now "edit the master": the source document's own motion effect parameters change ---
        var editedMotionEffect = fx.MotionEffect with
        {
            Parameters = new Dictionary<string, string> { ["Intensity"] = "0.5", ["Palette.Color"] = "Blue" },
        };
        var editedDocument = fx.Document with { Revision = 2, Effects = [editedMotionEffect, fx.DrumEffect, fx.LockedConflictInB] };
        var editedContext = ContextFor(fx, editedDocument);

        var analysis3 = await planner.DefineTemplateAsync(
            analysis2, editedContext, fx.Group.Id, fx.ChorusA.Id, DefaultMask, RhythmLayers, CancellationToken.None);

        Assert.Equal(template.Id, Assert.Single(analysis3.Templates).Id); // same template identity across the refresh.

        // Chorus C still shows ITS OWN override (not master's new value, not reverted) -- "without surprising cross-section changes."
        var previewC3 = planner.PreviewInstance(analysis3, template.Id, fx.ChorusC.Id, fx.PhraseAnalysis, fx.Document);
        var stillOverriddenInC = Assert.Single(previewC3.Effects);
        Assert.Equal("InstanceOnlyOverride", stillOverriddenInC.RecipeId);
        Assert.Equal("0.99", stillOverriddenInC.Parameters["Intensity"]);

        // Chorus B (no override of its own) picks up the master's new value once the lock is lifted.
        var unlockedDocument = editedDocument with { Effects = [editedMotionEffect, fx.DrumEffect] };
        var previewB3 = planner.PreviewInstance(analysis3, template.Id, fx.ChorusB.Id, fx.PhraseAnalysis, unlockedDocument);
        var motionInB3 = Assert.Single(previewB3.Effects);
        Assert.Equal("0.5", motionInB3.Parameters["Intensity"]);
    }

    [Fact]
    public async Task PreviewInstance_CopyTimingPolicy_WindowDoesNotFit_ExcludedWithReason()
    {
        var fx = BuildFixture();
        var planner = new ChoreographyTemplatePlanner(new JsonChoreographyTemplateStore(_tempDir));
        var copyMask = DefaultMask with { Timing = ChoreographyTimingPolicy.Copy };

        var analysis = await planner.DefineTemplateAsync(
            null, ContextFor(fx), fx.Group.Id, fx.ChorusA.Id, copyMask, RhythmLayers, CancellationToken.None);
        var template = analysis.Templates[0];

        // Chorus C is only 6 bars (60,000,000 ticks); the copied absolute window [1,000,000, 3,000,000) still fits
        // (it is well within the phrase), so instead prove non-fit using a phrase shorter than the copied offset.
        var tinyPhrase = Phrase(100, 100) with { StartTicks = 1_000_000_000, EndTicks = 1_000_000_000 + 500_000 };
        var phraseAnalysisWithTiny = fx.PhraseAnalysis with { Phrases = [.. fx.PhraseAnalysis.Phrases, tinyPhrase] };

        var preview = planner.PreviewInstance(analysis, template.Id, tinyPhrase.Id, phraseAnalysisWithTiny, fx.Document);

        Assert.Empty(preview.Effects);
        Assert.Contains(preview.ExcludedReasons, r => r.Contains("does not fit", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PreviewInstance_OrphanedOverride_StillHonoredAsStandaloneAddition()
    {
        var fx = BuildFixture();
        var planner = new ChoreographyTemplatePlanner(new JsonChoreographyTemplateStore(_tempDir));
        var context = ContextFor(fx);

        var analysis1 = await planner.DefineTemplateAsync(
            null, context, fx.Group.Id, fx.ChorusA.Id, DefaultMask, RhythmLayers, CancellationToken.None);
        var template = analysis1.Templates[0];

        var overrideForDrum = new ChoreographyInstanceOverride(
            SourceEffectId: fx.DrumEffect.Id,
            Target: DrumModel,
            Layer: 1,
            StartTicks: fx.ChorusC.StartTicks,
            EndTicks: fx.ChorusC.StartTicks + 400_000,
            RecipeId: "InstanceDrumOverride",
            RecipeVersion: "1.0",
            Parameters: new Dictionary<string, string>());

        var analysis2 = await planner.SetInstanceOverrideAsync(
            analysis1, context, template.Id, fx.ChorusC.Id, overrideForDrum, CancellationToken.None);

        // Now redefine the master from a document where the drum effect no longer exists at all.
        var documentWithoutDrum = fx.Document with { Revision = 2, Effects = [fx.MotionEffect, fx.LockedConflictInB] };
        var analysis3 = await planner.DefineTemplateAsync(
            analysis2, ContextFor(fx, documentWithoutDrum), fx.Group.Id, fx.ChorusA.Id, DefaultMask, RhythmLayers, CancellationToken.None);

        Assert.DoesNotContain(analysis3.Templates[0].Effects, e => e.SourceEffectId == fx.DrumEffect.Id);

        var preview = planner.PreviewInstance(analysis3, template.Id, fx.ChorusC.Id, fx.PhraseAnalysis, fx.Document);

        // The user's own explicit drum override survives even though the master no longer captures that source effect.
        Assert.Contains(preview.Effects, e => e.RecipeId == "InstanceDrumOverride");
    }

    [Fact]
    public async Task DefineTemplateAsync_UnknownMotifGroupId_ThrowsChoreographyTemplateInvalidException()
    {
        var fx = BuildFixture();
        var planner = new ChoreographyTemplatePlanner(new JsonChoreographyTemplateStore(_tempDir));

        var ex = await Assert.ThrowsAsync<ChoreographyTemplateInvalidException>(() =>
            planner.DefineTemplateAsync(null, ContextFor(fx), Guid.NewGuid(), fx.ChorusA.Id, DefaultMask, RhythmLayers, CancellationToken.None));
        Assert.Contains("Motif group", ex.Reason);
    }

    [Fact]
    public async Task DefineTemplateAsync_SourcePhraseNotInGroup_ThrowsChoreographyTemplateInvalidException()
    {
        var fx = BuildFixture();
        var planner = new ChoreographyTemplatePlanner(new JsonChoreographyTemplateStore(_tempDir));
        var outsidePhrase = Phrase(100, 108);

        var ex = await Assert.ThrowsAsync<ChoreographyTemplateInvalidException>(() =>
            planner.DefineTemplateAsync(null, ContextFor(fx), fx.Group.Id, outsidePhrase.Id, DefaultMask, RhythmLayers, CancellationToken.None));
        Assert.Contains("not a member", ex.Reason);
    }

    [Fact]
    public async Task DefineTemplateAsync_MismatchedProvenance_ThrowsChoreographyTemplateInvalidException()
    {
        var fx = BuildFixture();
        var planner = new ChoreographyTemplatePlanner(new JsonChoreographyTemplateStore(_tempDir));
        var staleMotifClustering = fx.MotifClustering with { SourcePhraseSegmentationVersionId = Guid.NewGuid() };
        var context = fx with { MotifClustering = staleMotifClustering };

        var ex = await Assert.ThrowsAsync<ChoreographyTemplateInvalidException>(() =>
            planner.DefineTemplateAsync(null, ContextFor(context), fx.Group.Id, fx.ChorusA.Id, DefaultMask, RhythmLayers, CancellationToken.None));
        Assert.Contains("SourcePhraseSegmentationVersionId", ex.Reason);
    }

    [Fact]
    public async Task DefineTemplateAsync_EmptySourceHash_ThrowsChoreographyTemplateInvalidException_MissingMediaNegativePath()
    {
        var fx = BuildFixture();
        var planner = new ChoreographyTemplatePlanner(new JsonChoreographyTemplateStore(_tempDir));
        var emptyHashContext = new ChoreographyTemplateContext(fx.SongId, "", fx.MotifClustering, fx.PhraseAnalysis, fx.Document);

        var ex = await Assert.ThrowsAsync<ChoreographyTemplateInvalidException>(() =>
            planner.DefineTemplateAsync(null, emptyHashContext, fx.Group.Id, fx.ChorusA.Id, DefaultMask, RhythmLayers, CancellationToken.None));
        Assert.Contains("missing media", ex.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DefineTemplateAsync_NullArguments_ThrowArgumentNullException()
    {
        var fx = BuildFixture();
        var planner = new ChoreographyTemplatePlanner(new JsonChoreographyTemplateStore(_tempDir));

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            planner.DefineTemplateAsync(null, null!, fx.Group.Id, fx.ChorusA.Id, DefaultMask, RhythmLayers, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            planner.DefineTemplateAsync(null, ContextFor(fx), fx.Group.Id, fx.ChorusA.Id, null!, RhythmLayers, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            planner.DefineTemplateAsync(null, ContextFor(fx), fx.Group.Id, fx.ChorusA.Id, DefaultMask, null!, CancellationToken.None));
    }

    [Fact]
    public async Task DefineTemplateAsync_CancellationAlreadyRequested_ThrowsOperationCanceledException_NeverCommitted()
    {
        var fx = BuildFixture();
        var store = new JsonChoreographyTemplateStore(_tempDir);
        var planner = new ChoreographyTemplatePlanner(store);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            planner.DefineTemplateAsync(null, ContextFor(fx), fx.Group.Id, fx.ChorusA.Id, DefaultMask, RhythmLayers, cts.Token));
        Assert.False(File.Exists(store.GetPath(fx.SongId)));
    }

    [Fact]
    public async Task DefineTemplateAsync_StaleCurrent_ThrowsChoreographyTemplateInvalidException_NeverCorrupted()
    {
        var fx = BuildFixture();
        var planner = new ChoreographyTemplatePlanner(new JsonChoreographyTemplateStore(_tempDir));
        var context = ContextFor(fx);

        var analysis1 = await planner.DefineTemplateAsync(
            null, context, fx.Group.Id, fx.ChorusA.Id, DefaultMask, RhythmLayers, CancellationToken.None);

        // Something is already persisted; calling again with current=null (as if the caller never loaded it) must fail.
        var ex = await Assert.ThrowsAsync<ChoreographyTemplateInvalidException>(() =>
            planner.DefineTemplateAsync(null, context, fx.Group.Id, fx.ChorusA.Id, DefaultMask, RhythmLayers, CancellationToken.None));
        Assert.Contains("reload", ex.Reason, StringComparison.OrdinalIgnoreCase);

        // The original, valid analysis must remain on disk, untouched.
        var store = new JsonChoreographyTemplateStore(_tempDir);
        var reloaded = await store.LoadAsync(store.GetPath(fx.SongId), CancellationToken.None);
        Assert.Equal(analysis1.VersionId, reloaded.VersionId);
    }

    [Fact]
    public async Task SetInstanceOverrideAsync_StaleCurrent_ThrowsChoreographyTemplateInvalidException()
    {
        var fx = BuildFixture();
        var planner = new ChoreographyTemplatePlanner(new JsonChoreographyTemplateStore(_tempDir));
        var context = ContextFor(fx);

        var analysis1 = await planner.DefineTemplateAsync(
            null, context, fx.Group.Id, fx.ChorusA.Id, DefaultMask, RhythmLayers, CancellationToken.None);
        await planner.DefineTemplateAsync(
            analysis1, context, fx.Group.Id, fx.ChorusA.Id, DefaultMask, RhythmLayers, CancellationToken.None); // bump to analysis2, persisted.

        var overrideForB = new ChoreographyInstanceOverride(
            fx.MotionEffect.Id, MotionModel, 0, fx.ChorusB.StartTicks, fx.ChorusB.StartTicks + 500_000, "X", "1.0", new Dictionary<string, string>());

        // analysis1 is now stale -- the persisted version has moved on to analysis2.
        await Assert.ThrowsAsync<ChoreographyTemplateInvalidException>(() =>
            planner.SetInstanceOverrideAsync(analysis1, context, analysis1.Templates[0].Id, fx.ChorusB.Id, overrideForB, CancellationToken.None));
    }

    [Fact]
    public async Task SetInstanceOverrideAsync_UnknownTemplateId_ThrowsChoreographyTemplateInvalidException()
    {
        var fx = BuildFixture();
        var planner = new ChoreographyTemplatePlanner(new JsonChoreographyTemplateStore(_tempDir));
        var context = ContextFor(fx);
        var analysis = await planner.DefineTemplateAsync(null, context, fx.Group.Id, fx.ChorusA.Id, DefaultMask, RhythmLayers, CancellationToken.None);

        var badOverride = new ChoreographyInstanceOverride(
            fx.MotionEffect.Id, MotionModel, 0, fx.ChorusB.StartTicks, fx.ChorusB.StartTicks + 500_000, "X", "1.0", new Dictionary<string, string>());

        var ex = await Assert.ThrowsAsync<ChoreographyTemplateInvalidException>(() =>
            planner.SetInstanceOverrideAsync(analysis, context, Guid.NewGuid(), fx.ChorusB.Id, badOverride, CancellationToken.None));
        Assert.Contains("Template", ex.Reason);
    }

    [Fact]
    public async Task SetInstanceOverrideAsync_TargetPhraseNotInGroup_ThrowsChoreographyTemplateInvalidException()
    {
        var fx = BuildFixture();
        var planner = new ChoreographyTemplatePlanner(new JsonChoreographyTemplateStore(_tempDir));
        var context = ContextFor(fx);
        var analysis = await planner.DefineTemplateAsync(null, context, fx.Group.Id, fx.ChorusA.Id, DefaultMask, RhythmLayers, CancellationToken.None);
        var outsidePhrase = Phrase(100, 108);

        var badOverride = new ChoreographyInstanceOverride(
            fx.MotionEffect.Id, MotionModel, 0, outsidePhrase.StartTicks, outsidePhrase.StartTicks + 500_000, "X", "1.0", new Dictionary<string, string>());

        var ex = await Assert.ThrowsAsync<ChoreographyTemplateInvalidException>(() =>
            planner.SetInstanceOverrideAsync(analysis, context, analysis.Templates[0].Id, outsidePhrase.Id, badOverride, CancellationToken.None));
        Assert.Contains("not a member", ex.Reason);
    }

    [Fact]
    public async Task SetInstanceOverrideAsync_MalformedOverride_EndBeforeStart_ThrowsChoreographyTemplateInvalidException()
    {
        var fx = BuildFixture();
        var planner = new ChoreographyTemplatePlanner(new JsonChoreographyTemplateStore(_tempDir));
        var context = ContextFor(fx);
        var analysis = await planner.DefineTemplateAsync(null, context, fx.Group.Id, fx.ChorusA.Id, DefaultMask, RhythmLayers, CancellationToken.None);

        var malformed = new ChoreographyInstanceOverride(
            fx.MotionEffect.Id, MotionModel, 0, fx.ChorusB.StartTicks + 500_000, fx.ChorusB.StartTicks, "X", "1.0", new Dictionary<string, string>());

        var ex = await Assert.ThrowsAsync<ChoreographyTemplateInvalidException>(() =>
            planner.SetInstanceOverrideAsync(analysis, context, analysis.Templates[0].Id, fx.ChorusB.Id, malformed, CancellationToken.None));
        Assert.Contains("EndTicks", ex.Reason);
    }

    [Fact]
    public async Task PreviewInstance_UnknownTemplateOrPhrase_ThrowsChoreographyTemplateInvalidException()
    {
        var fx = BuildFixture();
        var planner = new ChoreographyTemplatePlanner(new JsonChoreographyTemplateStore(_tempDir));
        var analysis = await planner.DefineTemplateAsync(null, ContextFor(fx), fx.Group.Id, fx.ChorusA.Id, DefaultMask, RhythmLayers, CancellationToken.None);

        Assert.Throws<ChoreographyTemplateInvalidException>(() =>
            planner.PreviewInstance(analysis, Guid.NewGuid(), fx.ChorusB.Id, fx.PhraseAnalysis, fx.Document));
        Assert.Throws<ChoreographyTemplateInvalidException>(() =>
            planner.PreviewInstance(analysis, analysis.Templates[0].Id, Guid.NewGuid(), fx.PhraseAnalysis, fx.Document));
    }

    [Fact]
    public async Task DefineTemplateAsync_ContainmentPolicy_OnlyFullyContainedEffectsCaptured()
    {
        var fx = BuildFixture();
        // An effect straddling the Chorus A / Chorus B boundary must never be captured into the template.
        var straddling = new EffectInstance(
            Guid.NewGuid(), MotionModel, 0, fx.ChorusA.EndTicks - 1_000_000, fx.ChorusA.EndTicks + 1_000_000, "Straddle", "1.0", new Dictionary<string, string>());
        var document = fx.Document with { Effects = [.. fx.Document.Effects, straddling] };

        var planner = new ChoreographyTemplatePlanner(new JsonChoreographyTemplateStore(_tempDir));
        var analysis = await planner.DefineTemplateAsync(
            null, ContextFor(fx, document), fx.Group.Id, fx.ChorusA.Id, DefaultMask, RhythmLayers, CancellationToken.None);

        Assert.DoesNotContain(analysis.Templates[0].Effects, e => e.SourceEffectId == straddling.Id);
    }
}
