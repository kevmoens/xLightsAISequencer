namespace XlightsAI.Studio.Release;

/// <summary>
/// Default <see cref="IReleaseGate"/>. PHASE-16 Step 100's own required acceptance, verbatim:
/// "Reproducible artifacts and first-user walkthrough tested." Section 1's own phase gate rule,
/// verbatim: "Every blocker must be fixed, deferred behind feature flag with documentation, or removed
/// from supported claims" — realized here as a pure, deterministic rule with no hidden weighting or
/// scoring (Section 9's own prompt, verbatim: "Hard gates cannot be traded for weighted scoring").
/// <see cref="Version"/> is supplied at construction, not per-call — Section 3's own proposed
/// <see cref="IReleaseGate.Evaluate"/> signature carries no version parameter, so the version this
/// gate decides for is a phase-local, disclosed constructor addition (mirrors
/// <see cref="CompatibilityMatrixRunner"/>'s own <c>artifactDirectory</c> constructor parameter
/// precedent) rather than a change to the Section-3-proposed interface shape.
/// </summary>
public sealed class ReleaseGate : IReleaseGate
{
    private readonly string _version;

    public ReleaseGate(string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        _version = version;
    }

    public ReleaseReadiness Evaluate(IReadOnlyList<TestEvidence> evidence, IReadOnlyList<string> unresolvedRisks)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(unresolvedRisks);

        var blockers = new List<string>();

        foreach (var item in evidence)
        {
            if (!item.Passed)
            {
                blockers.Add(string.IsNullOrWhiteSpace(item.Detail)
                    ? $"Evidence '{item.TestId}' did not pass."
                    : $"Evidence '{item.TestId}' did not pass: {item.Detail}");
            }
        }

        foreach (var risk in unresolvedRisks)
        {
            if (!string.IsNullOrWhiteSpace(risk))
            {
                blockers.Add(risk);
            }
        }

        var eligible = blockers.Count == 0 && evidence.Count > 0;
        if (eligible is false && evidence.Count == 0 && unresolvedRisks.Count == 0)
        {
            blockers.Add("No release evidence was supplied; a release cannot be evaluated from zero checks.");
        }

        return new ReleaseReadiness(_version, eligible, blockers, evidence);
    }
}
