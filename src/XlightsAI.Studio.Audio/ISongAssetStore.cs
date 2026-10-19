namespace XlightsAI.Studio.Audio;

/// <summary>
/// PHASE-04 Section 4 ("Project stores song file identity, original path/optional copied asset,
/// hash, codec probe, origin offset...") persistence boundary for a probed <see cref="SongAsset"/> —
/// the "Saved audio/analysis artifact with timestamp provenance" Step 024 user-visible outcome.
/// Mirrors <c>ISequenceDocumentStore</c>'s own save/load contract and atomic-write guarantee.
/// </summary>
public interface ISongAssetStore
{
    /// <summary>Persists <paramref name="asset"/> atomically and returns the real on-disk path it was written to.</summary>
    Task<string> SaveAsync(SongAsset asset, CancellationToken ct);

    /// <summary>Reads back a previously saved asset record from <paramref name="path"/> for independent inspection.</summary>
    /// <exception cref="FileNotFoundException"><paramref name="path"/> does not exist.</exception>
    /// <exception cref="SongAssetCorruptException"><paramref name="path"/> exists but is not a valid serialized <see cref="SongAsset"/>.</exception>
    Task<SongAsset> LoadAsync(string path, CancellationToken ct);
}
