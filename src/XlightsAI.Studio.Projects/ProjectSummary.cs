using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Projects;

/// <summary>
/// Lightweight row for a "recent projects" list. PHASE-01 Section 3 proposes
/// <c>ProjectSummary(ProjectId Id, string Name, DateTimeOffset LastOpenedUtc)</c>; this adds
/// <see cref="RootDirectory"/> as a deviation (disclosed in the step evidence) because
/// <see cref="IProjectService.OpenAsync"/> is directory-addressed, not ID-addressed — without the
/// directory a recent-projects UI has no way to actually re-open the entry it is displaying.
/// </summary>
public sealed record ProjectSummary(
    ProjectId Id,
    string Name,
    DateTimeOffset LastOpenedUtc,
    string RootDirectory);
