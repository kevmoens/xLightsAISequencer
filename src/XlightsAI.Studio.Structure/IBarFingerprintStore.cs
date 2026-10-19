using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Structure;

/// <summary>
/// Persistence boundary for a built <see cref="SongFingerprintSet"/>. Mirrors
/// <see cref="XlightsAI.Studio.Rhythm.IBeatGridStore"/>'s own save/load contract and atomic-write
/// guarantee, including <see cref="GetPath"/>'s deterministic naming -- <see cref="IBarFingerprintBuilder"/>
/// must be able to compute a song's cache path *before* deciding whether to reopen an existing set or
/// rebuild (Step 052's own "with caching" implementation guidance).
/// </summary>
public interface IBarFingerprintStore
{
    /// <summary>The deterministic on-disk path a fingerprint set for <paramref name="songId"/> would be saved at, whether or not it has been built yet.</summary>
    string GetPath(SongId songId);

    /// <summary>Persists <paramref name="set"/> atomically at <see cref="GetPath"/> and returns that path.</summary>
    Task<string> SaveAsync(SongFingerprintSet set, CancellationToken ct);

    /// <summary>Reads back a previously saved set from <paramref name="path"/> for independent inspection.</summary>
    /// <exception cref="FileNotFoundException"><paramref name="path"/> does not exist.</exception>
    /// <exception cref="BarFingerprintCorruptException"><paramref name="path"/> exists but is not a valid serialized <see cref="SongFingerprintSet"/>.</exception>
    Task<SongFingerprintSet> LoadAsync(string path, CancellationToken ct);
}
