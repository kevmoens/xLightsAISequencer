namespace XlightsAI.Studio.Shell;

/// <summary>
/// Explicit load states for the Projects screen, so "nothing to show" (Empty) is never
/// rendered the same as "still working" (Loading) or "something went wrong" (Error) — a blank,
/// unexplained screen is never an acceptable observable state.
/// </summary>
public enum ProjectsLoadState
{
    Loading,
    Empty,
    Loaded,
    Error,
}
