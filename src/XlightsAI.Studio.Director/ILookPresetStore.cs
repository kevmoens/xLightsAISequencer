using XlightsAI.Studio.Choreography;

namespace XlightsAI.Studio.Director;

/// <summary>
/// Persistence boundary for a saved <see cref="LookPreset"/>. Mirrors
/// <see cref="Composition.IVisualBudgetPresetStore"/>'s own save/load contract, atomic-write guarantee and
/// deterministic-path-by-name convention -- a sibling aggregate at its own separate on-disk path, keyed by
/// <see cref="LookPreset.Name"/>. Lives in <c>XlightsAI.Studio.Director</c> (the generator), not
/// <c>XlightsAI.Studio.Composition</c>, because <see cref="LookPreset"/>/<see cref="Look"/> live in
/// <c>XlightsAI.Studio.Choreography</c>, which <c>Composition</c> does not and must not reference (it is
/// a separate, role-precedence-based choreography mechanism -- see <c>Composition.ChoreographyCompiler</c>
/// -- unrelated to the Look/LookLayer world); <c>Director</c> already references <c>Choreography</c> and is
/// exactly the "generator's internal reuse path" step 30.3 asks this to be exposed through first.
/// </summary>
public interface ILookPresetStore
{
    /// <summary>The deterministic on-disk path <paramref name="name"/>'s own preset would be saved at, whether or not it has been saved yet.</summary>
    string GetPath(string name);

    /// <summary>Persists <paramref name="preset"/> atomically at <see cref="GetPath"/> and returns that path.</summary>
    Task<string> SaveAsync(LookPreset preset, CancellationToken ct);

    /// <summary>Reads back a previously saved preset from <paramref name="path"/> for independent inspection.</summary>
    /// <exception cref="FileNotFoundException"><paramref name="path"/> does not exist.</exception>
    /// <exception cref="LookPresetCorruptException"><paramref name="path"/> exists but is not a valid serialized <see cref="LookPreset"/>.</exception>
    Task<LookPreset> LoadAsync(string path, CancellationToken ct);

    /// <summary>Every preset name currently saved in this store's own directory, ordinal-sorted.</summary>
    Task<IReadOnlyList<string>> ListNamesAsync(CancellationToken ct);
}
