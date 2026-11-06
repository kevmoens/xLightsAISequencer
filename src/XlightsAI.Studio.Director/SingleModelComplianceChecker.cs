using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Director;

/// <summary>
/// Build-plan "richer analysis tools" Phase 8 (2026-11-02): the post-compile guard rail for
/// <see cref="SingleModelDirectiveParser"/>'s own extracted requests -- same "detect violation -&gt; derive
/// RevisionDirective -&gt; regenerate" pattern as <see cref="TimedCueComplianceChecker"/>/
/// <see cref="ChoreographyLegalityValidator.RuleModelCoverage"/>. A requested model is only flagged when it
/// was reached SOLELY through a group-targeted effect (never targeted directly by its own name anywhere in
/// the compiled show) -- a model targeted both individually and via a group is already compliant with "not
/// (only) a group," and a model never targeted at all is <see cref="ChoreographyLegalityValidator.RuleModelCoverage"/>'s
/// own, separate concern, not this one's.
/// </summary>
public static class SingleModelComplianceChecker
{
    public static IReadOnlyList<ModelId> FindGroupedViolations(
        ChoreographyIR ir, IReadOnlyList<ModelId> requiredIndividualModels, DisplayGraph layout)
    {
        ArgumentNullException.ThrowIfNull(ir);
        ArgumentNullException.ThrowIfNull(requiredIndividualModels);
        ArgumentNullException.ThrowIfNull(layout);

        if (requiredIndividualModels.Count == 0)
        {
            return [];
        }

        var violations = new List<ModelId>();
        foreach (var modelId in requiredIndividualModels)
        {
            var targetedDirectly = ir.Effects.Any(e => e.Target == modelId);
            if (targetedDirectly)
            {
                continue;
            }

            var targetedOnlyViaGroup = ir.Effects.Any(e => GroupContains(e.Target.Value, modelId, layout));
            if (targetedOnlyViaGroup)
            {
                violations.Add(modelId);
            }
        }

        return violations;
    }

    private static bool GroupContains(string targetName, ModelId modelId, DisplayGraph layout)
    {
        if (!PropGroupResolver.TryGetRealGroupName(targetName, layout, out _))
        {
            return false;
        }

        try
        {
            return PropGroupResolver.Resolve(targetName, layout).Contains(modelId);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
