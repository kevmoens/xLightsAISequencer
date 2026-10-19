namespace XlightsAI.Studio.Refinement;

/// <summary>
/// PHASE-12 Section 3's own proposed state machine graph, made explicit and enforced rather than left
/// implicit in caller code: "Queued -> Generating -> Validating -> PreviewReady/Blocked ->
/// AwaitingApproval -> Accepted/Rejected." <see cref="RefinementStatus.Accepted"/>,
/// <see cref="RefinementStatus.Rejected"/> and <see cref="RefinementStatus.Cancelled"/> are terminal --
/// no outgoing edge exists from any of them. This is the concrete mechanism behind this step's own
/// required failure/edge case, verbatim: "no-improvement and exhausted retry budget must not
/// auto-commit" -- a run can reach <see cref="RefinementStatus.Accepted"/> only from
/// <see cref="RefinementStatus.AwaitingApproval"/>, never directly from
/// <see cref="RefinementStatus.Generating"/>/<see cref="RefinementStatus.Validating"/>/
/// <see cref="RefinementStatus.PreviewReady"/>/<see cref="RefinementStatus.Blocked"/>, so an
/// exhausted-budget or no-improvement stop (which this step's own <see cref="IRefinementRunStore"/>
/// only ever drives to <see cref="RefinementStatus.AwaitingApproval"/> or
/// <see cref="RefinementStatus.Blocked"/>, never <see cref="RefinementStatus.Accepted"/>) cannot reach
/// <see cref="RefinementStatus.Accepted"/> without a distinct, later, human-approval transition (owned
/// by a future step -- this step does not implement that approval UI, only the fact that the state
/// machine makes the illegal shortcut structurally impossible to persist).
/// </summary>
public static class RefinementStatusTransitions
{
    private static readonly IReadOnlyDictionary<RefinementStatus, RefinementStatus[]> Edges =
        new Dictionary<RefinementStatus, RefinementStatus[]>
        {
            [RefinementStatus.Queued] = [RefinementStatus.Generating, RefinementStatus.Cancelled],
            [RefinementStatus.Generating] = [RefinementStatus.Validating, RefinementStatus.Blocked, RefinementStatus.Cancelled],
            [RefinementStatus.Validating] = [RefinementStatus.PreviewReady, RefinementStatus.Blocked, RefinementStatus.Cancelled],
            [RefinementStatus.PreviewReady] = [RefinementStatus.Generating, RefinementStatus.AwaitingApproval, RefinementStatus.Blocked, RefinementStatus.Cancelled],
            [RefinementStatus.AwaitingApproval] = [RefinementStatus.Accepted, RefinementStatus.Rejected, RefinementStatus.Cancelled],
            [RefinementStatus.Accepted] = [],
            [RefinementStatus.Rejected] = [],
            [RefinementStatus.Blocked] = [],
            [RefinementStatus.Cancelled] = [],
        };

    /// <summary>
    /// <see langword="true"/> when <paramref name="to"/> is reachable from <paramref name="from"/> in one
    /// step, or <paramref name="to"/> equals <paramref name="from"/> (an idempotent re-save of the same
    /// state -- required for this step's own "interrupted run resumes idempotently" acceptance, where a
    /// resumed run re-persists the exact in-flight state it already held before the interruption).
    /// </summary>
    public static bool CanTransition(RefinementStatus from, RefinementStatus to) =>
        to == from || Edges[from].Contains(to);

    /// <summary><see langword="true"/> when <paramref name="status"/> has no outgoing transition (a run in this state can never change state again).</summary>
    public static bool IsTerminal(RefinementStatus status) => Edges[status].Length == 0;
}
