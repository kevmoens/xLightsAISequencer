namespace XlightsAI.Studio.Shell;

/// <summary>
/// UI-only projection of one <see cref="XlightsAI.Studio.Editing.EffectBlockLayout"/>: the domain's
/// absolute pixel <c>X</c> is converted to a left margin relative to the previous block on the same
/// layer track (same "plain horizontal panel, no Avalonia Canvas absolute positioning" convention
/// <see cref="TimelineRulerTickView"/> already established — see STEP-16.md §9 for the documented
/// Avalonia <c>Canvas.Left</c>/<c>DataTemplate</c> gotcha this avoids). <see cref="SelectCommand"/>
/// is this block's own bound click handler, capturing its <see cref="EffectId"/> so the view never
/// needs a converter/command-parameter to recover which effect was clicked — PHASE-03 Step 018's own
/// required acceptance: "Selection maps exactly to effect ID and time span."
/// </summary>
public sealed record EffectBlockItemView(
    Guid EffectId,
    double MarginLeft,
    double Width,
    string Label,
    bool IsLocked,
    bool IsSelected,
    DelegateCommand SelectCommand);

/// <summary>
/// One layer's worth of <see cref="EffectBlockItemView"/>s for a single model row — PHASE-03 Step
/// 018's own "support overlapping layers explicitly": two effects whose time ranges overlap on the
/// same model render on distinct <see cref="EffectLayerRowView"/> instances (one per
/// <see cref="XlightsAI.Studio.Xlights.EffectInstance.Layer"/> value), never merged into one block
/// or silently hidden behind another.
/// </summary>
public sealed record EffectLayerRowView(int Layer, IReadOnlyList<EffectBlockItemView> Blocks);
