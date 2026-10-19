using System.Diagnostics;
using System.Text;

namespace XlightsAI.Studio.Rendering;

/// <summary>
/// Default <see cref="IRenderCapabilityProbe"/>. PHASE-13 Step 086 ("Probe installed xLights version/help
/// and optional headless render; use robust quoting/argument list and output directory isolation; fall
/// back to manual verification"). Starts the real, user-configured executable with
/// <see cref="_probeArguments"/> (upstream's own documented <c>--headless -s &lt;showdir&gt; &lt;seq.xsq&gt;</c>
/// / <c>--outputdir</c> flags are never assumed present -- Section 2: "no assumption every installed
/// release supports --headless"), captures its combined stdout+stderr text via
/// <see cref="ProcessStartInfo.ArgumentList"/> (never shell string interpolation), and reports which of
/// those two flag names the captured text itself mentions.
///
/// <para>
/// A process that never exits within <see cref="_timeout"/> is killed (its whole process tree, via
/// <see cref="Process.Kill(bool)"/>) and reported as supporting neither flag -- this is this step's own
/// required graceful fallback result, not a thrown exception, and the caller is never left blocked
/// indefinitely or with an unobserved orphan child.
/// </para>
/// </summary>
public sealed class XlightsRenderCapabilityProbe : IRenderCapabilityProbe
{
    private static readonly string[] DefaultProbeArguments = ["--help"];

    private readonly IReadOnlyList<string> _probeArguments;
    private readonly TimeSpan _timeout;

    /// <param name="probeArguments">
    /// Arguments passed to the probed executable to elicit its own usage/version text. Defaults to the
    /// single, upstream-documented <c>--help</c> flag (<see cref="DefaultProbeArguments"/>). Overridable
    /// only so tests can drive a real, non-xLights stand-in executable deterministically (the same
    /// disclosed convention PHASE-02 Step 014's own tests use with <c>where.exe</c>) -- production callers
    /// should accept the default.
    /// </param>
    /// <param name="timeout">How long to wait for the probed executable to exit before it is killed and reported as unsupported. Defaults to 15 seconds.</param>
    public XlightsRenderCapabilityProbe(IReadOnlyList<string>? probeArguments = null, TimeSpan? timeout = null)
    {
        _probeArguments = probeArguments ?? DefaultProbeArguments;
        _timeout = timeout ?? TimeSpan.FromSeconds(15);
    }

    public async Task<RenderCapability> ProbeAsync(string executablePath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new RenderCapabilityProbeInvalidException("An installed xLights executable path must be supplied.");
        }

        if (!File.Exists(executablePath))
        {
            throw new RenderCapabilityProbeInvalidException(
                $"No executable was found at '{executablePath}'. Install the target xLights version (or " +
                "correct the configured path) before probing render capabilities.");
        }

        ct.ThrowIfCancellationRequested();

        var version = "unknown";
        try
        {
            version = FileVersionInfo.GetVersionInfo(executablePath).FileVersion ?? "unknown";
        }
        catch (Exception)
        {
            // Best-effort only, same convention as XlightsLauncher: an unreadable version resource must
            // never block the probe itself.
        }

        var startInfo = new ProcessStartInfo(executablePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in _probeArguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = startInfo };
        var output = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };

        try
        {
            if (!process.Start())
            {
                throw new RenderCapabilityProbeInvalidException($"'{executablePath}' could not be started.");
            }
        }
        catch (Exception ex) when (ex is not RenderCapabilityProbeInvalidException)
        {
            throw new RenderCapabilityProbeInvalidException($"'{executablePath}' could not be started: {ex.Message}");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_timeout);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            KillProcessTreeSafely(process);
            throw;
        }
        catch (OperationCanceledException)
        {
            // Our own internal timeout, not the caller's token: a build that never answers within
            // _timeout is reported as supporting neither flag (this step's own required graceful
            // fallback), never left running and never thrown as a hard error.
            KillProcessTreeSafely(process);
            return new RenderCapability(version, executablePath, SupportsHeadless: false, SupportsOutputDirectory: false);
        }

        var captured = output.ToString();
        return new RenderCapability(
            version,
            executablePath,
            SupportsHeadless: captured.Contains("--headless", StringComparison.OrdinalIgnoreCase),
            SupportsOutputDirectory: captured.Contains("--outputdir", StringComparison.OrdinalIgnoreCase));
    }

    private static void KillProcessTreeSafely(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Already exited between the check and the kill -- nothing left to clean up.
        }
    }
}
