namespace XlightsAI.Studio.Release;

/// <summary>
/// Phase-local contract (not in the Section 3 proposed signature list; disclosed — mirrors Step 098's
/// own <c>ICompatibilityFixtureCatalog</c> precedent of isolating one genuinely new, narrow concern
/// Section 3 did not name). Section 3 defines how a rating is recorded
/// (<see cref="IAestheticTrialCollector"/>) but not how the already-recorded set is read back for honest
/// reporting (<see cref="AestheticTrialSummaryReporter"/>) — this closes that gap.
/// </summary>
public interface IAestheticTrialReader
{
    /// <exception cref="AestheticTrialStoreCorruptException">The persisted trial store exists but is not well-formed.</exception>
    Task<IReadOnlyList<AestheticRating>> LoadAllAsync(CancellationToken ct);
}
