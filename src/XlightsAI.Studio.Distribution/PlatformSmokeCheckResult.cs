namespace XlightsAI.Studio.Distribution;

/// <summary>
/// PHASE-15 Step 097. One named, independent check result inside a <see cref="PlatformSmokeTestReport"/>.
/// <see cref="Runnable"/> is distinct from <see cref="Passed"/>: a check that genuinely cannot execute on the
/// current host (e.g. launching a Linux apphost from a Windows machine) is <c>Runnable: false</c>, never a
/// faked pass -- "document unsupported combinations rather than shipping broken bundles" (Section 3
/// architectural design note, verbatim) depends on this distinction existing as real data, not a single opaque
/// boolean.
/// </summary>
/// <param name="Name">Stable check identifier, e.g. <c>"ApphostLaunches"</c>.</param>
/// <param name="Runnable">Whether this check could actually execute on the current host for this platform.</param>
/// <param name="Passed">The check's real result; only meaningful when <see cref="Runnable"/> is <see langword="true"/>.</param>
/// <param name="Detail">Human-readable explanation of what was actually observed.</param>
public sealed record PlatformSmokeCheckResult(string Name, bool Runnable, bool Passed, string Detail);
