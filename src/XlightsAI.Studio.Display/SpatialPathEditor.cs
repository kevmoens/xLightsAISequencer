using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Display;

/// <summary>Default <see cref="ISpatialPathEditor"/>. See the interface and <c>implementation-evidence/PHASE-09/STEP-062.md</c> for the full rationale.</summary>
public sealed class SpatialPathEditor : ISpatialPathEditor
{
    public SpatialPath Reorder(SpatialPath oldPath, IReadOnlyList<ModelId> newOrder)
    {
        ArgumentNullException.ThrowIfNull(oldPath);
        ArgumentNullException.ThrowIfNull(newOrder);

        var originalIds = new HashSet<ModelId>(oldPath.OrderedModels);
        var seen = new HashSet<ModelId>();
        foreach (var id in newOrder)
        {
            if (!originalIds.Contains(id))
            {
                throw new SpatialDisplayGraphInvalidException(
                    $"Reorder references node '{id.Value}', which is not part of path '{oldPath.Name}'.");
            }

            if (!seen.Add(id))
            {
                throw new SpatialDisplayGraphInvalidException($"Reorder lists node '{id.Value}' more than once.");
            }
        }

        if (seen.Count != originalIds.Count)
        {
            throw new SpatialDisplayGraphInvalidException(
                $"Reorder must list every node in path '{oldPath.Name}' exactly once ({originalIds.Count} expected, {seen.Count} given).");
        }

        return oldPath with { OrderedModels = newOrder, Version = oldPath.Version + 1 };
    }

    public SpatialPath Reverse(SpatialPath oldPath)
    {
        ArgumentNullException.ThrowIfNull(oldPath);

        var reversed = oldPath.OrderedModels.Reverse().ToList();
        return oldPath with { OrderedModels = reversed, Version = oldPath.Version + 1 };
    }
}
