using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Display;

/// <summary>
/// PHASE-09 Step 062 ("Define named custom paths and semantic spatial motions"): builds a brand-new,
/// unsaved <see cref="SpatialPath"/> (<see cref="SpatialPath.Version"/> always <c>0</c>, a fresh
/// <see cref="SpatialPath.Id"/>) by ordering a caller-chosen subset of a <see cref="SpatialDisplayGraph"/>'s
/// own nodes according to a named <see cref="DirectionPolicy"/> -- Core process flowchart's own "Suggest
/// candidate spatial paths" step, made concrete and named rather than left as a bare ordering function.
/// Pure and side-effect-free; never touches disk or <see cref="ISpatialPathCatalog"/>'s own persisted
/// state.
/// </summary>
public interface ISpatialPathSuggester
{
    /// <exception cref="SpatialDisplayGraphInvalidException">
    /// <paramref name="candidateNodeIds"/> is empty, references a node id missing from <paramref name="graph"/>,
    /// lists a node id more than once, references a node whose geometry could not be resolved (the
    /// <c>"NoGeometry"</c> tag -- ordering by position is meaningless without it), or
    /// <paramref name="directionPolicy"/> is not one of <see cref="DirectionPolicy.All"/>. Thrown before
    /// any ordering is computed and before any store write is attempted (this step's own required
    /// failure/edge case: "Invalid input... must not corrupt the project.").
    /// </exception>
    SpatialPath Suggest(SpatialDisplayGraph graph, string name, IReadOnlyList<ModelId> candidateNodeIds,
        string directionPolicy, bool isClosed);
}
