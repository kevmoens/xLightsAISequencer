namespace XlightsAI.Studio.Knowledge;

/// <summary>
/// Build-plan Phase 19 §2c: the single definition of "does this prop/model/group name read as a
/// Tree or Matrix-class prop" -- originally <c>ShowBibleValidator.IsTreeOrMatrixName</c> (Director-only),
/// moved down here so <see cref="XlightsAI.Studio.Pipeline.CraftRuleInducer"/> (which cannot reference
/// Director -- the dependency runs the other way) can share the exact same convention rather than
/// re-guessing it, instead of duplicating the name-matching rule in a second place.
/// </summary>
public static class TreeMatrixPropNameConvention
{
    public static bool IsTreeOrMatrixName(string name) =>
        name.Contains("Tree", StringComparison.OrdinalIgnoreCase) || name.Contains("Matrix", StringComparison.OrdinalIgnoreCase);
}
