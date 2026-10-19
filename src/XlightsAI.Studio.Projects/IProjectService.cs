using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Projects;

/// <summary>Application-layer project lifecycle, matching PHASE-01 Section 3's proposed signature.</summary>
public interface IProjectService
{
    Task<ProjectManifest> CreateAsync(string directory, string name, CancellationToken ct);

    Task<ProjectManifest> OpenAsync(string directory, CancellationToken ct);

    Task SaveAsync(ProjectId id, CancellationToken ct);

    Task<IReadOnlyList<ProjectSummary>> RecentAsync(CancellationToken ct);
}
