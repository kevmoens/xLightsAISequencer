using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Editing;

namespace XlightsAI.Studio.Shell;

/// <summary>
/// UI-only projection of one flattened <see cref="TimelineRowSlot"/>: the domain's depth-only
/// presentation state becomes an actual indentation width, and a group row gets a bound
/// <see cref="ToggleExpandCommand"/> (a leaf model row has none) — same "Avalonia-free Shell layer
/// projects pure domain data into something directly bindable" convention
/// <see cref="TimelineRulerTickView"/> already established for ruler ticks.
/// </summary>
public sealed class TimelineRowItemView
{
    /// <summary>How many pixels one nesting level indents a row — a presentation constant, not a domain concern.</summary>
    public const double IndentPixelsPerLevel = 16d;

    public TimelineRowItemView(
        TimelineRowSlot slot,
        DelegateCommand? toggleExpandCommand,
        IReadOnlyList<EffectLayerRowView>? effectLayers = null)
    {
        ArgumentNullException.ThrowIfNull(slot);

        Id = slot.Id;
        DisplayName = slot.DisplayName;
        IndentPixels = slot.Depth * IndentPixelsPerLevel;
        IsGroup = slot.IsGroup;
        HasChildren = slot.HasChildren;
        IsExpanded = slot.IsExpanded;
        ModelId = slot.ModelId;
        ToggleExpandCommand = toggleExpandCommand;
        EffectLayers = effectLayers ?? [];
    }

    public TimelineRowId Id { get; }

    public string DisplayName { get; }

    public double IndentPixels { get; }

    public bool IsGroup { get; }

    public bool HasChildren { get; }

    public bool IsExpanded { get; }

    public ModelId? ModelId { get; }

    /// <summary><see langword="null"/> for a leaf model row — there is nothing to expand/collapse.</summary>
    public DelegateCommand? ToggleExpandCommand { get; }

    /// <summary>
    /// PHASE-03 Step 018: this row's rendered effect blocks, one <see cref="EffectLayerRowView"/> per
    /// distinct <see cref="XlightsAI.Studio.Xlights.EffectInstance.Layer"/> value. Always empty for a
    /// group row (effects target models, never groups) and for a model row with no document loaded or
    /// no effects on that model yet — never a crash.
    /// </summary>
    public IReadOnlyList<EffectLayerRowView> EffectLayers { get; }
}
