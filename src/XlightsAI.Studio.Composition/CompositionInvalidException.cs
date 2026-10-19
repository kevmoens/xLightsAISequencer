namespace XlightsAI.Studio.Composition;

/// <summary>
/// Thrown when a <see cref="MusicalRoleAssignmentCatalog"/> operation cannot proceed because its inputs are
/// invalid -- an empty/unknown <see cref="MusicalRoleAssignment.SourceTrackId"/>, an undefined
/// <see cref="MusicalRole"/>, an unrecognized <see cref="PrecedenceTier"/>, a <see cref="MusicalRoleAssignment.SpatialPathId"/>
/// absent from the supplied path set, or a clear/assign against a pair with no live assignment to replace.
/// Thrown before any load/write is attempted -- this step's own required failure/edge case, verbatim:
/// "Invalid input, cancellation or stale persisted state must not corrupt the project."
/// </summary>
public sealed class CompositionInvalidException : Exception
{
    public CompositionInvalidException(string reason) : base(reason)
    {
        Reason = reason;
    }

    public string Reason { get; }
}
