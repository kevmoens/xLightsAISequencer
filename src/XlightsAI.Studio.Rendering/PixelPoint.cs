using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Rendering;

/// <summary>
/// PHASE-13 Section 3 proposed contract, verbatim: one illuminated point on <see cref="Display.SpatialDisplayGraph"/>
/// geometry at a single <see cref="PreviewFrame.TimeTicks"/> instant. <see cref="X"/>/<see cref="Y"/> are the same
/// normalized <c>[0, 1]</c> preview coordinates <see cref="Display.DisplayNode.X"/>/<see cref="Display.DisplayNode.Y"/>
/// already use -- never pixel/device space, despite the type's name (the spec's own chosen name, kept verbatim for
/// cross-phase signature stability).
/// </summary>
public sealed record PixelPoint(ModelId Model, double X, double Y, byte R, byte G, byte B);
