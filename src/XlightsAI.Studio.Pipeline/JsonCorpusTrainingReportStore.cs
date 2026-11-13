using System.Text.Json;

namespace XlightsAI.Studio.Pipeline;

/// <summary>JSON-file-backed persistence for one <see cref="CorpusTrainingReport"/>, one per training run. Atomic writes -- same temp-file-then-<see cref="File.Move(string, string, bool)"/> pattern as <see cref="JsonPatternLibraryStore"/>.</summary>
public sealed class JsonCorpusTrainingReportStore
{
    private const string FileName = "corpus-training-report.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _directory;

    public JsonCorpusTrainingReportStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string Path => System.IO.Path.Combine(_directory, FileName);

    public async Task<string> SaveAsync(CorpusTrainingReport report, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(report);
        Directory.CreateDirectory(_directory);
        var finalPath = Path;
        var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";
        try
        {
            await File.WriteAllTextAsync(tempPath, JsonSerializer.Serialize(report, JsonOptions), ct).ConfigureAwait(false);
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

    public async Task<CorpusTrainingReport> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(Path))
        {
            throw new FileNotFoundException($"No corpus training report exists at '{Path}'.", Path);
        }

        await using var stream = File.OpenRead(Path);
        var report = await JsonSerializer.DeserializeAsync<CorpusTrainingReport>(stream, cancellationToken: ct).ConfigureAwait(false);
        return report ?? throw new InvalidOperationException($"Corpus training report at '{Path}' deserialized to null.");
    }
}
