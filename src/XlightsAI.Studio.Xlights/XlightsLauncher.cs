using System.Diagnostics;

namespace XlightsAI.Studio.Xlights;

/// <summary>
/// Default <see cref="IXlightsLauncher"/>. PHASE-02 Step 014 ("Open output in installed xLights; verify
/// duration, timing, models and visible effects"): starts the real, user-configured xLights executable
/// with the real exported <c>.xsq</c> file as its sole argument, using <see cref="Process.Start(ProcessStartInfo)"/>
/// directly (no shell interpolation, so no command-injection surface from either path) and reports the
/// resulting process id plus the executable's own reported file version. Does not wait for xLights to
/// exit, render anything itself, or inspect xLights' own window/output — Section 2 is explicit: "An XML
/// parse pass does not prove xLights renders an effect"; the actual visual verification is the human's
/// own required acceptance step, not something this adapter can or should automate.
/// </summary>
public sealed class XlightsLauncher : IXlightsLauncher
{
    public Task<XlightsLaunchResult> LaunchAsync(string xlightsExecutablePath, string xsqPath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(xlightsExecutablePath))
        {
            throw new XlightsLaunchInvalidException("An installed xLights executable path must be supplied.");
        }

        if (!File.Exists(xlightsExecutablePath))
        {
            throw new XlightsLaunchInvalidException(
                $"No xLights executable was found at '{xlightsExecutablePath}'. Install the target xLights " +
                "version (or correct the configured path) before opening a sequence for real integration verification.");
        }

        if (string.IsNullOrWhiteSpace(xsqPath))
        {
            throw new XlightsLaunchInvalidException("An exported .xsq path must be supplied.");
        }

        if (!File.Exists(xsqPath))
        {
            throw new XlightsLaunchInvalidException(
                $"'{xsqPath}' does not exist; export an .xsq (PHASE-02 Step 013) before opening it in xLights.");
        }

        // Checked last, after every input-validity check above, so a cancelled request never masks an
        // actionable "missing executable"/"missing xsq" message behind a generic cancellation — but
        // always before any process is actually started.
        ct.ThrowIfCancellationRequested();

        string? version = null;
        try
        {
            version = FileVersionInfo.GetVersionInfo(xlightsExecutablePath).FileVersion;
        }
        catch (Exception)
        {
            // Best-effort only: an unreadable version resource must never block the launch itself.
        }

        var startInfo = new ProcessStartInfo(xlightsExecutablePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(xsqPath);

        Process process;
        try
        {
            process = Process.Start(startInfo)
                ?? throw new XlightsLaunchInvalidException($"xLights at '{xlightsExecutablePath}' could not be started.");
        }
        catch (Exception ex) when (ex is not XlightsLaunchInvalidException)
        {
            throw new XlightsLaunchInvalidException($"xLights at '{xlightsExecutablePath}' could not be started: {ex.Message}");
        }

        return Task.FromResult(new XlightsLaunchResult(xlightsExecutablePath, version, xsqPath, process.Id));
    }
}
