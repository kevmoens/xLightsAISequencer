using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace XlightsAI.Studio.Rendering;

/// <summary>
/// Default <see cref="IRenderAdapter"/>. PHASE-13 Step 086's own authoritative render tier: probes the
/// configured executable's real capabilities (<see cref="IRenderCapabilityProbe"/>) before ever attempting
/// to launch it, builds its argument list only from upstream's own documented flags
/// (<c>--headless -s &lt;showdir&gt; [--outputdir &lt;dir&gt;] &lt;seq.xsq&gt;</c> -- Section 12), and never
/// invents or assumes support for a flag the probe itself did not observe.
///
/// <para>
/// An unsupported build is a graceful, explicit fallback (<see cref="RenderArtifact.Success"/> =
/// <see langword="false"/> with manual-verification guidance in <see cref="RenderArtifact.Warnings"/>) --
/// never a thrown exception and never a launched process. A supported build that is launched but exits
/// non-zero, never produces a <c>.fseq</c>, or exceeds its timeout is this step's own required hard gate
/// failure (Section 2: "Treat render failures as blocking gate"), reported the same unsuccessful way so a
/// caller cannot accidentally branch differently on "never tried" versus "tried and failed".
/// </para>
/// </summary>
public sealed class XlightsRenderAdapter : IRenderAdapter
{
    private readonly IRenderCapabilityProbe _capabilityProbe;
    private readonly string _executablePath;
    private readonly TimeSpan _timeout;

    /// <param name="capabilityProbe">Probes the configured executable before every render attempt.</param>
    /// <param name="executablePath">The real, user-configured xLights executable path.</param>
    /// <param name="timeout">How long to wait for the render process to exit before it is killed and reported as a timed-out failure. Defaults to 120 seconds.</param>
    public XlightsRenderAdapter(IRenderCapabilityProbe capabilityProbe, string executablePath, TimeSpan? timeout = null)
    {
        _capabilityProbe = capabilityProbe ?? throw new ArgumentNullException(nameof(capabilityProbe));
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("An installed xLights executable path must be supplied.", nameof(executablePath));
        }

