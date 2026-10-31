using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography;

/// <summary>
/// Resolves a <see cref="Look"/>/<see cref="LookLayer"/>'s own prop reference (architecture §4's
/// <c>"props":"group:MiniTrees"</c>/<c>"subject":"group:Outline"</c>, or a bare model/group name) against
/// a real <see cref="DisplayGraph"/> into the concrete set of targetable <see cref="ModelId"/>s -- never
/// flattening a group's own declared membership into a cached, silently-stale copy (the same "groups may
/// overlap; do not flatten user group membership" discipline <see cref="DisplayModelGroup"/> itself
/// documents), and never inventing a resolution for a reference that does not actually exist in the
/// layout.
/// </summary>
public static class PropGroupResolver
{
    private const string GroupPrefix = "group:";

    /// <summary>
    /// Whether <paramref name="reference"/> names a real xLights model group in <paramref name="layout"/>
    /// (not a bare model name) -- lets a caller decide whether to address every member individually or
    /// target the group itself as one shared canvas. Real xLights <c>.xsq</c> output (confirmed against
    /// 20 real files, <c>docs/PHASE0-FINDINGS.md</c>) represents a group-targeted effect identically to a
    /// model-targeted one (<c>&lt;Element type="model" name="GroupName"&gt;</c> -- <c>type="modelGroup"</c>
    /// was never observed), so <see cref="XsqExporter"/> needs no change to accept <paramref name="groupName"/>
    /// as an ordinary <see cref="ModelId"/> value.
    /// </summary>
    public static bool TryGetRealGroupName(string reference, DisplayGraph layout, out string? groupName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentNullException.ThrowIfNull(layout);

        var name = reference.StartsWith(GroupPrefix, StringComparison.Ordinal) ? reference[GroupPrefix.Length..] : reference;
        var group = layout.Groups.FirstOrDefault(g => string.Equals(g.Name, name, StringComparison.Ordinal));
        groupName = group?.Name;
        return group is not null;
    }

    /// <exception cref="ArgumentException"><paramref name="reference"/> does not resolve to any real model or model group in <paramref name="layout"/>.</exception>
    public static IReadOnlyList<ModelId> Resolve(string reference, DisplayGraph layout)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentNullException.ThrowIfNull(layout);

        var modelsByName = layout.Models.ToDictionary(m => m.XlightsName, m => m, StringComparer.Ordinal);
        var isGroupRef = reference.StartsWith(GroupPrefix, StringComparison.Ordinal);
        var name = isGroupRef ? reference[GroupPrefix.Length..] : reference;

        if (!isGroupRef && modelsByName.TryGetValue(name, out var directModel))
        {
            return [directModel.Id];
        }

        var group = layout.Groups.FirstOrDefault(g => string.Equals(g.Name, name, StringComparison.Ordinal));
        if (group is not null)
        {
            return ExpandGroup(group, layout, modelsByName, new HashSet<string>(StringComparer.Ordinal));
        }

        if (modelsByName.TryGetValue(name, out var fallbackModel))
        {
            return [fallbackModel.Id];
        }

        throw new ArgumentException(
            $"Look reference '{reference}' does not resolve to any model or model group in the supplied layout.",
            nameof(reference));
    }

    private static IReadOnlyList<ModelId> ExpandGroup(
        DisplayModelGroup group, DisplayGraph layout, IReadOnlyDictionary<string, DisplayModel> modelsByName, HashSet<string> visitedGroups)
    {
        if (!visitedGroups.Add(group.Name))
        {
            // A cyclic nested-group reference -- never loop forever; this group's own contribution to an
            // ancestor's expansion is simply empty the second time it is reached.
            return [];
        }

        var result = new List<ModelId>();
        foreach (var member in group.Members)
        {
            switch (member.Kind)
            {
                case DisplayGroupMemberKind.Model:
                    if (modelsByName.TryGetValue(member.RawName, out var model))
                    {
                        result.Add(model.Id);
                    }

                    break;

                case DisplayGroupMemberKind.Submodel:
                    // Only the model half of a "ModelName/SubModelName" group-member reference is
                    // independently verified (DisplayGroupMemberKind.Submodel's own doc comment) -- target
                    // the parent model, since the submodel half cannot be cross-checked against this
                    // group-member list alone.
                    var modelPart = member.RawName.Split('/')[0];
                    if (modelsByName.TryGetValue(modelPart, out var parentModel))
                    {
                        result.Add(parentModel.Id);
                    }

                    break;

                case DisplayGroupMemberKind.Group:
                    var nested = layout.Groups.FirstOrDefault(g => string.Equals(g.Name, member.RawName, StringComparison.Ordinal));
                    if (nested is not null)
                    {
                        result.AddRange(ExpandGroup(nested, layout, modelsByName, visitedGroups));
                    }

                    break;

                case DisplayGroupMemberKind.Unresolved:
                default:
                    // A broken reference already flagged at import time -- never invented here either.
                    break;
            }
        }

        return result.Distinct().ToList();
    }
}
