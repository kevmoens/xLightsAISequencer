namespace XlightsAI.Studio.Shell;

/// <summary>
/// Startup lifecycle of the application shell. The full project-workspace state machine
/// (NeedsWorkspace/Migrating/IncompatibleVersion) described in PHASE-01 §3 belongs to the
/// project create/open/migrate slice (Steps 006/007) and is intentionally not modeled here;
/// this step owns only the narrower "did the shell itself start up" state.
/// </summary>
public enum StartupState
{
    Starting,
    Ready,
    Failed,
}
