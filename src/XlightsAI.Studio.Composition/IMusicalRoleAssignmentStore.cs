namespace XlightsAI.Studio.Composition;

/// <summary>
/// Persistence boundary for a saved <see cref="MusicalRoleAssignmentSet"/>. Mirrors
/// <see cref="XlightsAI.Studio.Display.IRoleAssignmentStore"/>'s own save/load contract, atomic-write
/// guarantee and deterministic-path-by-key convention -- a sibling aggregate at its own separate on-disk
/// path.
/// </summary>
public interface IMusicalRoleAssignmentStore
{
    /// <summary>The deterministic on-disk path assignments for <paramref name="phraseId"/> would be saved at, whether or not any have been saved yet.</summary>
    string GetPath(Guid phraseId);

    /// <summary>Persists <paramref name="assignments"/> atomically at <see cref="GetPath"/> and returns that path.</summary>
    Task<string> SaveAsync(MusicalRoleAssignmentSet assignments, CancellationToken ct);

    /// <summary>Reads back a previously saved assignment set from <paramref name="path"/> for independent inspection.</summary>
    /// <exception cref="FileNotFoundException"><paramref name="path"/> does not exist.</exception>
    /// <exception cref="CompositionCorruptException"><paramref name="path"/> exists but is not a valid serialized <see cref="MusicalRoleAssignmentSet"/>.</exception>
    Task<MusicalRoleAssignmentSet> LoadAsync(string path, CancellationToken ct);
}
