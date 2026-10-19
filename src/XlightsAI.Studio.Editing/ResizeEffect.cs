using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Editing;

/// <summary>
/// PHASE-03 Step 020's own command: resize a single effect's duration by changing its end time,
/// leaving <see cref="XlightsAI.Studio.Xlights.EffectInstance.StartTicks"/> untouched (Step 020
/// implementation guidance: "Resize independently of playback/frame snap" — no implicit snap is
/// applied here, matching <see cref="MoveEffect"/>'s own "snapping is a UI-gesture concern" rule;
/// a caller that wants snap-to-grid resize applies <see cref="EditTimeSnapping.SnapTicks"/> itself
/// before constructing this command). <see cref="NewEndTicks"/> is the caller's requested, pre-clamp
/// end time.
/// </summary>
public sealed record ResizeEffect(
    DocumentRevision ExpectedRevision,
    EditScope Scope,
    Guid EffectId,
    long NewEndTicks) : EditCommand(ExpectedRevision, Scope);
