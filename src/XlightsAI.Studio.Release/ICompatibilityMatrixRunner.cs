namespace XlightsAI.Studio.Release;

/// <summary>
/// PHASE-16 Section 3 proposed contract, kept exactly as specified. Step 098 ("Run tested
/// OS/xLights/audio/layout compatibility matrix and record known limitations"): runs each supplied
/// <see cref="CompatibilityCase"/> against real, already-imported layout/audio fixtures and a real
/// xLights-format export, returning one <see cref="TestEvidence"/> per case. Never throws for an
/// individual case's own real, expected failure (host cannot run the combination, invalid fixture
/// reference, invalid target xLights version, ...) — that is reported as a non-passing
/// <see cref="TestEvidence"/> instead (Section 2: "detect actual versions/capabilities... no
/// undocumented claim that all xLights versions... behave identically").
/// </summary>
public interface ICompatibilityMatrixRunner
{
    /// <exception cref="ArgumentNullException"><paramref name="cases"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<TestEvidence>> RunAsync(IReadOnlyList<CompatibilityCase> cases, CancellationToken ct);
}
