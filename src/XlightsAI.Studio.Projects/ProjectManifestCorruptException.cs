namespace XlightsAI.Studio.Projects;

/// <summary>
/// Thrown when a project directory's <c>project.json</c> exists but is not well-formed JSON (disk
/// corruption, an external non-atomic edit, a crash during a write performed by something other
/// than <see cref="JsonProjectStore.WriteManifestAtomicallyAsync"/>). Mirrors the typed-exception
/// pattern already used for every other project-layer failure
/// (<see cref="ProjectSchemaNotSupportedException"/>, <see cref="ProjectDirectoryInUseException"/>,
/// <see cref="ProjectStateStaleException"/>) instead of letting a raw
/// <see cref="System.Text.Json.JsonException"/> reach the UI layer.
/// </summary>
public sealed class ProjectManifestCorruptException : Exception
{
    public string ManifestPath { get; }

    public ProjectManifestCorruptException(string manifestPath, Exception inner)
        : base($"Project manifest at '{manifestPath}' is not well-formed JSON and could not be read.", inner)
    {
        ManifestPath = manifestPath;
    }
}
