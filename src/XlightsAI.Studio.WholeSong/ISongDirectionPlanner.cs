using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.WholeSong;

/// <summary>
/// PHASE-14 Step 090's own scoped realization of PHASE-14.md Section 3's proposed <c>IWholeSongPlanner</c>
/// contract -- narrowed to the song-level direction layer only (no phrase-plan assembly; see
/// <see cref="SongDirectionPlan"/>'s own doc comment for the disclosed deviation).
/// </summary>
public interface ISongDirectionPlanner
{
    /// <summary>
    /// Cross-validates every <paramref name="sectionInputs"/> entry against <paramref name="labels"/> and
    /// <paramref name="motifs"/> (every <see cref="SongSectionDirectionInput.SectionId"/> must exist in
    /// <paramref name="labels"/>; every <see cref="SongSectionDirectionInput.MotifGroupIds"/> entry must
    /// exist in <paramref name="motifs"/>), then builds and self-validates a <see cref="SongDirectionPlan"/>
    /// ordered by each section's own <c>SectionLabelCandidate.StartBarIndex</c>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="labels"/>, <paramref name="motifs"/> or <paramref name="sectionInputs"/> is <see langword="null"/>.</exception>
    /// <exception cref="SongDirectionInvalidException">A section id or motif group id is unknown, inputs are malformed, or <paramref name="sectionInputs"/> is empty -- see <see cref="SongDirectionPlan.Validate"/> for the full structural rule set.</exception>
    SongDirectionPlan Plan(
        SectionLabelAnalysis labels,
        MotifClusteringAnalysis motifs,
        IReadOnlyList<SongSectionDirectionInput> sectionInputs,
        long baseRevision,
        DateTimeOffset nowUtc);
}
