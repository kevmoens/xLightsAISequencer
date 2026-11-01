using System.Diagnostics;
using System.Text;
using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Rendering;

/// <summary>
/// Default <see cref="IRenderCapabilityProbe"/>. PHASE-13 Step 086's original design probed via
/// <c>--help</c>-text sniffing; Phase 0's de-risk spike (<c>docs/PHASE0-FINDINGS.md</c> section 4) found
/// that a **confirmed real false negative** against this machine's own installed xLights 2024.13: running
/// <c>--help</c> launches the full GUI and never prints usage text within any reasonable window (the real
/// <c>--headless</c> flag works correctly -- verified directly by actually invoking it), so text-sniffing
/// reports <c>SupportsHeadless: False</c> for a build that genuinely supports and correctly executes it.
///
/// <para>
/// This probe instead performs the real, authoritative test: it builds a minimal, self-contained synthetic
/// show directory (one trivial "Single Line" model) and a matching minimal <c>.xsq</c> in an isolated temp
/// folder, actually launches the probed executable with
/// <c>--headless -s &lt;tempshow&gt; --outputdir &lt;tempout&gt; &lt;tempxsq&gt;</c>, and reports
/// <see cref="RenderCapability.SupportsHeadless"/>/<see cref="RenderCapability.SupportsOutputDirectory"/>
/// true only when that real attempt exits <c>0</c> and produces a real, non-empty <c>.fseq</c> artifact --
/// exactly the same "don't trust an exit code, require the artifact" discipline <see cref="XlightsRenderAdapter"/>
/// already applies to its own real renders. If that first attempt fails, a second attempt without
/// <c>--outputdir</c> (checking for the <c>.fseq</c> written next to the <c>.xsq</c> instead) distinguishes a
/// build that supports headless rendering but not output-directory redirection. This was verified directly
/// against the real install at <c>C:\Program Files\xLights\xlights.exe</c> and a fully synthetic show
/// directory before being encoded here (exit 0, real non-empty <c>.fseq</c>, ~20ms).
/// </para>
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
    private const string DefaultProbeSequenceStem = "xlai-capability-probe";

    private readonly TimeSpan _timeout;
    private readonly string _probeSequenceStem;

    /// <param name="timeout">How long to wait for each real probe render attempt to exit before it is killed and that attempt reported as unsupported. Defaults to 15 seconds.</param>
    /// <param name="probeSequenceStem">
    /// The file-name stem (no extension) used for the probe's own temporary synthetic <c>.xsq</c>/<c>.fseq</c>.
    /// Defaults to <see cref="DefaultProbeSequenceStem"/>. Overridable only so tests can drive a real,
    /// non-xLights stand-in executable deterministically via its own documented stem-substring convention
    /// (<c>tools/FakeXlightsHost</c>'s <c>-fail-</c>/<c>-hang-</c> markers) -- production callers should
    /// accept the default.
    /// </param>
    public XlightsRenderCapabilityProbe(TimeSpan? timeout = null, string? probeSequenceStem = null)
    {
        _timeout = timeout ?? TimeSpan.FromSeconds(15);
        _probeSequenceStem = string.IsNullOrWhiteSpace(probeSequenceStem) ? DefaultProbeSequenceStem : probeSequenceStem;
    }

    public async Task<RenderCapability> ProbeAsync(string executablePath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new RenderCapabilityProbeInvalidException("An installed xLights executable path must be supplied.");
        }

        executablePath = XlightsExecutable.Resolve(executablePath)!;

        if (!File.Exists(executablePath))
        {
            throw new RenderCapabilityProbeInvalidException(
                $"No executable was found at '{executablePath}'. Install the target xLights version (or " +
                "correct the configured path) before probing render capabilities.");
        }

        ct.ThrowIfCancellationRequested();

        var version = XlightsExecutable.TryReadVersion(executablePath) ?? "unknown";

        var probeRoot = Path.Combine(Path.GetTempPath(), "xlai-render-probe-" + Guid.NewGuid().ToString("N"));
        var showDirectory = Path.Combine(probeRoot, "show");
        var outputDirectory = Path.Combine(probeRoot, "out");

        try
        {
            Directory.CreateDirectory(showDirectory);
            Directory.CreateDirectory(outputDirectory);
            File.WriteAllText(Path.Combine(showDirectory, "xlights_rgbeffects.xml"), SyntheticShowFixture.RgbEffectsXml);
            var xsqPath = Path.Combine(showDirectory, _probeSequenceStem + ".xsq");
            File.WriteAllText(xsqPath, SyntheticShowFixture.SequenceXml);

            // Attempt 1: --headless + --outputdir together. A real, isolated .fseq in outputDirectory proves both.
            var withOutputDir = await TryRealRenderAsync(
                executablePath, ["--headless", "-s", showDirectory, "--outputdir", outputDirectory, xsqPath], ct).ConfigureAwait(false);
            if (withOutputDir == RenderAttemptResult.TimedOut)
            {
                return new RenderCapability(version, executablePath, SupportsHeadless: false, SupportsOutputDirectory: false);
            }

            if (withOutputDir == RenderAttemptResult.ExitedZero && HasNonEmptyFseq(outputDirectory, _probeSequenceStem))
            {
                return new RenderCapability(version, executablePath, SupportsHeadless: true, SupportsOutputDirectory: true);
            }

            // Attempt 2: --headless alone (attempt 1 either exited non-zero or produced no artifact -- not a
            // timeout, so a build that simply doesn't support --outputdir still deserves this second try).
            // A real .fseq written next to the .xsq proves headless without outputdir support.
            var withoutOutputDir = await TryRealRenderAsync(
                executablePath, ["--headless", "-s", showDirectory, xsqPath], ct).ConfigureAwait(false);
            if (withoutOutputDir == RenderAttemptResult.ExitedZero && HasNonEmptyFseq(showDirectory, _probeSequenceStem))
            {
                return new RenderCapability(version, executablePath, SupportsHeadless: true, SupportsOutputDirectory: false);
            }

            return new RenderCapability(version, executablePath, SupportsHeadless: false, SupportsOutputDirectory: false);
        }
        finally
        {
            try
            {
                Directory.Delete(probeRoot, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup only -- a locked temp file must never fail the probe itself.
            }
            catch (UnauthorizedAccessException)
            {
                // Same as above.
            }
        }
    }

    private enum RenderAttemptResult { ExitedZero, ExitedNonZero, TimedOut }

    private async Task<RenderAttemptResult> TryRealRenderAsync(string executablePath, IReadOnlyList<string> arguments, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo(executablePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, _) => { };
        process.ErrorDataReceived += (_, _) => { };

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
            return RenderAttemptResult.TimedOut;
        }

        return process.ExitCode == 0 ? RenderAttemptResult.ExitedZero : RenderAttemptResult.ExitedNonZero;
    }

    private static bool HasNonEmptyFseq(string directory, string stem)
    {
        var path = Path.Combine(directory, stem + ".fseq");
        return File.Exists(path) && new FileInfo(path).Length > 0;
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
