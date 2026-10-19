using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// Thrown when an <see cref="IBeatGridEditor"/> operation's <c>expectedCorrectionVersion</c> argument
/// does not match the supplied <see cref="BeatGrid.CorrectionVersion"/> — the optimistic-concurrency
/// check named by Section 2's "Keep optimistic revision checks" and Section 3's proposed
/// <c>IBeatGridEditor</c> <c>expectedVersion</c> parameter (mapped onto <see cref="BeatGrid.CorrectionVersion"/>,
/// the one <see langword="long"/>-typed version field <see cref="BeatGrid"/> actually has — see
/// <see cref="BeatGridEditor"/>'s own remarks). Means some other edit (or a rebuild) already moved the
/// grid out from under the caller; the caller must reload and retry rather than silently apply a
/// stale edit on top of newer state.
/// </summary>
public sealed class BeatGridEditConflictException : Exception
{
    public BeatGridEditConflictException(SongId songId, long expectedCorrectionVersion, long actualCorrectionVersion)
        : base($"Cannot edit the beat grid for song '{songId.Value}': expected correction version {expectedCorrectionVersion} but the grid is now at {actualCorrectionVersion}.")
    {
        SongId = songId;
        ExpectedCorrectionVersion = expectedCorrectionVersion;
        ActualCorrectionVersion = actualCorrectionVersion;
    }

    public SongId SongId { get; }

    public long ExpectedCorrectionVersion { get; }

    public long ActualCorrectionVersion { get; }
}
