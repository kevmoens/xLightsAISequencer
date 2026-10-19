namespace XlightsAI.Studio.Shell;

/// <summary>
/// Shared loading/empty/error/loaded states for this step's new screens (Settings, Diagnostics),
/// following the same explicit-state pattern <see cref="ProjectsLoadState"/> established in
/// Step 004. Not retrofitted onto <see cref="ProjectsLoadState"/> itself, to avoid touching that
/// step's already-accepted contract.
/// </summary>
public enum ScreenLoadState
{
    Loading,
    Empty,
    Loaded,
    Error,
}
