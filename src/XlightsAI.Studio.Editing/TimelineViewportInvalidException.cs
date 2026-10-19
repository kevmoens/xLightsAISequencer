namespace XlightsAI.Studio.Editing;

/// <summary>
/// Thrown by <see cref="TimelineViewport.Create"/> when a caller supplies invalid timing
/// boundaries (e.g. an inverted content window) or an invalid zoom level. PHASE-03 Step 016's own
/// required failure/edge case: "invalid timing boundaries... must be handled explicitly" — this
/// type is the explicit, named signal, never a silent clamp or an unrelated exception type.
/// </summary>
public sealed class TimelineViewportInvalidException : Exception
{
    public TimelineViewportInvalidException(string message)
        : base(message)
    {
    }
}
