namespace XlightsAI.Studio.Director;

/// <summary>One violation of <see cref="ShowBibleValidator"/>'s own rules -- never thrown, always collected (same "report every problem in one pass" convention as <c>Choreography.LegalityViolation</c>).</summary>
public sealed record ShowBibleValidationError(string Rule, string Message);
