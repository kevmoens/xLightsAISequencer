namespace XlightsAI.Studio.Xlights;

/// <summary>
/// PHASE-02 Section 3 proposed contract: a normalized 2D preview-geometry point. Values come
/// directly from an xLights model's own world-position attributes when present; this type performs
/// no unit conversion or pixel/channel interpretation (Section 3: "Avoid guessing total pixel
/// counts or channel mappings from screen coordinates.").
/// </summary>
public sealed record Point2(double X, double Y);
