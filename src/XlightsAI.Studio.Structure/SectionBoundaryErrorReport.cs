namespace XlightsAI.Studio.Structure;

/// <summary>
/// Step 053's own required acceptance, verbatim: "Annotated song has useful boundaries <b>and error
/// report</b>." An honest account of how a <see cref="SectionBoundaryAnalysis"/> run actually behaved
/// -- never fabricated as a clean, fully-converged result when the adaptive threshold search did not
/// actually reach its target (Step 053's own failure/edge case: "no-improvement and exhausted retry
/// budget must not auto-commit" -- satisfied by reporting the truth here rather than silently
/// presenting a best-effort, possibly sparse boundary list as if it were a confident final answer).
/// </summary>
/// <param name="IterationsRun">How many adaptive-threshold refinement passes actually executed, bounded by <see cref="SectionBoundarySettings.MaxRefinementIterations"/>.</param>
/// <param name="Converged">
/// <see langword="true"/> only when the search reached <see cref="SectionBoundarySettings.MinBoundaryCount"/>
/// candidates before exhausting <see cref="IterationsRun"/>'s own budget or plateauing. <see langword="false"/>
/// means <see cref="SectionBoundaryAnalysis.Boundaries"/> is a disclosed best-effort result, not a
/// silently-accepted final one.
/// </param>
/// <param name="RetryBudgetExhausted"><see langword="true"/> when the loop stopped because <see cref="SectionBoundarySettings.MaxRefinementIterations"/> was reached without converging -- distinct from a deliberate early stop on plateau (see <see cref="Warnings"/>).</param>
/// <param name="Warnings">Human-readable, non-throwing observations about this run (e.g. "threshold search plateaued at N candidates before reaching MinBoundaryCount"; "song had fewer bars than MinBoundaryCount can ever satisfy"). Empty when the search converged cleanly on its first iteration.</param>
public sealed record SectionBoundaryErrorReport(
    int IterationsRun,
    bool Converged,
    bool RetryBudgetExhausted,
    IReadOnlyList<string> Warnings);
