namespace XlightsAI.Studio.Rendering;

/// <summary>
/// PHASE-13 Step 088 ("Compute display-level metrics for brightness, coverage, spatial motion and density"):
/// reduces a window of <see cref="PreviewFrame"/> samples -- produced today by Step 085's approximate
/// <see cref="ILocalPreviewRenderer"/>, and by any future model-mapped interpretation of Step 087's real
/// <c>DecodedRenderOutput</c> -- into grounded <see cref="DisplayMetrics"/> a caller can compare between a
/// candidate and an accepted baseline (Section 3: "For score comparisons choose properties that can be
/// grounded... not an unjustified scalar 'beauty' claim").
/// </summary>
public interface IDisplayMetricsCalculator
{
    /// <exception cref="DisplayMetricsInvalidException">
    /// <paramref name="frames"/> is null, contains a null frame, or <paramref name="totalDisplayPointCount"/>
    /// is not a positive number.
    /// </exception>
    DisplayMetrics Compute(IReadOnlyList<PreviewFrame> frames, int totalDisplayPointCount);
}
