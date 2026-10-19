namespace XlightsAI.Studio.Release;

/// <summary>
/// New, phase-local abstraction (disclosed). Real, independent audit of this step's own implementation
/// guidance, verbatim: "Gate release on ... source/model licenses, documentation, security/privacy
/// checks, changelog, support matrix, signed/checksummed artifact and known issues."
/// </summary>
public interface IReleaseDocumentationAuditor
{
    /// <summary>
    /// Checks every requirement against real on-disk content. Never throws for a missing file or an
    /// absent required substring — each failure becomes its own specific, human-readable issue string
    /// in the returned list (empty list = every requirement satisfied), feeding directly into
    /// <see cref="IReleaseGate.Evaluate"/>'s <c>unresolvedRisks</c>.
    /// </summary>
    IReadOnlyList<string> Audit(IReadOnlyList<ReleaseDocumentationRequirement> requirements);
}
