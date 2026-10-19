namespace XlightsAI.Studio.Display;

/// <summary>
/// PHASE-09 Step 064: the named boundary-handling policies <see cref="ISpatialGestureResolver.Resolve"/>
/// accepts via <see cref="ResolutionSettings.BoundaryPolicy"/> -- this step's own required acceptance text,
/// verbatim: "Ascending/descending tests preserve contour and reach boundaries safely." Deliberately a
/// distinct type name from the <see cref="ResolutionSettings.BoundaryPolicy"/> property it configures (the
/// same relationship <see cref="DirectionPolicy"/> already has to <c>ISpatialPathSuggester.Suggest</c>'s own
/// <c>directionPolicy</c> parameter), to avoid any reader confusion between "the type naming every known
/// policy" and "the one policy value a given settings instance carries."
/// </summary>
public static class GestureBoundaryPolicy
{
    /// <summary>
    /// A move past the path's own last/first position holds at that edge -- the same semantics
    /// <see cref="Melody.MelodicGestureMapper.RelativeStepClampPolicy"/> already established for Phase 07's
    /// own effect-recipe-coupled walk. The default (<see cref="ResolutionSettings.Default"/>).
    /// </summary>
    public const string Clamp = "Clamp";

    /// <summary>
    /// A move past the path's own last/first position instead visits every intermediate node up to that
    /// edge (<see cref="PathTraversalKind.Sweep"/>), dividing the gesture's own duration evenly across them,
    /// rather than jumping straight to a single clamped node.
    /// </summary>
    public const string Sweep = "Sweep";

    /// <summary>
    /// A move past the path's own last/first position reflects the overshoot back into the path and
    /// reverses the walk's own direction from that point onward (<see cref="PathTraversalKind.Reverse"/>) --
    /// a boustrophedon/ping-pong traversal that can bounce more than once for a single large overshoot.
    /// </summary>
    public const string Reverse = "Reverse";

    /// <summary>Every boundary policy this resolver currently recognizes -- used only to produce a clear rejection message; never consumed for branching logic elsewhere.</summary>
    public static readonly IReadOnlyList<string> All = [Clamp, Sweep, Reverse];
}
