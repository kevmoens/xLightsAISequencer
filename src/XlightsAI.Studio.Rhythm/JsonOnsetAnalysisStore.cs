using System.Text.Json;
using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// JSON-file-backed <see cref="IOnsetAnalysisStore"/>. Writes are atomic (same-directory temp file +
/// <see cref="File.Move(string, string, bool)"/> replace) — the same pattern
/// <see cref="JsonEnergyEnvelopeStore"/> already uses. Each analysis is written to its own file named
/// deterministically by its <see cref="OnsetAnalysis.SongId"/>, so re-running
/// <see cref="OnsetDetector.DetectAsync"/> for the same song always resolves to the same path.
/// </summary>
public sealed class JsonOnsetAnalysisStore : IOnsetAnalysisStore
{
    private readonly string _directory;

    public JsonOnsetAnalysisStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string GetPath(SongId songId) =>
        Path.Combine(_directory, $"onset-analysis-{songId.Value:N}.json");

    public async Task<string> SaveAsync(OnsetAnalysis analysis, CancellationToken ct)
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

    public async Task<OnsetAnalysis> LoadAsync(string path, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No onset analysis cache exists at '{path}'.", path);
        }

        await using var stream = File.OpenRead(path);
        try
        {
            var analysis = await JsonSerializer.DeserializeAsync<OnsetAnalysis>(stream, cancellationToken: ct).ConfigureAwait(false);
            return analysis ?? throw new OnsetAnalysisCorruptException(path, new InvalidOperationException("Deserialized to null."));
        }
        catch (JsonException ex)
        {
            throw new OnsetAnalysisCorruptException(path, ex);
        }
    }
}
