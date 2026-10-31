namespace XlightsAI.Studio.Xlights;

/// <summary>
/// PHASE-1a (docs/AI-CHOREOGRAPHY-BUILD-PLAN.md task 3, "Capture full 2D/3D position and bounding
/// geometry, not just a single <see cref="Point2"/>"): the model's own real-world transform exactly as
/// xLights itself stores it — world position, scale, rotation (the attributes the 3D preview actually
/// renders from), plus the second endpoint (<c>X2/Y2/Z2</c>) that line-shaped models (Single Line,
/// gutter runs) carry instead of a scale. Every field mirrors a real <c>&lt;model&gt;</c> attribute
/// 1:1 — nothing here is inferred or computed from screen pixels (PHASE-02 Section 3: "Avoid guessing
/// total pixel counts or channel mappings from screen coordinates").
/// </summary>
public sealed record DisplayBounds(
    double X,
    double Y,
    double Z,
    double? ScaleX = null,
    double? ScaleY = null,
    double? ScaleZ = null,
    double? RotateX = null,
    double? RotateY = null,
    double? RotateZ = null,
    double? EndX = null,
    double? EndY = null,
    double? EndZ = null);
