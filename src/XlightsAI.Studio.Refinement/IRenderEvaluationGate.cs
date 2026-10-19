using XlightsAI.Studio.Display;

namespace XlightsAI.Studio.Refinement;

/// <summary>
/// PHASE-13 Step 089 ("Add render/evaluation gate to refinement and report limitations"). This step's own
/// implementation guidance, verbatim: "Integrate xLights render results as separate hard and soft evaluation
/// evidence in loop." Produces additional <see cref="MetricValue"/> entries -- never a whole new
/// <see cref="QualityReport"/> of its own -- so a caller (<see cref="RenderGatedQualityEvaluator"/>) can merge
/// render evidence alongside <see cref="IChoreographyQualityEvaluator"/>'s own already-accepted metrics,
/// rather than replacing them.
/// <para>
/// <b>Architecture decision -- the fast preview tier, not the authoritative xLights process (disclosed).</b>
/// PHASE-13 Section 3 names two unequal render tiers: <see cref="XlightsAI.Studio.Rendering.ILocalPreviewRenderer"/>
/// (Step 085, fast/local/always-available) and <see cref="XlightsAI.Studio.Rendering.IRenderAdapter"/> (Step
/// 086, launches a real installed xLights process -- slow, and only available on a machine with a compatible
/// build). Every attempt inside a bounded refinement loop (potentially dozens per run, PHASE-12 Section 3)
/// needs cheap, synchronous-feeling evidence to decide whether to keep iterating -- launching a real xLights
/// process once per attempt would make every loop iteration as slow as a full external render, and would make
/// the loop itself depend on an installed xLights build simply to run at all (this phase's own Section 2:
/// "no assumption every installed release supports ... headless"). This gate therefore evaluates every
/// in-loop attempt against the fast preview tier only; the authoritative <see cref="XlightsAI.Studio.Rendering.IRenderAdapter"/>
/// render remains the separate, explicit, user-triggered verification this phase's own Section 1 user story
/// describes ("...then run xLights itself to confirm") -- performed once, by the human, against the single
/// candidate they are about to accept via <see cref="IRefinementCandidateCommitService.AcceptAsync"/>, not
/// automatically inside this loop.
/// </para>
/// </summary>
public interface IRenderEvaluationGate
{
    /// <summary>
    /// Renders <paramref name="candidate"/>'s own gestures against <paramref name="layout"/> over
    /// <paramref name="run"/>'s own permitted time scope and returns additional render-derived
    /// <see cref="MetricValue"/> entries -- at minimum, a hard gate named <c>"RenderPreview"</c>. Never
    /// throws for a malformed/unrenderable candidate; a broken candidate is reported as a failed hard-gate
    /// <see cref="MetricValue"/> with a clear <see cref="MetricValue.Explanation"/>, the same "report, never
    /// throw" shape <see cref="IChoreographyQualityEvaluator.EvaluateAsync"/> already uses -- this step's own
    /// required acceptance, verbatim: "Broken/unrenderable proposal cannot auto-apply; clear diagnostics."
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="run"/>, <paramref name="candidate"/>, or <paramref name="layout"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was cancelled before evaluation completed.</exception>
    Task<IReadOnlyList<MetricValue>> EvaluateAsync(LoopRun run, CandidateAttempt candidate, SpatialDisplayGraph layout, CancellationToken ct);
}
