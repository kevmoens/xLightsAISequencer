namespace XlightsAI.Studio.Shell;

/// <summary>
/// A user-readable, non-silent description of why the shell could not reach the Ready state.
/// </summary>
public sealed record StartupFailure(string Code, string Message);
