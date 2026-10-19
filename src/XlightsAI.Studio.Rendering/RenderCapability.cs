namespace XlightsAI.Studio.Rendering;

/// <summary>
/// PHASE-13 Section 3 proposed contract, verbatim fields. The result of probing one real, installed xLights
/// executable's own reported version and observed command-line behavior -- never an assumption that every
/// installed xLights release supports the same flags (Section 2: "no assumption every installed release
/// supports --headless"; this phase's own numbered Step 086: "Probe installed xLights version/help and
/// optional headless render").
/// </summary>
/// <param name="XlightsVersion">
/// Best-effort <see cref="System.Diagnostics.FileVersionInfo.FileVersion"/> of the probed executable, or
/// <c>"unknown"</c> when the OS could not report one (never blocks the probe itself).
/// </param>
/// <param name="ExecutablePath">The real, on-disk executable path that was probed.</param>
/// <param name="SupportsHeadless">
/// <see langword="true"/> only when the probed executable's own captured stdout/stderr text actually
/// mentioned the <c>--headless</c> flag -- never assumed from a version number or guessed.
/// </param>
/// <param name="SupportsOutputDirectory">
/// <see langword="true"/> only when the probed executable's own captured stdout/stderr text actually
/// mentioned the <c>--outputdir</c> flag.
/// </param>
public sealed record RenderCapability(string XlightsVersion, string ExecutablePath, bool SupportsHeadless, bool SupportsOutputDirectory);
