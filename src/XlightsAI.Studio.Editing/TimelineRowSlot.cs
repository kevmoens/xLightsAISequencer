using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Editing;

/// <summary>
/// PHASE-03 Step 017: one visible, pre-order-flattened row produced by <see cref="TimelineRowFlattener"/>
/// — the actual virtualized list a timeline's row column renders (PHASE-03 Section 3: "UI uses
/// virtualization for many rows and effects"). <see cref="Depth"/> is indentation-only presentation
/// state; <see cref="TimelineRowNode.Id"/>, carried through unchanged as <see cref="Id"/>, remains
/// the only identity a caller may key collapse state or a selection off of.
/// </summary>
public sealed record TimelineRowSlot(
    TimelineRowId Id,
    string DisplayName,
    int Depth,
    bool IsGroup,
    bool HasChildren,
    bool IsExpanded,
    ModelId? ModelId);
