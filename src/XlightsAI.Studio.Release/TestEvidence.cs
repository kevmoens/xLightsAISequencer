namespace XlightsAI.Studio.Release;

/// <summary>
/// PHASE-16 Section 3 proposed contract. One real, actually-observed result for a single
/// <see cref="CompatibilityCase"/> or other phase-16 check — Section 2's own verification rule applied
/// here: "Do not claim tests ran when they did not." <see cref="ArtifactPath"/> is the real on-disk
/// path a reviewer can open to inspect the actual produced output (e.g. the exported <c>.xsq</c>) when
/// <see cref="Passed"/> is <see langword="true"/>; it is empty for a failed/skipped case, since this
/// exporter never writes a partial file for a request it rejects.
/// </summary>
/// <param name="TestId">Stable, human-readable identity for this one check (e.g. the compatibility case it came from).</param>
/// <param name="Passed">Whether this check's real, observed outcome succeeded.</param>
/// <param name="ArtifactPath">The real on-disk artifact this check produced, or empty when none was produced.</param>
/// <param name="ToolVersion">The target/tool version this check exercised (e.g. the xLights version an export targeted).</param>
/// <param name="RecordedUtc">When this result was actually observed.</param>
/// <param name="Detail">
/// Phase-local additive field (not in the Section 3 proposed shape; disclosed): the real, specific
/// reason behind <see cref="Passed"/> — e.g. "cannot install/run on this host" or the exact exception
/// message from the rejected step — so a failure is never reported bare. Optional/defaulted so every
/// Section-3-proposed positional construction still compiles unchanged.
/// </param>
public sealed record TestEvidence(
    string TestId,
    bool Passed,
    string ArtifactPath,
    string ToolVersion,
    DateTimeOffset RecordedUtc,
    string? Detail = null);
