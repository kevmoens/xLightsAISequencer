using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Structure;

/// <summary>
/// Persistence boundary for user-entered manual phrase boundary corrections, keyed by the stable
/// <see cref="SectionLabelCandidate.Id"/> they target. Deliberately a <em>separate</em> store from
/// <see cref="IPhraseSegmentationStore"/> (Section 4: "Persist detected structure and user-edited
/// structure in separately versioned stores") -- an automatic-segmentation cache can be freely rebuilt or
/// invalidated (upstream re-analysis, a new <see cref="PhraseSegmentationSettings.RecipeId"/>, ...) without
/// ever touching or losing a human's accepted correction here. Each entry is the full, replacement list of
/// internal phrase-start bar indices for one section (strictly between that section's own start and end
/// bars) -- an empty list is a valid, explicit "no manual split points; one phrase spans the whole
/// section" correction, distinct from no entry at all ("defer entirely to the automatic recipe").
/// </summary>
public interface IPhraseBoundaryOverrideStore
{
    /// <summary>The deterministic on-disk path the boundary override set for <paramref name="songId"/> would be saved at, whether or not any override has been recorded yet.</summary>
    string GetPath(SongId songId);

    /// <summary>Reads back every persisted override for <paramref name="songId"/>, keyed by <see cref="SectionLabelCandidate.Id"/>. Returns an empty map, never throws, when no override file exists yet.</summary>
    /// <exception cref="PhraseSegmentationCorruptException">A file exists at <see cref="GetPath"/> but is not validly serialized.</exception>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<int>>> LoadAsync(SongId songId, CancellationToken ct);

    /// <summary>Persists the full override map for <paramref name="songId"/> atomically, replacing whatever was previously stored.</summary>
    Task SaveAsync(SongId songId, IReadOnlyDictionary<Guid, IReadOnlyList<int>> overrides, CancellationToken ct);
}
