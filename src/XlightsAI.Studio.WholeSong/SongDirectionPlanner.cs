using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.WholeSong;

/// <summary>Default <see cref="ISongDirectionPlanner"/>. Pure, deterministic, no I/O.</summary>
public sealed class SongDirectionPlanner : ISongDirectionPlanner
{
    public SongDirectionPlan Plan(
        SectionLabelAnalysis labels,
        MotifClusteringAnalysis motifs,
        IReadOnlyList<SongSectionDirectionInput> sectionInputs,
        long baseRevision,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(motifs);
        ArgumentNullException.ThrowIfNull(sectionInputs);

        if (sectionInputs.Count == 0)
        {
            throw new SongDirectionInvalidException("At least one SongSectionDirectionInput is required to plan a SongDirectionPlan.");
        }

        var labelsBySectionId = labels.Labels.ToDictionary(l => l.Id);
        var knownMotifGroupIds = motifs.Groups.Select(g => g.Id).ToHashSet();

        var seenSectionIds = new HashSet<Guid>();
        var sections = new List<SongSectionDirection>(sectionInputs.Count);

        foreach (var input in sectionInputs)
        {
            ArgumentNullException.ThrowIfNull(input);

            if (!seenSectionIds.Add(input.SectionId))
            {
                throw new SongDirectionInvalidException(
                    $"Duplicate section id '{input.SectionId}' supplied to SongDirectionPlanner.Plan.");
            }

            if (!labelsBySectionId.TryGetValue(input.SectionId, out var label))
            {
                throw new SongDirectionInvalidException(
                    $"Section id '{input.SectionId}' does not exist in SectionLabelAnalysis '{labels.VersionId}' for song '{labels.SongId}'.");
            }

            foreach (var motifGroupId in input.MotifGroupIds)
            {
                if (!knownMotifGroupIds.Contains(motifGroupId))
                {
                    throw new SongDirectionInvalidException(
                        $"Motif group id '{motifGroupId}' (linked from section '{input.SectionId}') does not exist in " +
                        $"MotifClusteringAnalysis '{motifs.VersionId}' for song '{motifs.SongId}'.");
                }
            }

            sections.Add(new SongSectionDirection(
                input.SectionId,
                label.Kind,
                input.TargetEnergy,
                input.TargetContrast,
                input.PaletteId,
                input.FocalRole,
                input.MotifGroupIds));
        }

        var ordered = sections
            .OrderBy(s => labelsBySectionId[s.SectionId].StartBarIndex)
            .ToList();

        var plan = new SongDirectionPlan(
            Guid.NewGuid(),
            labels.SongId,
            labels.VersionId,
            motifs.VersionId,
            baseRevision,
            nowUtc,
            ordered);

        plan.Validate();
        return plan;
    }
}
