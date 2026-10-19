using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Structure;

/// <summary>
/// Persistence boundary for user-entered <see cref="SectionKind"/> overrides, keyed by the stable
/// <see cref="SectionLabelCandidate.BoundaryId"/> they target. Deliberately a <em>separate</em> store from
/// <see cref="ISectionLabelStore"/> (Section 4: "Persist detected structure and user-edited structure in
/// separately versioned stores") -- an automatic-classification cache can be freely rebuilt or invalidated
/// (upstream re-analysis, a new <see cref="SectionLabelSettings.RecipeId"/>, ...) without ever touching or
/// losing a human's accepted correction here. This is this step's own required acceptance, verbatim:
/// "overrides survive restart."
/// </summary>
public interface ISectionLabelOverrideStore
{
    /// <summary>The deterministic on-disk path the override set for <paramref name="songId"/> would be saved at, whether or not any override has been recorded yet.</summary>
    string GetPath(SongId songId);

    /// <summary>Reads back every persisted override for <paramref name="songId"/>, keyed by <see cref="SectionLabelCandidate.BoundaryId"/>. Returns an empty map, never throws, when no override file exists yet.</summary>
    /// <exception cref="SectionLabelCorruptException">A file exists at <see cref="GetPath"/> but is not validly serialized.</exception>
    Task<IReadOnlyDictionary<Guid, SectionKind>> LoadAsync(SongId songId, CancellationToken ct);

    /// <summary>Persists the full override map for <paramref name="songId"/> atomically, replacing whatever was previously stored.</summary>
    Task SaveAsync(SongId songId, IReadOnlyDictionary<Guid, SectionKind> overrides, CancellationToken ct);
}
