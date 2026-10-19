using XlightsAI.Studio.Display;

namespace XlightsAI.Studio.Refinement;

/// <summary>
/// PHASE-13 Step 089 ("Add render/evaluation gate to refinement and report limitations"). A pure decorator
/// over an already-accepted <see cref="IChoreographyQualityEvaluator"/> (PHASE-12 Step 081's
/// <see cref="GestureQualityEvaluator"/>, unmodified): runs the inner evaluator first, then runs
/// <see cref="IRenderEvaluationGate"/> and merges its <see cref="MetricValue"/> evidence into the same
/// <see cref="QualityReport"/> rather than producing a second, separate report -- this step's own
/// implementation guidance, verbatim: "Integrate xLights render results as separate hard and soft evaluation
/// evidence in loop."
/// <para>
/// <b>Architecture decision -- composition over modifying <see cref="GestureRefinementOrchestrator"/>
/// (disclosed).</b> PHASE-12 Step 083's own already-accepted <see cref="GestureRefinementOrchestrator"/> is
/// constructed against the <see cref="IChoreographyQualityEvaluator"/> interface alone, by design (dependency
/// injection) -- wrapping <see cref="GestureQualityEvaluator"/> in this decorator and handing the wrapped
/// instance to the orchestrator's own already-accepted constructor integrates render evidence into the
/// existing loop with zero change to <see cref="GestureRefinementOrchestrator"/>'s own source (an immutable
/// prior-step contract, per this repository's own "treat all preceding completed step contracts as immutable"
/// rule) and zero migration risk to its own already-passing Step 083 tests.
/// </para>
/// <para>
/// <b>"Require user acceptance even when score improves" (disclosed -- already true by construction, not
/// changed here).</b> This step's own implementation guidance also reads, verbatim: "require user acceptance
/// even when score improves." Neither this decorator nor <see cref="GestureRefinementOrchestrator"/> ever
/// calls <see cref="IRefinementCandidateCommitService.AcceptAsync"/> -- the orchestrator only ever persists a
/// run into <see cref="RefinementStatus.AwaitingApproval"/> (Step 083's own already-accepted stop behavior),
/// regardless of how a merged <see cref="QualityReport"/>'s own aggregate score compares to a prior attempt's.
/// A strictly-improving render-gated candidate therefore still requires the same explicit, separate human
/// <see cref="IRefinementCandidateCommitService.AcceptAsync"/> call every other candidate already required --
/// this step adds a regression test proving that invariant (see <c>Step089AcceptanceTests</c>) rather than new
/// production code for a requirement the architecture already satisfies.
/// </para>
/// </summary>
public sealed class RenderGatedQualityEvaluator : IChoreographyQualityEvaluator
{
    private readonly IChoreographyQualityEvaluator _inner;
    private readonly IRenderEvaluationGate _renderGate;
    private readonly SpatialDisplayGraph _layout;

    public RenderGatedQualityEvaluator(IChoreographyQualityEvaluator inner, IRenderEvaluationGate renderGate, SpatialDisplayGraph layout)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _renderGate = renderGate ?? throw new ArgumentNullException(nameof(renderGate));
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
    }

    public async Task<QualityReport> EvaluateAsync(LoopRun run, CandidateAttempt candidate, ChoreographyQualityContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(context);
        ct.ThrowIfCancellationRequested();

        var baseReport = await _inner.EvaluateAsync(run, candidate, context, ct).ConfigureAwait(false);
        var renderMetrics = await _renderGate.EvaluateAsync(run, candidate, _layout, ct).ConfigureAwait(false);

        var mergedMetrics = baseReport.Metrics.Concat(renderMetrics).ToList();

        // Mirrors QualityReport.Validate's own rule ("a failed hard gate must always be reflected in
        // Violations so a candidate cannot auto-commit") for the newly-merged render metrics specifically --
        // this step's own required acceptance, verbatim: "Broken/unrenderable proposal cannot auto-apply;
        // clear diagnostics."
        var renderViolations = renderMetrics
            .Where(m => m.HardGate && m.Value == 0.0)
            .Select(m => $"Render hard gate '{m.Name}' failed: {m.Explanation}")
            .ToList();

        var mergedViolations = baseReport.Violations.Concat(renderViolations).ToList();

        var merged = baseReport with { Metrics = mergedMetrics, Violations = mergedViolations };
        merged.Validate();
        return merged;
    }
}
