namespace XlightsAI.Studio.Rendering;

/// <summary>
/// Build-plan Phase 7c "visual critique": turns one real, already-decoded <see cref="PreviewFrame"/>
/// into a small PNG a vision-model prompt can carry. Deliberately not display-quality -- a flat,
/// un-antialiased point-plot at a fixed small resolution (default 320x240) is all a judging call needs,
/// and keeping it simple keeps <see cref="MinimalPngEncoder"/> (this solution's only PNG writer, written
/// because no imaging package is referenced anywhere in it) trivial. <see cref="PixelPoint.X"/>/<see cref="PixelPoint.Y"/>
/// are already normalized <c>[0,1]</c> preview coordinates (see that type's own remarks), so no layout
/// bounds lookup is needed -- every point maps directly onto the raster regardless of which models it
/// came from.
/// </summary>
public static class PreviewFrameRasterizer
{
    private const int DefaultWidth = 320;
    private const int DefaultHeight = 240;

    /// <summary>
    /// Rasterizes every <see cref="PreviewFrame.Illuminated"/> point as a small filled square on a black
    /// background, then encodes the result as PNG bytes. A frame with zero illuminated points still
    /// produces a valid, fully-black PNG -- genuinely dark moments must render as genuinely dark images,
    /// never throw.
    /// </summary>
    public static byte[] RasterizeToPng(PreviewFrame frame, int width = DefaultWidth, int height = DefaultHeight, int pointRadiusPixels = 2)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width and height must be positive.");
        }

        var rgb = new byte[width * height * 3]; // zero-initialized: pure black background.

        foreach (var point in frame.Illuminated)
        {
            var cx = (int)Math.Round(Math.Clamp(point.X, 0.0, 1.0) * (width - 1));
            // Preview-coordinate Y grows downward in every other consumer of PixelPoint in this
            // repository (DisplayMetricsCalculator's own centroid math treats it the same way) --
            // kept consistent here rather than flipping it, so a rasterized frame's "up" visually
            // matches every other preview surface.
            var cy = (int)Math.Round(Math.Clamp(point.Y, 0.0, 1.0) * (height - 1));

            for (var dy = -pointRadiusPixels; dy <= pointRadiusPixels; dy++)
            {
                var y = cy + dy;
                if (y < 0 || y >= height)
                {
                    continue;
                }

                for (var dx = -pointRadiusPixels; dx <= pointRadiusPixels; dx++)
                {
                    var x = cx + dx;
                    if (x < 0 || x >= width)
                    {
                        continue;
                    }

                    var offset = ((y * width) + x) * 3;
                    rgb[offset] = point.R;
                    rgb[offset + 1] = point.G;
                    rgb[offset + 2] = point.B;
                }
            }
        }

        return MinimalPngEncoder.Encode(width, height, rgb);
    }
}
