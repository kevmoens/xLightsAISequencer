namespace XlightsAI.Studio.Distribution;

/// <summary>
/// PHASE-15 Step 097. Pure domain rule, no I/O: turns a set of real <see cref="PlatformSmokeTestReport"/>s
/// into the actual, honest <see cref="PlatformReleaseDecision"/> -- deterministic and independently testable
/// without needing a real publish or a real OS probe.
/// </summary>
public interface IPlatformReleaseEvaluator
{
    PlatformReleaseDecision Evaluate(IReadOnlyList<PlatformSmokeTestReport> reports);
}
