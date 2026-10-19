using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.WholeSong;

/// <summary>
/// Step 090's own required acceptance, verbatim: "Plan renders as inspectable timeline summary." A pure
/// read-side projection -- never persisted itself, never a source of truth, always re-derivable from an
/// already-persisted <see cref="SongDirectionPlan"/> plus the <c>SectionLabelAnalysis</c> it was built
/// against.
/// </summary>
public interface ISongDirectionTimelineProjector
{
    /// <summary>
    /// Joins every <paramref name="plan"/> section back to its own bar-range position in
    /// <paramref name="labels"/>, ordered by <see cref="SongDirectionTimelineEntry.StartBarIndex"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="plan"/> or <paramref name="labels"/> is <see langword="null"/>.</exception>
    /// <exception cref="SongDirectionInvalidException"><paramref name="labels"/>'s <c>VersionId</c> does not match <paramref name="plan"/>'s own <see cref="SongDirectionPlan.SourceSectionLabelVersionId"/> (a stale/mismatched structure analysis) or a <paramref name="plan"/> section id is absent from <paramref name="labels"/>.</exception>
    IReadOnlyList<SongDirectionTimelineEntry> Project(SongDirectionPlan plan, SectionLabelAnalysis labels);
}
