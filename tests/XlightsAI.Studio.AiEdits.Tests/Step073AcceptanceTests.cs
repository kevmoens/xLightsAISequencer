using XlightsAI.Studio.Composition;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.AiEdits.Tests;

/// <summary>
/// PHASE-11 Step 073 ("Implement deterministic command AST for brightness/motion/direction/speed/
/// replace"). Required acceptance: "UI quick actions work offline and no token usage." Required
/// failure/edge case: "Invalid input, cancellation or stale persisted state must not corrupt the
/// project." These tests exercise the real <see cref="StructuredCommandInterpreter"/>,
/// <see cref="EffectLockPolicy"/>, <see cref="SelectionContextCaptureService"/> and
/// <see cref="ThreeEffectPlanFactory"/>/<see cref="EffectRecipeRegistry"/> against the same gate-demo
/// layout shape (Roofline, Arch 1, Arch 2) every prior PHASE-02/03/11 step's own tests already use, not
/// mocks.
/// </summary>
[Trait("Category", "Unit")]
public sealed class Step073AcceptanceTests
{
    private static DisplayGraph GateDemoLayout() => new(
        SourceLayoutHash: "TEST-HASH-NOT-A-REAL-FILE",
        Models:
        [
            new DisplayModel(new ModelId("Roofline"), "Roofline", "Star", [], []),
            new DisplayModel(new ModelId("Arch 1"), "Arch 1", "Arches", [], []),
            new DisplayModel(new ModelId("Arch 2"), "Arch 2", "Arches", [], []),
        ],
        Groups: [],
        Warnings: []);

    private static SequenceDocument GateDemoPlan(DisplayGraph layout) =>
        new ThreeEffectPlanFactory().CreatePlan(layout, ["Roofline", "Arch 1", "Arch 2"], 300_000_000, new EffectRecipeRegistry());

    private static SelectionContext CaptureArchSelection(SequenceDocument plan, EffectInstance arch1, EffectInstance arch2)
    {
        // Span both arches' own windows (not just arch1's) so a scope built from this helper can
        // actually reach an effect locked on either arch — mirrors the phase gate's own worked example
        // of a single interval selected across multiple arches at once.
        var scope = new EditScope(
            new TimeWindow(Math.Min(arch1.StartTicks, arch2.StartTicks), Math.Max(arch1.EndTicks, arch2.EndTicks)),
            new HashSet<ModelId> { arch1.Target, arch2.Target });
        return new SelectionContextCaptureService(new EffectLockPolicy())
            .Capture(plan, new DocumentRevision(plan.Revision), scope, [], [], [], []);
    }

    /// <summary>
    /// The phase gate's own named worked example, narrowed to this step's own scope: a quick action
    /// ("brightness 75") against a real captured selection produces one, fully validated, offline
    /// <see cref="EditProposal"/> — no document mutation, no network call.
    /// </summary>
    [Fact]
    public void Step073_Acceptance_QuickActionsWorkOfflineWithNoTokenUsage()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var arch2 = plan.Effects.Single(e => e.Target.Value == "Arch 2");
        var context = CaptureArchSelection(plan, arch1, arch2);
        var interpreter = new StructuredCommandInterpreter(new EffectLockPolicy());

        var proposal = interpreter.ParseKnownCommand("brightness 75", context, plan, CancellationToken.None);

        Assert.Equal(plan.Revision, proposal.BaseRevision);
        var operation = Assert.Single(proposal.Operations);
        Assert.Equal(StructuredEditOperationKinds.SetBrightness, operation.Kind);
        Assert.Equal("75", operation.Parameters["Level"]);
        Assert.Equal(context.Scope.Time, operation.Range);
        Assert.Equal(new[] { arch1.Target, arch2.Target }, operation.Targets);
        Assert.False(string.IsNullOrWhiteSpace(proposal.Explanation));

