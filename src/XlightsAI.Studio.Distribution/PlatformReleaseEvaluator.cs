namespace XlightsAI.Studio.Distribution;

/// <summary>
/// Real <see cref="IPlatformReleaseEvaluator"/>. A platform is advertisable only when it has at least one
/// check and every check is both <see cref="PlatformSmokeCheckResult.Runnable"/> and
/// <see cref="PlatformSmokeCheckResult.Passed"/> -- this step's own required acceptance, verbatim: "Each
/// advertised platform passes [the] same smoke-test suite." A platform with a skipped (not-runnable) or failed
/// check is never advertised; it is recorded with the real, specific reason for every such check instead.
/// </summary>
public sealed class PlatformReleaseEvaluator : IPlatformReleaseEvaluator
{
    public PlatformReleaseDecision Evaluate(IReadOnlyList<PlatformSmokeTestReport> reports)
    {
        ArgumentNullException.ThrowIfNull(reports);

        var advertised = new List<string>();
        var unsupported = new Dictionary<string, IReadOnlyList<string>>();

        foreach (var report in reports)
        {
            var unresolved = report.Checks
                .Where(check => !check.Runnable || !check.Passed)
                .Select(check => $"{check.Name}: {check.Detail}")
                .ToList();

            if (unresolved.Count == 0 && report.Checks.Count > 0)
            {
                advertised.Add(report.Platform.RuntimeIdentifier);
            }
            else
            {
                unsupported[report.Platform.RuntimeIdentifier] = unresolved.Count == 0
                    ? ["No smoke-test checks were run for this platform."]
                    : unresolved;
            }
        }

        return new PlatformReleaseDecision(advertised, unsupported);
    }
}
