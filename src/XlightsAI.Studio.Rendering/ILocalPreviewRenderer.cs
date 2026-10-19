using XlightsAI.Studio.Display;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Rendering;

/// <summary>
/// PHASE-13 Section 3 proposed contract, verbatim signature: the fast, approximate preview tier -- "guaranteed
/// supported gestures from our compiler," never the authoritative xLights renderer (<c>IRenderAdapter</c>,
/// Step 086's own later scope). A pure function of its inputs: never reads or writes project/document state,
/// so invalid input or cancellation can never leave any persisted state corrupted (Step 085's own required
/// failure/edge case) -- there is nothing this renderer itself persists.
/// </summary>
public interface ILocalPreviewRenderer
{
    /// <exception cref="RenderPreviewInvalidException"><paramref name="window"/> is not a non-empty half-open <c>[start, end)</c> range.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was cancelled before or during enumeration.</exception>
    IAsyncEnumerable<PreviewFrame> PreviewAsync(
        SequenceDocument sequence,
        SpatialDisplayGraph layout,
        TimeWindow window,
        CancellationToken ct);
}
