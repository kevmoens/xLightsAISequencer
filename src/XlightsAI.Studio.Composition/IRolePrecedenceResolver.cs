namespace XlightsAI.Studio.Composition;

/// <summary>
/// PHASE-10 Step 066's own required acceptance, verbatim: "Synthetic role mapping yields deterministic
/// priorities." Pure, stateless and deterministic -- given the same set of <see cref="MusicalRoleAssignment"/>s
/// (in any input order), <see cref="ResolveOrder"/> always returns the same output order. Resolves no
/// actual time-overlap or effect-layer collision itself (that is Step 071's own, later scope: "Compose and
/// resolve overlapping effects with layer/priority strategies") -- this step only defines the ranking rule
/// a later compiler consults when two or more assignments do compete.
/// </summary>
public interface IRolePrecedenceResolver
{
    /// <summary>
    /// Returns <paramref name="assignments"/> ordered from highest to lowest event precedence. Rank order,
    /// highest first: (1) <see cref="Composition.PrecedenceTier"/> rank (<see cref="Composition.PrecedenceTier.All"/>'s
    /// own declared order -- "user-locked &gt; manual &gt; accepted motif &gt; new automated candidate",
    /// verbatim); (2) <see cref="MusicalRoleAssignment.Priority"/>, higher wins; (3) <see cref="MusicalRole"/>
    /// declared rank (<see cref="MusicalRole"/>'s own declaration order -- Lead first, Atmosphere last);
    /// (4) <see cref="MusicalRoleAssignment.SourceTrackId"/>, ordinal ascending -- a final, purely
    /// deterministic tie-break so two otherwise-identical assignments always sort the same way regardless
    /// of input order.
    /// </summary>
    IReadOnlyList<MusicalRoleAssignment> ResolveOrder(IReadOnlyList<MusicalRoleAssignment> assignments);
}
