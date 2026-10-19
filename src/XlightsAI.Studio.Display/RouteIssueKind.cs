namespace XlightsAI.Studio.Display;

/// <summary>
/// PHASE-09 Step 065's own implementation guidance, verbatim: "Validate missing/locked prop, effect
/// support, path intersections and overlaps." The named kinds <see cref="IRouteValidator.Validate"/>
/// produces. Deliberately string constants, not an enum -- the same convention <see cref="PathTraversalKind"/>/
/// <see cref="RoleTag"/> already established, so a later step can add a new kind without a breaking
/// contract change here.
/// </summary>
public static class RouteIssueKind
{
    /// <summary>A path references a node id that does not exist in the supplied display graph. <see cref="RouteIssueSeverity.Error"/>.</summary>
    public const string MissingNode = "MissingNode";

    /// <summary>A path references a node id that is explicitly locked/excluded via <see cref="LockedNodeSet"/>. <see cref="RouteIssueSeverity.Error"/>.</summary>
    public const string LockedNode = "LockedNode";

    /// <summary>A path references a node whose <see cref="DisplayNode.ModelKind"/> does not declare support for the recipe being validated against, per <see cref="EffectRecipeSupport"/>. <see cref="RouteIssueSeverity.Error"/>.</summary>
    public const string IncompatibleRecipe = "IncompatibleRecipe";

    /// <summary>Two non-adjacent segments of the path's own ordered route geometrically cross each other. <see cref="RouteIssueSeverity.Warning"/>.</summary>
    public const string PathIntersection = "PathIntersection";

    /// <summary>Two non-adjacent nodes in the path occupy the exact same normalized preview position. <see cref="RouteIssueSeverity.Warning"/>.</summary>
    public const string PathOverlap = "PathOverlap";

    /// <summary>
    /// The <see cref="SpatialPath"/> being validated no longer matches the live, currently-persisted path
    /// with the same id -- this step's own required title text, verbatim: "conflicting-write checks."
    /// <see cref="RouteIssueSeverity.Error"/>; thrown as <see cref="SpatialDisplayGraphInvalidException"/>
    /// by <see cref="IRouteValidationCatalog"/> rather than returned in a report, since a stale route must
    /// never even reach geometry validation (see <see cref="RouteValidationCatalog"/> remarks).
    /// </summary>
    public const string StaleRoute = "StaleRoute";

    /// <summary>Every route issue kind this validator currently recognizes.</summary>
    public static readonly IReadOnlyList<string> All = [MissingNode, LockedNode, IncompatibleRecipe, PathIntersection, PathOverlap, StaleRoute];
}
