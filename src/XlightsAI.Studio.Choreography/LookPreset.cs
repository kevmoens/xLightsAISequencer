namespace XlightsAI.Studio.Choreography;

/// <summary>
/// Build-plan Phase 30 step 30.3 ("reusable-presets"): one layer within a <see cref="LookPreset"/> --
/// everything a <see cref="LookLayer"/> declares about HOW it looks and moves (effect, role, z-order,
/// blend mode, duty cycle, cadence, parameter overrides, value-curve bindings), deliberately dropping
/// the one thing that is specific to the Look it was captured from: <see cref="LookLayer.Props"/> (WHERE
/// it targets). A preset is re-targeted as a whole onto a new "where" at apply time
/// (<see cref="LookPresetConverter.Apply"/>) -- never per-layer, matching every Look this codebase has
/// hand-authored or generated so far, which always binds every layer of a single Look to the same
/// subject/group.
/// </summary>
public sealed record LookPresetLayer(
    string Role,
    string Effect,
    int Z,
    string Blend,
    double Duty,
    string? Timescale,
    IReadOnlyDictionary<string, string>? Params = null,
    IReadOnlyDictionary<string, LookCurveBinding>? Curves = null)
{
    public IReadOnlyDictionary<string, string> ParamsOrEmpty => Params ?? EmptyParams;

    public IReadOnlyDictionary<string, LookCurveBinding> CurvesOrEmpty => Curves ?? EmptyCurves;

    private static readonly IReadOnlyDictionary<string, string> EmptyParams = new Dictionary<string, string>();
    private static readonly IReadOnlyDictionary<string, LookCurveBinding> EmptyCurves = new Dictionary<string, LookCurveBinding>();
}

/// <summary>
/// Build-plan Phase 30 step 30.3: "a persisted, named, palette-parameterized preset unit... distinct from
/// VisualBudgetPresetCatalog's generation-settings presets." A preset stores effect types, relative layer
/// order, blend modes (from step 30.1's <see cref="LayerBlendPlanner"/>) and relative timing/value-curve
/// shape -- never a baked-in absolute color (no <see cref="Look.PaletteRef"/>, no literal color anywhere;
/// this codebase's own compiled <see cref="Xlights.EffectInstance.PaletteIndex"/> is always 0 regardless of
/// layer -- a Look's color is carried entirely by its single declared <see cref="Look.PaletteRef"/>, so
/// "mapping color roles through the target palette" is exactly "compile the applied Look against a
/// different palette library entry," nothing more is needed or invented here) and never a specific target
/// (no <see cref="Look.Subject"/>/<see cref="LookLayer.Props"/> -- re-targeted at apply time instead).
/// </summary>
/// <param name="Name">Stable, unique identity within a preset store -- what a caller names this preset when saving and later asks for by name when applying.</param>
/// <param name="Motion">The source Look's own <see cref="Look.Motion"/> -- carried through unchanged (an applied Look keeps the same always-moving-rule cadence/filler-pool behavior its source Look had).</param>
/// <param name="Layers">This preset's own layers, in declared z-order (index 0 is the base/bottom layer) -- structurally identical in shape/order/blend/timing to the source Look's own layers, with only <see cref="LookLayer.Props"/> removed.</param>
/// <param name="SourceLookId">The <see cref="Look.Id"/> this preset was captured from, for traceability -- or <see langword="null"/> when unknown.</param>
/// <param name="SourceNote">A short, human-readable note on where/why this preset was promoted from (e.g. a corpus-mined <c>MultiLayerTemplate</c> id, or "promoted from a Phase 30.2 HitMotionCompound").</param>
/// <param name="Version">Monotonically incremented by a catalog each time a preset of this <see cref="Name"/> is re-saved -- same convention <see cref="Composition.VisualBudgetPreset"/> already established.</param>
public sealed record LookPreset(
    string Name,
    string Motion,
    IReadOnlyList<LookPresetLayer> Layers,
    string? SourceLookId = null,
    string? SourceNote = null,
    int Version = 0);

/// <summary>
/// Pure (no I/O -- this project is deliberately "a pure compiler library," see its own <c>.csproj</c>
/// comment) conversion between a real, compiled-against <see cref="Look"/> and a palette-/target-free
/// <see cref="LookPreset"/>. A later generation pass (or a persistence catalog in a caller project, e.g.
/// <c>XlightsAI.Studio.Director</c>'s own generator-reuse path) is responsible for actually saving/loading
/// one -- this class only ever captures or re-expands the structural shape.
/// </summary>
public static class LookPresetConverter
{
    /// <summary>
    /// Captures <paramref name="look"/>'s own layer stack (effect types, relative order, blend modes,
    /// relative timing/value-curve shape) as a reusable <see cref="LookPreset"/> named
    /// <paramref name="name"/> -- deliberately drops every layer's own <see cref="LookLayer.Props"/>
    /// (the one per-layer field that is specific to where this particular Look was targeted) and the
    /// Look's own <see cref="Look.PaletteRef"/>/<see cref="Look.Subject"/>/<see cref="Look.Id"/> (all
    /// specific to the song/target this Look was generated for, not to its reusable shape).
    /// </summary>
    public static LookPreset FromLook(string name, Look look, string? sourceNote = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(look);

        if (look.Layers.Count == 0)
        {
            throw new ArgumentException($"Look '{look.Id}' has no layers -- nothing to save as a preset.", nameof(look));
        }

        var layers = look.Layers
            .OrderBy(l => l.Z)
            .Select(l => new LookPresetLayer(l.Role, l.Effect, l.Z, l.Blend, l.Duty, l.Timescale, l.Params, l.Curves))
            .ToList();

        return new LookPreset(name, look.Motion, layers, look.Id, sourceNote);
    }

    /// <summary>
    /// Re-expands <paramref name="preset"/> into a new <see cref="Look"/> targeting
    /// <paramref name="targetProps"/> (every layer, same single-target convention every real Look in this
    /// codebase already follows) and referencing <paramref name="paletteRef"/> -- the one place color is
    /// ever (re-)introduced, via whatever real <see cref="Xlights.ColorPaletteDefinition"/>
    /// <paramref name="paletteRef"/> resolves to in the caller's own supplied palette library at compile
    /// time (<see cref="LookCompiler.Compile"/>). Effect types, blend modes, relative layer order
    /// (<see cref="LookLayer.Z"/>), duty, timescale, params and curve bindings are carried through
    /// byte-for-byte unchanged from the preset -- the acceptance definition's own "only palette-mapped
    /// colors differing" requirement.
    /// </summary>
    public static Look Apply(
        LookPreset preset,
        string newLookId,
        string newLookName,
        string intent,
        double energyCeiling,
        string paletteRef,
        string subject,
        string targetProps)
    {
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentException.ThrowIfNullOrWhiteSpace(newLookId);
        ArgumentException.ThrowIfNullOrWhiteSpace(paletteRef);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetProps);

        var layers = preset.Layers
            .Select(l => new LookLayer(l.Z, targetProps, l.Effect, l.Role, l.Params, l.Blend, l.Timescale, l.Duty, l.Curves))
            .ToList();

        return new Look(newLookId, newLookName, intent, energyCeiling, preset.Motion, paletteRef, subject, layers);
    }
}
