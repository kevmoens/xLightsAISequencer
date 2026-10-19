namespace XlightsAI.Studio.Distribution;

/// <summary>
/// PHASE-15 Step 097. Runs the real smoke-test suite for one <see cref="SupportedPlatform"/> against one real
/// published bundle directory. This step's own required acceptance, verbatim: "Each advertised platform passes
/// [the] same smoke-test suite" -- so every platform runs through the exact same named checks, never a
/// platform-specific subset.
/// </summary>
public interface IPlatformSmokeTestSuite
{
    Task<PlatformSmokeTestReport> RunAsync(SupportedPlatform platform, string bundleDirectory, CancellationToken ct);
}
