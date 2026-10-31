namespace XlightsAI.Studio.Xlights;

/// <summary>
/// PHASE-1a (docs/AI-CHOREOGRAPHY-BUILD-PLAN.md): a normalized classification of a model's raw
/// xLights <c>DisplayAs</c> string, replacing the previously-opaque <see cref="DisplayModel.Kind"/>
/// string as the thing resolution-aware effect legality (architecture §7 "Compositional legality")
/// actually switches on. <see cref="DisplayModel.Kind"/> is kept verbatim alongside this for
/// display/round-trip purposes; this is the normalized value code should branch on.
/// </summary>
public enum ModelCategory
{
    /// <summary>No recognized family matched the raw <c>DisplayAs</c> string.</summary>
    Other,

    /// <summary>xLights "Single Line" — one string, high positional resolution along its one axis.</summary>
    SingleLine,

    /// <summary>xLights "Arches" family — low-resolution, large-sweep candelabra/arch props.</summary>
    Arches,

    /// <summary>Any "Matrix" family (Vert Matrix, Horiz Matrix, Matrix) — high-resolution 2D grid, pixel-heavy.</summary>
    Matrix,

    /// <summary>Any "Tree N" family (e.g. "Tree 250") — radial/conical strand layout.</summary>
    Tree,

    /// <summary>xLights "Star" — a small radial prop, typically low pixel count.</summary>
    Star,

    /// <summary>xLights "Window Frame" — a rectangular frame counted by <c>WindowRows</c>/<c>WindowColumns</c> rather than <c>parm1</c>/<c>parm2</c>.</summary>
    WindowFrame,

    /// <summary>Any "Custom"-family model with a user-authored node layout grid.</summary>
    Custom,
}
