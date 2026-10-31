namespace XlightsAI.Studio.Xlights;

/// <summary>
/// PHASE-1a (docs/AI-CHOREOGRAPHY-BUILD-PLAN.md tasks 2 and 5): normalizes a model's raw
/// <c>DisplayAs</c> string into a <see cref="ModelCategory"/>, and computes its verified node/pixel
/// count from <c>parm1</c>/<c>parm2</c> (or <c>WindowRows</c>/<c>WindowColumns</c> for the Window Frame
/// family) — the mechanism architecture §12 blocker 1 calls "the single most important correctness
/// mechanism in the whole design". The formula below was cross-checked directly against the real
/// <c>C:\xLights</c> show's own <c>xlights_rgbeffects.xml</c>: Single Line (parm1=1, parm2=100 -&gt;
/// 100 nodes), Star (parm1=1, parm2=20 -&gt; 20 nodes), Tree 250 (parm1=1, parm2=98 -&gt; 98 nodes),
/// Arches (parm1=2 arches, parm2=100 nodes/arch -&gt; 200 nodes), Vert Matrix (parm1=9 strings,
/// parm2=100 pixels/string -&gt; 900 nodes, independently confirmed by that model's own 9 <c>StringN</c>
/// connection attributes) all reduce to the same <c>parm1 * parm2</c> formula — <c>parm3</c> (strands
/// per string) subdivides a string's own pixels for addressing/wiring purposes but never changes the
/// model's total node count.
/// </summary>
internal static class ModelGeometryCalculator
{
    public static ModelCategory NormalizeCategory(string displayAs)
    {
        if (string.IsNullOrWhiteSpace(displayAs))
        {
            return ModelCategory.Other;
        }

        if (displayAs.Equals("Single Line", StringComparison.OrdinalIgnoreCase))
        {
            return ModelCategory.SingleLine;
        }

        if (displayAs.StartsWith("Arches", StringComparison.OrdinalIgnoreCase))
        {
            return ModelCategory.Arches;
        }

        if (displayAs.Contains("Matrix", StringComparison.OrdinalIgnoreCase))
        {
            return ModelCategory.Matrix;
        }

        if (displayAs.StartsWith("Tree", StringComparison.OrdinalIgnoreCase))
        {
            return ModelCategory.Tree;
        }

        if (displayAs.Equals("Star", StringComparison.OrdinalIgnoreCase))
        {
            return ModelCategory.Star;
        }

        if (displayAs.Contains("Window Frame", StringComparison.OrdinalIgnoreCase))
        {
            return ModelCategory.WindowFrame;
        }

        if (displayAs.Contains("Custom", StringComparison.OrdinalIgnoreCase))
        {
            return ModelCategory.Custom;
        }

        return ModelCategory.Other;
    }

    /// <summary>
    /// <paramref name="parm1"/>/<paramref name="parm2"/> is the verified formula for every family this
    /// has been cross-checked against; Window Frame instead counts by
    /// <paramref name="windowRows"/> * <paramref name="windowColumns"/> (xLights' own per-row/column
    /// node layout for that family) when both are present, falling back to <c>parm1 * parm2</c>
    /// otherwise (e.g. an older Window Frame model saved before those attributes existed).
    /// </summary>
    public static int ComputeNodeCount(ModelCategory category, int parm1, int parm2, int? windowRows, int? windowColumns)
    {
        if (category == ModelCategory.WindowFrame && windowRows is > 0 && windowColumns is > 0)
        {
            return windowRows.Value * windowColumns.Value;
        }

        return Math.Max(0, parm1) * Math.Max(0, parm2);
    }
}
