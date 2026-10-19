namespace XlightsAI.Studio.Display;

/// <summary>
/// PHASE-09 Step 064's own required "User-visible outcome: Saved audio/analysis artifact with timestamp
/// provenance plus a synchronized waveform/timeline demonstration" -- the persisted, rebuildable output of
/// one <see cref="ISpatialGestureResolver.Resolve"/> run. A pure analysis cache (Section 4: "Analysis caches
/// may be rebuilt if their source/model/version hash changes"), never a user-accepted document -- unlike
/// <see cref="SpatialPathSet"/>/<see cref="RoleAssignmentSet"/> there is no conflict policy or history to
/// maintain here, because re-resolving the same inputs is always safe and always produces the same output
/// deterministically; a newer save simply replaces the cache file for that exact input combination.
/// <see cref="Gestures"/>' own <see cref="ResolvedGesture.StartTicks"/>/<see cref="ResolvedGesture.EndTicks"/>
/// are the "timestamp provenance" this step's own required text names -- every tick traces back to the
/// exact source audio-origin tick of the <see cref="Melody.MelodyTrack"/> note/gesture it was resolved
/// from, never a wall-clock generation timestamp (Section 2: "internally represent position with an
/// explicit canonical monotonic clock tied to source audio origin").
/// </summary>
/// <param name="SourceLayoutHash">The exact <see cref="SpatialDisplayGraph.SourceLayoutHash"/> this preview was resolved against.</param>
/// <param name="PathId">Identity of the <see cref="SpatialPath"/> this preview walks.</param>
/// <param name="PathVersion">The exact <see cref="SpatialPath.Version"/> resolved -- a cache from a stale path version is never silently reused across a path edit.</param>
/// <param name="MelodyTrackId">Identity of the <see cref="Melody.MelodyTrack"/> this preview was resolved from.</param>
/// <param name="BoundaryPolicy">The exact <see cref="ResolutionSettings.BoundaryPolicy"/> used -- part of this cache's own key, since the same gestures resolve differently under a different policy.</param>
/// <param name="Gestures">The real, ordered output of <see cref="ISpatialGestureResolver.Resolve"/> for this exact input combination.</param>
/// <param name="VersionId">Identity of this particular resolve run -- changes every time this preview is recomputed, even if numerically identical, the same provenance convention <see cref="SpatialDisplayGraph.VersionId"/> already established.</param>
public sealed record ResolvedGesturePreview(
    string SourceLayoutHash,
    Guid PathId,
    int PathVersion,
    Guid MelodyTrackId,
    string BoundaryPolicy,
    IReadOnlyList<ResolvedGesture> Gestures,
    Guid VersionId);
