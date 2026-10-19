using System.Text.Json;

namespace XlightsAI.Studio.Stems;

/// <summary>
/// JSON-file-backed <see cref="IStemActivityResultStore"/>. Writes are atomic (same-directory temp file
/// + <see cref="File.Move(string, string, bool)"/> replace) -- the same pattern
/// <c>XlightsAI.Studio.Rhythm.JsonEnergyEnvelopeStore</c> already uses. Each result is written to its
/// own file named deterministically by its <see cref="StemActivityResult.StemId"/>, so re-running
/// <see cref="StemActivityDetector.DetectAsync"/> for the same stem always resolves to the same path.
/// </summary>
public sealed class JsonStemActivityResultStore : IStemActivityResultStore
{
    private readonly string _directory;

    public JsonStemActivityResultStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string GetPath(Guid stemId) =>
        Path.Combine(_directory, $"stem-activity-{stemId:N}.json");

    public async Task<string> SaveAsync(StemActivityResult result, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(result);

        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(_directory);
        var finalPath = GetPath(result.StemId);
        var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";

        try
        {
            var json = JsonSerializer.Serialize(result);
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

    public async Task<StemActivityResult> LoadAsync(string path, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No stem activity cache exists at '{path}'.", path);
        }

        await using var stream = File.OpenRead(path);
        try
        {
            var result = await JsonSerializer.DeserializeAsync<StemActivityResult>(stream, cancellationToken: ct).ConfigureAwait(false);
            return result ?? throw new StemActivityResultCorruptException(path, new InvalidOperationException("Deserialized to null."));
        }
        catch (JsonException ex)
        {
            throw new StemActivityResultCorruptException(path, ex);
        }
    }
}
