namespace XlightsAI.Studio.Display;

/// <summary>
/// PHASE-09 Step 064 ("Map pitch intervals/movement to jumps, holds and path traversal"): the fixed
/// vocabulary of <see cref="ResolvedGesture.GestureKind"/> values this step's own implementation guidance
/// names verbatim -- "Resolve step, leap, hold, sweep and reverse against path length." Deliberately string
/// constants, not an enum, the same convention <see cref="SpatialEdge.Relation"/>/<see cref="DirectionPolicy"/>/
/// <see cref="RoleTag"/> already established ("not an enum so later steps can add new kinds without a
/// breaking contract change").
/// </summary>
public static class PathTraversalKind
{
    /// <summary>A single-position move along the path (<see cref="Melody.PitchMotion.StepUp"/>/<see cref="Melody.PitchMotion.StepDown"/>), within path bounds.</summary>
    public const string Step = "Step";

    /// <summary>A multi-position move along the path (<see cref="Melody.PitchMotion.LeapUp"/>/<see cref="Melody.PitchMotion.LeapDown"/>), within path bounds.</summary>
    public const string Leap = "Leap";

    /// <summary>No movement -- the walk pulses or sustains at its current position (<see cref="Melody.PitchMotion.Repeat"/>/<see cref="Melody.PitchMotion.Hold"/>/<see cref="Melody.PitchMotion.Rest"/>).</summary>
    public const string Hold = "Hold";

    /// <summary>
    /// A move whose raw target exceeds the path's own bounds, resolved under <see cref="GestureBoundaryPolicy.Sweep"/>
    /// into a continuous traversal of every intermediate node up to the boundary, instead of a single clamped jump.
    /// </summary>
    public const string Sweep = "Sweep";

    /// <summary>
    /// A move whose raw target exceeds the path's own bounds, resolved under <see cref="GestureBoundaryPolicy.Reverse"/>
    /// by reflecting the overshoot back into the path and reversing the walk's own direction from that point onward.
    /// </summary>
    public const string Reverse = "Reverse";

    /// <summary>Every gesture kind this resolver currently recognizes -- used only to produce a clear rejection message; never consumed for branching logic elsewhere.</summary>
    public static readonly IReadOnlyList<string> All = [Step, Leap, Hold, Sweep, Reverse];
}
