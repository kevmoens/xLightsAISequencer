namespace XlightsAI.Studio.Choreography;

/// <summary>
/// Build-plan Phase 18 §3 (Phase B)'s own disclosed stages a single fixture's render can stop at -- kept as
/// an explicit, named status (never a bare bool) so a sibling artifact inspected from disk always shows
/// exactly where/why a fixture never produced <see cref="PreviewFrame"/>s, per the phase's own "never a
/// silently skipped entry with no record of why" acceptance criterion.
/// </summary>
public enum EffectFixtureRenderStatus
{
    /// <summary>A real set of decoded <c>PreviewFrame</c>s was produced for this fixture.</summary>
    Rendered,

    /// <summary>Phase A's own compile for this effect/tier already failed; Phase B never attempted it.</summary>
    CompileSkipped,

    /// <summary><see cref="Xlights.XsqExporter"/> rejected the compiled sequence (e.g. an unresolved target, an invalid recipe).</summary>
    ExportFailed,

    /// <summary>The real headless xLights render itself failed, timed out, or reported no usable <c>.fseq</c>.</summary>
    RenderFailed,

    /// <summary>The render reported success but decoding/projecting/persisting its real <c>.fseq</c> failed.</summary>
    DecodeFailed,
}

/// <summary>
/// One effect/tier combination's own Phase B outcome -- additive sibling to <see cref="EffectFixturePlan"/>,
/// never folded into it, so Phase A's own persisted manifest schema stays exactly as it already is
/// (Phase 18 §3 task 4: "keep EffectFixtureManifest's own schema stable/additive, not a breaking rename").
/// </summary>
/// <param name="EffectName">Matches the originating <see cref="EffectFixturePlan.EffectName"/>.</param>
/// <param name="ResolutionTier">Matches the originating <see cref="EffectFixturePlan.ResolutionTier"/>.</param>
/// <param name="Status">Where this fixture's own Phase B attempt stopped -- see <see cref="EffectFixtureRenderStatus"/>.</param>
/// <param name="FailureReason">Null only when <see cref="Status"/> is <see cref="EffectFixtureRenderStatus.Rendered"/>; otherwise a real, disclosed reason, never a silent skip.</param>
/// <param name="ExportedXsqPath">The real, on-disk exported <c>.xsq</c> for this fixture, when export succeeded.</param>
/// <param name="FseqPath">The real, on-disk rendered <c>.fseq</c> for this fixture, when the render itself reported success.</param>
/// <param name="FramesPath">The real, on-disk JSON file holding this fixture's decoded <c>PreviewFrame</c>s, when <see cref="Status"/> is <see cref="EffectFixtureRenderStatus.Rendered"/>.</param>
/// <param name="FrameCount">How many <c>PreviewFrame</c>s were decoded (0 unless a render at least produced frames).</param>
/// <param name="WallClockSeconds">Real measured time this single fixture's own export+render+decode took -- the per-fixture evidence behind this phase's own disclosed "100+ real xLights launches, minutes-to-hours" cost.</param>
public sealed record EffectFixtureRenderOutcome(
    string EffectName,
    EffectFixtureResolutionTier ResolutionTier,
    EffectFixtureRenderStatus Status,
    string? FailureReason,
    string? ExportedXsqPath,
    string? FseqPath,
    string? FramesPath,
    int FrameCount,
    double WallClockSeconds);

/// <summary>
/// Phase 18 §3's own required sibling artifact to <see cref="EffectFixtureManifest"/>: "Every enumerated
/// effect/resolution-tier combination has either a real set of decoded PreviewFrames, or an explicit, logged
/// failure reason... never a silently skipped entry with no record of why." Built once per
/// <c>tools/EffectCharacterizer</c> run by <see cref="EffectFixtureRenderer.RenderAsync"/>.
/// </summary>
public sealed record EffectFixtureRenderManifest(
    DateTimeOffset GeneratedAtUtc,
    string XlightsExecutablePath,
    string ShowDirectory,
    IReadOnlyList<EffectFixtureRenderOutcome> Outcomes)
{
    public int RenderedCount => Outcomes.Count(o => o.Status == EffectFixtureRenderStatus.Rendered);

    public int FailedCount => Outcomes.Count(o => o.Status != EffectFixtureRenderStatus.Rendered);
}
