using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Melody;

namespace XlightsAI.Studio.Display;

/// <summary>
/// Application-orchestration boundary for Step 064: validates and resolves a <see cref="MelodyTrack"/>
/// against a <see cref="SpatialPath"/> via <see cref="ISpatialGestureResolver"/>, then persists the result
/// as a rebuildable <see cref="ResolvedGesturePreview"/> cache entry -- the same "validate before any
/// load/write is attempted" boundary <see cref="IRoleAssignmentCatalog"/>/<see cref="ISpatialPathCatalog"/>
/// already establish.
/// </summary>
public interface IGesturePreviewCatalog
{
    /// <exception cref="SpatialDisplayGraphInvalidException">See <see cref="ISpatialGestureResolver.Resolve"/>. Thrown before any cache file is written.</exception>
    Task<ResolvedGesturePreview> ResolveAsync(
        SpatialDisplayGraph display,
        SpatialPath path,
        MelodyTrack track,
        TimeWindow window,
        ResolutionSettings settings,
        CancellationToken ct);
}
