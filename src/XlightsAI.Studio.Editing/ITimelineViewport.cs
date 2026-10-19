namespace XlightsAI.Studio.Editing;

/// <summary>
/// PHASE-03 Section 3 proposed contract: the shared, bidirectional time/pixel transform every
/// timeline-aware control (ruler, rows, effect blocks, hit testing) maps through, so there is
/// exactly one place that answers "what tick is under this pixel" and "what pixel is this tick
/// at" for a given scroll/zoom state.
/// </summary>
public interface ITimelineViewport
{
    long TimeFromX(double pixel);

    double XFromTime(long ticks);
}
