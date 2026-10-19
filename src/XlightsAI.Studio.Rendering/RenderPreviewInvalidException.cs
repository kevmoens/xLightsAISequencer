namespace XlightsAI.Studio.Rendering;

/// <summary>
/// Thrown by <see cref="ILocalPreviewRenderer.PreviewAsync"/> when the requested preview window is malformed --
/// not a non-empty half-open <c>[start, end)</c> range. Always thrown before any <see cref="PreviewFrame"/> is
/// produced (checked before the first frame of the async iterator is yielded), mirroring
/// <see cref="Xlights.EffectPlanInvalidException"/>'s own "no partial/invalid output is ever produced" convention.
/// </summary>
public sealed class RenderPreviewInvalidException : Exception
{
    public RenderPreviewInvalidException(string message)
        : base(message)
    {
    }
}
