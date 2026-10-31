using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Rhythm;

/// <summary>Persistence boundary for a built <see cref="BandEnergyEnvelope"/>. Mirrors <see cref="IOnsetAnalysisStore"/>'s own save/load contract and atomic-write guarantee.</summary>
public interface IBandEnergyEnvelopeStore
{
    /// <summary>The deterministic on-disk path a band energy envelope for <paramref name="songId"/> would be saved at, whether or not it has been built yet.</summary>
    string GetPath(SongId songId);

    /// <summary>Persists <paramref name="envelope"/> atomically at <see cref="GetPath"/> and returns that path.</summary>
    Task<string> SaveAsync(BandEnergyEnvelope envelope, CancellationToken ct);

    /// <summary>Reads back a previously saved envelope from <paramref name="path"/> for independent inspection.</summary>
    /// <exception cref="FileNotFoundException"><paramref name="path"/> does not exist.</exception>
    /// <exception cref="BandEnergyAnalysisCorruptException"><paramref name="path"/> exists but is not a valid serialized <see cref="BandEnergyEnvelope"/>.</exception>
    Task<BandEnergyEnvelope> LoadAsync(string path, CancellationToken ct);
}
