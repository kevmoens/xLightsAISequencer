namespace XlightsAI.Studio.Choreography;

/// <summary>
/// Build-plan Phase 18 §2 (Phase A): the two representative-model tiers a real effect renders very
/// differently across (confirmed by <see cref="ChoreographyLegalityValidator.TexturalHighResolutionEffects"/>
/// and <see cref="ChoreographyLegalityValidator.LowResolutionNodeCountThreshold"/>) -- every effect in the
/// catalog is characterized against both, never just one.
/// </summary>
public enum EffectFixtureResolutionTier
{
    Low,
    High,
}

/// <summary>
/// One synthetic fixture model Phase A compiles effects against -- deliberately not a real show's own
/// layout (Phase 18 §2's own "otherwise a small synthetic DisplayGraph fixture... stands in" fallback),
/// so this catalog is buildable without any particular user's real show on disk.
/// </summary>
/// <param name="XlightsName">Matches <see cref="Xlights.DisplayModel.XlightsName"/> -- what <see cref="PropGroupResolver.Resolve"/> matches a <see cref="LookLayer.Props"/> reference against.</param>
/// <param name="Kind">The model's <see cref="Xlights.DisplayModel.Kind"/> (display-only; not read by <see cref="LookCompiler"/>).</param>
/// <param name="NodeCount">Pixel count -- the one dimension that actually drives <see cref="ChoreographyLegalityValidator.LowResolutionNodeCountThreshold"/>'s own distinction.</param>
public sealed record EffectFixtureModel(string XlightsName, string Kind, int NodeCount);

/// <summary>
/// One effect/tier combination's own fixture plan: what it will be compiled against, and (once compiled)
/// whether that compile actually succeeded -- Phase 18 §2's own acceptance criterion that every enumerated
/// effect "either has a real set of decoded PreviewFrames, or an explicit, logged failure reason... never a
/// silently skipped entry with no record of why" starts here, one phase early, at the compile step.
/// </summary>
public sealed record EffectFixturePlan(
    string EffectName,
    EffectFixtureResolutionTier ResolutionTier,
    EffectFixtureModel Model,
    double DurationSeconds,
    bool CompiledSuccessfully,
    string? CompileFailureReason);

/// <summary>
/// Phase 18 §2's own required artifact: "a real, inspectable list of every effect name this pass will
/// characterize, with its own resolved representative-model assignment(s) and duration, persisted somewhere
/// reviewable... before any rendering is attempted." Built once per <c>tools/EffectCharacterizer</c> run by
/// <see cref="EffectFixturePlanner.Build"/>, consumed by Phase B's own render step (not yet implemented).
/// </summary>
public sealed record EffectFixtureManifest(
    DateTimeOffset GeneratedAtUtc,
    string EffectMetadataDirectory,
    IReadOnlyList<EffectFixturePlan> Fixtures)
{
    public int SucceededCount => Fixtures.Count(f => f.CompiledSuccessfully);

    public int FailedCount => Fixtures.Count(f => !f.CompiledSuccessfully);
}
