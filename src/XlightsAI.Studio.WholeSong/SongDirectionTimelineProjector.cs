using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.WholeSong;

/// <summary>Default <see cref="ISongDirectionTimelineProjector"/>. Pure, deterministic, no I/O.</summary>
public sealed class SongDirectionTimelineProjector : ISongDirectionTimelineProjector
{
    public IReadOnlyList<SongDirectionTimelineEntry> Project(SongDirectionPlan plan, SectionLabelAnalysis labels)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(labels);

        if (plan.SourceSectionLabelVersionId != labels.VersionId)
        {
            throw new SongDirectionInvalidException(
                $"SongDirectionPlan '{plan.Id}' was validated against SectionLabelAnalysis " +
                $"'{plan.SourceSectionLabelVersionId}' but '{labels.VersionId}' was supplied for projection -- " +
                "the structure analysis has been rebuilt since this plan was created; re-plan before inspecting it.");
        }

        var labelsBySectionId = labels.Labels.ToDictionary(l => l.Id);

        var entries = new List<SongDirectionTimelineEntry>(plan.Sections.Count);
        foreach (var section in plan.Sections)
        {
            if (!labelsBySectionId.TryGetValue(section.SectionId, out var label))
            {
                throw new SongDirectionInvalidException(
                    $"Section id '{section.SectionId}' in SongDirectionPlan '{plan.Id}' does not exist in the supplied SectionLabelAnalysis.");
            }

            entries.Add(new SongDirectionTimelineEntry(
                section.SectionId,
                section.Kind,
                label.StartBarIndex,
                label.EndBarIndexExclusive,
                section.TargetEnergy,
                section.TargetContrast,
                section.PaletteId,
                section.FocalRole,
                section.MotifGroupIds));
        }

        return entries.OrderBy(e => e.StartBarIndex).ToList();
    }
}
