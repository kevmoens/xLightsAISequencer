namespace XlightsAI.Studio.Distribution;

/// <summary>
/// PHASE-15 Step 097. The real, reproducible test report for one <see cref="SupportedPlatform"/> -- this
/// step's own user-visible outcome, verbatim: "Versioned, inspectable project output plus a reproducible test
/// report."
/// </summary>
/// <param name="Platform">The platform this report was produced for.</param>
/// <param name="Checks">Every independent check that was attempted, in run order.</param>
public sealed record PlatformSmokeTestReport(SupportedPlatform Platform, IReadOnlyList<PlatformSmokeCheckResult> Checks);
