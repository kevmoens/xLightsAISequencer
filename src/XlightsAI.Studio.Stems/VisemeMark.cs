namespace XlightsAI.Studio.Stems;

/// <summary>One approximated viseme/phoneme-code window from <see cref="VisemeApproximator"/>.</summary>
/// <param name="Code">One of xLights' own Faces viseme codes (<c>AI</c>, <c>E</c>, <c>FV</c>, <c>L</c>, <c>MBP</c>, <c>O</c>, <c>U</c>, <c>WQ</c>) or <see cref="VisemeApproximator.RestCode"/> for a non-singing window.</param>
/// <param name="StartTicks">Half-open window start, in canonical 100&#8201;ns ticks from song origin.</param>
/// <param name="EndTicks">Half-open window end.</param>
public sealed record VisemeMark(string Code, long StartTicks, long EndTicks);
