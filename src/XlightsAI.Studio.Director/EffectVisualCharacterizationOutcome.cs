using XlightsAI.Studio.Choreography;

namespace XlightsAI.Studio.Director;

/// <summary>
/// Build-plan Phase 18 §5 (Phase D)'s own disclosed stages a single fixture's AI characterization can stop
/// at -- same discipline as <see cref="EffectFixtureRenderStatus"/>/<see cref="EffectFixtureVideoStatus"/>:
/// a named status, never a bare bool, so a sibling artifact inspected from disk always shows exactly
/// where/why a fixture never got a real description, per this phase's own "never a silently skipped entry"
/// acceptance criterion.
/// </summary>
public enum EffectVisualCharacterizationStatus
{
    /// <summary>A real, parsed description was returned by the configured vision-capable AI role.</summary>
    Characterized,

    /// <summary>Phase B's own render for this effect/tier never reached <see cref="EffectFixtureRenderStatus.Rendered"/>; this phase never attempted it.</summary>
    RenderSkipped,

    /// <summary>Phase B reported <see cref="EffectFixtureRenderStatus.Rendered"/>, but its persisted <c>frames.json</c> could not be read back.</summary>
    FramesReadFailed,

    /// <summary>The AI call itself failed (provider error, malformed/unparseable response).</summary>
    AiCallFailed,
}

/// <summary>
/// One effect/tier's own real, AI-generated visual description -- build-plan Phase 18 §5's own disclosed
/// heuristic, not ground truth (same "disclosed heuristic, not claimed fact" discipline this codebase already
/// applies to <c>VisemeApproximator</c>). Every field is the model's own free-text answer to a fixed
/// question, never post-processed/normalized into an enum -- a later Phase E consumer reads these as prose.
/// </summary>
public sealed record EffectVisualCharacterizationDescription(
    string MotionType,
    string Direction,
    string ApparentSpeed,
    string TextureDensity,
    string Summary);

/// <summary>
/// One effect/tier combination's own Phase D outcome -- additive sibling to
/// <see cref="EffectFixtureRenderOutcome"/>/<see cref="EffectFixtureVideoOutcome"/>, never folded into
/// either, so every earlier phase's own persisted manifest schema stays exactly as it already is.
/// </summary>
/// <param name="EffectName">Matches the originating <see cref="EffectFixtureRenderOutcome.EffectName"/>.</param>
/// <param name="ResolutionTier">Matches the originating <see cref="EffectFixtureRenderOutcome.ResolutionTier"/>.</param>
/// <param name="Status">Where this fixture's own Phase D attempt stopped -- see <see cref="EffectVisualCharacterizationStatus"/>.</param>
/// <param name="FailureReason">Null only when <see cref="Status"/> is <see cref="EffectVisualCharacterizationStatus.Characterized"/>; otherwise a real, disclosed reason, never a silent skip.</param>
/// <param name="Description">The real AI-generated description, when <see cref="Status"/> is <see cref="EffectVisualCharacterizationStatus.Characterized"/>.</param>
/// <param name="WallClockSeconds">Real measured time this single fixture's own frames-read+still-sample+AI-call took.</param>
/// <param name="MeanBrightness">
/// Build-plan Phase 21 §2a: this fixture's own real, measured whole-canvas mean luminance (0-1), from
/// <see cref="XlightsAI.Studio.Rendering.EffectFrameBrightnessMeasurer"/> run over the exact same frames <see cref="Description"/>
/// (when present) was written from. Populated whenever this fixture's real frames were successfully read --
/// independent of whether the AI call itself succeeded, since the measurement spends no AI budget and needs
/// nothing beyond the frames Phase B already decoded. Null only when frames were never read (<see cref="EffectVisualCharacterizationStatus.RenderSkipped"/>/<see cref="EffectVisualCharacterizationStatus.FramesReadFailed"/>).
/// </param>
/// <param name="PeakBrightness">This fixture's single brightest frame's own whole-canvas mean (0-1) -- see <see cref="XlightsAI.Studio.Rendering.EffectFrameBrightnessMeasurer"/>. Null under the same conditions as <see cref="MeanBrightness"/>.</param>
public sealed record EffectVisualCharacterizationOutcome(
    string EffectName,
    EffectFixtureResolutionTier ResolutionTier,
    EffectVisualCharacterizationStatus Status,
    string? FailureReason,
    EffectVisualCharacterizationDescription? Description,
    double WallClockSeconds,
    double? MeanBrightness = null,
    double? PeakBrightness = null);

/// <summary>
/// Phase 18 §5's own required sibling artifact: "A real generated description exists for every
/// successfully-rendered effect/tier combination... never a silently skipped entry." Built once per
/// <c>tools/EffectCharacterizer</c> run by <see cref="EffectVisualCharacterizer"/>.
/// </summary>
public sealed record EffectVisualCharacterizationManifest(
    DateTimeOffset GeneratedAtUtc,
    string ModelName,
    IReadOnlyList<EffectVisualCharacterizationOutcome> Outcomes)
{
    public int CharacterizedCount => Outcomes.Count(o => o.Status == EffectVisualCharacterizationStatus.Characterized);

    public int FailedCount => Outcomes.Count(o => o.Status != EffectVisualCharacterizationStatus.Characterized);
}
