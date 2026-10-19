using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.WholeSong;

/// <summary>
/// Thrown when an assembled effect still collides with another effect on the same <see cref="Target"/>
/// and layer after every layer up to <see cref="PhraseSeamAssembler.MaxLayers"/> has been tried -- this
/// step's own required failure/edge case, verbatim: "...exhausted retry budget must not auto-commit." No
/// partial <see cref="XlightsAI.Studio.Xlights.SequenceDocument"/> is ever returned when this is thrown;
/// the whole assembly attempt is rejected atomically, the same "no partial mutation" rule every other
/// Editing/Composition-project command in this repository already enforces.
/// </summary>
public sealed class PhraseAssemblyExhaustedException : Exception
{
    public PhraseAssemblyExhaustedException(ModelId target, TimeWindow window, int maxLayersAttempted)
        : base(
            $"Could not place an effect on '{target.Value}' over [{window.StartTicks}, {window.EndTicks}) " +
            $"without colliding with an existing effect after trying all {maxLayersAttempted} available " +
            "layers; the seam-resolution retry budget is exhausted. No document was committed.")
    {
        Target = target;
        Window = window;
        MaxLayersAttempted = maxLayersAttempted;
    }

    public ModelId Target { get; }

    public TimeWindow Window { get; }

    public int MaxLayersAttempted { get; }
}
