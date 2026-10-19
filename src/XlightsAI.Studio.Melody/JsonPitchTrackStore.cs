using System.Text.Json;

namespace XlightsAI.Studio.Melody;

/// <summary>
/// JSON-file-backed <see cref="IPitchTrackStore"/>. Writes are atomic (same-directory temp file +
/// <see cref="File.Move(string, string, bool)"/> replace) -- the same pattern
/// <see cref="JsonMelodyTrackStore"/> already uses. Each analysis is written to its own file named
/// deterministically by its <see cref="PitchTrackAnalysis.SourceStemId"/>.
/// </summary>
public sealed class JsonPitchTrackStore : IPitchTrackStore
{
    private readonly string _directory;

    public JsonPitchTrackStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string GetPath(Guid stemId) =>
        Path.Combine(_directory, $"pitch-track-{stemId:N}.json");

    public async Task<string> SaveAsync(PitchTrackAnalysis analysis, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(_directory);
        var finalPath = GetPath(analysis.SourceStemId);
        var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";

        try
        {
            var json = JsonSerializer.Serialize(analysis);
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

    public async Task<PitchTrackAnalysis> LoadAsync(string path, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No pitch-track analysis cache exists at '{path}'.", path);
        }

        await using var stream = File.OpenRead(path);
        try
        {
            var analysis = await JsonSerializer.DeserializeAsync<PitchTrackAnalysis>(stream, cancellationToken: ct).ConfigureAwait(false);
            return analysis ?? throw new PitchTrackAnalysisCorruptException(path, new InvalidOperationException("Deserialized to null."));
        }
        catch (JsonException ex)
        {
            throw new PitchTrackAnalysisCorruptException(path, ex);
        }
    }
}
