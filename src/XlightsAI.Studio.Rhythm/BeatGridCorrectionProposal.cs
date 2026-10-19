namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// One candidate correction <see cref="IBeatGridAutoCorrectionLoop.TryApply"/> may attempt — this
/// step's own "AI trust"/"validated structured proposal" boundary (Section 2): a proposal is untrusted
/// data describing <i>intent</i>, never executed directly; it is only ever realized by calling the
/// real, already-tested <see cref="IBeatGridEditor"/>.
/// </summary>
/// <param name="Kind">Which existing, already-tested <see cref="IBeatGridEditor"/> operation this proposal requests.</param>
/// <param name="BeatId">The beat this proposal targets — not validated to actually exist in the grid until <see cref="IBeatGridAutoCorrectionLoop.TryApply"/> attempts it (an unknown id is this step's own "malformed proposal" failure/edge case).</param>
/// <param name="NewTimeTicks">Required when <see cref="Kind"/> is <see cref="BeatGridCorrectionProposalKind.MoveBeat"/>; ignored for <see cref="BeatGridCorrectionProposalKind.SetDownbeat"/>.</param>
public sealed record BeatGridCorrectionProposal(BeatGridCorrectionProposalKind Kind, Guid BeatId, long? NewTimeTicks = null);
