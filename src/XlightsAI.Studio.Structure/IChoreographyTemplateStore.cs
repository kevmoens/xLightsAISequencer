using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Structure;

/// <summary>
/// Persistence boundary for a built <see cref="ChoreographyTemplateAnalysis"/>. Mirrors
/// <see cref="IMotifClusteringStore"/>'s own save/load contract and atomic-write guarantee, including
/// <see cref="GetPath"/>'s deterministic naming.
/// </summary>
public interface IChoreographyTemplateStore
{
    /// <summary>The deterministic on-disk path a choreography template analysis for <paramref name="songId"/> would be saved at, whether or not it has been built yet.</summary>
    string GetPath(SongId songId);

    /// <summary>Persists <paramref name="analysis"/> atomically at <see cref="GetPath"/> and returns that path.</summary>
    Task<string> SaveAsync(ChoreographyTemplateAnalysis analysis, CancellationToken ct);

    /// <summary>Reads back a previously saved analysis from <paramref name="path"/> for independent inspection.</summary>
    /// <exception cref="FileNotFoundException"><paramref name="path"/> does not exist.</exception>
    /// <exception cref="ChoreographyTemplateCorruptException"><paramref name="path"/> exists but is not a valid serialized <see cref="ChoreographyTemplateAnalysis"/>.</exception>
    Task<ChoreographyTemplateAnalysis> LoadAsync(string path, CancellationToken ct);
}
