namespace XlightsAI.Studio.Composition;

/// <summary>
/// Persistence boundary for a saved <see cref="VisualBudgetPreset"/>. Mirrors <see cref="IMusicalRoleAssignmentStore"/>'s
/// own save/load contract, atomic-write guarantee and deterministic-path-by-key convention -- a sibling
/// aggregate at its own separate on-disk path, keyed by preset <see cref="VisualBudgetPreset.Name"/> instead
/// of a phrase id.
/// </summary>
public interface IVisualBudgetPresetStore
{
    /// <summary>The deterministic on-disk path <paramref name="name"/>'s own preset would be saved at, whether or not it has been saved yet.</summary>
    string GetPath(string name);

    /// <summary>Persists <paramref name="preset"/> atomically at <see cref="GetPath"/> and returns that path.</summary>
    Task<string> SaveAsync(VisualBudgetPreset preset, CancellationToken ct);

    /// <summary>Reads back a previously saved preset from <paramref name="path"/> for independent inspection.</summary>
    /// <exception cref="FileNotFoundException"><paramref name="path"/> does not exist.</exception>
    /// <exception cref="CompositionCorruptException"><paramref name="path"/> exists but is not a valid serialized <see cref="VisualBudgetPreset"/>.</exception>
    Task<VisualBudgetPreset> LoadAsync(string path, CancellationToken ct);

    /// <summary>Every preset name currently saved in this store's own directory, ordinal-sorted.</summary>
    Task<IReadOnlyList<string>> ListNamesAsync(CancellationToken ct);
}
