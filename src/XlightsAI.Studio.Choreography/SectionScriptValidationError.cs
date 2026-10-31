namespace XlightsAI.Studio.Choreography;

/// <summary>One violation of <see cref="SectionScriptValidator"/>'s own rules -- never thrown, always collected (same convention as <c>XlightsAI.Studio.Director.ShowBibleValidationError</c>).</summary>
public sealed record SectionScriptValidationError(string Rule, string Message);
