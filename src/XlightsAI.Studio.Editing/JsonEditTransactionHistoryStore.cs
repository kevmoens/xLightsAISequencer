using System.Text.Json;

namespace XlightsAI.Studio.Editing;

/// <summary>
/// JSON-file-backed <see cref="IEditTransactionHistoryStore"/>. Writes are atomic (same-directory temp
/// file + <see cref="File.Move(string, string, bool)"/> replace) — the identical pattern
/// <see cref="XlightsAI.Studio.Xlights.JsonSequenceDocumentStore"/> already uses for a
/// <see cref="XlightsAI.Studio.Xlights.SequenceDocument"/>, applied here to that document's own
/// undo/redo stack. Each document's history is written to its own file named by its own id, so
/// concurrent documents never share a temp/final path.
/// </summary>
public sealed class JsonEditTransactionHistoryStore : IEditTransactionHistoryStore
{
    private readonly string _directory;

    public JsonEditTransactionHistoryStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public async Task SaveAsync(Guid documentId, EditTransactionHistorySnapshot snapshot, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(_directory);
        var finalPath = PathFor(documentId);
        var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";

        try
        {
            var json = JsonSerializer.Serialize(snapshot);
            await File.WriteAllTextAsync(tempPath, json, ct).ConfigureAwait(false);

            ct.ThrowIfCancellationRequested();

            File.Move(tempPath, finalPath, overwrite: true);
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

    public async Task<EditTransactionHistorySnapshot?> LoadAsync(Guid documentId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var path = PathFor(documentId);
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<EditTransactionHistorySnapshot>(stream, cancellationToken: ct).ConfigureAwait(false);
    }

    private string PathFor(Guid documentId) => Path.Combine(_directory, $"edit-history-{documentId:N}.json");
}
