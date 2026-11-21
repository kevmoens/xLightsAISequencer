using System.Text.RegularExpressions;
using XlightsAI.Studio.Choreography;

namespace XlightsAI.Studio.Director;

/// <summary>
/// Build-plan Phase 30 step 30.3 ("reusable-presets"): "Expose preset save/apply through the generator's
/// internal reuse path first (so a later generation pass can stamp a known-good preset onto a new
/// section)." Wraps <see cref="ILookPresetStore"/> with the same name-validation + monotonic-versioning
/// convention <c>Composition.VisualBudgetPresetCatalog</c> already established, plus the one operation that
/// is specific to a Look preset: <see cref="ApplyAsync"/>, which re-expands a saved preset onto a new
/// target/palette via <see cref="LookPresetConverter.Apply"/> -- the generator-reuse path itself, callable
/// by a later generation pass (e.g. <c>ChoreographyGenerationPipeline</c>) choosing to stamp a known-good
/// preset onto a new section, never applied automatically (same opt-in discipline as
/// <see cref="LayerBlendPlanner"/>/<see cref="HitMotionCompound"/> -- nothing calls this unless a caller
/// deliberately does).
/// </summary>
public sealed partial class LookPresetCatalog
{
    private readonly ILookPresetStore _store;

    public LookPresetCatalog(ILookPresetStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    public async Task<LookPreset?> GetAsync(string name, CancellationToken ct)
    {
        ValidateName(name);

        var path = _store.GetPath(name);
        if (!File.Exists(path))
        {
            return null;
        }

        return await _store.LoadAsync(path, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Captures <paramref name="look"/>'s own structural shape (<see cref="LookPresetConverter.FromLook"/>)
    /// and persists it as the next version of the preset named <paramref name="name"/> -- "save a preset
    /// from a generated Look," verbatim.
    /// </summary>
    public async Task<LookPreset> SaveAsync(string name, Look look, string? sourceNote, CancellationToken ct)
    {
        ValidateName(name);
        ArgumentNullException.ThrowIfNull(look);

        var existing = await GetAsync(name, ct).ConfigureAwait(false);

        ct.ThrowIfCancellationRequested();

        var captured = LookPresetConverter.FromLook(name, look, sourceNote);
        var preset = captured with { Version = existing is null ? 0 : existing.Version + 1 };
        await _store.SaveAsync(preset, ct).ConfigureAwait(false);
        return preset;
    }

    /// <summary>
    /// Loads the preset named <paramref name="name"/> and re-expands it onto a new target/palette via
    /// <see cref="LookPresetConverter.Apply"/> -- "apply it to a different model/group with a different
    /// palette," verbatim. Returns <see langword="null"/> when no preset of this name has been saved
    /// (never fabricated).
    /// </summary>
    public async Task<Look?> ApplyAsync(
        string name, string newLookId, string newLookName, string intent, double energyCeiling,
        string paletteRef, string subject, string targetProps, CancellationToken ct)
    {
        var preset = await GetAsync(name, ct).ConfigureAwait(false);
        if (preset is null)
        {
            return null;
        }

        return LookPresetConverter.Apply(preset, newLookId, newLookName, intent, energyCeiling, paletteRef, subject, targetProps);
    }

    public Task<IReadOnlyList<string>> ListNamesAsync(CancellationToken ct) => _store.ListNamesAsync(ct);

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !SafeNamePattern().IsMatch(name))
        {
            throw new ArgumentException(
                $"Preset name '{name}' must be non-blank and contain only letters, digits, '-', '_' or ' '.", nameof(name));
        }
    }

    [GeneratedRegex(@"^[A-Za-z0-9_\- ]+$")]
    private static partial Regex SafeNamePattern();
}
