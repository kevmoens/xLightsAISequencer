namespace XlightsAI.Studio.Choreography;

/// <summary>Thrown when <see cref="LookCompiler"/> cannot compile a <see cref="Look"/> against the supplied layout/vocabulary -- never a silently-dropped or invented result.</summary>
public sealed class ChoreographyCompilationException(string message) : Exception(message);
