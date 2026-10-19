using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace XlightsAI.Studio.Display;

/// <summary>
/// JSON-file-backed <see cref="IRouteValidationReportStore"/>. Writes are atomic (same-directory temp file +
/// <see cref="File.Move(string, string, bool)"/> replace), the same pattern
/// <see cref="JsonResolvedGesturePreviewStore"/> already uses. Never opens, reads for writing, or otherwise
/// touches any <c>xlights_rgbeffects.xml</c>/show-directory file -- only its own cache directory.
/// </summary>
public sealed class JsonRouteValidationReportStore : IRouteValidationReportStore
{
    private readonly string _directory;

    public JsonRouteValidationReportStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string GetPath(string sourceLayoutHash, Guid pathId, int pathVersion, string? recipeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceLayoutHash);

        var compositeKey = $"{sourceLayoutHash}|{pathId:N}|{pathVersion}|{recipeId ?? "(none)"}";
        var shortHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(compositeKey)))[..16];
        return Path.Combine(_directory, $"route-validation-report-{shortHash}.json");
    }

    public async Task<string> SaveAsync(RouteValidationReport report, string? recipeId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(report);

        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(_directory);
        var finalPath = GetPath(report.SourceLayoutHash, report.PathId, report.PathVersion, recipeId);
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

    public async Task<RouteValidationReport> LoadAsync(string path, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No route validation report exists at '{path}'.", path);
        }

        await using var stream = File.OpenRead(path);
        try
        {
            var report = await JsonSerializer.DeserializeAsync<RouteValidationReport>(stream, cancellationToken: ct).ConfigureAwait(false);
            return report ?? throw new SpatialDisplayGraphCorruptException(path, new InvalidOperationException("Deserialized to null."));
        }
        catch (JsonException ex)
        {
            throw new SpatialDisplayGraphCorruptException(path, ex);
        }
    }
}
