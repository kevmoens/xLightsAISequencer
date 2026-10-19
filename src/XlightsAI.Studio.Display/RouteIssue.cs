using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Display;

/// <summary>
/// PHASE-09 Step 065: one concrete problem <see cref="IRouteValidator.Validate"/> found with a
/// <see cref="SpatialPath"/>, reported rather than silently fixed or ignored (Section 2: "produce a
/// warning and fallback rather than silent mutation").
/// </summary>
/// <param name="NodeId">The node this issue concerns, or <see langword="null"/> for an issue that is not about one specific node (none currently produced, reserved for a future kind).</param>
/// <param name="Kind">One of <see cref="RouteIssueKind"/>'s named values.</param>
/// <param name="Severity">One of <see cref="RouteIssueSeverity"/>'s named values.</param>
/// <param name="Message">A human-readable explanation, safe to surface directly in SCREEN 05/12.</param>
public sealed record RouteIssue(ModelId? NodeId, string Kind, string Severity, string Message);
