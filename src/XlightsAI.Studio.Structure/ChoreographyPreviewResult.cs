using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Structure;

/// <summary>
/// The result of <see cref="IChoreographyTemplatePlanner.PreviewInstance"/>: the real
/// <see cref="EffectInstance"/> list this one occurrence would receive if accepted, plus a human-readable
/// reason for every template/override effect that was deliberately left out -- "preview effect list and
/// respect locks" (Step 057's own implementation guidance, verbatim). Never itself a mutation: nothing here
/// is written to any <see cref="SequenceDocument"/> until a later, explicit accept step applies it through
/// the normal revision-aware edit pipeline (Section 4: "Preview artifacts and AI proposals cannot update the
/// accepted document directly").
/// </summary>
/// <param name="Effects">Every effect this occurrence would receive, each carrying a stable, deterministic <see cref="EffectInstance.Id"/> derived from the target phrase and originating source effect so a repeated preview call for the same inputs is reproducible.</param>
/// <param name="ExcludedReasons">Human-readable, non-throwing reasons a template/override effect was left out of <see cref="Effects"/> -- either it would overwrite a locked destination effect ("respect locks," verbatim), or (<see cref="ChoreographyTimingPolicy.Copy"/> only) its copied window does not fit inside the target phrase.</param>
public sealed record ChoreographyPreviewResult(
    IReadOnlyList<EffectInstance> Effects,
    IReadOnlyList<string> ExcludedReasons);
