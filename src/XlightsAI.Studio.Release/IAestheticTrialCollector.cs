namespace XlightsAI.Studio.Release;

/// <summary>
/// PHASE-16 Section 3 proposed contract, kept exactly as specified. Step 099 ("Conduct A/B musicality
/// evaluations and manual-effort measurement on varied songs"): persists one real human
/// <see cref="AestheticRating"/>. Never persists an invalid rating (Step 099's own required
/// failure/edge case: "Invalid input... must not corrupt the project") and never silently drops an
/// already-persisted rating on a later failure (append-only, atomic write — see
/// <see cref="FileAestheticTrialCollector"/>).
/// </summary>
public interface IAestheticTrialCollector
{
    /// <exception cref="ArgumentNullException"><paramref name="rating"/> is <see langword="null"/>.</exception>
    /// <exception cref="AestheticRatingInvalidException"><paramref name="rating"/> fails its own <see cref="AestheticRating.Validate"/> check.</exception>
    /// <exception cref="AestheticTrialStoreCorruptException">The persisted trial store exists but is not well-formed.</exception>
    Task RecordAsync(AestheticRating rating, CancellationToken ct);
}
