using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Composition;

/// <summary>
/// PHASE-10 Step 068: one candidate visual gesture competing for <see cref="VisualBudget"/> admission over
/// <see cref="Window"/>. Deliberately wraps an already-accepted <see cref="MusicalRoleAssignment"/> (Step
/// 066's own contract) rather than duplicating its <c>SourceTrackId</c>/<c>Role</c>/<c>PrecedenceTier</c>/
/// <c>Priority</c> fields -- <see cref="VisualBudgetResolver"/> reuses the already-accepted
/// <see cref="IRolePrecedenceResolver"/> tie-break rule directly against <see cref="Assignment"/> instead of
/// re-implementing it.
/// </summary>
/// <param name="Assignment">The underlying role assignment this candidate gesture realizes; also this candidate's own precedence identity for <see cref="IRolePrecedenceResolver"/>.</param>
/// <param name="Window">The time span this gesture would occupy if admitted unmodified.</param>
/// <param name="Brightness">This gesture's own brightness-proxy contribution, in <c>[0,1]</c>, if admitted.</param>
/// <param name="IsFocal">
/// <see langword="true"/> when this gesture competes for <see cref="VisualBudget.MaxConcurrentFocalProps"/>
/// (the audience's primary visual attention); <see langword="false"/> for supporting/background gestures that
/// never count against the focal-concurrency cap (they can still be dropped by the density/brightness caps).
/// </param>
public sealed record VisualBudgetCandidate(
    MusicalRoleAssignment Assignment,
    TimeWindow Window,
    double Brightness,
    bool IsFocal);
