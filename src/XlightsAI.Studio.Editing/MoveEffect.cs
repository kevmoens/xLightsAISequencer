using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Editing;

/// <summary>
/// PHASE-03 Step 019's own command: move a single effect's start time, preserving its duration
/// exactly ("prevent inadvertently changing effect duration" — Step 019 implementation guidance).
/// <see cref="NewStartTicks"/> is the caller's requested, pre-clamp start time; a caller that wants
/// snap-to-grid behavior ("Drag effect starts with optional snap") applies
/// <see cref="EditTimeSnapping.SnapTicks"/> itself before constructing this command — snapping is a
/// UI-gesture concern (Section 3 "Architectural design notes": "UI gestures calculate EditScope...
/// then create commands"), not a hidden side effect of applying one.
/// </summary>
public sealed record MoveEffect(
    DocumentRevision ExpectedRevision,
    EditScope Scope,
    Guid EffectId,
    long NewStartTicks) : EditCommand(ExpectedRevision, Scope);
