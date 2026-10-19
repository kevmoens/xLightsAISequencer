using XlightsAI.Studio.Distribution;

namespace XlightsAI.Studio.Distribution.Tests;

/// <summary>
/// PHASE-15 Step 097 ("Add supported macOS/Linux deliverables only after validating inference and xLights
/// compatibility"). Every test below exercises the real <see cref="PlatformSmokeTestSuite"/> and
/// <see cref="PlatformReleaseEvaluator"/> production types directly against real bundle directories/files
/// written to a fresh temp directory -- no mocking framework is used anywhere in this file. The real host's
/// OS/architecture is simulated via an explicit fixture type (the same <c>Null*</c>/<c>Fixed*</c> convention
/// Step 094's own test file already established) so these tests are deterministic on every machine, while the
/// actual cross-platform publish+inspect demonstration that grounds <see cref="KnownPlatforms"/>'s real native
/// asset file names runs separately and is recorded in
/// <c>implementation-evidence/PHASE-15/STEP-097.md</c> §3.4/§5.
/// </summary>
public sealed class Step097AcceptanceTests : IDisposable
{
    private readonly string _root;

    public Step097AcceptanceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "xlai-step097-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string CreateBundle(string name, IEnumerable<string> fileNames)
    {
        var dir = Path.Combine(_root, name);
        Directory.CreateDirectory(dir);
        foreach (var fileName in fileNames)
        {
            File.WriteAllText(Path.Combine(dir, fileName), "fake-bytes-" + fileName);
        }

        return dir;
    }

    [Fact]
    public async Task Step097_Acceptance_Passes()
    {
        // Required acceptance, verbatim: "Each advertised platform passes [the] same smoke-test suite."
        // Three real, fully populated bundle directories are built, matching this step's own real
        // publish-and-inspect findings for win-x64/osx-x64/linux-x64 (KnownPlatforms; evidence §3.4/§5).
        // Simulating the host as Windows x64 proves only win-x64 -- the one platform whose binaries this host
        // can actually execute -- is advertised; the other two are documented as unsupported with the real,
        // specific "cannot execute on this host" reason, never silently shipped.
        var winBundle = CreateBundle("win-x64", KnownPlatforms.WindowsX64.RequiredNativeAssetFileNames);
        var osxBundle = CreateBundle("osx-x64", KnownPlatforms.MacOsX64.RequiredNativeAssetFileNames);
        var linuxBundle = CreateBundle("linux-x64", KnownPlatforms.LinuxX64.RequiredNativeAssetFileNames);

        var windowsHost = new RuntimeCapability("Microsoft Windows 10.0.26300", "X64", true, false, null);
        var suiteOnWindowsHost = new PlatformSmokeTestSuite(
            new FixedRuntimeCapabilityDetector(windowsHost),
            new FixedApphostLauncher(launched: true, detail: "simulated launch succeeded"));

        var winReport = await suiteOnWindowsHost.RunAsync(KnownPlatforms.WindowsX64, winBundle, CancellationToken.None);
        var osxReport = await suiteOnWindowsHost.RunAsync(KnownPlatforms.MacOsX64, osxBundle, CancellationToken.None);
        var linuxReport = await suiteOnWindowsHost.RunAsync(KnownPlatforms.LinuxX64, linuxBundle, CancellationToken.None);

        var evaluator = new PlatformReleaseEvaluator();
        var decision = evaluator.Evaluate([winReport, osxReport, linuxReport]);

        Assert.Contains("win-x64", decision.AdvertisedRuntimeIdentifiers);
        Assert.DoesNotContain("osx-x64", decision.AdvertisedRuntimeIdentifiers);
        Assert.DoesNotContain("linux-x64", decision.AdvertisedRuntimeIdentifiers);

        Assert.Contains(decision.UnsupportedReasons["osx-x64"], reason => reason.Contains("ApphostLaunches"));
        Assert.Contains(decision.UnsupportedReasons["linux-x64"], reason => reason.Contains("ApphostLaunches"));

        // Re-run with the simulated host now reporting as the macOS target instead: this proves the decision
        // genuinely depends on which binaries the host can execute, not a hard-coded platform name -- osx-x64
        // becomes the only advertised platform, and win-x64 (no longer executable on this simulated host)
        // becomes unsupported.
        var macHost = new RuntimeCapability("Darwin 23.1.0", "X64", true, false, null);
        var suiteOnMacHost = new PlatformSmokeTestSuite(
            new FixedRuntimeCapabilityDetector(macHost),
            new FixedApphostLauncher(launched: true, detail: "simulated launch succeeded"));

        var decisionOnMacHost = evaluator.Evaluate(
        [
            await suiteOnMacHost.RunAsync(KnownPlatforms.WindowsX64, winBundle, CancellationToken.None),
            await suiteOnMacHost.RunAsync(KnownPlatforms.MacOsX64, osxBundle, CancellationToken.None),
            await suiteOnMacHost.RunAsync(KnownPlatforms.LinuxX64, linuxBundle, CancellationToken.None),
        ]);

        Assert.Contains("osx-x64", decisionOnMacHost.AdvertisedRuntimeIdentifiers);
        Assert.DoesNotContain("win-x64", decisionOnMacHost.AdvertisedRuntimeIdentifiers);
    }

