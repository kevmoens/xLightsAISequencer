using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// Persistence boundary for a built <see cref="BeatGrid"/>. Mirrors <see cref="ITempoAnalysisStore"/>'s
/// own save/load contract and atomic-write guarantee, including <see cref="GetPath"/>'s deterministic
/// naming — <see cref="IBeatGridBuilder"/> must be able to compute a song's cache path *before*
/// deciding whether to reopen an existing grid or rebuild.
/// </summary>
public interface IBeatGridStore
{
    /// <summary>The deterministic on-disk path a beat grid for <paramref name="songId"/> would be saved at, whether or not it has been built yet.</summary>
    string GetPath(SongId songId);

    /// <summary>Persists <paramref name="grid"/> atomically at <see cref="GetPath"/> and returns that path.</summary>
    Task<string> SaveAsync(BeatGrid grid, CancellationToken ct);

    /// <summary>Reads back a previously saved grid from <paramref name="path"/> for independent inspection.</summary>
    /// <exception cref="FileNotFoundException"><paramref name="path"/> does not exist.</exception>
    /// <exception cref="BeatGridCorruptException"><paramref name="path"/> exists but is not a valid serialized <see cref="BeatGrid"/>.</exception>
    Task<BeatGrid> LoadAsync(string path, CancellationToken ct);
}
