namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// Which existing, already-tested <see cref="IBeatGridEditor"/> operation a
/// <see cref="BeatGridCorrectionProposal"/> requests — Step 037's own bounded auto-correction loop
/// (<see cref="IBeatGridAutoCorrectionLoop"/>) only ever drives already-existing Step 035 edits; it
/// never invents a new kind of edit of its own.
/// </summary>
public enum BeatGridCorrectionProposalKind
{
    /// <summary><see cref="IBeatGridEditor.MoveBeat"/> — moving a beat to <see cref="BeatGridCorrectionProposal.NewTimeTicks"/> (which may equal its own current position, a "confirm this beat" proposal).</summary>
    MoveBeat,

    /// <summary><see cref="IBeatGridEditor.SetDownbeat"/>.</summary>
    SetDownbeat,
}
