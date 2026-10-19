using XlightsAI.Studio.Composition;
using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Shell;

/// <summary>
/// UI-only projection of one <see cref="VisualBudgetCandidateResult"/> for SCREEN 12 CHOREOGRAPHY's visual
/// density/focus budget preview table (PHASE-10 Step 068). Same "Avalonia-free Shell layer projects pure
/// domain data into something directly bindable" convention <see cref="RoleRoutingRowView"/>/<see cref="DisplayNodeRowView"/>
/// already established.
/// </summary>
/// <param name="SourceTrackId">The instrument/stem track this candidate gesture belongs to.</param>
/// <param name="Role">The semantic role this candidate plays.</param>
/// <param name="IsFocal">Whether this candidate competes for <see cref="VisualBudget.MaxConcurrentFocalProps"/>.</param>
/// <param name="OriginalWindow">The candidate's own originally requested window, before any trim.</param>
/// <param name="Decision">Whether this candidate was admitted unmodified, trimmed, or dropped.</param>
/// <param name="AdmittedWindow">The window actually admitted, or <see langword="null"/> when <see cref="Decision"/> is <see cref="VisualBudgetDecision.Dropped"/>.</param>
/// <param name="Reason">A human-readable explanation, populated exactly when <see cref="Decision"/> is not <see cref="VisualBudgetDecision.Accepted"/>.</param>
public sealed record VisualBudgetCandidateRowView(
    string SourceTrackId,
    MusicalRole Role,
    bool IsFocal,
    TimeWindow OriginalWindow,
    VisualBudgetDecision Decision,
    TimeWindow? AdmittedWindow,
    string? Reason);
