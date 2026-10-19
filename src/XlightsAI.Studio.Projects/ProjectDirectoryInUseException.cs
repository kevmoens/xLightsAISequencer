namespace XlightsAI.Studio.Projects;

/// <summary>
/// Thrown by <see cref="IProjectService.CreateAsync"/> when the target directory already contains
/// a <c>project.json</c> for a different project. Creating must never silently overwrite an
/// existing project's manifest (PHASE-01 Section 4's non-destructive-write rule, applied to the
/// project directory itself rather than an xLights show file).
/// </summary>
public sealed class ProjectDirectoryInUseException : Exception
{
    public ProjectDirectoryInUseException(string directory)
        : base($"Directory '{directory}' already contains a different project; refusing to overwrite it.")
    {
    }
}
