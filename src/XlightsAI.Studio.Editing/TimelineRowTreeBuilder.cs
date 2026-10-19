using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Editing;

/// <summary>
/// PHASE-03 Step 017 ("Build prop rows, group expansion and stable mapping to model IDs"): builds
/// the nested row hierarchy a timeline's left-hand prop column shows, directly from an already
/// imported <see cref="DisplayGraph"/> — never re-parses XML, never touches the source show file.
///
/// <see cref="DisplayGraph"/>'s own group model expresses nesting only as "group B's member list
/// contains group A's name" (<see cref="DisplayGroupMemberKind.Group"/>; see
/// <see cref="ShowLayoutImporter.ClassifyGroupMember"/>) — there is no XML-containment nesting and
/// no precomputed tree. This builder resolves that flat, reference-based shape into an actual tree:
/// a group is a top-level row exactly when no other group references it; everything else is placed
/// as a child wherever it is referenced (the same group can be a child under more than one parent —
/// overlapping membership is never flattened).
///
/// Required failure/edge case for this step: "Invalid model/format/version must fail safely without
/// rewriting source files." This builder never throws on a malformed/cyclic layout (a group
/// referencing itself, directly or transitively, is a known risk explicitly called out by PHASE-02's
/// own group-validation work): a cycle stops recursion at the repeated name and is recorded as a
/// warning instead, and a group that is reachable only through a self-contained cycle (so it would
/// otherwise never appear under any true root) is still promoted to a top-level row rather than
/// silently vanishing from the timeline.
/// </summary>
public static class TimelineRowTreeBuilder
{
    /// <summary><see langword="null"/> <paramref name="layout"/> (no show imported yet) produces an empty tree with no warnings — this step's own "missing media" equivalent, same convention <see cref="TimelineViewport"/>'s callers use for an unloaded timeline.</summary>
    public static TimelineRowTree Build(DisplayGraph? layout)
    {
        if (layout is null)
        {
            return new TimelineRowTree([], []);
        }

        var modelsByName = new Dictionary<string, DisplayModel>(StringComparer.Ordinal);
        foreach (var model in layout.Models)
        {
            modelsByName[model.XlightsName] = model;
        }

        var groupsByName = new Dictionary<string, DisplayModelGroup>(StringComparer.Ordinal);
        foreach (var group in layout.Groups)
        {
            groupsByName[group.Name] = group;
        }

        var childGroupNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in layout.Groups)
        {
            foreach (var member in group.Members)
            {
                if (member.Kind == DisplayGroupMemberKind.Group)
                {
                    childGroupNames.Add(member.RawName);
                }
            }
        }

        var warnings = new List<string>();
        var placedGroupNames = new HashSet<string>(StringComparer.Ordinal);
        var roots = new List<TimelineRowNode>();

        foreach (var group in layout.Groups)
        {
            if (childGroupNames.Contains(group.Name))
            {
                continue;
            }

            roots.Add(BuildGroupNode(group.Name, groupsByName, modelsByName, new HashSet<string>(StringComparer.Ordinal), placedGroupNames, warnings));
        }

        // Anything still unplaced only ever appears inside a self-contained reference cycle with no
        // non-cyclic parent (e.g. two groups that reference only each other) — force it visible as
        // its own top-level row instead of letting it silently disappear from the timeline.
        foreach (var group in layout.Groups)
        {
            if (placedGroupNames.Contains(group.Name))
            {
                continue;
            }

            warnings.Add(
                $"Model group '{group.Name}' is part of a group-reference cycle with no non-cyclic parent; shown as a top-level row so it remains visible.");
            roots.Add(BuildGroupNode(group.Name, groupsByName, modelsByName, new HashSet<string>(StringComparer.Ordinal), placedGroupNames, warnings));
        }

        foreach (var model in layout.Models)
        {
            if (model.GroupNames.Count == 0)
            {
                roots.Add(ModelNode(model));
            }
        }

        return new TimelineRowTree(roots, warnings);
    }

    private static TimelineRowNode BuildGroupNode(
        string groupName,
        IReadOnlyDictionary<string, DisplayModelGroup> groupsByName,
        IReadOnlyDictionary<string, DisplayModel> modelsByName,
        IReadOnlySet<string> ancestorPath,
        HashSet<string> placedGroupNames,
        List<string> warnings)
    {
        placedGroupNames.Add(groupName);
        var pathWithSelf = new HashSet<string>(ancestorPath, StringComparer.Ordinal) { groupName };

        var children = new List<TimelineRowNode>();
        foreach (var member in groupsByName[groupName].Members)
        {
            if (member.Kind == DisplayGroupMemberKind.Model && modelsByName.TryGetValue(member.RawName, out var model))
            {
                children.Add(ModelNode(model));
            }
            else if (member.Kind == DisplayGroupMemberKind.Group && pathWithSelf.Contains(member.RawName))
            {
                warnings.Add($"Model group '{groupName}' has a cyclic reference to '{member.RawName}'; nesting stopped there.");
                placedGroupNames.Add(member.RawName);
                children.Add(new TimelineRowNode(TimelineRowId.ForGroup(member.RawName), member.RawName, IsGroup: true, ModelId: null, Children: []));
            }
            else if (member.Kind == DisplayGroupMemberKind.Group && groupsByName.ContainsKey(member.RawName))
            {
                children.Add(BuildGroupNode(member.RawName, groupsByName, modelsByName, pathWithSelf, placedGroupNames, warnings));
            }
            else
            {
                // Submodel or unresolved reference (or, defensively, a Model-kind entry that somehow
                // no longer resolves) — still shown as a visible leaf row identified by its raw name
                // rather than silently dropped.
                children.Add(new TimelineRowNode(TimelineRowId.ForModel(new ModelId(member.RawName)), member.RawName, IsGroup: false, ModelId: null, Children: []));
            }
        }

        return new TimelineRowNode(TimelineRowId.ForGroup(groupName), groupName, IsGroup: true, ModelId: null, Children: children);
    }

    private static TimelineRowNode ModelNode(DisplayModel model) =>
        new(TimelineRowId.ForModel(model.Id), model.XlightsName, IsGroup: false, model.Id, Children: []);
}
