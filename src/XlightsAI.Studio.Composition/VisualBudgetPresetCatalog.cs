using System.Text.RegularExpressions;

namespace XlightsAI.Studio.Composition;

/// <summary>Default <see cref="IVisualBudgetPresetCatalog"/>. See the interface and <c>implementation-evidence/PHASE-10/STEP-068.md</c> for the full rationale.</summary>
public sealed partial class VisualBudgetPresetCatalog : IVisualBudgetPresetCatalog
{
    private readonly IVisualBudgetPresetStore _store;

    public VisualBudgetPresetCatalog(IVisualBudgetPresetStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    public async Task<VisualBudgetPreset?> GetAsync(string name, CancellationToken ct)
    {
        ValidateName(name);

        var path = _store.GetPath(name);
        if (!File.Exists(path))
        {
            return null;
        }

        return await _store.LoadAsync(path, ct).ConfigureAwait(false);
    }

    public async Task<VisualBudgetPreset> SaveAsync(string name, VisualBudget budget, CancellationToken ct)
    {
        ValidateName(name);
        ArgumentNullException.ThrowIfNull(budget);
        budget.Validate();

        var existing = await GetAsync(name, ct).ConfigureAwait(false);

        ct.ThrowIfCancellationRequested();

        var preset = new VisualBudgetPreset(name, budget, existing is null ? 0 : existing.Version + 1);
        await _store.SaveAsync(preset, ct).ConfigureAwait(false);
        return preset;
    }

    public Task<IReadOnlyList<string>> ListNamesAsync(CancellationToken ct) => _store.ListNamesAsync(ct);

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !SafeNamePattern().IsMatch(name))
        {
            throw new CompositionInvalidException(
                $"Preset name '{name}' must be non-blank and contain only letters, digits, '-', '_' or ' '.");
        }
    }

    [GeneratedRegex(@"^[A-Za-z0-9_\- ]+$")]
    private static partial Regex SafeNamePattern();
}
