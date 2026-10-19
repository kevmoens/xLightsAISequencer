namespace XlightsAI.Studio.Projects;

/// <summary>
/// Thrown by <see cref="IProjectService.SaveAsync"/> when the manifest currently on disk no longer
/// matches the project identity that was opened (e.g. the directory was repointed at a different
/// project, or the file was deleted/replaced out from under the running process). Saving in this
/// state would silently corrupt whatever is actually on disk; this step refuses the write instead
/// of blindly overwriting stale/foreign persisted state (PHASE-01 Section 6 Step 006 failure case:
/// "stale persisted state must not corrupt the project").
/// </summary>
public sealed class ProjectStateStaleException : Exception
{
    public ProjectStateStaleException(string message)
        : base(message)
    {
    }
}
