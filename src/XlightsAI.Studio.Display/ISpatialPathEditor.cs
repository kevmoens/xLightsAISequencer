using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Display;

/// <summary>
/// PHASE-09 Section 3's own proposed contract, implemented for the first time in Step 062 ("Define named
/// custom paths and semantic spatial motions") -- no step before 062 is permitted to construct a
/// <see cref="SpatialPath"/> at all (see <c>SpatialPath.cs</c>'s own doc comment). Both methods are pure
/// and side-effect-free (persistence is <see cref="ISpatialPathCatalog"/>'s own separate concern, the
/// same split Step 061 already established between <see cref="DisplayLayoutProjection"/> (pure) and
/// <see cref="IDisplayLayoutEditor"/> (persisted)); each returns a new <see cref="SpatialPath"/> with the
/// same <see cref="SpatialPath.Id"/>/<see cref="SpatialPath.Name"/>/<see cref="SpatialPath.IsClosed"/> but
/// an incremented <see cref="SpatialPath.Version"/> -- this step's own implementation guidance: "path
/// history is versioned."
/// </summary>
public interface ISpatialPathEditor
{
    /// <exception cref="SpatialDisplayGraphInvalidException"><paramref name="newOrder"/> is not an exact permutation of <paramref name="oldPath"/>'s own <see cref="SpatialPath.OrderedModels"/> (missing, duplicate or foreign id) -- this step's own required failure/edge case: "Invalid input... must not corrupt the project."</exception>
    SpatialPath Reorder(SpatialPath oldPath, IReadOnlyList<ModelId> newOrder);

    /// <summary>Returns <paramref name="oldPath"/> with <see cref="SpatialPath.OrderedModels"/> reversed end-to-end and <see cref="SpatialPath.Version"/> incremented.</summary>
    SpatialPath Reverse(SpatialPath oldPath);
}
