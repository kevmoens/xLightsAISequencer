using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Xlights.Tests;

/// <summary>
/// PHASE-02 Step 012 — "Create effect plan containing three hardcoded simple effects on selected
/// real props." Required acceptance: "Plan is serialized and inspectable independently of XSQ."
/// Failure/edge case: "Malformed proposal, no-improvement and exhausted retry budget must not
/// auto-commit." These tests exercise the real <see cref="ThreeEffectPlanFactory"/>,
/// <see cref="EffectRecipeRegistry"/> and <see cref="JsonSequenceDocumentStore"/> against a real,
/// already-validated <see cref="DisplayGraph"/> (built the same way <see cref="ShowLayoutImporter"/>
/// would, from the gate demo's own three real props), and real file-system I/O — not mocks.
/// </summary>
[Trait("Category", "Integration")]
public sealed class Step012AcceptanceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-step012-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>
    /// The exact gate-demo targets (PHASE-02 Section 1: "Import a sample layout with Roofline, Arch
    /// 1 and Arch 2"). Asserts the full, real domain -> persistence pathway: exactly three effects,
    /// each on a real distinct prop, covering [0, duration) with no gap/overlap, and — the required
    /// acceptance itself — that the resulting plan can be saved and read back from disk, fully
    /// independent of any XSQ/xLights type.
    /// </summary>
    [Fact]
    public async Task Step012_Acceptance_Passes_PlanIsSerializedAndInspectableIndependentlyOfXsq()
    {
        var layout = GateDemoLayout();
        var registry = new EffectRecipeRegistry();
        var factory = new ThreeEffectPlanFactory();
        const long songDurationTicks = 300_000_000; // 30s at 100ns/tick.

        var plan = factory.CreatePlan(layout, ["Roofline", "Arch 1", "Arch 2"], songDurationTicks, registry);

        Assert.Equal(1, plan.Revision);
        Assert.Equal(3, plan.Effects.Count);
        Assert.Equal(["Roofline", "Arch 1", "Arch 2"], plan.Effects.Select(e => e.Target.Value));
        Assert.All(plan.Effects, e => Assert.Equal(EffectRecipeRegistry.SimpleOnRecipeId, e.RecipeId));
        Assert.All(plan.Effects, e => Assert.Equal(EffectRecipeRegistry.SimpleOnRecipeVersion, e.RecipeVersion));
        Assert.All(plan.Effects, e => Assert.True(e.EndTicks > e.StartTicks));

        // Half-open [start, end) windows tile [0, duration) exactly: no gap, no overlap, no overrun.
        Assert.Equal(0, plan.Effects[0].StartTicks);
        Assert.Equal(plan.Effects[0].EndTicks, plan.Effects[1].StartTicks);
        Assert.Equal(plan.Effects[1].EndTicks, plan.Effects[2].StartTicks);
        Assert.Equal(songDurationTicks, plan.Effects[2].EndTicks);

        // Serialized and inspectable independently of XSQ: round-trip through a real JSON file on
        // disk, read back by a fresh store instance, with no XSQ/XML type involved anywhere.
        var store = new JsonSequenceDocumentStore(_root);
        var path = await store.SaveAsync(plan, CancellationToken.None);

        Assert.True(File.Exists(path));
        var rawJson = await File.ReadAllTextAsync(path);
        Assert.Contains("\"RecipeId\"", rawJson);
        Assert.DoesNotContain("xsq", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<", rawJson, StringComparison.Ordinal); // never XML.

        var reloaded = await store.LoadAsync(path, CancellationToken.None);
        Assert.Equal(plan.Id, reloaded.Id);
        Assert.Equal(plan.Revision, reloaded.Revision);
        Assert.Equal(plan.Effects.Count, reloaded.Effects.Count);
        for (var i = 0; i < plan.Effects.Count; i++)
        {
            AssertEffectsEquivalent(plan.Effects[i], reloaded.Effects[i]);
        }
    }

    /// <summary>
    /// Field-by-field comparison rather than record <c>Equals</c>: <see cref="EffectInstance.Parameters"/>
    /// is an <see cref="IReadOnlyDictionary{TKey,TValue}"/>, and <see cref="Dictionary{TKey,TValue}"/>
    /// (the concrete type both the factory and JSON deserialization produce) does not override
    /// <c>Equals</c>/<c>GetHashCode</c> by content — two distinct dictionary instances with identical
    /// entries are never reference-equal, so record equality alone would wrongly fail a correct
    /// round-trip. The dictionary's own *content* is compared explicitly here instead.
    /// </summary>
    private static void AssertEffectsEquivalent(EffectInstance expected, EffectInstance actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Target, actual.Target);
        Assert.Equal(expected.Layer, actual.Layer);
        Assert.Equal(expected.StartTicks, actual.StartTicks);
        Assert.Equal(expected.EndTicks, actual.EndTicks);
        Assert.Equal(expected.RecipeId, actual.RecipeId);
        Assert.Equal(expected.RecipeVersion, actual.RecipeVersion);
        Assert.Equal(expected.Parameters, actual.Parameters);
    }

    /// <summary>A plan may equally target a real model group (Step 011's own targetable entity), not only individual models.</summary>
    [Fact]
    public async Task Plan_MayTargetARealModelGroup_NotOnlyIndividualModels()
    {
        var layout = GateDemoLayout();
        var registry = new EffectRecipeRegistry();
        var factory = new ThreeEffectPlanFactory();

        var plan = factory.CreatePlan(layout, ["Roofline", "Arch 1", "Front-Arches"], 300_000_000, registry);

        Assert.Equal(new ModelId("Front-Arches"), plan.Effects[2].Target);
        var store = new JsonSequenceDocumentStore(_root);
        var path = await store.SaveAsync(plan, CancellationToken.None);
        var reloaded = await store.LoadAsync(path, CancellationToken.None);
        Assert.Equal(plan.Id, reloaded.Id);
        Assert.Equal(plan.Revision, reloaded.Revision);
        for (var i = 0; i < plan.Effects.Count; i++)
        {
            AssertEffectsEquivalent(plan.Effects[i], reloaded.Effects[i]);
        }
    }

    [Fact]
    public void UnknownTargetName_FailsSafely_DoesNotCreateAPlan_NegativePath()
    {
        var layout = GateDemoLayout();
        var registry = new EffectRecipeRegistry();
        var factory = new ThreeEffectPlanFactory();

        var ex = Assert.Throws<EffectPlanInvalidException>(
            () => factory.CreatePlan(layout, ["Roofline", "Arch 1", "Ghost-Prop"], 300_000_000, registry));

        Assert.Contains("Ghost-Prop", ex.Message);
    }

    [Fact]
    public void WrongNumberOfTargets_FailsSafely_NegativePath()
    {
        var layout = GateDemoLayout();
        var registry = new EffectRecipeRegistry();
        var factory = new ThreeEffectPlanFactory();

        Assert.Throws<EffectPlanInvalidException>(
            () => factory.CreatePlan(layout, ["Roofline", "Arch 1"], 300_000_000, registry));
        Assert.Throws<EffectPlanInvalidException>(
            () => factory.CreatePlan(layout, ["Roofline", "Arch 1", "Arch 2", "Arch 2"], 300_000_000, registry));
    }

    [Fact]
    public void DuplicateTargetName_FailsSafely_NegativePath()
    {
        var layout = GateDemoLayout();
        var registry = new EffectRecipeRegistry();
        var factory = new ThreeEffectPlanFactory();

        var ex = Assert.Throws<EffectPlanInvalidException>(
            () => factory.CreatePlan(layout, ["Roofline", "Roofline", "Arch 1"], 300_000_000, registry));

        Assert.Contains("distinct", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveSongDuration_FailsSafely_NegativePath(long invalidDuration)
    {
        var layout = GateDemoLayout();
        var registry = new EffectRecipeRegistry();
        var factory = new ThreeEffectPlanFactory();

        Assert.Throws<EffectPlanInvalidException>(
            () => factory.CreatePlan(layout, ["Roofline", "Arch 1", "Arch 2"], invalidDuration, registry));
    }

    [Fact]
    public void UnknownRecipeId_FailsSafely_NegativePath()
    {
        var registry = new EffectRecipeRegistry();

        var ex = Assert.Throws<EffectPlanInvalidException>(
            () => registry.CreateVerified("effect.sparkle.v1", new ModelId("Roofline"), 0, 100));

        Assert.Contains("effect.sparkle.v1", ex.Message);
    }

    [Theory]
    [InlineData(100, 100)]
    [InlineData(100, 50)]
    public void InvalidEffectTimeWindow_FailsSafely_NegativePath(long start, long end)
    {
        var registry = new EffectRecipeRegistry();

        Assert.Throws<EffectPlanInvalidException>(
            () => registry.CreateVerified(EffectRecipeRegistry.SimpleOnRecipeId, new ModelId("Roofline"), start, end));
    }

    /// <summary>Loading a plan file that was never written must fail safely, never return a silently-empty document.</summary>
    [Fact]
    public async Task LoadingMissingPlanFile_FailsSafely_NegativePath()
    {
        var store = new JsonSequenceDocumentStore(_root);
        var missingPath = Path.Combine(_root, "sequence-plan-does-not-exist.json");

        await Assert.ThrowsAsync<FileNotFoundException>(() => store.LoadAsync(missingPath, CancellationToken.None));
    }

    /// <summary>A corrupt plan file fails safely rather than silently returning a null/default document.</summary>
    [Fact]
    public async Task LoadingCorruptPlanFile_FailsSafely_NegativePath()
    {
        Directory.CreateDirectory(_root);
        var corruptPath = Path.Combine(_root, "sequence-plan-corrupt.json");
        await File.WriteAllTextAsync(corruptPath, "{ not valid json");

        var store = new JsonSequenceDocumentStore(_root);
        await Assert.ThrowsAsync<SequenceDocumentCorruptException>(() => store.LoadAsync(corruptPath, CancellationToken.None));
    }

    /// <summary>Builds the gate demo's own layout in-memory (Roofline, Arch 1, Arch 2, plus a model group), matching PHASE-02 Section 1's exact phase-gate prop names.</summary>
    private static DisplayGraph GateDemoLayout() => new(
        SourceLayoutHash: "TEST-HASH-NOT-A-REAL-FILE",
        Models:
        [
            new DisplayModel(new ModelId("Roofline"), "Roofline", "Star", ["Front-Arches"], []),
            new DisplayModel(new ModelId("Arch 1"), "Arch 1", "Arches", ["Front-Arches"], []),
            new DisplayModel(new ModelId("Arch 2"), "Arch 2", "Arches", ["Front-Arches"], []),
        ],
        Groups:
        [
            new DisplayModelGroup("Front-Arches",
            [
                new DisplayGroupMember("Roofline", DisplayGroupMemberKind.Model),
                new DisplayGroupMember("Arch 1", DisplayGroupMemberKind.Model),
                new DisplayGroupMember("Arch 2", DisplayGroupMemberKind.Model),
            ]),
        ],
        Warnings: []);
}
