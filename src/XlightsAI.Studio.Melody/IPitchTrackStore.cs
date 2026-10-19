namespace XlightsAI.Studio.Melody;

/// <summary>
/// Persistence boundary for a built <see cref="PitchTrackAnalysis"/>. Mirrors
/// <c>XlightsAI.Studio.Rhythm.IOnsetAnalysisStore</c>'s own save/load contract and atomic-write
/// guarantee, including <see cref="GetPath"/>'s deterministic naming -- <see cref="PitchTracker"/> must
/// be able to compute a stem's cache path *before* deciding whether to reopen an existing analysis or
/// rebuild.
/// </summary>
public interface IPitchTrackStore
{
    /// <summary>The deterministic on-disk path a pitch-track analysis for <paramref name="stemId"/> would be saved at, whether or not it has been built yet.</summary>
    string GetPath(Guid stemId);

    /// <summary>Persists <paramref name="analysis"/> atomically at <see cref="GetPath"/> and returns that path.</summary>
    Task<string> SaveAsync(PitchTrackAnalysis analysis, CancellationToken ct);

    /// <summary>Reads back a previously saved analysis from <paramref name="path"/> for independent inspection.</summary>
    /// <exception cref="FileNotFoundException"><paramref name="path"/> does not exist.</exception>
    /// <exception cref="PitchTrackAnalysisCorruptException"><paramref name="path"/> exists but is not a valid serialized <see cref="PitchTrackAnalysis"/>.</exception>
    Task<PitchTrackAnalysis> LoadAsync(string path, CancellationToken ct);
}
