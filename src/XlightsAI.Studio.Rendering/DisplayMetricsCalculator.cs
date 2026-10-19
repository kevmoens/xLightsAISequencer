using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Rendering;

/// <summary>
/// Default <see cref="IDisplayMetricsCalculator"/>. Pure, synchronous and deterministic -- the same frame
/// window and total point count always reduce to the exact same <see cref="DisplayMetrics"/>, with no
/// partial/mutable state a cancellation or stale call could ever leave behind (mirroring Step 087's
/// <c>FseqRenderedFrameDecoder</c> remarks on the same point).
/// </summary>
public sealed class DisplayMetricsCalculator : IDisplayMetricsCalculator
{
    public DisplayMetrics Compute(IReadOnlyList<PreviewFrame> frames, int totalDisplayPointCount)
    {
        if (frames is null)
        {
            throw new DisplayMetricsInvalidException("Frame window must not be null.");
        }

        if (totalDisplayPointCount <= 0)
        {
            throw new DisplayMetricsInvalidException(
                $"Total addressable point count must be a positive number; got {totalDisplayPointCount}.");
        }

        for (var i = 0; i < frames.Count; i++)
        {
            if (frames[i] is null)
            {
                throw new DisplayMetricsInvalidException($"Frame at index {i} of the window is null.");
            }
        }

        if (frames.Count == 0)
        {
            return new DisplayMetrics(MeanBrightness: 0.0, CoverageRatio: 0.0, SpatialMotion: 0.0, DensityRatio: 0.0);
        }

        var brightnessSum = 0.0;
        var litPointCount = 0;
        var everLitModels = new HashSet<ModelId>();
        var densitySum = 0.0;

        (double X, double Y)? previousCentroid = null;
        var motionSum = 0.0;
        var motionTransitions = 0;

        foreach (var frame in frames)
        {
            var illuminated = frame.Illuminated;
            densitySum += Math.Min(1.0, illuminated.Count / (double)totalDisplayPointCount);

            if (illuminated.Count == 0)
            {
                continue;
            }

            var sumX = 0.0;
            var sumY = 0.0;
            foreach (var point in illuminated)
            {
                brightnessSum += ((0.299 * point.R) + (0.587 * point.G) + (0.114 * point.B)) / 255.0;
                litPointCount++;
                everLitModels.Add(point.Model);
                sumX += point.X;
                sumY += point.Y;
            }

            var centroid = (X: sumX / illuminated.Count, Y: sumY / illuminated.Count);
            if (previousCentroid is { } previous)
            {
                var dx = centroid.X - previous.X;
                var dy = centroid.Y - previous.Y;
                motionSum += Math.Sqrt((dx * dx) + (dy * dy));
                motionTransitions++;
            }

            previousCentroid = centroid;
        }

        var meanBrightness = litPointCount == 0 ? 0.0 : brightnessSum / litPointCount;
        var coverageRatio = Math.Min(1.0, everLitModels.Count / (double)totalDisplayPointCount);
        var densityRatio = densitySum / frames.Count;
        var spatialMotion = motionTransitions == 0 ? 0.0 : motionSum / motionTransitions;

        return new DisplayMetrics(meanBrightness, coverageRatio, spatialMotion, densityRatio);
    }
}
