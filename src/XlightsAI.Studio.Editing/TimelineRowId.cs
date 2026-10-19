using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Editing;

/// <summary>
/// PHASE-03 Step 017: the stable identity of one timeline row — a model's own <see cref="ModelId"/>
/// value (never a display row index, per this step's own implementation guidance: "avoid mixing
/// display row indices with identifiers") or a group's own declared name (<see cref="XlightsAI.Studio.Xlights.DisplayModelGroup"/>
/// has no <c>ModelId</c> of its own; its name is the only stable identity xLights assigns it).
/// Equal <see cref="TimelineRowId"/> values always refer to the same underlying model/group even
/// when that row appears under more than one parent (overlapping group membership is never
/// flattened/deduplicated away), so collapse state keyed by this type survives reload/re-render.
/// </summary>
public readonly record struct TimelineRowId(TimelineRowKind Kind, string Value)
{
    public static TimelineRowId ForModel(ModelId modelId) => new(TimelineRowKind.Model, modelId.Value);

    public static TimelineRowId ForGroup(string groupName) => new(TimelineRowKind.Group, groupName);
}
