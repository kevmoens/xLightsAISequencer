namespace XlightsAI.Studio.Distribution;

/// <summary>
/// Real <see cref="IPlatformSmokeTestSuite"/>. Three independent, additive checks per platform, mirroring
/// <see cref="StartupHealthChecks"/>'s own "separate named entries, never one opaque flag" convention:
///
/// 1. <c>PublishedBundleExists</c> -- the real bundle directory exists and is non-empty. Always runnable.
/// 2. <c>RequiredNativeAssetsPresent</c> -- every one of this platform's real, previously-observed
///    <see cref="SupportedPlatform.RequiredNativeAssetFileNames"/> exists on disk under the bundle directory.
///    A plain file-existence check, so it is always runnable regardless of host OS.
/// 3. <c>ApphostLaunches</c> -- only runnable when the real, probed <see cref="RuntimeCapability"/> of the
///    current host matches this platform's own OS/architecture (you cannot execute a Linux apphost on a
///    Windows host); when runnable, delegates to the injected <see cref="IApphostLauncher"/> to actually start
///    the real apphost and observe it. When not runnable, this is recorded as its own explicit, named,
///    non-faked gap (Section 3 architectural design note: "document unsupported combinations rather than
///    shipping broken bundles") rather than silently omitted or assumed to pass.
/// </summary>
public sealed class PlatformSmokeTestSuite : IPlatformSmokeTestSuite
{
    private readonly IRuntimeCapabilityDetector _capabilityDetector;
    private readonly IApphostLauncher _apphostLauncher;

    public PlatformSmokeTestSuite(IRuntimeCapabilityDetector capabilityDetector, IApphostLauncher? apphostLauncher = null)
    {
        ArgumentNullException.ThrowIfNull(capabilityDetector);

        _capabilityDetector = capabilityDetector;
        _apphostLauncher = apphostLauncher ?? new ProcessApphostLauncher();
    }

    public async Task<PlatformSmokeTestReport> RunAsync(SupportedPlatform platform, string bundleDirectory, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(platform);
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleDirectory);
        ct.ThrowIfCancellationRequested();

        var checks = new List<PlatformSmokeCheckResult>();

        var bundleExists = Directory.Exists(bundleDirectory) && Directory.EnumerateFileSystemEntries(bundleDirectory).Any();
        checks.Add(new PlatformSmokeCheckResult(
            "PublishedBundleExists",
            Runnable: true,
            Passed: bundleExists,
            Detail: bundleExists
                ? $"Bundle directory '{bundleDirectory}' exists and is non-empty."
                : $"Bundle directory '{bundleDirectory}' is missing or empty."));

        var missingAssets = platform.RequiredNativeAssetFileNames
            .Where(name => !File.Exists(Path.Combine(bundleDirectory, name)))
            .ToList();
        checks.Add(new PlatformSmokeCheckResult(
            "RequiredNativeAssetsPresent",
            Runnable: true,
            Passed: missingAssets.Count == 0,
            Detail: missingAssets.Count == 0
                ? $"All {platform.RequiredNativeAssetFileNames.Count} required native asset(s) present for {platform.RuntimeIdentifier}."
                : $"Missing required native asset(s) for {platform.RuntimeIdentifier}: {string.Join(", ", missingAssets)}."));

        var capability = await _capabilityDetector.ProbeAsync(ct).ConfigureAwait(false);
        var hostMatchesTarget =
            capability.OperatingSystem.Contains(platform.OperatingSystemSubstring, StringComparison.OrdinalIgnoreCase)
            && string.Equals(capability.Architecture, platform.Architecture, StringComparison.OrdinalIgnoreCase);

        if (!hostMatchesTarget)
        {
            checks.Add(new PlatformSmokeCheckResult(
                "ApphostLaunches",
                Runnable: false,
                Passed: false,
                Detail: $"Cannot execute {platform.RuntimeIdentifier} binaries on host '{capability.OperatingSystem}' " +
                        $"/ '{capability.Architecture}'; this step's own required acceptance (\"Each advertised " +
                        "platform passes [the] same smoke-test suite\") cannot be proven for this platform from " +
                        "this host -- a native or emulated host for this target is required."));
        }
        else
        {
            var apphostPath = Path.Combine(bundleDirectory, platform.ApphostFileName);
            var (launched, detail) = await _apphostLauncher.TryLaunchAsync(apphostPath, ct).ConfigureAwait(false);
            checks.Add(new PlatformSmokeCheckResult("ApphostLaunches", Runnable: true, Passed: launched, Detail: detail));
        }

        return new PlatformSmokeTestReport(platform, checks);
    }
}
