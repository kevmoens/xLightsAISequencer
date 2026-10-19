using XlightsAI.Studio.Composition;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Shell;

/// <summary>
/// UI-only bindable projection of one <see cref="SectionVisualDirection"/> for SCREEN 12 CHOREOGRAPHY's
/// sectional energy/color story table. Same "Avalonia-free Shell layer projects pure domain data into
/// something directly bindable" convention <see cref="VisualBudgetCandidateRowView"/> already established.
/// </summary>
/// <param name="SectionId">The section this row describes.</param>
/// <param name="Kind">The section's own already-classified label.</param>
/// <param name="PaletteId">The section's own planned palette.</param>
/// <param name="Intensity">The section's own planned intensity, in <c>[0,1]</c>.</param>
/// <param name="IntensityPercent"><see cref="Intensity"/> rounded to the nearest whole percent, for a directly bindable progress-bar/label display.</param>
public sealed record SectionVisualDirectionRowView(
    Guid SectionId,
    SectionKind Kind,
    string PaletteId,
    double Intensity,
    int IntensityPercent);
