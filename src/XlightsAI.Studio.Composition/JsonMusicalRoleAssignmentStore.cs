using System.Text.Json;

namespace XlightsAI.Studio.Composition;

/// <summary>
/// JSON-file-backed <see cref="IMusicalRoleAssignmentStore"/>. Writes are atomic (same-directory temp file
/// + <see cref="File.Move(string, string, bool)"/> replace), the exact same pattern
/// <see cref="XlightsAI.Studio.Display.JsonRoleAssignmentStore"/> already uses.
/// </summary>
public sealed class JsonMusicalRoleAssignmentStore : IMusicalRoleAssignmentStore
{
    private readonly string _directory;

    public JsonMusicalRoleAssignmentStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string GetPath(Guid phraseId) => Path.Combine(_directory, $"musical-role-assignments-{phraseId:N}.json");

    public async Task<string> SaveAsync(MusicalRoleAssignmentSet assignments, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(assignments);

        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(_directory);
        var finalPath = GetPath(assignments.PhraseId);
        var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";

        try
        {
            var json = JsonSerializer.Serialize(assignments);
            await File.WriteAllTextAsync(tempPath, json, ct).ConfigureAwait(false);

            ct.ThrowIfCancellationRequested();

            File.Move(tempPath, finalPath, overwrite: true);
            return finalPath;
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            throw;
        }
    }

    public async Task<MusicalRoleAssignmentSet> LoadAsync(string path, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No musical role assignment set exists at '{path}'.", path);
        }

        await using var stream = File.OpenRead(path);
        try
        {
            var assignments = await JsonSerializer.DeserializeAsync<MusicalRoleAssignmentSet>(stream, cancellationToken: ct).ConfigureAwait(false);
            return assignments ?? throw new CompositionCorruptException(path, new InvalidOperationException("Deserialized to null."));
        }
        catch (JsonException ex)
        {
            throw new CompositionCorruptException(path, ex);
        }
    }
}
