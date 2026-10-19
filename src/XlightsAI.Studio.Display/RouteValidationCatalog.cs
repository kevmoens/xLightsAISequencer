using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Display;

/// <summary>Default <see cref="IRouteValidationCatalog"/>. See the interface remarks.</summary>
public sealed class RouteValidationCatalog : IRouteValidationCatalog
{
    private readonly IRouteValidationReportStore _store;
    private readonly IRouteValidator _validator;

    public RouteValidationCatalog(IRouteValidationReportStore store, IRouteValidator validator)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(validator);
        _store = store;
        _validator = validator;
    }

    public async Task<RouteValidationReport> ValidateAsync(
        SpatialDisplayGraph display,
        SpatialPath path,
        LockedNodeSet lockedNodes,
        string? recipeId,
        EffectRecipeSupport recipeSupport,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(display);
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(lockedNodes);
        ArgumentNullException.ThrowIfNull(recipeSupport);

        ct.ThrowIfCancellationRequested();

        // Conflicting-write check: a live path attached to the graph at a different version than the one
        // being validated means some other edit has already superseded it -- reject before any geometry
        // check or cache write, rather than silently validating (and potentially passing) a stale route.
        var live = display.Paths.FirstOrDefault(p => p.Id == path.Id);
        if (live is not null && live.Version != path.Version)
        {
            throw new SpatialDisplayGraphInvalidException(
                $"Path '{path.Name}' (version {path.Version}) no longer matches the live version {live.Version} for the same id; reload the path before validating.");
        }

        // Throws SpatialDisplayGraphInvalidException synchronously, before any cache file is touched.
        var report = _validator.Validate(path, display, lockedNodes, recipeId, recipeSupport);

        ct.ThrowIfCancellationRequested();

        await _store.SaveAsync(report, recipeId, ct).ConfigureAwait(false);
        return report;
    }
}
