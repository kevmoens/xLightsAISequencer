namespace XlightsAI.Studio.Choreography;

/// <summary>One bar's half-open tick window, addressed by its typed <see cref="Index"/> rather than a raw int.</summary>
public sealed record BarWindow(BarIndex Index, TimeWindow Window);
