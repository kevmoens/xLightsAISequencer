namespace XlightsAI.Studio.Display;

/// <summary>
/// Thrown when <see cref="IDisplayGraphBuilder.Build"/> cannot proceed because its inputs are invalid --
/// an unusable <see cref="GeometryPolicy"/>, an imported <c>DisplayGraph</c> whose model/group names
/// collide into an ambiguous stable node id, or an alias that cannot be resolved against any imported
/// name. Thrown before any node/edge is produced and before any store write is attempted (Step 059's own
/// failure/edge case: "Invalid model/format/version must fail safely without rewriting source files").
/// </summary>
public sealed class SpatialDisplayGraphInvalidException : Exception
{
    public SpatialDisplayGraphInvalidException(string reason) : base(reason)
    {
        Reason = reason;
    }

    public string Reason { get; }
}
