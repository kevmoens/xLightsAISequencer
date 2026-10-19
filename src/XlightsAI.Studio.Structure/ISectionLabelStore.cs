using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Structure;

/// <summary>
/// Persistence boundary for a built, automatic (pre-override) <see cref="SectionLabelAnalysis"/>. Mirrors
/// <see cref="ISectionBoundaryStore"/>'s own save/load contract and atomic-write guarantee, including
/// <see cref="GetPath"/>'s deterministic naming. This store never itself knows about user overrides --
/// those live in the separately versioned <see cref="ISectionLabelOverrideStore"/> (Section 4: "Persist
/// detected structure and user-edited structure in separately versioned stores").
/// </summary>
public interface ISectionLabelStore
{
    /// <summary>The deterministic on-disk path an automatic section label analysis for <paramref name="songId"/> would be saved at, whether or not it has been built yet.</summary>
    string GetPath(SongId songId);

    /// <summary>Persists <paramref name="analysis"/> atomically at <see cref="GetPath"/> and returns that path.</summary>
    Task<string> SaveAsync(SectionLabelAnalysis analysis, CancellationToken ct);

    /// <summary>Reads back a previously saved analysis from <paramref name="path"/> for independent inspection.</summary>
    /// <exception cref="FileNotFoundException"><paramref name="path"/> does not exist.</exception>
    /// <exception cref="SectionLabelCorruptException"><paramref name="path"/> exists but is not a valid serialized <see cref="SectionLabelAnalysis"/>.</exception>
    Task<SectionLabelAnalysis> LoadAsync(string path, CancellationToken ct);
}
