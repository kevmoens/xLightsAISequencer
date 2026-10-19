namespace XlightsAI.Studio.Projects;

/// <summary>
/// App-level (not per-project) persistence for the "recent projects" list surfaced by
/// <see cref="IProjectService.RecentAsync"/>. Lives in the app data directory alongside Step 005's
/// <c>settings.json</c>/<c>diagnostics.log</c>, not inside any single project's own folder.
/// </summary>
public interface IRecentProjectsStore
{
    IReadOnlyList<ProjectSummary> Load();

    void Save(IReadOnlyList<ProjectSummary> entries);
}
