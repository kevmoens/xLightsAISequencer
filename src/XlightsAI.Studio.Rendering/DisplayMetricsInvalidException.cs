namespace XlightsAI.Studio.Rendering;

/// <summary>
/// Thrown by <see cref="IDisplayMetricsCalculator"/> when the requested window cannot be measured honestly --
/// a null frame window, a window containing a null frame, or a non-positive total addressable point count
/// (every ratio in <see cref="DisplayMetrics"/> is a fraction of that count; dividing by zero or a negative
/// count would silently fabricate a value instead of reporting the real failure). Always thrown before any
/// <see cref="DisplayMetrics"/> is produced, mirroring <see cref="RenderPreviewInvalidException"/>'s own
/// "no partial/invalid output is ever produced" convention.
/// </summary>
public sealed class DisplayMetricsInvalidException : Exception
{
    public DisplayMetricsInvalidException(string message)
        : base(message)
    {
    }
}
