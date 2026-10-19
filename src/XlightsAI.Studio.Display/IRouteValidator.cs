namespace XlightsAI.Studio.Display;

/// <summary>
/// PHASE-09 Step 065 ("Add route validation, conflicting-write checks and incompatible prop fallback").
/// Section 2's own instruction, verbatim: "Route resolution should honor missing props, locked/excluded
/// props and incompatible effect recipes; produce a warning and fallback rather than silent mutation." This
/// validator is the single place that instruction is implemented: a pure, synchronous geometry/policy check
/// over an already-built <see cref="SpatialDisplayGraph"/> and <see cref="SpatialPath"/>, independent of any
/// <see cref="ISpatialGestureResolver"/> run or XSQ export -- a caller runs this (or the persisting
/// <see cref="IRouteValidationCatalog"/>) before generating XSQ effects from a path, exactly the required
/// acceptance text, verbatim: "Invalid path cannot silently produce mis-targeted XSQ effects."
/// </summary>
public interface IRouteValidator
{
    /// <exception cref="SpatialDisplayGraphInvalidException">
    /// <paramref name="path"/> has no ordered models, or <paramref name="lockedNodes"/> was authored
    /// against a different layout than <paramref name="display"/> (a stale/mismatched input must be
    /// rejected outright rather than silently validated against the wrong layout). Thrown before any
    /// <see cref="RouteIssue"/> is produced.
    /// </exception>
    RouteValidationReport Validate(
        SpatialPath path,
        SpatialDisplayGraph display,
        LockedNodeSet lockedNodes,
        string? recipeId,
        EffectRecipeSupport recipeSupport);
}
