using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Editing;

/// <summary>
/// PHASE-03 Step 022's own command: the "lock semantics for approved effects" half of this step's
/// title. Sets <see cref="XlightsAI.Studio.Xlights.EffectInstance.Locked"/> to <see cref="Locked"/>
/// on a single effect. Deliberately bypasses <see cref="IEffectLockPolicy.AssertEditable"/>'s own
/// lock check (see <see cref="EditTransactionService"/>'s handling of this command) — the lock
/// toggle itself is the trusted, explicit "approve"/"un-approve" action the phase spec names
/// ("lock Arch 2"), not a content edit subject to the very lock bit it is setting; an already-locked
/// effect must still be unlockable, and a freshly-approved effect must be lockable in one call. Stale
/// revision and out-of-scope-model checks still apply — this command's only special treatment is the
/// lock check itself.
/// </summary>
public sealed record SetEffectLocked(
    DocumentRevision ExpectedRevision,
    EditScope Scope,
    Guid EffectId,
    bool Locked) : EditCommand(ExpectedRevision, Scope);
