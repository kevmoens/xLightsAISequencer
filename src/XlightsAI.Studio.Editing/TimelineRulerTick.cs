namespace XlightsAI.Studio.Editing;

/// <summary>One labeled ruler tick: its canonical tick value, its current pixel position, and its millisecond display label (PHASE-03 Section 2: "Internally represent position with... canonical monotonic clock... UI may show milliseconds").</summary>
public sealed record TimelineRulerTick(long Ticks, double X, string Label);
