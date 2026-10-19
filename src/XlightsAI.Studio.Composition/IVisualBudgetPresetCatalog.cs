namespace XlightsAI.Studio.Composition;

/// <summary>
/// PHASE-10 Step 068's own application-orchestration boundary: validates, persists, versions and lists named
/// <see cref="VisualBudget"/> presets. Every method rejects an invalid input (blank/unsafe preset name, or a
/// <see cref="VisualBudget"/> failing <see cref="VisualBudget.Validate"/>) <em>before</em> loading or writing
/// anything -- this step's own required failure/edge case, verbatim: "Invalid input, cancellation or stale
/// persisted state must not corrupt the project."
/// </summary>
public interface IVisualBudgetPresetCatalog
{
    /// <summary>Loads a previously saved preset named <paramref name="name"/>, or <see langword="null"/> if none has ever been saved under that name.</summary>
    /// <exception cref="CompositionInvalidException"><paramref name="name"/> is blank or contains characters not safe for a file name.</exception>
    Task<VisualBudgetPreset?> GetAsync(string name, CancellationToken ct);

    /// <summary>
    /// Saves <paramref name="budget"/> under <paramref name="name"/>, replacing any prior preset of the same
    /// name and incrementing its <see cref="VisualBudgetPreset.Version"/>.
    /// </summary>
    /// <exception cref="CompositionInvalidException"><paramref name="name"/> is blank/unsafe, or <paramref name="budget"/> fails <see cref="VisualBudget.Validate"/>.</exception>
    Task<VisualBudgetPreset> SaveAsync(string name, VisualBudget budget, CancellationToken ct);

    /// <summary>Every preset name currently saved, ordinal-sorted.</summary>
    Task<IReadOnlyList<string>> ListNamesAsync(CancellationToken ct);
}
