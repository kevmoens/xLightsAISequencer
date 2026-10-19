using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Structure;

/// <summary>
/// Persistence boundary for a built, automatic (pre-override) <see cref="PhraseSegmentationAnalysis"/>.
/// Mirrors <see cref="ISectionLabelStore"/>'s own save/load contract and atomic-write guarantee, including
/// <see cref="GetPath"/>'s deterministic naming. This store never itself knows about manual boundary
/// corrections -- those live in the separately versioned <see cref="IPhraseBoundaryOverrideStore"/>
/// (Section 4: "Persist detected structure and user-edited structure in separately versioned stores").
/// </summary>
public interface IPhraseSegmentationStore
{
    /// <summary>The deterministic on-disk path an automatic phrase segmentation analysis for <paramref name="songId"/> would be saved at, whether or not it has been built yet.</summary>
    string GetPath(SongId songId);

    /// <summary>Persists <paramref name="analysis"/> atomically at <see cref="GetPath"/> and returns that path.</summary>
    Task<string> SaveAsync(PhraseSegmentationAnalysis analysis, CancellationToken ct);

    /// <summary>Reads back a previously saved analysis from <paramref name="path"/> for independent inspection.</summary>
    /// <exception cref="FileNotFoundException"><paramref name="path"/> does not exist.</exception>
    /// <exception cref="PhraseSegmentationCorruptException"><paramref name="path"/> exists but is not a valid serialized <see cref="PhraseSegmentationAnalysis"/>.</exception>
    Task<PhraseSegmentationAnalysis> LoadAsync(string path, CancellationToken ct);
}
