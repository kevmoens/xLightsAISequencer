using XlightsAI.Studio.Composition;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.WholeSong;

/// <summary>
/// PHASE-14 Step 090's own required acceptance, verbatim: "Plan renders as inspectable timeline summary."
/// One <see cref="SongSectionDirection"/> joined back to the bar-range position its source
/// <c>SectionLabelCandidate</c> actually occupies, so a plan can be inspected/rendered purely as data --
/// no Avalonia view is required to satisfy this step's own acceptance text (see
/// <c>implementation-evidence/PHASE-14/STEP-090.md</c> for the disclosed decision), exactly as PHASE-13
/// Steps 086-089 already established for comparable "inspectable output, no new screen" acceptance text.
/// </summary>
/// <param name="SectionId">The section this entry describes.</param>
/// <param name="Kind">The section's own already-classified label.</param>
/// <param name="StartBarIndex">The first bar (inclusive) this section covers -- carried from the source <c>SectionLabelCandidate</c>, never recomputed here.</param>
/// <param name="EndBarIndexExclusive">One past the last bar this section covers (half-open).</param>
/// <param name="TargetEnergy">This section's own validated target energy.</param>
/// <param name="TargetContrast">This section's own validated target contrast.</param>
/// <param name="PaletteId">This section's own validated palette id.</param>
/// <param name="FocalRole">This section's own focal musical role.</param>
/// <param name="MotifGroupIds">Every motif group this section is linked to.</param>
public sealed record SongDirectionTimelineEntry(
    Guid SectionId,
    SectionKind Kind,
    int StartBarIndex,
    int EndBarIndexExclusive,
    double TargetEnergy,
    double TargetContrast,
    string PaletteId,
    MusicalRole FocalRole,
    IReadOnlyList<Guid> MotifGroupIds);
