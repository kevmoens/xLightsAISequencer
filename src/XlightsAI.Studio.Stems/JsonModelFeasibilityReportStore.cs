using System.Text.Json;

namespace XlightsAI.Studio.Stems;

/// <summary>
/// JSON-file-backed <see cref="IModelFeasibilityReportStore"/>. Writes are atomic (same-directory temp
/// file + <see cref="File.Move(string, string, bool)"/> replace) — the identical pattern
/// <c>JsonBeatGridStore</c> already established in <c>XlightsAI.Studio.Rhythm</c> for Phase 05's own
/// cache/decision artifacts. Large binary stems are explicitly out of scope for this step/store
/// (Section 4: "Large stems go to cache files, not SQLite BLOBs") — this store only ever persists the
/// small, structured feasibility decision document itself.
/// </summary>
public sealed class JsonModelFeasibilityReportStore : IModelFeasibilityReportStore
{
    private readonly string _directory;

    public JsonModelFeasibilityReportStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string GetPath(Guid reportId) =>
        Path.Combine(_directory, $"model-feasibility-{reportId:N}.json");

    public async Task<string> SaveAsync(ModelFeasibilityReport report, CancellationToken ct)
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

    public async Task<ModelFeasibilityReport> LoadAsync(string path, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No model feasibility report exists at '{path}'.", path);
        }

        await using var stream = File.OpenRead(path);
        try
        {
            var report = await JsonSerializer.DeserializeAsync<ModelFeasibilityReport>(stream, cancellationToken: ct)
                .ConfigureAwait(false);
            return report ?? throw new ModelFeasibilityReportCorruptException(path, new InvalidOperationException("Deserialized to null."));
        }
        catch (JsonException ex)
        {
            throw new ModelFeasibilityReportCorruptException(path, ex);
        }
    }
}
