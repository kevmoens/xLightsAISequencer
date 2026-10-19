namespace XlightsAI.Studio.Composition;

/// <summary>
/// PHASE-10 Step 066's own named event-precedence tiers, taken verbatim from PHASE-10.md Section 3's own
/// architectural design notes: "Default precedence: user-locked &gt; manual &gt; accepted motif &gt; new
/// automated candidate." Declared as string constants, not an enum, the same convention
/// <see cref="XlightsAI.Studio.Display.RoleTag"/>/<see cref="XlightsAI.Studio.Display.SpatialEdge.Relation"/>
/// already established -- a future step can add a new tier without a breaking contract change here.
/// <see cref="All"/>'s own declared order is this tier's own precedence rank, highest first; consumed by
/// <see cref="RolePrecedenceResolver"/>.
/// </summary>
public static class PrecedenceTier
{
    /// <summary>A human has explicitly locked this assignment; nothing may ever outrank it.</summary>
    public const string UserLocked = "UserLocked";

    /// <summary>A human chose this assignment directly, without locking it.</summary>
    public const string Manual = "Manual";

    /// <summary>Inherited from an already-accepted motif/phrase decision elsewhere in the song.</summary>
    public const string AcceptedMotif = "AcceptedMotif";

    /// <summary>A newly generated, not-yet-reviewed automated candidate -- the lowest precedence tier.</summary>
    public const string Automated = "Automated";

    /// <summary>Every precedence tier this project currently recognizes, in rank order (index 0 = highest precedence) -- used for validation and as <see cref="RolePrecedenceResolver"/>'s own primary sort key.</summary>
    public static readonly IReadOnlyList<string> All = [UserLocked, Manual, AcceptedMotif, Automated];
}
