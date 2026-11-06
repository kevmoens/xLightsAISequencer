using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Director.Tests;

/// <summary>Build-plan "richer analysis tools" Phase 8 (2026-11-02): <see cref="SingleModelComplianceChecker"/>.</summary>
public sealed class SingleModelComplianceCheckerTests
{
    private static DisplayGraph BuildLayout() => new(
        "TEST-HASH",
        Models:
        [
            new DisplayModel(new ModelId("Arch 1"), "Arch 1", "Arch", [], [], NodeCount: 50),
            new DisplayModel(new ModelId("Tree"), "Tree", "Tree", [], [], NodeCount: 900),
        ],
        Groups: [new DisplayModelGroup("Arches", [new DisplayGroupMember("Arch 1", DisplayGroupMemberKind.Model)])],
        Warnings: []);

    private static EffectInstance BuildEffect(string target) =>
        new(Guid.NewGuid(), new ModelId(target), 0, 0, 1000, "recipe", "1", new Dictionary<string, string>());

    [Fact]
    public void FindGroupedViolations_ModelOnlyReachedViaGroup_FlagsViolation()
    {
        var layout = BuildLayout();
        var ir = new ChoreographyIR([], [BuildEffect("Arches")], []);

        var violations = SingleModelComplianceChecker.FindGroupedViolations(ir, [new ModelId("Arch 1")], layout);

        Assert.Equal([new ModelId("Arch 1")], violations);
    }

    [Fact]
    public void FindGroupedViolations_ModelTargetedDirectly_NoViolation()
    {
        var layout = BuildLayout();
        var ir = new ChoreographyIR([], [BuildEffect("Arch 1"), BuildEffect("Arches")], []);

        var violations = SingleModelComplianceChecker.FindGroupedViolations(ir, [new ModelId("Arch 1")], layout);

        Assert.Empty(violations);
    }

    [Fact]
    public void FindGroupedViolations_ModelNeverTargetedAtAll_NoViolation()
    {
        // Zero coverage at all is ChoreographyLegalityValidator.RuleModelCoverage's own concern, not this one's.
        var layout = BuildLayout();
        var ir = new ChoreographyIR([], [BuildEffect("Tree")], []);

        var violations = SingleModelComplianceChecker.FindGroupedViolations(ir, [new ModelId("Arch 1")], layout);

        Assert.Empty(violations);
    }

    [Fact]
    public void FindGroupedViolations_NoRequiredModels_ReturnsEmpty()
    {
        var layout = BuildLayout();
        var ir = new ChoreographyIR([], [BuildEffect("Arches")], []);

        Assert.Empty(SingleModelComplianceChecker.FindGroupedViolations(ir, [], layout));
    }
}
