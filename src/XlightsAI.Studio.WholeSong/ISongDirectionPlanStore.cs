namespace XlightsAI.Studio.WholeSong;

/// <summary>
/// Persistence boundary for a saved <see cref="SongDirectionPlanRecord"/>. Mirrors
/// <see cref="XlightsAI.Studio.Composition.IVisualStoryStore"/>'s own save/load contract and
/// deterministic-path-by-key convention, plus an explicit optimistic-concurrency check on
/// <see cref="SaveAsync"/> -- this step's own required failure/edge case, verbatim: "...stale persisted
/// state must not corrupt the project."
/// </summary>
public interface ISongDirectionPlanStore
{
    /// <summary>The deterministic on-disk path <paramref name="songId"/>'s own plan would be saved at, whether or not it has been saved yet.</summary>
    string GetPath(Guid songId);

    /// <summary>
    /// Persists <paramref name="record"/> atomically at <see cref="GetPath"/> and returns that path, but
    /// only if <paramref name="expectedCurrentVersion"/> matches the version actually persisted right now
    /// (<see langword="null"/> means "I expect no prior record exists yet"). On a mismatch, nothing is
    /// written and the file on disk (if any) is left exactly as it was.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="record"/> is <see langword="null"/>.</exception>
    /// <exception cref="SongDirectionStaleException"><paramref name="expectedCurrentVersion"/> does not match the actually persisted current version.</exception>
    Task<string> SaveAsync(SongDirectionPlanRecord record, int? expectedCurrentVersion, CancellationToken ct);

    /// <summary>Reads back a previously saved record from <paramref name="path"/> for independent inspection.</summary>
    /// <exception cref="FileNotFoundException"><paramref name="path"/> does not exist.</exception>
    /// <exception cref="SongDirectionCorruptException"><paramref name="path"/> exists but is not a valid serialized <see cref="SongDirectionPlanRecord"/>.</exception>
    Task<SongDirectionPlanRecord> LoadAsync(string path, CancellationToken ct);
}
