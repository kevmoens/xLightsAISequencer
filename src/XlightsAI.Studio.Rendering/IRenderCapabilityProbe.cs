namespace XlightsAI.Studio.Rendering;

/// <summary>
/// PHASE-13 Section 3 proposed contract, verbatim signature. Step 086 ("Probe installed xLights
/// capabilities and implement version-aware render adapter"): interrogates one real, user-configured
/// xLights executable for its own reported version and which documented render flags it actually mentions
/// supporting -- a capability probe, never a universal assumption (Section 2).
/// </summary>
public interface IRenderCapabilityProbe
{
    /// <exception cref="RenderCapabilityProbeInvalidException"><paramref name="executablePath"/> is blank or does not exist, or the executable could not be started.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was cancelled before or during the probe.</exception>
    Task<RenderCapability> ProbeAsync(string executablePath, CancellationToken ct);
}
