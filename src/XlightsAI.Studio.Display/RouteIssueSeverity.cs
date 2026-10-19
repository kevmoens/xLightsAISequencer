namespace XlightsAI.Studio.Display;

/// <summary>
/// PHASE-09 Step 065 ("Add route validation, conflicting-write checks and incompatible prop fallback"):
/// how strongly <see cref="IRouteValidator"/> treats a given <see cref="RouteIssue"/>. Deliberately string
/// constants, not an enum, the same convention <see cref="RouteIssueKind"/>/<see cref="RoleTag"/>/
/// <see cref="GestureBoundaryPolicy"/> already established.
/// </summary>
public static class RouteIssueSeverity
{
    /// <summary>
    /// Blocks the route from being used to generate an XSQ effect -- this step's own required acceptance,
    /// verbatim: "Invalid path cannot silently produce mis-targeted XSQ effects." A node this step cannot
    /// verify is a safe, correct target (missing, locked, or a declared-incompatible recipe) is always
    /// <see cref="Error"/>, never silently downgraded.
    /// </summary>
    public const string Error = "Error";

    /// <summary>
    /// Informational only -- does not block route usage. A geometric self-intersection/overlap is still an
    /// artistically valid route (e.g. a deliberate figure-eight), so it is surfaced for the user's own
    /// judgment rather than rejected outright (Section 2: "produce a warning and fallback rather than
    /// silent mutation").
    /// </summary>
    public const string Warning = "Warning";

    /// <summary>Every severity this validator currently recognizes.</summary>
    public static readonly IReadOnlyList<string> All = [Error, Warning];
}
