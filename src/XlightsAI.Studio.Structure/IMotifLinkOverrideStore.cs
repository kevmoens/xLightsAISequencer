using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Structure;

/// <summary>
/// Persistence boundary for user-entered manual motif link/unlink decisions, keyed by <see cref="SongId"/>.
/// Deliberately a <em>separate</em> store from <see cref="IMotifClusteringStore"/> (Section 4: "Persist
/// detected structure and user-edited structure in separately versioned stores") -- the automatic
/// clustering cache can be freely rebuilt (a new <see cref="MotifClusteringSettings.RecipeId"/>, a changed
/// upstream phrase segmentation, ...) without ever touching or losing a human's accepted link/unlink
/// decision here. <see cref="MotifGroupOverrideApplier.Apply"/> merges this store's own output onto
/// whichever automatic <see cref="MotifClusteringAnalysis.Links"/> is current, at read time -- the same
/// "applied at read time, not baked into the cached automatic analysis" convention <see cref="PhraseSegmenter"/>
/// already established for <see cref="IPhraseBoundaryOverrideStore"/>.
/// </summary>
public interface IMotifLinkOverrideStore
{
    /// <summary>The deterministic on-disk path the link-override set for <paramref name="songId"/> would be saved at, whether or not any override has been recorded yet.</summary>
    string GetPath(SongId songId);

    /// <summary>Reads back every persisted link/unlink decision for <paramref name="songId"/>. Returns an empty list, never throws, when no override file exists yet.</summary>
    /// <exception cref="MotifClusteringCorruptException">A file exists at <see cref="GetPath"/> but is not validly serialized.</exception>
    Task<IReadOnlyList<MotifLinkOverride>> LoadAsync(SongId songId, CancellationToken ct);

    /// <summary>Persists the full link/unlink decision set for <paramref name="songId"/> atomically, replacing whatever was previously stored.</summary>
    Task SaveAsync(SongId songId, IReadOnlyList<MotifLinkOverride> overrides, CancellationToken ct);
}
