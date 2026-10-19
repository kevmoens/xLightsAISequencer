using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// Persistence boundary for a built <see cref="EnergyEnvelope"/>. Mirrors <c>IWaveformPyramidStore</c>'s
/// own save/load contract and atomic-write guarantee, including <see cref="GetPath"/>'s deterministic
/// naming — <see cref="EnergyEnvelopeAnalyzer"/> must be able to compute a song's cache path *before*
/// deciding whether to reopen an existing envelope or rebuild.
/// </summary>
public interface IEnergyEnvelopeStore
{
    /// <summary>The deterministic on-disk path an envelope for <paramref name="songId"/> would be saved at, whether or not it has been built yet.</summary>
    string GetPath(SongId songId);

    /// <summary>Persists <paramref name="envelope"/> atomically at <see cref="GetPath"/> and returns that path.</summary>
    Task<string> SaveAsync(EnergyEnvelope envelope, CancellationToken ct);

    /// <summary>Reads back a previously saved envelope from <paramref name="path"/> for independent inspection.</summary>
    /// <exception cref="FileNotFoundException"><paramref name="path"/> does not exist.</exception>
    /// <exception cref="EnergyEnvelopeCorruptException"><paramref name="path"/> exists but is not a valid serialized <see cref="EnergyEnvelope"/>.</exception>
    Task<EnergyEnvelope> LoadAsync(string path, CancellationToken ct);
}
