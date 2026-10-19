namespace XlightsAI.Studio.Editing;

/// <summary>
/// PHASE-03 Step 017: flattens a <see cref="TimelineRowTree"/> into the pre-order, depth-tagged
/// <see cref="TimelineRowSlot"/> list a timeline's row column actually renders, honoring the
/// caller's own set of currently-collapsed group rows — "retain user group collapse state" (this
/// step's implementation guidance). A collapsed group's children are never visited/materialized
/// at all, so a deeply nested, mostly-collapsed layout never pays the cost of flattening rows no
/// one can currently see — the same virtualization intent <see cref="TimelineRulerTickGenerator"/>
/// already applies to ruler ticks.
/// </summary>
public static class TimelineRowFlattener
{
    public static IReadOnlyList<TimelineRowSlot> Flatten(IReadOnlyList<TimelineRowNode> roots, IReadOnlySet<TimelineRowId> collapsedGroupIds)
    {
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(collapsedGroupIds);

        var slots = new List<TimelineRowSlot>();
        foreach (var root in roots)
        {
            Visit(root, depth: 0, collapsedGroupIds, slots);
        }

        return slots;
    }

    private static void Visit(TimelineRowNode node, int depth, IReadOnlySet<TimelineRowId> collapsedGroupIds, List<TimelineRowSlot> slots)
    {
        var isExpanded = node.IsGroup && !collapsedGroupIds.Contains(node.Id);
        slots.Add(new TimelineRowSlot(node.Id, node.DisplayName, depth, node.IsGroup, node.Children.Count > 0, isExpanded, node.ModelId));

        if (!isExpanded)
        {
            return;
        }

        foreach (var child in node.Children)
        {
            Visit(child, depth + 1, collapsedGroupIds, slots);
        }
    }
}
