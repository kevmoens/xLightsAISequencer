namespace XlightsAI.Studio.Xlights;

/// <summary>
/// The outcome of a successful <see cref="IXlightsLauncher.LaunchAsync"/> call. Recorded as evidence
/// alongside the human's own visual verification (PHASE-02 Step 014 required acceptance: "Human-attested
/// integration test with screenshot/render notes") — this record alone never proves the sequence
/// rendered correctly; it proves which real executable/version was launched against which real file.
/// </summary>
/// <param name="ExecutableVersion">
/// Best-effort <see cref="System.Diagnostics.FileVersionInfo.FileVersion"/> of the launched executable;
/// <see langword="null"/> when the OS could not report one (never blocks the launch itself).
/// </param>
public sealed record XlightsLaunchResult(string ExecutablePath, string? ExecutableVersion, string XsqPath, int ProcessId);
