namespace XlightsAI.Studio.Xlights;

/// <summary>
/// PHASE-1c (architecture §6, "timing tracks are first-class output, not metadata"): one labelled mark
/// in a <see cref="TimingTrack"/>, exactly the shape real xLights timing-track effects carry
/// (<c>&lt;Effect label="kick" startTime="0" endTime="418"&gt;&lt;/Effect&gt;</c>) — a half-open
/// <c>[StartTicks, EndTicks)</c> window in the same 100 ns <see cref="XlightsAI.Studio.Contracts.TimelineTick"/>
/// unit as every other IR time value, with no settings string of its own (unlike a model effect).
/// </summary>
public sealed record TimingMark(string Label, long StartTicks, long EndTicks);
