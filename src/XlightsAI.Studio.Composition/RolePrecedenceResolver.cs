namespace XlightsAI.Studio.Composition;

/// <summary>Default <see cref="IRolePrecedenceResolver"/>. See the interface and <c>implementation-evidence/PHASE-10/STEP-066.md</c> for the full rationale.</summary>
public sealed class RolePrecedenceResolver : IRolePrecedenceResolver
{
    public IReadOnlyList<MusicalRoleAssignment> ResolveOrder(IReadOnlyList<MusicalRoleAssignment> assignments)
    {
        ArgumentNullException.ThrowIfNull(assignments);

        // Every element's tier rank is explicitly precomputed in this loop -- deliberately not left to
        // OrderBy's own key selector, since .NET's OrderBy may elide invoking the key selector entirely
        // for a 0- or 1-element sequence (no comparison is ever needed to sort it), which would make an
        // unrecognized precedence tier on a lone assignment silently go unvalidated.
        var ranked = new List<(int TierRank, MusicalRoleAssignment Assignment)>(assignments.Count);
        foreach (var assignment in assignments)
        {
            ranked.Add((TierRank(assignment.PrecedenceTier), assignment));
        }

        return ranked
            .OrderBy(r => r.TierRank)
            .ThenByDescending(r => r.Assignment.Priority)
            .ThenBy(r => (int)r.Assignment.Role)
            .ThenBy(r => r.Assignment.SourceTrackId, StringComparer.Ordinal)
            .Select(r => r.Assignment)
            .ToList();
    }

    private static int TierRank(string precedenceTier)
    {
        var index = PrecedenceTier.All.ToList().IndexOf(precedenceTier);
        return index < 0
            ? throw new CompositionInvalidException($"Unknown precedence tier '{precedenceTier}'.")
            : index;
    }
}