    [Fact]
    public async Task Step097_NegativePath_MissingNativeAsset_IsNotAdvertised_WithRealReason()
    {
        // Required failure/edge case family (verbatim, carried across Steps 094-097): "Missing or incompatible
        // model... must yield a recoverable state," applied here to a broken macOS bundle that is genuinely
        // missing its own declared native windowing shim. Must be documented as unsupported naming the real
        // missing file, never advertised, never throw.
        var brokenOsxBundle = CreateBundle(
            "osx-x64-broken",
            KnownPlatforms.MacOsX64.RequiredNativeAssetFileNames.Where(f => f != "libAvaloniaNative.dylib"));

        var macHost = new RuntimeCapability("Darwin 23.1.0", "X64", true, false, null);
        var suite = new PlatformSmokeTestSuite(
            new FixedRuntimeCapabilityDetector(macHost),
            new FixedApphostLauncher(launched: true, detail: "simulated launch succeeded"));

        var report = await suite.RunAsync(KnownPlatforms.MacOsX64, brokenOsxBundle, CancellationToken.None);
        var decision = new PlatformReleaseEvaluator().Evaluate([report]);

        Assert.DoesNotContain("osx-x64", decision.AdvertisedRuntimeIdentifiers);
        Assert.Contains(decision.UnsupportedReasons["osx-x64"], reason => reason.Contains("libAvaloniaNative.dylib"));
    }

    [Fact]
    public async Task Step097_NegativePath_ApphostCrashesImmediately_IsNotAdvertised()
    {
        var winBundle = CreateBundle("win-x64-broken", KnownPlatforms.WindowsX64.RequiredNativeAssetFileNames);
        var windowsHost = new RuntimeCapability("Microsoft Windows 10.0.26300", "X64", true, false, null);
        var suite = new PlatformSmokeTestSuite(
            new FixedRuntimeCapabilityDetector(windowsHost),
            new FixedApphostLauncher(launched: false, detail: "apphost crashed immediately (exit code 134)"));

        var report = await suite.RunAsync(KnownPlatforms.WindowsX64, winBundle, CancellationToken.None);
        var decision = new PlatformReleaseEvaluator().Evaluate([report]);

        Assert.DoesNotContain("win-x64", decision.AdvertisedRuntimeIdentifiers);
        Assert.Contains(decision.UnsupportedReasons["win-x64"], reason => reason.Contains("crashed"));
    }

    [Fact]
    public async Task Step097_NegativePath_MissingBundleDirectory_IsFlaggedNotThrown()
    {
        var missingBundle = Path.Combine(_root, "does-not-exist");
        var windowsHost = new RuntimeCapability("Microsoft Windows 10.0.26300", "X64", true, false, null);
        var suite = new PlatformSmokeTestSuite(
            new FixedRuntimeCapabilityDetector(windowsHost),
            new FixedApphostLauncher(launched: true, detail: "unreachable"));

        var report = await suite.RunAsync(KnownPlatforms.WindowsX64, missingBundle, CancellationToken.None);

        var bundleCheck = report.Checks.Single(c => c.Name == "PublishedBundleExists");
        Assert.False(bundleCheck.Passed);
        Assert.True(bundleCheck.Runnable);
    }

    [Fact]
    public async Task Step097_NegativePath_Cancellation_ThrowsBeforeAnyWork()
    {
        var suite = new PlatformSmokeTestSuite(new RuntimeCapabilityDetector());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => suite.RunAsync(KnownPlatforms.WindowsX64, _root, cts.Token));
    }

    [Fact]
    public async Task Step097_NullOrBlankArguments_Throw()
    {
        var suite = new PlatformSmokeTestSuite(new RuntimeCapabilityDetector());

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => suite.RunAsync(null!, _root, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(
            () => suite.RunAsync(KnownPlatforms.WindowsX64, "", CancellationToken.None));

        Assert.Throws<ArgumentNullException>(() => new PlatformSmokeTestSuite(null!));
        Assert.Throws<ArgumentNullException>(() => new PlatformReleaseEvaluator().Evaluate(null!));
    }

    [Fact]
    public void Step097_Evaluator_PlatformWithNoChecksRun_IsNeverAdvertised()
    {
        var emptyReport = new PlatformSmokeTestReport(KnownPlatforms.LinuxX64, []);
        var decision = new PlatformReleaseEvaluator().Evaluate([emptyReport]);

        Assert.Empty(decision.AdvertisedRuntimeIdentifiers);
        Assert.Contains("linux-x64", decision.UnsupportedReasons.Keys);
    }

    private sealed class FixedRuntimeCapabilityDetector(RuntimeCapability capability) : IRuntimeCapabilityDetector
    {
        public Task<RuntimeCapability> ProbeAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(capability);
        }
    }

    private sealed class FixedApphostLauncher(bool launched, string detail) : IApphostLauncher
    {
        public Task<(bool Launched, string Detail)> TryLaunchAsync(string apphostPath, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult((launched, detail));
        }
    }
}
