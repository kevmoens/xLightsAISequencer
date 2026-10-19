namespace XlightsAI.Studio.Display;

/// <summary>
/// PHASE-09 Section 3's own proposed signature, verbatim: the declared, user-visible policy
/// <see cref="ISpatialGestureResolver.Resolve"/> uses to turn a <see cref="Melody.MelodyTrack"/>'s gestures
/// into a concrete walk along a <see cref="SpatialPath"/> (the same "never a silent default buried in code"
/// convention <see cref="Melody.GestureMapSettings"/> already established for Phase 07's own mapper).
/// </summary>
/// <param name="BoundaryPolicy">Which named <see cref="GestureBoundaryPolicy"/> value governs a move whose raw target exceeds the path's own bounds. Any other value is rejected -- this resolver never invents an unverified policy.</param>
/// <param name="SkipUnavailable">
/// When <see langword="true"/>, a <see cref="SpatialPath.OrderedModels"/> entry that no longer exists in
/// <see cref="SpatialDisplayGraph.Nodes"/> (e.g. a renamed/removed model since the path was last saved) is
/// silently excluded from the walk rather than rejected outright. When <see langword="false"/> (the
/// default), any such missing node rejects the whole resolution before any <see cref="ResolvedGesture"/> is
/// produced. Does not itself model a "locked" or "incompatible recipe" prop -- Phase 09 Section 2's own
/// "missing props, locked/excluded props and incompatible effect recipes" fallback is Step 065's own later
/// scope ("route validation, conflicting-write checks and incompatible prop fallback"); this flag only
/// covers the narrower "the node is simply gone from the graph" case this step's resolver can already see.
/// </param>
public sealed record ResolutionSettings(string BoundaryPolicy, bool SkipUnavailable)
{
    /// <summary><see cref="GestureBoundaryPolicy.Clamp"/>, <see cref="SkipUnavailable"/> <see langword="false"/> -- the conservative, never-surprising default.</summary>
    public static ResolutionSettings Default { get; } = new(GestureBoundaryPolicy.Clamp, SkipUnavailable: false);
}
