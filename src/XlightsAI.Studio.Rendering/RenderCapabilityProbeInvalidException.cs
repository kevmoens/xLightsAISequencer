namespace XlightsAI.Studio.Rendering;

/// <summary>
/// Thrown by <see cref="IRenderCapabilityProbe"/> when a requested probe is malformed or targets a missing
/// executable. Always thrown before any process is started, mirroring <see cref="Xlights.XlightsLaunchInvalidException"/>'s
/// own "no partial/invalid attempt is ever made" convention. A probed executable that starts but never
/// answers in time is a <em>result</em> (<see cref="RenderCapability"/> with both flags <see langword="false"/>),
/// not this exception -- see <see cref="XlightsRenderCapabilityProbe"/> remarks.
/// </summary>
public sealed class RenderCapabilityProbeInvalidException : Exception
{
    public RenderCapabilityProbeInvalidException(string message)
        : base(message)
    {
    }
}
