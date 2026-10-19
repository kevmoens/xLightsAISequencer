namespace XlightsAI.Studio.Distribution;

/// <summary>
/// PHASE-15 Step 097. Isolates the one genuinely side-effecting, host-OS-dependent part of a platform smoke
/// test -- actually starting a published apphost and observing whether it keeps running -- behind its own
/// interface, the same "explicit path, never silently assumed" boundary convention this project already
/// applies elsewhere (e.g. <c>XlightsAI.Studio.Xlights.XlightsLauncher</c>). <see cref="ProcessApphostLauncher"/>
/// is the real, production implementation; acceptance tests use an explicit deterministic stand-in instead of
/// spawning real OS processes, so the suite's own decision logic can be tested without flaky cross-platform
/// process-launch behavior in CI.
/// </summary>
public interface IApphostLauncher
{
    /// <summary>
    /// Never throws for a missing file or a process that fails/exits immediately -- those are reportable,
    /// real results, not crashes (Section 2: "detect actual versions/capabilities").
    /// </summary>
    Task<(bool Launched, string Detail)> TryLaunchAsync(string apphostPath, CancellationToken ct);
}
