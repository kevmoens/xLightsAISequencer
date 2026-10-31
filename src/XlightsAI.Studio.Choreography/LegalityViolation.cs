namespace XlightsAI.Studio.Choreography;

/// <summary>One violation of a build-plan Phase 4 task 4 ("compositional legality") rule -- never thrown, always collected, so a caller sees every problem in one pass rather than fixing them one exception at a time.</summary>
public sealed record LegalityViolation(string Rule, string Message);
