namespace XlightsAI.Studio.Refinement;

/// <summary>
/// The result of a successful <see cref="IRefinementRunStore.TryClaimAsync"/>: the opaque
/// <see cref="Token"/> the caller must present to every subsequent mutating call
/// (<see cref="IRefinementRunStore.SaveRunStateAsync"/>, <see cref="IRefinementRunStore.SaveAttemptAsync"/>,
/// <see cref="IRefinementRunStore.HeartbeatAsync"/>, <see cref="IRefinementRunStore.ReleaseClaimAsync"/>)
/// while this claim is held, plus the run's own state as of the moment the claim was granted.
/// </summary>
public sealed record RefinementClaim(Guid Token, LoopRun Run);
