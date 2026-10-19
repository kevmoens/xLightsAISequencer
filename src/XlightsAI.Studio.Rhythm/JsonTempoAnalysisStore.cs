using System.Text.Json;
using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// JSON-file-backed <see cref="ITempoAnalysisStore"/>. Writes are atomic (same-directory temp file +
/// <see cref="File.Move(string, string, bool)"/> replace) — the same pattern
/// <see cref="JsonOnsetAnalysisStore"/> already uses. Each analysis is written to its own file named
/// deterministically by its <see cref="TempoAnalysis.SongId"/>, so re-running
/// <see cref="ITempoDetector.DetectAsync"/> for the same song always resolves to the same path.
/// </summary>
public sealed class JsonTempoAnalysisStore : ITempoAnalysisStore
{
    private readonly string _directory;

    public JsonTempoAnalysisStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string GetPath(SongId songId) =>
        Path.Combine(_directory, $"tempo-analysis-{songId.Value:N}.json");

    public async Task<string> SaveAsync(TempoAnalysis analysis, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(_directory);
        var finalPath = GetPath(analysis.SongId);
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

    public async Task<TempoAnalysis> LoadAsync(string path, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No tempo analysis cache exists at '{path}'.", path);
        }

        await using var stream = File.OpenRead(path);
        try
        {
            var analysis = await JsonSerializer.DeserializeAsync<TempoAnalysis>(stream, cancellationToken: ct).ConfigureAwait(false);
            return analysis ?? throw new TempoAnalysisCorruptException(path, new InvalidOperationException("Deserialized to null."));
        }
        catch (JsonException ex)
        {
            throw new TempoAnalysisCorruptException(path, ex);
        }
    }
}
