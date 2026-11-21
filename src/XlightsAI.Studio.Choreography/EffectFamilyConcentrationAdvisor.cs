using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography;

/// <summary>
/// Build-plan Phase 30 step 30.4 (effect-family concentration advisory): "per section, no single effect
/// family... accounts for more than 50% of placed layers unless the section's declared intent
/// (<c>Look.Intent</c>) explicitly calls for sustained reliance on that family. Reported as an advisory...
/// not a hard validator failure." A sibling pass alongside <see cref="SectionTimelineValidator"/> -- same
/// "pure, read-only measurement over an already-compiled <see cref="ChoreographyIR"/>" shape as
/// <see cref="EmphasisContrastPlanner.Measure"/>, reusing <see cref="EffectFamilyIndex"/> (the real
/// corpus-mined grouping; see its own doc comment for why nothing else was guessed) rather than
/// inventing a second family notion. Never called automatically by <see cref="ChoreographyEngine"/>/
/// <see cref="SectionTimelineCompiler"/> -- opt-in, same discipline as <see cref="LayerBlendPlanner"/>.
/// </summary>
/// <param name="SectionId">The section this finding is about.</param>
/// <param name="LookId">The Look compiled for this section -- carried so a caller can look up its own <see cref="Look.Intent"/> without re-deriving it.</param>
/// <param name="Family">The dominant real family id (<see cref="EffectFamilyIndex.FamilyOf"/>) this section's placed layers lean on.</param>
/// <param name="FamilyLayerCount">How many of this section's real placed layers resolved to <see cref="Family"/>.</param>
/// <param name="TotalLayerCount">How many of this section's real placed layers resolved to any family at all (a layer whose recipe id does not resolve to a known xLights effect name is excluded, never guessed).</param>
public sealed record EffectFamilyConcentrationFinding(string SectionId, string LookId, string Family, int FamilyLayerCount, int TotalLayerCount)
{
    public double Share => TotalLayerCount == 0 ? 0.0 : FamilyLayerCount / (double)TotalLayerCount;
}

public static class EffectFamilyConcentrationAdvisor
{
    /// <summary>Build-plan Phase 30 step 30.4's own acceptance definition: "more than 50%."</summary>
    public const double ConcentrationThreshold = 0.5;

    /// <summary>
    /// Measures this one section's own real placed layers (every <see cref="EffectInstance"/> in
    /// <paramref name="ir"/> whose window falls inside <paramref name="sectionWindow"/>) and reports a
    /// finding when one real family's own share exceeds <see cref="ConcentrationThreshold"/> -- unless
    /// <paramref name="look"/>'s own declared <see cref="Look.Intent"/> already names that family
    /// (case-insensitive substring match against the family's own real effect-name id -- the family id IS
    /// a real xLights effect name per <see cref="EffectFamilyIndex"/>'s own doc comment, never an invented
    /// label, so this match is checking for a real, literal word the Director/user actually wrote, not
    /// fuzzy-guessing intent). Never throws, never mutates <paramref name="ir"/> -- advisory only, exactly
    /// like <see cref="EmphasisContrastPlanner.Measure"/>.
    /// </summary>
    public static IReadOnlyList<EffectFamilyConcentrationFinding> Measure(
        ChoreographyIR ir, TimeWindow sectionWindow, string sectionId, Look look,
        EffectRecipeCatalog recipeCatalog, EffectFamilyIndex familyIndex)
    {
        ArgumentNullException.ThrowIfNull(ir);
        ArgumentNullException.ThrowIfNull(sectionId);
        ArgumentNullException.ThrowIfNull(look);
        ArgumentNullException.ThrowIfNull(recipeCatalog);
        ArgumentNullException.ThrowIfNull(familyIndex);

        var familiesByLayer = ir.Effects
            .Where(e => e.StartTicks < sectionWindow.EndTicks && e.EndTicks > sectionWindow.StartTicks)
            .Select(e => ResolveFamily(e, recipeCatalog, familyIndex))
            .Where(f => f is not null)
            .Select(f => f!)
            .ToList();

        if (familiesByLayer.Count == 0)
        {
            return [];
        }

        var findings = new List<EffectFamilyConcentrationFinding>();
        foreach (var group in familiesByLayer.GroupBy(f => f, StringComparer.Ordinal))
        {
            var finding = new EffectFamilyConcentrationFinding(sectionId, look.Id, group.Key, group.Count(), familiesByLayer.Count);
            if (finding.Share > ConcentrationThreshold && !DeclaresSustainedFamilyFocus(look, finding.Family))
            {
                findings.Add(finding);
            }
        }

        return findings;
    }

    /// <summary>
    /// "unless the section's declared intent (<c>Look.Intent</c>) explicitly calls for sustained reliance
    /// on that family" -- a real, literal, case-insensitive mention of the family's own real effect-name id
    /// anywhere in the Look's own declared <see cref="Look.Intent"/> prose counts as that explicit call-out;
    /// never inferred from energy ceiling, motion, or any other field.
    /// </summary>
    private static bool DeclaresSustainedFamilyFocus(Look look, string family) =>
        !string.IsNullOrWhiteSpace(look.Intent) &&
        look.Intent.Contains(family, StringComparison.OrdinalIgnoreCase);

    private static string? ResolveFamily(EffectInstance effect, EffectRecipeCatalog recipeCatalog, EffectFamilyIndex familyIndex)
    {
        var recipe = recipeCatalog.Recipes.FirstOrDefault(r => string.Equals(r.RecipeId, effect.RecipeId, StringComparison.Ordinal));
        return recipe is null ? null : familyIndex.FamilyOf(recipe.XlightsEffectName);
    }
}
