namespace XlightsAI.Studio.Release;

/// <summary>
/// PHASE-16 Section 3 proposed contract, kept exactly as specified. The final, real release-gate
/// decision (Step 100, "Finalize documentation, attribution/licenses, privacy defaults, diagnostics,
/// changelog and versioned release") for one product <see cref="Version"/>: <see cref="Eligible"/> is
/// <see langword="true"/> only when every piece of supplied <see cref="Evidence"/> actually passed and
/// no <see cref="Blockers"/> remain — Section 1's own phase gate rule, verbatim: "Every blocker must be
/// fixed, deferred behind feature flag with documentation, or removed from supported claims." Never
/// constructed with <c>Eligible = true</c> while <see cref="Blockers"/> is non-empty — <see cref="IReleaseGate"/>
/// is this record's sole real producer and enforces that invariant.
/// </summary>
/// <param name="Version">The product version this readiness decision describes.</param>
/// <param name="Eligible">Whether this version may be tagged/published as a release right now.</param>
/// <param name="Blockers">Every real, specific, human-readable reason release is withheld; empty only when <see cref="Eligible"/> is <see langword="true"/>.</param>
/// <param name="Evidence">Every <see cref="TestEvidence"/> this decision was computed from.</param>
public sealed record ReleaseReadiness(
    string Version,
    bool Eligible,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<TestEvidence> Evidence);
