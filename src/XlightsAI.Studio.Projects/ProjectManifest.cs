using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Projects;

/// <summary>
/// Stable project identity/version record, persisted verbatim as <c>project.json</c> in the
/// project's root directory. Matches PHASE-01 Section 3's proposed signature exactly: single
/// declaration, no phase-local duplicate. <see cref="RootDirectory"/> is the project's own
/// app-owned workspace directory (never an xLights show folder); nothing in this step ever writes
/// to a directory the user did not explicitly choose as a project workspace.
/// </summary>
public sealed record ProjectManifest(
    ProjectId Id,
    int SchemaVersion,
    string Name,
    string RootDirectory,
    DateTimeOffset CreatedUtc);
