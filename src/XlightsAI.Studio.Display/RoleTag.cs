namespace XlightsAI.Studio.Display;

/// <summary>
/// PHASE-09 Step 063 ("Persist role tags: lead, rhythm, ambient, hero and accent"): the named artistic
/// role tags <see cref="IRoleAssignmentCatalog.AssignAsync"/> accepts. Deliberately string constants, not
/// an enum, the same convention <see cref="SpatialEdge.Relation"/>/<see cref="DirectionPolicy"/> already
/// established -- a future step can add a new tag without a breaking contract change here.
/// </summary>
public static class RoleTag
{
    /// <summary>The primary, most prominent voice for a given scope -- the step's own required text, verbatim.</summary>
    public const string Lead = "Lead";

    /// <summary>A supporting, rhythmic/percussive voice.</summary>
    public const string Rhythm = "Rhythm";

    /// <summary>A background, low-prominence voice that fills out the display without drawing focus.</summary>
    public const string Ambient = "Ambient";

    /// <summary>A one-off, especially prominent feature prop for a given scope.</summary>
    public const string Hero = "Hero";

    /// <summary>A brief, punctuating highlight.</summary>
    public const string Accent = "Accent";

    /// <summary>Every role tag <see cref="IRoleAssignmentCatalog"/> currently recognizes -- used only to produce a clear rejection message; never consumed for branching logic elsewhere.</summary>
    public static readonly IReadOnlyList<string> All = [Lead, Rhythm, Ambient, Hero, Accent];
}
