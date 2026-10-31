namespace XlightsAI.Studio.Pipeline;

/// <summary>
/// PHASE-2b (build plan §2b.7; architecture §8b): one effect placement parsed out of a corpus
/// <c>.xsq</c>, before any musical alignment — <see cref="EffectAlignmentParser"/>'s own output shape.
/// </summary>
/// <param name="ModelName">The owning <c>&lt;Element name="..."&gt;</c> this effect is placed on.</param>
/// <param name="ModelType">The owning element's <c>type</c> attribute (<c>"model"</c> or <c>"group"</c> — timing-track elements are never placements and are excluded upstream by <c>XsqStructuralReader</c>).</param>
/// <param name="EffectName">The effect's own declared name (e.g. <c>"On"</c>, <c>"Shockwave"</c>) — <c>XsqStructuralEffectPlacement.Name</c>, verbatim.</param>
/// <param name="StartTimeMs">Start time in milliseconds from sequence origin.</param>
/// <param name="EndTimeMs">End time (exclusive) in milliseconds from sequence origin.</param>
/// <param name="LayerIndex">Zero-based index of the <c>&lt;EffectLayer&gt;</c> this effect sits on within its owning element — not captured by <c>XsqStructuralReader</c> itself, so recovered here by a second, position-matched pass over the same document.</param>
/// <param name="PaletteIndex">Index into the sequence's <c>&lt;ColorPalettes&gt;</c> table.</param>
/// <param name="SettingsString">The resolved <c>&lt;EffectDB&gt;</c> entry's raw comma-separated settings string (via <see cref="XsqStructuralEffectPlacement"/>'s <c>Ref</c> index) — <c>E_</c>/<c>B_</c>/<c>T_</c> keys only, empty for an all-default effect (architecture §8a).</param>
/// <param name="PaletteString">The resolved <c>&lt;ColorPalette&gt;</c> entry's raw comma-separated string — <c>C_</c> keys only.</param>
public sealed record ParsedEffectInstance(
    string ModelName,
    string ModelType,
    string EffectName,
    long StartTimeMs,
    long EndTimeMs,
    int LayerIndex,
    int PaletteIndex,
    string SettingsString,
    string PaletteString);
