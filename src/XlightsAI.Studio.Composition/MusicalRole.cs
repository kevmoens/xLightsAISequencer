namespace XlightsAI.Studio.Composition;

/// <summary>
/// PHASE-10 Step 066 ("Define semantic musical roles and event precedence rules"): the semantic musical
/// role a given source track plays for a scope (phrase). Declared exactly as PHASE-10.md Section 3's own
/// proposed signature names it, verbatim, including declaration order -- that order is itself this step's
/// own baseline role-precedence tie-break (<see cref="RolePrecedenceResolver"/>): <see cref="Lead"/> is
/// the most prominent voice, <see cref="Atmosphere"/> the least.
/// </summary>
public enum MusicalRole
{
    Lead,
    Rhythm,
    Bass,
    Harmony,
    Accent,
    Atmosphere,
}
