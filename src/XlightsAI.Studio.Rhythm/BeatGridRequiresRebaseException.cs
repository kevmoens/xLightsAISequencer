using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// Thrown by <see cref="IBeatGridBuilder.BuildAsync"/> when a newer <see cref="OnsetAnalysis"/>/
/// <see cref="TempoAnalysis"/> pair would otherwise invalidate and silently overwrite a persisted
/// <see cref="BeatGrid"/> that already carries real user corrections
/// (<see cref="BeatGrid.CorrectionVersion"/> &gt; 0). Step 035's own "dependency invalidation" scope,
/// directly implementing Section 3's data-contract rule ("changes invalidate downstream generated
/// gestures but <i>not</i> manually accepted effects") and the phase-wide quality-gate matrix row
/// "Algorithm cache invalidation does not delete user-approved grid." An algorithmic re-analysis is
/// never, by itself, an authorized reason to discard a human's accepted corrections — a caller that
/// actually intends to discard them must do so explicitly (e.g. delete the cache file first), which
/// this exception forces to be a deliberate decision rather than a silent side effect.
/// </summary>
public sealed class BeatGridRequiresRebaseException : Exception
{
    public BeatGridRequiresRebaseException(SongId songId, Guid existingGridVersionId, long existingCorrectionVersion)
        : base($"Beat grid for song '{songId.Value}' (version {existingGridVersionId}, correction version {existingCorrectionVersion}) already carries user corrections; a newer onset/tempo analysis cannot silently overwrite it. Resolve explicitly (keep the corrected grid, or explicitly rebuild/discard it) before proceeding.")
    {
        SongId = songId;
        ExistingGridVersionId = existingGridVersionId;
        ExistingCorrectionVersion = existingCorrectionVersion;
    }

    public SongId SongId { get; }

    public Guid ExistingGridVersionId { get; }

    public long ExistingCorrectionVersion { get; }
}
