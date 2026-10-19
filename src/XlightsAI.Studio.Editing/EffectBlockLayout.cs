using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Editing;

/// <summary>
/// PHASE-03 Step 018 ("Render effect blocks and inspector selection"): one effect rendered as a
/// pixel-space block on a single model's timeline row, through the same shared
/// <see cref="ITimelineViewport"/> transform every other timeline-aware control maps through
/// (Section 3: "hit testing maps horizontal pixels through the common time transform").
/// <see cref="EffectId"/> is the only stable identity a caller may key selection off of (never a
/// list index or on-screen position — this step's own required acceptance: "Selection maps exactly
/// to effect ID and time span").
/// </summary>
public sealed record EffectBlockLayout(
    Guid EffectId,
    ModelId Target,
    int Layer,
    long StartTicks,
    long EndTicks,
    string RecipeId,
    bool Locked,
    double X,
    double Width);

/// <summary>One layer's worth of <see cref="EffectBlockLayout"/>s for a single model row, ordered by start time ascending.</summary>
public sealed record EffectLayerTrack(int Layer, IReadOnlyList<EffectBlockLayout> Blocks);
