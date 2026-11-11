namespace XlightsAI.Studio.Choreography;

/// <summary>
/// Build-plan Phase 18 §4 (Phase C)'s own disclosed stages a single fixture's video encode can stop at --
/// same discipline as <see cref="EffectFixtureRenderStatus"/>: a named status, never a bare bool, so a
/// sibling artifact inspected from disk always shows exactly where/why a fixture never produced a playable
/// video, per the phase's own "never a silently skipped entry" acceptance criterion.
/// </summary>
public enum EffectFixtureVideoStatus
{
    /// <summary>A real, playable animated GIF was encoded and persisted for this fixture.</summary>
    Encoded,

    /// <summary>Phase B's own render for this effect/tier never reached <see cref="EffectFixtureRenderStatus.Rendered"/>; Phase C never attempted it.</summary>
    RenderSkipped,

    /// <summary>Phase B reported <see cref="EffectFixtureRenderStatus.Rendered"/>, but its persisted <c>frames.json</c> could not be read back.</summary>
    FramesReadFailed,

    /// <summary>Rasterizing or GIF-encoding the real decoded frames failed.</summary>
    EncodeFailed,
}

/// <summary>
/// One effect/tier combination's own Phase C outcome -- additive sibling to <see cref="EffectFixtureRenderOutcome"/>,
/// never folded into it or <see cref="EffectFixtureManifest"/>, so both earlier phases' own persisted manifest
/// schemas stay exactly as they already are.
/// </summary>
/// <param name="EffectName">Matches the originating <see cref="EffectFixtureRenderOutcome.EffectName"/>.</param>
/// <param name="ResolutionTier">Matches the originating <see cref="EffectFixtureRenderOutcome.ResolutionTier"/>.</param>
/// <param name="Status">Where this fixture's own Phase C attempt stopped -- see <see cref="EffectFixtureVideoStatus"/>.</param>
/// <param name="FailureReason">Null only when <see cref="Status"/> is <see cref="EffectFixtureVideoStatus.Encoded"/>; otherwise a real, disclosed reason, never a silent skip.</param>
/// <param name="VideoPath">The real, on-disk animated GIF for this fixture, when <see cref="Status"/> is <see cref="EffectFixtureVideoStatus.Encoded"/>.</param>
/// <param name="FrameCount">How many rasterized frames were encoded into <see cref="VideoPath"/> (0 unless encoding at least started with real frames).</param>
/// <param name="WallClockSeconds">Real measured time this single fixture's own frames-read+rasterize+GIF-encode took.</param>
public sealed record EffectFixtureVideoOutcome(
    string EffectName,
    EffectFixtureResolutionTier ResolutionTier,
    EffectFixtureVideoStatus Status,
    string? FailureReason,
    string? VideoPath,
    int FrameCount,
    double WallClockSeconds);

/// <summary>
/// Phase 18 §4's own required sibling artifact to <see cref="EffectFixtureRenderManifest"/>: "a real, playable
/// video file exists per successfully-rendered effect/tier combination... never a silently skipped entry."
/// Built once per <c>tools/EffectCharacterizer</c> run by <see cref="EffectFixtureVideoEncoder"/>.
/// </summary>
public sealed record EffectFixtureVideoManifest(
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<EffectFixtureVideoOutcome> Outcomes)
{
    public int EncodedCount => Outcomes.Count(o => o.Status == EffectFixtureVideoStatus.Encoded);

    public int FailedCount => Outcomes.Count(o => o.Status != EffectFixtureVideoStatus.Encoded);
}
