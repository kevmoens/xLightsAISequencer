using System.Security.Cryptography;

namespace XlightsAI.Studio.Distribution;

/// <summary>
/// Real <see cref="IStartupHealthChecks"/>. Two independent, additive checks, each contributing its own
/// clear-text entries rather than one opaque pass/fail flag:
///
/// 1. OS/architecture support (phase test matrix row 8, "OS/arch incompatibility yields clear message") --
///    compares a real <see cref="RuntimeCapability"/> probe (<see cref="IRuntimeCapabilityDetector"/>,
///    never re-probed/duplicated here) against <paramref name="supportedOperatingSystemSubstring"/>/
///    <paramref name="supportedArchitecture"/>.
/// 2. Required-asset verification (this step's own title, "...with verified native assets"; phase test
///    matrix row 1 "Fresh Windows VM starts app...") -- every <see cref="DownloadableAsset"/> in the supplied
///    <see cref="ReleaseManifest"/> with <see cref="DownloadableAsset.Required"/> true must exist on disk
///    under <c>bundleDirectory</c> (joined by its own <see cref="DownloadableAsset.Id"/>) and its actual bytes
///    must hash to the declared <see cref="DownloadableAsset.Sha256"/> -- the same "declared hash is the sole
///    basis for verified, computed fresh from the real file every time" convention
///    <c>XlightsAI.Studio.Stems.ModelAssetManager</c> (PHASE-06 Step 039) already established, applied here
///    to already-bundled release assets rather than a lazily downloaded model. A missing or non-optional
///    file never throws -- it is reported as its own clear, named entry, so the caller can decide to block
///    startup, continue in a degraded/offline mode, or show the user a diagnostic (Section 2: "Missing or
///    incompatible model... must yield a recoverable state", carried into Step 094's own required failure
///    case).
/// </summary>
public sealed class StartupHealthChecks : IStartupHealthChecks
{
    private readonly string _bundleDirectory;
    private readonly ReleaseManifest _manifest;
    private readonly IRuntimeCapabilityDetector _capabilityDetector;
    private readonly string _supportedOperatingSystemSubstring;
    private readonly string _supportedArchitecture;

    public StartupHealthChecks(
        string bundleDirectory,
        ReleaseManifest manifest,
        IRuntimeCapabilityDetector capabilityDetector,
        string supportedOperatingSystemSubstring = "Windows",
        string supportedArchitecture = "X64")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleDirectory);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(capabilityDetector);
        ArgumentException.ThrowIfNullOrWhiteSpace(supportedOperatingSystemSubstring);
        ArgumentException.ThrowIfNullOrWhiteSpace(supportedArchitecture);

        _bundleDirectory = bundleDirectory;
        _manifest = manifest;
        _capabilityDetector = capabilityDetector;
        _supportedOperatingSystemSubstring = supportedOperatingSystemSubstring;
        _supportedArchitecture = supportedArchitecture;
    }

    public async Task<IReadOnlyList<string>> CheckAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var issues = new List<string>();

        var capability = await _capabilityDetector.ProbeAsync(ct).ConfigureAwait(false);
        if (!capability.OperatingSystem.Contains(_supportedOperatingSystemSubstring, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(capability.Architecture, _supportedArchitecture, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(
                $"Unsupported platform: '{capability.OperatingSystem}' / '{capability.Architecture}' is not a " +
                $"supported combination for this release (requires an OS containing " +
                $"'{_supportedOperatingSystemSubstring}' on '{_supportedArchitecture}'). " +
                "Install a build published for your platform.");
        }

        foreach (var asset in _manifest.Assets)
        {
            ct.ThrowIfCancellationRequested();
            var path = Path.Combine(_bundleDirectory, asset.Id);

            if (!File.Exists(path))
            {
                if (asset.Required)
                {
                    issues.Add($"Missing required asset '{asset.Id}' (version {asset.Version}) at '{path}'.");
                }
                continue;
            }

            var actualSha256 = await ComputeSha256Async(path, ct).ConfigureAwait(false);
            if (!string.Equals(actualSha256, asset.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(
                    $"Asset '{asset.Id}' (version {asset.Version}) failed integrity verification: " +
                    $"expected sha256 {asset.Sha256} but found {actualSha256}. The file will not be trusted.");
            }
        }

        return issues;
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
