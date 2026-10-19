using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Projects;

/// <summary>
/// No-op <see cref="IProjectService"/>, the same compatibility pattern Step 005 used for
/// <c>NullSettingsStore</c>/<c>NullAppDiagnostics</c>: lets <c>ShellViewModel</c> gain an optional
/// project-service collaborator without breaking Step 004/005's existing constructor call sites or
/// tests. Real composition (the App project) always supplies the real <see cref="ProjectService"/>.
/// </summary>
public sealed class NullProjectService : IProjectService
{
    public static readonly NullProjectService Instance = new();

    private NullProjectService()
    {
    }

    public Task<ProjectManifest> CreateAsync(string directory, string name, CancellationToken ct) =>
        throw new NotSupportedException("No project service is configured.");

    public Task<ProjectManifest> OpenAsync(string directory, CancellationToken ct) =>
        throw new NotSupportedException("No project service is configured.");

    public Task SaveAsync(ProjectId id, CancellationToken ct) =>
        throw new NotSupportedException("No project service is configured.");

    public Task<IReadOnlyList<ProjectSummary>> RecentAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ProjectSummary>>(Array.Empty<ProjectSummary>());
}
