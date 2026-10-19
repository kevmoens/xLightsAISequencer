namespace XlightsAI.Studio.Melody;

/// <summary>
/// Persistence boundary for a validated <see cref="MelodyTrack"/>. Mirrors
/// <c>XlightsAI.Studio.Stems.IStemActivityResultStore</c>'s own save/load contract and atomic-write
/// guarantee -- this step's own required acceptance, verbatim: "Fixture JSON round-trips with units and
/// source IDs."
/// </summary>
public interface IMelodyTrackStore
{
    /// <summary>The deterministic on-disk path a track for <paramref name="trackId"/> would be saved at, whether or not it has been built yet.</summary>
    string GetPath(Guid trackId);

    /// <summary>Persists <paramref name="track"/> atomically at <see cref="GetPath"/> and returns that path.</summary>
    Task<string> SaveAsync(MelodyTrack track, CancellationToken ct);

    /// <summary>Reads back a previously saved track from <paramref name="path"/> for independent inspection.</summary>
    /// <exception cref="FileNotFoundException"><paramref name="path"/> does not exist.</exception>
    /// <exception cref="MelodyTrackCorruptException"><paramref name="path"/> exists but is not a valid serialized <see cref="MelodyTrack"/>.</exception>
    Task<MelodyTrack> LoadAsync(string path, CancellationToken ct);
}
