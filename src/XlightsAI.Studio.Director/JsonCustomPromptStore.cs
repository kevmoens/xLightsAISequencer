using System.Text.Json;
using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Director;

/// <summary>
/// JSON-file-backed <see cref="ICustomPromptStore"/>. Mirrors the atomic temp-file +
/// <see cref="File.Move(string, string, bool)"/> pattern every other JSON store in this codebase
/// uses (<see cref="JsonRoleProviderConfigStore"/>, <c>JsonShowBibleStore</c>) -- one file per
/// directory/filename pair, never a partial write visible to a concurrent reader.
///
/// Reused for two distinct purposes (build-plan Phase 14 §2a/§2c.3), distinguished only by which
/// directory/filename the caller constructs this with: (1) the live, editable value a
/// <c>CustomPromptEditorViewModel</c> round-trips across sessions (one fixed filename under a
/// settings directory), and (2) the exact text actually used for one generation run, persisted
/// alongside that run's Show Bible/call log under its own project directory (filename keyed by
/// song id, so repeated runs never overwrite each other's record).
/// </summary>
public sealed class JsonCustomPromptStore : ICustomPromptStore
{
    private readonly string _directory;
    private readonly string _fileName;

    public JsonCustomPromptStore(string directory, string fileName = "custom-prompt.json")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        _directory = directory;
        _fileName = fileName;
    }

    public string GetPath() => Path.Combine(_directory, _fileName);

    public async Task<string> SaveAsync(CustomGenerationPrompt prompt, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(_directory);
        var finalPath = GetPath();
        var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";

        try
        {
            var json = JsonSerializer.Serialize(prompt);
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

    public async Task<CustomGenerationPrompt> LoadAsync(CancellationToken ct)
    {
        var path = GetPath();
        if (!File.Exists(path))
        {
            return CustomGenerationPrompt.Empty;
        }

        await using var stream = File.OpenRead(path);
        var prompt = await JsonSerializer.DeserializeAsync<CustomGenerationPrompt>(stream, cancellationToken: ct).ConfigureAwait(false);
        return prompt ?? CustomGenerationPrompt.Empty;
    }
}
