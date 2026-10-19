using System.Text.Json;

namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// JSON-file-backed <see cref="ISelectionContextStore"/>. Writes are atomic (same-directory temp file +
/// <see cref="File.Move(string, string, bool)"/> replace) -- the identical pattern
/// <see cref="XlightsAI.Studio.Composition.JsonMusicalRoleAssignmentStore"/> already uses. Serializes
/// through an explicit <see cref="JsonSerializerOptions"/> carrying <see cref="ReadOnlyModelIdSetJsonConverter"/>/
/// <see cref="ReadOnlyGuidSetJsonConverter"/> rather than a property-level <c>[JsonConverter]</c>
/// attribute, because the already-accepted <c>EditScope</c> this type nests cannot itself be modified
/// (PHASE-03's own immutable contract) to carry such an attribute.
/// </summary>
public sealed class JsonSelectionContextStore : ISelectionContextStore
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    private readonly string _directory;

    public JsonSelectionContextStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string GetPath(Guid id) => Path.Combine(_directory, $"selection-context-{id:N}.json");

    public async Task<string> SaveAsync(SelectionContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(_directory);
        var finalPath = GetPath(context.Id);
        var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";

        try
        {
            var json = JsonSerializer.Serialize(context, Options);
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

    public async Task<SelectionContext> LoadAsync(string path, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No selection context exists at '{path}'.", path);
        }

        await using var stream = File.OpenRead(path);
        try
        {
            var context = await JsonSerializer.DeserializeAsync<SelectionContext>(stream, Options, ct).ConfigureAwait(false);
            return context ?? throw new SelectionContextCorruptException(path, new InvalidOperationException("Deserialized to null."));
        }
        catch (JsonException ex)
        {
            throw new SelectionContextCorruptException(path, ex);
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new ReadOnlyModelIdSetJsonConverter());
        options.Converters.Add(new ReadOnlyGuidSetJsonConverter());
        return options;
    }
}
