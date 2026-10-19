using System.Text.Json;

namespace XlightsAI.Studio.Xlights;

/// <summary>
/// JSON-file-backed <see cref="ISequenceDocumentStore"/>. Writes are atomic (same-directory temp
/// file + <see cref="File.Move(string, string, bool)"/> replace), the same pattern
/// <c>JsonProjectStore</c>/<c>SettingsStore</c> already use for every other app-owned manifest in
/// this repository. Each document is written to its own file named by its own <see cref="SequenceDocument.Id"/>
/// — concurrent plans for different documents never share a temp/final path, and this store never
/// touches anything under an xLights show directory (Section 3: "never overwrite source show files").
/// </summary>
public sealed class JsonSequenceDocumentStore : ISequenceDocumentStore
{
    private readonly string _directory;

    public JsonSequenceDocumentStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public async Task<string> SaveAsync(SequenceDocument document, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(document);

        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(_directory);
        var finalPath = Path.Combine(_directory, $"sequence-plan-{document.Id:N}.json");
        var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";

        try
        {
            var json = JsonSerializer.Serialize(document);
            await File.WriteAllTextAsync(tempPath, json, ct).ConfigureAwait(false);

            // Re-checked after the (cancelable) write completes, before the file is promoted into
            // place under its real name — same "never promote a half-written file" rule
            // JsonProjectStore already applies to project.json.
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

    public async Task<SequenceDocument> LoadAsync(string path, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No sequence plan file exists at '{path}'.", path);
        }

        await using var stream = File.OpenRead(path);
        try
        {
            var document = await JsonSerializer.DeserializeAsync<SequenceDocument>(stream, cancellationToken: ct).ConfigureAwait(false);
            return document ?? throw new SequenceDocumentCorruptException(path, new InvalidOperationException("Deserialized to null."));
        }
        catch (JsonException ex)
        {
            throw new SequenceDocumentCorruptException(path, ex);
        }
    }
}