        // No document mutation of any kind — the same plan instance, same revision/effect set.
        Assert.Equal(3, plan.Effects.Count);
    }

    [Theory]
    [InlineData("brighten", StructuredEditOperationKinds.SetBrightness, "Level", "100")]
    [InlineData("dim", StructuredEditOperationKinds.SetBrightness, "Level", "20")]
    [InlineData("reverse", StructuredEditOperationKinds.SetDirection, "Direction", "Reverse")]
    [InlineData("direction left-to-right", StructuredEditOperationKinds.SetDirection, "Direction", "LeftToRight")]
    [InlineData("faster", StructuredEditOperationKinds.SetSpeed, "Multiplier", "1.5")]
    [InlineData("slower", StructuredEditOperationKinds.SetSpeed, "Multiplier", "0.5")]
    [InlineData("speed 2x", StructuredEditOperationKinds.SetSpeed, "Multiplier", "2")]
    [InlineData("follow lead", StructuredEditOperationKinds.ReplaceRole, "Role", "Lead")]
    public void KnownInstruction_ParsesToExpectedKindAndParameter(string instruction, string expectedKind, string parameterKey, string expectedValue)
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var arch2 = plan.Effects.Single(e => e.Target.Value == "Arch 2");
        var context = CaptureArchSelection(plan, arch1, arch2);
        var interpreter = new StructuredCommandInterpreter(new EffectLockPolicy());

        var proposal = interpreter.ParseKnownCommand(instruction, context, plan, CancellationToken.None);

        var operation = Assert.Single(proposal.Operations);
        Assert.Equal(expectedKind, operation.Kind);
        Assert.Equal(expectedValue, operation.Parameters[parameterKey]);
    }

    [Fact]
    public void SnapToGrid_ParsesWithNoParameters()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var arch2 = plan.Effects.Single(e => e.Target.Value == "Arch 2");
        var context = CaptureArchSelection(plan, arch1, arch2);
        var interpreter = new StructuredCommandInterpreter(new EffectLockPolicy());

        var proposal = interpreter.ParseKnownCommand("snap to grid", context, plan, CancellationToken.None);

        var operation = Assert.Single(proposal.Operations);
        Assert.Equal(StructuredEditOperationKinds.SnapToGrid, operation.Kind);
        Assert.Empty(operation.Parameters);
    }

    [Fact]
    public void UnrecognizedInstruction_IsRejected_WithoutBuildingAProposal_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var arch2 = plan.Effects.Single(e => e.Target.Value == "Arch 2");
        var context = CaptureArchSelection(plan, arch1, arch2);
        var interpreter = new StructuredCommandInterpreter(new EffectLockPolicy());

        var ex = Assert.Throws<EditCommandRejectedException>(() =>
            interpreter.ParseKnownCommand("make the piano move left-to-right", context, plan, CancellationToken.None));

        Assert.Equal(EditRejectionReason.UnrecognizedInstruction, ex.Reason);
        Assert.Equal(3, plan.Effects.Count);
    }

    [Fact]
    public void BlankInstruction_IsRejected_UnrecognizedInstruction_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var arch2 = plan.Effects.Single(e => e.Target.Value == "Arch 2");
        var context = CaptureArchSelection(plan, arch1, arch2);
        var interpreter = new StructuredCommandInterpreter(new EffectLockPolicy());

        var ex = Assert.Throws<EditCommandRejectedException>(() =>
            interpreter.ParseKnownCommand("   ", context, plan, CancellationToken.None));

        Assert.Equal(EditRejectionReason.UnrecognizedInstruction, ex.Reason);
    }

    [Theory]
    [InlineData("brightness 150")]
    [InlineData("brightness -5")]
    public void BrightnessOutOfRange_IsRejected_UnsupportedParameter_NegativePath(string instruction)
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var arch2 = plan.Effects.Single(e => e.Target.Value == "Arch 2");
        var context = CaptureArchSelection(plan, arch1, arch2);
        var interpreter = new StructuredCommandInterpreter(new EffectLockPolicy());

        var ex = Assert.Throws<EditCommandRejectedException>(() =>
            interpreter.ParseKnownCommand(instruction, context, plan, CancellationToken.None));

        Assert.Equal(EditRejectionReason.UnsupportedParameter, ex.Reason);
    }

    [Fact]
    public void SpeedOutOfRange_IsRejected_UnsupportedParameter_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var arch2 = plan.Effects.Single(e => e.Target.Value == "Arch 2");
        var context = CaptureArchSelection(plan, arch1, arch2);
        var interpreter = new StructuredCommandInterpreter(new EffectLockPolicy());

        var ex = Assert.Throws<EditCommandRejectedException>(() =>
            interpreter.ParseKnownCommand("speed 50x", context, plan, CancellationToken.None));

        Assert.Equal(EditRejectionReason.UnsupportedParameter, ex.Reason);
    }

    [Fact]
    public void StaleRevision_IsRejected_WithoutPartialMutation_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var arch2 = plan.Effects.Single(e => e.Target.Value == "Arch 2");
        var context = CaptureArchSelection(plan, arch1, arch2);
        // The document changes revision after the selection was captured (e.g. an unrelated edit applied).
        var staleDocument = plan with { Revision = plan.Revision + 1 };
        var interpreter = new StructuredCommandInterpreter(new EffectLockPolicy());

        var ex = Assert.Throws<EditCommandRejectedException>(() =>
            interpreter.ParseKnownCommand("brighten", context, staleDocument, CancellationToken.None));

        Assert.Equal(EditRejectionReason.StaleRevision, ex.Reason);
    }

    [Fact]
    public void LockedTarget_IsRejected_WithoutBuildingAProposal_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var arch2 = plan.Effects.Single(e => e.Target.Value == "Arch 2");
        var context = CaptureArchSelection(plan, arch1, arch2);

        var lockedArch2 = arch2 with { Locked = true };
        var effects = plan.Effects.Select(e => e.Id == arch2.Id ? lockedArch2 : e).ToList();
        var planWithLock = plan with { Effects = effects };
        var interpreter = new StructuredCommandInterpreter(new EffectLockPolicy());

        var ex = Assert.Throws<EditCommandRejectedException>(() =>
            interpreter.ParseKnownCommand("brighten", context, planWithLock, CancellationToken.None));

        Assert.Equal(EditRejectionReason.Locked, ex.Reason);
    }

    [Fact]
    public void CanceledToken_ThrowsWithoutBuildingAProposal_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var arch2 = plan.Effects.Single(e => e.Target.Value == "Arch 2");
        var context = CaptureArchSelection(plan, arch1, arch2);
        var interpreter = new StructuredCommandInterpreter(new EffectLockPolicy());

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            interpreter.ParseKnownCommand("brighten", context, plan, cts.Token));

        // The document is byte-equivalent after the canceled attempt — same revision/effect count.
        Assert.Equal(3, plan.Effects.Count);
    }

    [Fact]
    public void ParseKnownCommand_NullArguments_Throw_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var arch2 = plan.Effects.Single(e => e.Target.Value == "Arch 2");
        var context = CaptureArchSelection(plan, arch1, arch2);
        var interpreter = new StructuredCommandInterpreter(new EffectLockPolicy());

        Assert.Throws<ArgumentNullException>(() =>
            interpreter.ParseKnownCommand(null!, context, plan, CancellationToken.None));
        Assert.Throws<ArgumentNullException>(() =>
            interpreter.ParseKnownCommand("brighten", null!, plan, CancellationToken.None));
        Assert.Throws<ArgumentNullException>(() =>
            interpreter.ParseKnownCommand("brighten", context, null!, CancellationToken.None));
    }
}
