using System.Text.Json;

namespace XlightsAI.Studio.Projects;

/// <summary>
/// JSON-file-backed <see cref="IRecentProjectsStore"/> (<c>recent-projects.json</c> in the app data
/// directory), atomic-write, same pattern as Step 005's <c>SettingsStore</c>. A missing or corrupt
/// file is treated as "no recents yet" rather than thrown — a damaged recents list must never block
/// create/open/save of an otherwise-healthy project.
/// </summary>
public sealed class RecentProjectsStore : IRecentProjectsStore
{
    private readonly string _filePath;

    public RecentProjectsStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _filePath = Path.Combine(directory, "recent-projects.json");
    }

    public IReadOnlyList<ProjectSummary> Load()
    {
        if (!File.Exists(_filePath))
        {
            return Array.Empty<ProjectSummary>();
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var entries = JsonSerializer.Deserialize<List<ProjectSummary>>(json);
            return (IReadOnlyList<ProjectSummary>?)entries ?? Array.Empty<ProjectSummary>();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return Array.Empty<ProjectSummary>();
        }
    }

    public void Save(IReadOnlyList<ProjectSummary> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Unique per call: two concurrent Save calls must never share a temp file.
        var tempPath = $"{_filePath}.tmp-{Guid.NewGuid():N}";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(entries));
        File.Move(tempPath, _filePath, overwrite: true);
    }
}
