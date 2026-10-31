namespace XlightsAI.Studio.Xlights;

/// <summary>PHASE-1c: one re-parsed <c>&lt;Effect label=".." startTime=".." endTime=".."/&gt;</c> mark inside a structurally-read timing track.</summary>
public sealed record XsqStructuralTimingMark(string Label, long StartTimeMs, long EndTimeMs);
