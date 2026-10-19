namespace XlightsAI.Studio.WholeSong;

/// <summary>
/// Mirrors <see cref="ISongDirectionCatalog"/>'s own application-orchestration boundary: validates, persists
/// and versions one song's <see cref="MotifVariationPlan"/>. Rejects an invalid plan
/// (<see cref="MotifVariationPlan.Validate"/> failure) <em>before</em> loading or writing anything, and
/// always supplies the correct currently-persisted version to <see cref="IMotifVariationPlanStore.SaveAsync"/>
/// itself -- a normal catalog-mediated save can never be rejected as stale; staleness can only be observed by
/// a caller that bypasses this catalog and calls <see cref="IMotifVariationPlanStore"/> directly with a
/// version it captured earlier.
/// </summary>
public interface IMotifVariationCatalog
{
    /// <summary>Loads a previously saved plan for <paramref name="songId"/>, or <see langword="null"/> if none has ever been saved.</summary>
    Task<MotifVariationPlanRecord?> GetAsync(Guid songId, CancellationToken ct);

    /// <summary>
    /// Saves <paramref name="plan"/> under <paramref name="songId"/>, replacing any prior plan for the same
    /// song and incrementing its <see cref="MotifVariationPlanRecord.Version"/>.
    /// </summary>
    /// <exception cref="MotifVariationInvalidException"><paramref name="plan"/> fails <see cref="MotifVariationPlan.Validate"/>.</exception>
    Task<MotifVariationPlanRecord> SaveAsync(Guid songId, MotifVariationPlan plan, CancellationToken ct);
}
