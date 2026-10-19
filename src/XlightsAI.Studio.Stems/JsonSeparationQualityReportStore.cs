using System.Text.Json;

namespace XlightsAI.Studio.Stems;

/// <summary>
/// JSON-file-backed <see cref="ISeparationQualityReportStore"/>. Writes are atomic (same-directory temp
/// file + <see cref="File.Move(string, string, bool)"/> replace) -- the identical pattern
/// <see cref="JsonModelFeasibilityReportStore"/> already established for this phase's own decision
/// artifacts.
/// </summary>
public sealed class JsonSeparationQualityReportStore : ISeparationQualityReportStore
{
    private readonly string _directory;

    public JsonSeparationQualityReportStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string GetPath(Guid reportId) =>
        Path.Combine(_directory, $"separation-quality-{reportId:N}.json");

    public async Task<string> SaveAsync(SeparationQualityReport report, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(report);

        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(_directory);
        var finalPath = GetPath(report.Id);
        var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";

        try
        {
            var json = JsonSerializer.Serialize(report);
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

    public async Task<SeparationQualityReport> LoadAsync(string path, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No separation quality report exists at '{path}'.", path);
        }

        await using var stream = File.OpenRead(path);
        try
        {
            var report = await JsonSerializer.DeserializeAsync<SeparationQualityReport>(stream, cancellationToken: ct)
                .ConfigureAwait(false);
            return report ?? throw new SeparationQualityReportCorruptException(path, new InvalidOperationException("Deserialized to null."));
        }
        catch (JsonException ex)
        {
            throw new SeparationQualityReportCorruptException(path, ex);
        }
    }
}
