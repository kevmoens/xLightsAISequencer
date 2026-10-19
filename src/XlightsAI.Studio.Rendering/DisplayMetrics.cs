namespace XlightsAI.Studio.Rendering;

/// <summary>
/// PHASE-13 Step 088's own new contract ("Compute display-level metrics for brightness, coverage, spatial
/// motion and density") -- the proposed Section 3 signatures stop at Step 087's decoded-frame shapes and
/// define no metrics type, so this is additive, not a deviation from an existing verbatim signature. Every
/// value is a grounded, documented measurement over a window of <see cref="PreviewFrame"/> samples (Section
/// 2: "choose properties that can be grounded... not an unjustified scalar 'beauty' claim"), never a
/// subjective artistic score.
/// </summary>
/// <param name="MeanBrightness">
/// Mean normalized luma (ITU-R BT.601: <c>0.299R + 0.587G + 0.114B</c>, divided by 255) across every
/// illuminated point in every frame of the window, in <c>[0, 1]</c>. <c>0</c> when no point was ever
/// illuminated.
/// </param>
/// <param name="CoverageRatio">
/// Count of distinct <see cref="PixelPoint.Model"/> ids illuminated at least once anywhere in the window,
/// divided by the window's known total addressable point count, in <c>[0, 1]</c>. Cumulative -- a point lit
/// in exactly one frame counts the same as one lit in every frame.
/// </param>
/// <param name="SpatialMotion">
/// Mean Euclidean distance, in normalized <c>[0, 1]</c> preview-coordinate units, between the illuminated
/// centroid (mean <see cref="PixelPoint.X"/>/<see cref="PixelPoint.Y"/> of that frame's illuminated points)
/// of each consecutive pair of frames that both have at least one illuminated point. <c>0</c> when fewer
/// than two such frames exist (nothing moved, or nothing was ever lit to move).
/// </param>
/// <param name="DensityRatio">
/// Mean, across every frame in the window, of that single frame's illuminated point count divided by the
/// window's known total addressable point count, in <c>[0, 1]</c>. Instantaneous -- unlike
/// <see cref="CoverageRatio"/>, a point lit in every frame counts every time, not once.
/// </param>
public sealed record DisplayMetrics(
    double MeanBrightness,
    double CoverageRatio,
    double SpatialMotion,
    double DensityRatio);
