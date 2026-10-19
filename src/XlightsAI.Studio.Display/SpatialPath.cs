using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Display;

/// <summary>
/// PHASE-09 Section 3 proposed contract: a named, user-ordered route through <see cref="DisplayNode"/>
/// ids. Declared now (Section 3's own full signature) because <see cref="SpatialDisplayGraph.Paths"/>
/// must exist as a field from Step 059 onward, but no step before 062 ("Define named custom paths and
/// semantic spatial motions") ever constructs one -- <see cref="IDisplayGraphBuilder.Build"/> always
/// returns an empty <see cref="SpatialDisplayGraph.Paths"/> list.
/// </summary>
public sealed record SpatialPath(Guid Id, string Name, IReadOnlyList<ModelId> OrderedModels,
    bool IsClosed, int Version);
