using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Display;

/// <summary>
/// PHASE-09 Step 061 ("Create graphical layout/path editor with position correction"): one explicit,
/// user-authored correction to a single <see cref="DisplayNode"/>'s own normalized preview position --
/// Section 4's own "Store user-authored paths, display import hash and normalized coordinate overrides
/// separately." Never mutates <see cref="DisplayNode.X"/>/<see cref="DisplayNode.Y"/> in place (those
/// stay exactly whatever <see cref="IDisplayGraphBuilder.Build"/> itself derived from the imported
/// layout); <see cref="DisplayLayoutProjection.Apply"/> re-applies every override on top of a freshly
/// rebuilt graph instead, so re-importing the layout never silently discards a prior correction.
/// </summary>
public sealed record NodePositionOverride(ModelId NodeId, double X, double Y);
