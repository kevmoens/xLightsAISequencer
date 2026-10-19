namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// PHASE-11 Step 072's own persistence boundary for a captured <see cref="SelectionContext"/>. Kept
/// separate from <see cref="ISelectionContextCaptureService"/> -- capture is pure validation, this is
/// I/O -- the same separation every prior phase's own catalog/store pair already established (e.g.
/// <c>IMusicalRoleAssignmentCatalog</c>/<c>IMusicalRoleAssignmentStore</c>).
/// </summary>
public interface ISelectionContextStore
{
    /// <summary>Persists <paramref name="context"/> to a new file named by its own id, returning the written path.</summary>
    Task<string> SaveAsync(SelectionContext context, CancellationToken ct);

    /// <summary>Loads a previously saved <see cref="SelectionContext"/> from <paramref name="path"/>.</summary>
    /// <exception cref="FileNotFoundException">No file exists at <paramref name="path"/>.</exception>
    /// <exception cref="SelectionContextCorruptException">The file exists but failed to deserialize.</exception>
    Task<SelectionContext> LoadAsync(string path, CancellationToken ct);
}
