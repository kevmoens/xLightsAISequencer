namespace XlightsAI.Studio.WholeSong;

/// <summary>
/// Thrown when an <see cref="IMotifVariationScheduler.Schedule"/> call cannot proceed, or
/// <see cref="MotifVariationPlan.Validate"/> fails, because its inputs are invalid -- an out-of-range
/// <c>SectionEnergy</c>, an empty/duplicate occurrence request, an empty request list, or a non-numeric
/// <c>"Intensity."</c>-prefixed effect parameter this step's own intensity scaling cannot safely transform.
/// Thrown before any plan is constructed or any persistence is attempted -- this step's own required
/// failure/edge case, verbatim: "Invalid input, cancellation or stale persisted state must not corrupt the
/// project." Mirrors <c>SongDirectionInvalidException</c>'s/<c>PhraseAssemblyInvalidException</c>'s own
/// convention.
/// </summary>
public sealed class MotifVariationInvalidException : Exception
{
    public MotifVariationInvalidException(string reason) : base(reason)
    {
        Reason = reason;
    }

    public string Reason { get; }
}
