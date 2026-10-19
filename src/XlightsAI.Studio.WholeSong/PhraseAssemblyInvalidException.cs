namespace XlightsAI.Studio.WholeSong;

/// <summary>
/// Thrown when a phrase assembly request is malformed -- an empty/duplicate phrase id, an invalid
/// <c>[Start,End)</c> window, phrase windows out of order or overlapping each other, a seam-request set
/// that does not exactly cover every consecutive phrase pair once, an effect/window outside the permitted
/// <see cref="XlightsAI.Studio.Editing.EditScope"/>, or a <see cref="SeamStrategy.Hold"/>/<see cref="SeamStrategy.Bridge"/>
/// request made against a seam where an effect actually overlaps the boundary. Thrown before any
/// <see cref="XlightsAI.Studio.Xlights.SequenceDocument"/> mutation is built -- this step's own required
/// failure/edge case, verbatim: "Malformed proposal... must not auto-commit."
/// </summary>
public sealed class PhraseAssemblyInvalidException : Exception
{
    public PhraseAssemblyInvalidException(string reason) : base(reason)
    {
        Reason = reason;
    }

    public string Reason { get; }
}
