using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Audio;

/// <summary>
/// PHASE-04 Step 026 persistence boundary for a built <see cref="WaveformPyramidCache"/>. Mirrors
/// <see cref="ISongAssetStore"/>'s own save/load contract and atomic-write guarantee, extended with
/// <see cref="GetPath"/>: unlike a song asset (whose saved path is only known after a successful
/// save), <see cref="WaveformPyramidBuilder"/> must be able to compute a song's cache path *before*
/// deciding whether to reopen an existing cache or rebuild — the deterministic naming this exposes
/// is what makes "cache reopens without recompute" (Step 026 required acceptance) possible without a
/// separate index/manifest file.
/// </summary>
public interface IWaveformPyramidStore
{
    /// <summary>The deterministic on-disk path a pyramid for <paramref name="songId"/> would be saved at, whether or not it has been built yet.</summary>
    string GetPath(SongId songId);

    /// <summary>Persists <paramref name="cache"/> atomically at <see cref="GetPath"/> and returns that path.</summary>
    Task<string> SaveAsync(WaveformPyramidCache cache, CancellationToken ct);

    /// <summary>Reads back a previously saved pyramid cache from <paramref name="path"/> for independent inspection.</summary>
    /// <exception cref="FileNotFoundException"><paramref name="path"/> does not exist.</exception>
    /// <exception cref="WaveformPyramidCorruptException"><paramref name="path"/> exists but is not a valid serialized <see cref="WaveformPyramidCache"/>.</exception>
    Task<WaveformPyramidCache> LoadAsync(string path, CancellationToken ct);
}
