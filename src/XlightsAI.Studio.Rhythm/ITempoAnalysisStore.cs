using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// Persistence boundary for a built <see cref="TempoAnalysis"/>. Mirrors <see cref="IOnsetAnalysisStore"/>'s
/// own save/load contract and atomic-write guarantee, including <see cref="GetPath"/>'s deterministic
/// naming — <see cref="ITempoDetector"/> must be able to compute a song's cache path *before* deciding
/// whether to reopen an existing analysis or rebuild.
/// </summary>
public interface ITempoAnalysisStore
{
    /// <summary>The deterministic on-disk path a tempo analysis for <paramref name="songId"/> would be saved at, whether or not it has been built yet.</summary>
    string GetPath(SongId songId);

    /// <summary>Persists <paramref name="analysis"/> atomically at <see cref="GetPath"/> and returns that path.</summary>
    Task<string> SaveAsync(TempoAnalysis analysis, CancellationToken ct);

    /// <summary>Reads back a previously saved analysis from <paramref name="path"/> for independent inspection.</summary>
    /// <exception cref="FileNotFoundException"><paramref name="path"/> does not exist.</exception>
    /// <exception cref="TempoAnalysisCorruptException"><paramref name="path"/> exists but is not a valid serialized <see cref="TempoAnalysis"/>.</exception>
    Task<TempoAnalysis> LoadAsync(string path, CancellationToken ct);
}
