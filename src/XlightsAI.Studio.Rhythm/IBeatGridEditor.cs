namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// Step 035's own manual-correction boundary over an already-built <see cref="BeatGrid"/> (Step 034).
/// A narrower, concrete precursor to Section 3's proposed <c>IBeatGridEditor</c>: pure, synchronous,
/// in-memory domain operations over a <see cref="BeatGrid"/> value already in hand, not an I/O-bound
/// service that loads/saves by id itself — the same deliberate narrowing precedent Steps 033/034 set
/// relative to their own Section 3 proposals (see <see cref="ITempoDetector"/>/<see cref="IBeatGridBuilder"/>'s
/// own evidence). Persistence of an edited result is the caller's explicit responsibility via the
/// already-existing <see cref="IBeatGridStore.SaveAsync"/> — Section 2's "explicit human acceptance"
/// is a command-boundary concern above this editor, not something this editor performs for the caller.
/// <para>
/// Section 3's proposed <c>expectedVersion: long</c> optimistic-concurrency parameter is mapped onto
/// <see cref="BeatGrid.CorrectionVersion"/> — the one <see langword="long"/>-typed version field
/// <see cref="BeatGrid"/> actually carries (<see cref="BeatGrid.VersionId"/> is a <see cref="Guid"/>,
/// and changes on <i>every</i> rebuild, not only on a user correction — it cannot distinguish "nothing
/// changed since you loaded this" from "an identical-looking rebuild happened," so it cannot serve as
/// a correction-specific optimistic check). Every method here increments <see cref="BeatGrid.CorrectionVersion"/>
/// by exactly one and assigns a fresh <see cref="BeatGrid.VersionId"/> — the "changes invalidate
/// downstream generated gestures" signal (Section 3) a future Step 036 consumer keys off of — while
/// leaving <see cref="BeatGrid.SourceHash"/>, <see cref="BeatGrid.SourceOnsetAnalysisVersionId"/> and
/// <see cref="BeatGrid.SourceTempoAnalysisVersionId"/> completely unchanged, since no re-analysis
/// occurred: only an explicit, authorized human edit did.
/// </para>
/// </summary>
public interface IBeatGridEditor
{
    /// <summary>
    /// Moves (drags) the beat identified by <paramref name="beatId"/> to <paramref name="newTimeTicks"/>,
    /// re-sorts and renumbers every beat's <see cref="BeatMarker.BarIndex"/>/<see cref="BeatMarker.BeatWithinBar"/>
    /// by the new time order, and marks only the moved beat <see cref="BeatMarker.UserCorrected"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="grid"/> is <see langword="null"/>.</exception>
    /// <exception cref="BeatGridEditInvalidException"><paramref name="beatId"/> is not in <paramref name="grid"/>, or <paramref name="newTimeTicks"/> is negative.</exception>
    /// <exception cref="BeatGridEditConflictException"><paramref name="expectedCorrectionVersion"/> does not match <paramref name="grid"/>'s current <see cref="BeatGrid.CorrectionVersion"/>.</exception>
    BeatGrid MoveBeat(BeatGrid grid, Guid beatId, long newTimeTicks, long expectedCorrectionVersion);

    /// <summary>
    /// Reassigns which beat is a bar's downbeat: realigns bar boundaries so <paramref name="beatId"/>
    /// becomes <c>BeatWithinBar == 0</c> of its own bar (<c>BarIndex == 0</c>), and every other beat's
    /// bar numbering shifts relative to it (negative <see cref="BeatMarker.BarIndex"/> values for beats
    /// before the new downbeat are expected and valid — bars are counted relative to the chosen
    /// downbeat, not the grid's first beat). Marks only <paramref name="beatId"/>'s own beat
    /// <see cref="BeatMarker.UserCorrected"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="grid"/> is <see langword="null"/>.</exception>
    /// <exception cref="BeatGridEditInvalidException"><paramref name="beatId"/> is not in <paramref name="grid"/>.</exception>
    /// <exception cref="BeatGridEditConflictException"><paramref name="expectedCorrectionVersion"/> does not match <paramref name="grid"/>'s current <see cref="BeatGrid.CorrectionVersion"/>.</exception>
    BeatGrid SetDownbeat(BeatGrid grid, Guid beatId, long expectedCorrectionVersion);

    /// <summary>
    /// Inserts a new, fully-confident (<see cref="BeatMarker.Confidence"/> == 1.0), user-anchored beat
    /// at <paramref name="timeTicks"/> (Step 035's own implementation guidance, verbatim: "anchored
    /// beat insertion") and renumbers every beat's bar/beat-within-bar position-based from the grid's
    /// own earliest beat, matching <see cref="BeatGridBuilder"/>'s own numbering convention.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="grid"/> is <see langword="null"/>.</exception>
    /// <exception cref="BeatGridEditInvalidException"><paramref name="timeTicks"/> is negative, or a beat already exists at exactly <paramref name="timeTicks"/>.</exception>
    /// <exception cref="BeatGridEditConflictException"><paramref name="expectedCorrectionVersion"/> does not match <paramref name="grid"/>'s current <see cref="BeatGrid.CorrectionVersion"/>.</exception>
    BeatGrid InsertBeat(BeatGrid grid, long timeTicks, long expectedCorrectionVersion);

    /// <summary>
    /// Removes the beat identified by <paramref name="beatId"/> (Step 035's own implementation
    /// guidance, verbatim: "anchored beat ... removal") and renumbers the remaining beats
    /// position-based from the grid's own earliest beat. An empty resulting <see cref="BeatGrid.Beats"/>
    /// is valid, not an error (this step's own "silence... must be handled explicitly" edge case).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="grid"/> is <see langword="null"/>.</exception>
    /// <exception cref="BeatGridEditInvalidException"><paramref name="beatId"/> is not in <paramref name="grid"/>.</exception>
    /// <exception cref="BeatGridEditConflictException"><paramref name="expectedCorrectionVersion"/> does not match <paramref name="grid"/>'s current <see cref="BeatGrid.CorrectionVersion"/>.</exception>
    BeatGrid RemoveBeat(BeatGrid grid, Guid beatId, long expectedCorrectionVersion);
}
