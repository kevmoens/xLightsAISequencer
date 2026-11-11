namespace XlightsAI.Studio.Rendering;

/// <summary>
/// Build-plan Phase 21 §2a: "the mean pixel luminance averaged over every pixel in the fixture, lit or not".
/// Operates on the exact same <see cref="PreviewFrame"/>s <c>EffectVisualCharacterizer</c>'s AI description is
/// written from -- no new render pipeline, zero guessing. Deliberately distinct from
/// <see cref="DisplayMetricsCalculator"/>'s own <c>MeanBrightness</c>, which averages only over
/// <see cref="PreviewFrame.Illuminated"/> (the lit subset) -- that metric answers "how bright do the lit
/// pixels look," this one answers "how bright does the whole canvas look," which is the one the user
/// explicitly confirmed: a sparse, scattered effect (Fireworks, Snowflakes, Snowstorm, Meteors) must measure
/// low here even though its own individually-lit pixels are intense, because most of its canvas is dark at
/// any instant -- coverage sparsity IS dimness for this purpose.
/// </summary>
public static class EffectFrameBrightnessMeasurer
{
    /// <param name="frames">A fixture's own full, real decoded <see cref="PreviewFrame"/> sequence.</param>
    /// <param name="totalPixelCount">The fixture's own total addressable pixel count (e.g. 20 for the Low-resolution fixture, 900 for High) -- every pixel counts toward the average, not just the ones a given frame happens to light.</param>
    /// <returns>
    /// <c>MeanBrightness</c>: the average, across every frame, of that frame's own whole-canvas mean luminance
    /// (0-1). <c>PeakBrightness</c>: the single brightest frame's own whole-canvas mean (0-1) -- still a
    /// whole-frame average, never a single pixel's own peak value.
    /// </returns>
    public static (double MeanBrightness, double PeakBrightness) Measure(IReadOnlyList<PreviewFrame> frames, int totalPixelCount)
    {
        ArgumentNullException.ThrowIfNull(frames);
        if (totalPixelCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalPixelCount), totalPixelCount, "Total addressable pixel count must be positive.");
        }

        if (frames.Count == 0)
        {
            return (0.0, 0.0);
        }

        var frameAverageSum = 0.0;
        var peak = 0.0;

        foreach (var frame in frames)
        {
            var litLuminanceSum = 0.0;
            foreach (var point in frame.Illuminated)
            {
                litLuminanceSum += ((0.299 * point.R) + (0.587 * point.G) + (0.114 * point.B)) / 255.0;
            }

            var wholeCanvasAverage = litLuminanceSum / totalPixelCount;
            frameAverageSum += wholeCanvasAverage;
            if (wholeCanvasAverage > peak)
            {
                peak = wholeCanvasAverage;
            }
        }

        return (frameAverageSum / frames.Count, peak);
    }
}
