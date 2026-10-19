namespace XlightsAI.Studio.Display;

/// <summary>
/// PHASE-09 Step 065's own required "User-visible outcome: Versioned, inspectable project output plus a
/// reproducible test report" -- the persisted, rebuildable output of one <see cref="IRouteValidator.Validate"/>
/// run. A pure analysis cache, the same rationale <see cref="ResolvedGesturePreview"/> already established:
/// re-validating the same inputs is always safe and deterministic, so there is no conflict policy/history to
/// maintain here.
/// </summary>
/// <param name="SourceLayoutHash">The exact <see cref="SpatialDisplayGraph.SourceLayoutHash"/> this report was validated against.</param>
/// <param name="PathId">Identity of the <see cref="SpatialPath"/> validated.</param>
/// <param name="PathVersion">The exact <see cref="SpatialPath.Version"/> validated -- a cache from a stale path version is never silently reused across a path edit.</param>
/// <param name="Issues">Every <see cref="RouteIssue"/> found, in no particular guaranteed order.</param>
/// <param name="VersionId">Identity of this particular validation run -- changes every time this report is recomputed, even if numerically identical, the same provenance convention <see cref="SpatialDisplayGraph.VersionId"/>/<see cref="ResolvedGesturePreview.VersionId"/> already established.</param>
public sealed record RouteValidationReport(
    string SourceLayoutHash,
    Guid PathId,
    int PathVersion,
    IReadOnlyList<RouteIssue> Issues,
    Guid VersionId)
{
    /// <summary>
    /// <see langword="true"/> when at least one <see cref="RouteIssue"/> is <see cref="RouteIssueSeverity.Error"/>
    /// -- this step's own required acceptance, verbatim: "Invalid path cannot silently produce mis-targeted
    /// XSQ effects." A caller preparing to generate XSQ effects from this path must check this flag first.
    /// </summary>
    public bool HasBlockingIssues => Issues.Any(i => i.Severity == RouteIssueSeverity.Error);
}
