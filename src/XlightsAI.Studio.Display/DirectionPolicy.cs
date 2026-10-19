namespace XlightsAI.Studio.Display;

/// <summary>
/// PHASE-09 Step 062 ("Define named custom paths and semantic spatial motions"): the named direction
/// policies <see cref="ISpatialPathSuggester.Suggest"/> accepts. Deliberately string constants, not an
/// enum, the same convention <see cref="SpatialEdge.Relation"/> already established ("not an enum so
/// later steps can add new kinds without a breaking contract change") -- a future step can add e.g.
/// <c>"Radial"</c> without touching this type.
/// </summary>
public static class DirectionPolicy
{
    /// <summary>Ascending normalized X -- Section 7 test scenario 1's own "Five arches auto-order left-to-right."</summary>
    public const string LeftToRight = "LeftToRight";

    /// <summary>Descending normalized X -- the mirror of <see cref="LeftToRight"/>, for e.g. a stage-right-origin route.</summary>
    public const string RightToLeft = "RightToLeft";

    /// <summary>Ascending normalized Y (screen-space "down," since Y grows downward in this project's own normalized coordinate convention -- the same orientation <see cref="DisplayGraphBuilder"/> already uses).</summary>
    public const string TopToBottom = "TopToBottom";

    /// <summary>Descending normalized Y, the mirror of <see cref="TopToBottom"/>.</summary>
    public const string BottomToTop = "BottomToTop";

    /// <summary>
    /// Leftmost, rightmost, second-leftmost, second-rightmost, ... working inward to any middle node --
    /// this step's own required acceptance text, verbatim: "outside-in... previews match ordered props."
    /// </summary>
    public const string OutsideIn = "OutsideIn";

    /// <summary>Every direction policy <see cref="ISpatialPathSuggester"/> currently recognizes -- used only to produce a clear rejection message; never consumed for branching logic elsewhere.</summary>
    public static readonly IReadOnlyList<string> All = [LeftToRight, RightToLeft, TopToBottom, BottomToTop, OutsideIn];
}
