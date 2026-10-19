namespace XlightsAI.Studio.WholeSong;

/// <summary>
/// Thrown when a <see cref="SongSectionDirection"/>/<see cref="SongDirectionPlan"/> cannot be built or
/// persisted because its inputs are invalid -- a section id or motif group id absent from the song's own
/// already-accepted <c>SectionLabelAnalysis</c>/<c>MotifClusteringAnalysis</c>, an out-of-range energy or
/// contrast value, a blank palette id, a duplicate section id, or an empty section list. Thrown before any
/// plan is constructed or any persistence is attempted -- this step's own required failure/edge case,
/// verbatim: "Invalid input, cancellation or stale persisted state must not corrupt the project."
/// </summary>
public sealed class SongDirectionInvalidException : Exception
{
    public SongDirectionInvalidException(string reason) : base(reason)
    {
        Reason = reason;
    }

    public string Reason { get; }
}
