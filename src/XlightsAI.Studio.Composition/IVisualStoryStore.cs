namespace XlightsAI.Studio.Composition;

/// <summary>
/// Persistence boundary for a saved <see cref="VisualStoryRecord"/>. Mirrors <see cref="IVisualBudgetPresetStore"/>'s
/// own save/load contract, atomic-write guarantee and deterministic-path-by-key convention -- a sibling
/// aggregate at its own separate on-disk path, keyed by <see cref="VisualStoryRecord.SongId"/> instead of a
/// preset name.
/// </summary>
public interface IVisualStoryStore
{
    /// <summary>The deterministic on-disk path <paramref name="songId"/>'s own story would be saved at, whether or not it has been saved yet.</summary>
    string GetPath(Guid songId);

    /// <summary>Persists <paramref name="record"/> atomically at <see cref="GetPath"/> and returns that path.</summary>
    Task<string> SaveAsync(VisualStoryRecord record, CancellationToken ct);

    /// <summary>Reads back a previously saved record from <paramref name="path"/> for independent inspection.</summary>
    /// <exception cref="FileNotFoundException"><paramref name="path"/> does not exist.</exception>
    /// <exception cref="CompositionCorruptException"><paramref name="path"/> exists but is not a valid serialized <see cref="VisualStoryRecord"/>.</exception>
    Task<VisualStoryRecord> LoadAsync(string path, CancellationToken ct);
}