        _executablePath = executablePath;
        _timeout = timeout ?? TimeSpan.FromSeconds(120);
    }

    public async Task<RenderArtifact> RenderAsync(RenderRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.ShowDirectory) || !Directory.Exists(request.ShowDirectory))
        {
            throw new RenderRequestInvalidException(
                $"Show directory '{request.ShowDirectory}' does not exist; no render was attempted.");
        }

        if (string.IsNullOrWhiteSpace(request.ExportedXsqPath) || !File.Exists(request.ExportedXsqPath))
        {
            throw new RenderRequestInvalidException(
                $"Exported .xsq '{request.ExportedXsqPath}' does not exist; export it (PHASE-02 Step 013/015) before rendering.");
        }

        if (string.IsNullOrWhiteSpace(request.OutputDirectory))
        {
            throw new RenderRequestInvalidException("A render output directory must be supplied.");
        }

        ct.ThrowIfCancellationRequested();

        // An isolated, app-owned output folder (Section 2: "Never overwrite source show files silently",
        // applied here to this render's own evidence too) -- never the show directory itself.
        Directory.CreateDirectory(request.OutputDirectory);

        var warnings = new List<string>();
        if (request.Selection is { } selection)
        {
            warnings.Add(
                $"A partial selection ({selection.StartTicks}-{selection.EndTicks} ticks) was requested, but no " +
                "verified upstream xLights CLI flag exists for a scoped headless render (Section 2: \"do not " +
                "invent xLights effect parameters\"); the full exported .xsq was rendered and callers must filter " +
                "decoded frame/channel data (Step 087/088) to this window themselves.");
        }

        var capability = await _capabilityProbe.ProbeAsync(_executablePath, ct).ConfigureAwait(false);

        if (!capability.SupportsHeadless)
        {
            warnings.Add(
                $"The configured xLights executable at '{_executablePath}' (version {capability.XlightsVersion}) " +
                "did not report supporting '--headless' during capability probing. No render process was " +
                $"started; open '{request.ExportedXsqPath}' manually in xLights, choose Render All, and save to " +
                "verify this candidate (Section 12's own documented manual fallback).");
            return new RenderArtifact(
                Guid.NewGuid(),
                RenderAuthority.ActualXlights,
                Success: false,
                capability.XlightsVersion,
                FseqPath: null,
                Warnings: warnings,
                EvidenceHash: await ComputeEvidenceHashAsync(request.ExportedXsqPath, exitCode: null, ct).ConfigureAwait(false));
        }

        var arguments = new List<string> { "--headless", "-s", request.ShowDirectory };
        if (capability.SupportsOutputDirectory)
        {
            arguments.Add("--outputdir");
            arguments.Add(request.OutputDirectory);
        }
        else
        {
            warnings.Add(
                "The configured xLights executable did not report supporting '--outputdir'; rendered output " +
                "(if any) must be located relative to the show directory itself rather than the requested " +
                "isolated output folder.");
        }

        arguments.Add(request.ExportedXsqPath);

        var startInfo = new ProcessStartInfo(_executablePath)
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
        var stderr = new StringBuilder();
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };
        process.OutputDataReceived += (_, _) => { }; // drained so the child is never blocked on a full stdout pipe.

        try
        {
            if (!process.Start())
            {
                throw new RenderRequestInvalidException($"xLights at '{_executablePath}' could not be started.");
            }
        }
        catch (Exception ex) when (ex is not RenderRequestInvalidException)
        {
            throw new RenderRequestInvalidException($"xLights at '{_executablePath}' could not be started: {ex.Message}");
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
            KillProcessTreeSafely(process);
            warnings.Add(
                $"The render process exceeded its {_timeout.TotalSeconds:0}s timeout and was killed before completing.");
            return new RenderArtifact(
                Guid.NewGuid(),
                RenderAuthority.ActualXlights,
                Success: false,
                capability.XlightsVersion,
                FseqPath: null,
                Warnings: warnings,
                EvidenceHash: await ComputeEvidenceHashAsync(request.ExportedXsqPath, exitCode: null, ct).ConfigureAwait(false));
        }

        var stem = Path.GetFileNameWithoutExtension(request.ExportedXsqPath);
        var fseqPath = FindFseq(stem, request.OutputDirectory)
            ?? FindFseq(stem, Path.GetDirectoryName(Path.GetFullPath(request.ExportedXsqPath)) ?? request.ShowDirectory);

        var success = process.ExitCode == 0 && fseqPath is not null;
        if (process.ExitCode != 0)
        {
            warnings.Add($"xLights exited with code {process.ExitCode}.");
        }

        if (process.ExitCode == 0 && fseqPath is null)
        {
            warnings.Add(
                "xLights exited 0 but no .fseq artifact was found in the output or show directory; the render " +
                "is treated as failed rather than trusting a bare exit code (Section 2: an XML parse pass does " +
                "not prove xLights renders an effect -- the same applies to an exit code alone).");
        }

        if (stderr.Length > 0)
        {
            warnings.Add($"Render process stderr: {stderr}");
        }

        return new RenderArtifact(
            Guid.NewGuid(),
            RenderAuthority.ActualXlights,
            success,
            capability.XlightsVersion,
            fseqPath,
            warnings,
            await ComputeEvidenceHashAsync(request.ExportedXsqPath, process.ExitCode, ct).ConfigureAwait(false));
    }

    private static string? FindFseq(string stem, string directory)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return null;
        }

        var candidate = Path.Combine(directory, stem + ".fseq");
        return File.Exists(candidate) ? candidate : null;
    }

    private static async Task<string> ComputeEvidenceHashAsync(string xsqPath, int? exitCode, CancellationToken ct)
    {
        var bytes = await File.ReadAllBytesAsync(xsqPath, ct).ConfigureAwait(false);
        using var sha = SHA256.Create();
        sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
        var exitBytes = Encoding.UTF8.GetBytes(exitCode?.ToString() ?? "no-process-started");
        sha.TransformFinalBlock(exitBytes, 0, exitBytes.Length);
        return Convert.ToHexString(sha.Hash!);
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
