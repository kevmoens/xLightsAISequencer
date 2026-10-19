using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Composition.Tests;

/// <summary>
/// PHASE-10 Step 071 -- "Compose and resolve overlapping effects with layer/priority strategies."
/// Required acceptance: "No unintended model conflicts; XSQ renders as planned." Implementation
/// guidance: "Combine overlapping role plans into a single scoped candidate; validate collisions/
/// layers/locks and demonstrate purposeful coordination." Failure/edge case: "Clean machine and
/// unsupported runtime must produce user-readable diagnostics." These tests exercise the real
/// <see cref="ChoreographyCompiler"/>/<see cref="RolePrecedenceResolver"/>/<see cref="EffectLockPolicy"/>/
/// <see cref="EffectRecipeRegistry"/>/<see cref="XsqExporter"/>/<see cref="XsqStructuralReader"/> (not
/// mocks), against the phase gate's own named worked-example props (Arches, Roofline, Tree).
/// </summary>
public sealed class Step071AcceptanceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-step071-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static ChoreographyCompiler NewCompiler() =>
        new(new RolePrecedenceResolver(), new EffectLockPolicy(), new EffectRecipeRegistry());

    private static MusicalRoleAssignment Assignment(MusicalRole role, string trackId, string tier, int priority) =>
        new(Guid.NewGuid(), trackId, role, Guid.NewGuid(), tier, priority, Weight: 0.5, Version: 0);

    /// <summary>Phase gate's own named worked example layout: five real models, including the required Arches/Roofline/Tree.</summary>
    private static DisplayGraph GateDemoLayout() => new(
        SourceLayoutHash: "TEST-HASH-NOT-A-REAL-FILE",
        Models:
        [
            new DisplayModel(new ModelId("Arches"), "Arches", "ModelGroup", [], []),
            new DisplayModel(new ModelId("Roofline"), "Roofline", "Star", [], []),
            new DisplayModel(new ModelId("Tree"), "Tree", "Tree", [], []),
        ],
        Groups: [],
        Warnings: []);

    private static EditScope FullScope(TimeWindow time, params ModelId[] models) =>
        new(time, models.ToHashSet());

    [Fact]
    public async Task Step071_Acceptance_NoUnintendedModelConflicts_XsqRendersLayeredPlanAsCompiled()
    {
        // "No unintended model conflicts; XSQ renders as planned" -- required acceptance, verbatim. Lead
        // piano (Manual, higher priority) and an accent snare hit (Automated) both target Arches over a
        // genuinely overlapping window -- the phase gate's own named worked example.
        var arches = new ModelId("Arches");
        var leadPiano = Assignment(MusicalRole.Lead, "T-Piano", PrecedenceTier.Manual, priority: 1);
        var accentSnare = Assignment(MusicalRole.Accent, "T-Snare", PrecedenceTier.Automated, priority: 0);

        var candidates = new List<ChoreographyCandidate>
        {
            new(Guid.NewGuid(), leadPiano, arches, new TimeWindow(0, 1_000_000), EffectRecipeRegistry.SimpleOnRecipeId),
            new(Guid.NewGuid(), accentSnare, arches, new TimeWindow(200_000, 800_000), EffectRecipeRegistry.SimpleOnRecipeId), // genuinely overlaps
        };

        var baseDocument = new SequenceDocument(Guid.NewGuid(), Revision: 1, Effects: []);
        var scope = FullScope(new TimeWindow(0, 2_000_000), arches);
        var compiler = NewCompiler();

        var compiled = compiler.Compile(baseDocument, candidates, scope);

        // Both are admitted -- the lower-precedence accent is reassigned to its own layer rather than
        // dropped ("purposeful coordination," implementation guidance, verbatim), and neither silently
        // overwrites the other.
        Assert.Equal(2, compiled.Document.Effects.Count);
        var leadResult = compiled.Results.Single(r => r.Candidate.Assignment.SourceTrackId == "T-Piano");
        var accentResult = compiled.Results.Single(r => r.Candidate.Assignment.SourceTrackId == "T-Snare");
        Assert.Equal(ChoreographyResolutionDecision.Accepted, leadResult.Decision);
        Assert.Equal(0, leadResult.ResolvedLayer);
        Assert.Equal(ChoreographyResolutionDecision.Reassigned, accentResult.Decision);
        Assert.NotEqual(0, accentResult.ResolvedLayer);
        Assert.NotEmpty(compiled.Warnings);

        var leadEffect = compiled.Document.Effects.Single(e => e.StartTicks == 0 && e.EndTicks == 1_000_000);
        var accentEffect = compiled.Document.Effects.Single(e => e.StartTicks == 200_000 && e.EndTicks == 800_000);
        Assert.Equal(leadResult.ResolvedLayer!.Value, leadEffect.Layer);
        Assert.Equal(accentResult.ResolvedLayer!.Value, accentEffect.Layer);
        Assert.NotEqual(leadEffect.Layer, accentEffect.Layer);

        // "XSQ renders as planned": export the compiled document and independently re-read it back --
        // both layered effects survive export on the same element, at their planned windows, undisturbed
        // by each other (xLights frame-snap collision avoidance only ever applies within one layer).
        var outputPath = Path.Combine(_root, "layered.xsq");
        var exporter = new XsqExporter();
        await exporter.ExportNewAsync(GateDemoLayout(), compiled.Document, outputPath, "2026.03", CancellationToken.None);

        var structural = new XsqStructuralReader().Read(outputPath);
        var archesElement = structural.ElementEffects.Single(e => e.Name == "Arches");
        Assert.Equal(2, archesElement.Effects.Count);
        // xLights' own 25ms frame grid rounds each effect's own raw ticks independently (FrameIntervalMs);
        // 20ms/80ms round-to-nearest-25ms land at 25ms/75ms.
        Assert.Contains(archesElement.Effects, e => e.StartTimeMs == 0.0 && e.EndTimeMs == 100.0);
        Assert.Contains(archesElement.Effects, e => e.StartTimeMs == 25.0 && e.EndTimeMs == 75.0);
    }

    [Fact]
    public async Task Compile_CollisionsBeyondMaxLayers_DropsLowestPrecedence_NeverReplacesHigherPrecedence()
    {
        // "Validate collisions/layers/locks" -- once every layer up to MaxLayers is genuinely occupied by
        // a higher-precedence effect over the same overlapping window, the next lowest-precedence
        // candidate is dropped (never silently placed on top of an existing one).
        var arches = new ModelId("Arches");
        var window = new TimeWindow(0, 1_000_000);
        var candidates = new List<ChoreographyCandidate>();
        for (var i = 0; i <= ChoreographyCompiler.MaxLayers; i++)
        {
            var assignment = Assignment(MusicalRole.Accent, $"T-{i}", PrecedenceTier.Automated, priority: 100 - i); // strictly descending priority
            candidates.Add(new ChoreographyCandidate(Guid.NewGuid(), assignment, arches, window, EffectRecipeRegistry.SimpleOnRecipeId));
        }

        var baseDocument = new SequenceDocument(Guid.NewGuid(), Revision: 1, Effects: []);
        var scope = FullScope(window, arches);
        var compiler = NewCompiler();

        var compiled = compiler.Compile(baseDocument, candidates, scope);

        Assert.Equal(ChoreographyCompiler.MaxLayers, compiled.Document.Effects.Count); // one fewer than requested
        var dropped = Assert.Single(compiled.Dropped);
        Assert.Equal("T-" + ChoreographyCompiler.MaxLayers, dropped.Candidate.Assignment.SourceTrackId); // the lowest-priority one
        Assert.Contains("dropped", dropped.Reason, StringComparison.OrdinalIgnoreCase);

        var outputPath = Path.Combine(_root, "overflow.xsq");
        var exporter = new XsqExporter();
        await exporter.ExportNewAsync(GateDemoLayout(), compiled.Document, outputPath, "2026.03", CancellationToken.None);
        var structural = new XsqStructuralReader().Read(outputPath);
        Assert.Equal(ChoreographyCompiler.MaxLayers, structural.ElementEffects.Single(e => e.Name == "Arches").Effects.Count);
    }

    [Fact]
    public void Compile_NonOverlappingCandidatesOnDifferentModels_AllAcceptedAtLayerZero()
    {
        // Lead piano on Arches, kick/snare accent on Roofline, bass on Tree -- the phase gate's own named
        // worked example, with no time overlap at all: no layering/collision machinery should ever engage.
        var arches = new ModelId("Arches");
        var roofline = new ModelId("Roofline");
        var tree = new ModelId("Tree");
        var window = new TimeWindow(0, 1_000_000);

        var candidates = new List<ChoreographyCandidate>
        {
            new(Guid.NewGuid(), Assignment(MusicalRole.Lead, "T-Piano", PrecedenceTier.Manual, 1), arches, window, EffectRecipeRegistry.SimpleOnRecipeId),
            new(Guid.NewGuid(), Assignment(MusicalRole.Accent, "T-KickSnare", PrecedenceTier.Automated, 0), roofline, window, EffectRecipeRegistry.SimpleOnRecipeId),
            new(Guid.NewGuid(), Assignment(MusicalRole.Bass, "T-Bass", PrecedenceTier.Automated, 0), tree, window, EffectRecipeRegistry.SimpleOnRecipeId),
        };

        var baseDocument = new SequenceDocument(Guid.NewGuid(), Revision: 1, Effects: []);
        var scope = FullScope(window, arches, roofline, tree);
        var compiler = NewCompiler();

        var compiled = compiler.Compile(baseDocument, candidates, scope);

        Assert.Equal(3, compiled.Document.Effects.Count);
        Assert.All(compiled.Results, r => Assert.Equal(ChoreographyResolutionDecision.Accepted, r.Decision));
        Assert.All(compiled.Document.Effects, e => Assert.Equal(0, e.Layer));
        Assert.Empty(compiled.Warnings);
    }

    [Fact]
    public void Compile_SameInputsInAnyOrder_ProducesTheSameAdmissionOutcome()
    {
        // "Same inputs+seed produce repeatable plan" -- full-phase test matrix item #5.
        var arches = new ModelId("Arches");
        var lead = Assignment(MusicalRole.Lead, "T-Piano", PrecedenceTier.Manual, 1);
        var accent = Assignment(MusicalRole.Accent, "T-Snare", PrecedenceTier.Automated, 0);
        var window = new TimeWindow(0, 1_000_000);
        var overlap = new TimeWindow(200_000, 800_000);

        var leadCandidate = new ChoreographyCandidate(Guid.NewGuid(), lead, arches, window, EffectRecipeRegistry.SimpleOnRecipeId);
        var accentCandidate = new ChoreographyCandidate(Guid.NewGuid(), accent, arches, overlap, EffectRecipeRegistry.SimpleOnRecipeId);

        var baseDocument = new SequenceDocument(Guid.NewGuid(), Revision: 1, Effects: []);
        var scope = FullScope(window, arches);
        var compiler = NewCompiler();

        var resultA = compiler.Compile(baseDocument, [leadCandidate, accentCandidate], scope);
        var resultB = compiler.Compile(baseDocument, [accentCandidate, leadCandidate], scope);

        Assert.Equal(
            resultA.Results.Select(r => (r.Candidate.Id, r.Decision, r.ResolvedLayer)).OrderBy(x => x.Id),
            resultB.Results.Select(r => (r.Candidate.Id, r.Decision, r.ResolvedLayer)).OrderBy(x => x.Id));
    }

    [Fact]
    public void Compile_LeadPrioritySurvivesSimultaneousBassAndSnareOnset_KeepsLayerZero()
    {
        // Full-phase test matrix item #1, verbatim.
        var arches = new ModelId("Arches");
        var window = new TimeWindow(0, 1_000_000);
        var lead = new ChoreographyCandidate(
            Guid.NewGuid(), Assignment(MusicalRole.Lead, "T-Lead", PrecedenceTier.Manual, 1), arches, window, EffectRecipeRegistry.SimpleOnRecipeId);
        var bass = new ChoreographyCandidate(
            Guid.NewGuid(), Assignment(MusicalRole.Bass, "T-Bass", PrecedenceTier.Automated, 5), arches, window, EffectRecipeRegistry.SimpleOnRecipeId);
        var snare = new ChoreographyCandidate(
            Guid.NewGuid(), Assignment(MusicalRole.Accent, "T-Snare", PrecedenceTier.Automated, 5), arches, window, EffectRecipeRegistry.SimpleOnRecipeId);

        var baseDocument = new SequenceDocument(Guid.NewGuid(), Revision: 1, Effects: []);
        var scope = FullScope(window, arches);
        var compiler = NewCompiler();

        var compiled = compiler.Compile(baseDocument, [bass, snare, lead], scope);

        var leadResult = compiled.Results.Single(r => r.Candidate.Id == lead.Id);
        Assert.Equal(ChoreographyResolutionDecision.Accepted, leadResult.Decision);
        Assert.Equal(0, leadResult.ResolvedLayer);
        Assert.All(compiled.Results.Where(r => r.Candidate.Id != lead.Id), r => Assert.NotEqual(ChoreographyResolutionDecision.Dropped, r.Decision));
    }

    [Fact]
    public void Compile_ManualLockedEffectCannotBeReplacedByNewPlan_RejectsWholeCompile_NegativePath()
    {
        // Full-phase test matrix item #6, verbatim. A human has already locked an effect on Arches inside
        // the exact scope this compile would touch -- the entire compile must be rejected, not silently
        // routed around.
        var arches = new ModelId("Arches");
        var window = new TimeWindow(0, 1_000_000);
        var registry = new EffectRecipeRegistry();
        var locked = registry.CreateVerified(EffectRecipeRegistry.SimpleOnRecipeId, arches, 0, 500_000) with { Locked = true };
        var baseDocument = new SequenceDocument(Guid.NewGuid(), Revision: 1, Effects: [locked]);

        var candidate = new ChoreographyCandidate(
            Guid.NewGuid(), Assignment(MusicalRole.Lead, "T-Piano", PrecedenceTier.Manual, 1), arches, window, EffectRecipeRegistry.SimpleOnRecipeId);

        var scope = FullScope(window, arches);
        var compiler = NewCompiler();

        var ex = Assert.Throws<EditCommandRejectedException>(() => compiler.Compile(baseDocument, [candidate], scope));
        Assert.Equal(EditRejectionReason.Locked, ex.Reason);

        // No mutation occurred: the base document's own locked effect is unaffected by the rejected compile.
        Assert.Single(baseDocument.Effects);
        Assert.True(baseDocument.Effects[0].Locked);
    }

    [Fact]
    public void Compile_ScopeExcludesLockedModel_LockedEffectCarriedForwardUnchanged()
    {
        // The counterpart to the previous test: when the permitted scope does not reach the locked
        // effect's model at all, the compile proceeds, and the pre-existing locked effect survives into
        // the new document revision byte-for-byte unchanged.
        var arches = new ModelId("Arches");
        var roofline = new ModelId("Roofline");
        var window = new TimeWindow(0, 1_000_000);
        var registry = new EffectRecipeRegistry();
        var locked = registry.CreateVerified(EffectRecipeRegistry.SimpleOnRecipeId, arches, 0, 500_000) with { Locked = true };
        var baseDocument = new SequenceDocument(Guid.NewGuid(), Revision: 1, Effects: [locked]);

        var candidate = new ChoreographyCandidate(
            Guid.NewGuid(), Assignment(MusicalRole.Accent, "T-KickSnare", PrecedenceTier.Automated, 0), roofline, window, EffectRecipeRegistry.SimpleOnRecipeId);

        var scope = FullScope(window, roofline); // deliberately excludes Arches
        var compiler = NewCompiler();

        var compiled = compiler.Compile(baseDocument, [candidate], scope);

        Assert.Equal(2, compiled.Document.Effects.Count);
        Assert.Contains(compiled.Document.Effects, e => e.Id == locked.Id && e.Locked && e.StartTicks == locked.StartTicks && e.EndTicks == locked.EndTicks);
        Assert.Equal(2, compiled.Document.Revision);
    }

    [Fact]
    public void Compile_EmptyCandidates_ThrowsBeforeAnyComputation_NegativePath()
    {
        // "Clean machine" -- a project with no planned gestures yet. Required failure/edge case, verbatim.
        var baseDocument = new SequenceDocument(Guid.NewGuid(), Revision: 1, Effects: []);
        var scope = FullScope(new TimeWindow(0, 1), new ModelId("Arches"));
        var compiler = NewCompiler();

        var ex = Assert.Throws<CompositionInvalidException>(() => compiler.Compile(baseDocument, [], scope));
        Assert.Contains("clean machine", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Compile_UnsupportedRecipeId_ThrowsUserReadableDiagnostic_NegativePath()
    {
        // "Unsupported runtime" -- a candidate names a recipe this build does not actually know how to
        // compile. Required failure/edge case, verbatim.
        var arches = new ModelId("Arches");
        var window = new TimeWindow(0, 1_000_000);
        var candidate = new ChoreographyCandidate(
            Guid.NewGuid(), Assignment(MusicalRole.Lead, "T-Piano", PrecedenceTier.Manual, 1), arches, window, "effect.not-a-real-recipe.v1");

        var baseDocument = new SequenceDocument(Guid.NewGuid(), Revision: 1, Effects: []);
        var scope = FullScope(window, arches);
        var compiler = NewCompiler();

        var ex = Assert.Throws<EffectPlanInvalidException>(() => compiler.Compile(baseDocument, [candidate], scope));
        Assert.Contains("Unknown effect recipe id", ex.Message);
    }

    [Fact]
    public void Compile_CandidateTargetOutsideScope_Throws_NegativePath()
    {
        var arches = new ModelId("Arches");
        var window = new TimeWindow(0, 1_000_000);
        var candidate = new ChoreographyCandidate(
            Guid.NewGuid(), Assignment(MusicalRole.Lead, "T-Piano", PrecedenceTier.Manual, 1), new ModelId("Tree"), window, EffectRecipeRegistry.SimpleOnRecipeId);

        var baseDocument = new SequenceDocument(Guid.NewGuid(), Revision: 1, Effects: []);
        var scope = FullScope(window, arches); // Tree is not in scope

        var compiler = NewCompiler();
        Assert.Throws<CompositionInvalidException>(() => compiler.Compile(baseDocument, [candidate], scope));
    }

    [Fact]
    public void Compile_CandidateWindowOutsideScopeTime_Throws_NegativePath()
    {
        var arches = new ModelId("Arches");
        var candidate = new ChoreographyCandidate(
            Guid.NewGuid(), Assignment(MusicalRole.Lead, "T-Piano", PrecedenceTier.Manual, 1), arches, new TimeWindow(0, 2_000_000), EffectRecipeRegistry.SimpleOnRecipeId);

        var baseDocument = new SequenceDocument(Guid.NewGuid(), Revision: 1, Effects: []);
        var scope = FullScope(new TimeWindow(0, 1_000_000), arches); // scope ends before the candidate does

        var compiler = NewCompiler();
        Assert.Throws<CompositionInvalidException>(() => compiler.Compile(baseDocument, [candidate], scope));
    }

    [Fact]
    public void Compile_DuplicateCandidateId_Throws_NegativePath()
    {
        var arches = new ModelId("Arches");
        var window = new TimeWindow(0, 1_000_000);
        var id = Guid.NewGuid();
        var candidateA = new ChoreographyCandidate(id, Assignment(MusicalRole.Lead, "T-Piano", PrecedenceTier.Manual, 1), arches, window, EffectRecipeRegistry.SimpleOnRecipeId);
        var candidateB = new ChoreographyCandidate(id, Assignment(MusicalRole.Bass, "T-Bass", PrecedenceTier.Automated, 0), arches, window, EffectRecipeRegistry.SimpleOnRecipeId);

        var baseDocument = new SequenceDocument(Guid.NewGuid(), Revision: 1, Effects: []);
        var scope = FullScope(window, arches);

        var compiler = NewCompiler();
        Assert.Throws<CompositionInvalidException>(() => compiler.Compile(baseDocument, [candidateA, candidateB], scope));
    }

    [Theory]
    [InlineData(-1, 100)] // negative start
    [InlineData(100, 100)] // zero-length
    [InlineData(100, 0)] // reversed
    public void ChoreographyCandidate_Validate_RejectsInvalidWindow_NegativePath(long start, long end)
    {
        var candidate = new ChoreographyCandidate(
            Guid.NewGuid(), Assignment(MusicalRole.Lead, "T-Piano", PrecedenceTier.Manual, 1), new ModelId("Arches"), new TimeWindow(start, end), EffectRecipeRegistry.SimpleOnRecipeId);

        Assert.Throws<CompositionInvalidException>(candidate.Validate);
    }

    [Fact]
    public void ChoreographyCandidate_Validate_RejectsBlankRecipeId_NegativePath()
    {
        var candidate = new ChoreographyCandidate(
            Guid.NewGuid(), Assignment(MusicalRole.Lead, "T-Piano", PrecedenceTier.Manual, 1), new ModelId("Arches"), new TimeWindow(0, 100), "   ");

        Assert.Throws<CompositionInvalidException>(candidate.Validate);
    }

    [Fact]
    public void ChoreographyCandidate_Validate_RejectsBlankTarget_NegativePath()
    {
        var candidate = new ChoreographyCandidate(
            Guid.NewGuid(), Assignment(MusicalRole.Lead, "T-Piano", PrecedenceTier.Manual, 1), new ModelId("   "), new TimeWindow(0, 100), EffectRecipeRegistry.SimpleOnRecipeId);

        Assert.Throws<CompositionInvalidException>(candidate.Validate);
    }

    [Fact]
    public void ChoreographyCandidate_Validate_RejectsEmptyId_NegativePath()
    {
        var candidate = new ChoreographyCandidate(
            Guid.Empty, Assignment(MusicalRole.Lead, "T-Piano", PrecedenceTier.Manual, 1), new ModelId("Arches"), new TimeWindow(0, 100), EffectRecipeRegistry.SimpleOnRecipeId);

        Assert.Throws<CompositionInvalidException>(candidate.Validate);
    }

    [Fact]
    public void Compile_NullArguments_Throw_NegativePath()
    {
        var compiler = NewCompiler();
        var baseDocument = new SequenceDocument(Guid.NewGuid(), Revision: 1, Effects: []);
        var scope = FullScope(new TimeWindow(0, 1), new ModelId("Arches"));
        var candidates = new List<ChoreographyCandidate>
        {
            new(Guid.NewGuid(), Assignment(MusicalRole.Lead, "T-Piano", PrecedenceTier.Manual, 1), new ModelId("Arches"), new TimeWindow(0, 1), EffectRecipeRegistry.SimpleOnRecipeId),
        };

        Assert.Throws<ArgumentNullException>(() => compiler.Compile(null!, candidates, scope));
        Assert.Throws<ArgumentNullException>(() => compiler.Compile(baseDocument, null!, scope));
        Assert.Throws<ArgumentNullException>(() => compiler.Compile(baseDocument, candidates, null!));
    }
}
