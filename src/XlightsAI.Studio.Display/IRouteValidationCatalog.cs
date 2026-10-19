using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Display;

/// <summary>
/// PHASE-09 Step 065's own application-orchestration boundary: validates a <see cref="SpatialPath"/> via
/// <see cref="IRouteValidator"/>, then persists the result as a rebuildable <see cref="RouteValidationReport"/>
/// cache entry -- the same "validate before any load/write is attempted" boundary
/// <see cref="IGesturePreviewCatalog"/> already establishes.
///
/// <para>
/// <b>Conflicting-write check (this step's own required title text, verbatim):</b> before running any
/// geometry/policy check, <see cref="ValidateAsync"/> looks for a path with the same <see cref="SpatialPath.Id"/>
/// already attached to <paramref name="display"/>'s own <see cref="SpatialDisplayGraph.Paths"/> (the live,
/// currently-persisted ordering a caller attaches via <c>graph with { Paths = pathSet.Paths }</c>, the same
/// convention <see cref="ISpatialPathCatalog"/>'s own remarks already document). When one exists at a
/// <em>different</em> <see cref="SpatialPath.Version"/> than <paramref name="path"/>, the call is rejected
/// outright -- a caller validating an in-memory path copy that another edit (e.g. a concurrent
/// reorder/reverse) has since superseded can never reach geometry validation or produce a report, before a
/// stale route can be used to generate an XSQ effect. This step's own required acceptance text, verbatim:
/// "Invalid path cannot silently produce mis-targeted XSQ effects." A caller that never attaches a live path
/// set to <see cref="SpatialDisplayGraph.Paths"/> (e.g. validating a brand-new, not-yet-saved path) has
/// nothing to conflict with and is never rejected on this basis alone.
/// </para>
/// </summary>
public interface IRouteValidationCatalog
{
    /// <exception cref="SpatialDisplayGraphInvalidException">
    /// A path with the same id is attached to <paramref name="display"/>'s own <see cref="SpatialDisplayGraph.Paths"/>
    /// at a different version than <paramref name="path"/> (see remarks), or <see cref="IRouteValidator.Validate"/>
    /// itself rejects the input. Thrown before any cache file is written.
    /// </exception>
    Task<RouteValidationReport> ValidateAsync(
        SpatialDisplayGraph display,
        SpatialPath path,
        LockedNodeSet lockedNodes,
        string? recipeId,
        EffectRecipeSupport recipeSupport,
        CancellationToken ct);
}
