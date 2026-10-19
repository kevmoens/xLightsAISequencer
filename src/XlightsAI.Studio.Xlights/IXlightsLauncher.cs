namespace XlightsAI.Studio.Xlights;

/// <summary>
/// PHASE-02 Step 014 ("Open output in installed xLights; verify duration, timing, models and visible
/// effects"). Launches an installed xLights executable against an already-exported <c>.xsq</c> file so
/// a human can visually verify it, and records enough evidence (installed executable path/version,
/// process id) that the attempt is auditable. This never substitutes for the human visual check itself
/// (Section 2: "An XML parse pass does not prove xLights renders an effect") — it only proves the
/// real, external process was actually started against the real file on disk.
/// </summary>
public interface IXlightsLauncher
{
    /// <exception cref="XlightsLaunchInvalidException">
    /// <paramref name="xlightsExecutablePath"/> is blank or does not exist, or <paramref name="xsqPath"/>
    /// is blank or does not exist, or the configured executable could not be started. No process is
    /// ever started when this is thrown.
    /// </exception>
    Task<XlightsLaunchResult> LaunchAsync(string xlightsExecutablePath, string xsqPath, CancellationToken ct);
}
