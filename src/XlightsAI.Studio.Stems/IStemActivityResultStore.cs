namespace XlightsAI.Studio.Stems;

/// <summary>
/// Persistence boundary for a built <see cref="StemActivityResult"/>. Mirrors
/// <c>XlightsAI.Studio.Rhythm.IEnergyEnvelopeStore</c>'s own save/load contract and atomic-write
/// guarantee, including <see cref="GetPath"/>'s deterministic naming -- <see cref="StemActivityDetector"/>
/// must be able to compute a stem's cache path *before* deciding whether to reopen an existing result or
/// rebuild.
/// </summary>
public interface IStemActivityResultStore
{
    /// <summary>The deterministic on-disk path an activity result for <paramref name="stemId"/> would be saved at, whether or not it has been built yet.</summary>
    string GetPath(Guid stemId);

    /// <summary>Persists <paramref name="result"/> atomically at <see cref="GetPath"/> and returns that path.</summary>
    Task<string> SaveAsync(StemActivityResult result, CancellationToken ct);

    /// <summary>Reads back a previously saved result from <paramref name="path"/> for independent inspection.</summary>
    /// <exception cref="FileNotFoundException"><paramref name="path"/> does not exist.</exception>
    /// <exception cref="StemActivityResultCorruptException"><paramref name="path"/> exists but is not a valid serialized <see cref="StemActivityResult"/>.</exception>
    Task<StemActivityResult> LoadAsync(string path, CancellationToken ct);
}
