namespace XlightsAI.Studio.WholeSong;

/// <summary>
/// Mirrors <see cref="XlightsAI.Studio.Composition.IVisualStoryCatalog"/>'s own application-orchestration
/// boundary: validates, persists and versions one song's <see cref="SongDirectionPlan"/>. Rejects an invalid
/// plan (<see cref="SongDirectionPlan.Validate"/> failure) <em>before</em> loading or writing anything, and
/// always supplies the correct currently-persisted version to <see cref="ISongDirectionPlanStore.SaveAsync"/>
/// itself -- a normal catalog-mediated save can never be rejected as stale; staleness can only be observed
/// by a caller that bypasses this catalog and calls <see cref="ISongDirectionPlanStore"/> directly with a
/// version it captured earlier (the exact scenario this step's own negative-path test exercises).
/// </summary>
public interface ISongDirectionCatalog
{
    /// <summary>Loads a previously saved plan for <paramref name="songId"/>, or <see langword="null"/> if none has ever been saved.</summary>
    Task<SongDirectionPlanRecord?> GetAsync(Guid songId, CancellationToken ct);

    /// <summary>
    /// Saves <paramref name="plan"/> under <paramref name="songId"/>, replacing any prior plan for the same
    /// song and incrementing its <see cref="SongDirectionPlanRecord.Version"/>.
    /// </summary>
    /// <exception cref="SongDirectionInvalidException"><paramref name="plan"/> fails <see cref="SongDirectionPlan.Validate"/>.</exception>
    Task<SongDirectionPlanRecord> SaveAsync(Guid songId, SongDirectionPlan plan, CancellationToken ct);
}
