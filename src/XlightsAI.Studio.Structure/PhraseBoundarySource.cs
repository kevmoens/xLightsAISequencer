namespace XlightsAI.Studio.Structure;

/// <summary>
/// PHASE-08 Step 055's own declared evidence source for one <see cref="PhraseCandidate"/>'s start
/// boundary -- "Segment into phrases with bar-based hypothesis and melodic cadences" (Step 055's own
/// implementation guidance, verbatim) names exactly two automatic sources; <see cref="UserChosen"/> is
/// this step's own third source, satisfying the step's own title: "...with manual corrections."
/// </summary>
public enum PhraseBoundarySource
{
    /// <summary>The boundary is a plain bar-count split (every <c>TargetPhraseLengthBars</c> bars from the section start), with no corroborating melodic evidence nearby -- or the section's own already-approved start/end boundary, inherited as-is.</summary>
    BarHypothesis,

    /// <summary>The bar-hypothesis boundary was snapped to a nearby melodic cadence (a melody rest at least <see cref="PhraseSegmentationSettings.MinCadenceGapTicks"/> long) found within <see cref="PhraseSegmentationSettings.CadenceSearchRadiusBars"/> bars.</summary>
    MelodicCadence,

    /// <summary>A human explicitly chose this boundary via <see cref="IPhraseSegmenter.SetManualBoundariesAsync"/>, superseding whatever the automatic recipe would have produced for this section.</summary>
    UserChosen,
}
