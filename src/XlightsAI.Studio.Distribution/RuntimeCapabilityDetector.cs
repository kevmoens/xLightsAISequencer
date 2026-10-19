using System.Diagnostics;
using System.Runtime.InteropServices;

namespace XlightsAI.Studio.Distribution;

/// <summary>
/// Real <see cref="IRuntimeCapabilityDetector"/>. OS/architecture come directly from
/// <see cref="RuntimeInformation"/> -- never hard-coded -- so this reports truthfully on whatever machine it
/// actually runs on, including an unsupported one. xLights detection probes a caller-supplied candidate path
/// (the same "explicit path, never silently assumed" convention <c>XlightsAI.Studio.Xlights.XlightsLauncher</c>
/// already established for PHASE-02 Step 014) plus a short list of well-known default Windows install
/// locations; a missing/absent executable is reported as <c>XlightsDetected: false</c>, never thrown.
/// CPU inference is always reported available: Section 2's own "CPU-first inference" architectural
/// assumption means this phase never requires a GPU to be present.
///
/// PHASE-15 Step 097 ("...only after validating inference and xLights compatibility") additively extends the
/// default candidate list with the well-known macOS app-bundle path and the two conventional Linux install
/// paths documented by the xLights project itself -- Windows-only behavior is unchanged; a non-Windows path is
/// simply absent (and thus skipped) on a Windows host, exactly like the existing Windows-only paths are absent
/// on a non-Windows host.
/// </summary>
public sealed class RuntimeCapabilityDetector : IRuntimeCapabilityDetector
{
    private readonly string? _xlightsExecutableCandidatePath;

    public RuntimeCapabilityDetector(string? xlightsExecutableCandidatePath = null)
    {
        _xlightsExecutableCandidatePath = xlightsExecutableCandidatePath;
    }

    public Task<RuntimeCapability> ProbeAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var (detected, version) = ProbeXlights();

        return Task.FromResult(new RuntimeCapability(
            OperatingSystem: RuntimeInformation.OSDescription,
            Architecture: RuntimeInformation.OSArchitecture.ToString(),
            CpuInferenceAvailable: true,
            XlightsDetected: detected,
            XlightsVersion: version));
    }

    private (bool Detected, string? Version) ProbeXlights()
    {
        foreach (var candidate in CandidatePaths())
        {
            if (string.IsNullOrWhiteSpace(candidate) || !File.Exists(candidate))
            {
                continue;
            }

            string? version;
            try
            {
                version = FileVersionInfo.GetVersionInfo(candidate).FileVersion;
            }
            catch (Exception)
            {
                // Best-effort version read only: a PE-only API reading a non-Windows executable (macOS/Linux
                // candidates, Step 097) can throw types beyond the Windows-specific Win32Exception; the
                // executable was still genuinely found, so detection must not be downgraded to a crash over an
                // unreadable version string.
                version = null;
            }

            return (true, version);
        }

        return (false, null);
    }

    private IEnumerable<string?> CandidatePaths()
    {
        yield return _xlightsExecutableCandidatePath;
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "xLights", "xLights.exe");
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "xLights", "xLights.exe");
        yield return "/Applications/xLights.app/Contents/MacOS/xLights";
        yield return "/usr/local/bin/xlights";
        yield return "/usr/bin/xlights";
    }
}
