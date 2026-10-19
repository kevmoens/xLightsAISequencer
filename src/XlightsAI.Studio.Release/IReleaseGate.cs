namespace XlightsAI.Studio.Release;

/// <summary>
/// PHASE-16 Section 3 proposed contract, kept exactly as specified. The single, real, auditable decision
/// point Section 1 calls for ("Treat release as an auditable gate, not ceremonial documentation"): given
/// every real <see cref="TestEvidence"/> collected so far (compatibility matrix, license/doc audit,
/// checksum reproducibility, diagnostics) plus any additional <paramref name="unresolvedRisks"/> not
/// expressed as a single pass/fail check, decide whether this version may actually be released.
/// </summary>
public interface IReleaseGate
{
    /// <summary>
    /// Evaluates real, already-collected evidence and any additional named, unresolved risk strings.
    /// Never re-runs a check itself — a pure decision over evidence the caller already observed.
    /// </summary>
    ReleaseReadiness Evaluate(IReadOnlyList<TestEvidence> evidence, IReadOnlyList<string> unresolvedRisks);
}
