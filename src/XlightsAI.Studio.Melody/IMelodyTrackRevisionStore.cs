namespace XlightsAI.Studio.Melody;

/// <summary>
/// Persistence boundary for a <see cref="MelodyTrackRevisionState"/> — this step's own required
/// acceptance, verbatim: "User corrections persist and regenerate only dependent phrases." Keyed by
/// <see cref="MelodyTrackRevisionState.TrackId"/> (same stable identity as
/// <see cref="IMelodyTrackStore.GetPath"/>'s own <c>Guid trackId</c>, kept distinct from it: this
/// store's own artifact additionally carries <see cref="MelodyTrackRevisionState.Original"/>/
/// <see cref="MelodyTrackRevisionState.Revision"/>/<see cref="MelodyTrackRevisionState.LockedNoteIds"/>,
/// which a bare <see cref="MelodyTrack"/> JSON file has no field for).
/// </summary>
public interface IMelodyTrackRevisionStore
{
    /// <summary>The deterministic on-disk path a revision state for <paramref name="trackId"/> would be saved at, whether or not it has been built yet.</summary>
    string GetPath(Guid trackId);

    /// <summary>Persists <paramref name="state"/> atomically at <see cref="GetPath"/> and returns that path.</summary>
    Task<string> SaveAsync(MelodyTrackRevisionState state, CancellationToken ct);

    /// <summary>Loads the currently persisted revision state for <paramref name="trackId"/>, or <see langword="null"/> when none has ever been saved (never created implicitly).</summary>
    Task<MelodyTrackRevisionState?> LoadAsync(Guid trackId, CancellationToken ct);
}
